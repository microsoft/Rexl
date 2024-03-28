// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Lex;

using Integer = System.Numerics.BigInteger;
using TokenTuple = Immutable.Array<Token>;

/// <summary>
/// Lexer for the research expression language.
/// </summary>
public sealed class RexlLexer
{
    internal const char IdentQuoteOpen = '\'';
    internal const char IdentQuoteClose = '\'';
    internal const string IdentQuoteOpenStr = "'";
    internal const string IdentQuoteCloseStr = "'";

    private const string _hex = "0123456789ABCDEF";

    /// <summary>
    /// Information for a "fixed" token, that is, punctuator or keyword.
    /// * Tid is the token kind.
    /// * Rep is the string representation.
    /// * Std is the string representation of the standard form. For non-aliases, it is the same as Rep.
    /// * Deprecated indicates whether this form is deprecated, in which case Std is the suggested replacement.
    /// </summary>
    public abstract class FixedTokenInfo
    {
        public TokKind Tid { get; }
        public string Rep { get; }
        public string Std { get; }
        public bool Deprecated { get; }

        private protected FixedTokenInfo(TokKind tid, string rep, string std, bool deprecated)
        {
            Validation.AssertNonEmpty(rep);
            Validation.AssertNonEmpty(std);
            Validation.Assert(tid != TokKind.None);
            Validation.Assert(!deprecated || rep != std);

            Tid = tid;
            Rep = rep;
            Std = std;
            Deprecated = deprecated;
        }
    }

    /// <summary>
    /// The punctuator table maps from a ulong representation of a string (up to 8 characters) to an
    /// optional instance of this class. An entry can either represent a punctuator, or a prefix of
    /// a punctuator, or both. A pure prefix is represented by a null value.
    /// Note that this class type is immutable.
    /// </summary>
    private sealed class PunctuatorInfo : FixedTokenInfo
    {
        public readonly bool Prefix;

        // Constructor for a real instance.
        public PunctuatorInfo(TokKind tid, string rep, string std, bool deprecated, bool prefix)
            : base(tid, rep, std, deprecated)
        {
            // A comment sequence cannot be a prefix of a punctuator.
            Validation.Assert(tid != TokKind.CommentBlock && tid != TokKind.CommentLine || !prefix);
            Prefix = prefix;
        }

        /// <summary>
        /// Promote <paramref name="cur"/> to be a prefix, if it isn't already.
        /// It is fine to call this on null or on an info that is already a prefix.
        /// </summary>
        public static PunctuatorInfo SetPrefix(PunctuatorInfo cur)
        {
            if (cur == null || cur.Prefix)
                return cur;
            return new PunctuatorInfo(cur.Tid, cur.Rep, cur.Std, cur.Deprecated, prefix: true);
        }
    }

    private sealed class KeyWordInfo : FixedTokenInfo
    {
        public KeyWordInfo(TokKind tid, string rep, string std, bool deprecated)
            : base(tid, rep, std, deprecated)
        {
        }
    }

    // Max number of characters in a punctuator, represented as packed ulong.
    // This assumes that all punctuator characters fit in a byte.
    private const int _CchMaxPunc = sizeof(ulong) / sizeof(byte);
    // Number of bits per character, when encoded in a packed ulong.
    private const int _CbitPerChar = sizeof(byte) * 8;

    // Maps from punctuator or keyword tid to the standard text representation.
    private readonly ReadOnly.Dictionary<TokKind, string> _fixedText;
    // The punctuator table.
    private readonly ReadOnly.Dictionary<ulong, PunctuatorInfo> _puncTable;
    // The keyword table maps from string to keyword info.
    // Keywords are always expected to be fully lowercase.
    private readonly ReadOnly.Dictionary<string, KeyWordInfo> _keywordTable;

    // Info for the keywords and punctuators, in a form presentable to the public.
    private readonly Immutable.Array<FixedTokenInfo> _keywords;
    private readonly Immutable.Array<FixedTokenInfo> _puncs;

    // The one and only RexlLexer instance. Allocated on first use.
    private static volatile RexlLexer _lex;

    public static RexlLexer Instance
    {
        get
        {
            if (_lex == null)
                Interlocked.CompareExchange(ref _lex, new RexlLexer(), null);
            return _lex;
        }
    }

