// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

using IE = IEnumerable<object>;
using IV = IEnumerable<ushort>;
using O = Object;
using V = UInt16;

public sealed class DistinctGen : RexlOperationGenerator<DistinctFunc>
{
    public static readonly DistinctGen Instance = new DistinctGen();

    private readonly MethodInfo _meth1;
    private readonly MethodInfo _meth2;

    private DistinctGen()
    {
        _meth1 = new Func<IE, IEqualityComparer<O>, ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
        _meth2 = new Func<IE, Func<O, O>, IEqualityComparer<O>, ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType typeSeq = call.Type;
        Validation.Assert(call.Args[0].Type == typeSeq);
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        DType typeKey;
        Type stKey;
        MethodInfo meth;
        if (sts.Length == 1)
        {
            typeKey = typeItem;
            stKey = stItem;
            meth = _meth1.MakeGenericMethod(stItem);
        }
        else
        {
            typeKey = call.Args[1].Type;
            Validation.Assert(typeKey.IsEquatable);
            stKey = codeGen.GetSystemType(typeKey);
            meth = _meth2.MakeGenericMethod(stItem, stKey);
        }

        int carg = sts.Length;
        var parms = meth.GetParameters();
        Validation.Assert(parms.Length == carg + 3);
        Validation.Assert(parms[carg].ParameterType == typeof(IEqualityComparer<>).MakeGenericType(stKey));

        bool ci = call.Directives.GetItemOrDefault(sts.Length - 1).IsCi();
        if (codeGen.GenLoadEqCmpOrNull(typeKey, ti: false, ci: ci, out Type stEq, out Type stAgg))
        {
            Validation.Assert(stAgg == stKey);
            Validation.Assert(parms[carg].ParameterType.IsAssignableFrom(stEq));
        }

        var stsFull = new Type[carg + 1];
        sts.Copy(0, carg, stsFull, 0);
        stsFull[carg] = parms[carg].ParameterType;

        stRet = GenCallCtxId(codeGen, meth, stsFull, call);
        return true;
    }

    private static IEnumerable<T> Exec<T>(IEnumerable<T> src, IEqualityComparer<T> eq, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValueOrNull(eq);
        Validation.AssertValue(ctx);
        if (src == null)
            return null;
        return ExecCore(src, eq, ctx, id);
    }

    private static IEnumerable<T> ExecCore<T>(IEnumerable<T> src, IEqualityComparer<T> eq, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValueOrNull(eq);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        ctx.Ping(id);
        if (!ator.MoveNext())
            yield break;

        // Process the first item. Note that HashSet<T> handles null values without complaint.
        var items = new HashSet<T>(eq);
        var item = ator.Current;
        items.Add(item).Verify();
        yield return item;

        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                yield break;
            item = ator.Current;
            if (items.Add(item))
                yield return item;
        }
    }

    private static IEnumerable<T> Exec<T, K>(IEnumerable<T> src, Func<T, K> sel, IEqualityComparer<K> eq,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValueOrNull(eq);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (src == null)
            return null;
        return ExecCore(src, sel, eq, ctx, id);
    }

    private static IEnumerable<T> ExecCore<T, K>(IEnumerable<T> src, Func<T, K> sel, IEqualityComparer<K> eq,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValueOrNull(eq);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        ctx.Ping(id);
        if (!ator.MoveNext())
            yield break;

        // Process the first item. Note that HashSet<K> handles null values without complaint.
        var items = new HashSet<K>(eq);
        var item = ator.Current;
        items.Add(sel(item)).Verify();
        yield return item;

        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                yield break;
            item = ator.Current;
            if (items.Add(sel(item)))
                yield return item;
        }
    }
}

public sealed partial class ChainMapGen : RexlOperationGenerator<ChainMapFunc>
{
    public static readonly ChainMapGen Instance = new ChainMapGen();

    private readonly Immutable.Array<MethodInfo> _cseqToMeths;
    private readonly Immutable.Array<MethodInfo> _cseqToMethsIndexed;

