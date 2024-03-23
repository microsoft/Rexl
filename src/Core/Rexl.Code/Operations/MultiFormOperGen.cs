// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

public abstract partial class MultiFormOperGen<TOper, TExec, TCookie> : RexlOperationGenerator<TOper, TExec>
    where TOper : MultiFormOper<TCookie>
    where TExec : MultiFormOper<TCookie>.ExecutionOper
{
    private readonly MethodInfo _methMerge;
    private readonly MethodInfo _methMergeOneMany;
    private readonly MethodInfo _methCreateTup;

    private readonly MethodInfo _methGenCreateRunner;
    private readonly MethodInfo _methGenCreateRunnerMerge;

    protected MultiFormOperGen()
    {
        _methMerge =
            new Func<IEnumerable<object>, IEnumerable<object>, Func<object, object, object>, object,
                IEnumerable<object>>(MultiFormFuncHelper.Merge)
            .Method.GetGenericMethodDefinition();
        _methMergeOneMany =
            new Func<FlatteningSequence<object>, IEnumerable<object>, Func<object, object, object>,
                object, bool, IEnumerable<object>>(MultiFormFuncHelper.MergeOneMany)
            .Method.GetGenericMethodDefinition();
        _methCreateTup = new Func<object, object, (object, object)>(ValueTuple.Create)
            .Method.GetGenericMethodDefinition();

        // Note that these are not static methods. This does virtual resolution.
        _methGenCreateRunner = new Action<ICodeGen, TExec>(GenCreateRunner<object, object>)
            .Method.GetGenericMethodDefinition();
        _methGenCreateRunnerMerge = new Action<ICodeGen, TExec>(GenCreateRunner<object, object, object, object>)
            .Method.GetGenericMethodDefinition();
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return true;
    }

    /// <summary>
    /// Generate code for the core function "G"/"H".
    /// For a procedure, this will be saved as a Delegate and passed into the <see cref="ActionRunner"/>
    /// produced by <see cref="MultiFormOper{TCookie}.GenCreateRunner{TIn,TSrc,TDst}(ICodeGen)"/>.
    /// The stack will contain the input as TIn already loaded.
    /// The ActionRunner is available for loading as an argument in slot 1 (the first argument).
    /// </summary>
    protected abstract void GenCoreCode(ICodeGen codeGen, TExec exec, DType typeIn, DType typeOut, Type stIn, Type stOut);

    protected sealed override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        wrap = SeqWrapKind.DontWrap;

        var exec = GetChild(call);

        // For a proc, we need wrapping code to generate the action runner. For a func, we don't need
        // any of that.
        if (exec.IsFunc)
            return TryGenExec(exec, codeGen, call, sts, asTup: false, out stRet);
        return TryGenForProc(exec, codeGen, call, sts, out stRet);
    }

    /// <summary>
    /// Generate code for a procedure incovation. The generates the core "exec" logic into a delegate
    /// that is passed into an action runner creation method.
    /// </summary>
    protected bool TryGenForProc(TExec exec, ICodeGen codeGen,
        BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(exec);
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(call.Oper == exec);
        Validation.Assert(call.Type == exec.TypeCall);
        Validation.Assert(call.Args.Length == exec.Arity);

        // sts consists of:
        // (Non merging case) 1 item, the type of the input.
        // (Merging case) 3 items:
        // 1. The type of the source (as a with scope)
        // 2. The type of the input
        // 3. The type of the source
        // 
        // The stack consists of
        // (Non merging case) The input
        // (Merging case)
        // 1. The input
        // 2. The source
        //
        // Note that in the merging case, the first argument is not available for use
        // on the stack because it is defining the scope, hence the need for the third arg.
        Validation.Assert(call.Scopes.Length == 0 || call.Scopes.Length == 1 && call.Scopes[0].Kind == ScopeKind.With);
        Validation.Assert(exec.Merges == (call.Scopes.Length == 1));

        DType typeIn = exec.TypeIn;
        DType typeOut = exec.TypeOut;
        DType typeSrc = exec.TypeSrc;
        DType typeDst = exec.TypeDst;
        Type stSrc = null;

        // Create the system type array for the inner delegate that wraps the "exec" and merging code.
        Type[] stsParams;
        if (exec.Merges)
        {
            Validation.Assert(sts.Length == 3);
            // sts[0] is null since it's a 'with' scope.
            Validation.Assert(sts[0] is null);
            stsParams = new Type[3];
            stsParams[1] = sts[1];
            stsParams[2] = sts[2];
        }
        else
        {
            Validation.Assert(sts.Length == 1);
            stsParams = new Type[2];
            stsParams[1] = sts[0];
        }
        stsParams[0] = typeof(ActionRunner);

        Validation.Assert(stsParams.Length == 2 || stsParams.Length == 3);

        var typeManager = codeGen.TypeManager;
        if (!typeManager.TryEnsureSysType(typeIn, out Type stIn) ||
            !typeManager.TryEnsureSysType(typeOut, out Type stOut) ||
            !typeManager.TryEnsureSysType(typeDst, out Type stDst) ||
            (exec.Merges && !typeManager.TryEnsureSysType(typeSrc, out stSrc)))
        {
            stRet = null;
            return false;
        }

        stRet = typeof(ActionRunner);
        var stRetCore = stDst;
        if (exec.Merges)
            stRetCore = typeof(ValueTuple<,>).MakeGenericType(stDst, stOut);
        var cookie = codeGen.StartFunction("Core", stRetCore, stsParams);
        {
            // The ActionRunner is available via Ldarg(1) and not pushed on the stack
            // in the code generated by Base.TryGenCore.
            for (int i = 1; i < stsParams.Length; i++)
                codeGen.Writer.Ldarg(i + 1);
            if (!TryGenExec(exec, codeGen, call, sts, asTup: exec.Merges, out var stRetCore1))
            {
                codeGen.EndFunction(cookie);
                stRet = null;
                return false;
            }
            Validation.Assert(stRetCore1 == stRetCore);
        }
        var (stCore, methCore) = codeGen.EndFunction(cookie);

        codeGen.GenLoadConst(methCore, stCore);
        codeGen.GenLoadConst(exec);
        codeGen.GenLoadActionHost();
        codeGen.GenLoadExecCtx();

        MethodInfo act;
        if (exec.Merges)
            act = _methGenCreateRunnerMerge.MakeGenericMethod(stIn, stSrc, stDst, stOut);
        else
            act = _methGenCreateRunner.MakeGenericMethod(stIn, stDst);
        act.Invoke(this, new object[] { codeGen, exec });
        return true;
    }

    /// <summary>
    /// Generate code to create the <see cref="ActionRunner"/> for the 1 arg (no src) execution.
    /// The stack will contain in order:
    /// 1. The input as TIn
    /// 2. Core func as Func<ActionRunner, TIn, TDst>
    /// 3. ExecutionProc
    /// 4. ActionHost
    /// 5. ExecCtx
    /// </summary>
    protected abstract void GenCreateRunner<TIn, TDst>(ICodeGen codeGen, TExec exec);

    /// <summary>
    /// Generate code to create the <see cref="ActionRunner"/> for 2 arg (with src) execution.
    /// The stack will contain in order:
    /// 1. The input as TIn
    /// 2. The src as TSrc
    /// 3. Core func as Func<ActionRunner, TIn, TSrc, (TDst, TOut)>
    /// 4. ExecutionProc
    /// 5. ActionHost
    /// 6. ExecCtx
    /// </summary>
    /// <param name="codeGen"></param>
    protected abstract void GenCreateRunner<TIn, TSrc, TDst, TOut>(ICodeGen codeGen, TExec exec);
}
