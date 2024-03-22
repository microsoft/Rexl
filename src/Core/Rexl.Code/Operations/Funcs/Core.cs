// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed partial class ForEachGen : RexlOperationGenerator<ForEachFunc>
{
    public static readonly ForEachGen Instance = new ForEachGen();

    private readonly Immutable.Array<MethodInfo> _cseqToMeths;
    private readonly Immutable.Array<MethodInfo> _cseqToMethsIndexed;
    private readonly Immutable.Array<MethodInfo> _cseqToIfMeths;
    private readonly Immutable.Array<MethodInfo> _cseqToIfMethsIndexed;
    private readonly Immutable.Array<MethodInfo> _cseqToWhileMeths;
    private readonly Immutable.Array<MethodInfo> _cseqToWhileMethsIndexed;

    private ForEachGen()
    {
        _cseqToMeths = GetExecs();
        _cseqToMethsIndexed = GetExecInds();
        _cseqToIfMeths = GetExecIfs();
        _cseqToIfMethsIndexed = GetExecIfInds();
        _cseqToWhileMeths = GetExecWhiles();
        _cseqToWhileMethsIndexed = GetExecWhileInds();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        int carg = call.Args.Length;
        int cseq = call.Scopes.Length;

        // Get the destination and source system types.
        DType typeItemDst = call.Args[carg - 1].Type;
        Type stItemDst = codeGen.GetSystemType(typeItemDst);

        bool hasVolatile = call.Args[cseq].HasVolatile;
        bool isIndexed = call.Indices[0] != null;
        Immutable.Array<MethodInfo> cseqToMeth;
        var kind = ForEachFunc.GetFilterKind(call);
        switch (kind)
        {
        case SeqFilterKind.If:
            cseqToMeth = isIndexed ? _cseqToIfMethsIndexed : _cseqToIfMeths;
            hasVolatile |= call.Args[cseq + 1].HasVolatile;
            break;
        case SeqFilterKind.While:
            cseqToMeth = isIndexed ? _cseqToWhileMethsIndexed : _cseqToWhileMeths;
            hasVolatile |= call.Args[cseq + 1].HasVolatile;
            break;
        default:
            Validation.Assert(kind == SeqFilterKind.None);
            cseqToMeth = isIndexed ? _cseqToMethsIndexed : _cseqToMeths;
            break;
        }
        if (cseq >= cseqToMeth.Length)
        {
            stRet = null;
            wrap = default;
            return false;
        }

        var meth = cseqToMeth[cseq];
        Validation.AssertValue(meth);

        var stsItem = new Type[cseq + 1];
        for (int i = 0; i < cseq; i++)
        {
            DType typeSeq = call.Args[i].Type;
            DType typeItemSrc = typeSeq.ItemTypeOrThis;
            stsItem[i] = codeGen.GetSystemType(typeItemSrc);
        }
        stsItem[cseq] = stItemDst;
        meth = meth.MakeGenericMethod(stsItem);
        Validation.Assert(meth.ReturnType == codeGen.GetSystemType(call.Type));

        stRet = NeedsExecCtxCore(call) ?
            GenCallCtxId(codeGen, meth, sts, call) :
            GenCall(codeGen, meth, sts);
        wrap = hasVolatile ? SeqWrapKind.MustCache : default;
        return true;
    }
}
