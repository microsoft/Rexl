// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using Conditional = System.Diagnostics.ConditionalAttribute;
using ScopeTuple = Immutable.Array<ArgScope>;

/// <summary>
/// Optimizes a given <see cref="BoundNode"/> by applying
/// optimizations such as expression hoisting.
/// </summary>
public static class Optimizer
{
    public static BoundNode Run(IReducerHost host, BoundNode bnd)
    {
        return HoistVisitor.Run(host, bnd);
    }

    /// <summary>
    /// Implements expression hoisting out of nested loop scopes. This
    /// avoids repeated computations of expressions which don't vary
    /// within a loop/ForEach body.
    /// </summary>
    private sealed class HoistVisitor : ScopedBoundTreeVisitor
    {
        // It is illegal to hoist these or anything that contains them.
        private const BndNodeKindMask k_maskProhibited =
            BndNodeKindMask.Error |
            BndNodeKindMask.MissingValue |
            BndNodeKindMask.Namespace |
            BndNodeKindMask.CallVolatile |
            BndNodeKindMask.CallProcedure;

        /// <summary>
        /// Whether hoisting out of a loop scope is prohibited for this kind of node.
        /// </summary>
        private static bool IsProhibited(BoundNode bnd)
        {
            Validation.AssertValue(bnd);
            if ((bnd.AllKinds & k_maskProhibited) != 0)
                return true;
            return false;
        }

        /// <summary>
        /// Returns true if hoisting <paramref name="bnd"/> out of a loop scope is prohibited or doing so
        /// has no value by itself. In the latter case, it may be hoisted as part of a larger operation
        /// such as a variadic operation.
        /// </summary>
        private static bool IsInhibited(BoundNode bnd)
        {
            Validation.AssertValue(bnd);

            if (IsProhibited(bnd))
                return true;
            return bnd.IsCheap;
        }

        private struct HoistInfo
        {
            /// <summary>
            /// Final (possibly rewritten) bound node.
            /// </summary>
            public readonly BoundNode Bnd;

            /// <summary>
            /// Used scopes within the node (not including ones defined within it),
            /// referenced by their scope depth.
            /// </summary>
            public readonly BitSet ScopeRefs;

            /// <summary>
            /// Whether the bound node has been rewritten. 
            /// </summary>
            public readonly bool Rewritten;

            /// <summary>
            /// The base hoisting eval depth level destination of the bound node,
            /// without regard for the form of Bnd.
            /// </summary>
            public readonly int HoistDstBase;

            /// <summary>
            /// Whether the base hoisting destination should be disregarded
            /// except in special situations, such as variadic ops.
            /// </summary>
            public readonly bool Inhibit;

            /// <summary>
            /// The final hoisting eval depth level destination of the bound node.
            /// </summary>
            public int HoistDst => Inhibit ? _noHoist : HoistDstBase;

            /// <summary>
            /// Whether hoisting should be disregarded completely. Separate
            /// from <see cref="Inhibit"/> in that it still stops hoisting
            /// in variadic ops.
            /// </summary>
            public bool Prohibit => IsProhibited(Bnd);

            public HoistInfo(BoundNode bnd, BitSet scopeRefs, bool rewritten, int hoistDstBase, bool inhibit)
            {
                Validation.AssertValue(bnd);
                Validation.Assert(inhibit == IsInhibited(bnd));
                Bnd = bnd;
                ScopeRefs = scopeRefs;
                Rewritten = rewritten;
                HoistDstBase = hoistDstBase;
                Inhibit = inhibit;
            }
        }

        private const int _noHoist = int.MaxValue;
        private readonly IReducerHost _host;

        // Evaluation stack. Contains the current scope depth at that eval depth, the
        // parent node, and the list of BoundNode to hoist to that location.
        private readonly List<(int iscope, BndParentNode par, List<(ArgScope newScope, BoundNode bnd)> withs)> _evalStack;
        // Stack of information about visited nodes.
        private readonly List<HoistInfo> _stack;

        public static BoundNode Run(IReducerHost host, BoundNode bnd)
        {
            var visitor = new HoistVisitor(host);
            int num = bnd.Accept(visitor, 0);
            Validation.Assert(num == bnd.NodeCount);
            Validation.Assert(visitor._stack.Count == 1);
            return visitor._stack[0].Bnd;
        }

        private HoistVisitor(IReducerHost host)
        {
            _host = host ?? NoopReducerHost.Instance;
            _evalStack = new List<(int iscope, BndParentNode par, List<(ArgScope newScope, BoundNode bnd)> withs)>();
            _stack = new List<HoistInfo>();
        }

        /// <summary>
        /// Return the last (deepest) eval index whose iscope is no more than <paramref name="iscope"/>.
        /// </summary>
        private int ScopeToHoist(int iscope)
        {
            Validation.Assert(_evalStack.Count > 0);
            Validation.Assert(_evalStack[0].iscope == 0);
            Validation.AssertIndexInclusive(iscope, ScopeDepth);

            // Binary search to find the first item whose iscope is larger than the given one.
            int ivMin = 1;
            int ivLim = _evalStack.Count;
            while (ivMin < ivLim)
            {
                int ivMid = (int)((uint)(ivMin + ivLim) >> 1);
                Validation.Assert(ivMin <= ivMid & ivMid < ivLim);
                if (_evalStack[ivMid].iscope > iscope)
                    ivLim = ivMid;
                else
                    ivMin = ivMid + 1;
            }
            Validation.Assert(1 <= ivMin & ivMin <= _evalStack.Count);
            Validation.Assert(_evalStack[ivMin - 1].iscope <= iscope);
            Validation.Assert(ivMin == _evalStack.Count || _evalStack[ivMin].iscope > iscope);
            return ivMin - 1;
        }

        protected override bool PreVisitCore(BndParentNode bnd, int idx)
        {
            _evalStack.Add((ScopeDepth, bnd, null));
            return base.PreVisitCore(bnd, idx);
        }

