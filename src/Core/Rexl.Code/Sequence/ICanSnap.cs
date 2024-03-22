// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// An interface for sequences that can be "snap-shotted". This is for sequences that
/// are populated incrementally over time, to provide a snapshot of the values available
/// so far.
/// </summary>
public interface ICanSnap : IEnumerable
{
    /// <summary>
    /// Return a current snap-shot. The result may be <c>null</c> to indicate empty.
    /// The result may also be this object, if it is fully populated.
    /// </summary>
    IEnumerable? Snap();
}

public interface ICanSnap<T> : ICanSnap, IEnumerable<T>
{
    /// <summary>
    /// Return a current snap-shot. The result may be <c>null</c> to indicate empty.
    /// The result may also be this object, if it is fully populated.
    /// </summary>
    new IEnumerable<T>? Snap();
}
