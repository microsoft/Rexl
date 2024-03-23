// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// This is an abstract base class that implements a persistent/functional "dictionary" using a
/// red-black-tree, which is a self-balancing binary tree. All instances are immutable.
/// Each sub-class implements the key comparison, value equality, and hashing functionality.
/// Value equality is needed to avoid "modification" when a value is replaced with an
/// equivalent value.
/// 
/// Each key-value pair can have an associated (byte) tag value, defaulting to zero.
/// </summary>
public abstract partial class RedBlackTree<TKey, TVal> : IReadOnlyDictionary<TKey, TVal>
{
    /// <summary>
    /// The number of items in the tree.
    /// </summary>
    public int Count => _root is not null ? _root.Count : 0;

    /// <summary>
    /// The key of the first item in the tree.
    /// </summary>
    [MaybeNull]
    public TKey FirstKey => _root is not null ? _root.FirstNode.Key : default;

    /// <summary>
    /// The value of the first item in the tree.
    /// </summary>
    [MaybeNull]
    public TVal FirstVal => _root is not null ? _root.FirstNode.Value : default;

    /// <summary>
    /// The key and value of the first item in the tree.
    /// </summary>
    public (TKey key, TVal val) First => _root is not null ? (_root.FirstNode.Key, _root.FirstNode.Value) : default;

    /// <summary>
    /// Create a compatible tree with the given items.
    /// </summary>
    private protected Node? CreateNodes(ReadOnly.Array<(TKey Key, TVal Val)> items, out int[] indices)
    {
        int count = items.Length;
        switch (count)
        {
        case 0:
            indices = Array.Empty<int>();
            return null;
        case 1:
            {
                var kvp = items[0];
                Validation.BugCheckParam(KeyIsValid(kvp.Key), nameof(items));
                Validation.BugCheckParam(ValIsValid(kvp.Val), nameof(items));
                // REVIEW: Avoid this allocation?
                indices = new[] { 0 };
                return CreateLeaf(items[0]);
            }
        }

        indices = new int[count];
        for (int i = 0; i < count; ++i)
        {
            var kvp = items[i];
            Validation.BugCheckParam(KeyIsValid(kvp.Key), nameof(items));
            Validation.BugCheckParam(ValIsValid(kvp.Val), nameof(items));
            indices[i] = i;
        }

        Sorting.Sort(indices, 0, count,
            (ikvp1, ikvp2) =>
            {
                if (ikvp1 == ikvp2)
                    return 0;
                int cmp = KeyCompare(items[ikvp1].Key, items[ikvp2].Key);
                if (cmp != 0)
                    return cmp;
                Validation.Assert(KeyEquals(items[ikvp1].Key, items[ikvp2].Key));
                // Secondary sort order is _descending_ by index, so when there are dups,
                // we keep the _last_.
                return ikvp2 - ikvp1;
            });

        Sorting.RemoveDupsFromSorted(indices, 0, ref count,
            (ikvp1, ikvp2) => KeyEquals(items[ikvp1].Key, items[ikvp2].Key));
        Validation.Assert(1 <= count & count <= indices.Length);

        return CreateFromArraySorted(items, indices, 0, count);
    }

    /// <summary>
    /// Create a compatible tree with the given items, specified via parallel arrays.
    /// </summary>
    private protected Node? CreateNodes(ReadOnly.Array<TKey> keys, ReadOnly.Array<TVal> vals, out int[] indices)
    {
        Validation.BugCheck(keys.Length == vals.Length);

        int count = keys.Length;
        switch (count)
        {
        case 0:
            indices = Array.Empty<int>();
            return null;
        case 1:
            {
                var key = keys[0];
                var val = vals[0];
                Validation.BugCheckParam(KeyIsValid(key), nameof(keys));
                Validation.BugCheckParam(ValIsValid(val), nameof(vals));
                // REVIEW: Avoid this allocation?
                indices = new[] { 0 };
                return CreateLeaf(key, val, tag: 0);
            }
        }

        indices = new int[count];
        for (int i = 0; i < count; ++i)
        {
            Validation.BugCheckParam(KeyIsValid(keys[i]), nameof(keys));
            Validation.BugCheckParam(ValIsValid(vals[i]), nameof(vals));
            indices[i] = i;
        }

        Sorting.Sort(indices, 0, count,
            (ikvp1, ikvp2) =>
            {
                if (ikvp1 == ikvp2)
                    return 0;
                int cmp = KeyCompare(keys[ikvp1], keys[ikvp2]);
                if (cmp != 0)
                    return cmp;
                Validation.Assert(KeyEquals(keys[ikvp1], keys[ikvp2]));
                // Secondary sort order is _descending_ by index, so when there are dups,
                // we keep the _last_.
                return ikvp2 - ikvp1;
            });

        Sorting.RemoveDupsFromSorted(indices, 0, ref count,
            (ikvp1, ikvp2) => KeyEquals(keys[ikvp1], keys[ikvp2]));
        Validation.Assert(1 <= count & count <= indices.Length);

        return CreateFromArraySorted(keys, vals, indices, 0, count);
    }

    /// <summary>
    /// Look up the given key.
    /// </summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TVal value)
    {
        return TryGetValue(key, out value, out _, out _);
    }

    /// <summary>
    /// Look up the given key. When successful, produce both the value and the
    /// zero-based "index" of the item.
    /// </summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TVal value, out int index)
    {
        return TryGetValue(key, out value, out index, out _);
    }

    /// <summary>
    /// Look up the given key. When successful, produce the value and the tag value.
    /// </summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TVal value, out byte tag)
    {
        return TryGetValue(key, out value, out _, out tag);
    }

    /// <summary>
    /// Look up the given key. When successful, produce the value, the zero-based "index"
    /// of the item, and the tag value.
    /// </summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TVal value, out int index, out byte tag)
    {
        Validation.BugCheckParam(KeyIsValid(key), nameof(key));

        if (TryFindNode(_root, key, out var node, out index))
        {
            value = node.Value;
            tag = node.Tag;
            return true;
        }

        value = default;
        tag = default;
        index = -1;
        return false;
    }

