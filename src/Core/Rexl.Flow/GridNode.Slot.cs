// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Conditional = System.Diagnostics.ConditionalAttribute;
using RowMap = ReadOnly.Array<(int min, int lim)>;

partial class DocumentBase
{
    partial class GridConfigImpl
    {
        /// <summary>
        /// This is a read-only view of a mapping from "index" to "slot".
        /// Typically used for communicating information from <see cref="GridConfigImpl"/> to <see cref="TypeInfo"/>.
        /// </summary>
        private struct SlotInfo
        {
            private readonly int[] _rowToSlot;
            private readonly int _rowMin;
            public readonly int Count;

            public SlotInfo(int[] rowToSlot, int rowMin, int rowLim)
            {
                Validation.AssertValue(rowToSlot);
                Validation.AssertIndexInclusive(rowLim, rowToSlot.Length);
                Validation.AssertIndexInclusive(rowMin, rowLim);

                _rowToSlot = rowToSlot;
                _rowMin = rowMin;
                Count = rowLim - rowMin;
            }

            [Conditional("DEBUG")]
            private void AssertValid()
            {
                Validation.AssertValue(_rowToSlot);
                Validation.AssertIndexInclusive(Count, _rowToSlot.Length);
                Validation.AssertIndexInclusive(_rowMin, _rowToSlot.Length - Count);
            }

            /// <summary>
            /// Get the slot for the given index. When feasible, use one of the other methods.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetSlot(int index)
            {
                AssertValid();
                Validation.AssertIndex(index, Count);
                return _rowToSlot[index + _rowMin];
            }

            /// <summary>
            /// Get the value from <paramref name="src"/> at the slot corresponding to <paramref name="index"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetValue<T>(ReadOnly.Array<T> src, int index)
            {
                AssertValid();
                Validation.AssertIndex(index, Count);
                int slot = _rowToSlot[index + _rowMin];
                Validation.AssertIndex(slot, src.Length);
                return src[slot];
            }

            /// <summary>
            /// Set the <paramref name="value"/> in <paramref name="dst"/> at the slot corresponding to
            /// <paramref name="index"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetValue<T>(T value, T[] dst, int index)
            {
                Validation.AssertIndex(index, Count);
                int slot = _rowToSlot[index + _rowMin];
                Validation.AssertIndex(slot, dst.Length);
                dst[slot] = value;
            }

            /// <summary>
            /// Copy values from <paramref name="src"/> into <paramref name="dst"/>.
            /// Slots for <paramref name="dst"/> are provided by this.
            /// Return the previous values in <paramref name="dst"/>.
            /// </summary>
            public Immutable.Array<T> SetValuesDst<T>(ReadOnly.Array<T> src, T[] dst)
            {
                Validation.AssertValue(dst);
                Validation.AssertIndexInclusive(Count, src.Length);
                var bldr = Immutable.Array.CreateBuilder<T>(Count, init: true);
                for (int i = 0; i < Count; i++)
                {
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotDst, dst.Length);
                    bldr[i] = dst[slotDst];
                    dst[slotDst] = src[i];
                }
                return bldr.ToImmutable();
            }

            /// <summary>
            /// Copy values from <paramref name="src"/> to <paramref name="dst"/>.
            /// Slots for <paramref name="dst"/> are provided by this.
            /// </summary>
            public void CopyValuesDst<T>(ReadOnly.Array<T> src, T[] dst)
            {
                Validation.AssertValue(dst);
                Validation.AssertIndexInclusive(Count, src.Length);
                for (int i = 0; i < Count; i++)
                {
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = src[i];
                }
            }

            /// <summary>
            /// Copy values from <paramref name="src"/> to <paramref name="dst"/>.
            /// Slots for <paramref name="src"/> are provided by this.
            /// </summary>
            public void CopyValuesSrc<T>(ReadOnly.Array<T> src, T[] dst)
            {
                Validation.AssertValue(dst);
                Validation.AssertIndexInclusive(Count, dst.Length);
                for (int i = 0; i < Count; i++)
                {
                    int slotSrc = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotSrc, src.Length);
                    dst[i] = src[slotSrc];
                }
            }

