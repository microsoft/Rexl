// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

using Integer = System.Numerics.BigInteger;

partial class SumBaseGen
{
    #region Exec on single sequence of number type
    public static double ExecS(IEnumerable<double> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static double ExecS(IEnumerable<double?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecS(IEnumerable<long> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecS(IEnumerable<long?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static ulong ExecS(IEnumerable<ulong> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        ulong sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static ulong ExecS(IEnumerable<ulong?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        ulong sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecS(IEnumerable<Integer> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecS(IEnumerable<Integer?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static double ExecSum(IEnumerable<float> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static double ExecSum(IEnumerable<float?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<int> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<int?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<short> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<short?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<sbyte> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<uint> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<uint?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<ushort> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<ushort?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<byte> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<byte?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<bool> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                if (val) sum++;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecSum(IEnumerable<bool?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                if (val) sum++;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<long> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<long?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<int> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<int?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<short> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<short?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<sbyte> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<ulong> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<ulong?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<uint> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<uint?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<ushort> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<ushort?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<byte> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<byte?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<bool> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current;
                if (val) sum++;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecBig(IEnumerable<bool?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        // long is sufficient to avoid overflow.
        long sum = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault();
                if (val) sum++;
                count++;
            }
        }
        c = count;
        return sum;
    }
    #endregion Exec on single sequence of number type

    #region Exec with one sequence and selector function
    public static double Exec<T>(IEnumerable<T> s, Func<T, double> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static double ExecInd<T>(IEnumerable<T> s, Func<long, T, double> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(count, e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static double Exec<T>(IEnumerable<T> s, Func<T, double?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static double ExecInd<T>(IEnumerable<T> s, Func<long, T, double?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0, idx = 0;
        double sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(idx++, e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long Exec<T>(IEnumerable<T> s, Func<T, long> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecInd<T>(IEnumerable<T> s, Func<long, T, long> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(count, e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long Exec<T>(IEnumerable<T> s, Func<T, long?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        long sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static long ExecInd<T>(IEnumerable<T> s, Func<long, T, long?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0, idx = 0;
        long sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(idx++, e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static ulong Exec<T>(IEnumerable<T> s, Func<T, ulong> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        ulong sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static ulong ExecInd<T>(IEnumerable<T> s, Func<long, T, ulong> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        ulong sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(count, e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static ulong Exec<T>(IEnumerable<T> s, Func<T, ulong?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        ulong sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static ulong ExecInd<T>(IEnumerable<T> s, Func<long, T, ulong?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0, idx = 0;
        ulong sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(idx++, e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer Exec<T>(IEnumerable<T> s, Func<T, Integer> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecInd<T>(IEnumerable<T> s, Func<long, T, Integer> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(count, e.Current);
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer Exec<T>(IEnumerable<T> s, Func<T, Integer?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        Integer sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    public static Integer ExecInd<T>(IEnumerable<T> s, Func<long, T, Integer?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0, idx = 0;
        Integer sum = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(idx++, e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                sum += val;
                count++;
            }
        }
        c = count;
        return sum;
    }
    #endregion Exec with one sequence and selector function

    #region Neumaier / Kahan-Babuska on single sequence of number type
    public static double ExecK(IEnumerable<double> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<double?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<float> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<float?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<Integer> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<Integer?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<long> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<long?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<int> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<int?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<ulong> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<ulong?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<uint> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = e.Current.ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<uint?> items, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = e.Current; if (raw == null) continue; var val = raw.GetValueOrDefault().ToR8();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK(IEnumerable<short> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<short?> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<sbyte> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<ushort> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<ushort?> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<byte> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<byte?> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<bool> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }
    public static double ExecK(IEnumerable<bool?> items, ExecCtx ctx, int id, out long c)
    {
        return ExecSum(items, ctx, id, out c);
    }

    public static double ExecK<T>(IEnumerable<T> s, Func<T, double> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(e.Current);
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecIndK<T>(IEnumerable<T> s, Func<long, T, double> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var val = fn(count, e.Current);
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecK<T>(IEnumerable<T> s, Func<T, double?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0;
        double sum = 0;
        double cor = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    public static double ExecIndK<T>(IEnumerable<T> s, Func<long, T, double?> fn, ExecCtx ctx, int id, out long c)
    {
        long count = 0, idx = 0;
        double sum = 0;
        double cor = 0;
        if (s != null)
        {
            using var e = s.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var raw = fn(idx++, e.Current); if (raw == null) continue; var val = raw.GetValueOrDefault();
                var pre = sum;
                sum += val;
                cor += Math.Abs(pre) >= Math.Abs(val) ? pre - sum + val : val - sum + pre;
                count++;
            }
        }
        c = count;
        return sum + cor;
    }
    #endregion Neumaier / Kahan-Babuska on single sequence of number type

    #region Mean
    public static double ExecMean(IEnumerable<double> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<double?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<float> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<float?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<Integer> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<Integer?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<long> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<long?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<int> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<int?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<ulong> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<ulong?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<uint> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<uint?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<short> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<short?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<sbyte> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<sbyte?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<ushort> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<ushort?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<byte> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<byte?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<bool> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean(IEnumerable<bool?> items, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(items, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }

    public static double ExecMean<T0>(IEnumerable<T0> s0, Func<T0, double> fn, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(s0, fn, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecIndMean<T0>(IEnumerable<T0> s0, Func<long, T0, double> fn, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecIndK(s0, fn, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecMean<T0>(IEnumerable<T0> s0, Func<T0, double?> fn, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecK(s0, fn, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    public static double ExecIndMean<T0>(IEnumerable<T0> s0, Func<long, T0, double?> fn, ExecCtx ctx, int id, out long c)
    {
        double sum = ExecIndK(s0, fn, ctx, id, out c);
        return sum / Math.Max(c, 1);
    }
    #endregion Mean
}