    /// <summary>
    /// Return whether this contains the given key.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        return KeyIsValid(key) && TryFindNode(_root, key, out _);
    }

    /// <summary>
    /// Ensure that the <see cref="_nodes"/> field is non-null and contains the nodes in order.
    /// REVIEW: In theory, if there are lots of "almost clones" of the same tree, this
    /// scheme can get inefficient memory-wise. The scheme is based on the assumption that when
    /// a tree is being built up, it isn't also being iterated.
    /// </summary>
    private Node[] EnsureNodes()
    {
        Validation.Assert(_root is not null);
        Validation.Assert(_root.Count > 1);

        var nodes = _nodes;
        if (nodes is not null)
            return nodes;

        // Fill in the array. Use the upper part of the array as a "stack" for iteration.
        nodes = new Node[_root.Count];
        Node node = _root;
        int index = 0;
        int top = nodes.Length;

    LStart:
        Validation.Assert(index < top);
        Validation.Assert(top <= nodes.Length);
        if (node.Left is not null)
        {
            nodes[--top] = node;
            Validation.Assert(index < top);
            node = node.Left;
            goto LStart;
        }

    LSaveCur:
        Validation.Assert(index < top);
        Validation.Assert(top <= nodes.Length);
        nodes[index] = node;
        if (++index >= nodes.Length)
        {
            Validation.Assert(index == nodes.Length);
            Validation.Assert(node.Right is null);
            Validation.Assert(top == nodes.Length);
            Interlocked.CompareExchange(ref _nodes, nodes, null);
            return _nodes;
        }

        if (node.Right is not null)
        {
            node = node.Right;
            goto LStart;
        }

        Validation.Assert(top < nodes.Length);
        node = nodes[top++];
        Validation.Assert(node is not null);
        goto LSaveCur;
    }

    /// <summary>
    /// Get the key-value pairs.
    /// </summary>
    public IEnumerable<(TKey key, TVal val, byte tag)> GetInfos()
    {
        if (_root is null)
            yield break;

        if (_root.Count == 1)
        {
            yield return (_root.Key, _root.Value, _root.Tag);
            yield break;
        }

        var nodes = _nodes ?? EnsureNodes();
        foreach (var node in nodes)
            yield return (node.Key, node.Value, node.Tag);
    }

    /// <summary>
    /// Get the key-value pairs.
    /// </summary>
    public IEnumerable<(TKey key, TVal val)> GetPairs()
    {
        if (_root is null)
            yield break;

        if (_root.Count == 1)
        {
            yield return (_root.Key, _root.Value);
            yield break;
        }

        var nodes = _nodes ?? EnsureNodes();
        foreach (var node in nodes)
            yield return (node.Key, node.Value);
    }

    /// <summary>
    /// Get the keys.
    /// </summary>
    public IEnumerable<TKey> GetKeys()
    {
        if (_root is null)
            yield break;

        if (_root.Count == 1)
        {
            yield return _root.Key;
            yield break;
        }

        var nodes = _nodes ?? EnsureNodes();
        foreach (var node in nodes)
            yield return node.Key;
    }

    /// <summary>
    /// Get the values.
    /// </summary>
    public IEnumerable<TVal> GetValues()
    {
        if (_root is null)
            yield break;

        if (_root.Count == 1)
        {
            yield return _root.Value;
            yield break;
        }

        var nodes = _nodes ?? EnsureNodes();
        foreach (var node in nodes)
            yield return node.Value;
    }

    /// <summary>
    /// Set an item and return the new tree.
    /// </summary>
    protected Node? SetItemCore(TKey key, TVal val, byte tag)
    {
        Validation.BugCheckParam(KeyIsValid(key), nameof(key));
        Validation.BugCheckParam(ValIsValid(val), nameof(val));

        if (_root is null)
            return CreateLeaf(key, val, tag);

        // Any red-red violations at the root don't matter because the root gets forced to black.
        var root = _root;
        int idx = 0;
        AddItemCore(ref root, ref idx, redRoot: false, key, val, tag);
        Validation.Assert(root is not null);
        return root;
    }

    /// <summary>
    /// Set an item and return the new tree.
    /// </summary>
    protected Node? SetItemCore(TKey key, TVal val, byte tag, out int index, out bool isNew)
    {
        Validation.BugCheckParam(KeyIsValid(key), nameof(key));
        Validation.BugCheckParam(ValIsValid(val), nameof(val));

        if (_root is null)
        {
            index = 0;
            isNew = true;
            return CreateLeaf(key, val, tag);
        }

        // Any red-red violations at the root don't matter because the root gets forced to black.
        var root = _root;
        int num = root.Count;
        int idx = 0;
        AddItemCore(ref root, ref idx, redRoot: false, key, val, tag);
        Validation.Assert(root is not null);
        Validation.Assert(root.Count == num || root.Count == num + 1);
        index = idx;
        isNew = root.Count > num;
        return root;
    }

    /// <summary>
    /// Sets multiple items. When there are duplicate keys, the last one wins.
    /// </summary>
    protected Node? SetItemsCore(IEnumerable<(TKey Key, TVal Val)> items)
    {
        Validation.AssertValue(items);

        // Any red-red violations at the root don't matter because the root gets forced to black.
        var root = _root;
        foreach (var kvp in items)
        {
            Validation.BugCheck(KeyIsValid(kvp.Key));
            Validation.BugCheck(ValIsValid(kvp.Val));
            if (root is null)
                root = CreateLeaf(kvp.Key, kvp.Val, tag: 0);
            else
            {
                int idx = 0;
                AddItemCore(ref root, ref idx, redRoot: false, kvp.Key, kvp.Val, tag: 0);
            }
            Validation.Assert(root is not null);
        }
        return root;
    }

    /// <summary>
    /// Sets multiple items specified via parallel arrays. When there are duplicate keys, the last one wins.
    /// </summary>
    protected Node? SetItemsCore(ReadOnly.Array<TKey> keys, ReadOnly.Array<TVal> values)
    {
        Validation.Assert(keys.Length == values.Length);
        Validation.Assert(keys.Length > 0);

        int count = keys.Length;

        // Any red-red violations at the root don't matter because the root gets forced to black.
        var root = _root;
        for (int i = 0; i < count; i++)
        {
            var key = keys[i];
            var val = values[i];
            Validation.BugCheck(KeyIsValid(key));
            Validation.BugCheck(ValIsValid(val));
            if (root is null)
                root = CreateLeaf(key, val, tag: 0);
            else
            {
                int idx = 0;
                AddItemCore(ref root, ref idx, redRoot: false, key, val, tag: 0);
            }
            Validation.Assert(root is not null);
        }
        return root;
    }

    protected int GetHashCodeCore()
    {
        int hash = _hash;
        if (hash != 0)
            return hash;

        bool usesTag = UsesTag;
        var hc = new HashCode();
        hc.Add(Count);
        foreach (var kvp in GetInfos())
        {
            hc.Add(KeyHash(kvp.key));
            hc.Add(ValHash(kvp.val));
            if (usesTag)
                hc.Add(kvp.tag);
        }
        hash = hc.ToHashCode();
        if (hash == 0)
            hash = 1;
        return _hash = hash;
    }

    #region Implementation of IReadOnlyDictionary

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TVal>.Keys => GetKeys();

    IEnumerable<TVal> IReadOnlyDictionary<TKey, TVal>.Values => GetValues();

    TVal IReadOnlyDictionary<TKey, TVal>.this[TKey key]
    {
        get
        {
            Validation.BugCheckParam(KeyIsValid(key), nameof(key));
            Validation.BugCheckParam(TryFindNode(_root, key, out var node), nameof(key));
            return node.Value;
        }
    }

    IEnumerator<KeyValuePair<TKey, TVal>> IEnumerable<KeyValuePair<TKey, TVal>>.GetEnumerator()
    {
        if (_root is null)
            yield break;

        if (_root.Count == 1)
        {
            yield return new KeyValuePair<TKey, TVal>(_root.Key, _root.Value);
            yield break;
        }

        var nodes = _nodes ?? EnsureNodes();
        foreach (var node in nodes)
            yield return new KeyValuePair<TKey, TVal>(node.Key, node.Value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<TKey, TVal>>)this).GetEnumerator();
    }

    #endregion Implementation of IReadOnlyDictionary

    #region Abstract / virtual members.

    /// <summary>
    /// Whether the tag is used. The default is false.
    /// </summary>
    protected virtual bool UsesTag => false;

    protected abstract bool KeyIsValid(TKey key);
    protected abstract int KeyCompare(TKey key0, TKey key1);
    protected abstract bool KeyEquals(TKey key0, TKey key1);
    protected abstract int KeyHash(TKey key);

    protected abstract bool ValIsValid(TVal val);
    protected abstract bool ValEquals(TVal val0, TVal val1);
    protected abstract int ValHash(TVal val);

    protected virtual ushort GetFlags(TKey key, TVal val) => 0;

    #endregion Abstract / virtual members.
}

// This partial implements the tree construction logic. Most things in this partial are private.
//
// Red Black tree rules:
// 1) A node is either red or black.
// 2) The root is black.
// 3) All null nodes are black.
// 4) Both children of red nodes are black.
// 5) Every path from a node to any of its leaves contains the same number of black nodes.
//    This is the node's 'black height'.
// References:
//   Introduction to Algorithms -  Cormen, Leiserson, Rivest
//   Purely Functional Data Structures - Okasaki
partial class RedBlackTree<TKey, TVal>
{
    protected readonly Node? _root;

    // The nodes in order. Computed lazily and cached.
    private volatile Node[]? _nodes;

    // Computed lazily and cached.
    private int _hash;

    protected RedBlackTree(Node? root)
    {
        Validation.AssertValueOrNull(root);
        _root = root;
    }

    private Node? CreateFromArraySorted(
        ReadOnly.Array<(TKey Key, TVal Value)> pairs, ReadOnly.Array<int> indices, int min, int lim)
    {
        Validation.Assert(0 <= min & min <= lim & lim <= pairs.Length);
        Validation.Assert(lim <= indices.Length);

        int nodeCount = lim - min;
        switch (nodeCount)
        {
        case 0:
            return null;
        case 1:
            return CreateLeaf(pairs[indices[min]]);
        case 2:
            {
                var left = CreateLeaf(pairs[indices[min]]);
                return CreateInterior(pairs[indices[min + 1]], left, null, Reds.Left);
            }
        case 3:
            {
                var left = CreateLeaf(pairs[indices[min]]);
                var right = CreateLeaf(pairs[indices[min + 2]]);
                return CreateInterior(pairs[indices[min + 1]], left, right, Reds.None);
            }
        }

        {
            Reds reds = Reds.None;

            // We build a tree with maximal number of black nodes. We can always make the max "distance to
            // leaf" (DTL) be at most one more than the min DTL. Moreover, we can do that while "equally"
            // splitting nodes between the left and right subtrees.
            int half = nodeCount >> 1;
            if ((nodeCount & 1) != 0)
            {
                // The number of nodes is odd. The middle node is the root, and the left and right trees
                // are built with identical structure, so the left and right nodes should both be black.
                Validation.Assert(nodeCount == 2 * half + 1);
            }
            else
            {
                // The number of nodes is even. Give the extra node to the left, so the left subtree gets
                // N/2 nodes and the right subtree gets N/2 - 1 nodes.
                Validation.Assert(nodeCount == 2 * half);

                // The two trees can't have identical structure, but they can almost always have the same
                // black depth (min DTL). When they can, the left and right nodes should both be black.
                //
                // So when can't they have the same min DTL? When the right subtree is full (totally balanced),
                // which happens when N/2 + 1 is a power of two. In this case, the left node should be red and
                // the right should be black.
                //
                // This happens iff N + 2 is a power of two. That is, iff (N + 2) & (N + 1) is zero.
                if (((nodeCount + 2) & (nodeCount + 1)) == 0)
                    reds |= Reds.Left;
            }
            var left = CreateFromArraySorted(pairs, indices, min, min + half);
            var right = CreateFromArraySorted(pairs, indices, min + half + 1, lim);
            var res = CreateNode(pairs[indices[min + half]], left, right, reds, tag: 0);

#if DEBUG
            Validation.Assert((1UL << (res.BlackDepth + 1)) > (ulong)nodeCount);
            Validation.Assert((1UL << res.BlackDepth) - 1 <= (ulong)nodeCount);
#endif
            return res;
        }
    }

