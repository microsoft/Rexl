// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Statement;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// Base class for executing <see cref="StmtFlow"/> instances.
/// </summary>
public abstract partial class StmtInterp
{
    /// <summary>
    /// These handle the with chain for a <see cref="BlockFrame"/>. They are created by <see cref="Instruction.With"/>
    /// instructions.
    /// </summary>
    private sealed class WithNode
    {
        /// <summary>
        /// The next with in this chain.
        /// </summary>
        public readonly WithNode Next;

        /// <summary>
        /// The path for this "with".
        /// </summary>
        public readonly NPath Path;

        public WithNode(WithNode next, NPath path)
        {
            Next = next;
            Path = path;
        }
    }

    /// <summary>
    /// A <see cref="BlockFrame"/> is a nestable "context". Each has its namespace information and "with" chain.
    /// The interpreter has a stack of these. The <see cref="Instruction.Enter"/> instruction pushes one
    /// and the <see cref="Instruction.Leave"/> instruction pops some number of these. A jump instruction
    /// (<see cref="Instruction.JumpBase"/>) may also pop some of these (indicated by its
    /// <see cref="Instruction.JumpBase.LeaveCount"/>).
    /// </summary>
    private sealed partial class BlockFrame
    {
        /// <summary>
        /// Indicates whether this is a "root" block, meaning that any "with" information above
        /// this should not be searched. This is typically for an import into a namespace.
        /// </summary>
        public readonly bool IsRoot;

        /// <summary>
        /// The outer block. This is null for the base (outermost) block.
        /// </summary>
        public readonly BlockFrame Parent;

        /// <summary>
        /// The depth of this block, which is always one more than that of its parent.
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Root namespace for this block. This is used to enforce that imports can't see outside their
        /// root namespace.
        /// </summary>
        public readonly NPath NsRoot;

        /// <summary>
        /// Namespace specified for this block. If none was specified, this is inherited from the parent.
        /// </summary>
        public readonly NPath NsBlock;

        /// <summary>
        /// The current namespace.
        /// </summary>
        public NPath NsCur { get; private set; }

        /// <summary>
        /// The current namespace relative to <see cref="NsRoot"/>.
        /// </summary>
        public NPath NsRel { get; private set; }

        /// <summary>
        /// The "with" chain. A <c>null</c> value means the chain is empty.
        /// </summary>
        public WithNode With { get; private set; }

        /// <summary>
        /// Constructor for the base block (with no parent). The namespaces are all initialized to
        /// root, the <see cref="Parent"/> is set to <c>null</c>, and <see cref="Depth"/> is set to zero.
        /// </summary>
        public BlockFrame()
        {
            IsRoot = true;
            AssertValid();
        }

        /// <summary>
        /// Constructor for a non-base block. The <paramref name="nss"/> is optional. The <see cref="IsRoot"/>
        /// property is set to true iff a namespace is specified and <paramref name="forImport"/> is true.
        /// In this case, subsequent instructions cannot "see" outside the root namespace.
        /// </summary>
        public BlockFrame(BlockFrame parent, NamespaceSpec nss, bool forImport)
        {
            Validation.AssertValue(parent);
            Validation.AssertValueOrNull(nss);
            parent.AssertValid();

            Parent = parent;
            Depth = parent.Depth + 1;

            NsRoot = parent.NsRoot;
            NsCur = parent.NsCur;
            NsRel = parent.NsRel;

            if (nss == null)
            {
                NsBlock = parent.NsBlock;
                AssertValid();
                return;
            }

            var path = nss.IdentPath;
            if (path != null)
            {
                if (path.IsRooted)
                {
                    NsRel = path.FullName;
                    NsCur = NsRoot.AppendPath(NsRel);
                }
                else
                {
                    NsCur = NsCur.AppendPath(path.FullName);
                    NsRel = NsRoot.IsRoot ? NsCur : NsRel.AppendPath(path.FullName);
                }
            }
            else if (nss.IsRooted)
            {
                NsCur = NsRoot;
                NsRel = NPath.Root;
            }

            NsBlock = NsCur;
            if (forImport)
            {
                IsRoot = true;
                NsRoot = NsCur;
                NsRel = NPath.Root;
            }

            AssertValid();
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
#if DEBUG
            Validation.Assert(NsCur.NameCount == NsRoot.NameCount + NsRel.NameCount);
            Validation.Assert(NsCur == NsRoot.AppendPath(NsRel));
            Validation.Assert(NsBlock.StartsWith(NsRoot));
            Validation.Assert(NsCur.StartsWith(NsBlock));

            for (var with = With; with != null; with = with.Next)
                Validation.Assert(with.Path.StartsWith(NsRoot));
#endif
        }