    private ChainMapGen()
    {
        _cseqToMeths = GetExecs();
        _cseqToMethsIndexed = GetExecInds();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType typeSeq = call.Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        bool hasVolatile = false;
        MethodInfo meth;
        if (call.Args.Length == 1)
        {
            Validation.Assert(call.Scopes.Length == 0);

            meth = _cseqToMeths[0];
            Validation.AssertValue(meth);
            meth = meth.MakeGenericMethod(stItem);
        }
        else
        {
            Validation.Assert(call.Indices.Length == 1);

            // REVIEW: Consider composing as ForEach(..., sel)->ChainMap() at fold time.
            int cseq = call.Args.Length - 1;
            var stsItem = new Type[call.Args.Length];
            for (int i = 0; i < cseq; i++)
                stsItem[i] = codeGen.GetSystemType(call.Args[i].Type.ItemTypeOrThis);

            hasVolatile = call.Args[cseq].HasVolatile;
            Validation.Assert(call.Args[cseq].Type == typeSeq);
            stsItem[cseq] = stItem;

            bool indexed = call.Indices[0] != null;
            var cseqToMeths = indexed ? _cseqToMethsIndexed : _cseqToMeths;

            if (cseq >= cseqToMeths.Length)
            {
                stRet = null;
                wrap = default;
                return false;
            }

            meth = cseqToMeths[cseq];
            Validation.AssertValue(meth);
            meth = meth.MakeGenericMethod(stsItem);
        }

        Validation.Assert(meth.ReturnType == codeGen.GetSystemType(call.Type));
        stRet = GenCallCtxId(codeGen, meth, sts, call);
        wrap = hasVolatile ? SeqWrapKind.MustCache : default;
        return true;
    }

    // This contains the non-predicate form.
    // All other forms with predicates are generated in the "main" partial.
    partial class Execs
    {
        public static IEnumerable<T> Exec<T>(IEnumerable<IEnumerable<T>> src, ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(ctx);

            if (src != null)
            {
                using var e = src.GetEnumerator();
                for (; ; )
                {
                    ctx.Ping(id); if (!e.MoveNext()) yield break;
                    var seq = e.Current;
                    if (seq == null)
                        continue;
                    foreach (var item in seq)
                        yield return item;
                }
            }
        }
    }
}

public sealed class TakeAtGen : RexlOperationGenerator<TakeAtFunc>
{
    public static readonly TakeAtGen Instance = new TakeAtGen();

    private readonly MethodInfo _meth;
    private readonly MethodInfo _methOpt;

