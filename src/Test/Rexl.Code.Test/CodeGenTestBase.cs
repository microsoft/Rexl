// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

/// <summary>
/// Baseline test options.
/// </summary>
[Flags]
public enum TestCodeOptions
{
    None = 0x0000,

    WithIL = 0x0001,
    WithBytes = 0x0002,
    TupNewLine = 0x0004,
    Streaming = 0x0008,
    ShowBndKinds = 0x0010,
    AllowVolatile = 0x0020,
    AllowProcedure = 0x0040,

    /// <summary>
    /// Split input by "###" delimited blocks, rather than by lines.
    /// </summary>
    SplitBlocks = 0x0080,

    /// <summary>
    /// Whether modules should be prohibited (by the binder).
    /// </summary>
    ProhibitModule = 0x0100,

    AllowGeneral = 0x0200,
}

/// <summary>
/// Base class for codegen-level baseline tests.
/// </summary>
public abstract class CodeGenTestBase : RexlLineTestsBase<SbSysTypeSink, TestCodeOptions>
{
    private sealed class SinkImpl : SbSysTypeSink
    {
        public SinkImpl()
            : base()
        {
        }

        protected override object? MapArg(object? arg)
        {
            switch (arg)
            {
            case BoundNode bnd:
                return BndNodePrinter.Run(bnd, BndNodePrinter.Verbosity.Default);
            default:
                return ArgMapping.MapArg(arg);
            }
        }
    }

    private readonly SinkImpl _sink;

    protected sealed override SbSysTypeSink Sink => _sink;

    protected sealed override bool AnyOut => _sink.Builder.Length > 0;

    protected sealed override string GetTextAndReset()
    {
        var res = _sink.Builder.ToString();
        _sink.Builder.Clear();
        return res;
    }

    // The globals loaded from the global scripts.
    private Dictionary<NPath, (BoundNode bnd, object res)> _globals;

    // The type manager.
    protected EnumerableTypeManager TypeManager { get; }

    protected TestValueWriter ValueWriter { get; }

    /// <summary>
    /// The code gen for most tests.
    /// </summary>
    protected EnumerableCodeGeneratorBase CodeGenDirect { get; }

    /// <summary>
    /// The code gen for definitions.
    /// </summary>
    protected EnumerableCodeGeneratorBase CodeGenCaching { get; }

    /// <summary>
    /// The value writer, for displaying runtime-generated values.
    /// </summary>
    protected ValueWriterConfig Config { get; }

    /// <summary>
    /// The code gen host.
    /// </summary>
    protected CodeGenHost Host { get; set; }

    protected CodeGenTestBase(GeneratorRegistry gens)
    {
        _sink = new SinkImpl();

        // Setup TypeManager and code generators.
        TypeManager = new TestEnumTypeManager();
        Config = new ValueWriterConfig(lazyOrder: true, showStrides: true, showDoms: true);
        ValueWriter = new TestValueWriter(Config, _sink, TypeManager);
        CodeGenDirect = new EnumerableCodeGenerator(TypeManager, gens);
        CodeGenCaching = new CachingEnumerableCodeGenerator(TypeManager, gens);
    }

    protected override bool UseBlock(TestCodeOptions testOpts)
    {
        return (testOpts & TestCodeOptions.SplitBlocks) != 0;
    }

    protected override BindOptions TestOptsToBindOpts(TestCodeOptions testOpts)
    {
        BindOptions options = default;

        if ((testOpts & TestCodeOptions.AllowVolatile) != 0)
            options |= BindOptions.AllowVolatile;
        if ((testOpts & TestCodeOptions.AllowProcedure) != 0)
            options |= BindOptions.AllowProc;
        if ((testOpts & TestCodeOptions.ProhibitModule) != 0)
            options |= BindOptions.ProhibitModule;
        if ((testOpts & TestCodeOptions.AllowGeneral) != 0)
            options |= BindOptions.AllowGeneral;

        return options;
    }

