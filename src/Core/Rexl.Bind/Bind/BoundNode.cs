// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using DirTuple = Immutable.Array<Directive>;
using GlobalTable = ReadOnly.Dictionary<NPath, NPath>;
using IndexFlagsTuple = Immutable.Array<IndexFlags>;
using Integer = System.Numerics.BigInteger;
using NameTuple = Immutable.Array<DName>;
using ScopeMap = Dictionary<ArgScope, ArgScope>;
using ScopeTuple = Immutable.Array<ArgScope>;
using SliceTuple = Immutable.Array<SliceItemFlags>;

/// <summary>
/// Implements an immutable <see cref="DName"/> to <see cref="BoundNode"/> map. Primarily for
/// record and group-by nodes. There is no order associated with the items.
/// </summary>
public sealed class NamedItems : DNameRedBlackTree<NamedItems, BoundNode>
{
    public static readonly NamedItems Empty = new NamedItems(true, true);
    public static readonly NamedItems EmptyOptName = new NamedItems(false, true);
    public static readonly NamedItems EmptyOptNode = new NamedItems(true, false);
    public static readonly NamedItems EmptyOptBoth = new NamedItems(false, false);

    public bool NeedName { get; }
    public bool NeedNode { get; }

    private NamedItems(bool needName, bool needNode)
        : base(null)
    {
        NeedName = needName;
        NeedNode = needNode;
    }

    private NamedItems(Node root, NamedItems template)
        : base(root)
    {
        Validation.AssertValue(root);
        Validation.AssertValue(template);
        NeedName = template.NeedName;
        NeedNode = template.NeedNode;
    }

    private NamedItems GetEmpty()
    {
        if (NeedName)
            return NeedNode ? Empty : EmptyOptNode;
        return NeedNode ? EmptyOptName : EmptyOptBoth;
    }

    protected override bool KeyIsValid(DName key)
    {
        return key.IsValid || !NeedName;
    }

    protected override bool ValIsValid(BoundNode val)
    {
        return val != null || !NeedNode;
    }

    protected override bool ValEquals(BoundNode val0, BoundNode val1)
    {
        return val0 == val1;
    }

    protected override int ValHash(BoundNode val)
    {
        return val != null ? val.GetHashCode() : 0;
    }

    protected override NamedItems Wrap(Node root)
    {
        return root == _root ? this : root != null ? new NamedItems(root, this) : GetEmpty();
    }

    /// <summary>
    /// Return an instance of <see cref="NamedItems"/> based on this one with the given
    /// new <paramref name="values"/>. The <paramref name="values"/> should have
    /// length <see cref="Count"/> and be in the standard iteration order.
    /// </summary>
    public NamedItems MapValues(ReadOnly.Array<BoundNode> values)
    {
        Validation.BugCheckParam(values.Length == Count, nameof(values));
        int index = 0;
        var res = MapValuesCore(values, ref index);
        Validation.Assert(index == Count);
        return res;
    }

    /// <summary>
    /// Return an instance of <see cref="NamedItems"/> based on this one with the given
    /// new <paramref name="values"/> starting at <paramref name="index"/>. The length of
    /// <paramref name="values"/> should be at least <see cref="Count"/> plust the initial value
    /// of <paramref name="index"/>. On return <paramref name="index"/> will be increased by
    /// <see cref="Count"/>.
    /// </summary>
    public NamedItems MapValues(ReadOnly.Array<BoundNode> values, ref int index)
    {
        Validation.BugCheckParam(values.Length >= Count, nameof(values));
        Validation.BugCheckIndexInclusive(index, values.Length - Count, nameof(index));
        return MapValuesCore(values, ref index);
    }

    private NamedItems MapValuesCore(ReadOnly.Array<BoundNode> values, ref int index)
    {
        Validation.Assert(values.Length >= Count);
        Validation.AssertIndexInclusive(index, values.Length - Count);

        var items = this;
        foreach (var kvp in GetPairs())
        {
            var bndNew = values[index++];
            Validation.BugCheckParam(bndNew != null || !NeedNode, nameof(values));
            // REVIEW: It would be nice to avoid creating the outer wrapper over and over again.
            // That would be easy to do if we moved this to RedBlackTree.
            if (bndNew != kvp.val)
                items = items.SetItem(kvp.key, bndNew);
        }
        return items;
    }

    /// <summary>
    /// Returns whether any items contain volatile function invocations.
    /// </summary>
    public bool HasVolatile()
    {
        if (Count == 0)
            return false;
        foreach (var bnd in GetValues())
        {
            if (bnd.HasVolatile)
                return true;
        }
        return false;
    }
}

/// <summary>
/// Base class for all bound node classes.
/// Note that the only nodes that are allowed to be "shared" (have multiple parents)
/// are constant nodes. All others that need to be "shared" should be cloned.
/// </summary>
public abstract class BoundNode
{
    /// <summary>
    /// Interlock-incremented to assign <see cref="Ordinal"/>.
    /// </summary>
    private static long _ordAuto;

    /// <summary>
    /// This can be "updated" while building a module.
    /// REVIEW: Is there a better way?
    /// </summary>
    private DType _type;

    /// <summary>
    /// This is unique across the process, so nodes can be used as keys in red-black-trees.
    /// REVIEW: Is there a better way?
    /// </summary>
    internal long Ordinal { get; }

    /// <summary>
    /// The type of this bound node.
    /// </summary>
    public DType Type => _type;

    /// <summary>
    /// The total number of nodes in this tree.
    /// </summary>
    public abstract int NodeCount { get; }

    /// <summary>
    /// The number of (direct) children that this node has.
    /// </summary>
    public abstract int ChildCount { get; }

    /// <summary>
    /// The kind of bound node.
    /// </summary>
    public abstract BndNodeKind Kind { get; }

    /// <summary>
    /// Whether this node owns any scopes.
    /// </summary>
    public virtual bool OwnsScopes => false;

    /// <summary>
    /// This is the mask bit for the kind of this node, together with whether this node
    /// owns any scopes.
    /// </summary>
    public virtual BndNodeKindMask ThisKindMask => (BndNodeKindMask)(1ul << (int)Kind);

    /// <summary>
    /// This returns a bit-mask of the kinds of descendant nodes. This does not reflect the
    /// kind of this node. This includes impurity information.
    /// </summary>
    public abstract BndNodeKindMask ChildKinds { get; }

    /// <summary>
    /// This returns a bit-mask of the kinds of nodes in this tree, including the kind of this
    /// node and the kinds of all descendant nodes. This includes impurity information.
    /// </summary>
    public BndNodeKindMask AllKinds => ThisKindMask | ChildKinds;

    /// <summary>
    /// Whether this bound node contains any error bound nodes. If this returns false, it does NOT mean
    /// that no binding errors were were generated during binding, only that there are no error nodes
    /// in the bound tree.
    /// </summary>
    public bool HasErrors => (AllKinds & (BndNodeKindMask.Error | BndNodeKindMask.MissingValue)) != 0;

    /// <summary>
    /// Whether this node is itself an invocation of a procedure. This does not consider child nodes.
    /// For that, use <see cref="AllKinds"/>.
    /// </summary>
    public bool IsProcCall => Kind == BndNodeKind.CallProcedure;

    /// <summary>
    /// Whether the value of this node is influenced by any volatile calls.
    /// </summary>
    public virtual bool HasVolatile => (AllKinds & BndNodeKindMask.CallVolatile) != 0;

    /// <summary>
    /// Whether the value of this node is influenced by any volatile or procedure calls.
    /// </summary>
    public virtual bool IsImpure => (AllKinds & (BndNodeKindMask.CallVolatile | BndNodeKindMask.CallProcedure)) != 0;

    /// <summary>
    /// Return true for nodes that are cheap to compute, eg, constants, globals, scope references, etc.
    /// For nodes that are potentially expensive to compute, return false.
    /// </summary>
    public virtual bool IsCheap => false;

    /// <summary>
    /// Whether this is a constant value (of any type).
    /// </summary>
    public virtual bool IsConstant => false;

    /// <summary>
    /// Whether this is a constant value (of any type) that is not null.
    /// </summary>
    public virtual bool IsNonNullConstant => false;

    /// <summary>
    /// Whether this is a null value (of any type).
    /// </summary>
    public virtual bool IsNullValue => false;

    /// <summary>
    /// Whether this is a null value or of null type. This also returns <c>true</c> for sequences that
    /// are known to be empty (max count of zero).
    /// </summary>
    public bool IsKnownNull
    {
        get
        {
            if (IsNullValue)
                return true;
            if (Type.IsNull)
                return true;

            // Test for known empty sequence.
            if (!Type.IsSequence)
                return false;
            var (min, max) = GetItemCountRange();
            if (max == 0)
                return true;

            return false;
        }
    }

    private protected BoundNode(DType type)
    {
        Validation.Assert(type.IsValid);
        Ordinal = Interlocked.Increment(ref _ordAuto);
        _type = type;
    }

    private protected void UpdateType(DType typeNew)
    {
        Validation.Assert(Kind == BndNodeKind.ArgScopeRef);
        // This is currently only used for tuple types. Could also be used for record types if needed.
        Validation.Assert(_type.IsTupleReq);
        Validation.Assert(_type.IsPrefixTuple(typeNew));

        _type = typeNew;
    }

    /// <summary>
    /// If this is a constant of fractional type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetFractional(out double value)
    {
        value = default(double);
        return false;
    }

    /// <summary>
    /// If this is a constant of fractional-opt type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetFractionalOpt(out double? value)
    {
        value = null;
        return false;
    }

    /// <summary>
    /// If this is a constant of integral type that is accepted by I8, return true and fill in the value.
    /// </summary>
    public bool TryGetI8(out long value)
    {
        if (!TryGetIntegral(out var num) || !DType.I8Opt.Accepts(Type, DType.UseUnionDefault))
        {
            value = default;
            return false;
        }

        if (Type.RootKind.IsUx())
        {
            Validation.Assert(num >= 0 && num <= ulong.MaxValue);
            value = (long)(ulong)num;
        }
        else
        {
            Validation.Assert(num >= long.MinValue && num <= long.MaxValue);
            value = (long)num;
        }
        return true;
    }

    /// <summary>
    /// If this is a constant of integral type that is accepted by I8, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetI8Opt(out long? value)
    {
        if (!TryGetIntegralOpt(out var num) || !DType.I8Opt.Accepts(Type, DType.UseUnionDefault))
        {
            value = null;
            return false;
        }

        if (num == null)
        {
            value = null;
            return true;
        }

        var v = num.GetValueOrDefault();
        if (Type.RootKind.IsUx())
        {
            Validation.Assert(0 <= v && v <= ulong.MaxValue);
            value = (long)(ulong)v;
        }
        else
        {
            Validation.Assert(long.MinValue <= v && v <= long.MaxValue);
            value = (long)v;
        }
        return true;
    }

    /// <summary>
    /// If this is a constant of integral type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetIntegral(out Integer value)
    {
        value = default;
        return false;
    }

    /// <summary>
    /// If this is a constant of integral type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetIntegralOpt(out Integer? value)
    {
        value = null;
        return false;
    }

    /// <summary>
    /// If this is a constant of boolean type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetBool(out bool value)
    {
        value = default(bool);
        return false;
    }

    /// <summary>
    /// If this is a constant of boolean-opt type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetBoolOpt(out bool? value)
    {
        value = null;
        return false;
    }

    /// <summary>
    /// If this is a constant of text type, return true and fill in the value.
    /// </summary>
    public virtual bool TryGetString(out string value)
    {
        value = null;
        return false;
    }

    /// <summary>
    /// If the type is a sequence type return the min and max possible item counts.
    /// </summary>
    public virtual (long min, long max) GetItemCountRange()
    {
        // REVIEW: Ideally subclasses that may be sequence valued should override this,
        // so flow should only get here in the bug-check case.
        Validation.BugCheck(Type.IsSequence);
        return (0, long.MaxValue);
    }

    /// <summary>
    /// Accept the visitor. The return result is the node index of the next node (after this one and
    /// everything under it). This will be <paramref name="idx"/> plus <see cref="NodeCount"/>.
    /// </summary>
    public abstract int Accept(BoundTreeVisitor visitor, int idx);

    /// <summary>
    /// Determine whether this BoundNode is semantically equivalent to bnd, modulo
    /// the global substitution table. 
    /// 
    /// If a global in this BoundNode is inside the global table, it is used for
    /// comparison with bnd, otherwise the global's name is used. globalTable may be null.
    /// </summary>
    public bool Equivalent(BoundNode bnd, GlobalTable globalTable)
    {
        return Equivalent(bnd, null, globalTable);
    }

    /// <summary>
    /// Determine whether this BoundNode is semantically equivalent to bnd, modulo the
    /// provided Scope and global substitution table. Note that this is intended to be
    /// used only within the <see cref="BoundNode"/> world, but can't be protected since
    /// a node of one kind can call it on a node of another kind. Note also that while the
    /// globalTable is readonly, the scope map may be modified (additional scope equivalences
    /// may be added). Note that the scope map is not exposed to public clients, so this
    /// is acceptable.
    /// </summary>
    internal bool Equivalent(BoundNode bnd, ScopeMap scopeMap = default, GlobalTable globalTable = default)
    {
        if (bnd == this && scopeMap == null && globalTable.IsDefault)
            return true;
        if (bnd == null)
            return false;
        if (bnd.Kind != Kind)
            return false;
        if (bnd.Type != Type)
            return false;
        if (bnd.GetType() != GetType())
            return false;
        return Equiv(bnd, scopeMap, globalTable);
    }

    /// <summary>
    /// Called by the Equivalent function, which has already verified that the Kinds, Types, and classes are the same.
    /// </summary>
    private protected abstract bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable);

    /// <summary>
    /// Determine whether the given <see cref="NamedItems"/> instances are equivalent.
    /// </summary>
    private protected static bool EquivArgs(ArgTuple a, ArgTuple b, ScopeMap scopeMap, GlobalTable globalTable)
    {
        if (a.Length != b.Length)
            return false;
        if (a.Length == 0)
            return true;

        for (int i = 0; i < a.Length; i++)
        {
            var val0 = a[i];
            var val1 = b[i];
            if (val0 == null)
            {
                if (val1 != null)
                    return false;
            }
            else if (!val0.Equivalent(val1, scopeMap, globalTable))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Determine whether the given <see cref="NamedItems"/> instances are equivalent.
    /// </summary>
    private protected static bool EquivItems(NamedItems a, NamedItems b, ScopeMap scopeMap, GlobalTable globalTable)
    {
        if (a.Count != b.Count)
            return false;
        if (a.Count == 0)
            return true;

        using var ator0 = a.GetPairs().GetEnumerator();
        using var ator1 = b.GetPairs().GetEnumerator();
        for (; ; )
        {
            bool f0 = ator0.MoveNext();
            bool f1 = ator1.MoveNext();
            Validation.Assert(f0 == f1);
            if (!f0)
                break;
            var (name0, val0) = ator0.Current;
            var (name1, val1) = ator1.Current;
            if (name0 != name1)
                return false;
            if (val0 == null)
            {
                if (val1 != null)
                    return false;
            }
            else if (!val0.Equivalent(val1, scopeMap, globalTable))
                return false;
        }
        return true;
    }

    private protected static bool EquivNames(NameTuple a, NameTuple b)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Determine whether the scopes are equivalent, given the substitution table. Note that this
    /// function is NOT reflexive, since the table is directional. It is, however, transitive.
    /// </summary>
    private protected static bool EquivScopes(ArgScope a, ArgScope b, ScopeMap scopeMap)
    {
        if (a == b)
            return true;

        if (scopeMap != null)
        {
            while (scopeMap.TryGetValue(a, out var dst))
            {
                if (dst == b)
                    return true;
                if (dst == a)
                    return false;
                a = dst;
            }
        }

        return false;
    }

    public static int GetCount(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        return bnd.NodeCount;
    }

    public static int GetCount<TNode>(Immutable.Array<TNode> args)
        where TNode : BoundNode
    {
        Validation.Assert(!args.IsDefault);
        int count = 0;
        for (int i = 0; i < args.Length; i++)
        {
            var x = args[i];
            if (x != null)
                count += x.NodeCount;
        }
        return count;
    }

    private protected static int GetCount(NamedItems items)
    {
        Validation.AssertValue(items);
        int count = 0;
        foreach (var x in items.GetValues())
        {
            if (x != null)
                count += x.NodeCount;
        }
        return count;
    }

    public static BndNodeKindMask GetKinds(BoundNode bnd)
    {
        Validation.AssertValue(bnd);
        return bnd.AllKinds;
    }

    public static BndNodeKindMask GetKinds<TNode>(Immutable.Array<TNode> args)
        where TNode : BoundNode
    {
        Validation.Assert(!args.IsDefault);
        BndNodeKindMask kinds = 0;
        for (int i = 0; i < args.Length; i++)
        {
            var x = args[i];
            if (x != null)
                kinds |= x.AllKinds;
        }
        return kinds;
    }

    private protected static BndNodeKindMask GetKinds(NamedItems items)
    {
        Validation.AssertValue(items);
        BndNodeKindMask kinds = 0;
        foreach (var x in items.GetValues())
        {
            if (x != null)
                kinds |= x.AllKinds;
        }
        return kinds;
    }

    // Subclasses must implement this.
    public abstract override string ToString();
}

/// <summary>
/// Base class for bound node types that never have children.
/// </summary>
public abstract class BndLeafNode : BoundNode
{
    public sealed override int NodeCount => 1;
    public sealed override int ChildCount => 0;
    public sealed override BndNodeKindMask ChildKinds => 0;

    protected BndLeafNode(DType type)
        : base(type)
    {
    }
}

/// <summary>
/// Base class for bound node types that may have children.
/// </summary>
public abstract class BndParentNode : BoundNode
{
    public sealed override int NodeCount { get; }
    public sealed override int ChildCount { get; }
    public sealed override BndNodeKindMask ChildKinds { get; }

    protected BndParentNode(DType type, int childCount, BndNodeKindMask childKinds, int nodeCount)
        : base(type)
    {
        Validation.Assert(childCount >= 0);
        Validation.Assert(nodeCount >= childCount);
        ChildCount = childCount;
        ChildKinds = childKinds;
        NodeCount = nodeCount + 1;
    }

    /// <summary>
    /// Call accept on all the children. PreVisit has already been called on the parent
    /// and returned true. PostVisit will be called after this returns. The <paramref name="cur"/>
    /// value is the node index of the first child. The new "cur" value should be returned.
    /// </summary>
    protected abstract int AcceptChildren(BoundTreeVisitor visitor, int cur);

    // Parent nodes must use the BndNodePrinter. This is primarily so scopes are
    // rendered properly.
    public sealed override string ToString()
    {
        return BndNodePrinter.Run(this);
    }
}

/// <summary>
/// A bound node representing an error.
/// </summary>
public sealed partial class BndErrorNode : BndLeafNode
{
    /// <summary>
    /// The error.
    /// </summary>
    public RexlDiagnostic Error { get; }

    private BndErrorNode(RexlDiagnostic error, DType type)
        : base(type)
    {
        Validation.AssertValue(error);
        Validation.Assert(error.IsError);
        Error = error;
    }

    public static BndErrorNode Create(RexlDiagnostic error, DType? typeGuess = null)
    {
        Validation.BugCheckValue(error, nameof(error));
        Validation.BugCheckParam(error.IsError, nameof(error));
        return new BndErrorNode(error, typeGuess ?? DType.Vac);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        Validation.Assert(bnd is BndErrorNode);
        // Never consider two errors to be equivalent.
        return false;
    }

    public override string ToString()
    {
        return string.Format("Error({0})", Error.Message.Tag);
    }
}

/// <summary>
/// A namespace. It is an error if this isn't eliminated during binding.
/// </summary>
public sealed partial class BndNamespaceNode : BndLeafNode
{
    public NPath Path { get; }

    // REVIEW: What type should we use? There isn't a good choice. Will intellisense need
    // something distinctive?
    private BndNamespaceNode(NPath path)
        : base(DType.Vac)
    {
        Validation.Assert(!path.IsRoot);
        Path = path;
    }

    public static BndNamespaceNode Create(NPath path)
    {
        Validation.BugCheckParam(!path.IsRoot, nameof(path));
        return new BndNamespaceNode(path);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndNamespaceNode;
        Validation.Assert(other != null);
        return Path == other.Path;
    }

    public override string ToString()
    {
        return string.Format("NS<{0}>", Path.ToDottedSyntax());
    }
}

/// <summary>
/// This represents a missing function value, typically a function argument.
/// Clearly it is a kind of error.
/// </summary>
public sealed partial class BndMissingValueNode : BndLeafNode
{
    private BndMissingValueNode(DType type)
        : base(type)
    {
    }

    public static BndMissingValueNode Create(DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return new BndMissingValueNode(type);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        Validation.Assert(bnd is BndMissingValueNode);
        return Type == bnd.Type;
    }

    public override string ToString()
    {
        return Type.IsVac ? "<missing>" : string.Format("<missing>:{0}", Type);
    }
}

/// <summary>
/// Abstract base class for constant bound nodes.
/// </summary>
public abstract class BndConstNode : BndLeafNode
{
    private protected BndConstNode(DType type)
        : base(type)
    {
    }

    public sealed override bool IsConstant => true;

    public sealed override bool IsNonNullConstant => !IsNullValue;

    public sealed override bool IsCheap => true;
}

/// <summary>
/// This represents either a pure null (of type Null), or a null of any opt type.
/// REVIEW: Should we consider a typed null of a non-primitive type to
/// be a constant?
/// </summary>
public sealed partial class BndNullNode : BndConstNode
{
    public static readonly BndNullNode Null = new BndNullNode(DType.Null);
    public static readonly BndNullNode I8 = new BndNullNode(DType.I8Opt);

    private BndNullNode(DType type)
        : base(type)
    {
        Validation.Assert(Type.IsOpt);
    }

    /// <summary>
    /// Create a null of the given type. Note that the result is not necessarily a <see cref="BndNullNode"/>.
    /// </summary>
    public static BndConstNode Create(DType type)
    {
        Validation.BugCheckParam(type.IsOpt, nameof(type));

        if (type == DType.Null)
            return Null;
        if (type == DType.Text)
            return BndStrNode.Null;
        if (type == DType.I8Opt)
            return I8;
        return new BndNullNode(type);
    }

    public override bool IsNullValue => true;

    public override bool TryGetFractionalOpt(out double? value)
    {
        value = null;
        return Type.Kind.IsFractional();
    }

    public override bool TryGetIntegralOpt(out Integer? value)
    {
        value = null;
        return Type.Kind.IsIntegral();
    }

    public override bool TryGetBoolOpt(out bool? value)
    {
        value = null;
        return Type == DType.BitOpt;
    }

    public override (long min, long max) GetItemCountRange()
    {
        Validation.BugCheck(Type.IsSequence);
        return (0, 0);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        Validation.Assert(bnd is BndNullNode);
        return Type == bnd.Type;
    }

    public override string ToString()
    {
        return Type.IsNull ? "null" : string.Format("null:{0}", Type);
    }
}

/// <summary>
/// This represents the default value of a required non-numeric type.
/// </summary>
public sealed partial class BndDefaultNode : BndConstNode
{
    private BndDefaultNode(DType type)
        : base(type)
    {
        Validation.Assert(!Type.IsOpt);
        Validation.Assert(!Type.IsNumericXxx);
    }

    /// <summary>
    /// Creates a node representing the default of the given type. This method supports all valid
    /// types except Vac, which is disallowed because it has no valid value to default to.
    /// Note this may not return a <see cref="BndDefaultNode"/>.
    /// </summary>
    public static BndConstNode Create(DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (type.IsOpt)
            return BndNullNode.Create(type);
        if (type == DType.BitReq)
            return BndIntNode.False;
        if (type.IsIntegralXxx)
            return BndIntNode.Create(type, 0);
        if (type.IsFractionalXxx)
            return BndFltNode.Create(type, 0.0d);

        return new BndDefaultNode(type);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        Validation.Assert(bnd is BndDefaultNode);
        return Type == bnd.Type;
    }

    public override string ToString()
    {
        return string.Format("dflt<{0}>", Type);
    }
}

/// <summary>
/// Base class for a bound numeric node.
/// </summary>
public abstract class BndNumNode : BndConstNode
{
    private protected BndNumNode(DType type)
        : base(type)
    {
        Validation.Assert(type.IsNumericReq);
    }
}

/// <summary>
/// Represents a constant integral value.
/// Note that we try to avoid, but can't "outlaw", the "evil" values, namely 0x80 for i1,
/// 0x8000 for i2, etc. This is because the negation of them is (shockingly) equal to themselves.
/// </summary>
public sealed partial class BndIntNode : BndNumNode
{
    private static readonly Integer _valOnesS = -1;
    private static readonly Integer _valOnesBit = 0x01U;
    private static readonly Integer _valOnesU1 = 0xFFU;
    private static readonly Integer _valOnesU2 = 0xFFFFU;
    private static readonly Integer _valOnesU4 = 0xFFFF_FFFFU;
    private static readonly Integer _valOnesU8 = 0xFFFF_FFFF_FFFF_FFFFLU;

    public static Integer AllOnes(DKind kind)
    {
        Validation.BugCheckParam(kind.IsIntegral(), nameof(kind));

        switch (kind)
        {
        case DKind.Bit:
            return _valOnesBit;
        case DKind.U1:
            return _valOnesU1;
        case DKind.U2:
            return _valOnesU2;
        case DKind.U4:
            return _valOnesU4;
        case DKind.U8:
            return _valOnesU8;
        default:
            return _valOnesS;
        }
    }

    public static readonly BndIntNode True = new BndIntNode(DType.BitReq, 1);
    public static readonly BndIntNode False = new BndIntNode(DType.BitReq, 0);

    /// <summary>
    /// The integer value.
    /// </summary>
    public Integer Value { get; }

    private BndIntNode(DType type, Integer value)
        : base(type)
    {
#if DEBUG
        Validation.Assert(Type.IsIntegralReq);

        // Validate the consistency of the type and value.
        // Avoid the evil values (minimum values of signed types) whenever possible.
        // This is because, the negation of those values is (shockingly) equal to themselves!
        // They are still possible via integer literal constants, e.g. 0x80i1, so
        // we allow them here.
        switch (Type.Kind)
        {
        default:
            Validation.Assert(false);
            break;
        case DKind.IA:
            break;
        case DKind.I1:
            Validation.Assert(sbyte.MinValue <= value & value <= sbyte.MaxValue);
            break;
        case DKind.I2:
            Validation.Assert(short.MinValue <= value & value <= short.MaxValue);
            break;
        case DKind.I4:
            Validation.Assert(int.MinValue <= value & value <= int.MaxValue);
            break;
        case DKind.I8:
            Validation.Assert(long.MinValue <= value & value <= long.MaxValue);
            break;
        case DKind.Bit:
            Validation.Assert(value.IsZero | value.IsOne);
            break;
        case DKind.U1:
            Validation.Assert(0 <= value & value <= byte.MaxValue);
            break;
        case DKind.U2:
            Validation.Assert(0 <= value & value <= ushort.MaxValue);
            break;
        case DKind.U4:
            Validation.Assert(0 <= value & value <= uint.MaxValue);
            break;
        case DKind.U8:
            Validation.Assert(0 <= value & value <= ulong.MaxValue);
            break;
        }
#endif
        Value = value;
    }

    /// <summary>
    /// Create an integer constant node with the given kind, casting the <paramref name="value"/>
    /// if needed. Sets <paramref name="isChanged"/> to true if the original value was out of range.
    /// </summary>
    public static BndIntNode CreateCast(DType type, Integer value, out bool isChanged)
    {
        Validation.BugCheckParam(type.IsIntegralReq, nameof(type));

        DKind kind = type.Kind;
        Integer res;
        switch (kind)
        {
        case DKind.I1:
            res = value.CastI1();
            break;
        case DKind.I2:
            res = value.CastI2();
            break;
        case DKind.I4:
            res = value.CastI4();
            break;
        case DKind.I8:
            res = value.CastI8();
            break;
        case DKind.Bit:
            isChanged = !value.IsZero && !value.IsOne;
            return value.IsEven ? False : True;
        case DKind.U1:
            res = value.CastU1();
            break;
        case DKind.U2:
            res = value.CastU2();
            break;
        case DKind.U4:
            res = value.CastU4();
            break;
        case DKind.U8:
            res = value.CastU8();
            break;
        default:
            Validation.Assert(kind == DKind.IA);
            res = value;
            break;
        }

        isChanged = res != value;
        return new BndIntNode(type, res);
    }

    /// <summary>
    /// Create an integer constant node with the given kind. This throws if the value is out of range.
    /// </summary>
    public static BndIntNode Create(DType type, Integer value)
    {
        var bin = CreateCast(type, value, out bool isChanged);
        Validation.BugCheckParam(!isChanged, nameof(value));
        return bin;
    }

    public static BndIntNode CreateI(Integer value) { return new BndIntNode(DType.IAReq, value); }
    public static BndIntNode CreateI8(long value) { return new BndIntNode(DType.I8Req, value); }
    public static BndIntNode CreateI4(int value) { return new BndIntNode(DType.I4Req, value); }
    public static BndIntNode CreateI2(short value) { return new BndIntNode(DType.I2Req, value); }
    public static BndIntNode CreateI1(sbyte value) { return new BndIntNode(DType.I1Req, value); }
    public static BndIntNode CreateU8(ulong value) { return new BndIntNode(DType.U8Req, value); }
    public static BndIntNode CreateU4(uint value) { return new BndIntNode(DType.U4Req, value); }
    public static BndIntNode CreateU2(ushort value) { return new BndIntNode(DType.U2Req, value); }
    public static BndIntNode CreateU1(byte value) { return new BndIntNode(DType.U1Req, value); }
    public static BndIntNode CreateBit(bool value) { return value ? True : False; }

    /// <summary>
    /// Create an integer constant node with the given type. Asserts that the value
    /// is compatible with the given type.
    /// </summary>
    public static BndIntNode CreateAllOnes(DType type)
    {
        Validation.BugCheckParam(type.IsIntegralReq, nameof(type));
        return new BndIntNode(type, AllOnes(type.Kind));
    }

    public override bool TryGetBool(out bool value)
    {
        if (Type.RootKind != DKind.Bit)
            return base.TryGetBool(out value);
        value = !Value.IsZero;
        return true;
    }

    public override bool TryGetBoolOpt(out bool? value)
    {
        if (Type.RootKind != DKind.Bit)
            return base.TryGetBoolOpt(out value);
        value = !Value.IsZero;
        return true;
    }

    public override bool TryGetIntegral(out Integer value)
    {
        value = Value;
        return true;
    }

    public override bool TryGetIntegralOpt(out Integer? value)
    {
        value = Value;
        return true;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndIntNode;
        Validation.Assert(other != null);
        return Value == other.Value;
    }

    public override string ToString()
    {
        if (Type.RootKind == DKind.Bit)
            return Value.IsZero ? "false:b" : "true:b";
        return string.Format("{0}:{1}", Value, Type);
    }
}

/// <summary>
/// Represents a constant fractional value.
/// </summary>
public sealed partial class BndFltNode : BndNumNode
{
    // REVIEW: Ideally we would use Rational for this, perhaps eventually.

    /// <summary>
    /// The possibly null numeric value.
    /// </summary>
    public double Value { get; }

    private BndFltNode(DType type, double value)
        : base(type)
    {
#if DEBUG
        Validation.Assert(Type.IsFractionalReq);

        // Validate the consistency of the type and value.
        switch (Type.Kind)
        {
        default:
            Validation.Assert(false);
            break;
        case DKind.R8:
            break;
        case DKind.R4:
            var squashed = (double)(float)value;
            Validation.Assert(value.ToBits() == squashed.ToBits());
            break;
        }
#endif
        Value = value;
    }

    /// <summary>
    /// Create a fractional constant node with the given type. Checks that the value
    /// is compatible with the given type.
    /// </summary>
    public static BndFltNode Create(DType type, double value)
    {
        if (type == DType.R8Req)
            return new BndFltNode(type, value);
        Validation.BugCheckParam(type == DType.R4Req, nameof(type));
        var squashed = (double)(float)value;
        Validation.BugCheckParam(value.ToBits() == squashed.ToBits(), nameof(value));
        return new BndFltNode(type, value);
    }

    /// <summary>
    /// Create a fractional constant node with the given type. Casts the value to be
    /// compatible with the given type.
    /// </summary>
    public static BndFltNode CreateCast(DType type, double value)
    {
        if (type == DType.R8Req)
            return new BndFltNode(type, value);
        return new BndFltNode(type, (float)value);
    }

    /// <summary>
    /// Create a fractional constant of type R8.
    /// </summary>
    public static BndFltNode CreateR8(double value)
    {
        return new BndFltNode(DType.R8Req, value);
    }

    /// <summary>
    /// Create a fractional constant of type R4.
    /// </summary>
    public static BndFltNode CreateR4(float value)
    {
        return new BndFltNode(DType.R4Req, value);
    }

    public override bool TryGetFractional(out double value)
    {
        value = Value;
        return true;
    }

    public override bool TryGetFractionalOpt(out double? value)
    {
        value = Value;
        return true;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndFltNode;
        Validation.Assert(other != null);
        return Value.ToBits() == other.Value.ToBits();
    }

    public override string ToString()
    {
        // C# formats -0.0 as simply 0, so we must specifically check for it to render it properly.
        // We do this so that test baselines can show whether we have negative zero. Also, the default
        // rendering of infinite values is not consistent between windows and linux.
        return string.Concat(Value.ToStr(), ":", Type.ToString());
    }
}

/// <summary>
/// Represents a constant string value.
/// </summary>
public sealed partial class BndStrNode : BndConstNode
{
    public static readonly BndStrNode Null = new BndStrNode(null);
    public static readonly BndStrNode Empty = new BndStrNode("");

    /// <summary>
    /// The value.
    /// </summary>
    public string Value { get; }

    private BndStrNode(string value)
        : base(DType.Text)
    {
        Validation.AssertValueOrNull(Value);
        Value = value;
    }

    public static BndStrNode Create(string value)
    {
        Validation.BugCheckValueOrNull(value);
        return value != null ? new BndStrNode(value) : Null;
    }

    public override bool IsNullValue => Value == null;

    public override bool TryGetString(out string value)
    {
        value = Value;
        return true;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndStrNode;
        Validation.Assert(other != null);
        return Value == other.Value;
    }

    public override string ToString()
    {
        if (Value == null)
            return "str(<null>)";
        return string.Format("str({0})", Value);
    }
}

/// <summary>
/// Represents a compound constant value.
/// </summary>
public abstract partial class BndCmpConstNode : BndConstNode
{
    /// <summary>
    /// The value.
    /// </summary>
    public object Value { get; }

    protected BndCmpConstNode(DType type, object value)
        : base(type)
    {
        Validation.AssertValue(value);
        Value = value;
    }

    /// <summary>
    /// Rewrap with a reference compatible <see cref="DType"/>. For example, if the new <see cref="DType"/>
    /// is simply an opt version of the original, and the type is known to be a reference type (like record),
    /// this rewraps the contents with the updated <see cref="DType"/>.
    /// </summary>
    public abstract BoundNode MorphRefType(DType type);

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndCmpConstNode;
        Validation.Assert(other != null);
        // REVIEW: Any reason to try to compare?
        return other == this;
    }

    public override string ToString()
    {
        return string.Format("Compound<{0}>", Type);
    }
}

