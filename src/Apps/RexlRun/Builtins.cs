// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Code;

namespace Microsoft.Rexl.RexlRun;

/// <summary>
/// The registry for functions available in RexlRun. This includes the standard rexl builtin functions.
/// </summary>
internal sealed class RexlRunOperations : OperationRegistry
{
    public RexlRunOperations()
        : base(BuiltinFunctions.Instance, BuiltinProcedures.Instance)
    {
#if WITH_SOLVE
        AddParent(Solve.SolverFunctions.Instance);
#endif
#if WITH_ONNX
        AddParent(Onnx.ModelFunctions.Instance);
#endif
    }
}

internal sealed class RexlRunGenerators : GeneratorRegistry
{
    public RexlRunGenerators()
        : base(BuiltinGenerators.Instance)
    {
#if WITH_SOLVE
        AddParent(Solve.SolverGenerators.Instance);
#endif
#if WITH_ONNX
        AddParent(Onnx.ModelFuncGenerators.Instance);
#endif
    }
}
