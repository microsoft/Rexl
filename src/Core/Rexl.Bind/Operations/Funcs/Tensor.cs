// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using ArgTuple = Immutable.Array<BoundNode>;
using TypeTuple = Immutable.Array<DType>;

/// <summary>
/// Base class for tensor functions. Holds utilities that are used by multiple tensor functions.
/// </summary>
public abstract partial class TensorFunc : RexlOper
{
    private protected TensorFunc(string name, int arityMin, int arityMax)
        : base(isFunc: true, new DName(name), BindUtil.TensorNs, arityMin, arityMax)
    {
    }

    /// <summary>
    /// Used by SpecializeTypesCore to process slots containing shape/dimension values.
    /// Sets those slots to i8 type and returns the resulting tensor type.
    /// </summary>
    private protected static DType ProcessShapeArgs(DType typeItem, TypeTuple.Builder types, int slotMin, int slotLim)
    {
        // REVIEW: Support slots being tuple-valued.
        Validation.AssertIndexInclusive(slotMin, slotLim);
        int rank = slotLim - slotMin;
        for (int dim = 0; dim < rank; dim++)
            types[dim + slotMin] = DType.I8Req;
        return typeItem.ToTensor(false, rank);
    }

    /// <summary>
    /// Used by ReduceCore to process slots containing shape/dimension values.
    /// For any constant dims, ensures they are non-negative.
    /// </summary>
    private protected static ArgTuple.Builder ReduceShapeSlots(IReducer reducer, ArgTuple args, int slotMin, int slotLim)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(!args.IsDefault);

        // REVIEW: Support slots being tuple-valued.
        Validation.AssertIndexInclusive(slotLim, args.Length);
        Validation.AssertIndexInclusive(slotMin, slotLim);

        ArgTuple.Builder bldr = null;
        for (int slot = slotMin; slot < slotLim; slot++)
        {
            var arg = args[slot];
            if (arg.TryGetIntegral(out var val))
            {
                if (val.Sign < 0)
                {
                    reducer.Warn(arg, ErrorStrings.WrnTensorDimTooSmall);
                    bldr ??= args.ToBuilder();
                    bldr[slot] = BndIntNode.Create(DType.I8Req, 0);
                }
            }
        }

        return bldr;
    }

    /// <summary>
    /// If the shape arguments are constant, construct the shape.
    /// This method expects ReduceShapeSlots to have been called beforehand.
    /// </summary>
    private protected static bool TryGetShape(ArgTuple args, int slotMin, out Shape shape)
    {
        Validation.AssertIndex(slotMin, args.Length);

        if (!args[slotMin].TryGetI8(out long dim))
        {
            shape = default;
            return false;
        }
        Validation.Assert(dim >= 0);

        int rank = args.Length - slotMin;
        var bldr = Shape.CreateBuilder(rank);
        bldr[0] = dim;
        for (int i = 1; i < rank; i++)
        {
            if (!args[i + slotMin].TryGetI8(out dim))
            {
                shape = default;
                return false;
            }
            Validation.Assert(dim >= 0);
            bldr[i] = dim;
        }

        shape = bldr.ToImmutable();
        return true;
    }
}

/// <summary>
/// Tensor.Fill creates a constant tensor, where all the cells contain the same value.
/// Usage: <c>Tensor.Fill(value, dims...)</c>.
/// When no dims are present, the result is a rank zero tensor.
/// </summary>
public sealed partial class TensorFillFunc : TensorFunc
{
    public static readonly TensorFillFunc Instance = new TensorFillFunc();

