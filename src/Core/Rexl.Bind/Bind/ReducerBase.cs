// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using SliceTuple = Immutable.Array<SliceItemFlags>;

/// <summary>
/// Provides "host" callbacks for a reducer.
/// </summary>
public interface IReducerHost
{
    /// <summary>
    /// Invoked when <paramref name="bndOld"/> is mapped to a new node, namely <paramref name="bndNew"/>.
    /// This allows the host to update mappings from bound nodes to parse nodes, or other such info.
    /// The parameters should not be null and should have the same type.
    /// This returns <paramref name="bndNew"/> for convenience.
    /// </summary>
    BoundNode OnMapped(BoundNode bndOld, BoundNode bndNew);

    /// <summary>
    /// Invoked when <paramref name="bndNew"/> should be associated with <paramref name="bndOld"/>.
    /// This allows the host to update mappings from bound nodes to parse nodes, or other such info.
    /// The <paramref name="bndOld"/> parameter may be null but <paramref name="bndNew"/> should not be.
    /// Unlike with <see cref="OnMapped(BoundNode, BoundNode)"/> the nodes need not have the same same type.
    /// This returns <paramref name="bndNew"/> for convenience.
    /// </summary>
    BoundNode Associate(BoundNode bndOld, BoundNode bndNew);

    /// <summary>
    /// Invoked to report a warning situation (appropriate for an end user).
    /// </summary>
    void Warn(BoundNode bnd, StringId msg);
}

/// <summary>
/// A reducer host that does nothing.
/// </summary>
public abstract class ReducerHostBase : IReducerHost
{
    protected ReducerHostBase()
    {
    }

    public virtual BoundNode OnMapped(BoundNode bndOld, BoundNode bndNew)
    {
        Validation.BugCheckValue(bndOld, nameof(bndOld));
        Validation.BugCheckValue(bndNew, nameof(bndNew));
        Validation.BugCheckParam(bndOld.Type == bndNew.Type, nameof(bndNew));
        return bndNew;
    }

    public virtual BoundNode Associate(BoundNode bndOld, BoundNode bndNew)
    {
        Validation.BugCheckValueOrNull(bndOld);
        Validation.BugCheckValue(bndNew, nameof(bndNew));
        return bndNew;
    }

    public virtual void Warn(BoundNode bnd, StringId msg)
    {
    }
}

/// <summary>
/// A reducer host that does nothing.
/// </summary>
public sealed class NoopReducerHost : ReducerHostBase
{
    public static readonly NoopReducerHost Instance = new NoopReducerHost();

    private NoopReducerHost()
    {
    }
}

/// <summary>
/// Base class for reducing / rewriting a <see cref="BoundNode"/>.
/// </summary>
public abstract class ReducerBase : BoundTreeVisitor
{
    protected readonly IReducerHost _host;
    private readonly Stack<BoundNode> _stack;

    // The memoizations.
    private readonly bool _memoize;
    private readonly Dictionary<BoundNode, BoundNode> _memos;

    protected ReducerBase(IReducerHost host, bool memoize)
    {
        _host = host ?? NoopReducerHost.Instance;
        _stack = new Stack<BoundNode>();
        _memoize = memoize;
        _memos = _memoize ? new Dictionary<BoundNode, BoundNode>() : null;
    }

    /// <summary>
    /// Memoize the reduction from <paramref name="bndOld"/> to <paramref name="bndNew"/>.
    /// </summary>
    private void Memoize(BoundNode bndOld, BoundNode bndNew)
    {
        if (!_memoize)
            return;

        // REVIEW: Can't assert this since sub-classes can refine what base classes did.
        // Should come up with a guarantee....
        // Validation.Assert(!_memos.ContainsKey(bndOld));
        _memos[bndOld] = bndNew;
        if (bndOld != bndNew)
            _memos[bndNew] = bndNew;
    }

