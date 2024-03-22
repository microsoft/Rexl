// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Compression;

/// <summary>
/// Implements Brotli compression/decompression as non-seekable write/read sub-streams.
/// The streams support getting <see cref="Position"/>, which is relative to the beginning of the sub-stream.
/// The brotli page size is just under 64K.
/// </summary>
public static class Brotli
{
    // This value is copied from the System.IO.Compression.BrotliStream. It's just shy of 64K.
    private const int k_bufSize = (1 << 16) - 16;

    /// <summary>
    /// Create a Brotli compressing stream within a containing stream. The Brotli stream is non-seekable
    /// but getting <see cref="Stream.Position"/> works and tracks the total number of bytes written to
    /// the Brotli compressing stream (not containing stream).
    /// </summary>
    public static CodecSubStream.Writer CreateWriter(Stream outer)
    {
        Validation.BugCheckValue(outer, nameof(outer));
        Validation.BugCheckParam(outer.CanWrite, nameof(outer));
        return new Writer(outer);
    }

    /// <summary>
    /// Create a Brotli decompressing stream within a containing stream. The Brotli stream is non-seekable
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
            : base(outer, k_bufSize, k_bufSize)
        {
        }

        protected override bool TryCompressBlock(ReadOnlySpan<byte> src, Span<byte> dst, out int cbOut)
        {
            Validation.Assert(src.Length <= k_bufSize);
            Validation.Assert(dst.Length >= k_bufSize);

            // REVIEW: What are the best values to use? Got these from the BrotliStream
            // implementation with "optimal" compression mode.
            using var enc = new BrotliEncoder(quality: 4, window: 22);
            var status = enc.Compress(src, dst, out int cbAte, out cbOut, isFinalBlock: true);
            switch (status)
            {
            default:
                // If this ever happens, we need to modify this code.
                Validation.Assert(false);
                throw new IOException("Brotli compression: unknown status");

            case OperationStatus.InvalidData:
                // REVIEW: Can this ever happen?
                throw new IOException("Brotli compression: invalid data");

            case OperationStatus.NeedMoreData:
                // This should never happen, since we send true for isFinalBlock.
                Validation.Assert(false);
                throw new IOException("Brotli compression: unexpected need more data");

            case OperationStatus.DestinationTooSmall:
                // When this happens, the "compressed" form must be larger, so write it uncompressed.
                return false;

            case OperationStatus.Done:
                Validation.Assert(cbAte == src.Length);
                return true;
            }
        }
    }

    private sealed class Reader : CodecSubStream.Reader
    {
        public Reader(Stream outer, long cb)
            : base(outer, cb, k_bufSize, k_bufSize)
        {
        }

        protected override int DecompressBlock(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            Validation.Assert(src.Length <= k_bufSize);
            Validation.Assert(dst.Length >= k_bufSize);

            using var dec = new BrotliDecoder();
            var status = dec.Decompress(src, dst, out int cbAte, out int cbOut);
            switch (status)
            {
            default:
                // If this ever happens, we need to modify this code.
                Validation.Assert(false);
                throw new IOException("Brotli decompression: unknown status");

            case OperationStatus.InvalidData:
                throw new IOException("Brotli decompression: invalid data");

            case OperationStatus.DestinationTooSmall:
                throw new IOException("Brotli decompression: destination too small");

            case OperationStatus.NeedMoreData:
                // This is likely due to corrupt data in the stream.
                throw new IOException("Brotli decompression: need more");

            case OperationStatus.Done:
                Validation.Assert(cbAte <= src.Length);
                Validation.Assert(cbOut <= dst.Length);
                if (cbAte != src.Length)
                    throw new IOException("Brotli decompression: didn't use all bytes");
                return cbOut;
            }
        }
    }
}
