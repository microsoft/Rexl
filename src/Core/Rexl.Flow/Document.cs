// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Conditional = System.Diagnostics.ConditionalAttribute;
using FmaTuple = Immutable.Array<(RexlFormula fma, int grp)>;

/// <summary>
/// Abstract base class for the generic <see cref="Document{TNode}"/> class. This contains things that
/// don't depend on TNode.
/// </summary>
public abstract partial class DocumentBase
{
    /// <summary>
    /// Persistent dictionary from <see cref="Guid"/> to <see cref="Namespace"/>. This disallows
    /// the default (all zero) <see cref="Guid"/>.
    /// </summary>
    protected sealed class GuidToNamespace : GuidRedBlackTree<GuidToNamespace, Namespace>
    {
        public static readonly GuidToNamespace Empty = new GuidToNamespace(null);

        private GuidToNamespace(Node root)
            : base(root)
        {
        }

        protected override GuidToNamespace Wrap(Node root)
        {
            return root == _root ? this : root != null ? new GuidToNamespace(root) : Empty;
        }

        protected override bool KeyIsValid(Guid key)
        {
            return key != default;
        }

        protected override bool ValIsValid(Namespace val)
        {
            return val != null;
        }

        protected override bool ValEquals(Namespace val0, Namespace val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(Namespace val)
        {
            return val.GetHashCode();
        }
    }

    /// <summary>
    /// Persistent dictionary from <see cref="NPath"/> to Guid.
    /// </summary>
    protected sealed class NameToGuid : NPathRedBlackTree<NameToGuid, Guid>
    {
        public static readonly NameToGuid Empty = new NameToGuid(null);

        private NameToGuid(Node root)
            : base(root)
        {
        }

        protected override NameToGuid Wrap(Node root)
        {
            return root == _root ? this : root != null ? new NameToGuid(root) : Empty;
        }

        protected override bool KeyIsValid(NPath key)
        {
            return !key.IsRoot;
        }

        protected override bool ValIsValid(Guid val)
        {
            return val != default;
        }

        protected override bool ValEquals(Guid val0, Guid val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(Guid val)
        {
            return val.GetHashCode();
        }
    }

    /// <summary>
    /// Persistent dictionary from <see cref="NPath"/> to <see cref="Namespace"/>.
    /// This allows null value.
    /// </summary>
    protected sealed class NameToNamespace : NPathRedBlackTree<NameToNamespace, Namespace>
    {
        public static readonly NameToNamespace Empty = new NameToNamespace(null);

        private NameToNamespace(Node root)
            : base(root)
        {
        }

        protected override NameToNamespace Wrap(Node root)
        {
            return root == _root ? this : root != null ? new NameToNamespace(root) : Empty;
        }

        protected override bool KeyIsValid(NPath key)
        {
            return true;
        }

        protected override bool ValIsValid(Namespace val)
        {
            return true;
        }

