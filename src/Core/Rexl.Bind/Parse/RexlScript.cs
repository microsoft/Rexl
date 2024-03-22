// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Parse;

/// <summary>
/// Encapsulates the text and parse tree of some rexl code, whether expression
/// <seealso cref="RexlFormula"/> or statement list <seealso cref="RexlStmtList"/>, as well
/// as any parse errors. Note that it doesn't include binding information, since that depends
/// on context, while parsing does not.
/// </summary>
public abstract partial class RexlScript
{
    /// <summary>
    /// Maps from node to parent, for all nodes in the <see cref="ParseTree"/>. Built lazily (when needed).
    /// </summary>
    private volatile Dictionary<RexlNode, RexlNode> _toPar;

    /// <summary>
    /// The original source context.
    /// </summary>
    public SourceContext Source { get; }

    /// <summary>
    /// The text for the entire original script.
    /// </summary>
    public string Text => Source.Text;

    /// <summary>
    /// The portion of <see cref="Text"/> that is owned by this script, as indicated by
    /// the <see cref="TextRange"/> property. When this script is a "sub-formula", this
    /// is likely not all of <see cref="Text"/>.
    /// </summary>
    public string OwnText { get; }

    /// <summary>
    /// The range for the portion of <see cref="Text"/> that is owned by this script. When this
    /// script is a "sub-formula", this is likely not all of <see cref="Text"/>.
    /// </summary>
    public SourceRange TextRange { get; }

    /// <summary>
    /// The ParseTree.
    /// </summary>
    public RexlNode ParseTree { get; }

    /// <summary>
    /// The original token stream.
    /// </summary>
    public TokenStream Tokens { get; }

    /// <summary>
    /// All the tokens, including comment tokens.
    /// </summary>
    public Immutable.Array<Token> AllTokens { get; }

    /// <summary>
    /// The tokens with comments removed.
    /// </summary>
    public Immutable.Array<Token> FilteredTokens { get; }

    /// <summary>
    /// The set of tokens, identified by index, that used their alternative token for the parse tree.
    /// This may contain tokens outside the set of "valid" tokens for the script.
    /// </summary>
    public ReadOnly.HashSet<int> UseAlt { get; }

    /// <summary>
    /// Gets whether there are parsing errors or warnings.
    /// </summary>
    public bool HasDiagnostics { get { return Diagnostics.Length > 0; } }

    /// <summary>
    /// Gets the (possibly empty) array of parsing errors.
    /// </summary>
    public Immutable.Array<RexlDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets whether there are parsing errors.
    /// </summary>
    public bool HasErrors { get { return Errors.Length > 0; } }

    /// <summary>
    /// Gets the (possibly empty) array of parsing errors.
    /// </summary>
    public Immutable.Array<RexlDiagnostic> Errors { get; }

    /// <summary>
    /// Gets whether there are parsing warnings.
    /// </summary>
    public bool HasWarnings { get { return Warnings.Length > 0; } }

    /// <summary>
    /// Gets the (possibly empty) array of parsing warnings.
    /// </summary>
    public Immutable.Array<RexlDiagnostic> Warnings { get; }

    /// <summary>
    /// Gets the (possibly empty) array of parsing diagnostics that include guesses/changes.
    /// These are sorted by min with tie-breaker lim.
    /// </summary>
    public Immutable.Array<RexlDiagnostic> Suggestions { get; }

    /// <summary>
    /// If there are any diagnostics with suggested/guessed changes, this is the result of applying those changes.
    /// Otherwise, this is null.
    /// </summary>
    public string CorrectedText { get; }

    /// <summary>
    /// Adds any parsing warnings to <paramref name="warnings"/> and returns whether there were
    /// any parsing warnings.
    /// </summary>
    public bool GetWarnings(ref List<BaseDiagnostic> warnings)
    {
        if (Warnings.Length == 0)
            return false;

        if (warnings == null)
            warnings = new List<BaseDiagnostic>();
        warnings.AddRange(Warnings);
        return true;
    }

