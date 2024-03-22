// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using ArgTuple = Immutable.Array<BoundNode>;

/// <summary>
/// Baseline test options.
/// </summary>
[Flags]
public enum TestOptions
{
    None = 0x0000,

    ShowItemCount = 0x0001,
    Streaming = 0x0002,
    ShowBndKinds = 0x0004,
    Replicate = 0x0008,
    AllowVolatile = 0x0010,
    AllowProcedure = 0x0020,

    /// <summary>
    /// Split input by "###" delimited blocks, rather than by lines.
    /// </summary>
    SplitBlocks = 0x0040,

    /// <summary>
    /// Whether modules should be prohibited (by the binder).
    /// </summary>
    ProhibitModule = 0x0080,

    AllowGeneral = 0x0100,
}

/// <summary>
/// Base class for binder-level baseline tests.
/// </summary>
public abstract class BinderTestBase : RexlLineTestsBase<SbTypeSink, TestOptions>, IReducerHost
{
    private readonly SinkImpl _sink;

    protected sealed override SinkImpl Sink => _sink;

    protected sealed override bool AnyOut => _sink.Builder.Length > 0;

    protected sealed override string GetTextAndReset()
    {
        var res = _sink.Builder.ToString();
        _sink.Builder.Clear();
        return res;
    }

    private static readonly JsonSerializerOptions DiagSerializeOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    // The globals loaded from the global scripts.
    private Dictionary<NPath, BoundFormula> _globals;

    /// <summary>
    /// Whether to print typed node tree.
    /// </summary>
    protected bool ShouldPrintTypedParseTree { get; set; }

    /// <summary>
    /// Whether to include full log info of diagnostics.
    /// </summary>
    protected bool ShouldIncludeDiagLogInfo { get; set; }

    /// <summary>
    /// The verbosity for displaying bound nodes.
    /// </summary>
    protected BndNodePrinter.Verbosity Verb { get; set; } = BndNodePrinter.Verbosity.Terse;

    protected BinderTestBase()
    {
        _sink = new SinkImpl(this);
    }

    protected override bool UseBlock(TestOptions testOpts)
    {
        return (testOpts & TestOptions.SplitBlocks) != 0;
    }

    protected override BindOptions TestOptsToBindOpts(TestOptions testOpts)
    {
        BindOptions options = default;

        if ((testOpts & TestOptions.AllowVolatile) != 0)
            options |= BindOptions.AllowVolatile;
        if ((testOpts & TestOptions.AllowProcedure) != 0)
            options |= BindOptions.AllowProc;
        if ((testOpts & TestOptions.ProhibitModule) != 0)
            options |= BindOptions.ProhibitModule;
        if ((testOpts & TestOptions.AllowGeneral) != 0)
            options |= BindOptions.AllowGeneral;

        return options;
    }

    protected void WriteTypeAnnotation(BoundFormula bfma, RexlNode node)
    {
        // If TryGetNodeDType returns false, we still print default(DType) ('x').
        bfma.TryGetNodeType(node, out DType type);
        Sink.Write("DType:{0}", type);

        bfma.TryGetNodeScopeInfo(node, out var scopes);
        if (scopes == null)
            return;

        Sink.Write(", Scopes:[");
        string pre = "";
        foreach (var scope in scopes)
        {
            Sink.Write(pre);
            pre = ", ";
            if (scope.Alias.IsValid)
                Sink.WriteEscapedName(scope.Alias).Write(":");
            Sink.WriteType(scope.Type);
        }
        Sink.Write(']');
    }

    protected void PrintTypedParseTree(BoundFormula bfma)
    {
        Sink.WriteLine("Typed Parse Tree:");
        RexlTreeDumper.Print(Sink, bfma.Formula.ParseTree, "  ", null,
            (prefix, node) =>
            {
                Sink.Write(prefix);
                WriteTypeAnnotation(bfma, node);
            });
    }

    protected override void InitFile()
    {
        base.InitFile();

        // Setup global dictionary.
        _globals = new Dictionary<NPath, BoundFormula>();
    }

    protected override void HandleGlobal(BoundFormula bfma, NPath full, string disp)
    {
        Assert.IsNotNull(bfma);

        _globals[full] = bfma;
        Sink.WriteLine("**** New definition: {0}, type: {1}", disp, bfma.BoundTree.Type);
    }

