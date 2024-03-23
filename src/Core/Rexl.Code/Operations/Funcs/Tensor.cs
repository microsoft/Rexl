// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

using ArgTuple = Immutable.Array<BoundNode>;
using IE = IEnumerable<object>;
using Integer = System.Numerics.BigInteger;
using O = System.Object;
using RngTuple = Immutable.Array<SlotRange>;

public abstract class TensorGen<TOper> : RexlOperationGenerator<TOper>
    where TOper : TensorFunc
{
    private static readonly MethodInfo _methClip = new Func<long, ExecCtx, int, long>(ClipDim).Method;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ClipDim(long dim, ExecCtx ctx, int id)
    {
        // REVIEW: Warn on bad dimension.
        if (dim < 0)
            return 0;
        return dim;
    }

    private protected static int GenI8Array(ICodeGen codeGen, int id,
        ArgTuple args, int slotMin, int slotLim, bool clip, int cur)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertIndexInclusive(slotLim, args.Length);
        Validation.AssertIndexInclusive(slotMin, slotLim);

        var ilw = codeGen.Writer;
        int len = slotLim - slotMin;

        ilw
            .Ldc_I4(len)
            .Newarr(typeof(long));
        var meth = clip ? _methClip : null;
        for (int i = 0; i < len; i++)
        {
            ilw.Dup().Ldc_I4(i);
            var ind = args[i + slotMin];
            Validation.Assert(ind.Type == DType.I8Req);
            if (!clip)
                codeGen.GenCode(ind, ref cur);
            else if (ind.TryGetIntegral(out var v))
            {
                // The reducer should have already clipped the value.
                Validation.Assert(v >= 0);
                ilw.Ldc_I8((long)v);
                cur += ind.NodeCount;
            }
            else
            {
                codeGen.GenCode(ind, ref cur);
                codeGen.GenLoadExecCtx();
                ilw.Ldc_I4(id);
                ilw.Call(meth);
            }
            ilw.Stelem_I8();
        }
        return cur;
    }
}

public sealed class TensorFillGen : TensorGen<TensorFillFunc>
{
    public static readonly TensorFillGen Instance = new TensorFillGen();

    private readonly ReadOnly.Array<MethodInfo> _meths;
    private readonly MethodInfo _methRng;

    private TensorFillGen()
    {
        _meths = new[]
        {
            new Func<byte, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<byte, long, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<byte, long, long, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<byte, long, long, long, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methRng = new Func<byte, long[], ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));

        // REVIEW: Use the ExecCtx to report runtime warnings on the dimensions.
        return NeedsExecCtx(call.Args.Length);
    }

    private bool NeedsExecCtx(int arity) => arity > 1;

    protected override RngTuple GetArrayRangesCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));

        int cdim = call.Args.Length - 1;
        if (cdim < _meths.Length)
            return RngTuple.Empty;

        return RngTuple.Create(new SlotRange(1, 1 + cdim));
    }

    protected override bool IsValidArrayRangesCore(BndCallNode call, RngTuple ranges)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(!ranges.IsDefault);

        int cdim = call.Args.Length - 1;
        if (cdim < _meths.Length)
            return ranges.IsDefaultOrEmpty;

        return ranges.Length == 1 && ranges[0].Min == 1 && ranges[0].Count == cdim;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, RngTuple rngs,
        ReadOnly.Array<Type> sts, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(IsValidArrayRanges(call, rngs));

        DType type = call.Type;
        var args = call.Args;

        Validation.Assert(type.IsTensorReq);

        DType typeItem = type.GetTensorItemType();
        Validation.Assert(typeItem == args[0].Type);
        Type stItem = codeGen.GetSystemType(typeItem);

        int cdim = args.Length - 1;
        Validation.Assert(type.TensorRank == cdim);

        MethodInfo meth;
        if (cdim < _meths.Length)
        {
            Validation.Assert(sts.Length == 1 + cdim);
            Validation.Assert(rngs.IsDefaultOrEmpty);
            meth = _meths[cdim];
        }
        else
        {
            Validation.Assert(sts.Length == 2);
            Validation.Assert(rngs.Length == 1);
            meth = _methRng;
        }
        meth = meth.MakeGenericMethod(stItem);
        Validation.Assert(meth.ReturnType == typeof(Tensor<>).MakeGenericType(stItem));

        stRet = NeedsExecCtx(args.Length) ?
            GenCallCtxId(codeGen, meth, sts, call) :
            GenCall(codeGen, meth, sts);
        wrap = default;
        return true;
    }

    public static Tensor<T> Exec<T>(T value)
    {
        var res = Tensor<T>.CreateFill(value);
        return res;
    }

    public static Tensor<T> Exec<T>(T value, long dim, ExecCtx ctx, int id)
    {
        // REVIEW: What about oom?
        var res = Tensor<T>.CreateFill(value, ClipDim(dim, ctx, id));
        return res;
    }

    public static Tensor<T> Exec<T>(T value, long dim0, long dim1, ExecCtx ctx, int id)
    {
        // REVIEW: What about oom?
        var res = Tensor<T>.CreateFill(value, ClipDim(dim0, ctx, id), ClipDim(dim1, ctx, id));
        return res;
    }

    public static Tensor<T> Exec<T>(T value, long dim0, long dim1, long dim2, ExecCtx ctx, int id)
    {
        // REVIEW: What about oom?
        var res = Tensor<T>.CreateFill(value, ClipDim(dim0, ctx, id), ClipDim(dim1, ctx, id), ClipDim(dim2, ctx, id));
        return res;
    }

    public static Tensor<T> Exec<T>(T value, long[] dims, ExecCtx ctx, int id)
    {
        for (int i = 0; i < dims.Length; i++)
            dims[i] = ClipDim(dims[i], ctx, id);
        // REVIEW: What about oom?
        var res = Tensor<T>.CreateFill(value, dims);
        return res;
    }
}

public sealed class TensorFromGen : TensorGen<TensorFromFunc>
{
    public static readonly TensorFromGen Instance = new TensorFromGen();

    private readonly ReadOnly.Array<MethodInfo> _meths;
    private readonly ReadOnly.Array<MethodInfo> _methsDef;
    private readonly MethodInfo _methRng;
    private readonly MethodInfo _methRngDef;

    private TensorFromGen()
    {
        _meths = new[]
        {
            new Func<IEnumerable<byte>, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<IEnumerable<byte>, long, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methsDef = new[]
        {
            new Func<IEnumerable<byte>, byte, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<IEnumerable<byte>, long, byte, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methRng = new Func<IEnumerable<byte>, long[], ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
        _methRngDef = new Func<IEnumerable<byte>, long[], byte, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override RngTuple GetArrayRangesCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));

        int cdim = call.Args.Length - 1;
        if (cdim < _meths.Length)
            return RngTuple.Empty;

        return RngTuple.Create(new SlotRange(1, 1 + cdim));
    }

    protected override bool IsValidArrayRangesCore(BndCallNode call, RngTuple ranges)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(!ranges.IsDefault);

        int cdim = call.Args.Length - 1;
        if (cdim < _meths.Length)
            return ranges.IsDefaultOrEmpty;

        return ranges.Length == 1 && ranges[0].Min == 1 && ranges[0].Count == cdim;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, RngTuple rngs,
        ReadOnly.Array<Type> sts, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(IsValidArrayRanges(call, rngs));

