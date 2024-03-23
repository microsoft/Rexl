// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Base class for specifying traits of a signature of an operation (function or procedure).
/// Argument slots can introduce new scopes. Scopes may affect binding of later arguments,
/// including those that introduce additional scopes. This class tracks that information.
/// More precisely, it contains:
/// * SlotCount - the number of arguments.
/// * IsScope(slot) - whether the indicated slot defines a new scope.
/// * GetScopeKind(slot) - whether the indicated slot defines a new scope and, if so, what kind of scope.
/// * IsNested(slot) - whether the indicated slot is affected by preceding scopes.
/// </summary>
public abstract class ArgTraits
{
    /// <summary>
    /// The <see cref="RexlOper"/> that provided these traits.
    /// </summary>
    public RexlOper Oper { get; }

    /// <summary>
    /// Number of slots.
    /// </summary>
    public int SlotCount { get; }

    /// <summary>
    /// The number of arg scopes.
    /// </summary>
    public abstract int ScopeCount { get; }

    /// <summary>
    /// The number of scope groups.
    /// </summary>
    public virtual int ScopeIndexCount => 0;

    /// <summary>
    /// The slots that can participate in lifting over sequence.
    /// </summary>
    public abstract BitSet MaskLiftSeq { get; }

    /// <summary>
    /// The slots that are lifted over sequence which require a final sequence count of one after lifting is performed.
    /// Bits that are set for slots that do not participate in lifting over sequence are ignored.
    /// </summary>
    public virtual BitSet MaskLiftNeedsSeq => default;

    /// <summary>
    /// The slots that can participate in lifting over tensor.
    /// </summary>
    public abstract BitSet MaskLiftTen { get; }

    /// <summary>
    /// The slots that can participate in lifting over opt.
    /// </summary>
    public abstract BitSet MaskLiftOpt { get; }

