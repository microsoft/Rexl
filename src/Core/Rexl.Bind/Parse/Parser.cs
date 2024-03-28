// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Parse;

using IdentTuple = Immutable.Array<Identifier>;
using SliceItemTuple = Immutable.Array<SliceItemNode>;
using TokTuple = Immutable.Array<Token>;

public abstract partial class RexlScript
{
    /// <summary>
    /// Thrown when we hit a critical error, like nesting too deep. Note that the
    /// parser catches and processes this exception.
    /// REVIEW: Integrate with the diagnostic stuff.
    /// </summary>
    private sealed class RexlException : ApplicationException
    {
        /// <summary>
        /// The error node.
        /// </summary>
        public ErrorNode Error { get; }

        /// <summary>
        /// Context for the error. May be null.
        /// </summary>
        public ExprNode Context { get; }

        internal RexlException(ErrorNode err, ExprNode ctx)
        {
            Validation.AssertValue(err);
            Validation.AssertValueOrNull(ctx);

            Error = err;
            Context = ctx;
        }

        public override string ToString()
        {
            return string.Format(
                StringId.Culture,
                "{0} Tok: {1}, {Message: {3}",
                Context?.GetFullRange() ?? Error.GetFullRange(), Error.Token, Error.Format());
        }
    }

    /// <summary>
    /// Research Canvas expression language parser.
    /// Note: logically, this should "have a" <see cref="TokenCursor"/> rather than "be a"
    /// <see cref="TokenCursor"/>, but it's simply more efficient to "be a". If this class
    /// ever needs to be more broadly visible, this should change, or the members of
    /// <see cref="TokenCursor"/> should be made protected rather than public.
    /// </summary>
    private protected sealed class Parser : TokenCursor
    {
        /// <summary>
        /// This increments/decrements the stack depth.
        /// </summary>
        private struct NestContext : IDisposable
        {
            private Parser _psr;
#if DEBUG
            private readonly int _stackDepthPrev;
#endif
            private readonly bool _inTaskPrev;

            public NestContext(Parser psr, bool forTask = false)
            {
                Validation.AssertValue(psr);
                _psr = psr;
#if DEBUG
                _stackDepthPrev = _psr._stackDepth;
#endif
                _psr._stackDepth++;
                _inTaskPrev = _psr._inTask;
                if (forTask)
                    _psr._inTask = true;
            }

            public void Dispose()
            {
                if (_psr != null)
                {
#if DEBUG
                    Validation.Assert(_psr._stackDepth == _stackDepthPrev + 1);
#endif
                    _psr._stackDepth--;
                    _psr._inTask = _inTaskPrev;
                    _psr = null;
                }
            }
        }

        /// <summary>
        /// Gets the tokens produced by the Lexer, including comments.
        /// </summary>
        public TokenStream Tokens { get; }

        /// <summary>
        /// Gets the tokens that are used for parsing.
        /// Comments are removed from the tokens emmitted by Lexer.
        /// </summary>
        public TokTuple FilteredTokens { get; }

        private RexlNode _tree;

        /// <summary>
        /// Gets the parse tree.
        /// </summary>
        public RexlNode ParseTree
        {
            get { return _tree; }
            private set
            {
                Validation.Assert(_tree == null);
                _tree = value;
            }
        }

        private HashSet<int> _useAlt;
        public ReadOnly.HashSet<int> UseAlt => _useAlt;

        private List<RexlDiagnostic> _diags;

        // Nodes are assigned an integer id that is used to index into arrays later.
        private int _idNext;

        // Track the parsing stack depth and enforce a maximum, to avoid excessive recursion.
        // Note that this is not the same as TextNode.Depth, which grows from leaf to root, while
        // stack depth grows from root to leaf.
        private const int _maxStackDepth = 50;
        private int _stackDepth;

        // Whether we're parsing statements in a task definition.
        private bool _inTask;

        private Parser(TokenStream tokens, TokTuple toksFiltered)
            : base(toksFiltered)
        {
            Validation.AssertValue(tokens);

            Tokens = tokens;
            FilteredTokens = toksFiltered;
        }

        private static Parser Create(TokenStream tokens)
        {
            // Filter out comments before creating the cursor/parser.
            TokTuple.Builder bldr = null;
            int itokDst = 0;
            for (int itok = 0; itok < tokens.Length; itok++)
            {
                Validation.Assert(itokDst <= itok);
                var tok = tokens[itok];
                if (tok is CommentToken)
                {
                    if (bldr == null)
                        bldr = tokens.Tokens.ToBuilder();
                }
                else
                {
                    Validation.Assert((bldr != null) == (itokDst < itok));
                    if (bldr != null)
                        bldr[itokDst] = tok;
                    itokDst++;
                }
            }
            Validation.AssertIndexInclusive(itokDst, tokens.Length);
            Validation.Assert((itokDst < tokens.Length) == (bldr != null));

            TokTuple filtered;
            if (bldr == null)
                filtered = tokens.Tokens;
            else
            {
                Validation.Assert(bldr.Count == tokens.Length);
                bldr.RemoveTail(itokDst);
                filtered = bldr.ToImmutable();
            }

            return new Parser(tokens, filtered);
        }

        protected override void Advance()
        {
            if (TokCur.HasDiagnostics)
            {
                // Report token level diagnostics.
                switch (TokCur)
                {
                case FixedToken ft:
                    if (ft.Deprecated && !CurHasError())
                        PostTokDeprecation(ft.Std);
                    break;
                case TextLitToken txt:
                    if ((txt.Flags & TextLitFlags.BadEscape) != 0)
                        PostError(ErrorStrings.ErrBadTextEscape);
                    if ((txt.Flags & TextLitFlags.Unterminated) != 0)
                        PostError(ErrorStrings.ErrUnterminatedText);
                    break;
                case IntLitToken ilt:
                    if ((ilt.Flags & IntLitFlags.Error) != 0)
                        PostError(ErrorStrings.ErrBadNumLiteral);
                    break;
                case FltLitToken flt:
                    if (flt.HasError)
                        PostError(ErrorStrings.ErrBadNumLiteral);
                    break;
                case IdentToken id:
                    if (id.HasQuoteOpen && !id.HasQuoteClose)
                        PostError(ErrorStrings.ErrUnterminatedQuotedIdentifier);
                    else if (id.HasAnyFlags(IdentFlags.Modified))
                        PostError(ErrorStrings.ErrEmptyInvalidIdentifier);
                    break;
                }
            }

            base.Advance();
        }

        /// <summary>
        /// Returns any parsing errors as an array, possibly empty.
        /// </summary>
        public Immutable.Array<RexlDiagnostic> GetDiagnostics()
        {
            if (Util.Size(_diags) == 0)
                return Immutable.Array<RexlDiagnostic>.Empty;
            return _diags.ToImmutableArray();
        }

        /// <summary>
        /// Parses the script as a statement list.
        /// </summary>
        public static Parser ParseStmtList(SourceContext source)
        {
            Validation.BugCheckValue(source, nameof(source));

            var tokens = RexlLexer.Instance.LexSource(source);
            var psr = Parser.Create(tokens);
            psr.ParseStmtListCore();

            return psr;
        }

        /// <summary>
        /// Core functionality for top-level ParseStmtList entry points.
        /// </summary>
        private void ParseStmtListCore()
        {
            Validation.Assert(_stackDepth == 0);
            int cerrInit = _diags.Size();

            StmtListNode res;
            try
            {
                res = ParseStmtList();
                Validation.Assert(_stackDepth == 0);
                Validation.Assert(TidCur == TokKind.Eof);
            }
            catch (RexlException ex)
            {
                // We should have already posted an error.
                Validation.Assert(_diags.Size() > cerrInit);
                res = new StmtListNode(ref _idNext, ex.Error.Token,
                    Immutable.Array.Create<StmtNode>(new ExprStmtNode(ref _idNext, ex.Error)));
            }
            Validation.Assert(_stackDepth == 0);

            // Record the parse tree.
            ParseTree = res;
        }

        /// <summary>
        /// Parses a statement list.
        /// </summary>
        private StmtListNode ParseStmtList(TokKind tidStop = TokKind.None, bool forTask = false)
        {
            using (new NestContext(this, forTask))
            {
                var stmts = Immutable.Array.CreateBuilder<StmtNode>();

                Token tokOpen = null;
                Token tokRes = null;

                // If the first token is a semi, record it as tokOpen.
                if (TidCur == TokKind.Semi)
                    tokOpen = TokCur;

                Token tokSemi;
                bool needSemi = false;
                for (; ; )
                {
                    // Eat extra semi-colons without complaint, tracking the last one.
                    tokSemi = null;
                    while (TidCur == TokKind.Semi)
                        tokSemi = TokMove(TokKind.Semi);

                    if (TidCur == TokKind.Eof)
                        break;
                    if (TidCur == tidStop)
                        break;

                    if (stmts.Count == 0)
                        tokRes = TokCur;
                    else if (tokSemi == null)
                    {
                        // We didn't see a semicolon. If the previous statement needs a semi,
                        // report the error.
                        if (needSemi)
                            TokEat(TokKind.Semi);
                    }
                    else if (stmts.Count == 1)
                        tokRes = tokSemi;

                    int itok = ItokCur;
                    var stmt = ParseStmt();
                    needSemi = stmt.NeedsSemi;
                    stmts.Add(stmt);
                    Validation.Assert(itok <= ItokCur);
                    if (itok == ItokCur)
                        TidSkip();
                }

                return new StmtListNode(ref _idNext, tokRes ?? TokCur, stmts.ToImmutable(), tokOpen, tokSemi);
            }
        }

        /// <summary>
        /// Parses a nested statement, for example the then or else statement of <c>if</c>.
        /// Such a statement can be a non-label normal statement or a block statement.
        /// </summary>
        private StmtNode ParseNestedStatement()
        {
            while (TidCur == TokKind.Ident && TidPeek() == TokKind.Colon)
            {
                PostError(ErrorStrings.ErrBadLabel);
                TidSkip(TokKind.Ident);
                TidSkip(TokKind.Colon);
            }

            if (TidCur == TokKind.CurlyOpen)
                return ParseBlock();
            return ParseStmt();
        }