    private TakeAtGen()
    {
        _meth = new Func<IE, long, O, ExecCtx, int, O>(Exec).Method.GetGenericMethodDefinition();
        _methOpt = new Func<IV, long, V?, ExecCtx, int, V?>(ExecOpt).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        // REVIEW: Pushing the default, index, ExecCtx, and id args could be deferred
        // if the null check on src were inlined.

        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType typeRet = call.Type;
        var args = call.Args;
        int carg = call.Args.Length;
        DType typeSeq = args[0].Type;
        Validation.Assert(typeSeq.IsSequence);
        DType typeItem = typeSeq.ItemTypeOrThis;
        Validation.Assert(typeRet == typeItem || typeRet == typeItem.ToOpt());
        Validation.Assert(carg != 2 || typeRet == typeItem);

        Type stDst = codeGen.GetSystemType(typeRet);
        Type stItem = codeGen.GetSystemType(typeItem);
        bool needOpt = stDst != stItem;
        Validation.Assert(!needOpt || (stDst.IsGenericType && stDst.GetGenericTypeDefinition() == typeof(Nullable<>)));

        MethodInfo meth = (needOpt ? _methOpt : _meth).MakeGenericMethod(stItem);

        // REVIEW: Split out pings for each case by ID?
        if (call.Args.Length == 2)
            stRet = GenCallDefaultCtxId(codeGen, meth, sts, typeRet, call);
        else
            stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Exec<T>(IEnumerable<T> src, long index, T def, ExecCtx ctx, int id)
    {
        if (src == null)
            return def;
        if (TryExecCore(src, index, ctx, id, out T res))
            return res;
        return def;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T? ExecOpt<T>(IEnumerable<T> src, long index, T? def, ExecCtx ctx, int id)
        where T : struct
    {
        if (src == null)
            return def;
        if (TryExecCore(src, index, ctx, id, out T res))
            return res;
        return def;
    }

    private static bool TryExecCore<T>(IEnumerable<T> src, long index, ExecCtx ctx, int id, out T res)
    {
        Validation.AssertValue(src);

        if (src is T[] arr)
        {
            if (TryNormIndex(ref index, arr.Length))
            {
                res = arr[index];
                return true;
            }
            res = default;
            return false;
        }

        if (src is IList<T> list)
        {
            if (TryNormIndex(ref index, list.Count))
            {
                res = list[(int)index];
                return true;
            }
            res = default;
            return false;
        }

        if (src is ICanCount can)
        {
            // Note if index >= 0 and src does not implement ICursorable,
            // it's still worthwhile to see if we can easily determine if
            // index >= count to avoid enumerating through everything.

            bool haveCount = can.TryGetCount(out long count);
            if (!haveCount && index < 0)
            {
                count = can.GetCount(() => ctx.Ping(id));
                haveCount = true;
            }

            if (haveCount && !TryNormIndex(ref index, count))
            {
                res = default;
                return false;
            }
        }
        else if (src is ICollection<T> coll)
        {
            if (!TryNormIndex(ref index, coll.Count))
            {
                res = default;
                return false;
            }
        }

        if (index >= 0 && src is ICursorable<T> cursorable)
        {
            using var cursor = cursorable.GetCursor();
            if (!cursor.MoveTo(index))
            {
                res = default;
                return false;
            }
            res = cursor.Value;
            return true;
        }

        using var ator = src.GetEnumerator();
        if (index >= 0)
        {
            do
            {
                ctx.Ping(id);
                if (!ator.MoveNext())
                {
                    res = default;
                    return false;
                }
            }
            while (--index >= 0);
            res = ator.Current;
            return true;
        }

        Validation.Assert(index < 0);
        long lookback = -index;
        var buf = new T[lookback];
        long i = 0;
        bool filled = false;
        while (true)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            buf[i++] = ator.Current;
            if (i == lookback)
            {
                i = 0;
                filled = true;
            }
        }

        if (filled)
        {
            res = buf[i];
            return true;
        }
        res = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryNormIndex(ref long index, long count)
    {
        if (index >= count)
            return false;
        if (index < 0)
        {
            index += count;
            if (index < 0)
                return false;
        }

        Validation.AssertIndex(index, count);
        return true;
    }
}

public sealed class RepeatGen : GetMethGen<RepeatFunc>
{
    public static readonly RepeatGen Instance = new RepeatGen();

    private readonly MethodInfo _meth;

    private RepeatGen()
    {
        _meth = new Func<O, long, IE>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        Type stItem = codeGen.GetSystemType(call.Type.ItemTypeOrThis);
        meth = _meth.MakeGenericMethod(stItem);
        return true;
    }

    public static RepeatSequence<T> Exec<T>(T value, long n)
    {
        if (n <= 0)
            return null;
        return new RepeatSequence<T>(value, n);
    }
}

public sealed class CountGen : RexlOperationGenerator<CountFunc>
{
    public static readonly CountGen Instance = new CountGen();

    private readonly MethodInfo _meth;
    private readonly MethodInfo _methIf;
    private readonly MethodInfo _methIfInd;
    private readonly MethodInfo _methWhile;
    private readonly MethodInfo _methWhileInd;

    private CountGen()
    {
        _meth = new Func<IE, ExecCtx, int, long>(Exec).Method.GetGenericMethodDefinition();
        _methIf = new Func<IE, Func<O, bool>, ExecCtx, int, long>(ExecIf).Method.GetGenericMethodDefinition();
        _methIfInd = new Func<IE, Func<long, O, bool>, ExecCtx, int, long>(ExecIndIf).Method.GetGenericMethodDefinition();
        _methWhile = new Func<IE, Func<O, bool>, ExecCtx, int, long>(ExecWhile).Method.GetGenericMethodDefinition();
        _methWhileInd = new Func<IE, Func<long, O, bool>, ExecCtx, int, long>(ExecIndWhile).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        // Get the source system type.
        DType typeSeq = call.Args[0].Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);
        var carg = call.Args.Length;
        MethodInfo meth;

        if (carg == 1)
            meth = _meth;
        else
        {
            Validation.Assert(call.Indices.Length == 1);
            Validation.BugCheck(typeItem == call.Scopes[0].Type);

            bool indexed = call.Indices[0] != null;
            if (call.GetDirective(1) != Directive.While)
                meth = indexed ? _methIfInd : _methIf;
            else
                meth = indexed ? _methWhileInd : _methWhile;
        }

        meth = meth.MakeGenericMethod(stItem);
        stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    public static long Exec<T>(IEnumerable<T> src, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);

        if (src == null)
            return 0;
        if (src is ICollection<T> col)
            return col.Count;

        long count;
        if (src is ICanCount can)
        {
            // First call TryGetCount to avoid creating the ping delegate.
            if (can.TryGetCount(out count))
                return count;
            return can.GetCount(() => ctx.Ping(id));
        }

        using var e = src.GetEnumerator();
        for (count = 0; ;)
        {
            ctx.Ping(id);
            if (!e.MoveNext())
                return count;
            count++;
        }
    }

    public static long ExecIf<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return 0;

        using var e = src.GetEnumerator();
        long count = 0;
        for (; ; )
        {
            ctx.Ping(id);
            if (!e.MoveNext())
                return count;
            if (predicate(e.Current))
                count++;
        }
    }

