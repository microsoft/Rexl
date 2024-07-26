// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Onnx;
using Microsoft.Rexl.Solve;

namespace Microsoft.Rexl.RexlRun;

/// <summary>
/// The registry for functions available in RexlRun. This includes the standard rexl builtin functions.
/// </summary>
internal sealed class RexlRunOperations : OperationRegistry
{
    public RexlRunOperations()
        : base(BuiltinFunctions.Instance, BuiltinProcedures.Instance,
            SolverFunctions.Instance, ModelFunctions.Instance)
    {
    }
}

internal sealed class RexlRunGenerators : GeneratorRegistry
{
    public RexlRunGenerators()
        : base(BuiltinGenerators.Instance, SolverGenerators.Instance, ModelFuncGenerators.Instance)
    {
    }
}
