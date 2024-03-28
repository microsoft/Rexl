// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Statement;

using Microsoft.Rexl.Harness;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

// Use non-caching code generator, then wrap at the global level, just for coverage.
using CodeGenerator = EnumerableCodeGenerator;

public abstract class BlockTestsBase<TOpts> : RexlTestsBaseType<TOpts>
{
    private sealed class TestStorage : SimpleHarnessBase.LocalFileStorage
    {
        private readonly bool _createSeekableStreams;

        public TestStorage(bool createSeekableStreams = true)
        {
            _createSeekableStreams = createSeekableStreams;
        }

        protected override bool TryFindLocalFile(string pathPar, string path, out string pathFull)
        {
            // First look in Source, then in XTemp.
            if (base.TryFindLocalFile(pathPar, path, out pathFull))
                return true;
            return base.TryFindLocalFile(RenameToXTemp(pathPar), path, out pathFull);
        }

        protected override bool TryFindLocalDir(string pathPar, string path, out string pathFull)
        {
            // First look in Source, then in XTemp.
            if (base.TryFindLocalDir(pathPar, path, out pathFull))
                return true;
            return base.TryFindLocalDir(RenameToXTemp(pathPar), path, out pathFull);
        }

        protected override void ResolveNewLocalFile(string pathPar, string path, out string pathFull)
        {
            base.ResolveNewLocalFile(RenameToXTemp(pathPar), path, out pathFull);
        }

        /// <summary>
        /// Given a path with a 'Rexl.Code.Test/Scripts' in it, return the new path obtained by replacing
        /// 'Rexl.Code.Test/Scripts' -> 'XTemp'.
        /// </summary>
        private string RenameToXTemp(string path)
        {
            Validation.AssertValueOrNull(path);

            if (path == null)
                return null;

            var seps = new char[] { '/', '\\' };
            int ichLim = path.Length;
            for (; ; )
            {
                int ich = path.LastIndexOfAny(seps, ichLim - 1);
                if (ich < 0)
                    return path;
                if (path.Substring(ich + 1, ichLim - ich - 1) == "Scripts")
                {
                    int ichPrev = path.LastIndexOfAny(seps, ich - 1);
                    if (ichPrev < 0)
                        return path;
                    if (path.Substring(ichPrev + 1, ich - ichPrev - 1) != "Rexl.Code.Test")
                        return path;
                    return path.Substring(0, ichPrev + 1) + "XTemp" + path.Substring(ichLim);
                }
                ichLim = ich;
            }
        }

        protected override Stream CreateLocalFileStream(string pathFull, StreamOptions options = default)
        {
            var mode = (options & StreamOptions.DontOverwrite) != 0 ? FileMode.CreateNew : FileMode.Create;
            if (_createSeekableStreams)
                return new FileStream(pathFull, mode, FileAccess.ReadWrite);
            if ((options & StreamOptions.NeedSeek) == 0)
                return new NonSeekableFileStream(pathFull, mode, FileAccess.Write);
            throw new IOException("Can't create seekable stream");
        }

