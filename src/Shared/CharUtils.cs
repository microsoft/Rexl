// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Rexl.Private;

// REVIEW: Trim out un-needed methods at some point.
internal static class CharUtils
{
    /// <summary>
    /// Bit masks of the UnicodeCategory enum. A couple extra values are defined
    /// for convenience for the C# lexical grammar.
    /// </summary>
    [Flags]
    internal enum UniCatFlags : uint
    {
        // Letters
        UppercaseLetter = 1 << UnicodeCategory.UppercaseLetter, // Lu
        LowercaseLetter = 1 << UnicodeCategory.LowercaseLetter, // Ll
        TitlecaseLetter = 1 << UnicodeCategory.TitlecaseLetter, // Lt
        ModifierLetter = 1 << UnicodeCategory.ModifierLetter, // Lm
        OtherLetter = 1 << UnicodeCategory.OtherLetter, // Lo

        // Marks
        NonSpacingMark = 1 << UnicodeCategory.NonSpacingMark, // Mn
        SpacingCombiningMark = 1 << UnicodeCategory.SpacingCombiningMark, // Mc
        EnclosingMark = 1 << UnicodeCategory.EnclosingMark, // Me

        // Numbers
        DecimalDigitNumber = 1 << UnicodeCategory.DecimalDigitNumber, // Nd
        LetterNumber = 1 << UnicodeCategory.LetterNumber, // Nl (i.e. roman numeral one 0x2160)
        OtherNumber = 1 << UnicodeCategory.OtherNumber, // No

        // Spaces
        SpaceSeparator = 1 << UnicodeCategory.SpaceSeparator, // Zs
        LineSeparator = 1 << UnicodeCategory.LineSeparator, // Zl
        ParagraphSeparator = 1 << UnicodeCategory.ParagraphSeparator, // Zp

        // Control
        Control = 1 << UnicodeCategory.Control, // Cc
        Format = 1 << UnicodeCategory.Format, // Cf
        Surrogate = 1 << UnicodeCategory.Surrogate, // Cs
        PrivateUse = 1 << UnicodeCategory.PrivateUse, // Co

        // Punctuation
        ConnectorPunctuation = 1 << UnicodeCategory.ConnectorPunctuation, // Pc
        DashPunctuation = 1 << UnicodeCategory.DashPunctuation, // Pd
        OpenPunctuation = 1 << UnicodeCategory.OpenPunctuation, // Ps
        ClosePunctuation = 1 << UnicodeCategory.ClosePunctuation, // Pe
        InitialQuotePunctuation = 1 << UnicodeCategory.InitialQuotePunctuation, // Pi
        FinalQuotePunctuation = 1 << UnicodeCategory.FinalQuotePunctuation, // Pf
        OtherPunctuation = 1 << UnicodeCategory.OtherPunctuation, // Po

        // Symbols
        MathSymbol = 1 << UnicodeCategory.MathSymbol, // Sm
        CurrencySymbol = 1 << UnicodeCategory.CurrencySymbol, // Sc
        ModifierSymbol = 1 << UnicodeCategory.ModifierSymbol, // Sk
        OtherSymbol = 1 << UnicodeCategory.OtherSymbol, // So

        // Other
        OtherNotAssigned = 1 << UnicodeCategory.OtherNotAssigned, // Cn

        // Useful combinations.
        IdentStartChar = UppercaseLetter | LowercaseLetter | TitlecaseLetter |
          ModifierLetter | OtherLetter | LetterNumber,

        IdentPartChar = IdentStartChar | NonSpacingMark | SpacingCombiningMark |
          DecimalDigitNumber | ConnectorPunctuation | Format,
    }

    /// <summary>
    /// Returns whether ch is in the unicode decimal digit class. This includes characters outside
    /// the '0' .. '9' range. This method is typically used to allow digit-like code points in
    /// identifiers. Numeric literal lexing often uses the IsSimpleDigit method below.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDecDigitEx(char ch)
    {
        if (ch < 128)
            return (((uint)ch - '0') <= 9);

        return ((GetUniCatFlags(ch) & UniCatFlags.DecimalDigitNumber) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDecDigit(char ch)
    {
        return ((uint)ch - '0') <= 9;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDecDigit(char ch, out uint value)
    {
        value = (uint)ch - '0';
        return value <= 9;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsHexDigit(char ch)
    {
        return '0' <= ch && ch <= '9' || 'A' <= ch && ch <= 'F' || 'a' <= ch && ch <= 'f';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsHexDigit(char ch, out uint value)
    {
        if ('0' <= ch && ch <= '9')
        {
            value = (uint)ch - '0';
            return true;
        }
        if ('A' <= ch && ch <= 'F')
        {
            value = (uint)ch - ('A' - 10);
            return true;
        }
        if ('a' <= ch && ch <= 'f')
        {
            value = (uint)ch - ('a' - 10);
            return true;
        }
        value = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsFormatCh(char ch)
    {
        return (ch >= 128 && (GetUniCatFlags(ch) & UniCatFlags.Format) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsLatinAlpha(char ch)
    {
        return ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static UniCatFlags GetUniCatFlags(char ch)
    {
        Validation.Assert(ch >= 128);
        return ((UniCatFlags)(1u << (int)CharUnicodeInfo.GetUnicodeCategory(ch)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSpace(char ch)
    {
        if (ch >= 128)
            return (GetUniCatFlags(ch) & UniCatFlags.SpaceSeparator) != 0;

        switch (ch)
        {
        case ' ':
        // character tabulation
        case '\u0009':
        // line tabulation
        case '\u000B':
        // form feed
        case '\u000C':
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsLineTerm(char ch)
    {
        switch (ch)
        {
        // line feed, unicode 0x000A
        case '\n':
        // carriage return, unicode 0x000D
        case '\r':
        // Unicode next line
        case '\u0085':
        // Unicode line separator
        case '\u2028':
        // Unicode paragraph separator
        case '\u2029':
            return true;
        }

        return false;
    }
}
