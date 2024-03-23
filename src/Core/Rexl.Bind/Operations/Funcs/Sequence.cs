// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

/// <summary>
/// Sort sequence function.
/// </summary>
public sealed partial class SortFunc : RexlOper
{
    public static readonly SortFunc Sort = new SortFunc("Sort", false, true);
    public static readonly SortFunc SortUp = new SortFunc("SortUp", false, false);
    public static readonly SortFunc SortDown = new SortFunc("SortDown", true, true);

    /// <summary>
    /// Whether numeric types and types like Date and Time sort down (largest to smallest).
    /// </summary>
    public readonly bool IsDownNumeric;

    /// <summary>
    /// Whether other types, such as text and Guid, sort down.
    /// </summary>
    public readonly bool IsDownOther;

    private SortFunc(string name, bool isDownOther, bool isDownNumeric)
        : base(isFunc: true, new DName(name), 1, int.MaxValue)
    {
        IsDownOther = isDownOther;
        IsDownNumeric = isDownNumeric;
    }

    public bool IsDown(DType type)
    {
        Validation.BugCheckParam(type.IsSortable, nameof(type));

        if (IsDownNumeric == IsDownOther)
            return IsDownNumeric;

        var kind = type.RootKind;
        if (kind.IsNumeric())
            return IsDownNumeric;
        if (kind.IsChrono())
            return IsDownNumeric;

        return IsDownOther;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: false, carg);
        return ArgTraitsZip.Create(this, indexed: true, eager: false, carg, seqCount: 1);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        // Directives on any keys. Also allowed on the sequence when there are no keys.
        if (slot == 0 && traits.SlotCount > 1)
            return false;

        switch (dir)
        {
        case Directive.Ci:
        case Directive.Up:
        case Directive.Down:
        case Directive.UpCi:
        case Directive.DownCi:
            return true;
        }

        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);

        var types = info.GetArgTypes();
        var dirs = info.Dirs;
        Validation.Assert(types != null);
        Validation.Assert(types.Count >= 1);

        if (!dirs.IsDefault)
        {
            // Validate the directives.
            Validation.Assert(dirs.Length == types.Count);

            // Only look at the legal slots, namely not the 0th if dirs.Length > 1, since the binder
            // will complain on that one.
            for (int i = Math.Min(1, dirs.Length - 1); i < dirs.Length; i++)
            {
                // Validate the directive, if present.
                var dir = dirs[i];
                if (dir != default)
                {
                    switch (dir)
                    {
                    case Directive.Ci:
                    case Directive.Up:
                    case Directive.Down:
                    case Directive.UpCi:
                    case Directive.DownCi:
                        break;
                    default:
                        var parg = info.GetParseArg(i);
                        var dn = parg as DirectiveNode;
                        if (dn != null)
                            info.PostDiagnostic(RexlDiagnostic.Error(dn.DirToken, parg, ErrorStrings.ErrBadDirective));
                        else
                            info.PostDiagnostic(RexlDiagnostic.Error(parg, ErrorStrings.ErrBadDirective));
                        break;
                    }
                }
            }
        }

        // REVIEW: Do we ever need to do ToOpt?
        EnsureTypeSeq(types, 0);
        DType typeRet = types[0];
        if (types.Count == 1)
        {
            Validation.Assert(info.ParseArity <= 1);
            DType typeCmp = typeRet.ItemTypeOrThis;
            if (!typeCmp.IsSortable && info.ParseArity > 0)
            {
                info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0),
                    ErrorStrings.ErrNeedSortSelector_Type, typeCmp));
            }
        }
        else
        {
            Validation.Assert(info.ParseArity == info.Arity);
            for (int i = 1; i < types.Count; i++)
            {
                DType typeCmp = types[i];
                if (!typeCmp.IsSortable)
                {
                    info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(i),
                        ErrorStrings.ErrNotSortableType_Type, typeCmp));
                }
            }
        }

        return (typeRet, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;

        if (!type.IsSequence)
            return false;
        if (args[0].Type != type)
            return false;

        if (args.Length == 1)
        {
            var typeCmp = type.ItemTypeOrThis;
            if (!typeCmp.IsSortable)
                full = false;
        }
        else
        {
            for (int slot = 1; slot < args.Length; slot++)
            {
                if (!args[slot].Type.IsSortable)
                    full = false;
                if (args[slot].HasVolatile)
                    full = false;
            }
        }
        return true;
    }

    public override bool HasBadVolatile(BndCallNode call, out int slot)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.HasVolatile);

        // The sequence can be volatile without issue. The keys can't be.
        for (int i = 1; i < call.Args.Length; i++)
        {
            if (call.Args[i].HasVolatile)
            {
                slot = i;
                return true;
            }
        }

        slot = -1;
        return false;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        return call.Args[0].GetItemCountRange();
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg == 0);
        return PullWithFlags.Both;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.AssertValue(call);

        // REVIEW: Reductions on the selectors.
        // Non-exhaustive list of selectors that can be reduced/eliminated:
        // * Any selectors that come after a selector which is just the index.
        // * Constant selectors.
        // * Identity selectors.
        // * Selectors with values invariant to the item/index.
        // * Selectors that don't reference the item/index at all.
        // * Linear transformations on the item, e.g
        //   Sort(Range(10), 2 * it + 1) == Sort(Range(10)).
        //   A negative multiplier/determinant reverses the sort order.
        //   (Edge cases? Overflow? Floating point?)
        return base.ReduceCore(reducer, call);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Distinct sequence function.