    private RexlLexer()
    {
        var fixedText = new Dictionary<TokKind, string>();
        var puncTable = new Dictionary<ulong, PunctuatorInfo>();
        var keywordTable = new Dictionary<string, KeyWordInfo>();

        // Comments.
        _AddPunctuator("/*", TokKind.CommentBlock);
        _AddPunctuator("//", TokKind.CommentLine);

        // Punctuators
        _AddPunctuator("(", TokKind.ParenOpen);
        _AddPunctuator(")", TokKind.ParenClose);
        _AddPunctuator("[", TokKind.SquareOpen);
        _AddPunctuator("]", TokKind.SquareClose);
        _AddPunctuator("{", TokKind.CurlyOpen);
        _AddPunctuator("}", TokKind.CurlyClose);

        _AddPunctuator(".", TokKind.Dot);
        _AddPunctuator("!", TokKind.Bng);
        _AddPunctuator("~", TokKind.Tld);
        _AddPunctuator(",", TokKind.Comma);
        _AddPunctuator(";", TokKind.Semi);
        _AddPunctuator(":", TokKind.Colon);
        _AddPunctuator("?", TokKind.Que);
        _AddPunctuator("@", TokKind.At);
        _AddPunctuator("$", TokKind.Dol);
        _AddPunctuator("#", TokKind.Hash);

        _AddPunctuator("+", TokKind.Add);
        _AddPunctuator("-", TokKind.Sub);
        _AddPunctuator("*", TokKind.Mul);
        _AddPunctuator("/", TokKind.Div);
        _AddPunctuator("%", TokKind.Per);
        _AddPunctuator("|", TokKind.Bar);
        _AddPunctuator("&", TokKind.Amp);
        _AddPunctuator("^", TokKind.Car);

        _AddPunctuator("++", TokKind.AddAdd);
        _AddPunctuator("**", TokKind.MulMul);
        _AddPunctuator("||", TokKind.BarBar);
        _AddPunctuator("&&", TokKind.AmpAmp);
        _AddPunctuator("^^", TokKind.CarCar);
        _AddPunctuator("??", TokKind.QueQue);

        _AddPunctuator("=", TokKind.Equ);
        _AddPunctuator("<", TokKind.Lss);
        _AddPunctuator(">", TokKind.Grt);
        _AddPunctuator("<=", TokKind.LssEqu);
        _AddPunctuator(">=", TokKind.GrtEqu);

        _AddPunctuator("<<", TokKind.LssLss);
        _AddPunctuator(">>", TokKind.GrtGrt);
        _AddPunctuator(">>>", TokKind.GrtGrtGrt);

        _AddPunctuator("->", TokKind.SubGrt);
        _AddPunctuator("+>", TokKind.AddGrt);
        _AddPunctuator("=>", TokKind.EquGrt);
        _AddPunctuator(":=", TokKind.ColEqu);

        // Reserved keywords.
        foreach (var (kwd, tokKind) in RexlKeyWords.Keywords)
            _AddKeyword(kwd, tokKind);

        // Contextual keywords.
        _AddKeyword("func", TokKind.KtxFunc);
        _AddKeyword("function", TokKind.KtxFunc, alias: true);
        _AddKeyword("prop", TokKind.KtxProp);
        _AddKeyword("property", TokKind.KtxProp, alias: true);
        _AddKeyword("proc", TokKind.KtxProc);
        _AddKeyword("procedure", TokKind.KtxProc, alias: true);

        _AddKeyword("or", TokKind.KtxOr);
        _AddKeyword("xor", TokKind.KtxXor);
        _AddKeyword("and", TokKind.KtxAnd);

        _AddKeyword("div", TokKind.KtxDiv);
        _AddKeyword("mod", TokKind.KtxMod);
        _AddKeyword("min", TokKind.KtxMin);
        _AddKeyword("max", TokKind.KtxMax);

        _AddKeyword("bor", TokKind.KtxBor);
        _AddKeyword("bxor", TokKind.KtxBxor);
        _AddKeyword("band", TokKind.KtxBand);

        _AddKeyword("shl", TokKind.KtxShl);
        _AddKeyword("shr", TokKind.KtxShr);
        _AddKeyword("shri", TokKind.KtxShri);
        _AddKeyword("shru", TokKind.KtxShru);

        _AddKeyword("module", TokKind.KtxModule);
        _AddKeyword("plan", TokKind.KtxModule, alias: true);

        _AddKeyword("param", TokKind.KtxParam);
        _AddKeyword("parameter", TokKind.KtxParam, alias: true);
        _AddKeyword("const", TokKind.KtxConst);
        _AddKeyword("constant", TokKind.KtxConst, alias: true);
        _AddKeyword("var", TokKind.KtxVar);
        _AddKeyword("variable", TokKind.KtxVar, alias: true);
        _AddKeyword("let", TokKind.KtxLet);
        _AddKeyword("con", TokKind.KtxCon);
        _AddKeyword("constraint", TokKind.KtxCon, alias: true);
        _AddKeyword("msr", TokKind.KtxMsr);
        _AddKeyword("measure", TokKind.KtxMsr, alias: true);
        _AddKeyword("opt", TokKind.KtxOpt);
        _AddKeyword("optional", TokKind.KtxOpt, alias: true);
        _AddKeyword("req", TokKind.KtxReq);
        _AddKeyword("required", TokKind.KtxReq, alias: true);
        _AddKeyword("def", TokKind.KtxDef);
        _AddKeyword("default", TokKind.KtxDef, alias: true);

        _AddKeyword("task", TokKind.KtxTask);
        _AddKeyword("prime", TokKind.KtxPrime);
        _AddKeyword("play", TokKind.KtxPlay);
        _AddKeyword("pause", TokKind.KtxPause);
        _AddKeyword("poke", TokKind.KtxPoke);
        _AddKeyword("poll", TokKind.KtxPoll);
        _AddKeyword("finish", TokKind.KtxFinish);
        _AddKeyword("abort", TokKind.KtxAbort);

        _AddKeyword("publish", TokKind.KtxPublish);
        _AddKeyword("primary", TokKind.KtxPrimary);
        _AddKeyword("stream", TokKind.KtxStream);

        // Directives.
        _AddPunctuator("[~]", TokKind.DirCi);
        _AddPunctuator("[=]", TokKind.DirEq);
        _AddPunctuator("[~=]", TokKind.DirEqCi);
        _AddPunctuator("[<]", TokKind.DirUp);
        _AddPunctuator("[up]", TokKind.DirUp, goodAlias: true);
        _AddPunctuator("[>]", TokKind.DirDown);
        _AddPunctuator("[down]", TokKind.DirDown, goodAlias: true);
        _AddPunctuator("[~<]", TokKind.DirUpCi);
        _AddPunctuator("[~up]", TokKind.DirUpCi, goodAlias: true);
        _AddPunctuator("[~>]", TokKind.DirDownCi);
        _AddPunctuator("[~down]", TokKind.DirDownCi, goodAlias: true);

        _AddPunctuator("[key]", TokKind.DirKey);
        _AddPunctuator("[agg]", TokKind.DirAgg);
        _AddPunctuator("[group]", TokKind.DirAgg, goodAlias: true);
        _AddPunctuator("[map]", TokKind.DirMap);
        _AddPunctuator("[item]", TokKind.DirMap, goodAlias: true);
        _AddPunctuator("[auto]", TokKind.DirAuto);

        _AddPunctuator("[with]", TokKind.DirWith);
        _AddPunctuator("[guard]", TokKind.DirGuard);

        _AddPunctuator("[if]", TokKind.DirIf);
        _AddPunctuator("[while]", TokKind.DirWhile);
        _AddPunctuator("[else]", TokKind.DirElse);

        _AddPunctuator("[top]", TokKind.DirTop);

        _fixedText = fixedText;
        _puncTable = puncTable;
        _keywordTable = keywordTable;
        _keywords = keywordTable.Values.OrderBy(info => info.Rep, StringComparer.Ordinal).ToImmutableArray<FixedTokenInfo>();
        _puncs = puncTable.Values.Where(info => info != null).OrderBy(info => info.Rep, StringComparer.Ordinal).ToImmutableArray<FixedTokenInfo>();

        void _AddPunctuator(string rep, TokKind tid, bool deprecated = false, bool goodAlias = false)
        {
            Validation.AssertNonEmpty(rep);
            Validation.Assert(tid != TokKind.None);

            bool alias = deprecated || goodAlias;
            Validation.Assert(fixedText.ContainsKey(tid) == alias);

            string std = alias ? fixedText[tid] : rep;

            // We represent the chars in a ulong. We ensure an entry for each prefix, as well as the punctuator.
            // A pure prefix has a null value in the table.
            Validation.Assert(rep.Length <= _CchMaxPunc);
            bool prefix;
            ulong code = 0;
            for (int ich = 0; ;)
            {
                Validation.AssertIndex(ich, rep.Length);

                char ch = rep[ich];
                Validation.Assert(ch != 0);
                Validation.Assert(ch < (1UL << _CbitPerChar));
                ulong cur = (ulong)ch << (_CbitPerChar * ich);
                Validation.Assert((code & cur) == 0);
                code |= cur;

                // Get the current entry or default().
                prefix = puncTable.TryGetValue(code, out var info);

                // Update ich and see if we're done.
                if (++ich >= rep.Length)
                {
                    // Assert this isn't a duplicte.
                    Validation.Assert(info == null);
                    break;
                }

                // Make sure it is recorded as a prefix.
                puncTable[code] = PunctuatorInfo.SetPrefix(info);
            }

            // Add the punc info.
            puncTable[code] = new PunctuatorInfo(tid, rep, std, deprecated, prefix);

            if (!alias)
                fixedText.Add(tid, rep);
        }

        void _AddKeyword(string rep, TokKind tid, bool alias = false, bool deprecated = false)
        {
            Validation.AssertNonEmpty(rep);
            Validation.Assert(tid.IsKeyword());
            Validation.Assert(!keywordTable.ContainsKey(rep));
            Validation.Assert(fixedText.ContainsKey(tid) == alias);
            Validation.Assert(!deprecated || alias);

            string std = alias ? fixedText[tid] : rep;

            var kwi = new KeyWordInfo(tid, rep, std, deprecated);
            keywordTable.Add(rep, kwi);
            if (!alias)
                fixedText.Add(tid, rep);
        }
    }