    /// <summary>
    /// Whether the indicated slot lifts over sequence.
    /// </summary>
    public virtual bool LiftsOverSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return MaskLiftSeq.TestBit(slot);
    }

    /// <summary>
    /// Whether the indicated slot lifts over tensor.
    /// </summary>
    public virtual bool LiftsOverTen(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return MaskLiftTen.TestBit(slot);
    }

    /// <summary>
    /// Whether the indicated slot lifts over opt.
    /// </summary>
    public virtual bool LiftsOverOpt(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return MaskLiftOpt.TestBit(slot);
    }

    /// <summary>
    /// Whether the indicated slot is a scope.
    /// </summary>
    public abstract bool IsScope(int slot);

    /// <summary>
    /// Whether the indicated slot is a scope. If so, fills <paramref name="iscope"/> with
    /// the scope index. Otherwise <paramref name="iscope"/> should be negative.
    /// </summary>
    public abstract bool IsScope(int slot, out int iscope);

    /// <summary>
    /// Whether the indicated slot is a scope. If so, fills <paramref name="iscope"/> with
    /// the scope number and <paramref name="iidx"/> with the scope index number. If <paramref name="iidx"/>
    /// is a valid scope index number, fills <paramref name="firstForIdx"/> with <c>true</c> if this scope
    /// is the first in the arg list to have <paramref name="iidx"/> as its scope index number. Note this means for
    /// every valid <paramref name="iidx"/>, exactly one slot should return <c>true</c> via <paramref name="firstForIdx"/>.
    /// Valid <paramref name="iidx"/>es may only monotonically increase with subsequent slots, and must be contiguous.
    /// If not a scope, <paramref name="iscope"/> and <paramref name="iidx"/> should be negative, and
    /// <paramref name="firstForIdx"/> false.
    /// </summary>
    public virtual bool IsScope(int slot, out int iscope, out int iidx, out bool firstForIdx)
    {
        iidx = -1;
        firstForIdx = false;
        return IsScope(slot, out iscope);
    }

    /// <summary>
    /// Return true to indicate that when this slot "contains" a sequence (possibly nested in a record, tuple or tensor),
    /// some part of the sequence may be eagerly consumed. For example, <c>Count</c>, <c>Sum</c>, <c>TakeOne</c>, and
    /// <c>IsEmpty</c> eagerly consume some or all of the input sequences but <c>ForEach</c>, <c>Take</c>, <c>Sort</c>
    /// and <c>KeyJoin</c> do not.
    ///
    /// It is much safer to assume all sequences are eager by default, rather than lazy.
    /// </summary>
    public abstract bool IsEagerSeq(int slot);

    /// <summary>
    /// Gets the scope kind for this slot (None if the slot isn't a scope).
    /// </summary>
    public abstract ScopeKind GetScopeKind(int slot);

    /// <summary>
    /// Whether the indicated slot is nested inside one or more scopes.
    /// </summary>
    public abstract bool IsNested(int slot);

    /// <summary>
    /// The number of slots that are nested inside one or more scopes.
    /// </summary>
    public abstract int NestedCount { get; }

    /// <summary>
    /// This returns whether a particular scope is active for a particular nested slot.
    /// The <paramref name="slot"/> is the nested slot. Note that <c>IsNested(slot)</c>
    /// is expected to be true. The <paramref name="upCount"/> is the "upward" scope index,
    /// from this <paramref name="slot"/>. The closest pushed slot has <c>upCount</c> value
    /// <c>0</c>, with outer scopes having larger <c>upCount</c> values.
    /// </summary>
    public abstract bool IsScopeActive(int slot, int upCount);

    /// <summary>
    /// Whether the indicated slot is nested inside one or more scopes and is the end
    /// of a scope run, so should clear the scopes after processing.
    /// </summary>
    public virtual bool IsNestedTail(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

        if (!IsNested(slot))
            return false;
        if (slot == SlotCount - 1)
            return true;
        if (IsNested(slot + 1))
            return false;
        return true;
    }

    /// <summary>
    /// Returns true if the indicated slot expression is potentially computed repeatedly.
    /// This is the case when the argument is "nested" in one or more looping scopes, such
    /// as the selector in an invocation of <c>ForEach</c>.
    /// </summary>
    public bool IsRepeated(int slot)
    {
        // REVIEW: Should this be done more efficiently and if so, how?
        if (!IsNested(slot))
            return false;

        int up = 0;
        for (int i = slot; --i >= 0;)
        {
            if (!IsScope(i, out int iscp))
                continue;
            if (GetScopeKind(i).IsLoopScope() && IsScopeActive(slot, up))
                return true;
            up++;
        }
        return false;
    }

    /// <summary>
    /// Whether the indicated slot accepts a name.
    /// </summary>
    public abstract bool SupportsName(int slot);

    /// <summary>
    /// Whether the indicated slot accepts an implicit name.
    /// </summary>
    public abstract bool SupportsImplicitName(int slot);

    /// <summary>
    /// Whether implicit names can be dotted.
    /// </summary>
    public abstract bool SupportDottedImplicitNames { get; }

    /// <summary>
    /// Whether the indicated slot requires a name.
    /// </summary>
    public abstract bool RequiresName(int slot);

    public abstract bool AreEquivalent(ArgTraits cmp);

    protected ArgTraits(RexlOper oper, int slotCount)
    {
        Validation.BugCheckValue(oper, nameof(oper));
        Validation.BugCheckParam(slotCount >= 0, nameof(slotCount));
        Validation.BugCheckParam(oper.SupportsArity(slotCount), nameof(slotCount));
        Oper = oper;
        SlotCount = slotCount;
    }

    /// <summary>
    /// Create a fully general ArgTraits.
    /// </summary>
    public static ArgTraits CreateGeneral(RexlOper oper, int slotCount,
        BitSet maskLiftSeq = default, BitSet maskLiftTen = default, BitSet maskLiftOpt = default,
        ScopeKind scopeKind = ScopeKind.None, BitSet maskScope = default, BitSet maskNested = default,
        BitSet maskName = default, BitSet maskNameExp = default, BitSet maskNameReq = default, BitSet maskScopeInactive = default,
        BitSet maskLazySeq = default,
        Immutable.Array<ScopeKind> scopeKinds = default)
    {
        Validation.BugCheckValue(oper, nameof(oper));
        Validation.BugCheckParam(slotCount >= 0, nameof(slotCount));
        Validation.BugCheckParam(oper.SupportsArity(slotCount), nameof(slotCount));
        Validation.BugCheckParam(!maskLiftSeq.TestAtOrAbove(slotCount), nameof(maskLiftSeq));
        Validation.BugCheckParam(!maskLiftTen.TestAtOrAbove(slotCount), nameof(maskLiftTen));
        Validation.BugCheckParam(!maskLiftOpt.TestAtOrAbove(slotCount), nameof(maskLiftOpt));
        Validation.BugCheckParam(!maskNested.TestAtOrAbove(slotCount), nameof(maskNested));
        Validation.BugCheckParam(!maskName.TestAtOrAbove(slotCount), nameof(maskName));
        Validation.BugCheckParam(!maskLazySeq.TestAtOrAbove(slotCount), nameof(maskLazySeq));

        // Names must include explicit and required names.
        Validation.BugCheckParam(maskNameExp.IsSubset(maskName), nameof(maskNameExp));
        Validation.BugCheckParam(maskNameReq.IsSubset(maskName), nameof(maskNameExp));

        if (maskScope.IsEmpty)
        {
            Validation.BugCheckParam(scopeKind == ScopeKind.None, nameof(scopeKind));
            Validation.BugCheckParam(scopeKinds.IsDefaultOrEmpty, nameof(scopeKinds));
            Validation.BugCheckParam(maskNested.IsEmpty, nameof(maskNested));
            Validation.BugCheckParam(maskScopeInactive.IsEmpty, nameof(maskScopeInactive));
            return new ArgTraitsGeneral(
                oper, slotCount, maskLiftSeq, maskLiftTen, maskLiftOpt,
                ScopeKind.None, default, default, default,
                maskName, maskNameExp, maskNameReq, maskLazySeq,
                default);
        }

        // REVIEW: Validate maskScopeInactive.
        if (!scopeKinds.IsDefaultOrEmpty)
        {
            Validation.BugCheckParam(scopeKinds.Length == maskScope.Count, nameof(scopeKinds));
            int iscope = 0;
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!maskScope.TestBit(slot))
                    continue;
                switch (scopeKinds[iscope])
                {
                default:
                    Validation.BugCheckParam(false, nameof(scopeKinds));
                    break;

                case ScopeKind.With:
                    break;
                case ScopeKind.Guard:
                    // Guard scopes can't be lifted over opt (makes no sense).
                    Validation.BugCheckParam(!maskLiftOpt.TestBit(slot), nameof(maskLiftOpt), "Guard scopes can't lift over opt");
                    break;
                case ScopeKind.SeqItem:
                    // Explicit names must include map scopes.
                    maskNameExp = maskNameExp.SetBit(slot);
                    // Map scopes can't be lifted over sequence (makes no sense).
                    Validation.BugCheckParam(!maskLiftSeq.TestBit(slot), nameof(maskLiftSeq), "Map scopes can't lift over sequence");
                    break;
                case ScopeKind.TenItem:
                    // Explicit names must include tensor scopes.
                    maskNameExp = maskNameExp.SetBit(slot);
                    // Map scopes can't be lifted over sequence (makes no sense).
                    Validation.BugCheckParam(!maskLiftTen.TestBit(slot), nameof(maskLiftTen), "Tensor scopes can't lift over tensor");
                    break;
                case ScopeKind.Iter:
                    // Explicit names must include iter scopes.
                    maskNameExp = maskNameExp.SetBit(slot);
                    break;
                }
                iscope++;
            }
            Validation.Assert(iscope == maskScope.Count);
        }
        else
        {
            switch (scopeKind)
            {
            default:
                Validation.BugCheckParam(false, nameof(scopeKind));
                break;

            case ScopeKind.With:
                break;
            case ScopeKind.Guard:
                // Guard scopes can't be lifted over opt (makes no sense).
                Validation.BugCheckParam(!maskLiftOpt.Intersects(maskScope), nameof(maskLiftOpt), "Guard scopes can't lift over opt");
                break;
            case ScopeKind.SeqItem:
                // Explicit names must include map scopes.
                maskNameExp |= maskScope;
                // Map scopes can't be lifted over sequence (makes no sense).
                Validation.BugCheckParam(!maskLiftSeq.Intersects(maskScope), nameof(maskLiftSeq), "Map scopes can't lift over sequence");
                break;
            case ScopeKind.TenItem:
                // Explicit names must include tensor scopes.
                maskNameExp |= maskScope;
                // Tensor scopes can't be lifted over tensor (makes no sense).
                Validation.BugCheckParam(!maskLiftTen.Intersects(maskScope), nameof(maskLiftSeq), "Tensor scopes can't lift over tensor");
                break;
            case ScopeKind.Iter:
                // Explicit names must include iter scopes.
                maskNameExp |= maskScope;
                break;
            }
        }

        Validation.BugCheckParam(maskScope.SlotMax < maskNested.SlotMax, nameof(maskScope));
        Validation.BugCheckParam(maskScope.SlotMin < maskNested.SlotMin, nameof(maskNested));

        // Explicit names are allowed on all scopes.
        maskName |= maskScope;

        return new ArgTraitsGeneral(
            oper, slotCount, maskLiftSeq, maskLiftTen, maskLiftOpt,
            scopeKind, scopeKinds, maskScope, maskNested,
            maskName, maskNameExp, maskNameReq,
            maskScopeInactive, maskLazySeq);
    }

    /// <summary>
    /// Builds a "scope inactive" bitset. Accepts mappings from arg slots to the "upward" index of the scope
    /// to deactivate for that slot, where the scope up count starts at 0 for the closest scope and increases
    /// to the left of the slot.
    /// </summary>
    public static BitSet MakeScopeInactive(int scopeCount, params (int iarg, int upCount)[] items)
    {
        Validation.BugCheckValue(items, nameof(items));
        Validation.BugCheck(scopeCount >= 0, nameof(scopeCount));

        if (scopeCount == 0)
        {
            Validation.BugCheckParam(items.Length == 0, nameof(items));
            return default;
        }

        if (items.Length == 0)
            return default;

        BitSet res = default;
        foreach (var (iarg, upCount) in items)
        {
            Validation.BugCheckParam(iarg > 0, nameof(iarg));
            Validation.BugCheckIndex(upCount, scopeCount, nameof(upCount));
            res = res.SetBit((iarg - 1) * scopeCount + upCount);
        }
        return res;
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> that provides the following default behavior for subclasses:
///   * No lifting.
///   * No lazy sequences.
///   * No support for implicit names.
///   * No required names.
///   * A slot supports a name iff it introduces a scope.
/// </summary>
public abstract class ArgTraitsBare : ArgTraits
{
    public override BitSet MaskLiftSeq => default;

    public override BitSet MaskLiftTen => default;

    public override BitSet MaskLiftOpt => default;

    public override bool SupportDottedImplicitNames => false;

    protected ArgTraitsBare(RexlOper oper, int slotCount)
        : base(oper, slotCount)
    {
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return true;
    }

    public override bool RequiresName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public override bool SupportsImplicitName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public override bool SupportsName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return IsScope(slot);
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> that provides all the default behavior of <see cref="ArgTraitsBare"/> for subclasses,
/// but additionally has no scopes or nesting.
/// </summary>
public abstract class ArgTraitsNoScopesBare : ArgTraitsBare
{
    public override int ScopeCount => 0;

    public override int NestedCount => 0;

    protected ArgTraitsNoScopesBare(RexlOper oper, int slotCount)
        : base(oper, slotCount)
    {
    }

    public override ScopeKind GetScopeKind(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return ScopeKind.None;
    }

    public override bool IsNested(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public override bool IsScope(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public override bool IsScope(int slot, out int iscope)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        iscope = -1;
        return false;
    }

    public override bool IsScopeActive(int slot, int upCount)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));
        Validation.Assert(false);
        return false;
    }
}

