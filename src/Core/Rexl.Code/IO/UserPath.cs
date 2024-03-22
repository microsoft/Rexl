// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.IO;

/// <summary>
/// This plus a normalized user path completely specifies the semantics of "user path".
/// </summary>
[Flags]
public enum PathFlags
{
    None = 0x00,

    /// <summary>
    /// The path is "rooted", meaning that it is absolute and should not be interpreted
    /// as relative to the "current location".
    /// </summary>
    Abs = 0x01,

    /// <summary>
    /// The original path started with the equivalent of "./", meaning that it is should
    /// be interpreted as relative to the "current location". This won't be set if Rooted
    /// is set.
    /// </summary>
    Rel = 0x02,

    /// <summary>
    /// The path is known to specify a directory and not a file.
    /// </summary>
    Dir = 0x04,

    /// <summary>
    /// The path is in the shared file space.
    /// </summary>
    Shr = 0x08,

    /// <summary>
    /// Whether the result starts with any .. segments.
    /// </summary>
    Pop = 0x10,
}

/// <summary>
/// A "user path" specifies a location in "user file" space. "User file" space includes both shared and
/// individual locations. A "user path" is a string divided into segments by / and \ characters.
/// "Normalizing" a user path produces a standardized path string and a <see cref="PathFlags"/> value.
/// These contain the complete semantics of the user path in a unique representation.
/// 
/// A normalized path string:
/// * Uses / to divide segments, not \.
/// * Doesn't start or end with /. The flags indicate whether the path is "rooted" or known to be a "directory".
/// * Doesn't contain any segments consisting of only a dot.
/// * Double dot segments can only come at the beginning, before all non double-dot segments.
/// </summary>
public static class UserPath
{
    /// <summary>
    /// Normalizes the path so that:
    /// * There are no back slash characters.
    /// * There are no doubled slash characters.
    /// * Removes initial slash characters and sets <see cref="PathFlags.Abs"/> if there were any.
    /// * Removes trailing slash characters and sets <see cref="PathFlags.Dir"/> if there were any.
    /// * Removes "." segments. If such is at the end, sets <see cref="PathFlags.Dir"/>. If such is at the
    ///   beginning (not following a slash), sets <see cref="PathFlags.Rel"/>.
    /// * Resolves ".." components by popping a previous component if possible. If such is at the end,
    ///   sets <see cref="PathFlags.Dir"/>. If such is at the beginning (not following a slash), sets
    ///   <see cref="PathFlags.Rel"/>.
    /// * If <paramref name="noShr"/> is <c>false</c> and the first component is `shared`, removes it
    ///   and sets <see cref="PathFlags.Shr"/>.
    /// </summary>
    public static string Normalize(string path, out PathFlags flags, bool noShr = false)
    {
        Validation.BugCheckNonEmpty(path, nameof(path));

        const string shared = "shared";
        const string sharedSlash = "shared/";

        flags = PathFlags.None;

        // This avoids a bunch of memory allocation when not really needed.
        if (!path.Contains('\\', StringComparison.Ordinal) &&
            !path.Contains("/.", StringComparison.Ordinal) &&
            !path.Contains("//", StringComparison.Ordinal))
        {
            if (path.EndsWith('/'))
                flags |= PathFlags.Dir;
            if (path.StartsWith('/'))
                flags |= PathFlags.Abs;
            else if (path.StartsWith("./", StringComparison.Ordinal))
            {
                path = path.Substring(2);
                flags |= PathFlags.Rel;
            }
            else if (path.StartsWith("../", StringComparison.Ordinal))
                flags |= PathFlags.Rel | PathFlags.Pop;
            path = path.Trim('/');

            if (!noShr)
            {
                if (path.StartsWith(sharedSlash, StringComparison.Ordinal))
                {
                    flags |= PathFlags.Shr;
                    path = path.Substring(sharedSlash.Length);
                }
                else if (path == shared)
                {
                    flags |= PathFlags.Shr;
                    return "";
                }
            }

            switch (path)
            {
            case ".":
                flags |= PathFlags.Rel | PathFlags.Dir;
                return "";
            case "..":
                flags |= PathFlags.Rel | PathFlags.Dir | PathFlags.Pop;
                return path;
            }

            return path;
        }

        // This holds the character range of each part.
        // When min < 0, this is a "..". These will only exist at the beginning of the list.
        List<(int min, int len)> parts = new();
        int cch = path.Length;
        int ichBase = 0;
        int ich = 0;
        bool isDir = false;
        for (; ; ich++)
        {
            if (ich < cch && path[ich] != '/' && path[ich] != '\\')
                continue;

            if (ichBase < ich)
            {
                int min = ichBase;
                int len = ich - min;

                // Handle "." (ignore) and ".." (pop if possible).
                if (len == 1 && path[min] == '.')
                {
                    isDir = true;
                    if (min == 0)
                        flags |= PathFlags.Rel;
                }
                else if (len == 2 && path[min] == '.' && path[min + 1] == '.')
                {
                    isDir = true;
                    if (parts.Count > 0 && parts[^1].min >= 0)
                        parts.RemoveAt(parts.Count - 1);
                    else
                    {
                        parts.Add((-1, 2));
                        if (min == 0)
                            flags |= PathFlags.Rel;
                        flags |= PathFlags.Pop;
                    }
                }
                else
                {
                    isDir = false;
                    parts.Add((min, len));
                }
            }

            ichBase = ich + 1;
            if (ich >= cch)
                break;
            if (ich == 0)
                flags |= PathFlags.Abs;
            isDir = true;
        }
        Validation.Assert(ich == cch);
        Validation.Assert(ichBase >= cch);

        if (!noShr && parts.Count > 0)
        {
            var (min, len) = parts[0];
            if (min >= 0)
            {
                var piece = path.AsSpan(min, len);
                if (MemoryExtensions.Equals(shared, piece, StringComparison.Ordinal))
                {
                    flags |= PathFlags.Shr;
                    parts.RemoveAt(0);
                }
            }
        }

        if (isDir)
            flags |= PathFlags.Dir;

        int num = parts.Count;
        switch (num)
        {
        case 0:
            return "";
        case 1:
            var (min, len) = parts[0];
            if (min >= 0)
                return path.Substring(min, len);
            Validation.Assert(len == 2);
            return "..";
        }

        int cchDst = num - 1;
        for (int i = 0; i < num; i++)
            cchDst += parts[i].len;

        var res = string.Create(cchDst, (path, parts), static (dst, state) =>
        {
            var (path2, parts2) = state;
            var src = path2.AsSpan();
            for (int i = 0; i < parts2.Count; i++)
            {
                if (i > 0)
                {
                    dst[0] = '/';
                    dst = dst.Slice(1);
                }
                var (min, len) = parts2[i];
                Validation.Assert(len <= dst.Length);

                if (min >= 0)
                    src.Slice(min, len).CopyTo(dst);
                else
                {
                    Validation.Assert(len == 2);
                    dst[0] = '.';
                    dst[1] = '.';
                }
                dst = dst.Slice(len);
            }
            Validation.Assert(dst.Length == 0);
        });
        return res;
    }

