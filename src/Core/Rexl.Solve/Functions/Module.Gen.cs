// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
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

        if (!ctx.TryGetSink(out var sink))
        {
            ctx.Log(id, "Optimization not supported");
            return null;
        }

        if (!ctx.TryGetCodeGen(out var codeGen))
        {
            ctx.Log(id, "Optimization not supported");
            return null;
        }

        src.Bnd.NameToIndex.TryGetValue(new DName(measure), out int imsr).Verify();
        Validation.AssertIndex(imsr, src.Bnd.Symbols.Length);
        Validation.Assert(src.Bnd.Symbols[imsr].IsMeasureSym);

        if (!Solve.MipSolver.TryOptimize(sink, codeGen, isMax, src, imsr,
                solver is null ? default : new DName(solver), out var score, out var symValues))
        {
            return null;
        }

#if false
        // REVIEW: This should be code-gened somewhere and passed in as a Func<List<...>, TRec>.
        var typeRec = src.Bnd.TypeRec;
        var fact = codeGen.TypeManager.CreateRecordFactory(typeRec);
        var bldr = fact.Create().Open(partial: true);
        var names = new HashSet<DName>();
        foreach (var pair in symValues)
        {
            names.Add(pair.name).Verify();
            if (pair.value is null)
                continue;
            var setter = fact.GetFieldSetter(pair.name, out _, out var stFld);
            Validation.Assert(stFld.IsAssignableFrom(pair.value.GetType()));
            setter(bldr, pair.value);
        }
        var rec = bldr.Close();
        Validation.Assert(rec is TRec);

        var modDst = src.Update((TRec)rec, names);
        Validation.Assert(modDst is RuntimeModule<TRec>);
        return (RuntimeModule<TRec>)modDst;
#else
        return src.Update(symValues);
#endif
    }
}
