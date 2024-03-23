// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Abstract base class for procedures that read or write data. This helps with arg validation and
/// certification (text vs uri).
/// </summary>
public abstract partial class DataProc : RexlOper
{
    protected readonly DType _typeUri;

    protected DataProc(DName name, DType typeUri, int arityMin, int arityMax)
        : base(isFunc: false, name, arityMin, arityMax)
    {
        Validation.Assert(typeUri.IsUri);
        _typeUri = typeUri;
    }

    new protected void EnsureTextOrUri(Immutable.Array<DType>.Builder types, int slot)
        => RexlOper.EnsureTextOrUri(types, slot, _typeUri);

    new protected bool CertifyTextOrUri(DType type)
        => RexlOper.CertifyTextOrUri(type, _typeUri);
}

public abstract partial class ReadFromStreamProc : DataProc
{
    private protected ReadFromStreamProc(string name, DType typeUri, int arityMin = 1, int arityMax = 1)
        : base(new DName(name), typeUri, arityMin, arityMax)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.Assert(info.Arity == 1);

        var type = info.Args[0].Type;
        EnsureTextOrUri(ref type, _typeUri);
        return (DType.General, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (!CertifyTextOrUri(call.Args[0].Type))
            return false;
        return true;
    }
}

public sealed partial class ReadParquetProc : ReadFromStreamProc
{
    // REVIEW: Should make suppressOpt an argument of the proc. Should also support specifying
    // which columns to read, which rows to read, etc.
    public static readonly ReadParquetProc ReadParquet = new ReadParquetProc("ReadParquet");
    public static readonly ReadParquetProc ReadParquetReq = new ReadParquetProc("ReadParquetReq", suppressOpt: true);

    public bool SuppressOpt { get; }

    private ReadParquetProc(string name, bool suppressOpt = false)
        : base(new DName(name), UriFlavors.UriData)
    {
        this.SuppressOpt = suppressOpt;
    }
}

public sealed partial class ReadRbinProc : ReadFromStreamProc
{
    public static readonly ReadRbinProc ReadRbin = new ReadRbinProc();

    private ReadRbinProc()
        : base(new DName("ReadRbin"), UriFlavors.UriData)
    {
    }
}

public sealed partial class ReadBytesProc : ReadFromStreamProc
{
    public static readonly ReadBytesProc Instance = new ReadBytesProc();

    private ReadBytesProc()
        : base(new DName("ReadBytes"), DType.UriGen)
    {
    }
}

public sealed partial class ReadByteBlocksProc : ReadFromStreamProc
{
    public static readonly ReadByteBlocksProc Instance = new ReadByteBlocksProc();

    private ReadByteBlocksProc()
        : base(new DName("ReadByteBlocks"), DType.UriGen, 1, 2)
    {
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        EnsureTextOrUri(ref type, _typeUri);
        if (info.Arity == 1)
            return (DType.General, Immutable.Array.Create(type));
        return (DType.General, Immutable.Array.Create(type, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var args = call.Args;
        if (!CertifyTextOrUri(args[0].Type))
            return false;
        if (args.Length > 1 && args[1].Type != DType.I8Req)
            return false;
        return true;
    }
}

public sealed partial class ReadTextProc : ReadFromStreamProc
{
    public static readonly ReadTextProc Instance = new ReadTextProc();

    private ReadTextProc()
        : base(new DName("ReadText"), UriFlavors.UriText)
    {
    }
}

public sealed partial class ReadLinesProc : ReadFromStreamProc
{
    public static readonly ReadLinesProc Instance = new ReadLinesProc();

    private ReadLinesProc()
        : base(new DName("ReadLines"), UriFlavors.UriText)
    {
    }
}