    private TensorFillFunc()
        : base("Fill", 1, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        // REVIEW: Should we lift over the 0/value slot? Will we never need tensors of
        // sequence or opt type?
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        var typeItem = types[0];
        var typeRes = ProcessShapeArgs(typeItem, types, 1, types.Count);
        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int rank = args.Length - 1;
        if (!type.IsTensorReq)
            return false;
        if (type.TensorRank != rank)
            return false;
        if (type.GetTensorItemType() != args[0].Type)
            return false;
        for (int slot = 1; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var bldr = ReduceShapeSlots(reducer, call.Args, 1, call.Args.Length);
        if (bldr == null)
            return call;
        return call.SetArgs(bldr.ToImmutable());
    }
}

/// <summary>
/// Creates a tensor with values drawn from a sequence. When the sequence is too short to "cover"
/// all the cells, the remaining cells are filled with the default value.
/// Usage: <c>Tensor.From(seq, dims...)</c>.
/// Then no dims are present, the result is a rank one tensor with dim matching the length of the
/// sequence.
/// REVIEW: There is currently a type-hole when the item type is a req reference type;
/// uncovered cells are incorrectly filled with null.
/// REVIEW: Need a way to specify a fill value. Perhaps a directive? Or Tensor.FromOr?
/// </summary>
public sealed partial class TensorFromFunc : TensorFunc
{
    public static readonly TensorFromFunc Instance = new TensorFromFunc();

    private TensorFromFunc()
        : base("From", 1, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        var maskDims = BitSet.GetMask(1, carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskDims, maskLiftOpt: maskDims);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);
        var typeItem = types[0].ItemTypeOrThis;

        if (types.Count > 1)
        {
            var typeRes = ProcessShapeArgs(typeItem, types, 1, types.Count);
            return (typeRes, types.ToImmutable());
        }

