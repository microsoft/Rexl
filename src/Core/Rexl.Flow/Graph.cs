// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Flow;

using BfmaTuple = Immutable.Array<(BoundFormula bfma, int grp)>;
using FmaTuple = Immutable.Array<(RexlFormula fma, int grp)>;
using GuidSet = Immutable.Set<Guid>;

partial class Document<TDoc, TNode>
{
    /// <summary>
    /// Provided by the client of the document when constructing a Graph instance to
    /// lookup functions, as well as provide fuzzy match semantics.
    /// </summary>
    public abstract class GraphHost
    {
        /// <summary>
        /// The bind options to use when doing graph analysis.
        /// </summary>
        public abstract BindOptions GetBindOptions();

        /// <summary>
        /// When a node doesn't have a main formula, the analyzer calls this to get its base type.
        /// </summary>
        public abstract bool TryGetBaseType(TNode node, out DType type);

        /// <summary>
        /// Determine the extra formulas, if any, for the given node.
        /// </summary>
        public abstract FmaTuple GetExtraFormulas(TNode node, DType typeBase, GraphNode gnPrev);

        /// <summary>
        /// Return true if <paramref name="a"/> and <paramref name="b"/> should be considered a "fuzzy" match
        /// (for error recovery). This is used, for example, when matching to a scope name.
        /// </summary>
        public virtual bool IsFuzzyMatch(string a, string b)
        {
            return StrComparer.EqCi(a, b);
        }

        /// <summary>
        /// See if there is a global that is "close" to the given name.
        /// The host determines what "close" means, typically relaxing case, perhaps accents, perhaps misspellings.
        /// </summary>
        public virtual bool TryGetGlobalFuzzyMatch(NPath ns, DName name, out DName nameGuess)
        {
            nameGuess = default;
            return false;
        }

        /// <summary>
        /// Determine if there is a namespace that is "close" to the given name.
        /// The host determines what "close" means, typically relaxing case, perhaps accents, perhaps misspellings.
        /// </summary>
        public virtual bool TryGetNamespaceFuzzyMatch(NPath ns, DName name, out DName nameGuess)
        {
            nameGuess = default;
            return false;
        }

        /// <summary>
        /// Determine if there is a function with the given name and provide its <see cref="OperInfo"/>.
        /// </summary>
        public abstract bool TryGetOperInfo(NPath name, bool user, bool fuzzy, int arity, out OperInfo info);
    }

    /// <summary>
    /// Builds a <see cref="Graph"/> for the current version of this document,
    /// sharing as much information as possible with the provided previous
    /// graph (if non-null).
    /// </summary>
    public Graph GetGraph(GraphHost host, Graph prev, bool funcsChanged = false)
    {
        Validation.AssertValue(host);
        Validation.AssertValueOrNull(prev);
        return Analyzer.Analyze(this, host, prev, funcsChanged);
    }

    /// <summary>
    /// A node in a <see cref="Graph"/>. A <see cref="GraphNode"/> is immutable.
    /// </summary>
    public abstract class GraphNode : NodeBase
    {
        /// <summary>
        /// The flow node that this graph node was composed for.
        /// </summary>
        public abstract TNode FlowNode { get; }

        /// <summary>
        /// The extra formulas, which may be "explicit", ie, part of the underlying <see cref="TNode"/>,
        /// or generated from other aspects of the <see cref="TNode"/>.
        /// </summary>
        public abstract FmaTuple ExtraFormulas { get; }

        public sealed override bool HasFormulas => Formula != null || ExtraFormulas.Length > 0;

        public sealed override bool HasParseErrors
        {
            get
            {
                if (Formula != null && Formula.HasErrors)
                    return true;
                return ExtraFormulas.Length > 0 && ExtraFormulas.Any(ef => ef.fma.HasErrors);
            }
        }

        /// <summary>
        /// The set of node guids that the main formula references.
        /// </summary>
        public abstract GuidSet BaseUses { get; }

        /// <summary>
        /// The set of node guids that are referenced by any formula in this node.
        /// </summary>
        public abstract GuidSet Uses { get; }

        /// <summary>
        /// A graph node is dirty when it's binding/type analysis used a "guess" for
        /// one of its dependents, due to being in a cycle. Note that a node may be in
        /// a cycle but not be considered dirty. Each cycle has at least one "dirty"
        /// node in it. The dirty nodes are those that reference nodes that are later
        /// in the topological sort of the graph. Note that re-analyzing can change
        /// which nodes are marked dirty. The reason this is considered a node-level
        /// attribute is that, when this happens, the binding information is known to
        /// be suspect. That is, if the "forward" referenced nodes really have different
        /// types than "guessed", then the node's binding information is incorrect.
        /// NOTE: while this is a "local" property, whether a node is IN a cycle
        /// (rather than be considered the cause of a cycle) is a graph-level property,
        /// NOT local to a node, since a node is NOT integrally tied to all the other
        /// nodes and their dependencies. This is because we share node objects
        /// with other versions of the graph. In that case the same graph node may
        /// be in a cycle in one version and not in a cycle in another version.
        /// </summary>
        public abstract bool IsDirty { get; }

        /// <summary>
        /// Whether this graph node has any parse or bind errors.
        /// </summary>
        public abstract bool HasErrors { get; }

        /// <summary>
        /// The type of the base data, either config or main formular.
        /// </summary>
        public abstract DType BaseType { get; }

        /// <summary>
        /// The output type of this node, which is the output type from the last extra formula.
        /// </summary>
        public abstract DType Type { get; }

        /// <summary>
        /// Get the bound main formula, if there is one.
        /// </summary>
        public abstract BoundFormula MainBoundFormula { get; }

        /// <summary>
        /// Get the bound extra formulas and their groups.
        /// </summary>
        public abstract BfmaTuple ExtraBoundFormulas { get; }

        /// <summary>
        /// Get a particular bound extra formula.
        /// </summary>
        public abstract BoundFormula GetExtraBoundFormula(int index);

        /// <summary>
        /// Get the type of a particular bound extra formula.
        /// </summary>
        public abstract DType GetExtraDType(int index);
    }

    /// <summary>
    /// A Graph is constructed from a particular version of a document and a <see cref="GraphHost"/>.
    /// This construction process is known colloquially as "analysis". A <see cref="Graph"/> is immutable.
    /// </summary>
    public abstract partial class Graph
    {
        /// <summary>
        /// Get the number of nodes in the graph.
        /// </summary>
        public abstract int NodeCount { get; }

        /// <summary>
        /// The set of nodes that have forward dependencies. Note that this is a graph-level property,
        /// but is just the set of nodes that are marked dirty.
        /// REVIEW: It might be useful to have more detailed cycle information available.
        /// For now, the (temporary) method to construct the old "status info" computes the compute
        /// set of nodes that are in cycles.
        /// </summary>
        public abstract GuidSet CycleCuts { get; }

        /// <summary>
        /// Enumerate the nodes in the graph in a topological sorted order (when there are no cycles). If there
        /// are cycles, the set of nodes with forward dependencies is returned by the <see cref="CycleCuts"/>
        /// property. These are considered to "cause" the cycles. Note that this set depends on the particular
        /// (approximate) topological sort order selected.
        /// </summary>
        public abstract Immutable.Array<GraphNode> GetNodes();

