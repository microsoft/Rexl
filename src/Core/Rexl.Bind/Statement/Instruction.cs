// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Statement;

using NameTuple = Immutable.Array<DName>;

/// <summary>
/// The kinds of instructions.
/// </summary>
public enum InstructionKind
{
    None,

    /// <summary>
    /// End of the instruction stream.
    /// For <see cref="Instruction.End"/>.
    /// </summary>
    End,

    /// <summary>
    /// Enter/push a "frame".
    /// For <see cref="Instruction.Enter"/>.
    /// Contains an optional namespace specification.
    /// </summary>
    Enter,

    /// <summary>
    /// Leave/pop one or more (top) frames.
    /// For <see cref="Instruction.Leave"/>.
    /// Contains the count of frames to leave.
    /// </summary>
    Leave,

    /// <summary>
    /// Jump (unconditionally) to an address.
    /// For <see cref="Instruction.Jump"/>.
    /// A jump can leave frames, but not enter them.
    /// Contains the address and the count of frames to leave.
    /// </summary>
    Jump,

    /// <summary>
    /// Jump conditionally to an address.
    /// For <see cref="Instruction.JumpIf"/>.
    /// A jump can leave frames, but not enter them.
    /// Contains the "sense", the condition expression, the address, and the count of frames to leave (when
    /// the jump is performed).
    /// </summary>
    JumpIf,

    /// <summary>
    /// Evaluate an expression and, if not a task, display/report the resulting value.
    /// For <see cref="Instruction.Expr"/>.
    /// The expression may be an invocation of a procedure. When it is, the task is run to completion and its
    /// primary result is used as the resulting "value".
    /// </summary>
    Expr,

    /// <summary>
    /// Evaluate an expression and assign its value to a path.
    /// For <see cref="Instruction.Define"/>.
    /// Contains the value expression and the path.
    /// The expression may be an invocation of a procedure. When it is, the task is run to completion and its
    /// primary result is used as the resulting "value" (assigned to the path).
    /// </summary>
    Define,

    /// <summary>
    /// Define a user defined function.
    /// For <see cref="Instruction.DefineFunc"/>.
    /// Contains the name/path, the parameter names, the expression body, and whether the function can be used as
    /// a property.
    /// </summary>
    DefineFunc,

    /// <summary>
    /// Define a user defined procedure.
    /// For <see cref="Instruction.DefineProc"/>.
    /// Contains the name/path, the parameter names, the prime block, and the play block.
    /// </summary>
    DefineProc,

    /// Apply a command to a task.
    /// For <see cref="Instruction.TaskCmd"/>;
    /// Contains the command and path/name of the task.
    /// </summary>
    TaskCmd,

    /// <summary>
    /// Evaluate a procedure and optionally assign the resulting task to a path.
    /// For <see cref="Instruction.TaskProc"/>.
    /// </summary>
    TaskProc,

    /// <summary>
    /// Defines a task from an optional "with" record expression, an optional "prime" block and
    /// a required "play" block.
    /// For <see cref="Instruction.TaskBlock"/>.
    /// </summary>
    TaskBlock,

    /// <summary>
    /// Set the current namespace.
    /// For <see cref="Instruction.Namespace"/>.
    /// Contains the namespace specification.
    /// </summary>
    Namespace,

    /// <summary>
    /// Add "with" names.
    /// For <see cref="Instruction.With"/>.
    /// Contains the paths/names of the withs to add.
    /// </summary>
    With,

    /// <summary>
    /// Import into an optional namespace.
    /// For <see cref="Instruction.Namespace"/>.
    /// Contains an expression specifying what to import and an optional namespace specification.
    /// </summary>
    Import,

    /// <summary>
    /// Execute a script specified as a text value in the context of an optional namespace.
    /// For <see cref="Instruction.Execute"/>.
    /// Contains an expression specifying the script text and an optional namespace specification.
    /// </summary>
    Execute,

    /// <summary>
    /// Not valid - indicates end of valid range.
    /// </summary>
    _Lim
}

/// <summary>
/// Extension methods for <see cref="InstructionKind"/>.
/// </summary>
public static class InstructionKindExtensions
{
    /// <summary>
    /// Whether the instruction kind is valid.
    /// </summary>
    public static bool IsValid(this InstructionKind kind)
    {
        return InstructionKind.None < kind && kind < InstructionKind._Lim;
    }
}

