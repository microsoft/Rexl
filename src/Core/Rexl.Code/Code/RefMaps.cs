// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// The static <see cref="Run(BoundNode)"/> method does a pass over a <see cref="BoundNode"/> and
/// constructs an instance of <see cref="RefMaps"/>, which contains information needed for code generation.
/// This does <i>not</i> assume that the nodes form a tree (with no sharing). That is, it supports cases where
/// the same <see cref="BoundNode"/> instance appears multiple times in the DAG.
/// 
/// The <see cref="RefMaps"/> instance includes the following:
/// <list type="bullet">
/// <item>A list of all nodes in traversal order, with a parent preceeding its children.</item>
/// <item>A list of <see cref="ScopeInfo"/>s, one for each scope introduction, in traversal order.
///     The zero'th instance is a blank one, that serves as a sentinel when walking the "outer" chain via
///     <see cref="ScopeInfo.Outer"/>. The information for a scope includes its <see cref="ArgScope"/>
///     instance, its index in this list, the next outer scope, its owner, its owner's node index, its slot
///     number, its depth, etc. Note that an <see cref="ArgScope"/> instance may have multiple owners
///     as long as those owners don't overlap (neither contains the other).</item>
/// <item>A mapping from scope owner and <see cref="ArgScope"/> instance to <see cref="ScopeInfo"/>.</item>
/// <item>A list of <see cref="NestedArg"/>s, one for each arg/value that is nested in one or more scopes
///     owned by the value's parent. For example, each nested function argument, or each nested value
///     of <see cref="BndGroupByNode"/> or <see cref="BndSetFieldsNode"/>.
///     The zeroth instance is a blank one, that serves as a sentinel when walking the "outer" chain via
///     <see cref="NestedArg.Outer"/>. The information for each nested arg includes the closest
///     <see cref="ScopeInfo"/> that it is nested in, its owner, its owner's node index, its slot number,
///     its depth, whether it needs to be implemented as a delegate, etc.</item>
/// <item>A list of all global references, in traversal order. Note that <c>this</c> is considered a global
///     whose name is the root <see cref="NPath"/> value.</item>
/// <item>A list of the globals that are referenced. This is essentially the global references grouped by
///     the name of the global.</item>
/// <item>A mapping from the node index of a global reference to the containing <see cref="NestedArg"/>.</item>
/// <item>A list of uses of execution context. The uses are instances of <see cref="BndScopeOwnerNode"/>.</item>
/// <item>A list of scope references.</item>
/// <item>A mapping from nested arg to the range of global references that it contains.</item>
/// <item>A mapping from nested arg to the range of execution context references that it contains.</item>
/// <item>A mapping from nested arg to the range of scope references that it contains.</item>
/// </list>
/// </summary>
internal sealed partial class RefMaps
{
    /// <summary>
    /// A scope with its ownership and context information.
    /// </summary>
    public sealed class ScopeInfo
    {
        /// <summary>
        /// The index in the list of <see cref="ScopeInfo"/>s.
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// The next outer scope info.
        /// </summary>
        public readonly ScopeInfo Outer;

        /// <summary>
        /// The actual scope in the bound tree.
        /// </summary>
        public readonly ArgScope Scope;

        /// <summary>
        /// The owning bound node.
        /// </summary>
        public readonly BndScopeOwnerNode Owner;

        /// <summary>
        /// The traversal index of the owning bound node.
        /// </summary>
        public readonly int OwnerIdx;

        /// <summary>
        /// The slot for this scope within its owner. When <see cref="Owner"/> is a <see cref="BndCallNode"/>,
        /// this is the arg index. For others (<see cref="BndGroupByNode"/> or <see cref="BndSetFieldsNode"/>),
        /// it is negative.
        /// </summary>
        public readonly int Slot;

        /// <summary>
        /// Whether this is an "index" scope.
        /// </summary>
        public bool IsIndex => Scope != null && Scope.IsIndex;

