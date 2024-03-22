// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// Streaming data analysis.
/// </summary>
public static class StreamAnalysis
{
    /// <summary>
    /// Analyzes <paramref name="bnd"/> for eager use of the global with the given <paramref name="name"/>.
    /// </summary>
    public static BoundNode FindEagerStreamUse(BoundNode bnd, NPath name)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        Validation.BugCheckParam(!name.IsRoot, nameof(name));

        if (!bnd.IsProcCall)
            return null;
        if ((bnd.AllKinds & BndNodeKindMask.Global) == 0)
            return null;

        return SeqAggVtor.Run(name, bnd);
    }

    /// <summary>
    /// The visitor that does the analysis. A node is "tainted" if it may contain information
    /// from the stream. Note that the type of a tainted node must contain a sequence type but
    /// need not be a sequence type itself. Eg, it could be a tuple or record containing the
    /// stream in a slot or field. As soon as a tainted node is recognized to be eagerly processed,
    /// this considers that node as an eager use of the stream and no further anaylsis is performed.
    /// </summary>
    private sealed class SeqAggVtor : NoopScopedBoundTreeVisitor
    {
        private readonly NPath _name;

        /// <summary>
        /// Each expr pushes a bool indicating whether or not it is tainted. The bound node is
        /// useful for debugging but not really needed otherwise.
        /// </summary>
        private readonly List<(BoundNode bnd, bool tainted)> _stack;

        /// <summary>
        /// These are the scopes that are currently active. This is used to detect invalid uses of a scope
        /// (nested/overlapping declarations of the same <see cref="ArgScope"/> instance).
        /// </summary>
        private readonly HashSet<ArgScope> _activeScopes;

        /// <summary>
        /// A scope that aliases a tainted node is considered tainted. Since these are removed
        /// when the scope is popped, we don't need to worry about multiple declarations.
        /// </summary>
        private readonly HashSet<ArgScope> _taintedScopes;

        /// <summary>
        /// The set of tainted node indices.
        /// </summary>
        private readonly HashSet<int> _taintedNodes;

        /// <summary>
        /// This is considered a violating node.
        /// </summary>
        private BoundNode _dirty;

        /// <summary>
        /// All nodes by index.
        /// </summary>
        private readonly BoundNode[] _nodes;

        public SeqAggVtor(NPath name, BoundNode top)
        {
            Validation.Assert(!name.IsRoot);
            _name = name;
            _stack = new();
            _activeScopes = new();
            _taintedScopes = new();
            _taintedNodes = new();
            _nodes = new BoundNode[top.NodeCount];
        }

        public static BoundNode Run(NPath name, BoundNode top)
        {
            var vtor = new SeqAggVtor(name, top);
            int num = top.Accept(vtor, 0);
            Validation.Assert(num == top.NodeCount);
            Validation.Assert(vtor._stack.Count == 1);
            return vtor._dirty;
        }

        protected override bool Enter(BoundNode bnd, int idx)
        {
            Validation.AssertValue(bnd);
            Validation.AssertIndex(idx, _nodes.Length);
            Validation.Assert(_nodes[idx] == null);
            _nodes[idx] = bnd;
            return base.Enter(bnd, idx);
        }

        protected override void Leave(BoundNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            base.Leave(bnd, idx);
        }

        [Conditional("DEBUG")]
        private void AssertIdx(BoundNode bnd, int idx)
        {
            Validation.AssertValue(bnd);
            Validation.AssertIndex(idx, _nodes.Length);
            Validation.Assert(_nodes[idx] == bnd);
        }

        private bool IsTaintable(DType type)
        {
            return type.HasSequence || type.HasGeneral;
        }

        private bool Taint(BoundNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            if (!IsTaintable(bnd.Type))
                return false;
            _taintedNodes.Add(idx);
            return true;
        }

        private void Push(BoundNode bnd, int idx)
        {
            AssertIdx(bnd, idx);
            _stack.Add((bnd, _taintedNodes.Contains(idx)));
        }

        /// <summary>
        /// Pops the top <paramref name="count"/> values and returns the logical "or" of them.
        /// </summary>
        private bool PopAgg(int count)
        {
            switch (count)
            {
            case 0:
                return false;
            case 1:
                return _stack.Pop().tainted;
            }

            int index = _stack.Count - count;
            Validation.AssertIndex(index, _stack.Count);
            bool res = false;
            for (int i = index; i < _stack.Count; i++)
            {
                if (_stack[i].tainted)
                {
                    res = true;
                    break;
                }
            }
            _stack.RemoveRange(index, _stack.Count - index);
            return res;
        }

        protected override void VisitCore(BndLeafNode bnd, int idx)
        {
            // The only leaf nodes that matter are globals and scope references. Any others
            // are not tainted.
            AssertIdx(bnd, idx);
            Validation.Assert(bnd.Kind != BndNodeKind.Global);
            Validation.Assert(bnd.Kind != BndNodeKind.ArgScopeRef);
            Validation.Assert(bnd.Kind != BndNodeKind.IndScopeRef);

            Push(bnd, idx);
        }

        protected override void VisitImpl(BndGlobalNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            if (_dirty == null && bnd.FullName == _name)
                Taint(bnd, idx);
            Push(bnd, idx);
        }

        protected override void PushScope(ArgScope scope, BndScopeOwnerNode owner, int idx, int slot, bool isArgValid)
        {
            Validation.AssertValue(scope);
            AssertIdx(owner, idx);
            Validation.Assert(isArgValid);

            Validation.BugCheck(_activeScopes.Add(scope), "Invalid scope usage");

            Validation.Assert(!_taintedScopes.Contains(scope));

            base.PushScope(scope, owner, idx, slot, isArgValid);

            if (_dirty != null)
                return;

            bool guard = false;
            switch (scope.Kind)
            {
            case ScopeKind.With:
            case ScopeKind.Iter:
                // Only aliasing scopes matter.
                break;

            case ScopeKind.Guard:
                // If this is a sequence value, testing for null means testing for empty!
                guard = true;
                break;

            default:
                return;
            }

            BoundNode arg;
            int cur;
            if (owner is BndCallNode call)
            {
                Validation.AssertIndex(slot, call.Args.Length);
                arg = call.Args[slot];
                cur = GetArgNodeIndex(idx + 1, call.Args, slot);
                AssertIdx(arg, cur);
            }
            else if (owner is BndSetFieldsNode bsf)
            {
                Validation.Assert(bsf.Source.Type.IsRecordXxx);
                Validation.Assert(bsf.Scope.Kind == ScopeKind.With);
                arg = bsf.Source;
                cur = idx + 1;
            }
            else if (owner is BndGroupByNode bgb)
            {
                Validation.Assert(bgb.Source.Type.IsSequence);
                // REVIEW: Is GroupBy guaranteed to be lazy? Note that group scopes
                // aren't really aliases, so perhaps this doesn't need to do anything.
                // arg = bgb.Source;
                return;
            }
            else
            {
                // If this fires, there is a new kind of scope owner, so fix this to deal with it.
                Validation.Assert(false);
                return;
            }
            AssertIdx(arg, cur);

            // If the associated arg is not tainted, there's no need for any action.
            if (!_taintedNodes.Contains(cur))
                return;

            if (guard && arg.Type.IsSequence)
            {
                // Testing for null means testing for empty, so can iterate.
                _dirty = arg;
                return;
            }

            Validation.Assert(IsTaintable(arg.Type));
            Validation.Assert(IsTaintable(scope.Type));
            _taintedScopes.Add(scope);
        }

        protected override void PopScope(ArgScope scope)
        {
            Validation.AssertValue(scope);
            _activeScopes.Remove(scope).Verify();
            _taintedScopes.Remove(scope);
            base.PopScope(scope);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope scope)
        {
            AssertIdx(bnd, idx);
            if (_taintedScopes.Contains(bnd.Scope))
                Taint(bnd, idx);
            Push(bnd, idx);
        }

        protected override int VisitNonNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            Validation.AssertValue(call);
            // Note: can't call AssertIdx(arg, cur) here since the recording happens in the base call.
            Validation.AssertValue(arg);

            int ret = base.VisitNonNestedCallArg(call, slot, arg, cur);
            VisitCallArgCore(call, slot, arg, cur);
            return ret;
        }

        protected override int VisitNestedCallArg(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            Validation.AssertValue(call);
            // Note: can't call AssertIdx(arg, cur) here since the recording happens in the base call.
            Validation.AssertValue(arg);

            int ret = base.VisitNestedCallArg(call, slot, arg, cur);
            VisitCallArgCore(call, slot, arg, cur);
            return ret;
        }

        private void VisitCallArgCore(BndCallNode call, int slot, BoundNode arg, int cur)
        {
            Validation.AssertValue(call);
            AssertIdx(arg, cur);
            Validation.Assert(_stack.Peek().bnd == arg);
            Validation.Assert(_stack.Peek().tainted == _taintedNodes.Contains(cur));

            // REVIEW: This can be too restrictive at times, but better to error in that
            // direction than the other.
            if (_taintedNodes.Contains(cur) && call.Traits.IsEagerSeq(slot))
            {
                _dirty = call;
                return;
            }
        }

        protected override bool PreVisitCore(BndParentNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            if (_dirty != null)
            {
                Push(bnd, idx);
                return false;
            }

            return base.PreVisitCore(bnd, idx);
        }

        protected override void PostVisitCore(BndParentNode bnd, int idx)
        {
            AssertIdx(bnd, idx);

            base.PostVisitCore(bnd, idx);
            if (PopAgg(bnd.ChildCount))
                Taint(bnd, idx);
            Push(bnd, idx);
        }
    }
}
