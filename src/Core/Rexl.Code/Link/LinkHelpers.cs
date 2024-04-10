// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

namespace Microsoft.Rexl;

/// <summary>
/// Helpers for standard interpretation of links.
/// </summary>
public static class LinkHelpers
{
    /// <summary>
    /// Cache of the method info for <see cref="LinkFromPath(string)"/>, since it is used in code gen.
    /// </summary>
    public static readonly MethodInfo MethLinkFromPath = new Func<string, Link>(LinkFromPath).Method;

    /// <summary>
    /// Readonly array of system type containing just typeof(Link).
    /// </summary>
    public static readonly ReadOnly.Array<Type> StsLink = new[] { typeof(Link) };

    /// <summary>
    /// If <paramref name="link"/> is of kind Generic, returns the <see cref="Link.Path"/>.
    /// Otherwise, returns <c>null</c>.
    /// </summary>
    public static string GetLocalPath(this Link link)
    {
        if (link == null)
            return null;
        if (link.Kind != LinkKind.Generic)
            return null;
        return link.Path;
    }

    /// <summary>
    /// Create a generic link for the given path. If the path is null or just whitespace, returns null.
    /// </summary>
    public static Link LinkFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (path.StartsWith("http://") || path.StartsWith("https://"))
            return Link.CreateHttp(path);
        return Link.CreateGeneric(path);
    }
}
