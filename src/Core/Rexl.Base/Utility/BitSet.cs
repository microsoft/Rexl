// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

// Note: if this changes, eg, to System.UInt32, then the _blobOne and _shiftIblob constants also need
// to change.
using Blob = System.UInt64;
using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// Represents a set of non-negative integers as a bit vector. This is intended for only when the maximum
/// integer used is relatively small. The <see cref="BitSet"/> is immutable. Any operations create a new one.
/// When the maximum integer is less than 64, it uses no memory allocation. Otherwise, it allocates an array
/// with one slot for each additional 64 bits.
/// </summary>
public struct BitSet : IEnumerable<int>, IEquatable<BitSet>
{
    // The bit index is the low 6 bits.
    private const Blob _blobOne = 1UL;
    private const Blob _blobAll = ~0UL;
    private const int _shiftIblob = 6;
    private const int _cbitBlob = 1 << _shiftIblob;
    private const int _maskIbit = _cbitBlob - 1;

    private readonly Blob _lo;
    // Note that _hi is never of length zero but may be null. Also its highest blob is always non-zero.
    // REVIEW: Should we use an Immutable.Array<Blob> for this?
    private readonly Blob[]? _hi;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BitSet(Blob lo, Blob[]? hi = null)
    {
        _lo = lo;
        _hi = hi;
        AssertValid();
    }