    /// <summary>
    /// Adds any parsing errors to <paramref name="errors"/> and returns whether there were
    /// any parsing errors.
    /// </summary>
    public bool GetErrors(ref List<BaseDiagnostic> errors)
    {
        if (Errors.Length == 0)
            return false;

        if (errors == null)
            errors = new List<BaseDiagnostic>();
        errors.AddRange(Errors);
        return true;
    }

    /// <summary>
    /// Adds any parsing diagnostics to <paramref name="diagnostics"/> and returns whether there were
    /// any parsing diagnostics.
    /// </summary>
    public bool GetDiagnostics(ref List<BaseDiagnostic> diagnostics)
    {
        if (Diagnostics.Length == 0)
            return false;

        if (diagnostics == null)
            diagnostics = new List<BaseDiagnostic>();
        diagnostics.AddRange(Diagnostics);
        return true;
    }

    private protected RexlScript(
            SourceRange rng, RexlNode tree,
            TokenStream tokens, Immutable.Array<Token> toksFiltered, ReadOnly.HashSet<int> useAlt,
            Immutable.Array<RexlDiagnostic> diagnostics)
    {
        Validation.AssertValue(tokens);
        Validation.Assert(0 <= rng.Min & rng.Min <= rng.Lim & rng.Lim <= tokens.Source.Text.Length);
        Validation.AssertValue(tree);
        Validation.Assert(tokens.Length > 0 && tokens[tokens.Length - 1].Kind == TokKind.Eof);
        Validation.Assert(!toksFiltered.IsDefault);
        Validation.Assert(toksFiltered.Length > 0 && toksFiltered[toksFiltered.Length - 1].Kind == TokKind.Eof);
        Validation.Assert(!diagnostics.IsDefault);

        Source = tokens.Source;
        ParseTree = tree;

        var toksAll = tokens.Tokens;
        string text = Source.Text;
        if (rng.Min > 0 || rng.Lim < Text.Length)
        {
            // See if we need to filter tokens and/or diagnostics. Keep only tokens and diagnostics
            // associated with this tree, plus the eof token.
            int itokMin = rng.Min > 0 ? FindTok(toksAll, rng.Min) : 0;
            int itokLim = rng.Lim < text.Length ? FindTok(toksAll, rng.Lim) : toksAll.Length;
            while (itokLim < toksAll.Length && toksAll[itokLim].Range.Lim <= rng.Lim)
                itokLim++;

            if (itokMin < itokLim)
                rng = toksAll[itokMin].Range.Union(toksAll[itokLim - 1].Range);
            text = text.Substring(rng.Min, rng.Lim - rng.Min);

            if (itokMin > 0 || itokLim < toksAll.Length - 1)
            {
                // Filter tokens and diagnostics. Note that we want to keep the eof token.
                var bldr = toksAll.ToBuilder();
                if (itokLim < toksAll.Length - 1)
                    bldr.RemoveMinLim(itokLim, toksAll.Length - 1);
                if (itokMin > 0)
                    bldr.RemoveMinLim(0, itokMin);
                Validation.Assert(bldr.Count > 0 && bldr[bldr.Count - 1].Kind == TokKind.Eof);
                toksAll = bldr.ToImmutable();

                int itokDst = 0;
                for (int itokSrc = 0; itokSrc < bldr.Count; itokSrc++)
                {
                    Validation.Assert(itokDst <= itokSrc);
                    var tok = bldr[itokSrc];
                    if (tok is CommentToken)
                        continue;
                    if (itokDst < itokSrc)
                        bldr[itokDst] = tok;
                    itokDst++;
                }
                Validation.Assert(itokDst <= bldr.Count);
                if (itokDst < bldr.Count)
                {
                    bldr.RemoveTail(itokDst);
                    toksFiltered = bldr.ToImmutable();
                }
                else
                    toksFiltered = toksAll;

                // Filter diagnostics.
                Immutable.Array<RexlDiagnostic>.Builder bldrDiag = diagnostics.ToBuilder();
                int idiagDst = 0;
                for (int idiagSrc = 0; idiagSrc < bldrDiag.Count; idiagSrc++)
                {
                    var diag = bldrDiag[idiagSrc];
                    if (diag is RexlDiagnostic rerr)
                    {
                        if (rerr.Node != null)
                        {
                            // Toss the diag if the node isn't in our tree.
                            if (!InTree(rerr.Node))
                                continue;
                        }
                        else
                        {
                            // Toss the diag if the token isn't in the range.
                            if (rerr.Tok.Range.Min < rng.Min || rerr.Tok.Range.Lim > rng.Lim)
                                continue;
                        }
                    }

                    if (idiagDst < idiagSrc)
                        bldrDiag[idiagDst] = diag;
                    idiagDst++;
                }
                Validation.Assert(bldrDiag.Count == diagnostics.Length);
                Validation.Assert(0 <= idiagDst & idiagDst <= bldrDiag.Count);

                if (idiagDst < bldrDiag.Count)
                {
                    bldrDiag.RemoveTail(idiagDst);
                    diagnostics = bldrDiag.ToImmutable();
                }
            }
        }
        OwnText = text;
        TextRange = rng;
        Tokens = tokens;
        AllTokens = toksAll;
        FilteredTokens = toksFiltered;
        UseAlt = useAlt;

        Diagnostics = diagnostics;
        (Errors, Warnings, Suggestions) = RexlDiagnostic.Partition(diagnostics);

        if (Suggestions.Length > 0)
            CorrectedText = ApplyChanges(Suggestions);
    }