    protected virtual void OnMapped(BoundNode bndOld, BoundNode bndNew)
    {
        Validation.AssertValue(bndOld);
        Validation.AssertValue(bndNew);
        Validation.Assert(bndOld.Type == bndNew.Type);
        _host.OnMapped(bndOld, bndNew);
    }

    protected int StackDepth => _stack.Count;

    protected override bool Enter(BoundNode bnd, int idx)
    {
        if (_memoize && _memos.TryGetValue(bnd, out var res))
        {
            _stack.Push(res);
            return false;
        }
        return true;
    }

    protected override void Leave(BoundNode bnd, int idx)
    {
        Validation.Assert(!_memoize || _memos.ContainsKey(bnd));
    }

    /// <summary>
    /// Push a node <paramref name="bndNew"/> corresponding to an original node
    /// <paramref name="bndOld"/>. Note that these may be (and often are) the same
    /// node. Each visit is expected to push a single node (after popping inputs,
    /// if any).
    /// </summary>
    protected void Push(BoundNode bndOld, BoundNode bndNew)
    {
        Validation.AssertValue(bndOld);
        Validation.AssertValue(bndNew);

        if (bndOld == bndNew)
        {
            _stack.Push(bndOld);
            Memoize(bndOld, bndOld);
        }
        else
        {
            Validation.Assert(!bndOld.Equivalent(bndNew));
            Validation.Assert(bndOld.Type == bndNew.Type);
            OnMapped(bndOld, bndNew);
            _stack.Push(bndNew);
            Memoize(bndOld, bndNew);
        }
    }

    /// <summary>
    /// Pop an input to the current node being processed.
    /// </summary>
    protected BoundNode Pop()
    {
        Validation.Assert(_stack.Count > 0);
        return _stack.Pop();
    }

    /// <summary>
    /// Peek at the top of the stack.
    /// </summary>
    protected BoundNode Peek()
    {
        Validation.Assert(_stack.Count > 0);
        return _stack.Peek();
    }

    /// <summary>
    /// Reduce the given node and return the result.
    /// </summary>
    protected BoundNode Reduce(BoundNode bnd, ref int cur)
    {
        if (bnd == null)
            return bnd;

        int curOld = cur;
        cur = bnd.Accept(this, cur);
        Validation.Assert(cur == curOld + bnd.NodeCount);

        var res = Pop();
        Validation.Assert(res.Type == bnd.Type);
        Validation.Assert(bnd == res || !bnd.Equivalent(res));
        return res;
    }

    /// <summary>
    /// Reduce the given node and return true if it was changed.
    /// </summary>
    protected bool ReduceChild(ref BoundNode child, ref int cur)
    {
        if (child == null)
            return false;
        cur = child.Accept(this, cur);
        var res = Pop();
        if (child == res)
            return false;
        Validation.Assert(!child.Equivalent(res));
        child = res;
        return true;
    }

    /// <summary>
    /// Reduce each node in <paramref name="children"/>. If any are changed set
    /// <paramref name="children"/> to the new tuple and return true.
    /// </summary>
    protected bool ReduceChildren(ref ArgTuple children, ref int cur)
    {
        var bldr = Reduce(children, ref cur);
        if (bldr == null)
            return false;
        children = bldr.ToImmutable();
        return true;
    }

