// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Onnx;

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
        _opers = new AggregateOperationRegistry(
            TestFunctions.Instance, ModelFunctions.Instance);
        _gens = new AggregateGeneratorRegistry(
            TestGenerators.Instance, ModelFuncGenerators.Instance);
    }

    [TestMethod]
    public async Task ModelTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Model").ConfigureAwait(false);
    }
}
