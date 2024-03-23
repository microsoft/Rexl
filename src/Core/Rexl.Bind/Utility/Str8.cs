// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Represents text encoded as utf8 in a byte buffer.
/// </summary>
public sealed class Str8 : IEquatable<Str8>
{
    private readonly byte[] _buf;
    private readonly int _len;

    // This caches the string (when it is requested).
    private volatile string _str;

    /// <summary>
    /// The length of the data in bytes.
    /// </summary>
    public int Length => _len;

    /// <summary>
    /// This takes ownership of the array. The caller should <b>NOT</b> maintain an
    /// alias to this array.
    /// </summary>
    public Str8(byte[] buf, int len)
    {
        Validation.BugCheckValue(buf, nameof(buf));
        Validation.BugCheckIndexInclusive(len, buf.Length, nameof(len));
        _buf = buf;
        _len = len;
    }

    /// <summary>
    /// Encodes the string as UTF8 and wraps the resulting buffer.
    /// </summary>
    public Str8(string value)
    {
        Validation.BugCheckValue(value, nameof(value));
        _str = value;
        _buf = Encoding.UTF8.GetBytes(value);
        _len = _buf.Length;
    }

    /// <summary>
    /// Returns a readonly span over the buffer.
    /// </summary>
    public ReadOnlySpan<byte> GetSpan()
    {
        return _buf.AsSpan(0, _len);
    }

    public Stream GetStream()
    {
        return new MemoryStream(_buf, 0, _len, writable: false);
    }

    /// <summary>
    /// Get a standard string corresponding to this utf8 string.
    /// </summary>
    public string GetString()
    {
        var str = _str;
        if (str == null)
        {
            Interlocked.CompareExchange(ref _str, Encoding.UTF8.GetString(_buf.AsSpan(0, _len)), null);
            str = _str;
            Validation.Assert(str != null);
        }
        return str;
    }

    public static bool operator ==(Str8 a, Str8 b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (ReferenceEquals(a, null))
            return false;
        return a.Equals(b);
    }

    public static bool operator !=(Str8 a, Str8 b)
    {
        return !(a == b);
    }

    public bool Equals(Str8 other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (ReferenceEquals(other, null))
            return false;
        if (other._len != _len)
            return false;
        for (int ib = 0; ib < _len; ib++)
        {
            if (_buf[ib] != other._buf[ib])
                return false;
        }
        return true;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Str8);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_buf.AsSpan(0, _len));
        return hash.ToHashCode();
    }

    /// <summary>
    /// This is to avoid accidentally using <see cref="object.ToString"/> on a <see cref="Str8"/>
    /// value. When needed, <see cref="GetString"/> should be used instead.
    /// </summary>
    new public void ToString()
    {
        Validation.BugCheck(false);
    }
}
