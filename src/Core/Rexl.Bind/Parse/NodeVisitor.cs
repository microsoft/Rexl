// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Parse;

/// <summary>
/// Parse node kind.
/// </summary>
public enum NodeKind
{
    StmtList,
    ExprList,
    SymList,
    SliceList,
    ExprStmt,
    BlockStmt,
    GotoStmt,
    LabelStmt,
    IfStmt,
    WhileStmt,
    ImportStmt,
    ExecuteStmt,
    NamespaceStmt,
    WithStmt,
    DefinitionStmt,
    FuncStmt,
    TaskCmdStmt,
    TaskProcStmt,
    TaskBlockStmt,
    UserProcStmt,
    Error,
    MissingValue,
    Paren,
    NullLit,
    BoolLit,
    NumLit,
    TextLit,
    Box,
    ItName,
    ThisName,
    FirstName,
    DottedName,
    MetaProp,
    GetIndex,
    UnaryOp,
    BinaryOp,
    InHas,
    Compare,
    SliceItem,
    Indexing,
    Call,
    VariableDecl,
    Directive,
    ValueSymDecl,
    FreeVarDecl,
    If,
    Record,
    Sequence,
    Tuple,
    RecordProjection,
    TupleProjection,
    ValueProjection,
    ModuleProjection,
    Module,
}

