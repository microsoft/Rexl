// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class IsNullGen : RexlOperationGenerator<IsNullFunc>
{
    public static readonly IsNullGen Instance = new IsNullGen();

    private IsNullGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        // We inline this. The arg should be on the execution stack.
        DType type = call.Args[0].Type;
        codeGen.TypeManager.GenIsNull(codeGen.Generator, type);
        stRet = typeof(bool);
        return true;
    }
}

public sealed class IsEmptyGen : RexlOperationGenerator<IsEmptyFunc>
{
    public static readonly IsEmptyGen Instance = new IsEmptyGen();

    public MethodInfo MethIsEmptyEnum { get; }

    private IsEmptyGen()
    {
        MethIsEmptyEnum = new Func<IEnumerable<object>, bool>(IsEmptyEnum)
            .Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
            out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;
        DType type = call.Args[0].Type;
        if (type == DType.Text)
        {
            using (var loc = codeGen.AcquireLocal(typeof(string)))
            {
                Label labDone = default;

                ilw
                    .Dup()
                    .Stloc(loc)
                    .Ldnull()
                    .Ceq()
                    .Dup()
                    .Brtrue(ref labDone)
                    .Ldloc(loc)
                    .Call(typeof(string).GetProperty("Length", Type.EmptyTypes).GetGetMethod())
                    .Ceq()
                    .MarkLabel(labDone);
            }
            stRet = typeof(bool);
            return true;
        }

        if (type.SeqCount == 0)
        {
            Validation.Assert(false, "Shouldn't get here");
            stRet = null;
            return false;
        }

        // Call the IsEmptyEnum method.
        // REVIEW: Should we inline?
        Type stItem = codeGen.GetSystemType(type.ItemTypeOrThis);
        var meth = MethIsEmptyEnum.MakeGenericMethod(stItem);
        stRet = GenCall(codeGen, meth, sts);
        return true;
    }

    public static bool IsEmptyEnum<T>(IEnumerable<T> seq)
    {
        if (seq == null)
            return true;
        if (seq is IList<T> list)
            return list.Count == 0;
        using var e = seq.GetEnumerator();
        return !e.MoveNext();
    }
}
