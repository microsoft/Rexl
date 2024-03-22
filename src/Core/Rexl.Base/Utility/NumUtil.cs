// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Utility;

using Integer = System.Numerics.BigInteger;

/// <summary>
/// Describes the IEEE 754 status for a floating point value.
/// </summary>
public enum RxValueKind
{
    /// <summary>
    /// A nonzero, nonspecial value. This includes both normalized and denormalized values.
    /// </summary>
    Standard,

    /// <summary>
    /// (Positive) zero.
    /// </summary>
    Zero,

    /// <summary>
    /// Negative zero.
    /// </summary>
    NegZero,

    /// <summary>
    /// (Positive) infinity.
    /// </summary>
    Infinity,

    /// <summary>
    /// Negative infinity.
    /// </summary>
    NegInfinity,

    /// <summary>
    /// Not a number.
    /// </summary>
    NaN
}

public static partial class NumUtil
{
    /// <summary>
    /// Add two non-negative values, clipping the sum at <see cref="long.MaxValue"/>.
    /// Passing any negative value results in undefined behavior.
    /// </summary>
    public static long AddCounts(long a, long b)
    {
        Validation.Assert(a >= 0);
        Validation.Assert(b >= 0);

        long res = a + b;
        if (res < 0)
            res = long.MaxValue;
        return res;
    }

    /// <summary>
    /// Multiply two non-negative values, clipping the product at <see cref="long.MaxValue"/>.
    /// Passing any negative value results in undefined behavior.
    /// </summary>
    public static long MulCounts(long a, long b)
    {
        Validation.Assert(a >= 0);
        Validation.Assert(b >= 0);

        if (a == 0 || b == 0)
            return 0;
        if (a >= long.MaxValue / b)
            return long.MaxValue;
        long res = a * b;
        Validation.Assert(res / a == b);
        Validation.Assert(res % a == 0);
        return res;
    }

    /// <summary>
    /// Multiply two non-negative values, clipping the product at <see cref="long.MaxValue"/>.
    /// Return false on overflow. Passing any negative value results in undefined behavior.
    /// </summary>
    public static bool TryMulCounts(long a, long b, out long res)
    {
        Validation.Assert(a >= 0);
        Validation.Assert(b >= 0);

        if (a == 0 || b == 0)
        {
            res = 0;
            return true;
        }
        if (a >= long.MaxValue / b)
        {
            res = long.MaxValue;
            return false;
        }
        res = a * b;
        Validation.Assert(res / a == b);
        Validation.Assert(res % a == 0);
        return true;
    }

    // These safely (without exception) cast to a value within the range of the indicated numeric type.
    #region Integer Casting

    // The BigInteger conversion operators throw when the value is outside the destination type.
    // To work around this, we bitwise-and with MaxValue of the corresponding unsigned size.
    // We then cast that to the unsigned type, and then to the signed value (if needed).
    // REVIEW: Need a better way - these are quite expensive.
    public static Integer CastI1(this Integer value)
    {
        if (value <= sbyte.MaxValue && value >= sbyte.MinValue)
            return value;
        return (sbyte)(byte)(value & byte.MaxValue);
    }

    public static Integer CastI2(this Integer value)
    {
        if (value <= short.MaxValue && value >= short.MinValue)
            return value;
        return (short)(ushort)(value & ushort.MaxValue);
    }

    public static Integer CastI4(this Integer value)
    {
        if (value <= int.MaxValue && value >= int.MinValue)
            return value;
        return (int)(uint)(value & uint.MaxValue);
    }

    public static Integer CastI8(this Integer value)
    {
        if (value <= long.MaxValue && value >= long.MinValue)
            return value;
        return (long)(ulong)(value & ulong.MaxValue);
    }

    public static long CastLong(this Integer value)
    {
        if (value.Sign >= 0)
        {
            if (value <= ulong.MaxValue)
                return (long)(ulong)value;
        }
        else
        {
            if (value >= long.MinValue)
                return (long)value;
        }
        return (long)(ulong)(value & ulong.MaxValue);
    }

    public static ulong CastUlong(this Integer value)
    {
        if (value.Sign >= 0)
        {
            if (value <= ulong.MaxValue)
                return (ulong)value;
        }
        else
        {
            if (value >= long.MinValue)
                return (ulong)(long)value;
        }
        return (ulong)(value & ulong.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Integer CastUX(Integer value, ulong max)
    {
        Validation.Assert(max > 0);
        Validation.Assert(((max + 1) & max) == 0);
        if (value <= max && value.Sign >= 0)
            return value;
        return value & max;
    }

    public static Integer CastBit(this Integer value)
    {
        return CastUX(value, 1);
    }

    public static Integer CastU1(this Integer value)
    {
        return CastUX(value, byte.MaxValue);
    }

    public static Integer CastU2(this Integer value)
    {
        return CastUX(value, ushort.MaxValue);
    }

    public static Integer CastU4(this Integer value)
    {
        return CastUX(value, uint.MaxValue);
    }

    public static Integer CastU8(this Integer value)
    {
        return CastUX(value, ulong.MaxValue);
    }

    #endregion Integer Casting

    /// <summary>
    /// Test the specified bit of the given <paramref name="value"/>.
    /// Passing a negative <paramref name="ibit"/> results in undefined behavior.
    /// </summary>
    public static bool TestBit(this Integer value, int ibit)
    {
        // REVIEW: Would be nice for this to be more efficient.
        Validation.Assert(ibit >= 0);
        return !(value >> ibit).IsEven;
    }

    /// <summary>
    /// Raise a long to a power.
    /// </summary>
    public static long IntPow(long val, ulong exp)
    {
        long res = 1;
        long cur = val;
        for (; ; )
        {
            if ((exp & 1) != 0)
                res *= cur;
            if ((exp >>= 1) == 0)
                return res;
            cur *= cur;
        }
    }

    /// <summary>
    /// Raise a ulong to a power.
    /// </summary>
    public static ulong IntPow(ulong val, ulong exp)
    {
        ulong res = 1;
        ulong cur = val;
        for (; ; )
        {
            if ((exp & 1) != 0)
                res *= cur;
            if ((exp >>= 1) == 0)
                return res;
            cur *= cur;
        }
    }

    /// <summary>
    /// This is used to easily read and write the bits of a Guid.
    /// Thanks to Vance Morrison for educating me about this excellent aliasing mechanism.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct GuidBits
    {
        // These overlay each other, so we can go back and forth between guid value and its bits as two ulong values.
        [FieldOffset(0)]
        public Guid Value;

        [FieldOffset(0)]
        public ulong Bits0;
        [FieldOffset(8)]
        public ulong Bits1;
    }

    public static (ulong, ulong) ToBits(this Guid guid)
    {
        GuidBits bits = default;
        bits.Value = guid;
        return (bits.Bits0, bits.Bits1);
    }

    public static Guid ToGuid(ulong value0, ulong value1)
    {
        GuidBits bits = default;
        bits.Bits0 = value0;
        bits.Bits1 = value1;
        return bits.Value;
    }
}