        DType type = call.Type;
        var args = call.Args;

        Validation.Assert(type.IsTensorReq);

        DType typeItem = type.GetTensorItemType();
        Validation.Assert(typeItem.ToSequence() == args[0].Type);
        Type stItem = codeGen.GetSystemType(typeItem);

        var loadDef = !typeItem.IsOpt && !stItem.IsValueType;

        int cdim = args.Length - 1;
        Validation.Assert(type.TensorRank == Math.Max(1, cdim));

        MethodInfo meth;
        if (cdim < _meths.Length)
        {
            Validation.Assert(sts.Length == 1 + cdim);
            Validation.Assert(rngs.IsDefaultOrEmpty);
            meth = loadDef ? _methsDef[cdim] : _meths[cdim];
        }
        else
        {
            Validation.Assert(sts.Length == 2);
            Validation.Assert(rngs.Length == 1);
            meth = loadDef ? _methRngDef : _methRng;
        }
        meth = meth.MakeGenericMethod(stItem);
        Validation.Assert(meth.ReturnType == typeof(Tensor<>).MakeGenericType(stItem));

        stRet = loadDef ?
            GenCallDefaultCtxId(codeGen, meth, sts, typeItem, call) :
            GenCallCtxId(codeGen, meth, sts, call);

        wrap = default;
        return true;
    }

    public static Tensor<T> Exec<T>(IEnumerable<T> src, ExecCtx ctx, int id)
    {
        if (src == null)
            return Tensor<T>.CreateFill(default, 0);

        long count;
        long dim;
        if (src is ICanCount counter && counter.TryGetCount(out count))
            dim = ClipDim(count, ctx, id);
        else
        {
            var items = new List<T>();
            foreach (var item in src)
            {
                ctx.Ping(id);
                items.Add(item);
            }
            dim = items.Count;
            src = items;
        }

        return Tensor<T>.CreateFrom(src, dim);
    }

    public static Tensor<T> Exec<T>(IEnumerable<T> src, T def, ExecCtx ctx, int id)
    {
        if (src == null)
            return Tensor<T>.CreateFill(def, 0);

        long count;
        long dim;
        if (src is ICanCount counter && counter.TryGetCount(out count))
            dim = ClipDim(count, ctx, id);
        else
        {
            var items = new List<T>();
            foreach (var item in src)
            {
                ctx.Ping(id);
                items.Add(item);
            }
            dim = items.Count;
            src = items;
        }

        return Tensor<T>.CreateFrom(def, src, dim);
    }

    public static Tensor<T> Exec<T>(IEnumerable<T> src, long dim, ExecCtx ctx, int id)
    {
        return Tensor<T>.CreateFrom(src, ClipDim(dim, ctx, id));
    }

    public static Tensor<T> Exec<T>(IEnumerable<T> src, long dim, T def, ExecCtx ctx, int id)
    {
        return Tensor<T>.CreateFrom(def, src, ClipDim(dim, ctx, id));
    }

    public static Tensor<T> Exec<T>(IEnumerable<T> src, long[] dims, ExecCtx ctx, int id)
    {
        for (int i = 0; i < dims.Length; i++)
            dims[i] = ClipDim(dims[i], ctx, id);
        return Tensor<T>.CreateFrom(src, dims);
    }

    public static Tensor<T> Exec<T>(IEnumerable<T> src, long[] dims, T def, ExecCtx ctx, int id)
    {
        for (int i = 0; i < dims.Length; i++)
            dims[i] = ClipDim(dims[i], ctx, id);
        return Tensor<T>.CreateFrom(def, src, dims);
    }
}

public sealed class TensorBuildGen : TensorGen<TensorBuildFunc>
{
    public static readonly TensorBuildGen Instance = new TensorBuildGen();

    [Flags]
    private enum Key : uint
    {
        /// <summary>
        /// Multiple dimensions, ie, rank bigger than one.
        /// </summary>
        Multi = 0x01,

        /// <summary>
        /// Whether the index scope is used.
        /// </summary>
        Indexed = 0x02,

        /// <summary>
        /// Whether the fill value is passed in.
        /// </summary>
        Fill = 0x04,
    }

    private readonly ReadOnly.Array<MethodInfo> _keyToMeth;

    private TensorBuildGen()
    {
        var meths = new MethodInfo[8];

        static int I(Key key)
        {
            int res = (int)key;
            Validation.AssertIndex(res, 8);
            return res;
        }

        meths[0] = new Func<IE, long, Func<O, long>, Func<O, O>, ExecCtx, int, Tensor<O>>(ExecOne).Method.GetGenericMethodDefinition();
        meths[I(Key.Multi)] = new Func<IE, long[], Func<O, long>[], Func<O, O>, ExecCtx, int, Tensor<O>>(ExecGen).Method.GetGenericMethodDefinition();
        meths[I(Key.Indexed)] = new Func<IE, long, Func<long, O, long>, Func<long, O, O>, ExecCtx, int, Tensor<O>>(ExecOneIdx).Method.GetGenericMethodDefinition();
        meths[I(Key.Multi | Key.Indexed)] = new Func<IE, long[], Func<long, O, long>[], Func<long, O, O>, ExecCtx, int, Tensor<O>>(ExecGenIdx).Method.GetGenericMethodDefinition();

        meths[I(Key.Fill)] = new Func<IE, long, Func<O, long>, Func<O, O>, O, ExecCtx, int, Tensor<O>>(ExecOneFill).Method.GetGenericMethodDefinition();
        meths[I(Key.Multi | Key.Fill)] = new Func<IE, long[], Func<O, long>[], Func<O, O>, O, ExecCtx, int, Tensor<O>>(ExecGenFill).Method.GetGenericMethodDefinition();
        meths[I(Key.Indexed | Key.Fill)] = new Func<IE, long, Func<long, O, long>, Func<long, O, O>, O, ExecCtx, int, Tensor<O>>(ExecOneIdxFill).Method.GetGenericMethodDefinition();
        meths[I(Key.Multi | Key.Indexed | Key.Fill)] = new Func<IE, long[], Func<long, O, long>[], Func<long, O, O>, O, ExecCtx, int, Tensor<O>>(ExecGenIdxFill).Method.GetGenericMethodDefinition();

        _keyToMeth = meths;
    }