        protected override bool ValEquals(Namespace val0, Namespace val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(Namespace val)
        {
            return val != null ? val.GetHashCode() : 0;
        }
    }
}

/// <summary>
/// A Document represents an editable "blue print" for a data flow graph. It does not include the
/// actual data or state of the data flow. That information is externally owned and generally evolves
/// in response to notification events from the Document.
///
/// Every editing operation is recorded in the (owned) undo manager, and the edits within an undo group
/// are analyzed for implications with the conclusions communicated via notification events.
///
/// There is often a single physical "data flow" associated with an active document, but this is not
/// assumed by the architecture. There may be none or many. There is an assumption that within all of
/// the data flows, the data types are consistent, but the actual data values do not need to be consistent.
/// For example, analysis requires that each active non-formula node have a well-defined current
/// <see cref="DType"/> (which must therefore be shared across all associated data flows), but there is
/// no inspection or use of actual data (unless it is part of a rexl formula, which is considered literal,
/// not raw data).
/// </summary>
public abstract partial class Document<TDoc, TNode> : DocumentBase
    where TDoc : Document<TDoc, TNode>
    where TNode : DocumentBase.DocNode
{
    /// <summary>
    /// Persistent dictionary from <see cref="Guid"/> to <see cref="TNode"/>. This disallows
    /// the default (all zero) <see cref="Guid"/>.
    /// </summary>
    protected sealed class GuidToNode : GuidRedBlackTree<GuidToNode, TNode>
    {
        public static readonly GuidToNode Empty = new GuidToNode(null);

        private GuidToNode(Node root)
            : base(root)
        {
        }

        protected override GuidToNode Wrap(Node root)
        {
            return root == _root ? this : root != null ? new GuidToNode(root) : Empty;
        }

        protected override bool KeyIsValid(Guid key)
        {
            return key != default;
        }

        protected override bool ValIsValid(TNode val)
        {
            return val != null;
        }

        protected override bool ValEquals(TNode val0, TNode val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(TNode val)
        {
            return val.GetHashCode();
        }
    }

    #region The fields

    protected struct Fields
    {
        /// <summary>
        /// Guid to node mapping.
        /// </summary>
        public GuidToNode _guidToNode;

        /// <summary>
        /// Guid to namespace mapping, for explicit namespaces.
        /// </summary>
        public GuidToNamespace _guidToNs;

        /// <summary>
        /// Name to guid of node mapping.
        /// </summary>
        public NameToGuid _nameToGuid;

        /// <summary>
        /// Name to namespace mapping, created lazily. Implicit namespaces have null value.
        /// </summary>
        public NameToNamespace _nameToNs;

        public Fields(Document<TDoc, TNode> src)
        {
            _guidToNode = src._guidToNode;
            _nameToGuid = src._nameToGuid;
            _guidToNs = src._guidToNs;
            _nameToNs = src._nameToNs;
        }
    }

    // Guid to node mapping.
    protected readonly GuidToNode _guidToNode;

    // Guid to namespace mapping, for explicit namespaces.
    protected readonly GuidToNamespace _guidToNs;

    // Name to node guid mapping.
    // Note that this is derived information, that is, it can be computed from _guidToNode.
    protected readonly NameToGuid _nameToGuid;

    // Name to namespace mapping. Implicit namespaces have null value.
    // Note that this is derived information, that is, it can be computed from _guidToNode and _guidToNs.
    protected readonly NameToNamespace _nameToNs;

    #endregion The fields

    protected Document()
    {
        _guidToNode = GuidToNode.Empty;
        _guidToNs = GuidToNamespace.Empty;
        _nameToGuid = NameToGuid.Empty;
        _nameToNs = NameToNamespace.Empty.SetItem(NPath.Root, null);
        Validation.Assert(_nameToNs != null);
        AssertValid(new Fields(this));
    }

    protected Document(in Fields flds)
    {
        AssertValid(in flds);

        _guidToNode = flds._guidToNode;
        _nameToGuid = flds._nameToGuid;
        _guidToNs = flds._guidToNs;
        _nameToNs = EnsureNameToNs(in flds);
        Validation.Assert(_nameToNs != null);
        AssertValid(new Fields(this));
    }

    /// <summary>
    /// Makes sure the <see cref="_nameToNs"/> map is built (non-null). Note that this
    /// map is redundant information (a cache), and does NOT need to be persisted.
    /// </summary>
    private static NameToNamespace EnsureNameToNs(in Fields flds)
    {
        AssertValid(in flds);

        if (flds._nameToNs != null)
            return flds._nameToNs;

        var map = NameToNamespace.Empty;
        map = map.SetItem(NPath.Root, null);
        foreach (var (guid, ns) in flds._guidToNs.GetPairs())
        {
            Validation.Assert(ns.Guid == guid);
            map = map.SetItem(ns.Name, ns);
            for (var n = ns.Name; !(n = n.Parent).IsRoot && !map.ContainsKey(n);)
                map = map.SetItem(n, null);
        }

        foreach (var node in flds._guidToNode.GetValues())
        {
            for (var n = node.Name; !(n = n.Parent).IsRoot && !map.ContainsKey(n);)
                map = map.SetItem(n, null);
        }

#if DEBUG
        var fldsNew = flds;
        fldsNew._nameToNs = map;
        AssertValid(in fldsNew);
#endif
        return map;
    }

    /// <summary>
    /// Asserts validity of the given fields. Fundamentally, the name-based maps should
    /// be consistent with the guid-based maps (since they can be totally derived from the
    /// guid maps) and there should be no name conflicts between nodes and namespaces.
    /// </summary>
    /// <param name="flds"></param>
    [Conditional("DEBUG")]
    protected static void AssertValid(in Fields flds)
    {
#if DEBUG
        Validation.AssertValue(flds._guidToNode);
        Validation.AssertValue(flds._guidToNs);
        Validation.AssertValue(flds._nameToGuid);
        Validation.AssertValueOrNull(flds._nameToNs);

        foreach (var node in flds._guidToNode.GetValues())
        {
            var name = node.Name;
            Validation.Assert(!name.IsRoot);
            Validation.Assert(flds._nameToGuid.TryGetValue(name, out var guid) && guid == node.Guid);
            if (flds._nameToNs != null)
            {
                Validation.Assert(!flds._nameToNs.ContainsKey(name));
                Validation.Assert(flds._nameToNs.ContainsKey(name.Parent));
            }
        }
        Validation.Assert(flds._nameToGuid.Count == flds._guidToNode.Count);

        foreach (var (guid, ns) in flds._guidToNs.GetPairs())
        {
            Validation.Assert(ns != null);
            Validation.Assert(ns.Guid == guid);
            if (flds._nameToNs != null)
            {
                Validation.Assert(flds._nameToNs.TryGetValue(ns.Name, out var tmp));
                Validation.Assert(tmp == ns);
                Validation.Assert(flds._nameToNs.ContainsKey(ns.Name.Parent));
            }
        }

        if (flds._nameToNs != null)
        {
            int cnsEx = 0;
            int cnsIm = 0;
            foreach (var (name, ns) in flds._nameToNs.GetPairs())
            {
                Validation.Assert(!flds._nameToGuid.ContainsKey(name));
                if (ns == null)
                    cnsIm++;
                else
                {
                    cnsEx++;
                    flds._guidToNs.TryGetValue(ns.Guid, out var nsT).Verify();
                    Validation.Assert(nsT == ns);
                }
                Validation.Assert(flds._nameToNs.ContainsKey(name.Parent));
            }

            Validation.Assert(flds._guidToNs.Count == cnsEx);
        }
#endif
    }

    /// <summary>
    /// Returns whether the indicated <see cref="Fields"/> match this document.
    /// Since <see cref="_nameToGuid"/> and <see cref="_nameToNs"/> are determined
    /// by the guid-based maps, there is no need to test them.
    /// </summary>
    protected bool Match(in Fields flds)
    {
        AssertValid(in flds);

        if (_guidToNode != flds._guidToNode)
            return false;
        if (_guidToNs != flds._guidToNs)
            return false;

        // If these asserts fire, it's not critical, but something strange happened.
        Validation.Assert(_nameToGuid == flds._nameToGuid);
        Validation.Assert(_nameToNs == flds._nameToNs);
        return true;
    }

    [Conditional("DEBUG")]
    private void AssertNode(TNode node)
    {
#if DEBUG
        Validation.AssertValue(node);
        Validation.Assert(_guidToNode.TryGetValue(node.Guid, out var tmp) & tmp == node);
        var name = node.Name;
        Validation.Assert(!name.IsRoot);
        Validation.Assert(!_nameToNs.ContainsKey(name));
        Validation.Assert(_nameToGuid.TryGetValue(name, out var guid) & guid == node.Guid);
        Validation.Assert(_nameToNs.ContainsKey(name.Parent));
#endif
    }

    [Conditional("DEBUG")]
    private void AssertNamespace(NPath name, Namespace cur)
    {
#if DEBUG
        Validation.Assert(!_nameToGuid.ContainsKey(name));
        Validation.Assert(_nameToNs.TryGetValue(name, out var tmp) && tmp == cur);
        Validation.Assert(name.IsRoot || _nameToNs.ContainsKey(name.Parent));
        if (cur != null)
            Validation.Assert(_guidToNs.TryGetValue(cur.Guid, out tmp) & tmp == cur);
#endif
    }

    /// <summary>
    /// Get the current number of active nodes.
    /// </summary>
    public int NodeCount => _guidToNode.Count;

    /// <summary>
    /// Enumerates the current nodes.
    /// </summary>
    public IEnumerable<TNode> GetFlowNodes()
    {
        return _guidToNode.GetValues();
    }

    /// <summary>
    /// Returns a list of the current namespaces, with the name and namespace object. Note
    /// that implicit namespaces are represented as the name with a null object.
    /// </summary>
    public List<(NPath name, Namespace ns)> GetNamespaces()
    {
        return new List<(NPath name, Namespace ns)>(_nameToNs.GetPairs());
    }

    /// <summary>
    /// Lookup a node from the given name.
    /// </summary>
    public bool TryGetNode(NPath name, out TNode node)
    {
        if (!_nameToGuid.TryGetValue(name, out var guid))
        {
            node = null;
            return false;
        }

        _guidToNode.TryGetValue(guid, out node).Verify();
        Validation.Assert(node != null);
        Validation.Assert(node.Name == name);
        return true;
    }

    /// <summary>
    /// Return the node of the given name. BugChecks that there is one.
    /// </summary>
    public TNode GetNode(NPath name)
    {
        Validation.BugCheck(TryGetNode(name, out var node));
        Validation.AssertValue(node);
        return node;
    }

    /// <summary>
    /// Lookup a node from the given guid.
    /// </summary>
    public bool TryGetNode(Guid guid, out TNode node)
    {
        if (!_guidToNode.TryGetValue(guid, out node))
            return false;

        Validation.Assert(node != null);
        Validation.Assert(node.Guid == guid);
        return true;
    }

    /// <summary>
    /// Return the node of the given guid. BugChecks that there is one.
    /// </summary>
    public TNode GetNode(Guid guid)
    {
        Validation.BugCheck(TryGetNode(guid, out var node));
        Validation.AssertValue(node);
        return node;
    }

    /// <summary>
    /// Lookup a namespace from the given name. If the namespace is auto-managed (not
    /// explicit), returns true but <paramref name="ns"/> is set to null.
    /// </summary>
    public bool TryGetNamespace(NPath name, out Namespace ns)
    {
        return _nameToNs.TryGetValue(name, out ns);
    }

    /// <summary>
    /// Return the namepsace object of the given name. BugChecks that there is such a namespace.
    /// Returns null if the namespace is an auto created one.
    /// </summary>
    public Namespace GetNamespace(NPath name)
    {
        Validation.BugCheck(TryGetNamespace(name, out var ns));
        Validation.Assert(ns == null || _guidToNs.TryGetValue(ns.Guid, out var tmp) && tmp == ns);
        return ns;
    }

    /// <summary>
    /// Lookup a namespace from the given guid.
    /// </summary>
    public bool TryGetNamespace(Guid id, out Namespace ns)
    {
        return _guidToNs.TryGetValue(id, out ns);
    }

    /// <summary>
    /// Return the node of the given guid. BugChecks that there is one.
    /// </summary>
    public Namespace GetNamespace(Guid guid)
    {
        Validation.BugCheck(TryGetNamespace(guid, out var ns));
        Validation.AssertValue(ns);
        return ns;
    }

    /// <summary>
    /// Return whether there is a global with the given name.
    /// </summary>
    public bool ContainsNode(NPath name)
    {
        return _nameToGuid.ContainsKey(name);
    }

    /// <summary>
    /// Determine whether there is a namespace, explicit or implicit, with the given fully-qualified name.
    /// </summary>
    public bool ContainsNamespace(NPath name)
    {
        return _nameToNs.ContainsKey(name);
    }

    /// <summary>
    /// Return whether there is a node or namespace with the given name.
    /// </summary>
    public bool ContainsName(NPath name)
    {
        return _nameToGuid.ContainsKey(name) || _nameToNs.ContainsKey(name);
    }

    /// <summary>
    /// Return whether there is a conflict with given name. Examples of conflicts are:
    /// * <paramref name="name"/> matches an existing flow node.
    /// * <paramref name="name"/> matches an existing namespace.
    /// * A namespace in <paramref name="name"/> matches an existing flow node.
    /// </summary>
    public bool HasNameConflict(NPath name)
    {
        if (name.IsRoot)
            return true;
        if (_nameToGuid.ContainsKey(name))
            return true;
        if (_nameToNs.ContainsKey(name))
            return true;
        for (var ns = name.Parent; !ns.IsRoot; ns = ns.Parent)
        {
            if (_nameToGuid.ContainsKey(ns))
                return true;
            if (_nameToNs.ContainsKey(ns))
                return false;
        }
        return false;
    }


    /// <summary>
    /// Return whether there is a conflict with given name. Examples of conflicts are:
    /// * <paramref name="name"/> matches an existing flow node.
    /// * <paramref name="name"/> matches an existing namespace.
    /// * A namespace in <paramref name="name"/> matches an existing flow node.
    /// </summary>
    private static bool HasNameConflict(in Fields flds, NPath name)
    {
        AssertValid(in flds);

        if (name.IsRoot)
            return true;

        if (flds._nameToGuid.ContainsKey(name))
            return true;
        if (flds._nameToNs != null && flds._nameToNs.ContainsKey(name))
            return true;
        for (var ns = name.Parent; !ns.IsRoot; ns = ns.Parent)
        {
            if (flds._nameToGuid.ContainsKey(ns))
                return true;
            if (flds._nameToNs != null && flds._nameToNs.ContainsKey(ns))
                return false;
        }

        if (flds._nameToNs == null)
        {
            // The _nameToNs map isn't built yet.
            // Go through all nodes and explicit namespaces. If any of their names
            // start with name, then name is in conflict.
            foreach (var node in flds._guidToNode.GetValues())
            {
                if (node.Name.StartsWith(name))
                    return true;
            }
            foreach (var ns in flds._guidToNs.GetValues())
            {
                if (ns.Name.StartsWith(name))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Method to wrap <see cref="Fields"/> in a <see cref="TDoc"/>. If the <paramref name="flds"/> matches
    /// this doc, then it should return this doc.
    /// </summary>
    protected abstract TDoc Wrap(in Fields flds);

    #region Methods to create or "modify" TNode

    /// <summary>
    /// Create a node based on the given one but changing the name. This should NOT insert the
    /// new node in the document.
    /// </summary>
    protected abstract TNode SetNameCore(TNode node, NPath name);

    /// <summary>
    /// Create a node based on the given one but changing the main formula. This should NOT insert the
    /// new node in the document.
    /// </summary>
    protected abstract TNode SetFormulaCore(TNode node, RexlFormula fma);

    /// <summary>
    /// Create a node based on the given one but changing the config. This should NOT insert the
    /// new node in the document.
    /// </summary>
    protected abstract TNode SetConfigCore(TNode node, NodeConfig config);

    #endregion Methods to create or "modify" TNode

    /// <summary>
    /// Rename the given global. BugChecks that the name is different than its current name.
    /// The rename changes are typically computed by calling <see cref="Graph.GetRenameChanges(TNode, NPath, Func{GraphNode, bool})"/>.
    /// </summary>
    public TDoc RenameGlobal(Guid guid, NPath name, ReadOnly.Dictionary<(Guid guid, int ifma), string> renameChanges = default)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        Validation.BugCheckParam(!name.IsRoot, nameof(name));

        var nameOld = node.Name;

        // REVIEW: Should we complain about this?
        Validation.BugCheckParam(nameOld != name, nameof(name), "Shouldn't call RenameGlobal with the same name!");

        Validation.BugCheckParam(!HasNameConflict(name), nameof(name), "Name conflict");

        ValidateRenameChanges(renameChanges);
        var res = SetNameCore(node, name);

        var flds = new Fields(this);
        ReplaceNodeCore(ref flds, res, node);
        if (!renameChanges.IsDefaultOrEmpty)
            ApplyRenameChanges(ref flds, renameChanges);
        return Wrap(in flds);
    }

    private void ValidateRenameChanges(ReadOnly.Dictionary<(Guid guid, int ifma), string> renameChanges)
    {
        if (renameChanges.IsDefaultOrEmpty)
            return;

        foreach (var kvp in renameChanges)
        {
            var (guid, ifma) = kvp.Key;
            Validation.BugCheck(TryGetNode(guid, out var node));
            if (ifma < 0)
                ValidateFormulaChange(node, kvp.Value);
            else
                ValidateExtraFormulaChange(node, ifma, kvp.Value);
        }
    }

    /// <summary>
    /// Validate a main formula change, typically due to a "smart" rename.
    /// </summary>
    protected virtual void ValidateFormulaChange(TNode node, string text)
    {
        Validation.AssertValue(node);
        Validation.BugCheck(text != null);

        var fma = node.Formula;
        Validation.BugCheck(fma == null || fma.Text != text, "Shouldn't set node rename changes with the same text");
    }

    /// <summary>
    /// Validate an extra formula change, typically due to a "smart" rename.
    /// </summary>
    protected abstract void ValidateExtraFormulaChange(TNode node, int ifma, string text);

    private void ApplyRenameChanges(ref Fields fields, ReadOnly.Dictionary<(Guid guid, int ifma), string> renameChanges)
    {
        if (renameChanges.IsDefaultOrEmpty)
            return;

        foreach (var kvp in renameChanges)
        {
            var (guid, ifma) = kvp.Key;
            fields._guidToNode.TryGetValue(guid, out var node).Verify();

            TNode nodeNew;
            if (ifma < 0)
                nodeNew = ApplyFormulaChange(node, kvp.Value);
            else
                nodeNew = ApplyExtraFormulaChange(node, ifma, kvp.Value);
            Validation.Assert(guid == nodeNew.Guid);
            Validation.Assert(node.Name == nodeNew.Name);
            fields._guidToNode = fields._guidToNode.SetItem(guid, nodeNew);
        }
    }

    /// <summary>
    /// Apply a formula change, typically due to a "smart" rename.
    /// This just returns the node instance. It does NOT insert the new node into a (new) document.
    /// </summary>
    protected virtual TNode ApplyFormulaChange(TNode node, string text)
    {
        Validation.AssertValue(node);
        Validation.AssertValue(text);
        return SetFormulaCore(node, RexlFormula.Create(SourceContext.Create(text)));
    }

    /// <summary>
    /// Apply an extra formula change, typically due to a "smart" rename.
    /// This just returns the node instance. It does NOT insert the new node into a (new) document.
    /// </summary>
    protected abstract TNode ApplyExtraFormulaChange(TNode node, int ifma, string text);

    /// <summary>
    /// Map the given full name to the given node guid. This updates _nameToGuid, which
    /// should ONLY be modified by this function, RemapNameToNode, and UnmapNameFromNode.
    /// </summary>
    private static void MapNameToNode(ref Fields flds, NPath name, Guid guid)
    {
        Validation.Assert(!name.IsRoot);
        Validation.Assert(!flds._nameToGuid.ContainsKey(name));
        EnsureNamespace(ref flds, name.Parent);
        flds._nameToGuid = flds._nameToGuid.SetItem(name, guid);
    }

    /// <summary>
    /// Changes the name associated with a node guid. This updates _nameToGuid, which
    /// should ONLY be modified by this function, MapNameToNode, and UnmapNameFromNode.
    /// </summary>
    private static void RemapNameToNode(ref Fields flds, NPath nameNew, NPath nameOld, Guid guid)
    {
        Validation.Assert(!nameNew.IsRoot);
        Validation.Assert(!nameOld.IsRoot);
        Validation.Assert(nameNew != nameOld);
        Validation.Assert(!flds._nameToGuid.ContainsKey(nameNew));
        Validation.Assert(flds._nameToGuid.TryGetValue(nameOld, out var tmp) && tmp == guid);

        flds._nameToGuid = flds._nameToGuid.RemoveKnownItem(nameOld).SetItem(nameNew, guid);
        if (nameNew.Parent != nameOld.Parent)
        {
            RemoveFromNamespace(ref flds, nameOld);
            EnsureNamespace(ref flds, nameNew.Parent);
        }
    }

    /// <summary>
    /// Removes the mapping information for the given full name and guid. This updates _nameToGuid, which
    /// should ONLY be modified by this function, MapNameToNode, and RemapNameFromNode.
    /// </summary>
    private static void UnmapNameFromNode(ref Fields flds, NPath name, Guid guid)
    {
        Validation.Assert(!name.IsRoot);
        Validation.Assert(flds._nameToGuid.TryGetValue(name, out var tmp) & tmp == guid);
        flds._nameToGuid = flds._nameToGuid.RemoveKnownItem(name);
        RemoveFromNamespace(ref flds, name);
    }

    /// <summary>
    /// Should only be called by <see cref="MapNameToNs(NPath, int, bool)"/>.
    /// </summary>
    private static void EnsureNamespace(ref Fields flds, NPath name)
    {
        if (flds._nameToNs != null)
        {
            for (; !name.IsRoot && !flds._nameToNs.ContainsKey(name); name = name.Parent)
                flds._nameToNs = flds._nameToNs.SetItem(name, null);
        }
    }

    private static void RemoveFromNamespace(ref Fields flds, NPath name)
    {
        Validation.Assert(!name.IsRoot);
        if (name.NameCount <= 1)
            return;

        // If this removal might make an implicit namespace become empty, then invalidate the
        // name to ns mapping.
        if (flds._nameToNs != null && (!flds._nameToNs.TryGetValue(name.Parent, out var ns) || ns == null))
            flds._nameToNs = null;
    }

    #region Nodes

    /// <summary>
    /// Add the given node to this document. Checks that the name and guid don't conflict.
    /// </summary>
    public TDoc AddNode(TNode node)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.BugCheckParam(!HasNameConflict(node.Name), nameof(node), "Name conflict");
        Validation.BugCheckParam(!_guidToNode.ContainsKey(node.Guid), nameof(node), "Guid conflict");

        Validation.Assert(!_nameToGuid.GetValues().Any(x => x == node.Guid));

        var flds = new Fields(this);
        AddNodeCore(ref flds, node);
        return Wrap(in flds);
    }

    private static void AddNodeCore(ref Fields flds, TNode node)
    {
        AssertValid(in flds);
        Validation.AssertValue(node);
        Validation.Assert(!HasNameConflict(in flds, node.Name));
        Validation.Assert(!flds._guidToNode.ContainsKey(node.Guid));
        Validation.Assert(!flds._nameToGuid.GetValues().Any(x => x == node.Guid));

        flds._guidToNode = flds._guidToNode.SetItem(node.Guid, node);
        MapNameToNode(ref flds, node.Name, node.Guid);
        AssertValid(in flds);
    }

    /// <summary>
    /// Delete a node.
    /// </summary>
    public TDoc DeleteNode(Guid guid)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));

        var flds = new Fields(this);
        DeleteNodeCore(ref flds, node, guid);
        return Wrap(in flds);
    }

