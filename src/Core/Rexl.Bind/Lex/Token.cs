// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Lex;

using Integer = System.Numerics.BigInteger;
using TokenTuple = Immutable.Array<Token>;

/// <summary>
/// Represents a stream of tokens. Wraps a <see cref="SourceContext"/> and an
/// immutable array of <see cref="Token"/>. Each token points back to this same
/// <see cref="TokenStream"/>. The primary value of this, the reason we don't just
/// use an immutable array of token, is that this makes it impossible to create a
/// token stream with tokens from multiple source contexts.
/// </summary>
public abstract class TokenStream : IEnumerable<Token>
{
    /// <summary>
    /// The source context.
    /// </summary>
    public SourceContext Source { get; }

    /// <summary>
    /// The tokens.
    /// </summary>
    public abstract TokenTuple Tokens { get; }

    public int Length => Tokens.Length;

    public Token this[int index] => Tokens[index];

    public IEnumerator<Token> GetEnumerator() => Tokens.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private protected TokenStream(SourceContext source)
    {
        Validation.AssertValue(source);
        Source = source;
    }
}

/// <summary>
/// Token base class. Note that all token instances are manufactured by the lexer, so all
/// token classes have internal constructors.
/// </summary>
public abstract class Token
{
    /// <summary>
    /// The <see cref="TokenStream"/> that this token belongs to.
    /// </summary>
    public TokenStream Stream { get; }

    /// <summary>
    /// Whether this token has any diagnostic information, like errors or warnings.
    /// </summary>
    public abstract bool HasDiagnostics { get; }

    /// <summary>
    /// This is an index for the token. It is only meaningful to compare indices for tokens
    /// in the same token stream.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The token kind.
    /// </summary>
    public TokKind Kind { get; }

    /// <summary>
    /// The character range of this token in the source code.
    /// </summary>
    public SourceRange Range { get; }

    /// <summary>
    /// Whether this token has a distinct "alt" form. The "alt" form is a keyword,
    /// possibly with a different casing.
    /// </summary>
    public bool HasAlt { get; }

    /// <summary>
    /// An alternative token. Usually used for parsing contextual operators but may contain
    /// fuzzy matches of other keywords.
    /// </summary>
    public Token TokenAlt { get; }

    /// <summary>
    /// Whether the alternative token is a fuzzy match (e.g. lexer identified mis-casing) to a token
    /// as opposed to a direct match.
    /// </summary>
    public bool AltFuzzy { get; }

    private protected Token(TokenStream stream, int index, TokKind tid, int ichMin, int ichLim)
    {
        Validation.AssertValue(stream);
        Validation.Assert(index >= 0);
        Validation.Assert(tid.IsValid());

        Stream = stream;
        Index = index;
        Kind = tid;
        Range = new SourceRange(stream.Source, ichMin, ichLim);
        TokenAlt = this;
    }

    private protected Token(TokenStream stream, int index, TokKind tid, int ichMin, int ichLim, Token tokAlt, bool altFuzzy)
    {
        Validation.AssertValue(stream);
        Validation.Assert(index >= 0);
        Validation.Assert(tid.IsValid());
        Validation.AssertValueOrNull(tokAlt);
        Validation.Assert(!altFuzzy || tokAlt != null);

        Stream = stream;
        Index = index;
        Kind = tid;
        Range = new SourceRange(stream.Source, ichMin, ichLim);
        if (tokAlt != null)
        {
            TokenAlt = tokAlt;
            HasAlt = true;
        }
        else
        {
            TokenAlt = this;
            HasAlt = false;
        }
        AltFuzzy = altFuzzy;
    }

    /// <summary>
    /// Asserts that the object is in fact of type T before casting.
    /// </summary>
    internal T Cast<T>() where T : Token
    {
        Validation.Assert(this is T);
        return (T)this;
    }

    /// <summary>
    /// Returns "this as T". Result is null if this token is not of that type.
    /// </summary>
    public T As<T>() where T : Token
    {
        return this as T;
    }

    public override string ToString()
    {
        return Kind.ToString();
    }

    /// <summary>
    /// Renders for diagnostic display of a token, not for end users to see.
    /// </summary>
    public virtual string Render()
    {
        if (TokenAlt != this)
            return string.Format("Range={2}, Tid={0}, TidAlt=[{1}], AltFuzzy={4}, Text=[{3}]", Kind, TokenAlt.Kind, Range, Range.GetFragment(), AltFuzzy);
        return string.Format("Range={1}, Tid={0}, Text=[{2}]", Kind, Range, Range.GetFragment());
    }