    protected override void InitFile()
    {
        base.InitFile();

        // Setup global dictionary.
        _globals = new Dictionary<NPath, (BoundNode bnd, object res)>();
    }

    protected override void HandleGlobal(BoundFormula bfma, NPath full, string disp)
    {
        Assert.IsNotNull(bfma);

        if (bfma.HasErrors)
            return;

        var bnd = bfma.BoundTree;
        try
        {
            var resCodeGen = CodeGenCaching.Run(bnd, Host);
            var (val, _) = AssignArgsAndInvoke(resCodeGen);
            EnsureNs(full.Parent);
            _globals[full] = (bnd, val);
        }
        catch (Exception ex)
        {
            Sink.WriteLine("**** Definition exception! {0}", ex.Message);
            return;
        }

        Sink.WriteLine("**** New definitions: {0}, type: {1}", disp, bnd.Type);
    }

    protected override DType GetThisType()
    {
        if (_globals.TryGetValue(NPath.Root, out var pair))
        {
            Validation.Assert(pair.bnd.Type.IsValid);
            return pair.bnd.Type;
        }

        return default;
    }

    protected override DType GetGlobalType(NPath full)
    {
        Assert.IsTrue(!full.IsRoot);

        if (_globals.TryGetValue(full, out var pair))
        {
            Validation.Assert(pair.bnd.Type.IsValid);
            return pair.bnd.Type;
        }

        return base.GetGlobalType(full);
    }

