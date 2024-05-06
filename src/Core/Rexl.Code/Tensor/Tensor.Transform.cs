// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;

using SkiaSharp;

namespace Microsoft.Rexl;

partial class Tensor
{
    /// <summary>
    /// Get a (read only) span with values from the tensor. If needed, this copies the tensor values
    /// into a new buffer.
    /// </summary>
    public static bool TryGetSpan<T>(Tensor<T> ten, out ReadOnlySpan<T> span, bool canAlloc = true)
    {
        Validation.AssertValue(ten);

        if (TryGetMemory<T>(ten, out var mem, canAlloc))
        {
            span = mem.Span;
            return true;
        }

        span = default;
        return false;
    }

    /// <summary>
    /// Get a (read only) memory with values from the tensor. If needed, this copies the tensor values
    /// into a new buffer.
    /// </summary>
    public static bool TryGetMemory<T>(Tensor<T> ten, out ReadOnlyMemory<T> mem, bool canAlloc = true)
    {
        Validation.AssertValue(ten);

        int count = (int)ten._count;
        if (count != ten._count)
        {
            // Too big.
            mem = default;
            return false;
        }
        if (count <= 0)
        {
            // Empty.
            mem = default;
            return true;
        }

        T[] items;
        if (ten._regular && ten._buf.TryGetMemory(out var buf))
        {
            Validation.Assert(ten._root < buf.Length);
            Validation.Assert(ten._delta <= (buf.Length - ten._root) / count);
            if (count == 1 || ten._delta == 1)
            {
                mem = buf.Slice((int)ten._root, count);
                return true;
            }

            if (!canAlloc)
            {
                mem = default;
                return false;
            }

            items = new T[count];
            int delta = (int)ten._delta;
            int src = (int)ten._root;
            var span = buf.Span;
            for (int dst = 0; dst < count; dst++, src += delta)
                items[dst] = span[src];
        }
        else
        {
            if (!canAlloc)
            {
                mem = default;
                return false;
            }

            items = new T[count];
            int dst = 0;
            foreach (var v in ten.GetValues())
                items[dst++] = v;
            Validation.Assert(dst == count);
        }

        Validation.Assert(items.Length == count);
        mem = items;
        return true;
    }

    /// <summary>
    /// Convert the tensor bytes to a base64 encoding, represented as a string.
    /// </summary>
    public static bool TryGetBase64(Tensor<byte> src, out string res)
    {
        // REVIEW: Could pass false for canAlloc and if it fails, iterate the values
        // directly rather than copying the values.
        if (Tensor.TryGetSpan(src, out var buf))
        {
            res = Convert.ToBase64String(buf);
            return true;
        }

        res = null;
        return false;
    }

    /// <summary>
    /// Try to encode the pixels, represented as one of the pixel tensor types, as a png, represented
    /// as a rank one tensor.
    /// This currently assumes the alpha channel is irrelevant.
    /// </summary>
    public static bool TryGetPngFromPixels(Tensor src, out Tensor<byte> dst)
    {
        Validation.AssertValueOrNull(src);

        if (src is Tensor<byte> tenU1)
            return TryGetPngFromPixels(tenU1, out dst);
        if (src is Tensor<uint> tenU4)
            return TryGetPngFromPixels(tenU4, out dst);
        dst = null;
        return false;
    }

    /// <summary>
    /// Try to encode the pixels, represented as a rank three tensor of shape (height, width, 4), as a png,
    /// represented as a rank one tensor.
    /// This currently assumes the alpha channel is irrelevant.
    /// </summary>
    public static bool TryGetPngFromPixels(Tensor<byte> src, out Tensor<byte> dst)
    {
        dst = null;
        if (src is null)
            return false;

        // REVIEW: Perhaps support other representations such as gray scale,
        // different bit depths, etc?
        if (src.Rank != 3)
            return false;

        var shape = src.Shape;
        Validation.Assert(shape.Rank == 3);

        if (shape[2] != 4)
            return false;

        int height = (int)shape[0];
        if (height != shape[0])
            return false;
        if (height <= 0)
            return false;
        int width = (int)shape[1];
        if (width != shape[1])
            return false;
        if (width <= 0)
            return false;

        if (!Tensor.TryGetSpan(src, out var buf))
            return false;

        // REVIEW: Should some of this code be shared with other overloads? It's tricky to do that
        // since we don't really want to keep the source buffer fixed beyond the encode operation.
        // Also, we'll likely add support for other color types.

        // REVIEW: alpha or no? Make it an option?
        var info = new SKImageInfo(width, height, SKColorType.Rgb888x);
        // var info = new SKImageInfo(width, height, SKColorType.Rgba8888);

        // REVIEW: Perhaps implement an SKWStream that wraps (and grows as needed)
        // a byte array, then use that byte array in the Buffer<byte>? If we don't care about
        // the extra space at the end, this would save a copy.
        SKData data;
        unsafe
        {
            fixed (byte* p = buf)
            {
                using var pm = new SKPixmap(info, (IntPtr)p);
                data = pm.Encode(SKPngEncoderOptions.Default);
            }
        }

        if (data is null)
            return false;

        using (data)
        {
            var span = data.AsSpan();
            dst = Tensor<byte>.CreateFrom(span, span.Length);
        }

        Validation.Assert(dst is not null);
        return true;
    }

