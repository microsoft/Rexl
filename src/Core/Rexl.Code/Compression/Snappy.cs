// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

using Microsoft.Rexl.Private;

using Snappier;

namespace Microsoft.Rexl.Compression;

/// <summary>
/// Implements Snappy compression/decompression as non-seekable write/read sub-streams.
/// The streams support getting <see cref="Position"/>, which is relative to the beginning of the sub-stream.
/// The brotli page size is just under 64K.
/// </summary>
public static class SnappyUtil
{
    /// <summary>
    /// The max raw buffer size.
    /// REVIEW: This was copied from Brotli. What should Snappy use?
    /// </summary>
    private const int k_cbMaxRaw = (1 << 16) - 16;

    /// <summary>
    /// The max compressed length is computed. We use this (even though it's bigger than the raw size)
    /// to avoid dealing with exceptions.
    /// </summary>
    private static readonly int s_cbMaxCmp = Snappy.GetMaxCompressedLength(k_cbMaxRaw);

    /// <summary>
    /// Create a Snappy compressing stream within a containing stream. The Snappy stream is non-seekable
    /// but getting <see cref="Stream.Position"/> works and tracks the total number of bytes written to
    /// the Snappy compressing stream (not containing stream).
    /// </summary>
    public static CodecSubStream.Writer CreateWriter(Stream outer)
    {
        Validation.BugCheckValue(outer, nameof(outer));
        Validation.BugCheckParam(outer.CanWrite, nameof(outer));
        return new Writer(outer);
    }

    /// <summary>
    /// Create a Snappy decompressing stream within a containing stream. The Brotli stream is non-seekable
    /// but getting <see cref="Stream.Position"/> works and tracks the total number of bytes read from
    /// the Brotli decompressing stream (not containing stream). The <paramref name="cb"/> value should
    /// be the number of bytes in the Brotli stream, if known. Use <c>-1</c> if it is unknown.
    /// </summary>
    public static CodecSubStream.Reader CreateReader(Stream outer, long cb = -1)
    {
        Validation.BugCheckValue(outer, nameof(outer));
        Validation.BugCheckParam(outer.CanRead, nameof(outer));
        Validation.BugCheckParam(cb >= -1, nameof(cb));
        return new Reader(outer, cb);
    }

    private sealed class Writer : CodecSubStream.Writer
    {
        public Writer(Stream outer)
            : base(outer, s_cbMaxCmp, k_cbMaxRaw)
        {
        }

        protected override bool TryCompressBlock(ReadOnlySpan<byte> src, Span<byte> dst, out int cbOut)
        {
            Validation.Assert(src.Length <= k_cbMaxRaw);
            Validation.Assert(dst.Length >= s_cbMaxCmp);

            cbOut = Snappy.Compress(src, dst);
            return true;
        }
    }

    private sealed class Reader : CodecSubStream.Reader
    {
        public Reader(Stream outer, long cb)
            : base(outer, cb, s_cbMaxCmp, k_cbMaxRaw)
        {
        }

        protected override int DecompressBlock(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            Validation.Assert(src.Length <= s_cbMaxCmp);
            Validation.Assert(dst.Length >= k_cbMaxRaw);

            return Snappy.Decompress(src, dst);
        }
    }
}