    protected override RngTuple GetArrayRangesCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));

        int cdim = (call.Args.Length - 2) / 2;
        if (cdim <= 1)
            return RngTuple.Empty;

        return RngTuple.Create(new SlotRange(1, 1 + cdim), new SlotRange(1 + cdim, 1 + 2 * cdim));
    }

    protected override bool IsValidArrayRangesCore(BndCallNode call, RngTuple ranges)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(!ranges.IsDefault);

        int cdim = (call.Args.Length - 2) / 2;
        if (cdim <= 1)
            return ranges.IsDefaultOrEmpty;

        return ranges.Length == 2 &&
            ranges[0].Min == 1 && ranges[0].Count == cdim &&
            ranges[1].Min == 1 + cdim && ranges[1].Count == cdim;
    }

    protected override bool TryGenCodeCore(
        ICodeGen codeGen, BndCallNode call, RngTuple rngs, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(IsValidArrayRanges(call, rngs));

        DType type = call.Type;
        var args = call.Args;

        int carg = args.Length;
        bool hasFill = (carg & 1) != 0;
        int rank = (carg - 2) / 2;
        Validation.Assert(rank > 0);

        Validation.Assert(type.IsTensorReq);
        Validation.Assert(type.TensorRank == rank);

        DType typeItemDst = type.GetTensorItemType();
        Validation.Assert(typeItemDst == args[1 + 2 * rank].Type);
        Validation.Assert(typeItemDst == args[carg - 1].Type); // The fill value or selector.
        Type stItemDst = codeGen.GetSystemType(typeItemDst);
        stRet = codeGen.GetSystemType(type);

        Validation.Assert(args[0].Type.IsSequence);
        DType typeItemSrc = args[0].Type.ItemTypeOrThis;
        Type stItemSrc = codeGen.GetSystemType(typeItemSrc);

        Key key = 0;
        if (call.Indices[0] != null)
            key |= Key.Indexed;

        bool needDef = false;
        if (hasFill)
            key |= Key.Fill;
        else if (!typeItemDst.IsOpt && !stItemDst.IsValueType)
        {
            needDef = true;
            key |= Key.Fill;
        }

        if (rank == 1)
        {
            Validation.Assert(rngs.Length == 0);
            Validation.Assert(sts.Length == (hasFill ? 5 : 4));
        }
        else
        {
            key |= Key.Multi;
            Validation.Assert(rngs.Length == 2);
            Validation.Assert(sts.Length == (hasFill ? 5 : 4));
#if DEBUG
            Validation.Assert(sts[1] == typeof(long[]));
            Type stFnInd = (key & Key.Indexed) != 0 ?
                typeof(Func<,,>).MakeGenericType(typeof(long), stItemSrc, typeof(long)) :
                typeof(Func<,>).MakeGenericType(stItemSrc, typeof(long));
            Validation.Assert(sts[2] == stFnInd.MakeArrayType());
#endif
        }

        Validation.AssertIndex((int)key, _keyToMeth.Length);
        var meth = _keyToMeth[(int)key].VerifyValue().MakeGenericMethod(stItemSrc, stItemDst);
        Validation.Assert(meth.ReturnType == typeof(Tensor<>).MakeGenericType(stItemDst));

        stRet = needDef ?
            GenCallDefaultCtxId(codeGen, meth, sts, typeItemDst, call) :
            GenCallCtxId(codeGen, meth, sts, call);
        wrap = default;
        return true;
    }

    /// <summary>
    /// Rank one case.
    /// </summary>
    public static Tensor<TDst> ExecOne<TSrc, TDst>(
        IEnumerable<TSrc> src, long dim, Func<TSrc, long> fnInd, Func<TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        Validation.AssertValue(fnInd);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        dim = ClipDim(dim, ctx, id);
        if (dim == 0 || src == null)
            return Tensor<TDst>.CreateFill(default, dim);

        var bldr = Tensor<TDst>.Builder.Create(Shape.Create(dim));
        BuildOne(bldr, src, dim, fnInd, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    /// <summary>
    /// Rank one case, with fill.
    /// </summary>
    public static Tensor<TDst> ExecOneFill<TSrc, TDst>(
        IEnumerable<TSrc> src, long dim, Func<TSrc, long> fnInd, Func<TSrc, TDst> fnVal, TDst fill, ExecCtx ctx, int id)
    {
        Validation.AssertValue(fnInd);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        dim = ClipDim(dim, ctx, id);
        if (dim == 0 || src == null)
            return Tensor<TDst>.CreateFill(fill, dim);

        var bldr = Tensor<TDst>.Builder.Create(Shape.Create(dim), fill);
        BuildOne(bldr, src, dim, fnInd, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    private static void BuildOne<TSrc, TDst>(
        Tensor<TDst>.Builder bldr, IEnumerable<TSrc> src, long dim,
        Func<TSrc, long> fnInd, Func<TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var cur = ator.Current;
            long ind = fnInd(cur);
            if ((ulong)ind < (ulong)dim)
                bldr.Set(ind, fnVal(cur));
        }
    }

    /// <summary>
    /// Rank one case, indexed.
    /// </summary>
    public static Tensor<TDst> ExecOneIdx<TSrc, TDst>(
        IEnumerable<TSrc> src, long dim, Func<long, TSrc, long> fnInd, Func<long, TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        Validation.AssertValue(fnInd);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        dim = ClipDim(dim, ctx, id);
        if (dim == 0 || src == null)
            return Tensor<TDst>.CreateFill(default, dim);

        var bldr = Tensor<TDst>.Builder.Create(Shape.Create(dim));
        BuildOneIdx(bldr, src, dim, fnInd, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    /// <summary>
    /// Rank one case, indexed, with fill.
    /// </summary>
    public static Tensor<TDst> ExecOneIdxFill<TSrc, TDst>(
        IEnumerable<TSrc> src, long dim, Func<long, TSrc, long> fnInd, Func<long, TSrc, TDst> fnVal, TDst fill, ExecCtx ctx, int id)
    {
        Validation.AssertValue(fnInd);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        dim = ClipDim(dim, ctx, id);
        if (dim == 0 || src == null)
            return Tensor<TDst>.CreateFill(fill, dim);

        var bldr = Tensor<TDst>.Builder.Create(Shape.Create(dim), fill);
        BuildOneIdx(bldr, src, dim, fnInd, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    private static void BuildOneIdx<TSrc, TDst>(
        Tensor<TDst>.Builder bldr, IEnumerable<TSrc> src, long dim,
        Func<long, TSrc, long> fnInd, Func<long, TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        using var ator = src.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var cur = ator.Current;
            long ind = fnInd(idx, cur);
            if ((ulong)ind < (ulong)dim)
                bldr.Set(ind, fnVal(idx, cur));
        }
    }

    /// <summary>
    /// General rank.
    /// </summary>
    public static Tensor<TDst> ExecGen<TSrc, TDst>(
        IEnumerable<TSrc> src, long[] dims, Func<TSrc, long>[] fnInds, Func<TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        Validation.AssertValue(dims);
        Validation.AssertValue(fnInds);
        Validation.Assert(dims.Length == fnInds.Length);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        var shape = GetShape(dims, ctx, id, out long size);

        if (size == 0 || src == null)
            return Tensor<TDst>.CreateFill(default, shape);

        var bldr = Tensor<TDst>.Builder.Create(shape);
        BuildGen(bldr, src, shape, fnInds, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    /// <summary>
    /// General rank, with fill.
    /// </summary>
    public static Tensor<TDst> ExecGenFill<TSrc, TDst>(
        IEnumerable<TSrc> src, long[] dims, Func<TSrc, long>[] fnInds, Func<TSrc, TDst> fnVal, TDst fill, ExecCtx ctx, int id)
    {
        Validation.AssertValue(dims);
        Validation.AssertValue(fnInds);
        Validation.Assert(dims.Length == fnInds.Length);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        var shape = GetShape(dims, ctx, id, out long size);
        if (size == 0 || src == null)
            return Tensor<TDst>.CreateFill(fill, shape);

        var bldr = Tensor<TDst>.Builder.Create(shape, fill);
        BuildGen(bldr, src, shape, fnInds, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    private static void BuildGen<TSrc, TDst>(
        Tensor<TDst>.Builder bldr, IEnumerable<TSrc> src, Shape shape,
        Func<TSrc, long>[] fnInds, Func<TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        int rank = shape.Rank;
        Validation.Assert(rank >= 2);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var cur = ator.Current;
            long index = 0;
            long prev = 1;
            for (int d = 0; ; d++)
            {
                if (d >= rank)
                {
                    Validation.AssertIndex(index, bldr.Count);
                    bldr.Set(index, fnVal(cur));
                    break;
                }
                long iv = fnInds[d](cur);
                index = index * prev + iv;
                if ((ulong)iv >= (ulong)(prev = shape[d]))
                    break;
            }
        }
    }

    /// <summary>
    /// General rank, indexed.
    /// </summary>
    public static Tensor<TDst> ExecGenIdx<TSrc, TDst>(
        IEnumerable<TSrc> src, long[] dims, Func<long, TSrc, long>[] fnInds, Func<long, TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        Validation.AssertValue(dims);
        Validation.AssertValue(fnInds);
        Validation.Assert(dims.Length == fnInds.Length);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        var shape = GetShape(dims, ctx, id, out long size);

        if (size == 0 || src == null)
            return Tensor<TDst>.CreateFill(default, shape);

        var bldr = Tensor<TDst>.Builder.Create(shape);
        BuildGenIdx(bldr, src, shape, fnInds, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    /// <summary>
    /// General rank, indexed, with fill.
    /// </summary>
    public static Tensor<TDst> ExecGenIdxFill<TSrc, TDst>(
        IEnumerable<TSrc> src, long[] dims, Func<long, TSrc, long>[] fnInds, Func<long, TSrc, TDst> fnVal, TDst fill, ExecCtx ctx, int id)
    {
        Validation.AssertValue(dims);
        Validation.AssertValue(fnInds);
        Validation.Assert(dims.Length == fnInds.Length);
        Validation.AssertValue(fnVal);
        Validation.AssertValue(ctx);

        var shape = GetShape(dims, ctx, id, out long size);
        if (size == 0 || src == null)
            return Tensor<TDst>.CreateFill(fill, shape);

        var bldr = Tensor<TDst>.Builder.Create(shape, fill);
        BuildGenIdx(bldr, src, shape, fnInds, fnVal, ctx, id);
        return bldr.BuildGeneric();
    }

    private static void BuildGenIdx<TSrc, TDst>(
        Tensor<TDst>.Builder bldr, IEnumerable<TSrc> src, Shape shape,
        Func<long, TSrc, long>[] fnInds, Func<long, TSrc, TDst> fnVal, ExecCtx ctx, int id)
    {
        int rank = shape.Rank;
        Validation.Assert(rank >= 2);

        using var ator = src.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                break;
            var cur = ator.Current;
            long index = 0;
            long prev = 1;
            for (int d = 0; ; d++)
            {
                if (d >= rank)
                {
                    Validation.AssertIndex(index, bldr.Count);
                    bldr.Set(index, fnVal(idx, cur));
                    break;
                }
                long iv = fnInds[d](idx, cur);
                index = index * prev + iv;
                if ((ulong)iv >= (ulong)(prev = shape[d]))
                    break;
            }
        }
    }

    private static Shape GetShape(long[] dims, ExecCtx ctx, int id, out long size)
    {
        Validation.AssertValue(dims);
        Validation.AssertValue(ctx);

        int rank = dims.Length;
        Validation.Assert(rank >= 2);

        var bldrShape = Shape.CreateBuilder(rank);
        for (int i = 0; i < rank; i++)
        {
            long dim = ClipDim(dims[i], ctx, id);
            bldrShape[i] = dim;
        }

        Shape shape = bldrShape.ToImmutable();
        if (!shape.TryGetCount(out size))
            throw new InvalidOperationException("Tensor too large");
        return shape;
    }
}

public sealed class TensorShapeGen : TensorGen<TensorShapeFunc>
{
    public static TensorShapeGen Instance = new TensorShapeGen();

    private readonly MethodInfo _methShape;
    private readonly MethodInfo _methItem;

    private TensorShapeGen()
    {
        _methShape = typeof(Tensor).GetProperty("Shape").VerifyValue().GetGetMethod().VerifyValue();
        _methItem = typeof(Shape).GetProperty("Item", new Type[] { typeof(int) }).VerifyValue().GetGetMethod().VerifyValue();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType type = call.Args[0].Type;
        DType typeRes = call.Type;
        int rank = type.TensorRank;

        Validation.Assert(typeof(Tensor).IsAssignableFrom(sts[0]));
        Type stRes = codeGen.GetSystemType(typeRes);

        using var locShape = codeGen.AcquireLocal(typeof(Shape));
        var ilw = codeGen.Writer;
        ilw
            .Call(_methShape)
            .Stloc(locShape);
        codeGen.GenCreateTuple(typeRes, stRes);
        for (int slot = 0; slot < rank; slot++)
        {
            ilw
                .Dup()
                .Ldloca(locShape)
                .Ldc_I4(slot)
                .Call(_methItem);
            codeGen.GenStoreSlot(typeRes, stRes, slot, DType.I8Req);
        }

        stRet = stRes;
        return true;
    }
}

public sealed class TensorValuesGen : GetMethGen<TensorValuesFunc>
{
    public static readonly TensorValuesGen Instance = new TensorValuesGen();

    private readonly MethodInfo _meth = new Func<Tensor<object>, IEnumerable<object>>(Exec).Method.GetGenericMethodDefinition();

    private TensorValuesGen()
    {
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var typeTen = call.Args[0].Type;
        Validation.Assert(typeTen.IsTensorXxx);
        var st = codeGen.GetSystemType(typeTen.GetTensorItemType());
        meth = _meth.MakeGenericMethod(st);
        return true;
    }

    private static IEnumerable<T> Exec<T>(Tensor<T> ten)
    {
        if (ten == null)
            return null;
        return ten.GetValuesWithCount();
    }
}

public sealed class TensorReshapeGen : TensorGen<TensorReshapeFunc>
{
    public static readonly TensorReshapeGen Instance = new TensorReshapeGen();

    private readonly ReadOnly.Array<MethodInfo> _meths;
    private readonly ReadOnly.Array<MethodInfo> _methsDef;
    private readonly MethodInfo _methRng;
    private readonly MethodInfo _methRngDef;

    private TensorReshapeGen()
    {
        _meths = new[]
        {
            new Func<Tensor<byte>, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methsDef = new[]
        {
            new Func<Tensor<byte>, byte, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, byte, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methRng = new Func<Tensor<byte>, long[], ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
        _methRngDef = new Func<Tensor<byte>, long[], byte, ExecCtx, int, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        // REVIEW: Use the ExecCtx to report runtime warnings on the dimensions.
        return true;
    }

    protected override RngTuple GetArrayRangesCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));

        int cdim = call.Args.Length - 1;
        if (cdim < _meths.Length)
            return RngTuple.Empty;

        return RngTuple.Create(new SlotRange(1, 1 + cdim));
    }

    protected override bool IsValidArrayRangesCore(BndCallNode call, RngTuple ranges)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(!ranges.IsDefault);

        int cdim = call.Args.Length - 1;
        if (cdim < _meths.Length)
            return ranges.IsDefaultOrEmpty;

        return ranges.Length == 1 && ranges[0].Min == 1 && ranges[0].Count == cdim;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, RngTuple rngs,
        ReadOnly.Array<Type> sts, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(IsValidArrayRanges(call, rngs));

        DType typeDst = call.Type;
        var args = call.Args;

        Validation.Assert(typeDst.IsTensorReq);
        DType typeItem = typeDst.GetTensorItemType();

        DType typeSrc = args[0].Type;
        Validation.Assert(typeSrc.IsTensorReq);
        Validation.Assert(typeItem == typeSrc.GetTensorItemType());

        Type stItem = codeGen.GetSystemType(typeItem);

        var loadDef = !typeItem.IsOpt && !stItem.IsValueType;

        int cdim = args.Length - 1;
        Validation.Assert(typeDst.TensorRank == cdim);

        MethodInfo meth;
        if (cdim < _meths.Length)
        {
            Validation.Assert(sts.Length == 1 + cdim);
            Validation.Assert(rngs.IsDefaultOrEmpty);
            meth = loadDef ? _methsDef[cdim] : _meths[cdim];
        }
        else
        {
            Validation.Assert(sts.Length == 2);
            Validation.Assert(rngs.Length == 1);
            meth = loadDef ? _methRngDef : _methRng;
        }
        meth = meth.MakeGenericMethod(stItem);
        Validation.Assert(meth.ReturnType == typeof(Tensor<>).MakeGenericType(stItem));

        stRet = loadDef ?
            GenCallDefaultCtxId(codeGen, meth, sts, typeItem, call) :
            GenCallCtxId(codeGen, meth, sts, call);
        wrap = default;
        return true;
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        if (src.Count == 1)
            return src.Reshape();

        // REVIEW: Report the mismatch.
        return Tensor<T>.CreateFill(src.GetFirstOrDefault());
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, T def, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        if (src.Count == 1)
            return src.Reshape();

        // REVIEW: Report the mismatch.
        return Tensor<T>.CreateFill(src.Count == 0 ? def : src.GetFirst());
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long dim, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        long d0 = ClipDim(dim, ctx, id);
        if (src.Count == d0)
            return src.Reshape(d0);

        // REVIEW: What about oom?
        // REVIEW: Report the mismatch.
        return Tensor<T>.CreateFrom(src.GetValues(), d0);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long dim, T def, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        long d0 = ClipDim(dim, ctx, id);
        if (src.Count == d0)
            return src.Reshape(d0);

        // REVIEW: What about oom?
        // REVIEW: Report the mismatch.
        return Tensor<T>.CreateFrom(def, src.GetValues(), d0);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long[] dims, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        for (int i = 0; i < dims.Length; i++)
            dims[i] = ClipDim(dims[i], ctx, id);

        long count = src.Count;
        bool same = true;
        for (int i = 0; i < dims.Length; i++)
        {
            long d = dims[i];
            Validation.Assert(d >= 0);
            if (d == 0)
            {
                if (count != 0)
                {
                    same = false;
                    break;
                }
                continue;
            }

            if (count % d != 0)
            {
                same = false;
                break;
            }
            count /= d;
        }
        Validation.Assert(count >= 0);
        Validation.Coverage(count == 0 || count == 1 ? 1 : 0);
        if (count > 1)
            same = false;

        if (same)
            return src.Reshape(dims);

        // REVIEW: What about oom?
        // REVIEW: Report the mismatch.
        return Tensor<T>.CreateFrom(src.GetValues(), dims);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long[] dims, T def, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        for (int i = 0; i < dims.Length; i++)
            dims[i] = ClipDim(dims[i], ctx, id);

        long count = src.Count;
        bool same = true;
        for (int i = 0; i < dims.Length; i++)
        {
            long d = dims[i];
            Validation.Assert(d >= 0);
            if (d == 0)
            {
                if (count != 0)
                {
                    same = false;
                    break;
                }
                continue;
            }

            if (count % d != 0)
            {
                same = false;
                break;
            }
            count /= d;
        }
        Validation.Assert(count >= 0);
        Validation.Coverage(count == 0 || count == 1 ? 1 : 0);
        if (count > 1)
            same = false;

        if (same)
            return src.Reshape(dims);

        // REVIEW: What about oom?
        // REVIEW: Report the mismatch.
        return Tensor<T>.CreateFrom(def, src.GetValues(), dims);
    }
}

public sealed class TensorTransposeGen : TensorGen<TensorTransposeFunc>
{
    public static readonly TensorTransposeGen Instance = new TensorTransposeGen();

    private readonly MethodInfo _methReverse;
    private readonly ReadOnly.Array<MethodInfo> _methsConst;
    private readonly MethodInfo _methConstArr;
    private readonly ReadOnly.Array<MethodInfo> _meths;
    private readonly MethodInfo _methArr;

    private TensorTransposeGen()
    {
        _methReverse = new Func<Tensor<byte>, Tensor<byte>>(ExecReverse).Method.GetGenericMethodDefinition();
        _methsConst = new[]
        {
            new Func<Tensor<byte>, int, int, Tensor<byte>>(ExecConst).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, int, int, int, Tensor<byte>>(ExecConst).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, int, int, int, int, Tensor<byte>>(ExecConst).Method.GetGenericMethodDefinition(),
        };
        _methConstArr = new Func<Tensor<byte>, int[], Tensor<byte>>(ExecConst).Method.GetGenericMethodDefinition();
        _meths = new[]
        {
            new Func<Tensor<byte>, long, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, long, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, long, long, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methArr = new Func<Tensor<byte>, long[], Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        DType type = call.Type;
        var args = call.Args;

        if (!type.IsTensorReq || args[0].Type != type || !codeGen.TypeManager.TryEnsureSysType(type, out stRet))
        {
            Validation.Assert(false);
            return base.TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);
        }

        DType typeItem = type.GetTensorItemType();
        Type stItem = codeGen.GetSystemType(typeItem);
        Validation.Assert(stRet == typeof(Tensor<>).MakeGenericType(stItem));

        int rank = type.TensorRank;
        int given = args.Length - 1;

        int cur = idx + 1;

        MethodInfo meth;
        var ilw = codeGen.Writer;
        if (given <= 0)
        {
            codeGen.GenCode(args[0], ref cur);
            meth = _methReverse.MakeGenericMethod(stItem);
            Validation.Assert(meth.ReturnType == stRet);
            ilw.Call(meth);
            wrap = default;
            Validation.Assert(cur == idx + call.NodeCount);
            return true;
        }

        if (rank != given || rank < 2)
        {
            // Should have been handled by the binder/reducer.
            Validation.Assert(false);
            return base.TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);
        }

        // See if all indices are constant.
        long[] inds = null;
        if (args[1].Kind == BndNodeKind.Int)
        {
            inds = new long[rank];
            for (int i = 0; i < rank; i++)
            {
                if (!args[i + 1].TryGetIntegral(out var v))
                {
                    inds = null;
                    break;
                }
                inds[i] = (long)v;
            }
        }

        codeGen.GenCode(args[0], ref cur);
        if (inds != null)
        {
            // Constant indices (the common case).
            Validation.Assert(inds.Length == rank);

            var perm = CleansePerm(inds);
            if (perm == null)
            {
                // Identity permutation.
                wrap = default;
                return true;
            }

            int index = rank - 2;
            Validation.Assert(index >= 0);
            if (index < _methsConst.Length)
            {
                for (int i = 0; i < rank; i++)
                    ilw.Ldc_I4(perm[i]);
                meth = _methsConst[index];
            }
            else
            {
                codeGen.GenLoadConst(perm);
                meth = _methConstArr;
            }
        }
        else
        {
            int index = rank - 2;
            Validation.Assert(index >= 0);
            if (index < _meths.Length)
            {
                for (int i = 1; i <= rank; i++)
                    codeGen.GenCode(args[i], ref cur);
                meth = _meths[index];
            }
            else
            {
                cur = GenI8Array(codeGen, codeGen.EnsureIdRange(call, 1), args, 1, args.Length, clip: false, cur);
                meth = _methArr;
            }
            Validation.Assert(cur == idx + call.NodeCount);
        }

        meth = meth.MakeGenericMethod(stItem);
        Validation.Assert(meth.ReturnType == stRet);
        ilw.Call(meth);

        wrap = default;
        return true;
    }

    public static Tensor<T> ExecReverse<T>(Tensor<T> src)
    {
        Validation.AssertValue(src);
        return src.Transpose();
    }

    public static Tensor<T> ExecConst<T>(Tensor<T> src, int i0, int i1)
    {
        Validation.AssertValue(src);
        return src.Transpose(i0, i1);
    }

    public static Tensor<T> ExecConst<T>(Tensor<T> src, int i0, int i1, int i2)
    {
        Validation.AssertValue(src);
        return src.Transpose(i0, i1, i2);
    }

    public static Tensor<T> ExecConst<T>(Tensor<T> src, int i0, int i1, int i2, int i3)
    {
        Validation.AssertValue(src);
        return src.Transpose(i0, i1, i2, i3);
    }

    public static Tensor<T> ExecConst<T>(Tensor<T> src, int[] perm)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(perm);
        return src.Transpose(perm);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0, long i1)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 2);

        uint a = (ulong)i0 < 2 ? (uint)i0 + 1 : 0;
        uint b = (ulong)i1 < 2 ? (uint)i1 + 1 : 0;

        // Rank 2 has two permutations, the identity and reverse.
        if (a == 2 || b == 1)
            return src.Transpose();
        return src;
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0, long i1, long i2)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 3);

        uint a = (ulong)i0 < 3 ? (uint)i0 + 1 : 0;
        uint b = (ulong)i1 < 3 ? (uint)i1 + 1 : 0;
        uint c = (ulong)i2 < 3 ? (uint)i2 + 1 : 0;

        if (b == a)
            b = 0;
        if (c == a || c == b)
            c = 0;

        // Get the missing values.
        uint left = 0;
        uint mask = ((1u << (int)a) | (1u << (int)b) | (1u << (int)c)) >> 1;
        Validation.Assert(mask <= 0b111);
        switch (mask)
        {
        case 0b000:
            // All are blank, so the result is the identity permutation.
            return src;
        case 0b001: left = 0x32; break;
        case 0b010: left = 0x31; break;
        case 0b011: left = 0x03; break;
        case 0b100: left = 0x21; break;
        case 0b101: left = 0x02; break;
        case 0b110: left = 0x01; break;
        case 0b111: break;
        default:
            Validation.Assert(false);
            break;
        }

        if (left != 0)
        {
            // Fill in the blanks.
            if (a == 0) { a = left & 0x0F; left >>= 4; }
            if (b == 0) { b = left & 0x0F; left >>= 4; }
            if (c == 0) { c = left & 0x0F; left >>= 4; }
        }
        Validation.Assert(left == 0);
        Validation.Assert(a * b * c == 6);

        return src.Transpose((int)a - 1, (int)b - 1, (int)c - 1);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0, long i1, long i2, long i3)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 4);

        uint a = (ulong)i0 < 4 ? (uint)i0 + 1 : 0;
        uint b = (ulong)i1 < 4 ? (uint)i1 + 1 : 0;
        uint c = (ulong)i2 < 4 ? (uint)i2 + 1 : 0;
        uint d = (ulong)i3 < 4 ? (uint)i3 + 1 : 0;

        if (b == a)
            b = 0;
        if (c == a || c == b)
            c = 0;
        if (d == a || d == b || d == c)
            d = 0;

        // Get the missing values.
        uint left = 0;
        uint mask = ((1u << (int)a) | (1u << (int)b) | (1u << (int)c) | (1u << (int)d)) >> 1;
        Validation.Assert(mask <= 0b1111);
        switch (mask)
        {
        case 0b000:
            // All are blank, so the result is the identity permutation.
            return src;
        case 0b0001: left = 0x432; break;
        case 0b0010: left = 0x431; break;
        case 0b0011: left = 0x043; break;
        case 0b0100: left = 0x421; break;
        case 0b0101: left = 0x042; break;
        case 0b0110: left = 0x041; break;
        case 0b0111: left = 0x004; break;
        case 0b1000: left = 0x321; break;
        case 0b1001: left = 0x032; break;
        case 0b1010: left = 0x031; break;
        case 0b1011: left = 0x003; break;
        case 0b1100: left = 0x021; break;
        case 0b1101: left = 0x002; break;
        case 0b1110: left = 0x001; break;
        case 0b1111: break;
        default:
            Validation.Assert(false);
            break;
        }

        if (left != 0)
        {
            // Fill in the blanks.
            if (a == 0) { a = left & 0x0F; left >>= 4; }
            if (b == 0) { b = left & 0x0F; left >>= 4; }
            if (c == 0) { c = left & 0x0F; left >>= 4; }
            if (d == 0) { d = left & 0x0F; left >>= 4; }
        }
        Validation.Assert(left == 0);
        Validation.Assert(a * b * c * d == 24);

        return src.Transpose((int)a - 1, (int)b - 1, (int)c - 1, (int)d - 1);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long[] inds)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(inds);
        Validation.Assert(src.Rank == inds.Length);

        // Need to validate and potentially fix the permutation.
        var perm = CleansePerm(inds);
        if (perm == null)
            return src;
        return src.Transpose(perm);
    }

    /// <summary>
    /// Cleanse the permutation. Anything outside the range or that is a duplicate
    /// is replaced with "blank". Then blanks are filled in with unused values in order.
    /// Returns null if the result is the identity permutation.
    /// </summary>
    private static int[] CleansePerm(long[] inds)
    {
        int rank = inds.Length;
        var perm = new int[rank];

        BitSet have = default;
        BitSet need = default;
        for (int i = 0; i < rank; i++)
        {
            long v = inds[i];
            int k;
            if ((ulong)v < (ulong)rank && !have.TestBit(k = (int)v))
            {
                perm[i] = k;
                have = have.SetBit(k);
            }
            else
            {
                perm[i] = -1;
                need = need.SetBit(i);
            }
        }
        Validation.Assert(have.Count + need.Count == rank);

        if (have.IsEmpty)
            return null;

        // Fill in any blanks.
        int iv = 0;
        foreach (var i in need)
        {
            Validation.Assert(perm[i] < 0);
            while (have.TestBit(iv))
                iv++;
            Validation.Assert(iv < rank);
            perm[i] = iv;
            iv++;
        }

        // Check for the identity.
        for (int i = 0; i < rank; i++)
        {
            if (perm[i] != i)
                return perm;
        }

        // Identity.
        return null;
    }
}