        /// <summary>
        /// Resets the block. This is only legal on the base block.
        /// </summary>
        public void Reset()
        {
            // Only the base block can be reset.
            Validation.Assert(Parent == null);
            NsCur = NPath.Root;
            NsRel = NPath.Root;
            With = null;

            AssertValid();
        }

        /// <summary>
        /// Set the current namespace from the given <see cref="NamespaceSpec"/>.
        /// </summary>
        public void SetNamespace(NamespaceSpec nss)
        {
            AssertValid();
            Validation.AssertValue(nss);

            var path = nss.IdentPath;
            if (path != null)
            {
                if (path.IsRooted)
                {
                    NsRel = path.FullName;
                    NsCur = NsRoot.AppendPath(NsRel);
                }
                else if (NsCur == NsBlock)
                {
                    NsCur = NsCur.AppendPath(path.FullName);
                    NsRel = NsRoot.IsRoot ? NsCur : NsRel.AppendPath(path.FullName);
                }
                else
                {
                    NsCur = NsBlock.AppendPath(path.FullName);
                    NsRel = NsRoot.IsRoot ? NsCur : NPath.Root.AppendPartial(NsCur, NsRoot.NameCount);
                }
            }
            else if (nss.IsRooted)
            {
                NsCur = NsRoot;
                NsRel = NPath.Root;
            }
            else if (NsCur != NsBlock)
            {
                NsCur = NsBlock;
                NsRel = NsRoot.IsRoot ? NsCur : NPath.Root.AppendPartial(NsCur, NsRoot.NameCount);
            }

            AssertValid();
        }

        /// <summary>
        /// Add a "with" for the given <paramref name="path"/>.
        /// </summary>
        public void AddWith(NPath path)
        {
            Validation.Assert(path.StartsWith(NsRoot));
            if (With != null && With.Path == path)
                return;
            With = new WithNode(With, path);
        }
    }

    /// <summary>
    /// The interpreter maintains a stack of these representing the actively running scripts. This is so the
    /// interpreter loop can be "flat", meaning script recursion doesn't cause execution recursion.
    /// </summary>
    protected sealed partial class ScriptFrame
    {
        /// <summary>
        /// The next script frame in the stack.
        /// </summary>
        public readonly ScriptFrame Parent;

        /// <summary>
        /// This is the first real script frame (not the dummy empty bottom frame).
        /// </summary>
        public readonly ScriptFrame Root;

        /// <summary>
        /// The nesting depth of this script frame.
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// The source context that this script originated from. This may not be the direct source context
        /// when execute statements are present. We favor a context that has a "path" for file resolution.
        /// REVIEW: The whole mechanism needs to transition to a link based one, rather than "path"
        /// based.
        /// </summary>
        public readonly SourceContext Source;

        /// <summary>
        /// The statement flow being executed.
        /// </summary>
        public readonly StmtFlow Flow;

        /// <summary>
        /// Whether this script frame recovers from errors.
        /// </summary>
        public readonly bool Recover;

        /// <summary>
        /// Whether this script frame has a directly associated block frame. Typically this is the case when
        /// <c>in namespace X</c> is specified in an <c>import</c> or <cs>execute</cs> statement.
        /// </summary>
        public readonly bool WithBlock;