    public string ApplyChanges(ReadOnly.Array<RexlDiagnostic> mods)
    {
        if (mods.Length == 0)
            return null;

        StringBuilder res = null;
        int ichCur = TextRange.Min;
        RexlDiagnostic prev = null;
        foreach (var diag in mods)
        {
            Validation.Assert(prev == null || RexlDiagnostic.CompareSuggestions(prev, diag) <= 0);

            var guessCur = diag.Guess;
            if (guessCur == null)
                continue;
            if (diag.RngGuess.Source != TextRange.Source)
                continue;

            var rngCur = diag.RngGuess;
            Validation.Assert(rngCur.Min <= rngCur.Lim);
            if (!(TextRange.Min <= rngCur.Min && rngCur.Lim <= TextRange.Lim))
                continue;

            if (ichCur < rngCur.Lim)
            {
                res ??= new StringBuilder();
                AppendSafe(res, Text, ichCur, Math.Max(ichCur, rngCur.Min));
                AppendSafe(res, guessCur, 0, guessCur.Length);
                ichCur = rngCur.Lim;
            }
        }

        if (res == null)
        {
            Validation.Assert(ichCur == TextRange.Min);
            return null;
        }

        AppendSafe(res, Text, ichCur, TextRange.Lim);
        return res.ToString();
    }

    private static void AppendSafe(StringBuilder sb, string text, int ichMin, int ichLim)
    {
        Validation.AssertIndexInclusive(ichLim, text.Length);
        Validation.AssertIndexInclusive(ichMin, ichLim);

        if (ichLim > ichMin)
        {
            if (sb.Length > 0 && ichLim > ichMin && LexUtils.IsIdentPossible(text[ichMin]) && LexUtils.IsIdentPossible(sb[sb.Length - 1]))
                sb.Append(' ');
            sb.Append(text, ichMin, ichLim - ichMin);
        }
    }

    private protected static int FindTok(Immutable.Array<Token> toks, int ich)
    {
        Validation.Assert(toks.Length > 0);

        int itokMin = 0;
        int itokLim = toks.Length;
        while (itokMin < itokLim)
        {
            int itokMid = (itokMin + itokLim) / 2;
            if (toks[itokMid].Range.Min < ich)
                itokMin = itokMid + 1;
            else
                itokLim = itokMid;
        }
        Validation.Assert(itokMin == itokLim);
        Validation.Assert(itokMin == 0 || toks[itokMin - 1].Range.Min < ich);
        Validation.Assert(itokMin == toks.Length || toks[itokMin].Range.Min >= ich);
        return itokMin;
    }

    public override string ToString()
    {
        return OwnText;
    }

    /// <summary>
    /// Gets the range of tokens which intersects with the specified
    /// range of characters.
    /// </summary>
    public void GetTokenRangeFromCharRange(int ichMin, int ichLim, out int itokMin, out int itokLim)
    {
        Validation.BugCheck(0 <= ichMin & ichMin <= ichLim);
        GetTokenRangeFromCharRangeCore(AllTokens, ichMin, ichLim, out itokMin, out itokLim);
    }

