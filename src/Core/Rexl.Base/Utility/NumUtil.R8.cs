// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// NOTE: Diff NumUtil.R8.cs and NumUtil.R4.cs. Differences should be minimal.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Utility;

using Bits = NumUtil.DoubleBits;
using Integer = System.Numerics.BigInteger;
using IX = System.Int64;
using RX = System.Double;
using UX = System.UInt64;

partial class NumUtil
{
    /// <summary>
    /// Render the value as a string in a consistent way, distinguishing between 0 and negative zero
    /// and using ∞ for infinity.
    /// </summary>
    public static string ToStr(this RX value)
    {
        if (value == 0)
            return 1 / value < 0 ? "-0" : "0";
        if (1 / value == 0)
            return value < 0 ? "-∞" : "∞";
        return value.ToString("R");
    }

    /// <summary>
    /// The IA to R8 standard conversion is not accurate. Use this instead.
    /// </summary>
    public static RX ToR8(this Integer value)
    {
        bool neg = value.Sign < 0;
        if (neg)
            value = -value;

        RX res;
        long cb = value.GetByteCount(isUnsigned: true);
        if (cb <= sizeof(ulong))
            res = ((ulong)value).ToR8();
        else if (cb > Bits.CbMaxInt)
            res = RX.PositiveInfinity;
        else
            res = ToR8Core(in value, cb);

        return neg ? -res : res;
    }

    [SkipLocalsInit]
    private static RX ToR8Core(in Integer value, long cb)
    {
        Validation.Assert(cb > sizeof(ulong));
        Validation.Assert(cb <= Bits.CbMaxInt);

        const int k_ibitLo = Bits.CbitExp;
        const uint k_bitLo = 1U << k_ibitLo;

        Span<byte> bytes = stackalloc byte[Bits.CbMaxInt];
        value.TryWriteBytes(bytes, out int cbGot, isUnsigned: true, isBigEndian: false).Verify();
        Validation.Assert(cbGot == cb);
        Validation.Assert(bytes[cbGot - 1] != 0);

        // Build the high UX bits.
        UX man = Unsafe.ReadUnaligned<UX>(ref bytes[cbGot - sizeof(UX)]);
        UX maskHi = (UX)0xFF << (Bits.CbitTot - 8);
        Validation.Assert((man & maskHi) != 0);
        int cbitNeed = 0;
        for (int cbitCur = 4; cbitCur != 0; cbitCur >>= 1)
        {
            maskHi <<= cbitCur;
            if ((maskHi & man) == 0)
            {
                cbitNeed += cbitCur;
                man <<= cbitCur;
            }
        }
        Validation.Assert((IX)man < 0);
        Validation.AssertIndex(cbitNeed, 8);
        byte maskLo = (byte)0;
        if (cbitNeed > 0)
        {
            int shift = 8 - cbitNeed;
            man |= (uint)bytes[cbGot - (sizeof(UX) + 1)] >> shift;
            maskLo = (byte)((1U << shift) - 1);
        }
        int exp = (cbGot << 3) - 1 - cbitNeed;

        uint lo = (uint)man & (k_bitLo - 1);
        // Clear the implicit one bit.
        man = man << 1 >> 1;
        if (lo >= (k_bitLo >> 1))
        {
            bool up = false;
            if (lo > (k_bitLo >> 1))
                up = true;
            else if ((man & k_bitLo) != 0)
                up = true;
            else if ((bytes[cbGot - (sizeof(UX) + 1)] & maskLo) != 0)
                up = true;
            else
            {
                for (int i = cbGot - (sizeof(UX) + 2); i >= 0; i--)
                {
                    if (bytes[i] != 0)
                    {
                        up = true;
                        break;
                    }
                }
            }
            if (up)
            {
                // Note that this can go to infinity.
                man += k_bitLo;
                if ((IX)man < 0)
                {
                    man = 0;
                    exp++;
                }
            }
        }

        // Shift away the low bits and construct the RX.
        man >>= k_ibitLo;
        return Bits.FromBits(man | ((UX)(exp + Bits.RawExpZero) << Bits.CbitMan));
    }

