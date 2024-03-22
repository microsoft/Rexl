// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// Bound node kind. Note that these are also used as bit positions, so should be kept small
/// (all less than 64).
/// </summary>
public enum BndNodeKind
{
    /// <summary>
    /// No node should ever have this as its kind.
    /// </summary>
    Invalid,

    Error,
    MissingValue,

    /// <summary>
    /// Indicates a bound namespace. Except in error cases, this is temporary, only
    /// occurring during binding. When the containing DottedNameNode is bound, this
    /// is replaced by the child, either another Namespace or a Global.
    /// </summary>
    Namespace,

    // Constants.
    Null,
    Default,
    Int,
    Flt,
    Str,

    /// <summary>
    /// Compound structured constant, eg, a sequence, record, tensor, etc.
    /// This is never produced directly by the binder, only by further
    /// tree reduction, in particular by partial evaluation of a module.
    /// The node class is abstract, since the actual node typically
    /// has a dependence on a type manager and/or code generator, which
    /// are not bind-level concepts.
    /// </summary>
    CmpConst,

    This,
    Global,
    FreeVar,
    ArgScopeRef,
    IndScopeRef,
    GetField,
    GetSlot,
    IdxText,
    IdxTensor,
    IdxHomTup,
    TextSlice,
    TensorSlice,
    TupleSlice,

    // Casts.
    CastNum,
    CastRef,
    CastBox,
    CastOpt,
    CastVac,

    BinaryOp,
    VariadicOp,
    Compare,
    If,
    Sequence,
    Tensor,
    Tuple,
    Record,
    Module,
    ModToRec,

    /// <summary>
    /// A pure invocation of a function. Note that a function may support both pure and
    /// volatile invocations.
    /// REVIEW: Change to CallPure in a separate PR.
    /// </summary>
    Call,

    /// <summary>
    /// A volatile invocation of a function. The result from a volatile invocation may depend on
    /// external state that is not contained in the arguments. For example, the current date/time
    /// or the current temperature at a particular zip code. As with all function invocations,
    /// there are no side effects.
    /// </summary>
    CallVolatile,

    /// <summary>
    /// An invocation of a procedure. A procedure may be non-repeatable and/or have side effects.
    /// Also, they can produce any number of "results".
    /// </summary>
    CallProcedure,

    GroupBy,
    SetFields,
    ModuleProjection,

    // This isn't a valid kind. It reserves a slot in the mask for whether the node owns any scopes.
    _ScopeOwner,
}

[Flags]
public enum BndNodeKindMask : ulong
{
    Error = 1UL << BndNodeKind.Error,
    MissingValue = 1UL << BndNodeKind.MissingValue,
    Namespace = 1UL << BndNodeKind.Namespace,
    Null = 1UL << BndNodeKind.Null,
    Default = 1UL << BndNodeKind.Default,
    Int = 1UL << BndNodeKind.Int,
    Flt = 1UL << BndNodeKind.Flt,
    Str = 1UL << BndNodeKind.Str,
    CmpConst = 1UL << BndNodeKind.CmpConst,
    This = 1UL << BndNodeKind.This,
    Global = 1UL << BndNodeKind.Global,
    FreeVar = 1UL << BndNodeKind.FreeVar,
    ArgScopeRef = 1UL << BndNodeKind.ArgScopeRef,
    IndScopeRef = 1UL << BndNodeKind.IndScopeRef,
    GetField = 1UL << BndNodeKind.GetField,
    GetSlot = 1UL << BndNodeKind.GetSlot,
    IdxText = 1UL << BndNodeKind.IdxText,
    IdxTensor = 1UL << BndNodeKind.IdxTensor,
    IdxHomTup = 1UL << BndNodeKind.IdxHomTup,
    TextSlice = 1UL << BndNodeKind.TextSlice,
    TensorSlice = 1UL << BndNodeKind.TensorSlice,
    TupleSlice = 1UL << BndNodeKind.TupleSlice,
    CastNum = 1UL << BndNodeKind.CastNum,
    CastRef = 1UL << BndNodeKind.CastRef,
    CastBox = 1UL << BndNodeKind.CastBox,
    CastOpt = 1UL << BndNodeKind.CastOpt,
    CastVac = 1UL << BndNodeKind.CastVac,
    BinaryOp = 1UL << BndNodeKind.BinaryOp,
    VariadicOp = 1UL << BndNodeKind.VariadicOp,
    Compare = 1UL << BndNodeKind.Compare,
    If = 1UL << BndNodeKind.If,
    Sequence = 1UL << BndNodeKind.Sequence,
    Tensor = 1UL << BndNodeKind.Tensor,
    Tuple = 1UL << BndNodeKind.Tuple,
    Record = 1UL << BndNodeKind.Record,
    Module = 1UL << BndNodeKind.Module,
    ModToRec = 1UL << BndNodeKind.ModToRec,
    Call = 1UL << BndNodeKind.Call,
    CallVolatile = 1UL << BndNodeKind.CallVolatile,
    CallProcedure = 1UL << BndNodeKind.CallProcedure,
    GroupBy = 1UL << BndNodeKind.GroupBy,
    SetFields = 1UL << BndNodeKind.SetFields,
    ModuleProjection = 1UL << BndNodeKind.ModuleProjection,

