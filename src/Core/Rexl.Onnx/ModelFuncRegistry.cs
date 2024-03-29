// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl;
using Microsoft.Rexl.Code;

namespace Microsoft.Rexl.Onnx;

/// <summary>
/// The model functions in this project.
/// </summary>
public class ModelFunctions : OperationRegistry
{
    public static readonly ModelFunctions Instance = new ModelFunctions();

    private ModelFunctions()
    {
        AddOne(ResNetFunc.Instance);
        AddOne(EfficientNetFunc.Instance);
    }
}

/// <summary>
/// The generators corresponding to <see cref="ModelFunctions"/>.
/// </summary>
public sealed class ModelFuncGenerators : GeneratorRegistry
{
    public static readonly ModelFuncGenerators Instance = new ModelFuncGenerators();

    private ModelFuncGenerators()
    {
        Add(ResNetFunc.MakeGen());
        Add(EfficientNetFunc.MakeGen());
    }
}
