// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Harness;

using CodeGenerator = CachingEnumerableCodeGenerator;
using StopWatch = System.Diagnostics.Stopwatch;
using UserOperTuple = Immutable.Array<UserOper>;

/// <summary>
/// A harness/session contains sets of named globals, modules, tasks and udfs (user defined
/// functions). Its contents can be modified and used for evaluation by calling the
/// <see cref="RunAsync"/> method to execute a script consisting of extended/statement rexl.
/// The names are instances of <see cref="NPath"/>. Any prefix path of a name (not including
/// UDF paths) is considered an active "namespace". It has the concept of "current namespace".
/// </summary>
public abstract partial class HarnessBase
{
    protected readonly IHarnessConfig _config;

    /// <summary>
    /// Whether output should be verbose. Defaults to true.
    /// </summary>
    protected bool IsVerbose => _config.IsVerbose;

    /// <summary>
    /// The output sink.
    /// </summary>
    public abstract EvalSink Sink { get; }

    public static CodeGeneratorBase CreateDefaultCodeGenerator(EnumerableTypeManager tm, GeneratorRegistry gens)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckValue(gens, nameof(gens));
        return new CodeGenerator(tm, gens);
    }

    protected HarnessBase(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen)
    {
        Validation.BugCheckValue(config, nameof(config));
        Validation.BugCheckValue(opers, nameof(opers));
        Validation.BugCheckValue(codeGen, nameof(codeGen));

        _config = config;
        _opers = opers;
        _namespaces = new Dictionary<NPath, HashSet<DName>>();
        _nameToUserOpers = new Dictionary<NPath, UserOperTuple>();
        _host = new BindHostImpl(this);
        _codeGen = codeGen;

        _interp = new Interp(this);
        _swExec = new StopWatch();
        Validation.Assert(!_swExec.IsRunning);

        _tasks = new List<ActionRunner>();
        _tasksNeedPrime = new HashSet<ActionRunner>();
        _taskStates = new Dictionary<ActionRunner, TaskState>();
        _taskDeps = new List<(ActionRunner producer, ActionRunner consumer)>();
        _nameToTask = new Dictionary<NPath, ActionRunner>();
    }

    /// <summary>
    /// Flush any communication channels.
    /// </summary>
    public virtual void Flush() => Sink.Flush();

    /// <summary>
    /// Reset everything, throwing out all items, shutting down and disposing all tasks, etc.
    /// </summary>
    protected virtual void ResetSync(bool init = false)
    {
        Validation.BugCheck(!_interp.IsActive, "Can't reset when the interpreter is active");

        try
        {
            ResetRunnerInfo(dispose: true);
        }
        finally
        {
            ResetCoreInfo(init);
        }
    }

    /// <summary>
    /// Reset everything, throwing out all items, shutting down and disposing all tasks, etc.
    /// </summary>
    protected virtual async Task ResetAsync(bool init = false)
    {
        Validation.BugCheck(!_interp.IsActive, "Can't reset when the interpreter is active");

        try
        {
            await DisposeRunnersAsync().ConfigureAwait(false);
        }
        finally
        {
            ResetRunnerInfo(dispose: false);
            ResetCoreInfo(init);
        }
    }

    /// <summary>
    /// Reset all the core information: interpreter state, namespaces, etc. Optionally re-inits the
    /// namespaces to reflect functions and procedures.
    /// </summary>
    protected virtual void ResetCoreInfo(bool init)
    {
        Validation.Assert(!_interp.IsActive);

        _interp.Reset();
        _namespaces.Clear();
        _nameToUserOpers.Clear();
        _fuzzyUserOperPathToPath = null;

        if (init)
            InitNamespaces();
    }

    /// <summary>
    /// Reset all the runner/task information. Optionally dispose the runners.
    /// </summary>
    protected void ResetRunnerInfo(bool dispose)
    {
        try
        {
            if (dispose)
            {
                // Iterate in creation order so inputs are aborted before their consumers.
                // REVIEW: Would the opposite order be better?
                foreach (var runner in _tasks)
                {
                    // REVIEW: If one throws we still want to dispose of the others. Is there
                    // a simple way to do that short of doing our own try-catch and bundle into an
                    // aggregate exception?
                    Validation.AssertValue(runner);
                    runner.Dispose();
                }
            }
        }
        finally
        {
            _tasks.Clear();
            _tasksNeedPrime.Clear();
            _taskStates.Clear();
            _taskDeps.Clear();
            _nameToTask.Clear();
            _taskStateChanges = null;
        }
    }

    /// <summary>
    /// Call DisposeAsync on the runners/tasks.
    /// </summary>
    protected Task DisposeRunnersAsync()
    {
        // Iterate in creation order so inputs are aborted before their consumers.
        // REVIEW: Would the opposite order be better?
        List<Task> tasks = null;
        foreach (var runner in _tasks)
        {
            Validation.AssertValue(runner);
            var t = runner.DisposeAsync();

            // Note: generally tasks should only complete successfully, but in the rare (and likely
            // "broken") case when they might complete with an exception, it's best for that exception
            // to percolate to our caller, so we put it in the list.
            if (!t.IsCompletedSuccessfully)
                Util.Add(ref tasks, t.AsTask());
        }

        if (tasks != null)
            return Task.WhenAll(tasks);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Open a data stream from a <see cref="Link"/>. This is primarily for import. If import doesn't apply
    /// in a particular environment, this should throw a <see cref="NotSupportedException"/>.
    /// The optional <paramref name="linkCtx"/> value is for resolving relative paths.
    /// The returned <see cref="Link"/> value is the resolved "full path" link.
    /// Throws an exception (typically an <see cref="IOException"/>) on failure.
    /// </summary>
    public abstract Task<(Link full, Stream stream)> LoadStreamForImportAsync(Link linkCtx, Link link);

    public void PostDiagnostic(DiagSource src, BaseDiagnostic diag, RexlNode nodeCtx = null)
    {
        if (diag is not null)
            Sink.PostDiagnostic(src, diag, nodeCtx);
    }

    protected class HarnessExecCtx<THarness> : ExecCtx
        where THarness : HarnessBase
    {
        protected readonly THarness _harness;
        protected readonly Link _linkCtx;

        public HarnessExecCtx(THarness harness, Link linkCtx)
            : base()
        {
            Validation.AssertValue(harness);
            Validation.AssertValueOrNull(linkCtx);
            _harness = harness;
            _linkCtx = linkCtx;
        }

        public override void Log(int id, string msg)
        {
        }

        public override void Log(int id, string fmt, params object[] args)
        {
        }

        public override bool TryGetSink(out EvalSink sink)
        {
            sink = _harness.Sink;
            return true;
        }

        public override bool TryGetCodeGen(out CodeGeneratorBase codeGen)
        {
            codeGen = _harness._codeGen;
            return true;
        }
    }
}
