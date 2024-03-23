// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// DName wraps a string. The default value has <see cref="IsValid"/> set to false.
/// Others are guaranteed to contain a string (potentially requiring quoting in rexl formulas)
/// that is valid as a field name (in a record type), a rexl "variable", or path segments,
/// eg, part of a node name, function path, namespace name, etc. More precisely, the
/// name will not contain line terminators and will not consist entirely of space characters.
/// </summary>
public struct DName : IEquatable<DName>
{
    private const string FixingPrefix = "_X";
    private readonly string? _value;

    public DName(DName value)
    {
        _value = value._value;
    }

    public DName(string value)
    {
        Validation.BugCheckParam(IsValidDName(value), nameof(value));
        _value = value;
    }

    /// <summary>
    /// Constructor that can assert validity rather than check it.
    /// </summary>
    private DName(string value, bool dummy)
    {
        Validation.Assert(IsValidDName(value));
        _value = value;
    }

    /// <summary>
    /// The name as a string. Invalid returns empty string.
    /// </summary>
    public string Value { get { return _value ?? ""; } }

    /// <summary>
    /// The name as a string. Invalid returns null.
    /// </summary>
    public string? ValueOrNull { get { return _value; } }

    /// <summary>
    /// Whether the name is valid.
    /// </summary>
    public bool IsValid { get { return _value != null; } }

    public static implicit operator string(DName name)
    {
        return name.Value;
    }

    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    /// Compare the two names using ordinal comparison.
    /// </summary>
    public static int Compare(DName a, DName b)
    {
        return string.Compare(a.Value, b.Value, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        Validation.BugCheckValueOrNull(obj);

        if (obj is not DName name)
            return false;
        return Equals(name);
    }

    public bool Equals(DName other)
    {
        return _value == other._value;
    }

    public static bool operator ==(DName name1, DName name2)
    {
        return name1.Value == name2.Value;
    }

    public static bool operator !=(DName name1, DName name2)
    {
        return name1.Value != name2.Value;
    }

    /// <summary>
    /// Return a legal rexl identifier, escaping if needed. This throws for invalid.
    /// </summary>
    public string Escape()
    {
        Validation.BugCheck(IsValid);
        return LexUtils.EscapeNameCore(_value!);
    }

    /// <summary>
    /// Returns whether the given name is a valid DName as defined above.
    /// </summary>
    public static bool IsValidDName([NotNullWhen(true)] string? str)
    {
        Validation.BugCheckValueOrNull(str);

        if (string.IsNullOrEmpty(str))
            return false;

        bool someGood = false;
        for (int i = 0; i < str.Length; i++)
        {
            char ch = str[i];
            if (CharUtils.IsLineTerm(ch))
                return false;
            if (!someGood && !CharUtils.IsSpace(ch))
                someGood = true;
        }

        return someGood;
    }

    /// <summary>
    /// If the given <paramref name="str"/> is the valid contents of a <see cref="DName"/> (according to
    /// <see cref="IsValidDName(string)"/>), sets <paramref name="name"/> to wrap it and returns <c>true</c>.
    /// Otherwise, sets <paramref name="name"/> to to default (invalid) and returns <c>false</c>.
    /// </summary>
    public static bool TryWrap(string? str, out DName name)
    {
        if (IsValidDName(str))
        {
            name = new DName(str, true);
            return true;
        }

        name = default;
        return false;
    }

    /// <summary>
    /// Takes a string and makes it into a valid DName. Drops all line terminators.
    /// If the result contains all spaces, a standard prefix is prepended to the name.
    /// Sets <paramref name="fModified"/> to whether the characters were modified
    /// (some dropped and/or prefix added).
    /// </summary>
    public static DName MakeValid(string? str, out bool fModified)
    {
        Validation.BugCheckValueOrNull(str);

        if (string.IsNullOrEmpty(str))
        {
            fModified = true;
            return new DName(FixingPrefix);
        }

        bool good = false;
        StringBuilder? sb = null;
        for (int i = 0; i < str.Length; i++)
        {
            char ch = str[i];
            if (CharUtils.IsLineTerm(ch))
            {
                // Drop line terminators.
                if (sb == null)
                {
                    sb = new StringBuilder(str.Length);
                    sb.Append(str, 0, i);
                }
                continue;
            }

            if (!good && !CharUtils.IsSpace(str[i]))
                good = true;
            if (sb != null)
                sb.Append(ch);
        }

        fModified = false;
        if (sb != null)
        {
            fModified = true;
            str = sb.ToString();
        }
        if (!good)
        {
            fModified = true;
            str = FixingPrefix + str;
        }

        return new DName(str, true);
    }
}
