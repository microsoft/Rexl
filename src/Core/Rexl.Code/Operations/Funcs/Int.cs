// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using Integer = System.Numerics.BigInteger;

public sealed class IntHexGen : RexlOperationGenerator<IntHexFunc>
{
    public static readonly IntHexGen Instance = new IntHexGen();

    private readonly MethodInfo _methIA;
    private readonly MethodInfo _methBit;
    private readonly MethodInfo _methI8;
    private readonly ReadOnly.Array<Type> _stsI8;

    private IntHexGen()
    {
        _methIA = new Func<Integer, string>(IntHexFunc.Exec).Method;
        _methBit = new Func<bool, string>(IntHexFunc.Exec).Method;
        _methI8 = new Func<long, int, string>(IntHexFunc.Exec).Method;
        _stsI8 = new[] { typeof(long), typeof(int) };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var type = call.Args[0].Type;
        Validation.Assert(type.IsIntegralReq);
        var kind = type.RootKind;

        var ilw = codeGen.Writer;
        MethodInfo meth;
        switch (kind)
        {
        case DKind.IA: meth = _methIA; break;
        case DKind.Bit: meth = _methBit; break;

        default:
            int cb = kind.NumericSize();
            Validation.Assert(1 <= cb & cb <= 8 & (cb & (cb - 1)) == 0);
            if (cb < 8)
                ilw.Conv_U8();
            ilw.Ldc_I4(2 * cb);
            meth = _methI8;
            sts = _stsI8;
            break;
        }

        stRet = GenCall(codeGen, meth, sts);
        return true;
    }
}
