// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl;

namespace Microsoft.Rexl.Harness;

// This partial handles global values.
partial class HarnessBase
{
    /// <summary>
    /// Returns whether there is a standard global (not item in module or task) with the given name.
    /// </summary>
    public abstract bool HasGlobal(NPath name);

    /// <summary>
    /// Gets the current global names, sorted by name.
    /// </summary>
    public abstract Immutable.Array<NPath> GetGlobalNames();

    /// <summary>
    /// Gets the current global names and types, sorted by name.
    /// </summary>
    public abstract Immutable.Array<(NPath name, DType type)> GetGlobalInfos();

    /// <summary>
    /// Gets the type of <c>this</c>, if there is one.
    /// The default implementation treats <c>this</c> like a global with "root" name.
    /// </summary>
    protected virtual bool TryGetThisType(out DType type) => TryGetGlobalType(NPath.Root, out type);

    /// <summary>
    /// Returns whether there is a standard global (not item in module or task) with the given name
    /// and sets <paramref name="type"/> to its type.
    /// </summary>
    public abstract bool TryGetGlobalType(NPath name, out DType type);
}