        /// <summary>
        /// The depth in the "Outer" chain.
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Constructor for the top "blank" scope.
        /// </summary>
        public ScopeInfo()
        {
            OwnerIdx = -1;
        }

        public ScopeInfo(int index, ScopeInfo outer, ArgScope scope, BndScopeOwnerNode owner, int idx, int slot)
        {
            Validation.Assert(index > 0);
            Validation.AssertValue(outer);
            Validation.AssertValue(scope);
            Validation.AssertValue(owner);

            Index = index;
            Outer = outer;
            Scope = scope;
            Owner = owner;
            OwnerIdx = idx;
            Slot = slot;
            Depth = Outer.Depth + 1;
        }

        /// <summary>
        /// Returns whether this scope info encompasses (is in the outer chain for or equal to)
        /// <paramref name="other"/>.
        /// </summary>
        public bool Encompasses(ScopeInfo other)
        {
            if (other == null)
                return false;
            if (other.Depth < Depth)
                return false;
            while (other.Depth > Depth)
            {
                Validation.AssertValue(other.Outer);
                other = other.Outer;
            }
            if (other != this)
                return false;
            return true;
        }
    }

    /// <summary>
    /// Information about a nested argument.
    /// </summary>
    public sealed class NestedArg
    {
        /// <summary>
        /// The index in the list of <see cref="NestedArg"/>s.
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// The next outer nested arg.
        /// </summary>
        public readonly NestedArg Outer;

        /// <summary>
        /// The inner-most scope that this arg is nested in.
        /// </summary>
        public readonly ScopeInfo Scope;

        /// <summary>
        /// The owning bound node.
        /// </summary>
        public readonly BndScopeOwnerNode Owner;

        /// <summary>
        /// The traversal index of the owning bound node.
        /// </summary>
        public readonly int OwnerIdx;

        /// <summary>
        /// The slot of this arg within the owner.
        /// </summary>
        public readonly int Slot;

        /// <summary>
        /// The depth of this arg within the nested arg chain.
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Whether this arg needs a delegate with its scopes as parameters.
        /// This is required when the arg is nested in a loop scope.
        /// </summary>
        public readonly bool NeedsDelegate;

        /// <summary>
        /// Constructor for the top "blank" nested arg.
        /// REVIEW: Is this needed?
        /// </summary>
        public NestedArg()
        {
            OwnerIdx = -1;
        }

        public NestedArg(int index, NestedArg outer, ScopeInfo scope,
            BndScopeOwnerNode owner, int idx, int slot, bool needsDelegate)
        {
            Validation.Assert(index > 0);
            Validation.AssertValue(outer);
            Validation.AssertValue(scope);
            Validation.AssertValue(owner);

            Index = index;
            Outer = outer;
            Scope = scope;
            Owner = owner;
            OwnerIdx = idx;
            Slot = slot;
            Depth = Outer.Depth + 1;
            NeedsDelegate = needsDelegate;
        }
    }

    /// <summary>
    /// The number of nodes.
    /// </summary>
    public int NodeCount => _nodes.Length;

    /// <summary>
    /// Get a node from its index.
    /// </summary>
    public BoundNode GetNode(int idx)
    {
        Validation.AssertIndex(idx, _nodes.Length);
        return _nodes[idx].VerifyValue();
    }

    /// <summary>
    /// The total number of <see cref="ScopeInfo"/> instances, including the initial "blank".
    /// </summary>
    public int ScopeInfoCount => _scopeInfos.Count;

    /// <summary>
    /// The top "blank" scope info.
    /// </summary>
    public ScopeInfo TopScopeInfo => _scopeInfos[0];

    /// <summary>
    /// Get a scope info from its index.
    /// </summary>
    public ScopeInfo GetScopeInfo(int iscp)
    {
        Validation.AssertIndex(iscp, _scopeInfos.Count);
        Validation.Assert(iscp > 0);
        return _scopeInfos[iscp];
    }

