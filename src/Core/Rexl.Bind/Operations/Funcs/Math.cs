// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

using Integer = System.Numerics.BigInteger;

public static class StatFuncUtil
{
    public static readonly DName NameCount = new DName("Count");
    public static readonly DName NameSum = new DName("Sum");
    public static readonly DName NameMean = new DName("Mean");
    public static readonly DName NameMin = new DName("Min");
    public static readonly DName NameMax = new DName("Max");
}

public abstract class OneToOneMathFunc : OneToOneFunc
{
    protected OneToOneMathFunc(DName name)
        : base(name)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        Validation.Assert(carg == 1);
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        var type = info.Args[0].Type;
        if (!type.IsNumericXxx)
            type = DType.I8Req;
        else if (type.IsOpt)
        {
            // REVIEW: Is this possible, given that this lifts over opt?
            type = type.ToReq();
        }

        return (type, Immutable.Array.Create(type));
    }
}

public abstract class MathFunc : RexlOper
{
    protected MathFunc(DName name, int arityMin, int arityMax)
        : base(isFunc: true, name, arityMin, arityMax)
    {
    }

    protected static DType PromoteBySize(DType typeRes)
    {
        Validation.Assert(typeRes.IsNumericXxx);

        switch (typeRes.Kind)
        {
        case DKind.R4:
            return DType.GetNumericType(DKind.R8, typeRes.IsOpt);
        case DKind.I1:
        case DKind.I2:
        case DKind.I4:
        case DKind.U1:
        case DKind.U2:
        case DKind.U4:
        case DKind.Bit:
            return DType.GetNumericType(DKind.I8, typeRes.IsOpt);
        }
        return typeRes;
    }

    protected static DType PromoteBySizeBig(DType typeRes)
    {
        Validation.Assert(typeRes.IsNumericXxx);

        if (typeRes.IsFractionalXxx)
            return DType.GetNumericType(DKind.R8, typeRes.IsOpt);

        Validation.Assert(typeRes.IsIntegralXxx);
        return DType.GetNumericType(DKind.IA, typeRes.IsOpt);
    }
}

public abstract class MathAggFunc : MathFunc
{
    /// <summary>
    /// If the number of sequences exceeds this, the invocation will be rewritten to use ForEach.
    /// </summary>
    protected readonly int _maxSeqExplicit;

    protected MathAggFunc(DName name, int maxSeqExplicit)
        : base(name, 1, int.MaxValue)
    {
        Validation.Assert(maxSeqExplicit >= 0);
        _maxSeqExplicit = maxSeqExplicit;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: true, carg);
        return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: carg - 1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        DType typeItem = types[types.Count - 1];
        if (types.Count == 1)
            typeItem = typeItem.ItemTypeOrThis;

        if (!typeItem.IsNumericXxx)
            typeItem = typeItem.IsOpt ? DType.R8Opt : DType.R8Req;

        DType typeSrc = Promote(typeItem);
        DType typeAgg = AggFromSrc(typeSrc);
        Validation.Assert(typeAgg.IsNumericXxx);
        DType typeRes = ResFromAgg(typeAgg);

        if (types.Count == 1)
        {
            // The code generator should handle the type promotion (when needed).
            types[0] = typeItem.ToSequence();
            return (typeRes, types.ToImmutable());
        }

        int cargSeq = types.Count - 1;
        for (int i = 0; i < cargSeq; i++)
        {
            DType type = types[i];
            if (type.SeqCount == 0)
                types[i] = type.ToSequence();
        }

        // Promote the value expression to typeSrc.
        types[cargSeq] = typeSrc;

        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;

        DType typeItem = args[args.Length - 1].Type;
        if (args.Length == 1)
            typeItem = typeItem.ItemTypeOrThis;
        if (!typeItem.IsNumericXxx)
            return false;
        DType typeSrc = Promote(typeItem);
        DType typeAgg = AggFromSrc(typeSrc);
        Validation.Assert(typeAgg.IsNumericXxx);
        DType typeRes = ResFromAgg(typeAgg);
        if (type != typeRes)
            return false;

        if (args.Length == 1)
        {
            if (!args[0].Type.IsSequence)
                return false;
            Validation.Assert(args[0].Type == typeItem.ToSequence());
            return true;
        }