/// <summary>
/// Base class for <see cref="ArgTraits"/> with no scopes.
/// </summary>
public abstract class ArgTraitsNoScopes : ArgTraits
{
    public sealed override int ScopeCount => 0;
    public sealed override int NestedCount => 0;

    protected ArgTraitsNoScopes(RexlOper oper, int slotCount)
        : base(oper, slotCount)
    {
    }

    public sealed override ScopeKind GetScopeKind(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return ScopeKind.None;
    }

    public sealed override bool IsNested(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public sealed override bool IsScope(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public sealed override bool IsScope(int slot, out int iscope)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        iscope = -1;
        return false;
    }

    public sealed override bool IsScopeActive(int slot, int upCount)
    {
        throw Validation.BugExceptParam(nameof(upCount));
    }
}

/// <summary>
/// Base class for <see cref="ArgTraits"/> with no scopes and no names.
/// </summary>
public abstract class ArgTraitsNoScopesNoNames : ArgTraitsNoScopes
{
    public sealed override bool SupportDottedImplicitNames => false;

    protected ArgTraitsNoScopesNoNames(RexlOper oper, int slotCount)
        : base(oper, slotCount)
    {
    }

    public sealed override bool RequiresName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public sealed override bool SupportsName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public sealed override bool SupportsImplicitName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> with no scopes, no names, and no lifting.
/// </summary>
public sealed class ArgTraitsSimple : ArgTraitsNoScopesNoNames
{
    private readonly bool _eager;