    protected override void ProcessScript(string script, TestCodeOptions testOpts)
    {
        // An initial '#' means don't do code gen, just binding.
        bool gen = true;
        if (script.StartsWith('#'))
        {
            script = script[1..].Trim();
            gen = false;
        }

        Sink.WriteLine("> {0}", script);

        // Parsing and binding.
        var fma = RexlFormula.Create(SourceContext.Create(script));
        ValidateScript(fma);

        bool streaming = (testOpts & TestCodeOptions.Streaming) != 0;
        var host = new BindHostImpl(this, streaming);
        var options = BindOptions.AllowVolatile | TestOptsToBindOpts(testOpts);
        if (streaming)
            options |= BindOptions.AllowProc;
        var bfma = BoundFormula.Create(fma, host, options);
        ValidateBfma(bfma, host);

        var res = bfma.BoundTree;

        Sink.WriteLine("{0} : {1}", fma.ParseTree, res.Type);
        Sink.WriteLine("BndKind:{0}, Type:{1}, Bnd:({2})", res.Kind, res.Type, res);
        if ((testOpts & TestCodeOptions.ShowBndKinds) != 0)
            Sink.WriteLine("AllKinds: {0}", res.AllKinds);

        if (fma.HasDiagnostics)
        {
            Sink.WriteLine("=== Parse diagnostics:");
            AppendDiagnostics(fma.Diagnostics);
        }

        if (bfma.HasDiagnostics)
        {
            // If there are also parse diagnostics, print the banner.
            if (fma.HasDiagnostics)
                Sink.WriteLine("=== Bind diagnostics:");
            AppendDiagnostics(bfma.Diagnostics);
        }

        if (!gen)
            return;

        bool badStreamUse = false;
        if (!bfma.IsGood)
        {
            if (!streaming)
                return;
            if (bfma.Errors.Length > 1)
                return;
            if (!(bfma.Errors[0] is MessageDiagnosticBase diag) || diag.Message.Tag != ErrorStrings.ErrBadStreamUse.Tag)
                return;

            // The only error was a bad stream use, so go ahead and do code gen and see whether an illegal use
            // exception is thrown.
            badStreamUse = true;
        }

        CodeGenResult resCodeGen;
        try
        {
            // Note: we are reusing the same type manager for each line. So inserting/re-ordering scripts may
            // affect the output of others. Across different script files, the type manager is re-initialized.
            // REVIEW: Which code generator should we use for IL tests?
            if ((testOpts & TestCodeOptions.WithIL) == 0)
                resCodeGen = CodeGenDirect.Run(res, Host);
            else
            {
                // If there is a global with special name of type i8, it is used to limit display
                // to a single func index.
                if (_globals.TryGetValue(NPath.Root.Append(new DName("__IL_Func_Ind")), out var pair) &&
                    pair.bnd.Type == DType.I8Req && pair.res is long ind)
                {
                    resCodeGen = CodeGenCaching.Run(res, Host,
                        s =>
                        {
                            if (ind == 0)
                                Sink.WriteLine(s);
                            if (s.Length == 0)
                                ind--;
                        },
                        ILLogKind.Size);
                }
                else
                {
                    resCodeGen = CodeGenCaching.Run(res, Host, s => Sink.WriteLine(s), ILLogKind.Size);
                }
            }

            // Do code gen with some node sharing (construct some combinations) to ensure that node sharing works.
            // REVIEW: Should we also run these?
            if (!res.IsProcCall)
            {
                // Make (res, [res, res], res).
                var dbl = BndSequenceNode.Create(res.Type.ToSequence(), Immutable.Array<BoundNode>.Create(res, res));
                var combo = BndTupleNode.Create(Immutable.Array<BoundNode>.Create(res, dbl, res));
                var resCodeGenCombo = CodeGenDirect.Run(combo, Host);
                Assert.AreEqual(resCodeGen.Globals.Length, resCodeGenCombo.Globals.Length);
            }

            if (res is BndCallNode call)
            {
                // Make a combo of any sequence or I8 args.
                var bldr = call.Args.ToBuilder();
                for (int i = 0; i < bldr.Count; i++)
                {
                    var arg = bldr[i];
                    if (arg.Type.IsSequence)
                    {
                        // Concat it with itself.
                        bldr[i] = BndVariadicOpNode.Create(arg.Type, BinaryOp.SeqConcat,
                            Immutable.Array<BoundNode>.Create(arg, arg), default);
                    }
                    else if (arg.Type == DType.I8Req)
                    {
                        // Add it with itself.
                        bldr[i] = BndVariadicOpNode.Create(arg.Type, BinaryOp.Add,
                            Immutable.Array<BoundNode>.Create(arg, arg), default);
                    }
                }
                var call2 = call.SetArgs(bldr.ToImmutable());
                var resCodeGen2 = CodeGenDirect.Run(call2, Host);
                Assert.AreEqual(resCodeGen.Globals.Length, resCodeGen2.Globals.Length);

                if (streaming && call2.IsProcCall)
                {
                    // Run stream analysis.
                    bool badStreamUse2 = false;
                    foreach (var glob in resCodeGen2.Globals)
                    {
                        // When the streaming flag is set, any globals of sequence type whose leaf name
                        // is a single character is considered streaming.
                        if (!glob.Type.IsSequence || glob.Name.IsRoot || glob.Name.Leaf.Value.Length != 1)
                            continue;
                        var bad = StreamAnalysis.FindEagerStreamUse(call2, glob.Name);
                        if (bad != null)
                        {
                            badStreamUse2 = true;
                            break;
                        }
                    }
                    if (badStreamUse != badStreamUse2)
                        Sink.WriteLine("***!!! BUG! BUG! BUG! Inconsistent bad stream use: {0} for combo", badStreamUse2);
                }
            }
        }
        catch (NotImplementedException nyi)
        {
            Sink.WriteLine("*** NYI Exception: '{0}'", nyi.Message);
            return;
        }
        catch (NotSupportedException e)
        {
            Sink.WriteLine("*** Unsupported Exception: '{0}'", e.Message);
            return;
        }

        DType type = res.Type;
        Type st = TypeManager.GetSysTypeOrNull(type);
        Assert.IsTrue(st != null);

        var (val, ctx) = AssignArgsAndInvoke(resCodeGen, write: true, streaming: streaming);

        if (val == null)
            Assert.IsTrue(type.IsOpt);
        else if (val is Exception ex)
        {
            Sink.WriteLine("*** Exec Exception! ***");
            for (var exCur = ex; exCur != null; exCur = exCur.InnerException)
            {
                Sink.WriteLine(exCur.GetType().ToString());
                Sink.WriteLine(exCur.Message);
                // The stack trace contains local file names so is not baselineable.
                // Sink.WriteLine(exCur.StackTrace);
                Sink.WriteLine();
            }
            return;
        }
        else
            Assert.IsTrue(st.IsAssignableFrom(val.GetType()));

        if (!ValueWriter.IsSeq(val) && !ValueWriter.IsTen(val))
            Sink.TWrite("Type: ").TWriteRawType(val?.GetType()).Write(", Value: ");

        int maxPrev = Config.Max;
        if ((testOpts & TestCodeOptions.WithBytes) != 0)
            Config.Max = 2;
        bool tupPrev = Config.TupleNewLine;
        Config.TupleNewLine = (testOpts & TestCodeOptions.TupNewLine) != 0;
        ValueWriter.WriteValue(type, val);
        Config.Max = maxPrev;
        Config.TupleNewLine = tupPrev;

        if (ctx is TestExecCtx tctx)
        {
            // Prevent duplicate log lines from being appended when reiterating val below.
            tctx.SetOut(null);
            WritePingCounts(tctx, resCodeGen.IdBndMap);
        }

        bool testSer = !type.HasGeneral && !type.HasModule;
        if (type.HasSequence)
        {
            // Reiterating sequences should not modify elements. Repro for bug WI #44237.
            var resCgDirect = CodeGenDirect.Run(bfma.BoundTree, Host);
            var (data, _) = AssignArgsAndInvoke(resCgDirect, write: false, streaming: streaming);

            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            Sink.SetOut(sb1, out var prev);
            ValueWriter.WriteValue(type, data);
            Sink.SetOut(sb2, out _);
            ValueWriter.WriteValue(type, data);
            string str1 = sb1.ToString();
            string str2 = sb2.ToString();
            if (str1 != str2)
            {
                Sink.WriteLine("***!!! BUG! BUG! BUG! Sequence consistency failure:");
                Sink.WriteLine(str1);
                Sink.WriteLine("***!!! Second:");
                Sink.WriteLine(str2);
                Sink.WriteLine("***!!! End");
                testSer = false;
            }
            Sink.SetOut(prev, out _);
        }

        // Write and Read and verify consistency.
        // REVIEW: Currently, we can't read/write the general type.
        if (testSer)
        {
            Type stDes;
            object valDes;
            var sb1 = new StringBuilder();

            using (var strm = new MemoryStream())
            {
                // Check that the writer properly handles prexisting bytes in the stream.
                var bytesPre = new byte[] { 0xA1, 0x9F, 0x3E };
                var bytesPost = new byte[] { 0x5A, 0x7B, 0xF0, 0x93, 0x86 };
                strm.Write(bytesPre);
                bool tmp = TypeManager.TryWrite(strm, type, val);
                Assert.IsTrue(tmp);
                if ((testOpts & TestCodeOptions.WithBytes) != 0)
                    Sink.WriteLine("Total blob size: {0} bytes", strm.Length - 3);
                strm.Write(bytesPost);

#if SHOW_STREAM
                sb1.Clear();
                var data = strm.ToArray();
                foreach (byte b in data)
                    sb1.Append(32 <= b && b < 128 ? (char)b : '.').Append(' ');
                System.Diagnostics.Debug.WriteLine(sb1);
                sb1.Clear();
                const string hex = "0123456789ABCDEF";
                foreach (byte b in data)
                    sb1.Append(hex[b >> 4]).Append(hex[b & 0xF]);
                System.Diagnostics.Debug.WriteLine(sb1);
#endif

                strm.Position = 0;
                for (int i = 0; i < bytesPre.Length; i++)
                    Assert.AreEqual(strm.ReadByte(), bytesPre[i]);

                // Wrap in a stream that reads one byte at a time.
                using (var wrapper = new EachByteReadStream(strm))
                {
                    tmp = TypeManager.TryRead(wrapper, type, out stDes, out valDes);
                    Assert.IsTrue(tmp);
                }

                Assert.AreEqual(strm.Position, strm.Length - bytesPost.Length);
                for (int i = 0; i < bytesPost.Length; i++)
                    Assert.AreEqual(strm.ReadByte(), bytesPost[i]);
            }

            Assert.IsTrue((valDes == null) == (val == null));

            // TryDeserialize shouldn't succeed if this isn't true.
            Assert.IsTrue(valDes == null || stDes.IsAssignableFrom(valDes.GetType()));

            Assert.AreEqual(st, stDes);

            sb1.Clear();
            var sb2 = new StringBuilder();
            bool showStrides = Config.ShowTensorStrides;
            Config.ShowTensorStrides = false;
            bool lazyOrder = Config.LazyOrder;
            Config.LazyOrder = false;
            Sink.SetOut(sb1, out var prev);
            ValueWriter.WriteValue(type, val);
            Sink.SetOut(sb2, out _);
            ValueWriter.WriteValue(type, valDes);
            string str1 = sb1.ToString();
            string str2 = sb2.ToString();
            if (str1 != str2)
            {
                Sink.WriteLine("***!!! BUG! BUG! BUG! Serialization consistency failure:");
                Sink.WriteLine(str1);
                Sink.WriteLine("***!!! Second:");
                Sink.WriteLine(str2);
                Sink.WriteLine("***!!! End");
            }

            Sink.SetOut(prev, out _);
            Config.LazyOrder = lazyOrder;
            Config.ShowTensorStrides = showStrides;
        }

        if (type.IsSequence)
        {
            long count = -1;
            if (val is ICanCount counter)
            {
                if (counter.TryGetCount(out count))
                {
                    long numPing = 0;
                    var num = counter.GetCount(() => numPing++);
                    if (numPing != 0)
                        Sink.WriteLine("***!!! BUG! BUG! BUG! GetCount() used callback: {0}", numPing);
                    if (num != count)
                        Sink.WriteLine("***!!! BUG! BUG! BUG! GetCount() discrepancy: {0} vs {1}", num, count);
                }
                else
                    count = -1;
            }

            if (val is ICursorable cursable)
            {
                using var cursor = cursable.GetCursor();
                if (!cursor.MoveTo(0) && count > 0)
                    Sink.WriteLine("***!!! BUG! BUG! BUG! Cursor.MoveTo(0) failed");
                if (0 <= count && count <= 100 && cursor.MoveTo(count))
                    Sink.WriteLine("***!!! BUG! BUG! BUG! Cursor.MoveTo(count) succeeded");
                if (count > 0 && !cursor.MoveTo(Math.Min(100, count) - 1))
                    Sink.WriteLine("***!!! BUG! BUG! BUG! Cursor.MoveTo(Min(100, count) - 1) failed");
            }
        }
    }

