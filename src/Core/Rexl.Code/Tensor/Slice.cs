// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor<T>
{
    /// <summary>
    /// Slices this tensor.
    /// Returns null and sets the output Shape if a simple index is out of bounds.
    /// </summary>
    public Tensor<T> GetSlice(ReadOnly.Array<SliceItem> items, out Shape shape)
    {
        Validation.BugCheckParam(Rank == items.Length, nameof(items));
        return GetSliceCore(items, out shape);
    }

    internal Tensor<T> GetSliceCore(ReadOnly.Array<SliceItem> items, out Shape shape)
    {
        Validation.Assert(Rank == items.Length);
        var rank = 0;
        var useDef = false;
        long off = _root;
        for (int i = 0; i < Rank; i++)
        {
            var dim = _shape[i];
            if (!items[i].IsSimple(dim, out var ind))
                rank++;
            else if ((ulong)ind >= (ulong)dim)
                useDef = true;
            else if (ind != 0)
                off += ind * _strides[i];
        }

        var bldrShape = rank > 0 ? Shape.CreateBuilder(rank) : null;
        var bldrStride = !useDef && rank > 0 ? Shape.CreateBuilder(rank) : null;
        var ibldr = 0;
        for (int i = 0; i < Rank; i++)
        {
            if (items[i].IsRange(_shape[i], out var range))
            {
                if (range.start != 0)
                    off += _strides[i] * range.start;
                bldrShape[ibldr] = range.count;
                if (bldrStride != null)
                    bldrStride[ibldr] = _strides[i] * range.step;
                ibldr++;
            }
        }

        Validation.Assert(ibldr == rank);
        shape = bldrShape == null ? Shape.Scalar : bldrShape.ToImmutable();
        Validation.Assert(shape.Rank == rank);
        return useDef ? null : new Tensor<T>(_buf, shape, bldrStride == null ? Shape.Scalar : bldrStride.ToImmutable(), off);
    }
}

