// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// NOTE: Diff NumUtil.R8.cs and NumUtil.R4.cs. Differences should be minimal.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Utility;

using Bits = NumUtil.FloatBits;
using Integer = System.Numerics.BigInteger;
using IX = System.Int32;
using RX = System.Single;
using UX = System.UInt32;

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
    /// The IA to R4 standard conversion is not accurate. Use this instead.
    /// </summary>
    public static RX ToR4(this Integer value)
    {
        bool neg = value.Sign < 0;
        if (neg)
            value = -value;

        RX res;
        long cb = value.GetByteCount(isUnsigned: true);
        if (cb <= sizeof(ulong))
            res = ((ulong)value).ToR4();
        else if (cb > Bits.CbMaxInt)
            res = RX.PositiveInfinity;
        else
            res = ToR4Core(in value, cb);

        return neg ? -res : res;
    }

    [SkipLocalsInit]
    private static RX ToR4Core(in Integer value, long cb)
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
    /// The U8 to R4 standard conversion is not accurate. Use this instead.
    /// REVIEW: https://github.com/dotnet/runtime/issues/43895.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RX ToR4(this ulong value)
    {
        // NOTE: Make sure to use `ToR4()` rather than cast to RX. See the comment on ToR4 below.

        // If the high bit is not set, just use the long -> RX conversion.
        if ((long)value >= 0)
            return ((long)value).ToR4();
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
        var res = tmp.ToR4();
        return res + res;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this double value) => (RX)value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this float value) => value;
    /// <summary>
    /// REVIEW: Inlining this can produce an incorrect result. Hopefully this will be fixed in .net 8.
    /// Once it is, switch back to aggressive inlining. See https://github.com/dotnet/runtime/pull/90325.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] public static RX ToR4(this long value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this int value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this short value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this sbyte value) => value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this uint value) => (long)value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this ushort value) => (int)value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static RX ToR4(this byte value) => (int)value;

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
    /// Returns the bits of the float as a uint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UX ToBits(this RX value) => BitConverter.SingleToUInt32Bits(value);

    /// <summary>
    /// Returns the float whose bit representation is the given uint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RX ToFloat(this uint value) => BitConverter.UInt32BitsToSingle(value);

    /// <summary>
    /// Contains definitions related to the bit layout of a float value.
    /// </summary>
    public static class FloatBits
    {
        // Bit layout of float:
        // 1 sign bit
        // 8 exponent bits
        // 23 mantissa bits (with an implicit 1 in front, except for denormals).

        /// <summary>
        /// Mask for the sign bit.
        /// </summary>
        public const uint MaskSign = 0x8000_0000U;

        /// <summary>
        /// Mask for the exponent bits.
        /// </summary>
        public const uint MaskExp = 0x7F80_0000U;

        /// <summary>
        /// Mask for the mantissa bits.
        /// </summary>
        public const uint MaskMan = 0x007F_FFFFU;

        /// <summary>
        /// The bits for the float representation of the smallest I8 value.
        /// </summary>
        public const ulong BitsMinI8 = 0xDF00_0000U;

        /// <summary>
        /// The raw exponent value for infinities and nan.
        /// </summary>
        public const int RawExpInf = 0xFF;

        /// <summary>
        /// The raw exponent value for "1", when the exponent is logically zero.
        /// </summary>
        public const int RawExpZero = 0x7F;

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
        public const int CbitTot = 32;

        /// <summary>
        /// Number of mantissa bits.
        /// </summary>
        public const int CbitMan = 23;

        /// <summary>
        /// Number of exponent bits.
        /// </summary>
        public const int CbitExp = 8;

        /// <summary>
        /// Returns the float whose bit representation is the given uint.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FromBits(uint value) => BitConverter.UInt32BitsToSingle(value);
    }
}