    private void WritePingCounts(TestExecCtx tctx, IdBndMap idBndMap)
    {
        Sink.WriteLine("*** Ctx ping count: {0}", tctx.PingCount);
        if (tctx.PingCountNoId > 0)
            Sink.WriteLine("    [_] {0}", tctx.PingCountNoId);

        foreach (var (bnd, rng) in idBndMap.BndToIdRng)
        {
            Assert.IsTrue(rng.Count > 0);
            bool hasPings = false;
            for (int id = rng.Min; id < rng.Lim; id++)
            {
                if (tctx.GetIdPingCount(id) > 0)
                {
                    hasPings = true;
                    break;
                }
            }

            if (!hasPings)
                continue;

            long count = tctx.GetIdPingCount(rng.Min);
            string idRest = "";
            string countRest = "";
            if (rng.Count > 1)
            {
                var sb = new StringBuilder("=").Append(count);
                for (int id = rng.Min + 1; id < rng.Lim; id++)
                {
                    long pings = tctx.GetIdPingCount(id);
                    sb.Append('+').Append(pings);
                    count += pings;
                }
                countRest = sb.ToString();
                idRest = ":" + rng.Lim;
            }

            string strBnd = BndNodePrinter.Run(bnd, BndNodePrinter.Verbosity.Terse);
            Sink.WriteLine("    [{0}{1}]({2}{3}): {4}", rng.Min, idRest, count, countRest, strBnd);
        }
    }

