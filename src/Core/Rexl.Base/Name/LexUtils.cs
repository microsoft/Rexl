// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Types;

namespace Microsoft.Rexl;

/// <summary>
/// Utilities to quote names, paths, and text literals, as well as to lex names and paths.
/// This functionality should be kept consistent with the rexl lexer.
/// </summary>
public static class LexUtils
{
    private const char k_quote = '\'';
    private const string k_dbl_quotes = "''";
    private const string k_hex = "0123456789ABCDEF";

    /// <summary>
    /// Write a dotted syntax representation of the given path to the given sink. This handles
    /// quoting when needed.
    /// </summary>
    public static TSink WriteDottedSyntax<TSink>(this TSink sink, NPath path)
        where TSink : TextSink
    {
        // REVIEW: What should this do for root?
        if (path.IsRoot)
            return sink;
        if (path.NameCount == 1)
            return sink.WriteEscapedName(path.Leaf);
        var names = path.ToNames();
        sink.WriteEscapedName(names[0]);
        for (int i = 1; i < names.Count; i++)
            sink.TWrite('.').WriteEscapedName(names[i]);
        return sink;
    }

    /// <summary>
    /// Return true if the name needs quoting to be a legal rexl identifier.
    /// </summary>
    public static bool NeedsQuoted(string name, out int ichBad)
    {
        if (string.IsNullOrEmpty(name))
        {
            ichBad = 0;
            return true;
        }

        int cch = name.Length;
        int ich = 0;
        if (_IsIdentStart(name[ich]) && name[ich] != k_quote)
        {
            while (++ich < cch && _IsIdentCh(name[ich]))
            {
            }
            Validation.Assert(ich <= cch);
            if (ich >= cch)
            {
                // There are no bad characters. Needs quoted iff the name is a keyword.
                ichBad = cch;
                return RexlKeyWords.IsKeyword(name);
            }
        }

        Validation.AssertIndex(ich, cch);
        ichBad = ich;
        return true;
    }

    /// <summary>
    /// Write the name to the given <paramref name="sink"/>, quoting if needed.
    /// </summary>
    public static TSink WriteEscapedName<TSink>(this TSink sink, string name)
        where TSink : TextSink
    {
        int cch = name.Length;
        if (cch == 0)
        {
            sink.Write(k_dbl_quotes);
            return sink;
        }

        if (!LexUtils.NeedsQuoted(name, out int ich))
        {
            sink.Write(name);
            return sink;
        }

        // Need to quote.
        Validation.AssertIndexInclusive(ich, cch);
        sink.Write(k_quote);
        for (int ichPrev = 0; ; ich++)
        {
            if (ich < cch && name[ich] != k_quote)
                continue;

            // Write the recent seg.
            if (ich > ichPrev)
                sink.Write(name.AsSpan(ichPrev, ich - ichPrev));

            if (ich >= cch)
                break;

            sink.Write(k_dbl_quotes);
            ichPrev = ich + 1;
        }
        sink.Write(k_quote);

        return sink;
    }

    /// <summary>
    /// Takes a valid DName value and returns a legal Rexl identifier, escaping if needed.
    /// REVIEW: Remove this.
    /// </summary>
    public static string EscapeName(string name)
    {
        Validation.BugCheckParam(DName.IsValidDName(name), nameof(name));
        return EscapeNameCore(name);
    }

    /// <summary>
    /// Takes a valid DName value and returns a legal Rexl identifier, escaping if needed.
    /// </summary>
    internal static string EscapeNameCore(string name)
    {
        Validation.Assert(DName.IsValidDName(name));

        int cch = name.Length;
        Validation.Assert(cch > 0);

        if (!LexUtils.NeedsQuoted(name, out int ich))
            return name;

        // Need to quote.
        Validation.AssertIndexInclusive(ich, cch);
        StringBuilder sb = new StringBuilder(cch + 2);
        sb.Append(k_quote);

        for (int ichPrev = 0; ; ich++)
        {
            if (ich < cch && name[ich] != k_quote)
                continue;

            // Write the recent seg.
            if (ich > ichPrev)
                sb.Append(name, ichPrev, ich - ichPrev);

            if (ich >= cch)
                break;

            sb.Append(k_dbl_quotes);
            ichPrev = ich + 1;
        }
        sb.Append(k_quote);

        return sb.ToString();
    }

    /// <summary>
    /// Get a valid rexl text literal representation of <paramref name="value"/>.
    /// </summary>
    public static string GetTextLiteral(string value)
    {
        Validation.BugCheckValue(value, nameof(value));
        var sb = new StringBuilder();
        AppendTextLiteral(sb, value);
        return sb.ToString();
    }