    public virtual string GetTextString()
    {
        return ToString();
    }

    public virtual string GetStdString()
    {
        return GetTextString();
    }
}

/// <summary>
/// Base class for KeyToken and PuncToken.
/// </summary>
public abstract class FixedToken : Token
{
    internal RexlLexer.FixedTokenInfo Info { get; }

    public override bool HasDiagnostics => Info.Deprecated;

    /// <summary>
    /// The string representation of the token.
    /// </summary>
    public string Repr { get { return Info.Rep; } }

    /// <summary>
    /// The standard representation of the token.
    /// </summary>
    public string Std { get { return Info.Std; } }

    /// <summary>
    /// Whether the token form is deprecated.
    /// </summary>
    public bool Deprecated { get { return Info.Deprecated; } }

    private protected FixedToken(TokenStream stream, int index, RexlLexer.FixedTokenInfo info, int ichMin, int ichLim)
        : base(stream, index, info.VerifyValue().Tid, ichMin, ichLim)
    {
        Validation.AssertNonEmpty(info.Rep);
        Validation.AssertNonEmpty(info.Std);
        Validation.Assert(!info.Deprecated || info.Rep != info.Std);
        Info = info;
    }

    public override string GetTextString()
    {
        return Info.Rep;
    }

    public override string GetStdString()
    {
        return Info.Std;
    }
}

/// <summary>
/// Punctuator token.
/// </summary>
public sealed class PuncToken : FixedToken
{
    internal PuncToken(TokenStream stream, int index, RexlLexer.FixedTokenInfo info, int ichMin, int ichLim)
        : base(stream, index, info, ichMin, ichLim)
    {
    }

    public override string Render()
    {
        return string.Format("{0}, Punc=[{1}]", base.Render(), Info.Rep);
    }
}

/// <summary>
///  Keyword token.
/// </summary>
public sealed class KeyToken : FixedToken
{
    internal KeyToken(TokenStream stream, int index, RexlLexer.FixedTokenInfo info, int ichMin, int ichLim)
        : base(stream, index, info, ichMin, ichLim)
    {
    }

    public override string Render()
    {
        return string.Format("{0}, Key=[{1}]", base.Render(), Info.Rep);
    }
}

/// <summary>
/// Represents 'it$slot'.
/// </summary>
public sealed class ItSlotToken : Token
{
    /// <summary>
    /// The slot number.
    /// </summary>
    public int Slot { get; }

    public override bool HasDiagnostics => false;

    internal ItSlotToken(TokenStream stream, int index, int ichMin, int ichLim, int slot)
        : base(stream, index, TokKind.ItSlot, ichMin, ichLim)
    {
        Validation.Assert(slot >= 0);
        Slot = slot;
    }

    public override string ToString()
    {
        return string.Format("it${0}", Slot);
    }

    public override string Render()
    {
        return string.Format("{0}, Slot=[{1}]", base.Render(), Slot);
    }
}

/// <summary>
/// Represents '#slot'.
/// </summary>
public sealed class HashSlotToken : Token
{
    /// <summary>
    /// The slot number.
    /// </summary>
    public int Slot { get; }

    public override bool HasDiagnostics => false;

    internal HashSlotToken(TokenStream stream, int index, int ichMin, int ichLim, int slot)
        : base(stream, index, TokKind.HashSlot, ichMin, ichLim)
    {
        Validation.Assert(slot >= 0);
        Slot = slot;
    }

    public override string ToString()
    {
        return string.Format("#{0}", Slot);
    }

    public override string Render()
    {
        return string.Format("{0}, Slot=[{1}]", base.Render(), Slot);
    }
}

/// <summary>
/// Enum for size of numeric literal tokens.
/// </summary>
public enum NumLitSize
{
    Unspecified,
    OneByte,
    TwoBytes,
    FourBytes,
    EightBytes,
    UnlimitedSize,
    _Lim,
}

/// <summary>
/// Flags for integer literal tokens.
/// </summary>
[Flags]
public enum IntLitFlags
{
    None = 0x00,
    Bin = 0x01,
    Hex = 0x02,
    Unsigned = 0x04,
    Error = 0x08,
}