/// </summary>
public sealed partial class DistinctFunc : RexlOper
{
    public static readonly DistinctFunc Instance = new DistinctFunc();

    private DistinctFunc()
        : base(isFunc: true, new DName("Distinct"), 1, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: false, carg);
        Validation.Assert(carg == 2);
        return ArgTraitsZip.Create(this, indexed: true, eager: false, 2, 1);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        // The key slots allow an optional [key] or ci directive.
        switch (dir)
        {
        case Directive.Key:
            return slot == 1;
        case Directive.Ci:
        case Directive.Eq:
        case Directive.EqCi:
            return slot == traits.SlotCount - 1;
        default:
            return false;
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        int iargKey = info.Arity - 1;
        var typeCmp = info.Args[iargKey].Type;
        if (iargKey == 0)
            typeCmp = typeCmp.ItemTypeOrThis;

        RexlDiagnostic diag = null;
        if (!typeCmp.IsEquatable)
            diag = RexlDiagnostic.Error(info.GetParseArg(iargKey), ErrorStrings.ErrInequatableType_Type, typeCmp);
        else if (info.Dirs.GetItemOrDefault(iargKey).IsCi() && !typeCmp.HasText)
        {
            var parg = info.GetParseArg(iargKey);
            if (parg is DirectiveNode dn)
            {
                diag = RexlDiagnostic.WarningGuess(dn.DirToken, dn, ErrorStrings.WrnCmpCi_Type,
                    "[=]", dn.DirToken.Range, typeCmp);
            }
            else
                diag = RexlDiagnostic.Warning(parg, ErrorStrings.WrnCmpCi_Type, typeCmp);
        }
        if (diag is not null)
            info.PostDiagnostic(diag);

        if (iargKey == 0)
        {
            var typeSeq = typeCmp.ToSequence();
            return (typeSeq, Immutable.Array<DType>.Create(typeSeq));
        }

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);
        return (types[0], types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!type.IsSequence)
            return false;
        if (args[0].Type != type)
            return false;
        var typeCmp = args.Length == 1 ? args[0].Type.ItemTypeOrThis : args[1].Type;
        if (!typeCmp.IsEquatable)
            full = false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        var (min, max) = call.Args[0].GetItemCountRange();
        return (Math.Min(1, min), max);
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg == 0);
        return PullWithFlags.Both;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Chain together one or more sequences.
/// REVIEW: What should we call this?
/// </summary>
public sealed partial class ChainFunc : RexlOper
{
    public static readonly ChainFunc Instance = new ChainFunc();

    private ChainFunc()
        // Use operator record acceptance, so this will match the ++ operator.
        : base(isFunc: true, new DName("Chain"), union: DType.UseUnionOper, arityMin: 1, arityMax: int.MaxValue)
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
        Validation.Assert(info.Arity >= 1);

        DType type = info.Args[0].Type;
        if (!type.IsSequence && type != DType.Null)
            type = type.ToSequence();
        for (int i = 1; i < info.Arity; i++)
        {
            DType typeCur = info.Args[i].Type;
            if (typeCur.SeqCount == 0 && typeCur != DType.Null)
                typeCur = typeCur.ToSequence();
            type = DType.GetSuperType(type, typeCur, AcceptUseUnion);
        }

        if (!type.IsSequence)
        {
            Validation.Assert(type.IsNull);
            type = DType.Vac.ToSequence();
        }

