// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// The system type representing values with Uri DType. Conceptually this "points to" a
/// "file" or "information". There are several variants of this. See the <see cref="LinkKind"/>
/// enum for information.
/// </summary>
public sealed class Link : IEquatable<Link>
{
    /// <summary>
    /// Gets the kind of link - http, azure blob, azure data lake, etc.
    /// </summary>
    public LinkKind Kind { get; }

    /// <summary>
    /// Gets the source account id, if there is one.
    /// </summary>
    public string AccountId { get; }

    /// <summary>
    /// Gets the source path of file.
    /// </summary>
    public string Path { get; }

    public (LinkKind kind, string acct, string path) Key => (Kind, AccountId, Path);

    /// <summary>
    /// Creates an instance whose kind is generic.
    /// </summary>
    public static Link CreateGeneric(string path)
    {
        Validation.BugCheckNonEmpty(path, nameof(path));
        return new Link(LinkKind.Generic, null, path);
    }

    /// <summary>
    /// Creates an instance whose kind is http.
    /// </summary>
    public static Link CreateHttp(string path)
    {
        Validation.BugCheckNonEmpty(path, nameof(path));
        return new Link(LinkKind.Http, null, path);
    }

    /// <summary>
    /// Creates an instance with the given kind and account.
    /// </summary>
    public static Link Create(LinkKind kind, string acctId, string path)
    {
        Validation.BugCheckParam(kind.IsValid(), nameof(kind));
        Validation.BugCheckParam(kind.NeedsAccount() == !string.IsNullOrEmpty(acctId), nameof(acctId));
        Validation.BugCheckNonEmpty(path, nameof(path));
        return new Link(kind, acctId, path);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Link"/> class.
    /// </summary>
    private Link(LinkKind kind, string acctId, string path)
    {
        Validation.Assert(kind.IsValid());
        Validation.Assert(kind.NeedsAccount() == !string.IsNullOrEmpty(acctId));
        Validation.AssertNonEmpty(path);

        Kind = kind;
        AccountId = string.IsNullOrEmpty(acctId) ? null : acctId;
        Path = path;
    }

    /// <summary>
    /// Return whether this link is semantically the same as the given one.
    /// </summary>
    public bool IsSame(Link link)
    {
        if (this == link)
            return true;
        if (link == null)
            return false;
        if (Kind != link.Kind)
            return false;
        if (Path != link.Path)
            return false;
        if (AccountId != link.AccountId)
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, AccountId, Path);
    }

    public bool Equals(Link other)
    {
        return IsSame(other);
    }

    public override bool Equals(object obj)
    {
        return IsSame(obj as Link);
    }
}

/// <summary>
/// This represents the kind of a link object. The numeric values are serialized in binary representations
/// so should not be changed. If any values are added, the binary version numbers will need to be "bumped".
/// This applies to both rbin format and the statement interpreter state serialization.
/// </summary>
public enum LinkKind : byte
{
    /// <summary>
    /// Illegal.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// References a generic path of some form that the runtime knows nothing about.
    /// The end user and/or particular client of the runtime need to provide any additional
    /// context that may be needed.
    /// REVIEW: This kind is still not entirely clear. How should it be used? Will
    /// it cover all needed cases? Etc.
    /// </summary>
    Generic = 1,

    /// <summary>
    /// References a path in some kind of temporary or cache location. For example, the location
    /// may be within a temp file directory or a resource caching services such as RPS.
    /// </summary>
    Temporary = 2,

    /// <summary>
    /// References an http(s) based path.
    /// </summary>
    Http = 3,

    /// <summary>
    /// References a path within an azure blob account.
    /// </summary>
    AzureBlob = 4,

    /// <summary>
    /// References a path within an azure data lake (legacy) account.
    /// </summary>
    AzureDataLake = 5,

    /// <summary>
    /// References a path within a gen-2 azure data lake account.
    /// </summary>
    AzureDataLakeGen2 = 6,
}

/// <summary>
/// Utilitiies for <see cref="LinkKind"/>.
/// </summary>
public static class LinkKindUtil
{
    /// <summary>
    /// Whether the kind is valid.
    /// </summary>
    public static bool IsValid(this LinkKind kind)
    {
        return kind <= LinkKind.AzureDataLakeGen2 && kind != 0;
    }

    /// <summary>
    /// Whether the kind needs an associated account.
    /// </summary>
    public static bool NeedsAccount(this LinkKind kind)
    {
        return kind >= LinkKind.AzureBlob && kind <= LinkKind.AzureDataLakeGen2;
    }
}
