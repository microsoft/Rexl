// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;

using Microsoft.Rexl.IO;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Compression;

/// <summary>
/// The defined compression kinds.
/// WARNING: Since these values are written to files, do NOT change the values. And don't change the
/// underlying type from byte. It is fine to add new values.
/// </summary>
public enum CompressionKind : byte
{
    /// <summary>
    /// This indicates no compression. It absolutely must be zero, since old files wrote zero
    /// for the compression byte indicator. Most code explicitly uses 0 rather than None to mean
    /// no compression. Note that C# has no complaints implicitly converting 0 to an enum value.
    /// </summary>
    None = 0,

    /// <summary>
    /// Snappy compression, blocked for embedded stream scenarios.
    /// </summary>
    Snappy = 1,

    /// <summary>
    /// This includes file header information, which is important for stand alone files, but
    /// not for embedded stream compression.
    /// </summary>
    Gzip = 2,

    /// <summary>
    /// Like Gzip but without the file header information. Better for embedded stream scenarios.
    /// </summary>
    Deflate = 3,

    /// <summary>
    /// Brotli, blocked for embedded stream scenarios.
    /// </summary>
    Brotli = 4,
}

/// <summary>
/// Compression / decompression utilities.
/// </summary>
public static class CodecUtil
{
    /// <summary>
    /// Returns whether <paramref name="kind"/> is a valid <see cref="CompressionKind"/>. That is,
    /// returns false if it is not a defined enum value.
    /// </summary>
    public static bool IsValid(this CompressionKind kind)
    {
        switch (kind)
        {
        case 0:
        case CompressionKind.Snappy:
        case CompressionKind.Gzip:
        case CompressionKind.Deflate:
        case CompressionKind.Brotli:
            return true;

        default:
            return false;
        }
    }

    /// <summary>
    /// Returns true if <paramref name="kind"/> is currently supported.
    /// </summary>
    public static bool IsSupported(this CompressionKind kind)
    {
        switch (kind)
        {
        case CompressionKind.Snappy:
        case CompressionKind.Deflate:
        case CompressionKind.Brotli:
            return true;
        default:
            return false;
        }
    }

    /// <summary>
    /// Returns a name that is presentable to the user.
    /// </summary>
    public static string ToName(this CompressionKind kind)
    {
        Validation.BugCheckParam(kind.IsValid(), nameof(kind));

        switch (kind)
        {
        case 0: return "None";
        case CompressionKind.Snappy: return "Snappy";
        case CompressionKind.Gzip: return "Gzip";
        case CompressionKind.Deflate: return "Deflate";
        case CompressionKind.Brotli: return "Brotli";
        }

        Validation.Assert(false);
        return "<Unnamed>";
    }

    /// <summary>
    /// Try to map the given compression name to a compression kind. Returns false if there
    /// is no match.
    /// </summary>
    public static bool TryGetCompKind(string name, out CompressionKind kind)
    {
        switch (name.ToLowerInvariant())
        {
        case "none":
        case "no":
        case "":
            // No compression.
            kind = 0;
            return true;

        case "snappy":
        case "snappie":
        case "snappier":
            kind = CompressionKind.Snappy;
            return true;

        case "deflate":
            kind = CompressionKind.Deflate;
            return true;

        case "brotli":
        case "brotly":
            kind = CompressionKind.Brotli;
            return true;

        default:
            kind = 0;
            return false;
        }

    }

    /// <summary>
    /// Creates a sub-stream writer for the given <paramref name="kind"/>.
    /// </summary>
    public static Stream CreateWriter(this CompressionKind kind, Stream outer, bool needPosition)
    {
        bool leaveOpen = true;
        switch (kind)
        {
        case CompressionKind.Snappy:
            return SnappyUtil.CreateWriter(outer);

        case CompressionKind.Deflate:
            Stream res = new DeflateStream(outer, CompressionLevel.Optimal, leaveOpen);
            if (needPosition)
                res = new WriteSubStream(res, leaveOpen: false);
            return res;

        case CompressionKind.Brotli:
            return Brotli.CreateWriter(outer);

        case CompressionKind.Gzip:
            throw new NotSupportedException($"Compression {kind} is not yet supported");

        case 0:
            throw Validation.BugExceptParam(nameof(kind), "None is unexpected");

        default:
            throw Validation.BugExceptParam(nameof(kind), $"Unknown compression kind: {(byte)kind}");
        }
    }

    /// <summary>
    /// Creates a reader for the given <paramref name="kind"/> on the given stream from the current position.
    /// </summary>
    public static Stream CreateReader(this CompressionKind kind, Stream outer)
    {
        switch (kind)
        {
        case CompressionKind.Brotli:
            return Brotli.CreateReader(outer);

        case CompressionKind.Snappy:
            return SnappyUtil.CreateReader(outer);

        case CompressionKind.Deflate:
            return new DeflateStream(outer, CompressionMode.Decompress, leaveOpen: true);

        case CompressionKind.Gzip:
            throw new NotSupportedException($"Compression {kind} is not yet supported");

        case 0:
            throw Validation.BugExceptParam(nameof(kind), "None is unexpected");

        default:
            throw Validation.BugExceptParam(nameof(kind), $"Unknown compression kind: {(byte)kind}");
        }
    }

    /// <summary>
    /// Creates a sub-stream reader for the given <paramref name="kind"/> on the given stream, around the
    /// <paramref name="cbOuter"/> number of bytes. If the number of bytes is unknown <paramref name="cbOuter"/>
    /// should be <c>-1</c>. Sets <paramref name="pulled"/> to a delegate that returns the number of bytes
    /// pulled from the outer stream.
    /// </summary>
    public static Stream CreateReader(this CompressionKind kind, Stream outer, long cbOuter,
        out Func<long>? pulled)
    {
        switch (kind)
        {
        case CompressionKind.Brotli:
            {
                var reader = Brotli.CreateReader(outer, cbOuter);
                pulled = reader.GetOuterByteCount;
                return reader;
            }

        case CompressionKind.Snappy:
            {
                var reader = SnappyUtil.CreateReader(outer, cbOuter);
                pulled = reader.GetOuterByteCount;
                return reader;
            }

        case CompressionKind.Deflate:
            {
                bool leaveOpen = true;

                pulled = null;
                if (cbOuter < 0)
                {
                    long pos;
                    try { pos = outer.Position; }
                    catch { pos = -1; }
                    if (pos >= 0)
                        pulled = () => outer.Position - pos;
                }

                ReadSubStream? wrapper = null;
                try
                {
                    if (pulled == null)
                    {
                        wrapper = new ReadSubStream(outer, leaveOpen, cbOuter);
                        pulled = wrapper.GetOuterByteCount;
                        leaveOpen = false;
                    }
                    var reader = new DeflateStream(wrapper ?? outer, CompressionMode.Decompress, leaveOpen);
                    wrapper = null;
                    return reader;
                }
                finally
                {
                    // In case creating the deflate stream throws.
                    wrapper?.Dispose();
                }
            }

        case CompressionKind.Gzip:
            throw new NotSupportedException($"Compression {kind} is not yet supported");

        case 0:
            throw Validation.BugExceptParam(nameof(kind), "None is unexpected");

        default:
            throw Validation.BugExceptParam(nameof(kind), $"Unknown compression kind: {(byte)kind}");
        }
    }
}