    private static void DeleteNodeCore(ref Fields flds, TNode node, Guid guid)
    {
        Validation.AssertValue(node);
        Validation.Assert(node.Guid == guid);

        flds._guidToNode = flds._guidToNode.RemoveKnownItem(node.Guid);
        UnmapNameFromNode(ref flds, node.Name, node.Guid);
        AssertValid(in flds);
    }

    /// <summary>
    /// Replace an existing node (of the same guid) with the given <paramref name="node"/>.
    /// </summary>
    public TDoc ReplaceNode(TNode node)
    {
        Validation.BugCheckValue(node, nameof(node));
        var guid = node.Guid;
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var prev), nameof(node));
        Validation.Assert(prev.Guid == guid);

        if (node == prev)
            return (TDoc)this;

        // Check for a rename to a conflicting name.
        Validation.BugCheckParam(node.Name == prev.Name || !HasNameConflict(node.Name), nameof(node), "Name conflict");

        var flds = new Fields(this);
        ReplaceNodeCore(ref flds, node, prev);
        return Wrap(in flds);
    }

    private void ReplaceNodeCore(ref Fields flds, TNode node, TNode prev)
    {
        Validation.AssertValue(node);
        Validation.AssertValue(prev);
        Validation.Assert(node != prev);
        Validation.Assert(prev.Guid == node.Guid);

        var guid = node.Guid;
        if (node.Name != prev.Name)
        {
            MapNameToNode(ref flds, node.Name, guid);
            UnmapNameFromNode(ref flds, prev.Name, guid);
        }

        flds._guidToNode = flds._guidToNode.SetItem(guid, node);
        AssertValid(in flds);
    }