    /// <summary>
    /// Gets the minimal parse node which semantically covers the specified character range.
    /// If the selection contains a token used in the parse tree, such a minimal parse node
    /// is always well-defined, and both items in the returned tuple equal that node.
    /// Otherwise, it seeks the minimal nodes that covers the closest token to the character
    /// range from left and right respectively, and returns both. In this case, at least one
    /// of the two nodes in the returned tuple is not null.
    /// </summary>
    public (RexlNode Left, RexlNode Right) GetNodesFromCharRange(int ichMin, int ichLim)
    {
        Validation.BugCheck(0 <= ichMin & ichMin <= ichLim);

        var rngFull = ParseTree.GetFullRange();
        if (ichMin < rngFull.Min)
        {
            ichMin = rngFull.Min;
            if (ichLim < ichMin)
                ichLim = ichMin;
        }
        if (ichLim > rngFull.Lim)
        {
            ichLim = rngFull.Lim;
            if (ichMin > ichLim)
                ichMin = ichLim;
        }

        // Uses only the filtered tokens (no comment token) because
        // a comment token is not associated with parse node.
        var tokens = FilteredTokens;
        Validation.Assert(!tokens.IsDefault);
        GetTokenRangeFromCharRangeCore(tokens, ichMin, ichLim, out int itokMin, out int itokLim);
        Validation.Assert(0 <= itokMin & itokMin <= itokLim & itokLim <= tokens.Length);

        if (itokMin < itokLim)
        {
            // There are some valid tokens in selection.
            // Snap to union of selected token range.
            var rng = tokens[itokMin].Range.Union(tokens[itokLim - 1].Range);
            var node = RangeToNodeVisitor.Run(ParseTree, rng);
            if (node != null)
                return (node, node);
        }

        // There are no filtered tokens in selection, most commonly when the selection is just a
        // cursor position (ichMin == ichLim). We should decide a parse node it best refers to.
        // We make the decision by looking at the nodes that cover the range of its immediately left
        // and immediately right tokens, respectively.
        GetTokenRangeFromCharRangeCore(tokens, rngFull.Min, rngFull.Lim, out int itokFullMin, out int itokFullLim);
        Validation.Assert(0 <= itokFullMin & itokFullMin <= itokFullLim & itokFullLim <= tokens.Length);

        RexlNode leftNode = null, rightNode = null;
        while (itokMin > itokFullMin && leftNode == null)
        {
            var rng = tokens[--itokMin].Range;
            leftNode = RangeToNodeVisitor.Run(ParseTree, rng);
        }

        while (itokLim < itokFullLim && rightNode == null)
        {
            var rng = tokens[itokLim++].Range;
            rightNode = RangeToNodeVisitor.Run(ParseTree, rng);
        }

        Validation.Assert(leftNode != null || rightNode != null);
        return (leftNode, rightNode);
    }

    /// <summary>
    /// Gets the token range from an Immutable.Array of tokens from a given character range [ichMin, ichLim).
    /// The tokens are supposed to be the output of the lexer and should be in left to right
    /// order.
    /// </summary>
    /// <param name="tokens">The immutable array of tokens in order.</param>
    /// <param name="itokMin">
    /// The largest token index in tokens such that all tokens with
    /// index smaller than itokMin does not intersect with the character range.
    /// </param>
    /// <param name="itokLim">
    /// The smallest token index in tokens such that all tokens with index no smaller
    /// than itokLim does not intersect with the character range.
    /// </param>
    private void GetTokenRangeFromCharRangeCore(Immutable.Array<Token> tokens, int ichMin, int ichLim, out int itokMin, out int itokLim)
    {
        Validation.Assert(!tokens.IsDefaultOrEmpty);
        Validation.Assert(0 <= ichMin & ichMin <= ichLim);

        bool LeftIntersectTokenPredicate(int itok)
        {
            var rng = tokens[itok].Range;
            return rng.Lim > ichMin || rng.Min >= ichMin;
        }

        bool RightExclusiveTokenPredicate(int itok)
        {
            var rng = tokens[itok].Range;
            // REVIEW: Tweaking so zero width tokens are included.
            // return rng.Min >= ichLim;
            return rng.Min >= ichLim && rng.Lim > ichLim;
        }

        itokMin = BinarySearch(0, tokens.Length, LeftIntersectTokenPredicate);
        itokLim = BinarySearch(0, tokens.Length, RightExclusiveTokenPredicate);

        Validation.Assert(0 <= itokMin & itokMin <= itokLim & itokLim <= tokens.Length);
#if DEBUG
        for (int itok = 0; itok < tokens.Length; itok++)
        {
            if (itok < itokMin)
                Validation.Assert(!LeftIntersectTokenPredicate(itok));
            else
                Validation.Assert(LeftIntersectTokenPredicate(itok));

            if (itok < itokLim)
                Validation.Assert(!RightExclusiveTokenPredicate(itok));
            else
                Validation.Assert(RightExclusiveTokenPredicate(itok));
        }
#endif
    }