    /// <summary>
    /// Try to encode the pixels, represented as a rank two tensor of shape (height, width), as a png,
    /// represented as a rank one tensor. The uint values are assumed to be RGBA with R as the low 8 bits
    /// and A as the high 8 bits.
    /// This currently assumes the alpha channel is irrelevant.
    /// </summary>
    public static bool TryGetPngFromPixels(Tensor<uint> src, out Tensor<byte> dst)
    {
        dst = null;
        if (src is null)
            return false;

        // REVIEW: Perhaps support other representations such as gray scale,
        // different bit depths, etc?
        if (src.Rank != 2)
            return false;

        var shape = src.Shape;
        Validation.Assert(shape.Rank == 2);

        int height = (int)shape[0];
        if (height != shape[0])
            return false;
        if (height <= 0)
            return false;
        int width = (int)shape[1];
        if (width != shape[1])
            return false;
        if (width <= 0)
            return false;

        if (!Tensor.TryGetSpan(src, out var buf))
            return false;

        // REVIEW: alpha or no? Make it an option?
        var info = new SKImageInfo(width, height, SKColorType.Rgb888x);
        // var info = new SKImageInfo(width, height, SKColorType.Rgba8888);

        SKData data;
        unsafe
        {
            fixed (uint* p = buf)
            {
                using var pm = new SKPixmap(info, (IntPtr)p);
                data = pm.Encode(SKPngEncoderOptions.Default);
            }
        }

        if (data is null)
            return false;

        using (data)
        {
            var span = data.AsSpan();
            dst = Tensor<byte>.CreateFrom(span, span.Length);
        }

        Validation.Assert(dst is not null);
        return true;
    }

    /// <summary>
    /// Try to decode an image, represented as a rank one Tensor{byte}, to pixels, represented as a
    /// rank three Tensor{byte} of shape (height, width, 4).
    /// </summary>
    public static bool TryDecodePixels(Tensor<byte> src, out Tensor<byte> dst)
    {
        dst = null;
        if (src is null)
            return false;

        if (src.Rank != 1)
            return false;

        if (!Tensor.TryGetSpan(src, out var buf))
            return false;
        if (buf.Length == 0)
            return false;

        int height;
        int width;
        byte[] pixels;
        unsafe
        {
            fixed (byte* psrc = buf)
            {
                using var data = SKData.Create((IntPtr)psrc, buf.Length);
                if (data is null)
                    return false;
                using var codec = SKCodec.Create(data);
                if (codec is null)
                    return false;

                var infoSrc = codec.Info;
                height = infoSrc.Height;
                if (height <= 0)
                    return false;
                width = infoSrc.Width;
                if (width <= 0)
                    return false;

                var infoDst = infoSrc.WithColorType(SKColorType.Rgba8888);
                long size = (long)height * width * 4;
                if (size != infoDst.BytesSize)
                    return false;

                pixels = new byte[size];
                SKCodecResult res;
                fixed (byte* pdst = pixels)
                {
                    res = codec.GetPixels(infoDst, (IntPtr)pdst);
                }

                if (res != SKCodecResult.Success)
                    return false;
            }
        }

        dst = Tensor<byte>._CreateRaw(
            Shape.Create(height, width, 4), Shape.Create(4 * width, 4, 1),
            pixels, 0);
        return true;
    }

    /// <summary>
    /// Try to decode an image, from a stream, to pixels, represented as a
    /// rank three Tensor{byte} of shape (height, width, 4).
    /// </summary>
    public static bool TryDecodePixels(Stream stream, out Tensor<byte> dst)
    {
        if (stream is null)
        {
            dst = null;
            return false;
        }

        // REVIEW: Implement this by directly decoding from the stream.
        var ten = ReadAllBytes(stream);
        return TryDecodePixels(ten, out dst);
    }