/// <summary>
///  Base class for numeric literals.
/// </summary>
public abstract class NumLitToken : Token
{
    /// <summary>
    /// Enum indicating the size (in bytes) of the numeric literal.
    /// </summary>
    public NumLitSize Size { get; }

    private protected NumLitToken(TokenStream stream, int index, TokKind tid, int ichMin, int ichLim, NumLitSize size)
        : base(stream, index, tid, ichMin, ichLim)
    {
        Validation.Assert(0 <= size && size < NumLitSize._Lim);
        Size = size;
    }

    public override string Render()
    {
        return string.Format("{0}, Size={1}", base.Render(), Size);
    }
}

/// <summary>
/// Integer literal token.
/// </summary>
public sealed class IntLitToken : NumLitToken
{
    public override bool HasDiagnostics => (Flags & IntLitFlags.Error) != 0;

    /// <summary>
    /// The literal value, as a non-negative Integer value.
    /// </summary>
    public Integer Value { get; }

    /// <summary>
    /// Flags indicating any type suffixes and whether it was encoded in hex.
    /// </summary>
    public IntLitFlags Flags { get; }

    internal IntLitToken(TokenStream stream, int index, Integer value, int ichMin, int ichLim, IntLitFlags flags, NumLitSize size)
        : base(stream, index, TokKind.IntLit, ichMin, ichLim, size)
    {
        Value = value;
        Flags = flags;
    }

    public override string ToString()
    {
        // REVIEW: What should we use for ToString?
        return Value.ToString(CultureInfo.InvariantCulture);
    }

    public override string Render()
    {
        return string.Format("{0}, Value={1}, Flags={2}", base.Render(), Value, Flags);
    }
}

/// <summary>
/// Floating point literal token.
/// </summary>
public sealed class FltLitToken : NumLitToken
{
    public override bool HasDiagnostics => HasError;

    /// <summary>
    /// The literal value, as a double. Note that the size may indicate a "float", ie, r4.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Whether there are errors in the token, eg, invalid digit separator usage like 123_.456.
    /// </summary>
    public bool HasError { get; }

    internal FltLitToken(TokenStream stream, int index, double value, int ichMin, int ichLim, NumLitSize size, bool hasError)
        : base(stream, index, TokKind.FltLit, ichMin, ichLim, size)
    {
        Validation.Assert(!double.IsNaN(value));
        Value = value;
        HasError = hasError;
    }

    public override string ToString()
    {
        // REVIEW: What should we use for ToString?
        return Value.ToString(CultureInfo.InvariantCulture);
    }

    public override string Render()
    {
        return string.Format("{0}, Value={1:R}{2}", base.Render(), Value.ToStr(), HasError ? "<Err>" : "");
    }
}

/// <summary>
/// Flags for identifiers.
/// </summary>
[Flags]
public enum IdentFlags
{
    None = 0x00,

    /// <summary>
    /// Whether the identifier is quoted.
    /// </summary>
    QuoteOpen = 0x01,

    /// <summary>
    /// Whether the identifier used a close quote. If QuoteOpen is set, but QuoteClose is not,
    /// the parser should emit an error indicating the identifier quoting was not properly
    /// terminated.
    /// </summary>
    QuoteClose = 0x02,

    /// <summary>
    /// Whether this identifier token "wants" to be quoted. This is set when either <see cref="QuoteOpen"/>
    /// is set or the contents of the identifier really requires quotes, because it is a key word or empty, etc.
    /// </summary>
    WantsQuotes = 0x04,

    /// <summary>
    /// Whether the identifier text was deemed invalid for a <see cref="DName"/>.
    /// </summary>
    Modified = 0x08,

    /// <summary>
    /// The combination of all quote related flags.
    /// </summary>
    QuotesAll = QuoteOpen | QuoteClose | WantsQuotes,
}

/// <summary>
/// Identifier token.
/// </summary>
public sealed class IdentToken : Token
{
    public override bool HasDiagnostics => HasQuoteOpen && !HasQuoteClose || (Flags & IdentFlags.Modified) != 0;

    /// <summary>
    /// The flags for this identifier.
    /// </summary>
    public IdentFlags Flags { get; }

    // Unescaped, unmodified value.
    private readonly string _value;

