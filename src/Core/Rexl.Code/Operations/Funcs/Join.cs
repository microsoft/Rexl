// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using IE = IEnumerable<object>;
using L = System.Int64;
using O = System.Object;

public sealed class CrossJoinGen : RexlOperationGenerator<CrossJoinFunc>
{
    public static readonly CrossJoinGen Instance = new CrossJoinGen();

    private readonly MethodInfo _meth1;
    private readonly MethodInfo _methInd1;
    private readonly MethodInfo _meth2;
    private readonly MethodInfo _methInd2;
    private readonly MethodInfo _meth3;
    private readonly MethodInfo _methInd3;

    private CrossJoinGen()
    {
        _meth1 = new Func<IE, IE, Func<O, O, bool>, Func<O, O, O>, ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
        _methInd1 = new Func<IE, IE, Func<L, O, L, O, bool>, Func<L, O, L, O, O>, ExecCtx, int, IE>(ExecInd).Method.GetGenericMethodDefinition();
        _meth2 = new Func<IE, IE, Func<O, O, bool>, Func<O, O, O>, Func<O, O>, ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
        _methInd2 = new Func<IE, IE, Func<L, O, L, O, bool>, Func<L, O, L, O, O>, Func<L, O, O>, ExecCtx, int, IE>(ExecInd).Method.GetGenericMethodDefinition();
        _meth3 = new Func<IE, IE, Func<O, O, bool>, Func<O, O, O>, Func<O, O>, Func<O, O>, ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
        _methInd3 = new Func<IE, IE, Func<L, O, L, O, bool>, Func<L, O, L, O, O>, Func<L, O, O>, Func<L, O, O>, ExecCtx, int, IE>(ExecInd).Method.GetGenericMethodDefinition();
    }

    protected override bool NeedsIndexParamCore(BndCallNode call, int slot, int iidx)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.AssertIndex(slot, call.Args.Length);
        Validation.Assert(call.Traits.IsNested(slot));
        Validation.AssertIndex(iidx, call.Indices.Length);

