// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Threading.Tasks;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Lex;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class ModuleTests : BlockTestsBase<bool>
{
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

    [TestMethod]
    public async Task ModuleOptimizeBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Optimize").ConfigureAwait(false);
    }
}