        return (typeItem.ToTensor(false, 1), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int rank = Math.Max(1, args.Length - 1);
        if (!type.IsTensorReq)
            return false;
        if (type.TensorRank != rank)
            return false;
        var typeSrc = args[0].Type;
        if (!typeSrc.IsSequence)
            return false;
        if (type.GetTensorItemType() != typeSrc.ItemTypeOrThis)
            return false;
        for (int slot = 1; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        if (args.Length > 1)
        {
            var bldr = ReduceShapeSlots(reducer, args, 1, args.Length);
            if (bldr != null)
            {
                call = call.SetArgs(bldr.ToImmutable());
                args = call.Args;
            }
        }

        if (args[0] is BndSequenceNode bsn)
        {
            if (args.Length == 1)
                return BndTensorNode.Create(call.Type, bsn.Items, Shape.Create(bsn.Items.Length));
            if (TryGetShape(args, 1, out var shape) && shape.TryGetCount(out long count))
            {
                var items = bsn.Items;
                if (count <= items.Length)
                {
                    if (count < items.Length)
                        items = items.RemoveTail((int)count);
                    return BndTensorNode.Create(call.Type, items, shape);
                }
                // REVIEW: Should we warn and reduce?
            }
        }
        else if (args[0].IsNullValue)
        {
            if (args.Length == 1)
                return BndTensorNode.Create(call.Type, ArgTuple.Empty, Shape.Zero1);
            if (TryGetShape(args, 1, out var shape) && shape.TryGetCount(out long count))
            {
                if (count == 0)
                    return BndTensorNode.Create(call.Type, ArgTuple.Empty, shape);
                // REVIEW: Should we warn and reduce?
            }
        }

        return call;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Creates a tensor with values specified via a sequence, index selectors, and a value selector.
/// Usage: <c>Tensor.Build(seq, dims..., indices..., value[, fill])</c>.
/// There must be at least one dim. The number of indices matches the number of dims. The fill
/// value is optional.
/// REVIEW: Is there any point in supporting rank zero?
/// </summary>
public sealed partial class TensorBuildFunc : TensorFunc
{
    public static readonly TensorBuildFunc Instance = new TensorBuildFunc();

    private TensorBuildFunc()
        : base("Build", 4, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        int rank = (carg - 2) / 2;
        Validation.Assert(rank >= 0);
        return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: 1, nonPre: rank, nonPost: carg & 1);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);

        int carg = types.Count;
        int rank = (carg - 2) / 2;
        int ivSel = 1 + 2 * rank;
        var typeItem = types[ivSel];
        if ((carg & 1) != 0)
        {
            typeItem = DType.GetSuperType(typeItem, types[ivSel + 1], DType.UseUnionDefault);
            types[ivSel] = typeItem;
            types[ivSel + 1] = typeItem;
        }
        var typeRes = ProcessShapeArgs(typeItem, types, 1, 1 + rank);
        for (int i = 0; i < rank; i++)
            types[1 + rank + i] = DType.I8Req;
        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsTensorReq)
            return false;

        var args = call.Args;
        int carg = args.Length;
        int rank = (carg - 2) / 2;
        if (type.TensorRank != rank)
            return false;
        if (!args[0].Type.IsSequence)
            return false;
        int ivSel = 1 + 2 * rank;
        var typeItem = args[ivSel].Type;
        if (type.GetTensorItemType() != typeItem)
            return false;
        if ((carg & 1) != 0 && args[ivSel + 1].Type != typeItem)
            return false;
        for (int slot = 1; slot < ivSel; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.AssertValue(call);
        Validation.Assert(SupportsArity(call.Args.Length));
        Validation.Assert(call.Type.IsTensorReq);

        var args = call.Args;
        int rank = (args.Length - 2) / 2;
        Validation.Assert(call.Type.TensorRank == rank);

        if (rank > 0)
        {
            var bldr = ReduceShapeSlots(reducer, args, 1, 1 + rank);
            if (bldr != null)
                call = call.SetArgs(bldr.ToImmutable());
        }

        // REVIEW: Do other reductions.
        return call;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

public sealed partial class TensorRankFunc : TensorFunc
{
    public static readonly TensorRankFunc Instance = new TensorRankFunc();

    private TensorRankFunc()
        : base("Rank", 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        // REVIEW: Should this lift over opt or should it be the value from the dtype
        // even for "null" values?
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis.IsTensorReq;
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        EnsureTypeTen(ref type);
        return (DType.I8Req, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.I8Req)
            return false;
        var typeTen = call.Args[0].Type;
        if (!typeTen.IsTensorReq)
            return false;

        // This is always reduced.
        full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        DType type = call.Args[0].Type;
        if (type.IsTensorReq)
            return BndIntNode.CreateI8(type.TensorRank);
        return call;
    }
}

public sealed partial class TensorShapeFunc : TensorFunc
{
    public static readonly TensorShapeFunc Instance = new TensorShapeFunc();

    private TensorShapeFunc()
        : base("Shape", 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis.IsTensorReq;
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var typeSrc = info.Args[0].Type;
        EnsureTypeTen(ref typeSrc);

        // REVIEW: What should the type of the shape be? Tuple, sequence, tensor?
        DType typeRes = DType.CreateTuple(false, Immutable.Array.Fill(DType.I8Req, typeSrc.TensorRank));

        return (typeRes, Immutable.Array.Create(typeSrc));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsTupleReq)
            return false;

        var typeTen = call.Args[0].Type;
        if (!typeTen.IsTensorReq)
            return false;
        int rank = typeTen.TensorRank;
        if (type.TupleArity != rank)
            return false;
        if (rank == 0)
            return true;

        if (!type.IsHomTuple(out var typeItem))
            return false;
        if (typeItem != DType.I8Req)
            return false;

        return true;
    }
}

public sealed partial class TensorValuesFunc : TensorFunc
{
    public static readonly TensorValuesFunc Instance = new TensorValuesFunc();

    private TensorValuesFunc()
        : base("Values", 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01);
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis.IsTensorXxx;
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var typeSrc = info.Args[0].Type;
        EnsureTypeTenXxx(ref typeSrc);
        var typeItem = typeSrc.GetTensorItemType();

        return (typeItem.ToSequence(), Immutable.Array.Create(typeSrc));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        var typeTen = call.Args[0].Type;
        if (!typeTen.IsTensorXxx)
            return false;
        if (type.ItemTypeOrThis != typeTen.GetTensorItemType())
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        if (call.Args[0].IsNullValue)
            return BndNullNode.Create(call.Type);
        return base.ReduceCore(reducer, call);
    }
}

public sealed partial class TensorReshapeFunc : TensorFunc
{
    public static readonly TensorReshapeFunc Instance = new TensorReshapeFunc();