    /// <summary>
    /// Tokenize the given <paramref name="source"/>.
    /// </summary>
    public TokenStream LexSource(SourceContext source)
    {
        Validation.BugCheckValue(source, nameof(source));
        return TokenStreamImpl.Create(this, source);
    }

    /// <summary>
    /// Returns information for all the keywords, both reserved and contextual. These are sorted
    /// by the text of the keyword.
    /// </summary>
    public Immutable.Array<FixedTokenInfo> GetKeywords()
    {
        return _keywords;
    }

    /// <summary>
    /// Returns information for all the punctuators. These are sorted
    /// by the text of the punctuator.
    /// </summary>
    public Immutable.Array<FixedTokenInfo> GetPunctuators()
    {
        return _puncs;
    }

    /// <summary>
    /// Return the string representation of the given punctuator or keyword tid.
    /// </summary>
    public string GetFixedText(TokKind tid)
    {
        Validation.BugCheckParam(_fixedText.ContainsKey(tid), nameof(tid));
        return _fixedText[tid];
    }

    /// <summary>
    /// If the given <paramref name="tid"/> is a punctuator or keyword, returns true and sets
    /// <paramref name="text"/> to its string representation.
    /// </summary>
    public bool TryGetFixedText(TokKind tid, out string text)
    {
        return _fixedText.TryGetValue(tid, out text);
    }

    /// <summary>
    /// Escapes the given string as a Rexl string literal and append it to <paramref name="sb"/>.
    /// Opening and closing quotes are included in the output.
    /// </summary>
    public void AppendTextLiteral(StringBuilder sb, string str)
    {
        Validation.AssertValue(sb);
        if (str == null)
        {
            // REVIEW: Eventually we'll want syntax for text null.
            sb.Append("ToText(null)");
            return;
        }

        sb.Append('"');
        for (int ich = 0; ich < str.Length; ich++)
        {
            char ch = str[ich];
            switch (ch)
            {
            case '"':
                sb.Append("\\\"");
                break;
            case '\\':
                sb.Append("\\\\");
                break;
            case '\'':
                sb.Append("\\'");
                break;
            case '\0':
                sb.Append("\\0");
                break;
            case '\n':
                sb.Append("\\n");
                break;
            case '\r':
                sb.Append("\\r");
                break;
            case '\t':
                sb.Append("\\t");
                break;
            case '\u0085':
                sb.Append("\\u0085");
                break;
            case '\u2028':
                sb.Append("\\u2028");
                break;
            case '\u2029':
                sb.Append("\\u2029");
                break;
            default:
                Validation.Assert(!CharUtils.IsLineTerm(ch));
                sb.Append(ch);
                break;
            }
        }

        sb.Append('"');
    }