        if (typeItem != typeSrc)
            return false;
        int cseq = args.Length - 1;
        for (int slot = 0; slot < cseq; slot++)
        {
            if (!args[slot].Type.IsSequence)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Promotes an item-type/result-type to the desired numeric type.
    /// </summary>
    protected abstract DType Promote(DType typeDst);

    /// <summary>
    /// Get the aggregation type from the source type. Both are always numeric.
    /// </summary>
    protected abstract DType AggFromSrc(DType typeSrc);

    /// <summary>
    /// Get the result type from the aggregation type.
    /// The aggregation type is always numeric.
    /// The result type may be a tuple including the aggregation type.
    /// </summary>
    protected abstract DType ResFromAgg(DType typeAgg);

    /// <summary>
    /// Get the aggregation type from the result type.
    /// The aggregation type is always numeric.
    /// The result type may be a tuple including the aggregation type.
    /// </summary>
    public abstract DType AggFromRes(DType typeRes);

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Args.Length == call.Scopes.Length + 1);

        DType typeRes = call.Type;
        DType typeAgg = AggFromRes(typeRes);
        Validation.Assert(typeAgg.IsNumericXxx);

        int cseq = call.Scopes.Length;
        if (cseq > _maxSeqExplicit || cseq == 1 && call.Args[0] is BndCallNode bcn0 && bcn0.Oper is ForEachFunc)
        {
            // Rewrite using ForEach.
            var sel = call.Args[cseq];
            Validation.Assert(AggFromSrc(sel.Type) == typeAgg);
            BoundNode inner = reducer.Reduce(BndCallNode.Create(
                ForEachFunc.ForEach, sel.Type.ToSequence(), call.Args, call.Scopes, call.Indices));
            call = BndCallNode.Create(this, typeRes, Immutable.Array.Create(inner));
            Validation.Assert(IsValidCall(call));
            cseq = call.Scopes.Length;
        }

        // REVIEW: currently _maxSeqExplicit is 1, so cseq <= 1 is always true.
        if (cseq <= 1)
        {
            var (min, max) = call.Args[0].GetItemCountRange();
            if (max == 0)
                return BndDefaultNode.Create(call.Type);

            if (cseq == 0 && call.Args[0] is BndCallNode bcn1 && bcn1.Oper is ForEachFunc &&
                bcn1.Scopes.Length <= _maxSeqExplicit && !ForEachFunc.HasPredicate(bcn1))
            {
                // Rewrite from ForEach to explicit invocation.
                Validation.Assert(bcn1.Args.Length == bcn1.Scopes.Length + 1);

                cseq = bcn1.Scopes.Length;
                Validation.Assert(cseq > 0);
                Validation.Assert(bcn1.Indices.Length == 1);

                var args = bcn1.Args;
                var sel = args[cseq];
                Validation.Assert(bcn1.Type == sel.Type.ToSequence());
                Validation.Assert(sel.Type.IsNumericXxx);

                var typeSrc = Promote(sel.Type);
                Validation.Assert(AggFromSrc(typeSrc) == typeAgg);
                if (sel.Type != typeSrc)
                {
                    // Have to cast the selector.
                    Validation.Assert(typeSrc.Accepts(sel.Type, AcceptUseUnion));
                    Validation.Assert(typeSrc.IsOpt == sel.Type.IsOpt);
                    var selNew = reducer.Convert(sel, typeSrc, AcceptUseUnion);
                    Validation.Assert(selNew.Type == typeSrc);
                    args = args.SetItem(cseq, selNew);
                }

                return BndCallNode.Create(this, typeRes, args, bcn1.Scopes, bcn1.Indices);
            }
        }

        // REVIEW: Perhaps do other optimizations regarding ForEach?
        return call;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Rexl function which returns the absolute value of a number and preserves its type.
/// This means that for the minimum value of signed integer types, it will return the
/// same value. E.g. Abs(0x80i1) == -128.
/// </summary>
public sealed partial class AbsFunc : OneToOneMathFunc
{
    public static readonly AbsFunc Instance = new AbsFunc();

    private AbsFunc()
        : base(new DName("Abs"))
    {
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsNumericReq)
            return false;
        var args = call.Args;
        if (args[0].Type != type)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];

        // Abs is a no-op on unsigned types.
        if (arg.Type.Kind.IsUx())
            return arg;

        var typeRes = call.Type;
        if (typeRes.IsIntegralXxx && arg.TryGetIntegral(out var bi))
        {
            if (bi >= 0)
                return arg;
            switch (arg.Type.Kind)
            {
            case DKind.IA:
                return BndIntNode.CreateI(-bi);
            case DKind.I8:
                return BndIntNode.CreateI8(Exec((long)bi));
            case DKind.I4:
                return BndIntNode.CreateI4(Exec((int)bi));
            case DKind.I2:
                return BndIntNode.CreateI2(Exec((short)bi));
            case DKind.I1:
                return BndIntNode.CreateI1(Exec((sbyte)bi));
            }
            Validation.Assert(false);
        }
        else if (typeRes.IsFractionalXxx && arg.TryGetFractional(out var dbl))
        {
            // Test whether the high bit is set. This handles negative zero appropriately.
            if (!dbl.IsNegative())
                return arg;
            return BndFltNode.Create(arg.Type, -dbl);
        }

