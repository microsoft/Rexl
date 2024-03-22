// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.IO;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Compression;

/// <summary>
/// Implements a reading/decompressing or writing/compressing stream as part of a containing stream.
/// Subclasses implement the actual buffer-level compression/decompression.
/// 
/// Data is written in blocks. If compressing a block doesn't shrink it, the uncompressed data is
/// written. Each block is preceeded by a "code" consisting of one to four bytes containing:
/// * The lowest bit is 1 for compressed and 0 for uncompressed.
/// * The next two bits are one less than the number of bytes in the code (0 to 3).
/// * The remaining bits are the number of (remaining) bytes in the block. Thus the size of a block
///   must be strictly less than 1 << 29, ie, 2^29.
/// 
/// We use a minimum block size of 1K. Other than that, the sub-class specifies the block size.
/// 
/// The blocks are followed by a final 0 byte, indicating no more blocks. This final zero is written
/// when the stream is disposed. Hence, care must be taken to dispose of this stream before any
/// additional information is written to the outer stream.
/// 
/// Subclasses implement <see cref="GetOuterByteCount"/>, returning the number of bytes read from
/// or written to the "outer" containing stream, and <see cref="GetInnerByteCount"/>, returning the
/// number of (uncompressed) bytes read from or written to this stream. Because of compression and
/// blocking, these are not the same. The <see cref="Position"/> property returns the same as
/// <see cref="GetInnerByteCount"/>. Note that the writer's outer byte count will really only be
/// meaningful after the writer is disposed (not just flushed), since before that, the compressed
/// information may not have been flushed or completed.
/// </summary>
public abstract partial class CodecSubStream : Stream
{
    /// <summary>
    /// The base 2 log of the limit buffer size.
    /// </summary>
    private const int k_shfBufLim = 29;

    /// <summary>
    /// This is the limit buffer size, roughly 512 MB. Buffer sizes must be strictly less than this
    /// so that block lengths can be properly encoded by the block prefix "code".
    /// </summary>
    private const int k_cbBufLim = 1 << k_shfBufLim;

    /// <summary>
    /// The stream that this wraps, ie, that this is "embedded in".
    /// </summary>
    private readonly Stream _outer;

    /// <summary>
    /// Buffer and size for compressed bytes.
    /// </summary>
    private byte[] _bufCmp;
    private int _cbBufCmp;

    /// <summary>
    /// Buffer and size for uncompressed bytes.
    /// </summary>
    private byte[] _bufRaw;
    private int _cbBufRaw;

    protected bool IsDisposed => _bufRaw == null;

    private CodecSubStream(Stream outer, int cbCmp, int cbRaw)
    {
        Validation.BugCheckValue(outer, nameof(outer));
        Validation.BugCheckParam(cbCmp < k_cbBufLim, nameof(cbCmp));
        Validation.BugCheckParam(cbRaw < k_cbBufLim, nameof(cbRaw));

        // Use at least 1K.
        _cbBufCmp = Math.Max(1 << 10, cbCmp);
        _cbBufRaw = Math.Max(1 << 10, cbRaw);

        _outer = outer;
        _bufCmp = ArrayPool<byte>.Shared.Rent(_cbBufCmp);
        _bufRaw = ArrayPool<byte>.Shared.Rent(_cbBufRaw);
    }

    public sealed override bool CanSeek => false;