    /// <summary>
    /// Returns whether ch is valid as the first character of an identifier (including the open quote character).
    /// </summary>
    private static bool _IsIdentStart(char ch)
    {
        if (ch >= 128)
            return (CharUtils.GetUniCatFlags(ch) & CharUtils.UniCatFlags.IdentStartChar) != 0;
        return ((uint)(ch - 'a') < 26) || ((uint)(ch - 'A') < 26) || (ch == '_') || (ch == IdentQuoteOpen);
    }

    /// <summary>
    /// Returns whether ch is a valid (non-quoted) identifier character.
    /// </summary>
    private static bool _IsIdentCh(char ch)
    {
        if (ch >= 128)
            return (CharUtils.GetUniCatFlags(ch) & CharUtils.UniCatFlags.IdentPartChar) != 0;
        return ((uint)(ch - 'a') < 26) || ((uint)(ch - 'A') < 26) || ((uint)(ch - '0') <= 9) || (ch == '_');
    }

    private sealed class TokenStreamImpl : TokenStream
    {
        private readonly TokenTuple _tokens;

        public override TokenTuple Tokens => _tokens;

        private TokenStreamImpl(RexlLexer lexer, SourceContext source)
            : base(source)
        {
            LexerImpl impl = new LexerImpl(lexer, this, source.Text);

            var tokens = Immutable.Array.CreateBuilder<Token>();
            Token tok;
            while ((tok = impl.GetNextToken()) != null)
                tokens.Add(tok);

            tokens.Add(impl.GetEof());

            _tokens = tokens.ToImmutable();
        }

        public static TokenStreamImpl Create(RexlLexer lexer, SourceContext source)
        {
            return new TokenStreamImpl(lexer, source);
        }

        private sealed class LexerImpl
        {
            private readonly RexlLexer _lex;
            private readonly TokenStreamImpl _stream;
            private readonly string _source;
            private readonly int _cch;

            // Workspace for building current token.
            private readonly StringBuilder _sb;

            // Current position in the source text.
            private int _ichCur;
            // The start of the current token.
            private int _ichMinTok;

            private int _index;

            public LexerImpl(RexlLexer lex, TokenStreamImpl stream, string source)
            {
                Validation.AssertValue(lex);
                Validation.AssertValue(stream);
                Validation.AssertValue(source);

                _lex = lex;
                _stream = stream;
                _source = source;
                _cch = _source.Length;
                _sb = new StringBuilder();
            }

            /// <summary>
            /// Whether we've hit the end of input yet. If this returns true, _ChCur will be zero.
            /// </summary>
            private bool _Eof
            {
                get
                {
                    Validation.AssertIndexInclusive(_ichCur, _cch);
                    return _ichCur >= _cch;
                }
            }

            /// <summary>
            /// The current character. Zero if we've hit the end of input.
            /// </summary>
            private char _ChCur
            {
                get
                {
                    Validation.AssertIndexInclusive(_ichCur, _cch);
                    return _ichCur < _cch ? _source[_ichCur] : '\0';
                }
            }

            /// <summary>
            /// Advance to the next character.
            /// </summary>
            private void _Eat()
            {
                Validation.AssertIndex(_ichCur, _cch);
                _ichCur += 1;
            }

            /// <summary>
            /// Advance the given number of characters.
            /// </summary>
            private void _Eat(int cch)
            {
                Validation.AssertIndexInclusive(cch, _cch - _ichCur);
                _ichCur += cch;
            }

            /// <summary>
            /// Return the current character and advance to the next character.
            /// </summary>
            private char _ChEat()
            {
                Validation.AssertIndex(_ichCur, _cch);
                _ichCur += 1;
                return _source[_ichCur - 1];
            }

            /// <summary>
            /// Advance to the next character and returns it.
            /// </summary>
            private char _ChNext()
            {
                Validation.AssertIndex(_ichCur, _cch);
                _ichCur += 1;
                return _ChCur;
            }

            /// <summary>
            /// Return the character ich positions forward, without advancing the current position.
            /// </summary>
            private char _ChPeek(int ich = 1)
            {
                Validation.AssertIndexInclusive(ich, _cch - _ichCur);
                ich += _ichCur;
                return ich < _cch ? _source[ich] : '\0';
            }

            /// <summary>
            /// Records the current position as the beginning of the current token.
            /// </summary>
            private void _StartToken()
            {
                Validation.AssertIndexInclusive(_ichCur, _cch);
                _ichMinTok = _ichCur;
            }

            /// <summary>
            /// Returns the source text of the current token.
            /// </summary>
            private string _GetTokText()
            {
                Validation.AssertIndexInclusive(_ichCur, _cch);
                Validation.AssertIndexInclusive(_ichMinTok, _ichCur);
                return _source.Substring(_ichMinTok, _ichCur - _ichMinTok);
            }

            /// <summary>
            /// Form and return the next token. Returns null to signal end of input.
            /// </summary>
            public Token GetNextToken()
            {
                for (; ; )
                {
                    if (_Eof)
                        return null;
                    Token tok = _LexToken();
                    if (tok != null)
                        return tok;
                }
            }

            /// <summary>
            /// Creates an Eof token. Typically this is called by the client code once GetNextToken returns null,
            /// if it needs an Eof token.
            /// </summary>
            public EofToken GetEof()
            {
                Validation.Assert(_Eof);
                return new EofToken(_stream, _index++, _cch, _cch);
            }

