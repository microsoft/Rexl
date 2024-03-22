// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using NameTuple = Immutable.Array<DName>;

/// <summary>
/// Wraper around either a <see cref="UserFunc"/> or <see cref="UserProc"/>.
/// </summary>
public struct UserOper
{
    private readonly RexlOper _oper;

    public bool IsValid => _oper is not null;

    public RexlOper Oper => _oper;
    public UserFunc AsFunc => _oper as UserFunc;
    public UserProc AsProc => _oper as UserProc;

    public int Arity => (_oper as UserFunc)?.Arity ?? (_oper as UserProc)?.Arity ?? -1;

    private UserOper(UserFunc oper) { _oper = oper; }
    private UserOper(UserProc oper) { _oper = oper; }

    public static implicit operator UserOper(UserFunc oper) => new UserOper(oper);
    public static implicit operator UserOper(UserProc oper) => new UserOper(oper);
}

/// <summary>
/// Represents a function defined as a rexl expression.
/// </summary>
public sealed class UserFunc : RexlOper, IHaveSignatures
{
    /// <summary>
    /// The UDF signatures.
    /// </summary>
    private readonly Immutable.Array<Signature> _signatures;

    /// <summary>
    /// This is the root namespace when the udf was declared. The udf body does not look
    /// outside this namespace for other udfs.
    /// </summary>
    public NPath RootNamespace { get; }

    /// <summary>
    /// The arity of the UDF, which is fixed.
    /// </summary>
    public int Arity => ParamNames.Length;

    /// <summary>
    /// The parameter names.
    /// </summary>
    public NameTuple ParamNames { get; }

    /// <summary>
    /// The parsed formula. Note that this is not bound. Binding happens for each usage.
    /// </summary>
    public RexlFormula Formula { get; }

    /// <summary>
    /// Whether this function can be used as a "property".
    /// </summary>
    public bool IsProp { get; }

    private UserFunc(DName name, NPath ns, NPath nsRoot, NameTuple parms,
            RexlFormula fma, bool isProp, Immutable.Array<Signature> signatures)
        : base(isFunc: true, name, ns, parms.Length, parms.Length)
    {
        Validation.Assert(ns.StartsWith(nsRoot));
        Validation.AssertValue(fma);
        Validation.Assert(signatures.Length == 1);

        RootNamespace = nsRoot;
        ParamNames = parms;
        Formula = fma;
        IsProp = isProp && Arity == 1;
        _signatures = signatures;
    }

    public static UserFunc Create(NPath path, NPath nsRoot, NameTuple parms,
        RexlFormula fma, bool isProp, string description = null)
    {
        Validation.BugCheckParam(path.StartsWith(nsRoot), nameof(path));
        Validation.BugCheckParam(path.NameCount > nsRoot.NameCount, nameof(path));
        Validation.BugCheckParam(!parms.IsDefault, nameof(parms));
        Validation.BugCheckValue(fma, nameof(fma));

        var args = new Argument[parms.Length];
        for (int i = 0; i < args.Length; i++)
            args[i] = Argument.Create(StringId.Create(parms[i].Value), RexlStrings.ArgUdf);
        var descr = string.IsNullOrEmpty(description) ? RexlStrings.AboutUdf : StringId.Create(description);
        var signatures = Immutable.Array.Create(new Signature(descr, args));
        return new UserFunc(path.Leaf, path.Parent, nsRoot, parms, fma, isProp, signatures);
    }

    public override bool IsProperty(DType typeThis)
    {
        return IsProp;
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
        // UserFunc is expanded inline by the binder, so this should never be called.
        Validation.Assert(false);
        throw new InvalidOperationException("UserFunc should not be in a BndCallNode");
    }

    public Immutable.Array<Signature> GetSignatures()
    {
        return _signatures;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        // User funcs should be expanded, not used in BndCallNode.
        return false;
    }
}
