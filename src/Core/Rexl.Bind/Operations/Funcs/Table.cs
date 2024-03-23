// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Adds/sets computed fields to a record. The first parameter must be of a record type.
/// Note however, that this lifts over sequence and opt. The remaining parameters must
/// have names, either explicit or implicit. The parameter names are the new field names.
/// A literal null value means the field should be "dropped". For the rename version,
/// values that are a simple field of the first parameter type cause those fields to be
/// dropped, effectively "renaming" the field.
/// </summary>
public sealed partial class SetFieldsFunc : RexlOper
{
    public static readonly SetFieldsFunc AddFields = new SetFieldsFunc(rename: false);
    public static readonly SetFieldsFunc SetFields = new SetFieldsFunc(rename: true);

    /// <summary>
    /// Whether this does renaming.
    /// </summary>
    public bool IsRename { get; }

    private SetFieldsFunc(bool rename)
        : base(isFunc: true, new DName(rename ? "SetFields" : "AddFields"), 2, int.MaxValue)
    {
        IsRename = rename;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));

        var maskFlds = BitSet.GetMask(1, carg);
        return ArgTraits.CreateGeneral(this, carg, maskLiftSeq: 0x1, maskLiftTen: 0x1, maskLiftOpt: 0x1,
            scopeKind: ScopeKind.With, maskScope: 0x1, maskNested: maskFlds,
            maskName: maskFlds, maskNameReq: maskFlds, maskLazySeq: BitSet.GetMask(carg));
    }

    protected override DType GetScopeArgTypeCore(ArgTraits traits, int slot, DType type)
    {
        Validation.AssertValue(traits);
        Validation.Assert(slot == 0);
        Validation.Assert(traits.GetScopeKind(slot) == ScopeKind.With);

        // We lift so the first arg type shouldn't be sequence, tensor, or have a req.
        Validation.Assert(!type.IsSequence);
        Validation.Assert(!type.IsTensorXxx);
        Validation.Assert(!type.HasReq);
        if (type.IsRecordReq)
            return type;
        return DType.EmptyRecordReq;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        // The binder should totally handle this function, so shouldn't be in an InvocationInfo.
        throw Validation.BugExcept("Internal error");
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        // SetFields is transformed by the binder to a different kind of bound node.
        // It should never appear in a BndCallNode.
        return false;
    }
}

/// <summary>
/// Group by some keys and produce a nested structure.
/// </summary>
public sealed partial class GroupByFunc : RexlOper
{
    public static readonly GroupByFunc Instance = new GroupByFunc();

    private GroupByFunc()
        : base(isFunc: true, new DName("GroupBy"), 2, int.MaxValue)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));

        // This is totally wrong, but the binder handles everything anyway, so it doesn't really
        // matter what we do here.
        // REVIEW: Should the binder bypass even calling this?
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        // The binder should totally handle this function, so shouldn't be in an InvocationInfo.
        throw Validation.BugExcept("Internal error");
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        // GroupBy is transformed by the binder to a different kind of bound node.
        // It should never appear in a BndCallNode.
        return false;
    }
}
