// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Conditional = System.Diagnostics.ConditionalAttribute;

partial class DocumentBase
{
    partial class GridConfigImpl
    {
        /// <summary>
        /// This is a general single-byte flags enum. We use it for nullable types to encode
        /// whether there is a "value" or null.
        /// </summary>
        [Flags]
        private enum Flag : byte
        {
            F0 = 0x01,
            F1 = 0x02,
            F2 = 0x04,
            F3 = 0x08,
            F4 = 0x10,
            F5 = 0x20,
            F6 = 0x40,
            F7 = 0x80,
        }

        /// <summary>
        /// This is a read-only view of flags associated with a column. This is "parallel" to the
        /// values / rows in a grid, so is indexed by "slot". This is typically used for communicating
        /// information between <see cref="GridConfigImpl"/> and <see cref="TypeInfo"/>.
        /// </summary>
        private struct FlagInfoRdo
        {
            private readonly ReadOnly.Array<Flag> _flags;
            private readonly Flag _mask;

            /// <summary>
            /// This is "blank" if the column doesn't use a flag.
            /// </summary>
            public bool IsBlank => _mask == 0;

            /// <summary>
            /// The length of the flags "array".
            /// </summary>
            public int Length => _flags.Length;

            public FlagInfoRdo(ReadOnly.Array<Flag> flags, Flag mask)
            {
                Validation.Assert(flags.IsDefault == (mask == 0));
                Validation.Assert((mask & (mask - 1)) == 0);
                _flags = flags;
                _mask = mask;
                AssertValid();
            }

            [Conditional("DEBUG")]
            private void AssertValid()
            {
                Validation.Assert(_flags.IsDefault == (_mask == 0));
                Validation.Assert((_mask & (_mask - 1)) == 0);
            }

            /// <summary>
            /// Return whether the flag at the indicated slot is set.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Test(int slot)
            {
                AssertValid();
                Validation.AssertIndex(slot, _flags.Length);
                return (_flags[slot] & _mask) != 0;
            }

            /// <summary>
            /// Return whether any of the flags in the given slots are set.
            /// </summary>
            public bool TestAny(in SlotInfo slinSrc)
            {
                AssertValid();
                return !IsBlank && slinSrc.TestAny(_flags, _mask);
            }
        }

        /// <summary>
        /// This is a writable view of flags associated with a column. This is "parallel" to the
        /// values / rows in a grid, so is indexed by "slot". This is typically used for communicating
        /// information between <see cref="GridConfigImpl"/> and <see cref="TypeInfo"/>.
        /// </summary>
        private struct FlagInfoWrt
        {
            private readonly Flag[] _flags;
            private readonly Flag _mask;

            /// <summary>
            /// This is "blank" if the column doesn't use a flag.
            /// </summary>
            public bool IsBlank => _mask == 0;

            /// <summary>
            /// The length of the flags "array".
            /// </summary>
            public int Length => _flags.Length;

            public FlagInfoWrt(Flag[] flags, Flag mask)
            {
                Validation.Assert((flags == null) == (mask == 0));
                Validation.Assert((mask & (mask - 1)) == 0);
                _flags = flags;
                _mask = mask;
                AssertValid();
            }

            [Conditional("DEBUG")]
            private void AssertValid()
            {
                Validation.Assert((_flags == null) == (_mask == 0));
                Validation.Assert((_mask & (_mask - 1)) == 0);
            }

            /// <summary>
            /// Implicit conversion from writable to readable.
            /// </summary>
            public static implicit operator FlagInfoRdo(FlagInfoWrt flin)
            {
                flin.AssertValid();
                return new FlagInfoRdo(flin._flags, flin._mask);
            }

            /// <summary>
            /// Return whether the flag at the indicated slot is set.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Test(int slot)
            {
                AssertValid();
                Validation.AssertIndex(slot, _flags.Length);
                return (_flags[slot] & _mask) != 0;
            }

            /// <summary>
            /// Set the flag at the indicated slot to the given value.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(int slot, bool value)
            {
                AssertValid();
                Validation.AssertIndex(slot, _flags.Length);
                if (value)
                    _flags[slot] |= _mask;
                else
                    _flags[slot] &= ~_mask;
            }

            /// <summary>
            /// Set the flag at the indicated slot to true.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(int slot)
            {
                AssertValid();
                Validation.AssertIndex(slot, _flags.Length);
                _flags[slot] |= _mask;
            }

            /// <summary>
            /// Set the flag at the indicated slot to false.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear(int slot)
            {
                AssertValid();
                Validation.AssertIndex(slot, _flags.Length);
                _flags[slot] &= ~_mask;
            }

            /// <summary>
            /// Set the flags at the indicated slots to true.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(in SlotInfo slinDst)
            {
                AssertValid();
                if (!IsBlank)
                    slinDst.SetFlags(_flags, _mask);
            }

            /// <summary>
            /// Set the flags at the indicated slots to false.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear(in SlotInfo slinDst)
            {
                AssertValid();
                if (!IsBlank)
                    slinDst.ClearFlags(_flags, _mask);
            }

