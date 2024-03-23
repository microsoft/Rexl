// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using Conditional = System.Diagnostics.ConditionalAttribute;
using SysArray = System.Array;

/// <summary>
/// Static class for extension methods related to <see cref="Immutable.Array{T}"/>.
/// </summary>
public static class ImmutableExtensions
{
    /// <summary>
    /// Create an immutable array containing the given items.
    /// </summary>
    public static Immutable.Array<T> ToImmutableArray<T>(this T[]? items)
    {
        return Immutable.Array<T>.Create(items);
    }

    /// <summary>
    /// Create an immutable array containing the given items.
    /// </summary>
    public static Immutable.Array<T> ToImmutableArray<T>(this List<T>? items)
    {
        return Immutable.Array<T>.Create(items);
    }

    /// <summary>
    /// Create an immutable array containing the given items.
    /// </summary>
    public static Immutable.Array<T> ToImmutableArray<T>(this IEnumerable<T>? items)
    {
        return Immutable.Array<T>.Create(items);
    }
}

/// <summary>
/// Contains nested types for immutable data structures and enables "sharing secrets" between them.
/// </summary>
public partial struct Immutable
{
    // To convert between Immutable.Array and Immutable.Array<T>, we need a way to transport
    // a "shared secret". That's what this field is. There is no way for outside code to create
    // a non-default instance of Immutable, so this works.
    private readonly SysArray? _secret;

    private Immutable(SysArray? secret)
    {
        _secret = secret;
    }

    /// <summary>
    /// Contains static generic helper methods for <see cref="Immutable.Array{T}"/>.
    /// Also, can hold the contents of an <see cref="Immutable.Array{T}"/> as a
    /// <see cref="System.Array"/>, for when static typing is not convenient.
    /// </summary>
    public readonly partial struct Array
    {
        /// <summary>
        /// Create a builder for the given item type.
        /// </summary>
        public static Array<T>.Builder CreateBuilder<T>()
        {
            return Array<T>.CreateBuilder();
        }

        /// <summary>
        /// Allocate a builder with the given capacity. If <paramref name="init"/> is true, sets the size
        /// to the given capacity.
        /// </summary>
        public static Array<T>.Builder CreateBuilder<T>(int capacity, bool init = false)
        {
            return Array<T>.CreateBuilder(capacity, init);
        }

        /// <summary>
        /// Create an immutable array containing the given item(s).
        /// </summary>
        public static Array<T> Create<T>(T a) => Array<T>.Create(a);
        public static Array<T> Create<T>(T a, T b) => Array<T>.Create(a, b);
        public static Array<T> Create<T>(T a, T b, T c) => Array<T>.Create(a, b, c);
        public static Array<T> Create<T>(T a, T b, T c, T d) => Array<T>.Create(a, b, c, d);
        public static Array<T> Create<T>(T a, T b, T c, T d, T e) => Array<T>.Create(a, b, c, d, e);
        public static Array<T> Create<T>(T a, T b, T c, T d, T e, T f) => Array<T>.Create(a, b, c, d, e, f);
        public static Array<T> Create<T>(T a, T b, T c, T d, T e, T f, T g) => Array<T>.Create(a, b, c, d, e, f, g);
        public static Array<T> Create<T>(T a, T b, T c, T d, T e, T f, T g, T h) => Array<T>.Create(a, b, c, d, e, f, g, h);

        /// <summary>
        /// Create an immutable array containing the given items.
        /// </summary>
        public static Array<T> Create<T>(params T[]? items)
        {
            return Array<T>.Create(items);
        }

        /// <summary>
        /// Creates an immutable array of the indicated length filled with the value value.
        /// </summary>
        public static Array<T> Fill<T>(T value, int count) => Array<T>.Fill(value, count);
    }

