// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// REVIEW: For ci, should we just use InvariantCi or use InvariantCi with OrdinalCi as
// tie breaker? If we use a tie breaker, then we need to make EqCi require that both InvCi
// and OrdCi say equal. This PP symbol contols which way, with RELAXED_CI meaning that
// we only use InvCi with no tie breaker.
#define RELAXED_CI

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Microsoft.Rexl;

/// <summary>
/// Utilities for culture related functionality.
/// </summary>
public static class CultureUtil
{
    /// <summary>
    /// Standard CompareInfo to use.
    /// </summary>
    public static readonly CompareInfo CompareInfo = CultureInfo.InvariantCulture.CompareInfo;
}

/// <summary>
/// Our string comparers. For comparing and sorting, we use Invariant with Ordinal as tie breaker.
/// Null values are considered less than all non-null values.
/// </summary>
public sealed class StrComparer : IComparer<string>
{
    /// <summary>
    /// String comparer to use for case sensitive sort. We want no "ties", but we also want
    /// lowercase and uppercase sorted together, so we use Invariant with Ordinal as tie
    /// breaker.
    /// </summary>
    public static readonly StrComparer Cs = new StrComparer(true);

    /// <summary>
    /// String comparer to use for case insensitive sort.
    /// </summary>
#if !RELAXED_CI
    public static readonly StrComparer Ci = new StrComparer(false);
#else
    public static readonly StringComparer Ci = StringComparer.InvariantCultureIgnoreCase;
#endif

    /// <summary>
    /// Ordinal string comparer.
    /// </summary>
    public static readonly StringComparer Ordinal = StringComparer.Ordinal;

    /// <summary>
    /// Ordinal case insensitive string comparer.
    /// </summary>
    public static readonly StringComparer OrdinalCi = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Invariant string comparer.
    /// </summary>
    public static readonly StringComparer Invariant = StringComparer.InvariantCulture;

    /// <summary>
    /// Invariant case insensitive string comparer.
    /// </summary>
    public static readonly StringComparer InvariantCi = StringComparer.InvariantCultureIgnoreCase;

    private readonly StringComparer _inv;
    private readonly StringComparer _ord;

    private StrComparer(bool cs)
    {
        _inv = cs ? StringComparer.InvariantCulture : StringComparer.InvariantCultureIgnoreCase;
        _ord = cs ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(string? x, string? y)
    {
        int res = _inv.Compare(x, y);
        if (res != 0)
            return res;
        return _ord.Compare(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqCi(string? a, string? b)
    {
#if !RELAXED_CI
        return
            string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase) &&
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
#else
        return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetHashCodeCi(string? a)
    {
        if (a is null)
            return 0;
#if !RELAXED_CI
        return HashCode.Combine(
            string.GetHashCode(a, StringComparison.InvariantCultureIgnoreCase),
            string.GetHashCode(a, StringComparison.OrdinalIgnoreCase));
#else
        return string.GetHashCode(a, StringComparison.InvariantCultureIgnoreCase);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool Lt(string? a, string? b) => Cs.Compare(a, b) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool Le(string? a, string? b) => Cs.Compare(a, b) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool Gt(string? a, string? b) => Cs.Compare(a, b) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool Ge(string? a, string? b) => Cs.Compare(a, b) >= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool LtCi(string? a, string? b) => Ci.Compare(a, b) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool LeCi(string? a, string? b) => Ci.Compare(a, b) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool GtCi(string? a, string? b) => Ci.Compare(a, b) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool GeCi(string? a, string? b) => Ci.Compare(a, b) >= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool EqTi(string? a, string? b) => a is not null && b is not null && string.Equals(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool LtTi(string? a, string? b) => a is not null && b is not null && Cs.Compare(a, b) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool LeTi(string? a, string? b) => a is not null && b is not null && Cs.Compare(a, b) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool GtTi(string? a, string? b) => a is not null && b is not null && Cs.Compare(a, b) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool GeTi(string? a, string? b) => a is not null && b is not null && Cs.Compare(a, b) >= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool EqTiCi(string? a, string? b) => a is not null && b is not null && EqCi(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool LtTiCi(string? a, string? b) => a is not null && b is not null && Ci.Compare(a, b) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool LeTiCi(string? a, string? b) => a is not null && b is not null && Ci.Compare(a, b) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool GtTiCi(string? a, string? b) => a is not null && b is not null && Ci.Compare(a, b) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool GeTiCi(string? a, string? b) => a is not null && b is not null && Ci.Compare(a, b) >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static string? Min(string? a, string? b) => Cs.Compare(a, b) <= 0 ? a : b;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static string? Max(string? a, string? b) => Cs.Compare(a, b) >= 0 ? a : b;
}
