// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

partial class Tensor
{
    /// <summary>
    /// A buffer wrapping underlying storage for a tensor. The storage is immutable, so can be shared
    /// between tensors. This facilities slicing and reshaping.
    /// </summary>
    internal abstract class Buffer<T>
    {
        /// <summary>
        /// The threshold below which to eagerly map the underlying storage, rather than lazily map.
        /// REVIEW: What should we use for the threshold?
        /// </summary>
        public const long EagerThreshold = 32;

        /// <summary>
        /// An empty buffer.
        /// </summary>
        public static readonly Buffer<T> Empty = new ArrayImpl(Array.Empty<T>());

        private Buffer()
        {
        }

        /// <summary>
        /// Returns the length of the underlying storage.
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Returns the item at the indicated index.
        /// </summary>
        public abstract T this[long index] { get; }

        /// <summary>
        /// Implicit conversion from array to buffer.
        /// 
        /// REVIEW: Remove this and replace it with a Buffer builder.
        /// </summary>
        public static implicit operator Buffer<T>(T[] items) => new ArrayImpl(items);

        /// <summary>
        /// Map this buffer through an element-wise mapping function. If <paramref name="realize"/> is
        /// true, forces a fully realized (array based) buffer. Otherwise, may wrap in a lazy mapper.
        /// </summary>
        public Buffer<U> Map<U>(Func<T, U> map, bool realize = false)
        {
            Validation.AssertValue(map);

            var len = Length;
            switch (len)
            {
            case 0: return Buffer<U>.Empty;
            case 1: return new U[] { map(this[0]) };
            }

            if (len <= EagerThreshold || realize)
            {
                var dst = new U[len];
                for (int i = 0; i < len; i++)
                    dst[i] = map(this[i]);
                return dst;
            }

            return new Mapper<U>(this, map);
        }

        /// <summary>
        /// Try to get a span on the buffer. Note that this isn't always possible, since not all buffers
        /// are backed by contiguous memory. In particular, some buffers apply a map function to items,
        /// while others may be larger than 2^31 - 1 or have data spanning multiple physical blocks.
        /// </summary>
        public abstract bool TryGetSpan(out ReadOnlySpan<T> span);

        /// <summary>
        /// Try to get a Memory on the buffer. Note that this isn't always possible, since not all buffers
        /// are backed by contiguous memory. In particular, some buffers apply a map function to items,
        /// while others may be larger than 2^31 - 1 or have data spanning multiple physical blocks.
        /// </summary>
        public abstract bool TryGetMemory(out ReadOnlyMemory<T> mem);

        /// <summary>
        /// This implementation wraps a standard array.
        /// </summary>
        private sealed class ArrayImpl : Buffer<T>
        {
            private readonly T[] _items;

            public ArrayImpl(T[] items)
                : base()
            {
                Validation.AssertValue(items);
                _items = items;
            }

            public override long Length => _items.LongLength;

            public override T this[long index]
            {
                get
                {
                    Validation.AssertIndex(index, Length);
                    return _items.VerifyValue()[index];
                }
            }

            public override bool TryGetSpan(out ReadOnlySpan<T> span)
            {
                if (_items.LongLength <= int.MaxValue)
                {
                    span = _items;
                    return true;
                }

                span = default;
                return false;
            }

            public override bool TryGetMemory(out ReadOnlyMemory<T> mem)
            {
                if (_items.LongLength <= int.MaxValue)
                {
                    mem = _items;
                    return true;
                }

                mem = default;
                return false;
            }
        }

        /// <summary>
        /// This implementation wraps a source buffer and an element-wise mapping function.
        /// </summary>
        private sealed class Mapper<U> : Buffer<U>
        {
            private readonly Buffer<T> _src;
            private readonly Func<T, U> _map;

            public Mapper(Buffer<T> src, Func<T, U> map)
                : base()
            {
                Validation.AssertValue(src);
                Validation.AssertValue(map);
                _src = src;
                _map = map;
            }

            public override long Length => _src.Length;

            public override U this[long index] => _map(_src[index]);

            public override bool TryGetSpan(out ReadOnlySpan<U> span)
            {
                span = default;
                return false;
            }

            public override bool TryGetMemory(out ReadOnlyMemory<U> mem)
            {
                mem = default;
                return false;
            }
        }
    }
}