        return call;
    }

    public static long Exec(long x)
    {
        // We can't use Math.Abs because it will throw an exception when
        // given the evil values.
        return x < 0 ? -x : x;
    }

    public static int Exec(int x)
    {
        return x < 0 ? -x : x;
    }

    public static short Exec(short x)
    {
        return x < 0 ? (short)(-x) : x;
    }

    public static sbyte Exec(sbyte x)
    {
        return x < 0 ? (sbyte)(-x) : x;
    }
}

/// <summary>
/// Rexl functions which operate on and return R8.
/// </summary>
public sealed partial class R8Func : OneToOneMathFunc
{
    public const double MulDegToRad = Math.PI / 180;
    public const double MulRadToDeg = 180 / Math.PI;

    public static readonly R8Func Sqrt = new R8Func("Sqrt", Math.Sqrt);

    public static readonly R8Func Exp = new R8Func("Exp", Math.Exp);
    public static readonly R8Func Ln = new R8Func("Ln", Math.Log);
    public static readonly R8Func Log10 = new R8Func("Log10", Math.Log10);

    public static readonly R8Func Radians = new R8Func("Radians", ExecRadians);
    public static readonly R8Func Degrees = new R8Func("Degrees", ExecDegrees);

    public static readonly R8Func Sin = new R8Func("Sin", ExecSin);
    public static readonly R8Func Cos = new R8Func("Cos", ExecCos);
    public static readonly R8Func Tan = new R8Func("Tan", ExecTan);
    public static readonly R8Func Csc = new R8Func("Csc", ExecCsc);
    public static readonly R8Func Sec = new R8Func("Sec", ExecSec);
    public static readonly R8Func Cot = new R8Func("Cot", ExecCot);

    public static readonly R8Func SinD = new R8Func("SinD", ExecSinD);
    public static readonly R8Func CosD = new R8Func("CosD", ExecCosD);
    public static readonly R8Func TanD = new R8Func("TanD", ExecTanD);
    public static readonly R8Func CscD = new R8Func("CscD", ExecCscD);
    public static readonly R8Func SecD = new R8Func("SecD", ExecSecD);
    public static readonly R8Func CotD = new R8Func("CotD", ExecCotD);

    public static readonly R8Func Sinh = new R8Func("Sinh", Math.Sinh);
    public static readonly R8Func Cosh = new R8Func("Cosh", Math.Cosh);
    public static readonly R8Func Tanh = new R8Func("Tanh", Math.Tanh);
    public static readonly R8Func Csch = new R8Func("Csch", ExecCsch);
    public static readonly R8Func Sech = new R8Func("Sech", ExecSech);
    public static readonly R8Func Coth = new R8Func("Coth", ExecCoth);

    public static readonly R8Func Asin = new R8Func("Asin", Math.Asin);
    public static readonly R8Func Acos = new R8Func("Acos", Math.Acos);
    public static readonly R8Func Atan = new R8Func("Atan", Math.Atan);

    public static readonly R8Func Round = new R8Func("Round", Math.Round);
    public static readonly R8Func RoundUp = new R8Func("RoundUp", Math.Ceiling);
    public static readonly R8Func RoundDown = new R8Func("RoundDown", Math.Floor);
    public static readonly R8Func RoundIn = new R8Func("RoundIn", Math.Truncate);
    public static readonly R8Func RoundOut = new R8Func("RoundOut", ExecRoundOut);

    /// <summary>
    /// A delegate that performs the operation.
    /// </summary>
    public Func<double, double> Map { get; }

