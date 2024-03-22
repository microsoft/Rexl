// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

// Disable this warning, since that's how we test for NaN.
#pragma warning disable CS1718 // Comparison made to same variable

namespace Microsoft.Rexl.Code;

using Integer = System.Numerics.BigInteger;

// REVIEW: Consider returning early for all Min functions without count where the min is 0 (unsigned types).
partial class MinMaxGen
{

    #region Simple
    #region Req
    #region double
    public static long ExecMinMaxC(IEnumerable<double> items, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<double> items, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<double> items, ExecCtx ctx, int id, out double a)
    {
        Validation.AssertValue(ctx);
        double min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static double ExecMin(IEnumerable<double> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<double> items, ExecCtx ctx, int id, out double b)
    {
        Validation.AssertValue(ctx);
        double max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static double ExecMax(IEnumerable<double> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion double
    #region float
    public static long ExecMinMaxC(IEnumerable<float> items, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<float> items, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<float> items, ExecCtx ctx, int id, out float a)
    {
        Validation.AssertValue(ctx);
        float min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static float ExecMin(IEnumerable<float> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<float> items, ExecCtx ctx, int id, out float b)
    {
        Validation.AssertValue(ctx);
        float max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static float ExecMax(IEnumerable<float> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion float
    #region long
    public static long ExecMinMaxC(IEnumerable<long> items, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<long> items, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<long> items, ExecCtx ctx, int id, out long a)
    {
        Validation.AssertValue(ctx);
        long min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMin(IEnumerable<long> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<long> items, ExecCtx ctx, int id, out long b)
    {
        Validation.AssertValue(ctx);
        long max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static long ExecMax(IEnumerable<long> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion long
    #region ulong
    public static long ExecMinMaxC(IEnumerable<ulong> items, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<ulong> items, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<ulong> items, ExecCtx ctx, int id, out ulong a)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ulong ExecMin(IEnumerable<ulong> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<ulong> items, ExecCtx ctx, int id, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ulong ExecMax(IEnumerable<ulong> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ulong
    #region int
    public static long ExecMinMaxC(IEnumerable<int> items, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<int> items, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<int> items, ExecCtx ctx, int id, out int a)
    {
        Validation.AssertValue(ctx);
        int min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static int ExecMin(IEnumerable<int> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<int> items, ExecCtx ctx, int id, out int b)
    {
        Validation.AssertValue(ctx);
        int max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static int ExecMax(IEnumerable<int> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion int
    #region uint
    public static long ExecMinMaxC(IEnumerable<uint> items, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<uint> items, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<uint> items, ExecCtx ctx, int id, out uint a)
    {
        Validation.AssertValue(ctx);
        uint min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static uint ExecMin(IEnumerable<uint> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<uint> items, ExecCtx ctx, int id, out uint b)
    {
        Validation.AssertValue(ctx);
        uint max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static uint ExecMax(IEnumerable<uint> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion uint
    #region short
    public static long ExecMinMaxC(IEnumerable<short> items, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<short> items, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<short> items, ExecCtx ctx, int id, out short a)
    {
        Validation.AssertValue(ctx);
        short min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static short ExecMin(IEnumerable<short> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<short> items, ExecCtx ctx, int id, out short b)
    {
        Validation.AssertValue(ctx);
        short max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static short ExecMax(IEnumerable<short> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion short
    #region ushort
    public static long ExecMinMaxC(IEnumerable<ushort> items, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<ushort> items, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<ushort> items, ExecCtx ctx, int id, out ushort a)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ushort ExecMin(IEnumerable<ushort> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<ushort> items, ExecCtx ctx, int id, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ushort ExecMax(IEnumerable<ushort> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ushort
    #region sbyte
    public static long ExecMinMaxC(IEnumerable<sbyte> items, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<sbyte> items, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<sbyte> items, ExecCtx ctx, int id, out sbyte a)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static sbyte ExecMin(IEnumerable<sbyte> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<sbyte> items, ExecCtx ctx, int id, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static sbyte ExecMax(IEnumerable<sbyte> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion sbyte
    #region byte
    public static long ExecMinMaxC(IEnumerable<byte> items, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<byte> items, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<byte> items, ExecCtx ctx, int id, out byte a)
    {
        Validation.AssertValue(ctx);
        byte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static byte ExecMin(IEnumerable<byte> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<byte> items, ExecCtx ctx, int id, out byte b)
    {
        Validation.AssertValue(ctx);
        byte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static byte ExecMax(IEnumerable<byte> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion byte
    #region Integer
    public static long ExecMinMaxC(IEnumerable<Integer> items, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<Integer> items, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<Integer> items, ExecCtx ctx, int id, out Integer a)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static Integer ExecMin(IEnumerable<Integer> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<Integer> items, ExecCtx ctx, int id, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static Integer ExecMax(IEnumerable<Integer> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion Integer
    #region bool
    public static long ExecMinMaxC(IEnumerable<bool> items, ExecCtx ctx, int id, out bool a, out bool b)
    {
        Validation.AssertValue(ctx);
        bool min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!val) min = val;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static long ExecMinC(IEnumerable<bool> items, ExecCtx ctx, int id, out bool a)
    {
        Validation.AssertValue(ctx);
        bool min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (!val) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMaxC(IEnumerable<bool> items, ExecCtx ctx, int id, out bool b)
    {
        Validation.AssertValue(ctx);
        bool max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = e.Current;
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = e.Current;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    #endregion bool
    #endregion Req
    #region Opt
    #region double
    public static long ExecMinMaxC(IEnumerable<double?> items, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<double?> items, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<double?> items, ExecCtx ctx, int id, out double a)
    {
        Validation.AssertValue(ctx);
        double min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static double ExecMin(IEnumerable<double?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<double?> items, ExecCtx ctx, int id, out double b)
    {
        Validation.AssertValue(ctx);
        double max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static double ExecMax(IEnumerable<double?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion double
    #region float
    public static long ExecMinMaxC(IEnumerable<float?> items, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<float?> items, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<float?> items, ExecCtx ctx, int id, out float a)
    {
        Validation.AssertValue(ctx);
        float min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static float ExecMin(IEnumerable<float?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<float?> items, ExecCtx ctx, int id, out float b)
    {
        Validation.AssertValue(ctx);
        float max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static float ExecMax(IEnumerable<float?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion float
    #region long
    public static long ExecMinMaxC(IEnumerable<long?> items, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<long?> items, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<long?> items, ExecCtx ctx, int id, out long a)
    {
        Validation.AssertValue(ctx);
        long min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMin(IEnumerable<long?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<long?> items, ExecCtx ctx, int id, out long b)
    {
        Validation.AssertValue(ctx);
        long max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static long ExecMax(IEnumerable<long?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion long
    #region ulong
    public static long ExecMinMaxC(IEnumerable<ulong?> items, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<ulong?> items, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<ulong?> items, ExecCtx ctx, int id, out ulong a)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ulong ExecMin(IEnumerable<ulong?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<ulong?> items, ExecCtx ctx, int id, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ulong ExecMax(IEnumerable<ulong?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ulong
    #region int
    public static long ExecMinMaxC(IEnumerable<int?> items, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<int?> items, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<int?> items, ExecCtx ctx, int id, out int a)
    {
        Validation.AssertValue(ctx);
        int min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static int ExecMin(IEnumerable<int?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<int?> items, ExecCtx ctx, int id, out int b)
    {
        Validation.AssertValue(ctx);
        int max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static int ExecMax(IEnumerable<int?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion int
    #region uint
    public static long ExecMinMaxC(IEnumerable<uint?> items, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<uint?> items, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<uint?> items, ExecCtx ctx, int id, out uint a)
    {
        Validation.AssertValue(ctx);
        uint min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static uint ExecMin(IEnumerable<uint?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<uint?> items, ExecCtx ctx, int id, out uint b)
    {
        Validation.AssertValue(ctx);
        uint max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static uint ExecMax(IEnumerable<uint?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion uint
    #region short
    public static long ExecMinMaxC(IEnumerable<short?> items, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<short?> items, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<short?> items, ExecCtx ctx, int id, out short a)
    {
        Validation.AssertValue(ctx);
        short min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static short ExecMin(IEnumerable<short?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<short?> items, ExecCtx ctx, int id, out short b)
    {
        Validation.AssertValue(ctx);
        short max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static short ExecMax(IEnumerable<short?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion short
    #region ushort
    public static long ExecMinMaxC(IEnumerable<ushort?> items, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<ushort?> items, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<ushort?> items, ExecCtx ctx, int id, out ushort a)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ushort ExecMin(IEnumerable<ushort?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<ushort?> items, ExecCtx ctx, int id, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ushort ExecMax(IEnumerable<ushort?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ushort
    #region sbyte
    public static long ExecMinMaxC(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out sbyte a)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static sbyte ExecMin(IEnumerable<sbyte?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static sbyte ExecMax(IEnumerable<sbyte?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion sbyte
    #region byte
    public static long ExecMinMaxC(IEnumerable<byte?> items, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<byte?> items, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<byte?> items, ExecCtx ctx, int id, out byte a)
    {
        Validation.AssertValue(ctx);
        byte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static byte ExecMin(IEnumerable<byte?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<byte?> items, ExecCtx ctx, int id, out byte b)
    {
        Validation.AssertValue(ctx);
        byte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static byte ExecMax(IEnumerable<byte?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion byte
    #region Integer
    public static long ExecMinMaxC(IEnumerable<Integer?> items, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax(IEnumerable<Integer?> items, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC(IEnumerable<Integer?> items, ExecCtx ctx, int id, out Integer a)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static Integer ExecMin(IEnumerable<Integer?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC(IEnumerable<Integer?> items, ExecCtx ctx, int id, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static Integer ExecMax(IEnumerable<Integer?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion Integer
    #region bool
    public static long ExecMinMaxC(IEnumerable<bool?> items, ExecCtx ctx, int id, out bool a, out bool b)
    {
        Validation.AssertValue(ctx);
        bool min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!val) min = val;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static long ExecMinC(IEnumerable<bool?> items, ExecCtx ctx, int id, out bool a)
    {
        Validation.AssertValue(ctx);
        bool min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!val) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMaxC(IEnumerable<bool?> items, ExecCtx ctx, int id, out bool b)
    {
        Validation.AssertValue(ctx);
        bool max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = e.Current; if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = e.Current; if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    #endregion bool
    #endregion Opt
    #endregion Simple
    #region Selector
    #region Req
    #region double
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, double> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, double> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, double> fn, ExecCtx ctx, int id, out double a)
    {
        Validation.AssertValue(ctx);
        double min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static double ExecMin<T>(IEnumerable<T> items, Func<T, double> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, double> fn, ExecCtx ctx, int id, out double b)
    {
        Validation.AssertValue(ctx);
        double max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static double ExecMax<T>(IEnumerable<T> items, Func<T, double> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion double
    #region float
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, float> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, float> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, float> fn, ExecCtx ctx, int id, out float a)
    {
        Validation.AssertValue(ctx);
        float min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static float ExecMin<T>(IEnumerable<T> items, Func<T, float> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, float> fn, ExecCtx ctx, int id, out float b)
    {
        Validation.AssertValue(ctx);
        float max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static float ExecMax<T>(IEnumerable<T> items, Func<T, float> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion float
    #region long
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, long> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, long> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, long> fn, ExecCtx ctx, int id, out long a)
    {
        Validation.AssertValue(ctx);
        long min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMin<T>(IEnumerable<T> items, Func<T, long> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, long> fn, ExecCtx ctx, int id, out long b)
    {
        Validation.AssertValue(ctx);
        long max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static long ExecMax<T>(IEnumerable<T> items, Func<T, long> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion long
    #region ulong
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, ulong> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, ulong> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, ulong> fn, ExecCtx ctx, int id, out ulong a)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ulong ExecMin<T>(IEnumerable<T> items, Func<T, ulong> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, ulong> fn, ExecCtx ctx, int id, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ulong ExecMax<T>(IEnumerable<T> items, Func<T, ulong> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ulong
    #region int
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, int> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, int> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, int> fn, ExecCtx ctx, int id, out int a)
    {
        Validation.AssertValue(ctx);
        int min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static int ExecMin<T>(IEnumerable<T> items, Func<T, int> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, int> fn, ExecCtx ctx, int id, out int b)
    {
        Validation.AssertValue(ctx);
        int max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static int ExecMax<T>(IEnumerable<T> items, Func<T, int> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion int
    #region uint
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, uint> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, uint> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, uint> fn, ExecCtx ctx, int id, out uint a)
    {
        Validation.AssertValue(ctx);
        uint min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static uint ExecMin<T>(IEnumerable<T> items, Func<T, uint> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, uint> fn, ExecCtx ctx, int id, out uint b)
    {
        Validation.AssertValue(ctx);
        uint max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static uint ExecMax<T>(IEnumerable<T> items, Func<T, uint> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion uint
    #region short
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, short> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, short> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, short> fn, ExecCtx ctx, int id, out short a)
    {
        Validation.AssertValue(ctx);
        short min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static short ExecMin<T>(IEnumerable<T> items, Func<T, short> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, short> fn, ExecCtx ctx, int id, out short b)
    {
        Validation.AssertValue(ctx);
        short max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static short ExecMax<T>(IEnumerable<T> items, Func<T, short> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion short
    #region ushort
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, ushort> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, ushort> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, ushort> fn, ExecCtx ctx, int id, out ushort a)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ushort ExecMin<T>(IEnumerable<T> items, Func<T, ushort> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, ushort> fn, ExecCtx ctx, int id, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ushort ExecMax<T>(IEnumerable<T> items, Func<T, ushort> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ushort
    #region sbyte
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, sbyte> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, sbyte> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, sbyte> fn, ExecCtx ctx, int id, out sbyte a)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static sbyte ExecMin<T>(IEnumerable<T> items, Func<T, sbyte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, sbyte> fn, ExecCtx ctx, int id, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static sbyte ExecMax<T>(IEnumerable<T> items, Func<T, sbyte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion sbyte
    #region byte
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, byte> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, byte> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, byte> fn, ExecCtx ctx, int id, out byte a)
    {
        Validation.AssertValue(ctx);
        byte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static byte ExecMin<T>(IEnumerable<T> items, Func<T, byte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, byte> fn, ExecCtx ctx, int id, out byte b)
    {
        Validation.AssertValue(ctx);
        byte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static byte ExecMax<T>(IEnumerable<T> items, Func<T, byte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion byte
    #region Integer
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, Integer> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, Integer> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, Integer> fn, ExecCtx ctx, int id, out Integer a)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static Integer ExecMin<T>(IEnumerable<T> items, Func<T, Integer> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, Integer> fn, ExecCtx ctx, int id, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static Integer ExecMax<T>(IEnumerable<T> items, Func<T, Integer> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion Integer
    #region bool
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, bool> fn, ExecCtx ctx, int id, out bool a, out bool b)
    {
        Validation.AssertValue(ctx);
        bool min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!val) min = val;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, bool> fn, ExecCtx ctx, int id, out bool a)
    {
        Validation.AssertValue(ctx);
        bool min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (!val) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, bool> fn, ExecCtx ctx, int id, out bool b)
    {
        Validation.AssertValue(ctx);
        bool max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(e.Current);
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    #endregion bool
    #endregion Req
    #region Opt
    #region double
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, double?> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, double?> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, double?> fn, ExecCtx ctx, int id, out double a)
    {
        Validation.AssertValue(ctx);
        double min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static double ExecMin<T>(IEnumerable<T> items, Func<T, double?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, double?> fn, ExecCtx ctx, int id, out double b)
    {
        Validation.AssertValue(ctx);
        double max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static double ExecMax<T>(IEnumerable<T> items, Func<T, double?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion double
    #region float
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, float?> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, float?> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, float?> fn, ExecCtx ctx, int id, out float a)
    {
        Validation.AssertValue(ctx);
        float min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static float ExecMin<T>(IEnumerable<T> items, Func<T, float?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, float?> fn, ExecCtx ctx, int id, out float b)
    {
        Validation.AssertValue(ctx);
        float max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static float ExecMax<T>(IEnumerable<T> items, Func<T, float?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion float
    #region long
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, long?> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, long?> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, long?> fn, ExecCtx ctx, int id, out long a)
    {
        Validation.AssertValue(ctx);
        long min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMin<T>(IEnumerable<T> items, Func<T, long?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, long?> fn, ExecCtx ctx, int id, out long b)
    {
        Validation.AssertValue(ctx);
        long max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static long ExecMax<T>(IEnumerable<T> items, Func<T, long?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion long
    #region ulong
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, ulong?> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, ulong?> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, ulong?> fn, ExecCtx ctx, int id, out ulong a)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ulong ExecMin<T>(IEnumerable<T> items, Func<T, ulong?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, ulong?> fn, ExecCtx ctx, int id, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ulong ExecMax<T>(IEnumerable<T> items, Func<T, ulong?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ulong
    #region int
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, int?> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, int?> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, int?> fn, ExecCtx ctx, int id, out int a)
    {
        Validation.AssertValue(ctx);
        int min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static int ExecMin<T>(IEnumerable<T> items, Func<T, int?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, int?> fn, ExecCtx ctx, int id, out int b)
    {
        Validation.AssertValue(ctx);
        int max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static int ExecMax<T>(IEnumerable<T> items, Func<T, int?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion int
    #region uint
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, uint?> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, uint?> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, uint?> fn, ExecCtx ctx, int id, out uint a)
    {
        Validation.AssertValue(ctx);
        uint min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static uint ExecMin<T>(IEnumerable<T> items, Func<T, uint?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, uint?> fn, ExecCtx ctx, int id, out uint b)
    {
        Validation.AssertValue(ctx);
        uint max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static uint ExecMax<T>(IEnumerable<T> items, Func<T, uint?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion uint
    #region short
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, short?> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, short?> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, short?> fn, ExecCtx ctx, int id, out short a)
    {
        Validation.AssertValue(ctx);
        short min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static short ExecMin<T>(IEnumerable<T> items, Func<T, short?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, short?> fn, ExecCtx ctx, int id, out short b)
    {
        Validation.AssertValue(ctx);
        short max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static short ExecMax<T>(IEnumerable<T> items, Func<T, short?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion short
    #region ushort
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, ushort?> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, ushort?> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, ushort?> fn, ExecCtx ctx, int id, out ushort a)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ushort ExecMin<T>(IEnumerable<T> items, Func<T, ushort?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, ushort?> fn, ExecCtx ctx, int id, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ushort ExecMax<T>(IEnumerable<T> items, Func<T, ushort?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ushort
    #region sbyte
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, sbyte?> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, sbyte?> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, sbyte?> fn, ExecCtx ctx, int id, out sbyte a)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static sbyte ExecMin<T>(IEnumerable<T> items, Func<T, sbyte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, sbyte?> fn, ExecCtx ctx, int id, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static sbyte ExecMax<T>(IEnumerable<T> items, Func<T, sbyte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion sbyte
    #region byte
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, byte?> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, byte?> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, byte?> fn, ExecCtx ctx, int id, out byte a)
    {
        Validation.AssertValue(ctx);
        byte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static byte ExecMin<T>(IEnumerable<T> items, Func<T, byte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, byte?> fn, ExecCtx ctx, int id, out byte b)
    {
        Validation.AssertValue(ctx);
        byte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static byte ExecMax<T>(IEnumerable<T> items, Func<T, byte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion byte
    #region Integer
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, Integer?> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMax<T>(IEnumerable<T> items, Func<T, Integer?> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, Integer?> fn, ExecCtx ctx, int id, out Integer a)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static Integer ExecMin<T>(IEnumerable<T> items, Func<T, Integer?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer min = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, Integer?> fn, ExecCtx ctx, int id, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static Integer ExecMax<T>(IEnumerable<T> items, Func<T, Integer?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer max = default;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion Integer
    #region bool
    public static long ExecMinMaxC<T>(IEnumerable<T> items, Func<T, bool?> fn, ExecCtx ctx, int id, out bool a, out bool b)
    {
        Validation.AssertValue(ctx);
        bool min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!val) min = val;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static long ExecMinC<T>(IEnumerable<T> items, Func<T, bool?> fn, ExecCtx ctx, int id, out bool a)
    {
        Validation.AssertValue(ctx);
        bool min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!val) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMaxC<T>(IEnumerable<T> items, Func<T, bool?> fn, ExecCtx ctx, int id, out bool b)
    {
        Validation.AssertValue(ctx);
        bool max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    #endregion bool
    #endregion Opt
    #endregion Selector
    #region Indexed
    #region Req
    #region double
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, double> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, double> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, double> fn, ExecCtx ctx, int id, out double a)
    {
        Validation.AssertValue(ctx);
        double min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static double ExecMinInd<T>(IEnumerable<T> items, Func<long, T, double> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, double> fn, ExecCtx ctx, int id, out double b)
    {
        Validation.AssertValue(ctx);
        double max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static double ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, double> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion double
    #region float
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, float> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, float> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, float> fn, ExecCtx ctx, int id, out float a)
    {
        Validation.AssertValue(ctx);
        float min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static float ExecMinInd<T>(IEnumerable<T> items, Func<long, T, float> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, float> fn, ExecCtx ctx, int id, out float b)
    {
        Validation.AssertValue(ctx);
        float max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static float ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, float> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion float
    #region long
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, long> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, long> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, long> fn, ExecCtx ctx, int id, out long a)
    {
        Validation.AssertValue(ctx);
        long min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMinInd<T>(IEnumerable<T> items, Func<long, T, long> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, long> fn, ExecCtx ctx, int id, out long b)
    {
        Validation.AssertValue(ctx);
        long max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static long ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, long> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion long
    #region ulong
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, ulong> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, ulong> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, ulong> fn, ExecCtx ctx, int id, out ulong a)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ulong ExecMinInd<T>(IEnumerable<T> items, Func<long, T, ulong> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, ulong> fn, ExecCtx ctx, int id, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ulong ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, ulong> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ulong
    #region int
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, int> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, int> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, int> fn, ExecCtx ctx, int id, out int a)
    {
        Validation.AssertValue(ctx);
        int min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static int ExecMinInd<T>(IEnumerable<T> items, Func<long, T, int> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, int> fn, ExecCtx ctx, int id, out int b)
    {
        Validation.AssertValue(ctx);
        int max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static int ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, int> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion int
    #region uint
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, uint> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, uint> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, uint> fn, ExecCtx ctx, int id, out uint a)
    {
        Validation.AssertValue(ctx);
        uint min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static uint ExecMinInd<T>(IEnumerable<T> items, Func<long, T, uint> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, uint> fn, ExecCtx ctx, int id, out uint b)
    {
        Validation.AssertValue(ctx);
        uint max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static uint ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, uint> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion uint
    #region short
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, short> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, short> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, short> fn, ExecCtx ctx, int id, out short a)
    {
        Validation.AssertValue(ctx);
        short min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static short ExecMinInd<T>(IEnumerable<T> items, Func<long, T, short> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, short> fn, ExecCtx ctx, int id, out short b)
    {
        Validation.AssertValue(ctx);
        short max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static short ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, short> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion short
    #region ushort
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, ushort> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, ushort> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, ushort> fn, ExecCtx ctx, int id, out ushort a)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ushort ExecMinInd<T>(IEnumerable<T> items, Func<long, T, ushort> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, ushort> fn, ExecCtx ctx, int id, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ushort ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, ushort> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ushort
    #region sbyte
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, sbyte> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, sbyte> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, sbyte> fn, ExecCtx ctx, int id, out sbyte a)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static sbyte ExecMinInd<T>(IEnumerable<T> items, Func<long, T, sbyte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, sbyte> fn, ExecCtx ctx, int id, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static sbyte ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, sbyte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion sbyte
    #region byte
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, byte> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, byte> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, byte> fn, ExecCtx ctx, int id, out byte a)
    {
        Validation.AssertValue(ctx);
        byte min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static byte ExecMinInd<T>(IEnumerable<T> items, Func<long, T, byte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, byte> fn, ExecCtx ctx, int id, out byte b)
    {
        Validation.AssertValue(ctx);
        byte max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static byte ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, byte> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion byte
    #region Integer
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, Integer> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, Integer> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, Integer> fn, ExecCtx ctx, int id, out Integer a)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static Integer ExecMinInd<T>(IEnumerable<T> items, Func<long, T, Integer> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, Integer> fn, ExecCtx ctx, int id, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static Integer ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, Integer> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(idx++, e.Current);
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(idx++, e.Current);
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion Integer
    #region bool
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, bool> fn, ExecCtx ctx, int id, out bool a, out bool b)
    {
        Validation.AssertValue(ctx);
        bool min = default, max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!val) min = val;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, bool> fn, ExecCtx ctx, int id, out bool a)
    {
        Validation.AssertValue(ctx);
        bool min = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (!val) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, bool> fn, ExecCtx ctx, int id, out bool b)
    {
        Validation.AssertValue(ctx);
        bool max = default; long count = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var val = fn(count, e.Current);
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            val = fn(count, e.Current);
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    #endregion bool
    #endregion Req
    #region Opt
    #region double
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, double?> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, double?> fn, ExecCtx ctx, int id, out double a, out double b)
    {
        Validation.AssertValue(ctx);
        double min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, double?> fn, ExecCtx ctx, int id, out double a)
    {
        Validation.AssertValue(ctx);
        double min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static double ExecMinInd<T>(IEnumerable<T> items, Func<long, T, double?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, double?> fn, ExecCtx ctx, int id, out double b)
    {
        Validation.AssertValue(ctx);
        double max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static double ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, double?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        double max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion double
    #region float
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, float?> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, float?> fn, ExecCtx ctx, int id, out float a, out float b)
    {
        Validation.AssertValue(ctx);
        float min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, float?> fn, ExecCtx ctx, int id, out float a)
    {
        Validation.AssertValue(ctx);
        float min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static float ExecMinInd<T>(IEnumerable<T> items, Func<long, T, float?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val) && min == min) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, float?> fn, ExecCtx ctx, int id, out float b)
    {
        Validation.AssertValue(ctx);
        float max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static float ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, float?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        float max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val) && max == max) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion float
    #region long
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, long?> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, long?> fn, ExecCtx ctx, int id, out long a, out long b)
    {
        Validation.AssertValue(ctx);
        long min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, long?> fn, ExecCtx ctx, int id, out long a)
    {
        Validation.AssertValue(ctx);
        long min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMinInd<T>(IEnumerable<T> items, Func<long, T, long?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, long?> fn, ExecCtx ctx, int id, out long b)
    {
        Validation.AssertValue(ctx);
        long max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static long ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, long?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        long max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion long
    #region ulong
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, ulong?> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, ulong?> fn, ExecCtx ctx, int id, out ulong a, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, ulong?> fn, ExecCtx ctx, int id, out ulong a)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ulong ExecMinInd<T>(IEnumerable<T> items, Func<long, T, ulong?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, ulong?> fn, ExecCtx ctx, int id, out ulong b)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ulong ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, ulong?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ulong max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ulong
    #region int
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, int?> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, int?> fn, ExecCtx ctx, int id, out int a, out int b)
    {
        Validation.AssertValue(ctx);
        int min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, int?> fn, ExecCtx ctx, int id, out int a)
    {
        Validation.AssertValue(ctx);
        int min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static int ExecMinInd<T>(IEnumerable<T> items, Func<long, T, int?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, int?> fn, ExecCtx ctx, int id, out int b)
    {
        Validation.AssertValue(ctx);
        int max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static int ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, int?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        int max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion int
    #region uint
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, uint?> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, uint?> fn, ExecCtx ctx, int id, out uint a, out uint b)
    {
        Validation.AssertValue(ctx);
        uint min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, uint?> fn, ExecCtx ctx, int id, out uint a)
    {
        Validation.AssertValue(ctx);
        uint min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static uint ExecMinInd<T>(IEnumerable<T> items, Func<long, T, uint?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, uint?> fn, ExecCtx ctx, int id, out uint b)
    {
        Validation.AssertValue(ctx);
        uint max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static uint ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, uint?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        uint max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion uint
    #region short
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, short?> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, short?> fn, ExecCtx ctx, int id, out short a, out short b)
    {
        Validation.AssertValue(ctx);
        short min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, short?> fn, ExecCtx ctx, int id, out short a)
    {
        Validation.AssertValue(ctx);
        short min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static short ExecMinInd<T>(IEnumerable<T> items, Func<long, T, short?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, short?> fn, ExecCtx ctx, int id, out short b)
    {
        Validation.AssertValue(ctx);
        short max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static short ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, short?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        short max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion short
    #region ushort
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, ushort?> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, ushort?> fn, ExecCtx ctx, int id, out ushort a, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, ushort?> fn, ExecCtx ctx, int id, out ushort a)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static ushort ExecMinInd<T>(IEnumerable<T> items, Func<long, T, ushort?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, ushort?> fn, ExecCtx ctx, int id, out ushort b)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static ushort ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, ushort?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        ushort max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion ushort
    #region sbyte
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, sbyte?> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, sbyte?> fn, ExecCtx ctx, int id, out sbyte a, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, sbyte?> fn, ExecCtx ctx, int id, out sbyte a)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static sbyte ExecMinInd<T>(IEnumerable<T> items, Func<long, T, sbyte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, sbyte?> fn, ExecCtx ctx, int id, out sbyte b)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static sbyte ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, sbyte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        sbyte max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion sbyte
    #region byte
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, byte?> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, byte?> fn, ExecCtx ctx, int id, out byte a, out byte b)
    {
        Validation.AssertValue(ctx);
        byte min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, byte?> fn, ExecCtx ctx, int id, out byte a)
    {
        Validation.AssertValue(ctx);
        byte min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static byte ExecMinInd<T>(IEnumerable<T> items, Func<long, T, byte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, byte?> fn, ExecCtx ctx, int id, out byte b)
    {
        Validation.AssertValue(ctx);
        byte max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static byte ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, byte?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        byte max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion byte
    #region Integer
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, Integer?> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static void ExecMinMaxInd<T>(IEnumerable<T> items, Func<long, T, Integer?> fn, ExecCtx ctx, int id, out Integer a, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer min = default, max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, Integer?> fn, ExecCtx ctx, int id, out Integer a)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static Integer ExecMinInd<T>(IEnumerable<T> items, Func<long, T, Integer?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer min = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(min <= val)) min = val;
            goto LNext;
        }
    LDone:
        return min;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, Integer?> fn, ExecCtx ctx, int id, out Integer b)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    public static Integer ExecMaxInd<T>(IEnumerable<T> items, Func<long, T, Integer?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        Integer max = default; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!(max >= val)) max = val;
            goto LNext;
        }
    LDone:
        return max;
    }
    #endregion Integer
    #region bool
    public static long ExecMinMaxIndC<T>(IEnumerable<T> items, Func<long, T, bool?> fn, ExecCtx ctx, int id, out bool a, out bool b)
    {
        Validation.AssertValue(ctx);
        bool min = default, max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!val) min = val;
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        b = max;
        return count;
    }
    public static long ExecMinIndC<T>(IEnumerable<T> items, Func<long, T, bool?> fn, ExecCtx ctx, int id, out bool a)
    {
        Validation.AssertValue(ctx);
        bool min = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            min = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (!val) min = val;
            count++;
            goto LNext;
        }
    LDone:
        a = min;
        return count;
    }
    public static long ExecMaxIndC<T>(IEnumerable<T> items, Func<long, T, bool?> fn, ExecCtx ctx, int id, out bool b)
    {
        Validation.AssertValue(ctx);
        bool max = default; long count = 0; long idx = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
        LFirst:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            var raw = fn(idx++, e.Current); if (raw == null) goto LFirst; var val = raw.GetValueOrDefault();
            max = val;
            count++;
        LNext:
            ctx.Ping(id); if (!e.MoveNext()) goto LDone;
            raw = fn(idx++, e.Current); if (raw == null) goto LNext; val = raw.GetValueOrDefault();
            if (val) max = val;
            count++;
            goto LNext;
        }
    LDone:
        b = max;
        return count;
    }
    #endregion bool
    #endregion Opt
    #endregion Indexed
}