    /// <summary>
    /// Try to decode an image, represented as a rank one Tensor{byte}, to pixels, represented as a
    /// rank two Tensor{uint} of shape (height, width).
    /// </summary>
    public static bool TryDecodePixelsU4(Tensor<byte> src, out Tensor<uint> dst)
    {
        dst = null;
        if (src is null)
            return false;

        if (src.Rank != 1)
            return false;

        if (!Tensor.TryGetSpan(src, out var buf))
            return false;
        if (buf.Length == 0)
            return false;

        int height;
        int width;
        uint[] pixels;
        unsafe
        {
            fixed (byte* psrc = buf)
            {
                using var data = SKData.Create((IntPtr)psrc, buf.Length);
                if (data is null)
                    return false;
                using var codec = SKCodec.Create(data);
                if (codec is null)
                    return false;

                var infoSrc = codec.Info;
                height = infoSrc.Height;
                if (height <= 0)
                    return false;
                width = infoSrc.Width;
                if (width <= 0)
                    return false;

                var infoDst = infoSrc.WithColorType(SKColorType.Rgba8888);
                long size = (long)height * width * 4;
                if (size != infoDst.BytesSize)
                    return false;

                pixels = new uint[size / 4];
                SKCodecResult res;
                fixed (uint* pdst = pixels)
                {
                    res = codec.GetPixels(infoDst, (IntPtr)pdst);
                }

                if (res != SKCodecResult.Success)
                    return false;
            }
        }

        dst = Tensor<uint>._CreateRaw(Shape.Create(height, width), Shape.Create(width, 1), pixels, 0);
        return true;
    }

    /// <summary>
    /// Try to decode an image, from a stream, to pixels, represented as a
    /// rank three Tensor{byte} of shape (height, width, 4).
    /// </summary>
    public static bool TryDecodePixelsU4(Stream stream, out Tensor<uint> dst)
    {
        if (stream is null)
        {
            dst = null;
            return false;
        }

        // REVIEW: Implement this by directly decoding from the stream.
        var ten = ReadAllBytes(stream);
        return TryDecodePixelsU4(ten, out dst);
    }

    /// <summary>
    /// We're resizing a pixel tensor with the given shape. This tweaks the destination height and width
    /// (if needed) and does all the necessary tests.
    /// </summary>
    private static bool TryResolveResize(Shape shape, ref long dy, ref long dx, out int dySrc, out int dxSrc)
    {
        dySrc = (int)shape[0];
        dxSrc = (int)shape[1];
        if (dySrc != shape[0])
            return false;
        if (dySrc <= 0)
            return false;
        if (dxSrc != shape[1])
            return false;
        if (dxSrc <= 0)
            return false;

        if (dy <= 0 || dy != (int)dy)
            return false;
        if (dx != (int)dx)
            return false;

        // If dx <= 0, we're doing isotropic scaling with the min dimension being dy.
        if (dx <= 0)
        {
            // Isotropic with min dimension dy.
            int dsNew = (int)dy;
            if (dySrc < dxSrc)
            {
                long dxNew = ((long)dxSrc * dsNew + (dySrc >> 1)) / dySrc;
                Validation.Assert(dxNew >= dsNew);
                if (dxNew > int.MaxValue)
                    return false;
                dy = (int)dsNew;
                dx = (int)dxNew;
            }
            else if (dySrc > dxSrc)
            {
                long dyNew = ((long)dySrc * dsNew + (dxSrc >> 1)) / dxSrc;
                Validation.Assert(dyNew >= dsNew);
                if (dyNew > int.MaxValue)
                    return false;
                dy = (int)dyNew;
                dx = (int)dsNew;
            }
            else
                dy = dx = dsNew;
        }
        return true;
    }

