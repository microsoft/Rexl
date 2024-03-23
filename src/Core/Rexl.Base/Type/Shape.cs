// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

using ShapeArray = Immutable.Array<long>;

/// <summary>
/// Represents a tensor shape or strides as an immutable array of long. A legal shape
/// should have all entries non-negative. Strides can have negative entries. The
/// <see cref="Rank"/> is the number of entries.
/// </summary>
public struct Shape : IEnumerable<long>, IEquatable<Shape>
{
    /// <summary>
    /// Upper bound on dimension size. Also used to represented "unbounded" in tensor <see cref="DType"/> shapes.
    /// </summary>
    public const long DimMax = long.MaxValue;

    /// <summary>
    /// This is an empty <see cref="Shape"/>, representing the shape and strides for rank
    /// zero tensors.
    /// </summary>
    public static readonly Shape Scalar = new Shape(ShapeArray.Empty);

    /// <summary>All zeroes of rank 1.</summary>
    public static readonly Shape Zero1 = Create(0);

    /// <summary>All zeroes of rank 2.</summary>
    public static readonly Shape Zero2 = Create(0, 0);

    /// <summary>All zeroes of rank 3.</summary>
    public static readonly Shape Zero3 = Create(0, 0, 0);

    /// <summary>All zeroes of rank 4.</summary>
    public static readonly Shape Zero4 = Create(0, 0, 0, 0);

    /// <summary>All zeroes of rank 5.</summary>
    public static readonly Shape Zero5 = Create(0, 0, 0, 0, 0);

    /// <summary>All ones of rank 1. Useful for strides of a contiguous rank 1 tensor.</summary>
    public static readonly Shape One1 = Create(1);

    /// <summary>All ones of rank 2.</summary>
    public static readonly Shape One2 = Create(1, 1);

    /// <summary>All ones of rank 1.</summary>
    public static readonly Shape One3 = Create(1, 1, 1);

    /// <summary>All ones of rank 4.</summary>
    public static readonly Shape One4 = Create(1, 1, 1, 1);

    /// <summary>
    /// The entries as an <see cref="Immutable.Array{long}"/>.
    /// </summary>
    public readonly ShapeArray Items;

    public Shape(ShapeArray array)
    {
        Items = array;
    }

    public bool IsDefault => Items.IsDefault;

    /// <summary>
    /// The number of entries (dimensions).
    /// </summary>
    public int Rank => Items.Length;

    /// <summary>
    /// Accesses the entries.
    /// </summary>
    public long this[int index] => Items[index];

    /// <summary>
    /// Implicit conversion from immutable array to shape.
    /// </summary>
    public static implicit operator Shape(ShapeArray array) => new Shape(array);

    /// <summary>
    /// Creates a builder of un-determined rank.
    /// </summary>
    public static ShapeArray.Builder CreateBuilder() => ShapeArray.CreateBuilder();

    /// <summary>
    /// Creates a builder of known rank. The entries are pre-allocated. This is more efficient
    /// than using an un-determined rank builder.
    /// </summary>
    public static ShapeArray.Builder CreateBuilder(int rank) => ShapeArray.CreateBuilder(rank, init: true);

    /// <summary>
    /// Get a builder initialized with the entries of this shape.
    /// </summary>
    public ShapeArray.Builder ToBuilder() => Items.ToBuilder();

    /// <summary>
    /// Returns an all-zero shape of the given rank.
    /// </summary>
    public static Shape CreateZero(int rank)
    {
        Validation.BugCheckParam(rank >= 0, nameof(rank));

        switch (rank)
        {
        case 0: return Scalar;
        case 1: return Zero1;
        case 2: return Zero2;
        case 3: return Zero3;
        case 4: return Zero4;
        case 5: return Zero5;
        }
        return new Shape(CreateBuilder(rank).ToImmutable());
    }

    /// <summary>
    /// Returns a rank zero shape.
    /// </summary>
    public static Shape Create() => Scalar;

    /// <summary>
    /// Returns a rank one shape with the given dimension.
    /// </summary>
    public static Shape Create(long dim0) => new Shape(ShapeArray.Create(dim0));

    /// <summary>
    /// Returns a rank two shape with the given dimensions.
    /// </summary>
    public static Shape Create(long dim0, long dim1) => new Shape(ShapeArray.Create(dim0, dim1));

    /// <summary>
    /// Returns a rank three shape with the given dimensions.
    /// </summary>
    public static Shape Create(long dim0, long dim1, long dim2)
    {
        var bldr = CreateBuilder(3);
        bldr[0] = dim0;
        bldr[1] = dim1;
        bldr[2] = dim2;
        return new Shape(bldr.ToImmutable());
    }

    /// <summary>
    /// Returns a rank four shape with the given dimensions.
    /// </summary>
    public static Shape Create(long dim0, long dim1, long dim2, long dim3)
    {
        var bldr = CreateBuilder(4);
        bldr[0] = dim0;
        bldr[1] = dim1;
        bldr[2] = dim2;
        bldr[3] = dim3;
        return new Shape(bldr.ToImmutable());
    }

    /// <summary>
    /// Returns a rank five shape with the given dimensions.
    /// </summary>
    public static Shape Create(long dim0, long dim1, long dim2, long dim3, long dim4)
    {
        var bldr = CreateBuilder(5);
        bldr[0] = dim0;
        bldr[1] = dim1;
        bldr[2] = dim2;
        bldr[3] = dim3;
        bldr[4] = dim4;
        return new Shape(bldr.ToImmutable());
    }