        return HasIndices(call);
    }

    private bool HasIndices(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(call.Indices.Length == 2);
        return call.Indices[0] != null || call.Indices[1] != null;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        const int cseq = 2;
        int carg = call.Args.Length;
        int csel = carg - cseq - 1; // 1 is for the predicate.

        // Get the destination and source system types.
        DType typeItemDst = call.Type.ItemTypeOrThis;
        Type stItemDst = codeGen.GetSystemType(typeItemDst);

        var stsTypeArgs = new Type[cseq + 1];
        for (int i = 0; i < cseq; i++)
        {
            DType typeSeq = call.Args[i].Type;
            DType typeItemSrc = typeSeq.ItemTypeOrThis;
            stsTypeArgs[i] = codeGen.GetSystemType(typeItemSrc);
        }
        stsTypeArgs[cseq] = stItemDst;

        bool indexed = HasIndices(call);
        MethodInfo meth;
        bool hasVolatile = call.Args[2].HasVolatile || call.Args[3].HasVolatile;
        switch (csel)
        {
        case 1:
            meth = indexed ? _methInd1 : _meth1;
            break;
        case 2:
            meth = indexed ? _methInd2 : _meth2;
            hasVolatile |= call.Args[4].HasVolatile;
            break;
        default:
            Validation.Assert(csel == 3);
            meth = indexed ? _methInd3 : _meth3;
            hasVolatile |= call.Args[4].HasVolatile || call.Args[5].HasVolatile;
            break;
        }
        meth = meth.MakeGenericMethod(stsTypeArgs);

        stRet = GenCallCtxId(codeGen, meth, sts, call, 2);
        wrap = hasVolatile ? SeqWrapKind.MustCache : default;
        return true;
    }

    public static IEnumerable<TDst> Exec<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred, Func<T0, T1, TDst> fn,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return Iter(s0, s1, pred, fn, ctx, id0);
    }

    private static IEnumerable<TDst> Iter<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred, Func<T0, T1, TDst> fn,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);

        using var ator0 = s0.GetEnumerator();
        int id1 = id0 + 1;
        for (; ; )
        {
            ctx.Ping(id0);
            if (!ator0.MoveNext())
                break;
            var val0 = ator0.Current;
            using var ator1 = s1.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id1);
                if (!ator1.MoveNext())
                    break;
                var val1 = ator1.Current;
                if (pred(val0, val1))
                    yield return fn(val0, val1);
            }
        }
    }

    public static IEnumerable<TDst> ExecInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, long, T1, bool> pred, Func<long, T0, long, T1, TDst> fn,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return IterInd(s0, s1, pred, fn, ctx, id0);
    }

    private static IEnumerable<TDst> IterInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, long, T1, bool> pred, Func<long, T0, long, T1, TDst> fn,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);

        using var ator0 = s0.GetEnumerator();
        int id1 = id0 + 1;
        for (long index0 = 0; ; index0++)
        {
            ctx.Ping(id0);
            if (!ator0.MoveNext())
                break;
            var val0 = ator0.Current;
            using var ator1 = s1.GetEnumerator();
            for (long index1 = 0; ; index1++)
            {
                ctx.Ping(id1);
                if (!ator1.MoveNext())
                    break;
                var val1 = ator1.Current;
                if (pred(index0, val0, index1, val1))
                    yield return fn(index0, val0, index1, val1);
            }
        }
    }

    public static IEnumerable<TDst> Exec<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred, Func<T0, T1, TDst> fn, Func<T0, TDst> fn0,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return Iter(s0, s1, pred, fn, fn0, ctx, id0);
    }

    private static IEnumerable<TDst> Iter<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred, Func<T0, T1, TDst> fn, Func<T0, TDst> fn0,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(ctx);

        using var ator0 = s0.GetEnumerator();
        int id1 = id0 + 1;
        for (; ; )
        {
            ctx.Ping(id0);
            if (!ator0.MoveNext())
                break;
            var val0 = ator0.Current;
            bool match = false;
            using var ator1 = s1.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id1);
                if (!ator1.MoveNext())
                    break;
                var val1 = ator1.Current;
                if (pred(val0, val1))
                {
                    match = true;
                    yield return fn(val0, val1);
                }
            }

            if (!match)
                yield return fn0(val0);
        }
    }

    public static IEnumerable<TDst> ExecInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, long, T1, bool> pred, Func<long, T0, long, T1, TDst> fn, Func<long, T0, TDst> fn0,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return IterInd(s0, s1, pred, fn, fn0, ctx, id0);
    }

    private static IEnumerable<TDst> IterInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, long, T1, bool> pred, Func<long, T0, long, T1, TDst> fn, Func<long, T0, TDst> fn0,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(ctx);

        using var ator0 = s0.GetEnumerator();
        int id1 = id0 + 1;
        for (long index0 = 0; ; index0++)
        {
            ctx.Ping(id0);
            if (!ator0.MoveNext())
                break;
            var val0 = ator0.Current;
            bool match = false;
            using var ator1 = s1.GetEnumerator();
            for (long index1 = 0; ; index1++)
            {
                ctx.Ping(id1);
                if (!ator1.MoveNext())
                    break;
                var val1 = ator1.Current;
                if (pred(index0, val0, index1, val1))
                {
                    match = true;
                    yield return fn(index0, val0, index1, val1);
                }
            }

            if (!match)
                yield return fn0(index0, val0);
        }
    }

    public static IEnumerable<TDst> Exec<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred, Func<T0, T1, TDst> fn, Func<T0, TDst> fn0, Func<T1, TDst> fn1,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(fn1);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return Iter(s0, s1, pred, fn, fn0, fn1, ctx, id0);
    }

    private static IEnumerable<TDst> Iter<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred, Func<T0, T1, TDst> fn, Func<T0, TDst> fn0, Func<T1, TDst> fn1,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(ctx);

        using var ator0 = s0.GetEnumerator();
        long ivLim = -1;
        var inds = new HashSet<long>();
        int id1 = id0 + 1;
        for (; ; )
        {
            ctx.Ping(id0);
            if (!ator0.MoveNext())
                break;
            var val0 = ator0.Current;
            bool match = false;
            using var ator1 = s1.GetEnumerator();
            long iv = 0;
            for (; ; )
            {
                ctx.Ping(id1);
                if (!ator1.MoveNext())
                    break;
                var val1 = ator1.Current;
                if (pred(val0, val1))
                {
                    inds.Add(iv);
                    match = true;
                    yield return fn(val0, val1);
                }
                iv++;
            }
            if (ivLim < iv)
                ivLim = iv;

            if (!match)
                yield return fn0(val0);
        }

        Validation.Assert(inds.Count <= Math.Max(0, ivLim));
        if (inds.Count < ivLim)
        {
            long iv = 0;
            foreach (var val1 in s1)
            {
                if (!inds.Contains(iv))
                    yield return fn1(val1);
                iv++;
            }
        }
        else if (ivLim < 0)
        {
            // The left sequence was empty, so we never iterated through the right.
            foreach (var val1 in s1)
                yield return fn1(val1);
        }
    }

    public static IEnumerable<TDst> ExecInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, long, T1, bool> pred, Func<long, T0, long, T1, TDst> fn, Func<long, T0, TDst> fn0, Func<long, T1, TDst> fn1,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(fn1);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return IterInd(s0, s1, pred, fn, fn0, fn1, ctx, id0);
    }

    private static IEnumerable<TDst> IterInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, long, T1, bool> pred, Func<long, T0, long, T1, TDst> fn, Func<long, T0, TDst> fn0, Func<long, T1, TDst> fn1,
        ExecCtx ctx, int id0)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(fn);
        Validation.AssertValue(fn0);
        Validation.AssertValue(ctx);

        using var ator0 = s0.GetEnumerator();
        long ivLim = -1;
        var inds = new HashSet<long>();
        int id1 = id0 + 1;
        for (long index0 = 0; ; index0++)
        {
            ctx.Ping(id0);
            if (!ator0.MoveNext())
                break;
            var val0 = ator0.Current;
            bool match = false;
            using var ator1 = s1.GetEnumerator();
            long iv = 0;
            for (long index1 = 0; ; index1++)
            {
                ctx.Ping(id1);
                if (!ator1.MoveNext())
                    break;
                var val1 = ator1.Current;
                if (pred(index0, val0, index1, val1))
                {
                    inds.Add(iv);
                    match = true;
                    yield return fn(index0, val0, index1, val1);
                }
                iv++;
            }
            if (ivLim < iv)
                ivLim = iv;

            if (!match)
                yield return fn0(index0, val0);
        }

        Validation.Assert(inds.Count <= Math.Max(0, ivLim));
        if (inds.Count < ivLim)
        {
            long iv = 0;
            foreach (var val1 in s1)
            {
                if (!inds.Contains(iv))
                    yield return fn1(iv, val1);
                iv++;
            }
        }
        else if (ivLim < 0)
        {
            // The left sequence was empty, so we never iterated through the right.
            long iv = 0;
            foreach (var val1 in s1)
                yield return fn1(iv++, val1);
        }
    }
}