        return (type, Immutable.Array.Fill(type, info.Arity));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        var args = call.Args;
        for (int slot = 0; slot < args.Length; slot++)
        {
            if (args[slot].Type != type)
                return false;
        }

        // Always reduced to a variadic operation.
        full = false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        long min = 0;
        long max = 0;
        int cseq = call.Args.Length;
        for (int i = 0; i < cseq; i++)
        {
            var (a, b) = call.Args[i].GetItemCountRange();
            Validation.AssertIndexInclusive(a, b);
            min = NumUtil.AddCounts(min, a);
            max = NumUtil.AddCounts(max, b);
        }
        return (min, max);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        return reducer.Reduce(BndVariadicOpNode.Create(call.Type, BinaryOp.SeqConcat, call.Args, default));
    }
}

/// <summary>
/// Map each item of a sequence or sequences to a sequence, all of which are chained together, in order. If there
/// is only one argument, it must be a T** and produces T*.
/// </summary>
public sealed partial class ChainMapFunc : RexlOper
{
    public static readonly ChainMapFunc Instance = new ChainMapFunc();

    private ChainMapFunc()
        : base(isFunc: true, new DName("ChainMap"), 1, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: false, carg);
        return ArgTraitsZip.Create(this, indexed: true, eager: false, carg, seqCount: carg - 1);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        if (types.Count == 1)
        {
            DType typeSrc = types[0];
            while (typeSrc.SeqCount < 2)
                typeSrc = typeSrc.ToSequence();
            types[0] = typeSrc;
            return (typeSrc.ItemTypeOrThis, types.ToImmutable());
        }

        for (int i = 0; i < types.Count; i++)
        {
            DType type = types[i];
            if (type.SeqCount == 0)
                types[i] = type.ToSequence();
        }

        return (types[types.Count - 1], types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        var args = call.Args;
        if (args.Length == 1)
        {
            if (args[0].Type != type.ToSequence())
                return false;
        }
        else
        {
            int slot = args.Length;
            var typeSeq = args[--slot].Type;
            if (typeSeq != type)
                return false;
            while (--slot >= 0)
            {
                if (!args[slot].Type.IsSequence)
                    return false;
            }
        }
        return true;
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg < Math.Max(1, call.Scopes.Length));
        return PullWithFlags.Both;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Produces the first item in the sequence (that satisfies the optional predicate) or null.
/// </summary>
public sealed class FirstFunc : RexlOper
{
    public static readonly FirstFunc Instance = new FirstFunc();

    private FirstFunc()
        : base(isFunc: true, new DName("First"), 1, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(ArityMin == 1 & ArityMax == 2);
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: true, carg);
        return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: 1);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot != 1)
            return false;
        if (dir != Directive.If)
            return false;
        return true;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        DType typeRet = type.ItemTypeOrThis.ToOpt();

        if (info.Arity == 1)
            return (typeRet, Immutable.Array.Create(type));
        return (typeRet, Immutable.Array.Create(type, DType.BitReq));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsOpt)
            return false;
        var args = call.Args;
        var typeSeq = args[0].Type;
        if (!typeSeq.IsSequence)
            return false;
        var typeItem = typeSeq.ItemTypeOrThis;
        if (typeItem.ToOpt() != type)
            return false;
        if (args.Length == 2 && args[1].Type != DType.BitReq)
            return false;

        // Reducible to TakeOne.
        full = false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        // Reduce to TakeOne.
        Immutable.Array<BoundNode> args;
        Immutable.Array<Directive> dirs;
        var typeItem = call.Args[0].Type.ItemTypeOrThis;
        if (typeItem.IsOpt)
        {
            args = call.Args;
            dirs = default;
        }
        else
        {
            int arity = call.Args.Length;
            var bldrDirs = Immutable.Array<Directive>.CreateBuilder(arity + 1, init: true);
            bldrDirs[arity] = Directive.Else;

            args = call.Args.Add(BndNullNode.Create(call.Type));
            dirs = bldrDirs.ToImmutable();
        }

        return reducer.Reduce(BndCallNode.Create(
            TakeOneFunc.Instance, call.Type, args, call.Scopes, call.Indices, dirs, call.Names));
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg == 0);
        return PullWithFlags.Both;
    }
}