    /// <summary>
    /// Precondition: predicate is monotonically increasing.
    /// That is, if predicate(x) is true, then predicate(y) is true for all y >= x.
    /// If found, returns the smallest index in [min, lim) such that predicate returns true.
    /// If predicate is false for all indices in [min, lim), return value is lim.
    /// </summary>
    private int BinarySearch(int min, int lim, Func<int, bool> predicate)
    {
        while (min < lim)
        {
            int mid = (min + lim) / 2;
            Validation.Assert(mid < lim);
            if (predicate(mid))
                lim = mid;
            else
                min = mid + 1;
        }

        Validation.Assert(min == lim);
        return min;
    }

    /// <summary>
    /// The parse tree visitor to find the minimal node whose full range
    /// covers the specified character range.
    /// </summary>
    private sealed class RangeToNodeVisitor : NoopTreeVisitor
    {
        private readonly int _min;
        private readonly int _lim;
        private RexlNode _result;

        private RangeToNodeVisitor(int min, int lim)
        {
            Validation.Assert(min <= lim);
            _min = min;
            _lim = lim;
        }

        public static RexlNode Run(RexlNode tree, SourceRange rng)
        {
            // Snap the requested range to the full range of the tree.
            var rngTree = tree.GetFullRange();
            int min = rng.Min.Clamp(rngTree.Min, rngTree.Lim);
            int lim = rng.Lim.Clamp(rngTree.Min, rngTree.Lim);

            var visitor = new RangeToNodeVisitor(min, lim);
            tree.Accept(visitor);
            return visitor._result;
        }

        protected override bool PreVisitCore(RexlNode node)
        {
            // If the result is already found, there is no need to
            // process its children.
            if (_result != null)
                return false;

            // If the node does not cover the character range,
            // none of its children do. So there is no need to process
            // its children either.
            var rng = node.GetFullRange();
            return rng.Min <= _min && _lim <= rng.Lim;
        }

        protected override void VisitCore(RexlNode node)
        {
            if (_result == null)
            {
                var rng = node.GetFullRange();
                if (rng.Min <= _min && _lim <= rng.Lim)
                    _result = node;
            }
        }

        protected override void PostVisitImpl(CallNode node)
        {
            // For a piped call node, the range of the entire node is the same as the
            // range of its arguments, but the argument list is technically
            // a child of the node so it's passed up first. We need to check if we're
            // at the function name or argument list.
            if (node.TokPipe != null && _result == node.Args && (node.TokOpen == null || _lim <= node.TokOpen.Range.Min))
            {
                _result = node;
                return;
            }

            base.PostVisitImpl(node);
        }

        protected override void PostVisitCore(RexlNode node)
        {
            VisitCore(node);
        }
    }

    private Dictionary<RexlNode, RexlNode> EnsureParMap()
    {
        if (_toPar == null)
            Interlocked.CompareExchange(ref _toPar, ParentMapBuilder.Run(ParseTree), null);
        return _toPar;
    }