    private TensorReshapeFunc()
        : base("Reshape", 1, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeTen(types, 0);
        DType typeSrc = types[0];

        var typeItem = typeSrc.GetTensorItemType();
        var typeRes = ProcessShapeArgs(typeItem, types, 1, types.Count);
        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int rank = args.Length - 1;
        if (!type.IsTensorReq)
            return false;
        if (type.TensorRank != rank)
            return false;
        var typeSrc = args[0].Type;
        if (!typeSrc.IsTensorReq)
            return false;
        if (type.GetTensorItemType() != typeSrc.GetTensorItemType())
            return false;
        for (int slot = 1; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var bldr = ReduceShapeSlots(reducer, call.Args, 1, call.Args.Length);
        if (bldr == null)
            return call;
        return call.SetArgs(bldr.ToImmutable());
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        // In some cases, Reshape may end up building a new tensor buffer, so best to return true.
        return true;
    }
}

public sealed partial class TensorTransposeFunc : TensorFunc
{
    public static readonly TensorTransposeFunc Instance = new TensorTransposeFunc();

    private TensorTransposeFunc()
        : base("Transpose", 1, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        var maskSrc = BitSet.GetMask(1);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskSrc, maskLiftOpt: maskSrc);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        if (!types[0].IsTensorXxx)
            types[0] = types[0].ToTensor(false, types.Count > 1 ? types.Count - 1 : 2);
        DType typeSrc = types[0];

        for (int iarg = 1; iarg < types.Count; iarg++)
            types[iarg] = DType.I8Req;

        int rank = typeSrc.TensorRank;
        if (types.Count == rank + 1)
        {
            // It's very common for the indices to be literals. Since it is easy to get them
            // wrong, we test literals for validity.
            var slots = new BitSet();
            var allLit = true;
            for (int iarg = 1; iarg < info.Args.Length; iarg++)
            {
                var arg = info.Args[iarg];

                if (!arg.TryGetIntegral(out var i))
                {
                    allLit = false;
                    continue;
                }

                if (!(0 <= i && i < rank))
                {
                    info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(iarg),
                        ErrorStrings.WrnOutOfRange_Min_Lim, 0, rank));
                    allLit = false;
                    continue;
                }

                var ind = (int)i;
                if (slots.TestBit(ind))
                {
                    info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(iarg),
                        ErrorStrings.WrnAxisAlreadySpecified, ind));
                    allLit = false;
                    continue;
                }

                slots = slots.SetBit(ind);
            }
            Validation.Assert(!allLit || slots == BitSet.GetMask(rank));
        }
        else if (types.Count > 1)
        {
            if (types.Count > rank + 1)
            {
                info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(rank + 1),
                    ErrorStrings.ErrArityTooBig_Path_Num, Path, types.Count - rank - 1));
            }
            else
            {
                info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(types.Count - 1),
                    ErrorStrings.ErrArityTooSmall_Path_Num, Path, rank + 1 - types.Count));
            }
        }
        return (typeSrc, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int cdim = args.Length - 1;
        if (!type.IsTensorReq)
            return false;
        if (args[0].Type != type)
            return false;
        if (cdim > 0)
        {
            if (cdim != type.TensorRank)
                full = false;
            for (int slot = 1; slot < args.Length; slot++)
            {
                if (args[slot].Type != DType.I8Req)
                    return false;
            }
        }
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var src = call.Args[0];
        int rank = src.Type.GetTensorRank();
        if (rank <= 1)
            return src;

        if (call.Args.Length == rank + 1)
        {
            // Check for identity permutation.
            var id = true;
            for (int i = 1; i < call.Args.Length; i++)
            {
                if (!call.Args[i].TryGetIntegral(out var ind) || ind != i - 1)
                {
                    id = false;
                    break;
                }
            }
            if (id)
                return src;
        }

        return base.ReduceCore(reducer, call);
    }
}