/// <summary>
/// This is the run time represetation of a slicing item, which is either an index (with flags)
/// or a range (with flags). We squeeze this into 24 bytes by borrowing bits from step for the flags.
/// This assumes that step doesn't need more than 56 bits.
/// 
/// REVIEW: Is it reasonable to squeeze this into 16 bytes? Perhaps 40 bits for each of start/stop/step.
/// Is that large enough? Could squeeze flags into 5 bits leaving 41 bits for each of start/stop/step.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = sizeof(long), Size = 3 * sizeof(long))]
public readonly struct SliceItem
{
    // This is the unresolved start value for a range item or the unresolved index value for an index item.
    // Which it is depends on the flags.
    private readonly long _start;
    // This is the unresolved stop value for a range item and not used for an index item.
    private readonly long _stop;
    // The low 8 bits of this flags. The high 56 bits is the step value for a range item and not used for
    // an index item.
    private readonly long _flagsAndStep;

    // The allowed range of step values. Values outside this range get "clipped" to this range.
    private const long k_stepMin = unchecked((long)0xFF80_0000_0000_0000L);
    private const long k_stepMax = 0x007F_0000_0000_0000L;

    private SliceItem(IndexFlags flags, long index)
    {
        Validation.Assert(flags.IsValid());
        _start = index;
        _stop = default;
        _flagsAndStep = (long)(byte)flags;
        Validation.Assert(IsIndex);
    }

    private SliceItem(SliceItemFlags flags, long start, long stop, long step)
    {
        Validation.Assert(flags.IsRangeSlice());
        _start = start;
        _stop = stop;
        if (step > k_stepMax)
            step = k_stepMax;
        else if (step < k_stepMin)
            step = k_stepMin;
        _flagsAndStep = (step << 8) | (byte)flags;
        Validation.Assert(!IsIndex);
    }

    /// <summary>
    /// Whether this item is an index, rather than a range.
    /// </summary>
    public bool IsIndex => ((SliceItemFlags)_flagsAndStep & SliceItemFlags.Range) == 0;

    /// <summary>
    /// Create an item for an index and flags.
    /// </summary>
    public static SliceItem CreateIndex(long index, IndexFlags flags)
    {
        Validation.Assert(flags.IsValid());
        return new SliceItem(flags, index);
    }

    /// <summary>
    /// Create a fully specified range.
    /// </summary>
    public static SliceItem CreateRange(long start, long stop, long step)
    {
        return new SliceItem(SliceItemFlags.Range | SliceItemFlags.Start | SliceItemFlags.Stop | SliceItemFlags.Step,
            start, stop, step);
    }

    /// <summary>
    /// Create a range from start and stop values (both required, not optional).
    /// </summary>
    public static SliceItem CreateRR(long start, long stop, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) != 0);
        Validation.Assert((flags & SliceItemFlags.Stop) != 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        return new SliceItem(flags, start, stop, step);
    }

    /// <summary>
    /// Create a range from a required start value and optional stop value.
    /// </summary>
    public static SliceItem CreateRO(long start, long? stop, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) != 0);
        Validation.Assert((flags & SliceItemFlags.Stop) != 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        if (!stop.HasValue)
            flags &= ~(SliceItemFlags.Stop | SliceItemFlags.StopBack | SliceItemFlags.StopStar);
        return new SliceItem(flags, start, stop.GetValueOrDefault(), step);
    }

    /// <summary>
    /// Create a range from a required start value without a stop value.
    /// The only flags that matter are <see cref="SliceItemFlags.StartBack"/>,
    /// <see cref="SliceItemFlags.StopBack"/>, and <see cref="SliceItemFlags.StopStar"/>.
    /// </summary>
    public static SliceItem CreateR_(long start, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) != 0);
        Validation.Assert((flags & SliceItemFlags.Stop) == 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        return new SliceItem(flags, start, 0, step);
    }

    /// <summary>
    /// Create a range from an optional start value and required stop value.
    /// The only flags that matter are <see cref="SliceItemFlags.StartBack"/>,
    /// <see cref="SliceItemFlags.StopBack"/>, and <see cref="SliceItemFlags.StopStar"/>.
    /// </summary>
    public static SliceItem CreateOR(long? start, long stop, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) != 0);
        Validation.Assert((flags & SliceItemFlags.Stop) != 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        if (!start.HasValue)
            flags &= ~(SliceItemFlags.Start | SliceItemFlags.StartBack);
        return new SliceItem(flags, start.GetValueOrDefault(), stop, step);
    }

    /// <summary>
    /// Create a range from a required stop value without a start value.
    /// </summary>
    public static SliceItem Create_R(long stop, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) == 0);
        Validation.Assert((flags & SliceItemFlags.Stop) != 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        return new SliceItem(flags, 0, stop, step);
    }

    /// <summary>
    /// Create a range from optional start and stop values.
    /// The only flags that matter are <see cref="SliceItemFlags.StartBack"/>,
    /// <see cref="SliceItemFlags.StopBack"/>, and <see cref="SliceItemFlags.StopStar"/>.
    /// </summary>
    public static SliceItem CreateOO(long? start, long? stop, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) != 0);
        Validation.Assert((flags & SliceItemFlags.Stop) != 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        if (!start.HasValue)
            flags &= ~(SliceItemFlags.Start | SliceItemFlags.StartBack);
        if (!stop.HasValue)
            flags &= ~(SliceItemFlags.Stop | SliceItemFlags.StopBack | SliceItemFlags.StopStar);
        return new SliceItem(flags, start.GetValueOrDefault(), stop.GetValueOrDefault(), step);
    }

    /// <summary>
    /// Create a range from an optional start value without a stop value.
    /// </summary>
    public static SliceItem CreateO_(long? start, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) != 0);
        Validation.Assert((flags & SliceItemFlags.Stop) == 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        if (!start.HasValue)
            flags &= ~(SliceItemFlags.Start | SliceItemFlags.StartBack);
        return new SliceItem(flags, start.GetValueOrDefault(), 0, step);
    }

    /// <summary>
    /// Create a range from an optional stop value without a start value.
    /// </summary>
    public static SliceItem Create_O(long? stop, long step, SliceItemFlags flags)
    {
        Validation.Assert(flags.IsRangeSlice());
        Validation.Assert((flags & SliceItemFlags.Start) == 0);
        Validation.Assert((flags & SliceItemFlags.Stop) != 0);
        Validation.Assert((flags & SliceItemFlags.Step) != 0);
        if (!stop.HasValue)
            flags &= ~(SliceItemFlags.Stop | SliceItemFlags.StopBack | SliceItemFlags.StopStar);
        return new SliceItem(flags, 0, stop.GetValueOrDefault(), step);
    }

    /// <summary>
    /// Create a range with neither start nor stop values.
    /// </summary>
    public static SliceItem Create__(long step)
    {
        var flags = SliceItemFlags.Range | SliceItemFlags.Step;
        return new SliceItem(flags, 0, 0, step);
    }

    /// <summary>
    /// Whether this is a simple index, i.e. an integer. If so, sets <paramref name="index"/> to
    /// the index being accessed on a dimension of length <paramref name="len"/>.
    /// </summary>
    public bool IsSimple(long len, out long index)
    {
        // If this assert fires, it means someone is calling us incorrectly, but this still
        // does something sensical, so it doesn't really need to be a bug check.
        Validation.Assert(len >= 0);

        if (IsIndex)
        {
            if (len <= 0)
                index = 0;
            else
                index = ((IndexFlags)_flagsAndStep).AdjustIndex(_start, len);
            return true;
        }
        index = -1;
        return false;
    }

    /// <summary>
    /// Whether this is range. If so, sets <paramref name="range"/> to the resulting values
    /// for the given <paramref name="len"/>.
    /// </summary>
    public bool IsRange(long len, out (long start, long step, long count) range)
    {
        if (!IsIndex)
        {
            range = SliceUtil.GetRange(len, (SliceItemFlags)_flagsAndStep, _start, _stop, _flagsAndStep >> 8);
            return true;
        }
        range = default;
        return false;
    }
}
