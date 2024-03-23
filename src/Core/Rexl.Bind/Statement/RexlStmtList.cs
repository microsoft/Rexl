// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Parse;

/// <summary>
/// A rexl statement list script.
/// </summary>
public sealed class RexlStmtList : RexlScript
{
    /// <summary>
    /// The ParseTree as a <see cref="StmtListNode"/>.
    /// </summary>
    public new StmtListNode ParseTree { get; }

    private RexlStmtList(
            StmtListNode tree,
            TokenStream tokens, Immutable.Array<Token> toksFiltered, ReadOnly.HashSet<int> useAlt,
            Immutable.Array<RexlDiagnostic> diagnostics)
        : base(tokens.VerifyValue().Source.RangeAll, tree, tokens, toksFiltered, useAlt, diagnostics)
    {
        ParseTree = tree;
    }

    private RexlStmtList(
            SourceRange rng, StmtListNode tree,
            TokenStream tokens, Immutable.Array<Token> toksFiltered, ReadOnly.HashSet<int> useAlt,
            Immutable.Array<RexlDiagnostic> diagnostics)
        : base(rng, tree, tokens, toksFiltered, useAlt, diagnostics)
    {
        ParseTree = tree;
    }

    /// <summary>
    /// Parse the given <paramref name="script"/> as a <see cref="StmtListNode"/> and encapsulate
    /// the results, including parse tree, tokens, and errors.
    /// </summary>
    public static RexlStmtList Create(SourceContext source)
    {
        Validation.BugCheckValue(source, nameof(source));
        var psr = Parser.ParseStmtList(source);
        return new RexlStmtList(psr.ParseTree.Cast<StmtListNode>(), psr.Tokens, psr.FilteredTokens, psr.UseAlt, psr.GetDiagnostics());
    }

    /// <summary>
    /// This is typically used to get a <see cref="RexlStmtList"/> for a <see cref="StmtListNode"/> that is
    /// part of a <see cref="RexlStmtList"/>.
    /// </summary>
    public static RexlStmtList CreateSubStmtList(RexlScript rs, StmtListNode stmts)
    {
        Validation.BugCheckValue(rs, nameof(rs));
        Validation.BugCheckValue(stmts, nameof(stmts));
        Validation.BugCheckParam(rs.InTree(stmts), nameof(stmts));

        if (stmts == rs.ParseTree)
        {
            Validation.Assert(rs is RexlStmtList);
            return (RexlStmtList)rs;
        }

        var rng = stmts.GetFullRange();
        return new RexlStmtList(rng, stmts, rs.Tokens, rs.FilteredTokens, rs.UseAlt, rs.Diagnostics);
    }
}