public sealed partial class TensorExpandDimsFunc : TensorFunc
{
    public static readonly TensorExpandDimsFunc Instance = new TensorExpandDimsFunc();

    private TensorExpandDimsFunc()
        : base("ExpandDims", 2, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        var maskSrc = BitSet.GetMask(1);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskSrc, maskLiftOpt: maskSrc);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeTen(types, 0);
        DType typeSrc = types[0];

        for (int iarg = 1; iarg < types.Count; iarg++)
            types[iarg] = DType.I8Req;

        var typeItem = typeSrc.GetTensorItemType();
        int rank = typeSrc.TensorRank;

        // It's very common for the indices to be literals. Since it is easy to get them
        // wrong, we test literals for validity.
        var args = info.Args;
        var rankNew = rank + args.Length - 1;
        var slots = new BitSet();
        for (int iarg = 1; iarg < args.Length; iarg++)
        {
            types[iarg] = DType.I8Req;
            var arg = info.Args[iarg];

            if (!arg.TryGetIntegral(out var i))
                continue;

            if (!(0 <= i && i < rankNew))
            {
                info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(iarg),
                    ErrorStrings.WrnOutOfRange_Min_Lim, 0, rankNew));
                continue;
            }

            var ind = (int)i;
            if (slots.TestBit(ind))
            {
                info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(iarg),
                    ErrorStrings.WrnAxisAlreadySpecified, ind));
                continue;
            }

            slots = slots.SetBit(ind);
        }

        DType typeRes = typeItem.ToTensor(typeSrc.IsOpt, rankNew);
        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!type.IsTensorReq)
            return false;
        var typeSrc = args[0].Type;
        if (!typeSrc.IsTensorReq)
            return false;
        if (type.GetTensorItemType() != typeSrc.GetTensorItemType())
            return false;
        int rankNew = typeSrc.TensorRank + args.Length - 1;
        if (type.TensorRank != rankNew)
            return false;
        for (int slot = 1; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }
}

public abstract partial class TensorBinaryFunc : TensorFunc
{
    private readonly bool _forceFrac;
    private readonly bool _forceInt;

    private protected TensorBinaryFunc(string name, bool forceFrac, bool forceInt)
        : base(name, 2, 2)
    {
        Validation.Assert(!(forceFrac && forceInt));
        _forceFrac = forceFrac;
        _forceInt = forceInt;
    }

    protected sealed override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x03, maskLiftOpt: 0x03);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        DType type0 = info.Args[0].Type;
        DType type1 = info.Args[1].Type;

        EnsureTypeTen(ref type0);
        EnsureTypeTen(ref type1);

        var typeItem0 = type0.GetTensorItemType();
        var typeItem1 = type1.GetTensorItemType();

        var kindItem =
            typeItem0.IsNumericReq ?
                typeItem1.IsNumericReq ? DType.SuperNum(typeItem0.RootKind, typeItem1.RootKind) : typeItem0.RootKind :
                // REVIEW: What type should we use for the default? Perhaps R4?
                typeItem1.IsNumericReq ? typeItem1.RootKind : DKind.R8;
        Validation.Assert(kindItem.IsNumeric());
        if (_forceFrac && !kindItem.IsFractional())
            kindItem = DKind.R8;
        if (_forceInt && !kindItem.IsIntegral())
            kindItem = DKind.I8;
        var typeItem = DType.GetNumericType(kindItem);

        if (typeItem0 != typeItem)
            type0 = typeItem.ToTensor(false, type0.TensorRank);
        if (typeItem1 != typeItem)
            type1 = typeItem.ToTensor(false, type1.TensorRank);

        int rank0 = type0.TensorRank;
        int rank1 = type1.TensorRank;
        int rankRes = GetResultRank(rank0, rank1);
        var typeRes = rankRes == rank0 ? type0 : rankRes == rank1 ? type1 : typeItem.ToTensor(false, rankRes);
        return (typeRes, Immutable.Array.Create(type0, type1));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var type0 = call.Args[0].Type;
        var type1 = call.Args[1].Type;

        if (!type.IsTensorReq)
            return false;
        if (!type0.IsTensorReq)
            return false;
        if (!type1.IsTensorReq)
            return false;

        int rankRes = GetResultRank(type0.TensorRank, type1.TensorRank);
        if (type.TensorRank != rankRes)
            return false;

        var typeItem = type.GetTensorItemType();
        if (type0.GetTensorItemType() != typeItem)
            return false;
        if (type1.GetTensorItemType() != typeItem)
            return false;
        if (_forceFrac)
        {
            if (!typeItem.IsFractionalReq)
                return false;
        }
        else if (_forceInt)
        {
            if (!typeItem.IsIntegralReq)
                return false;
        }
        else if (!typeItem.IsNumericReq)
            return false;

        return true;
    }

    private protected abstract int GetResultRank(int rank0, int rank1);
}

