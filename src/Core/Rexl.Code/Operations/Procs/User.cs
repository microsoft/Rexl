// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class UserProcGen : RexlOperationGenerator<UserProc.Impl>
{
    public static readonly UserProcGen Instance = new UserProcGen();

    private readonly MethodInfo _meth;

    private UserProcGen()
    {
        _meth = new Func<RecordBase, Tuple<DType>, UserProc, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var proc = GetOper(call);

        var typeWith = call.Args[0].Type;
        Validation.Assert(typeWith.IsRecordReq);
        Validation.Assert(typeWith.FieldCount == proc.Parent.ParamNames.Length);

        codeGen.GenLoadConst(new Tuple<DType>(typeWith));
        codeGen.GenLoadConst(proc.Parent);
        codeGen.GenLoadActionHost();
        codeGen.Writer.Call(_meth);

        stRet = typeof(ActionRunner);
        return true;
    }

    private static ActionRunner Exec(RecordBase with, Tuple<DType> typeWith, UserProc proc, ActionHost host)
    {
        Validation.AssertValue(with);
        Validation.AssertValue(typeWith);
        Validation.AssertValue(proc);
        Validation.AssertValue(host);

        // REVIEW: Is there a better way to have the outer harness/interpreter create an
        // action runner for proc?
        return host.CreateUserProcRunner(proc, typeWith.Item1, with);
    }
}