    public override BitSet MaskLiftSeq => default;
    public override BitSet MaskLiftTen => default;
    public override BitSet MaskLiftOpt => default;

    private ArgTraitsSimple(RexlOper oper, bool eager, int slotCount)
        : base(oper, slotCount)
    {
        _eager = eager;
    }

    /// <summary>
    /// Create an ArgTraits with no scopes, no lifting, no names, and no directives.
    /// </summary>
    public static ArgTraits Create(RexlOper oper, bool eager, int slotCount)
    {
        return new ArgTraitsSimple(oper, eager, slotCount);
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _eager;
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsSimple other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;
        return true;
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> with lifting, no scopes, and no names.
/// </summary>
public sealed class ArgTraitsLifting : ArgTraitsNoScopesNoNames
{
    public override BitSet MaskLiftSeq { get; }
    public override BitSet MaskLiftTen { get; }
    public override BitSet MaskLiftOpt { get; }
    public override BitSet MaskLiftNeedsSeq { get; }

    private ArgTraitsLifting(RexlOper oper, int slotCount,
            BitSet maskLiftSeq, BitSet maskLiftTen, BitSet maskLiftOpt, BitSet maskLiftNeedsSeq = default)
        : base(oper, slotCount)
    {
        Validation.Assert(!maskLiftSeq.IsEmpty || !maskLiftTen.IsEmpty || !maskLiftOpt.IsEmpty);
        Validation.BugCheckParam(!maskLiftSeq.TestAtOrAbove(slotCount), nameof(maskLiftSeq));
        Validation.BugCheckParam(!maskLiftTen.TestAtOrAbove(slotCount), nameof(maskLiftTen));
        Validation.BugCheckParam(!maskLiftOpt.TestAtOrAbove(slotCount), nameof(maskLiftOpt));

        MaskLiftSeq = maskLiftSeq;
        MaskLiftTen = maskLiftTen;
        MaskLiftOpt = maskLiftOpt;
        MaskLiftNeedsSeq = maskLiftNeedsSeq;
    }

    /// <summary>
    /// Create an ArgTraits with no scopes, no names, and no directives.
    /// </summary>
    public static ArgTraits Create(RexlOper oper, int slotCount,
        BitSet maskLiftSeq = default, BitSet maskLiftTen = default, BitSet maskLiftOpt = default, BitSet maskLiftNeedsSeq = default)
    {
        if (maskLiftSeq.IsEmpty && maskLiftTen.IsEmpty && maskLiftOpt.IsEmpty)
            return ArgTraitsSimple.Create(oper, eager: true, slotCount);
        return new ArgTraitsLifting(oper, slotCount, maskLiftSeq, maskLiftTen, maskLiftOpt, maskLiftNeedsSeq);
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        // Generally functions that use this don't accept sequences, but the safe thing
        // to do is return true.
        return true;
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsLifting other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;
        if (MaskLiftSeq != other.MaskLiftSeq)
            return false;
        if (MaskLiftTen != other.MaskLiftTen)
            return false;
        if (MaskLiftOpt != other.MaskLiftOpt)
            return false;
        return true;
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> that supports names, but no scopes and no lifting.
/// </summary>
public sealed class ArgTraitsNamed : ArgTraitsNoScopes
{
    public override BitSet MaskLiftSeq => default;
    public override BitSet MaskLiftTen => default;
    public override BitSet MaskLiftOpt => default;

    // Which slots accept a name.
    private readonly BitSet _maskName;

    // Which slots that accept a name must have an explicit name.
    private readonly BitSet _maskNameExp;

    // Which slots that accept a name require a name.
    private readonly BitSet _maskNameReq;

    private ArgTraitsNamed(RexlOper oper, int slotCount, BitSet maskName, BitSet maskNameExp, BitSet maskNameReq)
        : base(oper, slotCount)
    {
        Validation.Assert(!maskName.TestAtOrAbove(slotCount));
        Validation.Assert(maskNameExp.IsSubset(maskName));
        Validation.Assert(maskNameReq.IsSubset(maskName));

        _maskName = maskName;
        _maskNameExp = maskNameExp;
        _maskNameReq = maskNameReq;

        SupportDottedImplicitNames = oper.SupportsImplicitDotted && !(maskName - maskNameExp).IsEmpty;
    }

    /// <summary>
    /// Create an ArgTraits with no scopes, no lifting, no directives, possibly with names.
    /// </summary>
    public static ArgTraits Create(RexlOper oper, int slotCount,
        BitSet maskName, BitSet maskNameExp = default, BitSet maskNameReq = default)
    {
        Validation.BugCheckParam(!maskName.TestAtOrAbove(slotCount), nameof(maskName));
        Validation.BugCheckParam(maskNameExp.IsSubset(maskName), nameof(maskNameExp));
        Validation.BugCheckParam(maskNameReq.IsSubset(maskName), nameof(maskNameReq));

        if (maskName.IsEmpty)
            return ArgTraitsSimple.Create(oper, eager: true, slotCount);
        return new ArgTraitsNamed(oper, slotCount, maskName, maskNameExp, maskNameReq);
    }

    public override bool SupportsName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskName.TestBit(slot);
    }

    public override bool SupportsImplicitName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskName.TestBit(slot) && !_maskNameExp.TestBit(slot);
    }

    public override bool SupportDottedImplicitNames { get; }

    public override bool RequiresName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskNameReq.TestBit(slot);
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        // Generally functions that use this don't accept sequences, but the safe thing
        // to do is return true.
        return true;
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsNamed other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;
        if (_maskName != other._maskName)
            return false;
        if (_maskNameExp != other._maskNameExp)
            return false;
        if (_maskNameReq != other._maskNameReq)
            return false;
        return true;
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> with a run of initial sequence args having sequence item scopes.
/// The sequences may be indexed, either individually for nested iteration or as a group for
/// parallel iteration.
/// </summary>
public abstract class ArgTraitsSeq : ArgTraitsBare
{
    public sealed override int ScopeCount { get; }
    public sealed override int ScopeIndexCount { get; }

    protected ArgTraitsSeq(RexlOper oper, bool parallel, bool indexed, int carg, int seqCount)
        : base(oper, carg)
    {
        Validation.Assert(seqCount > 0);
        Validation.AssertIndex(seqCount, carg);

        ScopeCount = seqCount;
        if (indexed)
            ScopeIndexCount = parallel ? 1 : seqCount;
    }

    public sealed override bool IsScope(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount;
    }

    public sealed override bool IsScope(int slot, out int iscope)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        if (slot < ScopeCount)
        {
            iscope = slot;
            return true;
        }
        iscope = -1;
        return false;
    }

    public sealed override bool IsScope(int slot, out int iscope, out int iidx, out bool firstForIdx)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

        if (slot < ScopeCount)
        {
            iscope = slot;
            if (ScopeIndexCount == 0)
            {
                iidx = -1;
                firstForIdx = false;
            }
            else if (ScopeIndexCount == 1)
            {
                iidx = 0;
                firstForIdx = slot == 0;
            }
            else
            {
                iidx = slot;
                firstForIdx = true;
            }
            return true;
        }
        iidx = iscope = -1;
        firstForIdx = false;
        return false;
    }

    public sealed override ScopeKind GetScopeKind(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount ? ScopeKind.SeqItem : ScopeKind.None;
    }

    public override bool SupportsName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount;
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> with parallel sequence args having sequence item scopes, optionally
/// followed by "pre" non-nested args, then nested args having all the sequences in scope, and
/// optionally "post" non-nested args.
/// </summary>
public sealed class ArgTraitsZip : ArgTraitsSeq
{
    private readonly bool _eager;

    // The number of non-nested args, before and after the nested args.
    private readonly int _nonPre;
    private readonly int _nonPost;

    public override int NestedCount { get; }

    private ArgTraitsZip(RexlOper oper, bool indexed, bool eager, int carg, int seqCount, int nonPre, int nonPost)
        : base(oper, parallel: true, indexed, carg, seqCount)
    {
        Validation.AssertIndex(nonPre, carg - seqCount);
        Validation.AssertIndex(nonPost, carg - seqCount - nonPre);

        _eager = eager;
        _nonPre = nonPre;
        _nonPost = nonPost;
        NestedCount = carg - seqCount - _nonPre - _nonPost;
        Validation.Assert(NestedCount > 0);
    }

    /// <summary>
    /// Create an <see cref="ArgTraits"/> with parallel sequence args having sequence item scopes, optionally
    /// followed by "pre" non-nested args, then nested args having all the sequences in scope, and optionally
    /// "post" non-nested args.
    /// </summary>
    public static ArgTraitsZip Create(RexlOper oper, bool indexed, bool eager,
        int carg, int seqCount, int nonPre = 0, int nonPost = 0)
    {
        Validation.BugCheckParam(carg > 0, nameof(carg));
        Validation.BugCheckParam(seqCount > 0, nameof(seqCount));
        Validation.BugCheckIndex(seqCount, carg, nameof(seqCount));
        Validation.BugCheckIndex(nonPre, carg - seqCount, nameof(nonPre));
        Validation.BugCheckIndex(nonPost, carg - seqCount - nonPre, nameof(nonPost));

        return new ArgTraitsZip(oper, indexed, eager, carg, seqCount, nonPre, nonPost);
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsZip other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;
        if (ScopeCount != other.ScopeCount)
            return false;
        if (ScopeIndexCount != other.ScopeIndexCount)
            return false;
        if (_nonPre != other._nonPre)
            return false;
        if (_nonPost != other._nonPost)
            return false;
        return true;
    }

    public override bool IsNested(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return ScopeCount + _nonPre <= slot && slot < SlotCount - _nonPost;
    }

    public override bool IsScopeActive(int slot, int upCount)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));
        return IsNested(slot);
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _eager && IsScope(slot);
    }
}

/// <summary>
/// An <see cref="ArgTraits"/> with two independent sequence args having sequence item scopes, followed by nested
/// args, each with a subset of the sequences in scope.
/// </summary>
public sealed class ArgTraitsJoin : ArgTraitsSeq
{
    private const int SeqCount = 2;

