// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class PixelsToPngGen : GetMethGen<PixelsToPngFunc>
{
    public static readonly PixelsToPngGen Instance = new PixelsToPngGen();

    private readonly MethodInfo _methU1;
    private readonly MethodInfo _methU4;

    private PixelsToPngGen()
    {
        _methU1 = new Func<Tensor<byte>, Tensor<byte>>(Exec).Method;
        _methU4 = new Func<Tensor<uint>, Tensor<byte>>(Exec).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var typeSrc = call.Args[0].Type;
        if (typeSrc == TensorUtil.TypePixU1)
            meth = _methU1;
        else
        {
            Validation.Assert(typeSrc == TensorUtil.TypePixU4);
            meth = _methU4;
        }
        return true;
    }

    private static Tensor<byte> Exec(Tensor<byte> src)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 3);

        if (Tensor.TryGetPngFromPixels(src, out var dst))
            return dst;
        return null;
    }

    private static Tensor<byte> Exec(Tensor<uint> src)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 2);

        if (Tensor.TryGetPngFromPixels(src, out var dst))
            return dst;
        return null;
    }
}

public sealed class GetPixelsGen : GetMethGen<GetPixelsFunc>
{
    public static readonly GetPixelsGen Instance = new GetPixelsGen();

    private readonly MethodInfo _methU4;
    private readonly MethodInfo _methU1;

    private GetPixelsGen()
    {
        _methU4 = new Func<Tensor<byte>, Tensor<uint>>(ExecU4).Method;
        _methU1 = new Func<Tensor<byte>, Tensor<byte>>(Exec).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);
        Validation.Assert(call.Type == fn.TypeDst);

        meth = fn.UseU4 ? _methU4 : _methU1;
        return true;
    }

    private static Tensor<byte> Exec(Tensor<byte> src)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 1);

        if (Tensor.TryDecodePixels(src, out var dst))
            return dst;
        return null;
    }

    private static Tensor<uint> ExecU4(Tensor<byte> src)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 1);

        if (Tensor.TryDecodePixelsU4(src, out var dst))
            return dst;
        return null;
    }
}

public sealed class ResizePixelsGen : RexlOperationGenerator<ResizePixelsFunc>
{
    public static readonly ResizePixelsGen Instance = new ResizePixelsGen();

    private readonly MethodInfo _methU1;
    private readonly MethodInfo _methU4;
    private readonly ReadOnly.Array<Type> _stsU1;
    private readonly ReadOnly.Array<Type> _stsU4;

    private ResizePixelsGen()
    {
        _methU1 = new Func<Tensor<byte>, long, long, Tensor<byte>>(Exec).Method;
        _methU4 = new Func<Tensor<uint>, long, long, Tensor<uint>>(Exec).Method;
        _stsU1 = new[] { typeof(Tensor<byte>), typeof(long), typeof(long) };
        _stsU4 = new[] { typeof(Tensor<uint>), typeof(long), typeof(long) };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var typeSrc = call.Args[0].Type;
        MethodInfo meth;
        ReadOnly.Array<Type> stsUse;
        if (typeSrc == TensorUtil.TypePixU1)
        {
            meth = _methU1;
            stsUse = _stsU1;
        }
        else
        {
            Validation.Assert(typeSrc == TensorUtil.TypePixU4);
            meth = _methU4;
            stsUse = _stsU4;
        }

        Validation.Assert(sts[0] == stsUse[0]);
        Validation.Assert(sts[1] == stsUse[1]);
        if (call.Args.Length < 3)
            codeGen.Writer.Ldc_I8(0);
        else
            Validation.Assert(sts[2] == stsUse[2]);

        stRet = GenCall(codeGen, meth, stsUse);
        return true;
    }

    private static Tensor<byte> Exec(Tensor<byte> src, long dy, long dx)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 3);

        if (Tensor.TryResizePixels(src, dy, dx, out var dst))
            return dst;
        return null;
    }

    private static Tensor<uint> Exec(Tensor<uint> src, long dy, long dx)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 2);

        if (Tensor.TryResizePixels(src, dy, dx, out var dst))
            return dst;
        return null;
    }
}

public sealed class ToBase64Gen : GetMethGen<ToBase64Func>
{
    public static readonly ToBase64Gen Instance = new ToBase64Gen();

    private readonly MethodInfo _meth;

    private ToBase64Gen()
    {
        _meth = new Func<Tensor<byte>, string>(Exec).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        meth = _meth;
        return true;
    }

    private static string Exec(Tensor<byte> src)
    {
        Validation.AssertValue(src);
        Validation.Assert(src.Rank == 1);

        if (Tensor.TryGetBase64(src, out var res))
            return res;
        return null;
    }
}

public sealed class ReadPixelsGen : ReadFromStreamGen<ReadPixelsFunc>
{
    public static readonly ReadPixelsGen Instance = new ReadPixelsGen();

    private readonly MethodInfo _methU4;
    private readonly MethodInfo _methU1;

    private ReadPixelsGen()
    {
        _methU4 = new Func<Link, ExecCtx, int, Tensor<uint>>(ExecU4).Method;
        _methU1 = new Func<Link, ExecCtx, int, Tensor<byte>>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);
        Validation.Assert(call.Type == fn.TypeDst);

        var ilw = codeGen.Writer;

        // Convert string to Link.
        var typeSrc = call.Args[0].Type;
        if (typeSrc.Kind == DKind.Text)
        {
            Validation.Assert(sts[0] == typeof(string));
            ilw.Call(LinkHelpers.MethLinkFromPath);
        }
        else
        {
            Validation.Assert(typeSrc.Kind == DKind.Uri);
            Validation.Assert(sts[0] == typeof(Link));
        }

        var meth = fn.UseU4 ? _methU4 : _methU1;
        stRet = GenCallCtxId(codeGen, meth, LinkHelpers.StsLink, call);
        return true;
    }

    private static Tensor<byte> Exec(Link link, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(link);
        Validation.AssertValue(ctx);

        try
        {
            using var stream = LoadStream(link, ctx, id);
            if (stream is null)
                return null;

            if (Tensor.TryDecodePixels(stream, out var dst))
                return dst;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Tensor<uint> ExecU4(Link link, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(link);
        Validation.AssertValue(ctx);

        try
        {
            using var stream = LoadStream(link, ctx, id);
            if (stream is null)
                return null;

            if (Tensor.TryDecodePixelsU4(stream, out var dst))
                return dst;
            return null;
        }
        catch
        {
            return null;
        }
    }
}
