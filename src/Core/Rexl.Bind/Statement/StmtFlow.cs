// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Statement;

/// <summary>
/// Represents a (partially) compiled statement list as an array of <see cref="Instruction"/>.
/// </summary>
public sealed partial class StmtFlow
{
    /// <summary>
    /// The source statement list.
    /// </summary>
    public RexlStmtList StmtList { get; }

    /// <summary>
    /// The diagnostics generated when compiling the <see cref="StmtList"/> to <see cref="Instructions"/>.
    /// </summary>
    public Immutable.Array<RexlDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Whether there are any diagnostics.
    /// </summary>
    public bool HasDiagnostics => Diagnostics.Length > 0;

    /// <summary>
    /// Whether there are any errors.
    /// </summary>
    public bool HasErrors { get; }

    /// <summary>
    /// The instuctions.
    /// </summary>
    public Immutable.Array<Instruction> Instructions { get; }

    private StmtFlow(RexlStmtList rsl, Immutable.Array<Instruction> insts, Immutable.Array<RexlDiagnostic>.Builder diags)
    {
        Validation.AssertValue(rsl);
        Validation.Assert(insts.Length > 0);
        Validation.Assert(insts.All(i => i != null && i.IsResolved));
        Validation.Assert(insts[^1].Kind == InstructionKind.End);

        StmtList = rsl;
        Instructions = insts;
        Diagnostics = diags != null ? diags.ToImmutable() : Immutable.Array<RexlDiagnostic>.Empty;
        if (Diagnostics.Length > 0)
            HasErrors = Diagnostics.Any(d => d.IsError);
    }

    /// <summary>
    /// Compile the given statement list.
    /// </summary>
    public static StmtFlow Create(RexlStmtList rsl)
    {
        Validation.BugCheckValue(rsl, nameof(rsl));
        return Builder.Build(rsl);
    }

    /// <summary>
    /// Dump a text representation of this flow into the given <paramref name="output"/>.
    /// </summary>
    public void DumpInto(TextSink output)
    {
        for (int i = 0; i < Instructions.Length; i++)
        {
            output.Write("{0,4}) ", i);
            var inst = Instructions[i];
            inst.DumpInto(output);
#if DEBUG
            switch (inst.Kind)
            {
            case InstructionKind.Jump:
                {
                    var ins = inst.Cast<Instruction.Jump>();
                    Validation.AssertIndex(ins.Address, Instructions.Length);
                    Validation.Assert(ins.LeaveCount == inst.Depth - Instructions[ins.Address].Depth);
                    break;
                }
            case InstructionKind.JumpIf:
                {
                    var ins = inst.Cast<Instruction.JumpIf>();
                    Validation.AssertIndex(ins.Address, Instructions.Length);
                    Validation.Assert(ins.LeaveCount == inst.Depth - Instructions[ins.Address].Depth);
                    break;
                }
            }
#endif
            output.WriteLine();
        }
    }
}

// This partial contains the "builder", aka "compiler".
partial class StmtFlow
{
    /// <summary>
    /// Translates a <see cref="RexlStmtList"/> into a <see cref="StmtFlow"/>.
    /// </summary>
    private sealed class Builder
    {
        /// <summary>
        /// Represents a context / block with its own state.
        /// </summary>
        private sealed class Block
        {
            /// <summary>
            /// The next outer block. If this is <c>null</c>, the <see cref="Depth"/> is zero.
            /// </summary>
            public readonly Block Outer;

            /// <summary>
            /// The depth of this block, which is one more than its parent's depth.
            /// </summary>
            public readonly int Depth;

            /// <summary>
            /// The labels defined directly in this block. This is used to resolve jumps when we
            /// are done with the block. This is <c>null</c> unless/until needed. Maps from name
            /// to instruction index, aka "address".
            /// </summary>
            public Dictionary<DName, int> Labels;

            /// <summary>
            /// The unresolved (possibly conditional) jumps in this block or nested blocks (that are
            /// no longer active). Contains pairs of index and label identifier. We store the
            /// <see cref="Identifier"/> (rather than just <see cref="DName"/>) for error reporting.
            /// </summary>
            public List<(int index, Identifier label)> Jumps;

