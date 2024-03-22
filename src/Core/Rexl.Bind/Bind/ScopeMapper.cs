// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ScopeFieldGenMap = ReadOnly.Dictionary<DName, BoundNode>;
using ScopeFieldMap = ReadOnly.Dictionary<DName, ArgScope>;
using ScopeGenMap = ReadOnly.Dictionary<ArgScope, BoundNode>;
using ScopeMap = ReadOnly.Dictionary<ArgScope, ArgScope>;
using ScopeSink = Action<ArgScope, BndScopeRefNode, ScopeFinder.IContext>;
using ScopeSlotGenMap = ReadOnly.Dictionary<int, BoundNode>;
using ScopeSlotMap = ReadOnly.Dictionary<int, ArgScope>;
using ScopeTuple = Immutable.Array<ArgScope>;

/// <summary>
/// Rewrites a bound tree to replace <see cref="ArgScope"/> instances according
/// to a provided scope map. The scope map need not be transitive, but it must
/// not contain cycles.
/// </summary>
public static class ScopeMapper
{
    /// <summary>
    /// Replace instances of <paramref name="src"/> with <paramref name="dst"/> and
    /// return the result. Note that the return result can (and typically does)
    /// share bound nodes in common with the input <paramref name="bnd"/>.
    /// </summary>
    public static BoundNode Run(IReducerHost host, BoundNode bnd, ArgScope src, ArgScope dst)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckValue(src, nameof(src));
        Validation.BugCheckValue(dst, nameof(dst));
        Validation.BugCheckParam(src.Type == dst.Type, nameof(dst));
        return Visitor.Run(host, bnd, new Dictionary<ArgScope, ArgScope> { { src, dst } });
    }

    /// <summary>
    /// Replace instances of <paramref name="src"/> with <paramref name="dst"/> and
    /// return the result. Note that the return result can (and typically does)
    /// share bound nodes in common with the input <paramref name="bnd"/>.
    /// </summary>
    public static BoundNode Run(IReducerHost host, BoundNode bnd, ScopeTuple src, ScopeTuple dst)
    {
        Validation.BugCheckParam(!src.IsDefault, nameof(src));
        Validation.BugCheckParam(!dst.IsDefault && src.Length == dst.Length, nameof(dst));

        if (src.Length == 0)
            return bnd;

        // REVIEW: Is it reasonable to try to avoid this allocation?
        var map = new Dictionary<ArgScope, ArgScope>(src.Length);
        for (int i = 0; i < src.Length; i++)
        {
            Validation.BugCheckParam(src[i] != null, nameof(src));
            Validation.BugCheckParam(dst[i] != null, nameof(dst));
            Validation.BugCheckParam(src[i].Type == dst[i].Type, nameof(dst));
            map.Add(src[i], dst[i]);
        }
        return Visitor.Run(host, bnd, map);
    }

    /// <summary>
    /// Replace instances of keys in <paramref name="scopeMap"/> with the corresponding values
    /// and return the result. Note that the return result can (and typically does)
    /// share bound nodes in common with the input <paramref name="bnd"/>.
    /// </summary>
    public static BoundNode Run(IReducerHost host, BoundNode bnd, ScopeMap scopeMap)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckParam(
            scopeMap.All(kvp => kvp.Value != null && kvp.Value.Type == kvp.Key.Type), nameof(scopeMap));
        return Visitor.Run(host, bnd, scopeMap);
    }

    private sealed class Visitor : ReducerBase
    {
        private readonly ScopeMap _map;
        private readonly BndNodeKindMask _mask;

        /// <summary>
        /// Replace instances of keys in <paramref name="scopeMap"/> with the corresponding values
        /// and return the result. Note that the return result can (and typically does)
        /// share bound nodes in common with the input <paramref name="bnd"/>.
        /// </summary>
        public static BoundNode Run(IReducerHost host, BoundNode bnd, ScopeMap scopeMap)
        {
            if (bnd == null || scopeMap.IsDefaultOrEmpty)
                return bnd;
            var vtor = new Visitor(host, scopeMap);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(vtor.StackDepth == 1);
            return vtor.Pop();
        }

        private Visitor(IReducerHost host, ScopeMap map)
            : base(host, memoize: false)
        {
            Validation.Assert(map.Count > 0);
            _map = map;
            _mask = 0;
            foreach (var kvp in _map)
                _mask |= kvp.Key.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
            Validation.Assert(_mask != 0);
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            if (!base.Enter(bnd, idx))
                return false;

            // Skip anything that doesn't include scopes.
            if ((bnd.AllKinds & _mask) == 0)
            {
                Push(bnd, bnd);
                return false;
            }

            return true;
        }

        protected override void OnMapped(BoundNode bndOld, BoundNode bndNew)
        {
#if DEBUG
            Validation.AssertValue(bndOld);
            Validation.AssertValue(bndNew);
            Validation.Assert(bndNew != bndOld);
            Validation.Assert(bndOld.Type == bndNew.Type);
            // Note that we can't assert the old and new kinds are equal,
            // an arg scope can be replaced with an index scope. But we
            // should still be able to assert that their types are equal.
            Validation.Assert(bndOld.GetType() == bndNew.GetType());
            Validation.Assert(bndOld.HasErrors == bndNew.HasErrors);
            Validation.Assert(bndOld.ChildCount == bndNew.ChildCount);
            Validation.Assert(bndOld.IsConstant == bndNew.IsConstant);
            Validation.Assert(bndOld.IsNullValue == bndNew.IsNullValue);
#endif
            base.OnMapped(bndOld, bndNew);
        }

        private bool MapScope(ref ArgScope scope)
        {
            Validation.AssertValue(scope);
            if (!_map.TryGetValue(scope, out var dst))
                return false;

            Validation.Assert(dst.Type == scope.Type);
            scope = dst;

            // Since the scope map is not necessarily transitive, we need to keep trying.
            // Of course, this will result in an infinite loop if the scope map has cycles.
            while (_map.TryGetValue(scope, out dst))
            {
                Validation.Assert(dst.Type == scope.Type);
                scope = dst;
            }
            return true;
        }

        protected override void VisitImpl(BndScopeRefNode bnd, int idx)
        {
            var scope = bnd.Scope;
            if (MapScope(ref scope))
                Push(bnd, BndScopeRefNode.Create(scope));
            else
                VisitCore(bnd, idx);
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            // We should NOT be mapping any of the scopes owned by this call.
            Validation.Assert(!bnd.Scopes.Any(s => _map.ContainsKey(s)));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            // We should NOT be mapping the scopes owned by this node.
            Validation.Assert(!_map.ContainsKey(bnd.ScopeForKeys));
            Validation.Assert(!_map.ContainsKey(bnd.ScopeForMaps));
            Validation.Assert(!_map.ContainsKey(bnd.ScopeForAggs));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            // We should NOT be mapping the scope owned by this node.
            Validation.Assert(!_map.ContainsKey(bnd.Scope));
            return base.PreVisitImpl(bnd, idx);
        }
    }
}