    /// <summary>
    /// Returns the parent of the given <paramref name="node"/> within the <see cref="ParseTree"/>.
    /// Throws if the given <paramref name="node"/> is not in the <see cref="ParseTree"/>. Returns
    /// <c>null</c> if <paramref name="node"/> is equal to <see cref="ParseTree"/>.
    /// </summary>
    public RexlNode GetParent(RexlNode node)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.BugCheckParam(EnsureParMap().TryGetValue(node, out var res), nameof(node));
        return res;
    }

    /// <summary>
    /// Returns whether the given <paramref name="node"/> is within the <see cref="ParseTree"/>.
    /// </summary>
    public bool InTree(RexlNode node)
    {
        Validation.BugCheckValue(node, nameof(node));
        return EnsureParMap().ContainsKey(node);
    }

    /// <summary>
    /// A visitor to build the parent map.
    /// </summary>
    private sealed class ParentMapBuilder : NoopTreeVisitor
    {
        private readonly Stack<RexlNode> _stack;
        private readonly Dictionary<RexlNode, RexlNode> _map;

        public ParentMapBuilder()
        {
            _stack = new Stack<RexlNode>();
            // Pushes a null RexlNode, as the parent to root.
            _stack.Push(null);
            _map = new Dictionary<RexlNode, RexlNode>();
        }

        public static Dictionary<RexlNode, RexlNode> Run(RexlNode tree)
        {
            Validation.AssertValue(tree);
            var bldr = new ParentMapBuilder();
            Validation.Assert(bldr._stack.Count == 1 && bldr._stack.Peek() == null);
            tree.Accept(bldr);
            return bldr._map;
        }

        protected override void EnterCore(RexlNode node)
        {
            base.EnterCore(node);

            Validation.Assert(!_map.ContainsKey(node), "Parent previously set!");
            _map.Add(node, PeekSrc());
        }
    }
}

/// <summary>
/// A rexl formula script.
/// </summary>
public sealed class RexlFormula : RexlScript
{
    /// <summary>
    /// The ParseTree.
    /// </summary>
    public new ExprNode ParseTree { get; }

    private RexlFormula(
            ExprNode tree,
            TokenStream tokens, Immutable.Array<Token> toksFiltered, ReadOnly.HashSet<int> useAlt,
            Immutable.Array<RexlDiagnostic> diagnostics)
        : base(tokens.VerifyValue().Source.RangeAll, tree, tokens, toksFiltered, useAlt, diagnostics)
    {
        ParseTree = tree;
    }

    private RexlFormula(
            SourceRange rng, ExprNode tree,
            TokenStream tokens, Immutable.Array<Token> toksFiltered, ReadOnly.HashSet<int> useAlt,
            Immutable.Array<RexlDiagnostic> diagnostics)
        : base(rng, tree, tokens, toksFiltered, useAlt, diagnostics)
    {
        ParseTree = tree;
    }

    /// <summary>
    /// Parse the given <paramref name="source"/> as a <see cref="ExprNode"/> and encapsulate
    /// the results, including parse tree, tokens, and errors.
    /// </summary>
    public static RexlFormula Create(SourceContext source)
    {
        Validation.BugCheckValue(source, nameof(source));
        var psr = Parser.ParseExpr(source);
        return new RexlFormula(
            psr.ParseTree.Cast<ExprNode>(),
            psr.Tokens, psr.FilteredTokens, psr.UseAlt, psr.GetDiagnostics());
    }

    /// <summary>
    /// This is typically used to get a <see cref="RexlFormula"/> for an <see cref="ExprNode"/> that is
    /// part of a <see cref="RexlStmtList"/>.
    /// </summary>
    public static RexlFormula CreateSubFormula(RexlScript rs, ExprNode tree)
    {
        Validation.BugCheckValue(rs, nameof(rs));
        Validation.BugCheckValue(tree, nameof(tree));
        Validation.BugCheckParam(rs.InTree(tree), nameof(tree));

        if (tree == rs.ParseTree)
        {
            Validation.Assert(rs is RexlFormula);
            return (RexlFormula)rs;
        }

        var rng = tree.GetFullRange();
        return new RexlFormula(rng, tree, rs.Tokens, rs.FilteredTokens, rs.UseAlt, rs.Diagnostics);
    }
}