    private Node? CreateFromArraySorted(ReadOnly.Array<TKey> keys, ReadOnly.Array<TVal> vals,
        ReadOnly.Array<int> indices, int min, int lim)
    {
        Validation.Assert(keys.Length == vals.Length);
        Validation.Assert(0 <= min & min <= lim & lim <= indices.Length);

        int nodeCount = lim - min;
        switch (nodeCount)
        {
        case 0:
            return null;
        case 1:
            {
                int i = indices[min];
                Validation.AssertIndex(i, keys.Length);
                return CreateLeaf(keys[i], vals[i], tag: 0);
            }
        case 2:
            {
                int i = indices[min];
                Validation.AssertIndex(i, keys.Length);
                var left = CreateLeaf(keys[i], vals[i], tag: 0);
                i = indices[min + 1];
                Validation.AssertIndex(i, keys.Length);
                return CreateInterior(keys[i], vals[i], left, null, Reds.Left, tag: 0);
            }
        case 3:
            {
                int i = indices[min];
                Validation.AssertIndex(i, keys.Length);
                var left = CreateLeaf(keys[i], vals[i], tag: 0);
                i = indices[min + 2];
                Validation.AssertIndex(i, keys.Length);
                var right = CreateLeaf(keys[i], vals[i], tag: 0);
                i = indices[min + 1];
                Validation.AssertIndex(i, keys.Length);
                return CreateInterior(keys[i], vals[i], left, right, Reds.None, tag: 0);
            }
        }

        {
            Reds reds = Reds.None;

            // We build a tree with maximal number of black nodes. We can always make the max "distance to
            // leaf" (DTL) be at most one more than the min DTL. Moreover, we can do that while "equally"
            // splitting nodes between the left and right subtrees.
            int half = nodeCount >> 1;
            if ((nodeCount & 1) != 0)
            {
                // The number of nodes is odd. The middle node is the root, and the left and right trees
                // are built with identical structure, so the left and right nodes should both be black.
                Validation.Assert(nodeCount == 2 * half + 1);
            }
            else
            {
                // The number of nodes is even. Give the extra node to the left, so the left subtree gets
                // N/2 nodes and the right subtree gets N/2 - 1 nodes.
                Validation.Assert(nodeCount == 2 * half);

                // The two trees can't have identical structure, but they can almost always have the same
                // black depth (min DTL). When they can, the left and right nodes should both be black.
                //
                // So when can't they have the same min DTL? When the right subtree is full (totally balanced),
                // which happens when N/2 + 1 is a power of two. In this case, the left node should be red and
                // the right should be black.
                //
                // This happens iff N + 2 is a power of two. That is, iff (N + 2) & (N + 1) is zero.
                if (((nodeCount + 2) & (nodeCount + 1)) == 0)
                    reds |= Reds.Left;
            }
            var left = CreateFromArraySorted(keys, vals, indices, min, min + half);
            var right = CreateFromArraySorted(keys, vals, indices, min + half + 1, lim);
            int i = indices[min + half];
            Validation.AssertIndex(i, keys.Length);
            var res = CreateNode(keys[i], vals[i], left, right, reds, tag: 0);

#if DEBUG
            Validation.Assert((1UL << (res.BlackDepth + 1)) > (ulong)nodeCount);
            Validation.Assert((1UL << res.BlackDepth) - 1 <= (ulong)nodeCount);
#endif
            return res;
        }
    }

    [Flags]
    protected enum Reds : byte
    {
        None = 0x0,
        Left = 0x1,
        Right = 0x2,
        Both = Left | Right,
    }

    /// <summary>
    /// Maps from a pair of bools to <see cref="Reds"/>.
    /// </summary>
    private static Reds RedsFrom(bool redLeft, bool redRight)
    {
        return (redLeft ? Reds.Left : 0) | (redRight ? Reds.Right : 0);
    }

    protected abstract class Node
    {
        public readonly TKey Key;
        public readonly TVal Value;

        // These pack into 64 bits.
        // REVIEW: LeafNode doesn't really need the _count or _reds fields.
        // Also, for LeafNode, the _flags is of limited cache value. Is the tag worth
        // the bloat? Is there another way that doesn't bloat LeafNode by 64 bits?
        private readonly int _count;
        private readonly ushort _flags;
        private protected readonly Reds _reds;
        private readonly byte _tag;

        private protected Node(TKey key, TVal value, int count, ushort flags, Reds reds, byte tag)
        {
            Key = key;
            Value = value;
            _count = count;
            _flags = flags;
            _reds = reds;
            _tag = tag;
        }

        public int Count => _count;
        public abstract Node FirstNode { get; }
        public abstract Node? Left { get; }
        public abstract Node? Right { get; }
        public Reds Reds => _reds;
        public bool RedLeft => (_reds & Reds.Left) != 0;
        public bool RedRight => (_reds & Reds.Right) != 0;
        public bool HasRedChild => _reds != 0;

        /// <summary>
        /// Each node "has" a set of flags stored as bits in a ushort. The flags for a tree
        /// consist of the union (bitwise or) of the flags for all of the kvps within the tree.
        /// </summary>
        public ushort Flags => _flags;

        public byte Tag => _tag;

#if DEBUG
        public abstract int BlackDepth { get; }
#endif

        // Creates a new node with the same key and structure of the current node
        // but uses the new value.
        public abstract Node CloneStructure(TVal value, byte tag, ushort flags);
    }

    /// <summary>
    /// All invokers of this should fully validate the key and value.
    /// This doesn't have access to the key/value validator logic.
    /// </summary>
    private sealed class LeafNode : Node
    {
        public LeafNode(TKey key, TVal value, ushort flags, byte tag)
            : base(key, value, count: 1, flags, reds: 0, tag)
        {
        }

        public override Node FirstNode => this;
        public override Node? Left => null;
        public override Node? Right => null;

#if DEBUG
        public override int BlackDepth => 1;
#endif

        public override Node CloneStructure(TVal value, byte tag, ushort flags)
        {
            return new LeafNode(Key, value, flags, tag);
        }
    }

    private sealed class InteriorNode : Node
    {
        private readonly Node _first;
        private readonly Node? _left;
        private readonly Node? _right;

        /// <summary>
        /// All invokers of this should fully validate the key ordering, as well as the key and value.
        /// This doesn't have access to the key comparer or the key/value validator logic.
        /// </summary>
        public InteriorNode(TKey key, TVal value, Node? left, Node? right,
                int count, ushort flags, Reds reds, byte tag)
            : base(key, value, count, flags, reds, tag)
        {
            Validation.Assert(left is not null || right is not null);
            Validation.Assert(left is not null || (reds & Reds.Left) == 0);
            Validation.Assert(right is not null || (reds & Reds.Right) == 0);

#if DEBUG
            int depth0 = (left is not null ? left.BlackDepth : 0) + Util.ToNum((reds & Reds.Left) == 0);
            int depth1 = (right is not null ? right.BlackDepth : 0) + Util.ToNum((reds & Reds.Right) == 0);
            Validation.Assert(depth0 == depth1);
            BlackDepth = depth0;

            int c = 1;
            _first = this;
            if (left is not null)
                c += left.Count;
            if (right is not null)
                c += right.Count;
            Validation.Assert(c == count);
#endif

            _first = this;
            if (left is not null)
            {
                _first = left.FirstNode;
                _left = left;
            }
            if (right is not null)
                _right = right;
        }

        public override Node FirstNode => _first;
        public override Node? Left => _left;
        public override Node? Right => _right;

#if DEBUG
        public override int BlackDepth { get; }
#endif

