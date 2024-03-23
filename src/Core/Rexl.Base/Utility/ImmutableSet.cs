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

public partial struct Immutable
{
    /// <summary>
    /// An immutable set, backed by a red-black tree.
    /// </summary>
    public readonly struct Set<TKey> : IEnumerable<TKey>, IEquatable<Set<TKey>>
        where TKey : IComparable<TKey>
    {
        /// <summary>
        /// The tree of items as a red-black-tree.
        /// </summary>
        private sealed class ItemTree : RedBlackTree<ItemTree, TKey, bool>
        {
            public static readonly ItemTree Empty = new ItemTree(null);

            private ItemTree(Node? root)
                : base(root)
            {
            }

            protected override int KeyCompare(TKey key0, TKey key1)
            {
                return key0.CompareTo(key1);
            }

            protected override bool KeyEquals(TKey key0, TKey key1)
            {
                return key0.Equals(key1);
            }

            protected override int KeyHash(TKey key)
            {
                return key.GetHashCode();
            }

            protected override bool KeyIsValid(TKey key)
            {
                return key != null;
            }

            protected override bool ValEquals(bool val0, bool val1)
            {
                return val0 == val1;
            }

            protected override int ValHash(bool val)
            {
                return val.GetHashCode();
            }

            protected override bool ValIsValid(bool val)
            {
                // All the values should be true. This is a "set", not "dictionary".
                return val == true;
            }

            protected override ItemTree Wrap(Node? root)
            {
                return root == _root ? this : root != null ? new ItemTree(root) : Empty;
            }
        }

        public static Set<TKey> Empty => default;

        /// <summary>
        /// The item tree, normalized to be null when the set is empty.
        /// </summary>
        private readonly ItemTree? _items;

        /// <summary>
        /// The item tree, always non-null.
        /// </summary>
        private ItemTree Items => _items ?? ItemTree.Empty;

        /// <summary>
        /// Return whether this is empty.
        /// </summary>
        public bool IsEmpty => _items == null;

        /// <summary>
        /// The number of items in this TKey set.
        /// </summary>
        public int Count => _items != null ? _items.Count : 0;

        /// <summary>
        /// Gets the first item in the set, or default if the set is empty.
        /// </summary>
        [MaybeNull]
        public TKey First => _items != null ? _items.FirstKey : default;

        /// <summary>
        /// Create a new set for a given backing ItemTree.
        /// 
        /// Note: If <paramref name="items"/> is empty, then we discard it and set _items to null, so an
        /// empty set will always have a null _items.
        /// </summary>
        private Set(ItemTree? items)
        {
            _items = items != null && items.Count > 0 ? items : null;
            AssertValid();
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            Validation.Assert(_items == null || _items.Count > 0);
        }

        /// <summary>
        /// Create a TKey set from a sequence of TKeys.
        /// </summary>
        public static Set<TKey> Create(IEnumerable<TKey>? items)
        {
            if (items == null)
                return default;
            return new Set<TKey>(ItemTree.Empty.Create(items.Select(g => (g, true))));
        }

        /// <summary>
        /// Tests whether the TKey is in the set.
        /// </summary>
        public bool Contains(TKey id)
        {
            AssertValid();
            return _items != null && _items.ContainsKey(id);
        }

        /// <summary>
        /// Tests whether this is a subset of <paramref name="big"/>.
        /// </summary>
        public bool IsSubset(Set<TKey> big)
        {
            AssertValid();
            big.AssertValid();

            var items0 = _items;
            if (items0 == null)
                return true;
            var items1 = big._items;
            if (items1 == null)
                return false;
            if (items0 == items1)
                return true;
            if (items0.Count > items1.Count)
                return false;

            foreach (var TKey in items0.GetKeys())
            {
                if (!items1.ContainsKey(TKey))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Tests whether this intersects <paramref name="other"/>.
        /// </summary>
        public bool Intersects(Set<TKey> other)
        {
            AssertValid();
            other.AssertValid();

            var items0 = _items;
            if (items0 == null)
                return false;
            var items1 = other._items;
            if (items1 == null)
                return false;
            if (items0 == items1)
                return true;
            if (items0.Count > items1.Count)
                Util.Swap(ref items0, ref items1);
            Validation.Assert(items0.Count <= items1.Count);

            foreach (var TKey in items0.GetKeys())
            {
                if (items1.ContainsKey(TKey))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Add an item and return the new instance.
        /// </summary>
        public Set<TKey> Add(TKey id)
        {
            AssertValid();
            return new Set<TKey>(Items.SetItem(id, true));
        }

        /// <summary>
        /// Add an item and return the new instance.
        /// </summary>
        public Set<TKey> Remove(TKey id)
        {
            AssertValid();
            if (_items == null)
                return this;
            var items = _items.RemoveItem(id);
            if (items == _items)
                return this;
            return new Set<TKey>(items);
        }

        /// <summary>
        /// Computes the union of two TKey sets.
        /// </summary>
        public static Set<TKey> operator |(Set<TKey> gs0, Set<TKey> gs1)
        {
            gs0.AssertValid();
            gs1.AssertValid();
            if (gs1._items == null)
                return gs0;
            if (gs0._items == null)
                return gs1;
            if (gs0._items.Count >= gs1._items.Count)
                return new Set<TKey>(gs0._items.SetItems(gs1._items.GetPairs()));
            return new Set<TKey>(gs1._items.SetItems(gs0._items.GetPairs()));
        }

        /// <summary>
        /// Computes the intersection of two TKey sets.
        /// </summary>
        public static Set<TKey> operator &(Set<TKey> gs0, Set<TKey> gs1)
        {
            gs0.AssertValid();
            gs1.AssertValid();

            var items0 = gs0._items;
            var items1 = gs1._items;
            if (items0 == null)
                return gs0;
            if (items1 == null)
                return gs1;
            if (items0.Count > items1.Count)
                Util.Swap(ref items0, ref items1);
            Validation.Assert(items0.Count <= items1.Count);
            var items = items0;
            foreach (var TKey in items0.GetKeys())
            {
                if (!items1.ContainsKey(TKey))
                {
                    items = items.RemoveKnownItem(TKey);
                    if (items.Count == 0)
                        break;
                }
            }
            return new Set<TKey>(items);
        }

        /// <summary>
        /// Computes the set difference of two TKey sets.
        /// REVIEW: Should we implement ^, which would be the symmetric set difference?
        /// </summary>
        public static Set<TKey> operator -(Set<TKey> gs0, Set<TKey> gs1)
        {
            gs0.AssertValid();
            gs1.AssertValid();

            var items0 = gs0._items;
            var items1 = gs1._items;
            if (items0 == null)
                return gs0;
            if (items1 == null)
                return gs0;
            if (items0 == items1)
                return Empty;
            foreach (var TKey in items1.GetKeys())
            {
                items0 = items0.RemoveItem(TKey);
                if (items0.Count == 0)
                    break;
            }
            return new Set<TKey>(items0);
        }

        public static bool operator ==(Set<TKey> gs0, Set<TKey> gs1)
        {
            gs0.AssertValid();
            gs1.AssertValid();

            var items0 = gs0._items;
            var items1 = gs1._items;
            if (items0 == items1)
                return true;
            if (items0 == null || items1 == null)
                return false;
            if (items0.Count != items1.Count)
                return false;
            return ItemTree.Equals(items0, items1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Set<TKey> gs0, Set<TKey> gs1)
        {
            return !(gs0 == gs1);
        }

        public bool Equals(Set<TKey> other)
        {
            return this == other;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Set<TKey> gs)
                return false;
            return this == gs;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(typeof(Set<TKey>), Items);
        }

        public IEnumerator<TKey> GetEnumerator()
        {
            return Items.GetKeys().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