public sealed class TensorExpandDimsGen : TensorGen<TensorExpandDimsFunc>
{
    public static readonly TensorExpandDimsGen Instance = new TensorExpandDimsGen();

    private readonly MethodInfo _methBits;
    private readonly MethodInfo _methBitsU4;
    private readonly MethodInfo _methBitsU8;
    private readonly ReadOnly.Array<MethodInfo> _meths;
    private readonly MethodInfo _methArr;

    private TensorExpandDimsGen()
    {
        _methBits = new Func<Tensor<byte>, BitSet, Tensor<byte>>(ExecBits).Method.GetGenericMethodDefinition();
        _methBitsU4 = new Func<Tensor<byte>, uint, Tensor<byte>>(ExecBits).Method.GetGenericMethodDefinition();
        _methBitsU8 = new Func<Tensor<byte>, ulong, Tensor<byte>>(ExecBits).Method.GetGenericMethodDefinition();
        _meths = new[]
        {
            new Func<Tensor<byte>, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, long, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
            new Func<Tensor<byte>, long, long, long, long, Tensor<byte>>(Exec).Method.GetGenericMethodDefinition(),
        };
        _methArr = new Func<Tensor<byte>, long[], Tensor<byte>>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        DType type = call.Type;
        var args = call.Args;

        if (!type.IsTensorReq || !args[0].Type.IsTensorReq || !codeGen.TypeManager.TryEnsureSysType(type, out stRet))
        {
            Validation.Assert(false);
            return base.TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);
        }