        /// <summary>
        /// The block depth when this script was "pushed".
        /// </summary>
        public readonly int BlockDepthEnter;

        /// <summary>
        /// The natural block depth of this script. This is either the same as <see cref="BlockDepthEnter"/>
        /// or one more (when <see cref="WithBlock"/> is <c>true</c>).
        /// </summary>
        public readonly int BlockDepthBase;

        /// <summary>
        /// The current block depth that this script accounts for.
        /// </summary>
        public int BlockDepthCur
        {
            get
            {
                if (Parent == null)
                    return 0;
                if (IsDone)
                    return -1;
                var inst = _insts[Pos];
                return inst.Depth + BlockDepthBase;
            }
        }

        /// <summary>
        /// The instruction stream, extracted from the <see cref="Flow"/>.
        /// </summary>
        private readonly Immutable.Array<Instruction> _insts;

        /// <summary>
        /// The number of instructions in this script frame.
        /// </summary>
        public int Count => _insts.Length;

        /// <summary>
        /// The current instruction position (index). This is set to <c>-1</c> when the script completes
        /// normally (without error).
        /// </summary>
        public int Pos { get; private set; }

        /// <summary>
        /// Whether this script frame is complete, indicated by the <see cref="Pos"/> being negative.
        /// </summary>
        public bool IsDone
        {
            get
            {
                Validation.Assert(Pos >= -1);
                return Pos < 0;
            }
        }

        /// <summary>
        /// The constructor for the base script frame (with no script).
        /// </summary>
        internal ScriptFrame()
        {
        }

        /// <summary>
        /// The constructor for a new frame.
        /// </summary>
        public ScriptFrame(
            ScriptFrame parent,
            SourceContext source, StmtFlow flow, bool recover,
            int blockDepth, bool withBlock)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(source);
            Validation.AssertValue(flow);
            Validation.Assert(blockDepth >= 0);

            Parent = parent;
            Root = parent.Root ?? this;
            Depth = parent.Depth + 1;
            Source = source;
            Flow = flow;
            Recover = recover;
            WithBlock = withBlock;
            BlockDepthEnter = blockDepth;
            BlockDepthBase = BlockDepthEnter + withBlock.ToNum();

            _insts = flow.Instructions;
        }

        /// <summary>
        /// Gets the current instruction. Asserts that the script isn't done.
        /// </summary>
        public Instruction GetCurrent()
        {
            Validation.AssertIndex(Pos, _insts.Length);
            return _insts[Pos];
        }

        /// <summary>
        /// Update the state for a jump to the given position.
        /// </summary>
        public void JumpTo(int pos)
        {
            Validation.Assert(!IsDone);
            Validation.AssertIndex(pos, _insts.Length);
            Pos = pos;
        }