    public static long ExecWhile<T>(IEnumerable<T> src, Func<T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return 0;

        using var e = src.GetEnumerator();
        long count = 0;
        for (; ; )
        {
            ctx.Ping(id);
            if (!e.MoveNext())
                return count;
            if (!predicate(e.Current))
                return count;
            count++;
        }
    }

    public static long ExecIndIf<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return 0;

        using var e = src.GetEnumerator();
        long count = 0;
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e.MoveNext())
                return count;
            if (predicate(idx, e.Current))
                count++;
        }
    }

    public static long ExecIndWhile<T>(IEnumerable<T> src, Func<long, T, bool> predicate, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);

        if (src == null)
            return 0;

        using var e = src.GetEnumerator();
        long count = 0;
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e.MoveNext())
                return count;
            if (!predicate(idx, e.Current))
                return count;
            count++;
        }
    }
}

public sealed class AnyAllGen : RexlOperationGenerator<AnyAllFunc>
{
    public static readonly AnyAllGen Instance = new AnyAllGen();

    private enum PredMethFlags : byte
    {
        None = 0x00,
        Opt = 0x01,
        Any = 0x02,
        Ind = 0x04,
    }

    private readonly MethodInfo _methAny;
    private readonly MethodInfo _methAll;
    private readonly MethodInfo _methAnyOpt;
    private readonly MethodInfo _methAllOpt;

    private readonly ReadOnly.Dictionary<PredMethFlags, MethodInfo> _methsPred;

    private AnyAllGen()
    {
        _methAny = new Func<IEnumerable<bool>, ExecCtx, int, bool>(ExecAny).Method;
        _methAll = new Func<IEnumerable<bool>, ExecCtx, int, bool>(ExecAll).Method;
        _methAnyOpt = new Func<IEnumerable<bool?>, ExecCtx, int, bool?>(ExecAny).Method;
        _methAllOpt = new Func<IEnumerable<bool?>, ExecCtx, int, bool?>(ExecAll).Method;

        _methsPred = new Dictionary<PredMethFlags, MethodInfo>()
        {
            { PredMethFlags.None, new Func<IE, Func<O, bool>, ExecCtx, int, bool>(ExecAll).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Opt, new Func<IE, Func<O, bool?>, ExecCtx, int, bool?>(ExecAll).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Any, new Func<IE, Func<O, bool>, ExecCtx, int, bool>(ExecAny).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Any | PredMethFlags.Opt, new Func<IE, Func<O, bool?>, ExecCtx, int, bool?>(ExecAny).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Ind, new Func<IE, Func<long, O, bool>, ExecCtx, int, bool>(ExecIndAll).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Ind | PredMethFlags.Opt, new Func<IE, Func<long, O, bool?>, ExecCtx, int, bool?>(ExecIndAll).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Ind | PredMethFlags.Any, new Func<IE, Func<long, O, bool>, ExecCtx, int, bool>(ExecIndAny).Method.GetGenericMethodDefinition() },
            { PredMethFlags.Ind | PredMethFlags.Any | PredMethFlags.Opt, new Func<IE, Func<long, O, bool?>, ExecCtx, int, bool?>(ExecIndAny).Method.GetGenericMethodDefinition() },
        };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        DType typeRet = call.Type;

        MethodInfo meth;
        if (call.Args.Length == 1)
        {
            if (typeRet.IsOpt)
                meth = fn.IsAny ? _methAnyOpt : _methAllOpt;
            else
                meth = fn.IsAny ? _methAny : _methAll;
        }
        else
        {
            Validation.Assert(call.Args.Length == 2);
            Validation.Assert(call.Indices.Length == 1);

            var flags = fn.IsAny ? PredMethFlags.Any : PredMethFlags.None;
            if (typeRet.IsOpt)
                flags |= PredMethFlags.Opt;
            if (call.Indices[0] != null)
                flags |= PredMethFlags.Ind;
            Validation.Assert(_methsPred.ContainsKey(flags));

            DType typeItem = call.Args[0].Type.ItemTypeOrThis;
            Type stItem = codeGen.GetSystemType(typeItem);

            meth = _methsPred[flags].MakeGenericMethod(stItem);
        }

        stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    public static bool ExecAny(IEnumerable<bool> src, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            if (ator.Current)
                return true;
        }
        return false;
    }