/// <summary>
/// Rewrite a bound node replacing references to a scope with a given bound node.
/// </summary>
public static class ScopeReplacer
{
    /// <summary>
    /// Returns the resulting (rewritten) tree together with the number of replacements.
    /// </summary>
    public static (BoundNode res, int count) Run(IReducerHost host, BoundNode bnd, ArgScope src, BoundNode dst)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckValue(src, nameof(src));
        Validation.BugCheckValue(dst, nameof(dst));
        Validation.BugCheckParam(src.Type == dst.Type, nameof(dst));
        return Visitor.Run(host, bnd, src, dst);
    }

    private sealed class Visitor : ReducerBase
    {
        private readonly ArgScope _src;
        private readonly BndNodeKindMask _mask;
        private readonly BoundNode _dst;
        private int _count;

        /// <summary>
        /// Returns the resulting (rewritten) tree together with the number of replacements.
        /// </summary>
        public static (BoundNode res, int count) Run(IReducerHost host, BoundNode bnd, ArgScope src, BoundNode dst)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValue(src);
            Validation.AssertValue(dst);
            var vtor = new Visitor(host, src, dst);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(vtor.StackDepth == 1);
            return (vtor.Pop(), vtor._count);
        }

        private Visitor(IReducerHost host, ArgScope src, BoundNode dst)
            : base(host, memoize: false)
        {
            _mask |= src.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
            _src = src;
            _dst = dst;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            if (!base.Enter(bnd, idx))
                return false;

            // Skip anything that doesn't include scopes.
            if ((bnd.AllKinds & _mask) == 0)
            {
                Push(bnd, bnd);
                return false;
            }

            return true;
        }

        protected override void OnMapped(BoundNode bndOld, BoundNode bndNew)
        {
#if DEBUG
            Validation.AssertValue(bndOld);
            Validation.AssertValue(bndNew);
            Validation.Assert(bndNew != bndOld);
            Validation.Assert(bndOld.Type == bndNew.Type);
#endif
        }

        protected override void VisitImpl(BndScopeRefNode bnd, int idx)
        {
            if (bnd.Scope == _src)
            {
                // REVIEW: Does anyone call this with more than one use when the _dst isn't cheap?
                Validation.Assert(_count == 0 || _dst.IsCheap);
                _count++;
                Push(bnd, _dst);
            }
            else
                VisitCore(bnd, idx);
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            // We should NOT be mapping any of the scopes owned by this call.
            Validation.Assert(bnd.Scopes.All(s => s != _src));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            // We should NOT be mapping the scopes owned by this node.
            Validation.Assert(bnd.ScopeForKeys != _src);
            Validation.Assert(bnd.ScopeForMaps != _src);
            Validation.Assert(bnd.ScopeForAggs != _src);
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            // We should NOT be mapping the scope owned by this node.
            Validation.Assert(bnd.Scope != _src);
            return base.PreVisitImpl(bnd, idx);
        }
    }
}

