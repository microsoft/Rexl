// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// Used for sorting <see cref="NPath"/> values in a way that is suitable for
/// humans to see.
/// </summary>
public sealed class NPathComparer : IComparer<NPath>
{
    public static readonly NPathComparer Instance = new NPathComparer();

    private NPathComparer()
    {
    }

    public int Compare(NPath x, NPath y)
    {
        return NPath.CompareUser(x, y);
    }
}

/// <summary>
/// Used for sorting <see cref="NPath"/> values in a way that is suitable for internal
/// non-visible uses, for example a lookup tree. Not intended as ordering for human readers.
/// </summary>
public sealed class NPathRawComparer : IComparer<NPath>
{
    public static readonly NPathRawComparer Instance = new NPathRawComparer();

    private NPathRawComparer()
    {
    }

    public int Compare(NPath x, NPath y)
    {
        return NPath.CompareRaw(x, y);
    }
}

/// <summary>
/// Implements case insensitive equality comparison of <see cref="NPath"/> values.
/// </summary>
public sealed class CiNPathComparer : IEqualityComparer<NPath>
{
    public static readonly CiNPathComparer Instance = new CiNPathComparer();

    private readonly StringComparer _cmp;

    private CiNPathComparer()
    {
        _cmp = StringComparer.InvariantCultureIgnoreCase;
    }

    public bool Equals(NPath x, NPath y)
    {
        if (x.NameCount != y.NameCount)
            return false;
        while (x.NameCount > 0)
        {
            if (!_cmp.Equals(x.Leaf, y.Leaf))
                return false;
            x = x.Parent;
            y = y.Parent;
        }
        return true;
    }