    // Whether any scopes are owned.
    ScopeOwner = 1UL << BndNodeKind._ScopeOwner,
}

partial class BndErrorNode
{
    public override BndNodeKind Kind => BndNodeKind.Error;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndMissingValueNode
{
    public override BndNodeKind Kind => BndNodeKind.MissingValue;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndNamespaceNode
{
    public override BndNodeKind Kind => BndNodeKind.Namespace;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndNullNode
{
    public override BndNodeKind Kind => BndNodeKind.Null;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndDefaultNode
{
    public override BndNodeKind Kind => BndNodeKind.Default;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndIntNode
{
    public override BndNodeKind Kind => BndNodeKind.Int;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndFltNode
{
    public override BndNodeKind Kind => BndNodeKind.Flt;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndStrNode
{
    public override BndNodeKind Kind => BndNodeKind.Str;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndCmpConstNode
{
    public override BndNodeKind Kind => BndNodeKind.CmpConst;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndThisNode
{
    public override BndNodeKind Kind => BndNodeKind.This;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndGlobalNode
{
    public override BndNodeKind Kind => BndNodeKind.Global;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndFreeVarNode
{
    public override BndNodeKind Kind => BndNodeKind.FreeVar;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}
partial class BndScopeRefNode
{
    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        visitor.Visit(this, idx);
        return idx + 1;
    }
}

partial class BndGetFieldNode
{
    public override BndNodeKind Kind => BndNodeKind.GetField;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndGetSlotNode
{
    public override BndNodeKind Kind => BndNodeKind.GetSlot;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndIdxTextNode
{
    public override BndNodeKind Kind => BndNodeKind.IdxText;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndIdxTensorNode
{
    public override BndNodeKind Kind => BndNodeKind.IdxTensor;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndIdxHomTupNode
{
    public override BndNodeKind Kind => BndNodeKind.IdxHomTup;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndTextSliceNode
{
    public override BndNodeKind Kind => BndNodeKind.TextSlice;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndTensorSliceNode
{
    public override BndNodeKind Kind => BndNodeKind.TensorSlice;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndTupleSliceNode
{
    public override BndNodeKind Kind => BndNodeKind.TupleSlice;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCastNumNode
{
    public override BndNodeKind Kind => BndNodeKind.CastNum;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCastRefNode
{
    public override BndNodeKind Kind => BndNodeKind.CastRef;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCastBoxNode
{
    public override BndNodeKind Kind => BndNodeKind.CastBox;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCastOptNode
{
    public override BndNodeKind Kind => BndNodeKind.CastOpt;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCastVacNode
{
    public override BndNodeKind Kind => BndNodeKind.CastVac;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndBinaryOpNode
{
    public override BndNodeKind Kind => BndNodeKind.BinaryOp;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndVariadicOpNode
{
    public override BndNodeKind Kind => BndNodeKind.VariadicOp;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCompareNode
{
    public override BndNodeKind Kind => BndNodeKind.Compare;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndIfNode
{
    public override BndNodeKind Kind => BndNodeKind.If;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndSequenceNode
{
    public override BndNodeKind Kind => BndNodeKind.Sequence;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndTensorNode
{
    public override BndNodeKind Kind => BndNodeKind.Tensor;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndTupleNode
{
    public override BndNodeKind Kind => BndNodeKind.Tuple;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndRecordNode
{
    public override BndNodeKind Kind => BndNodeKind.Record;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndModuleNode
{
    public override BndNodeKind Kind => BndNodeKind.Module;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndModToRecNode
{
    public override BndNodeKind Kind => BndNodeKind.ModToRec;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndCallNode
{
    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndGroupByNode
{
    public override BndNodeKind Kind => BndNodeKind.GroupBy;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndSetFieldsNode
{
    public override BndNodeKind Kind => BndNodeKind.SetFields;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}
partial class BndModuleProjectionNode
{
    public override BndNodeKind Kind => BndNodeKind.ModuleProjection;

    public sealed override int Accept(BoundTreeVisitor visitor, int idx)
    {
        Validation.BugCheckValue(visitor, nameof(visitor));
        if (visitor.PreVisit(this, idx))
        {
            int cur = AcceptChildren(visitor, idx + 1);
            Validation.Assert(cur == idx + NodeCount);
            visitor.PostVisit(this, idx);
        }
        return idx + NodeCount;
    }
}

partial class BoundTreeVisitor
{
    // Visit methods for leaf node types.
    public void Visit(BndErrorNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndMissingValueNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndNamespaceNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndNullNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndDefaultNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndIntNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndFltNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndStrNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndCmpConstNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndThisNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndGlobalNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndFreeVarNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }
    public void Visit(BndScopeRefNode bnd, int idx) { if (Enter(bnd, idx)) { VisitImpl(bnd, idx); Leave(bnd, idx); } }

    protected abstract void VisitImpl(BndErrorNode bnd, int idx);
    protected abstract void VisitImpl(BndMissingValueNode bnd, int idx);
    protected abstract void VisitImpl(BndNamespaceNode bnd, int idx);
    protected abstract void VisitImpl(BndNullNode bnd, int idx);
    protected abstract void VisitImpl(BndDefaultNode bnd, int idx);
    protected abstract void VisitImpl(BndIntNode bnd, int idx);
    protected abstract void VisitImpl(BndFltNode bnd, int idx);
    protected abstract void VisitImpl(BndStrNode bnd, int idx);
    protected abstract void VisitImpl(BndCmpConstNode bnd, int idx);
    protected abstract void VisitImpl(BndThisNode bnd, int idx);
    protected abstract void VisitImpl(BndGlobalNode bnd, int idx);
    protected abstract void VisitImpl(BndFreeVarNode bnd, int idx);
    protected abstract void VisitImpl(BndScopeRefNode bnd, int idx);

    // Visit methods for non-leaf node types.
    // If PreVisit returns true, the children are visited and PostVisit is called.
    public bool PreVisit(BndGetFieldNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndGetSlotNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndIdxTextNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndIdxTensorNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndIdxHomTupNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndTextSliceNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndTensorSliceNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndTupleSliceNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCastNumNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCastRefNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCastBoxNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCastOptNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCastVacNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndBinaryOpNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndVariadicOpNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCompareNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndIfNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndSequenceNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndTensorNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndTupleNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndRecordNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndModuleNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndModToRecNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndCallNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndGroupByNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndSetFieldsNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }
    public bool PreVisit(BndModuleProjectionNode bnd, int idx) { if (!Enter(bnd, idx)) return false; bool res = PreVisitImpl(bnd, idx); if (!res) Leave(bnd, idx); return res; }

    protected virtual bool PreVisitImpl(BndGetFieldNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndGetSlotNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndIdxTextNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndIdxTensorNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndIdxHomTupNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndTextSliceNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndTensorSliceNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndTupleSliceNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCastNumNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCastRefNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCastBoxNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCastOptNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCastVacNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndBinaryOpNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndVariadicOpNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCompareNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndIfNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndSequenceNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndTensorNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndTupleNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndRecordNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndModuleNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndModToRecNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndCallNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndGroupByNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndSetFieldsNode bnd, int idx) => PreVisitCore(bnd, idx);
    protected virtual bool PreVisitImpl(BndModuleProjectionNode bnd, int idx) => PreVisitCore(bnd, idx);

    public void PostVisit(BndGetFieldNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndGetSlotNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndIdxTextNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndIdxTensorNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndIdxHomTupNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndTextSliceNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndTensorSliceNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndTupleSliceNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCastNumNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCastRefNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCastBoxNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCastOptNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCastVacNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndBinaryOpNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndVariadicOpNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCompareNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndIfNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndSequenceNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndTensorNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndTupleNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndRecordNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndModuleNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndModToRecNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndCallNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndGroupByNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndSetFieldsNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }
    public void PostVisit(BndModuleProjectionNode bnd, int idx) { PostVisitImpl(bnd, idx); Leave(bnd, idx); }

    protected abstract void PostVisitImpl(BndGetFieldNode bnd, int idx);
    protected abstract void PostVisitImpl(BndGetSlotNode bnd, int idx);
    protected abstract void PostVisitImpl(BndIdxTextNode bnd, int idx);
    protected abstract void PostVisitImpl(BndIdxTensorNode bnd, int idx);
    protected abstract void PostVisitImpl(BndIdxHomTupNode bnd, int idx);
    protected abstract void PostVisitImpl(BndTextSliceNode bnd, int idx);
    protected abstract void PostVisitImpl(BndTensorSliceNode bnd, int idx);
    protected abstract void PostVisitImpl(BndTupleSliceNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCastNumNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCastRefNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCastBoxNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCastOptNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCastVacNode bnd, int idx);
    protected abstract void PostVisitImpl(BndBinaryOpNode bnd, int idx);
    protected abstract void PostVisitImpl(BndVariadicOpNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCompareNode bnd, int idx);
    protected abstract void PostVisitImpl(BndIfNode bnd, int idx);
    protected abstract void PostVisitImpl(BndSequenceNode bnd, int idx);
    protected abstract void PostVisitImpl(BndTensorNode bnd, int idx);
    protected abstract void PostVisitImpl(BndTupleNode bnd, int idx);
    protected abstract void PostVisitImpl(BndRecordNode bnd, int idx);
    protected abstract void PostVisitImpl(BndModuleNode bnd, int idx);
    protected abstract void PostVisitImpl(BndModToRecNode bnd, int idx);
    protected abstract void PostVisitImpl(BndCallNode bnd, int idx);
    protected abstract void PostVisitImpl(BndGroupByNode bnd, int idx);
    protected abstract void PostVisitImpl(BndSetFieldsNode bnd, int idx);
    protected abstract void PostVisitImpl(BndModuleProjectionNode bnd, int idx);
}

partial class NoopBoundTreeVisitor
{
    protected override void VisitImpl(BndErrorNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndMissingValueNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndNamespaceNode bnd, int idx) => VisitCore(bnd, idx);
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

    protected override void PostVisitImpl(BndGetFieldNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndGetSlotNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIdxTextNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIdxTensorNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIdxHomTupNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTextSliceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTensorSliceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTupleSliceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastNumNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastRefNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastBoxNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastOptNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastVacNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndBinaryOpNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndVariadicOpNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCompareNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIfNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndSequenceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTensorNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTupleNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndRecordNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndModuleNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndModToRecNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCallNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndGroupByNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndSetFieldsNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndModuleProjectionNode bnd, int idx) => PostVisitCore(bnd, idx);
}

partial class NoopScopedBoundTreeVisitor
{
    protected override void VisitImpl(BndErrorNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndMissingValueNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndNamespaceNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndNullNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndDefaultNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndIntNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndFltNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndStrNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndCmpConstNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndThisNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndGlobalNode bnd, int idx) => VisitCore(bnd, idx);
    protected override void VisitImpl(BndFreeVarNode bnd, int idx) => VisitCore(bnd, idx);

    protected override void PostVisitImpl(BndGetFieldNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndGetSlotNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIdxTextNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIdxTensorNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIdxHomTupNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTextSliceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTensorSliceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTupleSliceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastNumNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastRefNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastBoxNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastOptNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCastVacNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndBinaryOpNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndVariadicOpNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCompareNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndIfNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndSequenceNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTensorNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndTupleNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndRecordNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndModuleNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndModToRecNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndCallNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndGroupByNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndSetFieldsNode bnd, int idx) => PostVisitCore(bnd, idx);
    protected override void PostVisitImpl(BndModuleProjectionNode bnd, int idx) => PostVisitCore(bnd, idx);
}