    /// <summary>
    /// Returns a shape with the given dimensions.
    /// </summary>
    public static Shape Create(params long[] dims) => new Shape(ShapeArray.Create(dims));

    /// <summary>
    /// Returns a shape based on this one with the given dimension changed.
    /// </summary>
    public Shape SetDim(int d, long dim)
    {
        Validation.BugCheckIndex(d, Rank, nameof(d));

        if (dim == Items[d])
            return this;
        return new Shape(Items.SetItem(d, dim));
    }

    public Shape Reverse() => new Shape(Items.Reverse());

    /// <summary>
    /// Determines whether the shapes are equivalent (same rank and entries).
    /// </summary>
    public static bool operator ==(Shape a, Shape b)
    {
        if (a.Items.AreIdentical(b.Items))
            return true;

        if (a.IsDefault)
            return b.IsDefault;
        if (b.IsDefault)
            return false;
        if (a.Rank != b.Rank)
            return false;

        for (int i = 0; i < a.Rank; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    public static bool operator !=(Shape a, Shape b) => !(a == b);

    /// <summary>
    /// Returns whether the shape contains any zero entries.
    /// </summary>
    public bool AnyZero
    {
        get
        {
            int rank = Rank;
            for (int i = 0; i < rank; i++)
            {
                if (Items[i] == 0)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Returns whether the shape contains any negative entries.
    /// </summary>
    public bool AnyNegative
    {
        get
        {
            int rank = Rank;
            for (int i = 0; i < rank; i++)
            {
                if (Items[i] < 0)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Construct regular strides, with the given delta, from this shape. Returns false
    /// if there was overflow or any negative entries.
    /// 
    /// <paramref name="delta">Input: The offset between consecutives cells.
    /// Output: The offset between two items of this shape, which is the input value for delta * the count of items in this shape.</paramref>
    /// <paramref name="strides">The strides for this shape with the given delta.</paramref>
    /// </summary>
    public bool TryMakeStrides(ref long delta, out Shape strides)
    {
        int rank = Rank;
        if (rank == 0)
        {
            strides = Scalar;
            return true;
        }
        if (delta == 0 || AnyZero)
        {
            strides = CreateZero(rank);
            delta = 0;
            return true;
        }
        if (rank == 1 && delta == 1)
        {
            strides = One1;
            delta = Items[0];
            return true;
        }

        var bldr = CreateBuilder(rank);
        for (int i = rank; --i >= 0;)
        {
            bldr[i] = delta;
            var d = Items[i];
            if (d < 0 || !NumUtil.TryMulCounts(d, delta, out delta))
            {
                strides = default;
                return false;
            }
        }
        strides = new Shape(bldr.ToImmutable());
        return true;
    }

    /// <summary>
    /// Construct regular strides from this shape, but preserving zero slots in
    /// <paramref name="strides"/>. Also computes the number of distinct cells.
    /// Returns false if there was overflow or any negative entries.
    /// </summary>
    public bool TryMakeStridesLike(ref Shape strides, out long count)
    {
        int rank = Rank;
        if (AnyZero)
        {
            strides = CreateZero(rank);
            count = 0;
            return true;
        }
        if (rank == 0)
        {
            strides = Scalar;
            count = 1;
            return true;
        }
        if (rank == 1)
        {
            if (strides[0] == 0)
            {
                strides = Zero1;
                count = 1;
            }
            else
            {
                strides = One1;
                count = Items[0];
            }
            Validation.Assert(count != 0);
            return count > 0;
        }

        // REVIEW: Avoid allocating the builder when possible?
        long delta = 1;
        var bldr = CreateBuilder(rank);
        for (int i = rank; --i >= 0;)
        {
            if (strides[i] == 0)
                continue;
            bldr[i] = delta;
            var d = Items[i];
            if (d < 0 || !NumUtil.TryMulCounts(d, delta, out delta))
            {
                count = 0;
                return false;
            }
        }
        var res = new Shape(bldr.ToImmutable());
        if (strides != res)
            strides = res;
        count = delta;
        return true;
    }

    /// <summary>
    /// Computes the product of the entries in the shape.
    /// Returns false if there was overflow or any negative entries.
    /// </summary>
    public bool TryGetCount(out long count)
    {
        long num = 1;
        for (int i = 0; i < Items.Length; i++)
        {
            var d = Items[i];
            if (d < 0 || !NumUtil.TryMulCounts(d, num, out num))
            {
                count = -1;
                return false;
            }
        }
        count = num;
        return true;
    }

    public override int GetHashCode()
    {
        switch (Rank)
        {
        case 0: return 0;
        case 1: return HashCode.Combine(1, Items[0]);
        case 2: return HashCode.Combine(2, Items[0], Items[1]);
        case 3: return HashCode.Combine(3, Items[0], Items[1], Items[2]);
        case 4: return HashCode.Combine(4, Items[0], Items[1], Items[2], Items[3]);
        case 5: return HashCode.Combine(5, Items[0], Items[1], Items[2], Items[3], Items[4]);
        }

        var hash = new HashCode();
        hash.Add(Rank);
        for (int i = 0; i < Rank; i++)
            hash.Add(Items[i]);
        return hash.ToHashCode();
    }

    public bool Equals(Shape other)
    {
        return this == other;
    }

    public override bool Equals(object? obj)
    {
        return obj is Shape other && this == other;
    }

    public IEnumerator<long> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
}