    private R8Func(string name, Func<double, double> fn)
        : base(new DName(name))
    {
        Validation.AssertValue(fn);
        Map = fn;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        return (DType.R8Req, Immutable.Array.Create(DType.R8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.R8Req)
            return false;
        var args = call.Args;
        if (args[0].Type != type)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (arg.TryGetFractional(out var value))
            return BndFltNode.CreateCast(arg.Type, Map(value));

        return call;
    }

    public static double ExecRadians(double value)
    {
        return value * MulDegToRad;
    }

    public static double ExecDegrees(double value)
    {
        return value * MulRadToDeg;
    }

    public static double ExecSin(double value)
    {
        bool neg = false;
        if ((long)value.ToBits() < 0)
        {
            neg = true;
            value = -value;
        }
        value %= 2 * Math.PI;
        if (value > Math.PI)
        {
            neg = !neg;
            value = 2 * Math.PI - value;
        }
        if (value >= Math.PI / 2)
            value = Math.PI - value;
        var res = value >= Math.PI / 4 ? Math.Cos(Math.PI / 2 - value) : Math.Sin(value);
        return neg ? -res : res;
    }

    public static double ExecSinD(double value)
    {
        bool neg = false;
        if ((long)value.ToBits() < 0)
        {
            neg = true;
            value = -value;
        }
        value %= 360;
        if (value > 180)
        {
            neg = !neg;
            value = 360 - value;
        }
        if (value >= 90)
            value = 180 - value;
        var res = value >= 45 ? Math.Cos((90 - value) * MulDegToRad) : Math.Sin(value * MulDegToRad);
        return neg ? -res : res;
    }

    public static double ExecCos(double value)
    {
        bool neg = false;
        if ((long)value.ToBits() < 0)
            value = -value;
        value %= 2 * Math.PI;
        if (value > Math.PI)
            value = 2 * Math.PI - value;
        if (value > Math.PI / 2)
        {
            neg = true;
            value = Math.PI - value;
        }
        var res = value >= Math.PI / 4 ? Math.Sin(Math.PI / 2 - value) : Math.Cos(value);
        return neg ? -res : res;
    }

    public static double ExecCosD(double value)
    {
        bool neg = false;
        if ((long)value.ToBits() < 0)
            value = -value;
        value %= 360;
        if (value >= 180)
            value = 360 - value;
        if (value > 90)
        {
            neg = true;
            value = 180 - value;
        }
        var res = value >= 45 ? Math.Sin((90 - value) * MulDegToRad) : Math.Cos(value * MulDegToRad);
        return neg ? -res : res;
    }

    public static double ExecTan(double value)
    {
        bool neg = false;
        if ((long)value.ToBits() < 0)
        {
            neg = true;
            value = -value;
        }
        value %= Math.PI;
        if (value > Math.PI / 2)
        {
            neg = !neg;
            value = Math.PI - value;
        }
        double res;
        if (value == Math.PI / 2)
            res = double.PositiveInfinity;
        else
            res = value >= Math.PI / 4 ? 1 / Math.Tan(Math.PI / 2 - value) : Math.Tan(value);
        return neg ? -res : res;
    }

    public static double ExecTanD(double value)
    {
        bool neg = false;
        if ((long)value.ToBits() < 0)
        {
            neg = true;
            value = -value;
        }
        value %= 180;
        if (value > 90)
        {
            neg = !neg;
            value = 180 - value;
        }
        double res;
        if (value == 90)
            res = double.PositiveInfinity;
        else
            res = value >= 45 ? 1 / Math.Tan((90 - value) * MulDegToRad) : Math.Tan(value * MulDegToRad);
        return neg ? -res : res;
    }

    public static double ExecCsc(double value) => 1 / ExecSin(value);
    public static double ExecCscD(double value) => 1 / ExecSinD(value);
    public static double ExecSec(double value) => 1 / ExecCos(value);
    public static double ExecSecD(double value) => 1 / ExecCosD(value);
    public static double ExecCot(double value) => 1 / ExecTan(value);
    public static double ExecCotD(double value) => 1 / ExecTanD(value);

    public static double ExecCsch(double value) => 1 / Math.Sinh(value);
    public static double ExecSech(double value) => 1 / Math.Cosh(value);
    public static double ExecCoth(double value) => 1 / Math.Tanh(value);

    public static double ExecRoundOut(double value)
    {
        if ((long)value.ToBits() < 0)
            return -Math.Ceiling(-value);
        return Math.Ceiling(value);
    }
}

public abstract partial class SumBaseFunc : MathAggFunc
{
    public enum SumKind : byte
    {
        /// <summary>
        /// Use standard precision, r8, i8, u8, or ia.
        /// </summary>
        Normal,

        /// <summary>
        /// Use "big" precision, r8 or ia.
        /// </summary>
        Big,

        /// <summary>
        /// Uses Neumaier's "improved Kahan-Babuska algorithm". The agg and result types are r8.
        /// See https://en.wikipedia.org/wiki/Kahan_summation_algorithm.
        /// </summary>
        Kahan,

        /// <summary>
        /// Generate the mean by doing Kahan followed by a division.
        /// </summary>
        Mean,
    }

    public readonly SumKind Kind;
    public readonly bool WithCount;

    // REVIEW: Should we use a larger maxSeqExplicit?
    private protected SumBaseFunc(string name, SumKind kind, bool withCount)
        : base(new DName(name), maxSeqExplicit: 1)
    {
        Kind = kind;
        WithCount = withCount;
    }

    protected override DType Promote(DType typeDst)
    {
        switch (Kind)
        {
        case SumKind.Big:
            return PromoteBySizeBig(typeDst);
        case SumKind.Kahan:
        case SumKind.Mean:
            return typeDst.IsOpt ? DType.R8Opt : DType.R8Req;
        default:
            Validation.Assert(Kind == SumKind.Normal);
            return PromoteBySize(typeDst);
        }
    }

