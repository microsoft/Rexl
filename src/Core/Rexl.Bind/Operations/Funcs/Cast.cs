// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

using Integer = System.Numerics.BigInteger;

public static class CastUtil
{
    public const NumberStyles IntNumberStyles =
        NumberStyles.AllowThousands |
        NumberStyles.AllowParentheses |
        NumberStyles.Integer |
        NumberStyles.AllowCurrencySymbol;
    public const NumberStyles RealNumberStyles =
        NumberStyles.AllowThousands |
        NumberStyles.AllowParentheses |
        NumberStyles.Float |
        NumberStyles.AllowCurrencySymbol;

    // REVIEW: Currency symbol for InvariantCulture is `Â¤`. Needs better currency symbol support.
    public static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
    public static readonly IFormatProvider FormatProvider = InvariantCulture.NumberFormat;

    // REVIEW: Better to generate codes inline rather than distinct functions.
    public static bool TryToI8(this Integer value, long min, long max, out long result)
    {
        Validation.Assert(min < 0 && 0 < max);
        if (value.Sign < 0 ? min <= value : value <= max)
        {
            result = (long)value;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryToU8(this Integer value, ulong max, out ulong result)
    {
        if (value.Sign >= 0 && value <= max)
        {
            result = (ulong)value;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryParseI8(string s, out long result)
    {
        if (s != null)
            return long.TryParse(s, IntNumberStyles, FormatProvider, out result);
        result = default;
        return false;
    }

    public static bool TryParseU8(string s, out ulong result)
    {
        if (s != null)
            return ulong.TryParse(s, IntNumberStyles, FormatProvider, out result);
        result = default;
        return false;
    }

    public static bool TryParseInt(string s, out Integer result)
    {
        if (s != null)
            return Integer.TryParse(s, IntNumberStyles, FormatProvider, out result);
        result = default;
        return false;
    }

    public static bool TryParseR8(string s, out double result)
    {
        if (s != null)
        {
            // REVIEW: For overflow to infinity, .Net Core 3.0 does the right thing and returns with
            // an infinite result, but .Net Framework 4.X still returns false. This shouldn't be an issue
            // when we move to .Net 5.0.
            if (double.TryParse(s, RealNumberStyles, FormatProvider, out result))
                return true;

            // We also accept these forms.
            switch (s.Trim())
            {
            case "∞":
            case "Infinity":
                result = double.PositiveInfinity;
                return true;
            case "-∞":
            case "(∞)":
            case "-Infinity":
            case "(Infinity)":
                result = double.NegativeInfinity;
                return true;
            case "NaN":
            case "-NaN":
            case "(NaN)":
                result = double.NaN;
                return true;
            }
        }

        result = default;
        return false;
    }
}

public sealed partial class CastFunc : RexlOper
{
    public static readonly CastFunc CastI1 = new CastFunc("CastI1", DType.I1Req, typeof(sbyte));
    public static readonly CastFunc CastI2 = new CastFunc("CastI2", DType.I2Req, typeof(short));
    public static readonly CastFunc CastI4 = new CastFunc("CastI4", DType.I4Req, typeof(int));
    public static readonly CastFunc CastI8 = new CastFunc("CastI8", DType.I8Req, typeof(long));
    public static readonly CastFunc CastU1 = new CastFunc("CastU1", DType.U1Req, typeof(byte));
    public static readonly CastFunc CastU2 = new CastFunc("CastU2", DType.U2Req, typeof(ushort));
    public static readonly CastFunc CastU4 = new CastFunc("CastU4", DType.U4Req, typeof(uint));
    public static readonly CastFunc CastU8 = new CastFunc("CastU8", DType.U8Req, typeof(ulong));
    public static readonly CastFunc CastIA = new CastFunc("CastIA", DType.IAReq, typeof(Integer));
    public static readonly CastFunc CastR4 = new CastFunc("CastR4", DType.R4Req, typeof(float));
    public static readonly CastFunc CastR8 = new CastFunc("CastR8", DType.R8Req, typeof(double));

    public DType TypeDst { get; }
    public Type SysTypeDst { get; }

    private CastFunc(string name, DType type, Type st)
        : base(isFunc: true, new DName(name), 1, 1)
    {
        Validation.Assert(type.IsNumericReq);
        TypeDst = type;
        SysTypeDst = st;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        DType type = info.Args[0].Type;
        Validation.Assert(!type.HasReq);
        Validation.Assert(type.SeqCount == 0);

        // If the src type isn't an acceptable one, use the dst type.
        var kind = type.RootKind;
        if (!kind.IsNumeric() && !kind.IsChrono() && kind != DKind.Text)
            type = TypeDst;

        return (TypeDst, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeDst)
            return false;
        var typeSrc = call.Args[0].Type;
        if (typeSrc.HasReq)
            return false;
        if (typeSrc.IsSequence)
            return false;
        var kindSrc = typeSrc.RootKind;
        if (!kindSrc.IsNumeric() && !kindSrc.IsChrono() && kindSrc != DKind.Text)
            return false;

        // Reduce should take care of the case when _type accepts typeSrc and the chrono source case.
        if (TypeDst.Accepts(typeSrc, union: DType.UseUnionDefault))
            full = false;
        if (typeSrc.RootKind.IsChrono())
            full = false;

        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        // Cast is no-op when the input and output types match.
        var arg = call.Args[0];
        if (arg.Type == TypeDst)
            return arg;

        // If the output type accepts the input type, use numeric cast.
        if (TypeDst.Accepts(arg.Type, union: DType.UseUnionDefault))
            return reducer.Reduce(BndCastNumNode.Create(call.Args[0], TypeDst));

        var kindDst = TypeDst.Kind;
        var kindSrc = arg.Type.RootKind;
        switch (kindSrc)
        {
        case DKind.Text:
            if (!arg.TryGetString(out var str))
                return call;
            switch (kindDst)
            {
            case DKind.R8:
                return BndFltNode.CreateR8(ExecR8(str));
            case DKind.R4:
                return BndFltNode.CreateR4((float)ExecR8(str));
            case DKind.IA:
                return BndIntNode.CreateI(ExecInteger(str));
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                return LongToBnd(ExecI8(str), kindDst);
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                return LongToBnd((long)ExecU8(str), kindDst);
            default:
                Validation.Assert(false);
                return call;
            }
        case DKind.R8:
        case DKind.R4:
            if (!arg.TryGetFractional(out var dbl))
                return call;
            switch (kindDst)
            {
            case DKind.R4:
                return BndFltNode.CreateR4((float)dbl);
            case DKind.IA:
                // NaN and infinities produce zero.
                return BndIntNode.CreateI(dbl.IsFinite() ? (Integer)dbl : Integer.Zero);
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                return LongToBnd(NumUtil.ModToI8(dbl), kindDst);
            default:
                Validation.Assert(false);
                return call;
            }
        case DKind.Date:
        case DKind.Time:
            // Expand the TotalTicks property.
            arg = BndCallNode.Create(
                kindSrc == DKind.Date ? ChronoPartFunc.DateTotalTicks : ChronoPartFunc.TimeTotalTicks,
                DType.I8Req, Immutable.Array<BoundNode>.Create(arg));
            return reducer.Reduce(BndCallNode.Create(this, TypeDst, Immutable.Array<BoundNode>.Create(arg)));
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
            if (!arg.TryGetIntegral(out var bi))
                return call;
            // IA and RX accept all integer types, so dst must be a fixed sized integer type.
            switch (kindDst)
            {
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                return LongToBnd(bi.CastLong(), kindDst);
            default:
                Validation.Assert(false);
                return call;
            }
        default:
            Validation.Assert(false);
            return call;
        }
    }

    private static BndIntNode LongToBnd(long value, DKind kind)
    {
        Validation.Assert(kind.IsIxOrUx());

        switch (kind)
        {
        case DKind.I1:
            return BndIntNode.CreateI1((sbyte)value);
        case DKind.I2:
            return BndIntNode.CreateI2((short)value);
        case DKind.I4:
            return BndIntNode.CreateI4((int)value);
        case DKind.I8:
            return BndIntNode.CreateI8((long)value);
        case DKind.U1:
            return BndIntNode.CreateU1((byte)value);
        case DKind.U2:
            return BndIntNode.CreateU2((ushort)value);
        case DKind.U4:
            return BndIntNode.CreateU4((uint)value);
        case DKind.U8:
            return BndIntNode.CreateU8((ulong)value);
        }

        Validation.Assert(false);
        return null;
    }

    public static long ExecI8(string s)
    {
        if (s == null)
            return 0;
        if (long.TryParse(s, CastUtil.IntNumberStyles, CastUtil.FormatProvider, out long l))
            return l;
        if (Integer.TryParse(s, CastUtil.IntNumberStyles, CastUtil.FormatProvider, out var bi))
            return bi.CastLong();
        return 0;
    }

    public static ulong ExecU8(string s)
    {
        if (s == null)
            return 0;
        if (ulong.TryParse(s, CastUtil.IntNumberStyles, CastUtil.FormatProvider, out ulong ul))
            return ul;
        if (Integer.TryParse(s, CastUtil.IntNumberStyles, CastUtil.FormatProvider, out var bi))
            return bi.CastUlong();
        return 0;
    }

    public static Integer ExecInteger(string s)
    {
        return CastUtil.TryParseInt(s, out var bi) ? bi : 0;
    }

    public static double ExecR8(string s)
    {
        return CastUtil.TryParseR8(s, out var val) ? val : 0;
    }
}

public sealed partial class ToXXFunc : RexlOper
{
    public static readonly ToXXFunc ToI1 = new ToXXFunc("ToI1", DType.I1Opt);
    public static readonly ToXXFunc ToI2 = new ToXXFunc("ToI2", DType.I2Opt);
    public static readonly ToXXFunc ToI4 = new ToXXFunc("ToI4", DType.I4Opt);
    public static readonly ToXXFunc ToI8 = new ToXXFunc("ToI8", DType.I8Opt);
    public static readonly ToXXFunc ToU1 = new ToXXFunc("ToU1", DType.U1Opt);
    public static readonly ToXXFunc ToU2 = new ToXXFunc("ToU2", DType.U2Opt);
    public static readonly ToXXFunc ToU4 = new ToXXFunc("ToU4", DType.U4Opt);
    public static readonly ToXXFunc ToU8 = new ToXXFunc("ToU8", DType.U8Opt);
    public static readonly ToXXFunc ToIA = new ToXXFunc("ToIA", DType.IAOpt);
    public static readonly ToXXFunc ToR4 = new ToXXFunc("ToR4", DType.R4Opt);
    public static readonly ToXXFunc ToR8 = new ToXXFunc("ToR8", DType.R8Opt);

    /// <summary>
    /// The optional form of the return type.
    /// </summary>
    public DType TypeOpt { get; }

    /// <summary>
    /// The required form of the return type.
    /// </summary>
    public DType TypeReq { get; }

    /// <summary>
    /// The destination kind.
    /// </summary>
    public DKind KindDst => TypeReq.RootKind;

    /// <summary>
    /// The minimum value for the destination kind. Valid only when the destination
    /// kind is a fixed sized integer type (signed or unsigned).
    /// </summary>
    public long Min { get; }

    /// <summary>
    /// The maximum value for the destination kind. Valid only when the destination
    /// kind is a fixed sized integer type (signed or unsigned).
    /// </summary>
    public ulong Max { get; }

    private ToXXFunc(string name, DType type)
        : base(isFunc: true, new DName(name), 1, 2)
    {
        Validation.Assert(type.IsNumericOpt);
        TypeOpt = type;
        TypeReq = type.ToReq();

        if (KindDst.IsIx())
        {
            Max = ~0UL >> (64 - 8 * KindDst.NumericSize() + 1);
            Min = (long)~Max;
        }
        else
            Max = ~0UL >> (64 - 8 * KindDst.NumericSize());
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: 0x1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);

        var types = info.GetArgTypes();
        Validation.AssertValue(types);
        Validation.Assert(SupportsArity(types.Count));

        DType typeSrc = types[0];
        Validation.Assert(!typeSrc.HasReq);
        Validation.Assert(!typeSrc.IsSequence);

        // If the src type isn't one of the listed types, use String.
        DKind kindSrc = typeSrc.RootKind;
        if (kindSrc == DKind.Vac)
            types[0] = typeSrc = TypeReq;
        else if (!kindSrc.IsNumeric() && !kindSrc.IsChrono() && kindSrc != DKind.Text)
            types[0] = typeSrc = DType.Text;

        var typeRet = TypeOpt;
        if (IsReducibleToCast(typeSrc))
            typeRet = TypeReq;

        if (types.Count > 1)
        {
            if (!typeRet.IsOpt)
            {
                info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(1),
                    ErrorStrings.WrnSecondArgumentNotUsed_Func, Name));
            }

            if (types[1].IsOpt)
                types[1] = TypeOpt;
            else
                types[1] = typeRet = TypeReq;
        }

        return (typeRet, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        var typeSrc = args[0].Type;
        if (typeSrc.HasReq)
            return false;
        if (typeSrc.IsSequence)
            return false;
        var kindSrc = typeSrc.RootKind;
        if (!kindSrc.IsNumeric() && !kindSrc.IsChrono() && kindSrc != DKind.Text)
            return false;

        bool red = IsReducibleToCast(typeSrc);
        var typeDst = red ? TypeReq : TypeOpt;
        if (args.Length > 1)
        {
            var typeElse = args[1].Type;
            if (typeElse == TypeReq)
                typeDst = TypeReq;
            else if (typeElse != TypeOpt)
                return false;
        }
        if (type != typeDst)
            return false;

        // Reduce should take care of the red case and the chrono source case.
        if (red)
            full = false;
        if (kindSrc.IsChrono())
            full = false;

        return true;
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Coverage(iarg == 0 ? 0 : 1);
        return iarg == 0 ? PullWithFlags.With : 0;
    }

    /// <summary>
    // Returns whether <paramref name="typeSrc"/> can be converted to
    // <paramref name="typeDst"/> without any boundary tests.
    /// </summary>
    internal static bool IsReducibleToCast(DType typeSrc, DType typeDst)
    {
        Validation.Assert(!typeDst.IsSequence);
        Validation.Assert(!typeDst.IsOpt);

        switch (typeSrc.RootKind)
        {
        case DKind.Text:
            return false;
        case DKind.Date:
            return typeDst.Accepts(DType.I8Req, DType.UseUnionDefault) || typeDst == DType.U8Req;
        case DKind.Time:
            return typeDst.Accepts(DType.I8Req, DType.UseUnionDefault);
        case DKind.R8:
            return typeDst.RootKind.IsRx();
        case DKind.Bit:
            return true;
        case DKind.U8:
            // I8 accepts U8, but ToI8 should still test for overflow.
            if (typeDst.RootKind == DKind.I8)
                return false;
            goto default;
        default:
            return typeDst.Accepts(typeSrc, DType.UseUnionDefault);
        }
    }

    /// <summary>
    /// Whether an invocation with the given <paramref name="typeSrc"/> is reducible to
    /// an invocation of the corresponding `CastXX` function. This is true if the source
    /// can't be "out of range" of the destination type, that is, if no boundary tests are
    /// needed.
    /// </summary>
    public bool IsReducibleToCast(DType typeSrc)
    {
        return IsReducibleToCast(typeSrc, TypeReq);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        var arg = args[0];
        DKind kindSrc = arg.Type.RootKind;
        Validation.Assert(kindSrc.IsNumeric() || kindSrc.IsChrono() || kindSrc == DKind.Text);

        DType typeRet = call.Type;
        Validation.Assert(typeRet.Kind == KindDst);

        if (IsReducibleToCast(arg.Type))
            return ReduceToCast(reducer, call);
        Validation.Assert(call.Args.Length < 2 || call.Args[1].Type == typeRet);

        BoundNode result = null;
        switch (kindSrc)
        {
        case DKind.Text:
            if (!arg.TryGetString(out var str))
                return call;
            switch (KindDst)
            {
            case DKind.R8:
                if (CastUtil.TryParseR8(str, out var dblVal))
                    result = BndFltNode.CreateR8(dblVal);
                break;
            case DKind.R4:
                if (CastUtil.TryParseR8(str, out var fltVal))
                    result = BndFltNode.CreateR4((float)fltVal);
                break;
            case DKind.IA:
                if (CastUtil.TryParseInt(str, out var intVal))
                    result = BndIntNode.CreateI(intVal);
                break;
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                if (CastUtil.TryParseI8(str, out var ixVal) && Min <= ixVal && ixVal <= (long)Max)
                    result = BndIntNode.Create(TypeReq, ixVal);
                break;
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                if (CastUtil.TryParseU8(str, out var uxVal) && uxVal <= Max)
                    result = BndIntNode.Create(TypeReq, uxVal);
                break;
            default:
                Validation.Assert(false);
                return call;
            }
            break;
        case DKind.R8:
        case DKind.R4:
            if (!arg.TryGetFractional(out var dbl))
                return call;
            switch (KindDst)
            {
            // REVIEW: Should ToR4(R8) produce null when finite maps to infinite?
            case DKind.IA:
                // NaN and infinities produce null/args[1].
                if (dbl.IsFinite())
                    result = BndIntNode.CreateI((Integer)dbl);
                break;
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                if (NumUtil.TryToI8(dbl, out var ixVal) && Min <= ixVal && ixVal <= (long)Max)
                    result = BndIntNode.Create(TypeReq, ixVal);
                break;
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                if (NumUtil.TryToU8(dbl, out var uxVal) && uxVal <= Max)
                    result = BndIntNode.Create(TypeReq, uxVal);
                break;
            default:
                Validation.Assert(false);
                return call;
            }
            break;
        case DKind.Date:
        case DKind.Time:
            // Expand the TotalTicks property.
            args = args.SetItem(0, BndCallNode.Create(
                kindSrc == DKind.Date ? ChronoPartFunc.DateTotalTicks : ChronoPartFunc.TimeTotalTicks,
                DType.I8Req, Immutable.Array<BoundNode>.Create(arg)));
            return reducer.Reduce(BndCallNode.Create(this, call.Type, args));
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
            if (!arg.TryGetIntegral(out var bi))
                return call;
            // IA and RX accept all integer types, so dst must be a fixed sized integer type.
            switch (KindDst)
            {
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                if (CastUtil.TryToI8(bi, Min, (long)Max, out _))
                    result = BndIntNode.Create(TypeReq, bi);
                break;
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                if (CastUtil.TryToU8(bi, Max, out _))
                    result = BndIntNode.Create(TypeReq, bi);
                break;
            default:
                Validation.Assert(false);
                return call;
            }
            break;
        default:
            Validation.Assert(false);
            return call;
        }

        // Handle the "result". If it is null, the result should be null/args[1].
        if (result is null)
            return (args.Length == 1) ? BndNullNode.Create(typeRet) : args[1];
        Validation.Assert(result.Type == TypeReq);
        return typeRet.IsOpt ? BndCastOptNode.Create(result) : result;
    }

    private BoundNode ReduceToCast(IReducer reducer, BndCallNode call)
    {
        Validation.Assert(call.Type == TypeReq);
        Validation.Assert(IsReducibleToCast(call.Args[0].Type));

        CastFunc func;
        switch (KindDst)
        {
        case DKind.R8: func = CastFunc.CastR8; break;
        case DKind.R4: func = CastFunc.CastR4; break;
        case DKind.IA: func = CastFunc.CastIA; break;
        case DKind.I8: func = CastFunc.CastI8; break;
        case DKind.I4: func = CastFunc.CastI4; break;
        case DKind.I2: func = CastFunc.CastI2; break;
        case DKind.I1: func = CastFunc.CastI1; break;
        case DKind.U8: func = CastFunc.CastU8; break;
        case DKind.U4: func = CastFunc.CastU4; break;
        case DKind.U2: func = CastFunc.CastU2; break;
        case DKind.U1: func = CastFunc.CastU1; break;
        default:
            throw Validation.BugExcept();
        }

        var args = call.Args;
        var castArgs = (args.Length == 1) ? args : Immutable.Array.Create(args[0]);
        return reducer.Reduce(BndCallNode.Create(func, call.Type, castArgs));
    }
}

public sealed partial class ToFunc : RexlOper
{
    public static readonly ToFunc To = new ToFunc("To");

    private ToFunc(string name)
        : base(isFunc: true, new DName(name), arityMin: 2, arityMax: 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: 0x1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);

        var types = info.GetArgTypes();
        Validation.AssertValue(types);
        Validation.Assert(SupportsArity(types.Count));

        var typeOpt = types[1];
        if (typeOpt.IsNumericXxx && typeOpt.Kind != DKind.Bit)
            typeOpt = typeOpt.ToOpt();
        else
            typeOpt = DType.I8Opt;
        var typeReq = typeOpt.ToReq();

        // The rest of specialization is the same as ToXXFunc.
        DType typeSrc = types[0];
        Validation.Assert(!typeSrc.HasReq);
        Validation.Assert(typeSrc.SeqCount == 0);

        // If the src type isn't one of the listed types, use String.
        DKind kindSrc = typeSrc.RootKind;
        if (kindSrc == DKind.Vac)
            types[0] = typeSrc = typeReq;
        else if (!kindSrc.IsNumeric() && !kindSrc.IsChrono() && kindSrc != DKind.Text)
            types[0] = typeSrc = DType.Text;

        var typeRet = typeOpt;
        if (ToXXFunc.IsReducibleToCast(typeSrc, typeReq))
            typeRet = typeReq;

        if (!typeRet.IsOpt)
        {
            info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(1),
                ErrorStrings.WrnSecondArgumentNotUsed_Func, Name));
        }
        if (types[1].IsOpt)
            types[1] = typeOpt;
        else
            typeRet = types[1] = typeReq;

