// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Harness;

/// <summary>
/// This sub-class contains in memory stores for global values, and modules.
/// </summary>
public abstract partial class SimpleHarnessBase : HarnessBase
{
    protected readonly Storage _storage;

    protected SimpleHarnessBase(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen,
            Storage storage = null)
        : base(config, opers, codeGen)
    {
        Validation.BugCheckValueOrNull(storage);

        _storage = storage ?? new DefaultLocalFileStorage();
        _globals = new Dictionary<NPath, (DType type, BoundNode bnd, object res)>();
    }

    public void CleanupSync()
    {
        ResetSync();
    }

    public Task CleanupAsync()
    {
        return ResetAsync();
    }

    protected override void ResetCoreInfo(bool init)
    {
        base.ResetCoreInfo(init);
        _globals.Clear();
    }

    protected override ExecCtx CreateExecCtx(CodeGenResult resCodeGen)
    {
        return new SimpleHarnessExecCtx<SimpleHarnessBase>(this, SourceCur?.LinkCtx);
    }

    /// <summary>
    /// Get "the value" and its type, as well as number of "results" from the given source
    /// <paramref name="bnd"/> and the result of execution. For a pure expression, this is
    /// just the normal value/type with count of 1. For an action, the <paramref name="value"/>
    /// on input is an <see cref="ActionRunner"/>. On output the count is set to the number
    /// of results. If that number is positive, <paramref name="type"/> and <paramref name="value"/>
    /// are set to the first result. Otherwise, they are set to null.
    /// </summary>
    protected void ResolveValue(BoundNode bnd, out int count, out DType type, ref object value)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall == (value is ActionRunner));

        if (value is ActionRunner runner)
        {
            Validation.Assert(runner.WasSuccessful);
            count = runner.ResultCount;
            var info = runner.PrimaryResult;
            if (info == null)
            {
                type = DType.Null;
                value = null;
            }
            else
            {
                type = info.Type;
                value = runner.GetResultValue(info);
            }
        }
        else
        {
            count = 1;
            type = bnd.Type;
        }
    }

    protected override bool ProcessDefinition(bool error, DefnKind dk, NPath name, BoundNode bnd, object value)
    {
        Validation.AssertValue(bnd);

        DType type;
        if (!error)
            ResolveValue(bnd, out _, out type, ref value);
        else
            type = DType.Null;

        HandleGlobal(error, name, type, bnd, ref value);
        return true;
    }

    protected override bool ProcessTaskDefinition(bool error, NPath name, BoundNode bnd, ActionRunner runner)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall);
        Validation.Assert(error || runner != null);
        Validation.Assert(runner != null || error);

        HandleTask(name, runner);
        return true;
    }

    protected override bool ProcessTaskBlockDefinition(NPath name, ActionRunner runner)
    {
        Validation.Assert(!name.IsRoot);
        Validation.AssertValue(runner);

        HandleTask(name, runner);
        return true;
    }

    protected override bool ProcessExpr(BoundNode bnd, object value, ExecCtx ctx)
    {
        Validation.AssertValue(bnd);
        Validation.AssertValueOrNull(ctx);

        ResolveValue(bnd, out int count, out var type, ref value);
        if (count > 0)
            HandleValue(type, bnd, value, ctx);
        return true;
    }

    public override Task<(Link full, Stream stream)> LoadStreamForImportAsync(Link linkCtx, Link link)
    {
        return _storage.LoadStreamAsync(linkCtx, link);
    }

    protected class SimpleHarnessExecCtx<THarness> : HarnessExecCtx<THarness>
        where THarness : SimpleHarnessBase
    {
        public SimpleHarnessExecCtx(THarness harness, Link linkCtx)
            : base(harness, linkCtx)
        {
        }

        public override Stream LoadStream(Link link, int id)
        {
            if (link is null)
                return null;
            var (linkFull, stream) = _harness._storage.LoadStream(_linkCtx, link);
            return stream;
        }
    }
}

/// <summary>
/// This adds a stack of <see cref="EvalSink"/> to <see cref="SimpleHarnessBase"/>.
/// </summary>
public abstract partial class SimpleHarnessWithSinkStack : SimpleHarnessBase
{
    private EvalSink _sinkCur;
    private List<EvalSink> _sinkStack;

    public sealed override EvalSink Sink => _sinkCur ?? new EmptySink();

    protected SimpleHarnessWithSinkStack(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen,
            Storage storage = null)
        : base(config, opers, codeGen, storage)
    {
    }

    public async Task<(bool success, Stream suspendState)> RunAsync(EvalSink sink, SourceContext source, bool resetBefore)
    {
        Validation.CheckValue(sink, nameof(sink));
        Validation.BugCheckValue(source, nameof(source));

        PushSink(sink);
        try
        {
            return await RunAsync(source, resetBefore).ConfigureAwait(false);
        }
        finally
        {
            var prev = PopSink();
            Validation.Assert(prev == sink);
        }
    }

    public void PushSink(EvalSink output)
    {
        Validation.BugCheckValue(output, nameof(output));

        Util.Add(ref _sinkStack, _sinkCur);
        _sinkCur = output;
    }

    public EvalSink PopSink()
    {
        Validation.BugCheck(_sinkStack.TryPop(out var next));

        var prev = _sinkCur;
        _sinkCur = next;
        return prev;
    }

    private sealed class EmptySink : BlankEvalSink
    {
        public EmptySink()
            : base()
        {
        }

        protected override void PostWrite()
        {
            Validation.Assert(false);
        }
    }
}