    // Indicates which of the two sequences are in scope for the given nested arg.
    private readonly Immutable.Array<BitSet> _nestedActive;

    public override int NestedCount => _nestedActive.Length;

    private ArgTraitsJoin(RexlOper func, bool indexed, int carg, Immutable.Array<BitSet> nestedActive)
        : base(func, parallel: false, indexed, carg, SeqCount)
    {
        Validation.Assert(nestedActive.Length == carg - ScopeCount);
#if DEBUG
        for (int i = 0; i < nestedActive.Length; i++)
            Validation.Assert(!nestedActive[i].TestAtOrAbove(ScopeCount));
#endif
        _nestedActive = nestedActive;
    }

    /// <summary>
    /// Create an <see cref="ArgTraits"/> with two independent sequence args having sequence item scopes, followed by nested
    /// args, each with a subset of the sequences in scope. Each <see cref="BitSet"/> represents a single nested arg, and the
    /// set bits indicate which of the sequences are in scope for that arg, where bit 0 represents the first sequence and 1
    /// represents the second. Extra bit sets beyond the number of nested args determined by <paramref name="carg"/> are ignored.
    /// </summary>
    public static ArgTraitsJoin Create(RexlOper func, int carg, bool indexed, params BitSet[] nestedActive)
    {
        Validation.BugCheckValue(func, nameof(func));
        Validation.BugCheckParam(carg >= SeqCount, nameof(carg));
        int nestedCount = carg - SeqCount;
        Validation.BugCheckParam(nestedActive.Length >= nestedCount, nameof(nestedActive));

        var bldrNestedActive = Immutable.Array<BitSet>.CreateBuilder(nestedCount, init: true);
        for (int i = 0; i < nestedCount; i++)
        {
            Validation.Assert(!nestedActive[i].TestAtOrAbove(SeqCount));
            bldrNestedActive[i] = nestedActive[i];
        }

        return new ArgTraitsJoin(func, indexed, carg, bldrNestedActive.ToImmutable());
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsJoin other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;
        if (ScopeCount != other.ScopeCount)
            return false;
        if (ScopeIndexCount != other.ScopeIndexCount)
            return false;
        if (NestedCount != other.NestedCount)
            return false;
        for (int i = 0; i < NestedCount; i++)
        {
            if (_nestedActive[i] != other._nestedActive[i])
                return false;
        }
        return true;
    }