        return (typeRet, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;

        if (type.IsSequence)
            return false;
        var kind = type.RootKind;
        if (!kind.IsNumeric())
            return false;

        var typeSrc = args[0].Type;
        if (typeSrc.HasReq)
            return false;
        if (typeSrc.IsSequence)
            return false;
        var kindSrc = typeSrc.RootKind;
        if (!kindSrc.IsNumeric() && !kindSrc.IsChrono() && kindSrc != DKind.Text)
            return false;

        var typeElse = args[1].Type;
        var typeReq = typeElse.ToReq();
        var typeDst = ToXXFunc.IsReducibleToCast(typeSrc, typeReq) ? typeReq : typeElse;
        if (type != typeDst)
            return false;

        // Always reduces to ToXXFunc.
        full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        Validation.Assert(call.Type.Kind == call.Args[1].Type.Kind);

        ToXXFunc func;
        switch (call.Type.Kind)
        {
        case DKind.I1: func = ToXXFunc.ToI1; break;
        case DKind.I2: func = ToXXFunc.ToI2; break;
        case DKind.I4: func = ToXXFunc.ToI4; break;
        case DKind.I8: func = ToXXFunc.ToI8; break;
        case DKind.U1: func = ToXXFunc.ToU1; break;
        case DKind.U2: func = ToXXFunc.ToU2; break;
        case DKind.U4: func = ToXXFunc.ToU4; break;
        case DKind.U8: func = ToXXFunc.ToU8; break;
        case DKind.IA: func = ToXXFunc.ToIA; break;
        case DKind.R4: func = ToXXFunc.ToR4; break;
        case DKind.R8: func = ToXXFunc.ToR8; break;
        default:
            throw Validation.BugExcept();
        }

        return reducer.Reduce(BndCallNode.Create(func, call.Type, call.Args));
    }
}