/// <summary>
/// Represents an instruction for a statement interpreter.
/// </summary>
public abstract class Instruction
{
    /// <summary>
    /// The kind of instruction.
    /// </summary>
    public InstructionKind Kind { get; }

    /// <summary>
    /// The depth of this instruction (block nesting count).
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Whether this instruction is fully resolved.
    /// </summary>
    internal virtual bool IsResolved => true;

    private Instruction(InstructionKind kind, int depth)
    {
        Validation.Assert(kind.IsValid());
        Validation.Assert(depth >= 0);

        Kind = kind;
        Depth = depth;
    }

    /// <summary>
    /// Casts this instruction to the given instruction type.
    /// </summary>
    public T Cast<T>()
        where T : Instruction
    {
        Validation.Assert(this is T);
        return (T)this;
    }

    /// <summary>
    /// Dump a text representation of the instruction into the given <paramref name="sink"/>.
    /// </summary>
    public void DumpInto(TextSink sink)
    {
        Validation.BugCheckValue(sink, nameof(sink));
        sink.Write("[{0}] {1}", Depth, Kind);
        DumpExtra(sink);
    }

    /// <summary>
    /// Dump extra, instruction-specific, information into the <paramref name="sink"/>.
    /// </summary>
    protected virtual void DumpExtra(TextSink sink)
    {
        Validation.AssertValue(sink);
    }

    /// <summary>
    /// Append a namespace specification, for implementing <see cref="DumpExtra"/>.
    /// </summary>
    private static void AppendNamespace(TextSink sink, NamespaceSpec nss)
    {
        Validation.AssertValue(nss);
        if (nss.IdentPath != null)
        {
            sink.Write(' ');
            AppendName(sink, nss.IdentPath);
        }
        else if (nss.IsRooted)
            sink.Write(" @");
        else
            sink.Write(" _");
    }

