// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class CastGuidGen : RexlOperationGenerator<CastGuidFunc>
{
    public static CastGuidGen Instance = new CastGuidGen();

    private readonly MethodInfo _meth;

    private CastGuidGen()
    {
        _meth = new Func<string, Guid>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(codeGen.GetSystemType(call.Type) == typeof(Guid));

        var ilw = codeGen.Writer;
        if (call.Args.Length == 0)
        {
            codeGen.GenDefValType(typeof(Guid));
            stRet = typeof(Guid);
        }
        else
            stRet = GenCall(codeGen, _meth, sts);
        return true;
    }

    public static Guid Exec(string arg)
    {
        if (Guid.TryParse(arg, out var guid))
            return guid;
        return Guid.Empty;
    }
}

public sealed class ToGuidGen : RexlOperationGenerator<ToGuidFunc>
{
    public static ToGuidGen Instance = new ToGuidGen();

    private readonly MethodInfo _meth;

    private ToGuidGen()
    {
        _meth = new Func<string, Guid?>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        stRet = GenCall(codeGen, _meth, sts);
        return true;
    }

    public static Guid? Exec(string arg)
    {
        if (Guid.TryParse(arg, out var guid))
            return guid;
        return null;
    }
}

public sealed class MakeGuidGen : RexlOperationGenerator<MakeGuidFunc>
{
    public static MakeGuidGen Instance = new MakeGuidGen();

    private readonly MethodInfo _meth;

    private MakeGuidGen()
    {
        _meth = new Func<ExecCtx, int, Guid>(Exec).Method;
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return true;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == 0);

        stRet = GenCallCtxId(codeGen, _meth, sts, call);
        return true;
    }

    private static Guid Exec(ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        return ctx.MakeGuid(id);
    }
}