public sealed partial class ToTextFunc : RexlOper
{
    public static readonly ToTextFunc Instance = new ToTextFunc();

    private ToTextFunc()
        : base(isFunc: true, new DName("ToText"), 1, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        // REVIEW: Should null map to null?
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: 0x1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);

        var types = info.GetArgTypes();
        Validation.Assert(types != null);
        Validation.Assert(SupportsArity(types.Count));

        if (!types[0].IsPrimitiveXxx)
            types[0] = types[0].IsVacXxx ? DType.Text : DType.I4Req;

        if (types.Count == 2)
        {
            switch (types[0].RootKind)
            {
            case DKind.Date:
            case DKind.Time:
            case DKind.Guid:
                types[1] = DType.Text;
                break;
            default:
                info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(1),
                    ErrorStrings.ErrArityTooBig_Path_Num, Path, 1));
                break;
            }
        }

        return (DType.Text, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.Text)
            return false;

        var args = call.Args;
        var typeSrc = args[0].Type;
        if (!typeSrc.IsPrimitiveXxx)
            return false;
        if (typeSrc.HasReq)
            return false;

        switch (typeSrc.RootKind)
        {
        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
            if (args.Length > 1 && args[1].Type != DType.Text)
                return false;
            break;
        case DKind.Text:
            // Text should always be reduced.
            full = false;
            break;
        default:
            if (args.Length > 1)
                full = false;
            break;
        }

        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        if (call.Args.Length == 1)
        {
            var arg = call.Args[0];
            if (arg.Type == DType.Text)
                return arg;
            if (arg.TryGetBool(out var b))
                return BndStrNode.Create(b ? "true" : "false");
            if (arg.TryGetIntegral(out var i))
                return BndStrNode.Create(i.ToString());
            if (arg.TryGetFractional(out var dbl))
            {
                // REVIEW: We'll likely need to use format code here.
                // For example due to round-tripping or localization.
                if (arg.Type == DType.R4Req)
                    return BndStrNode.Create(((float)dbl).ToStr());
                return BndStrNode.Create(dbl.ToStr());
            }
        }

        return call;
    }
}