        /// <summary>
        /// Parses a single (non-block) statement, including a label statement.
        /// </summary>
        private StmtNode ParseStmt()
        {
            // Handle non-expr statement forms.
            switch (TidCur)
            {
            case TokKind.KwdImport:
            case TokKind.KwdExecute:
                {
                    var tok = TokMoveRaw();
                    var val = ParseExpr(Precedence.Expr);
                    NamespaceSpec nss = null;
                    if (TidCur == TokKind.KwdIn && TidPeek() == TokKind.KwdNamespace)
                    {
                        TidSkip(TokKind.KwdIn);
                        nss = ParseNamespaceSpec();
                    }
                    return CmdStmtNode.Create(ref _idNext, tok, val, nss);
                }

            case TokKind.KwdNamespace:
                return ParseNamespaceStmt();

            case TokKind.KwdWith:
                return ParseWithStmt();

            case TokKind.KwdGoto:
                return ParseGotoStmt();
            case TokKind.KwdIf:
                return ParseIfStmt();
            case TokKind.KwdWhile:
                return ParseWhileStmt();

            case TokKind.At:
                {
                    // See if it looks like a definition statement.
                    IdentPath idents;
                    if (TryParseIdentPath(TokKind.ColEqu, out idents))
                    {
                        return new DefinitionStmtNode(ref _idNext, TokMove(TokKind.ColEqu),
                            idents, ParseExpr(Precedence.Expr));
                    }
                }
                break;

            case TokKind.Ident:
                {
                    var tok = TokCur;
                    var tid = TidCurAlt;

                    // Handle contextual keywords.
                    IdentPath idents;
                    switch (tid)
                    {
                    case TokKind.KtxFunc:
                    case TokKind.KtxProp:
                        if (TryParseIdentPath(TokKind.ParenOpen, out idents, 1))
                        {
                            Validation.Assert(TidCur == TokKind.ParenOpen);
                            return new FuncStmtNode(ref _idNext,
                                tok.TokenAlt, idents,
                                TokMove(TokKind.ParenOpen),
                                ParseFuncParamNames(),
                                TokEat(TokKind.ParenClose),
                                TokEat(TokKind.ColEqu),
                                ParseExpr(Precedence.Expr));
                        }
                        break;

                    case TokKind.KtxProc:
                        if (TryParseIdentPath(TokKind.ParenOpen, out idents, 1))
                        {
                            Validation.Assert(TidCur == TokKind.ParenOpen);
                            var tokOpen = TokMove(TokKind.ParenOpen);
                            var names = ParseFuncParamNames();
                            var tokClose = TokEat(TokKind.ParenClose);
                            Token tokPrime = null;
                            BlockStmtNode prime = null;
                            if (TidCurAlt == TokKind.KtxPrime)
                            {
                                tokPrime = TokEatKwd(TokKind.KtxPrime);
                                prime = ParseBlock(forTask: true);
                            }
                            return new UserProcStmtNode(ref _idNext,
                                tok.TokenAlt, idents,
                                tokOpen, names, tokClose, tokPrime, prime,
                                TokEatKwd(TokKind.KtxPlay, TokKind.KwdAs), ParseBlock(forTask: true));
                        }
                        break;

                    case TokKind.KtxTask:
                    case TokKind.KtxPrime:
                    case TokKind.KtxPlay:
                    case TokKind.KtxPause:
                    case TokKind.KtxPoke:
                    case TokKind.KtxPoll:
                    case TokKind.KtxFinish:
                    case TokKind.KtxAbort:
                        Validation.Assert(tid.IsTaskModifier());
                        if (TryParseIdentPath(TokKind.None, out idents, 1))
                        {
                            if (TidCur == TokKind.ParenOpen)
                                return new TaskProcStmtNode(ref _idNext, tok.TokenAlt, ParseInvocation(idents));
                            return ParseTask(tok.TokenAlt, idents);
                        }
                        break;

                    case TokKind.KtxPublish:
                    case TokKind.KtxPrimary:
                    case TokKind.KtxStream:
                        if (_inTask && TryParseIdentPath(TokKind.ColEqu, out idents, 1))
                        {
                            return new DefinitionStmtNode(ref _idNext, TokMove(TokKind.ColEqu), idents,
                                ParseExpr(Precedence.Expr), tok.TokenAlt);
                        }
                        break;
                    }

                    // Handle a label.
                    if (TidPeek() == TokKind.Colon)
                    {
                        var ident = ParseIdentifier();
                        return new LabelStmtNode(ref _idNext, TokMove(TokKind.Colon), ident);
                    }

                    // See if it looks like a definition statement.
                    if (TryParseIdentPath(TokKind.ColEqu, out idents))
                    {
                        return new DefinitionStmtNode(ref _idNext, TokMove(TokKind.ColEqu),
                            idents, ParseExpr(Precedence.Expr));
                    }
                }
                break;

            case TokKind.KwdThis:
                if (TidPeek() == TokKind.ColEqu)
                {
                    // A form of ExprStmtNode.
                    var tokThis = TokMoveKwd(TokKind.KwdThis);
                    return new DefinitionStmtNode(ref _idNext, TokMove(TokKind.ColEqu), null,
                        ParseExpr(Precedence.Expr), tokThis);
                }
                break;

            case TokKind.ColEqu:
                {
                    // Missing ident path.
                    var idents = ParseIdentPath();
                    return new DefinitionStmtNode(ref _idNext,
                        TokMove(TokKind.ColEqu), idents, ParseExpr(Precedence.Expr));
                }

            case TokKind.Eof:
                // Will generate an error in ParseExprOrAction.
                break;
            }

            return new ExprStmtNode(ref _idNext, ParseExpr(Precedence.Expr));
        }

        /// <summary>
        /// Parses a block statement, which is statements surrounded by curly braces.
        /// </summary>
        private BlockStmtNode ParseBlock(bool forTask = false)
        {
            return new BlockStmtNode(ref _idNext, TokEat(TokKind.CurlyOpen) ?? TokCur,
                ParseStmtList(TokKind.CurlyClose, forTask), TokEat(TokKind.CurlyClose));
        }

        /// <summary>
        /// Parse a namespace specification.
        /// </summary>
        private NamespaceSpec ParseNamespaceSpec()
        {
            Validation.Assert(TidCur == TokKind.KwdNamespace);

            var tok = TokMoveKwd(TokKind.KwdNamespace);

            if (TidCur == TokKind.Ident || TidCur == TokKind.At && TidPeek() == TokKind.Ident)
            {
                // With an ident path.
                return new NamespaceSpec(tok, ParseIdentPath());
            }

            if (TidCur == TokKind.At)
                return new NamespaceSpec(tok, TokMove(TokKind.At));

            return new NamespaceSpec(tok);
        }

        /// <summary>
        /// Parse a namespace statement.
        /// </summary>
        private NamespaceStmtNode ParseNamespaceStmt()
        {
            Validation.Assert(TidCur == TokKind.KwdNamespace);

            var spec = ParseNamespaceSpec();

            if (TidCur != TokKind.CurlyOpen)
                return new NamespaceStmtNode(ref _idNext, spec);

            return new NamespaceStmtNode(ref _idNext, spec, ParseBlock());
        }

        /// <summary>
        /// Parse a with statement. There are two forms: "with paths" and "with paths { stmts }", where
        /// "paths" is a comma separated list of ident paths and "stmts" is a list of statements.
        /// </summary>
        private WithStmtNode ParseWithStmt()
        {
            Validation.Assert(TidCur == TokKind.KwdWith);

            var tok = TokMoveKwd(TokKind.KwdWith);
            var bldr = Immutable.Array<IdentPath>.CreateBuilder();
            for (; ; )
            {
                bldr.Add(ParseIdentPath());
                if (TidCur != TokKind.Comma)
                    break;
                TidSkip();
            }
            var paths = bldr.ToImmutable();

            if (TidCur != TokKind.CurlyOpen)
                return new WithStmtNode(ref _idNext, tok, paths);

            return new WithStmtNode(ref _idNext, tok, paths, ParseBlock());
        }

        /// <summary>
        /// Parse a <c>goto</c> statement.
        /// </summary>
        private GotoStmtNode ParseGotoStmt()
        {
            Validation.Assert(TidCur == TokKind.KwdGoto);

            return new GotoStmtNode(ref _idNext, TokMoveKwd(TokKind.KwdGoto), ParseIdentifier());
        }

        /// <summary>
        /// Parses what should be a parenthesized expression, for example the condition for <c>if</c>
        /// and <c>while</c> statements. If there is no leading paren, generates an error and parses
        /// an expression.
        /// </summary>
        private ExprNode ParseParenExpr()
        {
            if (TidCur == TokKind.ParenOpen)
                return ParenNode.Wrap(ref _idNext, TokMove(TokKind.ParenOpen), ParseExpr(Precedence.Expr), TokEat(TokKind.ParenClose));

            // Generate an error.
            TokEat(TokKind.ParenOpen);
            return ParseExpr(Precedence.Expr);
        }

        /// <summary>
        /// Parse an <c>if</c> statement.
        /// </summary>
        private IfStmtNode ParseIfStmt()
        {
            Validation.Assert(TidCur == TokKind.KwdIf);

            var tok = TokMoveKwd(TokKind.KwdIf);

            ExprNode cond = ParseParenExpr();
            StmtNode stmtThen = ParseNestedStatement();

            // Semi-colon is allowed regardless of whether the "then" statement is a block.
            Token tokSemi = null;
            if (TidCur == TokKind.Semi && TidPeek() == TokKind.KwdElse)
                tokSemi = TokMove(TokKind.Semi);

            Token tokElse = null;
            StmtNode stmtElse = null;
            if (TidCur == TokKind.KwdElse)
            {
                if (tokSemi == null && stmtThen is not BlockStmtNode)
                {
                    // Semi-colon is required. This generates the error.
                    TokEat(TokKind.Semi);
                }

                tokElse = TokMoveKwd(TokKind.KwdElse);
                stmtElse = ParseNestedStatement();
            }

            return new IfStmtNode(ref _idNext, tok, cond, stmtThen, tokSemi, tokElse, stmtElse);
        }

        /// <summary>
        /// Parse a <c>while</c> statement.
        /// </summary>
        private WhileStmtNode ParseWhileStmt()
        {
            Validation.Assert(TidCur == TokKind.KwdWhile);
            return new WhileStmtNode(ref _idNext, TokMoveKwd(TokKind.KwdWhile),
                ParseParenExpr(), ParseNestedStatement());
        }

        private IdentTuple ParseFuncParamNames()
        {
            switch (TidCur)
            {
            case TokKind.Eof:
            case TokKind.ParenClose:
            case TokKind.ColEqu:
                return IdentTuple.Empty;
            }

            var bldr = IdentTuple.CreateBuilder();
            for (; ; )
            {
                // Check for duplicates.
                if (TokCur is IdentToken id && !id.HasErrors)
                {
                    for (int i = 0; i < bldr.Count; i++)
                    {
                        if (bldr[i].Name == id.Name)
                        {
                            PostError(ErrorStrings.ErrDuplicateParamName_Name, id.Name.ToString());
                            break;
                        }
                    }
                }

                bldr.Add(ParseIdentifier());
                if (TidCur != TokKind.Comma)
                    break;
                TokMove(TokKind.Comma);
            }
            return bldr.ToImmutable();
        }

        private bool TryParseIdentifier(TokKind tidNext, out Identifier ident, int itokPeek = 0)
        {
            Validation.Assert(itokPeek >= 0);

            // See if it looks like a possibly compound name, followed by tidNext.
            // Note that @ and _ tokens may cause errors in ParseIdentifier, but we want our "best guess"
            // to be un-influenced by them.
            int itok = PosFromIdent(itokPeek);
            if (itok <= itokPeek)
            {
                ident = default;
                return false;
            }

            if (tidNext != TokKind.None && TidPeek(itok) != tidNext)
            {
                ident = default;
                return false;
            }

            for (int i = 0; i < itokPeek; i++)
            {
                // Assume the alt form is being used. If it is fuzzy, generate an error.
                TokMoveAlt();
            }

            ident = ParseIdentifier();
            return true;
        }