    private static bool IsAbs(string src)
    {
        Validation.AssertNonEmpty(src);
        char ch = src[0];
        bool isAbs = ch == '/' || ch == '\\';
#if DEBUG
        var s = Normalize(src, out var flags, noShr: true);
        Validation.Assert(isAbs == ((flags & PathFlags.Abs) != 0));
#endif
        return isAbs;
    }

    /// <summary>
    /// Normalizes a user path relative to an optional context path. If the context path is in
    /// shared space, the result is forced to be as well. Note that the <see cref="PathFlags.Abs"/>
    /// and <see cref="PathFlags.Rel"/> settings may come from a composition of the two paths,
    /// not just from <paramref name="src"/>.
    /// </summary>
    public static string Normalize(string src, string ctx, out PathFlags flags)
    {
        Validation.BugCheckNonEmpty(src, nameof(src));
        Validation.BugCheckValueOrNull(ctx);

        // If there is no context, just use src.
        if (string.IsNullOrEmpty(ctx) || ctx == "/")
            return Normalize(src, out flags);

        // Normalize ctx so we can compose.
        ctx = Normalize(ctx, out var flagsCtx);
        var ctxShr = flagsCtx & PathFlags.Shr;

        // Prepend with the directory portion of ctx.
        if (ctx.Length > 0 && !IsAbs(src))
        {
            var sep = (flagsCtx & PathFlags.Dir) == 0 ? "/../" : "/";
            src = ctx + sep + src;
        }

        // If the context is shared, force this to be shared.
        var res = Normalize(src, out flags, noShr: ctxShr != 0);
        flags |= ctxShr;
        return res;
    }
}