/// <summary>
/// Produces the item in the sequence at the given index, or a default value,
/// which may be implicit or explicit.
///
/// REVIEW: The natural complementary function DropAt needs a separate
/// implementation since DropAt would return a sequence, not a single item.
/// </summary>
public sealed partial class TakeAtFunc : RexlOper
{
    public static readonly TakeAtFunc Instance = new TakeAtFunc();

    private TakeAtFunc()
        : base(isFunc: true, new DName("TakeAt"), 2, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg, Immutable.Array<DName> names, BitSet implicitNames, Immutable.Array<Directive> dirs)
    {
        Validation.Assert(SupportsArity(carg));
        Validation.Assert(names.IsDefault || names.Length == carg);
        Validation.Assert(dirs.IsDefault || dirs.Length == carg);

        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x2, maskLiftOpt: 0x2);
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        throw new InvalidOperationException();
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot != 2)
            return false;
        if (dir != Directive.Else)
            return false;
        return true;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);
        var typeRes = types[0].ItemTypeOrThis;
        types[1] = DType.I8Req;

        if (info.Arity == 3)
        {
            // REVIEW: Use super-type.
            if (types[2].IsOpt && !typeRes.IsOpt)
                typeRes = typeRes.ToOpt();
            types[2] = typeRes;
        }

        // REVIEW: Should we warn on out of range to match tensor and homogeneous tuple warnings?
        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        var typeSeq = args[0].Type;

        if (!typeSeq.IsSequence)
            return false;
        if (args[1].Type != DType.I8Req)
            return false;
        if (args.Length == 3 && args[2].Type != type)
            return false;

        var typeItem = typeSeq.ItemTypeOrThis;
        if (typeItem == type)
            return true;
        if (typeItem.ToOpt() == type)
            return true;
        return false;

    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        var args = call.Args;

        if (call.Type.IsNull)
            return BndNullNode.Create(call.Type);

        var (min, max) = args[0].GetItemCountRange();
        if (max == 0)
            goto LRetDefault;

        if (min == max && args[1].TryGetI8(out long index))
        {
            bool normed = false;
            if (index < 0)
            {
                normed = true;
                index += max;
            }

            if (!Validation.IsValidIndex(index, max))
                goto LRetDefault;

            if (call.Args[0] is BndSequenceNode bndSeq)
            {
                Validation.AssertIndex(index, bndSeq.Items.Length);
                var bndItem = bndSeq.Items[(int)index];
                if (bndItem.Type != call.Type)
                {
                    Validation.Assert(bndItem.Type.ToOpt() == call.Type);
                    return BndCastOptNode.Create(bndItem);
                }
                return bndItem;
            }

            if (index == 0)
            {
                var dirs = args.Length == 3 ? Immutable.Array<Directive>.Create(Directive.None, Directive.Else) : default;
                return BndCallNode.Create(TakeOneFunc.Instance, call.Type, call.Args.RemoveAt(1),
                    Immutable.Array<ArgScope>.Empty, Immutable.Array<ArgScope>.Empty, dirs, default);
            }

            if (normed)
                return BndCallNode.Create(this, call.Type, call.Args.SetItem(1, BndIntNode.CreateI8(index)));
        }

        return base.ReduceCore(reducer, call);
    LRetDefault:
        if (args.Length == 3)
            return args[2];
        return BndDefaultNode.Create(call.Type);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Produces a sequence that yields the given first arg the number of times indicated by the second arg.
/// </summary>
public sealed partial class RepeatFunc : RexlOper
{
    public static readonly RepeatFunc Instance = new RepeatFunc();

    private RepeatFunc()
        : base(isFunc: true, new DName("Repeat"), 2, 2)
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

        var type = info.Args[0].Type;
        return (type.ToSequence(), Immutable.Array.Create(type, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        var typeItem = args[0].Type;
        if (type != typeItem.ToSequence())
            return false;
        if (args[1].Type != DType.I8Req)
            return false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        Validation.BugCheckParam(call.Args[1].Type == DType.I8Req, nameof(call));
        if (!call.Args[1].TryGetI8(out var num))
            return base.GetItemCountRangeCore(call);
        if (num < 0)
            num = 0;
        return (num, num);
    }
}

/// <summary>
/// Counts the number of items in the sequence (that satisfies the optional predicate).
/// </summary>
public sealed partial class CountFunc : RexlOper
{
    public static readonly CountFunc Instance = new CountFunc();