    /// <summary>
    /// This gets the position in uncompressed byte count from where the sub stream
    /// was started. That is, this returns the total number of bytes read/written so far.
    /// </summary>
    public sealed override long Position
    {
        get => GetInnerByteCount();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// The number of (uncompressed) bytes read or written.
    /// </summary>
    public abstract long GetInnerByteCount();

    /// <summary>
    /// Returns the number of (compressed) bytes read from or written to the outer stream.
    /// </summary>
    public abstract long GetOuterByteCount();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // WARNING: we choose to null out these fields when we return the arrays to the
            // shared pool. This risks null reference exceptions, but that is much safer than
            // continuing to use a shared array after returning it.
            var tmp = Interlocked.Exchange(ref _bufRaw, null!);
            if (tmp != null)
                ArrayPool<byte>.Shared.Return(tmp);
            tmp = Interlocked.Exchange(ref _bufCmp, null!);
            if (tmp != null)
                ArrayPool<byte>.Shared.Return(tmp);
        }
        base.Dispose(disposing);
    }

    public sealed override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public sealed override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// An abstract base class for a non-seekable writing/compressing stream as part of a containing stream.
    /// Subclasses implement the actual buffer-level compression.
    /// </summary>
    public abstract class Writer : CodecSubStream
    {
        // The number of bytes we've pushed to the outer stream.
        private long _cbPushed;

        // The number of uncompressed bytes that we've processed and sent
        // to the outer stream.
        private long _cbCommitted;

        // The number of bytes in the uncompressed buffer. These have been written to us
        // but not processed yet. The sum of this and _cbCommitted is the total number of
        // bytes written to us so far.
        private int _cbCur;

        public sealed override bool CanRead => false;

        public sealed override bool CanWrite => true;

        public sealed override long Length => GetInnerByteCount();

        public sealed override long GetInnerByteCount() => _cbCommitted + _cbCur;

        public sealed override long GetOuterByteCount() => _cbPushed;

        protected Writer(Stream outer, int cbCmp, int cbRaw)
            : base(outer, cbCmp, cbRaw)
        {
            Validation.BugCheckParam(_outer.CanWrite, nameof(outer));
            Validation.Assert((cbCmp >> k_shfBufLim) == 0);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && !IsDisposed)
                {
                    if (_cbCur > 0)
                        Send();
                    // Write the termination.
                    _bufRaw[0] = 0;
                    _outer.Write(_bufRaw, 0, 1);
                    _cbPushed += 1;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public sealed override void Flush()
        {
            if (_cbCur > 0)
                Send();
            _outer.Flush();
        }

        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected abstract bool TryCompressBlock(ReadOnlySpan<byte> src, Span<byte> dst, out int cbOut);

        private void Send()
        {
            Validation.Assert(_cbCur > 0);
            Validation.Assert(_cbCur <= _cbBufRaw);

            int cb = _cbCur;
            _cbCommitted += cb;
            _cbCur = 0;

            // Each block is preceeded by a code written in 1 to 4 bytes:
            // * The low bit is 1 for compressed, 0 for uncompressed.
            // * The next two bits are the number of additional bytes used to write the code (0 to 3).
            // * The remaining bits contain the length of the block in bytes (not including the code).
            // A zero code means no more blocks. We'll use one of the buffers for the code byte array.
            byte[] code;

            ReadOnlySpan<byte> bytes;
            ReadOnlySpan<byte> src = _bufRaw.AsSpan(0, cb);
            Span<byte> dst = _bufCmp.AsSpan(0, _cbBufCmp);
            if (TryCompressBlock(src, dst, out int cbOut) && cbOut < src.Length)
            {
                // Write the compressed form.
                bytes = dst.Slice(0, cbOut);
                code = _bufRaw;
                code[0] = 1;
            }
            else
            {
                // Write the raw form.
                bytes = src;
                code = _bufCmp;
                code[0] = 0;
            }

            int cbWrite = bytes.Length;
            Validation.Assert(cbWrite > 0);
            Validation.Assert(cbWrite < k_cbBufLim);

            uint codeLen = (uint)(cbWrite << 3);
            Validation.Assert((codeLen >> 3) == (uint)cbWrite);
            code[0] |= (byte)codeLen;
            code[1] = (byte)(codeLen >> 8);
            code[2] = (byte)(codeLen >> 16);
            code[3] = (byte)(codeLen >> 24);

            int cbCode;
            if (codeLen < 0x100U)
                cbCode = 1;
            else if (codeLen < 0x1_0000U)
                cbCode = 2;
            else if (codeLen < 0x100_0000U)
                cbCode = 3;
            else
                cbCode = 4;
            code[0] |= (byte)((cbCode - 1) << 1);

            // Write the code.
            _outer.Write(code.AsSpan(0, cbCode));
            _cbPushed += cbCode;

            // Now write the bytes.
            _outer.Write(bytes);
            _cbPushed += bytes.Length;
        }

        public sealed override void WriteByte(byte value)
        {
            Validation.Assert(_cbCur < _cbBufRaw);
            _bufRaw[_cbCur] = value;
            if (++_cbCur == _cbBufRaw)
                Send();
        }

        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public sealed override void Write(ReadOnlySpan<byte> src)
        {
            int count = src.Length;
            if (count == 0)
                return;

            Validation.Assert(_cbCur < _cbBufRaw);
            if (count == 1)
            {
                WriteByte(src[0]);
                return;
            }

            var dst = _bufRaw.AsSpan(_cbCur, _cbBufRaw - _cbCur);
            if (count < dst.Length)
            {
                src.CopyTo(dst);
                _cbCur += count;
                return;
            }

            // Write to end of _buf.
            int cb = dst.Length;
            src.Slice(0, cb).CopyTo(dst);
            _cbCur += cb;

            Validation.Assert(_cbCur == _cbBufRaw);
            Send();

            // From here on dst is the full _buf.
            dst = _bufRaw.AsSpan(0, _cbBufRaw);
            int ibSrc = cb;
            while (ibSrc < count)
            {
                cb = count - ibSrc;
                Validation.Assert(cb > 0);
                if (cb < dst.Length)
                {
                    src.Slice(ibSrc).CopyTo(dst);
                    _cbCur += cb;
                    Validation.Assert(_cbCur < _cbBufRaw);
                    return;
                }

                // Write to end of _buf.
                cb = dst.Length;
                src.Slice(ibSrc, cb).CopyTo(dst);
                _cbCur += cb;

                Validation.Assert(_cbCur == _cbBufRaw);
                Send();

                ibSrc += cb;
            }
        }
    }

    /// <summary>
    /// An abstract base class for a non-seekable reading/decompressing stream as part of a containing stream.
    /// Subclasses implement the actual buffer-level decompression.
    /// </summary>
    public abstract class Reader : CodecSubStream
    {
        // The number of valid bytes in the uncompressed buffer.
        private int _cbRaw;

        // The current read position in the uncompressed buffer.
        private int _ibRaw;

        // The total number of uncompressed bytes that we've kicked out of the buffer.
        // _cbProcessed + _ibRaw is the number of bytes that have been read from us so far.
        private long _cbProcessed;

        // When we read a block, we also read the next code byte, so we can accurately
        // track when we are done and, if the client does not try to read past the end,
        // we won't leave an extra byte in the containing stream.
        private byte _codeNext;

        // The total number of bytes from the outer stream that we can read. -1 if unknown.
        private readonly long _cbOuter;
        // The number of bytes we've pulled from outer.
        private long _cbPulled;

        public sealed override bool CanRead => true;

        public sealed override bool CanWrite => false;

        public sealed override long Length => throw new NotSupportedException();

        public sealed override long GetInnerByteCount() => _cbProcessed + _ibRaw;

        public sealed override long GetOuterByteCount() => _cbPulled;

        protected Reader(Stream outer, long cbOuter, int cbCmp, int cbRaw)
            : base(outer, cbCmp, cbRaw)
        {
            Validation.BugCheckParam(_outer.CanRead, nameof(outer));
            Validation.BugCheckParam(cbOuter >= -1, nameof(cbOuter));

            _cbOuter = cbOuter;
            if (_cbOuter == 0)
                _codeNext = 0;
            else
            {
                ReadCore(1, _bufCmp);
                _codeNext = _bufCmp[0];
            }
        }

        public sealed override void Flush()
        {
        }

        public sealed override int ReadByte()
        {
            if (_ibRaw >= _cbRaw)
            {
                if (_codeNext == 0)
                    return -1;
                Fetch();
            }
            Validation.Assert(_ibRaw < _cbRaw);
            return _bufRaw[_ibRaw++];
        }

        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public sealed override int Read(Span<byte> dst)
        {
            int count = dst.Length;
            if (count == 0)
                return 0;

            if (_ibRaw >= _cbRaw)
            {
                if (_codeNext == 0)
                    return 0;
                Fetch();
            }
            Validation.Assert(_ibRaw < _cbRaw);
            int cb = Math.Min(_cbRaw - _ibRaw, count);
            var src = _bufRaw.AsSpan(_ibRaw, cb);
            src.CopyTo(dst);
            _ibRaw += cb;
            return cb;
        }

        private void ReadCore(int cb, byte[] buf)
        {
            if (_cbOuter >= 0 && cb > _cbOuter - _cbPulled)
                throw new IOException("Read past end of compressed info");

            int ib = 0;
            for (; ; )
            {
                int cbLeft = cb - ib;
                int cbRead = _outer.Read(buf, ib, cb - ib);
                Validation.AssertIndexInclusive(cbRead, cbLeft);
                _cbPulled += cbRead;
                if (cbRead >= cbLeft)
                    return;
                if (cbRead <= 0)
                    throw new IOException("Unexpected EOF");
                ib += cbRead;
            }
        }

        /// <summary>
        /// Decompresses a block. Returns the number of bytes written to dst. Throws on failure,
        /// including if there were extra bytes in the source.
        /// </summary>
        protected abstract int DecompressBlock(ReadOnlySpan<byte> src, Span<byte> dst);

        private void Fetch()
        {
            Validation.Assert(_codeNext != 0);
            Validation.Assert(_ibRaw == _cbRaw);

            _cbProcessed += _ibRaw;
            _ibRaw = _cbRaw = 0;

            // The code should already be in _codeNext.
            uint code = _codeNext;
            Validation.Assert(code != 0);

            int cbCode = (int)((code >> 1) & 0x3);
            Validation.Assert(cbCode <= 3);
            if (cbCode > 0)
            {
                ReadCore(cbCode, _bufCmp);
                byte b = _bufCmp[0];
                code |= (uint)b << 8;
                if (cbCode > 1)
                {
                    b = _bufCmp[1];
                    code |= (uint)b << 16;
                    if (cbCode > 2)
                    {
                        b = _bufCmp[2];
                        code |= (uint)b << 24;
                    }
                }

                // The lead byte should not be zero.
                // REVIEW: Should we allow this? It's a poor encoding to waste the byte and serves
                // as a minor validity check, so perhaps best to keep it?
                if (b == 0)
                    throw new IOException("Bad compressed block len");
            }

            int cbBlock = (int)(code >> 3);
            if (cbBlock == 0)
                throw new IOException("Bad compressed block len");

            if ((code & 1) == 0)
            {
                if (cbBlock > _cbBufRaw)
                    throw new IOException("Uncompressed block len too big");
                ReadCore(cbBlock, _bufRaw);
                _cbRaw = cbBlock;
            }
            else
            {
                // Have some compressed bytes. Uncompress them.
                if (cbBlock > _cbBufCmp)
                    throw new IOException("Compressed block len too big");
                ReadCore(cbBlock, _bufCmp);
                var src = _bufCmp.AsSpan(0, cbBlock);
                var dst = _bufRaw.AsSpan(0, _cbBufRaw);

                int cbOut = DecompressBlock(src, dst);
                Validation.AssertIndexInclusive(cbOut, _cbBufRaw);
                if (cbOut <= 0)
                    throw new IOException("Decompressed to zero length");

                _cbRaw = cbOut;
            }

            // Read the next block code.
            ReadCore(1, _bufCmp);
            _codeNext = _bufCmp[0];
        }

        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
