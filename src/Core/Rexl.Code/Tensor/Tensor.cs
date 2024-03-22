// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl;

/// <summary>
/// This is the non-generic base class for tensors.
/// </summary>
public abstract partial class Tensor
{
    /// <summary>
    /// The logical number of cells. This is the product of the entries of <see cref="_shape"/>.
    /// Note that this is NOT the number of distinct cells, since zero strides can cause cells to
    /// be shared.
    /// </summary>
    private protected readonly long _count;

    /// <summary>
    /// The shape. All slots are non-negative (may be zero).
    /// </summary>
    private protected readonly Shape _shape;

    /// <summary>
    /// The root offset into the data buffer. That is, this is the index of the (0, ..., 0) cell.
    /// </summary>
    private protected readonly long _root;

    /// <summary>
    /// The strides. Always the same rank as <see cref="_shape"/>. This is always normalized, meaning
    /// that slots with size 1 have corresponding stride zero. This facilitates broadcasting.
    /// Also note that if <see cref="_count"/> is zero, this is all zeros.
    /// </summary>
    private protected readonly Shape _strides;

    /// <summary>
    /// Whether the indices of consecutive logical cells differ by a constant. If so, that
    /// constant is <see cref="_delta"/>. Note that the concept of "consecutive" cells wraps
    /// like an odometer.
    /// </summary>
    private protected readonly bool _regular;

    /// <summary>
    /// When the layout is regular (<see cref="_regular"/> is true), this is the index offset
    /// between consecutive cells.
    /// </summary>
    private protected readonly long _delta;

    private protected Tensor(Shape shape, Shape strides, long root)
    {
        Validation.Assert(!shape.IsDefault);
        Validation.Assert(!strides.IsDefault);
        Validation.Assert(shape.Rank == strides.Rank);
        Validation.Assert(!shape.AnyNegative);

        int rank = shape.Rank;
        _shape = shape;
        _root = root;
        if (rank == 0)
        {
            _count = 1;
            _regular = true;
            _delta = 0;
            _strides = Shape.Scalar;
        }
        else if (!shape.TryGetCount(out _count))
            throw new InvalidOperationException("Tensor too large");
        else if (_count <= 1)
        {
            _regular = true;
            _delta = 0;
            _strides = Shape.CreateZero(rank);
        }
        else if (rank == 1)
        {
            _regular = true;
            _delta = strides[0];
            _strides = strides;
        }
        else
        {
            // Determine whether the strides are normalized and regular.
            // * Normalized means that for any dim that is one (zero has been handled above), the corresponding
            //   stride should be zero. This facilitates broadcasting.
            // * Regular means that the remaining strides align so the indices of subsequent items are always
            //   separated by a constant "delta".
            _delta = 0;
            _regular = true;
            long count = 1;
            long stride = 0;
            bool normalized = true;
            for (int i = rank; --i >= 0;)
            {
                long dim = shape[i];
                Validation.Assert(dim > 0);

                long prev = count;
                count *= dim;

                if (dim > 1)
                {
                    if (prev == 1)
                        _delta = stride = strides[i];
                    else if (strides[i] != stride)
                    {
                        _regular = false;
                        _delta = 0;
                    }
                    stride *= dim;
                }
                else if (strides[i] != 0)
                    normalized = false;
            }

            // Force normalization.
            if (!normalized)
            {
                var bldr = Shape.CreateBuilder(rank);
                for (int i = 0; i < rank; i++)
                {
                    long dim = shape[i];
                    Validation.Assert(dim > 0);
                    if (dim > 1)
                        bldr[i] = strides[i];
                }
                strides = bldr.ToImmutable();
            }

            _strides = strides;
        }
    }

    /// <summary>
    /// Gets the rank (number of dimensions), of the tensor.
    /// </summary>
    public int Rank => _shape.Rank;

    /// <summary>
    /// Gets the number of items in the tensor. Note that a rank 0 tensor is considered
    /// a scalar so has one item.
    /// </summary>
    public long Count => _count;

    /// <summary>
    /// The shape of this tensor.
    /// </summary>
    public Shape Shape => _shape;

    /// <summary>
    /// The strides for this tensor.
    /// </summary>
    internal Shape Strides => _strides;

    /// <summary>
    /// Iterates over the values as objects (weakly typed, boxed). Typically only used for test
    /// and in-house harnesses.
    /// </summary>
    public abstract IEnumerable GetObjValues();

    #region Static methods to assist code gen.

    public static long _GetCountStatic(Tensor ten)
    {
        Validation.AssertValue(ten);
        return ten._count;
    }

    public static Shape _GetShapeStatic(Tensor ten)
    {
        Validation.AssertValue(ten);
        return ten._shape;
    }

    public static T _GetFirstStatic<T>(Tensor<T> ten)
    {
        Validation.AssertValue(ten);
        return ten.GetFirst();
    }

