// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// This wraps a source <see cref="IEnumerable{T}"/> with a cache, so the source
/// is iterated at most once. This IS thread safe, in the sense, that, assuming the underlying
/// enumerator doesn't care which thread it "moves" on, this can be iterated on multiple threads
/// simultaneously without the possibility of corruption.
/// </summary>
public sealed class CachingEnumerable<T> : ICursorable<T>, ICanCount, IDisposable
{
    // REVIEW: Is there a better way to implement this? Should probably leverage Buildable.

    // This lock protects the `_items, but not _ator.
    private readonly ReaderWriterLockSlim _lock;
    // REVIEW: Should probably introduce a block scheme to avoid exceptions when
    // the list grows too large. Eg, Range(0x20000000)->Count().
    private readonly List<T> _items;

    // This lock protects the _ator.
    private readonly object _lockAtor;

    // Whether the source has been fully cached.
    private volatile bool _fullyCached;
    // The source enumerable. This is set to null once we get an enumerator.
    private volatile IEnumerable<T>? _able;
    // The source enumerator. This is null before we get an enumerator and after dispose.
    private volatile IEnumerator<T>? _ator;

    // Cache the "computed" count, as an optimization. Since we can't make a long field be
    // volatile, we need a volatile bool field to remember whether we've computed the count.
    // We set _count to negative until we compute it.
    private long _count;
    private volatile bool _haveCount;

    // The source enumerable, if it implements ICanCount and we still need it. Set to null
    // when _haveCount is set to true.
    private volatile ICanCount? _srcCounter;

    public CachingEnumerable(IEnumerable<T> src)
    {
        Validation.AssertValue(src);
        _lock = new ReaderWriterLockSlim();
        _items = new List<T>();
        _lockAtor = new object();
        _able = src;
        // We wait to call GetEnumerator until the first time MoveNext is called on us.
        _ator = null;
        _srcCounter = src as ICanCount;
        _haveCount = false;
        _count = -1;
    }

    /// <summary>
    /// This constructor is when the count is known.
    /// </summary>
    public CachingEnumerable(long count, IEnumerable<T> src)
        : this(src)
    {
        Validation.BugCheckParam(count > 0, nameof(count));
        _count = count;
        _haveCount = true;
#if DEBUG
        if (_srcCounter != null && _srcCounter.TryGetCount(out long c2))
            Validation.Assert(c2 == count);
#endif
    }

