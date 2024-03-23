// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class TensorSoftMaxGen : GetMethGen<TensorSoftMaxFunc>
{
    public static readonly TensorSoftMaxGen Instance = new TensorSoftMaxGen();

    private readonly MethodInfo _meth;

    private TensorSoftMaxGen()
    {
        _meth = new Func<Tensor<float>, Tensor<float>>(Tensor.SoftMax).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        meth = _meth;
        return true;
    }
}