        public override Node CloneStructure(TVal value, byte tag, ushort flags)
        {
            if (_left is not null)
                flags |= _left.Flags;
            if (_right is not null)
                flags |= _right.Flags;
            return new InteriorNode(Key, value, _left, _right, Count, flags, _reds, tag);
        }
    }

    /// <summary>
    /// Creates an interior node. At least one of <paramref name="left"/> and <paramref name="right"/>
    /// must be non-null. This fully validates the construction via asserts.
    /// </summary>
    private InteriorNode CreateInterior(TKey key, TVal val, Node? left, Node? right, Reds reds, byte tag)
    {
        Validation.Assert(KeyIsValid(key));
        Validation.Assert(ValIsValid(val));

        // Must have at least one child.
        Validation.Assert(left is not null || right is not null);

        // The keys should be ordered properly.
        Validation.Assert(left is null || KeyCompare(left.Key, key) < 0);
        Validation.Assert(right is null || KeyCompare(key, right.Key) < 0);

        // A red node can't have a red child.
        Validation.Assert((reds & Reds.Left) == 0 || left is not null && !left.HasRedChild);
        Validation.Assert((reds & Reds.Right) == 0 || right is not null && !right.HasRedChild);

        var flags = GetFlags(key, val);
        int count = 1;
        if (left is not null)
        {
            count += left.Count;
            flags |= left.Flags;
        }
        if (right is not null)
        {
            count += right.Count;
            flags |= right.Flags;
        }
        return new InteriorNode(key, val, left, right, count, flags, reds, tag);
    }

    /// <summary>
    /// Creates an interior node. At least one of <paramref name="left"/> and <paramref name="right"/>
    /// must be non-null. This fully validates the construction via asserts.
    /// </summary>
    private InteriorNode CreateInterior(Node nodeData, Node? left, Node? right, Reds reds)
    {
        Validation.AssertValue(nodeData);
        return CreateInterior(nodeData.Key, nodeData.Value, left, right, reds, nodeData.Tag);
    }

    /// <summary>
    /// Creates an interior node. At least one of <paramref name="left"/> and <paramref name="right"/>
    /// must be non-null. This fully validates the construction via asserts.
    /// </summary>
    private InteriorNode CreateInterior((TKey key, TVal val) kvp, Node? left, Node? right, Reds reds)
    {
        return CreateInterior(kvp.key, kvp.val, left, right, reds, tag: 0);
    }

    /// <summary>
    /// Creates a leaf node.
    /// </summary>
    private LeafNode CreateLeaf(TKey key, TVal val, byte tag)
    {
        Validation.Assert(KeyIsValid(key));
        Validation.Assert(ValIsValid(val));
        var flags = GetFlags(key, val);
        return new LeafNode(key, val, flags, tag);
    }

    /// <summary>
    /// Creates a leaf node.
    /// </summary>
    private LeafNode CreateLeaf((TKey key, TVal val) kvp)
    {
        return CreateLeaf(kvp.key, kvp.val, tag: 0);
    }

    /// <summary>
    /// Creates a node. Note that left and/or right may be null. This fully validates the construction via asserts.
    /// </summary>
    private Node CreateNode(TKey key, TVal val, Node? left, Node? right, Reds reds, byte tag)
    {
        Validation.Assert(KeyIsValid(key));
        Validation.Assert(ValIsValid(val));
        Validation.AssertValueOrNull(left);
        Validation.AssertValueOrNull(right);

        if (left is null && right is null)
        {
            Validation.Assert(reds == 0);
            return CreateLeaf(key, val, tag);
        }

        // The keys should be ordered properly.
        Validation.Assert(left is null || KeyCompare(left.Key, key) < 0);
        Validation.Assert(right is null || KeyCompare(key, right.Key) < 0);

        // A red node can't have a red child.
        Validation.Assert((reds & Reds.Left) == 0 || left is not null && !left.HasRedChild);
        Validation.Assert((reds & Reds.Right) == 0 || right is not null && !right.HasRedChild);

        return CreateInterior(key, val, left, right, reds, tag);
    }

    private Node CreateNode(Node nodeData, Node? left, Node? right, Reds reds)
    {
        Validation.AssertValue(nodeData);
        return CreateNode(nodeData.Key, nodeData.Value, left, right, reds, nodeData.Tag);
    }

    private Node CreateNode(Node nodeData, Node? left, Node? right, bool redLeft, bool redRight)
    {
        Validation.AssertValue(nodeData);
        return CreateNode(nodeData.Key, nodeData.Value, left, right, RedsFrom(redLeft, redRight), nodeData.Tag);
    }

    private Node CreateNode(TKey key, TVal val, Node? left, Node? right, bool redLeft, bool redRight, byte tag)
    {
        return CreateNode(key, val, left, right, RedsFrom(redLeft, redRight), tag);
    }

    private Node CreateNode((TKey key, TVal val) kvp, Node? left, Node? right, Reds reds, byte tag)
    {
        return CreateNode(kvp.key, kvp.val, left, right, reds, tag);
    }

    // Enum for private Add API that needs to maintain more state about the tree
    private enum AddCoreResult
    {
        // The item was already in the tree, no change.
        ItemPresent,
        // The key was already in the tree, and was updated with a new value.
        // A new tree is being built, but no colors need to change.
        ItemUpdated,
        // The key was not in the tree.
        // A new tree is being built, but no colors need to change.
        ItemAdded,
        // The key was not in the tree.
        // A new tree is being built, the returned node should be red.
        NewNodeIsRed,
        // The key was not in the tree.
        // A new tree is being built, the returned node is red
        // and its left child is also red, the caller needs to fix this violation
        DoubleRedLeftChild,
        // The key was not in the tree.
        // A new tree is being built, the returned node is red
        // and its right child is also red, the caller needs to fix this violation
        DoubleRedRightChild
    }

    // Enum for private Remove API that needs to maintain more state about the tree
    private enum RemoveCoreResult
    {
        // The key was not in the tree, no change.
        ItemNotFound,
        // The key was in the tree.
        // A new tree is being built, but no colors need to change.
        ItemRemoved,
        // The key was in the tree.
        // A new tree is being built, the returned node should be black.
        NewNodeIsBlack,
        // The key was in the tree.
        // A new tree is being built, the returned node is 'double black'
        // which is a violation that needs to be fixed by the caller.
        NewNodeIsDoubleBlack,
    }

    private bool TryFindNode(Node? root, TKey key, [NotNullWhen(true)] out Node? node)
    {
        Validation.AssertValueOrNull(root);
        Validation.Assert(KeyIsValid(key));

        node = root;
        while (node is not null)
        {
            int cmp = KeyCompare(key, node.Key);
            if (cmp == 0)
            {
                Validation.Assert(KeyEquals(key, node.Key));
                return true;
            }
            node = (cmp < 0) ? node.Left : node.Right;
        }

        return false;
    }

    private bool TryFindNode(Node? root, TKey key, [NotNullWhen(true)] out Node? node, out int index)
    {
        Validation.AssertValueOrNull(root);
        Validation.Assert(KeyIsValid(key));

        int ind = 0;
        node = root;
        while (node is not null)
        {
            int cmp = KeyCompare(key, node.Key);
            if (cmp < 0)
            {
                node = node.Left;
                continue;
            }

            // Skipping the left tree so adjust ind accordingly.
            if (node.Left is not null)
                ind += node.Left.Count;

            if (cmp == 0)
            {
                Validation.Assert(KeyEquals(key, node.Key));
                index = ind;
                return true;
            }

            // Skipping this node, so add 1 to ind.
            ind += 1;
            node = node.Right;
        }

        index = -1;
        return false;
    }

