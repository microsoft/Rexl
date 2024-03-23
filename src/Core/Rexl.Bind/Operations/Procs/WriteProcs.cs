// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

public sealed partial class WriteParquetProc : DataProc
{
    public static readonly WriteParquetProc WriteParquet = new WriteParquetProc();

    private WriteParquetProc()
        : base(new DName("WriteParquet"), UriFlavors.UriData, 2, 2)
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

        // REVIEW: Support opt records somehow?
        var typeRoot = info.Args[0].Type.RootType;
        if (!typeRoot.IsRecordXxx)
            typeRoot = DType.EmptyRecordReq;
        else if (typeRoot.IsOpt)
            typeRoot = typeRoot.ToReq();
        var typeSrc = typeRoot.ToSequence();
        var typeUri = info.Args[1].Type;
        EnsureTextOrUri(ref typeUri, _typeUri);

        return (DType.General, Immutable.Array.Create(typeSrc, typeUri));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var args = call.Args;
        if (!args[0].Type.IsTableXxx)
            return false;
        if (!CertifyTextOrUri(args[1].Type))
            return false;
        return true;
    }
}

/// <summary>
/// Procedure to write rbin. First parameter is the value, second is the link, optional
/// third is whether to chunk at the top level (true for chunked). Optional 4th is
/// whether and how to compress. It can be a bool, i8 or text.
/// </summary>
public sealed partial class WriteRbinProc : DataProc
{
    public static readonly WriteRbinProc WriteRbin = new WriteRbinProc("WriteRbin");

    private WriteRbinProc(string name)
        : base(new DName(name), UriFlavors.UriData, 2, 4)
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

        var types = info.GetArgTypes();
        Validation.Assert(SupportsArity(types.Count));
        if (types[0].HasGeneral)
            info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(0), ErrorStrings.ErrSerializeG));
        EnsureTextOrUri(types, 1, _typeUri);
        if (types.Count > 2)
            types[2] = DType.BitReq;
        if (types.Count > 3)
        {
            // Can be bit, i8, or text.
            var type = types[3];
            if (type.Kind == DKind.Bit)
                types[3] = DType.BitReq;
            else if (type.Kind.IsNumeric() || !type.IsOpt)
                types[3] = DType.I8Req;
            else
                types[3] = DType.Text;
        }

        return (DType.General, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var args = call.Args;
        if (args[0].Type.HasGeneral)
            full = false;
        if (!CertifyTextOrUri(args[1].Type))
            return false;
        if (args.Length > 2 && args[2].Type != DType.BitReq)
            return false;
        if (args.Length > 3)
        {
            var type = args[3].Type;
            if (type.IsSequence)
                return false;
            if (type.HasReq)
                return false;
            switch (type.Kind)
            {
            case DKind.Bit:
            case DKind.I8:
            case DKind.Text:
                break;
            default:
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Procedure to write raw bytes. First parameter is the rank-one tensor of bytes, second is the
/// destination link.
/// </summary>
public sealed partial class WriteBytesProc : DataProc
{
    public static readonly WriteBytesProc Instance = new WriteBytesProc();

    private WriteBytesProc()
        : base(new DName("WriteBytes"), DType.UriGen, 2, 2)
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

        var typeSrc = TensorUtil.TypeBytes;
        if (info.Args[0].Type.IsOpt)
            typeSrc = typeSrc.ToOpt();
        var typeUri = info.Args[1].Type;
        EnsureTextOrUri(ref typeUri, _typeUri);

        return (DType.General, Immutable.Array.Create(typeSrc, typeUri));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var args = call.Args;
        if (args[0].Type.ToReq() != TensorUtil.TypeBytes)
            full = false;
        if (!CertifyTextOrUri(args[1].Type))
            return false;
        return true;
    }
}

/// <summary>
/// Procedure to write a png file from a supported pixel tensor type. First parameter is the pixel tensor,
/// second is the destination link.
/// </summary>
public sealed partial class WritePngProc : DataProc
{
    public static readonly WritePngProc Instance = new WritePngProc();

    private WritePngProc()
        : base(new DName("WritePng"), UriFlavors.UriImage, 2, 2)
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

        var type0 = info.Args[0].Type;
        var typeSrc = TensorUtil.InferPixType(type0);
        if (type0.IsOpt)
            typeSrc = typeSrc.ToOpt();
        var typeUri = info.Args[1].Type;
        EnsureTextOrUri(ref typeUri, _typeUri);

        return (DType.General, Immutable.Array.Create(typeSrc, typeUri));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var args = call.Args;
        if (!TensorUtil.IsPixTypeReq(args[0].Type.ToReq()))
            full = false;
        if (!CertifyTextOrUri(args[1].Type))
            return false;

        // We always reduce.
        full = false;
        return true;
    }

    protected override BndCallNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        // Reduce to WriteBytes(PixelsToPng(src), link).
        // REVIEW: We should support directly translating from pixels to stream.
        // This reduction is just a temporary shortcut.
        var src = call.Args[0];
        if (src.Type.IsOpt)
        {
            var scope = ArgScope.Create(ScopeKind.Guard, src.Type.ToReq());
            var inner = BndCallNode.Create(
                PixelsToPngFunc.Instance, TensorUtil.TypeBytes.ToOpt(),
                Immutable.Array<BoundNode>.Create(BndScopeRefNode.Create(scope)));
            var with = BndCallNode.Create(
                WithFunc.Guard, inner.Type, Immutable.Array<BoundNode>.Create(src, inner),
                Immutable.Array<ArgScope>.Create(scope));
            src = with;
        }
        else
        {
            var inner = BndCallNode.Create(
                PixelsToPngFunc.Instance, TensorUtil.TypeBytes.ToOpt(),
                Immutable.Array<BoundNode>.Create(src));
            src = inner;
        }

        var res = BndCallNode.Create(WriteBytesProc.Instance, DType.General,
            Immutable.Array.Create(src, call.Args[1]));
        return res;
    }
}