/// <summary>
/// Rewrite a bound node replacing references to given scopes with given bound nodes.
/// </summary>
public static class ScopeGenReplacer
{
    /// <summary>
    /// Returns the resulting (rewritten) tree together with the number of replacements.
    /// </summary>
    public static (BoundNode res, int count) Run(IReducerHost host, BoundNode bnd, ScopeGenMap map)
    {
        return Visitor.Run(host, bnd, map);
    }

    private sealed class Visitor : ReducerBase
    {
        private readonly ScopeGenMap _map;
        private readonly BndNodeKindMask _mask;
        private int _count;

        /// <summary>
        /// Returns the resulting (rewritten) tree together with the number of replacements.
        /// </summary>
        public static (BoundNode res, int count) Run(IReducerHost host, BoundNode bnd, ScopeGenMap map)
        {
            Validation.AssertValue(host);
            Validation.AssertValue(bnd);
            Validation.Assert(!map.IsDefaultOrEmpty);

            var vtor = new Visitor(host, map);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(vtor.StackDepth == 1);
            return (vtor.Pop(), vtor._count);
        }

        private Visitor(IReducerHost host, ScopeGenMap map)
            : base(host, memoize: false)
        {
            _map = map;
            _mask = 0;
            foreach (var kvp in _map)
                _mask |= kvp.Key.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            if (!base.Enter(bnd, idx))
                return false;

            // Skip anything that doesn't include scopes.
            if ((bnd.AllKinds & _mask) == 0)
            {
                Push(bnd, bnd);
                return false;
            }

            return true;
        }

        protected override void OnMapped(BoundNode bndOld, BoundNode bndNew)
        {
#if DEBUG
            Validation.AssertValue(bndOld);
            Validation.AssertValue(bndNew);
            Validation.Assert(bndNew != bndOld);
            Validation.Assert(bndOld.Type == bndNew.Type);
#endif
        }

        protected override void VisitImpl(BndScopeRefNode bnd, int idx)
        {
            if (_map.TryGetValue(bnd.Scope, out var dst))
            {
                // REVIEW: Does anyone call this with more than one use when the _dst isn't cheap?
                Validation.Assert(_count == 0 || dst.IsCheap);
                _count++;
                Push(bnd, dst);
            }
            else
                VisitCore(bnd, idx);
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            // We should NOT be mapping any of the scopes owned by this call.
            Validation.Assert(bnd.Scopes.All(s => !_map.ContainsKey(s)));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            // We should NOT be mapping the scopes owned by this node.
            Validation.Assert(!_map.ContainsKey(bnd.ScopeForKeys));
            Validation.Assert(!_map.ContainsKey(bnd.ScopeForMaps));
            Validation.Assert(!_map.ContainsKey(bnd.ScopeForAggs));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            // We should NOT be mapping the scope owned by this node.
            Validation.Assert(!_map.ContainsKey(bnd.Scope));
            return base.PreVisitImpl(bnd, idx);
        }
    }
}