    private AddCoreResult AddItemCore(ref Node root, ref int idx, bool redRoot, TKey key, TVal val, byte tag)
    {
        Validation.AssertValue(root);
        Validation.Assert(KeyIsValid(key));
        Validation.Assert(ValIsValid(val));

        int cmp = KeyCompare(key, root.Key);
        if (cmp == 0)
        {
            if (root.Left is not null)
                idx += root.Left.Count;
            Validation.Assert(KeyEquals(key, root.Key));
            if (tag == root.Tag && ValEquals(val, root.Value))
                return AddCoreResult.ItemPresent; // Don't build a new tree.
            var flags = GetFlags(root.Key, val);
            root = root.CloneStructure(val, tag, flags);
            return AddCoreResult.ItemUpdated; // Build a new tree, don't change the count.
        }

        Node? left;
        Node? right;
        AddCoreResult result;
        if (cmp < 0)
        {
            // Add to the left side of the tree.
            right = root.Right;
            if (root.Left is null)
            {
                // Make a new left child, set its color to red.
                root = CreateInterior(root, CreateLeaf(key, val, tag), right, Reds.Left | root.Reds);
                // Check for double red violation and warn the caller.
                return redRoot ? AddCoreResult.DoubleRedLeftChild : AddCoreResult.ItemAdded;
            }

            // DIAGRAM LEGEND:
            // [] - black node
            // numbers - color doesn't matter, may be null in some cases

            Node? newLeft;
            Node? newRight;
            left = root.Left;
            result = AddItemCore(ref left, ref idx, root.RedLeft, key, val, tag);
            switch (result)
            {
            case AddCoreResult.ItemUpdated:
            case AddCoreResult.ItemAdded:
                root = CreateInterior(root, left, right, root.Reds);
                return result;
            case AddCoreResult.NewNodeIsRed:
                root = CreateInterior(root, left, right, Reds.Left | root.Reds);
                // Check for double red violation and warn the caller.
                return redRoot ? AddCoreResult.DoubleRedLeftChild : AddCoreResult.ItemAdded;
            case AddCoreResult.DoubleRedLeftChild:
                //     [Z]     
                //     / \
                //    Y   4           Y
                //   /\              /  \
                //  X  3     =>   [X]   [Z]
                //  /\            /  \  /  \
                // 1 2           1   2 3   4
                Validation.Assert(!redRoot);

                newLeft = left.Left;
                newRight = CreateNode(root, left.Right, root.Right, left.RedRight, root.RedRight);
                root = CreateInterior(left, newLeft, newRight, Reds.None);
                return AddCoreResult.NewNodeIsRed;
            case AddCoreResult.DoubleRedRightChild:
                //    [Z]     
                //    / \
                //   X   4           Y
                //  /\              /  \
                // 1  Y     =>   [X]   [Z]
                //    /\         /  \  /  \
                //   2 3        1   2 3   4
                Validation.Assert(!redRoot);

                Node? leftRight = left.Right;
                Validation.AssertValue(leftRight);
                newLeft = CreateNode(left, left.Left, leftRight.Left, left.RedLeft, leftRight.RedLeft);
                newRight = CreateNode(root, leftRight.Right, right, leftRight.RedRight, root.RedRight);
                root = CreateInterior(leftRight, newLeft, newRight, Reds.None);
                return AddCoreResult.NewNodeIsRed;
            default:
                Validation.Assert(result == AddCoreResult.ItemPresent);
                return AddCoreResult.ItemPresent;
            }
        }
        else
        {
            // Add to the right side of the tree.
            if (root.Left is not null)
                idx += root.Left.Count;
            idx++;

            left = root.Left;
            if (root.Right is null)
            {
                // Make a new right child, set its color to red.
                root = CreateInterior(root, left, CreateLeaf(key, val, tag), root.Reds | Reds.Right);
                // Check for double red violation and warn the caller.
                return redRoot ? AddCoreResult.DoubleRedRightChild : AddCoreResult.ItemAdded;
            }

            Node? newLeft;
            Node? newRight;
            right = root.Right;
            result = AddItemCore(ref right, ref idx, root.RedRight, key, val, tag);
            switch (result)
            {
            case AddCoreResult.ItemUpdated:
            case AddCoreResult.ItemAdded:
                root = CreateInterior(root, left, right, root.Reds);
                return result;
            case AddCoreResult.NewNodeIsRed:
                root = CreateInterior(root, left, right, root.Reds | Reds.Right);
                // Check for double red violation and warn the caller.
                return redRoot ? AddCoreResult.DoubleRedRightChild : AddCoreResult.ItemAdded;
            case AddCoreResult.DoubleRedRightChild:
                //    [X]        
                //    / \
                //   1   Y              Y
                //      / \           /   \
                //     2  Z    =>   [X]   [Z]
                //        /\        / \   / \
                //       3 4       1   2 3   4
                Validation.Assert(!redRoot);

                newLeft = CreateNode(root, root.Left, right.Left, root.RedLeft, right.RedLeft);
                newRight = right.Right;
                root = CreateInterior(right, newLeft, newRight, Reds.None);
                return AddCoreResult.NewNodeIsRed;
            case AddCoreResult.DoubleRedLeftChild:
                //    [X]        
                //    / \
                //   1   Z              Y
                //      / \           /   \
                //     Y  4    =>   [X]   [Z]
                //     /\           / \   / \
                //     2 3         1   2 3   4
                Validation.Assert(!redRoot);

                Node? rightLeft = right.Left;
                Validation.AssertValue(rightLeft);
                newLeft = CreateNode(root, root.Left, rightLeft.Left, root.RedLeft, rightLeft.RedLeft);
                newRight = CreateNode(right, rightLeft.Right, right.Right, rightLeft.RedRight, right.RedRight);
                root = CreateInterior(rightLeft, newLeft, newRight, Reds.None);
                return AddCoreResult.NewNodeIsRed;
            default:
                Validation.Assert(result == AddCoreResult.ItemPresent);
                return AddCoreResult.ItemPresent;
            }
        }
    }

    protected bool TryRemoveItemCore(out Node? root, out int idx, TKey key)
    {
        root = _root;
        idx = 0;
        var res = RemoveItemCore(ref root, ref idx, redRoot: false, key);
        Validation.Assert((_root == root) == (res == RemoveCoreResult.ItemNotFound));
        return res != RemoveCoreResult.ItemNotFound;
    }

    private RemoveCoreResult RemoveItemCore(ref Node? root, ref int idx, bool redRoot, TKey key)
    {
        Validation.AssertValueOrNull(root);
        Validation.Assert(KeyIsValid(key));

        if (root is null)
            return RemoveCoreResult.ItemNotFound;

        int cmp = KeyCompare(key, root.Key);
        if (cmp == 0)
        {
            Validation.Assert(KeyEquals(key, root.Key));

            // We found it. Update count.
            if (root.Left is null)
            {
                bool redRight = root.RedRight;
                root = root.Right;
                // If the removed node or its only child is red, just color it black
                // and we are done, no black height change.
                if (redRoot || redRight)
                    return RemoveCoreResult.NewNodeIsBlack;
                // Mark the new node as a fake 'double black' to maintain the black height,
                // callers will do fixup, if needed.
                return RemoveCoreResult.NewNodeIsDoubleBlack;
            }

            idx += root.Left.Count;

            if (root.Right is null)
            {
                bool redLeft = root.RedLeft;
                root = root.Left;
                // If the removed node or its only child is red, just color it black
                // and we are done, no black height change.
                if (redRoot || redLeft)
                    return RemoveCoreResult.NewNodeIsBlack;
                // Mark the new node as a fake 'double black' to maintain the black height,
                // callers will do fixup, if needed.
                return RemoveCoreResult.NewNodeIsDoubleBlack;
            }

            Node leftMost;
            Node newRight = root.Right;
            // Find the left-most child of the right node and remove it, then use its key/value
            // as the new key/value for this node, thus 'removing' the target node.
            RemoveCoreResult result = RemoveLeftMost(ref newRight, root.RedRight, out leftMost);
            Validation.Assert(result != RemoveCoreResult.ItemNotFound);

            switch (result)
            {
            case RemoveCoreResult.ItemRemoved:
                root = CreateInterior(leftMost, root.Left, newRight, root.Reds);
                return RemoveCoreResult.ItemRemoved;
            case RemoveCoreResult.NewNodeIsBlack:
                root = CreateInterior(leftMost, root.Left, newRight, root.Reds & Reds.Left);
                return RemoveCoreResult.ItemRemoved;
            default:
                Validation.Assert(result == RemoveCoreResult.NewNodeIsDoubleBlack);
                return RemoveFixupRight(ref root, leftMost, redRoot, newRight);
            }
        }

        if (cmp < 0)
        {
            // Check the left side of the tree.
            Node? left = root.Left;
            RemoveCoreResult result = RemoveItemCore(ref left, ref idx, root.RedLeft, key);
            switch (result)
            {
            case RemoveCoreResult.ItemRemoved:
                root = CreateNode(root, left, root.Right, root.Reds);
                return RemoveCoreResult.ItemRemoved;
            case RemoveCoreResult.NewNodeIsBlack:
                root = CreateNode(root, left, root.Right, root.Reds & Reds.Right);
                return RemoveCoreResult.ItemRemoved;
            case RemoveCoreResult.NewNodeIsDoubleBlack:
                return RemoveFixupLeft(ref root, redRoot, left);
            default:
                Validation.Assert(result == RemoveCoreResult.ItemNotFound);
                return RemoveCoreResult.ItemNotFound;
            }
        }
        else
        {
            // Check the right side of the tree.
            if (root.Left is not null)
                idx += root.Left.Count;
            idx++;

            Node? right = root.Right;
            RemoveCoreResult result = RemoveItemCore(ref right, ref idx, root.RedRight, key);
            switch (result)
            {
            case RemoveCoreResult.ItemRemoved:
                root = CreateNode(root, root.Left, right, root.Reds);
                return RemoveCoreResult.ItemRemoved;
            case RemoveCoreResult.NewNodeIsBlack:
                root = CreateNode(root, root.Left, right, root.Reds & Reds.Left);
                return RemoveCoreResult.ItemRemoved;
            case RemoveCoreResult.NewNodeIsDoubleBlack:
                return RemoveFixupRight(ref root, root, redRoot, right);
            default:
                Validation.Assert(result == RemoveCoreResult.ItemNotFound);
                return RemoveCoreResult.ItemNotFound;
            }
        }
    }