    private CountFunc()
        : base(isFunc: true, new DName("Count"), 1, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(ArityMin == 1 & ArityMax == 2);
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: true, carg);
        return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: 1);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot != 1)
            return false;
        if (dir == Directive.If || dir == Directive.While)
            return true;
        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        if (info.Arity == 1)
            return (DType.I8Req, Immutable.Array.Create(type));
        Validation.Assert(info.Arity == 2);
        return (DType.I8Req, Immutable.Array.Create(type, DType.BitReq));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.I8Req)
            return false;
        var args = call.Args;
        if (!args[0].Type.IsSequence)
            return false;
        if (args.Length == 2 && args[1].Type != DType.BitReq)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        var args = call.Args;
        var carg = args.Length;
        var (min, max) = args[0].GetItemCountRange();

        if (max == 0)
            return BndIntNode.CreateI8(0);

        if (min == max)
        {
            if (carg == 1)
                return BndIntNode.CreateI8(max);
            if (args[1].TryGetBool(out bool pred))
                return BndIntNode.CreateI8(pred ? max : 0);
        }
        else
        {
            if (carg > 1 && args[1].TryGetBool(out bool pred))
            {
                if (!pred)
                    return BndIntNode.CreateI8(0);
                return BndCallNode.Create(this, call.Type, Immutable.Array<BoundNode>.Create(args[0]));
            }
        }
        return base.ReduceCore(reducer, call);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary> Implements Any and All functionality.
///
/// For both functions, there are two cases:
///   * Input sequence is of type bit, either opt or req.
///       The considered sequence is the input sequence.
///   * Input sequence is any type, with a predicate returning a bit.
///       The predicate can be either opt or req.
///       The considered sequence is the mapped input sequence.
///
/// Any returns true if an element in the considered sequence is true.
/// Otherwise, if it contains a null, it returns null.
/// Otherwise returns false.
///
/// All returns false if an element in the considered sequence is false.
/// Otherwise, if it contains a null, it returns null.
/// Otherwise returns true.
///
/// <example> Examples:
/// <code>Any([false, true, null])</code>  --> true
/// <code>Any(Range(10), it > 5)</code>  --> true
/// <code>All([true, true, null])</code>  --> NULL
/// <code>All(Range(10), it > -5)</code>  --> true
/// </example>
/// </summary>
public sealed partial class AnyAllFunc : RexlOper
{
    public static readonly AnyAllFunc Any = new AnyAllFunc(true);
    public static readonly AnyAllFunc All = new AnyAllFunc(false);

    /// <summary>
    /// Whether this is <c>Any</c> vs <c>All</c>.
    /// </summary>
    public bool IsAny { get; }

    private AnyAllFunc(bool isAny)
        : base(isFunc: true, new DName(isAny ? "Any" : "All"), 1, 2)
    {
        IsAny = isAny;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(ArityMin == 1 & ArityMax == 2);
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: true, carg);
        return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: 1);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot != 1)
            return false;
        if (dir != Directive.If)
            return false;
        return true;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(1 <= info.Arity & info.Arity <= 2);

        var type0 = info.Args[0].Type;
        EnsureTypeSeq(ref type0);

        bool isOpt = (info.Arity == 1) ? type0.ItemTypeOrThis.IsOpt : info.Args[1].Type.IsOpt;
        DType typeDst = isOpt ? DType.BitOpt : DType.BitReq;

        if (info.Arity == 1)
            return (typeDst, Immutable.Array.Create(typeDst.ToSequence()));
        return (typeDst, Immutable.Array.Create(type0, typeDst));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        var typeSrc = args[0].Type;
        if (!typeSrc.IsSequence)
            return false;
        var typeItem = args.Length == 1 ? typeSrc.ItemTypeOrThis : args[1].Type;
        if (typeItem.Kind != DKind.Bit)
            return false;
        if (type != typeItem)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        bool isOpt = call.Type.IsOpt;
        var (min, max) = call.Args[0].GetItemCountRange();

        static BoundNode ReduceBit(bool value, bool isOpt)
        {
            BoundNode reduced = BndIntNode.CreateBit(value);
            if (isOpt)
                reduced = BndCastOptNode.Create(reduced);
            return reduced;
        }

        if (max == 0)
        {
            Validation.Coverage(isOpt ? 1 : 0);
            return ReduceBit(!IsAny, isOpt);
        }

        if (call.Args.Length == 2 && call.Args[1].TryGetBoolOpt(out bool? pred))
        {
            if (min > 0)
            {
                if (pred == null)
                    return BndNullNode.Create(DType.BitOpt);

                Validation.Coverage(isOpt ? 1 : 0);
                return ReduceBit(pred.GetValueOrDefault(), isOpt);
            }

            if (pred != null)
            {
                bool predVal = pred.GetValueOrDefault();
                if (IsAny != predVal)
                {
                    Validation.Coverage(IsAny ? 1 : 0);
                    Validation.Coverage(predVal ? 1 : 0);
                    Validation.Coverage(isOpt ? 1 : 0);
                    return ReduceBit(predVal, isOpt);
                }
            }
        }

        // REVIEW: Can also reduce when the predicate is a constant index check.
        // e.g. Any(S, # = 0) is true when min > 0. There are many others.
        return base.ReduceCore(reducer, call);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Function to take a sequence and produce a sequence containing all pairs of the items, each pair