    /// <summary>
    /// Maps from owner, owner node index and <see cref="ArgScope"/> to <see cref="ScopeInfo"/>. For convenience,
    /// this supports the <paramref name="scope"/> being <c>null</c>, returning <c>null</c> in that case.
    /// </summary>
    public ScopeInfo GetScopeInfoFromOwner(BndScopeOwnerNode owner, int idx, ArgScope scope)
    {
        AssertIdx(owner, idx);

        if (scope == null)
            return null;

        Validation.BugCheck(_scopeInfoMap.TryGetValue((idx, scope), out var info));
        Validation.Assert(info.Owner == owner);
        Validation.Assert(info.OwnerIdx == idx);
        Validation.Assert(info.Scope == scope);
        return info;
    }

    /// <summary>
    /// The total number of nested args, including the initial "blank".
    /// </summary>
    public int NestedArgCount => Util.Size(_nestedArgs);

    /// <summary>
    /// The top "blank" nested arg.
    /// </summary>
    public NestedArg TopNestedArg => _nestedArgs[0];

    /// <summary>
    /// Get a nested arg from its index.
    /// </summary>
    public NestedArg GetNestedArg(int ina)
    {
        Validation.AssertIndex(ina, _nestedArgs.Count);
        Validation.Assert(ina > 0);
        return _nestedArgs[ina];
    }

    /// <summary>
    /// The total number of global references.
    /// </summary>
    public int GlobalRefCount => Util.Size(_globalRefs);

    /// <summary>
    /// Get a global reference from its index. Note that <paramref name="igr"/> is an index into
    /// the global references and NOT a node index. This returns the node and node index.
    /// </summary>
    public (BndGlobalBaseNode gref, int idx) GetGlobalRef(int igr)
    {
        Validation.AssertIndex(igr, Util.Size(_globalRefs));
        return _globalRefs[igr];
    }

    /// <summary>
    /// Get the nested arg that contains the indicated global reference. The <paramref name="idx"/> should
    /// be a node index of a <see cref="BndGlobalBaseNode"/>.
    /// </summary>
    public NestedArg GetGlobalRefCtx(int idx)
    {
        Validation.Assert(GetNode(idx) is BndGlobalBaseNode);
        Validation.Assert(_globalRefToCtx != null);
        Validation.Assert(_globalRefToCtx.ContainsKey(idx));
        return _globalRefToCtx[idx];
    }

    /// <summary>
    /// The number of referenced globals. Note that `this` counts as a global.
    /// </summary>
    public int GlobalCount => Util.Size(_globalNameToRefs);

    /// <summary>
    /// Get information about all the global references, grouped by global name.
    /// </summary>
    public IEnumerable<(NPath name, DType type, IEnumerable<int> refs)> GetGlobalInfos()
    {
        if (_globalNameToRefs == null)
            yield break;

        // REVIEW: Should probably sort these to get consistent order. Perhaps use a red-black tree?
        foreach (var kvp in _globalNameToRefs)
            yield return (kvp.Key, kvp.Value.type, kvp.Value.items);
    }

    /// <summary>
    /// The number of references to execution context.
    /// </summary>
    public int ExecRefCount => Util.Size(_execRefs);

    /// <summary>
    /// The total number of scope references.
    /// </summary>
    public int ScopeRefCount => Util.Size(_scopeRefs);

    /// <summary>
    /// Get the <see cref="ScopeInfo"/> for the indicated scope reference. Note that <paramref name="isr"/>
    /// is an index into the scope references and NOT a node index.
    /// </summary>
    public ScopeInfo GetScopeInfoFromRef(int isr)
    {
        Validation.AssertIndex(isr, Util.Size(_scopeRefs));
        int idx = _scopeRefs[isr];
        Validation.Assert(_nodes[idx] is BndScopeRefNode);
        Validation.Assert(_scopeRefToInfo.ContainsKey(idx));
        int iscp = _scopeRefToInfo[idx];
        return _scopeInfos[iscp];
    }