    public static T _GetAtStatic<T>(Tensor<T> ten, long index)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 1);
        return ten.GetAtIndexCore(index);
    }

    public static T _GetAtStatic<T>(Tensor<T> ten, long i0, long i1)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 2);
        return ten.GetAtIndicesCore(i0, i1);
    }

    public static T _GetAtStatic<T>(Tensor<T> ten, long i0, long i1, long i2)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 3);
        return ten.GetAtIndicesCore(i0, i1, i2);
    }

    public static T _GetAtStatic<T>(Tensor<T> ten, long i0, long i1, long i2, long i3)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 4);
        return ten.GetAtIndicesCore(i0, i1, i2, i3);
    }

    public static T _GetAtStatic<T>(Tensor<T> ten, params long[] inds)
    {
        Validation.AssertValue(ten);
        Validation.AssertValue(inds);
        Validation.Assert(ten.Rank == inds.Length);
        return ten.GetAtIndicesCore(inds);
    }

    public static Tensor<T> _GetSliceStatic<T>(Tensor<T> ten, T defValue, SliceItem item0)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 1);
        var res = ten.GetSliceCore(item0, out var shape);
        return res ?? Tensor<T>.CreateFillCore(defValue, shape);
    }

    public static Tensor<T> _GetSliceStatic<T>(Tensor<T> ten, T defValue, SliceItem item0, SliceItem item1)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 2);
        var res = ten.GetSliceCore(item0, item1, out var shape);
        return res ?? Tensor<T>.CreateFillCore(defValue, shape);
    }

    public static Tensor<T> _GetSliceStatic<T>(Tensor<T> ten, T defValue, SliceItem item0, SliceItem item1, SliceItem item2)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 3);
        var res = ten.GetSliceCore(item0, item1, item2, out var shape);
        return res ?? Tensor<T>.CreateFillCore(defValue, shape);
    }

    public static Tensor<T> _GetSliceStatic<T>(Tensor<T> ten, T defValue, SliceItem item0, SliceItem item1, SliceItem item2, SliceItem item3)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 4);
        var res = ten.GetSliceCore(item0, item1, item2, item3, out var shape);
        return res ?? Tensor<T>.CreateFillCore(defValue, shape);
    }

    public static Tensor<T> _GetSliceStatic<T>(Tensor<T> ten, T defValue, SliceItem item0, SliceItem item1, SliceItem item2, SliceItem item3, SliceItem item4)
    {
        Validation.AssertValue(ten);
        Validation.Assert(ten.Rank == 5);
        var res = ten.GetSliceCore(item0, item1, item2, item3, item4, out var shape);
        return res ?? Tensor<T>.CreateFillCore(defValue, shape);
    }

    public static Tensor<T> _GetSliceStatic<T>(Tensor<T> ten, T defValue, SliceItem[] items)
    {
        Validation.AssertValue(ten);
        Validation.AssertValue(items);
        Validation.Assert(ten.Rank == items.Length);
        var res = ten.GetSliceCore(items, out var shape);
        return res ?? Tensor<T>.CreateFillCore(defValue, shape);
    }

    public static Tensor<U> _Map<T, U>(Tensor<T> ten, Func<T, U> map)
    {
        Validation.AssertValue(ten);
        Validation.AssertValue(map);
        return ten.Map(map, realize: true);
    }

    public static Tensor<U> _MapLazy<T, U>(Tensor<T> ten, Func<T, U> map)
    {
        Validation.AssertValue(ten);
        Validation.AssertValue(map);
        return ten.Map(map, realize: false);
    }

    public static Tensor<U> _Zip<T0, T1, U>(Tensor<T0> ten0, Tensor<T1> ten1, Func<T0, T1, U> map, out bool shrunk)
    {
        Validation.AssertValue(ten0);
        Validation.AssertValue(ten1);
        Validation.AssertValue(map);
        return PointWise.Zip(ten0, ten1, map, out shrunk);
    }

    public static Tensor<U> _Zip<T0, T1, T2, U>(Tensor<T0> ten0, Tensor<T1> ten1, Tensor<T2> ten2, Func<T0, T1, T2, U> map, out bool shrunk)
    {
        Validation.AssertValue(ten0);
        Validation.AssertValue(ten1);
        Validation.AssertValue(ten2);
        Validation.AssertValue(map);
        return PointWise.Zip(ten0, ten1, ten2, map, out shrunk);
    }

    #endregion Static methods to assist code gen.
}

/// <summary>
/// The generic tensor class.
/// </summary>
public sealed partial class Tensor<T> : Tensor
{
    /// <summary>
    /// REVIEW: It would be nice to control access of this to only <see cref="Tensor"/>.
    /// </summary>
    internal readonly Buffer<T> _buf;

    private Tensor(Buffer<T> buf, Shape shape, Shape strides, long root)
        : base(shape, strides, root)
    {
        // REVIEW: Verify that the shape/strides/root are consistent with the buffer size.
        Validation.AssertValue(buf);
        _buf = buf;
    }

    private static Buffer<T> BufferFrom(long count, T[] src)
    {
        return BufferFrom(count, new ReadOnlySpan<T>(src));
    }