            /// <summary>
            /// Constructor for the top level block, with no parent. Note that the depth is zero.
            /// </summary>
            public Block()
            {
            }

            /// <summary>
            /// Constructor for a nested block.
            /// </summary>
            public Block(Block outer)
            {
                Validation.AssertValue(outer);
                Outer = outer;
                Depth = outer.Depth + 1;
            }
        }

        // The statement list being compiled.
        private readonly RexlStmtList _rsl;

        // The instruction array builder.
        private readonly Immutable.Array<Instruction>.Builder _insts;

        // The diagnostics array builder. This is null until/unless needed.
        private Immutable.Array<RexlDiagnostic>.Builder _diags;

        // Tracks the first instance of each label name, so we can report "can't jump into block" rather
        // than just "label not found". This is null until/unless needed.
        private Dictionary<DName, Identifier> _labelsFirst;

        // The current (inner-most) block.
        private Block _blockCur;

        // The depth of the current block.
        private int Depth => _blockCur.Depth;

        /// <summary>
        /// Entry point to build the <see cref="StmtFlow"/>.
        /// </summary>
        public static StmtFlow Build(RexlStmtList rsl)
        {
            var bldr = new Builder(rsl);

            foreach (var stmt in rsl.ParseTree.Children)
                bldr.AddStmt(stmt);

            return bldr.Complete();
        }

        private Builder(RexlStmtList rsl)
        {
            Validation.AssertValue(rsl);
            _rsl = rsl;
            _insts = Immutable.Array<Instruction>.CreateBuilder();
            _blockCur = new Block();
        }

        /// <summary>
        /// Add a diagnostic to the list. All additions should use this so it's easy to
        /// set a break point where all diags are added.
        /// </summary>
        private RexlDiagnostic AddDiag(RexlDiagnostic diag)
        {
            Validation.AssertValue(diag);
            _diags ??= Immutable.Array<RexlDiagnostic>.CreateBuilder();
            _diags.Add(diag);
            return diag;
        }

        /// <summary>
        /// Add an error at the indicated token.
        /// </summary>
        private void Error(Token tok, StringId msg)
        {
            Validation.AssertValue(tok);
            Validation.Assert(msg.IsValid);
            AddDiag(RexlDiagnostic.Error(tok, msg));
        }

        /// <summary>
        /// For "expressions" we use <see cref="RexlFormula"/>. This makes one from an <see cref="ExprNode"/>
        /// and the <see cref="RexlStmtList"/>.
        /// REVIEW: Do we really need to do this? Should everything use <see cref="ExprNode"/> instead of
        /// <see cref="RexlFormula"/>?
        /// </summary>
        private RexlFormula MakeExpr(ExprNode node)
        {
            if (node == null)
                return null;
            return RexlFormula.CreateSubFormula(_rsl, node);
        }

        /// <summary>
        /// Finish the compilation.
        /// </summary>
        private StmtFlow Complete()
        {
            Validation.Assert(Depth == 0);
            _insts.Add(new Instruction.End());
            ResolveJumps(_blockCur);
            return new StmtFlow(_rsl, _insts.ToImmutable(), _diags);
        }

        /// <summary>
        /// Resolve jumps in the indicated block. Any that can't be resolved are moved to the parent.
        /// If there is no parent, unresolved jumps produce errors and are resolved to jump to the
        /// <see cref="Instruction.End"/> instruction.
        /// </summary>
        private void ResolveJumps(Block block)
        {
            if (Util.Size(block.Jumps) == 0)
                return;

            foreach (var (index, label) in block.Jumps)
            {
                Validation.AssertIndex(index, _insts.Count);
                Validation.AssertValue(label);

                var jump = _insts[index] as Instruction.JumpBase;
                Validation.Assert(jump != null);
                Validation.Assert(jump.Depth >= block.Depth);

                if (!Util.TryGetValue(block.Labels, label.Name, out int addr))
                {
                    if (block.Outer != null)
                    {
                        Util.Add(ref block.Outer.Jumps, (index, label));
                        continue;
                    }

                    if (Util.TryGetValue(_labelsFirst, label.Name, out var labFirst))
                    {
                        Error(label.Token, ErrorStrings.ErrCantJumpInto);
                        Error(labFirst.Token, ErrorStrings.ErrLabelInBlock);
                    }
                    else
                        Error(label.Token, ErrorStrings.ErrUnknownLabel);
                    addr = _insts.Count - 1;
                    Validation.Assert(addr >= 0);
                    Validation.Assert(_insts[addr].VerifyValue().Kind == InstructionKind.End);
                }

                Validation.AssertIndexInclusive(addr, _insts.Count);
                if (addr == index)
                {
                    // REVIEW: This detects a trivial infinite loop. Should we also detect unconditional
                    // infinite looping somehow? That is, look for backward jump with no jumps out of the range?
                    Error(label.Token, ErrorStrings.ErrInfiniteLoop);
                }
                _insts[index] = jump.Resolve(addr, jump.Depth - block.Depth);
            }
        }

