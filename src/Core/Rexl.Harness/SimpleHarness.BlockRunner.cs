// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Statement;

namespace Microsoft.Rexl.Harness;

// This partial is for wrapping rexl statements as a task.
partial class SimpleHarnessBase
{
    protected override ActionRunner CreateBlockActionRunner(DType typeWith, RecordBase with,
        RexlStmtList script, BlockStmtNode prime, BlockStmtNode play)
    {
        Validation.Assert(typeWith.IsRecordReq);
        Validation.AssertValue(script);
        Validation.Assert(prime == null || script.InTree(prime));
        Validation.AssertValue(play);
        Validation.Assert(script.InTree(play));
        AssertValidWith(typeWith, with);

        return new ScriptActionRunner(this, typeWith, with, script, prime, play);
    }

    protected override ActionRunner CreateUserProcRunner(UserProc proc, DType typeWith, RecordBase with)
    {
        Validation.AssertValue(proc);
        Validation.Assert(typeWith.IsRecordReq);
        AssertValidWith(typeWith, with);

        return new ScriptActionRunner(this, typeWith, with, proc.Outer, proc.Prime, proc.Play);
    }

    private sealed class ScriptActionRunner : ThreadActionRunner
    {
        private readonly InnerSink _sink;
        private readonly InnerHarness _harness;
        private readonly RexlStmtList _rslPrime;
        private readonly RexlStmtList _rslBody;
        private readonly List<object> _values;

        public ScriptActionRunner(SimpleHarnessBase outer, DType typeWith,
                RecordBase with, RexlStmtList rslOuter, BlockStmtNode prime, BlockStmtNode body)
            : base()
        {
            Validation.AssertValueOrNull(prime);
            Validation.AssertValue(body);

            _sink = new InnerSink(this);
            _harness = new InnerHarness(this, outer, typeWith, with);
            if (prime != null)
                _rslPrime = RexlStmtList.CreateSubStmtList(rslOuter, prime.Statements);
            _rslBody = RexlStmtList.CreateSubStmtList(rslOuter, body.Statements);
            _values = new List<object>();
        }

        protected override Task PrimeCoreAsync()
        {
            if (_rslPrime == null)
                return Task.CompletedTask;
            return RunStmtsAsync(_rslPrime);
        }

        protected override Task RunCoreAsync()
        {
            return RunStmtsAsync(_rslBody);
        }

        private async Task RunStmtsAsync(RexlStmtList rsl)
        {
            Validation.AssertValue(rsl);

            // REVIEW: Don't ignore the return values. If the return is false, can we assert that
            // there is already an exception recorded? Can we assert that the suspend state is null?
            // REVIEW: Ensure errors are reflected in task information.
            var (success, suspendState) = await _harness.RunAsync(rsl).ConfigureAwait(false);
            suspendState?.Dispose();
        }

        protected override Task CleanupCoreAsync()
        {
            return _harness.ResetAsync();
        }

        private void PreInst(int pos, Instruction inst)
        {
            // Allow pause and abort before every instruction.
            // REVIEW: Should we only do this for certain instruction kinds? For example,
            // it's kind of silly to yield immediately before an end instruction. Possibly also
            // for unconditional jumps and others. Although, if there is an infinite loop with
            // unconditional jumps, we need to yield somewhere, so perhaps also have a count of
            // instructions since the last yield.
            Yield();
        }

        private bool HandleException(Exception ex, RexlFormula fma)
        {
            Validation.AssertValue(ex);
            Validation.AssertValue(fma);

            // The exception should be thrown.
            return false;
        }

        private bool HandleRunException(SourceContext source, Exception ex)
        {
            // The exception should be thrown.
            return false;
        }

        private void HandleIssue(BaseDiagnostic issue)
        {
            Validation.AssertValue(issue);
            if (issue.IsError)
                throw new RexlErrorException(issue);
        }

        private void Publish(DefnKind dk, NPath name, DType type, object value)
        {
            if (name.NameCount != 1)
            {
                // REVIEW: What should this do? ActionRunner doesn't yet support multi-segment
                // names, so this should produce some diagnostic.
                return;
            }

            ResultInfo ri;
            switch (dk)
            {
            case DefnKind.Publish:
                ri = AddResult(name.Leaf.Value, type);
                break;
            case DefnKind.Primary:
                ri = AddResult(name.Leaf.Value, type, isPrimary: true);
                break;

            case DefnKind.Stream:
                // REVIEW: Need to figure out streaming...
                return;

            default:
                return;
            }

            if (ri.Index < _values.Count)
                _values[ri.Index] = value;
            else
            {
                Validation.Assert(ri.Index == _values.Count);
                _values.Add(value);
            }
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertIndex(info.Index, _values.Count);
            return _values[info.Index];
        }

        private sealed class InnerHarness : SimpleHarnessBase
        {
            private readonly SimpleHarnessBase _outer;
            private readonly ScriptActionRunner _runner;

            public override EvalSink Sink => _runner._sink;

            public InnerHarness(ScriptActionRunner runner, SimpleHarnessBase outer, DType typeWith, RecordBase with)
                : base(new HarnessConfig(verbose: false), outer._opers, outer._codeGen, outer._storage)
            {
                Validation.AssertValue(runner);
                Validation.AssertValue(outer);
                Validation.AssertValueOrNull(with);

                _runner = runner;
                _outer = outer;
                foreach (var (tn, st, val) in TypeManager.GetRecordFieldValues(typeWith, with))
                    SetGlobal(NPath.Root.Append(tn.Name), tn.Type, null, val);
            }

            public Task<(bool success, Stream suspendState)> RunAsync(RexlStmtList rsl)
            {
                return ProcessScriptAsync(rsl);
            }

            protected override bool ProcessDefinition(bool error, DefnKind dk,
                NPath name, BoundNode bnd, object value)
            {
                Validation.AssertValue(bnd);

                bool ret = base.ProcessDefinition(error, dk, name, bnd, value);
                if (ret && dk != 0)
                    _runner.Publish(dk, name, bnd.Type, value);
                return ret;
            }

            protected override void PreInst(int pos, Instruction inst) => _runner.PreInst(pos, inst);

            protected override Task<bool> HandleAsync(int pos, Instruction.Expr inst)
            {
                Validation.AssertValue(inst);

                // These are pure output, so this ignores them.
                return Task.FromResult(true);
            }

            #region From HarnessBase.Handlers.cs

            protected override bool HandleRunException(SourceContext source, Exception ex)
            {
                Validation.AssertValueOrNull(source);
                Validation.AssertValue(ex);
                return _runner.HandleRunException(source, ex);
            }

            protected override bool HandleExecException(Exception ex, RexlFormula fma)
            {
                Validation.AssertValue(ex);
                Validation.AssertValue(fma);

                return _runner.HandleException(ex, fma);
            }

            #endregion From Harness.Handlers.cs
        }

        private sealed class InnerSink : BlankEvalSink
        {
            private readonly ScriptActionRunner _runner;

            public InnerSink(ScriptActionRunner runner)
                : base()
            {
                Validation.AssertValue(runner);
                _runner = runner;
            }

            protected override void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode? nodeCtx)
            {
                Validation.AssertValue(diag);
                _runner.HandleIssue(diag);
            }
        }
    }
}