            /// <summary>
            /// Lexes the next token. Generally, null returns should be skipped, since they represent space.
            /// </summary>
            private Token _LexToken()
            {
                _StartToken();

                char ch = _ChCur;

                switch (ch)
                {
                case '0':
                    switch (_ChPeek(1))
                    {
                    case 'x':
                    case 'X':
                        return _LexHexLit();
                    case 'b':
                    case 'B':
                        return _LexBinLit();
                    }
                    return _LexDecLit();
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return _LexDecLit();

                case '.':
                    if (CharUtils.IsDecDigit(_ChPeek(1)))
                        return _LexDecLit();
                    break;

                case '"':
                    // REVIEW: What should the default be for isSlash?
                    return _LexTextLit(isSlash: true);
                case 'x':
                    if (_ChPeek(1) == '"')
                    {
                        _Eat();
                        return _LexTextLit(isSlash: true);
                    }
                    break;
                case '@':
                    if (_ChPeek(1) == '"')
                    {
                        // Multi-line verbatim text.
                        _Eat();
                        return _LexTextLit(isSlash: false, multiLine: true);
                    }
                    break;
                case 'r':
                    if (_ChPeek(1) == '"')
                    {
                        // Verbatim text without multi-line.
                        _Eat();
                        return _LexTextLit(isSlash: false, multiLine: false);
                    }
                    break;

                case '#':
                    if (CharUtils.IsDecDigit(_ChPeek()))
                    {
                        _Eat();
                        int slot = _LexSlot();
                        return new HashSlotToken(_stream, _index++, _ichMinTok, _ichCur, slot);
                    }
                    break;
                }

                if (_IsIdentStart(ch))
                    return _LexIdent();
                if (CharUtils.IsSpace(ch) || CharUtils.IsLineTerm(ch))
                    return _LexSpace();

                return _LexPunc();
            }

            private Token _LexPunc()
            {
                // Last recorded punctuator length and kind.
                int cchGood = 0;
                PunctuatorInfo infoGood = null;
                ulong code = 0;
                for (int ichCur = 0; ;)
                {
                    // Not max length sequences should be marked as a prefix.
                    Validation.Assert(ichCur < _CchMaxPunc);

                    char ch = _ChPeek(ichCur);
                    if (ch == 0)
                        break;

                    // If the character doesn't fit in our encoding scheme, then it's clearly not
                    // part of a punctuator.
                    if ((ulong)ch >= (1UL << _CbitPerChar))
                        break;

                    ulong cur = (ulong)ch << (_CbitPerChar * ichCur);
                    Validation.Assert((code & cur) == 0);
                    code |= cur;

                    if (!_lex._puncTable.TryGetValue(code, out var infoCur))
                        break;
                    ichCur++;

                    // A null value in _puncTable indicates a pure prefix with no associated tid.
                    if (infoCur == null)
                        continue;

                    Validation.Assert(infoCur.Tid != TokKind.None);
                    switch (infoCur.Tid)
                    {
                    case TokKind.CommentBlock:
                    case TokKind.CommentLine:
                        // Comment sequence should not be a prefix for punctuators.
                        Validation.Assert(!infoCur.Prefix);
                        // Consume the start sequence.
                        _Eat(ichCur);
                        return _LexComment(infoCur.Tid);
                    }
                    cchGood = ichCur;
                    infoGood = infoCur;
                    if (!infoGood.Prefix)
                        break;
                }

                Validation.Assert((cchGood > 0) == (infoGood != null));
                if (cchGood > 0)
                {
                    // Consume the characters.
                    _Eat(cchGood);
                    return new PuncToken(_stream, _index++, infoGood, _ichMinTok, _ichCur);
                }

                // Bad character.
                _Eat();
                return new ErrorToken(_stream, _index++, _ichMinTok, _ichCur, _GetTokText());
            }

            private Token _LexHexLit()
            {
                Validation.Assert(_ChCur == '0' && (_ChPeek(1) == 'x' || _ChPeek(1) == 'X'));
                _Eat(2);

                // REVIEW: Should we allow adjacent _? We currently don't.
                var flags = IntLitFlags.Hex;
                Integer value = 0;
                bool lastGood = false;
                bool err = false;
                for (; ; _Eat())
                {
                    char ch = _ChCur;
                    if (CharUtils.IsHexDigit(ch, out uint cur))
                    {
                        Validation.Assert(cur < 0x10);
                        value = 16 * value + cur;
                        lastGood = true;
                    }
                    else if (ch == '_')
                    {
                        err |= !lastGood;
                        lastGood = false;
                    }
                    else
                        break;
                }
                err |= !lastGood;

                if (err)
                    flags |= IntLitFlags.Error;

                _LexNumTypeSuffix(false, true, ref flags, out var size, out var isFloat);
                Validation.Assert(!isFloat);
                return new IntLitToken(_stream, _index++, value, _ichMinTok, _ichCur, flags, size);
            }

            private Token _LexBinLit()
            {
                Validation.Assert(_ChCur == '0' && (_ChPeek(1) == 'b' || _ChPeek(1) == 'B'));
                _Eat(2);

                // REVIEW: Should we allow adjacent _? We currently don't.
                var flags = IntLitFlags.Bin;
                Integer value = 0;
                bool lastGood = false;
                bool err = false;
                for (; ; _Eat())
                {
                    char ch = _ChCur;
                    if (ch == '0' || ch == '1')
                    {
                        value *= 2;
                        if (ch == '1')
                            value += 1;
                        lastGood = true;
                    }
                    else if (ch == '_')
                    {
                        err |= !lastGood;
                        lastGood = false;
                    }
                    else
                        break;
                }
                err |= !lastGood;

                if (err)
                    flags |= IntLitFlags.Error;

                _LexNumTypeSuffix(false, true, ref flags, out var size, out var isFloat);
                Validation.Assert(!isFloat);
                return new IntLitToken(_stream, _index++, value, _ichMinTok, _ichCur, flags, size);
            }

