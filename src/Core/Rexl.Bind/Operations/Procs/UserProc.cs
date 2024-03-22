// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using NameTuple = Immutable.Array<DName>;

/// <summary>
/// Represents a procedure defined as rexl blocks for priming (optional) and playing (required).
/// </summary>
public sealed class UserProc : RexlOper
{
    private readonly Impl _impl;

    /// <summary>
    /// This is the root namespace when the udp was declared. The udp body does not look
    /// outside this namespace for udfs and udps.
    /// REVIEW: Is this statement true? Figure out which udps/udfs should be visible.
    /// </summary>
    public NPath RootNamespace { get; }

    /// <summary>
    /// The arity of the UDP, which is fixed.
    /// </summary>
    public int Arity => ParamNames.Length;

    /// <summary>
    /// The parameter names.
    /// </summary>
    public NameTuple ParamNames { get; }

    /// <summary>
    /// The <see cref="RexlStmtList"/> that contains the <see cref="Prime"/> and <see cref="Play"/> blocks.
    /// </summary>
    public RexlStmtList Outer { get; }

    /// <summary>
    /// The optional statement block for priming.
    /// </summary>
    public BlockStmtNode Prime { get; }

    /// <summary>
    /// The statement block for playing.
    /// </summary>
    public BlockStmtNode Play { get; }

    private UserProc(DName name, NPath ns, NPath nsRoot, NameTuple parms,
            RexlStmtList outer, BlockStmtNode prime, BlockStmtNode play)
        : base(isFunc: false, name: name, ns: ns, arityMin: parms.Length, arityMax: parms.Length)
    {
        Validation.Assert(ns.StartsWith(nsRoot));
        Validation.Assert(!parms.IsDefault);
        Validation.AssertValue(outer);
        Validation.AssertValueOrNull(prime);
        Validation.Assert(prime == null || outer.InTree(prime));
        Validation.AssertValue(play);
        Validation.Assert(outer.InTree(play));

        RootNamespace = nsRoot;
        ParamNames = parms;
        Outer = outer;
        Prime = prime;
        Play = play;
        _impl = new Impl(this);
    }

    public static UserProc Create(NPath path, NPath nsRoot, NameTuple parms,
        RexlStmtList outer, BlockStmtNode prime, BlockStmtNode play)
    {
        Validation.BugCheckParam(path.StartsWith(nsRoot), nameof(path));
        Validation.BugCheckParam(path.NameCount > nsRoot.NameCount, nameof(path));
        Validation.BugCheckParam(!parms.IsDefault, nameof(parms));
        Validation.BugCheckValueOrNull(prime);
        Validation.BugCheckParam(prime == null || outer.InTree(prime), nameof(prime));
        Validation.BugCheckValue(play, nameof(play));
        Validation.BugCheckParam(outer.InTree(play), nameof(play));

        return new UserProc(path.Leaf, path.Parent, nsRoot, parms, outer, prime, play);
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

        return (DType.General, info.GetArgTypes().ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (type != DType.General)
            return false;
        full = false;
        return true;
    }

    protected override BndCallNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var items = NamedItems.Empty;
        int carg = Arity;
        var rec = BndRecordNode.Create(ParamNames, call.Args);
        return BndCallNode.Create(_impl, DType.General, Immutable.Array<BoundNode>.Create(rec));
    }

    /// <summary>
    /// This has the "arguments" bundled as a record. Note that the arguments become initial "globals".
    /// </summary>
    public sealed class Impl : RexlOper
    {
        public override UserProc Parent { get; }

        public Impl(UserProc parent)
            : base(isFunc: false, name: new DName("Impl"), ns: parent.Path, arityMin: 1, arityMax: 1)
        {
            Validation.AssertValue(parent);
            Parent = parent;
        }

        protected override ArgTraits GetArgTraitsCore(int carg)
        {
            Validation.Assert(SupportsArity(carg));
            return ArgTraitsSimple.Create(this, eager: false, carg);
        }

        protected override bool CertifyCore(BndCallNode call, ref bool full)
        {
            Validation.Assert(SupportsArity(call.Args.Length));
            if (call.Type != DType.General)
                return false;
            if (!call.Args[0].Type.IsRecordReq)
                return false;
            return true;
        }
    }
}