public sealed partial class GotoStmtNode
{
    public override NodeKind Kind => NodeKind.GotoStmt;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class LabelStmtNode
{
    public override NodeKind Kind => NodeKind.LabelStmt;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class ErrorNode
{
    public override NodeKind Kind => NodeKind.Error;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class MissingValueNode
{
    public override NodeKind Kind => NodeKind.MissingValue;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class NullLitNode
{
    public override NodeKind Kind => NodeKind.NullLit;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class BoolLitNode
{
    public override NodeKind Kind => NodeKind.BoolLit;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class NumLitNode
{
    public override NodeKind Kind => NodeKind.NumLit;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class TextLitNode
{
    public override NodeKind Kind => NodeKind.TextLit;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class BoxNode
{
    public override NodeKind Kind => NodeKind.Box;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class ItNameNode
{
    public override NodeKind Kind => NodeKind.ItName;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class ThisNameNode
{
    public override NodeKind Kind => NodeKind.ThisName;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class FirstNameNode
{
    public override NodeKind Kind => NodeKind.FirstName;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
public sealed partial class MetaPropNode
{
    public override NodeKind Kind => NodeKind.MetaProp;

    public override void Accept(RexlTreeVisitor visitor)
    {
        Validation.AssertValue(visitor);
        visitor.Visit(this);
    }
}
partial class StmtListNode
{
    public override NodeKind Kind => NodeKind.StmtList;
}
partial class ExprListNode
{
    public override NodeKind Kind => NodeKind.ExprList;
}
partial class SymListNode
{
    public override NodeKind Kind => NodeKind.SymList;
}
partial class SliceListNode
{
    public override NodeKind Kind => NodeKind.SliceList;
}
partial class ExprStmtNode
{
    public override NodeKind Kind => NodeKind.ExprStmt;
}
partial class BlockStmtNode
{
    public override NodeKind Kind => NodeKind.BlockStmt;
}
partial class IfStmtNode
{
    public override NodeKind Kind => NodeKind.IfStmt;
}
partial class WhileStmtNode
{
    public override NodeKind Kind => NodeKind.WhileStmt;
}
partial class ImportStmtNode
{
    public override NodeKind Kind => NodeKind.ImportStmt;
}
partial class ExecuteStmtNode
{
    public override NodeKind Kind => NodeKind.ExecuteStmt;
}
partial class NamespaceStmtNode
{
    public override NodeKind Kind => NodeKind.NamespaceStmt;
}
partial class WithStmtNode
{
    public override NodeKind Kind => NodeKind.WithStmt;
}
partial class DefinitionStmtNode
{
    public override NodeKind Kind => NodeKind.DefinitionStmt;
}
partial class FuncStmtNode
{
    public override NodeKind Kind => NodeKind.FuncStmt;
}
partial class TaskCmdStmtNode
{
    public override NodeKind Kind => NodeKind.TaskCmdStmt;
}
partial class TaskProcStmtNode
{
    public override NodeKind Kind => NodeKind.TaskProcStmt;
}
partial class TaskBlockStmtNode
{
    public override NodeKind Kind => NodeKind.TaskBlockStmt;
}
partial class UserProcStmtNode
{
    public override NodeKind Kind => NodeKind.UserProcStmt;
}
partial class ParenNode
{
    public override NodeKind Kind => NodeKind.Paren;
}
partial class DottedNameNode
{
    public override NodeKind Kind => NodeKind.DottedName;
}
partial class GetIndexNode
{
    public override NodeKind Kind => NodeKind.GetIndex;
}
partial class UnaryOpNode
{
    public override NodeKind Kind => NodeKind.UnaryOp;
}
partial class BinaryOpNode
{
    public override NodeKind Kind => NodeKind.BinaryOp;
}
partial class InHasNode
{
    public override NodeKind Kind => NodeKind.InHas;
}
partial class CompareNode
{
    public override NodeKind Kind => NodeKind.Compare;
}
partial class SliceItemNode
{
    public override NodeKind Kind => NodeKind.SliceItem;
}
partial class IndexingNode
{
    public override NodeKind Kind => NodeKind.Indexing;
}
partial class CallNode
{
    public override NodeKind Kind => NodeKind.Call;
}
partial class VariableDeclNode
{
    public override NodeKind Kind => NodeKind.VariableDecl;
}
partial class DirectiveNode
{
    public override NodeKind Kind => NodeKind.Directive;
}
partial class ValueSymDeclNode
{
    public override NodeKind Kind => NodeKind.ValueSymDecl;
}
partial class FreeVarDeclNode
{
    public override NodeKind Kind => NodeKind.FreeVarDecl;
}
partial class IfNode
{
    public override NodeKind Kind => NodeKind.If;
}
partial class RecordNode
{
    public override NodeKind Kind => NodeKind.Record;
}
partial class SequenceNode
{
    public override NodeKind Kind => NodeKind.Sequence;
}
partial class TupleNode
{
    public override NodeKind Kind => NodeKind.Tuple;
}
partial class RecordProjectionNode
{
    public override NodeKind Kind => NodeKind.RecordProjection;
}
partial class TupleProjectionNode
{
    public override NodeKind Kind => NodeKind.TupleProjection;
}
partial class ValueProjectionNode
{
    public override NodeKind Kind => NodeKind.ValueProjection;
}
partial class ModuleProjectionNode
{
    public override NodeKind Kind => NodeKind.ModuleProjection;
}
partial class ModuleNode
{
    public override NodeKind Kind => NodeKind.Module;
}

partial class RexlTreeVisitor
{
    // Visit methods for leaf node types.
    public void Visit(GotoStmtNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(LabelStmtNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(ErrorNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(MissingValueNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(NullLitNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(BoolLitNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(NumLitNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(TextLitNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(BoxNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(ItNameNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(ThisNameNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(FirstNameNode node) { Enter(node); VisitImpl(node); Leave(node); }
    public void Visit(MetaPropNode node) { Enter(node); VisitImpl(node); Leave(node); }

    protected abstract void VisitImpl(GotoStmtNode node);
    protected abstract void VisitImpl(LabelStmtNode node);
    protected abstract void VisitImpl(ErrorNode node);
    protected abstract void VisitImpl(MissingValueNode node);
    protected abstract void VisitImpl(NullLitNode node);
    protected abstract void VisitImpl(BoolLitNode node);
    protected abstract void VisitImpl(NumLitNode node);
    protected abstract void VisitImpl(TextLitNode node);
    protected abstract void VisitImpl(BoxNode node);
    protected abstract void VisitImpl(ItNameNode node);
    protected abstract void VisitImpl(ThisNameNode node);
    protected abstract void VisitImpl(FirstNameNode node);
    protected abstract void VisitImpl(MetaPropNode node);

    // Visit methods for non-leaf node types.
    // If PreVisit returns true, the children are visited and PostVisit is called.
    public bool PreVisit(StmtListNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ExprListNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(SymListNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(SliceListNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ExprStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(BlockStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(IfStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(WhileStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ImportStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ExecuteStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(NamespaceStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(WithStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(DefinitionStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(FuncStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(TaskCmdStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(TaskProcStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(TaskBlockStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(UserProcStmtNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ParenNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(DottedNameNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(GetIndexNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(UnaryOpNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(BinaryOpNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(InHasNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(CompareNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(SliceItemNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(IndexingNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(CallNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(VariableDeclNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(DirectiveNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ValueSymDeclNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(FreeVarDeclNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(IfNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(RecordNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(SequenceNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(TupleNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(RecordProjectionNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(TupleProjectionNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ValueProjectionNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ModuleProjectionNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }
    public bool PreVisit(ModuleNode node) { Enter(node); bool res = PreVisitImpl(node); if (!res) Leave(node); return res; }

    protected virtual bool PreVisitImpl(StmtListNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ExprListNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(SymListNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(SliceListNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ExprStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(BlockStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(IfStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(WhileStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ImportStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ExecuteStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(NamespaceStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(WithStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(DefinitionStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(FuncStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(TaskCmdStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(TaskProcStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(TaskBlockStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(UserProcStmtNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ParenNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(DottedNameNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(GetIndexNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(UnaryOpNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(BinaryOpNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(InHasNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(CompareNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(SliceItemNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(IndexingNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(CallNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(VariableDeclNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(DirectiveNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ValueSymDeclNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(FreeVarDeclNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(IfNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(RecordNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(SequenceNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(TupleNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(RecordProjectionNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(TupleProjectionNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ValueProjectionNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ModuleProjectionNode node) { return PreVisitCore(node); }
    protected virtual bool PreVisitImpl(ModuleNode node) { return PreVisitCore(node); }

    public void PostVisit(StmtListNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ExprListNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(SymListNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(SliceListNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ExprStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(BlockStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(IfStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(WhileStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ImportStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ExecuteStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(NamespaceStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(WithStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(DefinitionStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(FuncStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(TaskCmdStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(TaskProcStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(TaskBlockStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(UserProcStmtNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ParenNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(DottedNameNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(GetIndexNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(UnaryOpNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(BinaryOpNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(InHasNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(CompareNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(SliceItemNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(IndexingNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(CallNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(VariableDeclNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(DirectiveNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ValueSymDeclNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(FreeVarDeclNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(IfNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(RecordNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(SequenceNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(TupleNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(RecordProjectionNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(TupleProjectionNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ValueProjectionNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ModuleProjectionNode node) { PostVisitImpl(node); Leave(node); }
    public void PostVisit(ModuleNode node) { PostVisitImpl(node); Leave(node); }

    protected abstract void PostVisitImpl(StmtListNode node);
    protected abstract void PostVisitImpl(ExprListNode node);
    protected abstract void PostVisitImpl(SymListNode node);
    protected abstract void PostVisitImpl(SliceListNode node);
    protected abstract void PostVisitImpl(ExprStmtNode node);
    protected abstract void PostVisitImpl(BlockStmtNode node);
    protected abstract void PostVisitImpl(IfStmtNode node);
    protected abstract void PostVisitImpl(WhileStmtNode node);
    protected abstract void PostVisitImpl(ImportStmtNode node);
    protected abstract void PostVisitImpl(ExecuteStmtNode node);
    protected abstract void PostVisitImpl(NamespaceStmtNode node);
    protected abstract void PostVisitImpl(WithStmtNode node);
    protected abstract void PostVisitImpl(DefinitionStmtNode node);
    protected abstract void PostVisitImpl(FuncStmtNode node);
    protected abstract void PostVisitImpl(TaskCmdStmtNode node);
    protected abstract void PostVisitImpl(TaskProcStmtNode node);
    protected abstract void PostVisitImpl(TaskBlockStmtNode node);
    protected abstract void PostVisitImpl(UserProcStmtNode node);
    protected abstract void PostVisitImpl(ParenNode node);
    protected abstract void PostVisitImpl(DottedNameNode node);
    protected abstract void PostVisitImpl(GetIndexNode node);
    protected abstract void PostVisitImpl(UnaryOpNode node);
    protected abstract void PostVisitImpl(BinaryOpNode node);
    protected abstract void PostVisitImpl(InHasNode node);
    protected abstract void PostVisitImpl(CompareNode node);
    protected abstract void PostVisitImpl(SliceItemNode node);
    protected abstract void PostVisitImpl(IndexingNode node);
    protected abstract void PostVisitImpl(CallNode node);
    protected abstract void PostVisitImpl(VariableDeclNode node);
    protected abstract void PostVisitImpl(DirectiveNode node);
    protected abstract void PostVisitImpl(ValueSymDeclNode node);
    protected abstract void PostVisitImpl(FreeVarDeclNode node);
    protected abstract void PostVisitImpl(IfNode node);
    protected abstract void PostVisitImpl(RecordNode node);
    protected abstract void PostVisitImpl(SequenceNode node);
    protected abstract void PostVisitImpl(TupleNode node);
    protected abstract void PostVisitImpl(RecordProjectionNode node);
    protected abstract void PostVisitImpl(TupleProjectionNode node);
    protected abstract void PostVisitImpl(ValueProjectionNode node);
    protected abstract void PostVisitImpl(ModuleProjectionNode node);
    protected abstract void PostVisitImpl(ModuleNode node);
}

partial class NoopTreeVisitor
{
    protected override void VisitImpl(GotoStmtNode node) { VisitCore(node); }
    protected override void VisitImpl(LabelStmtNode node) { VisitCore(node); }
    protected override void VisitImpl(ErrorNode node) { VisitCore(node); }
    protected override void VisitImpl(MissingValueNode node) { VisitCore(node); }
    protected override void VisitImpl(NullLitNode node) { VisitCore(node); }
    protected override void VisitImpl(BoolLitNode node) { VisitCore(node); }
    protected override void VisitImpl(NumLitNode node) { VisitCore(node); }
    protected override void VisitImpl(TextLitNode node) { VisitCore(node); }
    protected override void VisitImpl(BoxNode node) { VisitCore(node); }
    protected override void VisitImpl(ItNameNode node) { VisitCore(node); }
    protected override void VisitImpl(ThisNameNode node) { VisitCore(node); }
    protected override void VisitImpl(FirstNameNode node) { VisitCore(node); }
    protected override void VisitImpl(MetaPropNode node) { VisitCore(node); }

    protected override void PostVisitImpl(StmtListNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ExprListNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(SymListNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(SliceListNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ExprStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(BlockStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(IfStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(WhileStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ImportStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ExecuteStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(NamespaceStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(WithStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(DefinitionStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(FuncStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(TaskCmdStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(TaskProcStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(TaskBlockStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(UserProcStmtNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ParenNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(DottedNameNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(GetIndexNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(UnaryOpNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(BinaryOpNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(InHasNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(CompareNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(SliceItemNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(IndexingNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(CallNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(VariableDeclNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(DirectiveNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ValueSymDeclNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(FreeVarDeclNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(IfNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(RecordNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(SequenceNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(TupleNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(RecordProjectionNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(TupleProjectionNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ValueProjectionNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ModuleProjectionNode node) { PostVisitCore(node); }
    protected override void PostVisitImpl(ModuleNode node) { PostVisitCore(node); }
}