    protected override DType GetThisType()
    {
        if (_globals.TryGetValue(NPath.Root, out var bfma))
        {
            Validation.Assert(bfma.BoundTree.Type.IsValid);
            return bfma.BoundTree.Type;
        }

        return default;
    }

    protected override DType GetGlobalType(NPath full)
    {
        Assert.IsTrue(!full.IsRoot);

        if (_globals.TryGetValue(full, out var bfma))
        {
            Validation.Assert(bfma.BoundTree.Type.IsValid);
            return bfma.BoundTree.Type;
        }

        return base.GetGlobalType(full);
    }

    protected override void ProcessScript(string script, TestOptions testOpts)
    {
        Sink.WriteLine("> {0}", script);

        var fma = RexlFormula.Create(SourceContext.Create(script));
        ValidateScript(fma);
        ProcessFma(fma, testOpts);

        if (fma.CorrectedText != null)
        {
            Sink.WriteLine("=== Corrected by parser: [{0}]", fma.CorrectedText);
            fma = RexlFormula.Create(SourceContext.Create(fma.CorrectedText));
            ValidateScript(fma);
            ProcessFma(fma, testOpts);
        }
    }

    protected void ProcessFma(RexlFormula fma, TestOptions testOpts)
    {
        bool streaming = (testOpts & TestOptions.Streaming) != 0;
        var host = new BindHostImpl(this, streaming);

        // First show the bound formula before any reductions or optimizations.
        var options = BindOptions.DontReduce | TestOptsToBindOpts(testOpts);
        if (streaming)
            options |= BindOptions.AllowProc;
        var bfma = BoundFormula.Create(fma, host, options);
        ValidateBfma(bfma, host);

        // REVIEW: Perhaps this is worth expanding at some point.
        if (bfma.CorrectedText != null)
            Sink.WriteLine("Corrected by binder: [{0}]", bfma.CorrectedText);

        var res = bfma.BoundTree;

        if (ShouldPrintTypedParseTree)
            PrintTypedParseTree(bfma);
        else
            Sink.WriteLine("{0} : {1}", fma.ParseTree, res.Type);

        if (fma.HasDiagnostics)
        {
            Sink.WriteLine("=== Parse diagnostics:");
            AppendDiagnostics(fma.Tokens.Source, fma.Diagnostics);
        }

        if (bfma.HasDiagnostics)
        {
            // If there are also parse diagnostics, print the banner.
            if (fma.HasDiagnostics)
                Sink.WriteLine("=== Bind diagnostics:");
            AppendDiagnostics(fma.Tokens.Source, bfma.Diagnostics);
        }

        Sink.TWrite("Binder : ").WriteBndNode(res, testOpts).WriteLine();
        WriteReductions(res, testOpts);

        if ((testOpts & TestOptions.Replicate) != 0)
        {
            res = BndTupleNode.Create(ArgTuple.Create(res, res));
            Sink.TWrite("Tupled : ").WriteBndNode(res, testOpts).WriteLine();
            WriteReductions(res, testOpts);
        }
    }

    protected void WriteReductions(BoundNode res, TestOptions testOpts = default)
    {
        res = RunReduce(res, testOpts);
        var next = RunHoist(res, testOpts);
        const int limit = 10;
        int i = 0;
        while (next != res && i++ < limit)
        {
            next = RunReduce(res = next, testOpts);
            if (next == res)
                break;
            next = RunHoist(res = next, testOpts);
        }
    }

    protected BoundNode RunReduce(BoundNode res, TestOptions testOpts)
    {
        return RunTreePassCore(res, Reducer.Run, "Reducer", "Reduced", testOpts);
    }

    protected BoundNode RunHoist(BoundNode res, TestOptions testOpts)
    {
        return RunTreePassCore(res, Optimizer.Run, "Hoister", "Hoisted", testOpts);
    }

