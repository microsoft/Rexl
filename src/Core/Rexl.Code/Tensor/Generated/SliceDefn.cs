// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor<T>
{
    /// <summary>
    /// Slices this rank 1 tensor.
    /// Returns null and sets the output Shape if a simple index is out of bounds.
    /// </summary>
    public Tensor<T> GetSlice(SliceItem item0, out Shape shape)
    {
        Validation.BugCheck(Rank == 1);
        return GetSliceCore(item0, out shape);
    }

    internal Tensor<T> GetSliceCore(SliceItem item0, out Shape shape)
    {
        Validation.Assert(Rank == 1);

        var rank = 0;
        var useDef = false;
        long off = _root;

        var rng0 = item0.IsRange(_count, out var range0);
        if (rng0)
            rank++;
        else
        {
            item0.IsSimple(_count, out var ind0).Verify();
            if ((ulong)ind0 >= (ulong)_count)
                useDef = true;
            else if (ind0 != 0)
                off += ind0 * _delta;
        }

        if (rank == Rank &&
            range0.start == 0 && range0.count == _count)
        {
            shape = _shape;
            return this;
        }

        var bldrShape = rank > 0 ? Shape.CreateBuilder(rank) : null;
        var bldrStride = !useDef && rank > 0 ? Shape.CreateBuilder(rank) : null;
        var ibldr = 0;

        if (rng0)
        {
            if (range0.start != 0)
                off += _delta * range0.start;
            bldrShape[ibldr] = range0.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _delta * range0.step;
            ibldr++;
        }

        Validation.Assert(ibldr == rank);
        shape = bldrShape == null ? Shape.Scalar : bldrShape.ToImmutable();
        Validation.Assert(shape.Rank == rank);
        return useDef ? null : new Tensor<T>(_buf, shape, bldrStride == null ? Shape.Scalar : bldrStride.ToImmutable(), off);
    }

    /// <summary>
    /// Slices this rank 2 tensor.
    /// Returns null and sets the output Shape if a simple index is out of bounds.
    /// </summary>
    public Tensor<T> GetSlice(SliceItem item0, SliceItem item1, out Shape shape)
    {
        Validation.BugCheck(Rank == 2);
        return GetSliceCore(item0, item1, out shape);
    }

    internal Tensor<T> GetSliceCore(SliceItem item0, SliceItem item1, out Shape shape)
    {
        Validation.Assert(Rank == 2);

        var rank = 0;
        var useDef = false;
        long off = _root;

        var rng0 = item0.IsRange(_shape[0], out var range0);
        if (rng0)
            rank++;
        else
        {
            item0.IsSimple(_shape[0], out var ind0).Verify();
            if ((ulong)ind0 >= (ulong)_shape[0])
                useDef = true;
            else if (ind0 != 0)
                off += ind0 * _strides[0];
        }

        var rng1 = item1.IsRange(_shape[1], out var range1);
        if (rng1)
            rank++;
        else
        {
            item1.IsSimple(_shape[1], out var ind1).Verify();
            if ((ulong)ind1 >= (ulong)_shape[1])
                useDef = true;
            else if (ind1 != 0)
                off += ind1 * _strides[1];
        }

        if (rank == Rank &&
            range0.start == 0 && range0.count == _shape[0] &&
                range1.start == 0 && range1.count == _shape[1])
        {
            shape = _shape;
            return this;
        }

        var bldrShape = rank > 0 ? Shape.CreateBuilder(rank) : null;
        var bldrStride = !useDef && rank > 0 ? Shape.CreateBuilder(rank) : null;
        var ibldr = 0;

        if (rng0)
        {
            if (range0.start != 0)
                off += _strides[0] * range0.start;
            bldrShape[ibldr] = range0.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[0] * range0.step;
            ibldr++;
        }

        if (rng1)
        {
            if (range1.start != 0)
                off += _strides[1] * range1.start;
            bldrShape[ibldr] = range1.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[1] * range1.step;
            ibldr++;
        }

        Validation.Assert(ibldr == rank);
        shape = bldrShape == null ? Shape.Scalar : bldrShape.ToImmutable();
        Validation.Assert(shape.Rank == rank);
        return useDef ? null : new Tensor<T>(_buf, shape, bldrStride == null ? Shape.Scalar : bldrStride.ToImmutable(), off);
    }

    /// <summary>
    /// Slices this rank 3 tensor.
    /// Returns null and sets the output Shape if a simple index is out of bounds.
    /// </summary>
    public Tensor<T> GetSlice(SliceItem item0, SliceItem item1, SliceItem item2, out Shape shape)
    {
        Validation.BugCheck(Rank == 3);
        return GetSliceCore(item0, item1, item2, out shape);
    }

    internal Tensor<T> GetSliceCore(SliceItem item0, SliceItem item1, SliceItem item2, out Shape shape)
    {
        Validation.Assert(Rank == 3);

        var rank = 0;
        var useDef = false;
        long off = _root;

        var rng0 = item0.IsRange(_shape[0], out var range0);
        if (rng0)
            rank++;
        else
        {
            item0.IsSimple(_shape[0], out var ind0).Verify();
            if ((ulong)ind0 >= (ulong)_shape[0])
                useDef = true;
            else if (ind0 != 0)
                off += ind0 * _strides[0];
        }

        var rng1 = item1.IsRange(_shape[1], out var range1);
        if (rng1)
            rank++;
        else
        {
            item1.IsSimple(_shape[1], out var ind1).Verify();
            if ((ulong)ind1 >= (ulong)_shape[1])
                useDef = true;
            else if (ind1 != 0)
                off += ind1 * _strides[1];
        }

        var rng2 = item2.IsRange(_shape[2], out var range2);
        if (rng2)
            rank++;
        else
        {
            item2.IsSimple(_shape[2], out var ind2).Verify();
            if ((ulong)ind2 >= (ulong)_shape[2])
                useDef = true;
            else if (ind2 != 0)
                off += ind2 * _strides[2];
        }

        if (rank == Rank &&
            range0.start == 0 && range0.count == _shape[0] &&
                range1.start == 0 && range1.count == _shape[1] &&
                range2.start == 0 && range2.count == _shape[2])
        {
            shape = _shape;
            return this;
        }

        var bldrShape = rank > 0 ? Shape.CreateBuilder(rank) : null;
        var bldrStride = !useDef && rank > 0 ? Shape.CreateBuilder(rank) : null;
        var ibldr = 0;

        if (rng0)
        {
            if (range0.start != 0)
                off += _strides[0] * range0.start;
            bldrShape[ibldr] = range0.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[0] * range0.step;
            ibldr++;
        }

        if (rng1)
        {
            if (range1.start != 0)
                off += _strides[1] * range1.start;
            bldrShape[ibldr] = range1.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[1] * range1.step;
            ibldr++;
        }

        if (rng2)
        {
            if (range2.start != 0)
                off += _strides[2] * range2.start;
            bldrShape[ibldr] = range2.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[2] * range2.step;
            ibldr++;
        }

        Validation.Assert(ibldr == rank);
        shape = bldrShape == null ? Shape.Scalar : bldrShape.ToImmutable();
        Validation.Assert(shape.Rank == rank);
        return useDef ? null : new Tensor<T>(_buf, shape, bldrStride == null ? Shape.Scalar : bldrStride.ToImmutable(), off);
    }

    /// <summary>
    /// Slices this rank 4 tensor.
    /// Returns null and sets the output Shape if a simple index is out of bounds.
    /// </summary>
    public Tensor<T> GetSlice(SliceItem item0, SliceItem item1, SliceItem item2, SliceItem item3, out Shape shape)
    {
        Validation.BugCheck(Rank == 4);
        return GetSliceCore(item0, item1, item2, item3, out shape);
    }

    internal Tensor<T> GetSliceCore(SliceItem item0, SliceItem item1, SliceItem item2, SliceItem item3, out Shape shape)
    {
        Validation.Assert(Rank == 4);

        var rank = 0;
        var useDef = false;
        long off = _root;

        var rng0 = item0.IsRange(_shape[0], out var range0);
        if (rng0)
            rank++;
        else
        {
            item0.IsSimple(_shape[0], out var ind0).Verify();
            if ((ulong)ind0 >= (ulong)_shape[0])
                useDef = true;
            else if (ind0 != 0)
                off += ind0 * _strides[0];
        }

        var rng1 = item1.IsRange(_shape[1], out var range1);
        if (rng1)
            rank++;
        else
        {
            item1.IsSimple(_shape[1], out var ind1).Verify();
            if ((ulong)ind1 >= (ulong)_shape[1])
                useDef = true;
            else if (ind1 != 0)
                off += ind1 * _strides[1];
        }

        var rng2 = item2.IsRange(_shape[2], out var range2);
        if (rng2)
            rank++;
        else
        {
            item2.IsSimple(_shape[2], out var ind2).Verify();
            if ((ulong)ind2 >= (ulong)_shape[2])
                useDef = true;
            else if (ind2 != 0)
                off += ind2 * _strides[2];
        }

        var rng3 = item3.IsRange(_shape[3], out var range3);
        if (rng3)
            rank++;
        else
        {
            item3.IsSimple(_shape[3], out var ind3).Verify();
            if ((ulong)ind3 >= (ulong)_shape[3])
                useDef = true;
            else if (ind3 != 0)
                off += ind3 * _strides[3];
        }

        if (rank == Rank &&
            range0.start == 0 && range0.count == _shape[0] &&
                range1.start == 0 && range1.count == _shape[1] &&
                range2.start == 0 && range2.count == _shape[2] &&
                range3.start == 0 && range3.count == _shape[3])
        {
            shape = _shape;
            return this;
        }

        var bldrShape = rank > 0 ? Shape.CreateBuilder(rank) : null;
        var bldrStride = !useDef && rank > 0 ? Shape.CreateBuilder(rank) : null;
        var ibldr = 0;

        if (rng0)
        {
            if (range0.start != 0)
                off += _strides[0] * range0.start;
            bldrShape[ibldr] = range0.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[0] * range0.step;
            ibldr++;
        }

        if (rng1)
        {
            if (range1.start != 0)
                off += _strides[1] * range1.start;
            bldrShape[ibldr] = range1.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[1] * range1.step;
            ibldr++;
        }

        if (rng2)
        {
            if (range2.start != 0)
                off += _strides[2] * range2.start;
            bldrShape[ibldr] = range2.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[2] * range2.step;
            ibldr++;
        }

        if (rng3)
        {
            if (range3.start != 0)
                off += _strides[3] * range3.start;
            bldrShape[ibldr] = range3.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[3] * range3.step;
            ibldr++;
        }

        Validation.Assert(ibldr == rank);
        shape = bldrShape == null ? Shape.Scalar : bldrShape.ToImmutable();
        Validation.Assert(shape.Rank == rank);
        return useDef ? null : new Tensor<T>(_buf, shape, bldrStride == null ? Shape.Scalar : bldrStride.ToImmutable(), off);
    }

    /// <summary>
    /// Slices this rank 5 tensor.
    /// Returns null and sets the output Shape if a simple index is out of bounds.
    /// </summary>
    public Tensor<T> GetSlice(SliceItem item0, SliceItem item1, SliceItem item2, SliceItem item3, SliceItem item4, out Shape shape)
    {
        Validation.BugCheck(Rank == 5);
        return GetSliceCore(item0, item1, item2, item3, item4, out shape);
    }

    internal Tensor<T> GetSliceCore(SliceItem item0, SliceItem item1, SliceItem item2, SliceItem item3, SliceItem item4, out Shape shape)
    {
        Validation.Assert(Rank == 5);

        var rank = 0;
        var useDef = false;
        long off = _root;

        var rng0 = item0.IsRange(_shape[0], out var range0);
        if (rng0)
            rank++;
        else
        {
            item0.IsSimple(_shape[0], out var ind0).Verify();
            if ((ulong)ind0 >= (ulong)_shape[0])
                useDef = true;
            else if (ind0 != 0)
                off += ind0 * _strides[0];
        }

        var rng1 = item1.IsRange(_shape[1], out var range1);
        if (rng1)
            rank++;
        else
        {
            item1.IsSimple(_shape[1], out var ind1).Verify();
            if ((ulong)ind1 >= (ulong)_shape[1])
                useDef = true;
            else if (ind1 != 0)
                off += ind1 * _strides[1];
        }

        var rng2 = item2.IsRange(_shape[2], out var range2);
        if (rng2)
            rank++;
        else
        {
            item2.IsSimple(_shape[2], out var ind2).Verify();
            if ((ulong)ind2 >= (ulong)_shape[2])
                useDef = true;
            else if (ind2 != 0)
                off += ind2 * _strides[2];
        }

        var rng3 = item3.IsRange(_shape[3], out var range3);
        if (rng3)
            rank++;
        else
        {
            item3.IsSimple(_shape[3], out var ind3).Verify();
            if ((ulong)ind3 >= (ulong)_shape[3])
                useDef = true;
            else if (ind3 != 0)
                off += ind3 * _strides[3];
        }

        var rng4 = item4.IsRange(_shape[4], out var range4);
        if (rng4)
            rank++;
        else
        {
            item4.IsSimple(_shape[4], out var ind4).Verify();
            if ((ulong)ind4 >= (ulong)_shape[4])
                useDef = true;
            else if (ind4 != 0)
                off += ind4 * _strides[4];
        }

        if (rank == Rank &&
            range0.start == 0 && range0.count == _shape[0] &&
                range1.start == 0 && range1.count == _shape[1] &&
                range2.start == 0 && range2.count == _shape[2] &&
                range3.start == 0 && range3.count == _shape[3] &&
                range4.start == 0 && range4.count == _shape[4])
        {
            shape = _shape;
            return this;
        }

        var bldrShape = rank > 0 ? Shape.CreateBuilder(rank) : null;
        var bldrStride = !useDef && rank > 0 ? Shape.CreateBuilder(rank) : null;
        var ibldr = 0;

        if (rng0)
        {
            if (range0.start != 0)
                off += _strides[0] * range0.start;
            bldrShape[ibldr] = range0.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[0] * range0.step;
            ibldr++;
        }

        if (rng1)
        {
            if (range1.start != 0)
                off += _strides[1] * range1.start;
            bldrShape[ibldr] = range1.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[1] * range1.step;
            ibldr++;
        }

        if (rng2)
        {
            if (range2.start != 0)
                off += _strides[2] * range2.start;
            bldrShape[ibldr] = range2.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[2] * range2.step;
            ibldr++;
        }

        if (rng3)
        {
            if (range3.start != 0)
                off += _strides[3] * range3.start;
            bldrShape[ibldr] = range3.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[3] * range3.step;
            ibldr++;
        }

        if (rng4)
        {
            if (range4.start != 0)
                off += _strides[4] * range4.start;
            bldrShape[ibldr] = range4.count;
            if (bldrStride != null)
                bldrStride[ibldr] = _strides[4] * range4.step;
            ibldr++;
        }

        Validation.Assert(ibldr == rank);
        shape = bldrShape == null ? Shape.Scalar : bldrShape.ToImmutable();
        Validation.Assert(shape.Rank == rank);
        return useDef ? null : new Tensor<T>(_buf, shape, bldrStride == null ? Shape.Scalar : bldrStride.ToImmutable(), off);
    }

}