    /// <summary>
    /// Sets the formula to the given code. Throws if code is null.
    /// This is an undoable document operation.
    /// </summary>
    public TDoc SetFormula(Guid guid, string code)
    {
        return SetFormula(guid, RexlFormula.Create(SourceContext.Create(code)));
    }

    /// <summary>
    /// Sets the node's main formula to the given formula.
    /// </summary>
    public TDoc SetFormula(Guid guid, RexlFormula fma)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));

        var fmaOld = node.Formula;
        if (fmaOld == fma || fmaOld != null && fma != null && fmaOld.Text == fma.Text)
            return (TDoc)this;

        return ReplaceNode(SetFormulaCore(node, fma));
    }

    /// <summary>
    /// Set the node config. Note that this is a document editing operation.
    /// </summary>
    public TDoc SetConfig(Guid guid, NodeConfig config)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));

        var configOld = node.Config;
        if (configOld == config)
            return (TDoc)this;
        return ReplaceNode(SetConfigCore(node, config));
    }

    #endregion Nodes

    #region Namespaces

    /// <summary>
    /// Create an explicit namespace.
    /// </summary>
    public TDoc CreateExplicitNamespace(NPath name, NamespaceConfig config, Guid? guid = null)
    {
        return CreateExplicitNamespace(out _, name, config, guid);
    }

    /// <summary>
    /// Create an explicit namespace.
    /// </summary>
    public TDoc CreateExplicitNamespace(out Namespace ns, NPath name, NamespaceConfig config, Guid? guid = null)
    {
        Validation.BugCheckParam(guid == null || !_guidToNs.ContainsKey(guid.GetValueOrDefault()), "Guid conflict");
        Validation.BugCheckParam(!_nameToGuid.ContainsKey(name), nameof(name), "Name conflict");

        bool existed;
        if (_nameToNs.TryGetValue(name, out var nsCur))
        {
            Validation.BugCheck(nsCur == null, "Namespace already exists");
            existed = true;
        }
        else
            existed = false;

        var nsNew = new Namespace(name, config, guid);
        Validation.Assert(!_guidToNs.ContainsKey(nsNew.Guid));

        var flds = new Fields(this);
        flds._guidToNs = flds._guidToNs.SetItem(nsNew.Guid, nsNew);
        if (!existed)
            EnsureNamespace(ref flds, name.Parent);
        flds._nameToNs = flds._nameToNs.SetItem(name, nsNew);
        AssertValid(in flds);

        ns = nsNew;
        return Wrap(in flds);
    }

    /// <summary>
    /// Delete an explicit namespace and optionally its contents. When <paramref name="deleteContents"/> is true,
    /// this deletes any contents of the namespace (recursively). When <paramref name="deleteContents"/> is false,
    /// if the namespace is not empty, this converts it to an implicit namespace, but doesn't delete it.
    /// Note that an implicit namespace has no <see cref="Namespace"/> object or Guid associated with it.
    /// </summary>
    public TDoc DeleteExplicitNamespace(Namespace ns, bool deleteContents)
    {
        Validation.BugCheckValue(ns, nameof(ns));
        Validation.BugCheckParam(_guidToNs.TryGetValue(ns.Guid, out var nsT) && nsT == ns, nameof(ns));
        AssertNamespace(ns.Name, ns);

        var flds = new Fields(this);
        DeleteNamespaceCore(ref flds, ns.Name, ns, deleteContents);
        AssertValid(in flds);

        return Wrap(in flds);
    }

    /// <summary>
    /// Delete a namespace by name and optionally its contents. When <paramref name="deleteContents"/> is true,
    /// this deletes any contents of the namespace (recursively). When <paramref name="deleteContents"/> is false,
    /// if the namespace is not empty, this ensures that it is an implicit namespace, but doesn't delete it.
    /// </summary>
    public TDoc DeleteNamespace(NPath name, bool deleteContents = true)
    {
        if (!_nameToNs.TryGetValue(name, out var ns))
            return (TDoc)this;

        AssertNamespace(name, ns);
        if (ns == null && !deleteContents)
            return (TDoc)this;

        var flds = new Fields(this);
        DeleteNamespaceCore(ref flds, name, ns, deleteContents);
        AssertValid(in flds);

        return Wrap(in flds);
    }

    private static void DeleteNamespaceCore(ref Fields flds, NPath name, Namespace ns, bool force)
    {
        Validation.Assert(!name.IsRoot);
        Validation.AssertValueOrNull(ns);

        if (force)
        {
            foreach (var node in flds._guidToNode.GetValues())
            {
                if (node.Name.StartsWith(name))
                {
                    flds._nameToNs = null;
                    DeleteNodeCore(ref flds, node, node.Guid);
                }
            }
            foreach (var tmp in flds._guidToNs.GetValues())
            {
                if (tmp.Name.StartsWith(name))
                {
                    flds._nameToNs = null;
                    flds._guidToNs = flds._guidToNs.RemoveKnownItem(tmp.Guid);
                }
            }
        }
        else
        {
            Validation.Assert(ns != null);
            flds._nameToNs = null;
            flds._guidToNs = flds._guidToNs.RemoveKnownItem(ns.Guid);
        }
    }

    /// <summary>
    /// Rename the given namespace. BugChecks that the name is different than its current name.
    /// </summary>
    public TDoc RenameNamespace(NPath nameOld, NPath nameNew, ReadOnly.Dictionary<(Guid guid, int ifma), string> renameChanges = default)
    {
        Validation.BugCheckParam(_nameToNs.TryGetValue(nameOld, out var ns), nameof(nameOld));
        Validation.BugCheckParam(nameOld != nameNew, nameof(nameNew), "Shouldn't call RenameNamespace with the same name!");
        Validation.BugCheckParam(!HasNameConflict(nameNew), nameof(nameNew), "Name conflict");
        Validation.BugCheckParam(!nameNew.StartsWith(nameOld), nameof(nameNew), "Can't rename namespace to child of itself");

        Validation.Assert(!nameNew.IsRoot);

        ValidateRenameChanges(renameChanges);

        var flds = new Fields(this);
        flds._nameToNs = null;

        foreach (var chd in _guidToNs.GetValues())
        {
            if (chd.Name.StartsWith(nameOld))
            {
                NPath nmNew = nameNew.AppendPartial(chd.Name, nameOld.NameCount);
                flds._guidToNs = flds._guidToNs.SetItem(chd.Guid, new Namespace(nmNew, chd.Config, chd.Guid));
            }
        }

        foreach (var (name, guid) in _nameToGuid.GetPairs())
        {
            if (name.StartsWith(nameOld))
            {
                flds._guidToNode.TryGetValue(guid, out var cur).Verify();
                NPath nmNew = nameNew.AppendPartial(name, nameOld.NameCount);
                var updated = SetNameCore(cur, nmNew);
                flds._guidToNode = flds._guidToNode.SetItem(guid, updated);
                flds._nameToGuid = flds._nameToGuid.RemoveKnownItem(name);
                flds._nameToGuid = flds._nameToGuid.SetItem(nmNew, guid);
            }
        }

        ApplyRenameChanges(ref flds, renameChanges);

        AssertValid(in flds);
        return Wrap(in flds);
    }

    /// <summary>
    /// Set the config for a namespace.
    /// </summary>
    public TDoc SetNamespaceConfig(out Namespace ns, Guid guid, NamespaceConfig config)
    {
        Validation.BugCheckParam(_guidToNs.TryGetValue(guid, out var nsCur), nameof(guid));
        Validation.Assert(nsCur.Guid == guid);

        if (nsCur.Config == config)
        {
            ns = nsCur;
            return (TDoc)this;
        }

        var nsNew = new Namespace(nsCur.Name, config, nsCur.Guid);

        var flds = new Fields(this);
        flds._guidToNs = flds._guidToNs.SetItem(guid, nsNew);
        Validation.Assert(flds._nameToNs != null);
        flds._nameToNs = flds._nameToNs.SetItem(nsNew.Name, nsNew);

        ns = nsNew;
        AssertValid(in flds);
        return Wrap(in flds);
    }

    #endregion Namespaces

    #region Status

    /// <summary>
    /// Whether the given namespace is active in this document.
    /// </summary>
    public bool IsActive(Namespace ns)
    {
        return ns != null && _guidToNs.TryGetValue(ns.Guid, out var tmp) && tmp == ns;
    }

    /// <summary>
    /// Whether the given flow node is active in this document.
    /// </summary>
    public bool IsActive(TNode node)
    {
        return node != null && _guidToNode.TryGetValue(node.Guid, out var tmp) && tmp == node;
    }

    #endregion
}

