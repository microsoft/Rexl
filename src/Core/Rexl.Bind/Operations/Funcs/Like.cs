// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Type testing functions.
/// <c>LikeOrNull(a, b)</c> produces <c>a</c> if the runtime type of <c>a</c> is compatible with
/// the compile-time type of <c>b</c> and produces <c>null</c> otherwise.
/// <c>LikeOrDef(a, b)</c> is similar but produces the default value of the type, rathe than <c>null</c>.
/// <c>LikeOrVal(a, b)</c> is similar but produces the <c>b</c>, rathe than <c>null</c>.
/// <c>Like(a, b)</c> is an alias for <c>LikdOrNull(a, b)</c>.
/// <c>LikeOr(a, b)</c> is an alias for <c>LikdOrVal(a, b)</c>.
/// </summary>
public sealed partial class LikeFunc : RexlOper
{
    public enum OrKind : byte
    {
        Null,
        Default,
        Value,
    }

    public static readonly LikeFunc LikeOrNul = new LikeFunc("LikeOrNull", OrKind.Null);
    public static readonly LikeFunc LikeOrDef = new LikeFunc("LikeOrDef", OrKind.Default);
    public static readonly LikeFunc LikeOrVal = new LikeFunc("LikeOrVal", OrKind.Value);

    public OrKind Kind { get; }

    private LikeFunc(string name, OrKind kind)
        : base(isFunc: true, new DName(name), 2, 2)
    {
        Kind = kind;
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

        var typeSrc = info.Args[0].Type;
        var typeVal = info.Args[1].Type;

        if (!IsGoodValTypeCore(typeVal))
        {
            info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(1),
                ErrorStrings.ErrLikeVal_Func_Type, Name, typeVal));
        }
        else if (!WantsTypeTestCore(typeSrc, typeVal, out _))
        {
            info.PostDiagnostic(RexlDiagnostic.Warning(info.GetParseArg(0),
                ErrorStrings.WrnLikeSrcNotGeneral_Func_Type, Name, typeSrc));
        }

        var typeRes = typeVal;
        if (Kind == OrKind.Null)
            typeRes = typeRes.ToOpt();

        return (typeRes, Immutable.Array.Create(typeSrc, typeVal));
    }

    /// <summary>
    /// Whether the given <paramref name="type"/> is ok to use as a source type for
    /// an invocation of a <c>Like</c> function.
    /// </summary>
    public static bool IsGoodValType(DType type)
    {
        return IsGoodValTypeCore(type, nested: false);
    }

    /// <summary>
    /// Whether the given <paramref name="type"/> is ok to use as a source type for
    /// an invocation of a <c>Like</c> function.
    /// </summary>
    private static bool IsGoodValTypeCore(DType type, bool nested = false)
    {
        switch (type.Kind)
        {
        case DKind.General:
        case DKind.Vac:
            return false;

        case DKind.Sequence:
            // REVIEW: Support sequence.
            return false;

        case DKind.Tuple:
        case DKind.Module:
            // REVIEW: Support aggregate types.
            return false;

        case DKind.Record:
            return !nested;

        case DKind.Tensor:
            return IsGoodValTypeCore(type.GetTensorItemType(), nested: true);

        case DKind.Uri:
        case DKind.Text:
        case DKind.R8:
        case DKind.R4:
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
        case DKind.Bit:
        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
            return true;

        default:
            Validation.Assert(false);
            return false;
        }
    }

    /// <summary>
    /// Whether the given combination of source and destination type requires a run-time type
    /// test to determine the outcome. This asserts that <paramref name="typeDst"/> passes
    /// <see cref="IsGoodValType(DType)"/>.
    /// </summary>
    public static bool WantsTypeTest(DType typeSrc, DType typeDst)
    {
        return WantsTypeTestCore(typeSrc, typeDst, out _);
    }

    /// <summary>
    /// Whether the given combination of source and destination type requires a run-time type
    /// test to determine the outcome. This asserts that <paramref name="typeDst"/> passes
    /// <see cref="IsGoodValType(DType)"/>. If this returns false and <paramref name="refConv"/>
    /// is set to <c>true</c> then there is a "reference conversion" from the source type to
    /// destination type.
    /// </summary>
    private static bool WantsTypeTestCore(DType typeSrc, DType typeDst, out bool refConv)
    {
        Validation.Assert(IsGoodValTypeCore(typeDst));

        refConv = false;
        if (typeSrc == DType.General)
            return true;

        if (typeDst == typeSrc)
            return false;
        if (!typeSrc.IsRecordXxx)
            return false;
        if (!typeDst.IsRecordXxx)
            return false;
        if (!typeSrc.SameFieldReqs(typeDst))
            return false;

        var have = typeSrc.GetFieldOpts();
        var want = typeDst.GetFieldOpts();
        if (have.IsSubset(want))
        {
            // The src is automatically an instance of dst.
            refConv = true;
            return false;
        }
        return true;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var typeSrc = call.Args[0].Type;
        var typeVal = call.Args[1].Type;

        if (!IsGoodValType(typeVal))
            full = false;
        else if (!WantsTypeTestCore(typeSrc, typeVal, out _))
            full = false;

        var typeRes = typeVal;
        if (Kind == OrKind.Null)
            typeRes = typeRes.ToOpt();
        if (call.Type != typeRes)
            return false;

        if (Kind != OrKind.Value)
            full = false;

        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var src = call.Args[0];
        if (Kind != OrKind.Value)
        {
            // Reduce to LikeOrVal with the appropriate value. Note that if the result type
            // is opt, the "default value" is null, so can always use BndDefaultNode.Create.
            Validation.Assert(Kind == OrKind.Null | Kind == OrKind.Default);
            Validation.Assert(call.Type.IsOpt | Kind == OrKind.Default);
            return reducer.Reduce(BndCallNode.Create(LikeOrVal, call.Type,
                Immutable.Array.Create(src, BndDefaultNode.Create(call.Type))));
        }

        var val = call.Args[1];
        var typeVal = val.Type;
        Validation.Assert(typeVal == call.Type);

        if (!IsGoodValType(typeVal))
            return call;

        var typeSrc = src.Type;
        if (WantsTypeTestCore(typeSrc, typeVal, out bool refConv))
        {
            if (src is BndCastBoxNode box)
                return reducer.Reduce(BndCallNode.Create(this, call.Type, Immutable.Array.Create(box.Child, val)));
            if (src is BndCastRefNode crn)
                return reducer.Reduce(BndCallNode.Create(this, call.Type, Immutable.Array.Create(crn.Child, val)));
            return call;
        }

        // This handles the case when the conversion from the src record type to dst record type
        // is a reference conversion. That is, the two have the same req field types and the
        // opt bits of the src are a subset of the opt bits of the dst.
        if (refConv)
            return BndCastRefNode.Create(src, typeVal);

        // Determine which way it should go, src or val.
        if (typeSrc.ToReq() != typeVal.ToReq())
            return val;

        if (!typeSrc.IsOpt)
        {
            if (!typeVal.IsOpt)
            {
                Validation.Assert(typeSrc == typeVal);
                return src;
            }
            Validation.Assert(typeSrc.ToOpt() == typeVal);
            return BndCastOptNode.Create(src);
        }

        // Need to do a coalesce operation.
        return reducer.Reduce(BndBinaryOpNode.Create(typeVal, BinaryOp.Coalesce, src, val));
    }
}
