// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

public sealed class FloatIsNanGen : RexlOperationGenerator<FloatIsNanFunc>
{
    public static readonly FloatIsNanGen Instance = new FloatIsNanGen();

    private FloatIsNanGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        Validation.Assert(call.Args[0].Type.IsFractionalReq);

        var fn = GetOper(call);

        var ilw = codeGen.Writer;
        ilw
            .Dup()
            .Ceq();

        // _not means "is not nan", which is what we have with the ceq instruction. To get
        // "is nan", we need to invert the bool on the stack.
        if (!fn.IsNot)
        {
            ilw
                .Ldc_I4(0)
                .Ceq();
        }

        stRet = typeof(bool);
        return true;
    }
}

public sealed class FloatBitsGen : RexlOperationGenerator<FloatBitsFunc>
{
    public static readonly FloatBitsGen Instance = new FloatBitsGen();

    private readonly MethodInfo _methR8;
    private readonly MethodInfo _methR4;

    private FloatBitsGen()
    {
        _methR8 = new Func<double, ulong>(NumUtil.ToBits).Method;
        _methR4 = new Func<float, uint>(NumUtil.ToBits).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var type = call.Args[0].Type;
        Validation.Assert(type.IsFractionalReq);
        switch (type.RootKind)
        {
        case DKind.R8:
            // Note that ToBits returns ulong, but we want to treat it as signed.
            GenCall(codeGen, _methR8, sts);
            stRet = typeof(long);
            return true;
        case DKind.R4:
            // Note that ToBits returns uint, but we want to treat it as signed.
            GenCall(codeGen, _methR4, sts);
            stRet = typeof(int);
            return true;
        }

        Validation.Assert(false);
        return base.TryGenCodeCore(codeGen, call, sts, out stRet);
    }
}

public sealed class FloatFromBitsGen : RexlOperationGenerator<FloatFromBitsFunc>
{
    public static readonly FloatFromBitsGen Instance = new FloatFromBitsGen();

    private readonly MethodInfo _methR8;
    private readonly MethodInfo _methR4;
    private readonly ReadOnly.Array<Type> _stsU8;
    private readonly ReadOnly.Array<Type> _stsU4;

    private FloatFromBitsGen()
    {
        _methR8 = new Func<ulong, double>(NumUtil.ToDouble).Method;
        _methR4 = new Func<uint, float>(NumUtil.ToFloat).Method;
        _stsU8 = new[] { typeof(ulong) };
        _stsU4 = new[] { typeof(uint) };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == 1);

        var type = call.Type;
        Validation.Assert(type.IsFractionalReq);
        switch (type.RootKind)
        {
        case DKind.R8:
            // Note that ToDouble takes ulong, but we might have long or ulong. That doesn't matter.
            Validation.Assert(sts[0] == typeof(long) | sts[0] == typeof(ulong));
            stRet = GenCall(codeGen, _methR8, _stsU8);
            return true;
        case DKind.R4:
            // Note that ToBits returns uint, but we want to treat it as signed.
            Validation.Assert(sts[0] == typeof(int) | sts[0] == typeof(uint));
            stRet = GenCall(codeGen, _methR4, _stsU4);
            return true;
        }

        Validation.Assert(false);
        return base.TryGenCodeCore(codeGen, call, sts, out stRet);
    }
}