    public static bool ExecAny<T>(IEnumerable<T> src, Func<T, bool> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            if (prd(ator.Current))
                return true;
        }
        return false;
    }

    public static bool ExecIndAny<T>(IEnumerable<T> src, Func<long, T, bool> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        using var ator = src.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            if (prd(idx, ator.Current))
                return true;
        }
        return false;
    }

    public static bool? ExecAny(IEnumerable<bool?> src, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        bool hasNull = false;
        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var item = ator.Current;
            if (item.GetValueOrDefault())
                return item;
            if (!hasNull && !item.HasValue)
                hasNull = true;
        }
        return hasNull ? (bool?)null : false;
    }

    public static bool? ExecAny<T>(IEnumerable<T> src, Func<T, bool?> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        bool hasNull = false;
        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var item = prd(ator.Current);
            if (item.GetValueOrDefault())
                return item;
            if (!hasNull && !item.HasValue)
                hasNull = true;
        }
        return hasNull ? (bool?)null : false;
    }

    public static bool? ExecIndAny<T>(IEnumerable<T> src, Func<long, T, bool?> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        bool hasNull = false;
        using var ator = src.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var item = prd(idx, ator.Current);
            if (item.GetValueOrDefault())
                return item;
            if (!hasNull && !item.HasValue)
                hasNull = true;
        }
        return hasNull ? (bool?)null : false;
    }

    public static bool ExecAll(IEnumerable<bool> src, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);

        if (src == null)
            return true;

        foreach (var item in src)
        {
            if (!item)
                return item;
        }

        return true;
    }

    public static bool ExecAll<T>(IEnumerable<T> src, Func<T, bool> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return true;

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            if (!prd(ator.Current))
                return false;
        }
        return true;
    }

    public static bool ExecIndAll<T>(IEnumerable<T> src, Func<long, T, bool> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return true;

        using var ator = src.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            if (!prd(idx, ator.Current))
                return false;
        }
        return true;
    }

    public static bool? ExecAll(IEnumerable<bool?> src, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);

        if (src == null)
            return true;

        bool hasNull = false;
        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var item = ator.Current;
            if (!item.GetValueOrDefault())
            {
                if (item.HasValue)
                    return item;
                hasNull = true;
            }
        }
        return hasNull ? (bool?)null : true;
    }

    public static bool? ExecAll<T>(IEnumerable<T> src, Func<T, bool?> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return true;

        bool hasNull = false;
        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var item = prd(ator.Current);
            if (!item.GetValueOrDefault())
            {
                if (item.HasValue)
                    return item;
                hasNull = true;
            }
        }
        return hasNull ? (bool?)null : true;
    }

    public static bool? ExecIndAll<T>(IEnumerable<T> src, Func<long, T, bool?> prd, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(prd);
        Validation.AssertValue(ctx);

        if (src == null)
            return true;

        bool hasNull = false;
        using var ator = src.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var item = prd(idx, ator.Current);
            if (!item.GetValueOrDefault())
            {
                if (item.HasValue)
                    return item;
                hasNull = true;
            }
        }
        return hasNull ? (bool?)null : true;
    }
}

public sealed class MakePairsGen : GetMethGen<MakePairsFunc>
{
    public static readonly MakePairsGen Instance = new MakePairsGen();

    private readonly MethodInfo _meth;

    private MakePairsGen()
    {
        _meth = new Func<IE, IEnumerable<O[]>>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        DType typeItem = call.Args[0].Type.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        meth = _meth.MakeGenericMethod(stItem);
        return true;
    }

    public static IEnumerable<T[]> Exec<T>(IEnumerable<T> items)
    {
        if (items == null)
            return null;
        return ExecCore(items as T[] ?? items.ToArray());
    }

    private static IEnumerable<T[]> ExecCore<T>(T[] items)
    {
        Validation.AssertValue(items);

        // Construct all pairs.
        int count = items.Length;
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
                yield return new[] { items[i], items[j] };
        }
    }
}

public sealed class FoldGen : RexlOperationGenerator<FoldFunc>
{
    public static readonly FoldGen Instance = new FoldGen();

    private readonly MethodInfo _meth3;
    private readonly MethodInfo _methInd3;
    private readonly MethodInfo _meth4;
    private readonly MethodInfo _methInd4;

