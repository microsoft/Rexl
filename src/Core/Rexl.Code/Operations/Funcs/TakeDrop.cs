// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

using IE = IEnumerable<object>;
using IV = IEnumerable<int>;
using O = Object;
using V = Int32;

public sealed class TakeOneGen : RexlOperationGenerator<TakeOneFunc>
{
    public static readonly TakeOneGen Instance = new TakeOneGen();

    [Flags]
    private enum MethFlags : byte
    {
        None = 0x0,
        Opt = 0x01,
        Pred = 0x02, // Non-indexed.
        Ind = 0x04,
    }

    private readonly Dictionary<MethFlags, MethodInfo> _meths;

    private TakeOneGen()
    {
        _meths = new()
        {
            { MethFlags.None, new Func<IE, O, O>(Exec).Method.GetGenericMethodDefinition() },
            { MethFlags.Opt, new Func<IV, V?, V?>(ExecOpt).Method.GetGenericMethodDefinition() },
            { MethFlags.Pred, new Func<IE, Func<O, bool>, O, ExecCtx, int, O>(Exec).Method.GetGenericMethodDefinition() },
            { MethFlags.Pred | MethFlags.Opt, new Func<IV, Func<V, bool>, V?, ExecCtx, int, V?>(ExecOpt).Method.GetGenericMethodDefinition() },
            { MethFlags.Ind, new Func<IE, Func<long, O, bool>, O, ExecCtx, int, O>(ExecInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Ind | MethFlags.Opt, new Func<IV, Func<long, V, bool>, V?, ExecCtx, int, V?>(ExecIndOpt).Method.GetGenericMethodDefinition() },
        };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType typeRet = call.Type;
        var args = call.Args;
        int carg = args.Length;
        DType typeItem = args[0].Type.ItemTypeOrThis;

        Type stDst = codeGen.GetSystemType(typeRet);
        Type stItem = codeGen.GetSystemType(typeItem);
        bool needOpt = stDst != stItem;
        Validation.Assert(!needOpt || (stDst.IsGenericType && stDst.GetGenericTypeDefinition() == typeof(Nullable<>)));

        bool hasPred = call.Scopes.Length > 0;

        var flags = needOpt ? MethFlags.Opt : MethFlags.None;
        if (hasPred)
        {
            Validation.Assert(call.Indices.Length == 1);
            flags |= call.Indices[0] == null ? MethFlags.Pred : MethFlags.Ind;
        }

        if (!_meths.TryGetValue(flags, out var meth))
        {
            Validation.Assert(false);
            stRet = default;
            return false;
        }
        meth = meth.MakeGenericMethod(stItem);

        if (hasPred)
        {
            stRet = carg < 3 ?
                GenCallDefaultCtxId(codeGen, meth, sts, typeRet, call) :
                GenCallCtxId(codeGen, meth, sts, call);
        }
        else
        {
            stRet = carg < 2 ?
                GenCallDefault(codeGen, meth, sts, typeRet) :
                GenCall(codeGen, meth, sts);
        }
        return true;
    }

    /// <summary>
    /// The code generator uses this for modules.
    /// </summary>
    public static T Exec<T>(IEnumerable<T> src, T def)
    {
        Validation.AssertValueOrNull(src);
        if (src != null)
        {
            using var ator = src.GetEnumerator();
            if (ator.MoveNext())
                return ator.Current;
        }
        return def;
    }

    private static T? ExecOpt<T>(IEnumerable<T> src, T? def)
        where T : struct
    {
        Validation.AssertValueOrNull(src);
        if (src != null)
        {
            using var ator = src.GetEnumerator();
            if (ator.MoveNext())
                return ator.Current;
        }
        return def;
    }

    public static T Exec<T>(IEnumerable<T> src, Func<T, bool> predicate, T def, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src != null)
        {
            using var ator = src.GetEnumerator();
            for (; ; )
            {
                if (!ator.MoveNext())
                    break;
                var item = ator.Current;
                if (predicate(item))
                    return item;
                ctx.Ping(id);
            }
        }
        return def;
    }