        int added = args.Length - 1;
        Validation.Assert(added > 0);

        int rankSrc = args[0].Type.TensorRank;
        int rankDst = rankSrc + added;

        if (rankDst != type.TensorRank)
        {
            Validation.Assert(false);
            return base.TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);
        }

        DType typeItem = type.GetTensorItemType();
        if (typeItem != args[0].Type.GetTensorItemType())
        {
            Validation.Assert(false);
            return base.TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);
        }
        Type stItem = codeGen.GetSystemType(typeItem);
        Validation.Assert(stRet == typeof(Tensor<>).MakeGenericType(stItem));

        // See if all indices are constant and distinct (a very common case).
        BitSet slots = default;
        for (int i = 1; i < args.Length; i++)
        {
            var ind = args[i];
            if (ind.TryGetIntegral(out var v) && 0 <= v && v < rankDst)
                slots = slots.SetBit((int)v);
        }
        Validation.Assert(slots.Count <= added);

        int cur = idx + 1;

        codeGen.GenCode(args[0], ref cur);
        var ilw = codeGen.Writer;

        MethodInfo meth;
        if (slots.Count == added)
        {
            // All constant and distinct.
            if (!slots.IsLo)
            {
                // REVIEW: Unfortunately, this boxes, but it shouldn't really matter.
                codeGen.GenLoadConst<object>(slots);
                ilw.Unbox_Any(typeof(BitSet));
                meth = _methBits;
            }
            else
            {
                var lo = slots.LoBlob;
                uint small = (uint)lo;
                if (small == lo)
                {
                    ilw.Ldc_U4(small);
                    meth = _methBitsU4;
                }
                else
                {
                    ilw.Ldc_U8(lo);
                    meth = _methBitsU8;
                }
            }
        }
        else
        {
            if (added <= _meths.Length)
            {
                for (int i = 1; i <= added; i++)
                    codeGen.GenCode(args[i], ref cur);
                meth = _meths[added - 1];
            }
            else
            {
                cur = GenI8Array(codeGen, codeGen.EnsureIdRange(call, 1), args, 1, args.Length, clip: false, cur);
                meth = _methArr;
            }
            Validation.Assert(cur == idx + call.NodeCount);
        }

        meth = meth.MakeGenericMethod(stItem);
        Validation.Assert(meth.ReturnType == stRet);

        // REVIEW: Perhaps use GenCall?
        ilw.Call(meth);

        wrap = default;
        return true;
    }

    public static Tensor<T> ExecBits<T>(Tensor<T> src, uint slots)
    {
        Validation.AssertValue(src);
        return src.ExpandDims(slots);
    }

    public static Tensor<T> ExecBits<T>(Tensor<T> src, ulong slots)
    {
        Validation.AssertValue(src);
        return src.ExpandDims(slots);
    }

    public static Tensor<T> ExecBits<T>(Tensor<T> src, BitSet slots)
    {
        Validation.AssertValue(src);
        return src.ExpandDims(slots);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0)
    {
        Validation.AssertValue(src);
        int rank = src.Rank;
        i0 = i0.Clamp(0, rank);
        return src.ExpandDims((int)i0);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0, long i1)
    {
        Validation.AssertValue(src);
        if (i0 > i1)
            Util.Swap(ref i0, ref i1);
        int rank = src.Rank;
        i0 = i0.Clamp(0, rank);
        i1 = i1.Clamp(i0 + 1, rank + 1);
        return src.ExpandDims((int)i0, (int)i1);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0, long i1, long i2)
    {
        Validation.AssertValue(src);
        if (i0 > i1)
            Util.Swap(ref i0, ref i1);
        if (i0 > i2)
            Util.Swap(ref i0, ref i2);
        if (i1 > i2)
            Util.Swap(ref i1, ref i2);
        int rank = src.Rank;
        i0 = i0.Clamp(0, rank);
        i1 = i1.Clamp(i0 + 1, rank + 1);
        i2 = i2.Clamp(i1 + 1, rank + 2);
        return src.ExpandDims((int)i0, (int)i1, (int)i2);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long i0, long i1, long i2, long i3)
    {
        Validation.AssertValue(src);
        if (i0 > i1)
            Util.Swap(ref i0, ref i1);
        if (i0 > i2)
            Util.Swap(ref i0, ref i2);
        if (i0 > i3)
            Util.Swap(ref i0, ref i3);
        if (i1 > i2)
            Util.Swap(ref i1, ref i2);
        if (i1 > i3)
            Util.Swap(ref i1, ref i3);
        if (i2 > i3)
            Util.Swap(ref i2, ref i3);
        int rank = src.Rank;
        i0 = i0.Clamp(0, rank);
        i1 = i1.Clamp(i0 + 1, rank + 1);
        i2 = i2.Clamp(i1 + 1, rank + 2);
        i3 = i3.Clamp(i2 + 1, rank + 3);
        return src.ExpandDims((int)i0, (int)i1, (int)i2, (int)i3);
    }

    public static Tensor<T> Exec<T>(Tensor<T> src, long[] slots)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(slots);
        int len = slots.Length;
        int rank = src.Rank;
        int rankNew = rank + len;
        Validation.Assert(rankNew > rank);
        Validation.Assert(len > 4);

        Array.Sort(slots);
        BitSet bs = default;
        int cur = (int)slots[0].Clamp(0, rank);
        bs = bs.SetBit(cur);
        for (int i = 1; i < len; i++)
        {
            cur = (int)slots[i].Clamp(cur + 1, rank + i);
            bs = bs.SetBit(cur);
        }
        Validation.Assert(bs.Count == len);
        return src.ExpandDims(bs);
    }
}