    private FoldGen()
    {
        _meth3 = new Func<IE, O, Func<O, O, O>, ExecCtx, int, O>(Exec).Method.GetGenericMethodDefinition();
        _methInd3 = new Func<IE, O, Func<long, O, O, O>, ExecCtx, int, O>(ExecInd).Method.GetGenericMethodDefinition();
        _meth4 = new Func<IE, O, Func<O, O, O>, Func<O, O>, ExecCtx, int, O>(ExecRes).Method.GetGenericMethodDefinition();
        _methInd4 = new Func<IE, O, Func<long, O, O, O>, Func<O, O>, ExecCtx, int, O>(ExecResInd).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(call.Indices.Length == 1);

        DType typeSeq = call.Args[0].Type;
        DType typeItemSrc = typeSeq.ItemTypeOrThis;
        Type stItemSrc = codeGen.GetSystemType(typeItemSrc);
        DType typeIter = call.Args[1].Type;
        Type stIter = codeGen.GetSystemType(typeIter);
        DType typeDst = call.Type;
        Type stDst = codeGen.GetSystemType(typeDst);
        bool indexed = call.Indices[0] != null;

        MethodInfo meth;
        if (call.Args.Length == 3)
        {
            Validation.Assert(typeDst == typeIter);
            meth = (indexed ? _methInd3 : _meth3).MakeGenericMethod(stItemSrc, stIter);
        }
        else
        {
            Validation.Assert(typeDst == call.Args[3].Type);
            meth = (indexed ? _methInd4 : _meth4).MakeGenericMethod(stItemSrc, stIter, stDst);
        }

        stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    public static TIter Exec<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);
        if (src == null)
            return init;
        return ExecCore(src, init, fn, ctx, id);
    }

    private static TIter ExecCore<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);

        ctx.Ping(id);
        TIter cur = init;
        foreach (var item in src)
        {
            cur = fn(item, cur);
            ctx.Ping(id);
        }
        return cur;
    }

    public static TIter ExecInd<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);
        if (src == null)
            return init;
        return ExecIndCore(src, init, fn, ctx, id);
    }

    private static TIter ExecIndCore<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);

        ctx.Ping(id);
        TIter cur = init;
        long idx = 0;
        foreach (var item in src)
        {
            cur = fn(idx++, item, cur);
            ctx.Ping(id);
        }
        return cur;
    }

    public static TDst ExecRes<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);
        Validation.AssertValue(ctx);
        if (src == null)
            return fnRes(init);
        return ExecResCore(src, init, fn, fnRes, ctx, id);
    }

    private static TDst ExecResCore<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);
        Validation.AssertValue(ctx);

        ctx.Ping(id);
        TIter cur = init;
        foreach (var item in src)
        {
            cur = fn(item, cur);
            ctx.Ping(id);
        }
        return fnRes(cur);
    }

    public static TDst ExecResInd<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);
        Validation.AssertValue(ctx);
        if (src == null)
            return fnRes(init);
        return ExecIndResCore(src, init, fn, fnRes, ctx, id);
    }

    private static TDst ExecIndResCore<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);
        Validation.AssertValue(ctx);

        ctx.Ping(id);
        TIter cur = init;
        long idx = 0;
        foreach (var item in src)
        {
            cur = fn(idx++, item, cur);
            ctx.Ping(id);
        }
        return fnRes(cur);
    }
}

public sealed class ScanGen : RexlOperationGenerator<ScanFunc>
{
    public static readonly ScanGen Instance = new ScanGen();

    private readonly MethodInfo _meth3;
    private readonly MethodInfo _methZ3;
    private readonly MethodInfo _methInd3;
    private readonly MethodInfo _methIndZ3;
    private readonly MethodInfo _meth4;
    private readonly MethodInfo _methZ4;
    private readonly MethodInfo _methInd4;
    private readonly MethodInfo _methIndZ4;

