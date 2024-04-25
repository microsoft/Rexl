// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

using Microsoft.Rexl.Code;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;
// Use non-caching code generator, then wrap at the global level, just for coverage.
[TestClass]
public sealed class BlockTests : BlockTestsBase<bool>
{
    [TestMethod]
    public async Task ModelTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Block/Model").ConfigureAwait(false);
    }
}