    private RemoveCoreResult RemoveLeftMost(ref Node root, bool redRoot, out Node removedNode)
    {
        Validation.AssertValue(root);

        if (root.Left is null)
        {
            // We've reached the left most node, remove it.
            bool redRight = root.RedRight;
            removedNode = root;
            root = root.Right!;
            // If the removed node or its only child is red, just color it black
            // and we are done, no black height violations.
            if (redRoot || redRight)
                return RemoveCoreResult.NewNodeIsBlack;
            // Mark the new node as a fake 'double black' to maintain the black height,
            // callers will do fixup.
            return RemoveCoreResult.NewNodeIsDoubleBlack;
        }

        Node left = root.Left;
        RemoveCoreResult result = RemoveLeftMost(ref left, root.RedLeft, out removedNode);
        Validation.Assert(result != RemoveCoreResult.ItemNotFound);

        switch (result)
        {
        case RemoveCoreResult.ItemRemoved:
            root = CreateNode(root, left, root.Right, root.Reds);
            return RemoveCoreResult.ItemRemoved;
        case RemoveCoreResult.NewNodeIsBlack:
            root = CreateNode(root, left, root.Right, root.Reds & Reds.Right);
            return RemoveCoreResult.ItemRemoved;
        default:
            Validation.Assert(result == RemoveCoreResult.NewNodeIsDoubleBlack);
            return RemoveFixupLeft(ref root, redRoot, left);
        }
    }

    // Performs red-black tree fixup for the root whose new left child (left)
    // is a 'double black' node.
    private RemoveCoreResult RemoveFixupLeft(ref Node root, bool redRoot, Node? left)
    {
        Validation.AssertValue(root);
        Validation.AssertValue(root.Right);
        Validation.AssertValueOrNull(left);

        Node rootRight = root.Right;

        // There are 4 main cases, order matters to avoid introducing red-red violations.
        // DIAGRAM LEGEND:
        // [] - black node
        // [[]] - double black node
        // {} - red or black node
        // numbers - color doesn't matter, may be null in some cases

        // CASE 1: The 'double black' node's furthest nephew is red.
        // Perform a left rotation and recolor. Tree is valid after this step.
        if (rootRight.RedRight)
        {
            //        {B}                              {D}           
            //     /       \                        /       \
            // [[A]]        [D]                 [B]         [E]
            //  / \         / \      =>        /   \        / \
            // 1   2     {C}   E            [A]     {C}    5   6      
            //           / \  / \           / \     / \
            //          3   4 5 6          1   2   3   4
            Validation.Assert(!root.RedRight);

            Node newLeft = CreateNode(root, left, rootRight.Left, redLeft: false, rootRight.RedLeft);
            Node? newRight = rootRight.Right;
            root = CreateInterior(rootRight, newLeft, newRight, Reds.None);
            return RemoveCoreResult.ItemRemoved;
        }

        // CASE 2: The 'double black' node's nearest nephew is red.
        // Performing a right rotation and recolor on the root's right child transforms this to case 1.
        // Then performing a left rotation and recolor on the root makes a valid tree.
        // Both rotations and recolorings are performed in one step to save on node creation.
        if (rootRight.RedLeft)
        {
            //        {B}                    {B}                        {C}
            //     /       \               /     \                    /    \
            // [[A]]        [D]        [[A]]      [C]              [B]      [D]
            //  / \         / \    =>   /  \      /  \             / \      / \
            // 1   2      C   [E]      1   2    3     D     =>  [A]   3    4   [E]
            //           / \  / \                    / \        / \            / \
            //          3   4 5 6                   4  [E]     1   2          5   6
            //                                         / \
            //                                        5   6
            Validation.Assert(!root.RedRight);
            Validation.Assert(!rootRight.RedRight);

            Node? rootRightLeft = rootRight.Left;
            Validation.AssertValue(rootRightLeft);
            Node newLeft = CreateNode(root, left, rootRightLeft.Left, redLeft: false, rootRightLeft.RedLeft);
            Node newRight = CreateNode(rootRight, rootRightLeft.Right, rootRight.Right, rootRightLeft.RedRight, redRight: false);
            root = CreateInterior(rootRightLeft, newLeft, newRight, Reds.None);
            return RemoveCoreResult.ItemRemoved;
        }

        // CASE 3: The 'double black' node's sibling is red.
        // Performing a left rotation and recolor on the root transforms this into EITHER case 1, 2 OR 4, 
        // depending on the values of root.Right.Left.RedLeft and root.Right.Left.RedRight
        // All rotations and recolorings are performed in one step to save on node creation.
        // In all cases, the tree is valid after this step.
        if (root.RedRight)
        {
            Validation.Assert(!redRoot);
            Validation.Assert(!rootRight.HasRedChild);

            Node newLeft;
            Node? newRight;
            Node? rootRightLeft = rootRight.Left;
            Validation.AssertValue(rootRightLeft);
            // Case 3 -> Case 1
            if (rootRightLeft.RedRight)
            {
                //        [B]                           [D]                            [D]
                //     /       \                     /       \                       /     \
                // [[A]]          D                B          [E]                  C        [E]
                //  / \         /   \    =>     /     \       / \   =>          /     \     /  \
                // 1   2     [C]    [E]      [[A]]   [C]     6   7           [B]     [CZ]  6   7
                //           / \    / \       /\     /  \                   /  \     / \
                //          3   CZ  6 7      1  2   3   CZ                [A]   3  4    5
                //              / \                    /  \              /  \
                //             4   5                  4    5            1    2
                Node newLeftLeft = CreateNode(root, left, rootRightLeft.Left, redLeft: false, rootRightLeft.RedLeft);
                Node? newLeftRight = rootRightLeft.Right;
                newLeft = CreateInterior(rootRightLeft, newLeftLeft, newLeftRight, Reds.None);
                newRight = rootRight.Right;
                root = CreateInterior(rootRight, newLeft, newRight, Reds.Left);
                return RemoveCoreResult.ItemRemoved;
            }

            // Case 3 -> Case 2 -> Case 1
            if (rootRightLeft.RedLeft)
            {
                //        [B]                           [D]        <See Case 2                    [D]
                //     /       \                     /       \      for 2 stage rotation       /        \
                // [[A]]          D                B          [E]   using B as root>         CA          [E]
                //  / \         /   \    =>     /     \       / \        =>                /    \       /  \
                // 1   2     [C]    [E]      [[A]]   [C]     7   8                      [B]       [C]  7    8
                //           / \    / \       /\     /  \                              /  \     /    \
                //          CA [CZ] 7  8     1  2   CA [CZ]                          [A]   3    4   [CZ]
                //         / \  / \                 /\  /\                          /  \            /  \
                //        3  4 5   6               3  4 5 6                        1    2          5    6
                Node? rootRightLeftLeft = rootRightLeft.Left;
                Validation.AssertValue(rootRightLeftLeft);
                Node newLeftLeft = CreateNode(root, left, rootRightLeftLeft.Left, redLeft: false, rootRightLeftLeft.RedLeft);
                Node newLeftRight = CreateNode(rootRightLeft, rootRightLeftLeft.Right, rootRightLeft.Right, rootRightLeftLeft.RedRight, redRight: false);
                newLeft = CreateInterior(rootRightLeftLeft, newLeftLeft, newLeftRight, Reds.None);
                newRight = rootRight.Right;
                root = CreateInterior(rootRight, newLeft, newRight, Reds.Left);
                return RemoveCoreResult.ItemRemoved;
            }

            // Case 3 -> Case 4
            //        [B]                           [D]                        [D]       
            //     /       \                     /       \                  /       \    
            // [[A]]          D                B          [E]            [B]         [E] 
            //  / \         /   \    =>     /     \       / \   =>     /     \       / \ 
            // 1   2     [C]    [E]      [[A]]   [C]     3   4       [A]     C      3   4
            //           / \    / \       /\     /  \                /\     /  \         
            //         [CA][CZ] 3 4      1  2   [CA][CZ]            1  2   [CA][CZ]      
            newLeft = CreateNode(root, left, rootRightLeft, Reds.Right);
            newRight = rootRight.Right;
            root = CreateInterior(rootRight, newLeft, newRight, Reds.None);
            return RemoveCoreResult.NewNodeIsBlack;
        }

        // CASE 4: The 'double black' node's parent is red.
        // Move the parent's red to the sibling and color the parent black and the tree becomes valid.
        if (redRoot)
        {
            Validation.Assert(!root.RedRight);
            root = CreateInterior(root, left, rootRight, Reds.Right);
            return RemoveCoreResult.NewNodeIsBlack;
        }

        // CASE 5: All of the 'double black' node's relations are black (parent, sibling, both nephews)
        // Color the sibling red and make the root the new 'double black' node.
        Validation.Assert(!redRoot);
        Validation.Assert(!root.RedRight);
        Validation.Assert(!rootRight.RedLeft);
        Validation.Assert(!rootRight.RedRight);
        root = CreateInterior(root, left, rootRight, Reds.Right);
        return RemoveCoreResult.NewNodeIsDoubleBlack;
    }