        /// <summary>
        /// Enumerate the nodes in the graph in guid order.
        /// </summary>
        public abstract IEnumerable<GraphNode> GetNodesByGuid();

        /// <summary>
        /// Lookup a node from the given name.
        /// </summary>
        public abstract bool TryGetNode(NPath name, out GraphNode node);

        /// <summary>
        /// Lookup a node from the given guid.
        /// </summary>
        public abstract bool TryGetNode(Guid guid, out GraphNode node);

        /// <summary>
        /// Return whether there is a node with the given name.
        /// </summary>
        public abstract bool ContainsNode(NPath name);

        /// <summary>
        /// Return the node of the given name. BugChecks that there is one.
        /// </summary>
        public GraphNode GetNode(NPath name)
        {
            Validation.BugCheck(TryGetNode(name, out var node));
            Validation.AssertValue(node);
            return node;
        }

        /// <summary>
        /// Return the node of the given guid. BugChecks that there is one.
        /// </summary>
        public GraphNode GetNode(Guid guid)
        {
            Validation.BugCheck(TryGetNode(guid, out var node));
            Validation.AssertValue(node);
            return node;
        }

        /// <summary>
        /// Enumerate the namespaces, with the name and namespace object. Note that implicit namespaces
        /// are represented as the name with a null object.
        /// </summary>
        public abstract IEnumerable<(NPath name, Namespace ns)> GetNamespaces();

        /// <summary>
        /// Lookup a namespace from the given name. If the namespace is auto-managed (not
        /// explicit), returns true but <paramref name="ns"/> is set to null.
        /// </summary>
        public abstract bool TryGetNamespace(NPath name, out Namespace ns);

        /// <summary>
        /// Lookup a namespace from the given guid. If the namespace is auto-managed (not
        /// explicit), returns true but <paramref name="ns"/> is set to null.
        /// </summary>
        public abstract bool TryGetNamespace(Guid guid, out Namespace ns);

        /// <summary>
        /// Return whether there is a namespace with the given name.
        /// </summary>
        public abstract bool ContainsNamespace(NPath name);

        /// <summary>
        /// Return the namepsace object of the given name. BugChecks that there is such a namespace.
        /// Returns null if the namespace is an auto created one.
        /// </summary>
        public Namespace GetNamespace(NPath name)
        {
            Validation.BugCheck(TryGetNamespace(name, out var ns));
            return ns;
        }

        /// <summary>
        /// Returns the document wide formula changes that should occur if the given node is renamed.
        /// If no change would occur, null is returned instead. The dictionary key values consist of
        /// a Guid for the flow node, together with a formula index, with -1 meaning the main formula and
        /// non-negative meaning an index into the ExtraFormulas immutable array. The associated value
        /// is the new text for the formula.
        /// </summary>
        public abstract Dictionary<(Guid guid, int ifma), string> GetRenameChanges(Guid guid, NPath nameNew, Func<GraphNode, bool> predicate = null);

        /// <summary>
        /// Returns the changed formula if the given node is renamed.
        /// If no change would occur, null is returned instead.
        /// </summary>
        public abstract string GetRenamedFormula(Guid guid, NPath nameNew);

        /// <summary>
        /// Returns the document wide formula changes that should occur if the given namespace is renamed.
        /// If no change would occur, null is returned instead. See <see cref="GetRenameChanges(FlowNode, NPath, Func{GraphNode, bool})"/>
        /// for an explanation of the output dictionary.
        /// </summary>
        public abstract Dictionary<(Guid guid, int ifma), string> GetNamespaceRenameChanges(NPath nameOld, NPath nameNew, Func<GraphNode, bool> predicate = null);

        public abstract string GetRenamedFormula(Guid guid, NPath nsOld, NPath nsNew);
    }
}

partial class Document<TDoc, TNode>
{
    /// <summary>
    /// Persistent dictionary from <see cref="Guid"/> to <see cref="GraphNodeImpl"/>. This disallows
    /// the default (all zero) <see cref="Guid"/>.
    /// </summary>
    private sealed class GuidToGraphNode : GuidRedBlackTree<GuidToGraphNode, GraphNodeImpl>
    {
        public static readonly GuidToGraphNode Empty = new GuidToGraphNode(null);

        private GuidToGraphNode(Node root)
            : base(root)
        {
        }

        protected override GuidToGraphNode Wrap(Node root)
        {
            return root == _root ? this : root != null ? new GuidToGraphNode(root) : Empty;
        }

        protected override bool KeyIsValid(Guid key)
        {
            return key != default;
        }

        protected override bool ValIsValid(GraphNodeImpl val)
        {
            return val != null;
        }

        protected override bool ValEquals(GraphNodeImpl val0, GraphNodeImpl val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(GraphNodeImpl val)
        {
            return val.GetHashCode();
        }
    }

    /// <summary>
    /// The implementation of the abstract class <see cref="GraphNode"/>.
    /// </summary>
    private sealed class GraphNodeImpl : GraphNode
    {
        public readonly TNode _node;

        /// <summary>
        /// Whether binding formulas for this node looked up any names unsuccessfully.
        /// Note that this doesn't imply that the formulas have errors. For example,
        /// a formula in node "N.X" that references "Y" might record a miss for
        /// "N.Y", yet find a global at the root level named "Y". The reason this
        /// field is useful is that if new names appear (in a new doc version), this
        /// node should be re-bound.
        /// </summary>
        public readonly bool _hasNameMisses;

        private GraphNodeImpl(TNode node, DType typeBase, FmaTuple fmas, DType type, BoundFormula bfmaMain, BfmaTuple bfmas,
            GuidSet usesBase, GuidSet uses, bool isDirty)
        {
            Validation.AssertValue(node);
            Validation.Assert(typeBase.IsValid);
            Validation.Assert((node.Formula != null) == (bfmaMain != null));
            Validation.Assert(bfmaMain == null || bfmaMain.Formula == node.Formula);
            Validation.Assert(bfmaMain == null || typeBase == bfmaMain.BoundTree.Type);
            Validation.Assert(type.IsValid);
            Validation.Assert(!fmas.IsDefault);
            Validation.Assert(!bfmas.IsDefault);
            Validation.Assert(fmas.Length == bfmas.Length);
            Validation.Assert(typeBase == type || bfmas.Length > 0 && type == bfmas[bfmas.Length - 1].bfma.BoundTree.Type);
            Validation.Assert(usesBase.IsSubset(uses));

            _node = node;
            ExtraFormulas = fmas;
            BaseType = typeBase;
            Type = type;
            MainBoundFormula = bfmaMain;
            ExtraBoundFormulas = bfmas;
            BaseUses = usesBase;
            Uses = uses;
            IsDirty = isDirty;

            if (bfmaMain != null && (bfmaMain.HasErrors || bfmaMain.Formula.HasErrors))
                HasErrors = true;
            else
            {
                foreach (var (bfma, grp) in ExtraBoundFormulas)
                {
                    if (bfma.HasErrors || bfma.Formula.HasErrors)
                    {
                        HasErrors = true;
                        break;
                    }
                }
            }

            if (bfmaMain != null && bfmaMain.HostInfo is ReferenceBindInfo rbi && rbi.HasNameMisses())
                _hasNameMisses = true;
            else
            {
                foreach (var (bfma, grp) in ExtraBoundFormulas)
                {
                    if (bfma.HostInfo is ReferenceBindInfo rbiTmp && rbiTmp.HasNameMisses())
                    {
                        _hasNameMisses = true;
                        break;
                    }
                }
            }
        }

