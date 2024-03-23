// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Lexer for cells contents that may involve multiple languages and/or commands.
/// </summary>
public class CellLexer
{
    /// <summary>
    /// These are the token kinds.
    /// </summary>
    public enum TokenKind
    {
        /// <summary>
        /// Executable code handled by a language "handler".
        /// </summary>
        Code,

        /// <summary>
        /// A command token. This does not include arguments for the command.
        /// </summary>
        Cmd,

        /// <summary>
        /// Arguments for a command. One of these will only immediately follow a <see cref="Cmd"/> token.
        /// Note that the args are not individually parsed - this contains the text to the next line terminator,
        /// but not leading white space.
        /// </summary>
        Args,
    }

    /// <summary>
    /// A token.
    /// </summary>
    public abstract class Token
    {
        protected string _text;

        public TokenKind Kind { get; }

        public int Min { get; }
        public int Lim { get; }

        private protected Token(TokenKind kind, string text, int min, int lim)
        {
            Validation.AssertValue(text);
            Validation.AssertIndex(min, lim);
            Validation.AssertIndexInclusive(lim, text.Length);

            Kind = kind;
            _text = text;
            Min = min;
            Lim = lim;
        }
    }

    /// <summary>
    /// A token of kind <see cref="TokenKind.Code"/>, representing executable code.
    /// </summary>
    public sealed class CodeToken : Token
    {
        public CodeToken(string text, int min, int lim)
            : base(TokenKind.Code, text, min, lim)
        {
        }
    }

    /// <summary>
    /// A token of kind <see cref="TokenKind.Cmd"/>, representing a command name (but not args).
    /// </summary>
    public sealed class CmdToken : Token
    {
        public string Name { get; }

        public CmdToken(string text, int min, int lim)
            : base(TokenKind.Cmd, text, min, lim)
        {
            // The text should start with "#!" and contain at least one more character.
            Validation.Assert(Lim - Min > 2);
            Name = _text[(Min + 2)..Lim];
        }
    }

    /// <summary>
    /// A token of kind <see cref="TokenKind.Args"/>, representing command args as raw text.
    /// </summary>
    public sealed class ArgsToken : Token
    {
        public string Value { get; }

        public ArgsToken(string text, int min, int lim)
            : base(TokenKind.Args, text, min, lim)
        {
            Value = _text[Min..Lim];
        }
    }

    /// <summary>
    /// The text being lexed.
    /// </summary>
    private readonly string _text;

    /// <summary>
    /// The length of text being tokenized.
    /// </summary>
    private readonly int _lim;

    /// <summary>
    /// The min position of the token being processed.
    /// </summary>
    private int _min;

    /// <summary>
    /// The current limit position of the token being processed.
    /// </summary>
    private int _cur;

    /// <summary>
    /// The tokens being produced.
    /// </summary>
    private readonly List<Token> _tokens;

    private CellLexer(string text)
    {
        _text = text;
        _lim = _text.Length;
        _tokens = new();
        _min = _cur = 0;
    }

    /// <summary>
    /// Run the lexer and produce a list of tokens.
    /// </summary>
    public static IReadOnlyList<Token> Run(string text)
    {
        var impl = new CellLexer(text);
        return impl.LexCore();
    }

    private IReadOnlyList<Token> LexCore()
    {
        Validation.Assert(_min == 0);
        Validation.Assert(_cur == 0);
        Validation.Assert(_tokens.Count == 0);

        bool newLine = true;
        while (_cur < _lim)
        {
            // A command must be at the beginning of a line.
            if (newLine && IsCmd())
            {
                Flush(TokenKind.Code);
                LexCmd();
                SkipSpace();
                LexCmdArgs();
                SkipEol();
            }
            else
            {
                newLine = IsLineTerm(_text[_cur]);
                _cur++;
            }
        }

        Flush(TokenKind.Code);
        return _tokens;
    }

    /// <summary>
    /// A command must start with "#!" and a non-white-space character.
    /// </summary>
    private bool IsCmd()
    {
        if (_cur + 2 >= _lim)
            return false;
        if (_text[_cur] != '#')
            return false;
        if (_text[_cur + 1] != '!')
            return false;
        if (char.IsWhiteSpace(_text[_cur + 2]))
            return false;

        return true;
    }

    /// <summary>
    /// Stop at the first white-space character.
    /// </summary>
    private void LexCmd()
    {
        Validation.Assert(_min == _cur);
        Validation.Assert(IsCmd());

        while (_cur < _lim && !char.IsWhiteSpace(_text[_cur]))
            _cur++;
        Flush(TokenKind.Cmd);
    }

    /// <summary>
    /// Stop at the first new-line character.
    /// </summary>
    private void LexCmdArgs()
    {
        Validation.Assert(_min == _cur);

        while (_cur < _lim && !IsLineTerm(_text[_cur]))
            _cur++;
        Flush(TokenKind.Args);
    }

    /// <summary>
    /// Skip white-space that isn't a line terminator.
    /// </summary>
    private void SkipSpace()
    {
        Validation.Assert(_min == _cur);

        char ch;
        while (_cur < _lim && char.IsWhiteSpace(ch = _text[_cur]) && !IsLineTerm(ch))
            _cur++;
        _min = _cur;
    }

    /// <summary>
    /// Skip a single line terminator, treating CRLF as one.
    /// </summary>
    private void SkipEol()
    {
        Validation.Assert(_min == _cur);

        if (_cur >= _lim)
            return;

        switch (_text[_cur])
        {
        case '\r': // carriage return, unicode 0x000D
            _cur++;
            if (_cur < _lim && _text[_cur] == '\n')
                _cur++;
            break;
        case '\n': // line feed, unicode 0x000A
        case '\u0085': // Unicode next line
        case '\u2028': // Unicode line separator
        case '\u2029': // Unicode paragraph separator
            _cur++;
            break;
        }
        _min = _cur;
    }

    /// <summary>
    /// Return whether the given character is a line terminator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLineTerm(char ch)
    {
        switch (ch)
        {
        case '\n': // line feed, unicode 0x000A
        case '\r': // carriage return, unicode 0x000D
        case '\u0085': // Unicode next line
        case '\u2028': // Unicode line separator
        case '\u2029': // Unicode paragraph separator
            return true;
        }
        return false;
    }

    /// <summary>
    /// If the current text range is non-empty, emit a token of the given kind.
    /// </summary>
    private void Flush(TokenKind kind)
    {
        Validation.AssertIndexInclusive(_min, _cur);
        Validation.AssertIndexInclusive(_cur, _lim);

        if (_cur > _min)
        {
            _tokens.Add(CreateToken(kind, _text, _min, _cur));
            _min = _cur;
        }
    }

    /// <summary>
    /// Create a token of the given kind.
    /// </summary>
    private static Token CreateToken(TokenKind kind, string text, int min, int lim)
    {
        Validation.AssertValue(text);
        Validation.AssertIndex(min, lim);
        Validation.AssertIndexInclusive(lim, text.Length);

        switch (kind)
        {
        default:
            Validation.Assert(kind == TokenKind.Code);
            return new CodeToken(text, min, lim);
        case TokenKind.Cmd:
            return new CmdToken(text, min, lim);
        case TokenKind.Args:
            return new ArgsToken(text, min, lim);
        }
    }
}
