// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Statement;

namespace Microsoft.Rexl.Harness;

// This partial contains interpreter functionality.
partial class HarnessBase
{
    /// <summary>
    /// The <see cref="StmtInterp"/> for the harness. Just delegates to the harness.
    /// </summary>
    private sealed class Interp : StmtInterp
    {
        private readonly HarnessBase _harness;

        public Interp(HarnessBase harness)
        {
            _harness = harness;
        }

        protected override Stream GetSuspendState(SuspendException ex) => _harness.GetSuspendState(ex);
        public Stream GetSuspendStateInner() => GetSuspendStateCore();

        protected override bool TryGetStreamForSuspend(out Stream strm) => _harness.TryGetStreamForSuspend(out strm);
        public override void WriteState(Stream strm)
        {
            _harness.WritePreInterpState(strm);
            base.WriteState(strm);
            _harness.WritePostInterpState(strm);
        }

        protected override void PreInst(Instruction inst) => _harness.PreInst(ScrCur.Pos, inst);
        protected override bool IsNamespace(NPath path) => _harness.IsGeneralNamespace(path);
        protected override void Error(Token tok, StringId msg) => _harness.StmtError(tok, msg);
        protected override Task<bool> HandleAsync(Instruction.Expr inst) => _harness.HandleAsync(ScrCur.Pos, inst);
        protected override Task<bool> HandleAsync(Instruction.Define inst) => _harness.HandleAsync(ScrCur.Pos, inst);
        protected override bool Handle(Instruction.DefineFunc inst) => _harness.Handle(ScrCur.Pos, inst);
        protected override bool Handle(Instruction.DefineProc inst) => _harness.Handle(ScrCur.Pos, inst);
        protected override Task<bool> HandleAsync(Instruction.Import inst) => _harness.HandleAsync(ScrCur.Pos, inst);
        protected override bool Handle(Instruction.Execute inst) => _harness.Handle(ScrCur.Pos, inst);
        protected override Task<bool> HandleAsync(Instruction.TaskCmd inst) => _harness.HandleAsync(ScrCur.Pos, inst);
        protected override Task<bool> HandleAsync(Instruction.TaskProc inst) => _harness.HandleAsync(ScrCur.Pos, inst);
        protected override Task<bool> HandleAsync(Instruction.TaskBlock inst) => _harness.HandleAsync(ScrCur.Pos, inst);
        protected override bool TryTestCondition(RexlFormula cond, out bool value) => _harness.TryTestCondition(cond, out value);
    }

    /// <summary>
    /// The root source context.
    /// </summary>
    protected SourceContext SourceRoot => _interp.SourceRoot;

    /// <summary>
    /// The current source context.
    /// </summary>
    protected SourceContext SourceCur => _interp.SourceCur;

    // The current namespace information.
    protected NPath NsRoot => _interp.NsRoot;
    protected NPath NsCur => _interp.NsCur;
    protected NPath NsRel => _interp.NsRel;

    /// <summary>
    /// Compile the statement list to a flow.
    /// </summary>
    protected virtual StmtFlow CreateFlow(RexlStmtList rsl)
    {
        return StmtFlow.Create(rsl);
    }

    /// <summary>
    /// Suspend execution. To write additional information in the stream (besides the interpreter state)
    /// override <see cref="WritePreInterpState(Stream)"/> for information before the interpreter state and
    /// override <see cref="WritePostInterpState(Stream)"/> for information after the interpreter state.
    /// 
    /// Note that this does not return.
    /// </summary>
    [DoesNotReturn]
    protected virtual StmtInterp.SuspendException Suspend<T>(T cookie, string msg = null)
    {
        return _interp.Suspend(cookie, msg);
    }

    /// <summary>
    /// Write information to the state stream before the core interpreter state.
    /// </summary>
    protected virtual void WritePreInterpState(Stream strm)
    {
        Validation.AssertValue(strm);
    }

    /// <summary>
    /// Write information to the state stream after the core interpreter state.
    /// </summary>
    protected virtual void WritePostInterpState(Stream strm)
    {
        Validation.AssertValue(strm);
    }

    /// <summary>
    /// The default implementation creates a new memory stream.
    /// </summary>
    protected virtual bool TryGetStreamForSuspend(out Stream strm)
    {
        strm = new MemoryStream();
        return true;
    }

    /// <summary>
    /// Given the state stream for a suspension, process it in any appropriate way. Note that the state
    /// may be null, indicating that resuming will not be possible.
    /// </summary>
    protected virtual Stream GetSuspendState(StmtInterp.SuspendException ex)
    {
        Validation.AssertValueOrNull(ex);

        var exb = ex as StmtInterp.SuspendException<bool>;
        bool abort = exb?.Cookie ?? false;
        var res = abort ? null : _interp.GetSuspendStateInner();

        if (IsVerbose)
            Sink.TWriteLine().WriteLine(res != null ? "*** Suspended ***" : "*** Aborted ***");

        return res;
    }

