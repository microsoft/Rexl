// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using SysArray = System.Array;

public struct ReadOnly
{
    /// <summary>
    /// Wraps a <see cref="System.Array"/> in a way that enforces no mutation via this wrapper.
    /// Unlike <see cref="Immutable.Array{T}"/>, it does not guarantee that the array items will never change.
    /// </summary>
    public readonly struct Array : IEnumerable<object?>
    {
        private readonly SysArray? _items;

        public Array(SysArray? items)
        {
            Validation.AssertValueOrNull(items);
            _items = items;
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

        /// <summary>
        /// Returns the item at the indicated index.
        /// </summary>
        public object? this[int index]
        {
            get
            {
                Validation.BugCheckIndex(index, Length, nameof(index));
                return _items!.GetValue(index);
            }
        }

        public static implicit operator Array(SysArray? items)
        {
            return new ReadOnly.Array(items);
        }

        /// <summary>
        /// Try to cast the wrapped array to an array of <typeparamref name="T"/>.
        /// If possible, wrap as an <see cref="ReadOnly.Array{T}"/> and return true.
        /// </summary>
        public bool TryCast<T>(out Array<T> result)
        {
            if (_items is T[] items)
            {
                result = new Array<T>(items);
                return true;
            }

            // If _items is null, return true.
            result = default;
            return _items == null;
        }

        public IEnumerator<object?> GetEnumerator()
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
    /// Wraps a strongly typed array in a way that enforces no mutation via this wrapper.
    /// Unlike <see cref="Immutable.Array{T}"/>, it does not guarantee that the array items will never change.
    /// </summary>
    public readonly struct Array<T> : IEnumerable<T>
    {
        private readonly T[]? _items;

        public Array(T[]? items)
        {
            Validation.AssertValueOrNull(items);
            _items = items;
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

        /// <summary>
        /// Returns the item at the indicated index.
        /// </summary>
        public T this[int index]
        {
            get
            {
                Validation.BugCheckIndex(index, Length, nameof(index));
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
                return SysArray.Empty<T>();
            var dst = new T[_items.Length];
            SysArray.Copy(_items, dst, dst.Length);
            return dst;
        }

        /// <summary>
        /// If this is default, returns default. Otherwise returns the indicated item.
        /// </summary>
        public T? GetItemOrDefault(int index)
        {
            if (IsDefault)
                return default;
            Validation.BugCheckIndex(index, Length, nameof(index));
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
        /// Implicit conversion from system arry to <see cref="ReadOnly.Array{T}"/>.
        /// </summary>
        public static implicit operator Array<T>(T[]? items)
        {
            return new ReadOnly.Array<T>(items);
        }

        /// <summary>
        /// Implicit conversion to non-generic form.
        /// </summary>
        public static implicit operator Array(Array<T> value)
        {
            return new ReadOnly.Array(value._items);
        }

        /// <summary>
        /// Try to cast the wrapped array to an array of <typeparamref name="TNew"/>.
        /// If possible, wrap as an <see cref="ReadOnly.Array{TNew}"/> and return true.
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
        /// Copies the contents of this array to the given system array
        /// at the given indices and length.
        /// </summary>
        public void Copy(int minSrc, int limSrc, T[] dst, int minDst)
        {
            Validation.BugCheckIndexInclusive(limSrc, Length, nameof(limSrc));
            Validation.BugCheckIndexInclusive(minSrc, limSrc, nameof(minSrc));
            Validation.BugCheckIndexInclusive(minDst, dst.Length, nameof(minDst));
            Validation.BugCheckIndexInclusive(minDst + limSrc - minSrc, dst.Length, nameof(dst));
            int count = limSrc - minSrc;
            if (count > 0)
                SysArray.Copy(_items!, minSrc, dst, minDst, count);
        }

        /// <summary>
        /// Create an immutable array containing the given items.
        /// </summary>
        public Immutable.Array<T> ToImmutableArray()
        {
            return Immutable.Array<T>.Create(_items);
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
    }

    /// <summary>
    /// Wraps a dictionary in a way that enforces no mutation via this wrapper. It does not guarantee
    /// that the dictionary items will never change through other means (doesn't guarantee immutability).
    /// Note that, like the Array types, this DOES distinguish between "default" and "empty".
    /// </summary>
    public readonly struct Dictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        // Normalize empty dictionaries, so only a single instance is kept around.
        private static readonly System.Collections.Generic.Dictionary<TKey, TValue> _empty =
            new System.Collections.Generic.Dictionary<TKey, TValue>(0);

        // The items. Null indicates this is in the "default" state.
        private readonly System.Collections.Generic.Dictionary<TKey, TValue>? _items;

        public Dictionary(System.Collections.Generic.Dictionary<TKey, TValue>? items)
        {
            Validation.AssertValueOrNull(items);
            if (items != null && items.Count == 0)
                items = _empty;
            _items = items;
        }

        public static implicit operator Dictionary<TKey, TValue>(
            System.Collections.Generic.Dictionary<TKey, TValue>? items)
        {
            return new ReadOnly.Dictionary<TKey, TValue>(items);
        }

        /// <summary>
        /// Return whether this is in the default state.
        /// </summary>
        public bool IsDefault { get { return _items == null; } }

        /// <summary>
        /// Return whether this is in the default state or empty.
        /// </summary>
        public bool IsDefaultOrEmpty { get { return _items == null || _items.Count == 0; } }

        /// <summary>
        /// Returns the number of items. This returns zero for the default state.
        /// </summary>
        public int Count
        {
            get { return _items == null ? 0 : _items.Count; }
        }

        public IEnumerable<TKey> Keys => (_items ?? _empty).Keys;

        public IEnumerable<TValue> Values => (_items ?? _empty).Values;

        /// <summary>
        /// Returns the item at the indicated key.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                if (_items != null && key != null && _items.TryGetValue(key, out var value))
                    return value;
                throw Validation.BugExceptParam(nameof(key), "Invalid key");
            }
        }

        /// <summary>
        /// If this is default or the key is not in the dictionary, returns default. Otherwise returns the associated value.
        /// </summary>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (_items != null && key != null)
                return _items.TryGetValue(key, out value);
            value = default;
            return false;
        }

        /// <summary>
        /// If this is default or the key is not in the dictionary, returns default. Otherwise returns the associated value.
        /// </summary>
        [return: MaybeNull]
        public TValue GetValueOrDefault(TKey key)
        {
            if (_items != null && key != null && _items.TryGetValue(key, out var value))
                return value;
            return default;
        }

        /// <summary>
        /// Returns whether the key is in the dictionary.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return _items != null && key != null && _items.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return (_items ?? _empty).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Wraps a hash set in a way that enforces no mutation via this wrapper. It does not guarantee
    /// that the hash set items will never change through other means (doesn't guarantee immutability).
    /// Note that, like the Array types, this DOES distinguish between "default" and "empty".
    /// </summary>
    public readonly struct HashSet<T> : IEnumerable<T>
    {
        // Normalize empty hash sets, so only a single instance is kept around.
        private static readonly System.Collections.Generic.HashSet<T> _empty =
            new System.Collections.Generic.HashSet<T>();

        // The items. Null indicates this is in the "default" state.
        private readonly System.Collections.Generic.HashSet<T>? _items;

        public HashSet(System.Collections.Generic.HashSet<T>? items)
        {
            Validation.AssertValueOrNull(items);
            if (items != null && items.Count == 0)
                items = _empty;
            _items = items;
        }

        public static implicit operator HashSet<T>(System.Collections.Generic.HashSet<T>? items)
        {
            return new ReadOnly.HashSet<T>(items);
        }

        /// <summary>
        /// Return whether this is in the default state.
        /// </summary>
        public bool IsDefault { get { return _items == null; } }

        /// <summary>
        /// Return whether this is in the default state or empty.
        /// </summary>
        public bool IsDefaultOrEmpty { get { return _items == null || _items.Count == 0; } }

        /// <summary>
        /// Returns the number of items. This returns zero for the default state.
        /// </summary>
        public int Count
        {
            get { return _items == null ? 0 : _items.Count; }
        }

        /// <summary>
        /// Returns whether the item is in the hash set.
        /// </summary>
        public bool Contains(T item)
        {
            return _items != null && item != null && _items.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (_items ?? _empty).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