        private bool TryParseIdentPath(TokKind tidNext, out IdentPath idents, int itokPeek = 0)
        {
            Validation.Assert(itokPeek >= 0);

            // See if it looks like a possibly compound name, followed by tidNext.
            // Note that @ and _ tokens may cause errors in ParseIdentifier, but we want our "best guess"
            // to be un-influenced by them.
            int itok = PosFromIdent(itokPeek);
            if (itok <= itokPeek)
            {
                idents = default;
                return false;
            }

            int c = 1;
            TokKind cur;
            while ((cur = TidPeek(itok)) == TokKind.Dot || cur == TokKind.Bng)
            {
                c++;
                itok = PosFromIdent(itok + 1);
            }

            if (tidNext != TokKind.None && TidPeek(itok) != tidNext)
            {
                idents = default;
                return false;
            }

            for (int i = 0; i < itokPeek; i++)
            {
                // Assume the alt form is being used. If it is fuzzy, generate an error.
                TokMoveAlt();
            }

            idents = ParseIdentPath(c);

            // Not a big deal if this assert goes off - just means our capacity for idents was off.
            // Should fix it though (and understand why).
            Validation.Assert(idents.Length == c);
            return true;
        }

        private IdentPath ParseIdentPath(int cap = 1)
        {
            var bldr = Immutable.Array.CreateBuilder<Identifier>(cap);
            bldr.Add(ParseIdentifier(allowRooted: true));
            while (TidCur == TokKind.Dot || TidCur == TokKind.Bng)
            {
                if (TidCur != TokKind.Dot)
                    PostBinDeprecation(".");
                TidSkip();
                bldr.Add(ParseIdentifier());
            }
            return new IdentPath(bldr.ToImmutable());
        }

        private StmtNode ParseTask(Token tokTask, IdentPath idents)
        {
            Validation.AssertValue(tokTask);
            Validation.Assert(tokTask.Kind.IsTaskModifier());
            Validation.Assert(idents.Length > 0);

            var tok = TokCur;
            switch (TidCurAlt)
            {
            case TokKind.KwdWith:
            case TokKind.KtxPrime:
            case TokKind.KtxPlay:
            case TokKind.CurlyOpen:
                return ParseInlineTask(tokTask, idents);
            case TokKind.KwdAs:
                if (TidPeek() == TokKind.CurlyOpen)
                    return ParseInlineTask(tokTask, idents);
                break;
            default:
                if (tokTask.Kind != TokKind.KtxTask)
                {
                    // Some command (play, pause, finish, etc) followed by a task name.
                    return new TaskCmdStmtNode(ref _idNext, tokTask, idents);
                }
                break;
            }

            Token tokAs = TokEatKwd(TokKind.KwdAs);
            ExprNode value;
            if (TryParseIdentPath(TokKind.ParenOpen, out var identsCall, 0))
                value = ParseInvocation(identsCall);
            else
            {
                var err = PostError(ErrorStrings.ErrProcCallExpected);
                value = new ErrorNode(ref _idNext, tokAs ?? idents.Last.Token, err);
            }
            return new TaskProcStmtNode(ref _idNext, tokTask, idents, tokAs, value);
        }

        private StmtNode ParseInlineTask(Token tokTask, IdentPath idents)
        {
            Validation.AssertValue(tokTask);
            Validation.Assert(tokTask.Kind.IsTaskModifier());
            Validation.Assert(idents.Length > 0);

            Token tokWith = null;
            ExprNode with = null;
            if (TidCur == TokKind.KwdWith)
            {
                tokWith = TokMoveKwd(TokKind.KwdWith);
                with = ParseRecOrParen();
            }

            Token tokPrime = null;
            BlockStmtNode prime = null;
            if (TidCurAlt == TokKind.KtxPrime)
            {
                tokPrime = TokMoveAlt(TokKind.KtxPrime);
                prime = ParseBlock(forTask: true);
            }

            return new TaskBlockStmtNode(ref _idNext, tokTask, idents,
                tokWith, with, tokPrime, prime, TokEatKwd(TokKind.KtxPlay, TokKind.KwdAs),
                ParseBlock(forTask: true));
        }

        /// <summary>
        /// Parses the contents of a module.
        /// </summary>
        private SymListNode ParseModuleSyms()
        {
            using var nest = new NestContext(this);
            var syms = Immutable.Array.CreateBuilder<SymbolDeclNode>();

            Token tokRes = null;
            for (; ; )
            {
                // Eat extra semi-colons without complaint.
                Token tokSemi = null;
                while (TidCur == TokKind.Semi)
                    tokSemi = TokMove(TokKind.Semi);

                if (TidCur == TokKind.Eof)
                    break;
                if (TidCur == TokKind.CurlyClose)
                    break;

                if (syms.Count == 0)
                    tokRes = TokCur;
                else if (tokSemi == null)
                {
                    if (TidCur != TokKind.Ident && TidCur != TokKind.At)
                        break;
                    TokEat(TokKind.Semi);
                }
                else if (syms.Count == 1)
                    tokRes = tokSemi;

                int itok = ItokCur;
                var sym = ParseModuleSymbol();
                Validation.Assert(itok <= ItokCur);
                if (itok == ItokCur)
                    break;
                syms.Add(sym);
            }

            return new SymListNode(ref _idNext, tokRes ?? TokCur, syms.ToImmutable());
        }

        /// <summary>
        /// Parses a single symbol declaration in a module.
        /// </summary>
        private SymbolDeclNode ParseModuleSymbol()
        {
            // Caller should take care of eof.
            Validation.Assert(TidCur != TokKind.Eof);

            Token tokKind = null;
            TokKind tidPeek;
            ModSymKind sk;
            if (TidCur == TokKind.Ident && ((tidPeek = TidPeek()) == TokKind.Ident || tidPeek == TokKind.At))
            {
                // Digest definition kind, if there is one.
                switch (TidCurAlt)
                {
                case TokKind.KtxVar:
                    return ParseFreeVarDecl();

                case TokKind.KtxParam: sk = ModSymKind.Parameter; tokKind = TokMoveAlt(); break;
                case TokKind.KtxConst: sk = ModSymKind.Constant; tokKind = TokMoveAlt(); break;
                case TokKind.KtxLet: sk = ModSymKind.Let; tokKind = TokMoveAlt(); break;
                case TokKind.KtxMsr: sk = ModSymKind.Measure; tokKind = TokMoveAlt(); break;
                case TokKind.KtxCon: sk = ModSymKind.Constraint; tokKind = TokMoveAlt(); break;

                default:
                    sk = ModSymKind.Let;
                    break;
                }
            }
            else
            {
                sk = ModSymKind.Let;
                tokKind = null;
            }

            var ident = ParseIdentifier();

            // Parameter allows either "default" or ":=".
            Token tok;
            if (tokKind?.Kind == TokKind.KtxParam && TidCurAlt == TokKind.KtxDef)
                tok = TokMoveAlt(TokKind.KtxDef);
            else
                tok = TokEat(TokKind.ColEqu) ?? tokKind ?? ident.Token;

            var expr = ParseExpr(Precedence.Expr);
            return new ValueSymDeclNode(ref _idNext, tok, tokKind, sk, ident, expr);
        }

        private FreeVarDeclNode ParseFreeVarDecl()
        {
            Validation.Assert(TidCurAlt == TokKind.KtxVar);

            Token tokKind = TokMoveAlt(TokKind.KtxVar);
            Token tok = tokKind;
            var ident = ParseIdentifier();

            Token tokIn = null, tokFr = null, tokTo = null, tokDef = null;
            ExprNode valIn = null, valFr = null, valTo = null, valDef = null;

            void DoOne(ref Token tokCur, ref ExprNode valCur, Token tokConflict)
            {
                Validation.Assert((tokCur == null) == (valCur == null));
                bool bad = false;
                if (tokConflict is not null)
                {
                    PostError(ErrorStrings.ErrModuleFreeConflict_Kwd_Kwd,
                        tokConflict.GetStdString(),
                        TokCur.TokenAlt.GetStdString());
                    bad = true;
                }
                else if (tokCur != null)
                {
                    PostError(ErrorStrings.ErrModuleDuplicateVarDomainClause);
                    bad = true;
                }

                if (bad)
                {
                    TokMoveAlt();
                    ParseExpr(Precedence.Concat);
                }
                else
                {
                    tokCur = TokMoveAlt();
                    valCur = ParseExpr(Precedence.Concat);
                }
            }

            int count = 0;
            for (; ; )
            {
                count++;
                switch (TidCurAlt)
                {
                case TokKind.KwdIn: DoOne(ref tokIn, ref valIn, tokFr ?? tokTo); continue;
                case TokKind.KwdFrom: DoOne(ref tokFr, ref valFr, tokIn); continue;
                case TokKind.KwdTo: DoOne(ref tokTo, ref valTo, tokIn); continue;
                case TokKind.KtxDef: DoOne(ref tokDef, ref valDef, null); continue;
                }

                if (count == 1)
                {
                    // Assume := <default>
                    tokDef = TokEat(TokKind.ColEqu);
                    valDef = ParseExpr(Precedence.Concat);
                }
                break;
            }

            Token tokOptReq = null;
            var tid = TidCurAlt;
            switch (tid)
            {
            case TokKind.KtxReq:
            case TokKind.KtxOpt:
                tokOptReq = TokEatKwd(tid);
                break;
            }

            return new FreeVarDeclNode(
                ref _idNext, tok, tokKind, ident,
                tokIn, valIn,
                tokFr, valFr, tokTo, valTo,
                tokDef, valDef, tokOptReq);
        }

        /// <summary>
        /// Parse the script as an expression.
        /// </summary>
        public static Parser ParseExpr(SourceContext source)
        {
            Validation.BugCheckValue(source, nameof(source));

            var tokens = RexlLexer.Instance.LexSource(source);
            var psr = Parser.Create(tokens);
            psr.ParseExprCore();

            return psr;
        }

        private void ParseExprCore()
        {
            Validation.Assert(_stackDepth == 0);
            int cdiagInit = _diags.Size();

            ExprNode node;
            try
            {
                node = ParseExpr(Precedence.Error);
                Validation.Assert(_stackDepth == 0);
                if (TidCur != TokKind.Eof)
                    PostError(ErrorStrings.ErrBadToken);
            }
            catch (RexlException ex)
            {
                // We should have already posted an error.
                Validation.Assert(_diags.Size() > cdiagInit);
                node = ex.Error;
            }
            Validation.Assert(_stackDepth == 0);

            // Record the parse tree.
            ParseTree = node;
        }