    /// <summary>
    /// Get the <see cref="ScopeInfo"/> for the indicated scope reference node index.
    /// </summary>
    public ScopeInfo GetScopeInfoFromRefNode(int idx)
    {
        Validation.AssertIndex(idx, _nodes.Length);
        Validation.Assert(_nodes[idx] is BndScopeRefNode);
        Validation.Assert(_scopeRefToInfo.ContainsKey(idx));
        int iscp = _scopeRefToInfo[idx];
        return _scopeInfos[iscp];
    }

    /// <summary>
    /// Determine whether the given range of NestedArg indices uses globals and if so sets
    /// the min and lim indices of the global refs.
    /// </summary>
    public bool UsesGlobals(int inaMin, int inaLim, out int igrMin, out int igrLim)
    {
        Validation.Assert(0 < inaMin & inaMin < inaLim & inaLim <= NestedArgCount);

        (igrMin, int lim0) = _nestedArgToGlobalRefs[inaMin];
        Validation.Assert(0 <= igrMin & igrMin <= lim0 & lim0 <= GlobalRefCount);
        (int min1, igrLim) = _nestedArgToGlobalRefs[inaLim - 1];
        Validation.Assert(0 <= min1 & min1 <= igrLim & igrLim <= GlobalRefCount);
        Validation.Assert(inaLim == inaMin + 1 || lim0 <= min1);

        if (igrMin >= igrLim)
            return false;
        return true;
    }

    /// <summary>
    /// Determine whether the given range of <see cref="NestedArg"/> indices uses the exec ctx.
    /// </summary>
    public bool UsesExecCtx(int inaMin, int inaLim)
    {
        Validation.Assert(0 < inaMin & inaMin < inaLim & inaLim <= NestedArgCount);

        (int min, int lim0) = _nestedArgToExecRefs[inaMin];
        Validation.Assert(0 <= min & min <= lim0 & lim0 <= ExecRefCount);
        (int min1, int lim) = _nestedArgToExecRefs[inaLim - 1];
        Validation.Assert(0 <= min1 & min1 <= lim & lim <= ExecRefCount);
        Validation.Assert(inaLim == inaMin + 1 || lim0 <= min1);

        if (min >= lim)
            return false;

        return true;
    }

    /// <summary>
    /// Builds the set of scopes outside of <paramref name="scopeBase"/> for the given range of nested arg
    /// indices. The set is represented as a dictionary from scope info index to <see cref="ScopeInfo"/>.
    /// Returns null if there aren't any.
    /// </summary>
    public Dictionary<int, RefMaps.ScopeInfo> FindExtScopes(RefMaps.ScopeInfo scopeBase, int inaMin, int inaLim)
    {
        Validation.Assert(0 < inaMin & inaMin < inaLim & inaLim <= NestedArgCount);

        (int min, int lim0) = _nestedArgToScopeRefs[inaMin];
        Validation.Assert(0 <= min & min <= lim0 & lim0 <= ScopeRefCount);
        (int min1, int lim) = _nestedArgToScopeRefs[inaLim - 1];
        Validation.Assert(0 <= min1 & min1 <= lim & lim <= ScopeRefCount);
        Validation.Assert(inaLim == inaMin + 1 || lim0 <= min1);

        Dictionary<int, RefMaps.ScopeInfo> extScopes = null;
        for (int i = min; i < lim; i++)
        {
            var info = GetScopeInfoFromRef(i);

            // One should be nested inside the other. Just need to figure out which way.
            if (scopeBase.Scope != null && info.Depth <= scopeBase.Depth)
            {
                Validation.BugCheck(info.Encompasses(scopeBase));
                // It's an external scope that we need from the caller.
                extScopes ??= new();
                if (extScopes.TryGetValue(info.Index, out var tmp))
                    Validation.Assert(tmp == info);
                else
                    extScopes.Add(info.Index, info);
            }
            else
                Validation.BugCheck(scopeBase.Encompasses(info));
        }

        Validation.Assert(extScopes == null || extScopes.Count > 0);
        return extScopes;
    }