        private sealed class NonSeekableFileStream : FileStream
        {
            public NonSeekableFileStream(string path, FileMode mode, FileAccess access)
                : base(path, mode, access)
            {
            }

            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin loc)
            {
                throw new NotSupportedException();
            }
        }
    }

    private sealed class TestHarness : SimpleHarnessBase
    {
        private readonly BlockTestsBase<TOpts> _parent;
        private readonly new HarnessConfig _config;
        private readonly TOpts _options;

        // This flag indicates that a block test is in recover mode iff it starts with a /*recover*/ comment.
        private bool _customRecover;

        // Set to true when we are resuming interpreter execution. This signals the test harness that
        // the __page__ instruction should be ignored.
        private bool _resuming;
        // Set to true when we are resuming with fuzzed interpreter state. This signals the test harness
        // that the __page__ instruction should cause an abort (don't actually execute).
        private bool _fuzzing;
        // The number of caught exceptions while fuzzing.
        private int _caughtFuzz;
        // The number of normal resumptions while fuzzing.
        private int _normalFuzz;

        // Whether output is "muted".
        private bool _muted;

        private readonly SinkImpl _sink;

        public override EvalSink Sink => _sink;

        public static TestHarness Create(BlockTestsBase<TOpts> parent, OperationRegistry opers,
            CodeGeneratorBase codeGen, bool showIL, bool createSeekableStreams, TOpts options)
        {
            var config = new HarnessConfig(recover: true, verbose: true, showIL: showIL);
            var storage = new TestStorage(createSeekableStreams);
            return new TestHarness(parent, config, opers, codeGen, storage, options);
        }

        private TestHarness(BlockTestsBase<TOpts> parent, HarnessConfig config, OperationRegistry opers,
                CodeGeneratorBase codeGen, Storage storage, TOpts options)
            : base(config, opers, codeGen, storage)
        {
            _parent = parent;
            _config = config;
            _options = options;

            _muted = false;
            _sink = new SinkImpl(this);
        }

        protected override void ResetCoreInfo(bool init)
        {
            base.ResetCoreInfo(init);
            if (init)
            {
                // Expose the data path as a global.
                SetGlobal(NPath.Root.Append(new DName("_DATA_")), DType.Text, null, _parent.PathData + "/");
            }
        }

        private void SetMuted(bool muted)
        {
            if (muted != _muted)
            {
                Sink.Flush();
                _muted = muted;
            }
        }

        /// <summary>
        /// The <paramref name="customRecover"/> parameter should normally be false, in which case the
        /// script and sub-scripts are run with "recover" set to true. When <paramref name="customRecover"/>
        /// is true, each script is run in recover mode if it starts with the block comment <c>/*recover*/</c>.
        /// </summary>
        public async Task<bool> RunTestScriptAsync(
            SourceContext source, bool customRecover = false, bool fuzzSuspendState = false)
        {
            _customRecover = customRecover;
            _config.SetShouldContinue(GetRecover(source.Text));

            try
            {
                SetMuted(false);
                var (res, stateSuspend) = await base.RunAsync(source, resetBefore: true);
                SetMuted(false);

                while (stateSuspend != null)
                {
                    var stateResume = stateSuspend;
                    try
                    {
                        if (fuzzSuspendState)
                        {
                            var mem = stateResume as MemoryStream;
                            Assert.IsNotNull(mem);
                            var buf = mem.GetBuffer();
                            int count = (int)stateResume.Length;

                            for (int offset = -1; offset < 4; offset += 2)
                            {
                                Sink.WriteLine("*** Fuzzing Suspend State ({0} bytes) with offset {1} ***",
                                    count, offset);

                                // REVIEW: Should we do exhaustive or some kind of sampling?
                                _caughtFuzz = 0;
                                _normalFuzz = 0;
                                for (int i = 0; i < count; i++)
                                {
                                    byte cur = buf[i];
                                    buf[i] = (byte)(cur + offset);
                                    stateResume.Position = 0;
                                    _fuzzing = true;

                                    var (tmp, sus) = await base.ResumeAsync(stateResume);
                                    SetMuted(false);

                                    Assert.IsNull(sus);
                                    _fuzzing = false;
                                    buf[i] = cur;
                                }

                                Sink.WriteLine("*** Fuzzing: Caught {0}, Normal {1}", _caughtFuzz, _normalFuzz);
                            }
                        }

                        _resuming = true;
                        stateResume.Position = 0;

                        (res, stateSuspend) = await base.ResumeAsync(stateResume);
                    }
                    finally
                    {
                        SetMuted(false);

                        stateResume.Dispose();
                        _fuzzing = false;
                        _resuming = false;
                    }
                }
                Validation.Assert(stateSuspend == null);

                return res;
            }
            finally
            {
                SetMuted(false);

                // Call Reset to abort any tasks that haven't completed and to "forget" the runners.
                await ResetAsync();
            }
        }

        public void TestEvaluateExpression(SourceContext source)
        {
            Sink.WriteLine(">>> *** Source:");
            DumpLines(source.Text, "    ");
            var success = TryEvaluateExpression(source, out var value, out var type, out var fma, out var bfma, out var ex);
            if (!type.IsValid)
                Assert.IsFalse(success);
            Assert.IsNotNull(fma);
            if (fma.HasDiagnostics)
                DumpIssues(DiagSource.Parse, fma.Diagnostics);
            if (bfma != null && bfma.HasDiagnostics)
                DumpIssues(DiagSource.Bind, bfma.Diagnostics);
            if (ex != null)
                Sink.TWriteLine("  *** Execution errors:").TWrite("    ").WriteLine(ex.Message);

            if (success)
                Sink.WriteValue(type, value);
            else
            {
                Sink.WriteLine("Failed to evaluate value!");
                Assert.IsNull(value);
            }
        }

        /// <summary>
        /// Write diagnostics.
        /// </summary>
        private void DumpIssues<TDiag>(DiagSource src, Immutable.Array<TDiag> issues)
            where TDiag : BaseDiagnostic
        {
            int num = issues.Length;
            for (int i = 0; i < num; i++)
            {
                Sink.Write("  ");
                Sink.WriteDiag(src, issues[i]);
            }
        }

        protected override bool PushScriptCore(
            SourceContext source, StmtFlow flow, NamespaceSpec nss, bool forImport, bool? recover = null)
        {
            return base.PushScriptCore(source, flow, nss, forImport, recover ?? GetRecover(source.Text));
        }

        /// <summary>
        /// Used to determine the recovery mode for a script. When <see cref="_customRecover"/> is true,
        /// a script is in recover mode iff it starts with the block comment <c>/*recover*/</c>. Of course,
        /// this is a convention for tests, not real code.
        /// </summary>
        private bool GetRecover(string text)
        {
            if (!_customRecover)
                return true;
            return text.StartsWith("/*recover*/");
        }

        protected override ExecCtx CreateExecCtx(CodeGenResult resCodeGen)
        {
            return new ExecCtxImpl(this, SourceCur?.LinkCtx, resCodeGen.IdBndMap);
        }

        private sealed class ExecCtxImpl : IdBndMapExecCtx
        {
            private readonly TestHarness _harness;
            private readonly Link _linkCtx;

            public ExecCtxImpl(TestHarness harness, Link linkCtx, IdBndMap map)
                : base(map)
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

            public override RuntimeModule Optimize(int id, RuntimeModule src, DName measure, bool isMax, DName solver)
            {
                return _harness.Optimize(id, src, measure, isMax, solver);
            }

            public override Stream LoadStream(Link link, int id)
            {
                if (link is null)
                    return null;
                var (full, stream) = _harness._storage.LoadStream(_linkCtx, link);
                return stream;
            }
        }

        protected override Stream GetSuspendState(StmtInterp.SuspendException ex)
        {
            if (_fuzzing && ex.Message == "FuzzAbort")
                return null;
            return base.GetSuspendState(ex);
        }

        private void DumpLines(string text, string prefix = "> ")
        {
            Validation.AssertValue(text);

            var lines = SplitLines(text);
            foreach (var line in lines)
                Sink.WriteLine("{0}{1}", prefix, line);
        }

        /// <summary>
        /// Returns whether the given instruction is a "page" instruction, indicated by a parse tree
        /// consisting solely of the identifier __page__.
        /// </summary>
        private bool IsInterrupt(Instruction.Expr inst, out bool abort)
        {
            abort = false;
            if (inst.Value.ParseTree is not FirstNameNode fnn)
                return false;
            if (fnn.Ident.IsGlobal)
                return false;
            var str = fnn.Ident.Name.Value;
            switch (str)
            {
            case "__page__":
                return true;
            case "__abort__":
                abort = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns whether the given instruction is a "page" instruction, indicated by a parse tree
        /// consisting solely of the identifier __page__.
        /// </summary>
        private bool HandleTrace(Instruction.Expr inst)
        {
            if (inst.Value.ParseTree is not FirstNameNode fnn)
                return false;
            if (fnn.Ident.IsGlobal)
                return false;

            var str = fnn.Ident.Name.Value;
            switch (str)
            {
            case "__unmute__":
                Flush();
                _muted = false;
                return true;
            case "__mute__":
                Flush();
                _muted = true;
                return true;
            }

            return false;
        }

        protected override void PreInst(int pos, Instruction inst)
        {
            Validation.AssertValue(inst);

            if (_muted)
                return;

            Sink.Write("> {0,4}) ", pos);
            if (inst.Kind == InstructionKind.Expr && IsInterrupt(inst.Cast<Instruction.Expr>(), out bool abort))
                Sink.WriteLine(abort ? "[{0}] __abort__" : "[{0}] __page__", inst.Depth);
            else
            {
                inst.DumpInto(Sink);
                Sink.WriteLine();
            }
        }

        protected override Task<bool> HandleAsync(int pos, Instruction.Expr inst)
        {
            Validation.AssertValue(inst);
            Validation.Assert(!_fuzzing);

            if (!IsInterrupt(inst, out bool abort))
            {
                Validation.Assert(!_resuming);
                if (HandleTrace(inst))
                    return Task.FromResult(true);
                return base.HandleAsync(pos, inst);
            }

            Validation.Assert(!abort | !_resuming);
            if (!abort && _resuming)
            {
                _resuming = false;
                return Task.FromResult(true);
            }

            throw Suspend(abort, abort ? "Aborting" : null);
        }

        protected override StmtFlow CreateFlow(RexlStmtList rsl)
        {
            var flow = base.CreateFlow(rsl);

            Sink.WriteLine(">>> *** Source:");
            var rng = rsl.ParseTree.GetFullRange();
            DumpLines(rng.GetFragment(), "    ");
            Sink.WriteLine();

            Sink.WriteLine(">>> *** Instructions:");
            flow.DumpInto(Sink);
            Sink.WriteLine();

            return flow;
        }

        protected override void HandleResume(SourceContext source, Instruction inst, int pos, Stream strm)
        {
            Validation.AssertValue(source);
            Validation.AssertValue(inst);
            Validation.AssertValue(strm);
            Validation.Assert(_resuming | _fuzzing);

            // Should be at the end of the stream.
            Assert.AreEqual(strm.Length, strm.Position);

            if (_fuzzing)
            {
                _normalFuzz++;
                if (inst.Kind != InstructionKind.Expr || !IsInterrupt(inst.Cast<Instruction.Expr>(), out _))
                {
                    Sink.Write("FuzzAbort: ");
                    PreInst(pos, inst);
                }
                throw Suspend(true, "FuzzAbort");
            }

            base.HandleResume(source, inst, pos, strm);
        }

        protected override bool HandleRunException(SourceContext source, Exception ex)
        {
            Validation.AssertValueOrNull(source);
            Validation.AssertValue(ex);

            if (_fuzzing)
            {
                Validation.Assert(ex.Message != "FuzzAbort");
                _caughtFuzz++;
            }
            return base.HandleRunException(source, ex);
        }

        protected override bool HandleExecException(Exception ex, RexlFormula fma)
        {
            Validation.AssertValue(ex);
            Validation.AssertValue(fma);

            PostDiagnostic(DiagSource.ExecException, MessageDiag.Exception(ex), fma.ParseTree);
            return true;
        }

        protected override void HandleBoundFormula(BoundFormula bfma)
        {
            base.HandleBoundFormula(bfma);
            BoundTreeValidator.Run(bfma.BoundTree, bfma.HasErrors);
        }

        protected override void HandleGlobal(bool error, NPath name, DType type, BoundNode bnd, ref object value)
        {
            if (!error && type.IsSequence && value != null)
            {
                TypeManager.TryEnsureSysType(type.ItemTypeOrThis, out Type stItem).Verify();
                Validation.Assert(typeof(IEnumerable<>).MakeGenericType(stItem).IsAssignableFrom(value.GetType()));
                if (value is ICachingEnumerable)
                { }
                else if (value is ICollection)
                { }
                else
                {
                    // Wrap as caching enumerable.
                    Type stCache = typeof(CachingEnumerable<>).MakeGenericType(stItem);
                    value = Activator.CreateInstance(stCache, value);
                }
            }

            base.HandleGlobal(error, name, type, bnd, ref value);
        }

        protected override void HandleValue(DType type, BoundNode bnd, object value, ExecCtx ctx)
        {
            if (!_parent.TryHandleValue(Sink, _codeGen, _options, type, bnd, value, ctx))
                base.HandleValue(type, bnd, value, ctx);
        }

        protected override DateTimeOffset Now()
        {
            return new DateTimeOffset(2022, 9, 21, 22, 25, 34, 877, new TimeSpan(3, 15, 0));
        }

        protected override bool TryOptimizeMip(bool isMax, RuntimeModule modSrc, int imsr, DName solver,
            out double score, out List<(DName name, object value)> symValues)
        {
            Validation.AssertValue(modSrc);
            Validation.AssertIndex(imsr, modSrc.Bnd.Symbols.Length);
            Validation.Assert(modSrc.Bnd.Symbols[imsr].IsMeasureSym);

            return _parent.TryOptimizeMip(Sink, _codeGen, isMax, modSrc, imsr, solver, out score, out symValues);
        }

        private sealed class SinkImpl : FlushEvalSink
        {
            private readonly TestHarness _parent;
            private readonly ValueWriterConfig _config;
            private readonly StdValueWriter _valueWriter;

            protected override DiagFmtOptions DiagOptions => DiagFmtOptions.DefaultTest;

            protected override bool ShowDiagBanners => true;

            public SinkImpl(TestHarness parent)
                : base()
            {
                Validation.AssertValue(parent);
                _parent = parent;
                _config = parent._parent.CreateValueWriterConfig(parent._options);
                _valueWriter = new StdValueWriter(_config, this, parent.TypeManager);
            }

            protected override void WriteCore(string value)
            {
                // REVIEW: This is a silly hack to get a stable baseline for the
                // test to invoke Gurobi when no license is available. Find a better way.
                if (value.StartsWith("No Gurobi license found"))
                {
                    Console.WriteLine(value);
                    value = "No Gurobi license found!";
                }
                base.WriteCore(value);
            }

            protected override void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode nodeCtx)
            {
                if (diag.IsPureException && _parent._fuzzing)
                    WriteLine(diag.RawException.Message);
                else
                    WriteDiag(src, diag);
            }

            protected override void DumpPath(SourceContext source)
            {
                Validation.AssertValue(source);
                // Only dump the tail, not the full directory, since that contains machine specific information.
                var path = source.PathTail;
                if (!string.IsNullOrEmpty(path))
                {
                    Write("[");
                    Write(source.PathTail);
                    Write("] ");
                }
            }

            protected override void Dump(StringBuilder sb)
            {
                Validation.AssertValue(sb);
                if (!_parent._muted)
                    _parent._parent.Sink.Write(sb.ToString());
            }

            protected override void PostValueCore(DType type, object value)
            {
                Validation.Assert(type.IsValid);
                WriteValue(type, value);
            }

            /// <summary>
            /// Writes a string representation of a value.
            /// </summary>
            protected override void WriteValueCore(DType type, object? value, int max)
            {
                Validation.Assert(type.IsValid);
                _config.Max = max;
                _valueWriter.WriteValue(type, value);
            }
        }
    }

    protected BlockTestsBase()
    : base()
    {
    }

    protected virtual ValueWriterConfig CreateValueWriterConfig(TOpts options)
    {
        return new ValueWriterConfig();
    }

    protected virtual bool TryHandleValue(EvalSink sink, CodeGeneratorBase codeGen, TOpts options,
        DType type, BoundNode bnd, object value, ExecCtx ctx)
    {
        return false;
    }

    protected virtual bool TryOptimizeMip(EvalSink sink, CodeGeneratorBase codeGen,
        bool isMax, RuntimeModule modSrc, int imsr, DName solver,
        out double score, out List<(DName name, object value)> symValues)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(codeGen);
        Validation.AssertValue(modSrc);
        Validation.AssertIndex(imsr, modSrc.Bnd.Symbols.Length);
        Validation.Assert(modSrc.Bnd.Symbols[imsr].IsMeasureSym);

        var strSolver = solver.IsValid ? solver.Value : "<default>";
        sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverUnkown_Name, strSolver));
        score = double.NaN;
        symValues = null;
        return false;
    }

    protected Task ProcessFileWithIL(string pathHead, string pathTail, string text, TOpts options)
    {
        return ProcessFileCoreAsync(pathHead, pathTail, text, withIL: true);
    }

    protected Task ProcessFileNoIL(string pathHead, string pathTail, string text, TOpts options)
    {
        return ProcessFileCoreAsync(pathHead, pathTail, text, withIL: false);
    }

    protected Task ProcessFileNoILNonSeekableStreams(string pathHead, string pathTail, string text, TOpts options)
    {
        return ProcessFileCoreAsync(pathHead, pathTail, text, withIL: false,
            createSeekableStreams: false);
    }

    protected Task ProcessFileSegmented(string pathHead, string pathTail, string text, TOpts options)
    {
        return ProcessFileCoreAsync(pathHead, pathTail, text, withIL: false, segmented: true);
    }

    protected Task ProcessFileSegmentedCustomRecover(string pathHead, string pathTail, string text, TOpts options)
    {
        // This one has the policy that if a script starts with a block comment recover is set to
        // true, otherwise, recover is set to false.
        return ProcessFileCoreAsync(pathHead, pathTail, text,
            withIL: false, segmented: true, customRecover: true);
    }

    protected Task ProcessFileSegmentedFuzzing(string pathHead, string pathTail, string text, TOpts options)
    {
        // This one has the policy that if a script starts with a block comment recover is set to
        // true, otherwise, recover is set to false.
        return ProcessFileCoreAsync(pathHead, pathTail, text,
            withIL: false, segmented: true, customRecover: true, fuzzSuspendState: true);
    }

    protected static Link LinkFromHeadTail(string pathHead, string pathTail, TOpts options)
    {
        if (!string.IsNullOrEmpty(pathHead))
        {
            if (!string.IsNullOrEmpty(pathTail))
                return Link.CreateGeneric(Path.Combine(pathHead, pathTail));
            return Link.CreateGeneric(pathHead);
        }
        else if (!string.IsNullOrEmpty(pathTail))
            return Link.CreateGeneric(pathTail);
        return null;
    }

    protected async Task ProcessFileCoreAsync(string pathHead, string pathTail, string text,
        bool withIL, bool createSeekableStreams = true, bool segmented = false,
        bool customRecover = false, bool fuzzSuspendState = false, TOpts options = default)
    {
        // To get deterministic test behavior we need to override the pathHead when fuzzing
        // suspend state.
        if (fuzzSuspendState)
            pathHead = "X";

        Link linkFull = LinkFromHeadTail(pathHead, pathTail, options);

        // REVIEW: Perhaps use caching when there are nested sequences and otherwise just wrap at the global
        // level. We need to change HarnessBase to do that.
        var codeGen = new CodeGenerator(new TestEnumTypeManager(), TestGenerators.Instance);

        var harness = TestHarness.Create(this, TestOperations.Instance, codeGen,
            showIL: withIL, createSeekableStreams: createSeekableStreams, options);
        if (segmented)
        {
            var segs = SplitHashBlocks(text);
            foreach (var seg in segs)
            {
                await harness.RunTestScriptAsync(SourceContext.Create(linkFull, pathTail, seg), customRecover, fuzzSuspendState);
                Sink.WriteLine("###");
            }
        }
        else
            await harness.RunTestScriptAsync(SourceContext.Create(linkFull, pathTail, text), customRecover, fuzzSuspendState);
        Sink.WriteLine();
    }

    protected async Task ProcessFileAndEvaluateAsync(string pathHead, string pathTail, string text, TOpts options)
    {
        Link linkFull = LinkFromHeadTail(pathHead, pathTail, options);

        var codeGen = new CodeGenerator(new TestEnumTypeManager(), TestGenerators.Instance);

        var harness = TestHarness.Create(this, TestOperations.Instance, codeGen, false, false, options);
        var blocks = SplitHashBlocks(text);
        foreach (var block in blocks)
        {
            var source = SourceContext.Create(linkFull, pathTail, block);
            if (block.StartsWith("// Evaluate"))
                harness.TestEvaluateExpression(source);
            else
                await harness.RunAsync(source, resetBefore: false);
            harness.Flush();
            Sink.WriteLine("###");
        }
        Sink.WriteLine();
    }
}