/// <summary>
/// Represents a reference to a global or "this".
/// </summary>
public abstract class BndGlobalBaseNode : BndLeafNode
{
    public abstract NPath FullName { get; }

    public sealed override bool IsCheap => true;

    private protected BndGlobalBaseNode(DType type)
        : base(type)
    {
    }
}

/// <summary>
/// Represents a reference to the "this" value.
/// </summary>
public sealed partial class BndThisNode : BndGlobalBaseNode
{
    public override NPath FullName => NPath.Root;

    private BndThisNode(DType type)
        : base(type)
    {
    }

    public static BndThisNode Create(DType type)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return new BndThisNode(type);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        Validation.Assert(bnd is BndThisNode);
        Validation.Assert(bnd.Type == Type);
        return true;
    }

    public override string ToString()
    {
        return "<this>";
    }
}

/// <summary>
/// Represents a reference to a global.
/// </summary>
public sealed partial class BndGlobalNode : BndGlobalBaseNode
{
    public override NPath FullName { get; }

    private BndGlobalNode(NPath full, DType type)
        : base(type)
    {
        Validation.Assert(!full.IsRoot);
        FullName = full;
    }

    public static BndGlobalNode Create(NPath full, DType type)
    {
        Validation.BugCheckParam(!full.IsRoot, nameof(full));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return new BndGlobalNode(full, type);
    }

    public override (long min, long max) GetItemCountRange()
    {
        Validation.BugCheck(Type.IsSequence);
        return (0, long.MaxValue);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndGlobalNode;
        Validation.Assert(other != null);
        if (globalTable.TryGetValue(FullName, out var nameTmp))
            return nameTmp == other.FullName;
        return FullName == other.FullName;
    }

    public override string ToString()
    {
        return string.Format("Global({0})", FullName.ToDottedSyntax());
    }
}

/// <summary>
/// Reference to a scope value.
/// </summary>
public abstract partial class BndScopeRefNode : BndLeafNode
{
    /// <summary>
    /// The target scope.
    /// </summary>
    public ArgScope Scope { get; }

    private protected BndScopeRefNode(ArgScope scope)
        : base(scope.VerifyValue().Type)
    {
        Validation.AssertValue(scope);
        Scope = scope;
    }

    public static BndScopeRefNode Create(ArgScope scope)
    {
        Validation.BugCheckValue(scope, nameof(scope));
        // scope contains a singleton ref node.
        return scope.GetReference();
    }

    public override bool IsCheap => true;

    public override (long min, long max) GetItemCountRange()
    {
        // REVIEW: Perhaps we should be able to track the heritage to
        // get an accurate count range? At least in the case of With scopes.
        Validation.BugCheck(Type.IsSequence);
        return (0, long.MaxValue);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndScopeRefNode;
        Validation.Assert(other != null);
        return EquivScopes(Scope, other.Scope, scopeMap);
    }

    public sealed override string ToString()
    {
        return BndNodePrinter.Run(this);
    }
}

/// <summary>
/// For a field of anything except this[.parent]*.
/// </summary>
public sealed partial class BndGetFieldNode : BndParentNode
{
    public override bool IsCheap => Record.IsCheap;

    /// <summary>
    /// The field name.
    /// </summary>
    public DName Name { get; }

    /// <summary>
    /// The record containing the field.
    /// </summary>
    public BoundNode Record { get; }

    private BndGetFieldNode(DName name, BoundNode record, DType type)
        : base(type, 1, GetKinds(record), GetCount(record))
    {
        Validation.Assert(name.IsValid);
        Validation.AssertValue(record);
        Validation.Assert(record.Type.IsRecordReq);
        Validation.Assert(!(record is BndRecordNode));
        Name = name;
        Record = record;
    }

    public static BoundNode Create(DName name, BoundNode record)
    {
        Validation.BugCheckParam(name.IsValid, nameof(name));
        Validation.BugCheckValue(record, nameof(record));
        Validation.BugCheckParam(record.Type.IsRecordReq, nameof(record));
        Validation.BugCheckParam(record.Type.TryGetNameType(name, out var typeFld), nameof(name));
        if (record is BndDefaultNode)
            return BndDefaultNode.Create(typeFld);
        if (record is BndRecordNode brn)
        {
            if (brn.Items.TryGetValue(name, out var value))
            {
                Validation.Assert(value.Type == typeFld);
                return value;
            }
            return BndDefaultNode.Create(typeFld);
        }
        return new BndGetFieldNode(name, record, typeFld);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        return Record.Accept(visitor, cur);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndGetFieldNode;
        Validation.Assert(other != null);
        if (Name != other.Name)
            return false;
        return Record.Equivalent(other.Record, scopeMap, globalTable);
    }

    /// <summary>
    /// Determines whether the given <paramref name="bnd"/> is a field access of the indicated
    /// <paramref name="scope"/>, whether direct or "guarded". When it is, this sets
    /// <paramref name="fld"/> to the field name and returns true.
    /// </summary>
    public static bool IsScopeField(BoundNode bnd, ArgScope scope, out DName fld)
    {
        Validation.AssertValue(bnd);
        Validation.AssertValue(scope);

        // If this is a guard on a scope ref, extract (and test below) the inner value and scope.
        if (bnd is BndCallNode bcn)
        {
            if (bcn.Oper == WithFunc.Guard && bcn.Args.Length == 2 && bcn.Scopes.Length == 1 &&
                bcn.Args[0] is BndScopeRefNode bsr && bsr.Scope == scope)
            {
                bnd = bcn.Args[1];
                scope = bcn.Scopes[0];
            }
        }

        if (bnd is BndGetFieldNode bgf)
        {
            if (bgf.Record is BndScopeRefNode bsr && bsr.Scope == scope)
            {
                fld = bgf.Name;
                return true;
            }
        }

        fld = default(DName);
        return false;
    }
}

/// <summary>
/// Get a static slot from a tuple.
/// </summary>
public sealed partial class BndGetSlotNode : BndParentNode
{
    // This is critical for symbol reduction.
    public override bool IsCheap => Tuple.IsCheap;

    /// <summary>
    /// The slot index.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// The tuple containing the slot.
    /// </summary>
    public BoundNode Tuple { get; }

    private BndGetSlotNode(int slot, BoundNode tuple, DType type)
        : base(type, 1, GetKinds(tuple), GetCount(tuple))
    {
        Validation.AssertValue(tuple);
        Validation.Assert(tuple.Type.IsTupleReq);
        Validation.Assert(!(tuple is BndTupleNode));
        Validation.AssertIndex(slot, tuple.Type.TupleArity);
        Validation.Assert(type == tuple.Type.GetTupleSlotTypes()[slot]);
        Slot = slot;
        Tuple = tuple;
    }

    public static BoundNode Create(int slot, BoundNode tuple)
    {
        Validation.BugCheckValue(tuple, nameof(tuple));
        Validation.BugCheckParam(tuple.Type.IsTupleReq, nameof(tuple));
        var types = tuple.Type.GetTupleSlotTypes();
        Validation.BugCheckIndex(slot, types.Length, nameof(slot));
        if (tuple is BndDefaultNode)
            return BndDefaultNode.Create(types[slot]);
        if (tuple is BndTupleNode btn)
        {
            Validation.Assert(btn.Items[slot].Type == types[slot]);
            return btn.Items[slot];
        }
        return new BndGetSlotNode(slot, tuple, types[slot]);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        return Tuple.Accept(visitor, cur);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndGetSlotNode;
        Validation.Assert(other != null);
        if (Slot != other.Slot)
            return false;
        return Tuple.Equivalent(other.Tuple, scopeMap, globalTable);
    }
}

/// <summary>
/// Index into a text value.
/// </summary>
public sealed partial class BndIdxTextNode : BndParentNode
{
    /// <summary>
    /// The source text.
    /// </summary>
    public BoundNode Text { get; }

    /// <summary>
    /// The index value, separate from the <see cref="Modifier"/>.
    /// </summary>
    public BoundNode Index { get; }

    /// <summary>
    /// The index modifier, whether normal, relative to the end, wrapping, clipping, etc.
    /// </summary>
    public IndexFlags Modifier { get; }

    private BndIdxTextNode(BoundNode text, BoundNode index, IndexFlags mod)
        : base(DType.U2Req, 2,
              GetKinds(text) | GetKinds(index),
              GetCount(text) + GetCount(index))
    {
        Validation.Assert(text.Type == DType.Text);
        Validation.Assert(index.Type == DType.I8Req, nameof(index));
        Validation.Assert(mod.IsValid());

        Text = text;
        Index = index;
        Modifier = mod;
    }