/// bundled as a sequence. This is useful for several solver scenarios.
/// REVIEW: Should this be generalized somehow? Also, should the order be specified?
/// </summary>
public sealed partial class MakePairsFunc : RexlOper
{
    public static readonly MakePairsFunc Instance = new MakePairsFunc();

    private MakePairsFunc()
        : base(isFunc: true, new DName("MakePairs"), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        // REVIEW: This could be made non-eager, but the current implementation is eager.
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        // Return type is the arg type with one extra sequence count.
        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        return (type.ToSequence(), Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var typeSrc = call.Args[0].Type;
        if (!typeSrc.IsSequence)
            return false;
        if (type != typeSrc.ToSequence())
            return false;
        return true;
    }
}

/// <summary>
/// Folds a sequence using a seed value, iterative-selector and optional final-selector.
/// </summary>
public sealed partial class FoldFunc : RexlOper
{
    public static readonly FoldFunc Instance = new FoldFunc();

    private FoldFunc()
        : base(isFunc: true, new DName("Fold"), 3, 4)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(ArityMin == 3 & ArityMax == 4);
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));

        return new ArgTraitsFold(this, carg);
    }

    private sealed class ArgTraitsFold : ArgTraitsBare
    {
        public ArgTraitsFold(FoldFunc func, int slotCount)
            : base(func, slotCount)
        {
            Validation.Assert(func.SupportsArity(slotCount));
        }

        public override int ScopeCount => 2;

        public override int ScopeIndexCount => 1;

        public override int NestedCount => SlotCount - ScopeCount;

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (!(cmp is ArgTraitsFold cmpFold))
                return false;
            if (SlotCount != cmpFold.SlotCount)
                return false;
            Validation.Assert(Oper == cmpFold.Oper);
            return true;
        }

        public override ScopeKind GetScopeKind(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot switch
            {
                0 => ScopeKind.SeqItem,
                1 => ScopeKind.Iter,
                _ => ScopeKind.None,
            };
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot > 1;
        }

        public override bool IsScope(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot <= 1;
        }

        public override bool IsScope(int slot, out int iscope)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

            if (slot <= 1)
            {
                iscope = slot;
                return true;
            }
            iscope = -1;
            return false;
        }

        public override bool IsScope(int slot, out int iscope, out int iidx, out bool firstForIdx)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

            switch (slot)
            {
            case 0:
                iscope = 0;
                iidx = 0;
                firstForIdx = true;
                return true;

            case 1:
                iscope = 1;
                iidx = -1;
                firstForIdx = false;
                return true;

            default:
                iscope = -1;
                iidx = -1;
                firstForIdx = false;
                return false;
            }
        }

        public override bool IsEagerSeq(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot == 0;
        }

        public override bool IsScopeActive(int slot, int upCount)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));

            return slot switch
            {
                2 => true,
                3 => upCount == 0,
                _ => false,
            };
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);

        var typeIter = DType.GetSuperType(types[1], types[2], union: DType.UseUnionDefault);
        types[1] = typeIter;
        types[2] = typeIter;

        return (types[types.Count - 1], types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!args[0].Type.IsSequence)
            return false;
        if (args[1].Type != args[2].Type)
            full = false;
        var typeRes = args[args.Length - 1].Type;
        if (type != typeRes)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        var args = call.Args;

        if (args[1].Type != args[2].Type)
            return base.ReduceCore(reducer, call);

        if (call.Args.Length == 3)
        {
            // REVIEW: Technically possible to reduce in the 4-arg case as well, if
            // the state iter scope could be remapped to the init value in the result selector.
            var (_, max) = args[0].GetItemCountRange();
            if (max == 0)
            {
                var argInit = args[1];
                Validation.Assert(argInit.Type == call.Type);
                return argInit;
            }
        }

        return base.ReduceCore(reducer, call);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Generates a sequence from a sequence, using a seed value, iteration-selector and optional result-selector.
