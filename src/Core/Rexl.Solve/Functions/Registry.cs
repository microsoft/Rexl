﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Code;

namespace Microsoft.Rexl.Solve;

/// <summary>
/// The solver functions.
/// </summary>
public sealed class SolverFunctions : OperationRegistry
{
    public static readonly SolverFunctions Instance = new SolverFunctions();

    private SolverFunctions()
    {
        // Sat functions.
        AddOne(SatSolveFunc.Instance);
        AddOne(SatAtMostOneFunc.Instance);
        AddOne(SatNotFunc.Instance);

        // Module functions.
        AddOne(ModuleOptimizeFunc.Optimize);
        AddOne(ModuleOptimizeFunc.Minimize);
        AddOne(ModuleOptimizeFunc.Maximize);
    }
}

public sealed class SolverGenerators : GeneratorRegistry
{
    public static readonly SolverGenerators Instance = new SolverGenerators();

    private SolverGenerators()
    {
        Add(SatSolveFunc.MakeGen());
        Add(SatAtMostOneFunc.MakeGen());
        Add(SatNotFunc.MakeGen());

        Add(ModuleOptimizeGen.Instance);
    }
}
