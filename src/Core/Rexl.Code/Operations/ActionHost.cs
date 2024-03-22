// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Rexl.Code;

/// <summary>
/// The base host class for action execution.
/// </summary>
public abstract class ActionHost
{
    /// <summary>
    /// The type manager to use.
    /// </summary>
    public abstract TypeManager TypeManager { get; }

    /// <summary>
    /// Open a read-only stream with the given <paramref name="link"/>. Throws on failure.
    /// Sets <paramref name="full"/> to a "full path" for the file, if meaningful.
    /// Otherwise, sets it to <c>null</c> or <paramref name="link"/>.
    /// </summary>
    public abstract Task<(Link full, Stream stream)> LoadStreamAsync(Link link);

    /// <summary>
    /// Create a new stream with the given <paramref name="link"/>. Throws on failure.
    /// Sets <paramref name="full"/> to a "full path" for the file, if meaningful.
    /// Otherwise, sets it to <c>null</c> or <paramref name="link"/>.
    /// </summary>
    public abstract Task<(Link full, Stream stream)> CreateStreamAsync(Link link,
        StreamOptions options = default);

    /// <summary>
    /// Produces a sequence of files contained in the "directory" specified by the optional
    /// <paramref name="linkDir"/> value. When it is <c>null</c>, the current directory is
    /// assumed. Sets <paramref name="full"/> to a "full path" for the "directory", if meaningful.
    /// Otherwise, sets it to <c>null</c> or <paramref name="link"/>.
    /// </summary>
    public abstract IEnumerable<Link> GetFiles(Link linkDir, out Link full);

    /// <summary>
    /// Returns the current date, time, and time zone offset.
    /// </summary>
    public abstract DateTimeOffset Now();

    /// <summary>
    /// Create an action runner for a user proc and the given arguments. Generally, this is called
    /// when a user proc is invoked. It probably shouldn't be called directly by other actions.
    /// </summary>
    public abstract ActionRunner CreateUserProcRunner(UserProc proc, DType typeWith, RecordBase with);
}