            /// <summary>
            /// Copy values from <paramref name="src"/> at slots provided by <paramref name="slinSrc"/> to
            /// <paramref name="dst"/> at slots provided by this. This and <paramref name="slinSrc"/> must
            /// have the same <see cref="Count"/>.
            /// </summary>
            public void CopyValues<T>(ReadOnly.Array<T> src, in SlotInfo slinSrc, T[] dst)
            {
                Validation.Assert(slinSrc.Count == Count);
                for (int i = 0; i < Count; i++)
                {
                    int slotSrc = slinSrc._rowToSlot[i + slinSrc._rowMin];
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotSrc, src.Length);
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = src[slotSrc];
                }
            }

            /// <summary>
            /// Copy values from <paramref name="src"/> (identity slot mapping starting at <paramref name="min"/>) to
            /// <paramref name="dst"/> at slots provided by this. The length of <paramref name="src"/> must be at least
            /// <see cref="Count"/> plus <paramref name="min"/>.
            /// </summary>
            public void CopyValues<T>(ReadOnly.Array<T> src, int min, T[] dst)
            {
                Validation.AssertIndexInclusive(min, src.Length);
                Validation.Assert(Count <= src.Length - min);
                for (int i = 0; i < Count; i++)
                {
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = src[i + min];
                }
            }

            public void ClearValues<T>(T[] dst, T def = default)
            {
                for (int i = 0; i < Count; i++)
                {
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = def;
                }
            }

            /// <summary>
            /// Clears the bits in <paramref name="dst"/> indicated by <paramref name="mask"/>.
            /// </summary>
            public bool TestAny(ReadOnly.Array<Flag> src, Flag mask)
            {
                if (mask != 0)
                {
                    for (int i = 0; i < Count; i++)
                    {
                        int slotSrc = _rowToSlot[i + _rowMin];
                        Validation.AssertIndex(slotSrc, src.Length);
                        if ((src[slotSrc] & mask) != 0)
                            return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Sets the bits in <paramref name="dst"/> indicated by <paramref name="mask"/>.
            /// </summary>
            public void SetFlags(Flag[] dst, Flag mask)
            {
                for (int i = 0; i < Count; i++)
                {
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] |= mask;
                }
            }

            /// <summary>
            /// Sets the bits in <paramref name="dsts"/> indicated by <paramref name="masks"/>.
            /// </summary>
            public void SetFlags(Flag[][] dsts, Flag[] masks)
            {
                Validation.AssertNonEmpty(dsts);
                Validation.AssertNonEmpty(masks);
                Validation.Assert(masks.Length <= dsts.Length);

                for (int grp = 0; grp < masks.Length; grp++)
                {
                    var mask = masks[grp];
                    if (mask == 0)
                        continue;
                    var dst = dsts[grp];
                    for (int i = 0; i < Count; i++)
                    {
                        int slotDst = _rowToSlot[i + _rowMin];
                        Validation.AssertIndex(slotDst, dst.Length);
                        dst[slotDst] |= mask;
                    }
                }
            }

            /// <summary>
            /// Clears the bits in <paramref name="dst"/> indicated by <paramref name="mask"/>.
            /// </summary>
            public void ClearFlags(Flag[] dst, Flag mask)
            {
                var clear = ~mask;
                for (int i = 0; i < Count; i++)
                {
                    int slotDst = _rowToSlot[i + _rowMin];
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] &= clear;
                }
            }
        }

        /// <summary>
        /// Maps from row index to slot number in the <see cref="_values"/> and <see cref="_valFlags"/> arrays.
        /// REVIEW: Could use a piece table so edits are O(crowDel + crowIns) instead of O(_crow - row + crowIns).
        /// REVIEW: If we support null rows, could represent them with a slot of -1. Of course, that would change
        /// some of the invariants, like _slotLim == _crow + _slotsFree.Count.
        /// </summary>
        private int[] _rowToSlot;

        /// <summary>
        /// Slots at or above <see cref="_slotLim"/> are known to be free and should all be "blank", that is,
        /// the values arrays for such slot values should all contain their default value. All slots below
        /// <see cref="_slotLim"/> are either allocated (a value in <see cref="_rowToSlot"/>) or in the
        /// <see cref="_slotsFree"/> heap.
        /// </summary>
        private int _slotLim;

        /// <summary>
        /// The free slots in a heap (min index at the top of the heap).
        /// </summary>
        private readonly IntHeap _slotsFree;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SlotInfo GetSlotInfo(int rowMin, int rowLim)
        {
            Validation.AssertIndexInclusive(rowLim, _crow);
            Validation.AssertIndexInclusive(rowMin, rowLim);
            return new SlotInfo(_rowToSlot, rowMin, rowLim);
        }

        /// <summary>
        /// Create slot info for the indicated rows. When <paramref name="rowMap"/> is default, the slot
        /// info covers all rows.
        /// </summary>
        private SlotInfo GetSlotInfo(RowMap rowMap)
        {
            var (rowToSlot, min, lim) = GetSlotArray(rowMap, forceZeroMin: false);
            return new SlotInfo(rowToSlot, min, lim);
        }

        /// <summary>
        /// Returns a rowToSlot array and range for the given <paramref name="rowMap"/>. The returned array
        /// may be <see cref="_rowToSlot"/>. If <paramref name="forceZeroMin"/> is true, the min value of
        /// the range is guaranteed to be zero (creating a new array if needed).
        /// </summary>
        private (int[] rowToSlot, int rowMin, int rowLim) GetSlotArray(RowMap rowMap, bool forceZeroMin)
        {
            if (rowMap.IsDefault)
                return (_rowToSlot, 0, _crow);

            var (minAll, limAll, count, numRng) = rowMap.GetUnion(_crow);
            Validation.AssertIndexInclusive(minAll, _crow);
            Validation.AssertIndexInclusive(limAll, _crow);

            if (count == 0)
                return (Array.Empty<int>(), 0, 0);

            Validation.Assert(numRng > 0);
            if (numRng == 1)
            {
                Validation.Assert(count == limAll - minAll);
                if (minAll == 0 || !forceZeroMin)
                    return (_rowToSlot, minAll, limAll);
            }

            // Construct the new rowToSlot array.
            Validation.BugCheckParam(count <= int.MaxValue, nameof(rowMap), "Too many rows");
            var rowToSlot = new int[(int)count];
            int rowDst = 0;
            foreach (var (min, lim) in rowMap.CleanRanges(_crow))
            {
                Validation.Assert(minAll <= min & min < lim & lim <= limAll);
                Validation.Assert(rowDst <= count - (lim - min));
                for (int rowSrc = min; rowSrc < lim; rowSrc++)
                    rowToSlot[rowDst++] = _rowToSlot[rowSrc];
            }
            Validation.Assert(rowDst == count);
            return (rowToSlot, 0, rowToSlot.Length);
        }
    }
}