    private (object, ExecCtx) AssignArgsAndInvoke(CodeGenResult resCodeGen, bool write = false, bool streaming = false)
    {
        Validation.AssertValue(resCodeGen);
        var globals = resCodeGen.Globals;
        var typeRes = resCodeGen.BoundTree.Type;

        Validation.Assert(!globals.IsDefault);

        if (write)
        {
            Sink.Write("Func sig: (");
            if (globals.Length > 0)
            {
                string pre = "";
                foreach (var glob in globals)
                {
                    var name = glob.IsCtx ? "<ctx>" : glob.IsThis ? "<this>" : glob.Name.ToDottedSyntax();
                    Sink.Write("{0}{1}:{2}", pre, name, glob.Type);
                    pre = ", ";
                }
            }
            Sink.WriteLine(") to {0}", typeRes);
        }

        int carg = globals.Length;
        var args = new object[carg];
        var slotSet = new bool[carg];

        TestExecCtx ctx = null;
        if (carg > 0)
        {
            int cur = 0;
            foreach (var glob in globals)
            {
                Assert.IsNotNull(glob);
                int slot = glob.Slot;
                Assert.IsFalse(slotSet[slot], "The global map value at slot {0} appears multiple times.", slot);
                Assert.AreEqual(cur, slot);
                cur++;

                if (glob.IsCtx)
                {
                    // Execution context.
                    Assert.IsTrue(glob.Name.IsRoot);
                    Assert.IsFalse(glob.Type.IsValid);
                    Assert.IsNull(ctx);
                    ctx = new TestExecCtx(write ? Sink.Builder : null, resCodeGen.IdBndMap.Count);
                    args[slot] = ctx;
                }
                else if (_globals.TryGetValue(glob.Name, out var pair))
                {
                    // Known value (possibly "this", indicated by kvp.Key.IsRoot).
                    Assert.IsTrue(glob.Type.IsValid);
                    (BoundNode bndCur, object valCur) = pair;
                    Assert.AreEqual(glob.Type, bndCur.Type);
                    args[slot] = valCur;
                }
                else
                {
                    // The corresponding field in the global record type is used for invoking the generated code.
                    // In this case, the arg has default value, which becomes invalid if the type is a non-opt
                    // reference type.
                    Assert.IsTrue(glob.Type.IsValid);
                    Assert.IsFalse(glob.Name.IsRoot, "unexpected use of <this>");
                    _nsToGlobalType.TryGetValue(glob.Name.Parent, out var types).Verify();
                    var typeArg = types.GetNameTypeOrDefault(glob.Name.Leaf);

                    if (streaming && resCodeGen.CreateRunnerFunc != null &&
                        typeArg.IsSequence && glob.Name.Leaf.Value.Length == 1)
                    {
                        // The streaming cases. Use a fake sequence that blows up on any illegal calls.
                        args[slot] = CreateThrowingSequence(typeArg.ItemTypeOrThis);
                    }
                    else
                    {
                        Assert.AreEqual(glob.Type, typeArg);
                        Assert.IsFalse(IsNonOptReferenceType(typeArg));
                    }
                }

                slotSet[slot] = true;
            }
        }

        var fn = resCodeGen.Func;
        var fnProc = resCodeGen.CreateRunnerFunc;
        Validation.Assert((fn != null) != (fnProc != null));

        object res;
        try
        {
            if (fnProc != null)
            {
                var runner = fnProc(args, new NoopActionHost(TypeManager));
                runner.BeginAbort();
                return (runner, ctx);
            }

            res = fn(args);
        }
        catch (Exception ex)
        {
            if (ex.Data.Contains("IsBug"))
                Assert.Fail();
            return (ex, ctx);
        }

        if (ctx != null)
            ValidatePings(ctx);

        // Do some type validation.
        var t = TypeManager.IsOfType(res, typeRes);
        Assert.AreNotEqual<TriState>(TriState.No, t);

        if (!typeRes.IsOpt)
        {
            Assert.AreEqual(t, TypeManager.IsOfType(res, typeRes.ToOpt()));
            Assert.AreEqual(TriState.No, TypeManager.IsOfType(null, typeRes));
        }
        else
        {
            if (res != null)
            {
                Assert.AreEqual(TriState.Yes, TypeManager.IsOfType(null, typeRes));
                if (typeRes.HasReq)
                    Assert.AreEqual(t, TypeManager.IsOfType(res, typeRes.ToReq()));
            }
            else
                Assert.AreEqual(TriState.Yes, t);
        }

        return (res, ctx);
    }