    protected override DType AggFromSrc(DType typeSrc)
    {
        Validation.Assert(typeSrc.IsNumericXxx);
        Validation.Assert(Promote(typeSrc) == typeSrc);
        return typeSrc.ToReq();
    }

    protected override DType ResFromAgg(DType typeAgg)
    {
        Validation.Assert(typeAgg.IsNumericReq);
        if (!WithCount)
            return typeAgg;
        return DType.CreateRecord(false,
            new TypedName(StatFuncUtil.NameCount, DType.I8Req),
            new TypedName(Kind == SumKind.Mean ? StatFuncUtil.NameMean : StatFuncUtil.NameSum, typeAgg));
    }

    public override DType AggFromRes(DType typeRes)
    {
        DType typeAgg;
        if (typeRes.Kind == DKind.Record)
        {
            Validation.Assert(!typeRes.IsOpt);
            typeRes.TryGetNameType(Kind == SumKind.Mean ? StatFuncUtil.NameMean : StatFuncUtil.NameSum, out typeAgg).Verify();
        }
        else
            typeAgg = typeRes;
        Validation.Assert(typeAgg.IsNumericReq);
        Validation.Assert(ResFromAgg(typeAgg) == typeRes);
        return typeAgg;
    }
}

public sealed partial class SumFunc : SumBaseFunc
{
    public static readonly SumFunc Sum = new SumFunc("Sum", SumKind.Normal, withCount: false);
    public static readonly SumFunc SumC = new SumFunc("SumC", SumKind.Normal, withCount: true);
    public static readonly SumFunc SumBig = new SumFunc("SumBig", SumKind.Big, withCount: false);
    public static readonly SumFunc SumBigC = new SumFunc("SumBigC", SumKind.Big, withCount: true);
    public static readonly SumFunc SumK = new SumFunc("SumK", SumKind.Kahan, withCount: false);
    public static readonly SumFunc SumKC = new SumFunc("SumKC", SumKind.Kahan, withCount: true);

    private SumFunc(string name, SumKind kind, bool withCount)
        : base(name, kind, withCount)
    {
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        var res = base.ReduceCore(reducer, call);

        // Sum of an explicit sequence, with no conversion, can be written as an add.
        if (!(res is BndCallNode call2))
            return res;
        if (call2.Oper != Sum || call2.Args.Length != 1 || call2.Args[0].Type != call2.Type.ToSequence())
            return res;
        Validation.Assert(call2.Type.IsNumericReq);
        if (!(call2.Args[0] is BndSequenceNode bsn))
            return res;
        return reducer.Reduce(BndVariadicOpNode.Create(call2.Type, BinaryOp.Add, bsn.Items, default));
    }
}

public sealed partial class MeanFunc : SumBaseFunc
{
    public static readonly MeanFunc Mean = new MeanFunc("Mean", withCount: false);
    public static readonly MeanFunc MeanC = new MeanFunc("MeanC", withCount: true);

    private MeanFunc(string name, bool withCount)
        : base(name, SumKind.Mean, withCount)
    {
    }
}

public sealed partial class MinMaxFunc : MathAggFunc
{
    [Flags]
    public enum Parts
    {
        Min = 0x01,
        Max = 0x02,
        MinMax = Min | Max,
        Count = 0x04,

        // This is only used by code gen.
        _Indexed = 0x08
    }

    public static readonly MinMaxFunc Min = new MinMaxFunc("Min", kind: Parts.Min);
    public static readonly MinMaxFunc Max = new MinMaxFunc("Max", kind: Parts.Max);
    public static readonly MinMaxFunc MinMax = new MinMaxFunc("MinMax", kind: Parts.Min | Parts.Max);
    public static readonly MinMaxFunc MinC = new MinMaxFunc("MinC", kind: Parts.Min | Parts.Count);
    public static readonly MinMaxFunc MaxC = new MinMaxFunc("MaxC", kind: Parts.Max | Parts.Count);
    public static readonly MinMaxFunc MinMaxC = new MinMaxFunc("MinMaxC", kind: Parts.Min | Parts.Max | Parts.Count);

    /// <summary>
    /// The parts that this function returns.
    /// </summary>
    public Parts Kind { get; }

    public bool WithMin => (Kind & Parts.Min) != 0;
    public bool WithMax => (Kind & Parts.Max) != 0;
    public bool WithBoth => (Kind & Parts.MinMax) == Parts.MinMax;
    public bool WithCount => (Kind & Parts.Count) != 0;

    private MinMaxFunc(string name, Parts kind)
        : base(new DName(name), maxSeqExplicit: 1)
    {
        Validation.Assert(kind != 0);
        Kind = kind;
    }

