// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

partial class BoundFormula
{
    /// <summary>
    /// This is used to encode the active scope chain information when binding each parse node.
    /// This information is needed for intellisense.
    /// </summary>
    private sealed class ScopeInfo
    {
        /// <summary>
        /// The type of the scope item.
        /// </summary>
        public DType Type { get; }

        /// <summary>
        /// An optional alias for the scope item.
        /// </summary>
        public DName Alias { get; }

        /// <summary>
        /// Whether the alias is implicit.
        /// </summary>
        public bool AliasIsImplicit { get; }

        /// <summary>
        /// The outer scope item information, if there is one.
        /// </summary>
        public ScopeInfo Outer { get; }

        /// <summary>
        /// The depth of the scope chain.
        /// </summary>
        public int Depth { get; }

        public ScopeInfo(DType type, ScopeInfo outer, DName alias = default(DName), bool aliasIsImplicit = false)
        {
            Validation.Assert(type.IsValid);
            // The alias is optional.
            // Validation.Assert(alias.IsValid);
            Validation.AssertValueOrNull(outer);

            Type = type;
            Alias = alias;
            AliasIsImplicit = aliasIsImplicit;
            Outer = outer;
            Depth = (outer?.Depth ?? 0) + 1;
        }
    }

    /// <summary>
    /// Information about a parse node, including its type and scope-chain information.
    /// This is useful for intellisense features like suggestion generation as well
    /// as renaming.
    /// </summary>
    private sealed class NodeInfo
    {
        /// <summary>
        /// The type of the node.
        /// </summary>
        public DType Type { get; }

        /// <summary>
        /// The scope-chain information of the node.
        /// </summary>
        public ScopeInfo ScopeInfo { get; }

        /// <summary>
        /// Whether the node is used as an implicit name.
        /// This may happen when an identifier is an
        /// argument to a scoped function slot, or is used
        /// within record literal syntax without a variable
        /// declaration.
        ///
        /// Note that a node may form an implicit name without being used,
        /// in which case this flag is set to false.
        /// In the formula Guard(N, N*N), the first N is used
        /// as an implicit name in the following expression,
        /// but in Guard(N, it*it) N is not despite forming one.
        /// </summary>
        public bool IsUsedImplicitName { get; }

        /// <summary>
        /// The BoundNode associated with this parse node. This may be null.
        /// </summary>
        public BoundNode BoundNode { get; }

        public NodeInfo(DType type, ScopeInfo scopeInfo, bool isUsedImplicitName, BoundNode bnd)
        {
            Validation.AssertValueOrNull(scopeInfo);
            Validation.Assert(bnd == null || type == bnd.Type);

            Type = type;
            ScopeInfo = scopeInfo;
            IsUsedImplicitName = isUsedImplicitName;
            BoundNode = bnd;
        }

        internal NodeInfo WithUsedImplicitName()
        {
            return new NodeInfo(Type, ScopeInfo, true, BoundNode);
        }
    }

    /// <summary>
    /// Persistent map from <see cref="RexlNode"/> to <see cref="NodeInfo"/>.
    /// </summary>
    private sealed class NodeToInfo : RexlNodeMapping<NodeToInfo, RexlNode, NodeInfo>
    {
        public static readonly NodeToInfo Empty = new NodeToInfo(null);

        private NodeToInfo(Node root)
            : base(root)
        {
        }

        protected override bool ValIsValid(NodeInfo val)
        {
            return val != null;
        }

        protected override bool ValEquals(NodeInfo val0, NodeInfo val1)
        {
            Validation.AssertValue(val0);
            Validation.AssertValue(val1);
            return val0 == val1;
        }

        protected override int ValHash(NodeInfo val)
        {
            Validation.AssertValue(val);
            return val.GetHashCode();
        }

        protected override NodeToInfo Wrap(Node root)
        {
            return root == _root ? this : root == null ? Empty : new NodeToInfo(root);
        }
    }

    /// <summary>
    /// Persistent map from <see cref="CallNode"/> to <see cref="OperInfo"/> and <see cref="ArgTraits"/>.
    /// Note that the <see cref="OperInfo"/> is null when an <see cref="UnknownFunc"/> is bound.
    /// </summary>
    private sealed class CallToOper : RexlNodeMapping<CallToOper, CallNode, (OperInfo info, ArgTraits traits)>
    {
        public static readonly CallToOper Empty = new CallToOper(null);

        private CallToOper(Node root)
            : base(root)
        {
        }

        protected override bool ValIsValid((OperInfo info, ArgTraits traits) value)
        {
            return value.traits != null;
        }

        protected override bool ValEquals((OperInfo info, ArgTraits traits) val0, (OperInfo info, ArgTraits traits) val1)
        {
            Validation.AssertValue(val0.traits);
            Validation.AssertValue(val1.traits);
            return val0.info == val1.info && val0.traits == val1.traits;
        }

        protected override int ValHash((OperInfo info, ArgTraits traits) val)
        {
            Validation.AssertValue(val.traits);
            return val.GetHashCode();
        }

        protected override CallToOper Wrap(Node root)
        {
            return root == _root ? this : root == null ? Empty : new CallToOper(root);
        }
    }
}

/// <summary>
/// Persistent dictionary from (<see cref="NPath"/>, ulong) to <see cref="OperInfo"/>. This is used
/// to record operation lookups during binding.
/// <list type="bullet">
/// <item>The low 32 bits of the `ulong` value contain the arity.</item>
/// <item>Bit 32 is zero for function and 1 for proc.</item>
/// <item>Bit 33 is zero for normal and 1 for fuzzy.</item>
/// <item>Bit 34 is zero for core/model and 1 for user defined.</item>
/// </list>
/// The value is the result of the lookup, with null meaning that the lookup was unsuccessful.
/// </summary>
public sealed class NameArityToOperOpt : NPathExtraRedBlackTree<NameArityToOperOpt, ulong, OperInfo>
{
    public static readonly NameArityToOperOpt Empty = new NameArityToOperOpt(null);

    [Flags]
    public enum ExtraFlags
    {
        None = 0x00,
        Fuzzy = 0x01,
        Proc = 0x02,
        User = 0x04,

        FuzzyUser = Fuzzy | User,
        FuzzyProc = Fuzzy | Proc,
        FuzzyUserProc = Fuzzy | User | Proc,
    }

    private NameArityToOperOpt(Node root)
        : base(root)
    {
    }

    protected override bool KeyIsValid((NPath path, ulong extra) key)
    {
        return !key.path.IsRoot;
    }

    protected override NameArityToOperOpt Wrap(Node root)
    {
        return root == _root ? this : root != null ? new NameArityToOperOpt(root) : Empty;
    }

    protected override bool ValIsValid(OperInfo val)
    {
        return true;
    }

    protected override bool ValEquals(OperInfo val0, OperInfo val1)
    {
        return val0 == val1;
    }

    protected override int ValHash(OperInfo val)
    {
        return val != null ? val.GetHashCode() : 0;
    }
}