    public static T? ExecOpt<T>(IEnumerable<T> src, Func<T, bool> predicate, T? def, ExecCtx ctx, int id)
        where T : struct
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src != null)
        {
            using var ator = src.GetEnumerator();
            for (; ; )
            {
                if (!ator.MoveNext())
                    break;
                var item = ator.Current;
                if (predicate(item))
                    return item;
                ctx.Ping(id);
            }
        }
        return def;
    }

    public static T ExecInd<T>(IEnumerable<T> src, Func<long, T, bool> predicate, T def, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src != null)
        {
            using var ator = src.GetEnumerator();
            long idx = 0;
            for (; ; )
            {
                if (!ator.MoveNext())
                    break;
                var item = ator.Current;
                if (predicate(idx++, item))
                    return item;
                ctx.Ping(id);
            }
        }
        return def;
    }

    public static T? ExecIndOpt<T>(IEnumerable<T> src, Func<long, T, bool> predicate, T? def, ExecCtx ctx, int id)
        where T : struct
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src != null)
        {
            using var ator = src.GetEnumerator();
            long idx = 0;
            for (; ; )
            {
                if (!ator.MoveNext())
                    break;
                var item = ator.Current;
                if (predicate(idx++, item))
                    return item;
                ctx.Ping(id);
            }
        }
        return def;
    }
}

public sealed class TakeDropGen : RexlOperationGenerator<TakeDropFunc>
{
    public static readonly TakeDropGen Instance = new TakeDropGen();

    [Flags]
    private enum MethFlags : byte
    {
        Drop = 0x00,
        Take = 0x01,
        Num = 0x02,
        If = 0x04,
        Wh = 0x08,
        Ind = 0x10,

        ___If___ = If,
        ___Wh___ = Wh,
        ___IfInd = If | Ind,
        ___WhInd = Wh | Ind,

        Num_____ = Num,
        NumIf___ = Num | If,
        NumWh___ = Num | Wh,
        NumIfInd = Num | ___IfInd,
        NumWhInd = Num | ___WhInd,
    }

    private readonly Dictionary<MethFlags, MethodInfo> _meths;