    /// <summary>
    /// Create an instance of <see cref="RefMaps"/> for the given node. If the node is invalid,
    /// for example, contains improper scope references or introductions, this will throw.
    /// </summary>
    public static RefMaps Run(BoundNode bnd, CodeGeneratorBase codeGen)
    {
        Validation.AssertValue(bnd);
        Validation.AssertValue(codeGen);
        return Visitor.Run(bnd, codeGen);
    }

    /// <summary>
    /// Assert that the given node is associated with the given index.
    /// </summary>
    [Conditional("DEBUG")]
    public void AssertIdx(BoundNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        Validation.AssertIndex(idx, _nodes.Length);
        Validation.Assert(_nodes[idx] == bnd);
    }

    /// <summary>
    /// Assert validity of the nested arg instance.
    /// </summary>
    [Conditional("DEBUG")]
    public void AssertNestedArg(NestedArg nested, BoundNode owner, int slot)
    {
        Validation.AssertValue(nested);
        Validation.AssertIndex(nested.Index, _nestedArgs.Count);
        Validation.Assert(nested == _nestedArgs[nested.Index]);
        Validation.Assert(nested.Owner == owner);
        Validation.Assert(nested.Slot == slot);
    }

    /// <summary>
    /// Returns the list of nested arg ranges for the given scope owner.
    /// </summary>
    public List<(int inaMin, int inaLim)> GetNestedArgList(BndScopeOwnerNode owner, int idx)
    {
        AssertIdx(owner, idx);
        Validation.Assert(_ownerToNestedArgRanges.ContainsKey(idx));
        return _ownerToNestedArgRanges[idx];
    }

    /// <summary>
    /// Returns the <see cref="NestedArg"/> for the given slot of the given owner, returning
    /// <c>null</c> if there isn't one.
    /// </summary>
    public NestedArg GetNestedArg(BndScopeOwnerNode owner, int idx, int slot)
    {
        AssertIdx(owner, idx);

        var nas = _ownerToNestedArgRanges[idx];

        // The number of nested args may be very large, eg, with SetFields, so use binary search.
        int iiMin = 0;
        int iiLim = nas.Count;
        while (iiMin < iiLim)
        {
            int iiMid = (int)((uint)(iiMin + iiLim) >> 1);
            var (inaMin, inaLim) = nas[iiMid];
            Validation.Assert(0 <= inaMin && inaMin < inaLim && inaLim <= _nestedArgs.Count);
            var nested = _nestedArgs[inaMin];
            Validation.Assert(nested.Owner == owner);
            Validation.Assert(nested.OwnerIdx == idx);
            if (slot == nested.Slot)
                return nested;
            if (slot < nested.Slot)
                iiLim = iiMid;
            else
                iiMin = iiMid + 1;
        }

        Validation.Assert(false);
        return null;
    }
}

// This partial contains the private API.
partial class RefMaps
{
    /// <summary>
    /// The nodes in traversal (pre) order. Note that a node may appear more than once. That is,
    /// we don't assume a tree, but allow a DAG.
    /// </summary>
    private readonly BoundNode[] _nodes;

    /// <summary>
    /// All <see cref="ScopeInfo"/> instances in traversal order, including a top level "blank" one.
    /// </summary>
    private readonly List<ScopeInfo> _scopeInfos;

    /// <summary>
    /// Maps from node index of parent and <see cref="ArgScope"/> to corresponding <see cref="ScopeInfo"/>.
    /// </summary>
    private Dictionary<(int idx, ArgScope scope), ScopeInfo> _scopeInfoMap;

    /// <summary>
    /// Maps from traversal index of a <see cref="BndScopeOwnerNode"/> to the nested arg index range
    /// for each nested arg that it owns.
    /// </summary>
    private Dictionary<int, List<(int inaMin, int inaLim)>> _ownerToNestedArgRanges;

    /// <summary>
    /// All nested args in traversal order, including a top level "blank" one.
    /// </summary>
    private readonly List<NestedArg> _nestedArgs;

    /// <summary>
    /// List of all the global refs, in traversal order.
    /// </summary>
    private List<(BndGlobalBaseNode gref, int idx)> _globalRefs;