    public override bool IsNested(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        int inest = slot - ScopeCount;
        return inest >= 0 && !_nestedActive[inest].IsEmpty;
    }

    public override bool IsScopeActive(int slot, int upCount)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));

        int inest = slot - ScopeCount;
        Validation.Assert(inest < _nestedActive.Length);
        return inest >= 0 && _nestedActive[inest].TestBit(ScopeCount - 1 - upCount);
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }
}

/// <summary>
/// For with/guard scopes.
/// </summary>
public sealed class ArgTraitsWith : ArgTraitsBare
{
    /// <summary>
    /// The scope kind, either with or guard.
    /// </summary>
    private readonly ScopeKind _scopeKind;

    /// <summary>
    /// Whether this lifts over sequence on the first arg.
    /// </summary>
    private readonly BitSet _mask;

    public override int ScopeCount => SlotCount - 1;

    public override BitSet MaskLiftSeq => _mask;

    public override int NestedCount { get; }

    private ArgTraitsWith(RexlOper oper, int arity, bool guard, bool lifts)
        : base(oper, arity)
    {
        Validation.Assert(arity >= 2);
        _scopeKind = guard ? ScopeKind.Guard : ScopeKind.With;
        _mask = lifts ? 0x1 : default(BitSet);
        NestedCount = arity - 1;
    }

    public static ArgTraits Create(RexlOper oper, int arity, bool guard, bool lifts)
    {
        Validation.BugCheckValue(oper, nameof(oper));
        Validation.BugCheckParam(oper.SupportsArity(arity), nameof(arity));
        Validation.BugCheckParam(arity >= 2, nameof(arity));
        return new ArgTraitsWith(oper, arity, guard, lifts);
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsWith other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;
        if (_scopeKind != other._scopeKind)
            return false;
        if (_mask != other._mask)
            return false;
        return true;
    }

    public override ScopeKind GetScopeKind(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount ? _scopeKind : ScopeKind.None;
    }

    public override bool IsNested(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot > 0;
    }

    public override bool IsScope(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount;
    }

    public override bool IsScope(int slot, out int iscope)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        if (slot < ScopeCount)
        {
            iscope = slot;
            return true;
        }
        iscope = -1;
        return false;
    }

    public override bool IsScopeActive(int slot, int upCount)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));
        return slot > upCount;
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return false;
    }

    public override bool SupportsImplicitName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount;
    }

    public override bool SupportsName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return slot < ScopeCount;
    }
}