    private IEnumerable CreateThrowingSequence(DType typeItem)
    {
        TypeManager.TryEnsureSysType(typeItem, out var stItem).Verify();
        var meth = new Func<IEnumerable<object>>(CreateThrowingSequence<object>)
            .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
        return (IEnumerable)meth.Invoke(null, null);
    }

    /// <summary>
    /// This is used to detect eager use of a sequence in tests. All eager operations like
    /// <c>GetEnumerator</c> throw. Probing operations like <c>TryGetCount</c> are acceptable.
    /// </summary>
    protected static IEnumerable<T> CreateThrowingSequence<T>()
    {
        return new ThrowingSequence<T>();
    }

    private sealed class ThrowingSequence<T> :
        IEnumerable<T>, ICanCount, ICanSnap<T>, ICursorable<T>, IIndexedEnumerable<T>
    {
        public bool IsDone => throw new NotImplementedException();

        private Exception Throw()
        {
            throw new InvalidOperationException("Illegal call to sequence method");
        }

        public long GetCount(Action callback)
        {
            throw Throw();
        }

        public ICursor<T> GetCursor()
        {
            throw Throw();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw Throw();
        }

        public IEnumerable<T> Snap()
        {
            throw Throw();
        }

        public bool TryGetCount(out long count)
        {
            count = -1;
            return false;
        }

        ICursor ICursorable.GetCursor()
        {
            throw Throw();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw Throw();
        }

        IEnumerable ICanSnap.Snap()
        {
            throw Throw();
        }

        public IIndexedEnumerator<T> GetIndexedEnumerator()
        {
            throw Throw();
        }

        IIndexedEnumerator IIndexedEnumerable.GetIndexedEnumerator()
        {
            throw Throw();
        }
    }