    private TakeDropGen()
    {
        _meths = new()
        {
            { MethFlags.Drop | MethFlags.Num_____, new Func<IE, long, ExecCtx, int, IE>(ExecDropNum).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.NumIf___, new Func<IE, long, Func<O, bool>, ExecCtx, int, IE>(ExecDropNumIf).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.NumWh___, new Func<IE, long, Func<O, bool>, ExecCtx, int, IE>(ExecDropNumWh).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.NumIfInd, new Func<IE, long, Func<long, O, bool>, ExecCtx, int, IE>(ExecDropNumIfInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.NumWhInd, new Func<IE, long, Func<long, O, bool>, ExecCtx, int, IE>(ExecDropNumWhInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.Num_____, new Func<IE, long, IE>(ExecTakeNum).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.NumIf___, new Func<IE, long, Func<O, bool>, ExecCtx, int, IE>(ExecTakeNumIf).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.NumWh___, new Func<IE, long, Func<O, bool>, IE>(ExecTakeNumWh).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.NumIfInd, new Func<IE, long, Func<long, O, bool>, ExecCtx, int, IE>(ExecTakeNumIfInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.NumWhInd, new Func<IE, long, Func<long, O, bool>, IE>(ExecTakeNumWhInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.___If___, new Func<IE, Func<O, bool>, ExecCtx, int, IE>(ExecDropIf).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.___Wh___, new Func<IE, Func<O, bool>, ExecCtx, int, IE>(ExecDropWh).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.___IfInd, new Func<IE, Func<long, O, bool>, ExecCtx, int, IE>(ExecDropIfInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Drop | MethFlags.___WhInd, new Func<IE, Func<long, O, bool>, ExecCtx, int, IE>(ExecDropWhInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.___If___, new Func<IE, Func<O, bool>, ExecCtx, int, IE>(ExecTakeIf).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.___Wh___, new Func<IE, Func<O, bool>, IE>(ExecTakeWh).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.___IfInd, new Func<IE, Func<long, O, bool>, ExecCtx, int, IE>(ExecTakeIfInd).Method.GetGenericMethodDefinition() },
            { MethFlags.Take | MethFlags.___WhInd, new Func<IE, Func<long, O, bool>, IE>(ExecTakeWhInd).Method.GetGenericMethodDefinition() },
        };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        int arity = call.Args.Length;
        DType typeSeq = call.Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        var flags = fn.IsTake ? MethFlags.Take : MethFlags.Drop;

        bool hasPred = call.Scopes.Length > 0;
        if (arity > 2 || !hasPred)
            flags |= MethFlags.Num;

        wrap = default;
        if (hasPred)
        {
            if (call.Indices[0] != null)
                flags |= MethFlags.Ind;

            if (call.Args[arity - 1].HasVolatile)
                wrap = SeqWrapKind.MustCache;
            var dir = call.GetDirective(arity - 1);
            switch (dir)
            {
            default:
                Validation.Assert(dir == default || dir == Directive.If);
                flags |= MethFlags.If;
                break;
            case Directive.While:
                flags |= MethFlags.Wh;
                break;
            }
        }

        if (!_meths.TryGetValue(flags, out var meth))
        {
            Validation.Assert(false);
            stRet = default;
            return false;
        }
        meth = meth.MakeGenericMethod(stItem);

        stRet = NeedsExecCtxCore(call) ?
            GenCallCtxId(codeGen, meth, sts, call) :
            GenCall(codeGen, meth, sts);
        return true;
    }

    public static IEnumerable<T> ExecTakeNumIf<T>(IEnumerable<T> src, long n, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        if (n <= 0)
            return null;
        return ExecTakeNumIfCore(src, n, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecTakeNumIfCore<T>(IEnumerable<T> src, long n, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(val))
            {
                yield return val;
                if (--n <= 0)
                    yield break;
            }
            else
                ctx.Ping(id);
        }
    }

    public static IEnumerable<T> ExecTakeNumIfInd<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        if (n <= 0)
            return null;
        return ExecTakeNumIfIndCore(src, n, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecTakeNumIfIndCore<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (long i = 0; ; i++)
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(i, val))
            {
                yield return val;
                if (--n <= 0)
                    yield break;
            }
            else
                ctx.Ping(id);
        }
    }

    public static IEnumerable<T> ExecTakeNumWh<T>(IEnumerable<T> src, long n, Func<T, bool> predicate)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return null;
        if (n <= 0)
            return null;
        return ExecTakeNumWhCore(src, n, predicate);
    }

    private static IEnumerable<T> ExecTakeNumWhCore<T>(IEnumerable<T> src, long n, Func<T, bool> predicate)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);

        foreach (var item in src)
        {
            if (!predicate(item))
                yield break;
            yield return item;
            if (--n <= 0)
                yield break;
        }
    }

    public static IEnumerable<T> ExecTakeNumWhInd<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return null;
        if (n <= 0)
            return null;
        return ExecTakeNumWhIndCore(src, n, predicate);
    }

