// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class TensorForEachGen : RexlOperationGenerator<TensorForEachFunc>
{
    public static readonly TensorForEachGen Instance = new TensorForEachGen();

    private TensorForEachGen()
    {
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));

        // REVIEW: We should ping it while operating.
        return false;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var func = GetOper(call);

        int cten = call.Scopes.Length;

        DType typeDst = call.Type;
        DType typeItemDst = typeDst.GetTensorItemType();
        Type stItemDst = codeGen.GetSystemType(typeItemDst);

        // REVIEW: Implement larger arities.

        var stsGen = new Type[cten + 1];
        for (int i = 0; i < cten; i++)
        {
            DType typeSrc = call.Args[i].Type;
            Validation.BugCheckParam(typeSrc.IsTensorReq, nameof(call));
            DType typeItemSrc = typeSrc.GetTensorItemType();
            Validation.BugCheckParam(call.Scopes[i].Type == typeItemSrc, nameof(call));
            stsGen[i] = codeGen.GetSystemType(typeItemSrc);
        }
        stsGen[cten] = stItemDst;

        MethodInfo meth;
        bool hasShrunkOut = false;
        switch (cten)
        {
        case 1:
            meth = func.IsEager ? CodeGenUtil.TenMap : CodeGenUtil.TenMapLazy;
            break;
        case 2:
            meth = CodeGenUtil.TenZip2;
            hasShrunkOut = true;
            break;
        case 3:
            meth = CodeGenUtil.TenZip3;
            hasShrunkOut = true;
            break;
        default:
            // REVIEW: Implement more arities!
            return base.TryGenCodeCore(codeGen, call, sts, out stRet);
        }

        Validation.Assert(meth.IsGenericMethodDefinition);
        meth = meth.MakeGenericMethod(stsGen);

        if (!hasShrunkOut)
        {
            stRet = GenCall(codeGen, meth, sts);
            return true;
        }

        using var locShrunk = codeGen.AcquireLocal(typeof(bool));
        var ilw = codeGen.Writer
            .Ldloca(locShrunk)
            .Call(meth);
        stRet = meth.ReturnType;
        return true;
    }
}