    /// <summary>
    /// Appends a valid rexl text literal representation of <paramref name="value"/>
    /// to <paramref name="sb"/>.
    /// </summary>
    public static void AppendTextLiteral(StringBuilder sb, string value)
    {
        Validation.BugCheckValue(sb, nameof(sb));
        Validation.BugCheckValue(value, nameof(value));

        sb.Append('"');

        ReadOnlySpan<char> chars = value;
        int ichMin = 0;
        int ich = 0;
        for (; ich < chars.Length; ich++)
        {
            var ch = chars[ich];
            if (ch < 0x80)
            {
                if (ch >= ' ' && ch != '"' && ch != '\\')
                    continue;
            }
            else
            {
                const CharUtils.UniCatFlags bad =
                    CharUtils.UniCatFlags.LineSeparator |
                    CharUtils.UniCatFlags.ParagraphSeparator |
                    CharUtils.UniCatFlags.Control |
                    CharUtils.UniCatFlags.Format |
                    CharUtils.UniCatFlags.Surrogate |
                    CharUtils.UniCatFlags.PrivateUse |
                    CharUtils.UniCatFlags.OtherNotAssigned;

                var cat = CharUtils.GetUniCatFlags(ch);
                if ((cat & bad) == 0)
                    continue;
            }

            // Must escape.
            if (ichMin < ich)
                sb.Append(chars.Slice(ichMin, ich - ichMin));
            sb.Append('\\');
            switch (ch)
            {
            case '\t': sb.Append('t'); break;
            case '\n': sb.Append('n'); break;
            case '\r': sb.Append('r'); break;
            case '"': sb.Append('"'); break;
            case '\\': sb.Append('\\'); break;

            default:
                {
                    var val = (ushort)ch;
                    if (val >= 0x100)
                    {
                        sb.Append('u');
                        sb.Append(k_hex[val >> 12]);
                        sb.Append(k_hex[(val >> 8) & 0x0F]);
                    }
                    else
                        sb.Append('x');
                    sb.Append(k_hex[(val >> 4) & 0x0F]);
                    sb.Append(k_hex[val & 0x0F]);
                    break;
                }
            }
            ichMin = ich + 1;
        }

        if (ichMin < ich)
            sb.Append(chars.Slice(ichMin, ich - ichMin));
        sb.Append('"');
    }

    /// <summary>
    /// Lexes a possibly quoted name, returning true it the result is a valid DName.
    /// Stops when it has digested a name, with ich set appropriately.
    /// </summary>
    public static bool TryLexName(ref int ich, string str, out DName result)
    {
        Validation.BugCheckValue(str, nameof(str));
        Validation.BugCheckIndexInclusive(ich, str.Length, nameof(ich));

        // REVIEW: Should we better leverage the main-stream lexer code?
        result = default;
        if (ich >= str.Length)
            return false;

        if (!_IsIdentStart(str[ich]))
            return false;

        string res;
        if (str[ich] == k_quote)
        {
            // Delimited identifier, is like a string, but with a different closing character.
            ich++;

            // Gather the characters in the builder. Note that the string may have escapes in it,
            // so is not necessarily a direct substring of str.
            var sb = new StringBuilder();
            for (; ; )
            {
                if (ich >= str.Length)
                    return false;
                char ch = str[ich++];
                if (CharUtils.IsLineTerm(ch))
                    return false;

                if (ch == k_quote)
                {
                    if (ich >= str.Length || str[ich] != k_quote)
                        break;
                    ich++;
                }

                sb.Append(ch);
            }

            res = sb.ToString();
        }
        else
        {
            // Simple identifier.
            Validation.Assert(_IsIdentCh(str[ich]));

            int ichMin = ich;
            while (ich < str.Length && _IsIdentCh(str[ich]))
                ich++;

            if (ichMin == 0 && ich == str.Length)
                res = str;
            else
                res = str.Substring(ichMin, ich - ichMin);
        }

        return DName.TryWrap(res, out result);
    }

    /// <summary>
    /// Lexes a dotted name, with possibly quoted segments, returning true it the result is
    /// a non-root NPath.
    /// </summary>
    public static bool TryLexPath(string str, out NPath result)
    {
        Validation.BugCheckValue(str, nameof(str));
        int ich = 0;
        if (TryLexPath(ref ich, str, out result) && ich == str.Length)
            return true;
        result = NPath.Root;
        return false;
    }

    /// <summary>
    /// Lexes a dotted name, with possibly quoted segments. Allows empty (interpreted as root).
    /// </summary>
    public static bool TryLexPathOrRoot(string str, out NPath result)
    {
        Validation.BugCheckValue(str, nameof(str));
        if (str.Length == 0)
        {
            result = NPath.Root;
            return true;
        }
        return TryLexPath(str, out result);
    }

    /// <summary>
    /// Lexes a dotted name, with possibly quoted segments, returning true it the result is
    /// a non-root NPath. On return <see cref="ich"/> is set to where it stopped.
    /// </summary>
    public static bool TryLexPath(ref int ich, string str, out NPath result)
    {
        Validation.BugCheckValue(str, nameof(str));
        Validation.BugCheckIndexInclusive(ich, str.Length, nameof(ich));

        NPath res = NPath.Root;
        for (; ; )
        {
            if (ich >= str.Length)
                break;
            if (res.NameCount > 0)
            {
                // REVIEW: Should this allow '!' and should it allow surrounding white space?
                if (str[ich] != '.')
                    break;
                if (++ich >= str.Length)
                {
                    result = default(NPath);
                    return false;
                }
            }
            if (!TryLexName(ref ich, str, out DName name))
            {
                result = default(NPath);
                return false;
            }
            res = res.Append(name);
        }

        result = res;
        return !res.IsRoot;
    }

    /// <summary>
    /// Returns true if <paramref name="ch"/> is possibly an ident character.
    /// </summary>
    public static bool IsIdentPossible(char ch)
    {
        return _IsIdentCh(ch) || ch == '\'';
    }

    /// <summary>
    /// Returns true if <paramref name="sb"/> ends in a possible identifier character,
    /// indicating that a space is needed before another identifier is appended.
    /// </summary>
    public static bool NeedSpaceBeforeIdent(StringBuilder sb)
    {
        Validation.AssertValue(sb);
        return sb.Length > 0 && IsIdentPossible(sb[sb.Length - 1]);
    }

    /// <summary>
    /// Returns whether ch is valid as the first character of an identifier (including the open quote character).
    /// </summary>
    private static bool _IsIdentStart(char ch)
    {
        if (ch >= 128)
            return (CharUtils.GetUniCatFlags(ch) & CharUtils.UniCatFlags.IdentStartChar) != 0;
        return ((uint)(ch - 'a') < 26) || ((uint)(ch - 'A') < 26) || (ch == '_') || (ch == k_quote);
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
}