    protected override DType Promote(DType typeDst)
    {
        Validation.Assert(typeDst.IsNumericXxx);
        return typeDst;
    }

    protected override DType AggFromSrc(DType typeSrc)
    {
        Validation.Assert(typeSrc.IsNumericXxx);
        Validation.Assert(Promote(typeSrc) == typeSrc);
        return typeSrc.ToReq();
    }

    protected override DType ResFromAgg(DType typeAgg)
    {
        Validation.Assert(typeAgg.IsNumericReq);
        if (!WithCount && !WithBoth)
            return typeAgg;

        var typeRes = DType.EmptyRecordReq;
        if (WithCount)
            typeRes = typeRes.AddNameType(StatFuncUtil.NameCount, DType.I8Req);
        if (WithMin)
            typeRes = typeRes.AddNameType(StatFuncUtil.NameMin, typeAgg);
        if (WithMax)
            typeRes = typeRes.AddNameType(StatFuncUtil.NameMax, typeAgg);
        return typeRes;
    }

    public override DType AggFromRes(DType typeRes)
    {
        DType typeAgg;
        if (typeRes.Kind == DKind.Record)
        {
            Validation.Assert(!typeRes.IsOpt);
            if (!typeRes.TryGetNameType(StatFuncUtil.NameMin, out typeAgg))
                typeRes.TryGetNameType(StatFuncUtil.NameMax, out typeAgg).Verify();
        }
        else
            typeAgg = typeRes;
        Validation.Assert(typeAgg.IsNumericReq);
        Validation.Assert(ResFromAgg(typeAgg) == typeRes);
        return typeAgg;
    }
}

/// <summary>
/// Produces an arithmetic sequence of integers.
/// REVIEW: Should we make this support floating point, and generalize across integer types?
/// </summary>
public sealed partial class RangeFunc : RexlOper
{
    public static readonly RangeFunc Instance = new RangeFunc();

    private RangeFunc()
        : base(isFunc: true, new DName("Range"), 1, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.I8Req.ToSequence(), Immutable.Array.Fill(DType.I8Req, info.Arity));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.I8Req.ToSequence())
            return false;
        var args = call.Args;
        for (int slot = 0; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        int carg = args.Length;

        long num;

        Validation.BugCheckParam(args[0].Type == DType.I8Req, nameof(call));
        if (!args[0].TryGetIntegral(out var val0))
            return base.GetItemCountRangeCore(call);
        Validation.Assert(val0 <= long.MaxValue & val0 >= long.MinValue);
        var num0 = (long)val0;
        if (carg == 1)
            num = Math.Max(0, num0);
        else
        {
            Validation.BugCheckParam(args[1].Type == DType.I8Req, nameof(call));
            if (!args[1].TryGetIntegral(out var val1))
                return base.GetItemCountRangeCore(call);
            Validation.Assert(val1 <= long.MaxValue & val1 >= long.MinValue);
            var num1 = (long)val1;
            if (carg == 2)
                num = Util.GetCount(num0, num1);
            else
            {
                Validation.BugCheckParam(args[2].Type == DType.I8Req, nameof(call));
                if (!args[2].TryGetIntegral(out var val2))
                    return base.GetItemCountRangeCore(call);
                Validation.Assert(val2 <= long.MaxValue & val2 >= long.MinValue);
                var num2 = (long)val2;
                num = Util.GetCount(num0, num1, num2);
            }
        }

        return (num, num);
    }
}

/// <summary>
/// Produces an arithmetic sequence of integers. First arg is the number of items.
/// Optional second arg is the start value, defaulting to one (to match pfx).
/// Optional third arg is the step/increment value, defaulting to one.
/// This supports the standard numeric types for start and increment, meaning,
/// r8, ia, i8, u8. If there is only one arg (the count), the item type is i8,
/// otherwise the item type is determined from the 2nd and 3rd args.
/// </summary>
public sealed partial class SequenceFunc : RexlOper
{
    public static readonly SequenceFunc Instance = new SequenceFunc();

    private SequenceFunc()
        : base(isFunc: true, new DName("Sequence"), 1, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        DType typeItem;
        switch (info.Arity)
        {
        default:
            Validation.Assert(info.Arity == 1);
            return (DType.I8Req.ToSequence(), Immutable.Array.Create(DType.I8Req));
        case 2:
            typeItem = DType.GetNumericBinaryType(info.Args[1].Type.RootKind);
            return (typeItem.ToSequence(), Immutable.Array.Create(DType.I8Req, typeItem));
        case 3:
            typeItem = DType.GetNumericBinaryType(info.Args[1].Type.RootKind, info.Args[2].Type.RootKind);
            return (typeItem.ToSequence(), Immutable.Array.Create(DType.I8Req, typeItem, typeItem));
        }
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type.SeqCount != 1)
            return false;
        var typeItem = type.ItemTypeOrThis;
        if (!typeItem.IsNumericReq)
            return false;
        if (typeItem.RootKind.NumericSize() < 8)
            return false;

