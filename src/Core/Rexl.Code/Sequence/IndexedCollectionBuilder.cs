// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

// REVIEW: this abstract class might not be needed or if kept
// should be renamed so the name does not imply that a collection is being built
// and uses something like 'receiver' rather than 'builder'.
public abstract class IndexedCollectionBuilder
{
    /// <summary>
    /// Add an item to the sequence.
    /// </summary>
    public abstract void AddValue(long index, object? value);

    /// Complete building of the sequence. <paramref name="indexLim"/> is the limit of indices
    /// for the entire sequence.
    /// </summary>
    public abstract void Done(long indexLim);

    /// Exit the sequence.
    /// </summary>
    public abstract void Quit(Exception? ex);

    /// <summary>
    /// A flag indicating whether this builder is active.
    /// </summary>
    public abstract bool IsActive { get; }
}

public abstract class IndexedCollectionBuilder<T> : IndexedCollectionBuilder
{
    /// <summary>
    /// Add an item to the sequence.
    /// </summary>
    public abstract void Add(long index, T value);

    /// <summary>
    /// Add an item to the sequence.
    /// </summary>
    public sealed override void AddValue(long index, object? value)
    {
        Validation.BugCheck((value == null && default(T) == null) || value is T);
        Add(index, (T)value!);
    }
}