public sealed class KeyJoinGen : RexlOperationGenerator<KeyJoinFunc>
{
    public static readonly KeyJoinGen Instance = new KeyJoinGen();

    private readonly MethodInfo _meth;
    private readonly MethodInfo _methInd;

    private KeyJoinGen()
    {
        _meth = new Func<IE, IE,
           Func<O, O>, Func<O, O>,
           Func<O, O, O>, Func<O, O>, Func<O, O>,
           EqualityComparer<O>, bool,
           ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
        _methInd = new Func<IE, IE,
           Func<L, O, O>, Func<L, O, O>,
           Func<L, O, L, O, O>, Func<L, O, O>, Func<L, O, O>,
           EqualityComparer<O>, bool,
           ExecCtx, int, IE>(ExecInd).Method.GetGenericMethodDefinition();
    }

    protected override bool NeedsIndexParamCore(BndCallNode call, int slot, int iidx)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.AssertIndex(slot, call.Args.Length);
        Validation.Assert(call.Traits.IsNested(slot));
        Validation.AssertIndex(iidx, call.Indices.Length);

        return HasIndices(call);
    }

    private bool HasIndices(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(call.Indices.Length == 2);
        return call.Indices[0] != null || call.Indices[1] != null;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        const int cseq = 2;
        int carg = call.Args.Length;
        int csel = carg - cseq - 2; // 2 is for the key functions.
        var typeKey = call.Args[cseq].Type;
        Validation.Assert(typeKey.IsEquatable);
        Type stKey = codeGen.GetSystemType(typeKey);

        // Get the destination and source system types.
        DType typeItemDst = call.Type.ItemTypeOrThis;
        Type stItemDst = codeGen.GetSystemType(typeItemDst);

        var stsTypeArgs = new Type[cseq + 2];
        for (int i = 0; i < cseq; i++)
        {
            DType typeSeq = call.Args[i].Type;
            DType typeItemSrc = typeSeq.ItemTypeOrThis;
            stsTypeArgs[i] = codeGen.GetSystemType(typeItemSrc);
        }
        stsTypeArgs[cseq] = stKey;
        stsTypeArgs[cseq + 1] = stItemDst;

        bool indexed = HasIndices(call);
        Validation.Assert(NeedsIndexParam(call, cseq, 0) == indexed);
        Validation.Assert(NeedsIndexParam(call, cseq + 1, 1) == indexed);
        Validation.Assert(NeedsIndexParam(call, cseq + 2, 0) == indexed);
        Validation.Assert(NeedsIndexParam(call, cseq + 2, 1) == indexed);
        Validation.Assert(csel < 2 || NeedsIndexParam(call, cseq + 3, 0) == indexed);
        Validation.Assert(csel < 3 || NeedsIndexParam(call, cseq + 4, 1) == indexed);
#if DEBUG
        Type stFnLeftKey, stFnRightKey, stFnMatch;
        if (indexed)
        {
            stFnLeftKey = typeof(Func<,,>).MakeGenericType(typeof(long), stsTypeArgs[0], stKey);
            stFnRightKey = typeof(Func<,,>).MakeGenericType(typeof(long), stsTypeArgs[1], stKey);
            stFnMatch = typeof(Func<,,,,>).MakeGenericType(typeof(long), stsTypeArgs[0], typeof(long), stsTypeArgs[1], stItemDst);
        }
        else
        {
            stFnLeftKey = typeof(Func<,>).MakeGenericType(stsTypeArgs[0], stKey);
            stFnRightKey = typeof(Func<,>).MakeGenericType(stsTypeArgs[1], stKey);
            stFnMatch = typeof(Func<,,>).MakeGenericType(stsTypeArgs[0], stsTypeArgs[1], stItemDst);
        }

        // Validate required selectors.
        Validation.Assert(stFnLeftKey.IsAssignableFrom(sts[cseq]));
        Validation.Assert(stFnRightKey.IsAssignableFrom(sts[cseq + 1]));
        Validation.Assert(stFnMatch.IsAssignableFrom(sts[carg - csel]));
#endif

        // Validate the optional match selectors or load nulls for them.
        bool hasVolatile;
        switch (csel)
        {
        default:
            Validation.Assert(false);
            stRet = null;
            wrap = default;
            return false;

        case 1:
            codeGen.Writer.Ldnull().Ldnull();
            hasVolatile = call.Args[4].HasVolatile;
            break;
        case 2:
#if DEBUG
            Type stFnLeft = indexed ?
                typeof(Func<,,>).MakeGenericType(typeof(long), stsTypeArgs[0], stItemDst) :
                typeof(Func<,>).MakeGenericType(stsTypeArgs[0], stItemDst);
            Validation.Assert(stFnLeft.IsAssignableFrom(sts[carg - csel + 1]));
#endif
            codeGen.Writer.Ldnull();
            hasVolatile = call.Args[4].HasVolatile || call.Args[5].HasVolatile;
            break;
        case 3:
#if DEBUG
            Type stFnRight;
            if (indexed)
            {
                stFnLeft = typeof(Func<,,>).MakeGenericType(typeof(long), stsTypeArgs[0], stItemDst);
                stFnRight = typeof(Func<,,>).MakeGenericType(typeof(long), stsTypeArgs[1], stItemDst);
            }
            else
            {
                stFnLeft = typeof(Func<,>).MakeGenericType(stsTypeArgs[0], stItemDst);
                stFnRight = typeof(Func<,>).MakeGenericType(stsTypeArgs[1], stItemDst);
            }
            Validation.Assert(stFnLeft.IsAssignableFrom(sts[carg - csel + 1]));
            Validation.Assert(stFnRight.IsAssignableFrom(sts[carg - csel + 2]));
#endif
            hasVolatile = call.Args[4].HasVolatile || call.Args[5].HasVolatile || call.Args[6].HasVolatile;
            break;
        }

        var dirs = call.Directives;
        bool eq = dirs.GetItemOrDefault(2).IsEq() || dirs.GetItemOrDefault(3).IsEq();
        bool ci = typeKey.HasText && (dirs.GetItemOrDefault(2).IsCi() || dirs.GetItemOrDefault(3).IsCi());
        bool ti = !eq && (typeKey.HasOpt | typeKey.HasFloat);

        // Get the method.
        MethodInfo meth = indexed ? _methInd : _meth;
        meth = meth.MakeGenericMethod(stsTypeArgs);
        var parms = meth.GetParameters();
        Validation.Assert(parms.Length == 11);
        Validation.Assert(parms[7].ParameterType == typeof(EqualityComparer<>).MakeGenericType(stKey));
        Validation.Assert(parms[8].ParameterType == typeof(bool));

        // Load the equality comparer.
        if (codeGen.GenLoadEqCmpOrNull(typeKey, ti, ci, out Type stEq, out Type stAgg))
        {
            Validation.Assert(stAgg == stKey);
            Validation.Assert(parms[7].ParameterType.IsAssignableFrom(stEq));

            // Load the strict flag.
            codeGen.Writer.Ldc_B(ti);
        }
        else
        {
            Validation.Assert(!ti);
            // Load the strict flag.
            codeGen.Writer.Ldc_B(false);
        }

        var stsFull = new Type[9];
        int len = sts.Length;
        sts.Copy(0, len, stsFull, 0);
        for (; len < 9; len++)
            stsFull[len] = parms[len].ParameterType;

        // Call the method.
        stRet = GenCallCtxId(codeGen, meth, stsFull, call, 3);
        wrap = hasVolatile ? SeqWrapKind.MustCache : default;
        return true;
    }

    public static IEnumerable<TDst> Exec<T0, T1, TKey, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, Func<T0, TKey> fnKey0, Func<T1, TKey> fnKey1,
        Func<T0, T1, TDst> fn, Func<T0, TDst> fn0, Func<T1, TDst> fn1,
        EqualityComparer<TKey> cmp, bool strict,
        ExecCtx ctx, int idMin)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(fnKey0);
        Validation.AssertValue(fnKey1);
        Validation.AssertValue(fn);
        Validation.AssertValueOrNull(fn0);
        Validation.AssertValueOrNull(fn1);
        Validation.Assert(fn1 == null || fn0 != null);
        Validation.AssertValueOrNull(cmp);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return Iter(ctx, idMin, s0, s1, fnKey0, fnKey1, cmp, strict, fn, fn0, fn1);
    }

    private static IEnumerable<TDst> Iter<T0, T1, TKey, TDst>(
        ExecCtx ctx, int idMin,
        IEnumerable<T0> s0, IEnumerable<T1> s1, Func<T0, TKey> fnKey0, Func<T1, TKey> fnKey1,
        EqualityComparer<TKey> cmp, bool strict,
        Func<T0, T1, TDst> fn, Func<T0, TDst> fn0 = null, Func<T1, TDst> fn1 = null)
    {
        Validation.AssertValue(ctx);
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(fnKey0);
        Validation.AssertValue(fnKey1);
        Validation.AssertValueOrNull(cmp);
        Validation.Assert(!strict || cmp != null);
        Validation.AssertValue(fn);
        Validation.AssertValueOrNull(fn0);
        Validation.AssertValueOrNull(fn1);
        Validation.Assert(fn1 == null || fn0 != null);

        using var e0 = s0.GetEnumerator();

        // Process initial left items with bad key.
        T0 item0;
        TKey key0;
        int id0 = idMin + 1;
        for (; ; )
        {
            if (!e0.MoveNext())
            {
                if (fn1 != null)
                {
                    foreach (var item1 in s1)
                        yield return fn1(item1);
                }
                yield break;
            }
            item0 = e0.Current;
            key0 = fnKey0(item0);
            if (!(strict && !cmp.Equals(key0, key0)))
                break;
            if (fn0 != null)
                yield return fn0(item0);
            else
                ctx.Ping(id0);
        }
        Validation.Assert(!(strict && !cmp.Equals(key0, key0)));

        // Process first left item with good key. Also build the lookup table. Any right items
        // with bad key are processed immediately (and not put in the lookup).
        Lookup<T1, TKey> lookup = null;
        bool any = false;
        int id1 = idMin + 2;
        foreach (var item1 in s1)
        {
            var key1 = fnKey1(item1);
            if (strict && !cmp.Equals(key1, key1))
            {
                if (fn1 != null)
                    yield return fn1(item1);
                else
                    ctx.Ping(id1);
            }
            else
            {
                lookup ??= new Lookup<T1, TKey>(cmp, key0);
                if (lookup.Add(item1, key1))
                {
                    any = true;
                    yield return fn(item0, item1);
                }
                else
                    ctx.Ping(id1);
            }
        }

        for (; ; )
        {
            if (!any)
            {
                if (fn0 != null)
                    yield return fn0(item0);
                else
                    ctx.Ping(idMin);
            }

            // Fetch next left item.
            if (!e0.MoveNext())
                break;
            item0 = e0.Current;
            key0 = fnKey0(item0);
            any = false;

            if (lookup != null && !(strict && !cmp.Equals(key0, key0)))
            {
                var (item1, i1) = lookup.GetFirstMatch(key0);
                while (i1 >= 0)
                {
                    any = true;
                    yield return fn(item0, item1);
                    (item1, i1) = lookup.GetNextMatch(i1);
                }
            }
        }

        if (fn1 != null && lookup != null)
        {
            Validation.Assert(lookup.MatchCount <= lookup.Count);

            if (lookup.MatchCount < lookup.Count)
            {
                // Handle the unmatched right items.
                foreach (var item1 in lookup.GetUnmatchedItems())
                    yield return fn1(item1);
            }
        }
    }

    public static IEnumerable<TDst> ExecInd<T0, T1, TKey, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, Func<long, T0, TKey> fnKey0, Func<long, T1, TKey> fnKey1,
        Func<long, T0, long, T1, TDst> fn, Func<long, T0, TDst> fn0, Func<long, T1, TDst> fn1,
        EqualityComparer<TKey> cmp, bool strict,
        ExecCtx ctx, int idMin)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(fnKey0);
        Validation.AssertValue(fnKey1);
        Validation.AssertValue(fn);
        Validation.AssertValueOrNull(fn0);
        Validation.AssertValueOrNull(fn1);
        Validation.Assert(fn1 == null || fn0 != null);
        Validation.AssertValueOrNull(cmp);
        Validation.Assert(!strict || cmp != null);
        Validation.AssertValue(ctx);

        if (s0 == null || s1 == null)
            return null;
        return IterInd(ctx, idMin, s0, s1, fnKey0, fnKey1, cmp, strict, fn, fn0, fn1);
    }

    private static IEnumerable<TDst> IterInd<T0, T1, TKey, TDst>(
        ExecCtx ctx, int idMin,
        IEnumerable<T0> s0, IEnumerable<T1> s1, Func<long, T0, TKey> fnKey0, Func<long, T1, TKey> fnKey1,
        EqualityComparer<TKey> cmp, bool strict,
        Func<long, T0, long, T1, TDst> fn, Func<long, T0, TDst> fn0 = null, Func<long, T1, TDst> fn1 = null)
    {
        Validation.AssertValue(ctx);
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(fnKey0);
        Validation.AssertValue(fnKey1);
        Validation.AssertValueOrNull(cmp);
        Validation.Assert(!strict || cmp != null);
        Validation.AssertValue(fn);
        Validation.AssertValueOrNull(fn0);
        Validation.AssertValueOrNull(fn1);
        Validation.Assert(fn1 == null || fn0 != null);

        using var e0 = s0.GetEnumerator();

        // Process initial left items with null key.
        long index0, index1;
        index0 = index1 = 0;
        T0 item0;
        TKey key0;
        int id0 = idMin + 1;
        for (long i = 0; ; i++)
        {
            if (!e0.MoveNext())
            {
                if (fn1 != null)
                {
                    foreach (var item1 in s1)
                        yield return fn1(index1++, item1);
                }
                yield break;
            }
            item0 = e0.Current;
            index0 = i;
            key0 = fnKey0(index0, item0);
            if (!(strict && !cmp.Equals(key0, key0)))
                break;
            if (fn0 != null)
                yield return fn0(index0, item0);
            else
                ctx.Ping(id0);
        }
        Validation.Assert(!(strict && !cmp.Equals(key0, key0)));

        // Process first left item with good key. Also build the lookup table. Any right items
        // with bad key are processed immediately (and not put in the lookup).
        Lookup<(long, T1), TKey> lookup = null;
        bool any = false;
        int id1 = idMin + 2;
        index1 = 0;
        foreach (var item1 in s1)
        {
            var key1 = fnKey1(index1, item1);
            if (strict && !cmp.Equals(key1, key1))
            {
                if (fn1 != null)
                    yield return fn1(index1, item1);
                else
                    ctx.Ping(id1);
            }
            else
            {
                lookup ??= new Lookup<(long, T1), TKey>(cmp, key0);
                if (lookup.Add((index1, item1), key1))
                {
                    any = true;
                    yield return fn(index0, item0, index1, item1);
                }
                else
                    ctx.Ping(id1);
            }
            index1++;
        }

        for (; ; )
        {
            if (!any)
            {
                if (fn0 != null)
                    yield return fn0(index0, item0);
                else
                    ctx.Ping(idMin);
            }

            // Fetch next left item.
            if (!e0.MoveNext())
                break;
            item0 = e0.Current;
            key0 = fnKey0(++index0, item0);
            any = false;

            if (lookup != null && !(strict && !cmp.Equals(key0, key0)))
            {
                var (pair1, i1) = lookup.GetFirstMatch(key0);
                var (idxItem1, item1) = pair1;
                while (i1 >= 0)
                {
                    any = true;
                    yield return fn(index0, item0, idxItem1, item1);
                    ((idxItem1, item1), i1) = lookup.GetNextMatch(i1);
                }
            }
        }

        if (fn1 != null && lookup != null)
        {
            Validation.Assert(lookup.MatchCount <= lookup.Count);

            if (lookup.MatchCount < lookup.Count)
            {
                // Handle the unmatched right items.
                foreach (var (idxItem1, item1) in lookup.GetUnmatchedItems())
                    yield return fn1(idxItem1, item1);
            }
        }
    }

    /// <summary>
    /// A lookup table used for key based joining.
    /// REVIEW: Support at least multiple billions of items?
    /// </summary>
    private sealed class Lookup<TItem, TKey>
    {
        // The comparer.
        private readonly IEqualityComparer<TKey> _cmp;
        // The key value that matters while building.
        private readonly TKey _keyInit;
        // The first index corresponding to _keyInit, if it has been hit. Otherwise, -1.
        private int _indexInit;

        // Maps from key value to first and last item indices for that key. The _nexts array links the matches.
        private readonly Dictionary<TKey, (int first, int last)> _map;
        // Dictionary doesn't support a null key, so we we need to store the entry separately, when supported.
        private (int first, int last) _pairForNull;

        // The items. This array is parallel to _nexts.
        private TItem[] _items;
        // The low bit indicates whether the item has been "hit". The remaining bits store the next index plus one.
        private uint[] _nexts;

        // The number of items/nexts allocated.
        private int _cap;
        // The number of items/nexts that are active.
        private int _count;
        // The number of items that have been "hit".
        private int _chit;

        /// <summary>
        /// The number of items in the table.
        /// </summary>
        public long Count => _count;

        /// <summary>
        /// The number of items in the table that have been matched.
        /// </summary>
        public long MatchCount => _chit;

        public Lookup(IEqualityComparer<TKey> cmp, TKey keyInit)
        {
            Validation.AssertValueOrNull(cmp);
            _cmp = cmp ?? EqualityComparer<TKey>.Default;
            _keyInit = keyInit;
            _indexInit = -1;
            _map = new Dictionary<TKey, (int first, int last)>(_cmp);
            _pairForNull = (-1, -1);
        }

        private bool TryGet(TKey key, out (int first, int last) pair)
        {
            if (key is null)
            {
                pair = _pairForNull;
                return pair.first >= 0;
            }
            return _map.TryGetValue(key, out pair);
        }

        private void Set(TKey key, (int first, int last) pair)
        {
            if (key is null)
                _pairForNull = pair;
            else
                _map[key] = pair;
        }

        /// <summary>
        /// Add a new item (dups are fine) and return whether it matches the init key passed to the ctor.
        /// </summary>
        public bool Add(TItem item, TKey key)
        {
            int index = _count;
            if (index >= _cap)
            {
                int capMin = index + 1;
                int cap = Util.GetCapTarget(_cap, capMin);
                Util.Grow(ref _items, ref cap, capMin);
                Util.Grow(ref _nexts, ref cap, capMin);
                _cap = cap;
            }
            Validation.Assert(index < _cap);
            Validation.Assert(_nexts[index] == 0);
            _items[index] = item;

            bool hit;
            if (TryGet(key, out var pair))
            {
                Validation.AssertIndex(pair.first, _count);
                Validation.AssertIndex(pair.last, _count);
                Set(key, (pair.first, index));
                hit = (_indexInit == pair.first);
                Validation.Assert((_nexts[pair.last] >> 1) == 0);
                Validation.Assert(hit == ((_nexts[pair.last] & 1) != 0));
                _nexts[pair.last] |= (uint)(index + 1) << 1;
            }
            else
            {
                Set(key, (index, index));
                hit = _indexInit < 0 && _cmp.Equals(key, _keyInit);
                if (hit)
                    _indexInit = index;
            }

            _count = index + 1;
            if (hit)
            {
                _nexts[index] = 1;
                _chit++;
                Validation.Assert(_chit <= _count);
            }
            return hit;
        }

        /// <summary>
        /// Get the item value and index of the first match for the given key. The result
        /// index is -1 when there is no match.
        /// </summary>
        public (TItem item, int index) GetFirstMatch(TKey key)
        {
            if (!TryGet(key, out var pair))
                return (default, -1);
            Validation.AssertIndex(pair.first, _count);
            Validation.AssertIndex(pair.last, _count);
            var next = pair.first;
            if ((_nexts[next] & 1) == 0)
            {
                _nexts[next] |= 1;
                _chit++;
                Validation.Assert(_chit <= _count);
            }
            return (_items[next], next);
        }

        /// <summary>
        /// Get the next item value and index in the match chain including the given <paramref name="index"/>.
        /// The result index is -1 when there are no more items in the match chain.
        /// </summary>
        public (TItem item, int index) GetNextMatch(int index)
        {
            Validation.AssertIndex(index, _count);
            int next = (int)(_nexts[index] >> 1);
            Validation.AssertIndexInclusive(next, _count);
            if (--next < 0)
                return (default, -1);
            Validation.Assert(next > index);
            if ((_nexts[next] & 1) == 0)
            {
                _nexts[next] |= 1;
                _chit++;
                Validation.Assert(_chit <= _count);
            }
            return (_items[next], next);
        }

        /// <summary>
        /// Enumerate over the items that have never been "matched".
        /// </summary>
        public IEnumerable<TItem> GetUnmatchedItems()
        {
            Validation.Assert(_chit <= _count);
            int count = _count - _chit;
            int i = 0;
            for (; count > 0 && i < _count; i++)
            {
                if ((_nexts[i] & 1) == 0)
                {
                    yield return _items[i];
                    count--;
                }
            }
            Validation.Assert(count == 0);
            Validation.Assert(Enumerable.Range(i, _count - i).All(ind => (_nexts[ind] & 1) != 0));
        }
    }
}