    private static Buffer<T> BufferFrom(long count, ReadOnlySpan<T> src)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));

        var buf = new T[count];
        if (src.Length > count)
            src = src.Slice(0, (int)count);
        src.CopyTo(buf);
        return buf;
    }

    private static Buffer<T> BufferFrom(long count, IEnumerable<T> src)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));
        Validation.AssertValue(src);

        var buf = new T[count];
        long index = 0;
        foreach (var v in src)
        {
            buf[index++] = v;
            if (index >= count)
                break;
        }
        return buf;
    }

    private static Buffer<T> BufferFrom(long count, T[] src, T init)
    {
        return BufferFrom(count, new ReadOnlySpan<T>(src), init);
    }

    private static Buffer<T> BufferFrom(long count, ReadOnlySpan<T> src, T init)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));

        var buf = new T[count];
        if (src.Length > count)
            src = src.Slice(0, (int)count);
        src.CopyTo(buf);
        if (src.Length < count)
            Array.Fill(buf, init, src.Length, (int)(count - src.Length));

        return buf;
    }

    private static Buffer<T> BufferFrom(long count, IEnumerable<T> src, T init)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));
        Validation.AssertValue(src);

        var buf = new T[count];
        long index = 0;
        foreach (var v in src)
        {
            buf[index++] = v;
            if (index >= count)
                break;
        }

        if (index < count)
            Array.Fill(buf, init, (int)index, (int)(count - index));

        return buf;
    }

    // REVIEW: We really want this to be accessible only to Tensor!
    internal static Tensor<T> _CreateRaw(Shape shape, Shape strides, Buffer<T> buf, long root)
    {
        Validation.Assert(!shape.AnyNegative);
        return new Tensor<T>(buf, shape, strides, root);
    }

    // REVIEW: Should we allow this? Find a safe way to do what we need.
    public static Tensor<T> _CreateRaw(Shape shape, T[] buf)
    {
        long count = 1;
        Validation.BugCheckParam(shape.TryMakeStrides(ref count, out var strides), nameof(shape));
        Validation.BugCheckParam(buf is not null && buf.LongLength >= count, nameof(buf));

        return new Tensor<T>(buf, shape, strides, 0);
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with given value.
    /// </summary>
    public static Tensor<T> CreateFill(T value)
    {
        return new Tensor<T>(new T[] { value }, Shape.Scalar, Shape.Scalar, 0);
    }

    /// <summary>
    /// Create a constant tensor of rank one with the given dimension.
    /// </summary>
    public static Tensor<T> CreateFill(T value, long dim)
    {
        Validation.BugCheckParam(dim >= 0, nameof(dim));
        return new Tensor<T>(new T[] { value }, Shape.Create(dim), Shape.Zero1, 0);
    }

    /// <summary>
    /// Create a constant tensor of rank two with the given dimensions.
    /// </summary>
    public static Tensor<T> CreateFill(T value, long dim0, long dim1)
    {
        Validation.BugCheckParam(dim0 >= 0, nameof(dim0));
        Validation.BugCheckParam(dim1 >= 0, nameof(dim1));
        return new Tensor<T>(new T[] { value }, Shape.Create(dim0, dim1), Shape.Zero2, 0);
    }

    /// <summary>
    /// Create a constant tensor of rank three with the given dimensions.
    /// </summary>
    public static Tensor<T> CreateFill(T value, long dim0, long dim1, long dim2)
    {
        Validation.BugCheckParam(dim0 >= 0, nameof(dim0));
        Validation.BugCheckParam(dim1 >= 0, nameof(dim1));
        Validation.BugCheckParam(dim2 >= 0, nameof(dim2));
        return new Tensor<T>(new T[] { value }, Shape.Create(dim0, dim1, dim2), Shape.Zero3, 0);
    }

    /// <summary>
    /// Create a constant tensor with the given shape.
    /// </summary>
    public static Tensor<T> CreateFill(T value, Shape shape)
    {
        Validation.BugCheckParam(!shape.IsDefault, nameof(shape));
        Validation.BugCheckParam(!shape.AnyNegative, nameof(shape));
        int rank = shape.Rank;
        return new Tensor<T>(new T[] { value }, shape, Shape.CreateZero(rank), 0);
    }

    internal static Tensor<T> CreateFillCore(T value, Shape shape)
    {
        Validation.Assert(!shape.IsDefault);
        Validation.Assert(!shape.AnyNegative);
        int rank = shape.Rank;
        return new Tensor<T>(new T[] { value }, shape, Shape.CreateZero(rank), 0);
    }

    /// <summary>
    /// Create a constant tensor with the given dimensions.
    /// </summary>
    public static Tensor<T> CreateFill(T value, params long[] dims)
    {
        Validation.BugCheckValue(dims, nameof(dims));
        switch (dims.Length)
        {
        case 0: return CreateFill(value);
        case 1: return CreateFill(value, dims[0]);
        case 2: return CreateFill(value, dims[0], dims[1]);
        case 3: return CreateFill(value, dims[0], dims[1], dims[2]);
        default: return CreateFill(value, Shape.Create(dims));
        }
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with value being the first item of <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T[] src)
    {
        return CreateFrom(new ReadOnlySpan<T>(src));
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with value being the first item of <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(ReadOnlySpan<T> src)
    {
        if (src.Length == 0)
            return CreateFill(default);
        return new Tensor<T>(BufferFrom(1, src), Shape.Scalar, Shape.Scalar, 0);
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with value being the first item of <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(IEnumerable<T> src)
    {
        if (src == null)
            return CreateFill(default);
        return new Tensor<T>(BufferFrom(1, src), Shape.Scalar, Shape.Scalar, 0);
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with value being the first item of <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, T[] src)
    {
        return CreateFrom(init, new ReadOnlySpan<T>(src));
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with value being the first item of <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, ReadOnlySpan<T> src)
    {
        if (src.Length == 0)
            return CreateFill(init);
        return new Tensor<T>(BufferFrom(1, src, init), Shape.Scalar, Shape.Scalar, 0);
    }

    /// <summary>
    /// Create a rank zero (scalar) tensor with value being the first item of <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, IEnumerable<T> src)
    {
        if (src == null)
            return CreateFill(init);
        return new Tensor<T>(BufferFrom(1, src, init), Shape.Scalar, Shape.Scalar, 0);
    }

    /// <summary>
    /// Create a rank one tensor with the given dimension and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T[] src, long dim)
    {
        return CreateFrom(new ReadOnlySpan<T>(src), dim);
    }

    /// <summary>
    /// Create a rank one tensor with the given dimension and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(ReadOnlySpan<T> src, long dim)
    {
        Validation.BugCheckParam(dim >= 0, nameof(dim));
        if (dim == 0)
            return new Tensor<T>(Array.Empty<T>(), Shape.Create(0), Shape.Create(0), 0);
        if (src.Length == 0)
            return CreateFill(default, dim);
        return new Tensor<T>(BufferFrom(dim, src), Shape.Create(dim), Shape.Create(1), 0);
    }

    /// <summary>
    /// Create a rank one tensor with the given dimension and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(IEnumerable<T> src, long dim)
    {
        Validation.BugCheckParam(dim >= 0, nameof(dim));
        if (dim == 0)
            return new Tensor<T>(Array.Empty<T>(), Shape.Create(0), Shape.Create(0), 0);
        if (src == null)
            return CreateFill(default, dim);
        return new Tensor<T>(BufferFrom(dim, src), Shape.Create(dim), Shape.Create(1), 0);
    }

    /// <summary>
    /// Create a rank one tensor with the given dimension and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, T[] src, long dim)
    {
        return CreateFrom(init, new ReadOnlySpan<T>(src), dim);
    }

    /// <summary>
    /// Create a rank one tensor with the given dimension and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, ReadOnlySpan<T> src, long dim)
    {
        Validation.BugCheckParam(dim >= 0, nameof(dim));
        if (dim == 0)
            return new Tensor<T>(Array.Empty<T>(), Shape.Create(0), Shape.Create(0), 0);
        if (src.Length == 0)
            return CreateFill(init, dim);
        return new Tensor<T>(BufferFrom(dim, src, init), Shape.Create(dim), Shape.Create(1), 0);
    }

    /// <summary>
    /// Create a rank one tensor with the given dimension and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, IEnumerable<T> src, long dim)
    {
        Validation.BugCheckParam(dim >= 0, nameof(dim));
        if (dim == 0)
            return new Tensor<T>(Array.Empty<T>(), Shape.Create(0), Shape.Create(0), 0);
        if (src == null)
            return CreateFill(init, dim);
        return new Tensor<T>(BufferFrom(dim, src, init), Shape.Create(dim), Shape.Create(1), 0);
    }

    /// <summary>
    /// Create a tensor with the given shape and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T[] src, Shape shape)
    {
        return CreateFrom(new ReadOnlySpan<T>(src), shape);
    }

    /// <summary>
    /// Create a tensor with the given shape and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(ReadOnlySpan<T> src, Shape shape)
    {
        Validation.BugCheckParam(!shape.IsDefault, nameof(shape));
        int rank = shape.Rank;
        if (rank == 0)
        {
            if (src.Length == 0)
                return CreateFill(default);
            return new Tensor<T>(BufferFrom(1, src), Shape.Scalar, Shape.Scalar, 0);
        }

        if (src.Length == 0)
            return CreateFill(default, shape);

        long count = 1;
        Validation.BugCheckParam(shape.TryMakeStrides(ref count, out var strides), nameof(shape));
        Validation.Assert(count >= 0);
        return new Tensor<T>(count == 0 ? Array.Empty<T>() : BufferFrom(count, src), shape, strides, 0);
    }

    /// <summary>
    /// Create a tensor with the given shape and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(IEnumerable<T> src, Shape shape)
    {
        Validation.BugCheckParam(!shape.IsDefault, nameof(shape));
        int rank = shape.Rank;
        if (rank == 0)
        {
            if (src == null)
                return CreateFill(default);
            return new Tensor<T>(BufferFrom(1, src), Shape.Scalar, Shape.Scalar, 0);
        }

        if (src == null)
            return CreateFill(default, shape);

        long count = 1;
        Validation.BugCheckParam(shape.TryMakeStrides(ref count, out var strides), nameof(shape));
        Validation.Assert(count >= 0);
        return new Tensor<T>(count == 0 ? Array.Empty<T>() : BufferFrom(count, src), shape, strides, 0);
    }

    /// <summary>
    /// Create a tensor with the given shape and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, T[] src, Shape shape)
    {
        return CreateFrom(init, new ReadOnlySpan<T>(src), shape);
    }

    /// <summary>
    /// Create a tensor with the given shape and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, ReadOnlySpan<T> src, Shape shape)
    {
        Validation.BugCheckParam(!shape.IsDefault, nameof(shape));
        int rank = shape.Rank;
        if (rank == 0)
        {
            if (src.Length == 0)
                return CreateFill(init);
            return new Tensor<T>(BufferFrom(1, src, init), Shape.Scalar, Shape.Scalar, 0);
        }

        if (src.Length == 0)
            return CreateFill(init, shape);

        long count = 1;
        Validation.BugCheckParam(shape.TryMakeStrides(ref count, out var strides), nameof(shape));
        Validation.Assert(count >= 0);
        return new Tensor<T>(count == 0 ? Array.Empty<T>() : BufferFrom(count, src, init), shape, strides, 0);
    }

    /// <summary>
    /// Create a tensor with the given shape and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, IEnumerable<T> src, Shape shape)
    {
        Validation.BugCheckParam(!shape.IsDefault, nameof(shape));
        int rank = shape.Rank;
        if (rank == 0)
        {
            if (src == null)
                return CreateFill(init);
            return new Tensor<T>(BufferFrom(1, src, init), Shape.Scalar, Shape.Scalar, 0);
        }

        if (src == null)
            return CreateFill(init, shape);

        long count = 1;
        Validation.BugCheckParam(shape.TryMakeStrides(ref count, out var strides), nameof(shape));
        Validation.Assert(count >= 0);
        return new Tensor<T>(count == 0 ? Array.Empty<T>() : BufferFrom(count, src, init), shape, strides, 0);
    }

    /// <summary>
    /// Create a tensor with the given dimensions and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T[] src, params long[] dims)
    {
        return CreateFrom(new ReadOnlySpan<T>(src), dims);
    }

    /// <summary>
    /// Create a tensor with the given dimensions and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(ReadOnlySpan<T> src, params long[] dims)
    {
        Validation.BugCheckValue(dims, nameof(dims));
        switch (dims.Length)
        {
        case 0: return CreateFrom(src);
        case 1: return CreateFrom(src, dims[0]);
        default: return CreateFrom(src, Shape.Create(dims));
        }
    }

    /// <summary>
    /// Create a tensor with the given dimensions and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(IEnumerable<T> src, params long[] dims)
    {
        Validation.BugCheckValue(dims, nameof(dims));
        switch (dims.Length)
        {
        case 0: return CreateFrom(src);
        case 1: return CreateFrom(src, dims[0]);
        default: return CreateFrom(src, Shape.Create(dims));
        }
    }

    /// <summary>
    /// Create a tensor with the given dimensions and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, T[] src, params long[] dims)
    {
        return CreateFrom(init, new ReadOnlySpan<T>(src), dims);
    }

    /// <summary>
    /// Create a tensor with the given dimensions and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, ReadOnlySpan<T> src, params long[] dims)
    {
        Validation.BugCheckValue(dims, nameof(dims));
        switch (dims.Length)
        {
        case 0: return CreateFrom(init, src);
        case 1: return CreateFrom(init, src, dims[0]);
        default: return CreateFrom(init, src, Shape.Create(dims));
        }
    }

    /// <summary>
    /// Create a tensor with the given dimensions and values coming from <paramref name="src"/>.
    /// </summary>
    public static Tensor<T> CreateFrom(T init, IEnumerable<T> src, params long[] dims)
    {
        Validation.BugCheckValue(dims, nameof(dims));
        switch (dims.Length)
        {
        case 0: return CreateFrom(init, src);
        case 1: return CreateFrom(init, src, dims[0]);
        default: return CreateFrom(init, src, Shape.Create(dims));
        }
    }

    /// <summary>
    /// Get the first item of this tensor or (system) default if the tensor is empty.
    /// </summary>
    public T GetFirstOrDefault()
    {
        if (_count == 0)
            return default;
        return _buf[_root];
    }

    /// <summary>
    /// Get the first item of this tensor, throwing if the tensor is empty.
    /// </summary>
    public T GetFirst()
    {
        Validation.BugCheck(_count > 0);
        return _buf[_root];
    }

    /// <summary>
    /// Get the value at given logical index. This applies to tensors of any rank.
    /// </summary>
    public T GetAtIndex(long index)
    {
        Validation.BugCheckIndex(index, _count, nameof(index));
        return GetAtIndexCore(index);
    }

    internal T GetAtIndexCore(long index)
    {
        Validation.AssertIndex(index, _count);

        long off = _root;
        if (index == 0 || _regular && _delta == 0)
            return _buf[off];
        if (_regular)
            return _buf[off + index * _delta];

        for (int d = Rank; --d >= 0;)
        {
            index = Math.DivRem(index, _shape[d], out long cur);
            off += cur * _strides[d];
        }
        return _buf[off];
    }

    /// <summary>
    /// Get the value at given indices in this rank two tensor.
    /// </summary>
    public T GetAtIndices(long i0, long i1)
    {
        Validation.BugCheck(Rank == 2);
        Validation.BugCheckIndex(i0, _shape[0], nameof(i0));
        Validation.BugCheckIndex(i1, _shape[1], nameof(i1));
        return GetAtIndicesCore(i0, i1);
    }

    internal T GetAtIndicesCore(long i0, long i1)
    {
        Validation.Assert(Rank == 2);
        Validation.AssertIndex(i0, _shape[0]);
        Validation.AssertIndex(i1, _shape[1]);

        long off = _root;
        if (i0 != 0)
            off += _strides[0] * i0;
        if (i1 != 0)
            off += _strides[1] * i1;
        return _buf[off];
    }

    /// <summary>
    /// Get the value at given indices in this rank three tensor.
    /// </summary>
    public T GetAtIndices(long i0, long i1, long i2)
    {
        Validation.BugCheck(Rank == 3);
        Validation.BugCheckIndex(i0, _shape[0], nameof(i0));
        Validation.BugCheckIndex(i1, _shape[1], nameof(i1));
        Validation.BugCheckIndex(i2, _shape[2], nameof(i2));
        return GetAtIndicesCore(i0, i1, i2);
    }

    internal T GetAtIndicesCore(long i0, long i1, long i2)
    {
        Validation.Assert(Rank == 3);
        Validation.AssertIndex(i0, _shape[0]);
        Validation.AssertIndex(i1, _shape[1]);
        Validation.AssertIndex(i2, _shape[2]);

        long off = _root;
        if (i0 != 0)
            off += _strides[0] * i0;
        if (i1 != 0)
            off += _strides[1] * i1;
        if (i2 != 0)
            off += _strides[2] * i2;
        return _buf[off];
    }

    /// <summary>
    /// Get the value at given indices in this rank four tensor.
    /// </summary>
    public T GetAtIndices(long i0, long i1, long i2, long i3)
    {
        Validation.BugCheck(Rank == 4);
        Validation.BugCheckIndex(i0, _shape[0], nameof(i0));
        Validation.BugCheckIndex(i1, _shape[1], nameof(i1));
        Validation.BugCheckIndex(i2, _shape[2], nameof(i2));
        Validation.BugCheckIndex(i3, _shape[3], nameof(i3));
        return GetAtIndicesCore(i0, i1, i2, i3);
    }

    internal T GetAtIndicesCore(long i0, long i1, long i2, long i3)
    {
        Validation.Assert(Rank == 4);
        Validation.AssertIndex(i0, _shape[0]);
        Validation.AssertIndex(i1, _shape[1]);
        Validation.AssertIndex(i2, _shape[2]);
        Validation.AssertIndex(i3, _shape[3]);

        long off = _root;
        if (i0 != 0)
            off += _strides[0] * i0;
        if (i1 != 0)
            off += _strides[1] * i1;
        if (i2 != 0)
            off += _strides[2] * i2;
        if (i3 != 0)
            off += _strides[3] * i3;
        return _buf[off];
    }

    /// <summary>
    /// Get the value at given indices.
    /// </summary>
    public T GetAtIndices(ReadOnly.Array<long> inds)
    {
        Validation.BugCheck(Rank == inds.Length);
        for (int i = 0; i < Rank; i++)
            Validation.BugCheckIndex(inds[i], _shape[i], nameof(inds));
        return GetAtIndicesCore(inds);
    }

    internal T GetAtIndicesCore(ReadOnly.Array<long> inds)
    {
        Validation.Assert(Rank == inds.Length);

        long off = _root;
        for (int i = 0; i < Rank; i++)
        {
            var ind = inds[i];
            Validation.AssertIndex(ind, _shape[i]);
            if (ind != 0)
                off += ind * _strides[i];
        }
        return _buf[off];
    }

    /// <summary>
    /// Enumerates all values in "odometer order". Never returns <c>null</c>.
    /// </summary>
    public IEnumerable<T> GetValues()
    {
        if (_count == 0)
            return Array.Empty<T>();
        if (_regular)
        {
            if (_delta == 0)
                return new RepeatSequence<T>(_buf[_root], _count);
            return IterateValues1(_buf, _count, _delta, _root);
        }
        Validation.Assert(Rank > 1);
        return IterateValues(_buf, _count, _shape, _strides, _root);
    }

    /// <summary>
    /// Enumerates all values in "odometer order". The result implements
    /// <see cref="ICanCount"/>. When the count is zero, this returns <c>null</c>.
    /// </summary>
    public IEnumerable<T> GetValuesWithCount()
    {
        if (_count == 0)
            return null;
        if (_regular)
        {
            if (_delta == 0)
                return new RepeatSequence<T>(_buf[_root], _count);
            return WrapWithCount.Create<T>(_count, IterateValues1(_buf, _count, _delta, _root));
        }
        Validation.Assert(Rank > 1);
        return WrapWithCount.Create<T>(_count, IterateValues(_buf, _count, _shape, _strides, _root));
    }

    public override IEnumerable GetObjValues()
    {
        return GetValues();
    }

    /// <summary>
    /// Reshape to be a rank zero (scalar) tensor. Checks that the number of cells is one.
    /// </summary>
    public Tensor<T> Reshape()
    {
        Validation.BugCheck(Count == 1);
        if (Rank == 0)
            return this;
        return CreateFill(GetFirstOrDefault());
    }

    /// <summary>
    /// Reshape to be a rank one tensor (vector). Checks that the number of cells is dim.
    /// </summary>
    public Tensor<T> Reshape(long dim)
    {
        Validation.BugCheck(_count == dim);

        int rank = Rank;
        if (rank == 1)
        {
            Validation.Assert(_shape[0] == dim);
            return this;
        }

        var shape = Shape.Create(dim);
        if (_regular)
            return new Tensor<T>(_buf, shape, Shape.Create(_delta), _root);

        // Not regular, so need to copy.
        // REVIEW: Take advantage of cell sharing (zero strides), when things align correctly.
        // For example, if "1" dimensions are being added, removed, moved, etc. Or one dim is being
        // factored.
        return new Tensor<T>(BufferFrom(_count, GetValues()), shape, Shape.Create(1), 0);
    }

    public Tensor<T> Reshape(Shape shape)
    {
        Validation.BugCheckParam(!shape.IsDefault, nameof(shape));
        if (shape.Rank == 0)
            return Reshape();
        if (shape.Rank == 1)
            return Reshape(shape[0]);
        return ReshapeCore(shape);
    }

    public Tensor<T> Reshape(params long[] dims)
    {
        Validation.BugCheckValue(dims, nameof(dims));
        if (dims.Length == 0)
            return Reshape();
        if (dims.Length == 1)
            return Reshape(dims[0]);
        return ReshapeCore(Shape.Create(dims));
    }

    private Tensor<T> ReshapeCore(Shape shape)
    {
        Validation.Assert(shape.Rank >= 2);

        if (shape == _shape)
            return this;

        long count = _regular ? _delta : 1;
        Validation.BugCheckParam(shape.TryMakeStrides(ref count, out var strides), nameof(shape));
        if (_regular)
        {
            if (_delta == 0)
                Validation.BugCheckParam(shape.TryGetCount(out count), nameof(shape));
            else
            {
                Validation.Assert(count % _delta == 0);
                count /= _delta;
            }
        }

        Validation.BugCheckParam(count == _count, nameof(shape));

        if (_regular)
            return new Tensor<T>(_buf, shape, strides, _root);

        // Not regular, so need to copy.
        // REVIEW: Take advantage of cell sharing (zero strides), when things align correctly.
        // For example, if "1" dimensions are being added, removed, moved, etc. Or one dim is being
        // factored.
        return new Tensor<T>(BufferFrom(_count, GetValues()), shape, strides, 0);
    }

    /// <summary>
    /// Shrinks the indicated slot, <paramref name="d"/>, to the given size, <paramref name="dim"/>.
    /// </summary>
    public Tensor<T> ShrinkDim(int d, long dim)
    {
        Validation.BugCheckIndex(d, Rank, nameof(d));

        if (dim == _shape[d])
            return this;
        Validation.BugCheckIndex(dim, _shape[d], nameof(dim));

        var shape = _shape.SetDim(d, dim);
        return new Tensor<T>(_buf, shape, _strides, _root);
    }

    /// <summary>
    /// Map this tensor through an element-wise mapping function. If <paramref name="realize"/> is
    /// true, forces a fully realized (array based) buffer. Otherwise, may wrap in a lazy mapper.
    /// </summary>
    public Tensor<U> Map<U>(Func<T, U> map, bool realize = false)
    {
        Validation.BugCheckValue(map, nameof(map));

        if (_count == 0)
            return new Tensor<U>(Buffer<U>.Empty, _shape, Shape.CreateZero(_shape.Rank), 0);
        if (_regular && _delta == 0)
            return new Tensor<U>(new U[] { map(_buf[_root]) }, _shape, Shape.CreateZero(_shape.Rank), 0);

        if (!realize && _buf.Length > Buffer<U>.EagerThreshold)
        {
            // Use a lazy map.
            return Tensor<U>._CreateRaw(_shape, _strides, _buf.Map(map, realize), _root);
        }

        long countMap;
        Shape shapeMap;
        Shape stridesMap;
        Shape stridesDst;
        if (!_strides.AnyZero)
        {
            countMap = _count;
            shapeMap = _shape;
            stridesMap = _strides;
            long count = 1;
            _shape.TryMakeStrides(ref count, out stridesDst).Verify();
            Validation.Assert(count == _count);
        }
        else
        {
            // Leverage the zero strides.
            shapeMap = _shape;
            stridesMap = _strides;
            GetCompressedShapeAndStrides(ref shapeMap, ref stridesMap);
            if (!shapeMap.TryGetCount(out countMap))
                throw new InvalidOperationException("Tensor too large");
            stridesDst = _strides;
            _shape.TryMakeStridesLike(ref stridesDst, out long num).Verify();
            Validation.Assert(countMap == num);
        }

        var buf = Tensor<U>.BufferFrom(countMap, IterateValues(_buf, countMap, shapeMap, stridesMap, _root).Select(map));
        return new Tensor<U>(buf, _shape, stridesDst, 0);
    }

    /// <summary>
    /// Returns the transpose of this Tensor, that is, reverses the dimensions in the shape.
    ///
    /// If the shape of this Tensor is (i[0], i[1], ... i[n-2], i[n-1]),
    /// then this method returns a Tensor with shape (i[n-1], i[n-2], ... i[1], i[0]).
    /// </summary>
    public Tensor<T> Transpose()
    {
        if (Rank <= 1)
            return this;

        return new Tensor<T>(_buf, _shape.Reverse(), _strides.Reverse(), _root);
    }

    /// <summary>
    /// Transpose this tensor with its axes permuted in the order of the given arguments.
    /// </summary>
    public Tensor<T> Transpose(int i0, int i1)
    {
        Validation.BugCheck(Rank == 2);
        Validation.BugCheckIndex(i0, Rank, nameof(i0));
        Validation.BugCheckIndex(i1, Rank, nameof(i1));
        Validation.BugCheck((1 << i0 | 1 << i1) == 0x3);
        if (i0 == 0 && i1 == 1)
            return this;
        return new Tensor<T>(_buf, Shape.Create(_shape[i0], _shape[i1]), Shape.Create(_strides[i0], _strides[i1]), _root);
    }

    /// <summary>
    /// Transpose this tensor with its axes permuted in the order of the given arguments.
    /// </summary>
    public Tensor<T> Transpose(int i0, int i1, int i2)
    {
        Validation.BugCheck(Rank == 3);
        Validation.BugCheckIndex(i0, Rank, nameof(i0));
        Validation.BugCheckIndex(i1, Rank, nameof(i1));
        Validation.BugCheckIndex(i2, Rank, nameof(i2));
        Validation.BugCheck((1 << i0 | 1 << i1 | 1 << i2) == 0x7);
        if (i0 == 0 && i1 == 1 && i2 == 2)
            return this;
        return new Tensor<T>(_buf, Shape.Create(_shape[i0], _shape[i1], _shape[i2]), Shape.Create(_strides[i0], _strides[i1], _strides[i2]), _root);
    }

    /// <summary>
    /// Transpose this tensor with its axes permuted in the order of the given arguments.
    /// </summary>
    public Tensor<T> Transpose(int i0, int i1, int i2, int i3)
    {
        Validation.BugCheck(Rank == 4);
        Validation.BugCheckIndex(i0, Rank, nameof(i0));
        Validation.BugCheckIndex(i1, Rank, nameof(i1));
        Validation.BugCheckIndex(i2, Rank, nameof(i2));
        Validation.BugCheckIndex(i3, Rank, nameof(i3));
        Validation.BugCheck((1 << i0 | 1 << i1 | 1 << i2 | 1 << i3) == 0xF);
        if (i0 == 0 && i1 == 1 && i2 == 2 && i3 == 3)
            return this;
        return new Tensor<T>(_buf, Shape.Create(_shape[i0], _shape[i1], _shape[i2], _shape[i3]), Shape.Create(_strides[i0], _strides[i1], _strides[i2], _strides[i3]), _root);
    }

    /// <summary>
    /// Transpose this tensor with its axes permuted in the order of the given arguments.
    /// </summary>
    public Tensor<T> Transpose(params int[] permutation)
    {
        return Transpose(new ReadOnly.Array<int>(permutation));
    }

    /// <summary>
    /// Transpose this tensor with its axes permuted in the order of the given arguments.
    /// </summary>
    public Tensor<T> Transpose(ReadOnly.Array<int> permutation)
    {
        Validation.BugCheckParam(permutation.Length == Rank, nameof(permutation));

        if (Rank <= 1)
        {
            Validation.BugCheckParam(Rank == 0 || permutation[0] == 0, nameof(permutation));
            return this;
        }

        var bldrShape = Shape.CreateBuilder(Rank);
        var bldrStride = Shape.CreateBuilder(Rank);
        int ibldr = 0;
        var slots = new BitSet();
        var id = true;
        for (int i = 0; i < permutation.Length; i++)
        {
            var slot = permutation[i];
            Validation.BugCheckIndex(slot, Rank, nameof(permutation));
            Validation.BugCheckParam(!slots.TestBit(slot), nameof(permutation));
            if (permutation[i] != i)
                id = false;

            slots = slots.SetBit(slot);
            bldrShape[ibldr] = _shape[slot];
            bldrStride[ibldr] = _strides[slot];
            ibldr++;
        }
        if (id)
            return this;

        Validation.Assert(slots == BitSet.GetMask(Rank));
        Validation.Assert(ibldr == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// Insert a new dimension of size 1 at the given position.
    /// </summary>
    public Tensor<T> ExpandDims(int i0)
    {
        int count = Rank + 1;
        Validation.BugCheckIndex(i0, count, nameof(i0));

        if (Rank == 0)
            return new Tensor<T>(_buf, Shape.One1, Shape.Zero1, _root);

        var bldrShape = Shape.CreateBuilder(count);
        var bldrStride = Shape.CreateBuilder(count);
        int ishape = 0;
        for (int ibldr = 0; ibldr < count; ibldr++)
        {
            if (ibldr == i0)
            {
                bldrShape[ibldr] = 1;
                bldrStride[ibldr] = 0;
            }
            else
            {
                Validation.AssertIndex(ishape, Rank);
                bldrShape[ibldr] = Shape[ishape];
                bldrStride[ibldr] = _strides[ishape];
                ishape++;
            }
        }
        Validation.Assert(ishape == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// Insert new dimensions of size 1 at the given positions. The positions must be sorted.
    /// </summary>
    public Tensor<T> ExpandDims(int i0, int i1)
    {
        int count = Rank + 2;
        Validation.BugCheckIndex(i1, count, nameof(i1));
        Validation.BugCheckIndex(i0, i1, nameof(i0));

        if (Rank == 0)
            return new Tensor<T>(_buf, Shape.One2, Shape.Zero2, _root);

        var bldrShape = Shape.CreateBuilder(count);
        var bldrStride = Shape.CreateBuilder(count);
        int ishape = 0;
        for (int ibldr = 0; ibldr < count; ibldr++)
        {
            if (ibldr == i0 || ibldr == i1)
            {
                bldrShape[ibldr] = 1;
                bldrStride[ibldr] = 0;
            }
            else
            {
                Validation.AssertIndex(ishape, Rank);
                bldrShape[ibldr] = Shape[ishape];
                bldrStride[ibldr] = _strides[ishape];
                ishape++;
            }
        }
        Validation.Assert(ishape == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// Insert new dimensions of size 1 at the given positions. The positions must be sorted.
    /// </summary>
    public Tensor<T> ExpandDims(int i0, int i1, int i2)
    {
        int count = Rank + 3;
        Validation.BugCheckIndex(i2, count, nameof(i2));
        Validation.BugCheckIndex(i1, i2, nameof(i1));
        Validation.BugCheckIndex(i0, i1, nameof(i0));

        if (Rank == 0)
            return new Tensor<T>(_buf, Shape.One3, Shape.Zero3, _root);

        var bldrShape = Shape.CreateBuilder(count);
        var bldrStride = Shape.CreateBuilder(count);
        int ishape = 0;
        for (int ibldr = 0; ibldr < count; ibldr++)
        {
            if (ibldr == i0 || ibldr == i1 || ibldr == i2)
            {
                bldrShape[ibldr] = 1;
                bldrStride[ibldr] = 0;
            }
            else
            {
                Validation.AssertIndex(ishape, Rank);
                bldrShape[ibldr] = Shape[ishape];
                bldrStride[ibldr] = _strides[ishape];
                ishape++;
            }
        }
        Validation.Assert(ishape == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// Insert new dimensions of size 1 at the given positions.
    /// </summary>
    public Tensor<T> ExpandDims(int i0, int i1, int i2, int i3)
    {
        int count = Rank + 4;
        Validation.BugCheckIndex(i3, count, nameof(i3));
        Validation.BugCheckIndex(i2, i3, nameof(i2));
        Validation.BugCheckIndex(i1, i2, nameof(i1));
        Validation.BugCheckIndex(i0, i1, nameof(i0));

        if (Rank == 0)
            return new Tensor<T>(_buf, Shape.One4, Shape.Zero4, _root);

        var bldrShape = Shape.CreateBuilder(count);
        var bldrStride = Shape.CreateBuilder(count);
        int ishape = 0;
        for (int ibldr = 0; ibldr < count; ibldr++)
        {
            if (ibldr == i0 || ibldr == i1 || ibldr == i2 || ibldr == i3)
            {
                bldrShape[ibldr] = 1;
                bldrStride[ibldr] = 0;
            }
            else
            {
                Validation.AssertIndex(ishape, Rank);
                bldrShape[ibldr] = Shape[ishape];
                bldrStride[ibldr] = _strides[ishape];
                ishape++;
            }
        }
        Validation.Assert(ishape == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// Insert new dimensions of size 1 at the given positions. The positions should be soretd.
    /// </summary>
    public Tensor<T> ExpandDims(params int[] slots)
    {
        return ExpandDims(new ReadOnly.Array<int>(slots));
    }

    /// <summary>
    /// Insert new dimensions of size 1 at the given positions. The positions should be sorted.
    /// </summary>
    public Tensor<T> ExpandDims(ReadOnly.Array<int> slots)
    {
        if (slots.Length == 0)
            return this;

        int count = Rank + slots.Length;
        var set = new BitSet();
        int lim = count;
        for (int i = slots.Length; --i >= 0;)
        {
            int slot = slots[i];
            Validation.BugCheckIndex(slot, lim, nameof(slots));
            set = set.SetBit(slot);
            lim = slot;
        }
        Validation.Assert(set.Count == slots.Length);

        var bldrShape = Shape.CreateBuilder(count);
        var bldrStride = Shape.CreateBuilder(count);
        int ishape = 0;
        for (int ibldr = 0; ibldr < count; ibldr++)
        {
            if (set.TestBit(ibldr))
            {
                bldrShape[ibldr] = 1;
                bldrStride[ibldr] = 0;
            }
            else
            {
                Validation.AssertIndex(ishape, Rank);
                bldrShape[ibldr] = Shape[ishape];
                bldrStride[ibldr] = _strides[ishape];
                ishape++;
            }
        }
        Validation.Assert(ishape == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// Insert new dimensions of size 1 at the positions indicated by the <see cref="BitSet"/>.
    /// </summary>
    public Tensor<T> ExpandDims(BitSet slots)
    {
        if (slots.IsEmpty)
            return this;

        int rankNew = Rank + slots.Count;
        Validation.BugCheckParam(!slots.TestAtOrAbove(rankNew), nameof(rankNew));

        var bldrShape = Shape.CreateBuilder(rankNew);
        var bldrStride = Shape.CreateBuilder(rankNew);
        int ishape = 0;
        for (int ibldr = 0; ibldr < rankNew; ibldr++)
        {
            if (slots.TestBit(ibldr))
            {
                bldrShape[ibldr] = 1;
                bldrStride[ibldr] = 0;
            }
            else
            {
                Validation.AssertIndex(ishape, Rank);
                bldrShape[ibldr] = Shape[ishape];
                bldrStride[ibldr] = _strides[ishape];
                ishape++;
            }
        }
        Validation.Assert(ishape == Rank);
        return new Tensor<T>(_buf, bldrShape.ToImmutable(), bldrStride.ToImmutable(), _root);
    }

    /// <summary>
    /// A strongly typed tensor builder.
    /// 
    /// This class is not thread safe.
    /// </summary>
    public sealed class Builder
    {
        private readonly Shape _shape;
        private readonly Shape _strides;
        private readonly long _count;
        private T[] _items;

        // If true, then the _items array has already been used to build a tensor, so it needs to be copied to
        // make any further changes.
        private bool _isBuilt;

        /// <summary>
        /// The shape of the tensor being built.
        /// </summary>
        public Shape Shape => _shape;

        /// <summary>
        /// The number of items in the tensor being built.
        /// </summary>
        public long Count => _count;

        /// <summary>
        /// The strides for this tensor. The number of slots between between values with adjacent indices along each dimension.
        /// </summary>
        public Shape Strides => _strides;

        private Builder(Shape shape)
        {
            _count = 1;
            Validation.CheckParam(shape.TryMakeStrides(ref _count, out _strides), nameof(shape));
            Validation.CheckParam(!shape.IsDefault, nameof(shape));

            _shape = shape;
            _items = new T[Count];
            _isBuilt = false;
        }

        /// <summary>
        /// Create a builder initialized to the (system) default value of <typeparamref name="T"/>.
        /// </summary>
        public static Builder Create(Shape shape)
        {
            return new Builder(shape);
        }

        /// <summary>
        /// Create a builder initialized to the given <paramref name="init"/> value.
        /// </summary>
        public static Builder Create(Shape shape, T init)
        {
            var bldr = new Builder(shape);
            if (init != null)
                Array.Fill(bldr._items, init);
            return bldr;
        }

        /// <summary>
        /// Adds a value to the tensor.
        /// </summary>
        public void Set(long index, T value)
        {
            Validation.BugCheckIndex(index, _items.Length, nameof(index));

            if (_isBuilt)
            {
                _items = (T[])_items.Clone();
                _isBuilt = false;
            }
            _items[index] = value;
        }

        /// <summary>
        /// Build a tensor using the values added by the <see cref="Add"/> method.
        /// </summary>
        public Tensor<T> BuildGeneric()
        {
            _isBuilt = true;
            return Tensor<T>._CreateRaw(Shape, Strides, _items, 0);
        }
    }
}