    // Performs red-black tree fixup for the root whose new right child (right)
    // is a 'double black' node.
    // Takes two 'root' parameters (root, rootData) instead of one like RemoveFixupLeft
    // because the data for the new root might be coming from the result of a RemoveLeftMost
    private RemoveCoreResult RemoveFixupRight(ref Node root, Node rootData, bool redRoot, Node? right)
    {
        Validation.AssertValue(root);
        Validation.AssertValue(root.Left);
        Validation.AssertValue(rootData);
        Validation.AssertValueOrNull(right);

        Node rootLeft = root.Left;

        // There are 4 main cases, order matters to avoid introducing red-red violations.
        // See RemoveFixupLeft for diagrams, just reverse left and right.

        // CASE 1: The 'double black' node's furthest nephew is red.
        // Perform a right rotation and recolor. Tree is valid after this step.
        if (rootLeft.RedLeft)
        {
            Validation.Assert(!root.RedLeft);
            Node? newLeft = rootLeft.Left;
            Node newRight = CreateNode(rootData, rootLeft.Right, right, rootLeft.RedRight, redRight: false);
            root = CreateInterior(rootLeft, newLeft, newRight, Reds.None);
            return RemoveCoreResult.ItemRemoved;
        }

        // CASE 2: The 'double black' node's nearest nephew is red.
        // Performing a left rotation and recolor on the root's left child transforms this to case 1.
        // Then performing a right rotation and recolor on the root makes a valid tree.
        // Both rotations and recolorings are performed in one step to save on node creation.
        if (rootLeft.RedRight)
        {
            Validation.Assert(!root.RedLeft);
            Validation.Assert(!rootLeft.RedLeft);
            Node rootLeftRight = rootLeft.Right!;
            Node newLeft = CreateNode(rootLeft, rootLeft.Left, rootLeftRight.Left, redLeft: false, rootLeftRight.RedLeft);
            Node newRight = CreateNode(rootData, rootLeftRight.Right, right, rootLeftRight.RedRight, redRight: false);
            root = CreateInterior(rootLeftRight, newLeft, newRight, Reds.None);
            return RemoveCoreResult.ItemRemoved;
        }

        // CASE 3: The 'double black' node's sibling is red.
        // Performing a right rotation and recolor on the root transforms this into EITHER case 1, 2 OR 4,
        // depending on the values of root.Left.Right.RedLeft and root.Left.Right.RedRight
        // All rotations and recolorings are performed in one step to save on node creation.
        // In all cases, the tree is valid after this step.
        if (root.RedLeft)
        {
            Validation.Assert(!redRoot);
            Validation.Assert(!rootLeft.HasRedChild);

            Node? newLeft;
            Node newRight;
            Node? rootLeftRight = rootLeft.Right;
            Validation.AssertValue(rootLeftRight);
            // Case 3 -> Case 1
            if (rootLeftRight.RedLeft)
            {
                Node? newRightLeft = rootLeftRight.Left;
                Node newRightRight = CreateNode(rootData, rootLeftRight.Right, right, rootLeftRight.RedRight, redRight: false);
                newLeft = rootLeft.Left;
                newRight = CreateInterior(rootLeftRight, newRightLeft, newRightRight, Reds.None);
                root = CreateInterior(rootLeft, newLeft, newRight, Reds.Right);
                return RemoveCoreResult.ItemRemoved;
            }

            // Case 3 -> Case 2 -> Case 1
            if (rootLeftRight.RedRight)
            {
                Node? rootLeftRightRight = rootLeftRight.Right;
                Validation.AssertValue(rootLeftRightRight);
                Node newRightLeft = CreateNode(rootLeftRight, rootLeftRight.Left, rootLeftRightRight.Left, redLeft: false, rootLeftRightRight.RedLeft);
                Node newRightRight = CreateNode(rootData, rootLeftRightRight.Right, right, rootLeftRightRight.RedRight, redRight: false);
                newLeft = rootLeft.Left;
                newRight = CreateInterior(rootLeftRightRight, newRightLeft, newRightRight, Reds.None);
                root = CreateInterior(rootLeft, newLeft, newRight, Reds.Right);
                return RemoveCoreResult.ItemRemoved;
            }

            // Case 3 -> Case 4
            newLeft = rootLeft.Left;
            newRight = CreateInterior(rootData, rootLeftRight, right, Reds.Left);
            root = CreateInterior(rootLeft, newLeft, newRight, Reds.None);
            return RemoveCoreResult.NewNodeIsBlack;
        }

        // CASE 4: The 'double black' node's parent is red.
        // Move the parent's red to the sibling and color the parent black and the tree becomes valid.
        if (redRoot)
        {
            Validation.Assert(!root.RedLeft);
            root = CreateInterior(rootData, rootLeft, right, Reds.Left);
            return RemoveCoreResult.NewNodeIsBlack;
        }

        // CASE 5: All of the 'double black' node's relations are black (parent, sibling, both nephews)
        // Color the sibling red and make the root the new 'double black' node.
        Validation.Assert(!redRoot);
        Validation.Assert(!root.RedLeft);
        Validation.Assert(!rootLeft.HasRedChild);
        root = CreateInterior(rootData, rootLeft, right, Reds.Left);
        return RemoveCoreResult.NewNodeIsDoubleBlack;
    }
}