/// For ScanX, the result sequence starts with the seed value, so its final length is one greater than the length
/// of the source sequence. For ScanZ, the result sequence starts with the first item of the source sequence, so
/// its length is the same as that of the source sequence. For ScanZ, the result-selector can reference the source
/// item.
/// </summary>
public sealed partial class ScanFunc : RexlOper
{
    public static readonly ScanFunc ScanX = new ScanFunc(false);
    public static readonly ScanFunc ScanZ = new ScanFunc(true);

    /// <summary>
    /// Whether this is the zip form vs the extra form (<c>ScanZ</c> vs <c>ScanX</c>).
    /// </summary>
    public bool IsZip { get; }

    private ScanFunc(bool zip)
        : base(isFunc: true, new DName(zip ? "ScanZ" : "ScanX"), 3, 4)
    {
        IsZip = zip;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(ArityMin == 3 & ArityMax == 4);
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));

        return new ArgTraitsScan(this, carg);
    }

    private sealed class ArgTraitsScan : ArgTraitsBare
    {
        private readonly bool _zip;

        public ArgTraitsScan(ScanFunc func, int slotCount)
            : base(func, slotCount)
        {
            Validation.Assert(func.SupportsArity(slotCount));
            _zip = func.IsZip;
        }

        public override int ScopeCount => 2;

        public override int ScopeIndexCount => 1;

        public override int NestedCount => SlotCount - ScopeCount;

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (!(cmp is ArgTraitsScan cmpScan))
                return false;
            if (SlotCount != cmpScan.SlotCount)
                return false;
            if (_zip != cmpScan._zip)
                return false;
            Validation.Assert(Oper == cmpScan.Oper);
            return true;
        }

        public override ScopeKind GetScopeKind(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot switch
            {
                0 => ScopeKind.SeqItem,
                1 => ScopeKind.Iter,
                _ => ScopeKind.None,
            };
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot > 1;
        }

        public override bool IsScope(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot <= 1;
        }

        public override bool IsScope(int slot, out int iscope)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

            if (slot <= 1)
            {
                iscope = slot;
                return true;
            }
            iscope = -1;
            return false;
        }

        public override bool IsScope(int slot, out int iscope, out int iidx, out bool firstForIdx)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

            switch (slot)
            {
            case 0:
                iscope = 0;
                iidx = 0;
                firstForIdx = true;
                return true;

            case 1:
                iscope = 1;
                iidx = -1;
                firstForIdx = false;
                return true;

            default:
                iscope = -1;
                iidx = -1;
                firstForIdx = false;
                return false;
            }
        }

        public override bool IsScopeActive(int slot, int upCount)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));

            return slot switch
            {
                2 => true,
                3 => _zip || upCount == 0,
                _ => false,
            };
        }

        public override bool IsEagerSeq(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return false;
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);

        var typeIter = DType.GetSuperType(types[1], types[2], union: DType.UseUnionDefault);
        types[1] = typeIter;
        types[2] = typeIter;

        return (types[types.Count - 1].ToSequence(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!args[0].Type.IsSequence)
            return false;
        if (args[1].Type != args[2].Type)
            full = false;
        var typeRes = args[args.Length - 1].Type;
        if (type != typeRes.ToSequence())
            return false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        var (min, max) = call.Args[0].GetItemCountRange();
        if (!IsZip)
        {
            // Add one, but avoid overflow.
            min = Math.Min(min, long.MaxValue - 1) + 1;
            max = Math.Min(max, long.MaxValue - 1) + 1;
        }
        return (min, max);
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));

        if (!IsZip || iarg > 0)
            return PullWithFlags.With;
        return PullWithFlags.Both;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        var args = call.Args;

        if (args[1].Type != args[2].Type)
            return base.ReduceCore(reducer, call);

        var (_, max) = args[0].GetItemCountRange();
        if (max == 0)
        {
            if (IsZip)
                return BndSequenceNode.CreateEmpty(call.Type);
            if (args.Length == 3)
            {
                // REVIEW: Technically possible to reduce in the non-zip 4-arg case as well,
                // if the state iter scope could be remapped to the init value in the result selector.
                var argInit = args[1];
                Validation.Assert(argInit.Type.ToSequence() == call.Type);
                return BndSequenceNode.Create(call.Type, Immutable.Array<BoundNode>.Create(argInit));
            }
        }
        return base.ReduceCore(reducer, call);
    }
}