/// <summary>
/// Rewrites a bound tree to replace field/slot references of a given <see cref="ArgScope"/> according
/// to a provided field/slot map. Note that the "destination" values in the maps are <see cref="ArgScope"/>
/// values.
/// </summary>
public static class ScopeItemMapper
{
    /// <summary>
    /// Replace field/slot references of <paramref name="src"/> according to <paramref name="mapFld"/> and
    /// <paramref name="mapSlot"/>.
    /// </summary>
    public static BoundNode Run(IReducerHost host, BoundNode bnd, ArgScope src, ScopeFieldMap mapFld, ScopeSlotMap mapSlot)
    {
        Validation.BugCheckValue(host, nameof(host));
        Validation.BugCheckValue(src, nameof(src));
        return Visitor.Run(host, bnd, src, mapFld, mapSlot);
    }

    private sealed class Visitor : ReducerBase
    {
        private readonly ArgScope _src;
        private readonly BndNodeKindMask _mask;
        private readonly ScopeFieldMap _mapFld;
        private readonly ScopeSlotMap _mapSlot;

        public static BoundNode Run(IReducerHost host, BoundNode bnd, ArgScope src, ScopeFieldMap mapFld, ScopeSlotMap mapSlot)
        {
            Validation.AssertValue(bnd);
            if (bnd == null)
                return bnd;
            if (src.Type.IsRecordReq && mapFld.IsDefaultOrEmpty)
                return bnd;
            if (src.Type.IsTupleReq && mapSlot.IsDefaultOrEmpty)
                return bnd;

            var vtor = new Visitor(host, src, mapFld, mapSlot);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(vtor.StackDepth == 1);
            return vtor.Pop();
        }

        private Visitor(IReducerHost host, ArgScope src, ScopeFieldMap mapFld, ScopeSlotMap mapSlot)
            : base(host, memoize: false)
        {
            Validation.Assert(src.Type.IsAggReq);
            Validation.Assert(mapFld.Count + mapSlot.Count > 0);
            _src = src;
            _mask = _src.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
            _mapFld = mapFld;
            _mapSlot = mapSlot;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            if (!base.Enter(bnd, idx))
                return false;

            // Skip anything that doesn't include scopes.
            if ((bnd.AllKinds & _mask) == 0)
            {
                Push(bnd, bnd);
                return false;
            }

            return true;
        }

        protected override bool PreVisitImpl(BndGetFieldNode bnd, int idx)
        {
            if (!_mapFld.IsDefaultOrEmpty && bnd.Record.Type == _src.Type &&
                bnd.Record is BndScopeRefNode bsrn && bsrn.Scope == _src &&
                _mapFld.TryGetValue(bnd.Name, out var scope))
            {
                Validation.Assert(scope.Type == bnd.Type);
                Push(bnd, BndScopeRefNode.Create(scope));
                return false;
            }
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGetSlotNode bnd, int idx)
        {
            if (!_mapSlot.IsDefaultOrEmpty && bnd.Tuple.Type == _src.Type &&
                bnd.Tuple is BndScopeRefNode bsrn && bsrn.Scope == _src &&
                _mapSlot.TryGetValue(bnd.Slot, out var scope))
            {
                Validation.Assert(scope.Type == bnd.Type);
                Push(bnd, BndScopeRefNode.Create(scope));
                return false;
            }
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            // We should NOT be mapping any of the scopes owned by this call.
            Validation.Assert(bnd.Scopes.All(s => s != _src));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            // We should NOT be mapping the scopes owned by this node.
            Validation.Assert(bnd.ScopeForKeys != _src);
            Validation.Assert(bnd.ScopeForMaps != _src);
            Validation.Assert(bnd.ScopeForAggs != _src);
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            // We should NOT be mapping the scope owned by this node.
            Validation.Assert(bnd.Scope != _src);
            return base.PreVisitImpl(bnd, idx);
        }
    }
}

