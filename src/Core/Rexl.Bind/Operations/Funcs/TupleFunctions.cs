// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using TypeTuple = Immutable.Array<DType>;

/// <summary>
/// Base class for tuple functions.
/// </summary>
public abstract partial class TupleFunc : RexlOper
{
    private protected TupleFunc(string name, int arityMin, int arityMax)
        : base(isFunc: true, new DName(name), BindUtil.TupleNs, arityMin, arityMax)
    {
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis.IsTupleReq;
    }
}

/// <summary>
/// Functions/properties to extract slot values from tuples. Note that these cover slot numbers <c>0</c> to <c>9</c>.
/// The syntax <c>t!num</c>, eg, <c>t!3</c>, can also be used, and must be used for slot numbers larger than <c>9</c>.
/// </summary>
public sealed class TupleItemFunc : TupleFunc
{
    public static readonly TupleItemFunc Item0 = new TupleItemFunc("Item0", 0);
    public static readonly TupleItemFunc Item1 = new TupleItemFunc("Item1", 1);
    public static readonly TupleItemFunc Item2 = new TupleItemFunc("Item2", 2);
    public static readonly TupleItemFunc Item3 = new TupleItemFunc("Item3", 3);
    public static readonly TupleItemFunc Item4 = new TupleItemFunc("Item4", 4);
    public static readonly TupleItemFunc Item5 = new TupleItemFunc("Item5", 5);
    public static readonly TupleItemFunc Item6 = new TupleItemFunc("Item6", 6);
    public static readonly TupleItemFunc Item7 = new TupleItemFunc("Item7", 7);
    public static readonly TupleItemFunc Item8 = new TupleItemFunc("Item8", 8);
    public static readonly TupleItemFunc Item9 = new TupleItemFunc("Item9", 9);

    private readonly int _slot;

    private TupleItemFunc(string name, int slot)
        : base(new DName(name), 1, 1)
    {
        _slot = slot;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(carg == 1, nameof(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x1, maskLiftTen: 0x1, maskLiftOpt: 0x1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Oper == this);
        Validation.Assert(info.Arity == 1);

        var types = info.GetArgTypes();
        var typeSrc = types[0];
        Validation.Assert(!typeSrc.HasReq);
        Validation.Assert(!typeSrc.IsSequence);

        DType typeRes;
        ReadOnly.Array<DType> typesCur;
        if (!typeSrc.IsTupleReq)
        {
            info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0), ErrorStrings.ErrNotTuple));
            var typesTup = TypeTuple.CreateBuilder(_slot + 1, init: true);
            for (int i = 0; i < typesTup.Count; i++)
                typesTup[i] = DType.Vac;
            types[0] = DType.CreateTuple(false, typesTup.ToImmutable());
            typeRes = DType.Vac;
        }
        else if ((typesCur = typeSrc.GetTupleSlotTypes()).Length <= _slot)
        {
            info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0),
                ErrorStrings.ErrTupleTooSmall_Has_Need, typesCur.Length, _slot + 1));
            var typesTup = TypeTuple.CreateBuilder(_slot + 1, init: true);
            int i = 0;
            for (; i < typesCur.Length; i++)
                typesTup[i] = typesCur[i];
            for (; i < typesTup.Count; i++)
                typesTup[i] = DType.Vac;
            types[0] = DType.CreateTuple(false, typesTup.ToImmutable());
            typeRes = DType.Vac;
        }
        else
            typeRes = typesCur[_slot];

        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var typeSrc = call.Args[0].Type;

        if (!typeSrc.IsTupleReq)
            return false;
        int arity = typeSrc.TupleArity;
        if (_slot >= arity)
            return false;
        if (type != typeSrc.GetTupleSlotTypes()[_slot])
            return false;

        // Always reduces to BndGetSlotNode.
        full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
#if DEBUG
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        {
            var typeSrc = call.Args[0].Type;
            Validation.Assert(typeSrc.IsTupleReq);
            var types = typeSrc.GetTupleSlotTypes();
            Validation.Assert(_slot < types.Length);
            Validation.Assert(call.Type == types[_slot]);
        }
#endif

        return reducer.Reduce(BndGetSlotNode.Create(_slot, call.Args[0]));
    }
}

public sealed partial class TupleLenFunc : TupleFunc
{
    public static readonly TupleLenFunc Instance = new TupleLenFunc();

    private TupleLenFunc()
        : base("Len", 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        // REVIEW: Should this lift over opt or should it be the value from the DType
        // even for "null" values?
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        if (!type.IsTupleReq)
            type = DType.CreateTuple(false, type);
        return (DType.I8Req, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.I8Req)
            return false;
        var typeTup = call.Args[0].Type;
        if (!typeTup.IsTupleReq)
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
        Validation.Assert(type.IsTupleReq);
        return BndIntNode.CreateI8(type.TupleArity);
    }
}