    protected BoundNode RunTreePassCore(BoundNode res, Func<IReducerHost, BoundNode, BoundNode> func,
        string src, string action, TestOptions testOpts)
    {
        var next = func(this, res);
        if (next == res)
            return next;

        BoundTreeValidator.Run(next, next.HasErrors);
        if (next.Type != res.Type)
            Sink.WriteLine("*** {0} with different type! BUG! BUG! BUG! Type: {1}", action, next.Type);
        Sink.TWrite(src).TWrite(": ").WriteBndNode(next, testOpts).WriteLine();

        // Verify that the result is fully reduced. That is, run the reducer again and
        // holler if the result is different.
        res = next;
        next = func(this, res);
        BoundTreeValidator.Run(next, next.HasErrors);
        if (next != res)
        {
            Sink.WriteLine("*** {0} further! BUG! BUG! BUG! Type: {1}", action, next.Type);
            Sink.TWrite(src).TWrite(": ").WriteBndNode(next, testOpts).WriteLine();
        }

        return next;
    }

    protected void AppendDiagnostics<TDiag>(SourceContext source, Immutable.Array<TDiag> diagnostic)
        where TDiag : BaseDiagnostic
    {
        foreach (BaseDiagnostic d in diagnostic)
        {
            Sink.Write("*** ");
            var options = DiagFmtOptions.DefaultTest;
            if (d is RexlDiagnostic rd && rd.Tok.Stream.Source != source)
                options |= DiagFmtOptions.PositionBoth;
            d.Format(Sink, options: options);

            if (ShouldIncludeDiagLogInfo)
            {
                object toLog;
                Sink.WriteLine();
                switch (d)
                {
                case MessageDiag m:
                    toLog = new
                    {
                        MessageTag = m.Message.Tag,
                        IsError = m.IsError,
                    };
                    break;
                case RexlDiagnostic r:
                    bool isDep = r.Message.Tag?.StartsWith("WrnDeprecated") == true;
                    bool isNonDeprecatedIdent = !isDep && r.Tok.Kind == TokKind.Ident;
                    toLog = new
                    {
                        IsDeprecation = isDep,
                        TokText = isNonDeprecatedIdent || r.Guess == null ? null : r.RngGuess.GetFragment(),
                        Guess = r.Guess,
                        MessageTag = r.Message.Tag,
                        IsError = r.IsError,
                    };
                    break;
                default:
                    throw new NotSupportedException("Unhandled diagnostic type");
                }

                string s = JsonSerializer.Serialize(toLog, DiagSerializeOptions)
                    .Replace(",", string.Empty)
                    .Replace("\"", string.Empty);

                Sink.Write(s);
            }

            Sink.WriteLine();
        }
    }

    public BoundNode OnMapped(BoundNode bndOld, BoundNode bndNew)
    {
        // REVIEW: Ideally we should map the new
        // bound node back to the original parse tree.
        // Perhaps the BoundFormula should keep the bound node
        // to parse node mapping.
        Assert.IsNotNull(bndOld);
        Assert.IsNotNull(bndNew);
        Assert.AreEqual(bndOld.Type, bndNew.Type);
        return bndNew;
    }

    public BoundNode Associate(BoundNode bndOld, BoundNode bndNew)
    {
        Assert.IsNotNull(bndNew);
        return bndNew;
    }

    public void Warn(BoundNode bnd, StringId msg)
    {
        Sink.WriteLine("*** Warning: Node: {0}, Message: {1}", BndNodePrinter.Run(bnd, Verb), msg.GetString());
    }

    protected sealed class SinkImpl : SbTypeSink
    {
        private readonly BinderTestBase _parent;

        public SinkImpl(BinderTestBase parent)
            : base()
        {
            Validation.AssertValue(parent);
            _parent = parent;
        }

        public SinkImpl WriteBndNodeOpt(BoundNode bnd)
        {
            if (bnd is null)
                return this.TWrite("<null>");
            return this.WriteBndNode(bnd);
        }

        public SinkImpl WriteBndNode(BoundNode bnd, TestOptions testOpts = default)
        {
            Validation.AssertValue(bnd);
            if ((testOpts & TestOptions.ShowItemCount) != 0 && bnd.Type.IsSequence)
            {
                var (min, max) = bnd.GetItemCountRange();
                this.TWrite("(").TWrite(min).TWrite(", ").TWrite(max == long.MaxValue ? "*" : max.ToString()).Write(") ");
            }
            if ((testOpts & TestOptions.ShowBndKinds) != 0)
                this.Write("[{0}] ", bnd.AllKinds.ToString().Replace(", ", "|", StringComparison.Ordinal));
            return this.TWrite(BndNodePrinter.Run(bnd, _parent.Verb));
        }
    }
}