        /// <summary>
        /// Update the state to indicate that the script is done.
        /// </summary>
        public void Done()
        {
            Validation.AssertIndex(Pos, _insts.Length);
            Pos = -1;
        }
    }

    // The entire "state" of the interpreter must reside in these fields, not in locals
    // or other fields.
    #region Interpreter State

    /// <summary>
    /// The current top (inner-most) script frame.
    /// </summary>
    private ScriptFrame _scrCur;

    /// <summary>
    /// The current top (inner-most) block frame.
    /// </summary>
    private BlockFrame _blkCur;

    #endregion Interpreter State

    /// <summary>
    /// The current script frame.
    /// </summary>
    protected ScriptFrame ScrCur => _scrCur;

    /// <summary>
    /// The script depth.
    /// </summary>
    public int ScriptDepth => _scrCur.Depth;

    /// <summary>
    /// The current statement flow.
    /// </summary>
    public StmtFlow FlowCur => _scrCur.Flow;

    /// <summary>
    /// The current source context.
    /// </summary>
    public SourceContext SourceCur => _scrCur.Source;

    /// <summary>
    /// The current root script context. This is null if the interpreter is inactive.
    /// </summary>
    public SourceContext SourceRoot => _scrCur.Root?.Source;

    /// <summary>
    /// The current "recover" state. When we should continue when an error is encountered.
    /// </summary>
    public bool Recover => _scrCur.Recover;

    /// <summary>
    /// The current block depth.
    /// </summary>
    private int BlockDepth => _blkCur.Depth;

    /// <summary>
    /// The current root namespace.
    /// </summary>
    public NPath NsRoot => _blkCur.NsRoot;

    /// <summary>
    /// The current block namespace.
    /// </summary>
    public NPath NsBlock => _blkCur.NsBlock;

    /// <summary>
    /// The current namespace.
    /// </summary>
    public NPath NsCur => _blkCur.NsCur;

    /// <summary>
    /// The current namespace (<see cref="NsCur"/>) relative to <see cref="NsRoot"/>.
    /// </summary>
    public NPath NsRel => _blkCur.NsRel;

    /// <summary>
    /// Whether the interpreter is active. This should be true iff the interpreter
    /// loop is on the runtime stack.
    /// </summary>
    public bool IsActive => _scrCur.Depth > 0;

    /// <summary>
    /// The active withs.
    /// </summary>
    public IEnumerable<NPath> Withs
    {
        get
        {
            for (var blk = _blkCur; blk != null; blk = blk.Parent)
            {
                for (var with = blk.With; with != null; with = with.Next)
                    yield return with.Path;

                // Stop on a root block.
                if (blk.IsRoot)
                    break;
            }
        }
    }

    protected StmtInterp()
    {
        _scrCur = new();
        _blkCur = new();
    }

    /// <summary>
    /// Reset the interpreter. This should only be called when the interpreter is inactive.
    /// </summary>
    public void Reset()
    {
        Validation.BugCheck(!IsActive, "Invalid call to Reset");
        Validation.Assert(_scrCur.Parent == null);
        Validation.Assert(_blkCur.Parent == null);
        _blkCur.Reset();
    }

    /// <summary>
    /// Runs the given flow, possibly in a new "block". That is, this is the main interpreter loop.
    /// This is <i>not</i> re-entrant. That is, this checks that the interpreter is not active.
    /// </summary>
    public Task<(bool success, Stream suspendState)> RunAsync(SourceContext sctx, StmtFlow flow, bool recover)
    {
        Validation.BugCheck(!IsActive, nameof(RunAsync) + " is not re-entrant!");
        Validation.BugCheckValue(sctx, nameof(sctx));
        Validation.BugCheckValue(flow, nameof(flow));

        PushSourceFrame(sctx, flow, nss: null, forImport: false, recover: recover);
        return LoopAsync();
    }

    /// <summary>
    /// Pushes the given flow onto the script stack, possibly in a new "block". That is, this makes
    /// the script the top running one. Checks that the interpreter is active. That is, this should
    /// only be called when executing a script, typically for the <c>import</c> and <c>execute</c>
    /// statements. If <paramref name="nss"/> is specified, the block's namespace is set accordingly.
    /// When <paramref name="forImport"/> is also true, the namespace is the "root" during execution.
    /// This enforces that "import blah in namespace N" can't see outside namespace N.
    /// 
    /// NOTE: this returns before the script execution is complete (or even starts).
    /// </summary>
    public bool PushScript(SourceContext sctx, StmtFlow flow, NamespaceSpec nss, bool forImport, bool recover)
    {
        Validation.BugCheck(IsActive, nameof(PushScript) + " should be called only when the interpreter is active!");
        Validation.BugCheckValue(sctx, nameof(sctx));
        Validation.BugCheckValue(flow, nameof(flow));
        Validation.BugCheckValueOrNull(nss);

        PushSourceFrame(sctx, flow, nss, forImport, recover);
        return true;
    }

    /// <summary>
    /// Return the current instruction and position. Returns null when inactive.
    /// </summary>
    public Instruction GetCurrentInstruction(out int pos)
    {
        if (!IsActive)
        {
            pos = -1;
            return null;
        }

        pos = _scrCur.Pos;
        return _scrCur.GetCurrent();
    }

    private async Task<(bool success, Stream suspendState)> LoopAsync(Action<StmtInterp, Stream> callback = null,
        Stream streamIn = null)
    {
        Validation.AssertValueOrNull(callback);
        Validation.AssertValueOrNull(streamIn);

        // The main loop.
        try
        {
            // The callback may throw a suspend exception, so this needs to be inside the try.
            callback?.Invoke(this, streamIn);

            for (; ; )
            {
                var scr = _scrCur;
                Validation.Assert(!scr.IsDone);
                Validation.Assert(scr.BlockDepthCur == BlockDepth);

                bool res;
                var inst = scr.GetCurrent();
                if (IsAsync(inst.Kind))
                    res = await DoOneAsync(scr, inst).ConfigureAwait(false);
                else
                    res = DoOne(scr, inst);

                if (res)
                {
#if DEBUG
                    // Assert that scr is in the script stack.
                    Validation.Assert(scr.BlockDepthBase <= BlockDepth);
                    Validation.Assert(scr.Depth <= _scrCur.Depth);
                    var tmp = _scrCur;
                    while (tmp.Depth > scr.Depth)
                        tmp = tmp.Parent;
                    Validation.Assert(tmp == scr);
#endif
                    Validation.Assert(!scr.IsDone);
                    continue;
                }

                // Done with this script, so it should be the top one!
                Validation.Assert(scr == _scrCur);

                bool good = scr.IsDone;
                Validation.Assert(BlockDepth == scr.BlockDepthBase | !good);

                // If the top frame is in recover mode, the script should be done.
                Validation.Assert(good | !scr.Recover);

                // Unwind the top frame.
                UnwindOne();

                if (!good)
                {
                    // Unwind until we're at a frame that is in recover mode.
                    while (_scrCur.Depth > 0 && !_scrCur.Recover)
                        UnwindOne();
                }

                if (_scrCur.Depth == 0)
                {
                    // We're done with execution.
                    return (good, null);
                }
            }
        }
        catch (SuspendException ex)
        {
            var suspendState = GetSuspendState(ex);
            UnwindAll();
            return (false, suspendState);
        }
        catch
        {
            // For an exception, we unwind everything and rethrow.
            UnwindAll();
            throw;
        }
    }

    /// <summary>
    /// Pushes a new source frame with instruction pointer set to zero.
    /// </summary>
    private void PushSourceFrame(SourceContext sctx, StmtFlow flow, NamespaceSpec nss, bool forImport, bool recover)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValue(flow);
        Validation.AssertValueOrNull(nss);

        _scrCur = new ScriptFrame(_scrCur, sctx, flow, recover, BlockDepth, withBlock: nss != null || forImport);
        if (_scrCur.WithBlock)
            _blkCur = new BlockFrame(_blkCur, nss, forImport);
    }

    private void UnwindAll()
    {
        while (_scrCur.Depth > 0)
            UnwindOne();
        Validation.Assert(_blkCur.Depth == 0);
    }

    /// <summary>
    /// Unwinds (removes) the top source frame and any associated block frames.
    /// </summary>
    private void UnwindOne()
    {
        Validation.Assert(_scrCur.Depth > 0);
        while (BlockDepth > _scrCur.BlockDepthEnter)
            PopFrame();
        _scrCur = _scrCur.Parent;
    }

    /// <summary>
    /// Execute the current instruction on the given script context. Returns <c>true</c> if there
    /// are more instructions in this script to execute. If the recover mode is false, returns false
    /// on a failure.
    /// </summary>
    private bool DoOne(ScriptFrame scr, Instruction inst)
    {
        Validation.Assert(scr == _scrCur);
        Validation.Assert(!_scrCur.IsDone);

        Validation.Assert(inst == scr.GetCurrent());
        Validation.Assert(inst.Depth == BlockDepth - scr.BlockDepthBase);
        Validation.Assert(!IsAsync(inst.Kind));

        PreInst(inst);
        int posNext = scr.Pos + 1;

        bool good;
        switch (inst.Kind)
        {
        case InstructionKind.End:
            Validation.Assert(inst.Depth == 0);
            Validation.Assert(BlockDepth == scr.BlockDepthBase);
            scr.Done();
            return false;

        case InstructionKind.DefineFunc:
            good = Handle(inst.Cast<Instruction.DefineFunc>());
            break;
        case InstructionKind.DefineProc:
            good = Handle(inst.Cast<Instruction.DefineProc>());
            break;
        case InstructionKind.Execute:
            good = Handle(inst.Cast<Instruction.Execute>());
            break;

        case InstructionKind.Enter:
            {
                var enter = inst.Cast<Instruction.Enter>();
                Enter(enter.NsSpec);
                Validation.Assert(enter.DepthNew == BlockDepth - scr.BlockDepthBase);
                good = true;
                break;
            }
        case InstructionKind.Leave:
            {
                var leave = inst.Cast<Instruction.Leave>();
                Validation.AssertIndex(leave.DepthNew, leave.Depth);
                Leave(leave.DepthNew + scr.BlockDepthBase);
                good = true;
                break;
            }

        case InstructionKind.Jump:
            {
                var jump = inst.Cast<Instruction.Jump>();
                Validation.AssertIndex(jump.Address, scr.Count);
                Validation.AssertIndexInclusive(jump.LeaveCount, jump.Depth);
                if (jump.LeaveCount > 0)
                    Leave(BlockDepth - jump.LeaveCount);
                posNext = jump.Address;
                Validation.Assert(jump.Depth - jump.LeaveCount == BlockDepth - scr.BlockDepthBase);
                good = true;
                break;
            }
        case InstructionKind.JumpIf:
            {
                var jump = inst.Cast<Instruction.JumpIf>();
                Validation.AssertIndex(jump.Address, scr.Count);
                Validation.AssertIndexInclusive(jump.LeaveCount, jump.Depth);
                good = TryTestCondition(jump.Condition, out bool value);
                if (!good)
                {
                    // When there is a problem with the value, use "false" (for recovery).
                    value = false;
                }
                if (value == jump.Sense)
                {
                    if (jump.LeaveCount > 0)
                        Leave(BlockDepth - jump.LeaveCount);
                    posNext = jump.Address;
                    Validation.Assert(jump.Depth - jump.LeaveCount == BlockDepth - scr.BlockDepthBase);
                }
                break;
            }

        case InstructionKind.Namespace:
            SetNamespace(inst.Cast<Instruction.Namespace>());
            good = true;
            break;
        case InstructionKind.With:
            AddWiths(inst.Cast<Instruction.With>());
            good = true;
            break;

        default:
            Validation.Assert(false);
            throw new NotImplementedException();
        }

        if (!good && !scr.Recover)
            return false;

        // REVIEW: Detect (trivial) infinite loop?
        // Validation.BugCheck(posNext != pos);
        scr.JumpTo(posNext);
        return true;
    }

    private static bool IsAsync(InstructionKind kind)
    {
        switch (kind)
        {
        case InstructionKind.Expr:
        case InstructionKind.Define:
        case InstructionKind.Import:
        case InstructionKind.TaskCmd:
        case InstructionKind.TaskProc:
        case InstructionKind.TaskBlock:
            return true;
        }

        return false;
    }

    /// <summary>
    /// Execute the current instruction on the given script context. Returns <c>true</c> if there
    /// are more instructions in this script to execute. If the recover mode is false, returns false
    /// on a failure.
    /// </summary>
    private async Task<bool> DoOneAsync(ScriptFrame scr, Instruction inst)
    {
        Validation.Assert(scr == _scrCur);
        Validation.Assert(!_scrCur.IsDone);

        Validation.Assert(inst == scr.GetCurrent());
        Validation.Assert(inst.Depth == BlockDepth - scr.BlockDepthBase);
        Validation.Assert(IsAsync(inst.Kind));

        PreInst(inst);
        int posNext = scr.Pos + 1;

        bool good;
        Task<bool> task;
        switch (inst.Kind)
        {
        case InstructionKind.Expr: task = HandleAsync(inst.Cast<Instruction.Expr>()); break;
        case InstructionKind.Define: task = HandleAsync(inst.Cast<Instruction.Define>()); break;
        case InstructionKind.Import: task = HandleAsync(inst.Cast<Instruction.Import>()); break;
        case InstructionKind.TaskCmd: task = HandleAsync(inst.Cast<Instruction.TaskCmd>()); break;
        case InstructionKind.TaskProc: task = HandleAsync(inst.Cast<Instruction.TaskProc>()); break;
        case InstructionKind.TaskBlock: task = HandleAsync(inst.Cast<Instruction.TaskBlock>()); break;

        default:
            Validation.Assert(false);
            throw new NotImplementedException();
        }

        good = await task.ConfigureAwait(false);
        if (!good && !scr.Recover)
            return false;

        scr.JumpTo(posNext);
        return true;
    }

    /// <summary>
    /// Called when an instruction is about to be executed.
    /// </summary>
    protected virtual void PreInst(Instruction inst)
    {
    }

    protected abstract void Error(Token tok, StringId msg);
    protected abstract bool IsNamespace(NPath path);
    protected abstract bool TryTestCondition(RexlFormula cond, out bool value);

    protected abstract Task<bool> HandleAsync(Instruction.Expr inst);
    protected abstract Task<bool> HandleAsync(Instruction.Define inst);
    protected abstract bool Handle(Instruction.DefineFunc inst);
    protected abstract bool Handle(Instruction.DefineProc inst);
    protected abstract Task<bool> HandleAsync(Instruction.Import inst);
    protected abstract bool Handle(Instruction.Execute inst);
    protected abstract Task<bool> HandleAsync(Instruction.TaskCmd inst);
    protected abstract Task<bool> HandleAsync(Instruction.TaskProc inst);
    protected abstract Task<bool> HandleAsync(Instruction.TaskBlock inst);

    private void PushFrame(NamespaceSpec nss, bool forImport)
    {
        Validation.AssertValueOrNull(nss);
        _blkCur = new BlockFrame(_blkCur, nss, forImport);
    }

    private void PopFrame()
    {
        Validation.Assert(_blkCur.Parent != null);
        _blkCur = _blkCur.Parent;
    }

    private void Enter(NamespaceSpec nss)
    {
        Validation.AssertValueOrNull(nss);
        PushFrame(nss, forImport: false);
    }

    private void Leave(int depth)
    {
        Validation.Assert(depth >= 0);
        Validation.Assert(depth < _blkCur.Depth);
        while (depth < _blkCur.Depth)
            PopFrame();
        Validation.Assert(_blkCur.Depth == depth);
    }

    private void SetNamespace(Instruction.Namespace inst)
    {
        Validation.AssertValue(inst);
        _blkCur.SetNamespace(inst.NsSpec);
    }

    private void AddWiths(Instruction.With inst)
    {
        Validation.AssertValue(inst);

        var paths = inst.Paths;
        Validation.Assert(!paths.IsDefault);

        foreach (var path in paths)
        {
            for (var nsBase = path.IsRooted ? NsRoot : NsBlock; ; nsBase = nsBase.Parent)
            {
                var ns = nsBase.AppendPath(path.FullName);
                if (IsNamespace(ns))
                {
                    _blkCur.AddWith(ns);
                    break;
                }
                if (nsBase.NameCount <= NsRoot.NameCount)
                {
                    Error(path.Last.Token, ErrorStrings.ErrBadNamespace);
                    break;
                }
            }
        }
    }
}