    public int GetHashCode(NPath path)
    {
        if (path.IsRoot)
            return 0;
        if (path.NameCount == 1)
            return _cmp.GetHashCode(path.Leaf);

        var hash = new HashCode();
        hash.Add(path.NameCount);
        while (path.NameCount > 0)
        {
            hash.Add(_cmp.GetHashCode(path.Leaf));
            path = path.Parent;
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// An <see cref="NPath"/> is essentially a list of <see cref="DName"/>, starting at "Root", denoted by $ or ∂.
/// </summary>
public struct NPath : IEquatable<NPath>
{
    public const char RootChar = '\u2202';
    private const string RootString = "\u2202";

    // Note that this represents a path as a linked list from leaf to root. This makes appending
    // a single name fast and light-weight. It also supports common prefix sharing, since Node
    // is immutable (other than the lazily computed hash). Of course, it complicates processing
    // path segments from root to end, often encouraging recursion.
    private class Node
    {
        public const int HashNull = 0x340CA819;

        public readonly Node First;
        public readonly Node? Parent;
        public readonly DName Name;

        /// <summary>
        /// The NameCount is the number of names in the path starting here.
        /// </summary>
        public readonly int NameCount;

        // Computed lazily and cached. This only hashes the strings, NOT the length.
        private volatile int _hash;

        public Node(Node? par, DName name)
        {
            Validation.AssertValueOrNull(par);
            Validation.Assert(name.IsValid);

            First = par?.First ?? this;
            Parent = par;
            Name = name;
            NameCount = par != null ? 1 + par.NameCount : 1;

            AssertValid();
        }

        [Conditional("DEBUG")]
        internal void AssertValid()
        {
#if DEBUG
            Validation.AssertValueOrNull(Parent);
            Validation.Assert(Name.IsValid);
            Validation.Assert(NameCount == 1 + (Parent?.NameCount).GetValueOrDefault());
#endif
        }

        public override int GetHashCode()
        {
            if (_hash == 0)
                EnsureHash();
            return _hash;
        }

        private void EnsureHash()
        {
            int hash = HashCode.Combine(Parent?.GetHashCode() ?? HashNull, Name);
            if (hash == 0)
                hash = 1;
            Interlocked.CompareExchange(ref _hash, hash, 0);
        }
    }

    // The "root", aka $ or ∂, is indicated by null.
    private readonly Node? _node;

    public static readonly NPath Root = default(NPath);

    /// <summary>
    /// Wraps the given node as a NPath.
    /// </summary>
    private NPath(Node? node)
    {
        Validation.AssertValueOrNull(node);
        _node = node;
        AssertValid();
    }

    /// <summary>
    /// Extends the given NPath with another name.
    /// </summary>
    private NPath(NPath par, DName name)
    {
        par.AssertValid();
        Validation.Assert(name.IsValid);
        _node = new Node(par._node, name);
        AssertValid();
    }

    [Conditional("DEBUG")]
    private void AssertValid()
    {
#if DEBUG
        if (_node != null)
            _node.AssertValid();
#endif
    }

    /// <summary>
    /// Return whether this is the root.
    /// </summary>
    public bool IsRoot { get { return _node == null; } }

    /// <summary>
    /// Return the number of names in the path.
    /// </summary>
    public int NameCount { get { return (_node?.NameCount).GetValueOrDefault(); } }

    /// <summary>
    /// Return the leaf name in the path.
    /// </summary>
    public DName Leaf { get { return _node != null ? _node.Name : default(DName); } }

    /// <summary>
    /// Return the parent path.
    /// </summary>
    public NPath Parent { get { return _node != null ? new NPath(_node.Parent) : default(NPath); } }

    /// <summary>
    /// Return the first name (closest to the root) in the path.
    /// </summary>
    public DName First { get { return _node != null ? _node.First.Name : default(DName); } }

    internal object? GetBlob()
    {
        AssertValid();
        return _node;
    }

    internal static bool IsBlob(object? blob)
    {
        return blob is null || blob is Node;
    }

    internal static NPath CreateFromBlob(object? blob)
    {
        Validation.Assert(IsBlob(blob));
        return new NPath((Node?)blob);
    }

    /// <summary>
    /// Append the given name to this path, and return the result.
    /// </summary>
    public NPath Append(DName name)
    {
        Validation.BugCheckParam(name.IsValid, nameof(name));
        return new NPath(this, name);
    }

    /// <summary>
    /// Append the given path to this path and return the result.
    /// </summary>
    public NPath AppendPath(NPath path)
    {
        AssertValid();
        path.AssertValid();

        if (path.NameCount == 0)
            return this;

        if (IsRoot)
            return path;

        Stack<Node> nodes = new Stack<Node>(path.NameCount);
        Node? nodeCur;
        for (nodeCur = path._node; nodeCur != null; nodeCur = nodeCur.Parent)
            nodes.Push(nodeCur);
        Validation.Assert(nodes.Count == path.NameCount);

        nodeCur = nodes.Pop();
        Node nodeNew = new Node(_node, nodeCur.Name);
        while (nodes.TryPop(out nodeCur))
            nodeNew = new Node(nodeNew, nodeCur.Name);

        return new NPath(nodeNew);
    }

    /// <summary>
    /// Append the part of the given path from the given name index onward.
    /// </summary>
    public NPath AppendPartial(NPath path, int index)
    {
        AssertValid();
        path.AssertValid();
        Validation.BugCheckIndexInclusive(index, path.NameCount, nameof(index));

        if (path.NameCount <= index)
            return this;

        if (IsRoot && index <= 0)
            return path;

        Stack<Node> nodes = new Stack<Node>(path.NameCount - index);
        Node? nodeCur;
        for (nodeCur = path._node; nodeCur != null && nodeCur.NameCount > index; nodeCur = nodeCur.Parent)
            nodes.Push(nodeCur);
        Validation.Assert(nodes.Count == path.NameCount - index);

        nodeCur = nodes.Pop();
        Node nodeNew = new Node(_node, nodeCur.Name);
        while (nodes.TryPop(out nodeCur))
            nodeNew = new Node(nodeNew, nodeCur.Name);

        return new NPath(nodeNew);
    }

    /// <summary>
    /// If the NameCount is positive, returns the parent path (with one fewer names), placing the leaf name in
    /// the out parameter. Otherwise, sets name to default (which has IsValid false) and returns Root.
    /// </summary>
    public NPath PopOne(out DName name)
    {
        AssertValid();

        if (_node == null)
        {
            name = default(DName);
            return Root;
        }

        name = _node.Name;
        Validation.Assert(name.IsValid);
        return new NPath(_node.Parent);
    }

    /// <summary>
    /// If the NameCount exceeds <paramref name="count"/>, returns the prefix consisting of the first
    /// <paramref name="count"/> names. Otherwise, returns <c>this</c>.
    /// </summary>
    public NPath TrimTail(int count)
    {
        AssertValid();
        if (count <= 0)
            return Root;
        if (NameCount <= count)
            return this;
        Validation.AssertValue(_node);

        var node = _node;
        while (node.NameCount > count)
            node = node.Parent!;
        return new NPath(node);
    }

    /// <summary>
    /// Returns whether path is a proper prefix of, or equal to, "this".
    /// </summary>
    public bool StartsWith(NPath path)
    {
        AssertValid();
        path.AssertValid();

        if (path._node == null)
            return true;
        var node = _node;
        if (node == null)
            return false;

        if (path._node.NameCount > node.NameCount)
            return false;

        while (node.NameCount > path._node.NameCount)
            node = node.Parent!;
        return new NPath(node) == path;
    }

    public static NPath GetCommonPrefix(NPath a, NPath b)
    {
        var node0 = a._node;
        if (node0 is null)
            return a;
        var node1 = b._node;
        if (node1 is null)
            return b;

        if (node0.NameCount > node1.NameCount)
            Util.Swap(ref node0, ref node1);

        while (node0.NameCount < node1.NameCount)
        {
            node1 = node1.Parent;
            Validation.AssertValue(node1);
            Validation.Assert(node0.NameCount <= node1.NameCount);
        }
        Validation.Assert(node0.NameCount == node1.NameCount);

        Node? nodeRes = null;
        for (; node0 != null; node0 = node0.Parent, node1 = node1.Parent)
        {
            Validation.AssertValue(node1);
            if (node0.Name != node1.Name)
                nodeRes = null;
            else if (nodeRes == null)
                nodeRes = node0;
        }
        Validation.Assert(node0 == null);
        Validation.Assert(node1 == null);

        return new NPath(nodeRes);
    }

    public NPath GetCommonPrefix(NPath other)
    {
        return GetCommonPrefix(this, other);
    }

    // REVIEW: Minimize use of this.
    public override string ToString()
    {
        AssertValid();

        if (_node == null)
            return RootString;

        // Compute the number of characters needed. One for the root symbol.
        int cch = 1;

        // Add in the names and dots before each name.
        for (var node = _node; node != null; node = node.Parent)
            cch += node.Name.Value.Length + 1;

        // Allocate the buffer.
        StringBuilder sb = new StringBuilder(cch) { Length = cch };

        // Now fill in the characters, in reverse order.
        for (var node = _node; node != null; node = node.Parent)
        {
            Validation.AssertValue(node);
            node.AssertValid();

            string str = node.Name;
            int ich = str.Length;
            Validation.Assert(ich <= cch);
            while (ich > 0)
                sb[--cch] = str[--ich];

            // Preceeding dot.
            Validation.Assert(cch > 0);
            sb[--cch] = '.';
        }

        Validation.Assert(cch == 1);
        sb[--cch] = RootChar;
        Validation.Assert(cch == 0);

        return sb.ToString();
    }

    /// <summary>
    /// Convert this NPath to a string in dotted syntax, such as "A.B.C". Escapes the names when needed.
    /// REVIEW: This doesn't really belong here, since it invokes the Lexer!
    /// REVIEW: Reduce usage of this.
    /// </summary>
    public string ToDottedSyntax(string sep = ".")
    {
        AssertValid();

        if (_node == null)
            return "";
        if (NameCount == 1)
            return _node.Name.Escape();
        var names = ToNamePartsCore(true);
        return string.Join(sep, names);
    }

    /// <summary>
    /// Convert this NPath to an array of strings as they would be in dotted syntax.
    /// Such as { "A", "B", "C" }. Escapes the names when needed.
    /// REVIEW: Perhaps, move to Lexer when the above ToDottedSyntax() is also moved.
    /// </summary>
    public Immutable.Array<string> ToNameParts(bool escape = false)
    {
        AssertValid();

        if (_node == null)
            return Immutable.Array<string>.Empty;
        if (NameCount == 1)
            return Immutable.Array<string>.Create(escape ? _node.Name.Escape() : _node.Name);
        return ToNamePartsCore(escape);
    }

    private Immutable.Array<string> ToNamePartsCore(bool escape)
    {
        Validation.Assert(NameCount > 0);

        var names = Immutable.Array.CreateBuilder<string>(NameCount, init: true);
        var node = _node;
        int inode = NameCount;
        for (; --inode >= 0; node = node.Parent)
        {
            Validation.AssertValue(node);
            node.AssertValid();
            names[inode] = escape ? node.Name.Escape() : node.Name;
        }

        Validation.Assert(node == null);
        return names.ToImmutable();
    }

    /// <summary>
    /// Get an immutable array builder of <see cref="DName"/> containing the items in this path.
    /// </summary>
    public Immutable.Array<DName>.Builder ToNames(int start = 0)
    {
        AssertValid();
        Validation.BugCheckIndexInclusive(start, NameCount, nameof(start));

        var res = Immutable.Array<DName>.CreateBuilder(NameCount - start, init: true);
        var node = _node;
        for (int i = res.Count; --i >= 0;)
        {
            Validation.Assert(node != null);
            res[i] = node.Name;
            node = node.Parent;
        }
        Validation.Assert(start == 0 && node == null || start > 0 && node != null && node.NameCount == start);
        return res;
    }

    /// <summary>
    /// Get a NPath made of all items in this path converted to lowercase using
    /// the casing rule of the invariant culture.
    /// </summary>
    public NPath ToLower()
    {
        AssertValid();

        var names = ToNamePartsCore(false);
        var res = NPath.Root;
        for (int i = 0; i < names.Length; ++i)
            res = res.Append(new DName(names[i].ToLowerInvariant()));
        return res;
    }

    /// <summary>
    /// Compare two values in a manner useful for binary trees, etc.
    /// Note that the order defined by this is NOT appropriate for sorting for user
    /// visibility. For example, paths with more components always come after shorter
    /// ones, the names are considered in leaf to root order, etc. Generally, this is
    /// faster than <see cref="CompareUser(NPath, NPath)"/>.
    /// </summary>
    public static int CompareRaw(NPath a, NPath b)
    {
        a.AssertValid();
        b.AssertValid();

        var n0 = a._node;
        var n1 = b._node;
        if (n0 == n1)
            return 0;

        Validation.Assert(n0 != null || n1 != null);
        if (n0 == null)
            return -1;
        if (n1 == null)
            return +1;

        int dif = n0.NameCount - n1.NameCount;
        if (dif != 0)
            return dif;

        for (; ; )
        {
            int cmp = string.Compare(n0.Name.Value, n1.Name.Value, StringComparison.Ordinal);
            if (cmp != 0)
                return cmp;
            n0 = n0.Parent;
            n1 = n1.Parent;
            if (n0 == n1)
                return 0;
            Validation.Assert(n0 != null);
            Validation.Assert(n1 != null);
        }
    }

    /// <summary>
    /// Compare two values in a manner useful for user sorting, using conventional
    /// ordering. For example, if one path is a prefix of another, it comes earlier.
    /// Ordering is done using invariant case insensitive first, with case sensitive
    /// used as a tie breaker, and ordinal used as ultimate tie breaker. Generally, this
    /// is slower than <see cref="CompareRaw(NPath, NPath)"/>.
    /// </summary>
    public static int CompareUser(NPath a, NPath b)
    {
        a.AssertValid();
        b.AssertValid();

        var n0 = a._node;
        var n1 = b._node;
        if (n0 == n1)
            return 0;

        Validation.Assert(n0 != null || n1 != null);
        if (n0 == null)
            return -1;
        if (n1 == null)
            return +1;

        int res = n0.NameCount - n1.NameCount;
        if (res > 0)
        {
            for (int i = 0; i < res; i++)
                n0 = n0.Parent!;
        }
        else if (res < 0)
        {
            for (int i = 0; i < -res; i++)
                n1 = n1.Parent!;
        }
        Validation.Assert(n0.NameCount == n1.NameCount);

        for (; ; )
        {
            var s0 = n0.Name.Value;
            var s1 = n1.Name.Value;

            int cmp = CultureUtil.CompareInfo.Compare(s0, s1, CompareOptions.IgnoreCase);
            if (cmp == 0)
                cmp = CultureUtil.CompareInfo.Compare(s0, s1, CompareOptions.None);
            if (cmp == 0)
                cmp = string.Compare(s0, s1, StringComparison.Ordinal);
            if (cmp != 0)
                res = cmp;
            n0 = n0.Parent;
            n1 = n1.Parent;
            if (n0 == n1)
                return res;
            Validation.Assert(n0 != null);
            Validation.Assert(n1 != null);
        }
    }

    public static bool operator ==(NPath a, NPath b)
    {
        a.AssertValid();
        b.AssertValid();

        var n0 = a._node;
        var n1 = b._node;
        if (n0 == n1)
            return true;

        Validation.Assert(n0 != null || n1 != null);
        if (n0 == null)
            return false;
        if (n1 == null)
            return false;

        int dif = n0.NameCount - n1.NameCount;
        if (dif != 0)
            return false;

        for (; ; )
        {
            if (n0.GetHashCode() != n1.GetHashCode())
                return false;
            if (n0.Name != n1.Name)
                return false;
            n0 = n0.Parent;
            n1 = n1.Parent;
            if (n0 == n1)
                return true;
            Validation.Assert(n0 != null);
            Validation.Assert(n1 != null);
        }
    }

    public static bool operator !=(NPath a, NPath b)
    {
        return !(a == b);
    }

    public override int GetHashCode()
    {
        AssertValid();
        return _node == null ? Node.HashNull : _node.GetHashCode();
    }

    public bool Equals(NPath other)
    {
        return this == other;
    }

    public override bool Equals(object? obj)
    {
        AssertValid();
        Validation.BugCheckValueOrNull(obj);
        if (obj is not NPath other)
            return false;
        return this == other;
    }
}