public sealed class TensorDotGen : TensorGen<TensorDotFunc>
{
    public static readonly TensorDotGen Instance = new TensorDotGen();

    private delegate Tensor<T> Op<T>(Tensor<T> ten0, Tensor<T> ten1, int d0, int d1, out bool shrunk);

    private readonly Dictionary<DKind, MethodInfo> _meths;

    private TensorDotGen()
    {
        _meths = new Dictionary<DKind, MethodInfo>();
        _meths.Add(DKind.R8, new Op<double>(Tensor.Dot).Method);
        _meths.Add(DKind.R4, new Op<float>(Tensor.Dot).Method);
        _meths.Add(DKind.IA, new Op<Integer>(Tensor.Dot).Method);
        _meths.Add(DKind.I8, new Op<long>(Tensor.Dot).Method);
        _meths.Add(DKind.I4, new Op<int>(Tensor.Dot).Method);
        _meths.Add(DKind.I2, new Op<short>(Tensor.Dot).Method);
        _meths.Add(DKind.I1, new Op<sbyte>(Tensor.Dot).Method);
        _meths.Add(DKind.U8, new Op<ulong>(Tensor.Dot).Method);
        _meths.Add(DKind.U4, new Op<uint>(Tensor.Dot).Method);
        _meths.Add(DKind.U2, new Op<ushort>(Tensor.Dot).Method);
        _meths.Add(DKind.U1, new Op<byte>(Tensor.Dot).Method);
        _meths.Add(DKind.Bit, new Op<bool>(Tensor.Dot).Method);
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        // REVIEW: We should need it when the shapes don't match precisely.
        return false;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType type = call.Type;
        DType type0 = call.Args[0].Type;
        DType type1 = call.Args[1].Type;
        DType typeItem = type.GetTensorItemType();

        int rank = type.TensorRank;
        int rank0 = type0.TensorRank;
        int rank1 = type1.TensorRank;

        // Rank zero should be reduced to pointwise mul.
        Validation.Assert(rank0 > 0);
        Validation.Assert(rank1 > 0);
        Validation.Assert(rank == rank0 + rank1 - 2);

        if (!_meths.TryGetValue(typeItem.Kind, out var meth))
            return base.TryGenCodeCore(codeGen, call, sts, out stRet);

        stRet = meth.ReturnType;
        Validation.Assert(sts[0] == stRet);
        Validation.Assert(sts[1] == stRet);

        using var locShrunk = codeGen.AcquireLocal(typeof(bool));
        var ilw = codeGen.Writer;
        ilw
            .Ldc_I4(rank0 - 1)
            .Ldc_I4(Math.Max(0, rank1 - 2))
            .Ldloca(locShrunk)
            .Call(meth);

        // REVIEW: Determine whether shrinkage is possible and generate code accordingly.
        // That is, shrinkage won't happen if the input types are "fixed" and match.

        return true;
    }
}

