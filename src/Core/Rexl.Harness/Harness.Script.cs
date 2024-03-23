// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Statement;

namespace Microsoft.Rexl.Harness;

using Conditional = System.Diagnostics.ConditionalAttribute;
using StopWatch = System.Diagnostics.Stopwatch;

// This partial is for running and resuming scripts.
partial class HarnessBase
{
    /// <summary>
    /// The statement interpreter. This includes the namespace state and "withs" state.
    /// </summary>
    private readonly Interp _interp;

    private readonly StopWatch _swExec;

    /// <summary>
    /// Returns the execution time (so far).
    /// </summary>
    public TimeSpan ExecutionTime => _swExec.Elapsed;

    public async Task<(bool success, Stream suspendState)> RunAsync(SourceContext source, bool resetBefore)
    {
        Validation.BugCheckValue(source, nameof(source));
        Validation.BugCheck(!_interp.IsActive, "Harness is already actively executing");

        try
        {
            HandleRunBegin(source);
            if (resetBefore)
                await ResetAsync(init: true).ConfigureAwait(false);
            return await ProcessScriptAsync(source).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Validation.Assert(ex is not StmtInterp.SuspendException);
            if (!HandleRunException(source, ex))
                throw;
            return (false, null);
        }
        finally
        {
            HandleRunFinally(source);
        }
    }

    /// <summary>
    /// This resumes script execution from a checkpoint state.
    /// REVIEW: Currently only script interpreter state is saved, not values, tasks, modules, etc.
    /// </summary>
    public async Task<(bool success, Stream suspendState)> ResumeAsync(Stream resumeState)
    {
        Validation.BugCheckValue(resumeState, nameof(resumeState));
        Validation.BugCheck(!_interp.IsActive, "Harness is already actively executing");

        SourceContext source = null;
        try
        {
            return await _interp.ResumeAsync(resumeState,
                (interp, strm) =>
                {
                    Validation.AssertValue(interp);
                    Validation.AssertValue(strm);
                    source = interp.SourceRoot.VerifyValue();
                    HandleResume(source, interp.GetCurrentInstruction(out int pos), pos, strm);
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // SuspendException should be handled by _interp.Resume.
            Validation.Assert(ex is not StmtInterp.SuspendException);

            // Note that source may be null here.
            if (!HandleRunException(source, ex))
                throw;
            return (false, null);
        }
        finally
        {
            // Note that source may be null here.
            HandleRunFinally(source);
        }
    }

    private Task<(bool success, Stream suspendState)> ProcessScriptAsync(SourceContext source)
    {
        Validation.AssertValue(source);

        var rsl = RexlStmtList.Create(source);
        return ProcessScriptAsync(rsl);
    }

    protected async Task<(bool success, Stream suspendState)> ProcessScriptAsync(RexlStmtList rsl)
    {
        Validation.AssertValue(rsl);

        var swElap = StopWatch.StartNew();

        long ticksExe = 0;
        if (rsl.HasDiagnostics)
            HandleParseIssues(rsl.Diagnostics);

        Stream suspendState = null;
        bool fRet = !rsl.HasErrors;
        if (fRet)
        {
            StmtFlow flow = CreateFlow(rsl);
            if (flow.Diagnostics.Length > 0)
                HandleFlowIssues(flow.Diagnostics);

            if (flow.HasErrors)
                fRet = false;
            else
            {
                Validation.Assert(!_swExec.IsRunning);
                long ticksPre = _swExec.ElapsedTicks;

                (fRet, suspendState) = await _interp.RunAsync(rsl.Source, flow, recover: _config.ShouldContinue).ConfigureAwait(false);

                Validation.Assert(!_swExec.IsRunning);
                long ticksPost = _swExec.ElapsedTicks;

                ticksExe = ticksPost - ticksPre;
                Validation.Assert(ticksExe >= 0);
            }
        }

        Validation.Assert(swElap.IsRunning);
        swElap.Stop();

        HandleProcessScript(swElap.ElapsedTicks, ticksExe, fRet, ref suspendState);

        return (fRet, suspendState);
    }

    protected abstract ActionRunner CreateBlockActionRunner(DType typeWith, RecordBase with,
        RexlStmtList outer, BlockStmtNode prime, BlockStmtNode play);

    [Conditional("DEBUG")]
    protected void AssertValidWith(DType typeWith, RecordBase with)
    {
#if DEBUG
        if (with == null)
            Validation.Assert(typeWith.FieldCount == 0);
        else
        {
            Validation.Assert(TypeManager.TryEnsureSysType(typeWith, out Type st));
            Validation.Assert(st.IsAssignableFrom(with.GetType()));
        }
#endif
    }
}
