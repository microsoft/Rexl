// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// This is a sentinel interface indicating that caching on top of this is pointless.
/// </summary>
public interface ICachingEnumerable : IEnumerable
{
}

/// <summary>
/// This is a sentinel interface indicating that caching on top of this is pointless.
/// </summary>
public interface ICachingEnumerable<out T> : IEnumerable<T>, ICachingEnumerable
{
}

/// <summary>
/// For iterating over non-generic items in a dictated order.
/// </summary>
public interface ICursorable : IEnumerable, ICachingEnumerable
{
    /// <summary>
    /// Gets a seekable cursor that blocks when a requested item is not yet available.
    /// </summary>
    ICursor GetCursor();
}

/// <summary>
/// For iterating over items in a dictated order.
/// </summary>
public interface ICursorable<out T> : ICursorable, ICachingEnumerable<T>
{
    /// <summary>
    /// Gets a seekable cursor that blocks when a requested item is not yet available.
    /// </summary>
    new ICursor<T> GetCursor();
}

/// <summary>
/// For iterating over non-generic items in a dictated order.
/// </summary>
public interface ICursor : IEnumerator, IDisposable
{
    /// <summary>
    /// Advance to the indicated item, blocking until it is available.
    /// </summary>
    bool MoveTo(long index);

    /// <summary>
    /// Advance to the indicated item, blocking until it is available.
    /// </summary>
    bool MoveTo(long index, Action? callback);

    /// <summary>
    /// The current non-generic value.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// The current index.
    /// </summary>
    long Index { get; }
}

/// <summary>
/// For iterating over items in a dictated order.
/// </summary>
public interface ICursor<out T> : ICursor, IEnumerator<T>
{
    /// <summary>
    /// The current value.
    /// </summary>
    new T Value { get; }
}

/// <summary>
/// For iterating over non-generic items as they become available, not necessarily in index order.
/// </summary>
public interface IIndexedEnumerable : ICursorable
{
    /// <summary>
    /// Gets an enumerator that produces non-generic items in whatever order they become available.
    /// </summary>
    IIndexedEnumerator GetIndexedEnumerator();

    /// <summary>
    /// Gets a flag indicating whether the sequence is done building.
    /// </summary>
    bool IsDone { get; }
}

/// <summary>
/// For iterating over items as they become available, not necessarily in index order.
/// </summary>
public interface IIndexedEnumerable<out T> : IIndexedEnumerable, ICursorable<T>
{
    /// <summary>
    /// Gets an enumerator that produces items in whatever order they become available.
    /// </summary>
    new IIndexedEnumerator<T> GetIndexedEnumerator();
}

/// <summary>
/// For iterating over non-generic items as they become available, not necessarily in index order.
/// </summary>
public interface IIndexedEnumerator
{
    /// <summary>
    /// Advance to another item, blocking until one is available.
    /// </summary>
    bool MoveNext();

    /// <summary>
    /// The current non-generic value.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// The current index.
    /// </summary>
    long Index { get; }
}

/// <summary>
/// For iterating over items as they become available, not necessarily in index order.
/// </summary>
public interface IIndexedEnumerator<out T> : IIndexedEnumerator, IDisposable
{
    /// <summary>
    /// The current value.
    /// </summary>
    new T Value { get; }
}