public sealed class TensorPointWiseGen : TensorGen<TensorPointWiseFunc>
{
    public static readonly TensorPointWiseGen Instance = new TensorPointWiseGen();

    private delegate Tensor<T> Op<T>(Tensor<T> ten0, Tensor<T> ten1, out bool shrunk);

    private readonly Dictionary<ushort, MethodInfo> _meths;

    private static ushort GetKey(TensorPointWiseFunc.Bop bop, DKind kind)
    {
        return (ushort)((((uint)(byte)kind) << 8) | (byte)bop);
    }

    private TensorPointWiseGen()
    {
        _meths = new Dictionary<ushort, MethodInfo>();

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.R8), new Op<double>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.R4), new Op<float>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.IA), new Op<Integer>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.I8), new Op<long>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.I4), new Op<int>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.I2), new Op<short>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.I1), new Op<sbyte>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.U8), new Op<ulong>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.U4), new Op<uint>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.U2), new Op<ushort>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.U1), new Op<byte>(Tensor.Add).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Add, DKind.Bit), new Op<bool>(Tensor.AddSub).Method);

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.R8), new Op<double>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.R4), new Op<float>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.IA), new Op<Integer>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.I8), new Op<long>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.I4), new Op<int>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.I2), new Op<short>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.I1), new Op<sbyte>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.U8), new Op<ulong>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.U4), new Op<uint>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.U2), new Op<ushort>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.U1), new Op<byte>(Tensor.Sub).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Sub, DKind.Bit), new Op<bool>(Tensor.AddSub).Method);

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.R8), new Op<double>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.R4), new Op<float>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.IA), new Op<Integer>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.I8), new Op<long>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.I4), new Op<int>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.I2), new Op<short>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.I1), new Op<sbyte>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.U8), new Op<ulong>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.U4), new Op<uint>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.U2), new Op<ushort>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.U1), new Op<byte>(Tensor.Mul).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Mul, DKind.Bit), new Op<bool>(Tensor.MulDivMin).Method);

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.R8), new Op<double>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.R4), new Op<float>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.IA), new Op<Integer>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.I8), new Op<long>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.I4), new Op<int>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.I2), new Op<short>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.I1), new Op<sbyte>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.U8), new Op<ulong>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.U4), new Op<uint>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.U2), new Op<ushort>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.U1), new Op<byte>(Tensor.Min).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Min, DKind.Bit), new Op<bool>(Tensor.MulDivMin).Method);

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.R8), new Op<double>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.R4), new Op<float>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.IA), new Op<Integer>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.I8), new Op<long>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.I4), new Op<int>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.I2), new Op<short>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.I1), new Op<sbyte>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.U8), new Op<ulong>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.U4), new Op<uint>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.U2), new Op<ushort>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.U1), new Op<byte>(Tensor.Max).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.Max, DKind.Bit), new Op<bool>(Tensor.Max).Method);

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivFrac, DKind.R8), new Op<double>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivFrac, DKind.R4), new Op<float>(Tensor.Div).Method);

        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.IA), new Op<Integer>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.I8), new Op<long>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.I4), new Op<int>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.I2), new Op<short>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.I1), new Op<sbyte>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.U8), new Op<ulong>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.U4), new Op<uint>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.U2), new Op<ushort>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.U1), new Op<byte>(Tensor.Div).Method);
        _meths.Add(GetKey(TensorPointWiseFunc.Bop.DivInt, DKind.Bit), new Op<bool>(Tensor.MulDivMin).Method);
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        // REVIEW: We should need it when the shapes don't match precisely.
        return false;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        DType type = call.Type;
        DType type0 = call.Args[0].Type;
        DType type1 = call.Args[1].Type;
        DType typeItem = type.GetTensorItemType();

        int rank = type.TensorRank;
        int rank0 = type0.TensorRank;
        int rank1 = type1.TensorRank;

        if (!_meths.TryGetValue(GetKey(fn.Operator, typeItem.Kind), out var meth))
        {
            Validation.Assert(false);
            stRet = default;
            return false;
        }

        Validation.Assert(meth != null);

        bool canShrink;
        if (rank0 == 0)
        {
            Validation.Assert(type == type1);
            canShrink = false;
        }
        else if (rank1 == 0)
        {
            Validation.Assert(type == type0);
            canShrink = false;
        }
        else
        {
            Validation.Assert(rank == Math.Max(rank0, rank1));
            // REVIEW: Determine whether shrinkage is possible and generate code accordingly.
            // That is, shrinkage won't happen if the input types are "fixed" and either match or
            // broadcast.
            canShrink = true;
        }

        stRet = meth.ReturnType;
        Validation.Assert(sts[0] == stRet);
        Validation.Assert(sts[1] == stRet);

        using var locShrunk = codeGen.AcquireLocal(typeof(bool));
        var ilw = codeGen.Writer;
        ilw
            .Ldloca(locShrunk)
            .Call(meth);

        if (canShrink)
        {
            // REVIEW: Generate code to test locShrunk and issue a warning if true.
        }

        return true;
    }
}