/// <summary>
/// This is an abstract base class that implements a persistent/functional "dictionary" using a
/// red-black-tree, which is a self-balancing binary tree. All instances are immutable.
/// The "modification" methods return a result (of type <typeparamref name="TTree"/>) rather than
/// mutate the existing instance.
/// </summary>
public abstract partial class RedBlackTree<TTree, TKey, TVal> : RedBlackTree<TKey, TVal>
    where TTree : RedBlackTree<TTree, TKey, TVal>
{
    protected RedBlackTree(Node? root)
        : base(root)
    {
        Validation.Assert(this is TTree);
    }

    /// <summary>
    /// Create a compatible tree with the given items.
    /// </summary>
    public TTree Create(params (TKey Key, TVal Value)[] items)
    {
        return Wrap(CreateNodes(items, out _));
    }

    /// <summary>
    /// Create a compatible tree with the given items.
    /// </summary>
    public TTree Create(ReadOnly.Array<(TKey Key, TVal Value)> items)
    {
        return Wrap(CreateNodes(items, out _));
    }

    /// <summary>
    /// Create a compatible tree with the given items.
    /// </summary>
    public TTree Create(IEnumerable<(TKey Key, TVal Val)>? items)
    {
        return Wrap(CreateNodes(items?.ToArray(), out _));
    }

    /// <summary>
    /// Create a compatible tree with the given items.
    /// </summary>
    public TTree Create(IEnumerable<(TKey Key, TVal Val)>? items, out int[] indices)
    {
        return Wrap(CreateNodes(items?.ToArray(), out indices));
    }

    /// <summary>
    /// Create a compatible tree with the given items, specified via parallel arrays.
    /// </summary>
    public TTree Create(ReadOnly.Array<TKey> keys, ReadOnly.Array<TVal> vals)
    {
        return Wrap(CreateNodes(keys, vals, out _));
    }

    /// <summary>
    /// Create a compatible tree with the given items, specified via parallel arrays.
    /// </summary>
    public TTree Create(ReadOnly.Array<TKey> keys, ReadOnly.Array<TVal> vals, out int[] indices)
    {
        return Wrap(CreateNodes(keys, vals, out indices));
    }

    /// <summary>
    /// Returns whether the given tree and this one have the exact same root.
    /// </summary>
    public bool SameRoot(TTree? tree)
    {
        return tree is not null && tree._root == _root;
    }

    /// <summary>
    /// Set an item and return the new tree.
    /// </summary>
    public TTree SetItem(TKey key, TVal val, byte tag = 0)
    {
        return Wrap(SetItemCore(key, val, tag));
    }

    /// <summary>
    /// Set an item and return the new tree.
    /// </summary>
    public TTree SetItem(TKey key, TVal val, out int index, out bool isNew, byte tag = 0)
    {
        return Wrap(SetItemCore(key, val, tag, out index, out isNew));
    }

    /// <summary>
    /// Sets multiple items. When there are duplicate keys, the last one wins.
    /// </summary>
    public TTree SetItems(params (TKey Key, TVal Value)[] items)
    {
        return SetItems((IEnumerable<(TKey Key, TVal Value)>)(items));
    }

    /// <summary>
    /// Sets multiple items. When there are duplicate keys, the last one wins.
    /// </summary>
    public TTree SetItems(IEnumerable<(TKey Key, TVal Val)> items)
    {
        if (items is null)
            return (TTree)this;
        return Wrap(SetItemsCore(items));
    }

    /// <summary>
    /// Sets multiple items specified via parallel arrays. When there are duplicate keys, the last one wins.
    /// </summary>
    public TTree SetItems(ReadOnly.Array<TKey> keys, ReadOnly.Array<TVal> values)
    {
        Validation.BugCheck(keys.Length == values.Length);

        if (keys.Length == 0)
            return (TTree)this;

        return Wrap(SetItemsCore(keys, values));
    }

    /// <summary>
    /// Remove an item and return the new tree. If the item isn't present,
    /// returns "this".
    /// </summary>
    public TTree RemoveItem(TKey key) => RemoveItemCore(key, known: false, out _);

    /// <summary>
    /// Remove an item and return the new tree. If the item isn't present,
    /// returns "this".
    /// </summary>
    public TTree RemoveItem(TKey key, out int index) => RemoveItemCore(key, known: false, out index);

    /// <summary>
    /// Remove an item and return the new tree. Asserts that the item was there.
    /// </summary>
    public TTree RemoveKnownItem(TKey key) => RemoveItemCore(key, known: true, out _);

    /// <summary>
    /// Remove an item and return the new tree. Asserts that the item was there.
    /// </summary>
    public TTree RemoveKnownItem(TKey key, out int index) => RemoveItemCore(key, known: true, out index);

    private TTree RemoveItemCore(TKey key, bool known, out int index)
    {
        Validation.BugCheckParam(KeyIsValid(key), nameof(key));

        if (!TryRemoveItemCore(out var root, out index, key))
        {
            Validation.Assert(!known);
            index = -1;
            return (TTree)this;
        }
        Validation.AssertIndexInclusive(index, root?.Count ?? 0);
        return Wrap(root);
    }

    /// <summary>
    /// Returns whether the given trees are semantically equal.
    /// Note that we DON'T implement == and !=, so those operators indicate reference equality.
    /// </summary>
    public static bool Equals(TTree? tree1, TTree? tree2)
    {
        Validation.AssertValueOrNull(tree1);
        Validation.AssertValueOrNull(tree2);

        if (tree1 == tree2)
            return true;

        if (tree1 is null || tree2 is null)
            return false;

        if (tree1.Count != tree2.Count)
            return false;

        bool usesTag = tree1.UsesTag | tree2.UsesTag;
        using var ator1 = tree1.GetInfos().GetEnumerator();
        using var ator2 = tree2.GetInfos().GetEnumerator();
        while (ator1.MoveNext())
        {
            bool fTmp = ator2.MoveNext();
            Validation.Assert(fTmp);

            var info1 = ator1.Current;
            var info2 = ator2.Current;
            if (!tree1.KeyEquals(info1.key, info2.key))
                return false;
            if (!tree1.ValEquals(info1.val, info2.val))
                return false;
            if (usesTag && info1.tag != info2.tag)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns whether this tree is semantically equal to <paramref name="other"/>.
    /// Note that we DON'T implement == and !=, so those operators indicate reference equality.
    /// </summary>
    public bool Equals(TTree? other)
    {
        Validation.AssertValueOrNull(other);
        return Equals((TTree)this, other);
    }

    /// <summary>
    /// REVIEW: Should this use reference equality (as it does now) or semantic equality?
    /// </summary>
    public override bool Equals(object? obj)
    {
        return this == obj;
    }

    public override int GetHashCode()
    {
        return GetHashCodeCore();
    }

    protected abstract TTree Wrap(Node? root);
}

/// <summary>
/// A base implementation with key type <see cref="DName"/>. This seals the comparison (order) to be ordinal,
/// but allows further customization of key validity, eg, to disallow default.
/// </summary>
public abstract class DNameRedBlackTree<TTree, TVal> : RedBlackTree<TTree, DName, TVal>
    where TTree : DNameRedBlackTree<TTree, TVal>
{
    protected DNameRedBlackTree(Node? root)
        : base(root)
    {
    }

    protected sealed override int KeyCompare(DName key0, DName key1)
    {
        return DName.Compare(key0, key1);
    }

    protected sealed override bool KeyEquals(DName key0, DName key1)
    {
        return key0 == key1;
    }

    protected sealed override int KeyHash(DName key)
    {
        return key.GetHashCode();
    }
}

/// <summary>
/// A base implementation with key type <see cref="NPath"/>. This seals the key comparison,
/// but allows further customization of key validity, eg, to disallow <see cref="NPath.Root"/>.
/// </summary>
public abstract class NPathRedBlackTree<TTree, TVal> : RedBlackTree<TTree, NPath, TVal>
    where TTree : NPathRedBlackTree<TTree, TVal>
{
    protected NPathRedBlackTree(Node? root)
        : base(root)
    {
    }

    protected sealed override int KeyCompare(NPath key0, NPath key1)
    {
        return NPath.CompareRaw(key0, key1);
    }

    protected sealed override bool KeyEquals(NPath key0, NPath key1)
    {
        return key0 == key1;
    }

    protected sealed override int KeyHash(NPath key)
    {
        return key.GetHashCode();
    }
}

/// <summary>
/// A base implementation with key type (<see cref="NPath"/>, <typeparamref name="TExtra"/>).
/// This seals the key comparison, but allows further customization of key validity, eg, to disallow <see cref="NPath.Root"/>.
/// </summary>
public abstract class NPathExtraRedBlackTree<TTree, TExtra, TVal> : RedBlackTree<TTree, (NPath path, TExtra extra), TVal>
    where TTree : NPathExtraRedBlackTree<TTree, TExtra, TVal>
    where TExtra : IComparable<TExtra>, IEquatable<TExtra>
{
    protected NPathExtraRedBlackTree(Node? root)
        : base(root)
    {
    }

    protected sealed override int KeyCompare((NPath path, TExtra extra) key0, (NPath path, TExtra extra) key1)
    {
        int cmp = key0.extra.CompareTo(key1.extra);
        if (cmp != 0)
            return cmp;
        return NPath.CompareRaw(key0.path, key1.path);
    }

    protected sealed override bool KeyEquals((NPath path, TExtra extra) key0, (NPath path, TExtra extra) key1)
    {
        return key0.extra.Equals(key1.extra) && key0.path == key1.path;
    }

    protected sealed override int KeyHash((NPath path, TExtra extra) key)
    {
        return HashCode.Combine(key.extra, key.path);
    }
}

/// <summary>
/// A base implementation with key type <see cref="int"/>. This seals the key comparison,
/// but allows further customization of key validity, eg, to disallow negative.
/// </summary>
public abstract class IntRedBlackTree<TTree, TVal> : RedBlackTree<TTree, int, TVal>
    where TTree : IntRedBlackTree<TTree, TVal>
{
    protected IntRedBlackTree(Node? root)
        : base(root)
    {
    }

    protected override int KeyCompare(int key0, int key1)
    {
        return key0.CompareTo(key1);
    }

    protected override bool KeyEquals(int key0, int key1)
    {
        return key1 == key0;
    }

    protected override int KeyHash(int key)
    {
        return key;
    }
}

/// <summary>
/// A base implementation with key type <see cref="Guid"/>. This seals the key comparison,
/// but allows further customization of key validity, eg, to disallow default.
/// </summary>
public abstract class GuidRedBlackTree<TTree, TVal> : RedBlackTree<TTree, Guid, TVal>
    where TTree : GuidRedBlackTree<TTree, TVal>
{
    protected GuidRedBlackTree(Node? root)
        : base(root)
    {
    }

    protected override int KeyCompare(Guid key0, Guid key1)
    {
        return key0.CompareTo(key1);
    }

    protected override bool KeyEquals(Guid key0, Guid key1)
    {
        return key0 == key1;
    }

    protected override int KeyHash(Guid key)
    {
        return key.GetHashCode();
    }
}