            /// <summary>
            /// Gather decimal digits into <see cref="_sb"/>. Returns false if there are no digits or
            /// the first or last char is a separator or there are adjacent separators.
            /// </summary>
            private bool _TryGatherDigits()
            {
                Validation.Assert(CharUtils.IsDecDigit(_ChCur));

                // REVIEW: Should we allow adjacent _? We currently don't.
                bool lastGood = false;
                bool err = false;
                for (; ; _Eat())
                {
                    char ch = _ChCur;
                    if (CharUtils.IsDecDigit(ch, out uint cur))
                    {
                        Validation.Assert(cur < 10);
                        _sb.Append(ch);
                        lastGood = true;
                    }
                    else if (ch == '_')
                    {
                        err |= !lastGood;
                        lastGood = false;
                    }
                    else
                        break;
                }
                err |= !lastGood;

                return !err;
            }

            /// <summary>
            /// Lex a decimal integer or double literal.
            /// </summary>
            private Token _LexDecLit()
            {
                Validation.Assert(CharUtils.IsDecDigit(_ChCur) || _ChCur == '.' && CharUtils.IsDecDigit(_ChPeek(1)));

                // Gather characters here, for later parsing by Integer or double.
                _sb.Clear();

                // Lex the portion before the decimal / exponent.
                var flags = IntLitFlags.None;

                bool hasError = false;
                if (_ChCur != '.')
                {
                    Validation.Assert(CharUtils.IsDecDigit(_ChCur));
                    hasError |= !_TryGatherDigits();
                }
                int ichLimPre = _sb.Length;

                // Require that '.' is followed by a digit.
                bool hasDot = _ChCur == '.' && CharUtils.IsDecDigit(_ChPeek(1));
                if (hasDot)
                {
                    // Handle the fractional part.
                    _sb.Append(_ChEat());
                    hasError |= !_TryGatherDigits();
                }

                int ichMinExp = _sb.Length;
                char ch;
                bool hasExp = (_ChCur == 'e' || _ChCur == 'E') && (CharUtils.IsDecDigit(ch = _ChPeek()) || (ch == '+' || ch == '-') && CharUtils.IsDecDigit(_ChPeek(2)));
                if (hasExp)
                {
                    ichMinExp++;
                    _sb.Append(_ChEat());
                    if ((ch = _ChCur) == '+' || ch == '-')
                    {
                        _sb.Append(ch);
                        _Eat();
                    }

                    Validation.Assert(CharUtils.IsDecDigit(_ChCur));
                    hasError |= !_TryGatherDigits();
                }

                // Look for a numeric suffix.
                _LexNumTypeSuffix(hasDot || hasExp, false, ref flags, out var size, out var hasFloat);

                if (!(hasDot | hasExp | hasFloat))
                {
                    if (hasError)
                        flags |= IntLitFlags.Error;
                    if (Integer.TryParse(_sb.ToString(), out var value))
                        return new IntLitToken(_stream, _index++, value, _ichMinTok, _ichCur, flags, size);

                    // REVIEW: I don't think we can get here (unless there is a bug above).
                    Validation.Assert(false);
                    return new ErrorToken(_stream, _index++, _ichMinTok, _ichCur, _GetTokText(), "Can't parse big integer");
                }
                else
                {
                    if (double.TryParse(_sb.ToString(), out double val))
                        return new FltLitToken(_stream, _index++, val, _ichMinTok, _ichCur, size, hasError);

                    // double.TryParse returns false on overflow to infinity, but not underflow to zero. It's not clear we want to do that.
                    // It's very likely that parsing failed because of overflow, but we do some sanity checks before concluding that, in
                    // case something else is amiss (which it shouldn't be).
                    // REVIEW: With .Net Core 3.0 and later, we don't get here - instead double.TryParse succeeds and produces
                    // infinity, as it should. However, .Net Framework still fails above.
                    if (hasExp)
                    {
                        Validation.Assert(ichMinExp < _sb.Length);
                        int ichMin = ichMinExp;
                        if (_sb[ichMin] == '+')
                            ichMin++;
                        while (ichMin < _sb.Length && _sb[ichMin] == '0')
                            ichMin++;
                        if (ichMin < _sb.Length && CharUtils.IsDecDigit(_sb[ichMin]))
                            return new FltLitToken(_stream, _index++, double.PositiveInfinity, _ichMinTok, _ichCur, size, hasError);
                    }
                    else if (ichLimPre > 300)
                    {
                        // Lots of digits before the decimal point.
                        int ichMin = 0;
                        while (ichMin < ichLimPre && _sb[ichMin] == '0')
                            ichMin++;
                        if (ichMin < ichLimPre && CharUtils.IsDecDigit(_sb[ichMin]))
                            return new FltLitToken(_stream, _index++, double.PositiveInfinity, _ichMinTok, _ichCur, size, hasError);
                    }

                    // REVIEW: I don't think we can get here (unless there is a bug above).
                    Validation.Assert(false);
                    return new ErrorToken(_stream, _index++, _ichMinTok, _ichCur, _GetTokText(), "Can't parse floating point");
                }
            }

