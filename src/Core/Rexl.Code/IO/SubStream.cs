// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.IO;

/// <summary>
/// This implements a non-seekable read stream as part of a containing stream. It has two main purpoes,
/// to ensure that code reading the "sub-stream" does not read past the end (when the size of the sub-stream
/// is known, and to be able to report how many bytes have been read from the containing stream. This amount
/// is returned by the <see cref="GetOuterByteCount"/> method as well as by the <see cref="Position"/> property.
/// </summary>
public sealed partial class ReadSubStream : Stream
{
    /// <summary>
    /// The stream that this wraps, ie, that this is "embedded in".
    /// </summary>
    private readonly Stream _outer;

    private readonly bool _leaveOpen;

    /// <summary>
    /// For reading a single byte.
    /// </summary>
    private readonly byte[] _one;

    /// <summary>
    /// The logical size of this sub-stream. This is -1 if it isn't known.
    /// </summary>
    private readonly long _size;

    /// <summary>
    /// The number of bytes read.
    /// </summary>
    private long _cbRead;

    public sealed override bool CanSeek => false;

    public sealed override bool CanRead => true;

    public sealed override bool CanWrite => false;

    public sealed override bool CanTimeout => _outer.CanTimeout;

    public sealed override long Length => _size >= 0 ? _size : throw new NotSupportedException();

    public sealed override long Position
    {
        get => _cbRead;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Returns the number of bytes read from the outer stream.
    /// </summary>
    public long GetOuterByteCount() => _cbRead;

    /// <summary>
    /// The provided <paramref name="size"/> may be <c>-1</c>, indicating that the size is unknown.
    /// </summary>
    public ReadSubStream(Stream outer, bool leaveOpen, long size)
    {
        Validation.BugCheckValue(outer, nameof(outer));
        Validation.BugCheckParam(outer.CanRead, nameof(outer));
        Validation.BugCheckParam(size >= -1, nameof(size));

        _outer = outer;
        _leaveOpen = leaveOpen;
        _size = size;

        // REVIEW: Once we switch to .Net 7, we can use new Span<byte>(ref local) and won't need
        // this silly array.
        _one = new byte[1];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
            _outer.Dispose();
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

    public sealed override int ReadByte()
    {
        Validation.Assert(_cbRead <= _size | _size < 0);

        if (_size == _cbRead)
            return -1;
        int cb = _outer.Read(_one, 0, 1);
        if (cb == 0)
            return -1;
        Validation.Assert(cb == 1);
        _cbRead++;
        return _one[0];
    }

    public sealed override int Read(Span<byte> dst)
    {
        if (dst.Length == 0)
            return 0;

        long cbLeft;
        if (_size >= 0 && dst.Length > (cbLeft = _size - _cbRead))
            dst = dst.Slice(0, (int)cbLeft);
        int cb = _outer.Read(dst);
        Validation.AssertIndexInclusive(cb, dst.Length);
        _cbRead += cb;
        return cb;
    }

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        Validation.BugCheckValue(buffer, nameof(buffer));
        Validation.BugCheckIndexInclusive(count, buffer.Length, nameof(count));
        Validation.BugCheckIndexInclusive(offset, buffer.Length - count, nameof(offset));

        long cbLeft;
        if (_size >= 0 && count > (cbLeft = _size - _cbRead))
            count = (int)cbLeft;
        int cb = _outer.Read(buffer, offset, count);
        Validation.AssertIndexInclusive(cb, count);
        _cbRead += cb;
        return cb;
    }

    public sealed override void Flush()
    {
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Implements a non-seekable writing stream as part of a containing stream.
/// This supports getting the <see cref="Position"/>, which is relative to the beginning of this substream.
/// The <see cref="Length"/> property is also available and returns the same as <see cref="Position"/>.
/// </summary>
public sealed partial class WriteSubStream : Stream
{
    /// <summary>
    /// The stream that this wraps, ie, that this is "embedded in".
    /// </summary>
    private readonly Stream _outer;

    private readonly bool _leaveOpen;

    /// <summary>
    /// For writing a single byte.
    /// </summary>
    private readonly byte[] _one;

    /// <summary>
    /// The number of bytes written.
    /// </summary>
    private long _cb;

    public sealed override bool CanSeek => false;

    public sealed override bool CanRead => false;

    public sealed override bool CanWrite => true;

    public sealed override bool CanTimeout => _outer.CanTimeout;

    public sealed override long Length => _cb;

    public sealed override long Position
    {
        get => _cb;
        set => throw new NotSupportedException();
    }

    public WriteSubStream(Stream outer, bool leaveOpen)
    {
        Validation.BugCheckValue(outer, nameof(outer));
        Validation.BugCheckParam(outer.CanWrite, nameof(outer));

        _outer = outer;
        _leaveOpen = leaveOpen;

        // REVIEW: Once we switch to .Net 7, we can use new Span<byte>(ref local) and won't need
        // this silly array.
        _one = new byte[1];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
            _outer.Dispose();
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

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public sealed override void Flush()
    {
        _outer.Flush();
    }

    public override void WriteByte(byte value)
    {
        _one[0] = value;
        _outer.Write(_one, 0, 1);
        _cb++;
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));

        _outer.Write(buffer, offset, count);
        _cb += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _outer.Write(buffer);
        _cb += buffer.Length;
    }
}
