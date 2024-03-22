// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Information about a function/procedure invocation that is being processed. An instance of this is
/// passed to the <see cref="RexlOper.SpecializeTypes(InvocationInfo, Action{BaseDiagnostic}, out ArgTraits)"/>
/// method.
/// </summary>
public abstract class InvocationInfo
{
    /// <summary>
    /// An associated parse node. Note that this may be something other than a <see cref="CallNode"/>
    /// but is always non-null.
    /// </summary>
    public abstract ExprNode ParseNode { get; }

    /// <summary>
    /// The argument parse nodes. This is always non-default.
    /// </summary>
    public abstract Immutable.Array<ExprNode> ParseArgs { get; }

    /// <summary>
    /// This is the information of the associated <see cref="RexlOper"/> instance.
    /// </summary>
    public abstract OperInfo Info { get; }

    /// <summary>
    /// This is the associated <see cref="RexlOper"/> instance. An instance of <see cref="InvocationInfo"/>
    /// can only be passed to methods of its associated <see cref="RexlOper"/>, not to any other
    /// <see cref="RexlOper"/> instances.
    /// </summary>
    public abstract RexlOper Oper { get; }

    /// <summary>
    /// The <see cref="ArgTraits"/> instance provided by the <see cref="InvocationInfo.Oper"/>.
    /// </summary>
    public abstract ArgTraits Traits { get; }

    /// <summary>
    /// The bound arguments. Guaranteed to be <see cref="Arity"/> of these.
    /// </summary>
    public abstract Immutable.Array<BoundNode> Args { get; }

    /// <summary>
    /// The scopes. Guaranteed to be Traits.ScopeCount of these.
    /// </summary>
    public abstract Immutable.Array<ArgScope> Scopes { get; }

    /// <summary>
    /// The index scopes. Guaranteed to be Traits.ScopeIndexCount of these.
    /// </summary>
    public abstract Immutable.Array<ArgScope> Indices { get; }

    /// <summary>
    /// The names if any. This can either be default or have length the same as <see cref="Args"/>.
    /// </summary>
    public abstract Immutable.Array<DName> Names { get; }

    /// <summary>
    /// The name tokens if any. This can either be default or have length the same as <see cref="Args"/>.
    /// </summary>
    public abstract Immutable.Array<Token> NameTokens { get; }

    /// <summary>
    /// The directives if any. This can either be default or have length the same as <see cref="Args"/>.
    /// </summary>
    public abstract Immutable.Array<Directive> Dirs { get; }

    /// <summary>
    /// The length of <see cref="ParseArgs"/>.
    /// </summary>
    public int ParseArity { get { return ParseArgs.Length; } }

    /// <summary>
    /// The number of args, which is the same as Traits.SlotCount.
    /// </summary>
    public int Arity { get { return Traits.SlotCount; } }

    // Don't allow external sub-classes.
    private protected InvocationInfo()
    {
    }

    /// <summary>
    /// Given an arg index, return a reasonable parse node for it. Note that since the parse
    /// arity may be different than the arg arity, this is not necessarily just indexing into
    /// <see cref="ParseArgs"/>.
    /// </summary>
    public ExprNode GetParseArg(int iarg)
    {
        Validation.BugCheckIndex(iarg, Arity, nameof(iarg));
        if (iarg < ParseArity)
            return ParseArgs[iarg];
        return ParseNode;
    }

    /// <summary>
    /// Given an arg index, return a reasonable parse node for it for error messages. Note that
    /// since the parse arity may be different than the arg arity, this is not necessarily just
    /// indexing into <see cref="ParseArgs"/>. Also, unlike <see cref="GetParseArg(int)"/>, this
    /// strips a directive and name, when present.
    /// </summary>
    public ExprNode GetParseArgInner(int iarg)
    {
        Validation.BugCheckIndex(iarg, Arity, nameof(iarg));
        if (iarg >= ParseArity)
            return ParseNode;

        var parg = ParseArgs[iarg];
        if (parg is DirectiveNode dn)
            parg = dn.Value;
        if (parg is VariableDeclNode vd)
            parg = vd.Value;
        return parg;
    }

    /// <summary>
    /// Get the argument types.
    /// </summary>
    public Immutable.Array<DType>.Builder GetArgTypes()
    {
        if (Args.IsDefault)
            return null;
        var bldr = Immutable.Array.CreateBuilder<DType>(Args.Length, init: true);
        for (int iarg = 0; iarg < Args.Length; iarg++)
            bldr[iarg] = Args[iarg].Type;
        return bldr;
    }

    public abstract void PostDiagnostic(BaseDiagnostic diag);
}

/// <summary>
/// Implementation of <see cref="InvocationInfo"/> that wraps around
/// a <see cref="BndCallNode"/> and delegates most properties to it.
/// The exceptions are the parse level properties and the <see cref="Info"/>,
/// which are all null/default.
/// </summary>
internal sealed class WrapInvocationInfo : InvocationInfo
{
    private readonly Action<BaseDiagnostic> _diagSink;

    public BndCallNode Call { get; }

    public override ExprNode ParseNode => null;

    public override Immutable.Array<ExprNode> ParseArgs => default;

    public override Immutable.Array<Token> NameTokens => default;

    public override OperInfo Info => null;

    public override RexlOper Oper => Call.Oper;

    public override ArgTraits Traits => Call.Traits;

    public override Immutable.Array<BoundNode> Args => Call.Args;

    public override Immutable.Array<ArgScope> Scopes => Call.Scopes;

    public override Immutable.Array<ArgScope> Indices => Call.Indices;

    public override Immutable.Array<DName> Names => Call.Names;

    public override Immutable.Array<Directive> Dirs => Call.Directives;

    internal WrapInvocationInfo(BndCallNode call, Action<BaseDiagnostic> diagSink = null)
        : base()
    {
        Validation.AssertValue(call);
        Call = call;
        _diagSink = diagSink;
    }

    public override void PostDiagnostic(BaseDiagnostic diag)
    {
        _diagSink?.Invoke(diag);
    }
}