/// <summary>
/// Rewrites a bound tree to replace field/slot references of a given <see cref="ArgScope"/> according
/// to a provided field/slot map. Note that the "destination" values in the maps are <see cref="BoundNode"/>
/// values, not necessarily references to <see cref="ArgScope"/> instances.
/// </summary>
public static class ScopeItemReplacer
{
    /// <summary>
    /// Replace field/slot references of <paramref name="src"/> according to <paramref name="mapFld"/> and
    /// <paramref name="mapSlot"/>.
    /// </summary>
    public static BoundNode Run(IReducerHost host, BoundNode bnd, ArgScope src, ScopeFieldGenMap mapFld, ScopeSlotGenMap mapSlot)
    {
        Validation.BugCheckValue(host, nameof(host));
        Validation.BugCheckValue(src, nameof(src));
        return Visitor.Run(host, bnd, src, mapFld, mapSlot);
    }

    private sealed class Visitor : ReducerBase
    {
        private readonly ArgScope _src;
        private readonly BndNodeKindMask _mask;
        private readonly ScopeFieldGenMap _mapFld;
        private readonly ScopeSlotGenMap _mapSlot;

        public static BoundNode Run(IReducerHost host, BoundNode bnd, ArgScope src, ScopeFieldGenMap mapFld, ScopeSlotGenMap mapSlot)
        {
            Validation.AssertValue(bnd);
            if (bnd == null)
                return bnd;
            if (src.Type.IsRecordReq && mapFld.IsDefaultOrEmpty)
                return bnd;
            if (src.Type.IsTupleReq && mapSlot.IsDefaultOrEmpty)
                return bnd;

            var vtor = new Visitor(host, src, mapFld, mapSlot);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(vtor.StackDepth == 1);
            return vtor.Pop();
        }

        private Visitor(IReducerHost host, ArgScope src, ScopeFieldGenMap mapFld, ScopeSlotGenMap mapSlot)
            : base(host, memoize: false)
        {
            Validation.Assert(src.Type.IsRecordReq);
            Validation.Assert(mapFld.Count + mapSlot.Count > 0);
            _src = src;
            _mask = _src.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
            _mapFld = mapFld;
            _mapSlot = mapSlot;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            if (!base.Enter(bnd, idx))
                return false;

            // Skip anything that doesn't include scopes.
            if ((bnd.AllKinds & _mask) == 0)
            {
                Push(bnd, bnd);
                return false;
            }

            return true;
        }

        protected override bool PreVisitImpl(BndGetFieldNode bnd, int idx)
        {
            if (!_mapFld.IsDefaultOrEmpty && bnd.Record.Type == _src.Type &&
                bnd.Record is BndScopeRefNode bsrn && bsrn.Scope == _src &&
                _mapFld.TryGetValue(bnd.Name, out var dst))
            {
                Validation.Assert(dst.Type == bnd.Type);
                Push(bnd, dst);
                return false;
            }
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGetSlotNode bnd, int idx)
        {
            if (!_mapSlot.IsDefaultOrEmpty && bnd.Tuple.Type == _src.Type &&
                bnd.Tuple is BndScopeRefNode bsrn && bsrn.Scope == _src &&
                _mapSlot.TryGetValue(bnd.Slot, out var dst))
            {
                Validation.Assert(dst.Type == bnd.Type);
                Push(bnd, dst);
                return false;
            }
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            // We should NOT be mapping any of the scopes owned by this call.
            Validation.Assert(bnd.Scopes.All(s => s != _src));
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            // We should NOT be mapping the scopes owned by this node.
            Validation.Assert(bnd.ScopeForKeys != _src);
            Validation.Assert(bnd.ScopeForMaps != _src);
            Validation.Assert(bnd.ScopeForAggs != _src);
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            // We should NOT be mapping the scope owned by this node.
            Validation.Assert(bnd.Scope != _src);
            return base.PreVisitImpl(bnd, idx);
        }
    }
}

public static class ScopeFinder
{
    public interface IContext
    {
        int Depth { get; }
        BoundNode this[int index] { get; }
        int LocalScopeDepth { get; }
        (int index, BoundNode owner) GetLocalScopeOwner(int index);
    }

