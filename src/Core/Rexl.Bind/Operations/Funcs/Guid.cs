// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

public sealed partial class CastGuidFunc : RexlOper
{
    public static readonly CastGuidFunc Instance = new CastGuidFunc();

    private CastGuidFunc()
        : base(isFunc: true, new DName("CastGuid"), 0, 1)
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
        Validation.Assert(info.Arity <= 1);

        if (info.Arity == 0)
            return (DType.GuidReq, Immutable.Array<DType>.Empty);
        return (DType.GuidReq, Immutable.Array.Create(DType.Text));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.GuidReq)
            return false;
        var args = call.Args;
        if (args.Length > 0 && args[0].Type != DType.Text)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode bnd)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(bnd));

        if (bnd.Args.Length == 0)
            return BndDefaultNode.Create(bnd.Type);
        return base.ReduceCore(reducer, bnd);
    }
}

public sealed partial class ToGuidFunc : RexlOper
{
    public static readonly ToGuidFunc Instance = new ToGuidFunc();

    private ToGuidFunc()
        : base(isFunc: true, new DName("ToGuid"), 1, 1)
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
        Validation.Assert(info.Arity == 1);

        return (DType.GuidOpt, Immutable.Array.Create(DType.Text));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.GuidOpt)
            return false;
        var args = call.Args;
        if (args[0].Type != DType.Text)
            return false;
        return true;
    }
}

/// <summary>
/// Volatile function taking no arguments to create a new guid.
/// </summary>
public sealed partial class MakeGuidFunc : RexlOper
{
    public static readonly MakeGuidFunc Instance = new MakeGuidFunc();

    private MakeGuidFunc()
        : base(isFunc: true, new DName("Make"), NPath.Root.Append(new DName("Guid")), 0, 0)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(carg == 0);
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 0);

        return (DType.GuidReq, Immutable.Array<DType>.Empty);
    }

    public override bool IsVolatile(ArgTraits traits, DType type,
        Immutable.Array<BoundNode> args, Immutable.Array<ArgScope> scopes, Immutable.Array<ArgScope> indices,
        Immutable.Array<Directive> dirs, Immutable.Array<DName> names)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.Assert(type == DType.GuidReq);

        // This function is always volatile.
        return true;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.GuidReq)
            return false;
        return true;
    }
}