            private void _LexNumTypeSuffix(bool skipIntLex, bool skipFloatLex, ref IntLitFlags flags, out NumLitSize size, out bool isFloat)
            {
                isFloat = false;

                char ch;
                char chType = char.ToLowerInvariant(_ChCur);
                switch (chType)
                {
                case 'u':
                    if (skipIntLex)
                        goto default;
                    flags |= IntLitFlags.Unsigned;
                    ch = _ChNext();
                    if (ch == 'l' || ch == 'L')
                    {
                        size = NumLitSize.EightBytes;
                        _Eat();
                        return;
                    }
                    break;
                case 'l':
                    if (skipIntLex)
                        goto default;
                    size = NumLitSize.EightBytes;
                    ch = _ChNext();
                    if (ch == 'u' || ch == 'U')
                    {
                        flags |= IntLitFlags.Unsigned;
                        _Eat();
                    }
                    return;
                case 'i':
                    if (skipIntLex)
                        goto default;
                    _Eat();
                    break;
                case 'r':
                    if (skipFloatLex)
                        goto default;
                    isFloat = true;
                    _Eat();
                    break;
                case 'f':
                    if (skipFloatLex)
                        goto default;
                    isFloat = true;
                    size = NumLitSize.FourBytes;
                    _Eat();
                    return;
                case 'd':
                    if (skipFloatLex)
                        goto default;
                    isFloat = true;
                    size = NumLitSize.EightBytes;
                    _Eat();
                    return;
                default:
                    isFloat = false;
                    size = NumLitSize.Unspecified;
                    return;
                }

                switch (_ChCur)
                {
                case '1':
                    size = NumLitSize.OneByte;
                    _Eat();
                    break;
                case '2':
                    size = NumLitSize.TwoBytes;
                    _Eat();
                    break;
                case '4':
                    size = NumLitSize.FourBytes;
                    _Eat();
                    break;
                case '8':
                    size = NumLitSize.EightBytes;
                    _Eat();
                    break;
                case 'a':
                case 'A':
                    if (chType == 'i')
                    {
                        size = NumLitSize.UnlimitedSize;
                        _Eat();
                    }
                    else
                        size = NumLitSize.Unspecified;
                    break;
                default:
                    size = NumLitSize.Unspecified;
                    break;
                }
            }

            /// <summary>
            /// Lex an identifier or keyword.
            /// </summary>
            private Token _LexIdent()
            {
                Validation.Assert(_IsIdentStart(_ChCur));

                IdentFlags flags;
                string str;

                if (_ChCur == IdentQuoteOpen)
                {
                    // Delimited identifier, is like a verbatim string, but with a different closing character.
                    // REVIEW: Should we allow escapes in quoted identifiers?
                    flags = IdentFlags.QuoteOpen;
                    str = _LexTextCore(IdentQuoteClose, out bool hasClose);
                    if (hasClose)
                        flags |= IdentFlags.QuoteClose;
                }
                else
                {
                    // Unquoted identifier or keyword.
                    Validation.Assert(_IsIdentCh(_ChCur));

                    int ichMin = _ichCur;
                    while (_IsIdentCh(_ChCur))
                        _Eat();

                    str = _source.Substring(ichMin, _ichCur - ichMin);
                    flags = IdentFlags.None;

                    if (str == "it" && _ChCur == '$')
                    {
                        _Eat();
                        int slot = _LexSlot();
                        return new ItSlotToken(_stream, _index++, _ichMinTok, _ichCur, slot);
                    }

                    // See if it's a keyword.
                    if (_lex._keywordTable.TryGetValue(str, out var info))
                    {
                        int index = _index++;
                        var kt = new KeyToken(_stream, index, info, _ichMinTok, _ichCur);
                        if (info.Tid.IsContextualKwd())
                            return new IdentToken(_stream, index, str, _ichMinTok, _ichCur, flags, kt);
                        return kt;
                    }

                    // See if it's a mis-cased keyword, contextual or not.
                    if (_lex._keywordTable.TryGetValue(str.ToLowerInvariant(), out info))
                    {
                        int index = _index++;
                        var kt = new KeyToken(_stream, index, info, _ichMinTok, _ichCur);
                        return new IdentToken(_stream, index, str, _ichMinTok, _ichCur, flags, kt, altFuzzy: true);
                    }
                }

                return new IdentToken(_stream, _index++, str, _ichMinTok, _ichCur, flags);
            }

            private int _LexSlot()
            {
                Integer slot = 0;
                while (CharUtils.IsDecDigit(_ChCur, out uint value))
                {
                    slot = slot * 10 + value;
                    _Eat();
                }
                return slot > int.MaxValue ? int.MaxValue : (int)slot;
            }

            /// <summary>
            /// Lex a string literal.
            /// </summary>
            private Token _LexTextLit(bool isSlash, bool multiLine = false)
            {
                Validation.Assert(_ChCur == '"');
                Validation.Assert(!isSlash | !multiLine);
                var flags = TextLitFlags.None;

                bool hasClose;
                int ichBad = -1;
                string str = isSlash ? _LexSlashTextCore('"', out hasClose, out ichBad) : _LexTextCore('"', out hasClose, multiLine);
                if (!hasClose)
                    flags |= TextLitFlags.Unterminated;
                if (ichBad >= 0)
                    flags |= TextLitFlags.BadEscape;

                return new TextLitToken(_stream, _index++, str, _ichMinTok, _ichCur, flags, ichBad);
            }

            /// <summary>
            /// This lexes both quoted text and quoted identifiers. The item ends when either an un-escaped chClose is seen, or
            /// an eof or eol is encountered. In the latter case, the item is deemed un-terminated.
            /// </summary>
            private string _LexTextCore(char chClose, out bool hasClose, bool multiLine = false)
            {
                // Gather the characters in the builder. Note that the string may have escapes in it,
                // so is not necessarily a direct substring of _text.
                _sb.Clear();
                for (; ; )
                {
                    char ch = _ChNext();
                    if (_Eof)
                    {
                        // Un-terminated.
                        hasClose = false;
                        return _sb.ToString();
                    }

                    if (CharUtils.IsLineTerm(ch))
                    {
                        if (!multiLine)
                        {
                            // Un-terminated.
                            hasClose = false;
                            return _sb.ToString();
                        }
                        // REVIEW: This records CRLF as just LF and leaves others alone. Is this a good policy?
                        if (ch == '\x0C' && _ChPeek() == '\x0A')
                            ch = _ChNext();
                    }
                    else if (ch == chClose && _ChNext() != chClose)
                    {
                        // Properly terminated.
                        hasClose = true;
                        return _sb.ToString();
                    }

                    _sb.Append(ch);
                }
            }

