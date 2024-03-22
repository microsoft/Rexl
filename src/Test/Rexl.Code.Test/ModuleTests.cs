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
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Basic");
    }

    [TestMethod]
    public async Task ModuleCornerBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Corner");
    }

    [TestMethod]
    public async Task ModuleWipBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Wip");
    }

    [TestMethod]
    public async Task ModuleOptimizeBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Optimize");
    }
}