        public static GraphNodeImpl Create(TNode node, DType typeBase, FmaTuple fmas, BoundFormula bfmaMain, BfmaTuple bfmas,
            GuidSet usesBase, GuidSet uses, bool isDirty)
        {
            var type = bfmas.Length > 0 ? bfmas[bfmas.Length - 1].bfma.BoundTree.Type : typeBase;
            return new GraphNodeImpl(node, typeBase, fmas, type, bfmaMain, bfmas, usesBase, uses, isDirty);
        }

        public override Guid Guid => _node.Guid;

        public override NPath Name => _node.Name;

        public override NodeConfig Config => _node.Config;

        public override RexlFormula Formula => _node.Formula;

        public override TNode FlowNode => _node;

        public override FmaTuple ExtraFormulas { get; }

        public override GuidSet BaseUses { get; }

        public override GuidSet Uses { get; }

        public override bool HasErrors { get; }

        public override bool IsDirty { get; }

        public override DType BaseType { get; }

        public override DType Type { get; }

        public override BoundFormula MainBoundFormula { get; }

        public override BfmaTuple ExtraBoundFormulas { get; }

        public override BoundFormula GetExtraBoundFormula(int index)
        {
            Validation.BugCheckIndex(index, ExtraBoundFormulas.Length, nameof(index));
            return ExtraBoundFormulas[index].bfma;
        }

        public override DType GetExtraDType(int index)
        {
            Validation.BugCheckIndex(index, ExtraBoundFormulas.Length, nameof(index));
            return ExtraBoundFormulas[index].bfma.BoundTree.Type;
        }
    }

    /// <summary>
    /// The implementation of the abstract class <see cref="Graph"/>.
    /// </summary>
    private sealed partial class GraphImpl : Graph
    {
        /// <summary>
        /// When there are no cycles, this is a topological sort order. When there are cycles,
        /// nodes that have forward dependencies are guaranteed to be in at least one cycle. Also,
        /// each cycles contains at least one such node. These nodes are said to "cause" cycles.
        /// To identify all nodes in cycles, we could also compute the transitive closure of dependencies.
        /// Then any node that depends on itself would be in a cycle. The transitive closures can be expensive
        /// to compute, but may be generally useful for other things. For now, we don't bother computing
        /// this up front (but do in the temporary method to construct the old "status info").
        /// </summary>
        public readonly Immutable.Array<Guid> _topo;

        /// <summary>
        /// The graph nodes in the order specified by <see cref="_topo"/>.
        /// </summary>
        public readonly Immutable.Array<GraphNode> _topoNodes;

        /// <summary>
        /// Maps from name to guid of the graph nodes.
        /// </summary>
        public readonly NameToGuid _nameToGuid;

        /// <summary>
        /// Maps from guid to <see cref="GraphNodeImpl"/> instances.
        /// </summary>
        public readonly GuidToGraphNode _guidToNode;

        /// <summary>
        /// Maps from name of namespace to <see cref="Namespace"/> instance (for explicit namespaces)
        /// or null (for implicit namespaces). Provided by the document, but cached here.
        /// </summary>
        public readonly NameToNamespace _nameToNs;

        /// <summary>
        /// Maps from guid of explicit namespace to the <see cref="Namespace"/> instance.
        /// </summary>
        public readonly GuidToNamespace _guidToNs;

        public GraphImpl(
            Immutable.Array<Guid> topo, NameToGuid nameToGuid, GuidToGraphNode guidToNode,
            NameToNamespace nameToNs, GuidToNamespace guidToNs)
        {
            Validation.Assert(!topo.IsDefault);
            Validation.AssertValue(nameToGuid);
            Validation.AssertValue(guidToNode);
            Validation.Assert(nameToGuid.Count == topo.Length);
            Validation.Assert(guidToNode.Count == topo.Length);
            Validation.AssertValue(nameToNs);
            Validation.AssertValue(guidToNs);
            Validation.Assert(topo.All(guid => guidToNode.ContainsKey(guid)));
            Validation.Assert(nameToGuid.GetPairs().All(pair => guidToNode.TryGetValue(pair.val, out var node) && node.Name == pair.key));
            _topo = topo;
            _nameToGuid = nameToGuid;
            _guidToNode = guidToNode;
            _nameToNs = nameToNs;
            _guidToNs = guidToNs;

            // Find the set of nodes with forward dependencies. These are said to "cause" cycles.
            // Note that the precise set of nodes deemed to "cause" cycles is not uniquely determined.
            // The set of cause nodes is dependent on the topological sort order chosen.
            var bldr = Immutable.Array.CreateBuilder<GraphNode>(_topo.Length, init: true);
            GuidSet cuts = default;
            GuidSet seen = default;
            for (int i = 0; i < _topo.Length; i++)
            {
                var guid = _topo[i];
                Validation.Assert(!seen.Contains(guid));
                _guidToNode.TryGetValue(guid, out var gn).Verify();
                bldr[i] = gn;

                // The IsDirty flag should be the same as having no forward dependencies, but it's
                // more robust to not count on that invariant.
                Validation.Assert(gn.IsDirty == !gn.Uses.IsSubset(seen));
                if (!gn.Uses.IsSubset(seen))
                    cuts = cuts.Add(guid);

                seen = seen.Add(guid);
            }
            _topoNodes = bldr.ToImmutable();
            CycleCuts = cuts;
        }

        public override int NodeCount => _topoNodes.Length;

        public override GuidSet CycleCuts { get; }

        public override Immutable.Array<GraphNode> GetNodes()
        {
            return _topoNodes;
        }

        public override IEnumerable<GraphNode> GetNodesByGuid()
        {
            return _guidToNode.GetValues();
        }

        public override bool TryGetNode(NPath name, out GraphNode node)
        {
            if (!_nameToGuid.TryGetValue(name, out var guid))
            {
                node = null;
                return false;
            }

            _guidToNode.TryGetValue(guid, out var nodeTmp).Verify();
            Validation.Assert(nodeTmp != null);
            Validation.Assert(nodeTmp.Name == name);
            node = nodeTmp;
            return true;
        }

        public override bool TryGetNode(Guid guid, out GraphNode node)
        {
            if (!_guidToNode.TryGetValue(guid, out var nodeTmp))
            {
                node = null;
                return false;
            }

            Validation.Assert(nodeTmp != null);
            Validation.Assert(nodeTmp.Guid == guid);
            node = nodeTmp;
            return true;
        }

