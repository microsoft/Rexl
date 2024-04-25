// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Harness;

// This partial is for evaluation of expressions.
partial class HarnessBase
{
    // REVIEW: Does base harness need a code generator and type manager or does that
    // really belong in the SimpleHarness?
    protected readonly CodeGeneratorBase _codeGen;

    /// <summary>
    /// The type manager.
    /// </summary>
    public TypeManager TypeManager => _codeGen.TypeManager;

    /// <summary>
    /// Tries to evaluate the given <paramref name="source"/> as an expression in the context of this harness
    /// and returns the resulting value and type when the intput is a valid Rexl expression.
    /// </summary>
    public bool TryEvaluateExpression(SourceContext source,
        out object value, out DType type, out RexlFormula fma, out BoundFormula bfma, out Exception evalEx)
    {
        Validation.CheckValue(source, nameof(source));
        Validation.BugCheck(!_interp.IsActive, "Cannot evaluate expression while harness is actively executing");

        evalEx = null;
        fma = RexlFormula.Create(source);
        if (fma.HasErrors)
        {
            value = null;
            type = default;
            bfma = null;
            return false;
        }

        bfma = BoundFormula.Create(fma, _host, BindOptions.AllowVolatile);
        if (!bfma.IsGood)
        {
            value = null;
            type = default;
            return false;
        }

        var bnd = bfma.BoundTree;
        type = bnd.Type;

        try
        {
            var resCodeGen = GenCode(bnd);
            Validation.Assert(resCodeGen.Func != null);

            object[] args;
            int carg = resCodeGen.Globals.Length;
            if (carg > 0)
            {
                args = new object[carg];
                FillExecArgs(bnd, resCodeGen, forTask: false, args, out _, out var taskDeps);
                Validation.Assert(taskDeps == null);
            }
            else
                args = Array.Empty<object>();

            value = resCodeGen.Func(args);
            Validation.Assert(IsValueValid(resCodeGen.BoundTree.Type, resCodeGen.SysType, value));
        }
        catch (Exception ex)
        {
            value = null;
            evalEx = ex;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns whether the given <paramref name="value"/> is valid for the given <paramref name="type"/>
    /// and corresponding system type <paramref name="st"/>. This is mostly used for asserts/validation.
    /// </summary>
    protected bool IsValueValid(DType type, Type st, object value)
    {
        Validation.Assert(type.IsValid);
        Validation.AssertValue(st);
        Validation.Assert(TypeManager.GetSysTypeOrNull(type) == st);

        if (value is null)
            return type.IsOpt;
        return st.IsAssignableFrom(value.GetType());
    }

    /// <summary>
    /// Generate code for the given <see cref="BoundNode"/>.
    /// </summary>
    protected virtual CodeGenResult GenCode(BoundNode bnd)
    {
        StringBuilder sb = null;
        Action<string> ilSink = null;
        if (_config.ShowIL)
        {
            sb = new StringBuilder();
            ilSink = str => sb.AppendLine(str ?? "");
        }

        // Generate the code.
        var resCodeGen = GenCodeCore(bnd, host: null, ilSink);

        if (sb != null)
            HandleIL(sb.ToString());

        return resCodeGen;
    }

    /// <summary>
    /// Write IL to output.
    /// </summary>
    protected virtual void HandleIL(string il)
    {
        if (il.Length > 0)
            Sink.TWriteLine().TWrite(il).Flush();
    }

    /// <summary>
    /// Core method to generate code for a <see cref="BoundNode"/>. This includes parameters for
    /// capturing the IL.
    /// </summary>
    protected CodeGenResult GenCodeCore(BoundNode bnd, CodeGenHost? host = null,
        Action<string> ilSink = null, ILLogKind logKind = ILLogKind.None)
    {
        Validation.BugCheckValue(bnd, nameof(bnd));
        return _codeGen.Run(bnd, host, ilSink, logKind);
    }

    /// <summary>
    /// Creates an execution context to use for formula evaluation.
    /// </summary>
    protected abstract ExecCtx CreateExecCtx(CodeGenResult resCodeGen);

    /// <summary>
    /// Creates an action host.
    /// </summary>
    protected virtual ActionHost CreateActionHost()
    {
        return new ActionHostImpl(this, SourceCur);
    }

    /// <summary>
    /// If there is a global with the given name, set <paramref name="value"/> appropriately
    /// and return true.
    /// </summary>
    protected abstract bool TryGetGlobalValue(NPath name, DType type, out object value);

    /// <summary>
    /// Fill the arguments array for an invocation of a code-generated delegate.
    /// </summary>
    protected virtual void FillExecArgs(BoundNode bnd, CodeGenResult resCodeGen, bool forTask,
        object[] args, out ExecCtx ctx, out List<ActionRunner> taskDeps)
    {
        Validation.AssertValue(bnd);
        Validation.AssertValue(resCodeGen);
        Validation.AssertValue(args);

        var globals = resCodeGen.Globals;
        Validation.Assert(args.Length == globals.Length);

        ctx = null;
        taskDeps = null;
        if (args.Length == 0)
            return;

        // Build the args and identify task dependencies.
        for (int slot = 0; slot < globals.Length; slot++)
        {
            var glob = globals[slot];
            Validation.AssertValue(glob);
            Validation.Assert(glob.Slot == slot);

            if (glob.IsCtx)
            {
                // Execution context.
                Validation.Assert(glob.Name.IsRoot);
                args[slot] = ctx ??= CreateExecCtx(resCodeGen);
                continue;
            }

            object value;
            if (TryGetGlobalValue(glob.Name, glob.Type, out value))
            {
            }
            else if (TryGetTask(glob.Name.Parent, out var runner))
            {
                Expected(runner != null, "Dependency on null runner");
                Expected(_taskStates.TryGetValue(runner, out var state), "Runner dependency not tracked");
                Expected(runner.TryGetResultFromName(glob.Name.Leaf, out var info), "Runner result not found");
                Expected(glob.Type == info.Type, "Runner result has unexpected type");

                value = runner.GetResultValue(info);
                Validation.Assert(IsValueValid(glob.Type, glob.SysType, value));
                if (state != TaskState.Finished && info.IsStreaming)
                {
                    if (!forTask)
                    {
                        // Use a "snapshot" of the streaming result.
                        var snapper = value as ICanSnap;
                        value = snapper?.Snap();
                    }
                    else
                    {
#if DEBUG
                        // Binding should generate an error on eager stream use.
                        var bad = StreamAnalysis.FindEagerStreamUse(bnd, glob.Name);
                        Validation.Assert(bad == null);
#endif
                        if (taskDeps == null)
                            taskDeps = new List<ActionRunner>() { runner };
                        else if (!taskDeps.Contains(runner))
                            taskDeps.Add(runner);
                    }
                }
            }
            else
                throw Unexpected("Unknown global used");

            Validation.Assert(IsValueValid(glob.Type, glob.SysType, value));
            args[slot] = value;
        }
    }

    /// <summary>
    /// Throws an exception indicating an unexpected state.
    /// </summary>
    [DoesNotReturn]
    protected Exception Unexpected(string msg)
    {
        Validation.AssertValue(msg);
        Validation.Assert(false);
        throw new InvalidOperationException(msg);
    }

    /// <summary>
    /// Throws if the condition is false.
    /// </summary>
    protected void Expected(bool cond, string msg)
    {
        Validation.AssertValue(msg);
        if (!cond)
        {
            Validation.Assert(false);
            throw new InvalidOperationException(msg);
        }
    }

    /// <summary>
    /// Genererates code and executes the code to evaluate the given <see cref="BoundNode"/>. Sets
    /// <paramref name="value"/> to the result.
    /// </summary>
    private bool TryExecPure(RexlFormula fma, BoundNode bnd, out object value, out ExecCtx ctx)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(!bnd.IsProcCall);

        // Generate the code.
        var resCodeGen = GenCode(bnd);
        Validation.Assert(resCodeGen.Func != null);

        ctx = null;
        try
        {
            object[] args;
            int carg = resCodeGen.Globals.Length;
            if (carg > 0)
            {
                args = new object[carg];
                FillExecArgs(bnd, resCodeGen, forTask: false, args, out ctx, out var taskDeps);
                Validation.Assert(taskDeps == null);
            }
            else
                args = Array.Empty<object>();

            Validation.Assert(!_swExec.IsRunning);
            _swExec.Start();

            value = resCodeGen.Func(args);
            Validation.Assert(IsValueValid(resCodeGen.BoundTree.Type, resCodeGen.SysType, value));

            return true;
        }
        catch (Exception exCur)
        {
            if (!HandleExecException(exCur, fma))
                throw;
            value = null;
            return false;
        }
        finally
        {
            _swExec.Stop();
        }
    }

    private bool TryExecWith(RexlFormula fma, BoundNode bnd, out RecordBase rec, out ExecCtx ctx, out List<ActionRunner> taskDeps)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(!bnd.IsProcCall);
        Validation.Assert(bnd.Type.IsRecordReq);

        // Generate the code.
        var resCodeGen = GenCode(bnd);
        Validation.Assert(resCodeGen.Func != null);
        Validation.Assert(resCodeGen.SysType.IsSubclassOf(typeof(RecordBase)));

        ctx = null;
        taskDeps = null;
        try
        {
            // Build the args and identify task dependencies.
            object[] args;
            int carg = resCodeGen.Globals.Length;
            if (carg > 0)
            {
                args = new object[carg];
                FillExecArgs(bnd, resCodeGen, forTask: true, args, out ctx, out taskDeps);
            }
            else
                args = Array.Empty<object>();

            Validation.Assert(!_swExec.IsRunning);
            _swExec.Start();

            var value = resCodeGen.Func(args);
            Validation.Assert(IsValueValid(resCodeGen.BoundTree.Type, resCodeGen.SysType, value));
            rec = (RecordBase)value;

            return true;
        }
        catch (Exception exCur)
        {
            if (!HandleExecException(exCur, fma))
                throw;
            rec = null;
            return false;
        }
        finally
        {
            _swExec.Stop();
        }
    }