    public static void Run(BoundNode bnd, ScopeSink sink, ScopeMap scopeMap = default)
    {
        Visitor.Run(bnd, sink, scopeMap);
    }

    private sealed class Visitor : NoopScopedBoundTreeVisitor
    {
        private readonly ScopeSink _sink;
        private readonly ScopeMap _scopeMap;
        private const BndNodeKindMask _mask = BndNodeKindMask.ArgScopeRef | BndNodeKindMask.IndScopeRef;
        private readonly List<BoundNode> _stack;
        private readonly Dictionary<ArgScope, int> _localScopes;
        private readonly Context _ctx;

        public static void Run(BoundNode bnd, ScopeSink sink, ScopeMap scopeMap)
        {
            var vtor = new Visitor(sink, scopeMap);
            vtor.Go(bnd);
        }

        private Visitor(ScopeSink sink, ScopeMap map)
        {
            Validation.AssertValue(sink);
            _sink = sink;
            _scopeMap = map;
            _stack = new List<BoundNode>();
            _localScopes = new Dictionary<ArgScope, int>();
            _ctx = new Context(this);
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            if (!base.Enter(bnd, idx))
                return false;

            // Skip anything that doesn't include scopes.
            if ((bnd.AllKinds & _mask) == 0)
                return false;

            return true;
        }

        private bool MapScope(ref ArgScope scope)
        {
            Validation.AssertValue(scope);
            if (!_scopeMap.TryGetValue(scope, out var dst))
                return false;

            Validation.Assert(dst.Type == scope.Type);
            scope = dst;

            // Since the map is not necessarily transitive, we need to keep trying.
            // Of course, this will result in an infinite loop if the map has any cycles.
            while (_scopeMap.TryGetValue(scope, out dst))
            {
                Validation.Assert(dst.Type == scope.Type);
                scope = dst;
            }
            return true;
        }

        protected override void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            // We should NOT be mapping (or searching for) scopes owned by the src tree.
            Validation.Assert(!_scopeMap.ContainsKey(scope));
            Validation.Assert(!_localScopes.ContainsKey(scope));
            Validation.Assert(_stack.Count > 0);
            _localScopes.Add(scope, _stack.Count - 1);
            base.PushScope(scope, owner, idx, slot, isArgValid);
        }

        protected override void PopScope(ArgScope scope)
        {
            _localScopes.Remove(scope, out int depth).Verify();
            Validation.Assert(depth == _stack.Count - 1);
            base.PopScope(scope);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope ps)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValueOrNull(ps);

            var scope = bnd.Scope;
            if (!_localScopes.ContainsKey(scope))
            {
                MapScope(ref scope);
                _sink(scope, bnd, _ctx);
            }
        }

        protected override bool PreVisitCore(BndParentNode bnd, int idx)
        {
            if (!base.PreVisitCore(bnd, idx))
                return false;
            _stack.Add(bnd);
            return true;
        }

        protected override void PostVisitCore(BndParentNode node, int idx)
        {
            var tmp = _stack.Pop();
            Validation.Assert(tmp == node);
            base.PostVisitCore(node, idx);
        }

        private sealed class Context : IContext
        {
            private readonly Visitor _vtor;
            private readonly List<BoundNode> _stack;

            public Context(Visitor vtor)
            {
                _vtor = vtor;
                _stack = vtor._stack;
            }

            public BoundNode this[int index]
            {
                get
                {
                    int depth = _stack.Count;
                    Validation.BugCheckIndex(index, depth, nameof(index));
                    return _stack[depth - 1 - index];
                }
            }

            public int Depth => _stack.Count;

            public int LocalScopeDepth => _vtor.ScopeDepth;

            public (int index, BoundNode owner) GetLocalScopeOwner(int index)
            {
                int depth = _vtor.ScopeDepth;
                Validation.BugCheckIndex(index, depth, nameof(index));
                var scope = _vtor.GetScope(index);
                _vtor._localScopes.TryGetValue(scope.Scope, out int ibnd).Verify();
                int d = _stack.Count;
                Validation.AssertIndex(ibnd, d);
                return (d - 1 - ibnd, _stack[ibnd]);
            }
        }
    }
}