    /// <summary>
    /// Map from global name to the global type and list of references (node indices) to that global.
    /// Note that references to `this` are also included with root path.
    /// </summary>
    private Dictionary<NPath, (DType type, List<int> items)> _globalNameToRefs;

    /// <summary>
    /// Map from global reference index to the containing nested arg.
    /// </summary>
    private Dictionary<int, NestedArg> _globalRefToCtx;

    /// <summary>
    /// List of node indices that require the execution context, in traversal order. Currently, the only nodes
    /// that can reference the exec ctx are Call and GroupBy.
    /// </summary>
    private List<int> _execRefs;

    /// <summary>
    /// List of all the scope reference node indices, in traversal order.
    /// The nodes are of type <see cref="BndScopeRefNode"/>.
    /// </summary>
    private List<int> _scopeRefs;

    /// <summary>
    /// Maps from node index of a scope reference to the <see cref="ScopeInfo"/> index.
    /// </summary>
    private Dictionary<int, int> _scopeRefToInfo;

    /// <summary>
    /// Maps from nested arg index to the min and lim indices into <see cref="_globalRefs"/> of the global
    /// references that are contained by the nested arg. Since <see cref="_globalRefs"/> is in traversal order,
    /// the nested arg contains this entire range.
    /// </summary>
    private Dictionary<int, (int min, int lim)> _nestedArgToGlobalRefs;

    /// <summary>
    /// Maps from nested arg index to the min and lim indices into <see cref="_execRefs"/> of the exec ctx
    /// references that are contained by the nested arg. Since <see cref="_execRefs"/> is in traversal order,
    /// the nested arg contains this entire range.
    /// </summary>
    private Dictionary<int, (int min, int lim)> _nestedArgToExecRefs;

    /// <summary>
    /// Maps from nested arg index to the min and lim indices into <see cref="_scopeRefs"/> of the scope
    /// references that are contained by the nested arg. Since <see cref="_scopeRefs"/> is in traversal order,
    /// the nested arg contains this entire range.
    /// </summary>
    private Dictionary<int, (int, int)> _nestedArgToScopeRefs;

    private RefMaps(int nodeCount)
    {
        Validation.Assert(nodeCount > 0);
        _nodes = new BoundNode[nodeCount];
        _scopeInfos = new List<ScopeInfo>();
        _scopeInfos.Add(new ScopeInfo());
        _nestedArgs = new List<NestedArg>();
        _nestedArgs.Add(new NestedArg());
    }
}

// This partial contains the visitor for construction of the RefMaps instance.
partial class RefMaps
{
    private sealed class Visitor : NoopScopedBoundTreeVisitor
    {
        private readonly CodeGeneratorBase _codeGen;
        private readonly RefMaps _refs;

        // The current scope info.
        private ScopeInfo _scopeCur;

        // The current nested arg context.
        private NestedArg _nestedCur;

        private Visitor(BoundNode root, CodeGeneratorBase codeGen)
            : base()
        {
            Validation.AssertValue(root);
            Validation.AssertValue(codeGen);

            _codeGen = codeGen;
            _refs = new RefMaps(root.NodeCount);
            _scopeCur = _refs._scopeInfos[0];
            _nestedCur = _refs._nestedArgs[0];
        }

        public static RefMaps Run(BoundNode root, CodeGeneratorBase codeGen)
        {
            Validation.AssertValue(root);
            Validation.AssertValue(codeGen);

            var vis = new Visitor(root, codeGen);
            vis.Go(root);
            return vis._refs;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            Validation.AssertValue(bnd);
            Validation.AssertIndex(idx, _refs._nodes.Length);
            Validation.Assert(_refs._nodes[idx] == null);
            _refs._nodes[idx] = bnd;
            return base.Enter(bnd, idx);
        }

        protected override void Leave(BoundNode bnd, int idx)
        {
            _refs.AssertIdx(bnd, idx);
            base.Leave(bnd, idx);
        }

