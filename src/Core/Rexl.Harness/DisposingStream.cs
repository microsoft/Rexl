// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Wraps a <see cref="Stream"> and an <see cref="IDisposable"/>. All the stream members pass through to the
/// wrapped stream. Dispose handles both the stream and the disposable.
/// </summary>
public sealed class DisposingStream : Stream
{
    private readonly Stream _inner;
    private readonly IDisposable _disp;

    public DisposingStream(Stream inner, IDisposable disp)
    {
        Validation.AssertValue(inner);
        Validation.AssertValue(disp);

        _inner = inner;
        _disp = disp;
    }

    public override IAsyncResult BeginRead(
            byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        _inner.BeginRead(buffer, offset, count, callback, state);

    public override IAsyncResult BeginWrite(
            byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        _inner.BeginWrite(buffer, offset, count, callback, state);

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanTimeout => _inner.CanTimeout;
    public override bool CanWrite => _inner.CanWrite;
    public override void CopyTo(Stream destination, int bufferSize) =>
        _inner.CopyTo(destination, bufferSize);
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
        _inner.CopyToAsync(destination, bufferSize, cancellationToken);
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Close();
            _disp.Dispose();
        }
        // Note that base.Dispose(bool) does nothing, so this isn't necessary.
        // base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        _disp.Dispose();
        // Note that base.Dispose(bool) does nothing and there is no finalizer,
        // so these aren't necessary.
        // base.Dispose(true);
        // GC.SuppressFinalize(this);
    }
    public override int EndRead(IAsyncResult asyncResult) => _inner.EndRead(asyncResult);
    public override void EndWrite(IAsyncResult asyncResult) => _inner.EndWrite(asyncResult);
    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);
    public override Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);
    public override int ReadByte() => _inner.ReadByte();
    public override int ReadTimeout { get => _inner.ReadTimeout; set => _inner.ReadTimeout = value; }
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);
    public override void WriteByte(byte value) => _inner.WriteByte(value);
    public override int WriteTimeout { get => _inner.WriteTimeout; set => _inner.WriteTimeout = value; }

    // These overrides aren't needed since they simply forward to base.
    // To get VS to help identify virtuals that need to be overridden, temporarily change
    // to #if true, then start a line with "override". VS should display members that
    // haven't yet been overridden.
#if false
    public override void Close() => base.Close();
    protected override WaitHandle CreateWaitHandle() => base.CreateWaitHandle();
    public override bool Equals(object? obj) => base.Equals(obj);
    public override int GetHashCode() => base.GetHashCode();
    public override object InitializeLifetimeService() => base.InitializeLifetimeService();
    protected override void ObjectInvariant() => base.ObjectInvariant();
    public override string ToString() => base.ToString();
#endif
}
