// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

/// <summary>
/// Base class for rexl functions in the Float namespace that accept a single arg
/// and may be used as properties (when the arg type is a floating point type).
/// These lift over opt, sequence, and tensor.
/// </summary>
public abstract class FloatFuncOne : OneToOneFunc
{
    public static readonly NPath NsFloat = NPath.Root.Append(new DName("Float"));

    protected FloatFuncOne(DName name)
        : base(name, NsFloat)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var mask = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: mask, maskLiftTen: mask, maskLiftOpt: mask);
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis.IsFractionalReq;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        Validation.Assert(!type.HasReq);
        type = GetArgType(type);
        return (GetRetType(type), Immutable.Array.Create(type));
    }

    protected virtual DType GetArgType(DType typeIn)
    {
        if (typeIn.IsFractionalXxx)
            return typeIn;
        return DType.R8Req;
    }

    protected virtual DType GetRetType(DType typeArg)
    {
        Validation.Assert(typeArg.IsFractionalReq);
        return typeArg;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var typeIn = call.Args[0].Type;
        var typeArg = GetArgType(typeIn);
        if (typeArg != typeIn)
            return false;
        var typeRet = GetRetType(typeArg);
        if (call.Type != typeRet)
            return false;
        return true;
    }
}

/// <summary>
/// Tests a req floating point value for NaN. There are two variants, <see cref="IsNaN"/>
/// and <see cref="IsNotNaN"/>. The latter has simpler IL so is preferred to using
/// <c>!x.IsNaN</c>.
/// </summary>
public sealed partial class FloatIsNanFunc : FloatFuncOne
{
    public static readonly FloatIsNanFunc IsNaN = new(not: false);
    public static readonly FloatIsNanFunc IsNotNaN = new(not: true);

    public bool IsNot { get; }

    private FloatIsNanFunc(bool not)
        : base(new DName(not ? "IsNotNaN" : "IsNaN"))
    {
        IsNot = not;
    }

    protected override DType GetRetType(DType typeArg)
    {
        Validation.Assert(typeArg.IsFractionalReq);
        return DType.BitReq;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (!arg.TryGetFractional(out var value))
            return call;

        return BndIntNode.CreateBit(double.IsNaN(value) ^ IsNot);
    }
}

/// <summary>
/// Returns the bits of a req floating point value as either an I8 or I4, depending on
/// whether the input is R8 or R4. This returns a signed value so it can easily be tested
/// for negative.
/// </summary>
public sealed partial class FloatBitsFunc : FloatFuncOne
{
    public static readonly FloatBitsFunc Instance = new();

    private FloatBitsFunc()
        : base(new DName("Bits"))
    {
    }

    protected override DType GetRetType(DType typeArg)
    {
        Validation.Assert(typeArg.IsFractionalReq);
        return typeArg.RootKind == DKind.R8 ? DType.I8Req : DType.I4Req;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (!arg.TryGetFractional(out var value))
            return call;

        switch (arg.Type.RootKind)
        {
        default:
            Validation.Assert(false);
            return call;

        case DKind.R8:
            return BndIntNode.CreateI8((long)NumUtil.ToBits(value));
        case DKind.R4:
            return BndIntNode.CreateI4((int)NumUtil.ToBits((float)value));
        }
    }
}

/// <summary>
/// Maps from an integer value of type I8, U8, I4, or U4 to an R8 or R4 value with the same
/// bit representation. This is NOT a numeric conversion.
/// </summary>
public sealed partial class FloatFromBitsFunc : FloatFuncOne
{
    public static readonly FloatFromBitsFunc Instance = new();

    private FloatFromBitsFunc()
        : base(new DName("FromBits"))
    {
    }

    protected override DType GetArgType(DType typeIn)
    {
        Validation.Assert(!typeIn.HasReq);

        switch (typeIn.Kind)
        {
        default:
        case DKind.IA:
            return DType.I8Req;

        case DKind.I8:
        case DKind.U8:
        case DKind.I4:
        case DKind.U4:
            return typeIn;

        case DKind.I2:
        case DKind.U2:
        case DKind.I1:
        case DKind.U1:
        case DKind.Bit:
            return DType.I4Req;
        }
    }

    protected override DType GetRetType(DType typeArg)
    {
        Validation.Assert(typeArg.IsIntegralReq);

        switch (typeArg.RootKind)
        {
        case DKind.I8:
        case DKind.U8:
            return DType.R8Req;
        case DKind.I4:
        case DKind.U4:
            return DType.R4Req;
        }

        Validation.Assert(false);
        return DType.R8Req;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (!arg.TryGetI8(out var value))
            return call;

        switch (arg.Type.RootKind)
        {
        default:
            Validation.Assert(false);
            return call;

        case DKind.I8:
        case DKind.U8:
            return BndFltNode.CreateR8(NumUtil.ToDouble((ulong)value));
        case DKind.I4:
        case DKind.U4:
            return BndFltNode.CreateR4(NumUtil.ToFloat((uint)value));
        }
    }
}
