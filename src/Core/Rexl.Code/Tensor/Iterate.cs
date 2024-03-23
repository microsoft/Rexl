// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor
{
    /// <summary>
    /// Iterate over the indices into the underlying buffer.
    /// </summary>
    private protected static IEnumerable<long> IterateIndices(long count, Shape shape, Shape strides, long root)
    {
        Validation.Assert(shape.Rank == strides.Rank);
        Validation.Assert(!shape.AnyNegative);
        // Caller should handle empty.
        Validation.Assert(!shape.AnyZero);
        Validation.Assert(count > 0);

        switch (shape.Rank)
        {
        case 0:
            Validation.Assert(count == 1);
            return new long[] { root };
        case 1:
            Validation.Assert(count == shape[0]);
            return IterateIndices1(shape[0], strides[0], root);
        case 2:
            Validation.Assert(count == shape[0] * shape[1]);
            return IterateIndices2(shape[0], strides[0], shape[1], strides[1], root);
        case 3:
            Validation.Assert(count == shape[0] * shape[1] * shape[2]);
            return IterateIndices3(shape[0], strides[0], shape[1], strides[1], shape[2], strides[2], root);
        }
        return IterateIndicesGen(count, shape, strides, root);
    }

    /// <summary>
    /// Iterate over the values coming from the underlying buffer.
    /// REVIEW: It may be worth creating class wrappers for these that implement <see cref="ICanCount"/>
    /// and <see cref="ICachingEnumerable"/>. Then the result can be used directly in rexl flow.
    /// </summary>
    private protected static IEnumerable<T> IterateValues<T>(Buffer<T> buf, long count, Shape shape, Shape strides, long root)
    {
        Validation.Assert(shape.Rank == strides.Rank);
        Validation.Assert(!shape.AnyNegative);
        // Caller should handle empty.
        Validation.Assert(!shape.AnyZero);
        Validation.Assert(count > 0);

        switch (shape.Rank)
        {
        case 0:
            Validation.Assert(count == 1);
            return new T[] { buf[root] };
        case 1:
            Validation.Assert(count == shape[0]);
            return IterateValues1(buf, shape[0], strides[0], root);
        case 2:
            Validation.Assert(count == shape[0] * shape[1]);
            return IterateValues2(buf, shape[0], strides[0], shape[1], strides[1], root);
        case 3:
            Validation.Assert(count == shape[0] * shape[1] * shape[2]);
            return IterateValues3(buf, shape[0], strides[0], shape[1], strides[1], shape[2], strides[2], root);
        }
        return IterateValuesGen(buf, count, shape, strides, root);
    }

    private protected static IEnumerable<long> IterateIndices1(long dim0, long str0, long off)
    {
        for (long i0 = 0; i0 < dim0; i0++, off += str0)
            yield return off;
    }

    private protected static IEnumerable<T> IterateValues1<T>(Buffer<T> buf, long dim0, long str0, long off)
    {
        for (long i0 = 0; i0 < dim0; i0++, off += str0)
            yield return buf[off];
    }

    private static IEnumerable<long> IterateIndices2(long dim0, long str0, long dim1, long str1, long off)
    {
        var p1 = dim1 * str1;
        for (long i0 = 0; i0 < dim0; i0++, off += str0)
        {
            for (long i1 = 0; i1 < dim1; i1++, off += str1)
                yield return off;
            off -= p1;
        }
    }

    private static IEnumerable<T> IterateValues2<T>(Buffer<T> buf, long dim0, long str0, long dim1, long str1, long off)
    {
        var p1 = dim1 * str1;
        for (long i0 = 0; i0 < dim0; i0++, off += str0)
        {
            for (long i1 = 0; i1 < dim1; i1++, off += str1)
                yield return buf[off];
            off -= p1;
        }
    }

    private static IEnumerable<long> IterateIndices3(long dim0, long str0, long dim1, long str1, long dim2, long str2, long off)
    {
        var p1 = dim1 * str1;
        var p2 = dim2 * str2;
        for (long i0 = 0; i0 < dim0; i0++, off += str0)
        {
            for (long i1 = 0; i1 < dim1; i1++, off += str1)
            {
                for (long i2 = 0; i2 < dim2; i2++, off += str2)
                    yield return off;
                off -= p2;
            }
            off -= p1;
        }
    }

    private static IEnumerable<T> IterateValues3<T>(Buffer<T> buf, long dim0, long str0, long dim1, long str1, long dim2, long str2, long off)
    {
        var p1 = dim1 * str1;
        var p2 = dim2 * str2;
        for (long i0 = 0; i0 < dim0; i0++, off += str0)
        {
            for (long i1 = 0; i1 < dim1; i1++, off += str1)
            {
                for (long i2 = 0; i2 < dim2; i2++, off += str2)
                    yield return buf[off];
                off -= p2;
            }
            off -= p1;
        }
    }

    private static IEnumerable<long> IterateIndicesGen(long count, Shape shape, Shape strides, long off)
    {
        int rank = shape.Rank;
        Validation.Assert(rank > 3);
        Validation.Assert(count > 0);

        // REVIEW: Perhaps optimize away any "1" slots? It'd be best if the caller did that.
        long[] inds = new long[shape.Rank];
        long[] ps = new long[shape.Rank];
        for (int i = 1; i < rank; i++)
            ps[i] = shape[i] * strides[i];

        long num = 0;
        for (; ; )
        {
            Validation.Assert(num < count);
            yield return off;
            num++;

            for (int d = inds.Length; ;)
            {
                Validation.Assert(d > 0);
                off += strides[--d];
                if (++inds[d] < shape[d])
                    break;
                if (d == 0)
                {
                    Validation.Assert(num == count);
                    yield break;
                }

                // Overflow of current dimension so rewind accordingly.
                off -= ps[d];
                inds[d] = 0;
            }
        }
    }

    private static IEnumerable<T> IterateValuesGen<T>(Buffer<T> buf, long count, Shape shape, Shape strides, long off)
    {
        int rank = shape.Rank;
        Validation.Assert(rank > 3);
        Validation.Assert(count > 0);

        // REVIEW: Perhaps optimize away any "1" slots? It'd be best if the caller did that.
        long[] inds = new long[shape.Rank];
        long[] ps = new long[shape.Rank];
        for (int i = 1; i < rank; i++)
            ps[i] = shape[i] * strides[i];

        long num = 0;
        for (; ; )
        {
            Validation.Assert(num < count);
            yield return buf[off];
            num++;

            for (int d = inds.Length; ;)
            {
                Validation.Assert(d > 0);
                off += strides[--d];
                if (++inds[d] < shape[d])
                    break;
                if (d == 0)
                {
                    Validation.Assert(num == count);
                    yield break;
                }

                // Overflow of current dimension so rewind accordingly.
                off -= ps[d];
                inds[d] = 0;
            }
        }
    }

    /// <summary>
    /// Adjust the <paramref name="shape"/> and <paramref name="strides"/> so <paramref name="slotSkip"/> is dropped,
    /// all slots with zero stride are dropped, and adjacent slots with compatible strides are combined. This reduces
    /// the amount of computation needed and shares output values.
    /// </summary>
    private protected static void GetCompressedShapeAndStrides(ref Shape shape, ref Shape strides, int slotSkip = -1)
    {
        if (shape.AnyZero)
        {
            shape = Shape.Scalar;
            strides = Shape.Scalar;
            return;
        }

        Validation.Assert(!shape.AnyNegative);
        Validation.Assert(!shape.AnyZero);

        // REVIEW: Try to avoid allocating the builders if we don't need to? Callers could also handle
        // those cases, so this code might not need to.
        var bldrShape = shape.ToBuilder();
        var bldrStrides = strides.ToBuilder();
        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < bldrShape.Count; ivSrc++)
        {
            if (ivSrc == slotSkip)
                continue;
            if (bldrStrides[ivSrc] == 0)
                continue;
            bldrShape[ivDst] = bldrShape[ivSrc];
            bldrStrides[ivDst] = bldrStrides[ivSrc];
            ivDst++;
        }
        Validation.AssertIndexInclusive(ivDst, bldrShape.Count);
        if (ivDst < bldrShape.Count)
        {
            bldrShape.RemoveTail(ivDst);
            bldrStrides.RemoveTail(ivDst);
        }
        if (bldrShape.Count == 0)
        {
            Validation.Assert(ivDst == 0);
            shape = Shape.Scalar;
            strides = Shape.Scalar;
            return;
        }

        // Try to combine slots (when the strides allow).
        ivDst = 0;
        for (int ivSrc = 1; ivSrc < bldrShape.Count; ivSrc++)
        {
            var str = bldrStrides[ivSrc];
            var num = bldrShape[ivSrc];
            if (bldrStrides[ivDst] == str * num)
            {
                // Combine this slot with the previous.
                bldrStrides[ivDst] = str;
                bldrShape[ivDst] *= num;
            }
            else if (++ivDst < ivSrc)
            {
                bldrStrides[ivDst] = str;
                bldrShape[ivDst] = num;
            }
        }
        Validation.AssertIndex(ivDst, bldrShape.Count);
        if (++ivDst < bldrShape.Count)
        {
            bldrShape.RemoveTail(ivDst);
            bldrStrides.RemoveTail(ivDst);
        }

        shape = bldrShape.ToImmutable();
        strides = bldrStrides.ToImmutable();
    }
}