/// <summary>
/// Counts the number of references to the given scope. A reference nested in
/// one or more loop scopes counts for 1000.
/// REVIEW: Perhaps implement using <see cref="ScopeFinder"/>.
/// REVIEW: Checks for whether the count >= C could benefit from a new
/// method that short-circuits once C scope references are found.
/// </summary>
public static class ScopeCounter
{
    /// <summary>
    /// The count to use for a scope reference nested within a loop scope.
    /// </summary>
    public const int CountForLoop = 1000;

    /// <summary>
    /// Return the number of occurrences in <paramref name="bnd"/>, after replacements indicated by
    /// <paramref name="scopeMap"/>, of <paramref name="src"/>. An occurrence that is inside a map
    /// scope adds 1000 to the occurrence count.
    /// </summary>
    public static int Count(BoundNode bnd, ArgScope src, ScopeMap scopeMap = default)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckValue(src, nameof(src));
        return Counter.Run(bnd, src, scopeMap);
    }

    /// <summary>
    /// Return whether there are any references to <paramref name="src"/> in <paramref name="bnd"/>, after
    /// replacements indicated by <paramref name="scopeMap"/>.
    /// </summary>
    public static bool Any(BoundNode bnd, ArgScope src, ScopeMap scopeMap = default)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckValue(src, nameof(src));
        return Detector.Run(bnd, src, scopeMap);
    }

    /// <summary>
    /// Return whether there are any declarations of the given scopes in <paramref name="bnd"/>.
    /// </summary>
    public static (BndScopeOwnerNode owner, ArgScope scope) FindDecl(
        BoundNode bnd, ScopeTuple many = default, ArgScope one = null)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckParam(many.Length > 0 | one != null, nameof(one));
        return DeclFinder.Run(bnd, many, one);
    }

    private sealed class Counter : NoopScopedBoundTreeVisitor
    {
        private readonly ArgScope _src;
        private readonly BndNodeKindMask _mask;
        private readonly ScopeMap _scopeMap;
        private int _count;

        // Tracks nesting in loop scopes.
        private int _loopNest;

        public static int Run(BoundNode bnd, ArgScope src, ScopeMap scopeMap)
        {
            var vtor = new Counter(src, scopeMap);
            vtor.Go(bnd);
            Validation.Assert(vtor._loopNest == 0);
            return vtor._count;
        }

        private Counter(ArgScope src, ScopeMap map)
        {
            Validation.AssertValue(src);
            _src = src;
            _mask = _src.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
            _scopeMap = map;
            _loopNest = 0;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            // Skip anything that doesn't include what we are looking for.
            if ((bnd.AllKinds & _mask) == 0)
                return false;

            return true;
        }

        private bool MapScope(ref ArgScope scope)
        {
            Validation.AssertValue(scope);
            if (!_scopeMap.TryGetValue(scope, out var dst))
                return false;

            Validation.Assert(dst.Type == scope.Type);
            scope = dst;

            // Since the map is not necessarily transitive, we need to keep trying.
            // Of course, this will result in an infinite loop if the map has any cycles.
            while (_scopeMap.TryGetValue(scope, out dst))
            {
                Validation.Assert(dst.Type == scope.Type);
                scope = dst;
            }
            return true;
        }

        protected override void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            // We should NOT be mapping (or searching for) scopes owned by the src tree.
            Validation.Assert(!_scopeMap.ContainsKey(scope));
            Validation.Assert(scope != _src);
            base.PushScope(scope, owner, idx, slot, isArgValid);
            if (scope.Kind.IsLoopScope())
            {
                _loopNest++;
                Validation.Assert(_loopNest > 0);
            }
        }

        protected override void PopScope(ArgScope scope)
        {
            if (scope.Kind.IsLoopScope())
            {
                Validation.Assert(_loopNest > 0);
                _loopNest--;
            }
            base.PopScope(scope);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope ps)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValueOrNull(ps);

            var scope = bnd.Scope;
            MapScope(ref scope);
            if (scope == _src)
                _count += _loopNest > 0 ? CountForLoop : 1;
        }
    }

    private sealed class Detector : NoopBoundTreeVisitor
    {
        private readonly ArgScope _src;
        private readonly BndNodeKindMask _mask;
        private readonly ScopeMap _scopeMap;
        private bool _found;

        public static bool Run(BoundNode bnd, ArgScope src, ScopeMap scopeMap)
        {
            var vtor = new Detector(src, scopeMap);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            return vtor._found;
        }

        private Detector(ArgScope src, ScopeMap map)
        {
            Validation.AssertValue(src);
            _src = src;
            _mask = _src.IsIndex ? BndNodeKindMask.IndScopeRef : BndNodeKindMask.ArgScopeRef;
            _scopeMap = map;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            // If we've already found one, no need to drill in.
            if (_found)
                return false;

            // Skip anything that doesn't include what we are looking for.
            if ((bnd.AllKinds & _mask) == 0)
                return false;

            return true;
        }

        private bool MapScope(ref ArgScope scope)
        {
            Validation.AssertValue(scope);
            if (!_scopeMap.TryGetValue(scope, out var dst))
                return false;

            Validation.Assert(dst.Type == scope.Type);
            scope = dst;

            // Since the map is not necessarily transitive, we need to keep trying.
            // Of course, this will result in an infinite loop if the map has any cycles.
            while (_scopeMap.TryGetValue(scope, out dst))
            {
                Validation.Assert(dst.Type == scope.Type);
                scope = dst;
            }
            return true;
        }

        protected override void VisitImpl(BndScopeRefNode bnd, int idx)
        {
            Validation.AssertValue(bnd);

            var scope = bnd.Scope;
            MapScope(ref scope);
            if (scope == _src)
                _found = true;
        }
    }

    private sealed class DeclFinder : NoopBoundTreeVisitor
    {
        private readonly ScopeTuple _many;
        private readonly ArgScope _one;

        private BndScopeOwnerNode _owner;
        private ArgScope _scope;

        public static (BndScopeOwnerNode owner, ArgScope scope) Run(
            BoundNode bnd, ScopeTuple many = default, ArgScope one = null)
        {
            var vtor = new DeclFinder(many, one);
            int num = bnd.Accept(vtor, 0);
            Validation.Assert(num == bnd.NodeCount);
            return (vtor._owner, vtor._scope);
        }

        private DeclFinder(ScopeTuple many, ArgScope one)
        {
            Validation.Assert(many.Length > 0 | one != null);

            if (one == null && many.Length == 1)
            {
                _many = default;
                _one = many[0];
            }
            else
            {
                _many = many;
                _one = one;
            }
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            // If we've already found one, no need to drill in.
            if (_scope != null)
                return false;

            // Skip anything that doesn't include what we are looking for.
            if ((bnd.AllKinds & BndNodeKindMask.ScopeOwner) == 0)
                return false;

            return true;
        }

        private bool Test(BndScopeOwnerNode owner, ArgScope scope)
        {
            Validation.Assert(_owner == null);
            Validation.Assert(_scope == null);
            Validation.AssertValue(owner);

            if (scope == null)
                return false;
            if (scope == _one)
            {
                _owner = owner;
                _scope = scope;
                return true;
            }
            for (int i = 0; i < _many.Length; i++)
            {
                if (scope == _many[i])
                {
                    _owner = owner;
                    _scope = scope;
                    return true;
                }
            }
            return false;
        }

        private bool Test(BndScopeOwnerNode owner, ScopeTuple scopes)
        {
            Validation.Assert(_owner == null);
            Validation.Assert(_scope == null);

            for (int i = 0; i < scopes.Length; i++)
            {
                if (Test(owner, scopes[i]))
                    return true;
            }
            return false;
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            if (Test(bnd, bnd.Scopes))
                return false;
            if (Test(bnd, bnd.Indices))
                return false;
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            if (Test(bnd, bnd.ScopeForKeys))
                return false;
            if (Test(bnd, bnd.IndexForKeys))
                return false;
            if (Test(bnd, bnd.ScopeForAggs))
                return false;
            if (Test(bnd, bnd.ScopeForMaps))
                return false;
            if (Test(bnd, bnd.IndexForMaps))
                return false;
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            if (Test(bnd, bnd.Scope))
                return false;
            return base.PreVisitImpl(bnd, idx);
        }
    }
}