    /// <summary>
    /// Called before each instruction is executed.
    /// </summary>
    protected virtual void PreInst(int pos, Instruction inst)
    {
    }

    protected virtual Task<bool> HandleAsync(int pos, Instruction.Expr inst)
    {
        Validation.AssertValue(inst);

        var fma = inst.Value;
        if (!TryBind(fma, BindOptions.AllowImpure | BindOptions.AllowGeneral, out var bnd))
            return Task.FromResult(false);
        if (!bnd.IsProcCall)
            return HandleValueAsync(fma, bnd);
        return HandleValueFromProcAsync(fma, bnd);
    }

    protected virtual Task<bool> HandleValueAsync(RexlFormula fma, BoundNode bnd)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(!bnd.IsProcCall);

        bool res = TryExecPure(fma, bnd, out var value, out var ctx);
        return Task.FromResult(res && ProcessExpr(bnd, value, ctx));
    }

    protected virtual async Task<bool> HandleValueFromProcAsync(RexlFormula fma, BoundNode bnd)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall);

        var (res, runner, ctx) = await ExecProcAsync(fma, bnd, TaskExecKind.Primary).ConfigureAwait(false);
        Validation.Assert(runner != null || !res);
        ProcessStateChanges();
        return res && ProcessExpr(bnd, runner, ctx);
    }

    private Task<bool> HandleAsync(int pos, Instruction.Define inst)
    {
        Validation.AssertValue(inst);

        var fma = inst.Value;
        if (!TryBind(fma, BindOptions.AllowImpure | BindOptions.AllowGeneral, out var bnd))
            return Task.FromResult(false);

        var name = inst.Path?.Combine(NsCur, NsRoot) ?? default;
        if (!bnd.IsProcCall)
            return HandleDefineAsync(inst.DefnKind, name, fma, bnd);
        return HandleDefineFromProcAsync(name, fma, bnd);
    }

    protected virtual Task<bool> HandleDefineAsync(DefnKind dk, NPath name, RexlFormula fma, BoundNode bnd)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(!bnd.IsProcCall);

        bool res = TryExecPure(fma, bnd, out var value, out var ctx);
        return Task.FromResult(ProcessDefinition(!res, dk, name, bnd, value));
    }

    protected virtual async Task<bool> HandleDefineFromProcAsync(NPath name, RexlFormula fma, BoundNode bnd)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall);

        var (res, runner, _) = await ExecProcAsync(fma, bnd, TaskExecKind.Primary).ConfigureAwait(false);
        Validation.Assert(runner != null || !res);
        // REVIEW: DefnKind should be respected!
        res = ProcessDefinition(!res, DefnKind.None, name, bnd, runner);

        // REVIEW: Should this go before ProcessDefinition?
        ProcessStateChanges();
        return res;
    }

    private bool Handle(int pos, Instruction.DefineFunc inst)
    {
        Validation.AssertValue(inst);

        var name = inst.Path.Combine(NsCur, NsRoot);
        var udf = UserFunc.Create(name, NsRoot, inst.ParamNames, inst.Body, inst.IsProp);
        SetUserOper(udf, out var prev, out var cur);
        HandleUserOper(udf, prev, cur);
        return true;
    }

    private bool Handle(int pos, Instruction.DefineProc inst)
    {
        Validation.AssertValue(inst);

        var name = inst.Path.Combine(NsCur, NsRoot);
        var udp = UserProc.Create(name, NsRoot, inst.ParamNames, inst.Outer, inst.Prime, inst.Play);
        SetUserOper(udp, out var prev, out var cur);
        HandleUserOper(udp, prev, cur);
        return true;
    }

    private Task<bool> HandleAsync(int pos, Instruction.Import inst)
    {
        Validation.AssertValue(inst);

        if (!TryBind(inst.Value, BindOptions.AllowVolatile, out var bnd))
            return Task.FromResult(false);
        Validation.Assert(!bnd.IsProcCall);
        return HandleImportCoreAsync(inst, bnd);
    }

    protected virtual Task<bool> HandleImportCoreAsync(Instruction.Import inst, BoundNode bnd)
    {
        Validation.AssertValue(inst);
        Validation.AssertValue(bnd);

        bool ok = TryExecPure(inst.Value, bnd, out var value, out var ctx);
        if (!ok)
            return Task.FromResult(false);

        DType type = bnd.Type;
        Type st = _codeGen.TypeManager.GetSysTypeOrNull(type).VerifyValue();
        Validation.Assert(value == null || st.IsAssignableFrom(value.GetType()));

        return HandleImportAsync(inst.Value, bnd, st, value, inst.NsSpec);
    }

    protected virtual async Task<bool> HandleImportAsync(RexlFormula fma, BoundNode bnd, Type st, object value, NamespaceSpec nss)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.AssertValue(st);
        Validation.Assert(value == null || st.IsAssignableFrom(value.GetType()));
        Validation.AssertValueOrNull(nss);

        Link link;
        switch (bnd.Type.Kind)
        {
        default:
            StmtError(fma.ParseTree, ErrorStrings.ErrImportBadType_Type, bnd.Type);
            return false;

        case DKind.Text:
            Validation.Assert(st == typeof(string));
            {
                var str = (string)value;
                if (string.IsNullOrEmpty(str))
                {
                    StmtError(fma.ParseTree, ErrorStrings.ErrImportEmptyPath);
                    return false;
                }
                link = Link.CreateGeneric(str);
            }
            break;

        case DKind.Uri:
            Validation.Assert(st == typeof(Link));
            link = (Link)value;
            if (link is null)
            {
                StmtError(fma.ParseTree, ErrorStrings.ErrImportEmptyPath);
                return false;
            }

            {
                var flavor = bnd.Type.GetRootUriFlavor();
                if (flavor.NameCount < 2 || flavor.First != "Text" || flavor.TrimTail(2).Leaf != "Rexl")
                {
                    StmtError(fma.ParseTree, ErrorStrings.ErrImportBadFlavor_Flavor, flavor);
                    return false;
                }
            }
            break;
        }

        if (!TestExecNest(fma.ParseTree))
            return false;

        string script;
        Link full;
        Stream stream;
        try
        {
            (full, stream) = await LoadStreamForImportAsync(SourceCur.LinkCtx, link).ConfigureAwait(false);
            using (var rdr = new StreamReader(stream))
                script = await rdr.ReadToEndAsync().ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            HandleImportException(ex);
            return false;
        }

        var source = SourceContext.Create(full, link.GetLocalPath(), script);
        return PushScript(source, nss, forImport: true);
    }

    protected virtual bool TestExecNest(ExprNode node)
    {
        Validation.AssertValue(node);

        // REVIEW: What limit should we use?
        if (_interp.ScriptDepth <= 10)
            return true;

        StmtError(node, ErrorStrings.ErrExecTooDeep);
        return false;
    }

    private bool Handle(int pos, Instruction.Execute inst)
    {
        Validation.AssertValue(inst);

        // REVIEW: Need a "bind to type".
        if (!TryBind(inst.Value, BindOptions.AllowVolatile, out var bnd))
            return false;
        Validation.Assert(!bnd.IsProcCall);

        if (bnd.Type != DType.Text)
        {
            StmtError(inst.Value.ParseTree, ErrorStrings.ErrExecuteNeedsText_Type_Type, DType.Text, bnd.Type);
            return false;
        }

        return HandleExecuteCore(inst, bnd);
    }

    protected virtual bool HandleExecuteCore(Instruction.Execute inst, BoundNode bnd)
    {
        bool ok = TryExecPure(inst.Value, bnd, out var value, out var ctx);
        if (!ok)
            return false;

        if (value == null)
            return true;
        Validation.Assert(value is string);

        if (!TestExecNest(inst.Value.ParseTree))
            return false;

        var script = (string)value;
        // Note that the linkCtx and linkFull are different intentionally. There is no link to the source,
        // since it is a computed string, but we want to inherit the context.
        var source = SourceContext.Create(SourceCur.LinkCtx, null, null, script);
        return PushScript(source, inst.NsSpec, forImport: false);
    }

    private bool PushScript(SourceContext source, NamespaceSpec nss, bool forImport)
    {
        Validation.AssertValue(source);
        Validation.AssertValueOrNull(nss);

        var rsl = RexlStmtList.Create(source);
        if (rsl.HasDiagnostics)
        {
            HandleParseIssues(rsl.Diagnostics);
            if (rsl.HasErrors)
                return false;
        }

        StmtFlow flow = CreateFlow(rsl);
        if (flow.HasDiagnostics)
        {
            HandleFlowIssues(flow.Diagnostics);
            if (flow.HasErrors)
                return false;
        }

        return PushScriptCore(source, flow, nss, forImport);
    }

    protected virtual bool PushScriptCore(
        SourceContext source, StmtFlow flow, NamespaceSpec nss, bool forImport, bool? recover = null)
    {
        return _interp.PushScript(source, flow, nss, forImport, recover ?? _interp.Recover);
    }

    private Task<bool> HandleAsync(int pos, Instruction.TaskCmd inst)
    {
        Validation.AssertValue(inst);
        Validation.AssertValue(inst.Path);
        var name = inst.Path.Combine(NsCur, NsRoot);
        var tek = GetTek(inst.Cmd);
        return ProcessTaskCmdAsync(tek, inst.Path, name);
    }

    private Task<bool> HandleAsync(int pos, Instruction.TaskProc inst)
    {
        Validation.AssertValue(inst);

        if (!TryBind(inst.Value, BindOptions.AllowImpure | BindOptions.AllowGeneral, out var bnd))
            return Task.FromResult(false);

        if (!bnd.IsProcCall)
        {
            StmtError(inst.Value.ParseTree, ErrorStrings.ErrFuncUsedAsProc);
            return Task.FromResult(false);
        }

        var tek = GetTek(inst.Modifier);
        return HandleTaskAsync(tek, inst.Path != null ? inst.Path.Combine(NsCur, NsRoot) : default, inst.Value, bnd);
    }

    protected virtual async Task<bool> HandleTaskAsync(TaskExecKind tek, NPath name, RexlFormula fma, BoundNode bnd)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall);

        var (res, runner, _) = await ExecProcAsync(fma, bnd, tek);
        Validation.Assert(runner != null || !res);
        res = ProcessTaskDefinition(!res, name, bnd, runner);

        // REVIEW: Should this go before ProcessTaskDefinition?
        ProcessStateChanges();
        return res;
    }

    private Task<bool> HandleAsync(int pos, Instruction.TaskBlock inst)
    {
        Validation.AssertValue(inst);

        BoundNode bndWith = null;
        RexlFormula fmaWith = inst.Globals;
        if (fmaWith != null)
        {
            if (!TryBind(fmaWith, BindOptions.AllowVolatile, out bndWith))
                return Task.FromResult(false);
            if (!bndWith.Type.IsRecordReq)
            {
                StmtError(fmaWith.ParseTree, ErrorStrings.ErrTaskWithNeedRecord);
                return Task.FromResult(false);
            }
        }

        var tek = GetTek(inst.Modifier);
        return HandleTaskBlockAsync(tek, inst.Path.Combine(NsCur, NsRoot), bndWith, fmaWith, inst.Prime, inst.Body);
    }

    protected virtual async Task<bool> HandleTaskBlockAsync(TaskExecKind tek, NPath name,
        BoundNode bndWith, RexlFormula fmaWith, BlockStmtNode prime, BlockStmtNode body)
    {
        Validation.Assert(!name.IsRoot);
        Validation.AssertValueOrNull(bndWith);
        Validation.Assert((bndWith is null) == (fmaWith is null));
        Validation.AssertValueOrNull(prime);
        Validation.AssertValue(body);

        List<ActionRunner> taskDeps = null;
        RecordBase with = null;
        DType typeWith = bndWith?.Type ?? DType.EmptyRecordReq;
        if (typeWith.FieldCount > 0)
        {
            if (!TryExecWith(fmaWith, bndWith, out with, out _, out taskDeps))
                return false;
            Validation.Assert(with != null);
        }

        var runner = CreateBlockActionRunner(typeWith, with, _interp.FlowCur.StmtList, prime, body);
        await RegisterRunner(runner, tek, taskDeps);
        bool res = ProcessTaskBlockDefinition(name, runner);

        // REVIEW: Should this go before ProcessTaskBlockDefinition?
        ProcessStateChanges();
        return res;
    }

    private bool TryTestCondition(RexlFormula cond, out bool value)
    {
        Validation.AssertValue(cond);

        // REVIEW: Need a "bind to type".
        if (!TryBind(cond, BindOptions.AllowVolatile, out var bnd))
        {
            value = false;
            return false;
        }
        Validation.Assert(!bnd.IsProcCall);

        if (bnd.Type != DType.BitReq)
        {
            StmtError(cond.ParseTree, ErrorStrings.ErrIfNeedsBoolCondition_Type_Type, DType.BitReq, bnd.Type);
            value = false;
            return false;
        }

        bool ok = TryExecPure(cond, bnd, out var val, out var ctx);
        if (!ok)
        {
            value = false;
            return false;
        }

        Validation.Assert(val is bool);
        value = (bool)val;
        return true;
    }

    /// <summary>
    /// Process a global definition. If <paramref name="bnd"/> is impure, the <paramref name="value"/>
    /// is the action runner, and not its default value.
    /// </summary>
    protected abstract bool ProcessDefinition(bool error, DefnKind dk, NPath name, BoundNode bnd, object value);

    /// <summary>
    /// Process a task definition.
    /// </summary>
    protected abstract bool ProcessTaskDefinition(bool error, NPath name, BoundNode bnd, ActionRunner runner);

    /// <summary>
    /// Process a task block definition.
    /// </summary>
    protected abstract bool ProcessTaskBlockDefinition(NPath name, ActionRunner runner);

    /// <summary>
    /// Process an expression, doing whatever is appropriate with the value.
    /// </summary>
    protected abstract bool ProcessExpr(BoundNode bnd, object value, ExecCtx ctx);
}