    /// <summary>
    /// The identifier as a <see cref="DName"/>. The Modified flag indicates whether this was
    /// modified from the text in the source, to make it a valid <see cref="DName"/>.
    /// </summary>
    public DName Name { get; }

    /// <summary>
    /// The identifier as a <see cref="string"/>. The identifier could be modified to make it
    /// a valid <see cref="DName">. The raw name is the unescaped name prior to the modification.
    /// <summary>
    public string RawName { get { return _value; } }

    internal IdentToken(TokenStream stream, int index, string val, int ichMin, int ichLim,
            IdentFlags flags = IdentFlags.None, Token tokenAlt = null, bool altFuzzy = false)
        : base(stream, index, TokKind.Ident, ichMin, ichLim, tokenAlt, altFuzzy)
    {
        // The string may be empty, but shouldn't be null.
        Validation.AssertValue(val);
        Validation.Assert((flags & ~IdentFlags.QuotesAll) == 0);
        // QuoteClose should be set only if QuoteOpen is set.
        Validation.Assert((flags & (IdentFlags.QuoteOpen | IdentFlags.QuoteClose)) != IdentFlags.QuoteClose);

        // If QuoteOpen is set, WantsQuotes should also be set.
        if ((flags & IdentFlags.QuoteOpen) != 0)
            flags |= IdentFlags.WantsQuotes;

        // If the value is a keyword, this should be marked as wanting quotes.
        Validation.Assert((flags & IdentFlags.WantsQuotes) != 0 || !RexlKeyWords.IsKeyword(val));
        // If the value needs to be quoted, this should be marked as wanting quotes.
        Validation.Assert((flags & IdentFlags.WantsQuotes) != 0 || !LexUtils.NeedsQuoted(val, out _));

        _value = val;
        Name = DName.MakeValid(val, out bool modified);
        if (modified)
            flags |= IdentFlags.Modified;
        Flags = flags;
    }

    /// <summary>
    /// Whether any of the indicated flags are set.
    /// </summary>
    public bool HasAnyFlags(IdentFlags flags)
    {
        return (Flags & flags) != 0;
    }

    /// <summary>
    /// Whether the identifier is quoted.
    /// </summary>
    public bool HasQuoteOpen { get { return HasAnyFlags(IdentFlags.QuoteOpen); } }

    /// <summary>
    /// Whether the identifier was terminated via a close quote.
    /// </summary>
    public bool HasQuoteClose { get { return HasAnyFlags(IdentFlags.QuoteClose); } }

    /// <summary>
    /// Whether the identifier wants to be quoted.
    /// </summary>
    public bool WantsQuotes { get { return HasAnyFlags(IdentFlags.WantsQuotes); } }

    /// <summary>
    /// Whether the identifier has any errors. There are two ways it could be in error: that
    /// the text wasn't a proper <see cref="DName"/>, or the identifier is quoted but the close
    /// quote is missing.
    /// </summary>
    public bool HasErrors
    {
        get { return HasAnyFlags(IdentFlags.Modified) || HasAnyFlags(IdentFlags.QuoteOpen) && !HasAnyFlags(IdentFlags.QuoteClose); }
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(_value))
            return "''";

        if (!WantsQuotes)
        {
            Validation.Assert(!LexUtils.NeedsQuoted(_value, out _));
            return _value;
        }

        var sb = new SbTextSink();
        Format(sb);
        return sb.Builder.ToString();
    }

    public override string Render()
    {
        return string.Format("{0}, Name=[{1}], Flags={2}", base.Render(), Name, Flags);
    }

    /// <summary>
    /// Appends a rexl (possibly quoted) representation to the <paramref name="sink"/>.
    /// </summary>
    public void Format(TextSink sink)
    {
        Validation.AssertValue(sink);

        if (string.IsNullOrEmpty(_value))
        {
            sink.Write(RexlLexer.IdentQuoteOpen);
            sink.Write(RexlLexer.IdentQuoteClose);
            return;
        }

        if (!WantsQuotes)
        {
            Validation.Assert(!LexUtils.NeedsQuoted(_value, out _));
            sink.Write(_value);
            return;
        }

        sink.Write(RexlLexer.IdentQuoteOpen);
        for (int i = 0; i < _value.Length; i++)
        {
            char ch = _value[i];
            sink.Write(ch);
            if (ch == RexlLexer.IdentQuoteClose)
                sink.Write(ch);
        }
        sink.Write(RexlLexer.IdentQuoteClose);
    }
}