    ~CachingEnumerable()
    {
        DisposeCore();
    }

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    private void DisposeCore()
    {
        lock (_lockAtor)
        {
            _able = null;
            if (_ator != null)
            {
                try { _ator.Dispose(); }
                catch { }
                _ator = null;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<T> GetEnumerator()
    {
        // REVIEW: This read lock and the one in TryGetCount _may_ be needed to ensure that
        // all changes are committed to RAM. Not sure though.
        _lock.EnterReadLock();
        try
        {
            if (_fullyCached)
                return _items.GetEnumerator();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        return GetEnumeratorCore();
    }

    private IEnumerator<T> GetEnumeratorCore()
    {
        int index = 0;
        bool simple = false;
        while (TryGetItem(ref simple, index, out var item))
        {
            yield return item;
            index++;
        }
    }

    public bool TryGetCount(out long count)
    {
        if (_haveCount)
        {
            count = _count;
            Validation.Assert(count >= 0);
            return true;
        }

        if (_fullyCached)
        {
            // REVIEW: This read lock and the one in GetEnumerator _may_ be needed to ensure that
            // all changes are committed to RAM. Not sure though.
            _lock.EnterReadLock();
            try
            {
                count = _count = _items.Count;
                _haveCount = true;
                _srcCounter = null;
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        var counter = _srcCounter;
        if (counter != null && counter.TryGetCount(out long countFld))
        {
            count = _count = countFld;
            _haveCount = true;
            _srcCounter = null;
            return true;
        }

        count = -1;
        return false;
    }

    public long GetCount(Action? callback)
    {
        Validation.BugCheckValueOrNull(callback);

        if (TryGetCount(out long count))
            return count;

        var counter = _srcCounter;
        if (counter != null)
        {
            _count = counter.GetCount(callback);
            _haveCount = true;
            _srcCounter = null;
            return _count;
        }

        long cur;
        _lock.EnterReadLock();
        try
        {
            if (TryGetCount(out count))
                return count;
            cur = _items.Count;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        Validation.Assert(0 <= cur && cur <= int.MaxValue);
        int index = (int)cur;
        bool simple = false;
        for (; ; )
        {
            callback?.Invoke();
            if (!TryGetItem(ref simple, index, out _))
                break;
            index++;
            // This shouldn't overflow, since currently a List<T> can't grow as large as int.MaxValue.
            Validation.Assert(index > 0);
        }

        Validation.Assert(_fullyCached);
        count = _items.Count;
        Validation.Assert(index == count);
        Validation.Assert(!_haveCount || _count == count);
        _count = count;
        _haveCount = true;
        _srcCounter = null;
        return _count;
    }

    public ICursor<T> GetCursor()
    {
        return new Cursor(this);
    }

    ICursor ICursorable.GetCursor()
    {
        return GetCursor();
    }

    /// <summary>
    /// Try to get the indicated item from the cache.
    /// </summary>
    private bool TryGetItem(ref bool simple, int index, [MaybeNullWhen(false)] out T item)
    {
        Validation.Assert(!simple || _fullyCached);
        Validation.Assert(index >= 0);

        for (; ; )
        {
            if (simple)
                break;

            // Try to get the item from the cache. Grab a read lock.
            _lock.EnterReadLock();
            try
            {
                Validation.Assert(!simple);
                if (_fullyCached)
                {
                    simple = true;
                    break;
                }

                if (index < _items.Count)
                {
                    item = _items[index];
                    return true;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Need to advance the ator so grab the ator lock.
            Validation.Assert(!simple);
            lock (_lockAtor)
            {
                // There is a possibility that another thread has already advanced the ator.
                // Enter a read lock and see if our item is there already, or if the ator was
                // disposed by another thread.
                _lock.EnterReadLock();
                try
                {
                    if (_fullyCached)
                    {
                        simple = true;
                        break;
                    }

                    if (index < _items.Count)
                    {
                        item = _items[index];
                        return true;
                    }
                }
                finally
                {
                    _lock.ExitReadLock();
                }

                // Try to advance.
                Validation.Assert(!_fullyCached);
                Validation.Assert(!simple);

                if (_ator == null)
                {
                    Validation.Assert(_able != null);
                    _ator = _able.GetEnumerator();
                    _able = null;
                }

                if (!_ator.MoveNext())
                {
                    // No more items so throw away the ator.
                    try { _ator.Dispose(); }
                    catch { }
                    _fullyCached = true;
                    _ator = null;
                    item = default;
                    return false;
                }

                // Grab the write lock and record the new item. Since we have the ator lock, no one
                // else should get there before us.
                _lock.EnterWriteLock();
                try
                {
                    Validation.Assert(_ator != null);
                    _items.Add(_ator.Current);
                    if (index < _items.Count)
                    {
                        item = _items[index];
                        return true;
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        // No locking needed once the ator is gone.
        Validation.Assert(simple);
        Validation.Assert(_fullyCached);
        if (index < _items.Count)
        {
            item = _items[index];
            return true;
        }

        item = default;
        return false;
    }

    private sealed class Cursor : ICursor<T>
    {
        private CachingEnumerable<T> _parent;

        private bool _simple;
        private T? _value;
        private int _index;

        object? IEnumerator.Current => _value;

        public T Current => _value!;

        public T Value => _value!;

        public long Index => _index;

        object? ICursor.Value => _value;

        public Cursor(CachingEnumerable<T> parent)
        {
            Validation.AssertValue(parent);
            _parent = parent;
            _index = -1;
        }

        public void Dispose()
        {
            _parent = null!;
        }

        public bool MoveNext()
        {
            var parent = _parent;
            Validation.BugCheck(parent != null);

            if (!parent.TryGetItem(ref _simple, _index + 1, out var value))
                return false;

            _value = value;
            _index++;
            return true;
        }

        public bool MoveTo(long index)
        {
            var parent = _parent;
            Validation.BugCheck(parent != null);
            Validation.BugCheckParam(index >= 0, nameof(index));

            if (index > int.MaxValue)
                return false;
            if (!parent.TryGetItem(ref _simple, (int)index, out var value))
                return false;

            _value = value;
            _index = (int)index;
            return true;
        }

        public void Reset()
        {
            throw new InvalidOperationException();
        }
    }
}