/// <summary>
/// REVIEW: Should eliminate this form.
/// </summary>
public sealed class ArgTraitsGeneral : ArgTraits
{
    public override int ScopeCount { get; }

    /// <summary>
    /// When all scopes are of the same kind, this is the kind.
    /// </summary>
    private readonly ScopeKind _scopeDef;

    /// <summary>
    /// When there are different kinds of scopes, this specifies the kinds.
    /// </summary>
    private readonly Immutable.Array<ScopeKind> _scopeKinds;

    // REVIEW: This uses a lot of space. Is there a way to mitigate the memory hit?
    // Perhaps functions that support a limited number of arities should re-use the same
    // ArgTraits instances? Perhaps auto-cache similar to how signatures are cached?

    // Which slots are lifted over sequence.
    private readonly BitSet _maskLiftSeq;

    // Which slots are lifted over tensor.
    private readonly BitSet _maskLiftTen;

    // Which slots are lifted over opt.
    private readonly BitSet _maskLiftOpt;

    // Which slots are scopes.
    private readonly BitSet _maskScope;

    // Which slots are nested within previous scopes.
    private readonly BitSet _maskNested;

    // Which slots accept a name.
    private readonly BitSet _maskName;

    // Which slots that accept a name must have an explicit name.
    private readonly BitSet _maskNameExp;

    // Which slots that accept a name require a name.
    private readonly BitSet _maskNameReq;

    // Which slots lazily process a sequence.
    private readonly BitSet _maskLazySeq;

    /// <summary>
    /// This indicates slot/scope pairs that should be "de-activated". That is,
    /// it indicates positions where a nested arg should not have access to some
    /// of the containing scopes. Unlike the other bit sets, this one is indexed
    /// not by slot, but by <c>(slot - 1) * ScopeCount + scopeIndex</c>, where
    /// <c>scopeIndex</c> is increasing to the left from the position of <c>slot</c>.
    /// </summary>
    private readonly BitSet _maskScopeInactive;

    public override BitSet MaskLiftSeq => _maskLiftSeq;

    public override BitSet MaskLiftTen => _maskLiftTen;

    public override BitSet MaskLiftOpt => _maskLiftOpt;

    public override int NestedCount { get; }

    public override bool IsScope(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskScope.TestBit(slot);
    }

    public override bool IsScope(int slot, out int iscope)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        if (!_maskScope.TestBit(slot))
        {
            iscope = -1;
            return false;
        }