        private void Push(BoundNode bndOld, BoundNode bndNew, BitSet scopeRefs, int hoistDst, bool inhibit)
        {
            Validation.AssertValue(bndOld);
            Validation.AssertValue(bndNew);
            Validation.Assert(bndOld.Type == bndNew.Type);
            Validation.Assert(scopeRefs.SlotMax < ScopeDepth);
            Validation.Assert(inhibit == IsInhibited(bndOld));

            Validation.Assert(bndNew == bndOld || !bndNew.Equivalent(bndOld));
            if (bndOld is BndParentNode)
            {
                Validation.Assert(_evalStack.Count > 0);
                var (iscope, p, withs) = _evalStack.Pop();
                Validation.Assert(iscope == ScopeDepth);
                Validation.Assert(p == bndOld);

                if (withs != null && withs.Count > 0)
                {
                    // If there are nodes hoisted to this level, wrap bndNew in a With.
                    Validation.Assert(bndNew != bndOld);
                    _host.OnMapped(bndOld, bndNew);
                    bndNew = CreateWith(bndNew, withs);
                    Validation.Assert(bndOld.Type == bndNew.Type);
                }
            }

            if (bndNew != bndOld)
                _host.OnMapped(bndOld, bndNew);
            _stack.Add(new HoistInfo(bndNew, scopeRefs, bndNew != bndOld, hoistDst, inhibit));
        }

        private HoistInfo Pop()
        {
            Validation.Assert(_stack.Count > 0);
            return _stack.Pop();
        }

        private Immutable.Array<HoistInfo> Pop(int count)
        {
            Validation.AssertIndexInclusive(count, _stack.Count);
            if (count <= 0)
                return Immutable.Array<HoistInfo>.Empty;

            var bldr = Immutable.Array.CreateBuilder<HoistInfo>(count, init: true);
            int index = _stack.Count - count;
            for (int i = 0; i < count; i++)
                bldr[i] = _stack[index + i];

            _stack.RemoveRange(index, count);
            return bldr.ToImmutable();
        }

        private Immutable.Array<HoistInfo> PopAndReverse(int count)
        {
            Validation.AssertIndexInclusive(count, _stack.Count);
            if (count <= 0)
                return Immutable.Array<HoistInfo>.Empty;

            var bldr = Immutable.Array.CreateBuilder<HoistInfo>(count, init: true);
            int index = _stack.Count;
            for (int i = 0; i < count; i++)
                bldr[i] = _stack[--index];

            _stack.RemoveRange(index, count);
            return bldr.ToImmutable();
        }

        /// <summary>
        /// Reverse the top <paramref name="count"/> items on the stack.
        /// </summary>
        private void Reverse(int count)
        {
            Validation.AssertIndexInclusive(count, _stack.Count);
            for (int min = _stack.Count - count, max = _stack.Count - 1; min < max; min++, max--)
            {
                var tmp = _stack[min];
                _stack[min] = _stack[max];
                _stack[max] = tmp;
            }
        }

        /// <summary>
        /// Union the top <paramref name="count"/> scope ref sets.
        /// </summary>
        private BitSet UnionScopeRefs(int count)
        {
            Validation.AssertIndexInclusive(count, _stack.Count);
            BitSet res = default;
            for (int i = _stack.Count - count; i < _stack.Count; i++)
                res |= _stack[i].ScopeRefs;
            return res;
        }

        /// <summary>
        /// Surrounds <paramref name="bnd"/> with a new With call defined by <paramref name="hoisted"/>.
        /// </summary>
        private static BndCallNode CreateWith(BoundNode bnd, List<(ArgScope newScope, BoundNode bnd)> hoisted)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValue(hoisted);
            Validation.Assert(hoisted.Count > 0);

            var bldrArgs = ArgTuple.CreateBuilder(hoisted.Count + 1, init: true);
            var bldrScopes = ScopeTuple.CreateBuilder(hoisted.Count, init: true);
            for (int i = 0; i < hoisted.Count; i++)
            {
                var (scope, arg) = hoisted[i];
                bldrScopes[i] = scope;
                bldrArgs[i] = arg;
            }

            bldrArgs[hoisted.Count] = bnd;
            return BndCallNode.Create(WithFunc.With, bnd.Type, bldrArgs.ToImmutable(), bldrScopes.ToImmutable());
        }

        /// <summary>
        /// Returns the eval level to hoist <paramref name="bnd"/> to based on
        /// the currently pushed scopes, given that <paramref name="scopeRefs"/>
        /// contains the scope references within it.
        /// </summary>
        private int GetHoistDst(BoundNode bnd, BitSet scopeRefs, out int hoistDstBase, out bool inhibit)
        {
            Validation.AssertValue(bnd);
            inhibit = IsInhibited(bnd);

            var scopeLimRef = scopeRefs.SlotMax + 1;
            var scopeLimHoist = PeekLoopGroup().min;
            if (scopeLimRef <= scopeLimHoist)
            {
                // Clamp the scope level to just before the next loop scope group, so that
                // the hoisting definition happens just before it is used in a loop scope.
                // Binary search to find the first loop group whose min is larger than the given scopeLimRef.
                int ivMin = 0;
                int ivLim = LoopGroupDepth;
                while (ivMin < ivLim)
                {
                    int ivMid = (int)((uint)(ivMin + ivLim) >> 1);
                    Validation.Assert(ivMin <= ivMid & ivMid < ivLim);
                    if (GetLoopGroup(ivMid).min >= scopeLimRef)
                        ivLim = ivMid;
                    else
                        ivMin = ivMid + 1;
                }
                Validation.Assert(0 <= ivMin & ivMin <= LoopGroupDepth);
                Validation.Assert(ivMin == 0 || GetLoopGroup(ivMin - 1).min < scopeLimRef);
                Validation.Assert(ivMin == LoopGroupDepth || GetLoopGroup(ivMin).min >= scopeLimRef);
                scopeLimRef = ivMin == LoopGroupDepth ? ScopeDepth : GetLoopGroup(ivMin).min;
            }

            Validation.Assert(scopeLimRef <= ScopeDepth);
            if (scopeLimRef > scopeLimHoist)
                hoistDstBase = _noHoist;
            else
                hoistDstBase = ScopeToHoist(scopeLimRef);

            return inhibit ? _noHoist : hoistDstBase;
        }