        public override bool ContainsNode(NPath name)
        {
            return _nameToGuid.ContainsKey(name);
        }

        public override IEnumerable<(NPath name, Namespace ns)> GetNamespaces()
        {
            return new List<(NPath name, Namespace ns)>(_nameToNs.GetPairs());
        }

        public override bool TryGetNamespace(NPath name, out Namespace ns)
        {
            return _nameToNs.TryGetValue(name, out ns);
        }

        public override bool TryGetNamespace(Guid guid, out Namespace ns)
        {
            return _guidToNs.TryGetValue(guid, out ns);
        }

        public override bool ContainsNamespace(NPath name)
        {
            return _nameToNs.ContainsKey(name);
        }

        private Immutable.Array<(Guid guid, GuidSet deps)> GetDependencies()
        {
            int count = NodeCount;
            var bldr = Immutable.Array.CreateBuilder<(Guid guid, GuidSet deps)>(count, init: true);

            var guidToIndex = new Dictionary<Guid, int>(count);
            bool needMore = false;
            int i = 0;
            foreach (var gn in GetNodes())
            {
                var uses = gn.Uses;
                foreach (var guid in gn.Uses)
                {
                    if (guidToIndex.TryGetValue(guid, out int index))
                    {
                        Validation.AssertIndex(index, i);
                        uses |= bldr[index].deps;
                    }
                    else if (guid != gn.Guid)
                        needMore = true;
                }
                guidToIndex.Add(gn.Guid, i);
                bldr[i++] = (gn.Guid, uses);
            }
            Validation.Assert(i == count);

            while (needMore)
            {
                needMore = false;
                for (i = 0; i < count; i++)
                {
                    var (guid, uses) = bldr[i];
                    foreach (var guidDep in uses)
                    {
                        if (guidDep == guid)
                            continue;
                        int ivDep = guidToIndex[guidDep];
                        var (guidTmp, usesDep) = bldr[ivDep];
                        if (usesDep.IsSubset(uses))
                            continue;
                        uses |= usesDep;
                        needMore = true;
                    }
                    bldr[i] = (guid, uses);
                }
            }

            return bldr.ToImmutable();
        }
    }

    // This partial contains support for smart renaming.
    private sealed partial class GraphImpl : Graph
    {
        public override Dictionary<(Guid guid, int ifma), string> GetRenameChanges(Guid guid, NPath nameNew, Func<GraphNode, bool> predicate = null)
        {
            Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var gn), nameof(guid));

            var nameOld = gn.Name;
            if (nameOld == nameNew || nameOld.IsRoot || nameNew.IsRoot || _nameToGuid.ContainsKey(nameNew) || TryGetNamespace(nameNew, out _))
                return null;

            var nsChanged = nameNew.Parent != nameOld.Parent;
            var gns = _guidToNode.GetValues().Where(tmp => tmp.HasFormulas && (predicate == null || predicate(tmp)));
            return new NodeNameMapper(this, nameOld, nameNew).GetRenameChanges(gns);
        }