    private static void ValidatePings(TestExecCtx ctx)
    {
        Validation.AssertValue(ctx);

        long pingSum = 0;
        var pingInfo = ctx.GetPingInfo();
        for (int i = 0; i < pingInfo.Length; i++)
        {
            long pings = pingInfo[i];
            pingSum += pings;

            Assert.IsTrue(ctx.GetIdPingCount(i) == pings);

            if (i == ctx.IdCount)
                Assert.IsTrue(pings == ctx.PingCountNoId);
        }

        Assert.IsTrue(pingSum == ctx.GetTotalPingCount(includeNoId: true));
        Assert.IsTrue(pingSum - ctx.PingCountNoId == ctx.GetTotalPingCount(includeNoId: false));
    }

    private static bool IsNonOptReferenceType(DType type)
    {
        if (type.IsOpt)
            return false;

        switch (type.Kind)
        {
        case DKind.Record:
        case DKind.Tuple:
        case DKind.Tensor:
        case DKind.Uri:
            return true;
        }

        return false;
    }

    private void AppendDiagnostics<TDiag>(Immutable.Array<TDiag> diagnostics)
        where TDiag : BaseDiagnostic
    {
        foreach (BaseDiagnostic d in diagnostics)
        {
            Sink.Write("*** ");
            d.Format(Sink, options: DiagFmtOptions.DefaultTest);
            Sink.WriteLine();
        }
    }
}