    public readonly partial struct Array : IEnumerable<object>
    {
        private readonly SysArray? _items;

        private Array(SysArray? items)
        {
            Validation.AssertValueOrNull(items);
            _items = items;
        }

        /// <summary>
        /// Intended for <see cref="Immutable"/>, not external clients, but this is still safe to
        /// be called by external code, since the only instance of <see cref="Immutable"/> that
        /// such code can get is a default instance, containing a null <see cref="SysArray"/>.
        /// </summary>
        internal Array(Immutable token)
        {
            Validation.AssertValueOrNull(token._secret);
            _items = token._secret;
        }

        /// <summary>
        /// Clone the given array and wrap the result as immutable.
        /// </summary>
        public static Array Create(SysArray? items)
        {
            if (items == null)
                return default;
            if (items.Length == 0)
                return new Array(items);

            // Unfortunately, we can't seize ownership of the array; we must clone. Note that we DO
            // want to maintain the actual item type, so shouldn't use the Clone function below.
            return new Array((SysArray)items.Clone());
        }

        /// <summary>
        /// Return whether this is in the default state.
        /// </summary>
        public bool IsDefault { get { return _items == null; } }

        /// <summary>
        /// Return whether this is in the default state or empty.
        /// </summary>
        public bool IsDefaultOrEmpty { get { return _items == null || _items.Length == 0; } }

        /// <summary>
        /// Returns the length. Unlike the standard, this doesn't throw if we're in the default state.
        /// </summary>
        public int Length
        {
            get { return _items == null ? 0 : _items.Length; }
        }

        public object? this[int index]
        {
            get
            {
                Validation.AssertIndex(index, Length);
                return _items!.GetValue(index);
            }
        }

        /// <summary>
        /// Try to cast the wrapped array to an array of <typeparamref name="T"/>.
        /// If possible, wrap as an <see cref="Immutable.Array{T}"/> and return true.
        /// </summary>
        public bool TryCast<T>(out Array<T> result)
        {
            return Array<T>.TryCast(new Immutable(_items), out result);
        }

        public static implicit operator ReadOnly.Array(Array value)
        {
            return new ReadOnly.Array(value._items);
        }

        public IEnumerator<object> GetEnumerator()
        {
            if (_items != null)
            {
                foreach (var item in _items)
                    yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// This mimics the standard Immutable.Array. Reasons for our own implementation include:
    /// * So we can have a non-generic form that wraps a <see cref="System.Array"/>, still guarantees
    ///   immutability and can interoperate with the generic form.
    /// * To enable "casting" (similar to array covariance).
    /// * Better performance in the case when the size is known a priori (using the init = true form).
    /// * <see cref="Builder.ToImmutable"/> transfers ownership of the storage when beneficial. To
    ///   maintain contents of the builder, we use <see cref="Builder.ToImmutableCopy"/>. This is the
    ///   reverse of the system form where ToImmutable maintains the builder and MoveToImmutable transfers
    ///   ownership (and also _requires_ that the capacity matches the size).
    /// 
    /// REVIEW: Should implement an enumerator that doesn't require object allocation.
    /// </summary>
    public readonly partial struct Array<T> : IEnumerable<T>
    {
        private static readonly T[] s_empty = SysArray.Empty<T>();

        private readonly T[]? _items;

        public static readonly Array<T> Empty = new Array<T>(s_empty);

        private Array(T[]? items)
        {
            Validation.AssertValue(items);
            _items = items;
        }

        /// <summary>
        /// Create a builder for the given item type.
        /// </summary>
        public static Array<T>.Builder CreateBuilder()
        {
            return Builder.Create();
        }

        /// <summary>
        /// Allocate a builder with the given capacity. If <paramref name="init"/> is true, sets the size
        /// to the given capacity.
        /// </summary>
        public static Array<T>.Builder CreateBuilder(int capacity, bool init = false)
        {
            return Builder.Create(capacity, init);
        }

        public static Array<T> Create(T a) => new Array<T>(new T[] { a });
        public static Array<T> Create(T a, T b) => new Array<T>(new T[] { a, b });
        public static Array<T> Create(T a, T b, T c) => new Array<T>(new T[] { a, b, c });
        public static Array<T> Create(T a, T b, T c, T d) => new Array<T>(new T[] { a, b, c, d });
        public static Array<T> Create(T a, T b, T c, T d, T e) => new Array<T>(new T[] { a, b, c, d, e });
        public static Array<T> Create(T a, T b, T c, T d, T e, T f) => new Array<T>(new T[] { a, b, c, d, e, f });
        public static Array<T> Create(T a, T b, T c, T d, T e, T f, T g) => new Array<T>(new T[] { a, b, c, d, e, f, g });
        public static Array<T> Create(T a, T b, T c, T d, T e, T f, T g, T h) => new Array<T>(new T[] { a, b, c, d, e, f, g, h });

        public static Array<T> Create(params T[]? items)
        {
            if (items == null || items.Length == 0)
                return Empty;

            // Unfortunately, we can't seize ownership of the array; we must clone. Note that we DO
            // want to maintain the actual item type, so shouldn't use the Clone function below.
            return new Array<T>((T[])items.Clone());
        }

        public static Array<T> Create(List<T>? items)
        {
            if (items == null || items.Count == 0)
                return Empty;

            return new Array<T>(items.ToArray());
        }

        public static Array<T> Create(IEnumerable<T>? items)
        {
            if (items == null)
                return Empty;

            // REVIEW: Should we forbid this? Should we use ToImmutableCopy?
            if (items is Immutable.Array<T>.Builder bldr)
                return bldr.ToImmutable();

            var arr = items.ToArray();
            if (arr.Length == 0)
                return Empty;

            return new Array<T>(arr);
        }

        /// <summary>
        /// Creates an immutable array of the indicated length filled with the value value.
        /// </summary>
        public static Array<T> Fill(T value, int count)
        {
            Validation.BugCheckParam(count >= 0, nameof(count));
            if (count == 0)
                return Empty;
            var items = new T[count];
            SysArray.Fill(items, value);
            return new Array<T>(items);
        }

        public static Array<T> Cast<TSrc>(Array<TSrc> arr)
            where TSrc : class, T
        {
            return new Array<T>(arr._items);
        }

        /// <summary>
        /// Try to cast the wrapped array to an array of <typeparamref name="T"/>.
        /// If possible, wrap as an <see cref="Immutable.Array{T}"/> and return true.
        /// </summary>
        internal static bool TryCast(Immutable value, out Array<T> result)
        {
            if (value._secret is T[] items)
            {
                result = new Array<T>(items);
                return true;
            }

            // If value._secret is null, return true.
            result = default;
            return value._secret == null;
        }

        /// <summary>
        /// Try to cast the wrapped array to an array of <typeparamref name="TNew"/>.
        /// If possible, wrap as an <see cref="Immutable.Array{TNew}"/> and return true.
        /// </summary>
        public bool TryCast<TNew>(out Array<TNew> result)
        {
            if (_items is TNew[] items)
            {
                result = new Array<TNew>(items);
                return true;
            }

            // If _items is null, return true.
            result = default;
            return _items == null;
        }

        /// <summary>
        /// Return whether this is in the default state.
        /// </summary>
        public bool IsDefault { get { return _items == null; } }

        /// <summary>
        /// Return whether this is in the default state or empty.
        /// </summary>
        public bool IsDefaultOrEmpty { get { return _items == null || _items.Length == 0; } }

        /// <summary>
        /// Returns the length. Unlike the standard, this doesn't throw if we're in the default state.
        /// </summary>
        public int Length
        {
            get { return _items == null ? 0 : _items.Length; }
        }

        public T this[int index]
        {
            get
            {
                Validation.AssertIndex(index, Length);
                return _items![index];
            }
        }

        /// <summary>
        /// This is more efficient than using the Linq one. Note that this does NOT preserve the actual
        /// underlying array item type. It produces an array of <typeparamref name="T"/>, not an array
        /// of some <c>U</c> that derives from <typeparamref name="T"/>. This is so it is safe to assign
        /// values of type <typeparamref name="T"/> into slots of the result.
        /// </summary>
        public T[]? ToArray()
        {
            if (_items == null)
                return null;
            if (_items.Length == 0)
                return s_empty;
            return Clone(_items);
        }

        /// <summary>
        /// If this is default or the index is out of bounds, returns default. Otherwise returns the
        /// indicated item.
        /// </summary>
        [return: MaybeNull]
        public T GetItemOrDefault(int index)
        {
            if (!Validation.IsValidIndex(index, Length))
                return default;
            return _items![index];
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> wrapping the items.
        /// </summary>
        public ReadOnlySpan<T> AsSpan()
        {
            return _items;
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> wrapping the indicated range of items.
        /// </summary>
        public ReadOnlySpan<T> AsSpan(int start, int count)
        {
            return new ReadOnlySpan<T>(_items, start, count);
        }

        /// <summary>
        /// Implicit conversion to <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        public static implicit operator ReadOnlySpan<T>(Array<T> value)
        {
            return value._items;
        }

        /// <summary>
        /// Implicit conversion to non-generic form.
        /// </summary>
        public static implicit operator Array(Array<T> value)
        {
            return new Array(new Immutable(value._items));
        }

        /// <summary>
        /// Implicit conversion to <see cref="ReadOnly.Array"/>.
        /// </summary>
        public static implicit operator ReadOnly.Array(Array<T> value)
        {
            return new ReadOnly.Array(value._items);
        }

        /// <summary>
        /// Implicit conversion to <see cref="ReadOnly.Array{T}"/>.
        /// </summary>
        public static implicit operator ReadOnly.Array<T>(Array<T> value)
        {
            return new ReadOnly.Array<T>(value._items);
        }

        /// <summary>
        /// Returns whether this array is identical to the other, meaning that they
        /// reference the exact same underlying memory. When this returns false, the
        /// two may still be semantically equivalent.
        /// </summary>
        public bool AreIdentical(Array<T> other)
        {
            return _items == other._items;
        }

        /// <summary>
        /// Return a new instance with the given item appended.
        /// </summary>
        public Array<T> Add(T item)
        {
            int index = Length;
            var items = _items;
            SysArray.Resize(ref items, index + 1);
            items[index] = item;
            return new Array<T>(items);
        }

        /// <summary>
        /// Return a new instance with the given items appended.
        /// </summary>
        public Array<T> AddRange(Array<T> items)
        {
            if (items.IsDefaultOrEmpty)
                return this;
            if (IsDefaultOrEmpty)
                return items;
            var its = _items;
            SysArray.Resize(ref its, Length + items.Length);
            SysArray.Copy(items._items!, 0, its, Length, items.Length);
            return new Array<T>(its);
        }

        /// <summary>
        /// Return a new instance with the given items appended.
        /// </summary>
        public Array<T> AddRange<TSrc>(Array<TSrc> items)
            where TSrc : class, T
        {
            if (items.IsDefaultOrEmpty)
                return this;
            if (IsDefaultOrEmpty)
                return new Array<T>(items._items);
            var its = _items;
            SysArray.Resize(ref its, Length + items.Length);
            SysArray.Copy(items._items!, 0, its, Length, items.Length);
            return new Array<T>(its);
        }

        /// <summary>
        /// Return a new instance with the indicated item replaced.
        /// </summary>
        public Array<T> SetItem(int index, T item)
        {
            int len = Length;
            Validation.AssertIndex(index, len);

            var items = new T[len];
            if (len > 1)
                SysArray.Copy(_items!, items, items.Length);
            items[index] = item;
            return new Array<T>(items);
        }

        /// <summary>
        /// Return a new instance with the indicated item inserted.
        /// </summary>
        public Array<T> Insert(int index, T item)
        {
            int len = Length;
            Validation.AssertIndexInclusive(index, len);

            T[] items = new T[len + 1];
            if (index > 0)
                SysArray.Copy(_items!, 0, items, 0, index);
            items[index] = item;
            if (index < len)
                SysArray.Copy(_items!, index, items, index + 1, len - index);
            return new Array<T>(items);
        }

        /// <summary>
        /// Return a new instance with the indicated item removed.
        /// </summary>
        public Array<T> RemoveAt(int index)
        {
            int len = Length;
            Validation.AssertIndex(index, len);

            T[] items;
            if (index == len - 1)
            {
                if (len == 1)
                    return Empty;
                items = _items!;
                SysArray.Resize(ref items, index);
            }
            else
            {
                Validation.Assert(len > 1);
                items = new T[len - 1];
                if (index > 0)
                    SysArray.Copy(_items!, items, index);
                SysArray.Copy(_items!, index + 1, items, index, items.Length - index);
            }
            return new Array<T>(items);
        }

        /// <summary>
        /// Return a new instance with the indicated items removed.
        /// </summary>
        public Array<T> RemoveMinLim(int indexMin, int indexLim)
        {
            int len = Length;
            Validation.AssertIndexInclusive(indexLim, len);
            Validation.AssertIndexInclusive(indexMin, indexLim);

            if (indexMin >= indexLim)
                return this;
            if (indexMin <= 0 && len <= indexLim)
                return Empty;

            T[] items;
            if (indexLim == len)
            {
                items = _items!;
                SysArray.Resize(ref items, indexMin);
            }
            else
            {
                Validation.Assert(len > 1);
                int cvDel = indexLim - indexMin;
                items = new T[len - cvDel];
                if (indexMin > 0)
                    SysArray.Copy(_items!, items, indexMin);
                SysArray.Copy(_items!, indexLim, items, indexMin, items.Length - indexMin);
            }
            return new Array<T>(items);
        }

        /// <summary>
        /// Return a new instance with the tail items removed, that is, from index onward.
        /// </summary>
        public Array<T> RemoveTail(int index)
        {
            return RemoveMinLim(index, Length);
        }

        /// <summary>
        /// Return a new instance with the items within the indicated indices.
        /// </summary>
        public Array<T> GetMinLim(int indexMin, int indexLim)
        {
            Validation.AssertIndexInclusive(indexLim, Length);
            Validation.AssertIndexInclusive(indexMin, indexLim);

            if (indexMin >= indexLim)
                return Empty;
            if (indexMin <= 0 && indexLim >= Length)
                return this;
            Validation.Assert(_items != null);
            Validation.Assert(_items.Length >= indexLim);

            var arr = new T[indexLim - indexMin];
            SysArray.Copy(_items, indexMin, arr, 0, arr.Length);
            return new Array<T>(arr);
        }

        /// <summary>
        /// Return a new instance with the items within the indicated range.
        /// </summary>
        public Array<T> GetRange(int index, int count)
        {
            return GetMinLim(index, index + count);
        }

        /// <summary>
        /// Return a new instance with the items reversed.
        /// </summary>
        public Array<T> Reverse()
        {
            if (Length < 2)
                return this;
            var arr = Clone(_items!);
            SysArray.Reverse(arr);
            return new Array<T>(arr);
        }

        /// <summary>
        /// Get the index of <paramref name="item"/> in the array.
        /// </summary>
        public int IndexOf(T item)
        {
            if (_items == null)
                return -1;
            return System.Array.IndexOf(_items, item, 0);
        }

        /// <summary>
        /// Create a builder initialized to contain the indicated items.
        /// </summary>
        public Builder ToBuilder()
        {
            return Builder.Create(this);
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_items != null)
            {
                foreach (T item in _items)
                    yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Clone the given array. Note that this does NOT preserve the item type when the source
        /// item type is a sub-type of <typeparamref name="T"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T[] Clone(T[] items)
        {
            Validation.Assert(items.Length > 0);
            var res = new T[items.Length];
            SysArray.Copy(items, res, items.Length);
            return res;
        }
    }

    partial struct Array<T>
    {
        /// <summary>
        /// Class for building an <see cref="Immutable.Array{T}"/>.
        /// </summary>
        public sealed class Builder : IEnumerable<T>
        {
            /// <summary>
            /// The items as an array.
            /// </summary>
            private T[] _items;

            /// <summary>
            /// The number of "active" items in the array.
            /// </summary>
            private int _count;

            private Builder()
                : this(8)
            {
            }

            private Builder(int capacity, bool init = false)
            {
                Validation.Assert(capacity >= 0);
                _items = capacity > 0 ? new T[capacity] : s_empty;
                _count = init ? capacity : 0;
                AssertValid();
            }

            private Builder(T[] itemsShared)
            {
                Validation.AssertValue(itemsShared);
                _count = itemsShared.Length;
                _items = _count > 0 ? Clone(itemsShared) : s_empty;
                AssertValid();
            }

            /// <summary>
            /// Create a new builder with a default capacity.
            /// </summary>
            public static Builder Create()
            {
                return new Builder();
            }

            /// <summary>
            /// Create a new builder with the given capacity. If <paramref name="init"/> is <c>true</c>, this
            /// sets the <see cref="Count"/> to the capacity.
            /// </summary>
            public static Builder Create(int capacity, bool init)
            {
                Validation.BugCheckParam(capacity >= 0, nameof(capacity));
                return new Builder(capacity, init);
            }

            /// <summary>
            /// Create a new builder with a copy of the contents of <paramref name="arr"/>.
            /// </summary>
            public static Builder Create(Array<T> arr)
            {
                return new Builder(arr._items ?? s_empty);
            }

            [Conditional("DEBUG")]
            private void AssertValid()
            {
                Validation.Assert(_count <= _items.Length);
            }

            /// <summary>
            /// The number of items in the builder.
            /// </summary>
            public int Count => _count;

            public T this[int index]
            {
                get
                {
                    AssertValid();
                    Validation.AssertIndex(index, _count);
                    return _items[index];
                }

                set
                {
                    AssertValid();
                    Validation.AssertIndex(index, _count);
                    _items[index] = value;
                }
            }

            /// <summary>
            /// This is illegal - use <see cref="ToImmutable"/>.
            /// </summary>
            public void ToImmutableArray() => throw new InvalidOperationException();

            /// <summary>
            /// Creates an immutable array from the current contents and resets the builder
            /// to be empty. Use <see cref="ToImmutableCopy"/> if the contents of the builder
            /// should be preserved. This may reduce the capacity to zero, depending on the
            /// situation.
            /// </summary>
            public Array<T> ToImmutable()
            {
                AssertValid();

                if (_count == 0)
                    return new Array<T>(s_empty);

                T[] items = _items;
                if (_count != _items.Length)
                    SysArray.Resize(ref items, _count);
                else
                    _items = s_empty;
                _count = 0;
                return new Array<T>(items);
            }

            /// <summary>
            /// Create an immutable array but preserve the contents of this builder.
            /// </summary>
            public Array<T> ToImmutableCopy()
            {
                AssertValid();
                if (_count == 0)
                    return new Array<T>(s_empty);

                T[] items = _items;
                if (_count != _items.Length)
                    SysArray.Resize(ref items, _count);
                else
                    items = Clone(items);

                return new Array<T>(items);
            }

            /// <summary>
            /// Clear this builder to have zero items.
            /// </summary>
            public void Clear()
            {
                AssertValid();
                _count = 0;
            }

            /// <summary>
            /// Expands the number of items. Returns the original count, not
            /// the new count.
            /// </summary>
            private void EnsureCapacity(uint countNew)
            {
                Validation.Assert(countNew > (uint)_count);

                if (countNew > (uint)_items.Length)
                {
                    int cap = (int)Math.Max(countNew, (uint)_items.Length << 1);
                    SysArray.Resize(ref _items, cap);
                }
            }

            /// <summary>
            /// Add an item to the end.
            /// </summary>
            public void Add(T item)
            {
                AssertValid();

                uint countNew = (uint)_count + 1;
                EnsureCapacity(countNew);
                _items[_count] = item;
                _count = (int)countNew;
                AssertValid();
            }

            /// <summary>
            /// Insert an item at the given <paramref name="index"/>.
            /// </summary>
            public void Insert(int index, T item)
            {
                AssertValid();
                Validation.AssertIndexInclusive(index, _count);

                uint countNew = (uint)_count + 1;
                EnsureCapacity(countNew);
                if (index < _count)
                    SysArray.Copy(_items, index, _items, index + 1, _count - index);
                _items[index] = item;
                _count = (int)countNew;
                AssertValid();
            }

            /// <summary>
            /// Add an array of items to the end.
            /// </summary>
            public void AddRange(Array<T> items)
            {
                AssertValid();

                int delta = items.Length;
                if (delta > 0)
                {
                    uint countNew = (uint)_count + (uint)delta;
                    EnsureCapacity(countNew);
                    SysArray.Copy(items._items!, 0, _items, _count, delta);
                    _count = (int)countNew;
                }

                AssertValid();
            }

            /// <summary>
            /// Add an array of items to the end.
            /// </summary>
            public void AddRange(ReadOnlySpan<T> items)
            {
                AssertValid();

                int delta = items.Length;
                if (delta > 0)
                {
                    uint countNew = (uint)_count + (uint)delta;
                    EnsureCapacity(countNew);
                    items.CopyTo(_items.AsSpan(_count, delta));
                    _count = (int)countNew;
                }

                AssertValid();
            }

            /// <summary>
            /// Add an array of items to the end.
            /// </summary>
            public void AddRange(IReadOnlyList<T> items)
            {
                AssertValid();

                int delta = items.Count;
                if (delta > 0)
                {
                    uint countNew = (uint)_count + (uint)delta;
                    EnsureCapacity(countNew);
                    int index = _count;
                    for (int i = 0; i < delta; i++)
                        _items[index + i] = items[i];
                    _count = (int)countNew;
                }

                AssertValid();
            }

            /// <summary>
            /// Insert an array of items at the given <paramref name="index"/>.
            /// </summary>
            public void InsertRange(int index, Array<T> items)
            {
                AssertValid();
                Validation.AssertIndexInclusive(index, _count);

                int delta = items.Length;
                if (delta > 0)
                {
                    uint countNew = (uint)_count + (uint)delta;
                    EnsureCapacity(countNew);
                    if (index < _count)
                        SysArray.Copy(_items, index, _items, index + delta, _count - index);
                    SysArray.Copy(items._items!, 0, _items, index, delta);
                    _count = (int)countNew;
                }

                AssertValid();
            }

            /// <summary>
            /// Insert an array of items at the given <paramref name="index"/>.
            /// </summary>
            public void InsertRange(int index, ReadOnlySpan<T> items)
            {
                AssertValid();
                Validation.AssertIndexInclusive(index, _count);

                int delta = items.Length;
                if (delta > 0)
                {
                    uint countNew = (uint)_count + (uint)delta;
                    EnsureCapacity(countNew);
                    if (index < _count)
                        SysArray.Copy(_items, index, _items, index + delta, _count - index);
                    items.CopyTo(_items.AsSpan(index, delta));
                    _count = (int)countNew;
                }

                AssertValid();
            }

            /// <summary>
            /// Set the indicated range of items to the given <paramref name="value"/>.
            /// </summary>
            public void SetMinLim(int indexMin, int indexLim, T value)
            {
                AssertValid();
                Validation.AssertIndexInclusive(indexLim, _count);
                Validation.AssertIndexInclusive(indexMin, indexLim);

                if (indexMin >= indexLim)
                    return;

                for (int i = indexMin; i < indexLim; i++)
                    _items[i] = value;

                AssertValid();
            }

            /// <summary>
            /// Remove the item at the given <paramref name="index"/>.
            /// </summary>
            public void RemoveAt(int index)
            {
                AssertValid();
                Validation.AssertIndex(index, _count);

                if (index < --_count)
                    SysArray.Copy(_items, index + 1, _items, index, _count - index);

                AssertValid();
            }

            /// <summary>
            /// Remove the indicated range of indices.
            /// </summary>
            public void RemoveMinLim(int indexMin, int indexLim)
            {
                AssertValid();
                Validation.AssertIndexInclusive(indexLim, _count);
                Validation.AssertIndexInclusive(indexMin, indexLim);

                int num = indexLim - indexMin;
                if (num <= 0)
                    return;

                if (indexMin <= 0 && _count <= indexLim)
                {
                    _count = 0;
                    return;
                }

                _count -= num;
                if (indexMin < _count)
                    SysArray.Copy(_items, indexLim, _items, indexMin, _count - indexMin);

                AssertValid();
            }

            /// <summary>
            /// Remove items at and beyond the indicated index.
            /// </summary>
            public void RemoveTail(int index)
            {
                AssertValid();
                Validation.AssertIndexInclusive(index, _count);

                if (index <= 0)
                {
                    _count = 0;
                    return;
                }

                if (_count > index)
                    _count = index;

                AssertValid();
            }

            /// <summary>
            /// Reverses the items.
            /// </summary>
            public void Reverse()
            {
                AssertValid();

                if (_count <= 1)
                    return;

                for (int a = 0, b = _count - 1; a < b; a++, b--)
                {
                    T tmp = _items[a];
                    _items[a] = _items[b];
                    _items[b] = tmp;
                }
            }

            /// <summary>
            /// Sorts the items in place with QuadSort, which has quadratic complexity
            /// but is stable.
            /// </summary>
            public void QuadSort(Comparison<T> cmp)
            {
                AssertValid();

                if (_count <= 1)
                    return;

                Sorting.QuadSort(_items, 0, _count - 1, cmp);
            }

            /// <summary>
            /// Sorts the items in place using a potentially non-stable sort.
            /// </summary>
            public void Sort(Comparison<T> cmp)
            {
                AssertValid();

                if (_count <= 1)
                    return;

                _items.AsSpan(0, _count).Sort(cmp);
            }

            public IEnumerator<T> GetEnumerator()
            {
                AssertValid();
                for (int i = 0; i < _count; i++)
                    yield return _items[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}