/// <summary>
/// Generate a sequence of values from each item's index, or an iteration value computed from the index.
/// The former is equivalent to ForEach(Range(count)...) and the latter to ScanZ(Range(count)...).
///
/// REVIEW: should the stateful variant have further variants for including/omitting
/// the initial item from seed?
/// </summary>
public sealed partial class GenerateFunc : RexlOper
{
    public static readonly GenerateFunc Instance = new GenerateFunc();

    private GenerateFunc()
        : base(isFunc: true, new DName("Generate"), 2, 4)
    {
    }


    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return new ArgTraitsGenerate(this, carg);
    }

    private sealed class ArgTraitsGenerate : ArgTraitsBare
    {
        private readonly bool _stateful;

        public override int ScopeCount { get; }

        public override int NestedCount => SlotCount - ScopeCount;

        public ArgTraitsGenerate(GenerateFunc func, int slotCount)
            : base(func, slotCount)
        {
            Validation.Assert(func.SupportsArity(slotCount));
            _stateful = SlotCount > 2;
            ScopeCount = _stateful ? 2 : 1;
        }

        public override bool AreEquivalent(ArgTraits cmp)
        {
            if (!(cmp is ArgTraitsGenerate cmpGen))
                return false;
            if (SlotCount != cmpGen.SlotCount)
                return false;
            Validation.Assert(Oper == cmpGen.Oper);
            return true;
        }

        public override ScopeKind GetScopeKind(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot switch
            {
                0 => ScopeKind.Range,
                1 => _stateful ? ScopeKind.Iter : ScopeKind.None,
                _ => ScopeKind.None
            };
        }

        public override bool IsNested(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot switch
            {
                0 => false,
                1 => !_stateful,
                _ => true,
            };
        }

        public override bool IsScope(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return slot switch
            {
                0 => true,
                1 => _stateful,
                _ => false,
            };
        }

        public override bool IsScope(int slot, out int iscope)
        {
            return IsScope(slot, out iscope, out _, out _);
        }

        public override bool IsScope(int slot, out int iscope, out int iidx, out bool firstForIdx)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

            iidx = -1;
            firstForIdx = false;
            switch (slot)
            {
            case 0:
                iscope = 0;
                return true;

            case 1:
                if (!_stateful)
                    goto default;
                iscope = 1;
                return true;

            default:
                iscope = -1;
                return false;
            }
        }

        public override bool IsScopeActive(int slot, int upCount)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));

            return slot switch
            {
                0 => false,
                1 => !_stateful,
                _ => true,
            };
        }

        public override bool IsEagerSeq(int slot)
        {
            Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
            return false;
        }
    }

    protected override DType GetScopeArgTypeCore(ArgTraits traits, int iarg, DType type)
    {
        if (iarg == 0)
            return DType.I8Req;
        return base.GetScopeArgTypeCore(traits, iarg, type);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        types[0] = DType.I8Req;
        if (types.Count == 2)
            return (types[1].ToSequence(), types.ToImmutable());

        Validation.Assert(3 <= types.Count && types.Count <= 4);
        var typeIter = DType.GetSuperType(types[1], types[2], union: DType.UseUnionDefault);
        types[1] = typeIter;
        types[2] = typeIter;

        return (types[types.Count - 1].ToSequence(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (args[0].Type != DType.I8Req)
            return false;
        if (args.Length > 2 && args[1].Type != args[2].Type)
            full = false;
        var typeRes = args[args.Length - 1].Type;
        if (type != typeRes.ToSequence())
            return false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        if (!call.Args[0].TryGetI8(out var lim))
            return base.GetItemCountRangeCore(call);

        long num = Math.Max(0, lim);
        return (num, num);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        if (call.Args[0].TryGetI8(out var lim) && lim <= 0)
            return BndSequenceNode.CreateEmpty(call.Type);

        if (call.Args.Length == 2 && call.Args[1] is BndScopeRefNode bsr && bsr.Scope == call.Scopes[0])
        {
            return BndCallNode.Create(RangeFunc.Instance, call.Type,
                Immutable.Array<BoundNode>.Create(call.Args[0]));
        }

        return base.ReduceCore(reducer, call);
    }
}