    /// <summary>
    /// Reduce each node in <paramref name="children"/>. If any are changed return
    /// non-null builder containing the reduced nodes.
    /// </summary>
    protected ArgTuple.Builder Reduce(ArgTuple children, ref int cur)
    {
        Validation.Assert(!children.IsDefault);

        ArgTuple.Builder bldr = null;
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child == null)
                continue;
            cur = child.Accept(this, cur);
            var res = Pop();
            if (child != res)
            {
                Validation.Assert(!child.Equivalent(res));
                bldr ??= children.ToBuilder();
                bldr[i] = res;
            }
        }
        Validation.Assert(bldr == null || !AreEquiv(bldr, children));
        return bldr;
    }

    // Currently used only for asserts.
    protected bool AreEquiv(ArgTuple.Builder bldr, ArgTuple args)
    {
        Validation.AssertValue(bldr);

        if (bldr.Count != args.Length)
            return false;
        for (int i = 0; i < args.Length; i++)
        {
            var arg0 = bldr[i];
            var arg1 = args[i];
            if (arg0 != arg1 && !arg0.Equivalent(arg1))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Reduce each node in <paramref name="children"/>. If any are changed set
    /// <paramref name="children"/> to the new map and return true.
    /// </summary>
    protected bool ReduceChildren(ref NamedItems children, ref int cur)
    {
        bool changed = false;
        foreach (var (name, child) in children.GetPairs())
        {
            if (child == null)
                continue;
            cur = child.Accept(this, cur);
            var res = Pop();
            if (child != res)
            {
                Validation.Assert(!child.Equivalent(res));
                changed = true;
                children = children.SetItem(name, res);
            }
        }
        return changed;
    }

    /// <summary>
    /// Default visit method for leaf nodes.
    /// </summary>
    protected virtual void VisitCore(BndLeafNode bnd, int idx)
    {
        Push(bnd, bnd);
    }

    protected override void VisitImpl(BndErrorNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndNamespaceNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndMissingValueNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndNullNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndDefaultNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndIntNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndFltNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndStrNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndCmpConstNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndThisNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndGlobalNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndFreeVarNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndScopeRefNode bnd, int idx) => VisitCore(bnd, idx);

    // Parent nodes with one or more ArgTuple are processed in PreVisit.
    // Other parent nodes are processed in PostVisit. In either case, the
    // PreVisitCore and PostVisitCore methods should not be called.

    /// <summary>
    /// Parent nodes with one or more ArgTuple are processed in PreVisit.
    /// Other parent nodes are processed in PostVisit. In either case, the
    /// PreVisitCore and PostVisitCore methods should not be called (they
    /// assert and throw).
    /// </summary>
    protected sealed override bool PreVisitCore(BndParentNode bnd, int idx)
    {
        throw Validation.BugExcept();
    }

    /// <summary>
    /// Parent nodes with one or more ArgTuple are processed in PreVisit.
    /// Other parent nodes are processed in PostVisit. In either case, the
    /// PreVisitCore and PostVisitCore methods should not be called (they
    /// assert and throw).
    /// </summary>
    protected void PostVisitCore(BndParentNode bnd, int idx)
    {
        throw Validation.BugExcept();
    }

    protected override bool PreVisitImpl(BndGetFieldNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var record = bnd.Record;
        if (ReduceChild(ref record, ref cur))
            result = BndGetFieldNode.Create(bnd.Name, record);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndGetFieldNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndGetSlotNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var tuple = bnd.Tuple;
        if (ReduceChild(ref tuple, ref cur))
            result = BndGetSlotNode.Create(bnd.Slot, tuple);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndGetSlotNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndIdxTextNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var text = bnd.Text;
        var index = bnd.Index;
        Validation.Assert(text.Type == DType.Text);
        Validation.Assert(index.Type == DType.I8Req);

        if (ReduceChild(ref text, ref cur) | ReduceChild(ref index, ref cur))
            result = BndIdxTextNode.Create(text, index, bnd.Modifier);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndIdxTextNode bnd, int idx) { PostVisitCore(bnd, idx); }

    protected override bool PreVisitImpl(BndIdxTensorNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var tensor = bnd.Tensor;
        var inds = bnd.Indices;
        if (ReduceChild(ref tensor, ref cur) | ReduceChildren(ref inds, ref cur))
            result = bnd.SetChildren(tensor, inds);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndIdxTensorNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndIdxHomTupNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var tup = bnd.Tuple;
        var index = bnd.Index;
        Validation.Assert(tup.Type.IsHomTuple());
        Validation.Assert(index.Type == DType.I8Req);
        Validation.Assert(!index.IsConstant);

        if (ReduceChild(ref tup, ref cur) | ReduceChild(ref index, ref cur))
        {
            result = BndIdxHomTupNode.Create(tup, index, bnd.Modifier, out bool oor);
            if (oor)
                _host.Warn(bnd, ErrorStrings.WrnHomTupleIndexOutOfRange);
        }
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndIdxHomTupNode bnd, int idx) { PostVisitCore(bnd, idx); }

    /// <summary>
    /// Reduce the given slice information.
    /// </summary>
    protected SliceItemFlags ReduceSlice(SliceItemFlags flags,
        ArgTuple valsSrc, ref ArgTuple.Builder valsNew, ref int ival, ref int cur)
    {
        int count = flags.GetCount();
        Validation.Assert(ival <= valsSrc.Length - count);

        BoundNode a, b;
        BoundNode start, stop, step;
        bool backStart, backStop, starStop;

        switch (count)
        {
        default:
            Validation.Assert(false);
            return flags;

        case 0:
            // This is a full range item, with no values.
            return flags;

        case 1:
            a = valsSrc[ival++];
            if (!ReduceChild(ref a, ref cur))
            {
                valsNew?.Add(a);
                return flags;
            }
            if (flags.IsRangeSlice())
            {
                if ((flags & SliceItemFlags.Start) != 0)
                {
                    Validation.Assert((flags & (SliceItemFlags.Stop | SliceItemFlags.Step)) == 0);
                    start = a;
                    stop = step = null;
                }
                else if ((flags & SliceItemFlags.Stop) != 0)
                {
                    Validation.Assert((flags & SliceItemFlags.Step) == 0);
                    stop = a;
                    start = step = null;
                }
                else
                {
                    Validation.Assert((flags & SliceItemFlags.Step) != 0);
                    step = a;
                    start = stop = null;
                }
                backStart = (flags & SliceItemFlags.StartBack) != 0;
                backStop = (flags & SliceItemFlags.StopBack) != 0;
                starStop = (flags & SliceItemFlags.StopStar) != 0;
                break;
            }
            if (flags.IsTupleSlice())
            {
                var tuple = a;
                bool backs = tuple.Type.TupleArity > 3;
                Validation.Assert(tuple.Type.TupleArity == (backs ? 5 : 3));
                starStop = false;

                if (tuple is BndTupleNode btn)
                {
                    if (!backs)
                    {
                        start = btn.Items[0];
                        stop = btn.Items[1];
                        step = btn.Items[2];
                        backStart = backStop = false;
                        break;
                    }
                    if (btn.Items[1].TryGetBool(out backStart) &&
                        btn.Items[3].TryGetBool(out backStop))
                    {
                        start = btn.Items[0];
                        stop = btn.Items[2];
                        step = btn.Items[4];
                        break;
                    }
                }
            }
            else
                Validation.Assert(flags.IsIndex());
            (valsNew ??= CreateBuilder(valsSrc, ival - count)).Add(a);
            return flags;

        case 2:
            Validation.Assert(flags.IsRangeSlice());
            a = valsSrc[ival++];
            b = valsSrc[ival++];
            if (!(ReduceChild(ref a, ref cur) | ReduceChild(ref b, ref cur)))
            {
                if (valsNew != null)
                {
                    valsNew.Add(a);
                    valsNew.Add(b);
                }
                return flags;
            }
            if ((flags & SliceItemFlags.Start) == 0)
            {
                Validation.Assert((flags & SliceItemFlags.Stop) != 0);
                Validation.Assert((flags & SliceItemFlags.Step) != 0);
                start = null;
                stop = a;
                step = b;
            }
            else if ((flags & SliceItemFlags.Stop) == 0)
            {
                Validation.Assert((flags & SliceItemFlags.Step) != 0);
                start = a;
                stop = null;
                step = b;
            }
            else
            {
                Validation.Assert((flags & SliceItemFlags.Step) == 0);
                start = a;
                stop = b;
                step = null;
            }
            backStart = (flags & SliceItemFlags.StartBack) != 0;
            backStop = (flags & SliceItemFlags.StopBack) != 0;
            starStop = (flags & SliceItemFlags.StopStar) != 0;
            break;

        case 3:
            Validation.Assert(flags.IsRangeSlice());
            Validation.Assert((flags & SliceItemFlags.Start) != 0);
            Validation.Assert((flags & SliceItemFlags.Stop) != 0);
            Validation.Assert((flags & SliceItemFlags.Step) != 0);
            start = valsSrc[ival++];
            stop = valsSrc[ival++];
            step = valsSrc[ival++];
            if (!(ReduceChild(ref start, ref cur) | ReduceChild(ref stop, ref cur) | ReduceChild(ref step, ref cur)))
            {
                if (valsNew != null)
                {
                    valsNew.Add(start);
                    valsNew.Add(stop);
                    valsNew.Add(step);
                }
                return flags;
            }
            backStart = (flags & SliceItemFlags.StartBack) != 0;
            backStop = (flags & SliceItemFlags.StopBack) != 0;
            starStop = (flags & SliceItemFlags.StopStar) != 0;
            break;
        }

        // Range case - either because the original is a range or we deconstructed a tuple.
        valsNew ??= CreateBuilder(valsSrc, ival - count);
        return SliceUtils.Add(valsNew, start, backStart, stop, backStop, starStop, step);

        // Called when we know we need a values builder. It creates a builder with the same
        // capacity as the source values and fills with the first count values.
        static ArgTuple.Builder CreateBuilder(ArgTuple src, int count)
        {
            Validation.Assert(count < src.Length);
            var bldr = ArgTuple.CreateBuilder(src.Length);
            for (int j = 0; j < count; j++)
                bldr.Add(src[j]);
            return bldr;
        }
    }

    protected override bool PreVisitImpl(BndTextSliceNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        int cur = idx + 1;
        var text = bnd.Text;
        bool redText = ReduceChild(ref text, ref cur);

        var valsSrc = bnd.Values;
        ArgTuple.Builder valsNew = null;
        int ival = 0;
        SliceItemFlags item = ReduceSlice(bnd.Item, valsSrc, ref valsNew, ref ival, ref cur);
        Validation.Assert(ival == valsSrc.Length);
        Validation.Assert(cur == idx + bnd.NodeCount);

        BoundNode result = bnd;
        if (valsNew == null)
        {
            Validation.Assert(item == bnd.Item);
            if (redText)
                result = BndTextSliceNode.Create(text, bnd.Item, bnd.Values);
        }
        else
        {
            Validation.Assert(item != bnd.Item | valsNew.Count == valsSrc.Length);
            result = BndTextSliceNode.Create(text, item, valsNew.ToImmutable());
        }

        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndTextSliceNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndTensorSliceNode bnd, int idx)
    {
        Validation.AssertValue(bnd);

        int cur = idx + 1;
        var tensor = bnd.Tensor;
        bool redTen = ReduceChild(ref tensor, ref cur);

        var items = bnd.Items;
        var valsSrc = bnd.Values;
        SliceTuple.Builder itemsNew = null;
        ArgTuple.Builder valsNew = null;
        int ival = 0;
        for (int iitem = 0; iitem < items.Length; iitem++)
        {
            var itemNew = ReduceSlice(items[iitem], valsSrc, ref valsNew, ref ival, ref cur);
            if (itemNew != items[iitem])
                (itemsNew ??= items.ToBuilder())[iitem] = itemNew;
        }
        Validation.Assert(ival == valsSrc.Length);
        Validation.Assert(cur == idx + bnd.NodeCount);

        if (valsNew == null)
        {
            Validation.Assert(itemsNew == null);
            if (!redTen)
                Push(bnd, bnd);
            else
                Push(bnd, bnd.SetTensor(tensor));
            return false;
        }

        if (itemsNew == null)
        {
            Validation.Assert(valsNew.Count == valsSrc.Length);
            Push(bnd, BndTensorSliceNode.Create(tensor, bnd.Items, valsNew.ToImmutable()));
        }
        else
            Push(bnd, BndTensorSliceNode.Create(tensor, itemsNew.ToImmutable(), valsNew.ToImmutable()));
        return false;
    }
    protected sealed override void PostVisitImpl(BndTensorSliceNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndTupleSliceNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var tuple = bnd.Tuple;
        if (ReduceChild(ref tuple, ref cur))
            result = BndTupleSliceNode.Create(tuple, bnd.Start, bnd.Step, bnd.Count);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndTupleSliceNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected virtual bool PreVisitCastCore(BndCastNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var child = bnd.Child;
        if (ReduceChild(ref child, ref cur))
            result = bnd.SetChild(child, _host);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }

    protected override bool PreVisitImpl(BndCastNumNode bnd, int idx) => PreVisitCastCore(bnd, idx);
    protected sealed override void PostVisitImpl(BndCastNumNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndCastRefNode bnd, int idx) => PreVisitCastCore(bnd, idx);
    protected sealed override void PostVisitImpl(BndCastRefNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndCastBoxNode bnd, int idx) => PreVisitCastCore(bnd, idx);
    protected sealed override void PostVisitImpl(BndCastBoxNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndCastOptNode bnd, int idx) => PreVisitCastCore(bnd, idx);
    protected sealed override void PostVisitImpl(BndCastOptNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndCastVacNode bnd, int idx) => PreVisitCastCore(bnd, idx);
    protected sealed override void PostVisitImpl(BndCastVacNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndModToRecNode bnd, int idx) => PreVisitCastCore(bnd, idx);
    protected sealed override void PostVisitImpl(BndModToRecNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndBinaryOpNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        BoundNode arg0 = bnd.Arg0;
        BoundNode arg1 = bnd.Arg1;
        if (ReduceChild(ref arg0, ref cur) | ReduceChild(ref arg1, ref cur))
            result = BndBinaryOpNode.Create(bnd.Type, bnd.Op, arg0, arg1);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndBinaryOpNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndVariadicOpNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var args = bnd.Args;
        if (ReduceChildren(ref args, ref cur))
            result = BndVariadicOpNode.Create(bnd.Type, bnd.Op, args, bnd.Inverted);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndVariadicOpNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndCompareNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var args = bnd.Args;
        if (ReduceChildren(ref args, ref cur))
            result = BndCompareNode.Create(bnd.Ops, args);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndCompareNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndCallNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var args = bnd.Args;
        if (ReduceChildren(ref args, ref cur))
            result = bnd.SetArgs(args);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndCallNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndIfNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var condValue = bnd.CondValue;
        var trueValue = bnd.TrueValue;
        var falseValue = bnd.FalseValue;
        if (ReduceChild(ref condValue, ref cur) |
            ReduceChild(ref trueValue, ref cur) |
            ReduceChild(ref falseValue, ref cur))
        {
            result = BndIfNode.Create(condValue, trueValue, falseValue);
        }
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndIfNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndSequenceNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var items = bnd.Items;
        if (ReduceChildren(ref items, ref cur))
            result = BndSequenceNode.Create(bnd.Type, items);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndSequenceNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndTensorNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var items = bnd.Items;
        if (ReduceChildren(ref items, ref cur))
            result = BndTensorNode.Create(bnd.Type, items, bnd.Shape);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndTensorNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndTupleNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var items = bnd.Items;
        if (ReduceChildren(ref items, ref cur))
            result = BndTupleNode.Create(items, bnd.Type);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndTupleNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndRecordNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var items = bnd.Items;
        if (ReduceChildren(ref items, ref cur))
            result = BndRecordNode.Create(bnd.Type, items, bnd.NameHints);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndRecordNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndModuleNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var exts = bnd.Externals;
        var items = bnd.Items;
        if (ReduceChildren(ref exts, ref cur) | ReduceChildren(ref items, ref cur))
            result = bnd.SetItems(exts, items);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndModuleNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var source = bnd.Source;
        var keysPure = bnd.PureKeys;
        var keysKeep = bnd.KeepKeys;
        var maps = bnd.MapItems;
        var aggs = bnd.AggItems;
        if (ReduceChild(ref source, ref cur) |
            ReduceChildren(ref keysPure, ref cur) |
            ReduceChildren(ref keysKeep, ref cur) |
            ReduceChildren(ref maps, ref cur) |
            ReduceChildren(ref aggs, ref cur))
        {
            result = BndGroupByNode.Create(
                bnd.Type, source,
                bnd.ScopeForKeys, bnd.IndexForKeys, keysPure, keysKeep, bnd.KeysCi,
                bnd.ScopeForMaps, bnd.IndexForMaps, maps,
                bnd.ScopeForAggs, aggs);
        }
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndGroupByNode bnd, int idx) => PostVisitCore(bnd, idx);

    protected override bool PreVisitImpl(BndSetFieldsNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var source = bnd.Source;
        var adds = bnd.Additions;

        var changes = ReduceChild(ref source, ref cur);

        var scope = bnd.Scope;
        if (adds.Count > 0)
        {
            Validation.AssertValue(scope);
            Validation.Assert(scope.Kind == ScopeKind.With);
            changes |= ReduceChildren(ref adds, ref cur);
            changes |= DropSelfFields(bnd.Type, source, scope, ref adds);
        }

        if (changes)
            result = BndSetFieldsNode.Create(bnd.Type, source, scope, adds, bnd.NameHints);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndSetFieldsNode bnd, int idx) => PostVisitCore(bnd, idx);

    /// <summary>
    /// Drops fields that are assigned to themselves from the items in a <see cref="BndSetFieldsNode"/>.
    /// Returns true if there are any changes.
    /// </summary>
    protected bool DropSelfFields(DType typeDst, BoundNode source, ArgScope scope, ref NamedItems items)
    {
        Validation.Assert(typeDst.IsRecordReq);
        Validation.AssertValue(source);
        Validation.Assert(source.Type.IsRecordReq);
        Validation.AssertValue(scope);
        Validation.AssertValue(items);

        bool changes = false;
        var scope2 = (source as BndScopeRefNode)?.Scope;
        foreach (var (name, val) in items.GetPairs())
        {
            Validation.Assert(typeDst.TryGetNameType(name, out DType typeFld) & typeFld == val.Type);

            // REVIEW: Could also look for the case when source is a "constant" record and
            // the value matches the value in that constant record.

            if (val is not BndGetFieldNode bgf)
                continue;
            if (name != bgf.Name)
                continue;

            // Cases:
            // * The record is the same BoundNode as source.
            // * The record is a scope ref of our scope.
            // * The record is a scope ref of the scope referenced by source.
            if (bgf.Record == source)
            { }
            else if (bgf.Record is BndScopeRefNode bsrCur && (bsrCur.Scope == scope || bsrCur.Scope == scope2))
            { }
            else
                continue;

            changes = true;
            items = items.RemoveItem(name);
        }

        return changes;
    }

    protected override bool PreVisitImpl(BndModuleProjectionNode bnd, int idx)
    {
        Validation.AssertValue(bnd);
        BoundNode result = bnd;
        int cur = idx + 1;
        var mod = bnd.Module;
        var rec = bnd.Record;

        var changes = ReduceChild(ref mod, ref cur);

        var scope = bnd.Scope;
        Validation.AssertValue(scope);
        Validation.Assert(scope.Kind == ScopeKind.With);
        changes |= ReduceChild(ref rec, ref cur);

        if (changes)
            result = BndModuleProjectionNode.Create(mod, scope, rec);
        Validation.Assert(cur == idx + bnd.NodeCount);
        Push(bnd, result);
        return false;
    }
    protected sealed override void PostVisitImpl(BndModuleProjectionNode bnd, int idx) => PostVisitCore(bnd, idx);
}