        /// <summary>
        /// Hoists the <paramref name="child"/>.
        ///
        /// If the child is not hoisted or was not rewritten prior, returns
        /// false and sets <paramref name="arg"/> to the original node.
        /// Otherwise returns true and set it to its new value,
        /// recording the original bound node in <see cref="_evalStack"/>.
        /// </summary>
        private bool HoistChild(HoistInfo child, int hoistDst, out BoundNode arg, bool inLoop = false)
        {
            Validation.AssertValue(child.Bnd);
            Validation.Assert(child.HoistDstBase <= hoistDst || child.HoistDstBase == _noHoist);

            if (child.HoistDst < hoistDst || child.HoistDst < _noHoist && inLoop)
            {
                arg = MakeHoist(child.Bnd, child.HoistDst);
                return true;
            }

            arg = child.Bnd;
            return child.Rewritten;
        }

        /// <summary>
        /// Hoists the <paramref name="children"/> bound nodes
        /// with original parent <paramref name="bnd"/>
        ///
        /// If none of the children are hoisted or have been rewritten prior, returns
        /// false and sets <paramref name="newArgs"/> to default.
        /// Otherwise sets it to the children's new values,
        /// recording the original bound nodes in <see cref="_evalStack"/>.
        ///
        /// Regardless of the return value, sets <paramref name="scopeRefs"/>
        /// to <paramref name="bnd"/>'s scope references and <paramref name="hoistDst"/>
        /// to its hoisting destination.
        /// </summary>
        private bool HoistChildren(BndParentNode bnd, Immutable.Array<HoistInfo> children,
            out BitSet scopeRefs, out int hoistDstBase, out bool inhibit, out ArgTuple newArgs, BitSet nestedLoops = default)
        {
            Validation.AssertValue(bnd);
            Validation.Assert(bnd is BndScopeOwnerNode || nestedLoops.IsEmpty);
            Validation.Assert(bnd.ChildCount == children.Length);

            Validation.Assert(nestedLoops.SlotMax < children.Length);
            scopeRefs = default;
            foreach (var c in children)
                scopeRefs |= c.ScopeRefs;
            scopeRefs = scopeRefs.ClearAtAndAbove(ScopeDepth);
            var hoistDst = GetHoistDst(bnd, scopeRefs, out hoistDstBase, out inhibit);

            return HoistChildrenCore(children, hoistDst, out newArgs, nestedLoops);
        }

        private bool HoistChildrenCore(Immutable.Array<HoistInfo> children, int hoistDst, out ArgTuple newArgs, BitSet nestedLoops = default)
        {
            ArgTuple.Builder bldr = null;
            for (int i = 0; i < children.Length; i++)
            {
                var info = children[i];
                var child = info.Bnd;
                var hoistDstChild = info.HoistDst;
                Validation.Assert(hoistDstChild <= hoistDst || hoistDstChild == _noHoist);
                if (info.Rewritten)
                    bldr ??= MakeBuilder(i, children, true);

                // If the whole node is hoisted, we don't need to hoist children
                // to the same level as the whole.
                // The exception is if the whole is defining a loop scope which
                // the child would be hoisted out of.
                if (hoistDstChild < hoistDst || hoistDstChild < _noHoist && nestedLoops.TestBit(i))
                {
                    child = MakeHoist(child, hoistDstChild);
                    bldr ??= MakeBuilder(i, children, true);
                }

                if (bldr != null)
                    bldr[i] = child;
            }

            Validation.Assert(bldr == null || bldr.Count == children.Length);
            if (bldr == null)
            {
                newArgs = default;
                return false;
            }

            newArgs = bldr.ToImmutable();
            return true;
        }

        /// <summary>
        /// Creates a new builder the size of <paramref name="children"/>
        /// and copies the first <paramref name="count"/> of them over.
        /// If <paramref name="init"/> is true, the other slots are initialized to null.
        /// </summary>
        private ArgTuple.Builder MakeBuilder(int count, Immutable.Array<HoistInfo> children, bool init)
        {
            var bldr = ArgTuple.CreateBuilder(children.Length, init);
            if (init)
            {
                for (int i = 0; i < count; i++)
                    bldr[i] = children[i].Bnd;
            }
            else
            {
                for (int i = 0; i < count; i++)
                    bldr.Add(children[i].Bnd);
            }

            return bldr;
        }

        /// <summary>
        /// Creates a scope reference for <paramref name="bnd"/> and
        /// records it in <see cref="_evalStack"/>. If an equivalent
        /// bound node is already there, reuses its scope variable.
        /// </summary>
        private BndScopeRefNode MakeHoist(BoundNode bnd, int hoistDst)
        {
            Validation.Assert(!(bnd is BndLeafNode));
            Validation.AssertIndex(hoistDst, _evalStack.Count);

            var (iscope, par, withs) = _evalStack[hoistDst];
            if (withs == null)
                _evalStack[hoistDst] = (iscope, par, withs = new List<(ArgScope newScope, BoundNode bnd)>());

            foreach (var with in withs)
            {
                // Reuse a scope if bound node has been hoisted elsewhere already.
                if (with.bnd.Equivalent(bnd))
                    return BndScopeRefNode.Create(with.newScope);
            }

            var newScope = ArgScope.Create(ScopeKind.With, bnd.Type);
            withs.Add((newScope, bnd));
            return BndScopeRefNode.Create(newScope);
        }

