// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// This is public but just uses Asserts for validation rather than BugCheck. Users of this
/// should ensure that they don't violate the invariants.
/// </summary>
public static class Util
{
    /// <summary>
    /// Our standard encoder doesn't emit an identifier (since we're typically dealing with lots
    /// of text values in the same stream) and should throw when there are invalid bytes (I believe
    /// this is for reading - don't think it affects writing).
    /// </summary>
    public static UTF8Encoding StdUTF8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Return the size of the array, 0 if the array is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Size(this Array? items)
    {
        return items == null ? 0 : items.Length;
    }

    /// <summary>
    /// Return the byte conversion of the boolean value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToNum(this bool value)
    {
        return value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Return the size of the array, 0 if the array is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Size<T>(this T[]? items)
    {
        return items == null ? 0 : items.Length;
    }

    /// <summary>
    /// Return the size of the string, 0 if the string is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Size(this string? str)
    {
        return str == null ? 0 : str.Length;
    }

    /// <summary>
    /// Return the size of the list, 0 if the list is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Size<T>(this List<T>? items)
    {
        return items == null ? 0 : items.Count;
    }

    /// <summary>
    /// Return the size of the set, 0 if the set is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Size<T>(this HashSet<T>? items)
    {
        return items == null ? 0 : items.Count;
    }

    /// <summary>
    /// Return the size of the dictionary, 0 if the dictionary is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Size<TKey, TValue>(this Dictionary<TKey, TValue>? items)
        where TKey : notnull
    {
        return items == null ? 0 : items.Count;
    }

    /// <summary>
    /// Add an item to the list, allocating the list if it is null.
    /// </summary>
    public static void Add<T>([NotNull] ref List<T>? items, T item)
    {
        Validation.AssertValueOrNull(items);

        if (items == null)
            items = new List<T>();
        items.Add(item);
    }

    /// <summary>
    /// Add all the items from listSrc to listDst, allocating listDst if it needs to be.
    /// </summary>
    public static void Add<T>(ref List<T>? listDst, List<T>? listSrc)
    {
        Validation.AssertValueOrNull(listSrc);
        Validation.AssertValueOrNull(listDst);

        if (listSrc != null && listSrc.Count > 0)
        {
            if (listDst == null)
                listDst = new List<T>(listSrc.Count);
            listDst.AddRange(listSrc);
        }
    }

    public static void Add<T>(ref List<T>? listDst, IEnumerable<T>? src)
    {
        Validation.AssertValueOrNull(listDst);
        Validation.AssertValueOrNull(src);

        if (src != null)
        {
            if (listDst == null)
                listDst = new List<T>(src);
            else
                listDst.AddRange(src);
        }
    }

    /// <summary>
    /// Add an item to the hash set, allocating the hash set if it is null.
    /// </summary>
    public static bool Add<T>([NotNull] ref HashSet<T>? items, T item)
    {
        Validation.AssertValueOrNull(items);

        if (items == null)
            items = new HashSet<T>();
        return items.Add(item);
    }

    /// <summary>
    /// Return true if the hash set is non-null and contains the given value.
    /// </summary>
    public static bool Contains<T>([NotNullWhen(true)] HashSet<T>? items, T value)
    {
        return items != null && items.Contains(value);
    }

    /// <summary>
    /// Add an item to the dictionary, allocating the dictionary if it is null.
    /// </summary>
    public static void Add<TKey, TValue>([NotNull] ref Dictionary<TKey, TValue>? items, TKey key, TValue value)
        where TKey : notnull
    {
        if (items == null)
            items = new Dictionary<TKey, TValue>();
        items.Add(key, value);
    }

    /// <summary>
    /// Set an item in the dictionary, allocating the dictionary if it is null.
    /// </summary>
    public static void Set<TKey, TValue>([NotNull] ref Dictionary<TKey, TValue>? items, TKey key, TValue value)
        where TKey : notnull
    {
        if (items == null)
            items = new Dictionary<TKey, TValue>();
        items[key] = value;
    }

    /// <summary>
    /// Return true if the dictionary is non-null and contains the given key.
    /// </summary>
    public static bool ContainsKey<TKey, TValue>([NotNullWhen(true)] Dictionary<TKey, TValue>? items, TKey key)
        where TKey : notnull
    {
        return items != null && items.ContainsKey(key);
    }

    /// <summary>
    /// Returns false if items is null. Otherwise, calls items.TryGetValue.
    /// </summary>
    public static bool TryGetValue<TKey, TValue>(
            [NotNullWhen(true)] Dictionary<TKey, TValue>? items, TKey key, [MaybeNullWhen(false)] out TValue value)
        where TKey : notnull
    {
        if (items != null)
            return items.TryGetValue(key, out value);
        value = default(TValue);
        return false;
    }

    /// <summary>
    /// Returns the value in the dictionary if it is there, otherwise ensures that the dictionary is allocated
    /// and adds and returns the given value.
    /// </summary>
    public static TValue GetOrAdd<TKey, TValue>(
        [NotNullWhen(true)] ref Dictionary<TKey, TValue>? items, TKey key, TValue value)
        where TKey : notnull
    {
        if (items == null)
            items = new Dictionary<TKey, TValue>();
        else if (items.TryGetValue(key, out var cur))
            return cur;
        items.Add(key, value);
        return value;
    }

    /// <summary>
    /// Returns the value in the dictionary if it is there, otherwise ensures that the dictionary is allocated
    /// and adds and returns the given value.
    /// </summary>
    public static TValue GetOrAdd<TKey, TValue>(
        [NotNullWhen(true)] ref Dictionary<TKey, TValue>? items, TKey key, Func<TValue> valueFunc)
        where TKey : notnull
    {
        Validation.AssertValue(valueFunc);
        if (items == null)
            items = new Dictionary<TKey, TValue>();
        else if (items.TryGetValue(key, out var cur))
            return cur;
        var value = valueFunc();
        items.Add(key, value);
        return value;
    }

    /// <summary>
    /// Pops the "top" (last) item from a List.
    /// </summary>
    public static T Pop<T>(this List<T> items)
    {
        Validation.AssertValue(items);
        Validation.Assert(items.Count > 0);
        int index = items.Count - 1;
        T item = items[index];
        items.RemoveAt(index);
        return item;
    }

    /// <summary>
    /// Pops the "top" (last) <paramref name="n"/> items from a List.
    /// </summary>
    public static T[] Pop<T>(this List<T> items, int n)
    {
        Validation.AssertValue(items);
        Validation.Assert(items.Count >= n);

        var popped = new T[n];
        var idx = items.Count - n;
        items.CopyTo(idx, popped, 0, n);
        items.RemoveRange(items.Count - n, n);
        return popped;
    }

    /// <summary>
    /// Peeks the "top" (last) item of a List.
    /// </summary>
    public static T Peek<T>(this List<T> items)
    {
        Validation.AssertValue(items);
        Validation.Assert(items.Count > 0);
        int index = items.Count - 1;
        return items[index];
    }

    /// <summary>
    /// Tries to pop an element.
    /// </summary>
    public static bool TryPop<T>([NotNullWhen(true)] this List<T>? items, [MaybeNullWhen(false)] out T item)
    {
        Validation.AssertValueOrNull(items);
        if (items != null && items.Count > 0)
        {
            item = items.Pop();
            return true;
        }
        item = default(T);
        return false;
    }

    /// <summary>
    /// Tries to pop an element.
    /// </summary>
    public static bool TryPop<T>([NotNullWhen(true)] this Stack<T>? items, [MaybeNullWhen(false)] out T item)
    {
        Validation.AssertValueOrNull(items);
        if (items != null && items.Count > 0)
        {
            item = items.Pop();
            return true;
        }
        item = default(T);
        return false;
    }

    /// <summary>
    /// Append an element to an array, returning a new array.
    /// </summary>
    public static T[] Append<T>(T[]? a, T item)
    {
        int index;
        if (a == null || (index = a.Length) == 0)
            return new[] { item };
        Array.Resize(ref a, index + 1);
        a[index] = item;
        return a;
    }

    /// <summary>
    /// Creates an array consisting of <paramref name="elem"/> repeated <paramref name="count"/> times.
    /// </summary>
    public static T[] Repeat<T>(T elem, int count)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));

        T[] arr = new T[count];
        Array.Fill(arr, elem);
        return arr;
    }

    /// <summary>
    /// Given a current capacity and a minimum capacity, determine a good "target" capacity.
    /// </summary>
    public static int GetCapTarget(int capCur, int capMin)
    {
        // REVIEW: This value is taken from DotNet core source, as the largest array size.
        // Is there a better way to get it?
        const int capMax = 0x7FEFFFFF;

        // Try doubling and see if it overflows.
        int cap = Math.Max(capCur, 5) * 2;
        if ((uint)cap > (uint)capMax)
            cap = capMax;
        if (cap < capMin)
            cap = capMin;
        return cap;
    }

    /// <summary>
    /// Given a current capacity and a minimum capacity, determine a good "target" capacity.
    /// </summary>
    public static long GetCapTarget(long capCur, long capMin)
    {
        // REVIEW: This value is taken from DotNet core source, as the largest array size.
        // Is there a better way to get it?
        const long capMax = 0x7FEFFFFF;

        // Try doubling and see if it overflows.
        long cap = Math.Max(capCur, 5) * 2;
        if ((ulong)cap > (ulong)capMax)
            cap = capMax;
        if (cap < capMin)
            cap = capMin;
        return cap;
    }

    /// <summary>
    /// If the length of the array is at least <paramref name="len"/>, do nothing.
    /// Otherwise, try to resize it to <paramref name="len"/>. If that fails (because
    /// <paramref name="len"/> is too large) and <paramref name="len"/> is larger than
    /// <paramref name="lenMin"/>, then shrink <paramref name="len"/> to the average of
    /// the two and try again.
    /// </summary>
    public static void Grow<T>([NotNull] ref T[]? arr, ref int len, int lenMin)
    {
        Validation.AssertValueOrNull(arr);
        Validation.AssertIndexInclusive(lenMin, len);

        for (; ; )
        {
            if (arr != null && arr.Length >= len)
                break;
            try
            {
                Array.Resize(ref arr, len);
                break;
            }
            catch (OutOfMemoryException)
            {
                if (len <= lenMin)
                    throw;
                // Try a smaller size.
            }

            // Shrink len and try again.
            int lenOld = len;
            len = (int)((uint)(len + lenMin) / 2);
            Validation.Assert(len < lenOld);
        }
        Validation.Assert(arr != null && arr.LongLength >= len);
    }

    /// <summary>
    /// If the length of the array is at least <paramref name="len"/>, do nothing.
    /// Otherwise, try to resize it to <paramref name="len"/>. If that fails (because
    /// <paramref name="len"/> is too large) and <paramref name="len"/> is larger than
    /// <paramref name="lenMin"/>, then shrink <paramref name="len"/> to the average of
    /// the two and try again.
    /// </summary>
    public static void Grow<T>([NotNull] ref T[]? arr, ref long len, long lenMin)
    {
        Validation.AssertValueOrNull(arr);
        Validation.AssertIndexInclusive(lenMin, len);

        for (; ; )
        {
            if (arr != null && arr.Length >= len)
                break;
            try
            {
                Resize(ref arr, len);
                break;
            }
            catch (OutOfMemoryException)
            {
                if (len <= lenMin)
                    throw;
                // Try a smaller size.
            }

            // Shrink len and try again.
            long lenOld = len;
            len = (long)((ulong)(len + lenMin) / 2);
            Validation.Assert(len < lenOld);
        }
        Validation.Assert(arr != null && arr.LongLength >= len);
    }

    /// <summary>
    /// Resize an array using a <c>long</c> <paramref name="len"/>.
    /// </summary>
    public static void Resize<T>([NotNull] ref T[]? arr, long len)
    {
        Validation.Assert(len >= 0);

        if (arr != null && arr.LongLength == len)
            return;

        if (len <= int.MaxValue)
            Array.Resize(ref arr, (int)len);
        else
        {
            // REVIEW: Will this ever be supported?
            var arrNew = new T[len];
            if (arr != null)
                Array.Copy(arr, arrNew, arr.LongLength);
            arr = arrNew;
        }
    }

    /// <summary>
    /// Try to cast the Array to an array of <typeparamref name="T"/>.
    /// </summary>
    public static bool TryCast<T>(this Array? arr, out T[]? result)
    {
        result = arr as T[];
        return result != null || arr == null;
    }

    /// <summary>
    /// Try to cast the enumerable <paramref name="src"/> to an enumerable of <typeparamref name="T"/>.
    /// </summary>
    public static bool TryCast<T>(this IEnumerable? src, out IEnumerable<T>? result)
    {
        result = src as IEnumerable<T>;
        return result != null || src == null;
    }

    /// <summary>
    /// Swap two values.
    /// </summary>
    public static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    /// <summary>
    /// Clamp <paramref name="a"/> to the given <paramref name="min"/> and <paramref name="max"/>.
    /// </summary>
    public static int Clamp(this int a, int min, int max)
    {
        Validation.Assert(min <= max);
        if (a < min)
            return min;
        if (a > max)
            return max;
        return a;
    }

    /// <summary>
    /// Clamp <paramref name="a"/> to the given <paramref name="min"/> and <paramref name="max"/>.
    /// </summary>
    public static long Clamp(this long a, long min, long max)
    {
        Validation.Assert(min <= max);
        if (a < min)
            return min;
        if (a > max)
            return max;
        return a;
    }

    /// <summary>
    /// Gets the number of items between <paramref name="start"/> and <paramref name="stop"/>.
    /// </summary>
    public static long GetCount(long start, long stop)
    {
        if (stop <= start)
            return 0;

        // Restrict to at most long.MaxValue items. Technically it can be bigger, but it's pointless to support more.
        ulong unum = (ulong)stop - (ulong)start;
        long num = (long)unum;
        if (num < 0)
            num = long.MaxValue;
        return num;
    }

    /// <summary>
    /// Gets the number of items between <paramref name="start"/> and <paramref name="stop"/> using
    /// <paramref name="step"/> sized steps.
    /// </summary>
    public static long GetCount(long start, long stop, long step)
    {
        // Restrict to at most long.MaxValue items. Technically it can be bigger, but it's pointless to support more.
        ulong unum;
        if (step > 0)
        {
            ulong udif = stop > start ? (ulong)(stop - start) : 0;
            unum = udif / (ulong)step;
            if (udif % (ulong)step != 0 && (long)unum >= 0)
                unum++;
        }
        else if (step < 0)
        {
            ulong udif = stop < start ? (ulong)(start - stop) : 0;
            unum = udif / (ulong)(-step);
            if (udif % (ulong)(-step) != 0 && (long)unum >= 0)
                unum++;
        }
        else
            unum = 0;

        long num = (long)unum;
        if (num < 0)
            num = long.MaxValue;
        return num;
    }

    public static int CountBits(uint u)
    {
        u = (u & 0x55555555) + ((u >> 1) & 0x55555555);
        u = (u & 0x33333333) + ((u >> 2) & 0x33333333);
        u = (u & 0x0F0F0F0F) + ((u >> 4) & 0x0F0F0F0F);
        u = (u & 0x00FF00FF) + ((u >> 8) & 0x00FF00FF);
        u = (u & 0x0000FFFF) + ((u >> 16) & 0x0000FFFF);
        Validation.Assert(u <= 32);
        return (int)u;
    }

    public static int CountBits(ulong uu)
    {
        uu = (uu & 0x5555555555555555) + ((uu >> 1) & 0x5555555555555555);
        uu = (uu & 0x3333333333333333) + ((uu >> 2) & 0x3333333333333333);
        uu = (uu & 0x0F0F0F0F0F0F0F0F) + ((uu >> 4) & 0x0F0F0F0F0F0F0F0F);
        uu = (uu & 0x00FF00FF00FF00FF) + ((uu >> 8) & 0x00FF00FF00FF00FF);
        uu = (uu & 0x0000FFFF0000FFFF) + ((uu >> 16) & 0x0000FFFF0000FFFF);
        uu = (uu & 0x00000000FFFFFFFF) + ((uu >> 32) & 0x00000000FFFFFFFF);
        Validation.Assert(uu <= 64);
        return (int)uu;
    }

    /// <summary>
    /// Returns the index of the least significant set bit in u. Returns 32 if u is zero.
    /// </summary>
    public static int IbitLow(uint u)
    {
        if (u == 0)
            return 8 * sizeof(uint);
        return IbitHigh(u ^ (u - 1));
    }

    /// <summary>
    /// Returns the index of the least significant set bit in uu. Returns 64 if uu is zero.
    /// </summary>
    public static int IbitLow(ulong uu)
    {
        if (uu == 0)
            return 8 * sizeof(ulong);
        return IbitHigh(uu ^ (uu - 1));
    }

    /// <summary>
    /// Returns the index of the most significant set bit in u. Returns -1 if u is zero.
    /// </summary>
    public static int IbitHigh(uint u)
    {
        if (u == 0)
            return -1;

        int ibit = 31;
        int cbit = 16;
        uint mask = 0xFFFF0000U;
        uint v = u;
        while (cbit > 0)
        {
            if ((v & mask) == 0)
            {
                ibit -= cbit;
                v <<= cbit;
            }
            cbit >>= 1;
            mask <<= cbit;
        }
        Validation.Assert(0 <= ibit & ibit < 32);
        Validation.Assert((u & (1U << ibit)) != 0);
        Validation.Assert((u & (1U << ibit)) != 0);
        Validation.Assert(u <= ((1U << ibit) - 1 + (1U << ibit)));
        return ibit;
    }

    /// <summary>
    /// Returns the index of the most significant set bit in uu. Returns -1 if uu is zero.
    /// </summary>
    public static int IbitHigh(ulong uu)
    {
        if (uu == 0)
            return -1;

        int ibit = 63;
        int cbit = 32;
        ulong mask = 0xFFFFFFFF00000000UL;
        ulong v = uu;
        while (cbit > 0)
        {
            if ((v & mask) == 0)
            {
                ibit -= cbit;
                v <<= cbit;
            }
            cbit >>= 1;
            mask <<= cbit;
        }
        Validation.Assert(0 <= ibit & ibit < 64);
        Validation.Assert((uu & (1UL << ibit)) != 0);
        Validation.Assert((uu & (1UL << ibit)) != 0);
        Validation.Assert(uu <= ((1UL << ibit) - 1 + (1UL << ibit)));
        return ibit;
    }

    /// <summary>
    /// Returns a ulong Key with i1 left-shifted by 32-bits OR-ed with i2.
    /// Used as signature caching hash key.
    /// </summary>
    public static ulong MakeULong(int i1, int i2)
    {
        return ((ulong)(uint)i1 << 32) | (ulong)(uint)i2;
    }

    public static uint GetLo(ulong uu)
    {
        return (uint)uu;
    }

    public static uint GetHi(ulong uu)
    {
        return (uint)(uu >> 32);
    }
}