    private static IEnumerable<T> ExecTakeNumWhIndCore<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);

        long i = 0;
        foreach (var item in src)
        {
            if (!predicate(i++, item))
                yield break;
            yield return item;
            if (--n <= 0)
                yield break;
        }
    }

    public static IEnumerable<T> ExecTakeNum<T>(IEnumerable<T> src, long n)
    {
        Validation.AssertValueOrNull(src);
        if (src == null)
            return null;
        if (n <= 0)
            return null;

        if (src is ICollection<T> col)
        {
            if (col.Count <= n)
                return src;
            // REVIEW: When src is an array, should we just create a new array?
            return new TakeImplKnown<T>(src, n);
        }
        if (src is ICanCount can && can.TryGetCount(out long count))
        {
            if (count <= n)
                return src;
            return new TakeImplKnown<T>(src, n);
        }

        return new TakeImplGen<T>(src, n);
    }

    /// <summary>
    /// This version knows the final length, since the source sequence is strictly longer.
    /// This assumes that the underlying enumerable is caching, so this doesn't need to be,
    /// since it does no real work - just shortens the sequence.
    /// </summary>
    private sealed class TakeImplKnown<T> : ICachingEnumerable<T>, ICanCount
    {
        private readonly IEnumerable<T> _src;
        private readonly long _num;

        public TakeImplKnown(IEnumerable<T> src, long n)
        {
            Validation.AssertValue(src);
            Validation.Assert(n > 0);
            _src = src;
            _num = n;
        }

        public IEnumerator<T> GetEnumerator()
        {
            long count = _num;
            foreach (var item in _src)
            {
                yield return item;
                if (--count <= 0)
                    break;
            }
            Validation.Assert(count == 0);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetCount(out long count)
        {
            count = _num;
            return true;
        }

        public long GetCount(Action callback)
        {
            Validation.BugCheckValueOrNull(callback);
            return _num;
        }
    }

    /// <summary>
    /// This version doesn't know the final length a priori.
    /// This assumes that the underlying enumerable is caching, so this doesn't need to be,
    /// since it does no real work - just shortens the sequence.
    /// </summary>
    private sealed class TakeImplGen<T> : ICachingEnumerable<T>, ICanCount
    {
        private readonly IEnumerable<T> _src;
        private readonly long _max;

        // If this is bigger than _max, then we don't yet know the actual size,
        // otherwise, it is the actual size.
        private long _num;

        public TakeImplGen(IEnumerable<T> src, long n)
        {
            Validation.AssertValue(src);
            Validation.Assert(n > 0);
            _src = src;
            if (n == long.MaxValue)
                n--;
            _max = n;
            _num = n + 1;
            Validation.Assert(_num > _max);
        }

        public IEnumerator<T> GetEnumerator()
        {
            long max = _max;
            long num = _num;
            long count = 0;
            foreach (var item in _src)
            {
                yield return item;
                if (++count >= max)
                    break;
            }
            Validation.Assert(count <= max);

            // Update the size, if needed.
            if (num > count)
            {
                Validation.Assert(num > max);
                Interlocked.CompareExchange(ref _num, count, num);
            }
            Validation.Assert(_num == count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetCount(out long count)
        {
            count = _num;
            if (count <= _max)
                return true;
            count = -1;
            return false;
        }

        public long GetCount(Action callback)
        {
            Validation.BugCheckValueOrNull(callback);
            long num = _num;
            if (num <= _max)
                return num;

            if (_src is ICanCount can)
            {
                long count = can.GetCount(callback);
                if (count > _max)
                    count = _max;
                Interlocked.CompareExchange(ref _num, count, num);
                Validation.Assert(_num == count);
                return _num;
            }

            // Iterate through to discover the length.
            foreach (var _ in this)
            {
                if (callback != null)
                    callback();
            }

            Validation.Assert(_num <= _max);
            return _num;
        }
    }

    public static IEnumerable<T> ExecDropNum<T>(IEnumerable<T> src, long n, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);

        if (src == null || n <= 0)
            return src;
        // REVIEW: When src is an array, should we just create a new array?
        return CodeGenUtil.WrapWithCounter(src, -n, ExecDropNumCore(src, n, ctx, id));
    }

    private static IEnumerable<T> ExecDropNumCore<T>(IEnumerable<T> src, long n, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(ctx);

        foreach (var item in src)
        {
            if (--n >= 0)
                ctx.Ping(id);
            else
                yield return item;
        }
    }

    public static IEnumerable<T> ExecDropNumIf<T>(IEnumerable<T> src, long n, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        if (n <= 0)
            return src;
        return ExecDropNumIfCore(src, n, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropNumIfCore<T>(IEnumerable<T> src, long n, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(val))
            {
                ctx.Ping(id);
                if (--n <= 0)
                    break;
            }
            else
                yield return val;
        }
        Validation.Assert(n == 0);

        // One last ping.
        ctx.Ping(id);

        // Yield the rest.
        while (ator.MoveNext())
            yield return ator.Current;
    }

    public static IEnumerable<T> ExecDropNumIfInd<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        if (n <= 0)
            return src;
        return ExecDropNumIfIndCore(src, n, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropNumIfIndCore<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (long i = 0; ; i++)
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(i, val))
            {
                ctx.Ping(id);
                if (--n <= 0)
                    break;
            }
            else
                yield return val;
        }
        Validation.Assert(n == 0);

        // One last ping.
        ctx.Ping(id);

        // Yield the rest.
        while (ator.MoveNext())
            yield return ator.Current;
    }

    public static IEnumerable<T> ExecDropNumWh<T>(IEnumerable<T> src, long n, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        if (n <= 0)
            return src;
        return ExecDropNumWhCore(src, n, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropNumWhCore<T>(IEnumerable<T> src, long n, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(val))
            {
                ctx.Ping(id);
                if (--n <= 0)
                    break;
            }
            else
            {
                yield return val;
                break;
            }
        }

        // One last ping.
        ctx.Ping(id);

        // Yield the rest.
        while (ator.MoveNext())
            yield return ator.Current;
    }

    public static IEnumerable<T> ExecDropNumWhInd<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        if (n <= 0)
            return src;
        return ExecDropNumWhIndCore(src, n, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropNumWhIndCore<T>(IEnumerable<T> src, long n, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.Assert(n > 0);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (long i = 0; ; i++)
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(i, val))
            {
                ctx.Ping(id);
                if (--n <= 0)
                    break;
            }
            else
            {
                yield return val;
                break;
            }
        }
        // One last ping.
        ctx.Ping(id);

        // Yield the rest.
        while (ator.MoveNext())
            yield return ator.Current;
    }

    public static IEnumerable<T> ExecTakeIf<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        return ExecTakeIfCore(src, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecTakeIfCore<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(val))
                yield return val;
            else
                ctx.Ping(id);
        }
    }

    public static IEnumerable<T> ExecTakeIfInd<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        return ExecTakeIfIndCore(src, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecTakeIfIndCore<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (long i = 0; ; i++)
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (predicate(i, val))
                yield return val;
            else
                ctx.Ping(id);
        }
    }

    public static IEnumerable<T> ExecDropIf<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        return ExecDropIfCore(src, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropIfCore<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (!predicate(val))
                yield return val;
            else
                ctx.Ping(id);
        }
    }

    public static IEnumerable<T> ExecDropIfInd<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        return ExecDropIfIndCore(src, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropIfIndCore<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (long i = 0; ; i++)
        {
            if (!ator.MoveNext())
                yield break;
            var val = ator.Current;
            if (!predicate(i, val))
                yield return val;
            else
                ctx.Ping(id);
        }
    }

    public static IEnumerable<T> ExecTakeWh<T>(IEnumerable<T> src, Func<T, bool> predicate)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return null;
        return ExecTakeWhCore(src, predicate);
    }

    private static IEnumerable<T> ExecTakeWhCore<T>(IEnumerable<T> src, Func<T, bool> predicate)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);

        foreach (var item in src)
        {
            if (!predicate(item))
                yield break;
            yield return item;
        }
    }

    public static IEnumerable<T> ExecTakeWhInd<T>(IEnumerable<T> src, Func<long, T, bool> predicate)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return null;
        return ExecTakeWhIndCore(src, predicate);
    }

    private static IEnumerable<T> ExecTakeWhIndCore<T>(IEnumerable<T> src, Func<long, T, bool> predicate)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);

        long i = 0;
        foreach (var item in src)
        {
            if (!predicate(i++, item))
                yield break;
            yield return item;
        }
    }

    public static IEnumerable<T> ExecDropWh<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        return ExecDropWhCore(src, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropWhCore<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);

        using var ator = src.GetEnumerator();
        do
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                yield break;
        } while (predicate(ator.Current));

        do
        {
            yield return ator.Current;
        } while (ator.MoveNext());
    }

    public static IEnumerable<T> ExecDropWhInd<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        Validation.AssertValue(ctx);

        if (src == null)
            return null;
        return ExecDropWhIndCore(src, predicate, ctx, id);
    }

    private static IEnumerable<T> ExecDropWhIndCore<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(predicate);

        long i = 0;
        using var ator = src.GetEnumerator();
        do
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                yield break;
        } while (predicate(i++, ator.Current));

        do
        {
            yield return ator.Current;
        } while (ator.MoveNext());
    }
}