    /// <summary>
    /// Gets whether the bitset is empty, having no bits set.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            AssertValid();
            return _lo == 0 && _hi == null;
        }
    }

    /// <summary>
    /// Returns the slot of the high bit, or -1 if no bits are set.
    /// </summary>
    public int SlotMax
    {
        get
        {
            AssertValid();

            Blob bits;
            int iblob;

            if (_hi == null)
            {
                iblob = 0;
                bits = _lo;
            }
            else
            {
                iblob = _hi.Length;
                bits = _hi[iblob - 1];
                Validation.Assert(bits != 0);
            }

            return iblob * _cbitBlob + Util.IbitHigh(bits);
        }
    }

    /// <summary>
    /// Returns the slot of the low bit, or -1 if no bits are set.
    /// </summary>
    public int SlotMin
    {
        get
        {
            AssertValid();

            Blob bits;
            int iblob;

            if (_lo != 0)
            {
                iblob = 0;
                bits = _lo;
            }
            else if (_hi == null)
                return -1;
            else
            {
                iblob = 1;
                while (iblob < _hi.Length && _hi[iblob - 1] == 0)
                    iblob++;
                Validation.Assert(iblob <= _hi.Length);
                bits = _hi[iblob - 1];
                Validation.Assert(bits != 0);
            }

            // IbitLow produces the wrong value if bits is zero, so that case is handled above.
            Validation.Assert(bits != 0);
            return iblob * _cbitBlob + Util.IbitLow(bits);
        }
    }

    /// <summary>
    /// The count of slots that are in the bit set.
    /// </summary>
    public int Count
    {
        get
        {
            AssertValid();

            int count = _lo != 0 ? Util.CountBits(_lo) : 0;
            if (_hi != null)
            {
                foreach (var blob in _hi)
                {
                    if (blob != 0)
                        count += Util.CountBits(blob);
                }
            }
            return count;
        }
    }

    [Conditional("DEBUG")]
    private void AssertValid()
    {
#if DEBUG
        if (_hi != null)
        {
            Validation.Assert(_hi.Length > 0);
            Validation.Assert(_hi[_hi.Length - 1] != 0);
        }
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Blob[] Clone(Blob[] hi)
    {
        Validation.AssertValue(hi);
        return (Blob[])hi.Clone();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Blob[] CloneGrow(Blob[]? hi, int cblob)
    {
        Validation.AssertValueOrNull(hi);
        Validation.Assert(Util.Size(hi) < cblob);
        var hiNew = hi;
        Array.Resize(ref hiNew, cblob);
        Validation.Assert(hiNew != hi);
        return hiNew;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Blob[] CloneShrink(Blob[] hi, int cblob)
    {
        Validation.AssertValue(hi);
        Validation.Assert(hi.Length > cblob);
        var hiNew = hi;
        Array.Resize(ref hiNew, cblob);
        Validation.Assert(hiNew != hi);
        return hiNew;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Blob[] SetBlob(Blob[] hi, int iblob, Blob blob)
    {
        Validation.AssertValue(hi);
        Validation.Assert(0 <= iblob & iblob < hi.Length);
        if (blob == hi[iblob])
            return hi;

        var res = (Blob[])hi.Clone();
        res[iblob] = blob;
        return res;
    }

    /// <summary>
    /// Constructor from array of bools.
    /// </summary>
    public BitSet(params bool[] items)
        : this(items.AsSpan())
    {
    }

    /// <summary>
    /// Constructor from span of bools.
    /// </summary>
    public BitSet(ReadOnlySpan<bool> items)
    {
        int len = items.Length;
        while (len > 0 && !items[len - 1])
            len--;

        if (len < items.Length)
            items = items.Slice(0, len);
        int cblob = (len + _cbitBlob - 1) >> _shiftIblob;

        Blob lo = 0;
        Blob[]? hi = cblob > 1 ? new Blob[cblob - 1] : null;
        int iblob = -1;
        int ibit = 0;
        for (int i = 0; i < items.Length; i++)
        {
            Validation.Assert(0 <= ibit & ibit < _cbitBlob);
            if (items[i])
            {
                Blob bit = _blobOne << ibit;
                if (iblob < 0)
                {
                    Validation.Assert(lo < bit);
                    lo |= bit;
                }
                else
                {
                    Validation.AssertValue(hi);
                    Validation.AssertIndex(iblob, hi.Length);
                    Validation.Assert(hi[iblob] < bit);
                    hi[iblob] |= bit;
                }
            }
            if (++ibit >= _cbitBlob)
            {
                iblob++;
                ibit = 0;
            }
        }

        _lo = lo;
        _hi = hi;
    }

    /// <summary>
    /// Constructor from enumerable of bools.
    /// </summary>
    public BitSet(IEnumerable<bool> items)
    {
        Validation.BugCheckValue(items, nameof(items));

        Blob lo = 0;
        List<Blob>? hi = null;
        int iblob = -1;
        int ibit = 0;
        foreach (bool item in items)
        {
            Validation.Assert(0 <= ibit & ibit < _cbitBlob);
            if (item)
            {
                Blob bit = _blobOne << ibit;
                if (iblob < 0)
                {
                    Validation.Assert(lo < bit);
                    lo |= bit;
                }
                else
                {
                    hi ??= new List<Blob>();
                    while (hi.Count <= iblob)
                        hi.Add(0);
                    Validation.Assert(hi.Count == iblob + 1);
                    Validation.Assert(hi[iblob] < bit);
                    hi[iblob] |= bit;
                }
            }
            if (++ibit >= _cbitBlob)
            {
                iblob++;
                ibit = 0;
            }
        }

        _lo = lo;
        _hi = hi?.ToArray();
    }

    /// <summary>
    /// Constructor from span of bytes.
    /// </summary>
    public BitSet(ReadOnlySpan<byte> items)
    {
        int len = items.Length;
        while (len > 0 && items[len - 1] == 0)
            len--;

        if (len < items.Length)
            items = items.Slice(0, len);
        int cblob = (len + (_cbitBlob / 8) - 1) >> (_shiftIblob - 3);

        Blob lo = 0;
        Blob[]? hi = cblob > 1 ? new Blob[cblob - 1] : null;
        int iblob = -1;
        int ibit = 0;
        for (int i = 0; i < items.Length; i++)
        {
            Validation.Assert(0 <= ibit & ibit < _cbitBlob);
            Validation.Assert((ibit & 7) == 0);
            byte item = items[i];
            if (item != 0)
            {
                Blob bits = (Blob)item << ibit;
                if (iblob < 0)
                {
                    Validation.Assert(lo < (_blobOne << ibit));
                    lo |= bits;
                }
                else
                {
                    Validation.AssertValue(hi);
                    Validation.AssertIndex(iblob, hi.Length);
                    Validation.Assert(hi[iblob] < (_blobOne << ibit));
                    hi[iblob] |= bits;
                }
            }
            if ((ibit += 8) >= _cbitBlob)
            {
                iblob++;
                ibit = 0;
            }
        }

        _lo = lo;
        _hi = hi;
    }

    /// <summary>
    /// Returns a mask for all "slots" up to, but not including, the given one.
    /// </summary>
    public static BitSet GetMask(int slotLim)
    {
        Validation.BugCheckParam(slotLim >= 0, nameof(slotLim));

        // Get the index of the top bit, not one past.
        int slot = slotLim - 1;
        if (slot < 0)
            return default;

        int ibit = slot & _maskIbit;
        Blob blobHi = (_blobOne << ibit << 1) - 1;
        int iblob = slot >> _shiftIblob;
        if (iblob <= 0)
            return new BitSet(blobHi);

        var hi = new Blob[iblob];
        int i = 0;
        while (i < hi.Length - 1)
            hi[i++] = _blobAll;
        Validation.Assert(i == hi.Length - 1);
        hi[i] = blobHi;
        return new BitSet(_blobAll, hi);
    }

    /// <summary>
    /// Returns a mask for all slots from the given min up to, but not including, the given lim.
    /// </summary>
    public static BitSet GetMask(int slotMin, int slotLim)
    {
        Validation.BugCheckParam(slotMin >= 0, nameof(slotMin));
        Validation.BugCheckParam(slotLim >= slotMin, nameof(slotLim));

        // Get the index of the top bit, not one past.
        if (slotMin >= slotLim)
            return default;

        int slotMax = slotLim - 1;
        int iblobMin = slotMin >> _shiftIblob;
        int iblobMax = slotMax >> _shiftIblob;
        int ibitMin = slotMin & _maskIbit;
        int ibitMax = slotMax & _maskIbit;

        Blob blobMax = (_blobOne << ibitMax << 1) - 1;
        Blob blobMin = ~((_blobOne << ibitMin) - 1);

        if (iblobMax <= 0)
            return new BitSet(blobMin & blobMax);

        var hi = new Blob[iblobMax];
        int i = Math.Max(0, iblobMin - 1);
        while (i < hi.Length - 1)
            hi[i++] = _blobAll;
        Validation.Assert(i == hi.Length - 1);
        hi[i] = blobMax;

        if (iblobMin <= 0)
            return new BitSet(blobMin, hi);

        hi[iblobMin - 1] &= blobMin;
        return new BitSet(0, hi);
    }

    /// <summary>
    /// This is so we can use hex literals when a <see cref="BitSet"/> is needed.
    /// </summary>
    public static implicit operator BitSet(Blob bits)
    {
        return new BitSet(bits);
    }

    /// <summary>
    /// The low bunch of bits.
    /// </summary>
    public Blob LoBlob => _lo;

    /// <summary>
    /// The high bunches of bits.
    /// </summary>
    public ReadOnly.Array<Blob> HiBlobs => _hi;

    /// <summary>
    /// Whether this bit set fits just in low bits.
    /// </summary>
    public bool IsLo => _hi == null;

    /// <summary>
    /// Tests whether this is a subset of <paramref name="big"/>.
    /// </summary>
    public bool IsSubset(BitSet big)
    {
        if ((_lo & ~big._lo) != 0)
            return false;
        if (_hi == null)
            return true;
        if (big._hi == null)
            return false;
        if (_hi.Length > big._hi.Length)
            return false;
        for (int i = 0; i < _hi.Length; i++)
        {
            if ((_hi[i] & ~big._hi[i]) != 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Tests whether this intersects <paramref name="other"/>.
    /// </summary>
    public bool Intersects(BitSet other)
    {
        if ((_lo & other._lo) != 0)
            return true;
        if (_hi == null)
            return false;
        if (other._hi == null)
            return false;
        int len = Math.Min(_hi.Length, other._hi.Length);
        for (int i = 0; i < len; i++)
        {
            if ((_hi[i] & other._hi[i]) != 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Test whether the given bit or any higher bit is set.
    /// </summary>
    public bool TestAtOrAbove(int ibit)
    {
        AssertValid();
        Validation.BugCheckParam(ibit >= 0, nameof(ibit));

        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;

        if (iblob == 0)
            return _hi != null || _lo >= (_blobOne << ibit);
        if (_hi == null)
            return false;

        iblob--;
        Validation.Assert(iblob >= 0);
        int cblob = _hi.Length;
        if (iblob >= cblob)
            return false;

        return iblob < cblob - 1 || _hi[iblob] >= (_blobOne << ibit);
    }

    /// <summary>
    /// Test whether the given bit is set.
    /// </summary>
    public bool TestBit(int ibit)
    {
        AssertValid();
        Validation.BugCheckParam(ibit >= 0, nameof(ibit));

        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;

        Blob bit = _blobOne << ibit;
        if (iblob == 0)
            return (_lo & bit) != 0;
        if (_hi == null)
            return false;

        iblob--;
        Validation.Assert(iblob >= 0);
        int cblob = _hi.Length;
        if (iblob >= cblob)
            return false;

        return (_hi[iblob] & bit) != 0;
    }

    /// <summary>
    /// Set the given bit and return the result.
    /// </summary>
    public BitSet SetBit(int ibit)
    {
        AssertValid();
        Validation.BugCheckParam(ibit >= 0, nameof(ibit));

        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;

        Blob bit = _blobOne << ibit;
        if (iblob == 0)
            return new BitSet(_lo | bit, _hi);

        iblob--;
        Validation.Assert(iblob >= 0);
        if (_hi != null && iblob < _hi.Length)
            return new BitSet(_lo, SetBlob(_hi, iblob, _hi[iblob] | bit));

        var hi = CloneGrow(_hi, iblob + 1);
        hi[iblob] = bit;
        return new BitSet(_lo, hi);
    }

    /// <summary>
    /// Clear the given bit and return the result.
    /// </summary>
    public BitSet ClearBit(int ibit)
    {
        AssertValid();
        Validation.BugCheckParam(ibit >= 0, nameof(ibit));

        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;

        Blob bit = _blobOne << ibit;
        if (iblob == 0)
            return new BitSet(_lo & ~bit, _hi);
        if (_hi == null)
            return this;

        iblob--;
        Validation.Assert(iblob >= 0);
        int cblob = _hi.Length;
        if (iblob >= cblob)
            return this;

        if ((_hi[iblob] & bit) == 0)
            return this;

        // See if it can shrink.
        if (iblob == cblob - 1 && _hi[iblob] == bit)
        {
            cblob--;
            while (cblob > 0 && _hi[cblob - 1] == 0)
                cblob--;
            if (cblob == 0)
                return new BitSet(_lo);
            return new BitSet(_lo, CloneShrink(_hi, cblob));
        }

        return new BitSet(_lo, SetBlob(_hi, iblob, _hi[iblob] & ~bit));
    }

    /// <summary>
    /// Clear the given bit and all higher bits and return the result.
    /// </summary>
    public BitSet ClearAtAndAbove(int ibit)
    {
        AssertValid();
        Validation.BugCheckParam(ibit >= 0, nameof(ibit));

        if (ibit == 0)
            return default;

        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;

        Blob mask = (_blobOne << ibit) - 1;
        if (iblob == 0)
            return new BitSet(_lo & mask);
        if (_hi == null)
            return this;

        iblob--;
        Validation.Assert(iblob >= 0);
        int cblob = _hi.Length;
        if (iblob >= cblob)
            return this;

        if (iblob == cblob - 1 && (_hi[iblob] & ~mask) == 0)
            return this;

        // See if it can shrink.
        if ((_hi[iblob] & mask) == 0)
        {
            cblob = iblob;
            while (cblob > 0 && _hi[cblob - 1] == 0)
                cblob--;
            if (cblob == 0)
                return new BitSet(_lo);
            return new BitSet(_lo, CloneShrink(_hi, cblob));
        }

        var hi = iblob < cblob - 1 ? CloneShrink(_hi, iblob + 1) : Clone(_hi);
        hi[iblob] &= mask;
        Validation.Assert(hi[iblob] != 0);
        return new BitSet(_lo, hi);
    }

    /// <summary>
    /// Insert a bit at the indicated index and with the given value.
    /// </summary>
    public BitSet Insert(int ibit, bool value)
    {
        AssertValid();

        if (!TestAtOrAbove(ibit))
            return value ? SetBit(ibit) : ClearBit(ibit);

        int cblob = Util.Size(_hi);
        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;
        Validation.AssertIndex(iblob, cblob + 1);
        Validation.AssertIndex(ibit, _cbitBlob);

        // Insert the bit in the appropriate blob.
        Blob bit = _blobOne << ibit;
        Blob src = iblob == 0 ? _lo : _hi![iblob - 1];
        Blob blob = src & (bit - 1);
        blob |= (src ^ blob) << 1;
        if (value)
            blob |= bit;
        Blob carry = src >> (_cbitBlob - 1);
        Validation.Assert(carry <= 1);

        if (cblob == 0)
            return new BitSet(blob, carry != 0 ? new Blob[] { carry } : null);

        Blob blobHi = _hi![cblob - 1] >> (_cbitBlob - 1);
        Blob[] hi;
        if (blobHi == 0)
            hi = Clone(_hi);
        else
        {
            hi = _hi;
            Array.Resize(ref hi, cblob + 1);
            hi[cblob] = blobHi;
        }

        Blob lo;
        if (iblob == 0)
            lo = blob;
        else
        {
            lo = _lo;
            hi[iblob - 1] = blob;
        }

        for (int i = iblob; i < cblob; i++)
        {
            src = hi[i];
            hi[i] = (src << 1) | carry;
            carry = src >> (_cbitBlob - 1);
        }
        Validation.Assert(carry == blobHi);

        return new BitSet(lo, hi);
    }

    /// <summary>
    /// Delete the bit at the indicated index.
    /// </summary>
    public BitSet Delete(int ibit)
    {
        AssertValid();

        if (!TestAtOrAbove(ibit + 1))
            return ClearBit(ibit);

        int cblob = Util.Size(_hi);
        int iblob = ibit >> _shiftIblob;
        ibit &= _maskIbit;
        Validation.AssertIndex(iblob, cblob + 1);
        Validation.AssertIndex(ibit, _cbitBlob);

        Blob bit = _blobOne << ibit;
        Blob src = iblob == 0 ? _lo : _hi![iblob - 1];
        src &= ~bit;
        Blob blob = src & (bit - 1);
        blob |= (src ^ blob) >> 1;
        Validation.Assert((blob >> (_cbitBlob - 1)) == 0);

        if (cblob == 0)
            return new BitSet(blob);

        Blob[] hi;
        Blob blobHi = _hi![cblob - 1];
        Validation.Assert(blobHi != 0);
        if (blobHi == 1)
        {
            Validation.AssertIndex(iblob, cblob);
            if (cblob == 1)
                return new BitSet(blob | (blobHi << (_cbitBlob - 1)));
            hi = _hi;
            Array.Resize(ref hi, cblob - 1);
        }
        else
        {
            Validation.Assert(blobHi >= 2);
            hi = Clone(_hi);
            blobHi = 0;
        }

        for (int i = hi.Length; --i >= iblob;)
        {
            src = hi[i];
            hi[i] = (src >> 1) | (blobHi << (_cbitBlob - 1));
            blobHi = src;
        }
        blob |= blobHi << (_cbitBlob - 1);

        Blob lo;
        if (iblob == 0)
            lo = blob;
        else
        {
            lo = _lo;
            hi[iblob - 1] = blob;
        }

        return new BitSet(lo, hi);
    }

    /// <summary>
    /// Shift the bit set left.
    /// REVIEW: Should we also implement right shift?
    /// </summary>
    public static BitSet operator <<(BitSet bs, int shift)
    {
        bs.AssertValid();
        Validation.BugCheckParam(shift >= 0, nameof(shift));

        if (shift == 0 || bs.IsEmpty)
            return bs;

        int cblobLeft = shift >> _shiftIblob;
        int cbitLeft = shift ^ (cblobLeft << _shiftIblob);
        Validation.AssertIndex(cbitLeft, _cbitBlob);

        int cblobSrc = Util.Size(bs._hi);
        int cblobDst = cblobSrc + cblobLeft;

        Blob blobHi;
        if (cbitLeft == 0)
            blobHi = 0;
        else
        {
            blobHi = cblobSrc == 0 ? bs._lo : bs._hi![cblobSrc - 1];
            blobHi >>= _cbitBlob - cbitLeft;
        }

        if (cblobDst == 0 && blobHi == 0)
            return new BitSet(bs._lo << cbitLeft);

        Blob[] hi;
        if (blobHi == 0)
            hi = new Blob[cblobDst];
        else
        {
            hi = new Blob[cblobDst + 1];
            hi[cblobDst] = blobHi;
        }
        Validation.Assert(hi.Length == cblobDst || hi.Length == cblobDst + 1);
        Validation.Assert((blobHi == 0) == (hi.Length == cblobDst));
        Validation.Assert(hi.Length == cblobDst || hi[cblobDst] == blobHi);

        Blob lo = 0;
        if (cbitLeft == 0)
        {
            if (cblobSrc > 0)
                Array.Copy(bs._hi!, 0, hi, cblobLeft, cblobSrc);
            hi[cblobLeft - 1] = bs._lo;
            lo = 0;
        }
        else
        {
            int cbitRight = _cbitBlob - cbitLeft;
            Blob blobCur = bs._lo;
            if (cblobLeft == 0)
                lo = blobCur << cbitLeft;
            else
                hi[cblobLeft - 1] = blobCur << cbitLeft;
            for (int i = 0; i < cblobSrc; i++)
            {
                var blobPrev = blobCur;
                blobCur = bs._hi![i];
                hi[cblobLeft + i] = (blobCur << cbitLeft) | (blobPrev >> cbitRight);
            }
            Validation.Assert((blobCur >>= cbitRight) == blobHi);
        }
        return new BitSet(lo, hi);
    }

    /// <summary>
    /// Computes the bit-wise "or", which is equivalent to the union.
    /// </summary>
    public static BitSet operator |(BitSet bs0, BitSet bs1)
    {
        bs0.AssertValid();
        bs1.AssertValid();

        if (bs0._hi == null)
            return new BitSet(bs0._lo | bs1._lo, bs1._hi);
        int cblob0 = bs0._hi.Length;

        if (bs1._hi == null)
            return new BitSet(bs0._lo | bs1._lo, bs0._hi);
        int cblob1 = bs1._hi.Length;

        // REVIEW: Perhaps we should optimize the case when one of the arrays is
        // a subset of the other, in which case we can use the superset array.
        Blob[] hi;
        Blob[] other;
        int cblobOther;
        if (cblob0 >= cblob1)
        {
            hi = Clone(bs0._hi);
            other = bs1._hi;
            cblobOther = cblob1;
        }
        else
        {
            hi = Clone(bs1._hi);
            other = bs0._hi;
            cblobOther = cblob0;
        }

        for (int i = 0; i < cblobOther; i++)
            hi[i] |= other[i];

        return new BitSet(bs0._lo | bs1._lo, hi);
    }

    /// <summary>
    /// Computes the bit-wise "and" which is equivalent to the intersection.
    /// </summary>
    public static BitSet operator &(BitSet bs0, BitSet bs1)
    {
        bs0.AssertValid();
        bs1.AssertValid();

        if (bs0._hi == null || bs1._hi == null)
            return new BitSet(bs0._lo & bs1._lo);

        int cblob0 = bs0._hi.Length;
        int cblob1 = bs1._hi.Length;
        int cblob = Math.Min(cblob0, cblob1);
        Validation.Assert(cblob > 0);

        while ((bs0._hi![cblob - 1] & bs1._hi![cblob - 1]) == 0)
        {
            if (--cblob == 0)
                return new BitSet(bs0._lo & bs1._lo);
        }

        // REVIEW: Perhaps we should optimize the case when one of the arrays is
        // a subset of the other, in which case we can use the subset array.
        Blob[] hi = new Blob[cblob];
        for (int i = 0; i < cblob; i++)
            hi[i] = bs0._hi[i] & bs1._hi[i];

        return new BitSet(bs0._lo & bs1._lo, hi);
    }

    /// <summary>
    /// Computes the set difference.
    /// REVIEW: Should we implement ^, which would be the symmetric set difference?
    /// </summary>
    public static BitSet operator -(BitSet bs0, BitSet bs1)
    {
        bs0.AssertValid();
        bs1.AssertValid();

        if (bs0._hi == null)
            return new BitSet(bs0._lo & ~bs1._lo);
        int cblob0 = bs0._hi.Length;

        if (bs1._hi == null)
            return new BitSet(bs0._lo & ~bs1._lo, bs0._hi);

        int cblob = cblob0;
        int cblob1 = bs1._hi.Length;
        if (cblob <= cblob1)
        {
            Validation.AssertValue(bs1._hi);
            while (cblob > 0 && (bs0._hi[cblob - 1] & ~bs1._hi[cblob - 1]) == 0)
                cblob--;
        }

        if (cblob == 0)
            return new BitSet(bs0._lo & ~bs1._lo);

        // REVIEW: Perhaps we should optimize the case when one of the arrays is
        // a subset of the other, in which case we can use the subset array.
        Blob[] hi = new Blob[cblob];
        int cblobMin = Math.Min(cblob, cblob1);
        int i = 0;
        for (; i < cblobMin; i++)
            hi[i] = bs0._hi[i] & ~bs1._hi![i];
        for (; i < cblob; i++)
            hi[i] = bs0._hi[i];

        return new BitSet(bs0._lo & ~bs1._lo, hi);
    }

    public static bool operator ==(BitSet bs0, BitSet bs1)
    {
        bs0.AssertValid();
        bs1.AssertValid();

        if (bs0._lo != bs1._lo)
            return false;
        if (bs0._hi == null)
            return bs1._hi == null;
        if (bs1._hi == null)
            return false;
        if (bs0._hi.Length != bs1._hi.Length)
            return false;

        for (int i = 0; i < bs0._hi.Length; i++)
        {
            if (bs0._hi[i] != bs1._hi[i])
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BitSet bs0, BitSet bs1)
    {
        return !(bs0 == bs1);
    }

    public bool Equals(BitSet other)
    {
        return this == other;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BitSet bs)
            return false;
        return this == bs;
    }

    public override int GetHashCode()
    {
        AssertValid();

        HashCode code = new HashCode();
        code.Add(_lo);
        if (_hi != null)
        {
            for (int i = _hi.Length; --i >= 0;)
                code.Add(_hi[i]);
        }
        return code.ToHashCode();
    }

    public IEnumerator<int> GetEnumerator()
    {
        // REVIEW: Is there a more efficient way?
        int iblobLim = Util.Size(_hi);
        for (int iblob = -1; iblob < iblobLim; iblob++)
        {
            Blob blob = iblob < 0 ? _lo : _hi![iblob];
            Blob bit;
            for (int ibit = 0; ibit < _cbitBlob && (bit = (_blobOne << ibit)) <= blob; ibit++)
            {
                if ((bit & blob) != 0)
                    yield return ((iblob + 1) << 6) + ibit;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        if (IsEmpty)
            return "0";

        var sb = new StringBuilder();
        int cblob = Util.Size(_hi);
        for (int i = 0; i <= cblob; i++)
        {
            Blob uu = i == 0 ? _lo : _hi![i - 1];
            bool last = i == cblob;
            for (int nib = 0; nib < 2 * sizeof(Blob) && (uu != 0 || !last); nib++)
            {
                sb.Append("0123456789ABCDEF"[(int)(uu & 0x0F)]);
                uu >>= 4;
            }
        }

        for (int i = 0, j = sb.Length - 1; i < j; i++, j--)
        {
            char ch = sb[i];
            sb[i] = sb[j];
            sb[j] = ch;
        }

        return sb.ToString();
    }
}
