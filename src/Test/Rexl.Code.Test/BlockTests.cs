// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Threading.Tasks;
using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Lex;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class BlockTests : BlockTestsBase<bool>
{
    private readonly OperationRegistry _opers;
    private readonly GeneratorRegistry _gens;

    protected override OperationRegistry Operations => _opers;
    protected override GeneratorRegistry Generators => _gens;

    public BlockTests()
        : base()
    {
        _opers = new AggregateOperationRegistry(TestFunctions.Instance, MultiFormOperations.Instance);
        _gens = new AggregateGeneratorRegistry(TestGenerators.Instance, MultiFormGenerators.Instance);
    }

    [TestMethod]
    public async Task SegmentedTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmented, @"Block/Segmented").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SegmentedCustomRecoverTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmentedCustomRecover, @"Block/SegmentedCustomRecover")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SegmentedFuzzingTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmentedFuzzing, @"Block/SegmentedFuzzing")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task GeneralBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/General").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ContextualBaselineTests()
    {
        int count = await DoBaselineTestsAsync(Run, @"Block/Contextual/Ktx.txt").ConfigureAwait(false);
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
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Udf").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ScenarioBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Scenario").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcRbinTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Rbin").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcFileSysTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/FileSys.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcNowTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Now.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcParquetTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Parquet.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcNonSeekableTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoILNonSeekableStreams, @"Block/Procedures/NonSeekable.txt")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcParquetErrorsTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/ParquetErrors.txt")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcParquetPauseTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/ParquetPause.txt")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcBytesTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Bytes.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcTextTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Text.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcPipeSafetyTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/PipeSafety.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcPipingTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/Piping.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcStateTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/States.txt").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ProcMultiFormTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Procedures/MultiFormProc.txt")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task TaskTests()
    {
        await DoBaselineTestsAsync(ProcessFileSegmented, @"Block/Task").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SerializationTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Serialization").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task EvaluateExpressionBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileAndEvaluateAsync, @"Block/Evaluate").ConfigureAwait(false);
    }

    /// <summary>
    /// This one is to separate out work in progress. Normally, when pushed to master,
    /// this directory will be empty.
    /// </summary>
    [TestMethod]
    public async Task WipBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileWithIL, @"Block/Wip").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ImageTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Image").ConfigureAwait(false);
    }
}