    private ScanGen()
    {
        _meth3 = new Func<IE, O, Func<O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        _methZ3 = new Func<IE, O, Func<O, O, O>, IE>(ExecZ).Method.GetGenericMethodDefinition();
        _methInd3 = new Func<IE, O, Func<long, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        _methIndZ3 = new Func<IE, O, Func<long, O, O, O>, IE>(ExecIndZ).Method.GetGenericMethodDefinition();
        _meth4 = new Func<IE, O, Func<O, O, O>, Func<O, O>, IE>(ExecRes).Method.GetGenericMethodDefinition();
        _methZ4 = new Func<IE, O, Func<O, O, O>, Func<O, O, O>, IE>(ExecResZ).Method.GetGenericMethodDefinition();
        _methInd4 = new Func<IE, O, Func<long, O, O, O>, Func<O, O>, IE>(ExecResInd).Method.GetGenericMethodDefinition();
        _methIndZ4 = new Func<IE, O, Func<long, O, O, O>, Func<long, O, O, O>, IE>(ExecResIndZ).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(call.Indices.Length == 1);

        var fn = GetOper(call);

        DType typeSeq = call.Args[0].Type;
        DType typeItemSrc = typeSeq.ItemTypeOrThis;
        Type stItemSrc = codeGen.GetSystemType(typeItemSrc);
        DType typeIter = call.Args[1].Type;
        Type stIter = codeGen.GetSystemType(typeIter);
        DType typeItemDst = call.Args[call.Args.Length - 1].Type;
        Type stItemDst = codeGen.GetSystemType(typeItemDst);
        bool indexed = call.Indices[0] != null;

        // REVIEW: For the _zip version, if the source is null, we don't even need the init arg,
        // so (as an optimization) could try to avoid generating it. Probably not worth it....
        bool hasVolatile = call.Args[2].HasVolatile;
        MethodInfo meth;
        if (call.Args.Length == 3)
        {
            Validation.BugCheck(typeItemDst == typeIter);
            if (indexed)
                meth = fn.IsZip ? _methIndZ3 : _methInd3;
            else
                meth = fn.IsZip ? _methZ3 : _meth3;
            meth = meth.MakeGenericMethod(stItemSrc, stIter);
        }
        else
        {
            Validation.BugCheck(typeItemDst == call.Args[3].Type);
            hasVolatile |= call.Args[3].HasVolatile;
            if (indexed)
                meth = fn.IsZip ? _methIndZ4 : _methInd4;
            else
                meth = fn.IsZip ? _methZ4 : _meth4;
            meth = meth.MakeGenericMethod(stItemSrc, stIter, stItemDst);
        }

        stRet = GenCall(codeGen, meth, sts);
        wrap = hasVolatile ? SeqWrapKind.MustCache : default;
        return true;
    }

    public static IEnumerable<TIter> Exec<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);

        // Generate one additional item (from the init value).
        return CodeGenUtil.WrapWithCounter(src, 1, ExecCore(src, init, fn));
    }

    private static IEnumerable<TIter> ExecCore<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);

        TIter cur = init;
        yield return cur;
        if (src != null)
        {
            foreach (var item in src)
                yield return cur = fn(item, cur);
        }
    }

    public static IEnumerable<TIter> ExecInd<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);

        // Generate one additional item (from the init value).
        return CodeGenUtil.WrapWithCounter(src, 1, ExecIndCore(src, init, fn));
    }

    private static IEnumerable<TIter> ExecIndCore<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);

        TIter cur = init;
        yield return cur;
        if (src != null)
        {
            long idx = 0;
            foreach (var item in src)
                yield return cur = fn(idx++, item, cur);
        }
    }

    public static IEnumerable<TIter> ExecZ<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);

        if (src == null)
            return null;
        return CodeGenUtil.WrapWithCounter(src, ExecZCore(src, init, fn));
    }

    private static IEnumerable<TIter> ExecZCore<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);

        TIter cur = init;
        foreach (var item in src)
            yield return cur = fn(item, cur);
    }

    public static IEnumerable<TIter> ExecIndZ<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);

        if (src == null)
            return null;
        return CodeGenUtil.WrapWithCounter(src, ExecIndZCore(src, init, fn));
    }

    private static IEnumerable<TIter> ExecIndZCore<TSrc, TIter>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);

        TIter cur = init;
        long idx = 0;
        foreach (var item in src)
            yield return cur = fn(idx++, item, cur);
    }

    public static IEnumerable<TDst> ExecRes<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);

        // Generate one additional item (from the init value).
        return CodeGenUtil.WrapWithCounter(src, 1, ExecResCore(src, init, fn, fnRes));
    }

    private static IEnumerable<TDst> ExecResCore<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);

        TIter cur = init;
        yield return fnRes(init);
        if (src != null)
        {
            foreach (var item in src)
                yield return fnRes(cur = fn(item, cur));
        }
    }

    public static IEnumerable<TDst> ExecResInd<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);

        // Generate one additional item (from the init value).
        return CodeGenUtil.WrapWithCounter(src, 1, ExecResIndCore(src, init, fn, fnRes));
    }

    private static IEnumerable<TDst> ExecResIndCore<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, Func<TIter, TDst> fnRes)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);

        TIter cur = init;
        yield return fnRes(init);
        if (src != null)
        {
            long idx = 0;
            foreach (var item in src)
                yield return fnRes(cur = fn(idx++, item, cur));
        }
    }

    public static IEnumerable<TDst> ExecResZ<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, Func<TSrc, TIter, TDst> fnRes)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);
        if (src == null)
            return null;
        return CodeGenUtil.WrapWithCounter(src, ExecResZCore(src, init, fn, fnRes));
    }

    private static IEnumerable<TDst> ExecResZCore<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<TSrc, TIter, TIter> fn, Func<TSrc, TIter, TDst> fnRes)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);

        TIter cur = init;
        foreach (var item in src)
            yield return fnRes(item, cur = fn(item, cur));
    }

    public static IEnumerable<TDst> ExecResIndZ<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, Func<long, TSrc, TIter, TDst> fnRes)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);
        if (src == null)
            return null;
        return CodeGenUtil.WrapWithCounter(src, ExecResIndZCore(src, init, fn, fnRes));
    }

    private static IEnumerable<TDst> ExecResIndZCore<TSrc, TIter, TDst>(IEnumerable<TSrc> src, TIter init, Func<long, TSrc, TIter, TIter> fn, Func<long, TSrc, TIter, TDst> fnRes)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);
        Validation.AssertValue(fnRes);

        TIter cur = init;
        long idx = 0;
        foreach (var item in src)
        {
            yield return fnRes(idx, item, cur = fn(idx, item, cur));
            idx++;
        }
    }
}

