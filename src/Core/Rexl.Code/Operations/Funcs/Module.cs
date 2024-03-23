// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class ModuleOptimizeGen : RexlOperationGenerator<ModuleOptimizeFunc>
{
    public static readonly ModuleOptimizeGen Instance = new ModuleOptimizeGen();

    private readonly MethodInfo _meth;

    private ModuleOptimizeGen()
    {
        _meth = new Func<RuntimeModule<RecordBase>, string, bool, string, ExecCtx, int, RuntimeModule<RecordBase>>(Exec)
            .Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var func = GetOper(call);
        Validation.Assert(func.Kind == ModuleOptimizeFunc.OptKind.Opt);

        var typeMod = call.Args[0].Type;
        Validation.Assert(typeMod.IsModuleReq);
        var typeRec = typeMod.ModuleToRecord();
        var stRec = codeGen.GetSystemType(typeRec);

        var meth = _meth.MakeGenericMethod(stRec);

        var ilw = codeGen.Writer;
        int carg = call.Args.Length;

        // This doesn't support solver options yet.
        Validation.Assert(carg == 3 | carg == 4);

        // Load null/default for the solver if it wasn't specified.
        stRet = carg < 4 ?
            GenCallDefaultCtxId(codeGen, meth, sts, DType.Text, call) :
            GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    public static RuntimeModule<TRec> Exec<TRec>(
            RuntimeModule<TRec> src, string measure, bool isMax, string solver,
            ExecCtx ctx, int id)
        where TRec : RecordBase
    {
        Validation.AssertValue(src);
        Validation.Assert(DName.IsValidDName(measure));
        Validation.Assert(solver is null || DName.IsValidDName(solver));
        Validation.AssertValue(ctx);

        // REVIEW: Going through the exec ctx is a temporary hack.
        var res = ctx.Optimize(id, src, new DName(measure), isMax, solver is null ? default : new DName(solver));
        if (res is null)
            return null;

        Validation.Assert(res is RuntimeModule<TRec>);
        return (RuntimeModule<TRec>)res;
    }
}
