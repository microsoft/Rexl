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
public sealed class ModuleTests : BlockTestsBase<bool>
{
    protected override OperationRegistry Operations => TestFunctions.Instance;
    protected override GeneratorRegistry Generators => TestGenerators.Instance;

    [TestMethod]
    public async Task ModuleBasicBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Basic").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ModuleCornerBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Corner").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ModuleWipBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Wip").ConfigureAwait(false);
    }
}
