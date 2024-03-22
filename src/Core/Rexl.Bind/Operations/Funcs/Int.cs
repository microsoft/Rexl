// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using Integer = System.Numerics.BigInteger;

/// <summary>
/// Base class for rexl functions in the Int namespace that accept a single arg
/// and may be used as properties (when the arg type is an integer type).
/// These lift over opt, sequence, and tensor.
/// </summary>
public abstract class IntFuncOne : OneToOneFunc
{
    public static readonly NPath NsInt = NPath.Root.Append(new DName("Int"));

    protected IntFuncOne(DName name)
        : base(name, NsInt)
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
        return typeThis.IsIntegralReq;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        Validation.Assert(!type.HasReq);
        if (!type.IsIntegralXxx)
            type = DType.I8Req;
        return (GetRetType(type), Immutable.Array.Create(type));
    }

    protected virtual DType GetRetType(DType typeIn)
    {
        Validation.Assert(typeIn.IsIntegralReq);
        return typeIn;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        DType typeIn = call.Args[0].Type;
        if (!typeIn.IsIntegralReq)
            return false;
        DType typeRet = GetRetType(typeIn);
        if (call.Type != typeRet)
            return false;
        return true;
    }
}

/// <summary>
/// Maps an integer value to a text value containing a hexadecimal representation.
/// The number of hex digits is twice the number of bytes in the integer type.
/// An IA value is mapped to a minimal number of hexadecimal digits. If the integer
/// types is signed, the leading hex digit of the result will be at least 8 when
/// the value is negative.
/// </summary>
public sealed partial class IntHexFunc : IntFuncOne
{
    public static readonly IntHexFunc Instance = new();

    private IntHexFunc()
        : base(new DName("Hex"))
    {
    }

    protected override DType GetRetType(DType typeIn)
    {
        Validation.Assert(typeIn.IsIntegralReq);
        return DType.Text;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (!arg.TryGetIntegral(out var value))
            return call;

        string res;
        switch (arg.Type.RootKind)
        {
        default:
            Validation.Assert(false);
            return call;

        case DKind.IA: res = Exec(value); break;
        case DKind.I8: res = Exec((long)value, 2 * sizeof(long)); break;
        case DKind.I4: res = Exec((int)value, 2 * sizeof(int)); break;
        case DKind.I2: res = Exec((short)value, 2 * sizeof(short)); break;
        case DKind.I1: res = Exec((sbyte)value, 2 * sizeof(sbyte)); break;
        case DKind.U8: res = Exec((long)(ulong)value, 2 * sizeof(ulong)); break;
        case DKind.U4: res = Exec((uint)value, 2 * sizeof(uint)); break;
        case DKind.U2: res = Exec((ushort)value, 2 * sizeof(ushort)); break;
        case DKind.U1: res = Exec((byte)value, 2 * sizeof(byte)); break;
        case DKind.Bit: res = Exec(!value.IsZero); break;
        }

        return BndStrNode.Create(res);
    }

    public static string Exec(Integer val)
    {
        return val.ToString("X");
    }

    public static string Exec(long val, int cch)
    {
        return string.Create(cch, (ulong)val, ToHex);
    }

    private static void ToHex(Span<char> dst, ulong val)
    {
        Validation.Assert(dst.Length <= 2 * sizeof(ulong));

        for (int ich = dst.Length; --ich >= 0;)
        {
            dst[ich] = "0123456789ABCDEF"[(int)(val & 0x0F)];
            val >>= 4;
        }
    }

    public static string Exec(bool val) => val ? "1" : "0";
}