    /// <summary>
    /// Append an <see cref="IdentPath"/>, for implementing <see cref="DumpExtra"/>.
    /// </summary>
    private static void AppendName(TextSink sink, IdentPath path)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(path);
        if (path.IsRooted)
            sink.Write('@');
        sink.WriteDottedSyntax(path.FullName);
    }

    /// <summary>
    /// Append an <see cref="Identifier"/>, for implementing <see cref="DumpExtra"/>.
    /// </summary>
    private static void AppendName(TextSink sink, Identifier name)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(name);
        name.Token.Format(sink);
    }

    private static void AppendWith(TextSink sink, ExprNode node)
    {
        Validation.AssertValue(sink);
        if (node == null)
            return;

        sink.Write(" with ");
        bool parens = node.Kind != NodeKind.Record;
        if (parens)
            sink.Write('(');
        sink.Write(RexlPrettyPrinter.Print(node, Precedence.Error));
        if (parens)
            sink.Write(')');
    }

    /// <summary>
    /// This instruction marks the end of an instruction stream.
    /// It contains no information. Its depth is always zero.
    /// </summary>
    public sealed class End : Instruction
    {
        internal End()
            : base(InstructionKind.End, depth: 0)
        {
        }
    }

    /// <summary>
    /// Enter/push a "frame".
    /// Contains an optional namespace specification.
    /// </summary>
    public sealed class Enter : Instruction
    {
        /// <summary>
        /// The depth after entering the new frame.
        /// </summary>
        public int DepthNew => Depth + 1;

        /// <summary>
        /// The (optional) namespace.
        /// </summary>
        public NamespaceSpec NsSpec { get; }

        internal Enter(int depth, NamespaceSpec nss = null)
            : base(InstructionKind.Enter, depth)
        {
            Validation.AssertValueOrNull(nss);
            NsSpec = nss;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.Write(" ({0}=>{1})", Depth, DepthNew);
            if (NsSpec != null)
                AppendNamespace(sink, NsSpec);
        }
    }

    /// <summary>
    /// Leave/pop one or more "frames".
    /// Contains the count of frames to leave.
    /// </summary>
    public sealed class Leave : Instruction
    {
        /// <summary>
        /// The number of frames to leave/pop.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// The depth after execution of this instruction.
        /// </summary>
        public int DepthNew => Depth - Count;

        internal Leave(int depth, int count)
            : base(InstructionKind.Leave, depth)
        {
            Validation.Assert(depth > 0);
            Validation.Assert(0 < count & count <= depth);
            Count = count;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.Write(" ({0}=>{1})", Depth, DepthNew);
        }
    }

    /// <summary>
    /// Base class for jump instructions.
    /// A jump can leave frames, but not enter them.
    /// Contains the target address, the number of frames to leave when the jump is performed,
    /// and whether this instruction has been properly "resolved". An unresolved jump should
    /// never be present in a completed instruction flow.
    /// 
    /// REVIEW: Perhaps this should store a relative/delta address so code is "relocatable".
    /// </summary>
    public abstract class JumpBase : Instruction
    {
        /// <summary>
        /// The target address. For an unresolved jump, this is <c>-1</c>.
        /// </summary>
        public int Address { get; }

        /// <summary>
        /// The number of frames to leave when the jump is performed. For an unresolved jump, this is <c>-1</c>.
        /// </summary>
        public int LeaveCount { get; }

        internal override bool IsResolved => Address >= 0;

        /// <summary>
        /// Constructor for an unresolved address.
        /// </summary>
        private protected JumpBase(InstructionKind kind, int depth)
            : base(kind, depth)
        {
            Address = -1;
            LeaveCount = -1;
        }

        /// <summary>
        /// Constructor for a resolved address.
        /// </summary>
        private protected JumpBase(InstructionKind kind, int depth, int addr, int leaveCount)
            : base(kind, depth)
        {
            Validation.BugCheckParam(addr >= 0, nameof(addr));
            Validation.BugCheckParam(0 <= leaveCount & leaveCount <= depth, nameof(leaveCount));

            Address = addr;
            LeaveCount = leaveCount;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.Write(" {0} ({1}=>{2})", Address, Depth, Depth - LeaveCount);
        }

        /// <summary>
        /// Resolve this jump to the given address (and leave count). Return the resulting instruction.
        /// </summary>
        internal JumpBase Resolve(int addr, int leaveCount)
        {
            Validation.BugCheck(!IsResolved);
            Validation.BugCheckParam(addr >= 0, nameof(addr));
            Validation.BugCheckParam(0 <= leaveCount & leaveCount <= Depth, nameof(leaveCount));

            return ResolveCore(addr, leaveCount);
        }

        private protected abstract JumpBase ResolveCore(int addr, int leaveCount);
    }

    /// <summary>
    /// Jump (unconditionally) to an address.
    /// A jump can leave frames, but not enter them.
    /// Contains the address and the count of frames to leave.
    /// </summary>
    public sealed class Jump : JumpBase
    {
        /// <summary>
        /// Constructor for an unresolved address.
        /// </summary>
        internal Jump(int depth)
            : base(InstructionKind.Jump, depth)
        {
        }

        /// <summary>
        /// Constructor for a resolved address.
        /// </summary>
        internal Jump(int depth, int addr, int leaveCount = 0)
            : base(InstructionKind.Jump, depth, addr, leaveCount)
        {
        }

        private protected override JumpBase ResolveCore(int addr, int leaveCount)
        {
            Validation.Assert(!IsResolved);
            return new Jump(Depth, addr, leaveCount);
        }
    }

    /// <summary>
    /// Jump conditionally to an address.
    /// A jump can leave frames, but not enter them.
    /// Contains the "sense", the condition expression, the address, and the count of frames to leave (when
    /// the jump is performed).
    /// </summary>
    public sealed class JumpIf : JumpBase
    {
        /// <summary>
        /// The condition to evaluate and test.
        /// </summary>
        public RexlFormula Condition { get; }

        /// <summary>
        /// The jump is executed when the condition value matches the sense.
        /// </summary>
        public bool Sense { get; }

        /// <summary>
        /// Constructor for an unresolved address.
        /// </summary>
        internal JumpIf(int depth, RexlFormula condition, bool sense)
            : base(InstructionKind.JumpIf, depth)
        {
            Validation.AssertValue(condition);
            Condition = condition;
            Sense = sense;
        }

        /// <summary>
        /// Constructor for a resolved address.
        /// </summary>
        internal JumpIf(int depth, RexlFormula condition, bool sense, int addr, int leaveCount)
            : base(InstructionKind.JumpIf, depth, addr, leaveCount)
        {
            Validation.AssertValue(condition);
            Condition = condition;
            Sense = sense;
        }

        private protected override JumpBase ResolveCore(int addr, int leaveCount)
        {
            Validation.Assert(!IsResolved);
            return new JumpIf(Depth, Condition, Sense, addr, leaveCount);
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            if (!Sense)
                sink.Write("Not");
            base.DumpExtra(sink);
            sink.TWrite(' ').Write(Condition.ParseTree.ToString());
        }
    }

    /// <summary>
    /// Evaluate an expression and display/report the resulting value.
    /// The expression may be an invocation of a procedure. When it is, the procedure invocation is evaluated
    /// to produce a "task". The task is played to completion and its primary result is used as the resulting
    /// "value".
    /// </summary>
    public sealed class Expr : Instruction
    {
        /// <summary>
        /// The value.
        /// </summary>
        public RexlFormula Value { get; }

        internal Expr(int depth, RexlFormula value)
            : base(InstructionKind.Expr, depth)
        {
            Validation.AssertValue(value);
            Value = value;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.TWrite(' ').Write(Value.ParseTree.ToString());
        }
    }

    /// <summary>
    /// Evaluate an expression and assign its value to a path.
    /// Contains the value expression and the path.
    /// The expression may be an invocation of a procedure. When it is, the procedure invocation is evaluated
    /// to produce a "task". The task is played to completion and its primary result is used as the resulting
    /// "value"  (assigned to the path).
    /// </summary>
    public sealed class Define : Instruction
    {
        /// <summary>
        /// The definition kind.
        /// </summary>
        public DefnKind DefnKind { get; }

        /// <summary>
        /// The path/name for the value. Note that the actual name may depend on the current namespace state.
        /// </summary>
        public IdentPath Path { get; }

        /// <summary>
        /// The value.
        /// </summary>
        public RexlFormula Value { get; }

        internal Define(int depth, DefnKind dk, IdentPath path, RexlFormula value)
            : base(InstructionKind.Define, depth)
        {
            Validation.Assert((path == null) == (dk == DefnKind.This));
            Validation.AssertValue(value);
            DefnKind = dk;
            Path = path;
            Value = value;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.Write(' ');
            switch (DefnKind)
            {
            case DefnKind.None:
                Validation.Assert(Path != null);
                AppendName(sink, Path);
                break;
            case DefnKind.This:
                sink.Write("this");
                break;
            default:
                Validation.Assert(Path != null);
                sink.TWrite(DefnKind.ToString()).Write(' ');
                AppendName(sink, Path);
                break;
            }
            sink.TWrite(" <- ").Write(Value.ParseTree.ToString());
        }
    }

    /// <summary>
    /// Define a user defined function.
    /// Contains the name/path, the parameter names, the expression body, and whether the function can be used as
    /// a property.
    /// </summary>
    public sealed class DefineFunc : Instruction
    {
        /// <summary>
        /// Whether this can be used as a property.
        /// </summary>
        public bool IsProp { get; }

        /// <summary>
        /// The path/name for the UDF. Note that the actual name may be dependent on the current namespace state.
        /// </summary>
        public IdentPath Path { get; }

        /// <summary>
        /// The parameter names.
        /// </summary>
        public NameTuple ParamNames { get; }

        /// <summary>
        /// The body of the function.
        /// </summary>
        public RexlFormula Body { get; }

        internal DefineFunc(int depth, IdentPath path, NameTuple names, RexlFormula body, bool isProp)
            : base(InstructionKind.DefineFunc, depth)
        {
            Validation.AssertValue(path);
            Validation.Assert(!names.IsDefault);
            Validation.AssertValue(body);
            IsProp = isProp;
            Path = path;
            ParamNames = names;
            Body = body;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            if (IsProp)
                sink.Write(" [prop]");
            sink.Write(' ');
            AppendName(sink, Path);
            sink.Write('(');
            string pre = "";
            foreach (var name in ParamNames)
            {
                sink.TWrite(pre).WriteEscapedName(name);
                pre = ", ";
            }
            sink.TWrite(") <- ").Write(Body.ParseTree.ToString());
        }
    }

    /// <summary>
    /// Define a user defined procedure.
    /// Contains the name/path, the parameter names, the prime block, and the play block.
    /// </summary>
    public sealed class DefineProc : Instruction
    {
        /// <summary>
        /// The path/name for the UDP. Note that the actual name may be dependent on the current namespace state.
        /// </summary>
        public IdentPath Path { get; }

        /// <summary>
        /// The parameter names.
        /// </summary>
        public NameTuple ParamNames { get; }

        /// <summary>
        /// The outer <see cref="RexlStmtList"/> containing the <see cref="Prime"/> and <see cref="Play"/> blocks.
        /// </summary>
        public RexlStmtList Outer { get; }

        /// <summary>
        /// The (optional) block of statements for priming.
        /// </summary>
        public BlockStmtNode Prime { get; }

        /// <summary>
        /// The block of statements defining the body of the task.
        /// </summary>
        public BlockStmtNode Play { get; }

        internal DefineProc(int depth, IdentPath path, NameTuple names,
                RexlStmtList outer, BlockStmtNode prime, BlockStmtNode play)
            : base(InstructionKind.DefineProc, depth)
        {
            Validation.AssertValue(path);
            Validation.Assert(!names.IsDefault);
            Validation.AssertValue(outer);
            Validation.Assert(prime == null || outer.InTree(prime));
            Validation.AssertValue(play);
            Validation.Assert(outer.InTree(play));
            Path = path;
            ParamNames = names;
            Outer = outer;
            Prime = prime;
            Play = play;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.Write(' ');
            AppendName(sink, Path);
            sink.Write('(');
            string pre = "";
            foreach (var name in ParamNames)
            {
                sink.TWrite(pre).WriteEscapedName(name);
                pre = ", ";
            }
            sink.Write(')');
            if (Prime != null)
                sink.Write(" prime { ... }");
            sink.Write(" play { ... }");
        }
    }

    public sealed class TaskCmd : Instruction
    {
        /// <summary>
        /// The "command".
        /// REVIEW: Use something better than TokKind for this.
        /// </summary>
        public TokKind Cmd { get; }

        /// <summary>
        /// The path/name of the task. This is used to lookup the task, but may not be the tasks full name.
        /// </summary>
        public IdentPath Path { get; }

        internal TaskCmd(int depth, TokKind cmd, IdentPath path)
            : base(InstructionKind.TaskCmd, depth)
        {
            Validation.Assert(cmd.IsTaskCmd());
            Validation.AssertValue(path);
            Cmd = cmd;
            Path = path;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.TWrite(' ').TWrite(RexlLexer.Instance.GetFixedText(Cmd)).Write(' ');
            AppendName(sink, Path);
        }
    }

    /// <summary>
    /// Creates a task from an invocation of a procedure.
    /// Contains an optional path, a task "modifier", and the procedure invocation.
    /// Evaluates the procedure invocation to produce the task, assigns the task to the (optional) path, and
    /// applies the command to the task (eg, "play", "finish", etc).
    /// </summary>
    public sealed class TaskProc : Instruction
    {
        /// <summary>
        /// The "modifier" token, <c>task</c>, <c>prime</c>, etc.
        /// </summary>
        public Token ModTok { get; }

        /// <summary>
        /// The modifier, <c>task</c>, <c>play</c>, etc, as a <see cref="TokKind"/>.
        /// REVIEW: Use something better than TokKind for this.
        /// </summary>
        public TokKind Modifier => ModTok.Kind;

        /// <summary>
        /// The (optional) path/name for the task. Note that the actual name may depend on the current
        /// namespace state.
        /// </summary>
        public IdentPath Path { get; }

        /// <summary>
        /// The value.
        /// </summary>
        public RexlFormula Value { get; }

        internal TaskProc(int depth, Token tokMod, IdentPath path, RexlFormula value)
            : base(InstructionKind.TaskProc, depth)
        {
            Validation.AssertValue(tokMod);
            Validation.AssertValueOrNull(path);
            Validation.AssertValue(value);
            ModTok = tokMod;
            Path = path;
            Value = value;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.TWrite(' ').TWrite(RexlLexer.Instance.GetFixedText(Modifier)).Write(' ');
            if (Path != null)
            {
                AppendName(sink, Path);
                sink.Write(" as ");
            }
            sink.Write(Value.ParseTree.ToString());
        }
    }

    /// <summary>
    /// Creates a task from an inline block of statements.
    /// Contains a path, a task "modifier", the block of statements, and an optional record expression
    /// that provides initial "globals" in the task script.
    /// </summary>
    public sealed class TaskBlock : Instruction
    {
        /// <summary>
        /// The "modifier" token, <c>task</c>, <c>prime</c>, etc.
        /// </summary>
        public Token ModTok { get; }

        /// <summary>
        /// The modifier, <c>task</c>, <c>play</c>, etc, as a <see cref="TokKind"/>.
        /// REVIEW: Use something better than TokKind for this.
        /// </summary>
        public TokKind Modifier => ModTok.Kind;

        /// <summary>
        /// The path/name for the task. Note that the actual name may depend on the current
        /// namespace state.
        /// </summary>
        public IdentPath Path { get; }

        /// <summary>
        /// An optional expression that should be record valued and defines initial "globals" for
        /// the task script.
        /// </summary>
        public RexlFormula Globals { get; }

        /// <summary>
        /// The (optional) block of statements for priming.
        /// </summary>
        public BlockStmtNode Prime { get; }

        /// <summary>
        /// The block of statements defining the body of the task.
        /// </summary>
        public BlockStmtNode Body { get; }

        internal TaskBlock(int depth, Token tokCmd, IdentPath path, RexlFormula globals,
                BlockStmtNode prime, BlockStmtNode body)
            : base(InstructionKind.TaskBlock, depth)
        {
            Validation.AssertValue(tokCmd);
            Validation.AssertValue(path);
            Validation.AssertValueOrNull(globals);
            Validation.AssertValueOrNull(prime);
            Validation.AssertValue(body);
            ModTok = tokCmd;
            Path = path;
            Globals = globals;
            Prime = prime;
            Body = body;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            sink.TWrite(' ').TWrite(RexlLexer.Instance.GetFixedText(Modifier)).Write(' ');
            AppendName(sink, Path);
            if (Globals != null)
                sink.TWrite(" with ").TWrite(Globals.ParseTree.ToString());
            if (Prime != null)
                sink.TWrite(" prime {...}");
        }
    }

    /// <summary>
    /// Set the current namespace.
    /// Contains the namespace specification.
    /// </summary>
    public sealed class Namespace : Instruction
    {
        /// <summary>
        /// The namespace specification.
        /// </summary>
        public NamespaceSpec NsSpec { get; }

        internal Namespace(int depth, NamespaceSpec nss)
            : base(InstructionKind.Namespace, depth)
        {
            Validation.AssertValue(nss);
            NsSpec = nss;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            AppendNamespace(sink, NsSpec);
        }
    }

    /// <summary>
    /// Add "with" names.
    /// Contains the paths/names of the withs to add.
    /// </summary>
    public sealed class With : Instruction
    {
        /// <summary>
        /// The paths.
        /// </summary>
        public Immutable.Array<IdentPath> Paths { get; }

        internal With(int depth, Immutable.Array<IdentPath> paths)
            : base(InstructionKind.With, depth)
        {
            Validation.Assert(!paths.IsDefault);
            Paths = paths;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);
            string pre = " ";
            foreach (var path in Paths)
            {
                sink.Write(pre);
                AppendName(sink, path);
                pre = ", ";
            }
        }
    }

    /// <summary>
    /// Import into an optional namespace.
    /// Contains an expression specifying what to import and an optional namespace specification.
    /// </summary>
    public sealed class Import : Instruction
    {
        /// <summary>
        /// The expression specifying what to import.
        /// </summary>
        public RexlFormula Value { get; }

        /// <summary>
        /// The optional namespace specification.
        /// </summary>
        public NamespaceSpec NsSpec { get; }

        internal Import(int depth, RexlFormula value, NamespaceSpec nss)
            : base(InstructionKind.Import, depth)
        {
            Validation.AssertValue(value);
            Validation.AssertValueOrNull(nss);
            Value = value;
            NsSpec = nss;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);

            if (NsSpec != null)
            {
                sink.Write(" in");
                AppendNamespace(sink, NsSpec);
            }

            sink.TWrite(": ").Write("{0}", Value.ParseTree);
        }
    }

    /// <summary>
    /// Execute a script specified as a text value in the context of an optional namespace.
    /// Contains an expression specifying the script text and an optional namespace specification.
    /// </summary>
    public sealed class Execute : Instruction
    {
        /// <summary>
        /// The expression specifying the script text.
        /// </summary>
        public RexlFormula Value { get; }

        /// <summary>
        /// The optional namespace specification.
        /// </summary>
        public NamespaceSpec NsSpec { get; }

        internal Execute(int depth, RexlFormula value, NamespaceSpec nss)
            : base(InstructionKind.Execute, depth)
        {
            Validation.AssertValue(value);
            Validation.AssertValueOrNull(nss);
            Value = value;
            NsSpec = nss;
        }

        protected override void DumpExtra(TextSink sink)
        {
            Validation.AssertValue(sink);

            if (NsSpec != null)
            {
                sink.Write(" in");
                AppendNamespace(sink, NsSpec);
            }

            sink.TWrite(": ").Write("{0}", Value.ParseTree);
        }
    }
}