        protected override void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            Validation.AssertValue(_scopeCur);
            Validation.Assert(_scopeCur.Depth == ScopeDepth);

            base.PushScope(scope, owner, idx, slot, isArgValid);

            Validation.AssertValue(scope);
            Validation.Assert(!Util.ContainsKey(_refs._scopeInfoMap, (idx, scope)));
            Validation.AssertValue(owner);

            for (var s = _scopeCur; s.Scope != null; s = s.Outer)
            {
                // An ArgScope cannot be re-used in a nested context, but can be in disjoint contexts.
                Validation.BugCheck(s.Scope != scope, "Nested ArgScope usage");
            }

            var scopeCur = new ScopeInfo(_refs._scopeInfos.Count, _scopeCur, scope, owner, idx, slot);
            _refs._scopeInfos.Add(scopeCur);
            Util.Add(ref _refs._scopeInfoMap, (idx, scope), scopeCur);

            _scopeCur = scopeCur;

            Validation.Assert(_scopeCur.Depth == ScopeDepth);
        }

        protected override void PopScope(ArgScope scope)
        {
            Validation.AssertValue(_scopeCur);
            Validation.AssertValue(_scopeCur.Outer);
            Validation.Assert(_scopeCur.Depth == ScopeDepth);
            Validation.AssertValue(scope);
            Validation.Assert(_scopeCur.Scope == scope);

            _scopeCur = _scopeCur.Outer;

            base.PopScope(scope);

            Validation.Assert(_scopeCur.Depth == ScopeDepth);
        }

        protected override void PushNestedArg(BndScopeOwnerNode owner, int idx, int slot, bool needsDelegate)
        {
            Validation.AssertValue(_nestedCur);
            Validation.Assert(_nestedCur.Depth == NestedArgDepth);

            base.PushNestedArg(owner, idx, slot, needsDelegate);

            Validation.AssertValue(owner);

            var na = new NestedArg(_refs._nestedArgs.Count, _nestedCur, _scopeCur,
                owner, idx, slot, needsDelegate);
            _refs._nestedArgs.Add(na);

            _refs._ownerToNestedArgRanges ??= new();
            if (!_refs._ownerToNestedArgRanges.TryGetValue(idx, out var list))
                _refs._ownerToNestedArgRanges[idx] = list = new List<(int inaMin, int inaLim)>();
            list.Add((na.Index, -1));
            Util.Add(ref _refs._nestedArgToGlobalRefs, na.Index, (Util.Size(_refs._globalRefs), -1));
            Util.Add(ref _refs._nestedArgToExecRefs, na.Index, (Util.Size(_refs._execRefs), -1));
            Util.Add(ref _refs._nestedArgToScopeRefs, na.Index, (Util.Size(_refs._scopeRefs), -1));

            _nestedCur = na;

            Validation.Assert(_nestedCur.Depth == NestedArgDepth);
        }

        protected override void PopNestedArg()
        {
            Validation.AssertValue(_nestedCur);
            Validation.AssertValue(_nestedCur.Outer);
            Validation.Assert(_nestedCur.Depth == NestedArgDepth);

            var nested = _nestedCur;

            var list = _refs.GetNestedArgList(nested.Owner, nested.OwnerIdx);
            Validation.Assert(list.Count > 0);

            (int min, int lim) = list[list.Count - 1];
            Validation.Assert(min == nested.Index);
            Validation.Assert(lim == -1);
            Validation.Assert(min < _refs._nestedArgs.Count && _refs._nestedArgs[min] == nested);
            list[list.Count - 1] = (min, _refs._nestedArgs.Count);

            Validation.Assert(Util.ContainsKey(_refs._nestedArgToGlobalRefs, nested.Index));
            (min, lim) = _refs._nestedArgToGlobalRefs[nested.Index];
            Validation.Assert(0 <= min & min <= Util.Size(_refs._globalRefs));
            Validation.Assert(lim == -1);
            _refs._nestedArgToGlobalRefs[nested.Index] = (min, Util.Size(_refs._globalRefs));

            Validation.Assert(Util.ContainsKey(_refs._nestedArgToExecRefs, nested.Index));
            (min, lim) = _refs._nestedArgToExecRefs[nested.Index];
            Validation.Assert(0 <= min & min <= Util.Size(_refs._execRefs));
            Validation.Assert(lim == -1);
            _refs._nestedArgToExecRefs[nested.Index] = (min, Util.Size(_refs._execRefs));

            Validation.Assert(Util.ContainsKey(_refs._nestedArgToScopeRefs, nested.Index));
            (min, lim) = _refs._nestedArgToScopeRefs[nested.Index];
            Validation.Assert(0 <= min & min <= Util.Size(_refs._scopeRefs));
            Validation.Assert(lim == -1);
            _refs._nestedArgToScopeRefs[nested.Index] = (min, Util.Size(_refs._scopeRefs));

            _nestedCur = _nestedCur.Outer;

            base.PopNestedArg();

            Validation.Assert(_nestedCur.Depth == NestedArgDepth);
        }

