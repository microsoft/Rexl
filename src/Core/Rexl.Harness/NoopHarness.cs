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

namespace Microsoft.Rexl.Harness;

/// <summary>
/// This harness base class does nothing but parameter validation for the required abstract methods.
/// It exists primarily as a template for creating a new harness based on <see cref="HarnessBase"/>.
/// </summary>
public abstract class NoopHarness : HarnessBase
{
    protected NoopHarness(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen)
        : base(config, opers, codeGen)
    {
    }

    #region From Harness.cs

    public override Task<(Link full, Stream stream)> LoadStreamForImportAsync(Link linkCtx, Link link)
    {
        throw new NotSupportedException();
    }

    #endregion From Harness.cs

    #region From Harness.Interp.cs

    protected override Task<bool> ProcessTaskCmdAsync(TaskExecKind tek, IdentPath path, NPath name)
    {
        Validation.AssertValue(path);
        Validation.Assert(!name.IsRoot);
        return Task.FromResult(false);
    }

    protected override bool ProcessDefinition(bool error, DefnKind dk, NPath name, BoundNode bnd, object value)
    {
        Validation.AssertValue(bnd);
        return false;
    }

    protected override bool ProcessTaskDefinition(bool error, NPath name, BoundNode bnd, ActionRunner runner)
    {
        Validation.AssertValue(bnd);
        Validation.Assert(bnd.IsProcCall);
        Validation.Assert(error || runner != null);
        return false;
    }

    protected override bool ProcessTaskBlockDefinition(NPath name, ActionRunner runner)
    {
        Validation.Assert(!name.IsRoot);
        Validation.AssertValue(runner);
        return false;
    }

    protected override bool ProcessExpr(BoundNode bnd, object value, ExecCtx ctx)
    {
        Validation.AssertValue(bnd);
        Validation.AssertValueOrNull(ctx);
        return false;
    }

    #endregion From Harness.Interp.cs

    #region From Harness.Variable.cs

    public override bool HasGlobal(NPath name)
    {
        throw new NotImplementedException();
    }

    public override Immutable.Array<NPath> GetGlobalNames()
    {
        throw new NotImplementedException();
    }

    public override Immutable.Array<(NPath name, DType type)> GetGlobalInfos()
    {
        throw new NotImplementedException();
    }

    public override bool TryGetGlobalType(NPath name, out DType type)
    {
        throw new NotImplementedException();
    }

    protected override bool TryGetTaskItemType(NPath task, DName item, out DType type, out bool isStream)
    {
        throw new NotImplementedException();
    }

    protected override bool TryGetTaskItemTypeFuzzy(NPath task, DName item, out DName itemGuess, out DType type, out bool isStream)
    {
        throw new NotImplementedException();
    }

    #endregion From Harness.Variable.cs

    #region From Harness.Task.cs

    protected override Task<(Link full, Stream stream)> LoadStreamForTaskAsync(SourceContext sctx, Link link)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValueOrNull(link);
        throw new NotSupportedException();
    }

    protected override Task<(Link full, Stream stream)> CreateStreamForTaskAsync(SourceContext sctx, Link link,
        StreamOptions options = default)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValueOrNull(link);
        throw new NotImplementedException();
    }

    protected override IEnumerable<Link> GetFilesForTask(SourceContext sctx, Link linkDir, out Link full)
    {
        Validation.AssertValue(sctx);
        Validation.AssertValueOrNull(linkDir);
        throw new NotImplementedException();
    }

    protected override ActionRunner CreateUserProcRunner(UserProc proc, DType typeWith, RecordBase with)
    {
        Validation.AssertValue(proc);
        Validation.Assert(typeWith.IsRecordReq);
        AssertValidWith(typeWith, with);

        throw new NotImplementedException();
    }

    #endregion  From Harness.Task.cs

    #region From Harness.Evaluate.cs

    protected override ExecCtx CreateExecCtx(CodeGenResult resCodeGen)
    {
        throw new NotSupportedException();
    }

    protected override bool TryGetGlobalValue(NPath name, DType type, out object value)
    {
        value = null;
        return false;
    }

    #endregion From Harness.Evaluate.cs

    #region From Harness.Script.cs

    protected override ActionRunner CreateBlockActionRunner(DType typeWith, RecordBase with,
        RexlStmtList outer, BlockStmtNode prime, BlockStmtNode play)
    {
        Validation.Assert(typeWith.IsRecordReq);
        Validation.AssertValue(outer);
        Validation.Assert(prime == null || outer.InTree(prime));
        Validation.AssertValue(play);
        Validation.Assert(outer.InTree(play));
        AssertValidWith(typeWith, with);

        throw new NotSupportedException();
    }

    #endregion From Harness.Script.cs

    /// <summary>
    /// This exists to ensure that <see cref="NoopHarness"/> overrides all abstracts except
    /// the <see cref="Sink"/> property.
    /// </summary>
    private sealed class SealedImpl : NoopHarness
    {
        public override EvalSink Sink => throw new NotImplementedException();

        private SealedImpl(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen)
            : base(config, opers, codeGen)
        {
        }
    }
}
