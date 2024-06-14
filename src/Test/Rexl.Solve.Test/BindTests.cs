// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Rexl;
using Microsoft.Rexl.Solve;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class BindTests : BinderTestBase
{
    private readonly OperationRegistry _opers;

    protected override OperationRegistry Operations => _opers;

    public BindTests()
        : base()
    {
        _opers = new AggregateOperationRegistry(TestFunctions.Instance, SolverFunctions.Instance);
    }

    [TestMethod]
    public void FuncBindTests()
    {
        DoBaselineTests(ProcessFile, @"Bind");
    }
}