        private void AddStmt(StmtNode stmt)
        {
            switch (stmt.Kind)
            {
            case NodeKind.ExprStmt:
                {
                    var esn = stmt.Cast<ExprStmtNode>();
                    var e = MakeExpr(esn.Value);
                    _insts.Add(new Instruction.Expr(Depth, e));
                    break;
                }
            case NodeKind.DefinitionStmt:
                {
                    var dsn = stmt.Cast<DefinitionStmtNode>();
                    _insts.Add(new Instruction.Define(Depth, dsn.DefnKind, dsn.IdentPath, MakeExpr(dsn.Value)));
                    break;
                }
            case NodeKind.FuncStmt:
                {
                    var fsn = stmt.Cast<FuncStmtNode>();
                    _insts.Add(new Instruction.DefineFunc(Depth, fsn.IdentPath, fsn.ParamNames,
                        MakeExpr(fsn.Value), fsn.IsProp));
                    break;
                }
            case NodeKind.UserProcStmt:
                {
                    var ups = stmt.Cast<UserProcStmtNode>();
                    _insts.Add(new Instruction.DefineProc(Depth, ups.IdentPath, ups.ParamNames,
                        _rsl, ups.Prime, ups.Play));
                    break;
                }

            case NodeKind.TaskCmdStmt:
                {
                    var tcs = stmt.Cast<TaskCmdStmtNode>();
                    _insts.Add(new Instruction.TaskCmd(Depth, tcs.Cmd, tcs.Name));
                    break;
                }
            case NodeKind.TaskProcStmt:
                {
                    var tps = stmt.Cast<TaskProcStmtNode>();
                    _insts.Add(new Instruction.TaskProc(Depth, tps.Token.TokenAlt, tps.IdentPath,
                        MakeExpr(tps.Value)));
                    break;
                }
            case NodeKind.TaskBlockStmt:
                {
                    var tbs = stmt.Cast<TaskBlockStmtNode>();
                    _insts.Add(new Instruction.TaskBlock(Depth, tbs.Token.TokenAlt, tbs.IdentPath,
                        MakeExpr(tbs.With), tbs.Prime, tbs.Body));
                    break;
                }

            case NodeKind.IfStmt:
                {
                    var isn = stmt.Cast<IfStmtNode>();
                    var cond = MakeExpr(isn.Condition);
                    if (isn.Then is GotoStmtNode gsn)
                    {
                        Util.Add(ref _blockCur.Jumps, (_insts.Count, gsn.Label));
                        _insts.Add(new Instruction.JumpIf(Depth, cond, sense: true));
                        if (isn.Else != null)
                            AddStmt(isn.Else);
                    }
                    else if (isn.Else is GotoStmtNode gsnElse)
                    {
                        Util.Add(ref _blockCur.Jumps, (_insts.Count, gsnElse.Label));
                        _insts.Add(new Instruction.JumpIf(Depth, cond, sense: false));
                        AddStmt(isn.Then);
                    }
                    else
                    {
                        // Fill in the JumpIf below.
                        int indexJumpIf = _insts.Count;
                        _insts.Add(null);

                        AddStmt(isn.Then);
                        if (isn.Else == null)
                            _insts[indexJumpIf] = new Instruction.JumpIf(Depth, cond, sense: false, _insts.Count, 0);
                        else
                        {
                            // Fill in the Jump below.
                            int indexJump = _insts.Count;
                            _insts.Add(null);

                            _insts[indexJumpIf] = new Instruction.JumpIf(Depth, cond, sense: false, _insts.Count, 0);
                            AddStmt(isn.Else);

                            _insts[indexJump] = new Instruction.Jump(Depth, _insts.Count);
                        }
                    }
                    break;
                }
            case NodeKind.WhileStmt:
                {
                    var wsn = stmt.Cast<WhileStmtNode>();
                    var cond = MakeExpr(wsn.Condition);

                    // Fill in the JumpIf below.
                    int index = _insts.Count;
                    _insts.Add(null);

                    AddStmt(wsn.Body);
                    _insts.Add(new Instruction.Jump(Depth, index));

                    _insts[index] = new Instruction.JumpIf(Depth, cond, sense: false, _insts.Count, 0);
                    break;
                }
            case NodeKind.BlockStmt:
                {
                    var bsn = stmt.Cast<BlockStmtNode>();
                    var block = EnterBlock(bsn);
                    AddBlockContents(bsn);
                    LeaveBlock(block);
                    break;
                }
            case NodeKind.LabelStmt:
                {
                    var lsn = stmt.Cast<LabelStmtNode>();
                    var label = lsn.Label;
                    if (Util.TryGetValue(_blockCur.Labels, label.Name, out int addr))
                        Error(label.Token, ErrorStrings.ErrDuplicateLabel);
                    else
                    {
                        Util.Add(ref _blockCur.Labels, label.Name, _insts.Count);
                        if (!Util.ContainsKey(_labelsFirst, label.Name))
                            Util.Add(ref _labelsFirst, label.Name, label);
                    }
                    break;
                }
            case NodeKind.GotoStmt:
                {
                    var gsn = stmt.Cast<GotoStmtNode>();
                    Util.Add(ref _blockCur.Jumps, (_insts.Count, gsn.Label));
                    _insts.Add(new Instruction.Jump(Depth));
                    break;
                }
            case NodeKind.NamespaceStmt:
                {
                    var nsn = stmt.Cast<NamespaceStmtNode>();
                    var bsn = nsn.Block;
                    var block = EnterBlock(bsn, nsn.NsSpec);
                    AddBlockContents(bsn);
                    LeaveBlock(block);
                    break;
                }
            case NodeKind.WithStmt:
                {
                    var wsn = stmt.Cast<WithStmtNode>();
                    var bsn = wsn.Block;
                    var block = EnterBlock(bsn);
                    _insts.Add(new Instruction.With(Depth, wsn.IdentPaths));
                    AddBlockContents(bsn);
                    LeaveBlock(block);
                    break;
                }
            case NodeKind.ImportStmt:
                {
                    var csn = stmt.Cast<CmdStmtNode>();
                    _insts.Add(new Instruction.Import(Depth, MakeExpr(csn.Value), csn.Namespace));
                    break;
                }
            case NodeKind.ExecuteStmt:
                {
                    var csn = stmt.Cast<CmdStmtNode>();
                    _insts.Add(new Instruction.Execute(Depth, MakeExpr(csn.Value), csn.Namespace));
                    break;
                }

            default:
                throw new NotImplementedException();
            }
        }

        private Block EnterBlock(BlockStmtNode bsn, NamespaceSpec nss = null)
        {
            if (bsn == null)
            {
                if (nss != null)
                    _insts.Add(new Instruction.Namespace(Depth, nss));
                return null;
            }

            _insts.Add(new Instruction.Enter(Depth, nss));
            return _blockCur = new Block(_blockCur);
        }

        private void AddBlockContents(BlockStmtNode bsn)
        {
            if (bsn == null)
                return;
            foreach (var s in bsn.Statements.Children)
                AddStmt(s);
        }

        private void LeaveBlock(Block block)
        {
            if (block == null)
                return;

            Validation.Assert(block == _blockCur);
            Validation.Assert(block.Outer != null);
            _insts.Add(new Instruction.Leave(block.Depth, 1));
            ResolveJumps(block);
            _blockCur = block.Outer;
        }
    }
}
