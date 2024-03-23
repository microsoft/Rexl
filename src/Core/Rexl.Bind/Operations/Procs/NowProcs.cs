// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

public sealed partial class NowProc : RexlOper
{
    public static readonly NowProc NowUtc = new NowProc("NowUtc", NowKind.Utc);
    public static readonly NowProc NowLocal = new NowProc("NowLocal", NowKind.Local);
    public static readonly NowProc NowOffset = new NowProc("NowOffset", NowKind.Offset);

    public NowKind Kind { get; }

    private NowProc(string name, NowKind kind)
        : base(isFunc: false, new DName(name), 0, 0)
    {
        Kind = kind;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 0);

        return (DType.General, Immutable.Array<DType>.Empty);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        return true;
    }

    /// <summary>
    /// The kind of the proc, which only determines which published value is the
    /// primary result.
    /// The order of these is the same as the order of the published results, which
    /// are shared by all proc variations.
    /// </summary>
    public enum NowKind : byte
    {
        Utc,
        Local,
        Offset,
    }
}