        /// <summary>
        /// Parses the next (maximal) expression with precedence >= precMin.
        /// </summary>
        private ExprNode ParseExpr(Precedence precMin)
        {
            // ParseOperand may accept PrefixUnary and higher, so ParseExpr should never be called
            // with precMin > Precedence.PrefixUnary - it will not correctly handle such cases.
            Validation.Assert(Precedence.Error <= precMin & precMin <= Precedence.PrefixUnary);

            using (new NestContext(this))
            {
                ExprNode node = ParseOperand(precMin);

                // Process operators and right operands as long as the precedence bound is satisfied.
                for (; ; )
                {
                    Validation.AssertValue(node);

                    // Limit recursion depth to avoid stack overflow. Note that we do this here, and combine both
                    // _stackDepth and node.Depth to protect visitors from hitting stack overflow.
                    if (node.TreeDepth + _stackDepth > _maxStackDepth)
                        throw new RexlException(new ErrorNode(ref _idNext, TokCur, PostError(ErrorStrings.ErrRuleNestedTooDeeply)), node);

                    Token tok;
                    int num;
                    var tid = TidCurAlt;
                    switch (tid)
                    {
                    case TokKind.SquareOpen:
                        Validation.Assert(precMin <= Precedence.Primary);
                        node = ParseIndexing(node);
                        continue;

                    case TokKind.ParenOpen:
                        Validation.Assert(precMin <= Precedence.Primary);
                        if (node is DottedNameNode dotted && dotted.Root is FirstNameNode)
                        {
                            node = ParseInvocation(dotted);
                            continue;
                        }

                        // Since an invocation isn't legal, this looks like a value. Assume a missing binary operator.
                        goto case TokKind.Ident;

                    case TokKind.Dot:
                        Validation.Assert(precMin <= Precedence.Primary);
                        node = new DottedNameNode(ref _idNext, TokMove(TokKind.Dot), node, ParseIdentifier());
                        continue;

                    case TokKind.Dol:
                        Validation.Assert(precMin <= Precedence.Primary);
                        switch (PeekPastCompareMods(out num))
                        {
                        // REVIEW: Should support strict `in`. Perhaps when strict deals with null, also
                        // support strict `has`?
                        default:
                            if (num == 1)
                            {
                                // Meta property.
                                if (node is DottedNameNode dnn && dnn.Root is FirstNameNode)
                                    node = new MetaPropNode(ref _idNext, TokMove(TokKind.Dol), dnn.ToIdents(), ParseIdentifier());
                                else if (node is FirstNameNode fnn)
                                    node = new MetaPropNode(ref _idNext, TokMove(TokKind.Dol), IdentPath.Create(fnn.Ident), ParseIdentifier());
                                else
                                {
                                    PostErrorGuess(ErrorStrings.ErrBadMetaProp, ".", TokCur.Range);
                                    node = new DottedNameNode(ref _idNext, TokMove(TokKind.Dol), node, ParseIdentifier());
                                }
                                continue;
                            }
                            break;

                        case TokKind.Equ:
                        case TokKind.Lss:
                        case TokKind.LssEqu:
                        case TokKind.GrtEqu:
                        case TokKind.Grt:
                            break;
                        }

                        // Comparison.
                        if (precMin > Precedence.Compare)
                            return node;
                        node = ParseComparison(node);
                        continue;

                    case TokKind.Per:
                        Validation.Assert(precMin <= Precedence.PostfixUnary);
                        tok = TokMove(TokKind.Per);
                        node = new UnaryOpNode(ref _idNext, tok, UnaryOp.Percent, node);
                        continue;

                    case TokKind.MulMul:
                    case TokKind.Car:
                        Validation.Assert(precMin <= Precedence.Power);
                        if (tid != TokKind.Car)
                            PostBinDeprecation("^");
                        // Note that the right operand can include unary operators, both prefix (like -) and postfix (like %), as well as Power.
                        node = new BinaryOpNode(ref _idNext, TokMove(tid), BinaryOp.Power, node, ParseExpr(Precedence.PrefixUnary));
                        continue;

                    case TokKind.Bar:
                        if (precMin > Precedence.Pipe)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(tid), BinaryOp.Pipe, node, ParseExpr(Precedence.Pipe + 1));
                        continue;

                    case TokKind.SubGrt:
                    case TokKind.AddGrt:
                        Validation.Assert(precMin <= Precedence.Primary);
                        {
                            bool isConcat = tid == TokKind.AddGrt;
                            switch (TidPeek())
                            {
                            case TokKind.CurlyOpen:
                                Validation.Assert(precMin <= Precedence.Primary);
                                tok = TokMove(tid);
                                node = new RecordProjectionNode(ref _idNext, tok, node, isConcat,
                                    ParseRecordExpr(TokMove(TokKind.CurlyOpen)));
                                continue;
                            case TokKind.ParenOpen:
                                Validation.Assert(precMin <= Precedence.Primary);
                                tok = TokMove(tid);
                                var value = isConcat ? ParseTupleExpr() : ParseTupleOrParenExpr();
                                if (value is TupleNode tup)
                                    node = new TupleProjectionNode(ref _idNext, tok, node, isConcat, tup);
                                else
                                    node = new ValueProjectionNode(ref _idNext, tok, node, value);
                                continue;
                            case TokKind.Amp:
                                Validation.Assert(precMin <= Precedence.Primary);
                                if (isConcat)
                                    ErrorTid(TokKind.SubGrt);
                                tok = TokMove(tid);
                                isConcat = true;
                                Validation.Assert(TidCur == TokKind.Amp);

                                switch (TidPeek())
                                {
                                case TokKind.CurlyOpen:
                                    TidSkip(TokKind.Amp);
                                    node = new RecordProjectionNode(ref _idNext, tok, node, isConcat,
                                        ParseRecordExpr(TokMove(TokKind.CurlyOpen)));
                                    continue;

                                case TokKind.ParenOpen:
                                    TidSkip(TokKind.Amp);
                                    node = new TupleProjectionNode(ref _idNext, tok, node, isConcat, ParseTupleExpr());
                                    continue;
                                }

                                PostError(ErrorStrings.ErrBadToken);
                                TidSkip(TokKind.Amp);
                                node = ParsePipeCall(tok, node);
                                continue;
                            }

                            if (isConcat)
                                ErrorTid(TokKind.SubGrt);
                            node = ParsePipeCall(TokMove(tid), node);
                            continue;
                        }

                    case TokKind.Mul:
                        if (precMin > Precedence.Mul)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.Mul), BinaryOp.Mul, node, ParseExpr(Precedence.Mul + 1));
                        continue;
                    case TokKind.Div:
                        if (precMin > Precedence.Mul)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.Div), BinaryOp.Div, node, ParseExpr(Precedence.Mul + 1));
                        continue;
                    case TokKind.KtxDiv:
                        if (precMin > Precedence.Mul)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxDiv), BinaryOp.IntDiv, node, ParseExpr(Precedence.Mul + 1));
                        continue;
                    case TokKind.KtxMod:
                        if (precMin > Precedence.Mul)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxMod), BinaryOp.IntMod, node, ParseExpr(Precedence.Mul + 1));
                        continue;
                    case TokKind.Add:
                        if (precMin > Precedence.Add)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.Add), BinaryOp.Add, node, ParseExpr(Precedence.Add + 1));
                        continue;
                    case TokKind.Sub:
                        if (precMin > Precedence.Add)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.Sub), BinaryOp.Sub, node, ParseExpr(Precedence.Add + 1));
                        continue;

                    case TokKind.KtxBand:
                        if (precMin > Precedence.BitAnd)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxBand), BinaryOp.BitAnd, node, ParseExpr(Precedence.BitAnd + 1));
                        continue;
                    case TokKind.KtxBxor:
                        if (precMin > Precedence.BitXor)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxBxor), BinaryOp.BitXor, node, ParseExpr(Precedence.BitXor + 1));
                        continue;
                    case TokKind.KtxBor:
                        if (precMin > Precedence.BitOr)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxBor), BinaryOp.BitOr, node, ParseExpr(Precedence.BitOr + 1));
                        continue;
                    case TokKind.KtxMin:
                        if (precMin > Precedence.MinMax)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxMin), BinaryOp.Min, node, ParseExpr(Precedence.MinMax + 1));
                        continue;
                    case TokKind.KtxMax:
                        if (precMin > Precedence.MinMax)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(TokKind.KtxMax), BinaryOp.Max, node, ParseExpr(Precedence.MinMax + 1));
                        continue;
                    case TokKind.LssLss:
                    case TokKind.KtxShl:
                        if (precMin > Precedence.Shift)
                            return node;
                        if (tid != TokKind.KtxShl)
                            PostBinDeprecation("shl");
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.Shl, node, ParseExpr(Precedence.Shift + 1));
                        continue;
                    case TokKind.GrtGrt:
                    case TokKind.KtxShr:
                        if (precMin > Precedence.Shift)
                            return node;
                        if (tid != TokKind.KtxShr)
                            PostBinDeprecation("shr");
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.Shr, node, ParseExpr(Precedence.Shift + 1));
                        continue;
                    case TokKind.KtxShri:
                        if (precMin > Precedence.Shift)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.Shri, node, ParseExpr(Precedence.Shift + 1));
                        continue;
                    case TokKind.GrtGrtGrt:
                    case TokKind.KtxShru:
                        if (precMin > Precedence.Shift)
                            return node;
                        if (tid != TokKind.KtxShru)
                            PostBinDeprecation("shru");
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.Shru, node, ParseExpr(Precedence.Shift + 1));
                        continue;

                    case TokKind.Amp:
                        if (precMin > Precedence.Concat)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.Amp), BinaryOp.GenConcat, node, ParseExpr(Precedence.Concat + 1));
                        continue;
                    case TokKind.AddAdd:
                        if (precMin > Precedence.Concat)
                            return node;
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.AddAdd), BinaryOp.SeqConcat, node, ParseExpr(Precedence.Concat + 1));
                        continue;

                    // Comparison operators. Standard (non-deprecated) forms are = < <= >= > possibly prefixed
                    // by ~ ! not $ or any non-repeating combination of them.
                    case TokKind.Equ:
                    case TokKind.Lss:
                    case TokKind.LssEqu:
                    case TokKind.GrtEqu:
                    case TokKind.Grt:
                        if (precMin > Precedence.Compare)
                            return node;
                        node = ParseComparison(node);
                        continue;
                    case TokKind.KwdIn:
                        if (TidPeek() == TokKind.KwdNamespace)
                            return node;
                        if (precMin > Precedence.InHas)
                            return node;
                        node = ParseInHas(node);
                        continue;
                    case TokKind.KwdHas:
                        if (precMin > Precedence.InHas)
                            return node;
                        node = ParseInHas(node);
                        continue;
                    case TokKind.KwdNot:
                        if (TidCur == TokKind.KwdNot)
                            goto case TokKind.Bng;
                        goto case TokKind.Ident;

                    case TokKind.KwdWith:
                        if (TidCur != tid && TidPeek() != TokKind.CurlyOpen)
                            goto case TokKind.Ident;
                        {
                            Validation.Assert(precMin <= Precedence.Primary);
                            node = new ModuleProjectionNode(ref _idNext, TokEatKwd(tid), node,
                                ParseRecOrParen());
                            continue;
                        }

                    case TokKind.EquGrt:
                        {
                            Validation.Assert(precMin <= Precedence.Primary);
                            // REVIEW: Should we also support an identifier on the right as in:
                            // M=>R, where R is a record value?
                            node = new ModuleProjectionNode(ref _idNext, TokMove(tid), node,
                                ParseRecOrParen());
                            continue;
                        }

                    case TokKind.At:
                    case TokKind.Tld:
                    case TokKind.Bng:
                        // See if this is a modifier on a comparison operator.
                        switch (PeekPastCompareMods(out num))
                        {
                        case TokKind.KwdIn:
                        case TokKind.KwdHas:
                            if (precMin > Precedence.InHas)
                                return node;
                            node = ParseInHas(node);
                            continue;
                        case TokKind.IntLit:
                            if (num != 1 || TidCur != TokKind.Bng)
                                goto default;
                            // Deprecated GetSlot: x!<index>
                            Validation.Assert(precMin <= Precedence.Primary);
                            {
                                var tokBng = TokMove(TokKind.Bng);
                                var tokIntLit = TokMove(TokKind.IntLit).Cast<IntLitToken>();

                                string guess;
                                SourceRange rng;
                                SliceItemTuple sliceItems;
                                guess = string.Format("[{0}]", tokIntLit);
                                rng = tokBng.Range.Union(tokIntLit.Range);
                                sliceItems = SliceItemTuple.Create(
                                    SliceItemNode.Create(ref _idNext, null, null, new NumLitNode(ref _idNext, tokIntLit)));
                                AddDiag(RexlDiagnostic.WarningGuess(tokBng, ErrorStrings.WrnDeprecatedTupleGetSlot, guess, rng));
                                var sliceList = new SliceListNode(ref _idNext, tokBng, sliceItems);
                                node = IndexingNode.Create(ref _idNext, tokBng, node, sliceList, null);
                                continue;
                            }
                        default:
                            var tidPeek = TidPeek(num, useAlt: true);
                            if (tidPeek == TokKind.KwdIn || tidPeek == TokKind.KwdHas)
                                goto case TokKind.KwdIn;
                            if (precMin > Precedence.Compare)
                                return node;
                            node = ParseComparison(node);
                            continue;
                        }

                    case TokKind.KtxAnd:
                    case TokKind.AmpAmp:
                        if (precMin > Precedence.And)
                            return node;
                        if (tid != TokKind.KtxAnd)
                            PostBinDeprecation("and");
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.And, node, ParseExpr(Precedence.And + 1));
                        continue;

                    case TokKind.KtxXor:
                    case TokKind.CarCar:
                        if (precMin > Precedence.Xor)
                            return node;
                        if (tid != TokKind.KtxXor)
                            PostBinDeprecation("xor");
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.Xor, node, ParseExpr(Precedence.Xor + 1));
                        continue;

                    case TokKind.KtxOr:
                    case TokKind.BarBar:
                        if (precMin > Precedence.Or)
                            return node;
                        if (tid != TokKind.KtxOr)
                            PostBinDeprecation("or");
                        node = new BinaryOpNode(ref _idNext, TokMoveAlt(tid), BinaryOp.Or, node, ParseExpr(Precedence.Or + 1));
                        continue;

                    case TokKind.QueQue:
                        if (precMin > Precedence.Coalesce)
                            return node;
                        // Note that ?? is right associative, like ^.
                        node = new BinaryOpNode(ref _idNext, TokMove(TokKind.QueQue), BinaryOp.Coalesce, node, ParseExpr(Precedence.Coalesce));
                        continue;

                    case TokKind.KwdIf:
                        if (precMin > Precedence.If)
                            return node;
                        // Note right associativity.
                        node = new IfNode(ref _idNext, TokMoveAlt(TokKind.KwdIf), node, ParseExpr(Precedence.Expr), TokEatKwd(TokKind.KwdElse), ParseExpr(Precedence.If));
                        continue;

                    case TokKind.Semi:
                        return node;

                    // Handle error cases that look like a value or statement.
                    case TokKind.Ident:
                    case TokKind.IntLit:
                    case TokKind.FltLit:
                    case TokKind.TxtLit:
                    case TokKind.KwdTrue:
                    case TokKind.KwdFalse:
                    case TokKind.KwdNull:
                    case TokKind.KwdThis:
                        // Note that other cases may jump to here!
                        // We expected an operator, but see a value. Assume that a binary operator is missing.
                        if (precMin > Precedence.Error)
                            return node;
                        PostError(ErrorStrings.ErrOperatorExpected);
                        node = new BinaryOpNode(ref _idNext, TokCur, BinaryOp.Error, node, ParseExpr(Precedence.PrefixUnary));
                        continue;

                    case TokKind.KwdAs:
                    default:
                        return node;
                    }
                }
            }
        }

        private ExprNode ParseOperand(Precedence precMin)
        {
            Validation.Assert(Precedence.Error <= precMin & precMin <= Precedence.PrefixUnary);

            // Limit recursion depth to avoid stack overflow, here and in visitors.
            if (_stackDepth > _maxStackDepth)
                throw new RexlException(new ErrorNode(ref _idNext, TokCur, PostError(ErrorStrings.ErrRuleNestedTooDeeply)), null);

            for (; ; )
            {
                var tid = TidCur;
                switch (tid)
                {
                // (Expr) or tuple literal.
                case TokKind.ParenOpen:
                    return ParseTupleOrParenExpr();

                // {id:Expr*}
                case TokKind.CurlyOpen:
                    return ParseRecordExpr(TokMove(TokKind.CurlyOpen));

                // [Expr*]
                case TokKind.SquareOpen:
                    return ParseSequenceExpr();

                // bnot Expr
                case TokKind.KwdBnot:
                    if (precMin > Precedence.BitNot)
                        PostError(ErrorStrings.ErrBadBnot);
                    return new UnaryOpNode(ref _idNext, TokMoveKwd(TokKind.KwdBnot), UnaryOp.BitNot, ParseExpr(Precedence.BitNot));

                // ~ Expr
                case TokKind.Tld:
                    return new UnaryOpNode(ref _idNext, TokMove(TokKind.Tld), UnaryOp.BitNot, ParseExpr(Precedence.PrefixUnary));

                // -Expr
                case TokKind.Sub:
                    return new UnaryOpNode(ref _idNext, TokMove(TokKind.Sub), UnaryOp.Negate, ParseExpr(Precedence.PrefixUnary));
                // +Expr
                case TokKind.Add:
                    return new UnaryOpNode(ref _idNext, TokMove(TokKind.Add), UnaryOp.Posate, ParseExpr(Precedence.PrefixUnary));

                // not Expr
                case TokKind.KwdNot:
                    if (precMin > Precedence.Not)
                        PostError(ErrorStrings.ErrBadNot);
                    return new UnaryOpNode(ref _idNext, TokMoveKwd(TokKind.KwdNot), UnaryOp.Not, ParseExpr(Precedence.Not));

                // ! Expr
                case TokKind.Bng:
                    return new UnaryOpNode(ref _idNext, TokMove(TokKind.Bng), UnaryOp.Not, ParseExpr(Precedence.PrefixUnary));

                // Literals
                case TokKind.KwdNull:
                    return new NullLitNode(ref _idNext, TokMoveKwd(TokKind.KwdNull));
                case TokKind.KwdTrue:
                case TokKind.KwdFalse:
                    return new BoolLitNode(ref _idNext, TokMoveKwd(tid));
                case TokKind.IntLit:
                    return new NumLitNode(ref _idNext, TokMove(TokKind.IntLit).Cast<NumLitToken>());
                case TokKind.FltLit:
                    return new NumLitNode(ref _idNext, TokMove(TokKind.FltLit).Cast<NumLitToken>());
                case TokKind.TxtLit:
                    return new TextLitNode(ref _idNext, TokMove(TokKind.TxtLit).Cast<TextLitToken>());

                case TokKind.Hash:
                    switch (TidPeek())
                    {
                    case TokKind.KwdIt:
                        {
                            var tokHash = TokMove(TokKind.Hash);
                            var tokIt = TokMoveKwd(TokKind.KwdIt).Cast<KeyToken>();
                            return new GetIndexNode(ref _idNext, tokHash, new ItNameNode(ref _idNext, tokIt));
                        }
                    case TokKind.ItSlot:
                        {
                            var tokHash = TokMove(TokKind.Hash);
                            var tokItSlot = TokMove(TokKind.ItSlot).Cast<ItSlotToken>();
                            var it = new ItNameNode(ref _idNext, tokItSlot);
                            return new GetIndexNode(ref _idNext, tokHash, it);
                        }
                    case TokKind.Ident:
                        {
                            var tokHash = TokMove(TokKind.Hash);
                            if (TokCur.Kind == TokKind.Ident && TokCur.HasAlt && !TokCur.AltFuzzy &&
                                TokCur.TokenAlt.Kind.IsContextualKwd() && tokHash.Range.Lim < TokCur.Range.Min)
                            {
                                // '# <ident-or-ktx>' is parsed differently than '#<ident-or-ktx>'.
                                // The former should be parsed as '#<ident>' and the latter as '#0 <ktx>',
                                // to ensure no surprising parse errors result from expressions like '# mod 3'.
                                // It's simply easier to handle this in the parser rather than the lexer.
                                return new GetIndexNode(ref _idNext, tokHash, 0);
                            }
                            var name = new FirstNameNode(ref _idNext, ParseIdentifier(false));
                            return new GetIndexNode(ref _idNext, tokHash, name);
                        }
                    case TokKind.At:
                        {
                            var tokHash = TokMove(TokKind.Hash);
                            var name = new FirstNameNode(ref _idNext, ParseIdentifier(false));
                            return new GetIndexNode(ref _idNext, tokHash, name);
                        }
                    default:
                        {
                            var tokHash = TokMove(TokKind.Hash);
                            return new GetIndexNode(ref _idNext, tokHash, slot: 0);
                        }
                    }
                case TokKind.HashSlot:
                    return new GetIndexNode(ref _idNext, TokMove(TokKind.HashSlot).Cast<HashSlotToken>());

                case TokKind.At:
                    {
                        var ident = ParseIdentifier(true);
                        if (TidCur == TokKind.ParenOpen)
                            return ParseInvocation(ident);
                        return new FirstNameNode(ref _idNext, ident);
                    }
                case TokKind.Ident:
                    {
                        var tidKtx = TidCurAlt;
                        switch (tidKtx)
                        {
                        case TokKind.KtxModule:
                            if (TidPeek() == TokKind.CurlyOpen)
                            {
                                return new ModuleNode(ref _idNext, TokMoveAlt(tidKtx),
                                    TokMove(TokKind.CurlyOpen), ParseModuleSyms(), TokEat(TokKind.CurlyClose));
                            }
                            break;
                        }
                        var ident = ParseIdentifier(true);
                        if (TidCur == TokKind.ParenOpen)
                            return ParseInvocation(ident);
                        return new FirstNameNode(ref _idNext, ident);
                    }

                case TokKind.KwdThis:
                    return new ThisNameNode(ref _idNext, TokMoveKwd(TokKind.KwdThis));

                // it[$<slot>].
                case TokKind.ItSlot:
                    {
                        var tok = TokMove(TokKind.ItSlot).Cast<ItSlotToken>();
                        return new ItNameNode(ref _idNext, tok);
                    }
                case TokKind.KwdIt:
                    {
                        var tok = TokMoveKwd(TokKind.KwdIt).Cast<KeyToken>();
                        return new ItNameNode(ref _idNext, tok);
                    }
                case TokKind.Box:
                    return new BoxNode(ref _idNext, TokMoveKwd(TokKind.Box).Cast<KeyToken>());

                case TokKind.KwdWith:
                    if (TidPeek() != TokKind.ParenOpen)
                        goto default;
                    {
                        // A mis-casing of the With function.
                        var tok = TokCur;
                        var ident = new Identifier(new IdentToken(tok.Stream, tok.Index, "With", tok.Range.Min, tok.Range.Lim));
                        PostErrorGuess(ErrorStrings.ErrBadTok_Cur_Instead, "With", tok.Range, "with", "With");
                        TidSkip();
                        return ParseInvocation(ident);
                    }

                case TokKind.Error:
                    var err = PostError(ErrorStrings.ErrBadToken);
                    return new ErrorNode(ref _idNext, TokMove(TokKind.Error), err);

                case TokKind.Eof:
                default:
                    if (tid.ToDir() != Directive.None)
                    {
                        PostError(ErrorStrings.ErrBadDirective);
                        TidSkip(tid);
                        continue;
                    }

                    // Any other input should cause parsing errors.
                    PostError(ErrorStrings.ErrOperandExpected);
                    return new MissingValueNode(ref _idNext, TokCur);
                }
            }
        }

        /// <summary>
        /// Returns the token index that would result from a call to <see cref="ParseIdentifier(bool)"/> at the
        /// given token position. This should be in sync with the implementation of ParseIdentifier.
        /// </summary>
        private int PosFromIdent(int itok)
        {
            if (TidPeek(itok) == TokKind.At)
                itok++;
            switch (TidPeek(itok))
            {
            case TokKind.Ident:
                itok++;
                break;
            case TokKind.Box:
                // This case is an error, but ParseIdentifier will eat it.
                itok++;
                break;
            }
            return itok;
        }

        private Identifier ParseIdentifier(bool allowRooted = false)
        {
            IdentToken tok;
            Token at = null;
#if DEBUG
            int itokEnd = ItokCur + PosFromIdent(0);
#endif
            if (TidCur == TokKind.At)
            {
                if (!allowRooted)
                    PostErrorGuess(ErrorStrings.ErrGlobalIdentNotAllowed, "", TokCur.Range);
                at = TokMove(TokKind.At);
            }

            if (TidCur == TokKind.Ident)
                tok = TokMoveRaw().Cast<IdentToken>();
            else if (TidCur == TokKind.Box)
            {
                ErrorTid(TokKind.Ident);
                var rng = TokCur.Range;
                tok = new IdentToken(TokCur.Stream, TokCur.Index, "_", rng.Min, rng.Lim, IdentFlags.WantsQuotes);
                TidSkip(TokKind.Box);
            }
            else
            {
                ErrorTid(TokKind.Ident);
                int ich = TokCur.Range.Min;
                tok = new IdentToken(TokCur.Stream, TokCur.Index, "", ich, ich, IdentFlags.WantsQuotes);
            }
#if DEBUG
            Validation.Assert(ItokCur == itokEnd);
#endif

            return new Identifier(tok, at);
        }

        /// <summary>
        /// Parse an indexing expression based on the given <paramref name="node"/>.
        /// </summary>
        private IndexingNode ParseIndexing(ExprNode node)
        {
            Validation.AssertValue(node);
            Validation.Assert(TidCur == TokKind.SquareOpen);

            Token tok = TokMove(TokKind.SquareOpen);
            return IndexingNode.Create(ref _idNext, tok, node, ParseSliceList(TokKind.SquareClose), TokEat(TokKind.SquareClose));

        }

        /// <summary>
        /// Parses a (possibly empty) list of slice items. Does not allow trailing comma.
        /// </summary>
        private SliceListNode ParseSliceList(TokKind tidStop)
        {
            if (TidCur == tidStop || TidCur == TokKind.Eof)
                return new SliceListNode(ref _idNext, TokCur, SliceItemTuple.Empty);

            var list = Immutable.Array.CreateBuilder<SliceItemNode>();
            var tokList = TokCur;
            list.Add(ParseSliceItem());
            while (TidCur == TokKind.Comma)
            {
                if (list.Count == 1)
                    tokList = TokCur;
                TidSkip(TokKind.Comma);
                list.Add(ParseSliceItem());
            }

            return new SliceListNode(ref _idNext, tokList, list.ToImmutable());
        }

        private SliceItemNode ParseSliceItem()
        {
            Token back1 = null;
            Token edge1 = null;
            Token star1 = null;
            ExprNode start = TidCur != TokKind.Colon ? ParseIndex(out back1, out edge1, out star1) : null;

            if (star1 != null)
                BadIndexMod(star1, ErrorStrings.ErrBadTimesModifierInSlice_Tok);

            if (TidCur != TokKind.Colon)
                return SliceItemNode.Create(ref _idNext, back1, edge1, start);

            if (edge1 != null)
                BadIndexMod(edge1);

            var col1 = TokEat(TokKind.Colon);

            ExprNode stop = null;
            Token back2 = null;
            Token edge2 = null;
            Token star2 = null;
            switch (TidCur)
            {
            case TokKind.Comma:
            case TokKind.SquareClose:
            case TokKind.Colon:
            case TokKind.Eof:
                break;

            default:
                stop = ParseIndex(out back2, out edge2, out star2);
                break;
            }

            if (edge2 != null)
                BadIndexMod(edge2);

            if (TidCur != TokKind.Colon)
                return SliceItemNode.Create(ref _idNext, back1, start, col1, back2, star2, stop);
            var col2 = TokEat(TokKind.Colon);

            ExprNode step;
            switch (TidCur)
            {
            case TokKind.Comma:
            case TokKind.SquareClose:
            case TokKind.Colon:
            case TokKind.Eof:
                step = null;
                break;

            default:
                step = ParseExpr(Precedence.Error);
                break;
            }
            return SliceItemNode.Create(ref _idNext, back1, start, col1, back2, star2, stop, col2, step);
        }

        private void BadIndexMod(Token tok)
        {
            BadIndexMod(tok, ErrorStrings.ErrBadIndexModifierInSlice_Tok);
        }

        private void BadIndexMod(Token tok, StringId msg_tok)
        {
            AddDiag(RexlDiagnostic.ErrorGuess(tok, msg_tok, "", tok.Range, tok.GetTextString()));
        }

        private void PostDupToken()
        {
            PostErrorGuess(ErrorStrings.ErrRedundantToken_Tok, "", TokCur.Range, TokCur.GetTextString());
        }

        private void PostConflictingCmpMod(Token tokGood)
        {
            PostErrorGuess(ErrorStrings.ErrConflictingCmpModifier_Bad_Good, "",
                TokCur.Range, TokCur.GetTextString(), tokGood.GetTextString());
        }

        private ExprNode ParseIndex(out Token tokBack, out Token tokEdge, out Token tokStar)
        {
            tokBack = null;
            tokEdge = null;
            tokStar = null;
            for (; ; )
            {
                switch (TidCur)
                {
                case TokKind.Car:
                    if (tokBack is null)
                        tokBack = TokCur;
                    else
                        PostDupToken();
                    TidSkip();
                    break;

                case TokKind.Per:
                case TokKind.Amp:
                    if (tokEdge is null)
                        tokEdge = TokCur;
                    else if (tokEdge.Kind == TidCur)
                        PostDupToken();
                    else
                    {
                        PostErrorGuess(ErrorStrings.ErrConflictingIndexModifier_Bad_Good, "",
                            TokCur.Range, TokCur.GetTextString(), tokEdge.GetTextString());
                    }
                    TidSkip();
                    break;

                case TokKind.Mul:
                    if (tokStar is null)
                        tokStar = TokCur;
                    else
                        PostDupToken();
                    TidSkip();
                    break;

                default:
                    return ParseExpr(Precedence.Error);
                }
            }
        }

        private ExprNode ParsePipeCall(Token tokPipe, ExprNode arg0)
        {
            var idents = ParseIdentPath();
            return CallNode.CreatePiped(ref _idNext, tokPipe, idents, TokEat(TokKind.ParenOpen), ParseArgList(TokKind.ParenClose, arg0), TokEat(TokKind.ParenClose));
        }

        private CallNode ParseInvocation(Identifier head)
        {
            Validation.AssertValue(head);
            return ParseInvocation(IdentPath.Create(head));
        }

        private CallNode ParseInvocation(DottedNameNode pathNode)
        {
            Validation.AssertValue(pathNode);
            // The root of the DottedNameNode must be an identifier.
            Validation.Assert(pathNode.Root is FirstNameNode);
            return ParseInvocation(pathNode.ToIdents());
        }

        private CallNode ParseInvocation(IdentPath idents)
        {
            Validation.AssertValue(idents);
            Validation.Assert(idents.Length > 0);
            Validation.Assert(TidCur == TokKind.ParenOpen);

            Token tok = TokMove(TokKind.ParenOpen);
            var list = ParseArgList(TokKind.ParenClose);
            return CallNode.Create(ref _idNext, idents, tok, list, TokEat(TokKind.ParenClose));

        }

        /// <summary>
        /// Parses an argument list for an invocation. Note that directives and variable decls are allowed.
        /// The return value will include <paramref name="arg0"/> as the first argument if it is not null.
        /// If the current token is tidStop or Eof, then returns an empty list, or arg0 if not null.
        /// </summary>
        private ExprListNode ParseArgList(TokKind tidStop, ExprNode arg0 = null)
        {
            if (TidCur == tidStop || TidCur == TokKind.Eof)
                return new ExprListNode(ref _idNext, TokCur, arg0 == null ? Immutable.Array<ExprNode>.Empty : Immutable.Array.Create(arg0));

            var list = Immutable.Array.CreateBuilder<ExprNode>();
            if (arg0 != null)
            {
                var expr = arg0;
                if (TidCurAlt == TokKind.KwdAs)
                {
                    var tokSep = TokMoveAlt(TokKind.KwdAs);
                    switch (TidCur)
                    {
                    default:
                        expr = new VariableDeclNode(ref _idNext, tokSep, ParseIdentifier(), expr);
                        break;
                    case TokKind.Box:
                        expr = new VariableDeclNode(ref _idNext, tokSep, TokMoveKwd(TokKind.Box).Cast<KeyToken>(), expr);
                        break;
                    }
                    TokEat(TokKind.Comma);
                }
                list.Add(expr);
            }

            var tokList = TokCur;
            for (; ; )
            {
                // Check for a directive.
                var dir = TidCur.ToDir();
                var tokDir = dir != Directive.None ? TokMoveRaw() : null;

                // Check for name : value.
                Token tokSep = null;
                Identifier ident = null;
                KeyToken box = null;
                if (TidPeek() == TokKind.Colon)
                {
                    switch (TidCur)
                    {
                    case TokKind.Ident:
                        ident = ParseIdentifier();
                        tokSep = TokEat(TokKind.Colon).VerifyValue();
                        break;
                    case TokKind.Box:
                        box = TokMoveKwd(TokKind.Box).Cast<KeyToken>();
                        tokSep = TokEat(TokKind.Colon).VerifyValue();
                        break;
                    }
                }

                // Parse the expression.
                ExprNode expr = ParseExpr(Precedence.Error);

                // Check for value as name.
                if (TidCurAlt == TokKind.KwdAs)
                {
                    if (ident == null && box == null)
                    {
                        tokSep = TokMoveAlt(TokKind.KwdAs);
                        switch (TidCur)
                        {
                        default:
                            ident = ParseIdentifier();
                            break;
                        case TokKind.Box:
                            box = TokMoveKwd(TokKind.Box).Cast<KeyToken>();
                            break;
                        }
                    }
                    else
                    {
                        PostError(ErrorStrings.ErrBadToken);
                        switch (TidSkip(TokKind.KwdAs))
                        {
                        case TokKind.Ident:
                        case TokKind.Box:
                            TidSkip();
                            break;
                        }
                    }
                }

                if (ident != null)
                    expr = new VariableDeclNode(ref _idNext, tokSep, ident, expr);
                else if (box != null)
                    expr = new VariableDeclNode(ref _idNext, tokSep, box, expr);
                if (tokDir != null)
                    expr = new DirectiveNode(ref _idNext, tokDir, dir, expr);
                list.Add(expr);

                if (TidCur != TokKind.Comma)
                    break;
                if (list.Count == 1)
                    tokList = TokCur;
                TidSkip(TokKind.Comma);
            }

            return new ExprListNode(ref _idNext, tokList, list.ToImmutable());
        }

        /// <summary>
        /// Parses an expression list that requires elements to be "named", that is, either var decls or dotted names.
        /// For a dotted name, the leaf name becomes the "name" of the item.
        /// This allows a trailing comma. If the current token is tidStop or Eof, then returns an empty list. On return, if
        /// the last token eaten was a (trailing) comma, tokTrail will be that comma.
        /// </summary>
        private ExprListNode ParseNamedList(TokKind tidStop, out Token tokTrail)
        {
            tokTrail = null;
            if (TidCur == tidStop || TidCur == TokKind.Eof)
                return new ExprListNode(ref _idNext, TokCur, Immutable.Array<ExprNode>.Empty);

            var list = Immutable.Array.CreateBuilder<ExprNode>();
            var tokList = TokCur;
            while (TidCur != tidStop && TidCur != TokKind.Eof)
            {
                // Check for name : value.
                Identifier ident = null;
                Token tokSep = null;
                if (TidCur == TokKind.Ident && TidPeek() == TokKind.Colon || TidCur == TokKind.Colon)
                {
                    ident = ParseIdentifier();
                    tokSep = TokEat(TokKind.Colon).VerifyValue();
                }

                // Parse the expression.
                ExprNode expr = ParseExpr(Precedence.Error);

                // Check for value as name.
                // REVIEW: Case-correction/fuzzy?
                if (TidCurAlt == TokKind.KwdAs ||
                    ident == null && expr.Kind != NodeKind.FirstName && expr.Kind != NodeKind.DottedName)
                {
                    if (ident == null)
                    {
                        tokSep = TokEatKwd(TokKind.KwdAs);
                        ident = ParseIdentifier();
                        tokSep ??= ident.Token;
                    }
                    else
                    {
                        PostError(ErrorStrings.ErrBadToken);
                        switch (TidSkip(TokKind.KwdAs))
                        {
                        case TokKind.Ident:
                        case TokKind.Box:
                            TidSkip();
                            break;
                        }
                    }
                }

                if (ident != null)
                    expr = new VariableDeclNode(ref _idNext, tokSep, ident, expr);
                list.Add(expr);

                tokTrail = null;
                if (TidCur != TokKind.Comma)
                    break;
                if (list.Count == 1)
                    tokList = TokCur;
                tokTrail = TokMove(TokKind.Comma);
            }

            return new ExprListNode(ref _idNext, tokList, list.ToImmutable());
        }

        /// <summary>
        /// Parses an expression list that allows a trailing comma. Note that named elements (var decls) are
        /// not allowed. If the current token is tidStop or Eof, then returns an empty list. On return, if the
        /// last token eaten was a (trailing) comma, tokTrail will be that comma.
        /// </summary>
        private ExprListNode ParseExprList(TokKind tidStop, out Token tokTrail)
        {
            tokTrail = null;
            if (TidCur == tidStop || TidCur == TokKind.Eof)
                return new ExprListNode(ref _idNext, TokCur, Immutable.Array<ExprNode>.Empty);

            var list = Immutable.Array.CreateBuilder<ExprNode>();
            var tokList = TokCur;
            while (TidCur != tidStop && TidCur != TokKind.Eof)
            {
                list.Add(ParseExpr(Precedence.Error));

                tokTrail = null;
                if (TidCur != TokKind.Comma)
                    break;
                if (list.Count == 1)
                    tokList = TokCur;
                tokTrail = TokMove(TokKind.Comma);
            }

            return new ExprListNode(ref _idNext, tokList, list.ToImmutable());
        }

        /// <summary>
        /// Used to skip through contiguous set of tokens of kinds Bng, KwdNot, Tld, and Dol.
        /// Eats all nots, tildes, and dollars, setting the first three out parameters to the first
        /// of each (or null) and the last to the last of mentioned kinds (or null).
        /// Issues errors on duplicates.
        /// </summary>
        private void EatCmpMods(
            out Token tokNot, out Token tokTld, out Token tokDol, out Token tokAt, out Token tokLast)
        {
            tokNot = null;
            tokTld = null;
            tokDol = null;
            tokAt = null;
            tokLast = null;

            for (; ; TidSkip())
            {
                switch (TidCur)
                {
                case TokKind.Bng:
                case TokKind.KwdNot:
                    if (tokNot is not null)
                        PostDupToken();
                    else
                        tokLast = tokNot = TokCur;
                    break;
                case TokKind.Tld:
                    if (tokTld is not null)
                        PostDupToken();
                    else
                        tokLast = tokTld = TokCur;
                    break;
                case TokKind.Dol:
                    if (tokDol is not null)
                        PostDupToken();
                    else if (tokAt is not null)
                        PostConflictingCmpMod(tokAt);
                    else
                        tokLast = tokDol = TokCur;
                    break;
                case TokKind.At:
                    if (tokAt is not null)
                        PostDupToken();
                    else if (tokDol is not null)
                        PostConflictingCmpMod(tokDol);
                    else
                        tokLast = tokAt = TokCur;
                    break;

                default:
                    Validation.Assert(tokDol is null || tokAt is null);
                    return;
                }
            }
        }

        /// <summary>
        /// Used to peek past a contiguous set of <see cref="CompareModifiers"/> tokens.
        /// Returns the first token kind that is not of a modifier kind and sets <paramref name="num"/>
        /// to the number of tokens skipped.
        /// </summary>
        private TokKind PeekPastCompareMods(out int num)
        {
            for (int i = 0; ; i++)
            {
                var tid = TidPeek(i, useAlt: false);
                switch (tid)
                {
                case TokKind.Bng:
                case TokKind.KwdNot:
                case TokKind.Tld:
                case TokKind.Dol:
                case TokKind.At:
                    break;
                default:
                    num = i;
                    return tid;
                }
            }
        }

        private CompareNode ParseComparison(ExprNode node)
        {
            Validation.AssertValue(node);

            var tok = TokCur;
            var toks = Immutable.Array.CreateBuilder<(Token Op, Token Not, Token Tld, Token Str)>();
            var cops = Immutable.Array.CreateBuilder<CompareOp>();
            var exprs = Immutable.Array.CreateBuilder<ExprNode>();
            exprs.Add(node);

            for (; ; )
            {
                EatCmpMods(out var tokNot, out var tokTld, out var tokDol, out var tokAt, out var tokLast);
                Validation.Assert(tokDol is null || tokAt is null);
                CompareRoot root;
                Token tokOp;

                switch (TidCur)
                {
                default:
                    if (tokLast is null)
                    {
                        Validation.Assert(tokTld is null);
                        Validation.Assert(tokNot is null);
                        Validation.Assert(tokDol is null);
                        Validation.Assert(tokAt is null);
                        Validation.Assert(exprs.Count == toks.Count + 1);
                        Validation.Assert(toks.Count == cops.Count);
                        return new CompareNode(ref _idNext, toks[0].Op ?? tok, exprs.ToImmutable(), cops.ToImmutable(), toks.ToImmutable());
                    }
                    tokOp = null;
                    string guess;
                    switch (tokLast.Kind)
                    {
                    case TokKind.Tld: guess = "~="; break;
                    case TokKind.KwdNot: guess = "not ="; break;
                    case TokKind.Bng: guess = "!="; break;
                    case TokKind.Dol: guess = "$="; break;
                    case TokKind.At: guess = "@="; break;
                    default:
                        Validation.Assert(false);
                        guess = "=";
                        break;
                    }
                    PostErrorGuess(ErrorStrings.ErrComparisonOperatorExpected, guess, tokLast.Range);
                    root = CompareRoot.Equal;
                    break;
                case TokKind.Equ:
                    root = CompareRoot.Equal;
                    tokOp = TokMove(TokKind.Equ);
                    if (TidCur == TokKind.Equ)
                    {
                        PostError(ErrorStrings.ErrRedundantToken_Tok, TokCur.GetTextString());
                        TidSkip();
                    }
                    break;
                case TokKind.Lss:
                    root = CompareRoot.Less;
                    tokOp = TokMove(TokKind.Lss);
                    break;
                case TokKind.LssEqu:
                    root = CompareRoot.LessEqual;
                    tokOp = TokMove(TokKind.LssEqu);
                    break;
                case TokKind.GrtEqu:
                    root = CompareRoot.GreaterEqual;
                    tokOp = TokMove(TokKind.GrtEqu);
                    break;
                case TokKind.Grt:
                    root = CompareRoot.Greater;
                    tokOp = TokMove(TokKind.Grt);
                    break;
                }

                CompareModifiers mods = default;
                if (tokNot is not null)
                    mods |= CompareModifiers.Not;
                if (tokTld is not null)
                    mods |= CompareModifiers.Ci;
                // If neither $ nor @ are present, strictness depends on root, with eq being non-strict
                // and ordered operators being strict.
                if (tokDol is not null || tokAt is null && root != CompareRoot.Equal)
                    mods |= CompareModifiers.Strict;

                toks.Add((tokOp, tokNot, tokTld, tokDol ?? tokAt));
                cops.Add(new CompareOp(root, mods));
                exprs.Add(ParseExpr(Precedence.Compare + 1));
            }
        }

        private ExprNode ParseInHas(ExprNode node)
        {
            Validation.AssertValue(node);
            EatCmpMods(out var tokNot, out var tokTld, out var tokDol, out var tokAt, out _);
            Validation.Assert(tokDol is null || tokAt is null);

            // We use TidCurAlt instead of TidCur to allow cases like "In" and "Has" to be mapped onto "in" and "has".
            Validation.Assert(TidCurAlt == TokKind.KwdIn || TidCurAlt == TokKind.KwdHas);
            BinaryOp bop = TidCurAlt == TokKind.KwdIn ? BinaryOp.In : BinaryOp.Has;
            var tokOp = TokMoveAlt(oper: true);

            if (tokNot is not null)
                bop = bop.GetInHasNeg();
            if (tokTld is not null)
                bop = bop.GetInHasCi();
            if (tokDol is not null || tokAt is not null)
            {
                var tokBad = tokDol ?? tokAt;
                AddDiag(RexlDiagnostic.ErrorGuess(tokBad, ErrorStrings.ErrCmpModOn_Oper_Mod,
                    "", tokBad.Range, tokOp.GetStdString(), tokBad.GetStdString()));
            }

            return new InHasNode(ref _idNext, tokOp, bop, node, ParseExpr(Precedence.InHas + 1), tokNot, tokTld);
        }

        /// <summary>
        /// Parses a record node or a parenthesized expression.
        /// </summary>
        private ExprNode ParseRecOrParen()
        {
            // REVIEW: Perhaps also support single identifier (first name node).
            if (TidCur == TokKind.CurlyOpen)
                return ParseRecordExpr(TokMove(TokKind.CurlyOpen));
            return ParseParenExpr();
        }

        /// <summary>
        /// Parse a record expression of the form: {id:expr, id:expr, ...}.
        /// </summary>
        private RecordNode ParseRecordExpr(Token tok)
        {
            Validation.AssertValue(tok);

            var list = ParseNamedList(TokKind.CurlyClose, out var tokTrail);
            return new RecordNode(ref _idNext, tok, list, TokEat(TokKind.CurlyClose) ?? tokTrail);
        }

        // Parse a sequence expression: [], [<expr-list>] or [<expr-list> , ].
        private SequenceNode ParseSequenceExpr()
        {
            Validation.Assert(TidCur == TokKind.SquareOpen);

            Token tokOpen = TokMoveRaw();
            var list = ParseExprList(TokKind.SquareClose, out var tokTrail);
            // For a sequence, a trailing comma is not an error.
            return new SequenceNode(ref _idNext, tokOpen, list, TokEat(TokKind.SquareClose) ?? tokTrail);
        }

        // Parse a tuple expression or parenthesized expr: (), (<expr-list>) or (<expr-list> , ).
        private ExprNode ParseTupleOrParenExpr()
        {
            Validation.Assert(TidCur == TokKind.ParenOpen);

            Token tokOpen = TokMoveRaw();
            var list = ParseExprList(TokKind.ParenClose, out var tokTrail);
            var tokClose = TokEat(TokKind.ParenClose);

            if (list.Count == 1 && tokTrail == null)
                return ParenNode.Wrap(ref _idNext, tokOpen, list.Children[0], tokClose);

            // For a tuple, a trailing comma is not an error.
            return new TupleNode(ref _idNext, tokOpen, list, tokClose ?? tokTrail);
        }

        // Parse a tuple expression, without requiring trailing comma for arity-one: (), (<expr-list>) or (<expr-list> , ).
        private TupleNode ParseTupleExpr()
        {
            Validation.Assert(TidCur == TokKind.ParenOpen);

            Token tokOpen = TokMoveRaw();
            var list = ParseExprList(TokKind.ParenClose, out var tokTrail);
            var tokClose = TokEat(TokKind.ParenClose);
            return new TupleNode(ref _idNext, tokOpen, list, tokClose ?? tokTrail);
        }

        /// <summary>
        /// Add a diagnostic to the list. All additions should use this so it's easy to
        /// set a break point where all diags are added.
        /// </summary>
        private RexlDiagnostic AddDiag(RexlDiagnostic diag)
        {
            Validation.AssertValue(diag);
            Util.Add(ref _diags, diag);
            return diag;
        }

        /// <summary>
        /// Post an error at the current token.
        /// </summary>
        private RexlDiagnostic PostError(StringId msg)
        {
            Validation.Assert(msg.IsValid);
            return AddDiag(RexlDiagnostic.Error(TokCur, msg));
        }

        /// <summary>
        /// Post an error at the current token, using the given args.
        /// </summary>
        private RexlDiagnostic PostError(StringId msg, params object[] args)
        {
            Validation.Assert(msg.IsValid);
            Validation.AssertNonEmpty(args);
            return AddDiag(RexlDiagnostic.Error(TokCur, msg, args));
        }

        /// <summary>
        /// Post an error at the current token, with the given guess.
        /// </summary>
        private RexlDiagnostic PostErrorGuess(StringId msg, string guess, SourceRange rngGuess)
        {
            Validation.Assert(msg.IsValid);
            Validation.AssertValue(guess);
            RexlDiagnostic diag = RexlDiagnostic.ErrorGuess(TokCur, msg, guess, rngGuess);
            Util.Add(ref _diags, diag);
            return diag;
        }

        /// <summary>
        /// Post an error at the current token, using the given args.
        /// </summary>
        private RexlDiagnostic PostErrorGuess(StringId msg, string guess, SourceRange rngGuess, params object[] args)
        {
            Validation.Assert(msg.IsValid);
            Validation.AssertNonEmpty(args);
            Validation.AssertValue(guess);
            return AddDiag(RexlDiagnostic.ErrorGuess(TokCur, msg, guess, rngGuess, args));
        }

        /// <summary>
        /// Post a warning at the current token.
        /// </summary>
        private RexlDiagnostic PostWarning(StringId msg)
        {
            Validation.Assert(msg.IsValid);
            return AddDiag(RexlDiagnostic.Warning(TokCur, msg));
        }

        /// <summary>
        /// Return whether there is an error at the current token.
        /// </summary>
        private bool CurHasError()
        {
            if (_diags == null)
                return false;

            // Diagnostics should be sorted by token index.
            for (int i = _diags.Count; --i >= 0;)
            {
                var d = _diags[i];
                if (d.Tok != TokCur)
                    break;
                if (d.IsError)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Post a warning for deprecated binary operator at the current token.
        /// </summary>
        private RexlDiagnostic PostBinDeprecation(string sugg)
        {
            Validation.AssertNonEmpty(sugg);
            var tok = TokCur;
            return AddDiag(RexlDiagnostic.WarningGuess(tok, ErrorStrings.WrnDeprecatedBinOp_Old_New, sugg, tok.Range, tok.GetTextString(), sugg));
        }

        /// <summary>
        /// Post a warning for deprecated token at the current token.
        /// </summary>
        private RexlDiagnostic PostTokDeprecation(string sugg)
        {
            Validation.AssertNonEmpty(sugg);
            var tok = TokCur;
            return AddDiag(RexlDiagnostic.WarningGuess(tok,
                ErrorStrings.WrnDeprecatedToken_Old_New, sugg, tok.Range, tok.GetTextString(), sugg));
        }

        /// <summary>
        /// Post an error for using a fuzzy match at the current token. The error messages varies based
        /// on <paramref name="oper"/>.
        /// </summary>
        private void PostFuzzy(Token tok, bool oper)
        {
            Validation.Assert(tok.AltFuzzy);
            var rep = tok.TokenAlt.GetStdString();
            var text = tok.GetTextString();
            PostErrorGuess(
                oper ? ErrorStrings.ErrOperatorExpected_Name_Guess : ErrorStrings.ErrUseFuzzy_Name_Guess,
                rep, tok.Range, text, rep);
        }

        /// <summary>
        /// This is <i>not</i> for keywords (reserved or contextual). Asserts that <see cref="TidCur"/>
        /// is <paramref name="tid"/>. Then calls <see cref="TokMoveRaw()"/>.
        /// </summary>
        private Token TokMove(TokKind tid)
        {
            Validation.Assert(!tid.IsKeyword());
            Validation.Assert(TidCur == tid);
            return TokMoveRaw();
        }

        /// <summary>
        /// This is for keyword tids. Asserts that <see cref="TidCur"/> is <paramref name="tid"/>.
        /// Then calls <see cref="TokMoveRaw()"/>.
        /// </summary>
        private Token TokMoveKwd(TokKind tid)
        {
            Validation.Assert(tid.IsKeyword());
            Validation.Assert(TidCur == tid);
            return TokMoveRaw();
        }

        /// <summary>
        /// This can be used with any tid. Asserts that <see cref="TidCurAlt"/> is <paramref name="tid"/>.
        /// Then calls <see cref="TokMoveAlt()"/>.
        /// </summary>
        private Token TokMoveAlt(TokKind tid)
        {
            Validation.Assert(TidCurAlt == tid);
            return TokMoveAlt(oper: tid.IsKtxOperator());
        }

        /// <summary>
        /// If the current token doesn't have an alt form, calls <see cref="TokMoveRaw()"/>. Otherwise,
        /// records the token index as using the alt form, moves to the next token, and returns the alt form.
        /// If the alt form is a fuzzy match, reports an error.
        /// </summary>
        private Token TokMoveAlt(bool oper = false)
        {
            if (!TokCur.HasAlt)
                return TokMoveRaw();

            if (TokCur.AltFuzzy)
                PostFuzzy(TokCur, oper);
            Util.Add(ref _useAlt, TokCur.Index).Verify();
            return TokMoveRaw().TokenAlt;
        }

        /// <summary>
        /// Used for non-keyword token kinds.
        /// Returns the current token if it's of the given kind and moves to the next token.
        /// If the token is not the right kind, reports an error, leaves the token, and returns null.
        /// </summary>
        private Token TokEat(TokKind tid)
        {
            Validation.Assert(!tid.IsKeyword());
            if (TidCur == tid)
                return TokMoveRaw();
            ErrorTid(tid);
            return null;
        }

        /// <summary>
        /// Used for keyword token kinds, both reserved and contextual.
        /// Returns the current token if it's of the given kind and moves to the next token.
        /// If the token is not the right kind, reports an error, leaves the token, and returns null.
        /// If the alt form is used, records its index. If the alt form was a fuzzy match, reports an error.
        /// </summary>
        private KeyToken TokEatKwd(TokKind tid)
        {
            Validation.Assert(tid.IsKeyword());
            if (TidCurAlt == tid)
            {
                if (TokCur.AltFuzzy)
                {
                    // This assumes that even if the tid is an operator (like "in"), it is being used in a
                    // non-operator context, eg, "maximize A in B".
                    PostFuzzy(TokCur, oper: false);
                }
                if (TokCur != TokCur.TokenAlt)
                    Util.Add(ref _useAlt, TokCur.Index).Verify();
                return TokMoveRaw().TokenAlt.Cast<KeyToken>();
            }
            ErrorTid(tid);
            return null;
        }

        /// <summary>
        /// Used for keyword token kinds, both reserved and contextual.
        /// Returns the current token if it's either of the given kinds and moves to the next token.
        /// If the token is not the right kind, reports an error, leaves the token, and returns null.
        /// If the alt form is used, records its index. If the alt form was a fuzzy match, reports an error.
        /// </summary>
        private KeyToken TokEatKwd(TokKind tid1, TokKind tid2)
        {
            Validation.Assert(tid1.IsKeyword());
            Validation.Assert(tid2.IsKeyword());
            if (TidCurAlt == tid1 || TidCurAlt == tid2)
            {
                if (TokCur.AltFuzzy)
                {
                    // This assumes that even if the tid is an operator (like "in"), it is being used in a
                    // non-operator context, eg, "maximize A in B".
                    PostFuzzy(TokCur, oper: false);
                }
                if (TokCur != TokCur.TokenAlt)
                    Util.Add(ref _useAlt, TokCur.Index).Verify();
                return TokMoveRaw().TokenAlt.Cast<KeyToken>();
            }
            ErrorTid(tid1);
            return null;
        }

        private void ErrorTid(TokKind tidWanted)
        {
            Validation.Assert(TokCur.Kind != tidWanted);
            PostError(ErrorStrings.ErrExpectedFound_Ex_Fnd, tidWanted, TokCur);
        }
    }
}