/// <summary>
/// This base class includes support for explicit extra formulas, rather than
/// extra formulas that are manufactured from other node-level information.
/// </summary>
public abstract partial class DocumentExt<TDoc, TNode> : Document<TDoc, TNode>
    where TDoc : DocumentExt<TDoc, TNode>
    where TNode : DocumentBase.DocExtNode
{
    protected DocumentExt()
        : base()
    {
    }

    protected DocumentExt(in Fields flds)
        : base(in flds)
    {
    }

    /// <summary>
    /// Insert an extra formula (that can reference "this") at the given <paramref name="index"/>
    /// within the given group, <paramref name="grp"/>.
    /// </summary>
    public TDoc InsertExtraFormula(Guid guid, int index, RexlFormula fma, int grp = 0)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));

        (int ifmaMin, int ifmaLim) = node.ExtraFormulas.GetGrpIndices(grp);
        Validation.BugCheckIndexInclusive(index, ifmaLim - ifmaMin, nameof(index));
        Validation.BugCheckValue(fma, nameof(fma));

        int ifma = ifmaMin + index;
        var extraOld = node.ExtraFormulas;
        var extraNew = extraOld.Insert(ifma, (fma, grp));
        return ReplaceNode(SetExtraCore(node, extraNew));
    }

    /// <summary>
    /// Remove the extra formula at the given <paramref name="index"/>
    /// within the given group, <paramref name="grp"/>.
    /// </summary>
    public TDoc RemoveExtraFormula(Guid guid, int index, int grp = 0)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));

        (int ifmaMin, int ifmaLim) = node.ExtraFormulas.GetGrpIndices(grp);
        Validation.BugCheckIndex(index, ifmaLim - ifmaMin, nameof(index));

        int ifma = ifmaMin + index;
        var extraOld = node.ExtraFormulas;
        Validation.Assert(extraOld[ifma].grp == grp);
        var extraNew = extraOld.RemoveAt(ifma);
        return ReplaceNode(SetExtraCore(node, extraNew));
    }

    /// <summary>
    /// Replace the extra formula at the given <paramref name="index"/>
    /// within the given group, <paramref name="grp"/>.
    /// </summary>
    public TDoc ReplaceExtraFormula(Guid guid, int index, RexlFormula fma, int grp = 0)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));

        (int ifmaMin, int ifmaLim) = node.ExtraFormulas.GetGrpIndices(grp);
        Validation.BugCheckIndex(index, ifmaLim - ifmaMin, nameof(index));
        Validation.BugCheckValue(fma, nameof(fma));

        int ifma = ifmaMin + index;
        var valOld = node.ExtraFormulas[ifma];
        Validation.Assert(valOld.grp == grp);
        Validation.BugCheck(valOld.fma != fma && valOld.fma.Text != fma.Text, "Shouldn't call ReplaceExtraFormula with the same text!");
        var extraOld = node.ExtraFormulas;
        var extraNew = extraOld.SetItem(ifma, (fma, grp));
        return ReplaceNode(SetExtraCore(node, extraNew));
    }

    /// <summary>
    /// Create a node based on the given one with but changing the extra formulas. This should NOT insert the
    /// new node in the document.
    /// </summary>
    protected abstract TNode SetExtraCore(TNode node, FmaTuple extra);

    protected override void ValidateExtraFormulaChange(TNode node, int ifma, string text)
    {
        Validation.AssertValue(node);
        Validation.BugCheckIndex(ifma, node.ExtraFormulas.Length, nameof(ifma));
        Validation.BugCheck(text != null);

        var fma = node.ExtraFormulas[ifma].fma;
        Validation.Assert(fma != null);
        Validation.BugCheck(fma.Text != text, "Shouldn't set extra formula rename changes with the same text");
    }

    protected override TNode ApplyExtraFormulaChange(TNode node, int ifma, string text)
    {
        Validation.AssertValue(node);
        Validation.AssertIndex(ifma, node.ExtraFormulas.Length);
        var valOld = node.ExtraFormulas[ifma];

        Validation.BugCheck(valOld.fma.Text != text, "Shouldn't set extra formula rename changes with the same text");
        var extraOld = node.ExtraFormulas;
        var extraNew = extraOld.SetItem(ifma, (RexlFormula.Create(SourceContext.Create(text)), valOld.grp));
        return SetExtraCore(node, extraNew);
    }
}