    /// <summary>
    /// Creates a node to index into the given text value.
    /// </summary>
    public static BoundNode Create(BoundNode text, BoundNode index, IndexFlags mod)
    {
        Validation.BugCheckValue(text, nameof(text));
        Validation.BugCheckParam(text.Type == DType.Text, nameof(text));
        Validation.BugCheckValue(index, nameof(index));
        Validation.BugCheckParam(index.Type == DType.I8Req, nameof(index));
        Validation.BugCheckParam(mod.IsValid(), nameof(mod));

        if (text.TryGetString(out var src))
        {
            if (string.IsNullOrEmpty(src))
                return BndIntNode.CreateU2(0);
            if (index.TryGetI8(out long raw))
            {
                long idx = mod.AdjustIndex(raw, src.Length);
                if ((ulong)idx >= (ulong)src.Length)
                    return BndIntNode.CreateU2(0);
                return BndIntNode.CreateU2((ushort)src[(int)idx]);
            }
        }

        return new BndIdxTextNode(text, index, mod);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Text.Accept(visitor, cur);
        cur = Index.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndIdxTextNode;
        Validation.Assert(other != null);
        if (other.Modifier != Modifier)
            return false;
        if (!Text.Equivalent(other.Text, scopeMap, globalTable))
            return false;
        if (!Index.Equivalent(other.Index, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// Get an item by indexing into a tensor.
/// </summary>
public sealed partial class BndIdxTensorNode : BndParentNode
{
    /// <summary>
    /// The source tensor.
    /// </summary>
    public BoundNode Tensor { get; }

    /// <summary>
    /// The indices.
    /// </summary>
    public ArgTuple Indices { get; }

    /// <summary>
    /// The index modifiers. This is parallel to <see cref="Indices"/>.
    /// </summary>
    public IndexFlagsTuple Modifiers { get; }

    private BndIdxTensorNode(
            BoundNode tensor, ArgTuple indices, IndexFlagsTuple modifiers, DType type)
        : base(type, 1 + indices.Length,
              GetKinds(tensor) | GetKinds(indices),
              GetCount(tensor) + GetCount(indices))
    {
        Validation.AssertValue(tensor);
        Validation.Assert(tensor.Type.IsTensorReq);
        Validation.Assert(indices.Length == tensor.Type.TensorRank);
        Validation.Assert(type == tensor.Type.GetTensorItemType());
        Validation.Assert(indices.All(i => i.Type == DType.I8Req));
        Validation.Assert(modifiers.Length == tensor.Type.TensorRank);
        Tensor = tensor;
        Indices = indices;
        Modifiers = modifiers;
    }

    public static BndIdxTensorNode Create(BoundNode tensor, ArgTuple indices, IndexFlagsTuple modifiers)
    {
        Validation.BugCheckValue(tensor, nameof(tensor));
        Validation.BugCheckParam(tensor.Type.IsTensorReq, nameof(tensor));
        Validation.BugCheckParam(!indices.IsDefault, nameof(indices));
        Validation.BugCheckParam(indices.Length == tensor.Type.TensorRank, nameof(indices));
        Validation.BugCheckParam(indices.All(i => i.Type == DType.I8Req), nameof(indices));
        Validation.BugCheckParam(!modifiers.IsDefault, nameof(modifiers));
        Validation.BugCheckParam(modifiers.Length == tensor.Type.TensorRank, nameof(modifiers));
        Validation.BugCheckParam(modifiers.All(m => m.IsValid()), nameof(modifiers));

        return new BndIdxTensorNode(tensor, indices, modifiers, tensor.Type.GetTensorItemType());
    }

    public BndIdxTensorNode SetChildren(BoundNode tensor, ArgTuple indices)
    {
        Validation.BugCheckValue(tensor, nameof(tensor));
        Validation.BugCheckParam(tensor.Type == Tensor.Type, nameof(tensor));
        Validation.BugCheckParam(!indices.IsDefault, nameof(indices));
        Validation.BugCheckParam(indices.Length == Tensor.Type.TensorRank, nameof(indices));
        Validation.BugCheckParam(indices.All(i => i.Type == DType.I8Req), nameof(indices));

        return new BndIdxTensorNode(tensor, indices, Modifiers, Type);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Tensor.Accept(visitor, cur);
        foreach (var ind in Indices)
            cur = ind.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndIdxTensorNode;
        Validation.Assert(other != null);
        for (int i = 0; i < Modifiers.Length; i++)
        {
            if (Modifiers[i] != other.Modifiers[i])
                return false;
        }
        if (!Tensor.Equivalent(other.Tensor, scopeMap, globalTable))
            return false;
        if (!EquivArgs(Indices, other.Indices, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// Get an item by dynamically indexing into a homogeneous tuple.
/// </summary>
public sealed partial class BndIdxHomTupNode : BndParentNode
{
    /// <summary>
    /// The source homogeneous tuple.
    /// </summary>
    public BoundNode Tuple { get; }

    /// <summary>
    /// The index value, separate from the <see cref="Modifier"/>. Note that when an index is constant,
    /// the indexing operation is represented by <see cref="BndGetSlotNode"/> rather than this class.
    /// </summary>
    public BoundNode Index { get; }

    /// <summary>
    /// The index modifier, whether normal, relative to the end, wrapping, clipping, etc.
    /// </summary>
    public IndexFlags Modifier { get; }

    private BndIdxHomTupNode(BoundNode tuple, BoundNode index, IndexFlags mod, DType type)
        : base(type, 2,
              GetKinds(tuple) | GetKinds(index),
              GetCount(tuple) + GetCount(index))
    {
        var tupType = tuple.Type;
        Validation.Assert(tupType.IsHomTuple());
        Validation.Assert(tupType.TupleArity > 0);
        Validation.Assert(index.Type == DType.I8Req, nameof(index));
        Validation.Assert(!index.IsConstant);
        Validation.Assert(mod.IsValid());

        Tuple = tuple;
        Index = index;
        Modifier = mod;
    }

    /// <summary>
    /// Creates a node to index into the given homogeneous tuple. Note this may not be
    /// a <see cref="BndIdxHomTupNode"/>. Sets <paramref name="outOfRange"/> to true
    /// if a constant index was found that isn't a valid slot for the tuple's arity.
    /// The caller should warn in this case.
    /// </summary>
    public static BoundNode Create(BoundNode tuple, BoundNode index, IndexFlags mod, out bool outOfRange)
    {
        Validation.BugCheckValue(tuple, nameof(tuple));
        Validation.BugCheckParam(tuple.Type.IsHomTuple(out var typeItem), nameof(tuple));
        int arity = tuple.Type.TupleArity;
        Validation.BugCheckParam(arity > 0, nameof(tuple));
        Validation.BugCheckValue(index, nameof(index));
        Validation.BugCheckParam(index.Type == DType.I8Req, nameof(index));
        Validation.BugCheckParam(mod.IsValid(), nameof(mod));

        bool constSlot = index.TryGetI8(out long slot);
        if (constSlot)
        {
            slot = mod.AdjustIndex(slot, arity);
            outOfRange = !Validation.IsValidIndex(slot, arity);
            if (!outOfRange)
                return BndGetSlotNode.Create((int)slot, tuple);
            return BndDefaultNode.Create(typeItem);
        }

        outOfRange = false;
        if (tuple is BndDefaultNode)
            return BndDefaultNode.Create(typeItem);
        return new BndIdxHomTupNode(tuple, index, mod, typeItem);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Tuple.Accept(visitor, cur);
        cur = Index.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndIdxHomTupNode;
        Validation.Assert(other != null);
        if (other.Modifier != Modifier)
            return false;
        if (!Tuple.Equivalent(other.Tuple, scopeMap, globalTable))
            return false;
        if (!Index.Equivalent(other.Index, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// Specifies how to deal with an index, whether it is from zero or from the back, and what to do
/// when the value is out of bounds, either less than zero or greater than or equal to the size.
/// </summary>
public enum IndexFlags : byte
{
    /// <summary>
    /// Normal index with out of bounds resulting in the default value.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Index is a negative offset from the size.
    /// </summary>
    Back = 0x01,

    /// <summary>
    /// Wrap around, ie, mod by the size.
    /// </summary>
    Wrap = 0x02,

    /// <summary>
    /// Clip to the valid indices range.
    /// </summary>
    Clip = 0x04,

    /// <summary>
    /// Combination of both <see cref="Back"/> and <see cref="Wrap"/>.
    /// </summary>
    BackWrap = Back | Wrap,

    /// <summary>
    /// Combination of both <see cref="Back"/> and <see cref="Clip"/>.
    /// </summary>
    BackClip = Back | Clip,
}

/// <summary>
/// Contains information for a tensor slice item. Everything is squeezed into a byte.
/// Note that this "extends" the <see cref="IndexFlags"/> enum - using the same values
/// for corresponding concepts.
/// </summary>
[Flags]
public enum SliceItemFlags : byte
{
    /// <summary>
    /// The default is an index (not a triple or tuple range).
    /// </summary>
    Index = 0x00,

    /// <summary>
    /// When neither <see cref="Range"/> nor <see cref="Tuple"/> is set, this flag indicates whether
    /// the index has the "back" modifier, meaning that the index should be subtracted from the length.
    /// </summary>
    IndexBack = IndexFlags.Back,

    /// <summary>
    /// When neither <see cref="Range"/> nor <see cref="Tuple"/> is set, this flag indicates whether
    /// the index has the "wrap" modifier, meaning that the index should wrap around, that is be mod'ed
    /// by the length.
    /// </summary>
    IndexWrap = IndexFlags.Wrap,

    /// <summary>
    /// When neither <see cref="Range"/> nor <see cref="Tuple"/> is set, this flag indicates whether
    /// the index has the "clip" modifier, meaning that the index should clipped to be in range.
    /// </summary>
    IndexClip = IndexFlags.Clip,

    /// <summary>
    ///  When <see cref="Range"/> is set, this flag indicates whether the range has a start value.
    /// </summary>
    Start = 0x08,

    /// <summary>
    ///  When <see cref="Range"/> is set, this flag indicates whether the range has a stop value.
    /// </summary>
    Stop = 0x10,

    /// <summary>
    ///  When <see cref="Range"/> is set, this flag indicates whether the range has a step value.
    /// </summary>
    Step = 0x20,

    /// <summary>
    /// Whether the item is a slice specified by three optional values: start, stop, step.
    /// </summary>
    Range = 0x40,

    /// <summary>
    /// Whether the item is a slice specified by a tuple. The tuple can either have 3 slots,
    /// each of type I8 or I8?, indicating start, stop, step values, or can have 5 slots, alternating
    /// start, stop, step, with bool "back" indicators (for start and stop).
    /// </summary>
    Tuple = 0x80,

    /// <summary>
    /// When the item is a range with a start value, this indicates whether the start value has
    /// the "back" modifier.
    /// </summary>
    StartBack = IndexFlags.Back,

    /// <summary>
    /// When the item is a range with a stop value, this indicates whether the stop value has
    /// the "back" modifier.
    /// </summary>
    StopBack = IndexFlags.Wrap,

    /// <summary>
    /// When the item is a range with a stop value, this indicates whether the stop value has
    /// the "star" modifier, meaning that it indicates a count rather than position.
    /// </summary>
    StopStar = IndexFlags.Clip,
}

/// <summary>
/// Utilities for indexing and slicing.
/// </summary>
public static class SliceUtils
{
    /// <summary>
    /// Flags specifing index out of bounds behavior.
    /// </summary>
    private const IndexFlags k_indexOob = IndexFlags.Wrap | IndexFlags.Clip;

    /// <summary>
    /// All index modifier flags.
    /// </summary>
    private const IndexFlags k_indexMod = IndexFlags.Back | k_indexOob;

    /// <summary>
    /// All tuple flags.
    /// </summary>
    private const SliceItemFlags k_tupleAll = SliceItemFlags.Tuple;

    /// <summary>
    /// All range flags.
    /// </summary>
    private const SliceItemFlags k_rangeAll = SliceItemFlags.Range |
        SliceItemFlags.Start | SliceItemFlags.Stop | SliceItemFlags.Step |
        SliceItemFlags.StartBack | SliceItemFlags.StopBack | SliceItemFlags.StopStar;

    /// <summary>
    /// Test validity of the <see cref="IndexFlags"/> value.
    /// </summary>
    public static bool IsValid(this IndexFlags flags)
    {
        if ((flags & ~k_indexMod) != 0)
            return false;
        return true;
    }

    /// <summary>
    /// Test validity of the <see cref="SliceItemFlags"/> value.
    /// </summary>
    public static bool IsValid(this SliceItemFlags flags)
    {
        // Handle range.
        if ((flags & SliceItemFlags.Range) != 0)
        {
            if ((flags & ~k_rangeAll) != 0)
                return false;
            // Shouldn't have XxxBack or XxxTime if Xxx isn't set.
            if ((flags & (SliceItemFlags.Start | SliceItemFlags.StartBack)) == SliceItemFlags.StartBack)
                return false;
            if ((flags & (SliceItemFlags.StopBack | SliceItemFlags.StopStar)) != 0 &&
                (flags & SliceItemFlags.Stop) == 0)
            {
                return false;
            }
            return true;
        }

        // Handle tuple.
        if ((flags & SliceItemFlags.Tuple) != 0)
            return (flags & ~k_tupleAll) == 0;

        // Handle index.
        return ((IndexFlags)flags).IsValid();
    }

    /// <summary>
    /// Converts the <see cref="IndexFlags"/> value to the corresponding <see cref="SliceItemFlags"/>
    /// value.
    /// </summary>
    public static SliceItemFlags ToSliceFlags(this IndexFlags flags)
    {
        Validation.Assert(flags.IsValid());
        return (SliceItemFlags)flags;
    }

    /// <summary>
    /// Assert that the <see cref="SliceItemFlags"/> value is for an index and converts it the
    /// corresponding <see cref="IndexFlags"/> value.
    /// </summary>
    public static IndexFlags ToIndexFlags(this SliceItemFlags flags)
    {
        Validation.Assert(flags.IsIndex());
        return (IndexFlags)flags;
    }

    /// <summary>
    /// Returns the number of values that an item with the given <paramref name="flags"/> uses.
    /// </summary>
    public static int GetCount(this SliceItemFlags flags)
    {
        if (!flags.IsRangeSlice())
            return 1;
        int count = 0;
        if ((flags & SliceItemFlags.Start) != 0)
            count++;
        if ((flags & SliceItemFlags.Stop) != 0)
            count++;
        if ((flags & SliceItemFlags.Step) != 0)
            count++;
        return count;
    }

    /// <summary>
    /// Tests whether the <paramref name="flags"/> specifies a range slice item.
    /// </summary>
    public static bool IsRangeSlice(this SliceItemFlags flags)
    {
        Validation.Assert(flags.IsValid());
        return (flags & SliceItemFlags.Range) != 0;
    }

    /// <summary>
    /// Tests whether the <paramref name="flags"/> specifies a tuple slice item.
    /// </summary>
    public static bool IsTupleSlice(this SliceItemFlags flags)
    {
        Validation.Assert(flags.IsValid());
        return (flags & SliceItemFlags.Tuple) != 0;
    }

    /// <summary>
    /// Tests whether the <paramref name="flags"/> specifies an index slice item.
    /// </summary>
    public static bool IsIndex(this SliceItemFlags flags)
    {
        Validation.Assert(flags.IsValid());
        return (flags & (SliceItemFlags.Tuple | SliceItemFlags.Range)) == 0;
    }

    /// <summary>
    /// Tests whether the <paramref name="flags"/> contains index modifiers.
    /// </summary>
    public static bool HasIndexMods(this IndexFlags flags)
    {
        Validation.Assert(flags.IsValid());
        return (flags & k_indexMod) != 0;
    }

    /// <summary>
    /// Asserts that <paramref name="flags"/> indicates an index slice item and tests
    /// whether it contains index modifiers.
    /// </summary>
    public static bool HasIndexMods(this SliceItemFlags flags)
    {
        Validation.Assert(flags.IsIndex());
        return ((IndexFlags)flags).HasIndexMods();
    }

    /// <summary>
    /// Given an <paramref name="index"/>, <paramref name="mod"/>, and the <paramref name="size"/>
    /// of the dimension, return the result "net" index.
    /// </summary>
    public static long AdjustIndex(this IndexFlags flags, long index, long size)
    {
        Validation.Assert(flags.IsValid());

        switch (flags)
        {
        case 0:
            return index;
        case IndexFlags.Back:
            return size - index;
        case IndexFlags.Wrap:
            if (size == 0)
                return 0;
            index %= size;
            if (index < 0)
                index += size;
            Validation.AssertIndex(index, size);
            return index;
        case IndexFlags.BackWrap:
            index = size - index;
            goto case IndexFlags.Wrap;
        case IndexFlags.Clip:
            if (index >= size)
                index = size - 1;
            if (index < 0)
                index = 0;
            return index;
        case IndexFlags.BackClip:
            index = size - index;
            goto case IndexFlags.Clip;

        default:
            Validation.Assert(false);
            return index;
        }
    }

    /// <summary>
    /// Returns whether the given range values indciate a full range. If any of the values are
    /// bound null values, they are set to reference <c>null</c>.
    /// </summary>
    public static bool IsFull(ref BoundNode start, bool backStart,
        ref BoundNode stop, bool backStop, bool starStop, ref BoundNode step)
    {
        Integer c;

        if (start != null && start.IsNullValue)
            start = null;
        if (stop != null && stop.IsNullValue)
            stop = null;
        if (step != null && step.IsNullValue)
            step = null;

        if (start != null)
        {
            if (backStart)
                return false;
            if (!start.TryGetIntegral(out c))
                return false;
            if (c > 0)
                return false;
        }

        if (stop != null)
        {
            // The starStop value is irrelevant. In either case, backStop must be true and the
            // value must be zero.
            if (!backStop)
                return false;
            if (!stop.TryGetIntegral(out c))
                return false;
            if (c > 0)
                return false;
        }

        if (step != null)
        {
            if (!step.TryGetIntegral(out c))
                return false;
            if (c < 0)
                return false;
            if (c > 1)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Adds the values for a range to the given builder (if provided) and returns the corresponding
    /// <see cref="SliceItemFlags"/>.
    /// </summary>
    public static SliceItemFlags Add(ArgTuple.Builder values, BoundNode start, BoundNode stop, BoundNode step)
    {
        return Add(values, start, false, stop, false, false, step);
    }

    /// <summary>
    /// Adds the values for a range to the given builder (if provided) and returns the corresponding
    /// <see cref="SliceItemFlags"/>.
    /// </summary>
    public static SliceItemFlags Add(ArgTuple.Builder values, BoundNode start, bool back1,
        BoundNode stop, bool back2, bool star2, BoundNode step)
    {
        Validation.BugCheckValueOrNull(values);
        Validation.BugCheckParam(start == null || start.Type.Kind == DKind.I8, nameof(start));
        Validation.BugCheckParam(stop == null || stop.Type.Kind == DKind.I8, nameof(stop));
        Validation.BugCheckParam(step == null || step.Type.Kind == DKind.I8, nameof(step));

        var flags = SliceItemFlags.Range;
        if (IsFull(ref start, back1, ref stop, back2, star2, ref step))
            return flags;

        if (start != null && !start.IsNullValue)
        {
            if (start is BndCastOptNode copt)
                start = copt.Child;
            values?.Add(start);
            flags |= SliceItemFlags.Start;
            if (back1)
                flags |= SliceItemFlags.StartBack;
        }
        if (stop != null && !stop.IsNullValue)
        {
            if (stop is BndCastOptNode copt)
                stop = copt.Child;
            values?.Add(stop);
            flags |= SliceItemFlags.Stop;
            if (back2)
                flags |= SliceItemFlags.StopBack;
            if (star2)
                flags |= SliceItemFlags.StopStar;
        }
        if (step != null && !step.IsNullValue)
        {
            if (step is BndCastOptNode copt)
                step = copt.Child;
            values?.Add(step);
            flags |= SliceItemFlags.Step;
        }
        Validation.Assert(flags.IsRangeSlice());

        return flags;
    }
}

/// <summary>
/// Represents slicing of text.
/// </summary>
public sealed partial class BndTextSliceNode : BndParentNode
{
    /// <summary>
    /// The source text.
    /// </summary>
    public BoundNode Text { get; }

    /// <summary>
    /// The item flags, at least one of which will be a slice. The length of this is the
    /// same as the rank of the input <see cref="Tensor"/>. This is never an "index" slice.
    /// </summary>
    public SliceItemFlags Item { get; }

    /// <summary>
    /// The values needed for slicing. These are "referenced" by <see cref="Item"/>. Note
    /// that a range item can use from zero to three values.
    /// </summary>
    public ArgTuple Values { get; }

    private BndTextSliceNode(BoundNode text, SliceItemFlags item, ArgTuple values)
        : base(DType.Text, values.Length + 1,
              GetKinds(text) | GetKinds(values),
              GetCount(text) + GetCount(values))
    {
        Validation.AssertValue(text);
        Validation.Assert(text.Type == DType.Text);
        // Shouldn't be all indices.
        Text = text;
        Item = item;
        Values = values;
    }

    /// <summary>
    /// Creates a node representing the given <paramref name="text"/> sliced according
    /// to <paramref name="item"/> and <paramref name="values"/>. May reduce to a constant
    /// result.
    /// </summary>
    public static BoundNode Create(BoundNode text, SliceItemFlags item, ArgTuple values)
    {
        Validation.BugCheckValue(text, nameof(text));
        Validation.BugCheckParam(text.Type == DType.Text, nameof(text));
        Validation.BugCheckParam(item.IsValid(), nameof(item));
        Validation.BugCheckParam(!values.IsDefault, nameof(values));

        if (item.IsTupleSlice())
        {
            Validation.BugCheckParam(values.Length == 1, nameof(values));
            Validation.BugCheckParam(values[0] != null, nameof(values));
            var typeTup = values[0].Type;
            Validation.BugCheckParam(typeTup.IsTupleReq, nameof(values));
            bool backs = typeTup.TupleArity > 3;
            Validation.BugCheckParam(typeTup.TupleArity == (backs ? 5 : 3), nameof(values));
            int slot = 0;
            var types = typeTup.GetTupleSlotTypes();
            Validation.BugCheckParam(types[slot++].Kind == DKind.I8, nameof(values));
            if (backs)
                Validation.BugCheckParam(types[slot++] == DType.BitReq, nameof(values));
            Validation.BugCheckParam(types[slot++].Kind == DKind.I8, nameof(values));
            if (backs)
                Validation.BugCheckParam(types[slot++] == DType.BitReq, nameof(values));
            Validation.BugCheckParam(types[slot++].Kind == DKind.I8, nameof(values));
            Validation.Assert(slot == typeTup.TupleArity);

            if (text.TryGetString(out var val) && string.IsNullOrEmpty(val))
                return text;

            // No need to try reductions here. The reducer handles that.
            return new BndTextSliceNode(text, item, values);
        }

        // This only supports tuple and range, not index.
        Validation.BugCheckParam(item.IsRangeSlice(), nameof(item));

        static BoundNode GetValue(ref int ival, ArgTuple values)
        {
            Validation.BugCheckParam(ival < values.Length, nameof(values));
            var val = values[ival++];
            Validation.BugCheckParam(val != null, nameof(values));
            Validation.BugCheckParam(val.Type.Kind == DKind.I8, nameof(values));
            return val;
        }

        BoundNode start = null;
        BoundNode stop = null;
        BoundNode step = null;
        int num = 0;
        if ((item & SliceItemFlags.Start) != 0)
            start = GetValue(ref num, values);
        if ((item & SliceItemFlags.Stop) != 0)
            stop = GetValue(ref num, values);
        if ((item & SliceItemFlags.Step) != 0)
            step = GetValue(ref num, values);
        Validation.BugCheckParam(values.Length == num, nameof(values));

        if (num == 0)
        {
            // The identity slice.
            return text;
        }

        if (!text.TryGetString(out var src))
            return new BndTextSliceNode(text, item, values);

        if (string.IsNullOrEmpty(src))
            return text;

        // See if we can reduce to a constant text value.
        long? startOpt = null;
        long? stopOpt = null;
        long? stepOpt = null;
        if (start != null && !start.TryGetI8Opt(out startOpt) ||
            stop != null && !stop.TryGetI8Opt(out stopOpt) ||
            step != null && !step.TryGetI8Opt(out stepOpt))
        {
            return new BndTextSliceNode(text, item, values);
        }

        var flags = item;
        if (!startOpt.HasValue)
            flags &= ~(SliceItemFlags.Start | SliceItemFlags.StartBack);
        if (!stopOpt.HasValue)
            flags &= ~(SliceItemFlags.Stop | SliceItemFlags.StopBack | SliceItemFlags.StopStar);
        flags |= SliceItemFlags.Step;
        var (a, b, c) = SliceUtil.GetRange(src.Length, flags,
            startOpt.GetValueOrDefault(), stopOpt.GetValueOrDefault(), stepOpt.GetValueOrDefault());

        if (c <= 0)
            return BndStrNode.Empty;
        if (b == 1)
        {
            if (c == src.Length)
            {
                Validation.Assert(a == 0);
                return text;
            }
            return BndStrNode.Create(src.Substring((int)a, (int)c));
        }

        var res = string.Create((int)c, (src, (int)a, (int)b), static (dst, state) =>
        {
            var (src, ich, dich) = state;
            for (int i = 0; i < dst.Length; i++)
            {
                Validation.AssertIndex(ich, src.Length);
                dst[i] = src[ich];
                ich += dich;
            }
        });
        return BndStrNode.Create(res);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Text.Accept(visitor, cur);
        foreach (var value in Values)
            cur = value.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var bsn = bnd as BndTextSliceNode;
        Validation.AssertValue(bsn);
        if (Item != bsn.Item)
            return false;
        if (Values.Length != bsn.Values.Length)
            return false;
        if (!Text.Equivalent(bsn.Text, scopeMap, globalTable))
            return false;
        for (int i = 0; i < Values.Length; i++)
        {
            if (!Values[i].Equivalent(bsn.Values[i], scopeMap, globalTable))
                return false;
        }
        return true;
    }
}

/// <summary>
/// Represents slicing of a tensor.
/// </summary>
public sealed partial class BndTensorSliceNode : BndParentNode
{
    /// <summary>
    /// The source tensor.
    /// </summary>
    public BoundNode Tensor { get; }

    /// <summary>
    /// The item flags, at least one of which will be a slice. The length of this is the
    /// same as the rank of the input <see cref="Tensor"/>.
    /// </summary>
    public SliceTuple Items { get; }

    /// <summary>
    /// The values needed for slicing. These are "referenced" by the <see cref="Items"/>. Note
    /// that they are NOT in 1:1 correspondence with <see cref="Items"/> since range items can
    /// use from zero to three values.
    /// </summary>
    public ArgTuple Values { get; }

    private BndTensorSliceNode(BoundNode tensor, SliceTuple items, ArgTuple values, DType type)
        : base(type, values.Length + 1,
              GetKinds(tensor) | GetKinds(values),
              GetCount(tensor) + GetCount(values))
    {
        Validation.AssertValue(tensor);
        Validation.Assert(tensor.Type.IsTensorReq);
        // Shouldn't be all indices.
        Validation.Assert(items.Any(item => !item.IsIndex()));
        Tensor = tensor;
        Items = items;
        Values = values;
    }

    public static BoundNode Create(BoundNode tensor, SliceTuple items, ArgTuple values)
    {
        Validation.BugCheckValue(tensor, nameof(tensor));
        Validation.BugCheckParam(tensor.Type.IsTensorReq, nameof(tensor));
        Validation.BugCheckParam(!items.IsDefault, nameof(items));
        Validation.BugCheckParam(items.Length == tensor.Type.TensorRank, nameof(items));
        Validation.BugCheckParam(!values.IsDefault, nameof(values));

        int rankSlice = 0;
        bool id = true;
        int ival = 0;
        foreach (var item in items)
        {
            Validation.BugCheckParam(item.IsValid(), nameof(items));
            if (item.IsRangeSlice())
            {
                int count = 0;
                if ((item & SliceItemFlags.Start) != 0)
                    count++;
                if ((item & SliceItemFlags.Stop) != 0)
                    count++;
                if ((item & SliceItemFlags.Step) != 0)
                    count++;
                if (count > 0)
                {
                    Validation.BugCheckParam(ival <= values.Length - count, nameof(values));
                    for (int j = 0; j < count; j++)
                    {
                        Validation.BugCheckParam(values[ival] != null, nameof(values));
                        Validation.BugCheckParam(values[ival].Type.Kind == DKind.I8, nameof(values));
                        ival++;
                    }
                    id = false;
                }
                rankSlice++;
            }
            else if (item.IsTupleSlice())
            {
                Validation.BugCheckParam(ival < values.Length, nameof(values));
                Validation.BugCheckParam(values[ival] != null, nameof(values));
                var typeTup = values[ival].Type;
                Validation.BugCheckParam(typeTup.IsTupleReq, nameof(values));
                bool backs = typeTup.TupleArity > 3;
                Validation.BugCheckParam(typeTup.TupleArity == (backs ? 5 : 3), nameof(values));
                int slot = 0;
                var types = typeTup.GetTupleSlotTypes();
                Validation.BugCheckParam(types[slot++].Kind == DKind.I8, nameof(values));
                if (backs)
                    Validation.BugCheckParam(types[slot++] == DType.BitReq, nameof(values));
                Validation.BugCheckParam(types[slot++].Kind == DKind.I8, nameof(values));
                if (backs)
                    Validation.BugCheckParam(types[slot++] == DType.BitReq, nameof(values));
                Validation.BugCheckParam(types[slot++].Kind == DKind.I8, nameof(values));
                Validation.Assert(slot == typeTup.TupleArity);
                ival++;
                rankSlice++;
                id = false;
            }
            else
            {
                Validation.Assert(item.IsIndex());
                Validation.BugCheckParam(ival < values.Length, nameof(values));
                Validation.BugCheckParam(values[ival] != null, nameof(values));
                Validation.BugCheckParam(values[ival].Type.Kind == DKind.I8, nameof(values));
                ival++;
                id = false;
            }
        }
        Validation.BugCheckParam(rankSlice > 0, nameof(items));
        Validation.BugCheckParam(ival == values.Length, nameof(values));

        if (id)
            return tensor;
        var type = tensor.Type.GetTensorItemType().ToTensor(false, rankSlice);
        return new BndTensorSliceNode(tensor, items, values, type);
    }

    /// <summary>
    /// Return a new node with the given <paramref name="tensor"/> but all else the
    /// same as this node.
    /// </summary>
    public BndTensorSliceNode SetTensor(BoundNode tensor)
    {
        Validation.BugCheckValue(tensor, nameof(tensor));
        Validation.BugCheckParam(tensor.Type == Tensor.Type, nameof(tensor));
        if (tensor == Tensor)
            return this;
        return new BndTensorSliceNode(tensor, Items, Values, Type);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Tensor.Accept(visitor, cur);
        foreach (var value in Values)
            cur = value.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var bsn = bnd as BndTensorSliceNode;
        Validation.AssertValue(bsn);
        Validation.Assert(Items.Length == bsn.Items.Length);
        if (Values.Length != bsn.Values.Length)
            return false;
        if (!Tensor.Equivalent(bsn.Tensor, scopeMap, globalTable))
            return false;
        for (int i = 0; i < Items.Length; i++)
        {
            if (Items[i] != bsn.Items[i])
                return false;
        }
        for (int i = 0; i < Values.Length; i++)
        {
            if (!Values[i].Equivalent(bsn.Values[i], scopeMap, globalTable))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Get a sliced tuple.
/// </summary>
public sealed partial class BndTupleSliceNode : BndParentNode
{
    /// <summary>
    /// The source tuple.
    /// </summary>
    public BoundNode Tuple { get; }

    /// <summary>
    /// The start index in the source tuple.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// The step index in the slice range (must be none-zero).
    /// </summary>
    public int Step { get; }

    /// <summary>
    /// The count of the slice range.
    /// </summary>
    public int Count { get; }

    private BndTupleSliceNode(DType type, BoundNode tuple, int start, int step, int count)
        : base(type, 1, GetKinds(tuple), GetCount(tuple))
    {
#if DEBUG
        Validation.Assert(type.IsTupleReq);
        Validation.Assert(type.TupleArity == count);
        Validation.AssertValue(tuple);
        Validation.Assert(tuple.Type.IsTupleReq);
        int aritySrc = tuple.Type.TupleArity;
        Validation.Assert(aritySrc > 0);
        Validation.AssertIndex(start, aritySrc);
        Validation.Assert(step != 0);
        Validation.Assert(count > 0);
        Validation.AssertIndex(start + (count - 1) * (long)step, aritySrc);
#endif
        Tuple = tuple;
        Start = start;
        Step = step;
        Count = count;
    }

    public static BoundNode Create(BoundNode tuple, int start, int step, int count)
    {
        Validation.BugCheckValue(tuple, nameof(tuple));
        Validation.BugCheckParam(tuple.Type.IsTupleReq, nameof(tuple));
        int aritySrc = tuple.Type.TupleArity;
        Validation.BugCheckParam(aritySrc > 0, nameof(tuple));
        Validation.BugCheckIndex(start, aritySrc, nameof(start));
        Validation.BugCheckParam(count > 0, nameof(count));
        Validation.BugCheckIndex(start + (count - 1) * (long)step, aritySrc, nameof(count));

        if (tuple is BndTupleNode btn)
        {
            var bldrArgs = ArgTuple.CreateBuilder(count, init: true);
            for (int i = 0; i < count; i++)
                bldrArgs[i] = btn.Items[start + i * step];
            return BndTupleNode.Create(bldrArgs.ToImmutable());
        }

        var typesSrc = tuple.Type.GetTupleSlotTypes();
        var bldrType = Immutable.Array<DType>.CreateBuilder(count, init: true);
        for (int i = 0; i < bldrType.Count; i++)
            bldrType[i] = typesSrc[start + i * step];
        Validation.BugCheckParam(count == bldrType.Count, nameof(count));

        var type = DType.CreateTuple(opt: false, bldrType.ToImmutable());

        if (tuple is BndDefaultNode)
            return BndDefaultNode.Create(type);

        return new BndTupleSliceNode(type, tuple, start, step, count);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        return Tuple.Accept(visitor, cur);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var btsn = bnd as BndTupleSliceNode;
        Validation.AssertValue(btsn);

        return Tuple.Equivalent(btsn.Tuple, scopeMap, globalTable) &&
            (Start == btsn.Start) &&
            (Step == btsn.Step) &&
            (Count == btsn.Count);
    }
}

/// <summary>
/// Abstract base class for cast nodes. The type must accept the child's type.
/// </summary>
public abstract class BndCastNode : BndParentNode
{
    /// <summary>
    /// The value being cast.
    /// </summary>
    public BoundNode Child { get; }

    private protected BndCastNode(BoundNode child, DType type)
        : base(type, 1, GetKinds(child), GetCount(child))
    {
        Validation.AssertValue(child);
        Validation.Assert(type.IsValid);
        Validation.Assert(type != child.Type);

        // Any record types involved should have exactly the same set of fields, so acceptance
        // should be true under both modes.
        Validation.Assert(type.Accepts(child.Type, union: true));
        Validation.Assert(type.Accepts(child.Type, union: false));

        Child = child;
    }

    /// <summary>
    /// Cast the node to type general.
    /// </summary>
    public static BoundNode CastGeneral(BoundNode child)
    {
        Validation.BugCheckValue(child, nameof(child));

        DType typeSrc = child.Type;
        if (typeSrc.IsGeneral)
            return child;
        if (child.IsNullValue || typeSrc.IsVacXxx)
            return BndNullNode.Create(DType.General);
        if (typeSrc.Kind.IsReferenceFriendly())
            return BndCastRefNode.CreateCore(child, DType.General);
        return BndCastBoxNode.Create(child);
    }

    protected sealed override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        return Child.Accept(visitor, cur);
    }

    /// <summary>
    /// Return a new node with the given child and everything else the same. The given
    /// child must have the same type as the current child.
    /// </summary>
    public BoundNode SetChild(BoundNode child, IReducerHost host = null)
    {
        if (child == Child)
            return this;
        Validation.BugCheckValue(child, nameof(child));
        Validation.BugCheck(child.Type == Child.Type, nameof(child));
        return SetChildCore(child, host);
    }

    private protected abstract BoundNode SetChildCore(BoundNode child, IReducerHost host = null);

    private protected sealed override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndCastNode;
        // These are ensured by BoundNode.Equivalent, which should be the only invoker of this.
        Validation.Assert(other != null);
        Validation.Assert(Type == other.Type);

        return Child.Equivalent(other.Child, scopeMap, globalTable);
    }
}

/// <summary>
/// A numeric cast. The type must be req.
/// </summary>
public sealed partial class BndCastNumNode : BndCastNode
{
    private BndCastNumNode(BoundNode child, DType type)
        : base(child, type)
    {
        Validation.Assert(Child.Type.IsNumericReq);
        Validation.Assert(Type.IsNumericReq);
    }

    /// <summary>
    /// For numeric cast. If the <paramref name="child"/> is a constant, this performs the
    /// conversion and returns the new constant. If that conversion results in an overflow
    /// and <paramref name="host"/> is provided, then a warning is reported to the host.
    /// Note that this will only happen when the source type is integer and the result
    /// type is floating point, with the final value being infinite.
    /// </summary>
    public static BoundNode Create(BoundNode child, DType type, IReducerHost host = null)
    {
        Validation.BugCheckValue(child, nameof(child));
        Validation.BugCheckParam(child.Type.IsNumericReq, nameof(child));
        Validation.BugCheckParam(type.IsNumericReq, nameof(type));
        Validation.BugCheckParam(type.Accepts(child.Type, DType.UseUnionDefault), nameof(type));

        // REVIEW: Should this handle folding cast of cast when equivalent?
        if (child.Type == type)
            return child;
        if (child is BndIntNode bin)
        {
            if (type.IsIntegralReq)
            {
                var res = BndIntNode.CreateCast(type, bin.Value, out bool overflow);
                if (overflow)
                {
                    // U8 to I8 can "overflow".
                    Validation.Assert(type.RootKind == DKind.I8);
                    Validation.Assert(bin.Type.RootKind == DKind.U8); ;
                    if (host != null)
                        host.Warn(child, ErrorStrings.WrnUnsignedIntLiteralOverflow);
                }
                return res;
            }

            Validation.Assert(type.IsFractionalReq);
            double v = type.RootKind == DKind.R4 ? bin.Value.ToR4() : bin.Value.ToR8();
            var bfnRes = BndFltNode.CreateCast(type, v);
            if (host != null && !bfnRes.Value.IsFinite())
                host.Warn(child, ErrorStrings.WrnFloatOverflow);
            return bfnRes;
        }
        if (child is BndFltNode bfn)
        {
            Validation.Assert(type.IsFractionalReq);
            return BndFltNode.CreateCast(type, bfn.Value);
        }
        return new BndCastNumNode(child, type);
    }

    private protected override BoundNode SetChildCore(BoundNode child, IReducerHost host = null)
    {
        Validation.AssertValue(child);
        Validation.Assert(child.Type == Child.Type);
        return BndCastNumNode.Create(child, Type, host);
    }

    public override bool IsCheap
    {
        get
        {
            // Casting to/from the i type is not cheap.
            if (!Child.IsCheap)
                return false;
            if (Type.Kind == DKind.IA)
                return false;
            if (Child.Type.Kind == DKind.IA)
                return false;
            return true;
        }
    }
}

/// <summary>
/// A reference cast. This is intended to be a pure "reference" conversion, where the system types are both reference
/// types and either the same reference type or assignment compatible. Technically, bound trees are independent of the
/// eventual representation as executable code, but this situation is convenient to separate from other casts that
/// transform the expected representation as system types.
/// 
/// This handles the cases:
/// * Type is a reference friendly opt type and Child has the corresponding reference friendly req type.
/// * Type and Child.Type have the same sequence counts with their item types satisfying the above.
/// * Type is general or sequence of general and child has a larger sequence count.
/// * Type is general or sequence of general and child has the same sequence count and the item type is reference friendly.
/// 
/// Note that we don't assume that the system types for tensors, records, or tuples is covariant. We DO assume
/// that T?[...] and T[...] use the same system type when T is "reference friendly".
/// </summary>
public sealed partial class BndCastRefNode : BndCastNode
{
    public override bool IsCheap => Child.IsCheap;

    private BndCastRefNode(BoundNode child, DType type)
        : base(child, type)
    {
        Validation.Assert(!Child.IsNullValue);
        Validation.Assert(!(Child is BndCastRefNode));
        Validation.Assert(Conversion.IsRefConv(child.Type, type));
    }

    public static BndCastRefNode Create(BoundNode child, DType type)
    {
        Validation.BugCheckValue(child, nameof(child));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(Conversion.IsRefConv(child.Type, type), nameof(type));
        return CreateCore(child, type);
    }

    /// <summary>
    /// This is called from the public BndCastNode.Create and this class's Create methods.
    /// </summary>
    internal static BndCastRefNode CreateCore(BoundNode child, DType type)
    {
        Validation.AssertValue(child);
        Validation.Assert(Conversion.IsRefConv(child.Type, type));

        // A RefCast of a RefCast can be reduced.
        if (child is BndCastRefNode brc)
            return new BndCastRefNode(brc.Child, type);
        return new BndCastRefNode(child, type);
    }

    /// <summary>
    /// Returns whether there is a reference conversion from the child's type to the given type.
    /// REVIEW: Is this really needed? Is this the best way to do this?
    /// </summary>
    public static bool AreValid(BoundNode child, DType type)
    {
        if (child == null)
            return false;
        if (type == child.Type)
            return false;
        if (!type.Accepts(child.Type, DType.UseUnionDefault))
            return false;
        if (child.Type.HasReq)
            return false;
        if (type.HasReq && !child.Type.IsOpt)
            return false;

        if (type.RootKind == DKind.General)
        {
            // Casting to sequence of general. Either the source should have a higher sequence count
            // or they should have the same sequence count and the source type should be reference friendly.
            if (type.SeqCount == child.Type.SeqCount)
            {
                if (!child.Type.RootKind.IsReferenceFriendly())
                    return false;
            }
            else
            {
                if (type.SeqCount >= child.Type.SeqCount)
                    return false;
            }
        }
        else
        {
            // They should have the same sequence count, the root type should be reference friendly, and
            // the root types should differ only in optness.
            if (child.Type.SeqCount != type.SeqCount)
                return false;
            if (!type.RootKind.IsReferenceFriendly())
                return false;
            if (type.RootKind != child.Type.RootKind)
                return false;
            switch (type.RootKind)
            {
            case DKind.Uri:
            case DKind.Tensor:
                break;

            case DKind.Record:
                if (child.Type.RootToOpt() != type)
                    return false;
                break;

            default:
                return false;
            }
        }

        return true;
    }

    private protected override BoundNode SetChildCore(BoundNode child, IReducerHost host = null)
    {
        Validation.AssertValue(child);
        Validation.Assert(child.Type == Child.Type);

        if (child.IsNullValue)
            return BndNullNode.Create(Type);
        if (child is BndCmpConstNode bccn)
            return bccn.MorphRefType(Type);
        return CreateCore(child, Type);
    }

    public override (long min, long max) GetItemCountRange()
    {
        return Child.GetItemCountRange();
    }
}

/// <summary>
/// A boxing cast. This handles the case where the destination type is General and the
/// source type is not reference friendly.
/// Note that (unlike most other casts) boxing is not "cheap" even when its child is.
/// </summary>
public sealed partial class BndCastBoxNode : BndCastNode
{
    private BndCastBoxNode(BoundNode child)
        : base(child, DType.General)
    {
        Validation.Assert(Child.Type.SeqCount == 0);
        Validation.Assert(!Child.Type.Kind.IsReferenceFriendly());
        Validation.Assert(!Child.Type.IsVacXxx);
    }

    public static BndCastBoxNode Create(BoundNode child)
    {
        Validation.BugCheckValue(child, nameof(child));
        Validation.BugCheckParam(!child.Type.Kind.IsReferenceFriendly(), nameof(child));
        Validation.BugCheckParam(!child.Type.IsVacXxx, nameof(child));
        return new BndCastBoxNode(child);
    }

    private protected override BoundNode SetChildCore(BoundNode child, IReducerHost host = null)
    {
        Validation.AssertValue(child);
        Validation.Assert(child.Type == Child.Type);
        return new BndCastBoxNode(child);
    }
}

/// <summary>
/// A opt wrapping cast.
/// </summary>
public sealed partial class BndCastOptNode : BndCastNode
{
    public override bool IsConstant => Child.IsConstant;

    public override bool IsNonNullConstant => Child.IsNonNullConstant;

    public override bool IsCheap => Child.IsCheap;

    private BndCastOptNode(BoundNode child, DType type)
        : base(child, type)
    {
    }

    public static BndCastNode Create(BoundNode child)
    {
        Validation.BugCheckValue(child, nameof(child));
        Validation.BugCheck(!child.Type.IsOpt);
        var typeDst = child.Type.ToOpt();
        if (Conversion.IsRefConv(child.Type, typeDst))
            return BndCastRefNode.Create(child, typeDst);
        return new BndCastOptNode(child, typeDst);
    }

    private protected override BoundNode SetChildCore(BoundNode child, IReducerHost host = null)
    {
        Validation.AssertValue(child);
        Validation.Assert(child.Type == Child.Type);
        return new BndCastOptNode(child, Type);
    }

    public override bool TryGetIntegralOpt(out Integer? value)
    {
        return Child.TryGetIntegralOpt(out value);
    }

    public override bool TryGetFractionalOpt(out double? value)
    {
        return Child.TryGetFractionalOpt(out value);
    }

    public override bool TryGetBoolOpt(out bool? value)
    {
        return Child.TryGetBoolOpt(out value);
    }
}

/// <summary>
/// A cast from vac. This gets reduced to a default node, so no reason to mark it
/// as being cheap.
/// </summary>
public sealed partial class BndCastVacNode : BndCastNode
{
    private BndCastVacNode(BoundNode child, DType type)
        : base(child, type)
    {
        Validation.Assert(Child.Type.IsVac);
    }

    public static BoundNode Create(BoundNode child, DType type)
    {
        Validation.BugCheckValue(child, nameof(child));
        Validation.BugCheckParam(child.Type.IsVac, nameof(child));
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (type.IsVac)
            return child;
        return new BndCastVacNode(child, type);
    }

    private protected override BoundNode SetChildCore(BoundNode child, IReducerHost host = null)
    {
        Validation.AssertValue(child);
        Validation.Assert(child.Type == Child.Type);
        return new BndCastVacNode(child, Type);
    }
}

/// <summary>
/// A binary operation. Note that the binder represents many binary operators using BndVariadicOpNode.
/// </summary>
public sealed partial class BndBinaryOpNode : BndParentNode
{
    public const BinaryOp BopMin = BinaryOp.IntDiv;
    public const BinaryOp BopLim = BinaryOp.Max + 1;

    /// <summary>
    /// The left operand.
    /// </summary>
    public BoundNode Arg0 { get; }

    /// <summary>
    /// The right operand.
    /// </summary>
    public BoundNode Arg1 { get; }

    /// <summary>
    /// The operator.
    /// </summary>
    public BinaryOp Op { get; }

    private BndBinaryOpNode(DType type, BinaryOp op, BoundNode arg0, BoundNode arg1)
        : base(type, 2,
              GetKinds(arg0) | GetKinds(arg1),
              GetCount(arg0) + GetCount(arg1))
    {
        Validation.AssertValue(arg0);
        Validation.AssertValue(arg1);
        Validation.Assert(BopMin <= op & op < BopLim);

        Arg0 = arg0;
        Arg1 = arg1;
        Op = op;
    }

    public static BndBinaryOpNode Create(DType type, BinaryOp op, BoundNode arg0, BoundNode arg1)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));

        // REVIEW: Perhaps this should do some normalization, eg, create variadic nodes for
        // ops that are represented as such.
        Validation.BugCheckParam(BopMin <= op & op < BopLim, nameof(op));

        Validation.BugCheckValue(arg0, nameof(arg0));
        Validation.BugCheckValue(arg1, nameof(arg1));

        return new BndBinaryOpNode(type, op, arg0, arg1);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Arg0.Accept(visitor, cur);
        cur = Arg1.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndBinaryOpNode;
        Validation.Assert(other != null);

        if (Op != other.Op)
            return false;
        if (!Arg0.Equivalent(other.Arg0, scopeMap, globalTable))
            return false;
        if (!Arg1.Equivalent(other.Arg1, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// Fully associative operators can be flattened to variadic. For example, string concatenation,
/// as well as integer addition and multiplication, but NOT floating point arithmetic. Note
/// that we can flatten left-associative chains of floating point addition / multiplication / division,
/// for example ((a + b) + c), but NOT (a + (b + c)).
/// </summary>
public sealed partial class BndVariadicOpNode : BndParentNode
{
    public const BinaryOp BopMin = BinaryOp.Or;
    public const BinaryOp BopLim = BinaryOp.Mul + 1;

    /// <summary>
    /// The operands.
    /// </summary>
    public ArgTuple Args { get; }

    /// <summary>
    /// Specifies which arguments (if any) should be "inverted". For example, when the
    /// <see cref="Op"/> is <see cref="BinaryOp.Add"/>, an inverted arg is subtracted.
    /// Similarly, when the <see cref="Op"/> is <see cref="BinaryOp.Mul"/> and the type
    /// supports division (floating point), then an inverted arg is divided into the
    /// previous value. If the first arg is marked inverted, then it is negated for add
    /// and divided into 1 for mul. Note that for add with floating point, this isn't quite
    /// the same thing as <c>0 - arg</c>, because of signed zero subtleties.
    /// </summary>
    public BitSet Inverted { get; }

    /// <summary>
    /// The operator.
    /// </summary>
    public BinaryOp Op;

    private BndVariadicOpNode(DType type, BinaryOp op, ArgTuple args, BitSet invs)
        : base(type, args.Length, GetKinds(args), GetCount(args))
    {
        Validation.Assert(BopMin <= op & op < BopLim);
        Validation.Assert(!args.IsDefault);
        Validation.AssertAllValues(args);
        Validation.Assert(!invs.TestAtOrAbove(args.Length));
        Args = args;
        Op = op;
        Inverted = invs;
    }

    /// <summary>
    /// Tests whether <paramref name="bnd"/> is a variadic op node with the indicated
    /// operator, and sets <paramref name="args"/> and <paramref name="invs"/> accordingly.
    /// </summary>
    internal static bool TryGetArgs(BinaryOp op, BoundNode bnd, out ArgTuple args, out BitSet invs)
    {
        Validation.Assert(BopMin <= op & op <= BopLim, nameof(op));
        Validation.AssertValue(bnd, nameof(bnd));

        if (bnd is BndVariadicOpNode vop && vop.Op == op)
        {
            args = vop.Args;
            invs = vop.Inverted;
            Validation.Assert(!invs.TestAtOrAbove(args.Length));
            return true;
        }
        args = default;
        invs = default;
        return false;
    }

    /// <summary>
    /// For a full associative binary operator, flattens and returns the effective args from the two
    /// given bound nodes, together with the appropriate invert bits. Sets indexSplit to the start of
    /// the args coming from bnd1. Note that this does NOT check that the two bound nodes have the same type.
    /// </summary>
    internal static (ArgTuple.Builder bldr, BitSet invs) GetVarArgs(BinaryOp op, bool inv, BoundNode bnd0, BoundNode bnd1, out int indexSplit)
    {
        Validation.Assert(op.IsAssociative(), nameof(op));

        // These extract the args and do bug checks on op, bnd0, and bnd1.
        bool c0 = TryGetArgs(op, bnd0, out var args0, out var invs0);
        bool c1 = TryGetArgs(op, bnd1, out var args1, out var invs1);

        int cap = (c0 ? args0.Length : 1) + (c1 ? args1.Length : 1);
        var bldr = ArgTuple.CreateBuilder(cap, init: true);
        Validation.Assert(bldr.Count == cap);

        BitSet invs;
        int ivDst = 0;
        if (c0)
        {
            invs = invs0;
            for (; ivDst < args0.Length; ivDst++)
                bldr[ivDst] = args0[ivDst];
        }
        else
        {
            Validation.Assert(invs0.IsEmpty);
            invs = default;
            bldr[ivDst++] = bnd0;
        }

        indexSplit = ivDst;

        if (c1)
        {
            if (inv)
                invs1 = BitSet.GetMask(args1.Length) - invs1;
            invs |= invs1 << ivDst;
            for (int ivSrc = 0; ivSrc < args1.Length;)
                bldr[ivDst++] = args1[ivSrc++];
        }
        else
        {
            if (inv)
                invs = invs.SetBit(ivDst);
            bldr[ivDst++] = bnd1;
        }

        Validation.Assert(ivDst == bldr.Count);
        Validation.Assert(0 < indexSplit & indexSplit < bldr.Count);
        return (bldr, invs);
    }

    /// <summary>
    /// For a (full) associative binary operator, flattens the two given args. The <paramref name="inv"/> flag
    /// indicates whether the inverse operator (sub for add, div for mul) should be used (on the right arg).
    /// </summary>
    internal static (ArgTuple args, BitSet invs) Flatten(BinaryOp op, bool inv, BoundNode bnd0, BoundNode bnd1)
    {
        Validation.Assert(op.IsAssociative(), nameof(op));
        var (bldr, invs) = GetVarArgs(op, inv, bnd0, bnd1, out _);
        return (bldr.ToImmutable(), invs);
    }

    /// <summary>
    /// For a left associative binary operator, flatten the left-most arg. The <paramref name="inv"/> flag
    /// indicates whether the inverse operator (sub for add, div for mul) should be used (on the right arg).
    /// </summary>
    internal static (ArgTuple args, BitSet invs) FlattenLeft(BinaryOp op, bool inv, BoundNode bnd0, BoundNode bnd1)
    {
        Validation.Assert(op.IsAssociative(), nameof(op));
        Validation.AssertValue(bnd1, nameof(bnd1));
        if (TryGetArgs(op, bnd0, out var args, out var invs))
            return (args.Add(bnd1), inv ? invs.SetBit(args.Length) : invs);
        return (ArgTuple.Create(bnd0, bnd1), inv ? 0x2 : default(BitSet));
    }

    /// <summary>
    /// For a full associative binary operator, flatten the args.
    /// </summary>
    internal static void Flatten(BinaryOp op, ref ArgTuple args, ref BitSet invs)
    {
        Validation.Assert(op.IsAssociative(), nameof(op));
        Validation.Assert(!args.IsDefault, nameof(args));
        Validation.Assert(!invs.TestAtOrAbove(args.Length), nameof(invs));

        var src = args;
        int num = 0;
        bool any = false;
        for (int iv = 0; iv < src.Length; iv++)
        {
            var cur = src[iv];
            Validation.BugCheckValue(cur, nameof(args));
            if (TryGetArgs(op, cur, out var argsCur, out _))
            {
                Validation.Assert(argsCur.Length > 0);
                num += argsCur.Length;
                any = true;
            }
            else
                num++;
        }
        if (!any)
        {
            Validation.Assert(num == src.Length);
            return;
        }

        var bldrRes = ArgTuple.CreateBuilder(num, init: true);
        Validation.Assert(bldrRes.Count == num);
        BitSet invsRes = default;

        int ivDst = 0;
        for (int iv = 0; iv < args.Length; iv++)
        {
            Validation.Assert(ivDst < num);
            var cur = src[iv];
            bool inv = invs.TestBit(iv);
            if (TryGetArgs(op, cur, out var argsCur, out var invsCur))
            {
                Validation.Assert(ivDst <= num - argsCur.Length);
                if (inv)
                    invsCur = BitSet.GetMask(argsCur.Length) - invsCur;
                invsRes |= invsCur << ivDst;
                for (int ivCur = 0; ivCur < argsCur.Length;)
                    bldrRes[ivDst++] = argsCur[ivCur++];
            }
            else
            {
                if (inv)
                    invsRes = invsRes.SetBit(ivDst);
                bldrRes[ivDst++] = cur;
            }
        }

        Validation.Assert(ivDst == bldrRes.Count);
        Validation.Assert(!invsRes.TestAtOrAbove(ivDst));
        args = bldrRes.ToImmutable();
        invs = invsRes;
    }

    /// <summary>
    /// Given args in <paramref name="bldr"/>, flatten args for the given full associate
    /// <paramref name="op"/>.
    /// </summary>
    internal static void Flatten(BinaryOp op, ref ArgTuple.Builder bldr, ref BitSet invs)
    {
        Validation.Assert(op.IsAssociative(), nameof(op));
        Validation.AssertValue(bldr, nameof(bldr));
        Validation.Assert(!invs.TestAtOrAbove(bldr.Count), nameof(invs));

        var src = bldr;
        int num = 0;
        bool any = false;
        for (int iv = 0; iv < src.Count; iv++)
        {
            var cur = src[iv];
            Validation.BugCheckValue(cur, nameof(bldr));
            if (TryGetArgs(op, cur, out var argsCur, out _))
            {
                Validation.Assert(argsCur.Length > 0);
                num += argsCur.Length;
                any = true;
            }
            else
                num++;
        }
        if (!any)
        {
            Validation.Assert(num == src.Count);
            return;
        }

        var bldrRes = ArgTuple.CreateBuilder(num);
        BitSet invsRes = default;

        for (int iv = 0; iv < src.Count; iv++)
        {
            Validation.Assert(bldrRes.Count < num);
            var cur = src[iv];
            bool inv = invs.TestBit(iv);
            if (TryGetArgs(op, cur, out var argsCur, out var invsCur))
            {
                Validation.Assert(bldrRes.Count <= num - argsCur.Length);
                if (inv)
                    invsCur = BitSet.GetMask(argsCur.Length) - invsCur;
                invsRes |= invsCur << bldrRes.Count;
                bldrRes.AddRange(argsCur);
            }
            else
            {
                if (inv)
                    invsRes = invsRes.SetBit(bldrRes.Count);
                bldrRes.Add(cur);
            }
        }

        Validation.Assert(bldrRes.Count == num);
        Validation.Assert(!invsRes.TestAtOrAbove(num));
        bldr = bldrRes;
        invs = invsRes;
    }

    /// <summary>
    /// For a left associative binary operator, flatten the left-most arg.
    /// </summary>
    internal static void FlattenLeft(BinaryOp op, ref ArgTuple args, ref BitSet invs)
    {
        Validation.Assert(op.IsAssociative(), nameof(op));
        Validation.Assert(!args.IsDefault, nameof(args));
        Validation.AssertAllValues(args);
        Validation.Assert(!invs.TestAtOrAbove(args.Length), nameof(invs));

        if (args.Length == 0)
            return;
        if ((op == BinaryOp.Mul || op == BinaryOp.Div) && invs.TestBit(0))
            return;
        if (!TryGetArgs(op, args[0], out var argsLeft, out var invsLeft))
            return;

        int cap = argsLeft.Length + args.Length - 1;
        var bldr = ArgTuple.CreateBuilder(cap, init: true);
        Validation.Assert(bldr.Count == cap);

        int ivDst = 0;
        for (; ivDst < argsLeft.Length; ivDst++)
            bldr[ivDst] = argsLeft[ivDst];

        if (invs.TestBit(0))
            invsLeft = BitSet.GetMask(ivDst) - invsLeft;
        invs = invsLeft | invs.ClearBit(0) << (ivDst - 1);

        for (int ivSrc = 1; ivSrc < args.Length;)
            bldr[ivDst++] = args[ivSrc++];

        Validation.Assert(ivDst == bldr.Count);
        Validation.Assert(!invs.TestAtOrAbove(ivDst));
        args = bldr.ToImmutable();
    }

    private static void ValidateType(DType type, BinaryOp op)
    {
        switch (op)
        {
        case BinaryOp.Or:
        case BinaryOp.And:
            Validation.BugCheckParam(type.Kind == DKind.Bit, nameof(type));
            break;
        case BinaryOp.Xor:
            Validation.BugCheckParam(type == DType.BitReq, nameof(type));
            break;
        case BinaryOp.BitOr:
        case BinaryOp.BitXor:
        case BinaryOp.BitAnd:
            Validation.BugCheckParam(type.IsIntegralReq, nameof(type));
            break;
        case BinaryOp.StrConcat:
            Validation.BugCheckParam(type == DType.Text, nameof(type));
            break;
        case BinaryOp.TupleConcat:
            Validation.BugCheckParam(type.IsTupleReq, nameof(type));
            break;
        case BinaryOp.RecordConcat:
            Validation.BugCheckParam(type.IsRecordReq, nameof(type));
            break;
        case BinaryOp.SeqConcat:
            Validation.BugCheckParam(type.IsSequence, nameof(type));
            break;
        case BinaryOp.Add:
        case BinaryOp.Mul:
            Validation.BugCheckParam(type.IsIntegralReq | type.IsFractionalReq, nameof(type));
            break;
        default:
            throw Validation.ExceptParam(nameof(op));
        }
    }

    /// <summary>
    /// Create a variadic node for the given operator and args. This flattens appropriately.
    /// </summary>
    public static BoundNode Create(DType type, BinaryOp op, BoundNode bnd0, BoundNode bnd1, bool inv)
    {
        switch (op)
        {
        case BinaryOp.TupleConcat:
            {
                Validation.BugCheckParam(!inv, nameof(inv));
                var res = CreateTupleConcat(bnd0, bnd1);
                Validation.BugCheckParam(res.Type == type, nameof(type));
                return res;
            }

        case BinaryOp.RecordConcat:
            {
                Validation.BugCheckParam(!inv, nameof(inv));
                var res = CreateRecordConcat(bnd0, bnd1);
                Validation.BugCheckParam(res.Type == type, nameof(type));
                return res;
            }

        case BinaryOp.Sub:
            inv = !inv;
            op = BinaryOp.Add;
            break;
        case BinaryOp.Div:
            Validation.BugCheckParam(type.IsFractionalXxx, nameof(type));
            inv = !inv;
            op = BinaryOp.Mul;
            break;
        }

        ValidateType(type, op);

        // Floating point operators are left associative, not fully associative.
        var (args, invs) = type.IsFractionalXxx ? FlattenLeft(op, inv, bnd0, bnd1) : Flatten(op, inv, bnd0, bnd1);

        if (type.HasReq)
        {
            // The type has a req form. The only operators that support this are these bool ops.
            Validation.BugCheck(op == BinaryOp.Or || op == BinaryOp.And, nameof(type));
            Validation.BugCheckParam(type == DType.BitOpt, nameof(type));
            Validation.BugCheckParam(bnd0.Type.Kind == DKind.Bit, nameof(bnd0));
            Validation.BugCheckParam(bnd1.Type.Kind == DKind.Bit, nameof(bnd1));
        }
        else
        {
            // In the typical form, the arg types must all match the result type.
            Validation.BugCheckParam(bnd0.Type == type, nameof(bnd0));
            Validation.BugCheckParam(bnd1.Type == type, nameof(bnd1));
        }

        return new BndVariadicOpNode(type, op, args, invs);
    }

    public static BoundNode Create(DType type, BinaryOp op, ArgTuple args, BitSet invs)
    {
        Validation.BugCheckParam(BopMin <= op & op < BopLim, nameof(op));
        Validation.BugCheckParam(invs.IsEmpty || op == BinaryOp.Add || op == BinaryOp.Mul, nameof(invs));

        switch (op)
        {
        case BinaryOp.TupleConcat:
            {
                Flatten(op, ref args, ref invs);
                Validation.BugCheck(invs.IsEmpty, nameof(invs));
                var res = CreateTupleConcatCore(args);
                Validation.BugCheckParam(type == res.Type, nameof(type));
                return res;
            }

        case BinaryOp.RecordConcat:
            {
                Flatten(op, ref args, ref invs);
                Validation.BugCheck(invs.IsEmpty, nameof(invs));
                var res = CreateRecordConcatCore(args);
                Validation.BugCheckParam(type == res.Type, nameof(type));
                return res;
            }
        }

        ValidateType(type, op);

        // Floating point operators are left associative, not fully associative.
        if (type.IsFractionalXxx)
            FlattenLeft(op, ref args, ref invs);
        else
            Flatten(op, ref args, ref invs);

        if (type.HasReq)
        {
            // The type has a req form. The only operators that support this are these bool ops.
            Validation.BugCheck(op == BinaryOp.Or || op == BinaryOp.And, nameof(type));
            Validation.BugCheckParam(type == DType.BitOpt, nameof(type));
            Validation.BugCheckParam(args.All(arg => arg.Type.Kind == DKind.Bit), nameof(args));
        }
        else
        {
            // In the typical form, the arg types must all match the result type.
            Validation.BugCheckParam(args.All(arg => arg.Type == type), nameof(args));
        }

        if (args.Length == 1 && !invs.TestBit(0))
        {
            switch (op)
            {
            case BinaryOp.StrConcat:
                // Unary str concat maps null to empty, so is not a no-op.
                break;

            default:
                var arg = args[0];
                if (arg.Type == type)
                    return arg;

                Validation.Assert(arg.Type == DType.BitReq);
                Validation.Assert(type == DType.BitOpt);
                return BndCastOptNode.Create(arg);
            }
        }

        return new BndVariadicOpNode(type, op, args, invs);
    }

    /// <summary>
    /// Create a concatenation of tuples. This flattens appropriately.
    /// </summary>
    public static BoundNode CreateTupleConcat(BoundNode bnd0, BoundNode bnd1)
    {
        Validation.BugCheckParam(bnd0.Type.IsTupleReq, nameof(bnd0));
        Validation.BugCheckParam(bnd1.Type.IsTupleReq, nameof(bnd1));

        var (args, invs) = Flatten(BinaryOp.TupleConcat, false, bnd0, bnd1);
        Validation.Assert(invs.IsEmpty);

        return CreateTupleConcatCore(args);
    }

    private static BoundNode CreateTupleConcatCore(ArgTuple args)
    {
        Validation.Assert(args.Length >= 2);

        int arity = 0;
        for (int i = 0; i < args.Length; i++)
        {
            var typeCur = args[i].Type;
            Validation.Assert(typeCur.IsTupleReq);
            arity += typeCur.TupleArity;
        }

        var types = Immutable.Array<DType>.CreateBuilder(arity, init: true);
        int slotCur = 0;
        for (int i = 0; i < args.Length; i++)
        {
            var typeCur = args[i].Type;
            foreach (var type in typeCur.GetTupleSlotTypes())
                types[slotCur++] = type;
        }
        return new BndVariadicOpNode(DType.CreateTuple(false, types.ToImmutable()),
            BinaryOp.TupleConcat, args, default);
    }

    /// <summary>
    /// Create a concatenation of records. This flattens appropriately.
    /// </summary>
    public static BoundNode CreateRecordConcat(BoundNode bnd0, BoundNode bnd1)
    {
        Validation.BugCheckParam(bnd0.Type.IsRecordReq, nameof(bnd0));
        Validation.BugCheckParam(bnd1.Type.IsRecordReq, nameof(bnd1));

        var (args, invs) = Flatten(BinaryOp.RecordConcat, false, bnd0, bnd1);
        Validation.Assert(invs.IsEmpty);

        return CreateRecordConcatCore(args);
    }

    private static BoundNode CreateRecordConcatCore(ArgTuple args)
    {
        Validation.Assert(args.Length >= 2);

        int i = args.Length;
        var typeRes = args[--i].Type;
        Validation.Assert(typeRes.IsRecordReq);
        while (--i >= 0)
        {
            var typeCur = args[i].Type;
            Validation.Assert(typeCur.IsRecordReq);
            foreach (var tn in typeCur.GetNames())
            {
                if (!typeRes.Contains(tn.Name))
                    typeRes = typeRes.Add(tn);
            }
        }
        return new BndVariadicOpNode(typeRes, BinaryOp.RecordConcat, args, default);
    }

    public override (long min, long max) GetItemCountRange()
    {
        if (Op != BinaryOp.SeqConcat)
            return base.GetItemCountRange();

        long min = 0;
        long max = 0;
        int cseq = Args.Length;
        for (int i = 0; i < cseq; i++)
        {
            var (a, b) = Args[i].GetItemCountRange();
            Validation.AssertIndexInclusive(a, b);
            min = NumUtil.AddCounts(min, a);
            max = NumUtil.AddCounts(max, b);
        }
        return (min, max);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var arg in Args)
            cur = arg.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndVariadicOpNode;
        Validation.Assert(other != null);

        if (Op != other.Op)
            return false;
        if (Inverted != other.Inverted)
            return false;
        if (!EquivArgs(Args, other.Args, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// A bound comparison chain.
/// </summary>
public sealed partial class BndCompareNode : BndParentNode
{
    /// <summary>
    /// These flags indicate how a null operand value should be handled.
    /// There is one of these values for each operand.
    /// </summary>
    [Flags]
    public enum NullTo : byte
    {
        /// <summary>
        /// What happens with null depends on the null-ness of the other operand and the
        /// particular operator. Eg, for =, if one operand is non-null, the other being null
        /// results in false, while for !=, if one operand is non-null, the other being null
        /// makes the clause true.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// If the operand is null, the answer is false. This flag always takes precedence over any "true"
        /// flags that may be set. That is, false must be enforced first.
        /// </summary>
        False = 0x01,

        /// <summary>
        /// When the operand is null and is the left operand of the current clause, this flag indicates
        /// that the clause is absolutely true, regardless of the value of the right operand. For example,
        /// this is set for the left operand of a strict positive operator or a non-strict LE operator,
        /// since null is LE anything. Note that if either operand has the false flag, that takes priority.
        /// </summary>
        TrueLeftAbs = 0x02,

        /// <summary>
        /// When the operand is null and is the right operand of the current clause, this flag indicates
        /// that the clause is absolutely true, regardless of the value of the left operand. For example,
        /// this is set for the right operand of a strict positive operator or a non-strict GE operator,
        /// since anything is GE null. Note that if either operand has the false flag, that takes priority.
        /// </summary>
        TrueRightAbs = 0x04,

        /// <summary>
        /// When the operand is null and is the left operand of the current clause, this flag indicates
        /// that the clause is true conditioned on the right operand being non-null. For example, this
        /// is set for the left operand of a non-strict LT operator since null is LT any non-null value.
        /// The right operand will have the <see cref="False"/> flag. If the right operand is not opt,
        /// then the type system enforces the guarantee, in which case the left operand can be marked with
        /// the stronger <see cref="TrueLeftAbs"/>.
        /// </summary>
        TrueLeftCnd = 0x08,

        /// <summary>
        /// When the operand is null and is the right operand of the current clause, this flag indicates
        /// that the clause is true conditioned on the left operand being non-null. For example, this
        /// is set for the right operand of a non-strict GT operator since any non-null value is GT null.
        /// The left operand will have the <see cref="False"/> flag. If the left operand is not opt,
        /// then the type system enforces the guarantee, in which case the right operand can be marked with
        /// the stronger <see cref="TrueRightAbs"/>.
        /// </summary>
        TrueRightCnd = 0x10,

        /// <summary>
        /// The combination of <see cref="TrueLeftAbs"/> and <see cref="TrueLeftCnd"/>.
        /// </summary>
        TrueLeft = TrueLeftAbs | TrueLeftCnd,

        /// <summary>
        /// The combination of <see cref="TrueRightAbs"/> and <see cref="TrueRightCnd"/>.
        /// </summary>
        TrueRight = TrueRightAbs | TrueRightCnd,

        /// <summary>
        /// The flags that are relevant for a left operand.
        /// </summary>
        Left = False | TrueLeftAbs | TrueLeftCnd,

        /// <summary>
        /// The flags that are relevant for a right operand.
        /// </summary>
        Right = False | TrueRightAbs | TrueRightCnd,
    }

    /// <summary>
    /// Gets the root type of the operands. Note that some args may be opt while others are not.
    /// This is always the "required" form of the type (when there is a required form).
    /// </summary>
    public DType ArgType { get; }

    /// <summary>
    /// Gets the kind of the operands.
    /// </summary>
    public DKind ArgKind => ArgType.RootKind;

    /// <summary>
    /// Whether any of the operators are ordered.
    /// </summary>
    public bool HasOrdered { get; }

    /// <summary>
    /// This is the indices of operands that are opt.
    /// </summary>
    public BitSet IsOpt { get; }

    /// <summary>
    /// Whether any of the operands are opt.
    /// </summary>
    public bool HasOpt => !IsOpt.IsEmpty;

    /// <summary>
    /// This is parallel to <see cref="Args"/>. It indicates how a null value for the operand should
    /// be handled. Note that this has values propagated. For example, in `a = b >= 3`, we know that
    /// `b` can't be `null` so will have <see cref="NullTo.False"/> set. By propagation,
    /// `a` also can't be `null`, so will also have the <see cref="NullTo.False"/> flag.
    /// </summary>
    public Immutable.Array<NullTo> NullToFlags { get; }

    /// <summary>
    /// The operands.
    /// </summary>
    public ArgTuple Args { get; }

    /// <summary>
    /// The operators. Note that some of these may be "None".
    /// </summary>
    public Immutable.Array<CompareOp> Ops { get; }

    private BndCompareNode(DType typeArg, bool hasOrdered, BitSet isOpt,
            Immutable.Array<CompareOp> ops, ArgTuple args)
        : base(DType.BitReq, args.Length, GetKinds(args), GetCount(args))
    {
        Validation.Assert(!typeArg.HasReq);
        Validation.Assert(hasOrdered ? typeArg.IsComparable : typeArg.IsEquatable, nameof(args));
        Validation.Assert(!ops.IsDefaultOrEmpty);
        Validation.Assert(ops[0] != CompareOp.None);
        Validation.Assert(ops[ops.Length - 1] != CompareOp.None);
        Validation.Assert(hasOrdered == ops.Any(op => op.IsOrdered));
        Validation.Assert(args.Length == ops.Length + 1);
        Validation.Assert(args.All(a => a is not null && (a.Type == typeArg || a.Type == typeArg.ToOpt())));

        ArgType = typeArg;
        HasOrdered = hasOrdered;
        IsOpt = isOpt;
        Ops = ops;
        Args = args;
        NullToFlags = ComputeNullTos(ops, isOpt);
    }

    /// <summary>
    /// Given a chain of comparison operators and a bit set indicating which args have opt type,
    /// returns the immuatable array <see cref="NullTo"/> values for the args.
    /// </summary>
    public static Immutable.Array<NullTo> ComputeNullTos(Immutable.Array<CompareOp> ops, BitSet isOpt)
    {
        Validation.BugCheck(!ops.IsDefaultOrEmpty);

        // Determine the NullToFlags values. Initialize with non-opt sending null to false.
        var nullTo = Immutable.Array<NullTo>.CreateBuilder(ops.Length + 1, init: true);
        for (int i = 0; i < nullTo.Count; i++)
        {
            if (!isOpt.TestBit(i))
                nullTo[i] |= NullTo.False;
        }

        // Do a pass in each direction.
        for (int pass = 0; pass < 2; pass++)
        {
            int iop = pass == 0 ? 0 : ops.Length - 1;
            int inc = pass == 0 ? 1 : -1;
            for (int i = 0; i < ops.Length; i++, iop += inc)
            {
                var op = ops[iop];
                if (op.IsStrict)
                {
                    // Strict operators are easy. Null goes to true/false depending on not.
                    if (op.IsNot)
                    {
                        nullTo[iop] |= NullTo.TrueLeftAbs;
                        nullTo[iop + 1] |= NullTo.TrueRightAbs;
                    }
                    else
                    {
                        nullTo[iop] |= NullTo.False;
                        nullTo[iop + 1] |= NullTo.False;
                    }
                }
                else
                {
                    op = op.SimplifyForTotalOrder();
                    Validation.Assert(!op.IsNot || op.Root == CompareRoot.Equal);
                    switch (op.Root)
                    {
                    case CompareRoot.Equal:
                        // If one can't be null, null in the other maps to true/false, depending on not.
                        if ((nullTo[iop] & NullTo.False) != 0)
                        {
                            if (!op.IsNot)
                                nullTo[iop + 1] |= NullTo.False;
                            else if (!isOpt.TestBit(iop))
                                nullTo[iop + 1] |= NullTo.TrueRightAbs;
                            else
                                nullTo[iop + 1] |= NullTo.TrueRightCnd;
                        }
                        if ((nullTo[iop + 1] & NullTo.False) != 0)
                        {
                            if (!op.IsNot)
                                nullTo[iop] |= NullTo.False;
                            else if (!isOpt.TestBit(iop + 1))
                                nullTo[iop] |= NullTo.TrueLeftAbs;
                            else
                                nullTo[iop] |= NullTo.TrueLeftCnd;
                        }
                        break;
                    case CompareRoot.Less:
                        // x < null is false.
                        nullTo[iop + 1] |= NullTo.False;
                        // null < non-null is true.
                        if (!isOpt.TestBit(iop + 1))
                            nullTo[iop] |= NullTo.TrueLeftAbs;
                        else
                            nullTo[iop] |= NullTo.TrueLeftCnd;
                        break;
                    case CompareRoot.Greater:
                        // null > x is false.
                        nullTo[iop] |= NullTo.False;
                        // non-null > null is true.
                        if (!isOpt.TestBit(iop))
                            nullTo[iop + 1] |= NullTo.TrueRightAbs;
                        else
                            nullTo[iop + 1] |= NullTo.TrueRightCnd;
                        break;
                    case CompareRoot.GreaterEqual:
                        // x >= null is true.
                        nullTo[iop + 1] |= NullTo.TrueRightAbs;
                        // null >= non-null is false.
                        if ((nullTo[iop + 1] & NullTo.False) != 0)
                            nullTo[iop] |= NullTo.False;
                        break;
                    case CompareRoot.LessEqual:
                        // null <= x is true.
                        nullTo[iop] |= NullTo.TrueLeftAbs;
                        // non-null <= null is false.
                        if ((nullTo[iop] & NullTo.False) != 0)
                            nullTo[iop + 1] |= NullTo.False;
                        break;
                    }
                }
            }
        }

        // For any that have NullTo.False set, erase the true flags.
        for (int i = 0; i < nullTo.Count; i++)
        {
            if ((nullTo[i] & NullTo.False) != 0)
                nullTo[i] = NullTo.False;
        }

#if DEBUG
        for (int iop = 0; iop < ops.Length; iop++)
        {
            // Ignore none ops.
            if (ops[iop].Root == CompareRoot.None)
                continue;
            // If one can't be null, the other's nullTo value should be true or false, not none.
            int jop = iop + 1;
            if ((nullTo[iop] & NullTo.False) != 0)
                Validation.Assert((nullTo[jop] & NullTo.Right) != 0);
            if (nullTo[jop] == NullTo.False)
                Validation.Assert((nullTo[iop] & NullTo.Left) != 0);
        }
#endif

        return nullTo.ToImmutable();
    }

    public static BndCompareNode Create(Immutable.Array<CompareOp> ops, ArgTuple args)
    {
        Validation.BugCheckParam(!ops.IsDefaultOrEmpty, nameof(ops));
        Validation.BugCheckParam(ops[0] != CompareOp.None, nameof(ops));
        Validation.BugCheckParam(ops[ops.Length - 1] != CompareOp.None, nameof(ops));
        Validation.BugCheckParam(args.Length == ops.Length + 1, nameof(args));

        Validation.BugCheckParam(args[0] is not null, nameof(args));
        var type = args[0].Type.ToReq();
        BitSet isOpt = default;
        if (args[0].Type.IsOpt)
            isOpt = isOpt.SetBit(0);

        bool hasOrdered = false;
        for (int i = 0; i < ops.Length; i++)
        {
            var arg = args[i + 1];
            Validation.BugCheckParam(arg is not null, nameof(args));
            Validation.BugCheckParam(arg.Type.ToReq() == type, nameof(args));
            if (arg.Type.IsOpt)
                isOpt = isOpt.SetBit(i + 1);

            var op = ops[i];
            if (op == CompareOp.None)
                Validation.BugCheck(ops[i - 1] != CompareOp.None, nameof(ops));
            else
                hasOrdered |= op.IsOrdered;
        }
        Validation.BugCheckParam(hasOrdered ? type.IsComparable : type.IsEquatable, nameof(args));

        return new BndCompareNode(type, hasOrdered, isOpt, ops, args);
    }

    public static BndCompareNode Create(CompareOp op, BoundNode arg0, BoundNode arg1)
    {
        Validation.BugCheckParam(op != CompareOp.None, nameof(op));
        Validation.BugCheckValue(arg0, nameof(arg0));
        Validation.BugCheckValue(arg1, nameof(arg1));

        var type = arg0.Type.ToReq();
        bool hasOrdered = op.IsOrdered;

        Validation.BugCheckParam(hasOrdered ? type.IsComparable : type.IsEquatable, nameof(arg0));
        Validation.BugCheckParam(arg1.Type == type || arg1.Type == type.ToOpt(), nameof(arg1));

        BitSet isOpt = default;
        if (arg0.Type.IsOpt)
            isOpt = isOpt.SetBit(0);
        if (arg1.Type.IsOpt)
            isOpt = isOpt.SetBit(1);

        return new BndCompareNode(type, hasOrdered, isOpt,
            Immutable.Array.Create(op), ArgTuple.Create(arg0, arg1));
    }

    /// <summary>
    /// This is a hack for testing. It creates a new <see cref="BndCompareNode"/> with the indicated op
    /// replaced with None. This throws if the iop is the first or last op, or adjacent to another None.
    /// </summary>
    public BndCompareNode SuppressOp(int iop)
    {
        Validation.BugCheckParam(0 < iop && iop < Ops.Length - 1, nameof(iop));
        Validation.BugCheckParam(Ops[iop - 1] != CompareOp.None, nameof(iop));
        Validation.BugCheckParam(Ops[iop + 1] != CompareOp.None, nameof(iop));

        var ops = Ops.SetItem(iop, CompareOp.None);
        bool hasOrdered = ops.Any(op => op.IsOrdered);

        return new BndCompareNode(ArgType, hasOrdered, IsOpt, Ops.SetItem(iop, CompareOp.None), Args);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var arg in Args)
            cur = arg.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndCompareNode;
        Validation.Assert(other != null);

        if (!EquivArgs(Args, other.Args, scopeMap, globalTable))
            return false;

        Validation.Assert(Ops.Length == other.Ops.Length);
        for (int i = 0; i < Ops.Length; i++)
        {
            if (Ops[i] != other.Ops[i])
                return false;
        }

        return true;
    }
}

/// <summary>
/// An <see cref="ArgScope"/> introduces a single value and is typically "owned" by a particular slot
/// of a particular BndCallNode. Note that the scope doesn't know its parent/owner.
/// </summary>
public sealed class ArgScope
{
    /// <summary>
    /// This is the only <see cref="BndScopeRefNode"/> for this scope.
    /// </summary>
    private readonly ScopeRef _ref;

    /// <summary>
    /// This can be "updated" while building a module.
    /// REVIEW: Is there a better way?
    /// </summary>
    private DType _type;

    /// <summary>
    /// Whether this is an index for one or more "item" scopes.
    /// </summary>
    public bool IsIndex => Kind == ScopeKind.SeqIndex;

    /// <summary>
    /// The kind of scope.
    /// </summary>
    public ScopeKind Kind { get; }

    /// <summary>
    /// The type of the value associated with this scope.
    /// </summary>
    public DType Type => _type;

    /// <summary>
    /// Creates a non-index scope.
    /// </summary>
    public static ArgScope Create(ScopeKind kind, DType type)
    {
        Validation.BugCheckParam(ScopeKind.None < kind && kind < ScopeKind.SeqIndex, nameof(kind));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(kind != ScopeKind.Range || type == DType.I8Req, nameof(type));
        return new ArgScope(kind, type);
    }

    /// <summary>
    /// Creates a <see cref="ScopeKind.SeqIndex"/> scope.
    /// </summary>
    public static ArgScope CreateIndex()
    {
        return new ArgScope(ScopeKind.SeqIndex, DType.I8Req);
    }

    /// <summary>
    /// Creates a module tuple scope, only to be used when constructing a module. This is
    /// used for the items and the externals.
    /// </summary>
    internal static ArgScope CreateModuleTupleScope(out Action<DType> updateType)
    {
        var scope = new ArgScope(ScopeKind.With, DType.EmptyTupleReq);
        updateType = scope.UpdateType;
        return scope;
    }

    private void UpdateType(DType type)
    {
        // REVIEW: This assumes thread safety isn't an issue, which it shouldn't be during
        // module binding.
        Validation.Assert(_type.IsTupleReq);
        Validation.Assert(_type.IsPrefixTuple(type));

        _type = type;
        _ref.Update(type);
    }

    private ArgScope(ScopeKind kind, DType type)
    {
        Validation.Assert(ScopeKind.None < kind & kind < ScopeKind._Lim);
        Validation.Assert(type.IsValid);
        Validation.Assert(kind != ScopeKind.Range && kind != ScopeKind.SeqIndex || type == DType.I8Req);
        Kind = kind;
        _type = type;
        _ref = new ScopeRef(this);
    }

    internal BndScopeRefNode GetReference() => _ref.VerifyValue();

    /// <summary>
    /// Whether the given arg is compatible with this scope.
    /// </summary>
    public bool IsValidArg(BoundNode arg)
    {
        if (arg == null)
            return false;

        switch (Kind)
        {
        case ScopeKind.SeqItem:
            return arg.Type == Type.ToSequence();
        case ScopeKind.TenItem:
            return arg.Type.IsTensorReq && arg.Type.GetTensorItemType() == Type;

        case ScopeKind.Iter:
        case ScopeKind.With:
            return arg.Type == Type;

        case ScopeKind.Guard:
            if (Type.IsOpt)
                return arg.Type == Type;
            Validation.Assert(Type.SeqCount == 0);
            return arg.Type == Type.ToOpt();

        case ScopeKind.Range:
            Validation.Assert(Type == DType.I8Req);
            return arg.Type == DType.I8Req;

        default:
            Validation.Assert(false);
            return false;
        }
    }

    public override string ToString()
    {
        return string.Format("Scope({0}, {1})", Kind, Type);
    }

    private sealed class ScopeRef : BndScopeRefNode
    {
        public override BndNodeKind Kind { get; }

        public ScopeRef(ArgScope scope)
            : base(scope)
        {
            Validation.Assert(scope._ref == null);
            Kind = Scope.IsIndex ? BndNodeKind.IndScopeRef : BndNodeKind.ArgScopeRef;
        }

        public void Update(DType type)
        {
            UpdateType(type);
        }
    }
}

/// <summary>
/// Represents a "free variable" for "symbolic" processing.
/// </summary>
public sealed partial class BndFreeVarNode : BndLeafNode
{
    public override bool IsCheap => true;

    /// <summary>
    /// An optional id, with unspecified semantics (depends on the context).
    /// </summary>
    public int Id { get; }

    private BndFreeVarNode(DType type, int id)
        : base(type)
    {
        Validation.Assert(type.IsValid);
        Id = id;
    }

    public static BndFreeVarNode Create(DType type, int id)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return new BndFreeVarNode(type, id);
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        return bnd == this;
    }

    public override string ToString()
    {
        return string.Format("${0}", Id);
    }
}

/// <summary>
/// Base class for owners of scopes.
/// </summary>
public abstract class BndScopeOwnerNode : BndParentNode
{
    public abstract override bool OwnsScopes { get; }

    public override BndNodeKindMask ThisKindMask => OwnsScopes ?
        base.ThisKindMask | BndNodeKindMask.ScopeOwner :
        base.ThisKindMask;

    protected BndScopeOwnerNode(DType type, int childCount, BndNodeKindMask childKinds, int nodeCount)
        : base(type, childCount, childKinds, nodeCount)
    {
    }

    protected static void VerifyScopeDecls(BoundNode bnd, ScopeTuple many, ArgScope one)
    {
        if (many.IsDefaultOrEmpty && one == null)
            return;
        if ((bnd.AllKinds & BndNodeKindMask.ScopeOwner) == 0)
            return;

        var (owner, scope) = ScopeCounter.FindDecl(bnd, many, one);
        Validation.Assert((owner != null) == (scope != null));
        if (owner != null)
            throw Validation.BugExcept("Duplicate scope definition");
    }

    protected static void VerifyScopeDecls(ArgTuple args, ScopeTuple many, ArgScope one)
    {
        if (many.IsDefaultOrEmpty && one == null)
            return;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if ((arg.AllKinds & BndNodeKindMask.ScopeOwner) == 0)
                continue;
            var (owner, scope) = ScopeCounter.FindDecl(arg, many, one);
            Validation.Assert((owner != null) == (scope != null));
            if (owner != null)
                throw Validation.BugExcept("Duplicate scope definition");
        }
    }
}

/// <summary>
/// A function or procedure invocation. This is used for the three kinds <see cref="BndNodeKind.Call"/>,
/// <see cref="BndNodeKind.CallVolatile"/>, and <see cref="BndNodeKind.CallProcedure"/>.
/// </summary>
public sealed partial class BndCallNode : BndScopeOwnerNode
{
    public override BndNodeKind Kind { get; }

    public override bool OwnsScopes { get; }

    /// <summary>
    /// The operation being invoked.
    /// </summary>
    public RexlOper Oper { get; }

    /// <summary>
    /// Whether this is a volatile invocation of a function.
    /// </summary>
    public bool IsVolatile => Kind == BndNodeKind.CallVolatile;

    /// <summary>
    /// The arguments.
    /// </summary>
    public ArgTuple Args { get; }

    /// <summary>
    /// Traits of the arguments positions.
    /// </summary>
    public ArgTraits Traits { get; }

    /// <summary>
    /// Associated scopes ordered by the slot in which they are introduced.
    /// </summary>
    public ScopeTuple Scopes { get; }

    /// <summary>
    /// Associated index scopes ordered by the slot in which their associated scope is introduced.
    /// An entry is null to indicate that it isn't needed.
    /// </summary>
    public ScopeTuple Indices { get; }

    /// <summary>
    /// If not default, contains the directive for each slot, any or all of which may be <see cref="Directive.None"/>.
    /// </summary>
    public DirTuple Directives { get; }

    /// <summary>
    /// If not default, contains the name for each slot, any or all of which may be null.
    /// </summary>
    public NameTuple Names { get; }

    /// <summary>
    /// Whether this call has been validated by the <see cref="Oper"/> to be suitable for binding,
    /// but perhaps not for code generation.
    /// </summary>
    public bool Certified { get; }

    /// <summary>
    /// Whether this call has been validated by the <see cref="Oper"/> to be suitable for all
    /// purposes, including code generation.
    /// </summary>
    public bool CertifiedFull { get; }

    private BndCallNode(RexlOper oper, DType type, ArgTraits traits, ArgTuple args,
            ScopeTuple scopes, ScopeTuple indices, DirTuple dirs, NameTuple names)
        : base(type, args.Length, GetKinds(args), GetCount(args))
    {
        Validation.AssertValue(oper);
        Validation.Assert(type.IsValid);
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == oper);
        Validation.Assert(!args.IsDefault);
        Validation.Assert(oper.SupportsArity(args.Length));
        Validation.Assert(traits.SlotCount == args.Length);
        Validation.Assert(!scopes.IsDefault);
        Validation.Assert(traits.ScopeCount == scopes.Length);
        Validation.Assert(scopes.All(s => s != null && !s.IsIndex));
        Validation.Assert(!indices.IsDefault);
        Validation.Assert(indices.All(s => s == null || s.IsIndex));

        // If there is a mismatch, we create blank indices below.
        Validation.Assert(traits.ScopeIndexCount == indices.Length || indices.Length == 0);

        Validation.Assert(dirs.IsDefault || dirs.Length == args.Length);
        Validation.Assert(names.IsDefault || names.Length == args.Length);

        OwnsScopes = scopes.Length > 0;

        // If there are any indices that aren't used, set them to null.
        if (indices.Length > 0)
        {
            Validation.Assert(OwnsScopes);

            BitSet used = default;
            for (int slot = 0; slot < traits.SlotCount; slot++)
            {
                if (!traits.IsScope(slot, out int iscope, out int iidx, out bool firstForIdx))
                    continue;
                if (iidx < 0)
                    continue;
                if (!firstForIdx)
                    continue;
                Validation.AssertIndex(iscope, traits.ScopeCount);
                Validation.AssertIndex(iidx, traits.ScopeIndexCount);
                Validation.Assert(!used.TestBit(iidx));
                var ind = indices[iidx];
                if (ind == null)
                    continue;

                // We have found the first slot that introduces a scope with an index which we may want to set to null if not used.
                // Maintain the proper upward index to refer to this scope while we check each following arg for references:
                // start at 0, and increment for each newly introduced scope in the following slots.
                int upCount = 0;
                for (int iarg = slot + 1; iarg < traits.SlotCount; iarg++)
                {
                    if (traits.IsScopeActive(iarg, upCount) && (args[iarg].AllKinds & BndNodeKindMask.IndScopeRef) != 0)
                    {
                        if (ScopeCounter.Any(args[iarg], ind))
                        {
                            used = used.SetBit(iidx);
                            break;
                        }
                    }
                    if (traits.IsScope(iarg))
                        upCount++;
                }
            }

            ScopeTuple.Builder bldr = null;
            for (int iidx = 0; iidx < indices.Length; iidx++)
            {
                var ind = indices[iidx];
                if (ind == null || used.TestBit(iidx))
                    continue;
                bldr ??= indices.ToBuilder();
                bldr[iidx] = null;
            }
            if (bldr != null)
                indices = bldr.ToImmutable();
        }
        else if (traits.ScopeIndexCount > 0)
            indices = ScopeTuple.CreateBuilder(traits.ScopeIndexCount, init: true).ToImmutable();

        Oper = oper;
        Kind = oper.IsProc ?
            BndNodeKind.CallProcedure :
            oper.IsVolatile(traits, type, args, scopes, indices, dirs, names) ?
                BndNodeKind.CallVolatile : BndNodeKind.Call;

        Args = args;
        Traits = traits;
        Scopes = scopes;
        Indices = indices;
        Directives = dirs;
        Names = names;

        Validation.Assert(!Certified);
        Certified = Oper.Certify(this, out bool full);
        CertifiedFull = full;

        // We insist on certification (but not full). Otherwise, there is a bug somewhere.
        Validation.BugCheck(Certified, "BndCallNode certification failed");
    }

    public static BndCallNode Create(RexlOper oper, DType type, ArgTuple args)
    {
        return Create(oper, type, args, ScopeTuple.Empty, ScopeTuple.Empty, default, default, null);
    }

    public static BndCallNode Create(RexlOper oper, DType type, ArgTuple args, ScopeTuple scopes)
    {
        return Create(oper, type, args, scopes, ScopeTuple.Empty, default, default, null);
    }

    public static BndCallNode Create(RexlOper oper, DType type, ArgTuple args,
        ScopeTuple scopes, ScopeTuple indices)
    {
        return Create(oper, type, args, scopes, indices, default, default, null);
    }

    public static BndCallNode Create(RexlOper oper, DType type, ArgTuple args, DirTuple dirs)
    {
        return Create(oper, type, args, ScopeTuple.Empty, ScopeTuple.Empty, dirs, default, null);
    }

    public static BndCallNode Create(RexlOper oper, DType type, ArgTuple args,
        ScopeTuple scopes, ScopeTuple indices, DirTuple dirs, NameTuple names)
    {
        return Create(oper, type, args, scopes, indices, dirs, names, null);
    }

    /// <summary>
    /// Only internal code should pass in arg traits.
    /// </summary>
    internal static BndCallNode Create(RexlOper oper, DType type, ArgTuple args,
        ScopeTuple scopes, ArgTraits traits)
    {
        return Create(oper, type, args, scopes, ScopeTuple.Empty, default, default, traits);
    }

    /// <summary>
    /// Only internal code should pass in arg traits.
    /// </summary>
    internal static BndCallNode Create(RexlOper oper, DType type, ArgTuple args,
        ScopeTuple scopes, ScopeTuple indices, DirTuple dirs, NameTuple names, ArgTraits traits)
    {
        Validation.BugCheckValue(oper, nameof(oper));
        Validation.BugCheckParam(!args.IsDefault, nameof(args));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(!scopes.IsDefault, nameof(scopes));
        Validation.BugCheckParam(!indices.IsDefault, nameof(indices));
        Validation.BugCheckParam(dirs.IsDefault || dirs.Length == args.Length, nameof(dirs));
        Validation.BugCheckParam(names.IsDefault || names.Length == args.Length, nameof(names));
        Validation.BugCheckParam(traits == null || traits.Oper == oper, nameof(traits));
        Validation.BugCheckParam(traits == null || traits.SlotCount == args.Length, nameof(traits));

        // See if there is a function to substitute.
        var oper2 = oper.ReduceTo;
        if (oper2 != null && oper2 != oper)
        {
            oper = oper2;
            traits = null;
        }
        Validation.BugCheckParam(oper.SupportsArity(args.Length), nameof(oper));

        if (traits == null)
        {
            traits = oper.GetArgTraits(args.Length, names, default, dirs);
            Validation.Assert(traits.Oper == oper);
            Validation.Assert(traits.SlotCount == args.Length);
        }
        Validation.Assert(traits.Oper == oper);
        Validation.Assert(traits.SlotCount == args.Length);

        Validation.BugCheckParam(traits.ScopeCount == scopes.Length, nameof(scopes));

        for (int slot = 0; slot < args.Length; slot++)
            Validation.BugCheckValue(args[slot], nameof(args));

        if (scopes.Length > 0)
        {
            // Validate the scope kinds and types.
            int iscopeNext = 0;
            for (int slot = 0; slot < args.Length; slot++)
            {
                if (!traits.IsScope(slot, out int iscope))
                    continue;
                Validation.Assert(iscopeNext == iscope);
                var scope = scopes[iscope];
                Validation.BugCheckParam(scope != null && !scope.IsIndex, nameof(scopes));
                var sk = traits.GetScopeKind(slot);
                Validation.BugCheckParam(sk == scope.Kind ||
                    sk == ScopeKind.Guard && scope.Kind == ScopeKind.With, nameof(scopes));
                Validation.BugCheckParam(scope.IsValidArg(args[slot]), nameof(args));

                // Ensure that the scopes are unique.
                for (int j = 0; j < iscope; j++)
                    Validation.BugCheckParam(scopes[j] != scope, nameof(scopes), "Duplicate scope");

                iscopeNext++;
            }
            Validation.Assert(iscopeNext == scopes.Length);

            VerifyScopeDecls(args, scopes, null);
        }

        // Note that indices.Length can be zero even when traits.ScopeIndexCount is not. In that case
        // the ctor fixes things up with an all null indices array.
        if (indices.Length > 0)
        {
            Validation.BugCheckParam(traits.ScopeIndexCount == indices.Length, nameof(indices));
            int count = 0;
            ArgScope first = null;
            for (int i = 0; i < indices.Length; i++)
            {
                var ind = indices[i];
                if (ind == null)
                    continue;
                Validation.BugCheckParam(ind.IsIndex, nameof(indices));
                if (count == 0)
                    first = ind;
                count++;

                // Ensure that the indices are unique.
                for (int j = 0; j < i; j++)
                    Validation.BugCheckParam(indices[j] != ind, nameof(indices), "Duplicate index scope");
            }

            if (count == 1)
                VerifyScopeDecls(args, default, first);
            else if (count > 0)
                VerifyScopeDecls(args, indices, null);
        }

        return new BndCallNode(oper, type, traits, args, scopes, indices, dirs, names);
    }

    /// <summary>
    /// Returns a new call node with the args replaced. The given <paramref name="args"/>
    /// must have the same types as <see cref="Args"/>.
    /// </summary>
    public BndCallNode SetArgs(ArgTuple args)
    {
        Validation.BugCheckParam(!args.IsDefault && args.Length == Args.Length, nameof(args));
        for (int i = 0; i < args.Length; i++)
            Validation.BugCheckParam(args[i] != null && args[i].Type == Args[i].Type, nameof(args));

        if (OwnsScopes)
        {
            VerifyScopeDecls(args, Scopes, null);
            if (Indices.Length > 0)
            {
                int count = 0;
                ArgScope first = null;
                for (int i = 0; i < Indices.Length; i++)
                {
                    var ind = Indices[i];
                    if (ind == null)
                        continue;
                    Validation.Assert(ind.IsIndex);
                    if (count == 0)
                        first = ind;
                    count++;
                }

                if (count == 1)
                    VerifyScopeDecls(args, default, first);
                else if (count > 0)
                    VerifyScopeDecls(args, Indices, null);
            }
        }

        var res = new BndCallNode(Oper, Type, Traits, args, Scopes, Indices, Directives, Names);

        // A non-volatile invocation shouldn't be changed to a volatile one.
        Validation.BugCheck(!res.IsVolatile || IsVolatile);

        return res;
    }

    /// <summary>
    /// Get the directive for the given slot, if there is one.
    /// </summary>
    public Directive GetDirective(int slot)
    {
        Validation.BugCheckIndex(slot, Args.Length, nameof(slot));
        if (Directives.IsDefault)
            return Directive.None;
        return Directives[slot];
    }

    /// <summary>
    /// Get the name for the given slot, if there is one.
    /// </summary>
    public DName GetName(int slot)
    {
        Validation.BugCheckIndex(slot, Args.Length, nameof(slot));
        if (Names.IsDefault)
            return default;
        return Names[slot];
    }

    public override (long min, long max) GetItemCountRange()
    {
        Validation.BugCheck(Type.IsSequence);
        return Oper.GetItemCountRange(this);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var arg in Args)
            cur = arg.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndCallNode;
        Validation.Assert(other != null);

        if (Oper != other.Oper)
            return false;
        if (Args.Length != other.Args.Length)
            return false;
        if (Directives.IsDefault != other.Directives.IsDefault)
            return false;
        if (Names.IsDefault != other.Names.IsDefault)
            return false;
        Validation.Assert(Scopes.Length == other.Scopes.Length);

        // Check the args.
        int count = Util.Size(scopeMap);
        int iscope = 0;
        try
        {
            for (int slot = 0; slot < Args.Length; slot++)
            {
                if (!Args[slot].Equivalent(other.Args[slot], scopeMap, globalTable))
                    return false;
                if (!Directives.IsDefault && Directives[slot] != other.Directives[slot])
                    return false;
                // REVIEW: Perhaps we can ignore names that are in slots with a scope.
                // Map(x: Range(10), x+1) and Map(y: Range(10), y+1) can be considered equivalent.
                if (!Names.IsDefault && Names[slot] != other.Names[slot])
                    return false;
                bool isScope = Traits.IsScope(slot, out int iscopeCur);
                bool isScope2 = other.Traits.IsScope(slot, out int iscopeCur2);
                if (isScope != isScope2)
                    return false;
                if (isScope)
                {
                    // REVIEW: If these asserts fail, the arg traits are invalid. Should we bug-check for
                    // that somewhere?
                    Validation.Assert(iscopeCur == iscope);
                    Validation.Assert(iscopeCur2 == iscope);
                    Validation.AssertIndex(iscope, Scopes.Length);
                    Util.Add(ref scopeMap, Scopes[iscope], other.Scopes[iscope]);
                    iscope++;
                }
            }
            Validation.Assert(iscope == Scopes.Length);
        }
        finally
        {
            Validation.Assert(Util.Size(scopeMap) == count + iscope);
            for (int i = 0; i < iscope; i++)
                scopeMap.Remove(Scopes[i]);
            Validation.Assert(Util.Size(scopeMap) == count);
        }

        // REVIEW: We assume that the ArgTraits are determined solely by the arity, function, names
        // and directives. Since those are the same, we can assert that the ArgTraits are equivalent.
        Validation.Assert(Traits.AreEquivalent(other.Traits));

        return true;
    }
}

/// <summary>
/// A bound if expression.
/// REVIEW: Perhaps make this variadic.
/// </summary>
public sealed partial class BndIfNode : BndParentNode
{
    /// <summary>
    /// The condition value.
    /// </summary>
    public BoundNode CondValue { get; }

    /// <summary>
    /// The value when the condition is true.
    /// </summary>
    public BoundNode TrueValue { get; }

    /// <summary>
    /// The value when the condition is false.
    /// </summary>
    public BoundNode FalseValue { get; }

    private BndIfNode(DType type, BoundNode condValue, BoundNode trueValue, BoundNode falseValue)
        : base(type, 3,
              GetKinds(condValue) | GetKinds(trueValue) | GetKinds(falseValue),
              GetCount(condValue) + GetCount(trueValue) + GetCount(falseValue))
    {
        Validation.AssertValue(condValue);
        Validation.Assert(condValue.Type == DType.BitReq);
        Validation.AssertValue(trueValue);
        Validation.AssertValue(falseValue);
        Validation.Assert(trueValue.Type == Type);
        Validation.Assert(falseValue.Type == Type);

        CondValue = condValue;
        TrueValue = trueValue;
        FalseValue = falseValue;
    }

    public static BndIfNode Create(BoundNode condValue, BoundNode trueValue, BoundNode falseValue)
    {
        Validation.BugCheckValue(condValue, nameof(condValue));
        Validation.BugCheckParam(condValue.Type == DType.BitReq, nameof(condValue));
        Validation.BugCheckValue(trueValue, nameof(trueValue));
        Validation.BugCheckValue(falseValue, nameof(falseValue));
        Validation.BugCheckParam(trueValue.Type == falseValue.Type, nameof(falseValue));

        return new BndIfNode(trueValue.Type, condValue, trueValue, falseValue);
    }

    public override (long min, long max) GetItemCountRange()
    {
        var (min0, max0) = TrueValue.GetItemCountRange();
        var (min1, max1) = FalseValue.GetItemCountRange();

        if (CondValue.TryGetBool(out bool cond))
            return cond ? (min0, max0) : (min1, max1);

        return (Math.Min(min0, min1), Math.Max(max0, max1));
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = CondValue.Accept(visitor, cur);
        cur = TrueValue.Accept(visitor, cur);
        cur = FalseValue.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndIfNode;
        Validation.Assert(other != null);

        if (!CondValue.Equivalent(other.CondValue, scopeMap, globalTable))
            return false;
        if (!TrueValue.Equivalent(other.TrueValue, scopeMap, globalTable))
            return false;
        if (!FalseValue.Equivalent(other.FalseValue, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// A bound record node.
/// </summary>
public sealed partial class BndRecordNode : BndParentNode
{
    /// <summary>
    /// The name/value items. Note that the names are fields of the
    /// record type, but not necessarily all the fields. That is, some field
    /// names of the type may be missing from this.
    /// </summary>
    public NamedItems Items { get; }

    /// <summary>
    /// Has no semantic meaning, but conveys field name ordering.
    /// Generally, the names will be field names, but are not guaranteed to
    /// be so, nor are they guaranteed to be in <see cref="Items"/>.
    /// </summary>
    public NameTuple NameHints { get; }

    private BndRecordNode(DType type, NamedItems items, NameTuple nameHints = default)
        : base(type, items.Count, GetKinds(items), GetCount(items))
    {
        Validation.Assert(Type.IsRecordReq);
        Validation.Assert(items != null && items.NeedName && items.NeedNode);
        Validation.Assert(items.GetPairs().All(item => item.val != null && item.val.Type == type.GetNameTypeOrDefault(item.key)), nameof(items));

        Items = items;
        NameHints = nameHints;
    }

    public static BndRecordNode Create(DType type, NamedItems items, NameTuple nameHints = default)
    {
        Validation.BugCheckParam(type.IsRecordReq, nameof(type));
        Validation.BugCheckParam(items != null && items.NeedName && items.NeedNode, nameof(items));
        Validation.BugCheckParam(items.GetPairs().All(item => item.val != null && item.val.Type == type.GetNameTypeOrDefault(item.key)), nameof(items));

        return new BndRecordNode(type, items, nameHints);
    }

    /// <summary>
    /// Creates a <see cref="BndRecordNode"/> from the given arrays of <paramref name="names"/> and
    /// <paramref name="values"/>, which must be of the same length.
    /// </summary>
    public static BndRecordNode Create(NameTuple names, ArgTuple values)
    {
        Validation.BugCheckParam(!names.IsDefault, nameof(names));
        Validation.BugCheckParam(!values.IsDefault, nameof(values));
        Validation.BugCheckParam(values.Length == names.Length, nameof(values));

        int count = names.Length;
        var type = DType.EmptyRecordReq;
        var items = NamedItems.Empty;
        switch (count)
        {
        case 0:
            break;
        case 1:
            Validation.BugCheckParam(names[0].IsValid, nameof(names));
            Validation.BugCheckValue(values[0], nameof(values));
            items = items.SetItem(names[0], values[0]);
            type = type.SetNameType(names[0], values[0].Type);
            break;
        default:
            {
                var types = Immutable.Array<DType>.CreateBuilder(count, init: true);
                for (int i = 0; i < count; i++)
                {
                    Validation.BugCheckParam(names[i].IsValid, nameof(names));
                    Validation.BugCheckValue(values[i], nameof(values));
                    types[i] = values[i].Type;
                }
                items = items.SetItems(names, values);
                type = DType.CreateRecord(false, names, types.ToImmutable());
            }
            break;
        }

        return new BndRecordNode(type, items, names);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var value in Items.GetValues())
            cur = value.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndRecordNode;
        Validation.Assert(other != null);

        if (Type != other.Type)
            return false;
        if (Items.Count != other.Items.Count)
            return false;
        if (!EquivNames(NameHints, other.NameHints))
            return false;
        if (!EquivItems(Items, other.Items, scopeMap, globalTable))
            return false;
        return true;
    }
}

/// <summary>
/// A bound sequence node.
/// </summary>
public sealed partial class BndSequenceNode : BndParentNode
{
    /// <summary>
    /// The items.
    /// </summary>
    public ArgTuple Items { get; }

    private BndSequenceNode(DType type, ArgTuple items)
        : base(type, items.Length, GetKinds(items), GetCount(items))
    {
        Validation.Assert(type.IsSequence);
        Validation.Assert(items.Length > 0);
        Validation.AssertAllValues(items);
        Validation.Assert(items.All(item => item.Type == type.ItemTypeOrThis));
        Items = items;
    }

    public static BoundNode CreateEmpty(DType type)
    {
        Validation.BugCheckParam(type.IsSequence, nameof(type));

        // Normalize empty to null.
        return BndNullNode.Create(type);
    }

    public static BoundNode Create(DType type, ArgTuple items)
    {
        Validation.BugCheckParam(type.IsSequence, nameof(type));
        Validation.BugCheckParam(!items.IsDefault, nameof(items));

        // Normalize empty to null.
        if (items.Length == 0)
            return BndNullNode.Create(type);

        Validation.BugCheckAllValues(items, nameof(items));
        var typeItem = type.ItemTypeOrThis;
        Validation.BugCheckParam(items.All(item => item.Type == typeItem), nameof(items));
        return new BndSequenceNode(type, items);
    }

    public override (long min, long max) GetItemCountRange()
    {
        Validation.Assert(Type.IsSequence);
        var count = Items.Length;
        return (count, count);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var item in Items)
            cur = item.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndSequenceNode;
        Validation.Assert(other != null);
        return EquivArgs(Items, other.Items, scopeMap, globalTable);
    }
}

/// <summary>
/// A bound tensor node.
/// </summary>
public sealed partial class BndTensorNode : BndParentNode
{
    /// <summary>
    /// The items.
    /// </summary>
    public ArgTuple Items { get; }

    /// <summary>
    /// The shape of the tensor.
    /// </summary>
    public Shape Shape { get; }

    private BndTensorNode(DType type, ArgTuple items, Shape shape)
        : base(type, items.Length, GetKinds(items), GetCount(items))
    {
        Validation.Assert(type.IsTensorReq);
        Validation.Assert(!items.IsDefault);
        Validation.AssertAllValues(items);
        Validation.Assert(items.All(item => item.Type == type.GetTensorItemType()));
        Validation.Assert(shape.TryGetCount(out long count) && count == items.Length);
        Items = items;
        Shape = shape;
    }

    public static BoundNode CreateEmpty(DType type)
    {
        Validation.BugCheckParam(type.IsSequence, nameof(type));

        // Normalize empty to null.
        return BndNullNode.Create(type);
    }

    public static BoundNode Create(DType type, ArgTuple items, Shape shape)
    {
        Validation.BugCheckParam(type.IsTensorReq, nameof(type));
        Validation.BugCheckParam(!items.IsDefault, nameof(items));
        Validation.BugCheckAllValues(items, nameof(items));
        Validation.BugCheckParam(shape.TryGetCount(out long count) && count == items.Length, nameof(shape));

        var typeItem = type.GetTensorItemType();
        Validation.BugCheckParam(items.All(item => item.Type == typeItem), nameof(items));
        return new BndTensorNode(type, items, shape);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var item in Items)
            cur = item.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndTensorNode;
        Validation.Assert(other != null);
        return Shape == other.Shape && EquivArgs(Items, other.Items, scopeMap, globalTable);
    }
}

/// <summary>
/// A bound tuple node.
/// </summary>
public sealed partial class BndTupleNode : BndParentNode
{
    /// <summary>
    /// The items.
    /// </summary>
    public ArgTuple Items { get; }

    private BndTupleNode(DType type, ArgTuple items)
        : base(type, items.Length, GetKinds(items), GetCount(items))
    {
        Validation.Assert(type.IsTupleReq);
        Validation.Assert(!items.IsDefault);
        Validation.AssertAllValues(items);
        Validation.Assert(type.TupleArity == items.Length);
        Validation.Assert(Enumerable.Zip(type.GetTupleSlotTypes(), items, (t, arg) => arg.Type == t).All(b => b));
        Items = items;
    }

    public static BndTupleNode Create(ArgTuple items, DType type = default)
    {
        Validation.BugCheckParam(!items.IsDefault, nameof(items));
        Validation.BugCheckAllValues(items, nameof(items));
        var typeRes = DType.CreateTuple(opt: false, items.Select(arg => arg.Type));
        Validation.BugCheckParam(!type.IsValid || typeRes == type, nameof(type));
        return new BndTupleNode(typeRes, items);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var item in Items)
            cur = item.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndTupleNode;
        Validation.Assert(other != null);
        return EquivArgs(Items, other.Items, scopeMap, globalTable);
    }
}

/// <summary>
/// A bound GroupBy invocation.
/// </summary>
public sealed partial class BndGroupByNode : BndScopeOwnerNode
{
    // REVIEW: There are several possible indices that make
    // semantic sense to have in scope within [item] and [group] selectors:
    //  * Global- Index within the input sequence. Currently available in [item].
    //  * Group- Index of the group, ordered by instantiation with first member.
    //  * Local- Index within the group. Implicitly available in [group] with ForEach.
    //
    // It's unclear how exactly these indices would be referenced if
    // made to be in scope, or how useful they would be to begin with.
    // See LDM notes for 2022-11-21 for more details.

    public override bool OwnsScopes => true;

    /// <summary>
    /// The source sequence.
    /// </summary>
    public BoundNode Source { get; }

    /// <summary>
    /// The scope for evaluation of the keys.
    /// </summary>
    public ArgScope ScopeForKeys { get; }

    /// <summary>
    /// The index scope for evaluation of the keys. Null if unused.
    /// </summary>
    public ArgScope IndexForKeys { get; }

    /// <summary>
    /// Keys that are used only for grouping, but not kept in the output.
    /// </summary>
    public ArgTuple PureKeys { get; }

    /// <summary>
    /// Keys that are kept as fields in the output.
    /// </summary>
    public NamedItems KeepKeys { get; }

    /// <summary>
    /// This is parallel to the pure keys followed by keep keys and indicates which keys
    /// are case insensitive.
    /// </summary>
    public BitSet KeysCi { get; }

    /// <summary>
    /// The scope for the maps, if there are any.
    /// </summary>
    public ArgScope ScopeForMaps { get; }

    /// <summary>
    /// The (global) index scope for the maps. Null if unused.
    /// REVIEW: Should we have the others as well?
    /// </summary>
    public ArgScope IndexForMaps { get; }

    /// <summary>
    /// The names and values for the maps. Possibly empty. If there is only one Map/Agg, its name
    /// may be default. A null value indicates the "auto" value.
    /// REVIEW: Is there a better way to indicate "auto"?
    /// </summary>
    public NamedItems MapItems { get; }

    /// <summary>
    /// The scope for the aggs, if there are any.
    /// REVIEW: Should this group as a whole be indexed?
    /// </summary>
    public ArgScope ScopeForAggs { get; }

    /// <summary>
    /// The names and values for the aggs. Possibly empty. If there is only one Map/Agg, its name
    /// may be default.
    /// </summary>
    public NamedItems AggItems { get; }

    private BndGroupByNode(
            DType type, int childCount, BndNodeKindMask childKinds, int nodeCount, BoundNode source,
            ArgScope scopeKey, ArgScope indexKey, ArgTuple keysPure, NamedItems keysKeep, BitSet keysCi,
            ArgScope scopeMap, ArgScope indexMap, NamedItems maps,
            ArgScope scopeAgg, NamedItems aggs)
        : base(type, childCount, childKinds, nodeCount)
    {
#if DEBUG
        Validation.Assert(Type.SeqCount > 0);

        Validation.AssertValue(scopeKey);
        Validation.Assert(scopeKey.Kind == ScopeKind.SeqItem);
        Validation.Assert(scopeKey.Type.ToSequence() == source.Type);
        Validation.Assert(indexKey == null || indexKey.IsIndex);
        Validation.Assert(!keysPure.IsDefault);
        Validation.Assert(keysKeep != null && keysKeep.NeedName && keysKeep.NeedNode);
        Validation.Assert(keysKeep.GetKeys().All(n => Type.IsTableXxx & Type.Contains(n)));
        Validation.Assert(!keysCi.TestAtOrAbove(keysPure.Length + keysKeep.Count));

        Validation.AssertValueOrNull(scopeMap);
        if (scopeMap != null)
        {
            Validation.Assert(scopeMap.Kind == ScopeKind.SeqItem);
            Validation.Assert(scopeMap.Type.ToSequence() == source.Type);
        }
        Validation.Assert(indexMap == null || scopeMap != null && indexMap.IsIndex);
        Validation.Assert(maps != null && !maps.NeedName && !maps.NeedNode);
        Validation.Assert((maps.Count == 0) == (scopeMap == null));

        Validation.AssertValueOrNull(scopeAgg);
        if (scopeAgg != null)
        {
            Validation.Assert(scopeAgg.Kind == ScopeKind.With);
            Validation.Assert(scopeAgg.Type == source.Type);
        }
        Validation.Assert(aggs != null && !aggs.NeedName && aggs.NeedNode);
        Validation.Assert((aggs.Count == 0) == (scopeAgg == null));

        bool anyNames =
            keysKeep.Count > 0 ||
            aggs.GetKeys().Any(x => x.IsValid) ||
            maps.GetKeys().Any(x => x.IsValid);

        if (anyNames)
        {
            Validation.Assert(Type.IsTableReq);
            Validation.Assert(maps.GetKeys().All(n => Type.Contains(n)));
            Validation.Assert(aggs.GetKeys().All(n => Type.Contains(n)));
        }
        else
            Validation.Assert(maps.Count + aggs.Count <= 1);
#endif
        // For keys and maps, set their indices to null if not used.
        static bool IsUsed(ArgScope scope, IEnumerable<BoundNode> bnds)
        {
            Validation.AssertValue(scope);
            Validation.AssertValue(bnds);

            foreach (var bnd in bnds)
            {
                if (bnd == null)
                    continue;
                if (ScopeCounter.Any(bnd, scope))
                    return true;
            }
            return false;
        }

        if (indexKey != null && !IsUsed(indexKey, keysPure) && !IsUsed(indexKey, keysKeep.GetValues()))
            indexKey = null;
        if (indexMap != null && !IsUsed(indexMap, maps.GetValues()))
            indexMap = null;

        Source = source;
        ScopeForKeys = scopeKey;
        IndexForKeys = indexKey;
        PureKeys = keysPure;
        KeepKeys = keysKeep;
        KeysCi = keysCi;
        ScopeForMaps = scopeMap;
        IndexForMaps = indexMap;
        MapItems = maps;
        ScopeForAggs = scopeAgg;
        AggItems = aggs;
    }

    public static BndGroupByNode Create(
        DType type, BoundNode source,
        ArgScope scopeKey, ArgScope indexKey, ArgTuple keysPure, NamedItems keysKeep, BitSet keysCi,
        ArgScope scopeMap, ArgScope indexMap, NamedItems maps,
        ArgScope scopeAgg, NamedItems aggs)
    {
        Validation.BugCheckValue(source, nameof(source));
        Validation.BugCheckParam(!keysPure.IsDefault, nameof(keysPure));
        Validation.BugCheckAllValues(keysPure, nameof(keysPure));
        Validation.BugCheckParam(keysKeep != null && keysKeep.NeedName && keysKeep.NeedNode, nameof(keysKeep));
        Validation.BugCheckParam(aggs != null && !aggs.NeedName && aggs.NeedNode, nameof(aggs));
        Validation.BugCheckParam(maps != null && !maps.NeedName && !maps.NeedNode, nameof(maps));
        Validation.BugCheckParam(!keysCi.TestAtOrAbove(keysPure.Length + keysKeep.Count), nameof(keysCi));

        int count = 1 + keysPure.Length + keysKeep.Count + maps.Count + aggs.Count - maps.GetValues().Count(m => m == null);
        var childKinds = GetKinds(source) | GetKinds(keysPure) | GetKinds(keysKeep) | GetKinds(maps) | GetKinds(aggs);
        var totalCount = GetCount(source) + GetCount(keysPure) + GetCount(keysKeep) + GetCount(maps) + GetCount(aggs);

        // Validate the scopes.
        // REVIEW: Is this worth doing here? Should we instead have a bound node validation
        // mechanism for this kind of thing?
        static void DoOne(BoundNode val, ArgScope scope)
        {
            if (val is null)
                return;
            var (owner, _) = ScopeCounter.FindDecl(val, default, scope);
            Validation.BugCheck(owner is null);
        }
        void DoOneScope(ArgScope scope)
        {
            if (scope == null)
                return;
            DoOne(source, scope);
            foreach (var key in keysPure)
                DoOne(key, scope);
            foreach (var (name, val) in keysKeep)
                DoOne(val, scope);
            foreach (var (name, val) in maps)
                DoOne(val, scope);
            foreach (var (name, val) in aggs)
                DoOne(val, scope);
        }
        DoOneScope(scopeKey);
        DoOneScope(indexKey);
        DoOneScope(scopeMap);
        DoOneScope(indexMap);
        DoOneScope(scopeAgg);

        return new BndGroupByNode(
            type, count, childKinds, totalCount, source,
            scopeKey, indexKey, keysPure, keysKeep, keysCi,
            scopeMap, indexMap, maps,
            scopeAgg, aggs);
    }

    /// <summary>
    /// This returns <c>false</c> iff there is (at least) one key containing text that is marked as case
    /// insensitive and another key containing text that is not marked as case insensitive. When this
    /// returns true (the case insensitive markings are consistent across all keys that contain text,
    /// if any), <paramref name="ci"/> is set to <c>true</c> iff there are such keys and they are (all)
    /// marked as case insensitive.
    /// 
    /// Note that "containing text" means either the key type is text or the key type is an aggregate
    /// (record or tuple) type with one or more component types containing text.
    /// 
    /// Note: the code generator uses this to determine whether it needs to generate a custom equality
    /// comparer or can use a standard case (in)sensitive comparer (for an aggregate containing all key
    /// values).
    /// </summary>
    public bool AreKeysSameCi(out bool ci)
    {
        ci = false;
        if (KeysCi.IsEmpty)
            return true;

        int ikey = 0;
        bool anyTx = false;
        bool anyCi = false;
        foreach (var key in PureKeys)
        {
            if (key.Type.HasText)
            {
                bool bit = KeysCi.TestBit(ikey);
                if (!anyTx)
                    anyCi = bit;
                else if (anyCi != bit)
                    return false;
                anyTx = true;
            }
            ikey++;
        }
        Validation.Assert(ikey == PureKeys.Length);
        foreach (var key in KeepKeys.GetValues())
        {
            if (key.Type.HasText)
            {
                bool bit = KeysCi.TestBit(ikey);
                if (!anyTx)
                    anyCi = bit;
                else if (anyCi != bit)
                    return false;
                anyTx = true;
            }
            ikey++;
        }
        Validation.Assert(ikey == PureKeys.Length + KeepKeys.Count);
        Validation.Assert(!KeysCi.TestAtOrAbove(ikey));

        ci = anyCi;
        return true;
    }

    public override (long min, long max) GetItemCountRange()
    {
        Validation.Assert(Type.IsSequence);
        var (min, max) = Source.GetItemCountRange();
        return (Math.Min(1, min), max);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Source.Accept(visitor, cur);
        foreach (var item in PureKeys)
            cur = item.Accept(visitor, cur);
        foreach (var item in KeepKeys.GetValues())
            cur = item.Accept(visitor, cur);
        foreach (var item in MapItems.GetValues())
        {
            if (item != null)
                cur = item.Accept(visitor, cur);
        }
        foreach (var item in AggItems.GetValues())
            cur = item.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndGroupByNode;
        Validation.Assert(other != null);

        if (PureKeys.Length != other.PureKeys.Length)
            return false;
        if (KeepKeys.Count != other.KeepKeys.Count)
            return false;
        if (KeysCi != other.KeysCi)
            return false;
        if (MapItems.Count != other.MapItems.Count)
            return false;
        if (AggItems.Count != other.AggItems.Count)
            return false;
        if (Type != other.Type)
            return false;

        if (!Source.Equivalent(other.Source, scopeMap, globalTable))
            return false;

        // REVIEW: Are there scope equivalence checks that we should do?
        Validation.Assert((ScopeForMaps == null) == (other.ScopeForMaps == null));
        Validation.Assert((ScopeForAggs == null) == (other.ScopeForAggs == null));

        int count = Util.Size(scopeMap);
        try
        {
            Util.Add(ref scopeMap, ScopeForKeys, other.ScopeForKeys);
            try
            {
                if (!EquivArgs(PureKeys, other.PureKeys, scopeMap, globalTable))
                    return false;
                if (!EquivItems(KeepKeys, other.KeepKeys, scopeMap, globalTable))
                    return false;
            }
            finally
            {
                scopeMap.Remove(ScopeForKeys);
            }

            if (MapItems.Count > 0)
            {
                Util.Add(ref scopeMap, ScopeForMaps, other.ScopeForMaps);
                try
                {
                    if (!EquivItems(MapItems, other.MapItems, scopeMap, globalTable))
                        return false;
                }
                finally
                {
                    scopeMap.Remove(ScopeForMaps);
                }
            }

            if (AggItems.Count > 0)
            {
                Util.Add(ref scopeMap, ScopeForAggs, other.ScopeForAggs);
                try
                {
                    if (!EquivItems(AggItems, other.AggItems, scopeMap, globalTable))
                        return false;
                }
                finally
                {
                    scopeMap.Remove(ScopeForAggs);
                }
            }
        }
        finally
        {
            Validation.Assert(count == Util.Size(scopeMap));
        }

        return true;
    }
}

/// <summary>
/// Handles adding, removing, renaming fields. The result of augmenting record projection or
/// calling a <see cref="SetFieldsFunc"/>.
/// </summary>
public sealed partial class BndSetFieldsNode : BndScopeOwnerNode
{
    public override bool OwnsScopes => Scope != null;

    /// <summary>
    /// The source record.
    /// </summary>
    public BoundNode Source { get; }

    /// <summary>
    /// The scope for evaluation of the field values. This is null iff <see cref="Additions"/> is empty.
    /// </summary>
    public ArgScope Scope { get; }

    /// <summary>
    /// The fields to add. May be empty.
    /// </summary>
    public NamedItems Additions { get; }

    /// <summary>
    /// Has no semantic meaning, but conveys field name ordering.
    /// Generally, the names will be field names, but are not guaranteed to
    /// be so, nor are they guaranteed to be in <see cref="Additions"/>.
    /// </summary>
    public NameTuple NameHints { get; }

    private BndSetFieldsNode(
            DType type, int childCount, BndNodeKindMask childKinds, int nodeCount,
            BoundNode source, ArgScope scope, NamedItems additions, NameTuple nameHints)
        : base(type, childCount, childKinds, nodeCount)
    {
        Source = source;
        Scope = scope;
        Additions = additions;
        NameHints = nameHints;
    }

    public static BoundNode Create(DType type, BoundNode source, ArgScope scope, NamedItems additions, NameTuple nameHints = default)
    {
        Validation.BugCheckParam(type.IsRecordReq, nameof(type));
        Validation.BugCheckValue(source, nameof(source));
        Validation.BugCheckParam(source.Type.IsRecordReq, nameof(source));
        Validation.BugCheckParam(additions != null && additions.NeedName && additions.NeedNode, nameof(additions));
        Validation.BugCheckParam(scope == null || scope.Kind == ScopeKind.With && scope.Type == source.Type, nameof(scope));

        var typeSrc = source.Type;
        if (additions.Count == 0)
        {
            // See if this is a no-op (just pass through source). This happens if there are no "additions"
            // and the destination type is the same as the source type.
            Validation.Coverage(scope == null ? 1 : 0);
            if (type == typeSrc)
                return source;
            scope = null;
        }
        else
        {
            Validation.BugCheckValue(scope, nameof(scope));
            foreach (var (name, val) in additions.GetPairs())
            {
                Validation.BugCheckParam(type.TryGetNameType(name, out var typeFld), nameof(type));
                Validation.BugCheckParam(typeFld == val.Type, nameof(type));
                VerifyScopeDecls(val, default, scope);
            }
        }

        foreach (var tn in type.GetNames())
        {
            if (!additions.TryGetValue(tn.Name, out _))
            {
                if (typeSrc.TryGetNameType(tn.Name, out var typeFld))
                    Validation.BugCheckParam(typeFld == tn.Type, nameof(type));
                else if (!tn.Type.IsOpt && !tn.Type.IsPrimitiveXxx)
                    throw Validation.BugExceptParam(nameof(type));
            }
        }

        int count = 1 + additions.Count;
        var childKinds = GetKinds(source) | GetKinds(additions);
        var nodeCount = GetCount(source) + GetCount(additions);
        return new BndSetFieldsNode(type, count, childKinds, nodeCount, source, scope, additions, nameHints);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Source.Accept(visitor, cur);
        foreach (var item in Additions.GetValues())
            cur = item.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndSetFieldsNode;
        Validation.Assert(other != null);
        Validation.Assert(other.Type == Type);

        if (Additions.Count != other.Additions.Count)
            return false;
        if (!Source.Equivalent(other.Source, scopeMap, globalTable))
            return false;
        if (!EquivNames(NameHints, other.NameHints))
            return false;
        if (Additions.Count == 0)
            return true;
        Validation.Assert(Scope != null);
        Validation.Assert(other.Scope != null);

        int count = Util.Size(scopeMap);
        try
        {
            Util.Add(ref scopeMap, Scope, other.Scope);
            try
            {
                if (!EquivItems(Additions, other.Additions, scopeMap, globalTable))
                    return false;
            }
            finally
            {
                scopeMap.Remove(Scope);
            }
        }
        finally
        {
            Validation.Assert(count == Util.Size(scopeMap));
        }

        return true;
    }
}

/// <summary>
/// Abstract base class for bound node visitors. The public methods should be called
/// only by the bound node classes. The behavior is implemented by overriding the "Impl"
/// methods.
/// </summary>
public abstract partial class BoundTreeVisitor
{
    /// <summary>
    /// The default behavior for the pre-visit methods.
    /// </summary>
    protected virtual bool PreVisitCore(BndParentNode bnd, int idx) { return true; }

    /// <summary>
    /// Called at the beginning of each node visitation. If it returns false,
    /// <see cref="Leave(BoundNode)"/> is still called but the visit methods are not.
    /// </summary>
    protected virtual bool Enter(BoundNode bnd, int idx) { return true; }

    /// <summary>
    /// Called at the end of each node visitation. Called whether <see cref="Enter(BoundNode)"/>
    /// returns true or false.
    /// </summary>
    protected virtual void Leave(BoundNode bnd, int idx) { }

    /// <summary>
    /// Given a base node index, <paramref name="cur"/>, args array, and index into the args array,
    /// return the node index for that arg.
    /// </summary>
    protected int GetArgNodeIndex(int cur, ArgTuple args, int iarg)
    {
        Validation.AssertIndex(iarg, args.Length);

        for (int i = 0; i < iarg; i++)
            cur += args[i].NodeCount;
        return cur;
    }
}

public abstract partial class NoopBoundTreeVisitor : BoundTreeVisitor
{
    protected virtual void VisitCore(BndLeafNode bnd, int idx)
    {
    }

    protected virtual void PostVisitCore(BndParentNode bnd, int idx)
    {
    }
}

/// <summary>
/// Abstract base class for bound node visitors that want push / pop methods for scopes and
/// nested args.
/// </summary>
public abstract class ScopedBoundTreeVisitor : BoundTreeVisitor
{
    protected sealed class PushedScope
    {
        public readonly int Depth;
        public readonly ArgScope Scope;
        public readonly BoundNode Owner;
        public readonly int OwnerIdx;
        public readonly int Slot;
        public bool Active;

        public PushedScope(int depth, ArgScope scope, BoundNode owner, int idx, int slot)
        {
            Depth = depth;
            Scope = scope;
            Slot = slot;
            Owner = owner;
            OwnerIdx = idx;
            Active = true;
        }
    }

    // The pushed scopes.
    private readonly List<PushedScope> _scopes;

    // Track the nested arg depth - for asserts.
    private int _nestedDepth;

    // Groups of loop scopes that are currently pushed. A group is defined as scopes which are defined
    // in sequence within the same call, for example within a Zip call.
    private readonly List<(int min, int count, BoundNode parent)> _loopScopeGroups;

    protected int ScopeDepth { get { return _scopes.Count; } }

    protected int NestedArgDepth { get { return _nestedDepth; } }

    protected int LoopGroupDepth { get { return _loopScopeGroups.Count; } }

    protected ScopedBoundTreeVisitor()
    {
        _scopes = new List<PushedScope>();
        _loopScopeGroups = new List<(int min, int count, BoundNode)>();
        _nestedDepth = 0;
    }

    protected void Go(BoundNode bnd)
    {
        Validation.Assert(ScopeDepth == 0);
        Validation.Assert(_nestedDepth == 0);

        int cur = bnd.Accept(this, 0);
        Validation.Assert(cur == bnd.NodeCount);

        Validation.Assert(ScopeDepth == 0);
        Validation.Assert(_nestedDepth == 0);
    }

    /// <summary>
    /// This is invoked immediately after the "value" for the scope has been visited.
    /// </summary>
    protected virtual void InitScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
    {
        Validation.AssertValue(scope);
        Validation.AssertValue(owner);
    }

    /// <summary>
    /// This is invoked when the scope becomes active. Note that there may be other values
    /// that have been visited between a scope being inited and pushed.
    /// REVIEW: Currently the only way that <paramref name="isArgValid"/> can be false is for
    /// an invalid bound node other than <see cref="BndCallNode"/> to be constructed. The latter
    /// throws on an attempt to create the call node. Should we drop the <paramref name="isArgValid"/>
    /// parameter?
    /// </summary>
    protected virtual void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
    {
        Validation.AssertValue(scope);
        Validation.AssertValue(owner);
        _scopes.Add(new PushedScope(_scopes.Count, scope, owner, idx, slot));

        if (scope.Kind.IsLoopScope())
        {
            int index = _loopScopeGroups.Count - 1;
            var item = index >= 0 ? _loopScopeGroups[index] : default;
            if (item.parent == owner)
                _loopScopeGroups[index] = (item.min, item.count + 1, owner);
            else
                _loopScopeGroups.Add((_scopes.Count - 1, 1, owner));
        }
    }

    /// <summary>
    /// This is invoked when the scope is being retired.
    /// </summary>
    protected virtual void PopScope(ArgScope scope)
    {
        Validation.Assert(ScopeDepth > 0);
        Validation.Assert(_scopes.Peek().Scope == scope);
        var ps = _scopes.Pop();
        ps.Active = false;

        if (scope.Kind.IsLoopScope())
        {
            Validation.Assert(_loopScopeGroups.Count > 0);
            int index = _loopScopeGroups.Count - 1;
            var (min, count, parent) = _loopScopeGroups[index];
            if (count > 1)
                _loopScopeGroups[index] = (min, count - 1, parent);
            else
                _loopScopeGroups.RemoveAt(index);
        }
    }

    /// <summary>
    /// This is invoked sometime after PopScope. If the scope owner is a <see cref="BndCallNode"/>,
    /// it is called immediately after the PostVisit call. Currently scopes owned by a
    /// <see cref="BndGroupByNode"/> are disposed right after they are popped, so the lifetimes
    /// of the scopes don't overlap.
    /// REVIEW: Is there any reason to handle the <see cref="BndGroupByNode"/> scopes
    /// differently?
    /// </summary>
    protected virtual void DisposeScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot)
    {
        Validation.AssertValue(scope);
        Validation.AssertValue(owner);
    }

    protected virtual void PushNestedArg(BndScopeOwnerNode owner, int idx, int slot, bool needsDelegate)
    {
        Validation.AssertValue(owner);
        _nestedDepth++;
        Validation.Assert(_nestedDepth > 0);
    }

    protected virtual void PopNestedArg()
    {
        Validation.Assert(_nestedDepth > 0);
        _nestedDepth--;
    }

    protected override void VisitImpl(BndScopeRefNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        int depth = _scopes.Count;
        while (--depth >= 0)
        {
            var info = _scopes[depth];
            Validation.Assert(info.Depth == depth);
            if (info.Active && info.Scope == bnd.Scope)
            {
                VisitCore(bnd, idx, info);
                return;
            }
        }
        VisitCore(bnd, idx, null);
    }

    protected abstract void VisitCore(BndScopeRefNode bnd, int idx, PushedScope scope);

    protected sealed override bool PreVisitImpl(BndCallNode call, int idx)
    {
        if (!PreVisitCall(call, idx))
            return false;

        int cur = idx + 1;
        if (call.Scopes.Length == 0)
        {
            for (int slot = 0; slot < call.Args.Length; slot++)
                cur = VisitNonNestedCallArg(call, slot, call.Args[slot], cur);
            PostVisit(call, idx);
            return false;
        }

        int scopeDepthBase = ScopeDepth;
        int iscopeInit = 0;
        int iidxInit = 0;
        int iscopePush = 0;
        int iidxPush = 0;
        int slotLimScope = 0;
        bool needsDelegate = false;
        var traits = call.Traits;
        for (int slot = 0; slot < call.Args.Length; slot++)
        {
            if (!traits.IsNested(slot))
            {
                Validation.Assert(ScopeDepth == scopeDepthBase);
                cur = VisitNonNestedCallArg(call, slot, call.Args[slot], cur);
            }
            else
            {
                // Push scopes that have been "inited" but not yet pushed.
                while (slotLimScope < slot)
                {
                    if (traits.IsScope(slotLimScope, out int iscope, out int iidx, out bool firstForIdx))
                    {
                        Validation.Assert(iscopePush < iscopeInit);
                        Validation.Assert(iscope == iscopePush);
                        if (iidx >= iidxPush)
                        {
                            Validation.Assert(firstForIdx);
                            Validation.Assert(iidx == iidxPush);
                            var index = call.Indices[iidxPush++];
                            if (index != null)
                            {
                                Validation.Assert(index.IsIndex);
                                PushScope(index, call, idx, slotLimScope, isArgValid: true);
                            }
                        }
                        var scope = call.Scopes[iscopePush++];

                        // Index scopes shouldn't be associated with args.
                        Validation.Assert(!scope.IsIndex);
                        PushScope(scope, call, idx, slotLimScope, scope.IsValidArg(call.Args[slotLimScope]));
                        if (scope.Kind.IsLoopScope())
                            needsDelegate = true;
                    }
                    slotLimScope++;
                }
                Validation.Assert(iscopePush == iscopeInit);

                // Set the scope Active flags.
                bool activePrev = true;
                for (int depth = ScopeDepth, upCount = 0; --depth >= scopeDepthBase;)
                {
                    var info = _scopes[depth];
                    Validation.AssertIndex(info.Slot, slot);
                    Validation.Assert(traits.IsScope(info.Slot));
                    info.Active = activePrev = info.Scope.IsIndex ? activePrev : traits.IsScopeActive(slot, upCount++);
                }
                PushNestedArg(call, idx, slot, needsDelegate);
                cur = VisitNestedCallArg(call, slot, call.Args[slot], cur);
                PopNestedArg();

                if (traits.IsNestedTail(slot))
                {
                    Validation.Assert(!traits.IsScope(slot));

                    // Clear scopes.
                    Validation.Assert(ScopeDepth > scopeDepthBase);
                    while (ScopeDepth > scopeDepthBase)
                        PopScope(_scopes.Peek().Scope);
                }
            }

            if (traits.IsScope(slot, out int iscopeCur, out int iidxCur, out bool firstForIdxCur))
            {
                // Init the associated scope.
                Validation.Assert(iscopeCur == iscopeInit);
                Validation.Assert(iscopeInit < call.Scopes.Length);
                if (iidxCur >= 0)
                {
                    Validation.AssertIndex(iidxCur, call.Indices.Length);
                    if (iidxCur >= iidxInit)
                    {
                        Validation.Assert(firstForIdxCur);
                        Validation.Assert(iidxCur == iidxInit);
                        var index = call.Indices[iidxInit++];
                        if (index != null)
                            InitScope(index, call, idx, slot, isArgValid: true);
                    }
                }
                var scope = call.Scopes[iscopeInit++];
                InitScope(scope, call, idx, slot, scope.IsValidArg(call.Args[slot]));
            }
        }
        Validation.Assert(iscopeInit == call.Scopes.Length);
        Validation.Assert(iscopePush == call.Scopes.Length);
        Validation.Assert(ScopeDepth == scopeDepthBase);

        PostVisit(call, idx);

        if (iscopeInit > 0)
        {
            for (int slot = call.Args.Length; --slot >= 0;)
            {
                if (!traits.IsScope(slot, out int iscope, out int iidx, out bool firstForIdx))
                    continue;
                Validation.Assert(iscopeInit > 0);
                var scope = call.Scopes[--iscopeInit];
                Validation.Assert(iscope == iscopeInit);
                DisposeScope(scope, call, idx, slot);
                if (firstForIdx)
                {
                    var scopeIdx = call.Indices[--iidxInit];
                    Validation.Assert(iidx == iidxInit);
                    if (scopeIdx != null)
                        DisposeScope(scopeIdx, call, idx, slot);
                }
            }
            Validation.Assert(iscopeInit == 0);
        }

        return false;
    }

    protected virtual bool PreVisitCall(BndCallNode call, int idx) { return PreVisitCore(call, idx); }

    protected virtual int VisitNonNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
    {
        Validation.AssertValue(call);
        Validation.Assert(0 <= slot & slot < call.Args.Length);
        Validation.Assert(!call.Traits.IsNested(slot));
        Validation.Assert(call.Args[slot] == arg);
        return arg.Accept(this, cur);
    }

    protected virtual int VisitNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
    {
        Validation.AssertValue(call);
        Validation.Assert(0 <= slot & slot < call.Args.Length);
        Validation.Assert(call.Traits.IsNested(slot));
        Validation.Assert(call.Args[slot] == arg);
        return arg.Accept(this, cur);
    }

    protected sealed override bool PreVisitImpl(BndGroupByNode bgb, int idx)
    {
        Validation.AssertValue(bgb);

        if (!PreVisitGroupBy(bgb, idx))
            return false;

        int cur = idx + 1;
        int depthBase = ScopeDepth;
        cur = bgb.Source.Accept(this, cur);
        Validation.Assert(ScopeDepth == depthBase);

        if (bgb.IndexForKeys != null)
        {
            InitScope(bgb.IndexForKeys, bgb, idx, -1, isArgValid: true);
            PushScope(bgb.IndexForKeys, bgb, idx, -1, isArgValid: true);
        }
        bool isValid = bgb.ScopeForKeys.IsValidArg(bgb.Source);
        InitScope(bgb.ScopeForKeys, bgb, idx, -1, isValid);
        PushScope(bgb.ScopeForKeys, bgb, idx, -1, isValid);

        int slot = 0;
        for (; slot < bgb.PureKeys.Length; slot++)
        {
            PushNestedArg(bgb, idx, slot, needsDelegate: true);
            cur = VisitGroupByKey(bgb, slot, bgb.PureKeys[slot], cur);
            PopNestedArg();
        }
        Validation.Assert(slot == bgb.PureKeys.Length);
        if (bgb.KeepKeys.Count > 0)
        {
            foreach (var val in bgb.KeepKeys.GetValues())
            {
                PushNestedArg(bgb, idx, slot, needsDelegate: true);
                cur = VisitGroupByKey(bgb, slot, val, cur);
                PopNestedArg();
                slot++;
            }
        }
        Validation.Assert(slot == bgb.PureKeys.Length + bgb.KeepKeys.Count);

        // REVIEW: Should we wait to dispose the scopes until after PostVisit?
        PopScope(bgb.ScopeForKeys);
        DisposeScope(bgb.ScopeForKeys, bgb, idx, -1);
        if (bgb.IndexForKeys != null)
        {
            PopScope(bgb.IndexForKeys);
            DisposeScope(bgb.IndexForKeys, bgb, idx, -1);
        }
        Validation.Assert(ScopeDepth == depthBase);

        if (bgb.MapItems.Count > 0)
        {
            Validation.Assert(bgb.ScopeForMaps != null);
            if (bgb.IndexForMaps != null)
            {
                InitScope(bgb.IndexForMaps, bgb, idx, -2, isArgValid: true);
                PushScope(bgb.IndexForMaps, bgb, idx, -2, isArgValid: true);
            }
            isValid = bgb.ScopeForMaps.IsValidArg(bgb.Source);
            InitScope(bgb.ScopeForMaps, bgb, idx, -2, isValid);
            PushScope(bgb.ScopeForMaps, bgb, idx, -2, isValid);

            int slotBase = slot;
            foreach (var val in bgb.MapItems.GetValues())
            {
                PushNestedArg(bgb, idx, slot, needsDelegate: true);
                cur = VisitGroupByMap(bgb, slot - slotBase, val, cur);
                PopNestedArg();
                slot++;
            }
            Validation.Assert(slot == slotBase + bgb.MapItems.Count);

            // REVIEW: Should we wait to dispose the scopes until after PostVisit?
            PopScope(bgb.ScopeForMaps);
            DisposeScope(bgb.ScopeForMaps, bgb, idx, -2);
            if (bgb.IndexForMaps != null)
            {
                PopScope(bgb.IndexForMaps);
                DisposeScope(bgb.IndexForMaps, bgb, idx, -2);
            }
        }
        Validation.Assert(ScopeDepth == depthBase);

        if (bgb.AggItems.Count > 0)
        {
            Validation.Assert(bgb.ScopeForAggs != null);
            isValid = bgb.ScopeForAggs.IsValidArg(bgb.Source);
            InitScope(bgb.ScopeForAggs, bgb, idx, -3, isValid);
            PushScope(bgb.ScopeForAggs, bgb, idx, -3, isValid);

            int slotBase = slot;
            foreach (var val in bgb.AggItems.GetValues())
            {
                // Since we are in an agg scope, we need a delegate.
                PushNestedArg(bgb, idx, slot, needsDelegate: true);
                cur = VisitGroupByAgg(bgb, slot - slotBase, val, cur);
                PopNestedArg();
                slot++;
            }
            Validation.Assert(slot == slotBase + bgb.AggItems.Count);

            PopScope(bgb.ScopeForAggs);

            // REVIEW: Should we wait to dispose the scopes until after PostVisit?
            DisposeScope(bgb.ScopeForAggs, bgb, idx, -3);
        }
        Validation.Assert(ScopeDepth == depthBase);
        Validation.Assert(cur == idx + bgb.NodeCount);

        PostVisit(bgb, idx);
        return false;
    }

    protected virtual bool PreVisitGroupBy(BndGroupByNode bgb, int idx) { return PreVisitCore(bgb, idx); }

    protected virtual int VisitGroupByKey(BndGroupByNode bgb, int slot, BoundNode arg, int cur)
    {
        Validation.AssertValue(bgb);
        Validation.AssertIndex(slot, bgb.PureKeys.Length + bgb.KeepKeys.Count);
        Validation.AssertValue(arg);
        return arg.Accept(this, cur);
    }

    protected virtual int VisitGroupByMap(BndGroupByNode bgb, int slot, BoundNode arg, int cur)
    {
        Validation.AssertValue(bgb);
        Validation.AssertIndex(slot, bgb.MapItems.Count);
        if (arg != null)
            cur = arg.Accept(this, cur);
        return cur;
    }

    protected virtual int VisitGroupByAgg(BndGroupByNode bgb, int slot, BoundNode arg, int cur)
    {
        Validation.AssertValue(bgb);
        Validation.AssertIndex(slot, bgb.AggItems.Count);
        Validation.AssertValue(arg);
        return arg.Accept(this, cur);
    }

    protected sealed override bool PreVisitImpl(BndSetFieldsNode bsf, int idx)
    {
        Validation.AssertValue(bsf);

        if (!PreVisitSetFields(bsf, idx))
            return false;

        int cur = idx + 1;
        int depthBase = ScopeDepth;
        cur = bsf.Source.Accept(this, cur);
        Validation.Assert(ScopeDepth == depthBase);

        if (bsf.Scope != null)
        {
            bool isValid = bsf.Scope.IsValidArg(bsf.Source);
            InitScope(bsf.Scope, bsf, idx, -1, isValid);
            PushScope(bsf.Scope, bsf, idx, -1, isValid);

            int slot = 0;
            foreach (var val in bsf.Additions.GetValues())
            {
                PushNestedArg(bsf, idx, slot, needsDelegate: false);
                cur = VisitSetFieldsAddition(bsf, slot, val, cur);
                PopNestedArg();
                slot++;
            }
            Validation.Assert(slot == bsf.Additions.Count);

            PopScope(bsf.Scope);
            Validation.Assert(ScopeDepth == depthBase);

            // REVIEW: Should we wait to dispose the scopes until after PostVisit?
            DisposeScope(bsf.Scope, bsf, idx, -1);
        }
        Validation.Assert(ScopeDepth == depthBase);
        Validation.Assert(cur == idx + bsf.NodeCount);

        PostVisit(bsf, idx);
        return false;
    }

    protected virtual bool PreVisitSetFields(BndSetFieldsNode bsf, int idx) { return PreVisitCore(bsf, idx); }

    protected virtual int VisitSetFieldsAddition(BndSetFieldsNode bsf, int slot, BoundNode arg, int cur)
    {
        Validation.AssertValue(bsf);
        Validation.AssertIndex(slot, bsf.Additions.Count);
        Validation.AssertValue(arg);
        return arg.Accept(this, cur);
    }

    protected sealed override bool PreVisitImpl(BndModuleNode bmod, int idx)
    {
        Validation.AssertValue(bmod);

        if (!PreVisitModule(bmod, idx))
            return false;

        int cur = idx + 1;
        int depthBase = ScopeDepth;

        for (int slot = 0; slot < bmod.Externals.Length; slot++)
        {
            int prev = cur;
            cur = VisitModuleExternal(bmod, slot, bmod.Externals[slot], cur);
            Validation.Assert(cur == prev + bmod.Externals[slot].NodeCount);
        }

        if (bmod.ScopeExt is not null)
        {
            InitScope(bmod.ScopeExt, bmod, idx, -2, isArgValid: true);
            PushScope(bmod.ScopeExt, bmod, idx, -2, isArgValid: true);
        }
        InitScope(bmod.ScopeItems, bmod, idx, -1, isArgValid: true);
        PushScope(bmod.ScopeItems, bmod, idx, -1, isArgValid: true);

        for (int slot = 0; slot < bmod.Items.Length; slot++)
        {
            PushNestedArg(bmod, idx, slot, needsDelegate: false);
            int prev = cur;
            cur = VisitModuleItem(bmod, slot, bmod.Items[slot], cur);
            Validation.Assert(cur == prev + bmod.Items[slot].NodeCount);
            PopNestedArg();
        }

        PopScope(bmod.ScopeItems);
        DisposeScope(bmod.ScopeItems, bmod, idx, -1);

        if (bmod.ScopeExt is not null)
        {
            PopScope(bmod.ScopeExt);
            DisposeScope(bmod.ScopeExt, bmod, idx, -2);
        }

        Validation.Assert(ScopeDepth == depthBase);
        Validation.Assert(cur == idx + bmod.NodeCount);

        PostVisit(bmod, idx);
        return false;
    }

    protected virtual bool PreVisitModule(BndModuleNode bmod, int idx) { return PreVisitCore(bmod, idx); }

    protected virtual int VisitModuleExternal(BndModuleNode bmod, int slot, BoundNode bnd, int cur)
    {
        Validation.AssertValue(bmod);
        Validation.AssertValue(bnd);
        Validation.AssertIndex(slot, bmod.Externals.Length);
        Validation.Assert(bmod.Externals[slot] == bnd);

        return bnd.Accept(this, cur);
    }

    protected virtual int VisitModuleItem(BndModuleNode bmod, int slot, BoundNode bnd, int cur)
    {
        Validation.AssertValue(bmod);
        Validation.AssertValue(bnd);
        Validation.AssertIndex(slot, bmod.Items.Length);
        Validation.Assert(bmod.Items[slot] == bnd);

        return bnd.Accept(this, cur);
    }

    protected sealed override bool PreVisitImpl(BndModuleProjectionNode bmp, int idx)
    {
        Validation.AssertValue(bmp);

        if (!PreVisitModuleProjection(bmp, idx))
            return false;

        int cur = idx + 1;
        int depthBase = ScopeDepth;
        cur = bmp.Module.Accept(this, cur);
        Validation.Assert(ScopeDepth == depthBase);

        bool isValid = bmp.Scope.IsValidArg(bmp.Module);
        InitScope(bmp.Scope, bmp, idx, -1, isValid);
        PushScope(bmp.Scope, bmp, idx, -1, isValid);

        PushNestedArg(bmp, idx, 0, needsDelegate: false);
        cur = VisitModuleProjectionRecord(bmp, bmp.Record, cur);
        PopNestedArg();
        Validation.Assert(cur == idx + bmp.NodeCount);

        PopScope(bmp.Scope);
        Validation.Assert(ScopeDepth == depthBase);

        // REVIEW: Should we wait to dispose the scopes until after PostVisit?
        DisposeScope(bmp.Scope, bmp, idx, -1);

        PostVisit(bmp, idx);
        return false;
    }

    protected virtual bool PreVisitModuleProjection(BndModuleProjectionNode bmp, int idx) { return PreVisitCore(bmp, idx); }

    protected virtual int VisitModuleProjectionRecord(BndModuleProjectionNode bmp, BoundNode rec, int cur)
    {
        Validation.AssertValue(bmp);
        Validation.AssertValue(rec);
        return rec.Accept(this, cur);
    }

    protected PushedScope GetScope(int index)
    {
        Validation.BugCheckIndex(index, _scopes.Count, nameof(index));
        return _scopes[index];
    }

    protected (int min, int lim) GetLoopGroup(int index)
    {
        Validation.BugCheckIndex(index, _loopScopeGroups.Count, nameof(index));
        var (min, count, _) = _loopScopeGroups[index];
        return (min, min + count);
    }

    protected (int min, int lim) PeekLoopGroup()
    {
        if (_loopScopeGroups.Count > 0)
        {
            var (min, count, _) = _loopScopeGroups.Peek();
            return (min, min + count);
        }
        return (-1, -1);
    }
}

public abstract partial class NoopScopedBoundTreeVisitor : ScopedBoundTreeVisitor
{
    protected NoopScopedBoundTreeVisitor()
    {
    }

    protected virtual void VisitCore(BndLeafNode node, int idx)
    {
        Validation.Assert(node.ChildCount == 0);
    }

    protected virtual void PostVisitCore(BndParentNode node, int idx)
    {
    }
}

/// <summary>
/// Base class for a persistent map from <see cref="BoundNode"/> (or sub-class) to something.
/// This disallows null node. Note that a sub-class can override methods appropriately to
/// handle null if it needs to.
/// </summary>
public abstract class BoundNodeMapping<TTree, TKey, TVal> : RedBlackTree<TTree, TKey, TVal>
    where TTree : BoundNodeMapping<TTree, TKey, TVal>
    where TKey : BoundNode
{
    protected BoundNodeMapping(Node root)
        : base(root)
    {
    }

    protected override bool KeyIsValid(TKey key)
    {
        return key != null;
    }

    protected override int KeyCompare(TKey key0, TKey key1)
    {
        Validation.AssertValue(key0);
        Validation.AssertValue(key1);
        Validation.Assert(key0.Ordinal != key1.Ordinal || key0 == key1);
        return key0 == key1 ? 0 : key0.Ordinal < key1.Ordinal ? -1 : +1;
    }

    protected override bool KeyEquals(TKey key0, TKey key1)
    {
        Validation.AssertValue(key0);
        Validation.AssertValue(key1);
        Validation.Assert(key0.Ordinal != key1.Ordinal || key0 == key1);
        return key1 == key0;
    }

    protected override int KeyHash(TKey key)
    {
        Validation.AssertValue(key);
        return key.GetHashCode();
    }
}