internal sealed class TestExecCtx : PingsPerIdExecCtx
{
    private StringBuilder _sb;

    private readonly long _pingCancel;
    private long _pingCount;

    private volatile int _secCount;
    private long _seed;

    public long PingCount => _pingCount;

    public sealed class ExecException : Exception
    {
        public ExecException()
        {
        }
    }

    public TestExecCtx(StringBuilder sb, int idCount, long pingCancel = 0)
        : base(idCount)
    {
        Validation.AssertValueOrNull(sb);
        _sb = sb;
        _pingCancel = pingCancel;
    }

    public override DateTimeOffset GetDateTimeOffset(int id)
    {
        // Use a fixed date, time, and offset plus an incremented number of seconds.
        int cur = Interlocked.Increment(ref _secCount);

        return new DateTimeOffset(
            // This part is the "clock time", not UTC. The offset is subtracted to produce UTC.
            new DateTime(2023, 4, 10, 13, 30, 40) + new TimeSpan(1234567) + new TimeSpan(0, 0, cur),
            new TimeSpan(-7, 0, 0));
    }

    public override Guid MakeGuid(int id)
    {
        // For testing, we use a random value for the "a" field with all others zero.
        var rnd = new Random((int)Interlocked.Increment(ref _seed));
        return new Guid(rnd.Next(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public override Random GetRandom(int id)
    {
        // Use an auto-incremented seed.
        return base.GetRandom(id, Interlocked.Increment(ref _seed));
    }

    public override double GetRandomValue(int id)
    {
        // Use an auto-incremented seed.
        return base.GetRandomValue(id, Interlocked.Increment(ref _seed));
    }

    public void SetOut(StringBuilder sb)
    {
        Validation.AssertValueOrNull(sb);
        _sb = sb;
    }

    public override void Ping(int id = -1)
    {
        base.Ping(id);
        if (Interlocked.Increment(ref _pingCount) == _pingCancel)
            Cancel(new ExecException());
    }

    public override void Log(int id, string fmt, params object[] args) => Log(id, string.Format(fmt, args));

    public override void Log(int id, string msg)
    {
        _sb?.Append(string.Format("  ** [{0}] ", id == -1 ? "_" : id.ToString())).AppendLine(msg);
    }
}

/// <summary>
/// A stream wrapper that reads one byte at a time. This is for testing stream reading functionality,
/// ensuring that the caller does not depend on <see cref="Stream.Read(byte[], int, int)"/>
/// reading the full count of bytes requested.
/// NOTE: This does <i>not</i> take ownership of the wrapped stream. That is, dispose/close of this
/// does nothing.
/// </summary>
internal sealed class EachByteReadStream : Stream
{
    private readonly Stream _strm;

    public EachByteReadStream(Stream strm)
    {
        Validation.AssertValue(strm);
        _strm = strm;
    }

    public override bool CanRead => _strm.CanRead;

    public override bool CanSeek => _strm.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _strm.Length;

    public override long Position { get => _strm.Position; set => throw new NotImplementedException(); }

    public override void Flush()
    {
        _strm.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Validation.BugCheckValue(buffer, nameof(buffer));
        Validation.BugCheckIndex(offset, buffer.Length, nameof(offset));
        Validation.BugCheckIndex(count - 1, buffer.Length - offset, nameof(count));

        // Only ask for one byte.
        return _strm.Read(buffer, offset, 1);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _strm.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}
