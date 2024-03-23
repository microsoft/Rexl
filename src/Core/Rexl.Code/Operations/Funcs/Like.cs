// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class LikeGen : RexlOperationGenerator<LikeFunc>
{
    public static readonly LikeGen Instance = new LikeGen();

    private readonly MethodInfo _methIsRecInst;
    private readonly MethodInfo _methTenGetRank;

    private LikeGen()
    {
        _methIsRecInst =
            new Func<RecordBase, Tuple<RecordRuntimeTypeInfo, BitSet>, bool>(IsRecInst).Method;
        _methTenGetRank = typeof(Tensor)
            .GetMethod("get_Rank", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            .VerifyValue();
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);

        Validation.Assert(fn.Kind == LikeFunc.OrKind.Value);

        var val = call.Args[1];
        var typeVal = val.Type;
        Validation.Assert(typeVal == call.Type);
        Validation.Assert(LikeFunc.IsGoodValType(typeVal));

        var typeSrc = call.Args[0].Type;
        Validation.Assert(LikeFunc.WantsTypeTest(typeSrc, typeVal));

        var stDst = codeGen.GetSystemType(typeVal.ToReq());

        // We get the rrti before we spit out any IL.
        RecordRuntimeTypeInfo rrti = null;
        if (typeVal.IsRecordXxx && !codeGen.TypeManager.TryEnsureRrti(typeVal, out rrti))
        {
            stRet = default;
            wrap = default;
            return false;
        }

        int cur = idx + 1;
        var stSrc = codeGen.GenCode(call.Args[0], ref cur);
        Validation.Assert(stSrc.IsClass);

        var ilw = codeGen.Writer;
        if (typeVal.IsRecordXxx)
        {
            Validation.Assert(typeSrc.IsRecordXxx | typeSrc == DType.General);
            Validation.AssertValue(rrti);
            Validation.Assert(typeVal.SameFieldReqs(rrti.TypeReq));

            Label labDone = default;
            Label labBad = default;
            ilw
                .Isinst(stDst)
                .Dup()
                .Brfalse(ref val.IsKnownNull ? ref labDone : ref labBad)
                .Dup();
            codeGen.GenLoadConst(Tuple.Create(rrti, typeVal.GetFieldOpts()));
            ilw
                .Call(_methIsRecInst)
                .Brtrue(ref labDone)
                .MarkLabelIfUsed(labBad)
                .Pop();
            var stTmp = codeGen.GenCode(val, ref cur);
            Validation.Assert(stDst.IsAssignableFrom(stTmp));
            ilw.MarkLabel(labDone);
        }
        else if (typeVal.IsTensorXxx)
        {
            Validation.Assert(stDst.IsGenericType);
            Validation.Assert(stDst.GetGenericTypeDefinition() == typeof(Tensor<>));

            Label labDone = default;
            Label labBad = default;
            ilw
                .Isinst(stDst)
                .Dup()
                .Brfalse(ref val.IsKnownNull ? ref labDone : ref labBad)
                .Dup()
                .Callvirt(_methTenGetRank)
                .Ldc_I4(typeVal.TensorRank)
                .Beq(ref labDone)
                .MarkLabelIfUsed(labBad)
                .Pop();
            var stTmp = codeGen.GenCode(val, ref cur);
            Validation.Assert(stDst.IsAssignableFrom(stTmp));
            ilw.MarkLabel(labDone);
        }
        else if (stDst.IsClass)
        {
            if (val.IsKnownNull)
                ilw.Isinst(stDst);
            else
            {
                Label labDone = default;
                ilw
                    .Isinst(stDst)
                    .Dup()
                    .Brtrue(ref labDone)
                    .Pop();
                var stTmp = codeGen.GenCode(val, ref cur);
                Validation.Assert(stDst.IsAssignableFrom(stTmp));
                ilw.MarkLabel(labDone);
            }
        }
        else
        {
            Validation.Assert(stDst.IsValueType);
            Validation.Assert(!stDst.IsGenericType);

            Label labBad = default;
            Label labDone = default;
            ilw
                .Dup()
                .Isinst(stDst)
                .Brfalse(ref labBad)
                .Unbox_Any(stDst);

            if (typeVal.HasReq)
            {
                // Wrap in Nullable<T>.
                codeGen.TypeManager.GenWrapOpt(codeGen.Generator, typeVal.ToReq(), typeVal);
                stDst = codeGen.GetSystemType(typeVal);
            }

            ilw
                .Br(ref labDone)
                .MarkLabel(labBad)
                .Pop();
            var stTmp = codeGen.GenCode(val, ref cur);
            Validation.Assert(stDst.IsAssignableFrom(stTmp));
            ilw.MarkLabel(labDone);
        }

        stRet = stDst;
        wrap = default;
        return true;
    }

    private static bool IsRecInst(RecordBase rec, Tuple<RecordRuntimeTypeInfo, BitSet> pair)
    {
        Validation.AssertValue(rec);
        Validation.AssertValue(pair);
        Validation.Assert(pair.Item1.RecSysType.IsAssignableFrom(rec.GetType()));

        // This assumes that the rrti is normalized (to a single instance).
        if (rec.Rrti != pair.Item1)
            return false;

        // Check the opt bits. Need the actual to be a subset of the allowed.
        var allow = pair.Item2;
        var nulls = rec.GetNullBits();
        return nulls.IsSubset(allow);
    }
}
