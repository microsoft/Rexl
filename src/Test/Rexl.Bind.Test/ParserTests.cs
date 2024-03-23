// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Statement;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public class ParserTests : RexlTestsBaseText<bool>
{
    [TestMethod]
    public void ParseExprBaselineTests()
    {
        DoBaselineTests(ProcessExprFile, @"Parser/Expr");
    }

    [TestMethod]
    public void ParseWipBaselineTests()
    {
        DoBaselineTests(ProcessExprFile, @"Parser/Wip");
    }

    private void ProcessExprFile(string pathHead, string pathTail, string text, bool opts)
    {
        var segs = SplitHashBlocks(text);
        foreach (var source in segs)
        {
            var lines = SplitLines(source);
            foreach (var line in lines)
                Sink.WriteLine("> {0}", line);

            var fma = RexlFormula.Create(SourceContext.Create(source));
            ValidateScript(fma);

            var node = fma.ParseTree;
            Sink.WriteLine("Node: [{0}]", RexlPrettyPrinter.Print(node));
            if (fma.CorrectedText != null)
                Sink.WriteLine("Corrected: [{0}]", fma.CorrectedText);
            Sink.WriteLine("Dump:");
            RexlTreeDumper.Print(Sink, node, "  ", source).WriteLine();

            if (fma.HasDiagnostics)
            {
                foreach (var diag in fma.Diagnostics)
                {
                    diag.Format(Sink, options: DiagFmtOptions.DefaultTest);
                    Sink.WriteLine();
                }
            }

            foreach (var tok in fma.AllTokens)
            {
                if (tok is CommentToken cmt)
                    Sink.WriteLine("Comment: {0}", cmt.Render());
            }
            Sink.WriteLine("###");
        }
    }

    [TestMethod]
    public void ParseStmtBaselineTests()
    {
        DoBaselineTests(ProcessStmtFile, @"Parser/Stmt");
    }

    [TestMethod]
    public void ParseModuleBaselineTests()
    {
        DoBaselineTests(ProcessStmtFile, @"Parser/Module", options: true);
    }

    private void ProcessStmtFile(string pathHead, string pathTail, string text, bool noDump)
    {
        bool dump = !noDump;
        var segs = SplitHashBlocks(text);
        foreach (var source in segs)
        {
            var lines = SplitLines(source);
            foreach (var line in lines)
                Sink.WriteLine("> {0}", line);

            var rsl = RexlStmtList.Create(SourceContext.Create(source));
            ValidateScript(rsl);

            var node = rsl.ParseTree;
            Sink.WriteLine("Node: [{0}]", RexlPrettyPrinter.Print(node));
            if (rsl.CorrectedText != null)
                Sink.WriteLine("Corrected: [{0}]", rsl.CorrectedText);
            if (dump)
            {
                Sink.WriteLine("Dump:");
                RexlTreeDumper.Print(Sink, node, "  ", source).WriteLine();
            }

            if (rsl.HasDiagnostics)
            {
                foreach (var diag in rsl.Diagnostics)
                {
                    diag.Format(Sink, options: DiagFmtOptions.DefaultTest);
                    Sink.WriteLine();
                }
            }

            foreach (var tok in rsl.AllTokens)
            {
                if (tok is CommentToken cmt)
                    Sink.WriteLine("Comment: {0}", cmt.Render());
            }

            StmtFlow flow;
            try
            {
                flow = StmtFlow.Create(rsl);
            }
            catch (NotImplementedException)
            {
                flow = null;
            }

            if (flow != null)
            {
                if (flow.Diagnostics.Length > 0)
                {
                    Sink.WriteLine();
                    Sink.WriteLine("*** Flow Diagnostics:");
                    foreach (var diag in flow.Diagnostics)
                    {
                        diag.Format(Sink, options: DiagFmtOptions.DefaultTest);
                        Sink.WriteLine();
                    }
                }
                Sink.WriteLine();
                flow.DumpInto(Sink);
            }
            else
                Sink.WriteLine("Flow conversion NYI");

            Sink.WriteLine("###");
        }
    }

    private void ValidateScript(RexlScript script)
    {
        Assert.IsNotNull(script);

        // REVIEW: This is O(n^2). Is it acceptable for unit tests?
        int ichMin = script.TextRange.Min;
        int ichLim = script.TextRange.Lim;
        int del0 = Math.Max(1, (ichLim - ichMin) / 100);
        int del1 = Math.Max(1, (ichLim - ichMin) / 50);

        for (int ichMinCur = ichMin; ichMinCur <= ichLim; ichMinCur += del0)
        {
            for (int ichLimCur = ichMinCur; ichLimCur <= ichLim; ichLimCur += del1)
            {
                var (left, right) = script.GetNodesFromCharRange(ichMinCur, ichLimCur);
                Assert.IsTrue(left != null || right != null);
            }
        }
    }
}