public sealed partial class TensorDotFunc : TensorBinaryFunc
{
    public static readonly TensorDotFunc Instance = new TensorDotFunc();

    private TensorDotFunc()
        : base("Dot", false, false)
    {
    }

    private protected override int GetResultRank(int rank0, int rank1)
    {
        Validation.Assert(rank0 >= 0);
        Validation.Assert(rank1 >= 0);

        if (rank0 == 0)
            return rank1;
        if (rank1 == 0)
            return rank0;

        // REVIEW: Overflow?
        return rank0 + rank1 - 2;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (!base.CertifyCore(call, ref full))
            return false;

        // If either is rank zero, reduce will change to an invocation of Mul, so this
        // function shouldn't see rank zero in code gen.
        if (call.Args[0].Type.TensorRank == 0 || call.Args[1].Type.TensorRank == 0)
            full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        // Reduce to Pointwise mul when an arg is rank 0.
        if (call.Args[0].Type.TensorRank == 0 || call.Args[1].Type.TensorRank == 0)
            return reducer.Reduce(BndCallNode.Create(TensorPointWiseFunc.Mul, call.Type, call.Args));

        return call;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

public sealed partial class TensorPointWiseFunc : TensorBinaryFunc
{
    public enum Bop
    {
        Add,
        Sub,
        Mul,
        Min,
        Max,
        DivInt, // Integer division.
        DivFrac // Fractional division.
    }

    public static readonly TensorPointWiseFunc Add = new TensorPointWiseFunc("Add", Bop.Add);
    public static readonly TensorPointWiseFunc Sub = new TensorPointWiseFunc("Sub", Bop.Sub);
    public static readonly TensorPointWiseFunc Mul = new TensorPointWiseFunc("Mul", Bop.Mul);
    public static readonly TensorPointWiseFunc Min = new TensorPointWiseFunc("Min", Bop.Min);
    public static readonly TensorPointWiseFunc Max = new TensorPointWiseFunc("Max", Bop.Max);
    public static readonly TensorPointWiseFunc Div = new TensorPointWiseFunc("Div", Bop.DivInt, forceInt: true);
    public static readonly TensorPointWiseFunc Divide = new TensorPointWiseFunc("Divide", Bop.DivFrac, forceFrac: true);

    public Bop Operator { get; }

    private TensorPointWiseFunc(string name, Bop bop, bool forceFrac = false, bool forceInt = false)
        : base(name, forceFrac, forceInt)
    {
        Operator = bop;
    }

    private protected override int GetResultRank(int rank0, int rank1)
    {
        Validation.Assert(rank0 >= 0);
        Validation.Assert(rank1 >= 0);
        return Math.Max(rank0, rank1);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}
