// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Maps from pixels to png file encoding. The input may be either:
/// <list type="bullet">
/// <item>A rank three tensor of U1 with shape (height, width, 4). The values are RGBA channels.</item>
/// <item>A rank two tensor of U4 with shape (height, width). The values are RGBA values encoded as U4
///   with R in the low 8 bits and A in the high 8 bits.</item>
/// </list>
/// The translation currently ignores the A channel, treating all pixels as opaque.
/// The result is a rank-one tensor of U1.
/// Produces <c>null</c> if something goes wrong.
/// </summary>
public sealed partial class PixelsToPngFunc : RexlOper
{
    public static readonly PixelsToPngFunc Instance = new PixelsToPngFunc();

    private PixelsToPngFunc()
        : base(isFunc: true, new DName("PixelsToPng"), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = TensorUtil.InferPixType(info.Args[0].Type);
        return (TensorUtil.TypeBytes.ToOpt(), Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TensorUtil.TypeBytes.ToOpt())
            return false;
        if (!TensorUtil.IsPixTypeReq(call.Args[0].Type))
            return false;
        return true;
    }
}

/// <summary>
/// Maps from encoded image to pixels. The output may be either:
/// <list type="bullet">
/// <item>A rank three tensor of U1 with shape (height, width, 4). The values are RGBA channels.</item>
/// <item>A rank two tensor of U4 with shape (height, width). The values are RGBA values encoded as U4
///   with R in the low 8 bits and A in the high 8 bits.</item>
/// </list>
/// The input is a rank-one tensor of U1.
/// Produces <c>null</c> if something goes wrong.
/// </summary>
public sealed partial class GetPixelsFunc : RexlOper
{
    public static readonly GetPixelsFunc Default = new GetPixelsFunc("GetPixels", useU4: false);
    public static readonly GetPixelsFunc U4 = new GetPixelsFunc("GetPixelsU4", useU4: true);

    public bool UseU4 { get; }

    public DType TypeDst { get; }

    private GetPixelsFunc(string name, bool useU4)
        : base(isFunc: true, new DName(name), 1, 1)
    {
        UseU4 = useU4;
        TypeDst = useU4 ? TensorUtil.TypePixU4.ToOpt() : TensorUtil.TypePixU1.ToOpt();
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (TypeDst, Immutable.Array.Create(TensorUtil.TypeBytes));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeDst)
            return false;
        if (call.Args[0].Type != TensorUtil.TypeBytes)
            return false;
        return true;
    }
}

/// <summary>
/// Resizes a pixel tensor.
/// First arg is the source pixels.
/// Next two args are <c>i8</c> values, <c>h</c> and <c>w</c>.
/// If <c>w</c> is omitted or not positive, <c>h</c> is the size to use for the smaller of the height and width.
/// Otherwise, given values are the target height and width.
/// Produces <c>null</c> if something goes wrong.
/// </summary>
public sealed partial class ResizePixelsFunc : RexlOper
{
    public static readonly ResizePixelsFunc Instance = new ResizePixelsFunc();

    private ResizePixelsFunc()
        : base(isFunc: true, new DName("ResizePixels"), 2, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        var mask = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: mask, maskLiftOpt: mask);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = TensorUtil.InferPixType(info.Args[0].Type);
        if (info.Arity == 2)
            return (type.ToOpt(), Immutable.Array.Create(type, DType.I8Req));
        return (type.ToOpt(), Immutable.Array.Create(type, DType.I8Req, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (!TensorUtil.IsPixTypeOpt(call.Type))
            return false;
        if (call.Args[0].Type != call.Type.ToReq())
            return false;
        if (call.Args[1].Type != DType.I8Req)
            return false;
        if (call.Args.Length > 2 && call.Args[1].Type != DType.I8Req)
            return false;
        return true;
    }
}

/// <summary>
/// Maps from bytes, as rank-one tensor of U1, to a base-64 encoded text value.
/// </summary>
public sealed partial class ToBase64Func : RexlOper
{
    public static readonly ToBase64Func Instance = new ToBase64Func();

    private ToBase64Func()
        : base(isFunc: true, new DName("ToBase64"), 1, 1)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.Text, Immutable.Array.Create(TensorUtil.TypeBytes));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var typeDst = call.Type;
        if (typeDst != DType.Text)
            return false;
        var typeSrc = call.Args[0].Type;
        if (typeSrc != TensorUtil.TypeBytes)
            return false;
        return true;
    }
}

/// <summary>
/// Maps from encoded image to pixels. The output may be either:
/// <list type="bullet">
/// <item>A rank three tensor of U1 with shape (height, width, 4). The values are RGBA channels.</item>
/// <item>A rank two tensor of U4 with shape (height, width). The values are RGBA values encoded as U4
///   with R in the low 8 bits and A in the high 8 bits.</item>
/// </list>
/// The input is a URI or text.
/// </summary>
public sealed partial class ReadPixelsFunc : ReadFromStreamFunc
{
    public static readonly ReadPixelsFunc Default = new ReadPixelsFunc("ReadPixels", useU4: false);
    public static readonly ReadPixelsFunc U4 = new ReadPixelsFunc("ReadPixelsU4", useU4: true);

    public bool UseU4 { get; }
    public DType TypeDst { get; }

    private ReadPixelsFunc(string name, bool useU4)
        : base(new DName(name), UriFlavors.UriImage, 1, 1)
    {
        UseU4 = useU4;
        TypeDst = useU4 ? TensorUtil.TypePixU4.ToOpt() : TensorUtil.TypePixU1.ToOpt();
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var type = info.Args[0].Type;
        EnsureTextOrUri(ref type);
        return (TypeDst, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeDst)
            return false;
        return base.CertifyCore(call, ref full);
    }
}