        switch (call.Args.Length)
        {
        default:
            Validation.Assert(false);
            return false;
        case 1:
            if (typeItem != DType.I8Req)
                return false;
            break;
        case 2:
            if (call.Args[1].Type != typeItem)
                return false;
            break;
        case 3:
            if (call.Args[1].Type != typeItem)
                return false;
            if (call.Args[2].Type != typeItem)
                return false;
            break;
        }
        if (call.Args[0].Type != DType.I8Req)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        if (call.Args[0].TryGetI8(out var num) && num <= 0)
            return BndNullNode.Create(call.Type);
        return base.ReduceCore(reducer, call);
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Args[0].Type == DType.I8Req);

        if (!call.Args[0].TryGetI8(out var num))
            return base.GetItemCountRangeCore(call);
        if (num < 0)
            num = 0;
        return (num, num);
    }
}

public abstract class BinaryNumericFunc : MathFunc
{
    internal BinaryNumericFunc(string name)
        : base(new DName(name), 2, 2)
    {
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
        Validation.Assert(info.Arity == 2);

        var typeRes = DType.GetNumericBinaryType(info.Args[0].Type.RootKind, info.Args[1].Type.RootKind);
        return (typeRes, Immutable.Array.Create(typeRes, typeRes));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var typeRes = DType.GetNumericBinaryType(type.RootKind, type.RootKind);
        if (type != typeRes)
            return false;
        var args = call.Args;
        if (args[0].Type != typeRes)
            return false;
        if (args[1].Type != typeRes)
            return false;
        return true;
    }
}

/// <summary>
/// The Rexl function which returns the truncated quotient of two numbers x and y.
/// It follows C# semantics for integer division and truncated double division, except when y is 0.
/// In that case, for integer types it returns 0, and for rational types it returns infinity
/// with the same sign as the normal quotient.
/// </summary>
public sealed partial class DivFunc : BinaryNumericFunc
{
    public static readonly DivFunc Instance = new DivFunc();

    private DivFunc()
        : base("Div")
    {
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var typeRes = call.Type;
        var args = call.Args;
        Validation.Assert(args.Length == 2);
        Validation.Assert(args[0].Type == typeRes);
        Validation.Assert(args[1].Type == typeRes);

        if (typeRes.IsIntegralXxx && args[1].TryGetIntegral(out var yi))
        {
            if (args[0].TryGetIntegral(out var xi))
            {
                switch (typeRes.Kind)
                {
                case DKind.IA:
                    return BndIntNode.CreateI(Exec(xi, yi));
                case DKind.I8:
                    return BndIntNode.CreateI8(Exec((long)xi, (long)yi));
                case DKind.U8:
                    return BndIntNode.CreateU8(Exec((ulong)xi, (ulong)yi));
                }
                Validation.Assert(false);
            }
            else if (yi == 0)
                return args[1];
            else if (yi == 1)
                return args[0];
        }
        else if (typeRes.IsFractionalXxx &&
            args[0].TryGetFractional(out var xr) && args[1].TryGetFractional(out var yr))
        {
            switch (typeRes.Kind)
            {
            case DKind.R8:
                return BndFltNode.CreateR8(Exec(xr, yr));
            }
            Validation.Assert(false);
        }

        return call;
    }

    #region Exec implementations

    public static double Exec(double x, double y)
    {
        return Math.Truncate(x / y);
    }

    public static Integer Exec(Integer x, Integer y)
    {
        // REVIEW: is this the behavior we want for y == 0?
        if (y == 0)
            return 0;
        return x / y;
    }

    public static long Exec(long x, long y)
    {
        if (y == 0)
            return 0;
        // When x is the evil value, C# will throw an overflow exception, so we need to case on it.
        if (y == -1)
            return -x;
        return x / y;
    }

    public static ulong Exec(ulong x, ulong y)
    {
        if (y == 0)
            return 0;
        return x / y;
    }

    #endregion
}

/// <summary>
/// The Rexl function which returns the remainder of two numbers x and y.
/// It follows C# semantics for modulus, except when y is 0. In that case,
/// for integer types it returns 0, and for rational types it returns 0
/// with the same sign as x.
/// </summary>
public sealed partial class ModFunc : BinaryNumericFunc
{
    public static readonly ModFunc Instance = new ModFunc();