    /// <summary>
    /// Genererates code and executes the code to evaluate the given <see cref="BoundNode"/>, considering
    /// the given <see cref="TaskExecKind"/>. The <see cref="BoundNode"/> is asserted to be a procedure
    /// invocation. The result value is the <see cref="ActionRunner"/> instance. Note that, depending on the
    /// <paramref name="tek"/>, this may wait for the task to get into the desired state.
    /// 
    /// REVIEW: Perhaps change this to not wait for a state - callers should probably do that,
    /// perhaps after the runner to a name, etc.
    /// </summary>
    private async Task<(bool res, ActionRunner runner, ExecCtx ctx)> ExecProcAsync(
        RexlFormula fma, BoundNode bnd, TaskExecKind tek)
    {
        Validation.AssertValue(fma);
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall);

        // Generate the code.
        var resCodeGen = GenCode(bnd);
        Validation.Assert(resCodeGen.CreateRunnerFunc != null);

        _taskStateChanges?.Clear();
        ExecCtx ctx = null;
        try
        {
            // Build the args and identify task dependencies.
            List<ActionRunner> taskDeps = null;

            object[] args;
            int carg = resCodeGen.Globals.Length;
            if (carg > 0)
            {
                args = new object[carg];
                FillExecArgs(bnd, resCodeGen, forTask: true, args, out ctx, out taskDeps);
            }
            else
                args = Array.Empty<object>();

            Validation.Assert(!_swExec.IsRunning);
            _swExec.Start();

            // Invoke the code-gen'ed delegate to create the action runner.
            var runner = resCodeGen.CreateRunnerFunc(args, CreateActionHost());
            await RegisterRunnerAsync(runner, tek, taskDeps).ConfigureAwait(false);
            return (true, runner, ctx);
        }
        catch (Exception exCur)
        {
            if (!HandleExecException(exCur, fma))
                throw;
            return (false, null, ctx);
        }
        finally
        {
            _swExec.Stop();
        }
    }

    /// <summary>
    /// Registers a new <see cref="ActionRunner"/>, including setting the task's state according to
    /// <paramref name="tek"/> and processing dependencies.
    /// </summary>
    private async Task RegisterRunnerAsync(ActionRunner runner, TaskExecKind tek, List<ActionRunner> taskDeps)
    {
        Validation.AssertValue(runner);
        Validation.Assert(tek != TaskExecKind.None);
        Validation.AssertValueOrNull(taskDeps);

        // Start tracking this runner.
        TrackTask(runner);

        // Record the dependencies.
        if (taskDeps.Size() > 0)
        {
            foreach (var ar in taskDeps)
            {
                Validation.Assert(ar != null);
                if (_taskStates.TryGetValue(ar, out var st).Verify() && st != TaskState.Finished)
                    _taskDeps.Add((ar, runner));
            }
        }

        await ApplyTaskExecKindAsync(runner, tek).ConfigureAwait(false);
        if (tek == TaskExecKind.Primary)
        {
            Validation.Assert(runner.State == RunnerState.Done);
            if (!runner.WasSuccessful)
                throw runner.GetException();
        }
    }
}