/// <summary>
/// Flags for text literal tokens.
/// </summary>
[Flags]
public enum TextLitFlags
{
    None = 0x00,

    /// <summary>
    /// Whether the string literal was unterminated.
    /// </summary>
    Unterminated = 0x01,

    /// <summary>
    /// Escaping error.
    /// </summary>
    BadEscape = 0x02,
}

/// <summary>
/// String literal token.
/// </summary>
public sealed class TextLitToken : Token
{
    public override bool HasDiagnostics => (Flags & (TextLitFlags.Unterminated | TextLitFlags.BadEscape)) != 0;

    /// <summary>
    /// The text value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The flags, indicating whether the text literal was properly terminated and whether there
    /// were one or more escape errors.
    /// </summary>
    public TextLitFlags Flags { get; }

    /// <summary>
    /// When there is an escape error, this is the relative location of the first one, within the
    /// token's <see cref="Token.Range"/>. Otherwise, this returns -1.
    /// </summary>
    public int IchBad { get; }

    /// <summary>
    /// The sanitized text, with characters outside the typical ascii range (0x20 through 0x7E)
    /// replaced with their hex characters between angle brackets. This is computed lazily.
    /// </summary>
    public string Sanitized { get { return _sanitized ??= Sanitize(Value); } }
    private string _sanitized;

    internal TextLitToken(TokenStream stream, int index, string val, int ichMin, int ichLim, TextLitFlags flags, int ichBad)
        : base(stream, index, TokKind.TxtLit, ichMin, ichLim)
    {
        Validation.AssertValue(val);
        Validation.Assert(ichBad >= -1);
        Validation.Assert(((flags & TextLitFlags.BadEscape) != 0) == (ichBad >= 0));
        Value = val;
        Flags = flags;
        IchBad = ichBad;
    }

    public override string ToString()
    {
        // REVIEW: Escape properly here.
        return Value;
    }

    public override string Render()
    {
        // Baselining tests use Render, and they are sensitive to extended character use,
        // hence the use of Sanitized.
        return string.Format(
            IchBad >= 0 ? "{0}, Value=[{1}], Flags={2}, IchBad={3}" : "{0}, Value=[{1}], Flags={2}",
            base.Render(), Sanitized, Flags, IchBad);
    }

    private static string Sanitize(string value)
    {
        StringBuilder sb = null;
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (0x20 <= ch && ch < 0x7F)
            {
                if (sb != null)
                    sb.Append(ch);
                continue;
            }
            if (sb == null)
            {
                sb = new StringBuilder();
                sb.Append(value, 0, i);
            }

            uint u = (uint)ch;
            sb
                .Append('<')
                .AppendFormat(u <= ushort.MaxValue ? "{0:X2}" : "{0:X4}", u)
                .Append('>');
        }

        return sb?.ToString() ?? value;
    }
}

/// <summary>
/// An error token.
/// </summary>
public sealed class ErrorToken : Token
{
    public override bool HasDiagnostics => true;

    public string Text { get; }

    public string Detail { get; }

    internal ErrorToken(TokenStream stream, int index, int ichMin, int ichLim, string text, string detail = null)
        : base(stream, index, TokKind.Error, ichMin, ichLim)
    {
        Validation.AssertValue(text);
        Validation.AssertValueOrNull(detail);
        Text = text;
        Detail = detail;
    }

    public override string GetTextString()
    {
        return Text;
    }
}

/// <summary>
/// End-of-input token
/// </summary>
public sealed class EofToken : Token
{
    public override bool HasDiagnostics => false;

    internal EofToken(TokenStream stream, int index, int ichMin, int ichLim)
        : base(stream, index, TokKind.Eof, ichMin, ichLim)
    {
    }

    public override string GetTextString()
    {
        return "<eof>";
    }
}

/// <summary>
/// Comment token.
/// </summary>
public sealed class CommentToken : Token
{
    public override bool HasDiagnostics => false;

    internal CommentToken(TokenStream stream, int index, TokKind tid, int ichMin, int ichLim)
        : base(stream, index, tid, ichMin, ichLim)
    {
        Validation.Assert(tid == TokKind.CommentBlock || tid == TokKind.CommentLine);
    }
}