    private ModFunc()
        : base("Mod")
    {
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var typeRes = call.Type;
        var args = call.Args;
        Validation.Assert(args.Length == 2);
        Validation.Assert(args[0].Type == typeRes);
        Validation.Assert(args[1].Type == typeRes);

        if (typeRes.IsIntegralXxx && args[1].TryGetIntegral(out var yi))
        {
            // When yi is +1 or -1, the result is zero, so might as well treat xi as zero.
            if (args[0].TryGetIntegral(out var xi) || yi == 1 || yi == -1)
            {
                switch (typeRes.Kind)
                {
                case DKind.IA:
                    return BndIntNode.CreateI(Exec(xi, yi));
                case DKind.I8:
                    return BndIntNode.CreateI8(Exec((long)xi, (long)yi));
                case DKind.U8:
                    return BndIntNode.CreateU8(Exec((ulong)xi, (ulong)yi));
                }
                Validation.Assert(false);
            }
            else if (yi == 0)
                return args[1];
        }
        else if (typeRes.IsFractionalXxx &&
            args[0].TryGetFractional(out var xr) && args[1].TryGetFractional(out var yr))
        {
            switch (typeRes.Kind)
            {
            case DKind.R8:
                return BndFltNode.CreateR8(Exec(xr, yr));
            }
            Validation.Assert(false);
        }

        return call;
    }

    #region Exec implementations

    public static double Exec(double x, double y)
    {
        // We define Mod(x,0.0) to be sign(x) * 0.0.
        // If x is nonzero and not NaN, we can do a simple comparison against 0.0.
        // This includes infinity, since positive infinity > 0.0 and negative infinity < 0.0.
        // Otherwise we can multiply x by 0.0 to get a zero value with the correct sign.
        // This gives the correct answer in the case of positive/negative 0.0.
        // This also covers the NaN case, since NaN * 0.0 == NaN.
        if (y == 0)
            return x > 0.0 ? 0.0 : x < 0.0 ? -0.0 : x * 0.0;
        return x % y;
    }

    public static Integer Exec(Integer x, Integer y)
    {
        if (-1 <= y && y <= 1)
            return 0;
        return x % y;
    }

    public static long Exec(long x, long y)
    {
        if (-1 <= y && y <= 1)
            return 0;
        return x % y;
    }

    public static ulong Exec(ulong x, ulong y)
    {
        if (y <= 1)
            return 0;
        return x % y;
    }

    #endregion
}

/// <summary>
/// The Rexl function which, given x and y, rounds x towards 0 to the nearest multiple of y.
/// When y is an integer type and equals 0, and when it is a rational type and close to 0,
/// it returns x.
/// </summary>
public sealed partial class BinFunc : BinaryNumericFunc
{
    public static readonly BinFunc Instance = new BinFunc();

    private BinFunc()
        : base("Bin")
    {
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var typeRes = call.Type;
        var args = call.Args;
        Validation.Assert(args.Length == 2);
        Validation.Assert(args[0].Type == typeRes);
        Validation.Assert(args[1].Type == typeRes);

        if (typeRes.IsIntegralXxx && args[1].TryGetIntegral(out var yi))
        {
            if (args[0].TryGetIntegral(out var xi))
            {
                switch (typeRes.Kind)
                {
                case DKind.IA:
                    return BndIntNode.CreateI(Exec(xi, yi));
                case DKind.I8:
                    return BndIntNode.CreateI8(Exec((long)xi, (long)yi));
                case DKind.U8:
                    return BndIntNode.CreateU8(Exec((ulong)xi, (ulong)yi));
                }
                Validation.Assert(false);
            }
            else if (-1 <= yi && yi <= 1)
                return args[0];
        }
        else if (typeRes.IsFractionalXxx && args[1].TryGetFractional(out var yr))
        {
            if (args[0].TryGetFractional(out var xr))
            {
                switch (typeRes.Kind)
                {
                case DKind.R8:
                    return BndFltNode.CreateR8(Exec(xr, yr));
                }
                Validation.Assert(false);
            }
            else if (yr == 0)
                return args[0];
        }

        return call;
    }

    #region Exec implementations

    public static double Exec(double x, double y)
    {
        // If this condition is true, then y is insignificant compared to x
        // or is close to 0, so we return x to avoid underflow.
        // REVIEW: This produces different results for Bin(1/0, 1/0) and
        // Bin(1/0, -1/0). We need to think about the proper test.
        if (x + y == x)
            return x;
        return Math.Truncate(x / y) * y;
    }

    public static Integer Exec(Integer x, Integer y)
    {
        if (-1 <= y && y <= 1)
            return x;
        return (x / y) * y;
    }

    public static long Exec(long x, long y)
    {
        if (-1 <= y && y <= 1)
            return x;
        return (x / y) * y;
    }

    public static ulong Exec(ulong x, ulong y)
    {
        if (y <= 1)
            return x;
        return (x / y) * y;
    }

    #endregion
}