    /// <summary>
    /// The U8 to R8 standard conversion is not accurate. Use this instead.
    /// REVIEW: https://github.com/dotnet/runtime/issues/43895.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RX ToR8(this ulong value)
    {
        // If the high bit is not set, just use the long -> RX conversion.
        if ((long)value >= 0)
            return (RX)(long)value;
#if false
        // This version explicitly handles all rounding, so is the most paranoid, but slower.
        const int cbitToss = 8 * sizeof(ulong) - Bits.CbitTot + Bits.CbitExp;
        const ulong bitHiToss = 1UL << (cbitToss - 1);
        IX tmp = (IX)(value >> cbitToss);
        ulong lo = value & ((1UL << cbitToss) - 1);
        if ((lo >= bitHiToss) && (lo > bitHiToss || (tmp & 1) != 0))
            tmp++;
        return (RX)(1L << cbitToss) * (RX)tmp;
#else
        // Shift so the high bit is clear, then use long -> RX. Or in the lost bit to get correct rounding.
        long tmp = (long)((value >> 1) | (value & 1));
        var res = (RX)tmp;
        return res + res;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this double value) => (RX)value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this float value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this long value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this int value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this short value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this sbyte value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this uint value) => (long)value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this ushort value) => (int)value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR8(this byte value) => (int)value;

    /// <summary>
    /// This does a RX equality test where two nan values are considered equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Eq(RX a, RX b)
    {
        // The Benchmark project shows that these are equivalent, performance wise.
        return a.Equals(b);
        // return EqualityComparer<RX>.Default.Equals(a, b);
        // return a == b || RX.IsNaN(b) && RX.IsNaN(a);
        // return a == b || b != b && a != a;
    }

    /// <summary>
    /// Tests for <paramref name="a"/> being less than <paramref name="b"/>, where
    /// nan is less than anything else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Lt(RX a, RX b)
    {
        if (a < b)
            return true;
        if (a >= b)
            return false;

        // At least one must be NaN. The answer is false iff b is NaN.
        Validation.Assert(RX.IsNaN(a) | RX.IsNaN(b));
        return !RX.IsNaN(b);
    }

    /// <summary>
    /// Tests for <paramref name="a"/> being greater than <paramref name="b"/>, where
    /// nan is less than anything else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Gt(RX a, RX b) => Lt(b, a);

    /// <summary>
    /// Tests for <paramref name="a"/> being less than or equal to <paramref name="b"/>, where
    /// nan is less than anything else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Le(RX a, RX b)
    {
        if (a <= b)
            return true;
        if (a > b)
            return false;

        // At least one must be NaN. The answer is true iff a is nan.
        Validation.Assert(RX.IsNaN(a) | RX.IsNaN(b));
        return RX.IsNaN(a);
    }

    /// <summary>
    /// Tests for <paramref name="a"/> being greater than or equal to <paramref name="b"/>, where
    /// nan is less than anything else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Ge(RX a, RX b) => Le(b, a);

    public static RxValueKind GetValueKind(this RX value)
    {
        var bits = value.ToBits();

        if ((bits & Bits.MaskExp) < Bits.MaskExp)
        {
            if (bits == 0)
                return RxValueKind.Zero;
            if (bits << 1 == 0)
                return RxValueKind.NegZero;
            return RxValueKind.Standard;
        }

        if ((bits & Bits.MaskMan) > 0)
            return RxValueKind.NaN;
        return (IX)bits < 0 ? RxValueKind.NegInfinity : RxValueKind.Infinity;
    }

    /// <summary>
    /// Convert the RX to an I8, using mod 2^64.
    /// </summary>
    public static long ModToI8(this RX value)
    {
        UX raw = value.ToBits();

        // Get the base-2 exponent.
        int exp = ((int)(raw >> Bits.CbitMan) & Bits.RawExpInf) - Bits.RawExpZero;

        // If the exponent is negative or too big, the result is zero.
        if ((uint)exp >= Bits.CbitMan + 64)
            return 0;

        // Get the value, not considering exponent.
        long val = (long)(raw & Bits.MaskMan) | (1L << Bits.CbitMan);

        // Shift to adjust for the exponent. Note that negation needs to be applied differently
        // for the two shift directions.
        exp -= Bits.CbitMan;
        if (exp < 0)
            val >>= -exp;
        if ((IX)raw < 0)
            val = -val;
        if (exp > 0)
            val <<= exp;
        return val;
    }

    /// <summary>
    /// Convert the RX to an I8. Sets <paramref name="result"/> to zero
    /// if <paramref name="value"/> is outside the range of I8.
    /// </summary>
    public static bool TryToI8(this RX value, out long result)
    {
        UX raw = value.ToBits();

        // Get the base-2 exponent.
        int exp = ((int)(raw >> Bits.CbitMan) & Bits.RawExpInf) - Bits.RawExpZero;

        // If the exponent is negative or too big, the result is zero.
        if ((uint)exp >= Bits.CbitMan + 64)
        {
            result = 0;
            return exp < 0;
        }

        // Get the value, not considering exponent.
        result = (long)(raw & Bits.MaskMan) | (1L << Bits.CbitMan);

        // Shift to adjust for the exponent. Note that negation needs to be applied differently
        // for the two shift directions.
        exp -= Bits.CbitMan;
        if (exp < 0)
            result >>= -exp;
        if ((IX)raw < 0)
            result = -result;

        if (exp > 0)
        {
            if (exp >= (63 - Bits.CbitMan) && raw != Bits.BitsMinI8)
            {
                result = default;
                return false;
            }
            result <<= exp;
        }

        return true;
    }

    /// <summary>
    /// Convert the RX to an U8. Sets <paramref name="result"/> to zero
    /// if <paramref name="value"/> is outside the range of U8.
    /// </summary>
    public static bool TryToU8(this RX value, out ulong result)
    {
        UX raw = ToBits(value);

        // Get the base-2 exponent.
        int exp = ((int)(raw >> Bits.CbitMan) & Bits.RawExpInf) - Bits.RawExpZero;

        // If the exponent is negative or too big, the result is zero.
        if ((uint)exp >= Bits.CbitMan + 64)
        {
            result = 0;
            return exp < 0;
        }
        if ((IX)raw < 0)
        {
            result = default;
            return false;
        }

        // Get the value, not considering exponent.
        result = (ulong)(raw & Bits.MaskMan) | (1UL << Bits.CbitMan);

        // Shift to adjust for the exponent. Note that negation needs to be applied differently
        // for the two shift directions.
        exp -= Bits.CbitMan;
        if (exp < 0)
            result >>= -exp;
        else if (exp > 0)
        {
            if (exp >= (64 - Bits.CbitMan))
            {
                result = default;
                return false;
            }
            result <<= exp;
        }

        return true;
    }

    /// <summary>
    /// Returns whether the value is infinite (positive or negative). Returns false for nan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInfinite(this RX value)
    {
        return (value.ToBits() & ~Bits.MaskSign) == Bits.MaskExp;
    }

    /// <summary>
    /// Returns whether the value is finite. Returns false for infinities and nan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(this RX value)
    {
        return (value.ToBits() & Bits.MaskExp) < Bits.MaskExp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegative(this RX value) => (IX)value.ToBits() < 0;

    /// <summary>
    /// Convert the RX to a tick count for a <see cref="DateTime"/>, or -1 if it is out of bounds.
    /// </summary>
    public static long ToDateTicks(this RX value)
    {
        UX raw = value.ToBits();
        if ((IX)raw < 0)
        {
            // Negative value or -0.0. Map -0.0 to 0, the rest to -1.
            return raw == Bits.MaskSign ? 0 : -1;
        }

        // Get the base-2 exponent.
        int exp = ((int)(raw >> Bits.CbitMan) & Bits.RawExpInf) - Bits.RawExpZero;

        // If the exponent is negative, the result is 0.
        // If the exponent is too big, the result is -1.
        if ((uint)exp >= 62)
            return ~(exp >> 31);

        // Get the value, not considering exponent.
        long val = (long)(raw & Bits.MaskMan) | (1L << Bits.CbitMan);

        // Shift to adjust for the exponent. Note that negation needs to be applied differently
        // for the two shift directions.
        Validation.AssertIndex(exp, 62);
        exp -= Bits.CbitMan;
        if (exp <= 0)
            return val >> -exp;

        val <<= exp;
        if (val > ChronoUtil.TicksMax)
            return -1;

        return val;
    }

    /// <summary>
    /// Returns the bits of the double as a ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UX ToBits(this RX value) => BitConverter.DoubleToUInt64Bits(value);

    /// <summary>
    /// Returns the double whose bit representation is the given ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RX ToDouble(this ulong value) => BitConverter.UInt64BitsToDouble(value);

    /// <summary>
    /// Contains definitions related to the bit layout of a double value.
    /// </summary>
    public static class DoubleBits
    {
        // Bit layout of double:
        // 1 sign bit
        // 11 exponent bits
        // 52 mantissa bits (with an implicit 1 in front, except for denormals).

        /// <summary>
        /// Mask for the sign bit.
        /// </summary>
        public const ulong MaskSign = 0x8000_0000_0000_0000UL;

        /// <summary>
        /// Mask for the exponent bits.
        /// </summary>
        public const ulong MaskExp = 0x7FF0_0000_0000_0000UL;

        /// <summary>
        /// Mask for the mantissa bits.
        /// </summary>
        public const ulong MaskMan = 0x000F_FFFF_FFFF_FFFFUL;

        /// <summary>
        /// The bits for the double representation of the smallest I8 value.
        /// </summary>
        public const ulong BitsMinI8 = 0xC3E0_0000_0000_0000UL;

        /// <summary>
        /// The raw exponent value for infinities and nan.
        /// </summary>
        public const int RawExpInf = 0x7FF;

        /// <summary>
        /// The raw exponent value for "1", when the exponent is logically zero.
        /// </summary>
        public const int RawExpZero = 0x3FF;

        /// <summary>
        /// The base two exponent for infinity (without the bias).
        /// </summary>
        public const int ExpInf = RawExpInf - RawExpZero;

        /// <summary>
        /// The maximum number of bytes of a big integer representation of a finite value.
        /// </summary>
        public const int CbMaxInt = (ExpInf + 7) >> 3;

        /// <summary>
        /// Total number of bits.
        /// </summary>
        public const int CbitTot = 64;

        /// <summary>
        /// Number of mantissa bits.
        /// </summary>
        public const int CbitMan = 52;

        /// <summary>
        /// Number of exponent bits.
        /// </summary>
        public const int CbitExp = 11;

        /// <summary>
        /// Returns the double whose bit representation is the given ulong.
        /// </summary>
        public static double FromBits(ulong value) => BitConverter.UInt64BitsToDouble(value);
    }
}