        protected override void VisitImpl(BndThisNode bnd, int idx)
        {
            _refs.AssertIdx(bnd, idx);
            VisitGlobal(bnd, idx);
        }

        protected override void VisitImpl(BndGlobalNode bnd, int idx)
        {
            _refs.AssertIdx(bnd, idx);
            VisitGlobal(bnd, idx);
        }

        private void VisitGlobal(BndGlobalBaseNode bnd, int idx)
        {
            _refs.AssertIdx(bnd, idx);
            var name = bnd.FullName;

            Validation.Assert(!Util.ContainsKey(_refs._globalRefToCtx, idx));
            Util.Add(ref _refs._globalRefs, (bnd, idx));
            Util.Add(ref _refs._globalRefToCtx, idx, _nestedCur);

            _refs._globalNameToRefs ??= new();
            if (!_refs._globalNameToRefs.TryGetValue(name, out var pair))
                _refs._globalNameToRefs[name] = pair = (bnd.Type, new List<int>());
            else
            {
                Validation.BugCheck(pair.type == bnd.Type, "Inconsistent global type");
                Validation.Assert(pair.items.Count > 0);
            }
            pair.items.Add(idx);
        }

        protected override bool PreVisitImpl(BndBinaryOpNode bnd, int idx)
        {
            switch (bnd.Op)
            {
            case BinaryOp.In:
            case BinaryOp.InNot:
            case BinaryOp.InCi:
            case BinaryOp.InCiNot:
                // REVIEW: Hard-coding this is less than ideal.
                Util.Add(ref _refs._execRefs, idx);
                break;
            }
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitCall(BndCallNode call, int idx)
        {
            _refs.AssertIdx(call, idx);

            if (_codeGen.NeedsExecCtx(call))
                Util.Add(ref _refs._execRefs, idx);

            return base.PreVisitCall(call, idx);
        }

        protected override bool PreVisitGroupBy(BndGroupByNode bgb, int idx)
        {
            _refs.AssertIdx(bgb, idx);

            // REVIEW: Hard-coding this is less than ideal.
            Util.Add(ref _refs._execRefs, idx);
            return base.PreVisitGroupBy(bgb, idx);
        }

        protected override void VisitImpl(BndScopeRefNode bnd, int idx)
        {
            _refs.AssertIdx(bnd, idx);

            Util.Add(ref _refs._scopeRefs, idx);

            // Find the scope in our scope stack. If it isn't there, the scope reference is bad, so
            // the whole bound node dag is bad.
            var scope = bnd.Scope;
            var info = _scopeCur;
            while (info.Scope != scope)
            {
                Validation.BugCheck(info.Outer != null, "Unresolved scope reference");
                info = info.Outer;
            }
            Util.Add(ref _refs._scopeRefToInfo, idx, info.Index);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope ps)
        {
            // Shouldn't get here since we override VisitImpl.
            Validation.Assert(false);
        }
    }
}