        protected override void PostVisitImpl(BndCallNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            var traits = bnd.Traits;
            Validation.Assert(traits.SlotCount == bnd.Args.Length);
            bool anyLoops = false;
            BitSet nestedLoops = default;
            for (int i = 0; i < traits.SlotCount; i++)
            {
                if (anyLoops && traits.IsNested(i))
                    nestedLoops = nestedLoops.SetBit(i);
                if (!anyLoops)
                {
                    var kind = traits.GetScopeKind(i);
                    if (kind.IsLoopScope())
                        anyLoops = true;
                }
            }
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs, nestedLoops))
                final = BndCallNode.Create(bnd.Oper, bnd.Type, newArgs, bnd.Scopes, bnd.Indices, bnd.Directives, bnd.Names);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndGroupByNode bnd, int idx)
        {
            int depth = _stack.Count;
            bool changed = false;
            Reverse(bnd.ChildCount);
            var scopeRefs = UnionScopeRefs(bnd.ChildCount).ClearAtAndAbove(ScopeDepth);
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            Validation.Assert(inhibit == IsInhibited(bnd));

            // Source.
            changed |= HoistChild(Pop(), hoistDst, out var src);

            // Pure keys.
            // Note that keys define a map scope.
            if (bnd.PureKeys.Length > 0 && HoistChildrenCore(PopAndReverse(bnd.PureKeys.Length), hoistDst, out var keysPure, BitSet.GetMask(0, bnd.PureKeys.Length)))
                changed = true;
            else
                keysPure = bnd.PureKeys;

            // Keep keys.
            // Note that keys define a map scope.
            var keysKeep = bnd.KeepKeys;
            foreach (var (name, valCur) in bnd.KeepKeys.GetPairs())
            {
                var childKey = Pop();
                if (HoistChild(childKey, hoistDst, out var valNew, inLoop: true))
                {
                    keysKeep = keysKeep.SetItem(name, valNew);
                    changed = true;
                }
            }

            // Maps.
            // Note that maps define a map scope.
            var mapItems = bnd.MapItems;
            if (mapItems.Count > 0)
            {
                var hoistDstMap = hoistDst;
                foreach (var (name, valCur) in bnd.MapItems.GetPairs())
                {
                    if (valCur == null)
                        continue;
                    var childMap = Pop();
                    if (HoistChild(childMap, hoistDstMap, out var valNew, inLoop: true))
                    {
                        mapItems = mapItems.SetItem(name, valNew);
                        changed = true;
                    }
                }
            }

            var aggItems = bnd.AggItems;
            foreach (var (name, valCur) in bnd.AggItems.GetPairs())
            {
                var childAgg = Pop();
                if (HoistChild(childAgg, hoistDst, out var valNew))
                {
                    aggItems = aggItems.SetItem(name, valNew);
                    changed = true;
                }
            }

            Validation.Assert(depth - _stack.Count == bnd.ChildCount);

            var final = bnd;
            if (changed)
            {
                final = BndGroupByNode.Create(bnd.Type, src,
                    bnd.ScopeForKeys, bnd.IndexForKeys, keysPure, keysKeep, bnd.KeysCi,
                    bnd.ScopeForMaps, bnd.IndexForMaps, mapItems, bnd.ScopeForAggs, aggItems);
            }

            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndSetFieldsNode bnd, int idx)
        {
            int depth = _stack.Count;
            bool changed = false;
            Reverse(bnd.ChildCount);
            var scopeRefs = UnionScopeRefs(bnd.ChildCount).ClearAtAndAbove(ScopeDepth);
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            Validation.Assert(inhibit == IsInhibited(bnd));

            // Source.
            changed |= HoistChild(Pop(), hoistDst, out var src);

            var additions = bnd.Additions;
            foreach (var (name, valCur) in additions.GetPairs())
            {
                if (HoistChild(Pop(), hoistDst, out var valNew))
                {
                    additions = additions.SetItem(name, valNew);
                    changed = true;
                }
            }

            Validation.Assert(depth - _stack.Count == bnd.ChildCount);

            BoundNode final = bnd;
            if (changed)
                final = BndSetFieldsNode.Create(bnd.Type, src, bnd.Scope, additions, bnd.NameHints);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndModuleProjectionNode bnd, int idx)
        {
            Validation.Assert(bnd.ChildCount == 2);

            int depth = _stack.Count;
            bool changed = false;
            var scopeRefs = UnionScopeRefs(bnd.ChildCount).ClearAtAndAbove(ScopeDepth);
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            Validation.Assert(inhibit == IsInhibited(bnd));

            // Pop in reverse order.
            changed |= HoistChild(Pop(), hoistDst, out var rec);
            changed |= HoistChild(Pop(), hoistDst, out var mod);

            Validation.Assert(depth - _stack.Count == bnd.ChildCount);

            BoundNode final = bnd;
            if (changed)
                final = BndModuleProjectionNode.Create(mod, bnd.Scope, rec);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope scope)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValueOrNull(scope);

            if (scope == null)
                Push(bnd, bnd, default, _noHoist, inhibit: true);
            else
            {
                var scopeRef = (BitSet)0x1 << scope.Depth;
                GetHoistDst(bnd, scopeRef, out var hoistDstBase, out var inhibit);
                Validation.Assert(hoistDstBase >= ScopeToHoist(scope.Depth + 1));
                Validation.Assert(inhibit);
                Push(bnd, bnd, scopeRef, hoistDstBase, inhibit: true);
            }
        }

        private void VisitLeafCore(BndLeafNode bnd, int idx)
        {
            GetHoistDst(bnd, default, out var hoistDstBase, out bool inhibit);
            Validation.Assert(inhibit);
            Push(bnd, bnd, default, hoistDstBase, inhibit: true);
        }

        protected override void VisitImpl(BndErrorNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndNamespaceNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndMissingValueNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndNullNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndDefaultNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndIntNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndFltNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndStrNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndCmpConstNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndThisNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndGlobalNode bnd, int idx) { VisitLeafCore(bnd, idx); }
        protected override void VisitImpl(BndFreeVarNode bnd, int idx) { VisitLeafCore(bnd, idx); }

        protected override void PostVisitImpl(BndVariadicOpNode bnd, int idx)
        {
            switch (bnd.Op)
            {
            case BinaryOp.Add:
            case BinaryOp.Mul:
                if (bnd.Type.IsFractionalXxx)
                    PostVisitFractional(bnd);
                else
                    PostVisitCommutative(bnd);
                break;
            case BinaryOp.StrConcat:
            case BinaryOp.SeqConcat:
                PostVisitSequential(bnd);
                break;
            case BinaryOp.TupleConcat:
            case BinaryOp.RecordConcat:
                // Avoid hoisting intermediate ops.
                var children = Pop(bnd.ChildCount);
                BoundNode final = bnd;
                if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                    final = BndVariadicOpNode.Create(bnd.Type, bnd.Op, newArgs, bnd.Inverted);
                Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
                break;
            default:
                PostVisitCommutative(bnd);
                break;
            }
        }

        /// <summary>
        /// Hoists the children of fractional addition and multiplication.
        ///
        /// These operations are not fully associative but it is still legal
        /// to hoist prefixes. That is, it is still legal to rewrite
        /// op(a, b, c, d) as With(x:op(a, b, c), op(x, d)).
        /// This process can occur repeatedly for each prefix
        /// of hoisted children, so that each child is hoisted as far up the stack as possible.
        /// </summary>
        private void PostVisitFractional(BndVariadicOpNode bnd)
        {
            var children = Pop(bnd.ChildCount);

            BitSet scopeRefs = default;
            foreach (var c in children)
                scopeRefs |= c.ScopeRefs;
            scopeRefs = scopeRefs.ClearAtAndAbove(ScopeDepth);
            GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            Validation.Assert(inhibit == IsInhibited(bnd));

            ArgTuple.Builder bldrGrp = null;
            BitSet inv = default;
            int sizeGrp = 0;
            int hoistGrp = int.MinValue;
            bool prohibitGrp = false;
            // Keep track of the largest hoisting destination. If
            // we encounter a larger destination than previously seen,
            // we can hoist the prefix of children to the previous max
            // destination.
            for (int ichild = 0; ichild < children.Length; ichild++)
            {
                Validation.Assert(sizeGrp <= ichild);
                Validation.Assert(bldrGrp != null || sizeGrp == ichild);
                Validation.Assert(bldrGrp == null || sizeGrp == bldrGrp.Count);
                var info = children[ichild];
                var child = info.Bnd;
                var hoistBaseChild = info.HoistDstBase;
                var inhibitChild = info.Inhibit;
                prohibitGrp |= info.Prohibit;

                if (ichild == 0 && children.Length > 1 && inhibitChild && hoistBaseChild < children[1].HoistDstBase)
                    hoistBaseChild = children[1].HoistDstBase;
                if (info.Rewritten)
                    bldrGrp ??= MakeBuilder(ichild, children, false);

                if (hoistBaseChild <= hoistGrp)
                {
                    if (hoistBaseChild < hoistGrp && !inhibitChild)
                    {
                        bldrGrp ??= MakeBuilder(ichild, children, false);
                        child = MakeHoist(child, hoistBaseChild);
                    }
                    bldrGrp?.Add(child);
                    if (bnd.Inverted.TestBit(ichild))
                        inv = inv.SetBit(sizeGrp);
                    sizeGrp++;
                    continue;
                }

                if (!prohibitGrp && sizeGrp > 0)
                {
                    bldrGrp ??= MakeBuilder(ichild, children, false);
                    // Now that we've come across a larger destination,
                    // hoist the previous prefix children.
                    var bndHoist = bldrGrp.Count == 1 && inv.IsEmpty ?
                        bldrGrp[0] :
                        BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldrGrp.ToImmutable(), inv);

                    // When a group of children is hoisted, they are
                    // replaced by a single scope reference which should
                    // be accessible by the next group of children. Every
                    // group after the first should start with a reference
                    // to the previous group.
                    bldrGrp.Clear();
                    bldrGrp.Add(MakeHoist(bndHoist, hoistGrp));
                    inv = default;
                    sizeGrp = 1;
                }

                hoistGrp = hoistBaseChild;
                bldrGrp?.Add(child);
                if (bnd.Inverted.TestBit(ichild))
                    inv = inv.SetBit(sizeGrp);
                sizeGrp++;
            }

            Validation.Assert(hoistGrp == hoistDstBase);
            BoundNode final = bnd;
            if (bldrGrp != null)
                final = BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldrGrp.ToImmutable(), inv);
            Validation.Assert(final.Type == bnd.Type);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        /// <summary>
        /// Hoists the children of an associative but not commutative operation,
        /// such as sequence and string concatenation.
        ///
        /// If a consecutive sequence of children can be hoisted, then they are
        /// spliced from the original node's children and replaced with a single
        /// scope reference. This process can occur repeatedly for each subsequence
        /// of hoisted children, so that each child is hoisted as far up the stack as possible.
        /// </summary>
        private void PostVisitSequential(BndVariadicOpNode bnd)
        {
            Validation.Assert(bnd.Inverted.IsEmpty);
            var children = Pop(bnd.ChildCount);

            BitSet scopeRefs = default;
            foreach (var c in children)
                scopeRefs |= c.ScopeRefs;
            scopeRefs = scopeRefs.ClearAtAndAbove(ScopeDepth);
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            Validation.Assert(inhibit == IsInhibited(bnd));

            ArgTuple.Builder bldrTop = null;
            ArgTuple.Builder bldrGrp = null;
            HashSet<int> hoistDsts = null;
            List<(BoundNode bnd, int hoistDst, bool leaf)> scratch = null;
            bool prohibitGrp = false;
            for (int imin = 0; imin < children.Length;)
            {
                var info = children[imin];
                var child = info.Bnd;
                var hoistBaseChild = info.HoistDstBase;
                prohibitGrp |= info.Prohibit;

                int ilim = imin + 1;
                if (!prohibitGrp && hoistBaseChild < hoistDst)
                {
                    if (hoistDsts == null)
                        hoistDsts = new HashSet<int>();
                    else
                        hoistDsts.Clear();
                    hoistDsts.Add(hoistBaseChild);

                    // See if we can hoist up consecutive entries together.
                    while (ilim < bnd.ChildCount && children[ilim].HoistDstBase < hoistDst && !children[ilim].Prohibit)
                    {
                        hoistDsts.Add(children[ilim].HoistDstBase);
                        ilim++;
                    }

                    if (ilim - imin > 1)
                    {
                        // Allocate a new list with the bound nodes and hoist destinations
                        // as a scratchpad. For each hoist destination, hoist up consecutive
                        // runs of nodes which can be hoisted to there, and then replace
                        // them in the scratchpad. The first entry in the pad will contain
                        // the scope reference node, and the rest are "removed".
                        if (scratch == null)
                            scratch = new List<(BoundNode bnd, int hoistDst, bool leaf)>(children.Length - imin);
                        else
                            scratch.Clear();
                        for (int i = imin; i < ilim; i++)
                        {
                            var info1 = children[i];
                            scratch.Add((info1.Bnd, info1.HoistDstBase, info1.Inhibit));
                        }

                        int iscratchLim = scratch.Count;
                        foreach (var dst in hoistDsts.OrderBy(i => i))
                        {
                            int ivDst = 0;
                            for (int iminSub = 0; iminSub < iscratchLim; iminSub++)
                            {
                                var (bndMin, dstMin, leaf) = scratch[iminSub];
                                if (dstMin <= dst)
                                {
                                    int ilimSub = iminSub + 1;
                                    if (ilimSub < iscratchLim && leaf)
                                        dstMin = scratch[ilimSub].hoistDst;
                                    while (ilimSub < iscratchLim && scratch[ilimSub].hoistDst <= dst)
                                        ilimSub++;

                                    // Hoist one or many nodes from the scratchpad.
                                    // Replace the first node in the scratchpad with the resulting
                                    // scope reference, and compress the rest of the scratchpad.
                                    int citem = ilimSub - iminSub;
                                    if (citem == 1)
                                        scratch[ivDst] = (leaf ? bndMin : MakeHoist(bndMin, dst), dst, true);
                                    else
                                    {
                                        if (bldrGrp == null)
                                            bldrGrp = ArgTuple.CreateBuilder(children.Length - imin);
                                        else
                                            bldrGrp.Clear();

                                        for (int i = iminSub; i < ilimSub; i++)
                                            bldrGrp.Add(scratch[i].bnd);

                                        var sub = BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldrGrp.ToImmutable(), default);
                                        scratch[ivDst] = (MakeHoist(sub, dst), dst, true);
                                        iminSub = ilimSub - 1;
                                    }
                                }
                                else if (ivDst < iminSub)
                                    scratch[ivDst] = scratch[iminSub];
                                ivDst++;
                            }
                            iscratchLim = ivDst;
                        }

                        Validation.Assert(iscratchLim == 1);
                        child = scratch[0].bnd;
                    }
                    else if (!info.Inhibit)
                        child = MakeHoist(child, hoistBaseChild);
                }

                if (bldrTop == null && (info.Rewritten || child != info.Bnd))
                    bldrTop = MakeBuilder(imin, children, false);
                if (bldrTop != null)
                    bldrTop.Add(child);
                imin = ilim;
            }

            BoundNode final = bnd;
            if (bldrTop != null)
                final = BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldrTop.ToImmutable(), default);
            Validation.Assert(final.Type == bnd.Type);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        /// <summary>
        /// Hoists the children of an associative and commutative operation,
        /// such as integer addition and multiplication.
        ///
        /// If multiple children can be hoisted, they are all removed
        /// from the node and replaced with a single scope reference.
        /// Regardless if one or many are hoisted, this scope reference
        /// is placed at the end of the node's children list.
        /// This process can occur repeatedly for each group
        /// of hoisted children, so that each child is hoisted as far up the stack as possible.
        /// </summary>
        private void PostVisitCommutative(BndVariadicOpNode bnd)
        {
            var children = Pop(bnd.ChildCount);

            BitSet scopeRefs = default;
            foreach (var c in children)
                scopeRefs |= c.ScopeRefs;
            scopeRefs = scopeRefs.ClearAtAndAbove(ScopeDepth);
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            Validation.Assert(inhibit == IsInhibited(bnd));

            // Note that if some args are being hoisted,
            // then it is worthwhile to hoist leaf nodes, etc, to the outer-most (smallest) dst.
            // Similarly, if there are multiple leaf nodes (but no other hoistable),
            // it may still make sense to hoist their aggregation.
            var cleaf = 0;
            var choist = 0;
            bool prohibitGrp = false;
            foreach (var info in children)
            {
                prohibitGrp |= info.Prohibit;
                if (info.HoistDst < hoistDst)
                    choist++;
                else if (info.HoistDstBase < hoistDst && !info.Prohibit)
                    cleaf++;
            }

            ArgTuple.Builder bldrTop = null;
            BitSet invTop = default;
            List<(int hoistDst, BoundNode bnd, bool inv, bool leaf)> argsHoist = null;
            // Pluck out args that will be hoisted and replace with a single
            // scope reference.
            for (int ichild = 0; ichild < children.Length; ichild++)
            {
                var info = children[ichild];
                var child = info.Bnd;
                var hoistDstChild = info.HoistDst;
                var hoistDstBaseChild = info.HoistDstBase;
                if (info.Rewritten && bldrTop == null)
                {
                    invTop = bnd.Inverted.ClearAtAndAbove(ichild);
                    bldrTop = MakeBuilder(ichild, children, false);
                }

                if (!prohibitGrp && (hoistDstChild < hoistDst || cleaf + choist > 1 && hoistDstBaseChild < hoistDst))
                {
                    bool leaf = false;
                    if (!(hoistDstChild < hoistDst))
                    {
                        hoistDstChild = hoistDstBaseChild;
                        leaf = true;
                    }
                    // Hoist all children to the same scope reference and place at the start.
                    if (bldrTop == null)
                    {
                        invTop = bnd.Inverted.ClearAtAndAbove(ichild);
                        bldrTop = MakeBuilder(ichild, children, false);
                    }
                    Util.Add(ref argsHoist, (hoistDstChild, child, bnd.Inverted.TestBit(ichild), leaf));
                }
                else if (bldrTop != null)
                {
                    if (bnd.Inverted.TestBit(ichild))
                        invTop = invTop.SetBit(bldrTop.Count);
                    bldrTop.Add(child);
                }
            }

            if (argsHoist != null)
            {
                Validation.AssertValue(bldrTop);
                Validation.Assert(argsHoist.Count > 0);
                BoundNode argTop;
                bool invHoist;
                if (argsHoist.Count == 1)
                {
                    var (dstArg, bndArg, invArg, leaf) = argsHoist[0];
                    Validation.Assert(!leaf);
                    argTop = MakeHoist(bndArg, dstArg);
                    invHoist = invArg;
                }
                else
                {
                    // Hoist as many children as possible. If many
                    // children go to the same hoist destination,
                    // make another BndVariadicOpNode.

                    // Use a stable sort.
                    argsHoist.QuadSort((c1, c2) => c1.hoistDst - c2.hoistDst);
                    // We don't want to hoist just a leaf and nothing else, so if
                    // it is hoisted by itself we can combine it with the next group.
                    // Note that only the first leaf has this issue, as the rest
                    // will be joined with the scope ref to the previous group.
                    var (dstCur, bndCur, invCur, leafCur) = argsHoist[0];
                    Validation.Assert(dstCur <= argsHoist[1].hoistDst);
                    if (leafCur)
                        dstCur = argsHoist[1].hoistDst;

                    var bldr = ArgTuple.CreateBuilder(argsHoist.Count);
                    bldr.Add(bndCur);
                    int hoistGrp = dstCur;
                    BitSet inv = invCur ? (BitSet)0x1 : default;
                    // Keep track of opt-ness for hoisted groups. Since hoisting
                    // happens left to right, and created scopes become elements of this
                    // processing level, opt can go from false to true but
                    // never true to false.
                    bool opt = bndCur.Type.IsOpt;
                    for (int iarg = 1; iarg < argsHoist.Count; iarg++)
                    {
                        (dstCur, bndCur, invCur, leafCur) = argsHoist[iarg];

                        Validation.Assert(dstCur >= hoistGrp);
                        if (dstCur > hoistGrp)
                        {
                            Validation.Assert(!inv.TestAtOrAbove(bldr.Count));
                            var typeGrp = !opt && bnd.Type.IsOpt ? bnd.Type.ToReq() : bnd.Type;
                            var bndHoistGrp = bldr.Count == 1 && inv.IsEmpty ? bldr[0] : BndVariadicOpNode.Create(typeGrp, bnd.Op, bldr.ToImmutable(), inv);
                            Validation.Assert(opt == bndHoistGrp.Type.IsOpt);

                            // When a group of children is hoisted, they are
                            // replaced by a single scope reference which should
                            // be accessible by the next group of children. Every
                            // group after the first should start with a reference
                            // to the previous group.
                            bldr.Clear();
                            var hst = MakeHoist(bndHoistGrp, hoistGrp);
                            Validation.Assert(opt == hst.Type.IsOpt);
                            bldr.Add(hst);
                            inv = default;
                        }

                        opt |= bndCur.Type.IsOpt;
                        if (invCur)
                            inv = inv.SetBit(bldr.Count);
                        bldr.Add(bndCur);
                        hoistGrp = dstCur;
                        Validation.Assert(opt == bldr.Any(b => b.Type.IsOpt));
                    }

                    Validation.Assert(bldr.Count > 1);
                    Validation.Assert(opt == bldr.Any(b => b.Type.IsOpt));
                    var type = !opt && bnd.Type.IsOpt ? bnd.Type.ToReq() : bnd.Type;
                    var bndHoist = BndVariadicOpNode.Create(type, bnd.Op, bldr.ToImmutable(), inv);
                    Validation.Assert(opt == bndHoist.Type.IsOpt);
                    argTop = MakeHoist(bndHoist, hoistGrp);
                    Validation.Assert(opt == argTop.Type.IsOpt);
                    invHoist = false;
                }

                bldrTop.Insert(0, argTop);
                invTop <<= 1;
                if (invHoist)
                    invTop = invTop.SetBit(0);
            }

            BoundNode final = bnd;
            if (bldrTop != null)
                final = BndVariadicOpNode.Create(bnd.Type, bnd.Op, bldrTop.ToImmutable(), invTop);
            Validation.Assert(final.Type == bnd.Type);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        [Conditional("DEBUG")]
        private void AssertSingleChild(HoistInfo child, int parentHoistDst)
        {
            Validation.Assert(child.HoistDst == parentHoistDst || child.HoistDstBase == parentHoistDst);
        }

        protected override void PostVisitImpl(BndGetFieldNode bnd, int idx)
        {
            var child = Pop();
            var hoistDst = GetHoistDst(bnd, child.ScopeRefs, out var hoistDstBase, out var inhibit);
            AssertSingleChild(child, hoistDst);
            var final = child.Rewritten ? BndGetFieldNode.Create(bnd.Name, child.Bnd) : bnd;
            Push(bnd, final, child.ScopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndGetSlotNode bnd, int idx)
        {
            var child = Pop();
            var hoistDst = GetHoistDst(bnd, child.ScopeRefs, out var hoistDstBase, out var inhibit);
            AssertSingleChild(child, hoistDst);
            var final = child.Rewritten ? BndGetSlotNode.Create(bnd.Slot, child.Bnd) : bnd;
            Push(bnd, final, child.ScopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndIdxTextNode bnd, int idx)
        {
            var index = Pop();
            var text = Pop();
            var scopeRefs = text.ScopeRefs | index.ScopeRefs;
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            BoundNode final = bnd;
            if (HoistChild(text, hoistDst, out var argTxt) | HoistChild(index, hoistDst, out var argIdx))
                final = BndIdxTextNode.Create(argTxt, argIdx, bnd.Modifier);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndIdxTensorNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndIdxTensorNode.Create(newArgs[0], newArgs.RemoveAt(0), bnd.Modifiers);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndIdxHomTupNode bnd, int idx)
        {
            var index = Pop();
            var tuple = Pop();
            var scopeRefs = tuple.ScopeRefs | index.ScopeRefs;
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            BoundNode final = bnd;
            if (HoistChild(tuple, hoistDst, out var argTup) | HoistChild(index, hoistDst, out var argIdx))
            {
                final = BndIdxHomTupNode.Create(argTup, argIdx, bnd.Modifier, out bool oor);
                Validation.Assert(!oor);
            }
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndTextSliceNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndTextSliceNode.Create(newArgs[0], bnd.Item, newArgs.RemoveAt(0));
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndTensorSliceNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndTensorSliceNode.Create(newArgs[0], bnd.Items, newArgs.RemoveAt(0));
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndTupleSliceNode bnd, int idx)
        {
            var tuple = Pop();
            var scopeRefs = tuple.ScopeRefs;
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            BoundNode final = bnd;
            if (HoistChild(tuple, hoistDst, out var argTup))
                final = BndTupleSliceNode.Create(argTup, bnd.Start, bnd.Step, bnd.Count);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        private void PostVisitCastCore(BndCastNode bnd)
        {
            var child = Pop();
            int hoistDst = GetHoistDst(bnd, child.ScopeRefs, out int hoistDstBase, out bool inhibit);
            AssertSingleChild(child, hoistDst);
            var final = child.Rewritten ? bnd.SetChild(child.Bnd, _host) : bnd;
            Push(bnd, final, child.ScopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndCastNumNode bnd, int idx)
        {
            PostVisitCastCore(bnd);
        }

        protected override void PostVisitImpl(BndCastRefNode bnd, int idx)
        {
            PostVisitCastCore(bnd);
        }

        protected override void PostVisitImpl(BndCastBoxNode bnd, int idx)
        {
            // Boxing allocates memory, so it's not cheap.
            PostVisitCastCore(bnd);
        }

        protected override void PostVisitImpl(BndCastOptNode bnd, int idx)
        {
            PostVisitCastCore(bnd);
        }

        protected override void PostVisitImpl(BndCastVacNode bnd, int idx)
        {
            PostVisitCastCore(bnd);
        }

        protected override void PostVisitImpl(BndModToRecNode bnd, int idx)
        {
            PostVisitCastCore(bnd);
        }

        protected override void PostVisitImpl(BndBinaryOpNode bnd, int idx)
        {
            var child1 = Pop();
            var child0 = Pop();
            var scopeRefs = child0.ScopeRefs | child1.ScopeRefs;
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            BoundNode final = bnd;
            if (HoistChild(child0, hoistDst, out var arg0) | HoistChild(child1, hoistDst, out var arg1))
                final = BndBinaryOpNode.Create(bnd.Type, bnd.Op, arg0, arg1);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndCompareNode bnd, int idx)
        {
            // REVIEW: Perhaps split the comparisons like BndVariadicOpNode.
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndCompareNode.Create(bnd.Ops, newArgs);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndIfNode bnd, int idx)
        {
            var falseValue = Pop();
            var trueValue = Pop();
            var condValue = Pop();
            var scopeRefs = condValue.ScopeRefs | trueValue.ScopeRefs | falseValue.ScopeRefs;
            var hoistDst = GetHoistDst(bnd, scopeRefs, out var hoistDstBase, out var inhibit);
            BoundNode final = bnd;
            if (HoistChild(condValue, hoistDst, out var newCond) |
                HoistChild(trueValue, hoistDst, out var newTrue) |
                HoistChild(falseValue, hoistDst, out var newFalse))
            {
                final = BndIfNode.Create(newCond, newTrue, newFalse);
            }
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndSequenceNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndSequenceNode.Create(bnd.Type, newArgs);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndTensorNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndTensorNode.Create(bnd.Type, newArgs, bnd.Shape);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndTupleNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndTupleNode.Create(newArgs, bnd.Type);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndRecordNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var newArgs))
                final = BndRecordNode.Create(bnd.Type, bnd.Items.MapValues(newArgs), bnd.NameHints);
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }

        protected override void PostVisitImpl(BndModuleNode bnd, int idx)
        {
            var children = Pop(bnd.ChildCount);
            BoundNode final = bnd;
            if (HoistChildren(bnd, children, out var scopeRefs, out var hoistDstBase, out var inhibit, out var args))
            {
                Validation.Assert(args.Length == bnd.Externals.Length + bnd.Items.Length);
                var exts = ArgTuple.Empty;
                int split = bnd.Externals.Length;
                if (split > 0)
                {
                    exts = args.RemoveTail(split);
                    args = args.RemoveMinLim(0, split);
                }
                final = bnd.SetItems(exts, args);
            }
            Push(bnd, final, scopeRefs, hoistDstBase, inhibit);
        }
    }
}