        iscope = (_maskScope.ClearAtAndAbove(slot)).Count;
        return true;
    }

    public override ScopeKind GetScopeKind(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        if (_scopeKinds.IsDefaultOrEmpty)
            return _maskScope.TestBit(slot) ? _scopeDef : ScopeKind.None;
        if (!IsScope(slot, out int iscope))
            return ScopeKind.None;
        Validation.AssertIndex(iscope, _scopeKinds.Length);
        return _scopeKinds[iscope];
    }

    public override bool IsNested(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskNested.TestBit(slot);
    }

    public override bool IsScopeActive(int slot, int upCount)
    {
        Validation.BugCheckParam(IsNested(slot), nameof(slot));
        // REVIEW: Need an easy way to more fully validate "upCount".
        Validation.BugCheckIndex(upCount, ScopeCount, nameof(upCount));
        return !_maskScopeInactive.TestBit((slot - 1) * ScopeCount + upCount);
    }

    public override bool IsNestedTail(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));

        if (!IsNested(slot))
            return false;
        if (slot == SlotCount - 1)
            return true;
        if (IsNested(slot + 1))
            return false;
        return true;
    }

    public override bool IsEagerSeq(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return !_maskLazySeq.TestBit(slot);
    }

    public override bool SupportsName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskName.TestBit(slot);
    }

    public override bool SupportsImplicitName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskName.TestBit(slot) && !_maskNameExp.TestBit(slot);
    }

    public override bool SupportDottedImplicitNames { get; }

    public override bool RequiresName(int slot)
    {
        Validation.BugCheckIndex(slot, SlotCount, nameof(slot));
        return _maskNameReq.TestBit(slot);
    }

    public override bool AreEquivalent(ArgTraits cmp)
    {
        if (this == cmp)
            return true;
        if (!(cmp is ArgTraitsGeneral other))
            return false;
        if (Oper != other.Oper)
            return false;
        if (SlotCount != other.SlotCount)
            return false;

        if (_maskScope != other._maskScope)
            return false;
        Validation.Assert(ScopeCount == other.ScopeCount);

        if (_maskNested != other._maskNested)
            return false;
        if (_maskName != other._maskName)
            return false;
        if (_maskNameExp != other._maskNameExp)
            return false;
        if (_maskLiftSeq != other._maskLiftSeq)
            return false;
        if (_maskLiftTen != other._maskLiftTen)
            return false;
        if (_maskLiftOpt != other._maskLiftOpt)
            return false;

        if (_scopeKinds.Length != other._scopeKinds.Length)
            return false;
        if (_scopeKinds.Length > 0)
        {
            Validation.Assert(_scopeKinds.Length == ScopeCount);
            for (int i = 0; i < _scopeKinds.Length; i++)
            {
                if (_scopeKinds[i] != other._scopeKinds[i])
                    return false;
            }
        }
        else if (_scopeDef != other._scopeDef)
            return false;

        return true;
    }

    internal ArgTraitsGeneral(
            RexlOper oper, int slotCount, BitSet maskLiftSeq, BitSet maskLiftTen, BitSet maskLiftOpt,
            ScopeKind scopeKind, Immutable.Array<ScopeKind> scopeKinds, BitSet maskScope, BitSet maskNested,
            BitSet maskName, BitSet maskNameExp, BitSet maskNameReq,
            BitSet maskScopeInactive,
            BitSet maskLazySeq)
        : base(oper, slotCount)
    {
        Validation.Assert(!maskLiftSeq.TestAtOrAbove(slotCount));
        Validation.Assert(!maskLiftTen.TestAtOrAbove(slotCount));
        Validation.Assert(!maskLiftOpt.TestAtOrAbove(slotCount));
        Validation.Assert(!maskNested.TestAtOrAbove(slotCount));
        Validation.Assert(!maskName.TestAtOrAbove(slotCount));
        Validation.Assert(!maskLazySeq.TestAtOrAbove(slotCount));

        // Names must include explicit names and required names.
        Validation.Assert(maskNameExp.IsSubset(maskName));
        Validation.Assert(maskNameReq.IsSubset(maskName));

        // Names must include scopes.
        Validation.Assert(maskScope.IsSubset(maskName));

        _maskLiftSeq = maskLiftSeq;
        _maskLiftTen = maskLiftTen;
        _maskLiftOpt = maskLiftOpt;
        _maskName = maskName;
        _maskNameExp = maskNameExp;
        _maskNameReq = maskNameReq;
        _maskLazySeq = maskLazySeq;

        if (maskScope == 0)
        {
            Validation.Assert(scopeKind == ScopeKind.None);
            Validation.Assert(scopeKinds.IsDefaultOrEmpty);
            Validation.Assert(maskNested.IsEmpty);
            Validation.Assert(maskScopeInactive.IsEmpty);

            _maskScope = default;
            _maskNested = default;
            _maskScopeInactive = default;
            ScopeCount = 0;
            _scopeDef = ScopeKind.None;
            _scopeKinds = default;
        }
        else
        {
#if DEBUG
            if (!scopeKinds.IsDefaultOrEmpty)
            {
                Validation.Assert(scopeKinds.Length == maskScope.Count);
                int iscope = 0;
                for (int slot = 0; slot < slotCount; slot++)
                {
                    if (!maskScope.TestBit(slot))
                        continue;
                    switch (scopeKinds[iscope])
                    {
                    default:
                        Validation.Assert(false);
                        break;

                    case ScopeKind.With:
                        break;
                    case ScopeKind.Guard:
                        // Guard scopes can't be lifted over opt (makes no sense).
                        Validation.Assert(!_maskLiftOpt.TestBit(slot));
                        break;
                    case ScopeKind.SeqItem:
                        // Explicit names must include map scopes.
                        Validation.Assert(maskNameExp.TestBit(slot));
                        // Map scopes can't be lifted over sequence (makes no sense).
                        Validation.Assert(!_maskLiftSeq.TestBit(slot));
                        break;
                    case ScopeKind.TenItem:
                        // Explicit names must include tensor scopes.
                        Validation.Assert(maskNameExp.TestBit(slot));
                        // Tensor scopes can't be lifted over tensor (makes no sense).
                        Validation.Assert(!_maskLiftTen.TestBit(slot));
                        break;
                    case ScopeKind.Iter:
                        // Explicit names must include iter scopes.
                        Validation.Assert(maskNameExp.TestBit(slot));
                        break;
                    }
                    iscope++;
                }
                Validation.Assert(iscope == maskScope.Count);
            }
            else
            {
                switch (scopeKind)
                {
                default:
                    Validation.Assert(false);
                    break;

                case ScopeKind.With:
                    break;
                case ScopeKind.Guard:
                    // Guard scopes can't be lifted over opt (makes no sense).
                    Validation.Assert(!_maskLiftOpt.Intersects(maskScope));
                    break;
                case ScopeKind.SeqItem:
                    // Explicit names must include map scopes.
                    Validation.Assert(maskScope.IsSubset(maskNameExp));
                    // Map scopes can't be lifted over sequence (makes no sense).
                    Validation.Assert(!_maskLiftSeq.Intersects(maskScope));
                    break;
                case ScopeKind.TenItem:
                    // Explicit names must include map scopes.
                    Validation.Assert(maskScope.IsSubset(maskNameExp));
                    // Tensor scopes can't be lifted over tensor (makes no sense).
                    Validation.Assert(!_maskLiftTen.Intersects(maskScope));
                    break;
                case ScopeKind.Iter:
                    // Explicit names must include iter scopes.
                    Validation.Assert(maskScope.IsSubset(maskNameExp));
                    break;
                }
            }

            Validation.Assert(maskScope.SlotMax < maskNested.SlotMax);
            Validation.Assert(maskScope.SlotMin < maskNested.SlotMin);

            // REVIEW: Validate maskScopeInactive.
#endif
            _maskScope = maskScope;
            _maskNested = maskNested;
            _maskScopeInactive = maskScopeInactive;
            ScopeCount = maskScope.Count;
            NestedCount = maskNested.Count;
            _scopeDef = scopeKind;
            _scopeKinds = scopeKinds;
        }

        SupportDottedImplicitNames = oper.SupportsImplicitDotted && !(maskName - maskNameExp).IsEmpty;
    }
}