            /// <summary>
            /// This lexes both quoted text and quoted identifiers with possible back-slash style escapes.
            /// The item ends when either an un-escaped chClose is seen, or an eof or eol is encountered.
            /// In the latter case, the item is deemed un-terminated. If there is one or more ill-formed
            /// escape sequence, <paramref name="ichBad"/> is set to its offset relative to the start of
            /// the token. Otherwise, <paramref name="ichBad"/> is set to -1.
            /// </summary>
            private string _LexSlashTextCore(char chClose, out bool hasClose, out int ichBad)
            {
                int? ichErr = null;

                // Gather the characters in the builder. Note that the string may have escapes in it,
                // so is not necessarily a direct substring of _text.
                _sb.Clear();
                for (; ; )
                {
                    char ch = _ChNext();
                    if (CharUtils.IsLineTerm(ch) || _Eof)
                    {
                        // Un-terminated.
                        hasClose = false;
                        ichBad = ichErr ?? -1;
                        return _sb.ToString();
                    }

                    if (ch == chClose)
                    {
                        // REVIEW: Should we make doubled chClose an escape in this form? Could help users.
                        if (_ChNext() != chClose)
                        {
                            // Properly terminated.
                            hasClose = true;
                            ichBad = ichErr ?? -1;
                            return _sb.ToString();
                        }

                        // Record the chClose character.
                        Validation.Assert(ch == chClose);
                    }
                    else if (ch == '\\')
                    {
                        // Escape sequence.
                        ch = _ChNext();
                        switch (ch)
                        {
                        default:
                            // REVIEW: Record the bad escape location.
                            ichErr ??= _ichCur - _ichMinTok;
                            if (_Eof)
                            {
                                // Un-terminated with bad escape.
                                _sb.Append('\\');
                                hasClose = false;
                                ichBad = ichErr.GetValueOrDefault();
                                return _sb.ToString();
                            }
                            break;

                        case '\\':
                        case '\'':
                        case '"':
                            break;

                        case '0':
                            // Null character.
                            ch = default;
                            break;

                        // REVIEW: C# supports a, b, f, and v. Doesn't seem like we need/want them.
                        case 'n':
                            ch = '\n';
                            break;
                        case 'r':
                            ch = '\r';
                            break;
                        case 't':
                            ch = '\t';
                            break;

                        case 'x':
                            // REVIEW: Unlike C#, we allow/require exactly two hex digits.
                            {
                                if (!CharUtils.IsHexDigit(_ChPeek(1), out uint b) || !CharUtils.IsHexDigit(_ChPeek(2), out uint a))
                                {
                                    ichErr ??= _ichCur - _ichMinTok;
                                    break;
                                }

                                ch = (char)((b << 4) | a);
                                _Eat(2);
                                break;
                            }
                        case 'u':
                            {
                                if (!CharUtils.IsHexDigit(_ChPeek(1), out uint d) || !CharUtils.IsHexDigit(_ChPeek(2), out uint c) ||
                                    !CharUtils.IsHexDigit(_ChPeek(3), out uint b) || !CharUtils.IsHexDigit(_ChPeek(4), out uint a))
                                {
                                    ichErr ??= _ichCur - _ichMinTok;
                                    break;
                                }

                                ch = (char)((d << 12) | (c << 8) | (b << 4) | a);
                                _Eat(4);
                                break;
                            }
                        case 'U':
                            // REVIEW: Unlike C#, we don't currently implement this form. We do "reserve" it as we might want it in the future.
                            ichErr ??= _ichCur - _ichMinTok;
                            break;
                        }
                    }

                    _sb.Append(ch);
                }
            }

            /// <summary>
            /// Lex a sequence of space characters. Always returns null.
            /// </summary>
            private Token _LexSpace()
            {
                Validation.Assert(CharUtils.IsSpace(_ChCur) || CharUtils.IsLineTerm(_ChCur));
                while (CharUtils.IsSpace(_ChNext()) || CharUtils.IsLineTerm(_ChCur))
                {
                }
                return null;
            }

            /// <summary>
            /// Lex a line or block comment.
            /// </summary>
            private Token _LexComment(TokKind tid)
            {
                Validation.Assert(tid == TokKind.CommentBlock || tid == TokKind.CommentLine);

                if (tid == TokKind.CommentBlock)
                {
                    // The caller should have consumed the starting sequence.
                    Validation.Assert(_ichCur == _ichMinTok + 2);

                    // REVIEW: It would be nice if we didn't have to hard code the comment terminator.
                    // REVIEW: If/when we record line mapping, we'll need to adjust for line terminators,
                    // and be careful about multi-character sequences, like CRLF.
                    while (!_Eof && (_ChCur != '*' || _ChPeek(1) != '/'))
                        _Eat();
                    if (_Eof)
                        return new ErrorToken(_stream, _index++, _ichMinTok, _ichCur, _GetTokText(), "Unterminated comment");
                    _Eat(2);
                }
                else
                {
                    while (!CharUtils.IsLineTerm(_ChCur) && !_Eof)
                        _Eat();
                    // Don't include the line terminator(s) in the comment token.
                }

                return new CommentToken(_stream, _index++, tid, _ichMinTok, _ichCur);
            }
        }
    }
}