        public override string GetRenamedFormula(Guid guid, NPath nameNew)
        {
            Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var gn), nameof(guid));

            var nameOld = gn.Name;
            if (nameOld == nameNew || nameOld.IsRoot || nameNew.IsRoot || _nameToGuid.ContainsKey(nameNew) || TryGetNamespace(nameNew, out _))
                return null;

            return new NodeNameMapper(this, nameOld, nameNew).RenameFormula(gn.Name, gn.MainBoundFormula, gn.Formula);
        }

        public override Dictionary<(Guid guid, int ifma), string> GetNamespaceRenameChanges(NPath nameOld, NPath nameNew, Func<GraphNode, bool> predicate = null)
        {
            if (nameOld == nameNew || nameNew.IsRoot || _nameToGuid.ContainsKey(nameNew) || TryGetNamespace(nameNew, out _))
                return null;

            var gns = _guidToNode.GetValues().Where(tmp => tmp.HasFormulas && (predicate == null || predicate(tmp)));
            return new NamespaceNameMapper(this, nameOld, nameNew).GetRenameChanges(gns);
        }

        public override string GetRenamedFormula(Guid guid, NPath nsOld, NPath nsNew)
        {
            Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var gn), nameof(guid));

            if (nsOld == nsNew || nsNew.IsRoot || _nameToGuid.ContainsKey(nsNew) || TryGetNamespace(nsNew, out _))
                return null;

            return new NamespaceNameMapper(this, nsOld, nsNew).RenameFormula(gn.Name, gn.MainBoundFormula, gn.Formula);
        }

        /// <summary>
        /// When a name in the document changes, all formulas referencing the old name should be updated to the new
        /// name. A NameMapper is a helper to rewrite an affected formula or compute the updates to the affected nodes
        /// when some name change happens.
        /// </summary>
        private abstract class NameMapper
        {
            protected readonly GraphImpl _graph;
            protected readonly NPath _nameOld;
            protected readonly NPath _nameNew;

            /// The list <paramref name="_locs"/> contains the new path for referencing the global, the parse
            /// node from the original text, and an indicator if we need to use the @ symbol to distinguish it.
            protected List<(NPath nameText, RexlNode loc, bool needAt)> _locs;
            private SbTextSink _sink;

            protected NameMapper(GraphImpl graph, NPath nameOld, NPath nameNew)
            {
                Validation.AssertValue(graph);
                Validation.Assert(nameOld != nameNew);
                Validation.Assert(!nameNew.IsRoot);
                _graph = graph;
                _nameOld = nameOld;
                _nameNew = nameNew;
            }

            /// <summary>
            /// Returns the changed formula or null if no change.
            /// </summary>
            public string RenameFormula(NPath nameCur, BoundFormula bfma, RexlFormula fma)
            {
                Validation.AssertValue(bfma);
                GetLocs(nameCur, bfma);

                if (Util.Size(_locs) == 0)
                    return null;

                Validation.AssertValue(fma);
                Validation.Assert(bfma.Formula == fma);

#if DEBUG
                // Assert that the nodes are sorted. The ranges of tokens shouldn't overlap but may touch.
                Validation.AssertValue(_locs);
                Validation.Assert(_locs.Any());
                var locsArr = _locs.ToArray();
                for (int j = 0; j < locsArr.Length - 1; j++)
                    Validation.Assert(locsArr[j].loc.Token.Range.Lim <= locsArr[j + 1].loc.Token.Range.Min);
#endif

                if (_sink == null)
                    _sink = new SbTextSink();
                else
                    _sink.Builder.Clear();
                var ichLimSrc = 0;
                var text = fma.Text;
                Validation.Assert(!string.IsNullOrWhiteSpace(text));
                foreach (var (nameText, loc, needAt) in _locs)
                {
                    Validation.Assert(loc is FirstNameNode || loc is DottedNameNode);
                    var rng = loc.GetFullRange();
                    Validation.Assert(rng.Min >= ichLimSrc);
                    Validation.Assert(rng.Lim <= text.Length);

                    // Surrounding can cause the lexer to not recognize a spliced range as a token boundary,
                    // for example when a quoted identifier is adjacent to a keyword. We need to inject a
                    // space in this case.
                    if (rng.Min > ichLimSrc)
                        _sink.Write(text.AsSpan(ichLimSrc, rng.Min - ichLimSrc));
                    ichLimSrc = rng.Lim;

                    if (LexUtils.NeedSpaceBeforeIdent(_sink.Builder))
                        _sink.Write(' ');

                    // If the old name was an implicit name, we need to explicitly create the variable.
                    if (bfma.IsUsedImplicitName(loc))
                    {
                        var fnn = loc.Cast<FirstNameNode>();
                        _sink.WriteEscapedName(fnn.Ident.Name).Write(": ");
                    }
                    if (needAt)
                        _sink.Write('@');

                    _sink.WriteDottedSyntax(nameText);
                    if (ichLimSrc < text.Length && LexUtils.IsIdentPossible(text[ichLimSrc]))
                        _sink.Write(' ');
                }
                Validation.Assert(ichLimSrc <= text.Length);
                if (ichLimSrc < text.Length)
                    _sink.Write(text.AsSpan(ichLimSrc, text.Length - ichLimSrc));

                var result = _sink.Builder.ToString();
                _sink.Builder.Clear();
                _locs.Clear();

                return result;
            }

            /// <summary>
            /// Returns formula changes that should occur on the given <paramref name="gns"/>.
            /// </summary>
            public Dictionary<(Guid guid, int ifma), string> GetRenameChanges(IEnumerable<GraphNode> gns)
            {
                Dictionary<(Guid guid, int ifma), string> changes = null;
                foreach (var gn in gns)
                {
                    Validation.Assert(gn != null && gn.HasFormulas);

                    int cex = gn.ExtraFormulas.Length;
                    for (int ifma = -1; ifma < cex; ifma++)
                    {
                        RexlFormula fma;
                        BoundFormula bfma;

                        if (ifma < 0)
                        {
                            if ((fma = gn.Formula) == null)
                                continue;
                            bfma = gn.MainBoundFormula;
                            Validation.AssertValue(bfma);
                            Validation.Assert(bfma.Formula == gn.Formula);
                        }
                        else
                        {
                            fma = gn.ExtraFormulas[ifma].fma;
                            bfma = gn.GetExtraBoundFormula(ifma);
                        }

                        string renamedFormula = RenameFormula(gn.Name, bfma, fma);
                        if (renamedFormula != null)
                            Util.Add(ref changes, (gn.Guid, ifma), renamedFormula);
                    }
                }

                return changes;
            }

            protected static int Cmp((NPath nameText, RexlNode loc, bool needAt) a, (NPath nameText, RexlNode loc, bool needAt) b)
            {
                var rangeA = a.loc.GetFullRange();
                var rangeB = b.loc.GetFullRange();
                // The ranges shouldn't overlap. They may be empty or touch with another.
                Validation.Assert(rangeA.Lim <= rangeB.Min || rangeB.Lim <= rangeA.Min || a.loc == b.loc);
                return rangeA.Min + rangeA.Lim - rangeB.Min - rangeB.Lim;
            }

            /// <summary>
            /// Returns a suitable suffix of <paramref name="nameGbl"/> that will refer to it
            /// when bound in namespace <paramref name="nsCur"/> in the context of <paramref name="bfma"/>
            /// at location <paramref name="loc"/>. If there is none, the entire
            /// path will be returned and <paramref name="needAt"/> will be set to true.
            ///
            /// REVIEW: What kind of rules can we use to preserve (parts of) the original used path?
            /// Currently we always return the shortest possible suffix.
            /// </summary>
            protected static NPath RewritePath(NPath nsCur, NPath nameGbl, BoundFormula bfma, RexlNode loc, Func<NPath, bool> isNameFunc, out bool needAt)
            {
                var len = nameGbl.NameCount;
                var inScope = bfma.TryGetNodeScopeInfo(loc, out var scopes);
                var common = nameGbl.GetCommonPrefix(nsCur);
                for (int i = common.NameCount; i >= 0; i--)
                {
                    Validation.Assert(i < len);
                    var usedPathTmp = NPath.Root.AppendPartial(nameGbl, i);
                    var first = usedPathTmp.First;

                    // Avoid clashing with a scope variable.
                    if (inScope && scopes.Any(s => s.ContainsName(first)))
                        continue;

                    // Walk up the namespace to see if the used path will correctly bind.
                    for (var prefix = nsCur; ; prefix = prefix.Parent)
                    {
                        Validation.Assert(prefix.NameCount >= i);
                        if (prefix.NameCount == i)
                        {
                            // Used path refers to the correct global.
                            needAt = false;
                            return usedPathTmp;
                        }

                        var path = prefix.Append(first);
                        if (isNameFunc(path))
                        {
                            // Name will change to refer to something different.
                            // Must lengthen used path or prefix with '@'.
                            break;
                        }
                    }
                }

                // No suffix of the global can refer it so we need to use the entire global path, prefixed with @.
                needAt = true;
                return nameGbl;
            }

            protected static (NPath usedPath, bool hasAt) GetGlobalPath(RexlNode node)
            {
                switch (node)
                {
                case DottedNameNode dnn:
                    var idents = dnn.ToIdents();
                    return (idents.FullName, idents.First.IsGlobal);
                case FirstNameNode fnn:
                    return (NPath.Root.Append(fnn.Ident.Name), fnn.Ident.IsGlobal);
                default:
                    Validation.Assert(false);
                    return (default, false);
                }
            }

            /// <summary>
            /// The parameters are the name and bound formula of a node whose formula
            /// we may wish to rewrite, as well as a null or empty list.
            /// The function should fill in the list <paramref name="_locs"/> with global path information for modifying the formula text.
            /// The list contents should include the new path for the global, the parse node from the original text,
            /// and an indicator if we need to use the @ symbol to distinguish it.
            /// </summary>
            protected abstract void GetLocs(NPath nameCur, BoundFormula bfma);
        }

        /// <summary>
        /// A NodeNameMapper is a helper to rewrite an affected formulas or compute the updates to the affected nodes
        /// when a node in the document is renamed.
        /// </summary>
        private sealed class NodeNameMapper : NameMapper
        {
            public NodeNameMapper(GraphImpl graph, NPath nameOld, NPath nameNew)
                : base(graph, nameOld, nameNew)
            {
            }

            private bool IsName(NPath path)
            {
                if (path == _nameOld)
                    return false;
                return _graph._nameToGuid.ContainsKey(path) || _graph._nameToNs.ContainsKey(path);
            }

            protected override void GetLocs(NPath nameCur, BoundFormula bfma)
            {
                Validation.AssertValue(bfma);
                Validation.Assert(Util.Size(_locs) == 0);
                bool nsChanged = _nameNew.Parent != _nameOld.Parent;

                var info = bfma.HostInfo as ReferenceBindInfo;
                if (info == null)
                    return;

                if (nameCur == _nameOld && nsChanged)
                {
                    // If the namespace of the current node changes, we need to change
                    // the references to globals in case the current used paths no longer
                    // work.
                    foreach (var (nameGbl, gblRefs) in info.GetReferencedGlobals())
                    {
                        if (nameGbl == _nameOld)
                            continue;
                        foreach (var loc in gblRefs)
                        {
                            var (usedPath, hasAt) = GetGlobalPath(loc);
                            // Respect the @ designator and keep the full path of the global.
                            if (hasAt)
                                continue;

                            var finalPath = RewritePath(_nameNew.Parent, nameGbl, bfma, loc, IsName, out var needAt);
                            if (needAt || finalPath != usedPath)
                                Util.Add(ref _locs, (finalPath, loc, needAt));
                        }
                    }
                }

                // REVIEW: Self-reference is technically valid for referencing
                // a node's raw data. This is not necessary if we use a keyword like 'this' instead.
                var nsCur = nameCur == _nameOld ? _nameNew.Parent : nameCur.Parent;
                var parseNodes = info.GetParseNodesForName(_nameOld);
                foreach (var loc in parseNodes)
                {
                    var (usedPath, hasAt) = GetGlobalPath(loc);
                    if (hasAt)
                    {
                        // Respect the @ designator and keep the full path of the global.
                        Validation.Assert(usedPath == _nameOld);
                        Util.Add(ref _locs, (_nameNew, loc, true));
                        continue;
                    }

                    var finalPath = RewritePath(nsCur, _nameNew, bfma, loc, IsName, out var needAt);
                    if (needAt || finalPath != usedPath)
                        Util.Add(ref _locs, (finalPath, loc, needAt));
                }

                if (Util.Size(_locs) > 1 && nameCur == _nameOld && nsChanged)
                    _locs.Sort(Cmp);
            }
        }

        /// <summary>
        /// A NamespaceNameMapper is a helper to rewrite an affected formula or compute the updates to the affected
        /// nodes when a namespace in the document is renamed.
        /// </summary>
        private sealed class NamespaceNameMapper : NameMapper
        {
            public NamespaceNameMapper(GraphImpl graph, NPath nameOld, NPath nameNew)
                : base(graph, nameOld, nameNew)
            {
            }

            private bool IsName(NPath path)
            {
                if (path.StartsWith(_nameOld))
                    path = _nameNew.AppendPartial(path, _nameOld.NameCount);
                return _graph._nameToGuid.ContainsKey(path) || _graph._nameToNs.ContainsKey(path);
            }

            protected override void GetLocs(NPath nameCur, BoundFormula bfma)
            {
                Validation.AssertValue(bfma);
                Validation.Assert(Util.Size(_locs) == 0);

                var info = bfma.HostInfo as ReferenceBindInfo;
                if (info == null)
                    return;

                var nameCurNew = nameCur;
                if (nameCur.StartsWith(_nameOld))
                    nameCurNew = _nameNew.AppendPartial(nameCur, _nameOld.NameCount);

                foreach (var (nameGbl, gblRefs) in info.GetReferencedGlobals())
                {
                    foreach (var loc in gblRefs)
                    {
                        var (usedPath, hasAt) = GetGlobalPath(loc);
                        if (hasAt)
                        {
                            // Respect the @ designator and keep the full path of the global.
                            Validation.Assert(nameGbl == usedPath);
                            if (nameGbl.StartsWith(_nameOld))
                                Util.Add(ref _locs, (_nameNew.AppendPartial(nameGbl, _nameOld.NameCount), loc, true));
                            continue;
                        }

                        var nameGblNew = nameGbl;
                        if (nameGbl.StartsWith(_nameOld))
                            nameGblNew = _nameNew.AppendPartial(nameGbl, _nameOld.NameCount);
                        var finalPath = RewritePath(nameCurNew.Parent, nameGblNew, bfma, loc, IsName, out var needAt);
                        if (needAt || finalPath != usedPath)
                            Util.Add(ref _locs, (finalPath, loc, needAt));
                    }
                }

                if (Util.Size(_locs) > 1)
                    _locs.Sort(Cmp);
            }
        }
    }

    /// <summary>
    /// The analyzer is essentially a builder of <see cref="GraphImpl"/>. It is short-lived, ie,
    /// created, run, forgotten.
    /// </summary>
    private sealed partial class Analyzer
    {
        private readonly GraphHost _host;
        private readonly NameToGuid _nameToGuid;
        private readonly GuidToNode _guidToNode;
        private readonly NameToNamespace _nameToNs;
        private readonly GuidToNamespace _guidToNs;

        private readonly GraphImpl _prev;
        private readonly bool _hasNameChanges;
        private readonly bool _funcsChanged;

        private GuidToGraphNode _guidToGn;
        private Immutable.Array<Guid> _topoPrev;
        private Immutable.Array<Guid>.Builder _bldrTopo;
        private int _ivDst;

        private GuidSet _done;
        private GuidSet _typeChanged;
        private GuidSet _nameChanged;

        private List<(Guid guid, GuidSet need)> _stack;

        private Analyzer(Document<TDoc, TNode> doc, GraphHost host, Graph prev, bool funcsChanged)
        {
            Validation.AssertValue(doc);
            Validation.AssertValue(host);
            Validation.Assert(prev == null || prev is GraphImpl);

            _host = host;
            _nameToGuid = doc._nameToGuid;
            _guidToNode = doc._guidToNode;
            _nameToNs = doc._nameToNs;
            _guidToNs = doc._guidToNs;

            _prev = (GraphImpl)prev;
            _funcsChanged = funcsChanged;

            if (_prev != null)
            {
                _topoPrev = _prev._topo;
                _guidToGn = _prev._guidToNode;
                _hasNameChanges = !_prev._nameToGuid.SameRoot(_nameToGuid);
            }
            else
            {
                _topoPrev = Immutable.Array<Guid>.Empty;
                _guidToGn = GuidToGraphNode.Empty;
                _hasNameChanges = _nameToGuid.Count > 0;
            }
            Validation.Assert(_topoPrev.Length == _guidToGn.Count);

            _bldrTopo = _topoPrev.ToBuilder();

            _done = default;
            _typeChanged = default;
        }

        public static Graph Analyze(Document<TDoc, TNode> doc, GraphHost host, Graph prev, bool funcsChanged)
        {
            var analyzer = new Analyzer(doc, host, prev, funcsChanged);
            analyzer.RemoveDeleted();
            analyzer.ProcessNodes();
            return analyzer.MakeGraph();
        }

        /// <summary>
        /// Remove deleted nodes, that is, ones in the previous graph that are not in the document.
        /// </summary>
        private void RemoveDeleted()
        {
            int ivDst = 0;
            for (int ivSrc = 0; ivSrc < _topoPrev.Length; ivSrc++)
            {
                var guid = _topoPrev[ivSrc];
                Validation.Assert(_guidToGn.ContainsKey(guid));
                if (!_guidToNode.ContainsKey(guid))
                    _guidToGn = _guidToGn.RemoveKnownItem(guid);
                else
                {
                    if (ivDst < ivSrc)
                        _bldrTopo[ivDst] = guid;
                    ivDst++;
                }
            }
            Validation.Assert(_guidToGn.Count == ivDst);
            if (ivDst < _bldrTopo.Count)
                _bldrTopo.RemoveTail(ivDst);
            Validation.Assert(_guidToGn.Count == _bldrTopo.Count);
            _topoPrev = _bldrTopo.ToImmutable();
        }

        /// <summary>
        /// Process all the nodes.
        /// </summary>
        private void ProcessNodes()
        {
            Validation.Assert(_bldrTopo.Count <= _guidToNode.Count);
            if (_bldrTopo.Count < _guidToNode.Count)
                _bldrTopo = Immutable.Array<Guid>.CreateBuilder(_guidToNode.Count, init: true);
            Validation.Assert(_ivDst == 0);

            // First process previously existing nodes in the proper order.
            for (int ivSrc = 0; ivSrc < _topoPrev.Length; ivSrc++)
            {
                var guid = _topoPrev[ivSrc];
                Validation.Assert(_guidToNode.ContainsKey(guid));
                if (!_done.Contains(guid))
                    ProcessNode(guid);
                Validation.Assert(_done.Count == _ivDst);
            }

            // Now process any newly added nodes that haven't yet been processed.
            if (_ivDst < _bldrTopo.Count)
            {
                foreach (var guidSrc in _guidToNode.GetKeys())
                {
                    var guid = guidSrc;
                    Validation.Assert(_guidToNode.ContainsKey(guid));
                    if (!_done.Contains(guid))
                        ProcessNode(guid);
                    Validation.Assert(_done.Count == _ivDst);
                }
            }
        }

        /// <summary>
        /// Process the indicated node and any that it depends on.
        /// </summary>
        private void ProcessNode(Guid guid)
        {
            Validation.Assert(Util.Size(_stack) == 0);
            Validation.Assert(!_done.Contains(guid));
            Validation.Assert(_guidToNode.ContainsKey(guid));

            for (; ; )
            {
                Validation.Assert(_ivDst < _bldrTopo.Count);
                Validation.Assert(!_done.Contains(guid));
                _guidToNode.TryGetValue(guid, out var node).Verify();

                // If there is a main formula, it determines the base type. Otherwise the base type comes from
                // the graph host, using the config.
                // REVIEW: Should the host be given more than just the config? Ideally, anything that
                // affects the base type is directly part of the node, but, in theory, that could include
                // information outside of the "config" mechanism. On the other hand, it should be easy to
                // include such information in the config. Moreover, if we pass the node, then 
                DType typeBase;
                if (node.Formula != null)
                    typeBase = default;
                else if (!_host.TryGetBaseType(node, out typeBase) || !typeBase.IsValid)
                    typeBase = DType.Vac;

                GuidSet need;
                if (_guidToGn.TryGetValue(guid, out var gn) && IsPrevGood(gn, node, typeBase))
                {
                    // All upstream nodes are done, their types are the same and this node
                    // hasn't changed, so mark it done.
                    _done = _done.Add(guid);
                }
                else
                {
                    if (!TryBind(node, gn,
                        ref typeBase, out var fmas, out var bfmaMain, out var bfmas,
                        out var usesBase, out var uses, out need))
                    {
                        // Process "need" before the current node.
                        // REVIEW: This will thrash the order when there is a cycle.
                        Validation.Assert(!need.IsEmpty);
                        Validation.Assert(!_done.Intersects(need));
                        goto LProcessNext;
                    }

                    var gnNew = GraphNodeImpl.Create(node, typeBase, fmas, bfmaMain, bfmas,
                        usesBase, uses, isDirty: !uses.IsSubset(_done));

                    _guidToGn = _guidToGn.SetItem(guid, gnNew);
                    _done = _done.Add(guid);
                    if (gn == null || gn.Type != gnNew.Type)
                        _typeChanged = _typeChanged.Add(guid);
                    if (gn == null || gn.Name != gnNew.Name)
                        _nameChanged = _nameChanged.Add(guid);
                }
                if (_bldrTopo[_ivDst] != guid)
                    _bldrTopo[_ivDst] = guid;
                _ivDst++;

                if (_stack == null || _stack.Count == 0)
                    break;
                (guid, need) = _stack.Pop();

            LProcessNext:
                while (!need.IsEmpty)
                {
                    var next = need.First;
                    need = need.Remove(next);
                    if (!_done.Contains(next))
                    {
                        Util.Add(ref _stack, (guid, need));
                        guid = next;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Return true if the previous graph node is still good.
        /// </summary>
        private bool IsPrevGood(GraphNodeImpl gn, TNode node, DType typeBase)
        {
            Validation.AssertValue(gn);
            Validation.AssertValue(node);
            Validation.Assert(gn.Guid == node.Guid);

            // If the gn had forward dependencies its binding "guessed" some types,
            // so can't be trusted.
            if (gn.IsDirty)
                return false;

            // If the node changed at all, rebind.
            if (gn._node != node)
                return false;

            // If the node has no formula and the base type changed, rebind.
            if (node.Formula == null && typeBase != gn.BaseType)
                return false;

            // If this node now has forward dependencies, rebind.
            if (!gn.Uses.IsSubset(_done))
                return false;

            // If any of the dependencies had their types change or name change, rebind.
            if (gn.Uses.Intersects(_typeChanged))
                return false;
            if (gn.Uses.Intersects(_nameChanged))
                return false;

            // If there are any name changes and this has name misses, rebind.
            if (_hasNameChanges && gn._hasNameMisses)
                return false;

            if (_funcsChanged)
            {
                if (BmfaFuncsChanged(gn.MainBoundFormula))
                    return false;
                foreach (var (bfma, grp) in gn.ExtraBoundFormulas)
                {
                    if (BmfaFuncsChanged(bfma))
                        return false;
                }
            }

            // All looks good so re-use it,
            return true;
        }

        private bool BmfaFuncsChanged(BoundFormula bfma)
        {
            if (bfma == null || !(bfma.HostInfo is ReferenceBindInfo rbi))
                return false;
            if (rbi.FuncPathsTested.Count == 0)
                return false;

            // Look through all the non-fuzzy lookups first.
            bool haveFuzzy = false;
            foreach (var (key, oiPrev) in rbi.FuncPathsTested)
            {
                var (name, extra) = key;
                int arity = (int)Util.GetLo(extra);
                var flags = (NameArityToOperOpt.ExtraFlags)Util.GetHi(extra);

                bool user = false;
                if (flags != 0)
                {
                    // This is some combination of { user, fuzzy, proc }. We should also have the normal version and it
                    // should be missing or have the wrong arity.
                    Validation.Assert(rbi.FuncPathsTested.TryGetValue((name, Util.MakeULong(0, arity)), out var oiTmp) &&
                        (oiTmp == null || !oiTmp.Oper.SupportsArity(arity)));

                    // Ignore proc searches, since these always fail.
                    if ((flags & NameArityToOperOpt.ExtraFlags.Proc) != 0)
                    {
                        Validation.Assert(oiPrev == null);
                        continue;
                    }
                    if ((flags & NameArityToOperOpt.ExtraFlags.Fuzzy) != 0)
                    {
                        haveFuzzy = true;
                        continue;
                    }
                    user = (flags & NameArityToOperOpt.ExtraFlags.User) != 0;
                }

                if (!_host.TryGetOperInfo(name, user, fuzzy: false, arity, out var oi))
                    oi = null;
                if (oi != oiPrev)
                    return true;
            }

            if (!haveFuzzy)
                return false;

            // Look through the fuzzy lookups now.
            foreach (var (key, infoPrev) in rbi.FuncPathsTested)
            {
                var (name, extra) = key;
                var flags = (NameArityToOperOpt.ExtraFlags)Util.GetHi(extra);

                if ((flags & NameArityToOperOpt.ExtraFlags.Fuzzy) == 0)
                    continue;
                if ((flags & NameArityToOperOpt.ExtraFlags.Proc) != 0)
                    continue;
                bool user = (flags & NameArityToOperOpt.ExtraFlags.User) != 0;

                int arity = (int)Util.GetLo(extra);
                if (!_host.TryGetOperInfo(name, user, fuzzy: true, arity, out var oi))
                    oi = null;
                if (oi != infoPrev)
                    return true;
            }

            // None of the function lookups changed, so we're good.
            return false;
        }

        private GraphImpl MakeGraph()
        {
            Validation.Assert(_ivDst == _bldrTopo.Count);
            Validation.Assert(_ivDst == _nameToGuid.Count);
            Validation.Assert(_ivDst == _guidToGn.Count);

            return new GraphImpl(_bldrTopo.ToImmutable(), _nameToGuid, _guidToGn, _nameToNs, _guidToNs);
        }

        /// <summary>
        /// Rebind this node. Note that this doesn't worry with trying to do minimal re-binding.
        /// It just re-binds all the formulas in the node.
        /// </summary>
        private bool TryBind(TNode node, GraphNodeImpl gnPrev,
            ref DType typeBase, out FmaTuple fmas, out BoundFormula bfmaMain, out BfmaTuple bfmas,
            out GuidSet usesBase, out GuidSet uses, out GuidSet need)
        {
            Validation.AssertValue(node);
            Validation.AssertValueOrNull(gnPrev);

            var nbs = new NodeBindState(this, node.Guid, node.Name.Parent);
            if (node.Formula == null)
            {
                Validation.Assert(typeBase.IsValid);
                bfmaMain = null;
            }
            else
            {
                Validation.Assert(!typeBase.IsValid);
                var fma = node.Formula;
                var bfma = BoundFormula.Create(fma, nbs, _host.GetBindOptions());
                typeBase = bfma.BoundTree.Type;
                bfmaMain = bfma;
            }
            Validation.Assert(typeBase.IsValid);

            var typeThis = typeBase;
            usesBase = nbs.Uses;

            fmas = _host.GetExtraFormulas(node, typeBase, gnPrev);

            int cex = fmas.Length;
            if (cex > 0)
            {
                var bldr = BfmaTuple.CreateBuilder(cex, init: true);
                for (int i = 0; i < cex; i++)
                {
                    var (fma, grp) = fmas[i];
                    var bfma = BoundFormula.Create(fma, nbs, _host.GetBindOptions(), typeThis);
                    bldr[i] = (bfma, grp);
                    typeThis = bfma.BoundTree.Type;
                }
                bfmas = bldr.ToImmutable();
            }
            else
                bfmas = BfmaTuple.Empty;

            uses = nbs.Uses;
            need = nbs.Need;

            return need.IsEmpty;
        }

        /// <summary>
        /// Used when binding the formulas associated with a node. It collects information on
        /// bindings.
        /// </summary>
        private sealed class NodeBindState : ReferenceBindHost
        {
            private readonly Analyzer _anz;
            private readonly Guid _guidCur;

            /// <summary>
            /// Tracks the nodes referenced.
            /// </summary>
            public GuidSet Uses;

            /// <summary>
            /// Trackes the nodes referenced that are not yet "done" according to <see cref="Analyzer._done"/>.
            /// </summary>
            public GuidSet Need;

            public NodeBindState(Analyzer anz, Guid guidCur, NPath nsCur)
                : base(nsCur, NPath.Root)
            {
                Validation.AssertValue(anz);
                _anz = anz;
                _guidCur = guidCur;
            }

            public override bool IsFuzzyMatch(string a, string b)
            {
                return _anz._host.IsFuzzyMatch(a, b);
            }

            protected override bool TryFindNamespaceItemCore(NPath ns, DName name, out NPath path, out DType type, out bool isStream)
            {
                Validation.Assert(name.IsValid);

                isStream = false;
                path = ns.Append(name);

                if (TryBindToGlobal(path, guess: false, out type))
                {
                    Validation.Assert(type.IsValid);
                    return true;
                }

                if (_anz._nameToNs.ContainsKey(path))
                {
                    type = default;
                    return true;
                }

                path = default;
                type = default;
                return false;
            }

            public override bool TryFindNamespaceItemFuzzy(ExprNode ctx, NPath ns, DName name, out DName nameGuess, out DType type, out bool isStream)
            {
                isStream = false;

                if (_anz._host.TryGetGlobalFuzzyMatch(ns, name, out nameGuess) && TryBindToGlobal(ns.Append(nameGuess), guess: true, out type))
                {
                    Validation.Assert(type.IsValid);
                    return true;
                }

                type = default;
                if (_anz._host.TryGetNamespaceFuzzyMatch(ns, name, out nameGuess) && _anz._nameToNs.ContainsKey(ns.Append(nameGuess)))
                    return true;

                nameGuess = default;
                return false;
            }

            private bool TryBindToGlobal(NPath name, bool guess, out DType type)
            {
                Validation.Assert(!name.IsRoot);

                if (!_anz._nameToGuid.TryGetValue(name, out var guid))
                {
                    type = default;
                    return false;
                }

                if (!guess)
                    Uses = Uses.Add(guid);

                GraphNodeImpl gn;
                if (_anz._done.Contains(guid))
                {
                    _anz._guidToGn.TryGetValue(guid, out gn).Verify();
                    type = gn.Type;
                    Validation.Assert(type.IsValid);
                    return true;
                }

                // If we're using this node as a guess, don't change the bind order or anything else.
                // Otherwise, we risk breaking invariants among various guid sets.
                if (!guess)
                {
                    // Declare that we need this guid if it doesn't cause a cycle.
                    if (guid != _guidCur && (_anz._stack == null || !_anz._stack.Any(pair => pair.guid == guid)))
                        Need = Need.Add(guid);
                }

                // Even though guid hasn't been done yet, it will be in the tree if it isn't new, so use
                // its previous type as the guess type.
                if (guid != _guidCur && _anz._guidToGn.TryGetValue(guid, out gn))
                    type = gn.Type;
                else
                    type = DType.Vac;
                return true;
            }

            public override bool IsNamespace(NPath name)
            {
                return _anz._nameToNs.ContainsKey(name);
            }

            protected override bool TryGetOperInfoCore(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
            {
                return _anz._host.TryGetOperInfo(name, user, fuzzy, arity, out info);
            }
        }
    }
}
