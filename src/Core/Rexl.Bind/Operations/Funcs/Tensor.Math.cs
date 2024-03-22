// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using TypeTuple = Immutable.Array<DType>;

/// <summary>
/// Accepts a rank-one (opt) tensor of R4  and produces the same.
/// Performs the standard soft max computation.
/// REVIEW: Should we also support R8?
/// </summary>
public sealed partial class TensorSoftMaxFunc : RexlOper
{
    public static readonly TensorSoftMaxFunc Instance = new TensorSoftMaxFunc();

    private TensorSoftMaxFunc()
        : base(isFunc: true, new DName("SoftMax"), BindUtil.TensorNs, 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (TensorUtil.TypeFloatVector, TypeTuple.Create(TensorUtil.TypeFloatVector));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TensorUtil.TypeFloatVector)
            return false;
        if (call.Args[0].Type != TensorUtil.TypeFloatVector)
            return false;
        return true;
    }
}
