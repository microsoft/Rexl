// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Abstract base class for functions that read data. This helps with arg validation and
/// certification (text vs uri).
/// </summary>
public abstract partial class ReadFromStreamFunc : RexlOper
{
    protected readonly DType _typeUri;

    protected ReadFromStreamFunc(string name, DType typeUri, int arityMin = 1, int arityMax = 1)
        : base(isFunc: true, new DName(name), arityMin, arityMax)
    {
        Validation.Assert(typeUri.IsUri);
        _typeUri = typeUri;
    }

    new protected void EnsureTextOrUri(ref DType type)
        => RexlOper.EnsureTextOrUri(ref type, _typeUri);

    new protected void EnsureTextOrUri(Immutable.Array<DType>.Builder types, int slot)
        => RexlOper.EnsureTextOrUri(types, slot, _typeUri);

    new protected bool CertifyTextOrUri(DType type)
        => RexlOper.CertifyTextOrUri(type, _typeUri);

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (!CertifyTextOrUri(call.Args[0].Type))
            return false;
        return true;
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Read the bytes of a link into a rank-one tensor of byte. If anything goes wrong,
/// returns <c>null</c>. Compare with <see cref="ReadBytesProc"/>.
/// </summary>
public sealed partial class ReadBytesFunc : ReadFromStreamFunc
{
    public static readonly ReadBytesFunc ReadAll = new ReadBytesFunc("ReadAll");

    private ReadBytesFunc(string name)
        : base(name, DType.UriGen)
    {
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        EnsureTextOrUri(ref type);
        return (TensorUtil.TypeBytes.ToOpt(), Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TensorUtil.TypeBytes.ToOpt())
            return false;
        return base.CertifyCore(call, ref full);
    }
}