            /// <summary>
            /// Copy flags from the given <paramref name="flinSrc"/> using slots <paramref name="slinSrc"/>
            /// to the slots <paramref name="slinDst"/>. Note that <paramref name="slinDst"/> and
            /// <paramref name="slinSrc"/> must have the same number of slots.
            /// </summary>
            public void CopyFrom(in FlagInfoRdo flinSrc, in SlotInfo slinSrc, in SlotInfo slinDst)
            {
                AssertValid();
                Validation.Assert(slinSrc.Count == slinDst.Count);

                for (int i = 0; i < slinSrc.Count; i++)
                {
                    int slotSrc = slinSrc.GetSlot(i);
                    int slotDst = slinDst.GetSlot(i);
                    Validation.AssertIndex(slotSrc, flinSrc.Length);
                    Validation.AssertIndex(slotDst, _flags.Length);
                    if (flinSrc.Test(slotSrc))
                        _flags[slotDst] |= _mask;
                    else
                        _flags[slotDst] &= ~_mask;
                }
            }
        }

        /// <summary>
        /// Any column whose type info needs a flag bit is assigned a grp (index into <see cref="_valFlags"/>)
        /// and a mask value which has a single bit set. For others, mask is zero.
        /// </summary>
        private Immutable.Array<(int grp, Flag mask)> _colToFlagInfo;

        /// <summary>
        /// These arrays contain value flags. This is indexed by grp. Each item array is indexed by "slot".
        /// Within a value, a column references a single bit.
        /// </summary>
        private Flag[][] _valFlags;

        /// <summary>
        /// Returns the value of the corresponding flag. Asserts that the column uses a flag.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TestFlag(int col, int slot)
        {
            Validation.AssertIndex(col, _ccol);
            Validation.Assert(_colToTin[col].NeedFlag);

            (var grp, var mask) = _colToFlagInfo[col];
            Validation.AssertIndex(grp, _valFlags.Length);
            Validation.Assert(mask != 0);
            Validation.Assert((mask & (mask - 1)) == 0);
            return (_valFlags[grp][slot] & mask) != 0;
        }

        /// <summary>
        /// Get read-only flag information for the column. This works whether or not the column
        /// uses a flag.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FlagInfoRdo GetFlagInfoRdo(int col)
        {
            Validation.AssertIndex(col, _ccol);
            (int grp, Flag mask) = _colToFlagInfo[col];
            Validation.Assert((mask != 0) == _colToTin[col].NeedFlag);
            Validation.Assert((mask & (mask - 1)) == 0);
            if (mask == 0)
                return default;
            Validation.AssertIndex(grp, _valFlags.Length);
            return new FlagInfoRdo(_valFlags[grp], mask);
        }

        /// <summary>
        /// Get writable flag information for the column. This works whether or not the column
        /// uses a flag.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FlagInfoWrt GetFlagInfoWrt(int col)
        {
            Validation.AssertIndex(col, _ccol);
            (int grp, Flag mask) = _colToFlagInfo[col];
            Validation.Assert((mask != 0) == _colToTin[col].NeedFlag);
            Validation.Assert((mask & (mask - 1)) == 0);
            if (mask == 0)
                return default;
            Validation.AssertIndex(grp, _valFlags.Length);
            return new FlagInfoWrt(_valFlags[grp], mask);
        }

        /// <summary>
        /// Find (or make) an available flag bit.
        /// </summary>
        private (int grp, Flag mask) AllocFlagBit(Immutable.Array<(int grp, Flag mask)>.Builder bldr = null, int cap = -1)
        {
            Validation.Assert(bldr == null || bldr.Count >= _ccol);

            // Get a free (grp, mask) pair.
            int grp = 0;
            Flag mask = Flag.F0;
            if (_valFlags.Length > 0)
            {
                // Get all the used flags.
                var ccol = bldr?.Count ?? _ccol;
                var used = new Flag[_valFlags.Length];
                for (int colCur = 0; colCur < ccol; colCur++)
                {
                    (int grpCur, Flag maskCur) = bldr != null ? bldr[colCur] : _colToFlagInfo[colCur];
                    Validation.AssertIndex(grpCur, used.Length);
                    Validation.Assert(bldr != null || (maskCur != 0) == _colToTin[colCur].NeedFlag);
                    Validation.Assert((used[grpCur] & maskCur) == 0);
                    used[grpCur] |= maskCur;
                }

                // Find an unused bit in a group.
                for (; grp < used.Length; grp++)
                {
                    var avail = ~used[grp];
                    if (avail != 0)
                    {
                        // Get the low bit in avail. Note that "avail & (avail - 1)" clears the low bit
                        // of avail, then xoring with avail leaves just that low bit.
                        mask = avail ^ (avail & (avail - 1));
                        Validation.Assert((mask & used[grp]) == 0);
                        break;
                    }
                }
            }

            // mask should have one bit set.
            Validation.Assert(mask != 0);
            Validation.Assert((mask & (mask - 1)) == 0);

            Validation.AssertIndexInclusive(grp, _valFlags.Length);
            if (grp >= _valFlags.Length)
            {
                // Add a new flag group.
                Array.Resize(ref _valFlags, grp + 1);
                _valFlags[grp] = new Flag[Math.Max(_numAlloced, cap)];
            }

            return (grp, mask);
        }
    }
}