public sealed class GenerateGen : RexlOperationGenerator<GenerateFunc>
{
    public static readonly GenerateGen Instance = new GenerateGen();

    private readonly MethodInfo _meth2;
    private readonly MethodInfo _meth3;
    private readonly MethodInfo _meth4;

    private GenerateGen()
    {
        _meth2 = new Func<long, Func<long, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        _meth3 = new Func<long, O, Func<long, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        _meth4 = new Func<long, O, Func<long, O, O>, Func<long, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType typeIter = call.Args[1].Type;
        Type stIter = codeGen.GetSystemType(typeIter);

        bool hasVolatile;
        Type stItemDst = stIter;
        MethodInfo meth;
        switch (call.Args.Length)
        {
        case 2:
            meth = _meth2.MakeGenericMethod(stIter);
            hasVolatile = call.Args[1].HasVolatile;
            break;
        case 3:
            Validation.Assert(typeIter == call.Args[2].Type);
            meth = _meth3.MakeGenericMethod(stIter);
            hasVolatile = call.Args[2].HasVolatile;
            break;
        case 4:
            var typeDst = call.Args[3].Type;
            Validation.Assert(typeDst.ToSequence() == call.Type);
            hasVolatile = call.Args[2].HasVolatile || call.Args[3].HasVolatile;
            stItemDst = codeGen.GetSystemType(typeDst);
            meth = _meth4.MakeGenericMethod(stIter, stItemDst);
            break;
        default:
            Validation.Assert(false);
            stRet = null;
            wrap = default;
            return false;
        }
        Validation.Assert(typeof(IEnumerable<>).MakeGenericType(stItemDst).IsAssignableFrom(meth.ReturnType));

        stRet = GenCall(codeGen, meth, sts);
        wrap = hasVolatile ? SeqWrapKind.MustCache : default;
        return true;
    }

    public static IEnumerable<TDst> Exec<TDst>(long lim, Func<long, TDst> fn)
    {
        if (lim <= 0)
            return null;

        return WrapWithCount.Create<TDst>(lim, ExecCore(lim, fn));
    }

    private static IEnumerable<TDst> ExecCore<TDst>(long lim, Func<long, TDst> fn)
    {
        for (long i = 0; i < lim; i++)
            yield return fn(i);
    }

    public static IEnumerable<TIter> Exec<TIter>(long lim, TIter iter, Func<long, TIter, TIter> fn)
    {
        if (lim <= 0)
            return null;

        return WrapWithCount.Create<TIter>(lim, ExecCore(lim, iter, fn));
    }

    private static IEnumerable<TIter> ExecCore<TIter>(long lim, TIter iter, Func<long, TIter, TIter> fn)
    {
        for (long i = 0; i < lim; i++)
            yield return iter = fn(i, iter);
    }

    public static IEnumerable<TDst> Exec<TIter, TDst>(long lim, TIter iter, Func<long, TIter, TIter> fn, Func<long, TIter, TDst> fnRes)
    {
        if (lim <= 0)
            return null;

        return WrapWithCount.Create<TDst>(lim, ExecCore(lim, iter, fn, fnRes));
    }

    private static IEnumerable<TDst> ExecCore<TIter, TDst>(long lim, TIter iter, Func<long, TIter, TIter> fn, Func<long, TIter, TDst> fnRes)
    {
        for (long i = 0; i < lim; i++)
            yield return fnRes(i, iter = fn(i, iter));
    }
}
