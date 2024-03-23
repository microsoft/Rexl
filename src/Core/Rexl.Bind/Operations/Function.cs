// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using ArgTuple = Immutable.Array<BoundNode>;
using DirTuple = Immutable.Array<Directive>;
using NameTuple = Immutable.Array<DName>;
using ScopeTuple = Immutable.Array<ArgScope>;

// This partial is for function specific members.
partial class RexlOper
{
    /// <summary>
    /// Returns whether the operation should be lifted over seq on the given argument. The ArgTraits say yes.
    /// </summary>
    internal protected virtual bool VerifyLiftOverSeq(ArgTraits traits, int iarg, DType typeArg)
    {
        Validation.AssertValue(traits);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.AssertIndex(iarg, traits.SlotCount);
        Validation.Assert(traits.LiftsOverSeq(iarg));
        Validation.Assert(typeArg.SeqCount > traits.MaskLiftNeedsSeq.TestBit(iarg).ToNum());
        return IsFunc;
    }

    /// <summary>
    /// Returns whether the operation should be lifted over tensor on the given argument. The ArgTraits say yes.
    /// </summary>
    internal protected virtual bool VerifyLiftOverTen(ArgTraits traits, int iarg, DType typeArg)
    {
        Validation.AssertValue(traits);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.AssertIndex(iarg, traits.SlotCount);
        Validation.Assert(traits.LiftsOverTen(iarg));
        Validation.Assert(typeArg.IsTensorXxx);
        Validation.Coverage(typeArg.IsOpt ? 1 : 0);
        return IsFunc;
    }

    /// <summary>
    /// Returns whether the operation should be lifted over opt on the given argument. The ArgTraits say yes.
    /// By default, this says no for string, yes for anything else.
    /// </summary>
    internal protected virtual bool VerifyLiftOverOpt(ArgTraits traits, int iarg, DType typeArg)
    {
        Validation.AssertValue(traits);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.AssertIndex(iarg, traits.SlotCount);
        Validation.Assert(traits.LiftsOverOpt(iarg));
        Validation.Assert(typeArg.HasReq);
        return IsFunc;
    }

    /// <summary>
    /// Tests whether this operation can be used as a property for the given type.
    /// </summary>
    public virtual bool IsProperty(DType typeThis)
    {
        return false;
    }

    /// <summary>
    /// Returns flags indicating whether the indicated argument can have a `With`/`Guard` invocation
    /// "pulled out" so <c>F(..., With(w, val), ...)</c> can be reduced to <c>With(w, F(..., val, ...))</c>.
    /// The <see cref="PullWithFlags.Guard"/> flag will not be set unless the <see cref="PullWithFlags.With"/>
    /// flag is set. Generally, this will return <see cref="PullWithFlags.None"/> if the argument is nested
    /// in a looping scope or if the argument is not "strict", meaning that it is not always evaluated before the
    /// operation is evaluated.
    /// </summary>
    public PullWithFlags GetPullWithFlags(BndCallNode call, int iarg)
    {
        Validation.BugCheckParam(IsValidCall(call), nameof(call));

        if (!IsFunc)
            return PullWithFlags.None;

        if (call.Traits.IsRepeated(iarg))
            return PullWithFlags.None;

        var res = GetPullWithFlagsCore(call, iarg);
        Validation.Assert((res & PullWithFlags.Both) != PullWithFlags.Guard);
        return res;
    }

    /// <summary>
    /// Returns flags indicating whether the indicated argument can have a `With`/`Guard` invocation
    /// "pulled out" so <c>F(..., With(w, val), ...)</c> can be reduced to <c>With(w, F(..., val, ...))</c>.
    /// The <see cref="PullWithFlags.Guard"/> flag should not be set unless the <see cref="PullWithFlags.With"/>
    /// flag is set. This should only be called if the argument is not nested in a looping scope. It should return
    /// <see cref="PullWithFlags.None"/> if the argument is not "strict", meaning that it is not always evaluated
    /// before the operation is evaluated. The default implementation assumes that the argument is strict but
    /// that it is <i>not</i> ok to pull "guard" (only "with").
    /// </summary>
    protected virtual PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsFunc);
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));

        return PullWithFlags.With;
    }

    /// <summary>
    /// Returns whether an invocation of this operation with the given information is a volatile
    /// function invocation. Note that a function may support both volatile and pure invocations.
    /// </summary>
    public virtual bool IsVolatile(ArgTraits traits, DType type, ArgTuple args,
        ScopeTuple scopes, ScopeTuple indices, DirTuple dirs, NameTuple names)
    {
        Validation.Assert(IsFunc);
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.Assert(type.IsValid);
        Validation.Assert(!args.IsDefault);
        Validation.Assert(traits.SlotCount == args.Length);
        Validation.Assert(!scopes.IsDefault);
        Validation.Assert(scopes.Length == traits.ScopeCount);
        Validation.Assert(!indices.IsDefault);
        Validation.Assert(indices.Length == traits.ScopeIndexCount);

        return false;
    }

    /// <summary>
    /// If the return type is a sequence type, returns the possible range of item counts.
    /// </summary>
    public (long min, long max) GetItemCountRange(BndCallNode call)
    {
        Validation.BugCheckParam(IsValidCall(call), nameof(call));
        Validation.BugCheckParam(call.Type.IsSequence, nameof(call));

        var (min, max) = GetItemCountRangeCore(call);
        Validation.Assert(0 <= min && min <= max);
        return (min, max);
    }

    protected virtual (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Type.IsSequence);
        return (0, long.MaxValue);
    }
}

/// <summary>
/// Used for an "unknown" function.
/// </summary>
public sealed class UnknownFunc : RexlOper
{
    // REVIEW: Perhaps manufacture a new one of these for each use and give it a path based on
    // the user provided name?
    public static readonly UnknownFunc Instance = new UnknownFunc();

    private UnknownFunc()
        : base(isFunc: true, new DName("Unknown"), NPath.Root.Append(new DName("__err__")), 0, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.Vac, info.GetArgTypes().ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Vac)
            return false;

        // Not valid for code gen.
        full = false;
        return true;
    }
}

public abstract class OneToOneFunc : RexlOper
{
    protected OneToOneFunc(DName name)
        : base(isFunc: true, name, NPath.Root, 1, 1)
    {
    }

    protected OneToOneFunc(DName name, NPath ns)
        : base(isFunc: true, name, ns, 1, 1)
    {
    }
}
