// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Threading.Tasks;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Lex;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class BlockTests : BlockTestsBase<bool>
{
    [TestMethod]
    public async Task SegmentedTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmented, @"Block/Segmented");
    }

    [TestMethod]
    public async Task SegmentedCustomRecoverTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmentedCustomRecover, @"Block/SegmentedCustomRecover");
    }

    [TestMethod]
    public async Task SegmentedFuzzingTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmentedFuzzing, @"Block/SegmentedFuzzing");
    }

    [TestMethod]
    public async Task GeneralBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/General");
    }

    [TestMethod]
    public async Task ContextualBaselineTests()
    {
        int count = await DoBaselineTestsAsync(Run, @"Block/Contextual/Ktx.txt");
        Assert.AreEqual(1, count);

        Task Run(string pathHead, string pathTail, string text, bool options)
        {
            // Make sure all contextual keywords can be used as an ordinary global name.
            // Create a script that assigns to and then evaluates each contextual keyword.
            var sb = new StringBuilder();
            foreach (var info in RexlLexer.Instance.GetKeywords())
            {
                if (info.Tid.IsContextualKwd())
                {
                    Assert.IsFalse(info.Tid.IsReservedKwd());
                    sb.AppendFormat("{0} := \"{0}:{1}\"; {0};", info.Rep, info.Std).AppendLine();
                }
            }

            return ProcessFileNoIL(pathHead, pathTail, sb.ToString(), options);
        }
    }

    [TestMethod]
    public async Task UdfBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Udf");
    }

    [TestMethod]
    public async Task ScenarioBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Scenario");
    }

    [TestMethod]
    public async Task ProcRbinTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Rbin");
    }

    [TestMethod]
    public async Task ProcFileSysTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/FileSys.txt");
    }

    [TestMethod]
    public async Task ProcNowTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Now.txt");
    }

    [TestMethod]
    public async Task ProcParquetTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Parquet.txt");
    }

    [TestMethod]
    public async Task ProcNonSeekableTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoILNonSeekableStreams, @"Block/Procedures/NonSeekable.txt");
    }

    [TestMethod]
    public async Task ProcParquetErrorsTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/ParquetErrors.txt");
    }

    [TestMethod]
    public async Task ProcParquetPauseTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/ParquetPause.txt");
    }

    [TestMethod]
    public async Task ProcBytesTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Bytes.txt");
    }

    [TestMethod]
    public async Task ProcTextTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Text.txt");
    }

    [TestMethod]
    public async Task ProcPipeSafetyTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/PipeSafety.txt");
    }

    [TestMethod]
    public async Task ProcPipingTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Piping.txt");
    }

    [TestMethod]
    public async Task ProcStateTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/States.txt");
    }

    [TestMethod]
    public async Task ProcMultiFormTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/MultiFormProc.txt");
    }

    [TestMethod]
    public async Task TaskTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmented, @"Block/Task");
    }

    [TestMethod]
    public async Task SerializationTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Serialization");
    }

    [TestMethod]
    public async Task EvaluateExpressionBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileAndEvaluateAsync, @"Block/Evaluate");
    }

    /// <summary>
    /// This one is to separate out work in progress. Normally, when pushed to master,
    /// this directory will be empty.
    /// </summary>
    [TestMethod]
    public async Task WipBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileWithIL, @"Block/Wip");
    }

    [TestMethod]
    public async Task ImageTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Image");
    }
}