    /// <summary>
    /// Try to resize the pixels, represented as a rank three tensor of shape (height, width, 4).
    /// If dx <= 0, an isotropic scaling is performed making the smaller dimension be dy.
    /// </summary>
    public static bool TryResizePixels(Tensor<byte> src, long dy, long dx, out Tensor<byte> dst)
    {
        dst = null;
        if (src is null)
            return false;

        // REVIEW: Perhaps support other representations such as gray scale,
        // different bit depths, etc?
        if (src.Rank != 3)
            return false;

        var shape = src.Shape;
        Validation.Assert(shape.Rank == 3);

        // REVIEW: Fix this to handle the no alpha case (3 channels).
        if (shape[2] != 4)
            return false;

        if (!TryResolveResize(shape, ref dy, ref dx, out int dySrc, out int dxSrc))
            return false;

        if (dySrc == dy && dxSrc == dx)
        {
            // Already the requested size.
            dst = src;
            return true;
        }

        if (!Tensor.TryGetSpan(src, out var bufSrc))
            return false;

        var infoSrc = new SKImageInfo(dxSrc, dySrc, SKColorType.Rgba8888);
        var infoDst = new SKImageInfo((int)dx, (int)dy, SKColorType.Rgba8888);
        var bufDst = new byte[dy * dx * 4];

        unsafe
        {
            fixed (byte* ps = bufSrc)
            fixed (byte* pd = bufDst)
            {
                using var pmSrc = new SKPixmap(infoSrc, (IntPtr)ps);
                using var pmDst = new SKPixmap(infoDst, (IntPtr)pd);
                if (!pmSrc.ScalePixels(pmDst, SKFilterQuality.High))
                    return false;
            }
        }

        dst = Tensor<byte>._CreateRaw(
            Shape.Create(dy, dx, 4), Shape.Create(4 * dx, 4, 1),
            bufDst, 0);
        return true;
    }

    /// <summary>
    /// Try to resize the pixels, represented as a rank two tensor of shape (height, width).
    /// If dx < 0, an isotropic scaling is performed making the smaller dimension be dy.
    /// </summary>
    public static bool TryResizePixels(Tensor<uint> src, long dy, long dx, out Tensor<uint> dst)
    {
        dst = null;
        if (src is null)
            return false;

        if (src.Rank != 2)
            return false;

        var shape = src.Shape;
        Validation.Assert(shape.Rank == 2);

        if (!TryResolveResize(shape, ref dy, ref dx, out int dySrc, out int dxSrc))
            return false;

        if (dySrc == dy && dxSrc == dx)
        {
            // Already the requested size.
            dst = src;
            return true;
        }

        if (!Tensor.TryGetSpan(src, out var bufSrc))
            return false;

        var infoSrc = new SKImageInfo(dxSrc, dySrc, SKColorType.Rgba8888);
        var infoDst = new SKImageInfo((int)dx, (int)dy, SKColorType.Rgba8888);
        var bufDst = new uint[dy * dx];

        unsafe
        {
            fixed (uint* ps = bufSrc)
            fixed (uint* pd = bufDst)
            {
                using var pmSrc = new SKPixmap(infoSrc, (IntPtr)ps);
                using var pmDst = new SKPixmap(infoDst, (IntPtr)pd);
                if (!pmSrc.ScalePixels(pmDst, SKFilterQuality.High))
                    return false;
            }
        }

        dst = Tensor<uint>._CreateRaw(
            Shape.Create(dy, dx), Shape.Create(dx, 1),
            bufDst, 0);
        return true;
    }

    /// <summary>
    /// Read all bytes from the stream into a rank-one tensor of byte.
    /// </summary>
    public static Tensor<byte> ReadAllBytes(Stream stream)
    {
        Validation.AssertValue(stream);

        // REVIEW: There are several improvements that are possible for this code. Eg,
        // if the stream length is knownable, can pre-allocated the buffer and read all at once;
        // if not, use an array pool for buf; etc.

        const int cbPage = 4096;
        var buf = new byte[cbPage];
        List<byte[]> bufs = null;
        int ib = 0;
        for (; ; )
        {
            Validation.AssertIndexInclusive(ib, buf.Length);
            if (ib >= buf.Length)
            {
                Util.Add(ref bufs, buf);
                buf = new byte[cbPage];
                ib = 0;
            }
            int cb = stream.Read(buf, ib, buf.Length - ib);
            if (cb == 0)
                break;
            ib += cb;
        }

        Validation.AssertIndexInclusive(ib, buf.Length);
        Tensor.Buffer<byte> buffer;
        if (bufs == null)
        {
            if (ib == 0)
                buf = Array.Empty<byte>();
            else
                Array.Resize(ref buf, ib);
            buffer = buf;
        }
        else
        {
            long cb = bufs.Count * (long)cbPage + ib;
            long ibDst = 0;
            var res = new byte[cb];
            foreach (var item in bufs)
            {
                Validation.Assert(item.Length == cbPage);
                Array.Copy(item, 0, res, ibDst, cbPage);
                ibDst += cbPage;
            }
            if (ib > 0)
            {
                Array.Copy(buf, 0, res, ibDst, ib);
                ibDst += ib;
            }
            Validation.Assert(ibDst == cb);
            buffer = res;
        }
        return Tensor<byte>._CreateRaw(Shape.Create(buffer.Length), Shape.One1, buffer, 0);
    }

    /// <summary>
    /// Read all bytes from the stream into a rank-one tensor of byte.
    /// </summary>
    public static async Task<Tensor<byte>> ReadAllBytesAsync(Stream stream)
    {
        Validation.AssertValue(stream);

        // REVIEW: There are several improvements that are possible for this code. Eg,
        // if the stream length is knownable, can pre-allocated the buffer and read all at once;
        // if not, use an array pool for buf; etc.

        const int cbPage = 4096;
        var buf = new byte[cbPage];
        List<byte[]> bufs = null;
        int ib = 0;
        for (; ; )
        {
            Validation.AssertIndexInclusive(ib, buf.Length);
            if (ib >= buf.Length)
            {
                Util.Add(ref bufs, buf);
                buf = new byte[cbPage];
                ib = 0;
            }
            int cb = await stream.ReadAsync(buf, ib, buf.Length - ib).ConfigureAwait(false);
            if (cb == 0)
                break;
            ib += cb;
        }

        Validation.AssertIndexInclusive(ib, buf.Length);
        Tensor.Buffer<byte> buffer;
        if (bufs == null)
        {
            if (ib == 0)
                buf = Array.Empty<byte>();
            else
                Array.Resize(ref buf, ib);
            buffer = buf;
        }
        else
        {
            long cb = bufs.Count * (long)cbPage + ib;
            long ibDst = 0;
            var res = new byte[cb];
            foreach (var item in bufs)
            {
                Validation.Assert(item.Length == cbPage);
                Array.Copy(item, 0, res, ibDst, cbPage);
                ibDst += cbPage;
            }
            if (ib > 0)
            {
                Array.Copy(buf, 0, res, ibDst, ib);
                ibDst += ib;
            }
            Validation.Assert(ibDst == cb);
            buffer = res;
        }
        return Tensor<byte>._CreateRaw(Shape.Create(buffer.Length), Shape.One1, buffer, 0);
    }

    /// <summary>
    /// Compute the soft max of a score vector.
    /// </summary>
    public static Tensor<float> SoftMax(Tensor<float> input)
    {
        if (input is null)
            return null;

        Validation.BugCheckParam(input.Rank == 1, nameof(input));
        Validation.Assert(input._regular);
        var shape = input.Shape;
        long size = shape[0];
        if (size <= 0)
            return input;

        long delta = input._delta;
        if (delta == 0)
        {
            var v = input._buf[input._root];
            if (float.IsNegativeInfinity(v))
                v = float.NaN;
            else if (!float.IsNaN(v))
                v = (float)(1.0 / size);
            return Tensor<float>.CreateFill(v, shape);

        }
        Validation.Assert(size > 1);

        var src = input._buf;
        var buf = new float[size];
        long cinf = 0;
        long ivSrc = input._root;
        float max = float.NegativeInfinity;
        for (long iv = 0; iv < size; iv++, ivSrc += delta)
        {
            var val = src[ivSrc];
            if (float.IsNaN(val))
                return Tensor<float>.CreateFill(val, shape);
            if (max < val)
                max = val;
            if (float.IsPositiveInfinity(val))
                cinf++;
            buf[iv] = val;
        }

        if (float.IsNegativeInfinity(max))
        {
            // All negative infinity.
            Validation.Assert(cinf == 0);
            return Tensor<float>.CreateFill(float.NaN, shape);
        }

        if (cinf > 0)
        {
            float v = (float)(1.0 / cinf);
            if (cinf == size)
                return Tensor<float>.CreateFill(v, shape);
            for (long iv = 0; iv < size; iv++, ivSrc += delta)
            {
                if (float.IsPositiveInfinity(buf[iv]))
                    buf[iv] = v;
                else
                    buf[iv] = 0;
            }
        }
        else
        {
            double sum = 0.0;
            for (long iv = 0; iv < size; iv++, ivSrc += delta)
            {
                var e = Math.Exp(buf[iv] - max);
                Validation.Assert(e >= 0);
                Validation.Assert(e <= 1);
                buf[iv] = (float)e;
                sum += e;
            }
            for (long iv = 0; iv < size; iv++, ivSrc += delta)
                buf[iv] = (float)(buf[iv] / sum);
        }

        return Tensor<float>._CreateRaw(shape, Shape.One1, buf, 0);
    }
}
