// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// Sequence that iterates over an indexed sequence of groups. Provides
/// an enumerator over the individual items of the groups, optionally
/// with the group index.
/// REVIEW: Implement ICursorable, ICanCount.
/// </summary>
public abstract class FlatteningSequence<T> : IEnumerable<T>
{
    /// <summary>
    /// The implementation.
    /// </summary>
    private sealed class Impl : FlatteningSequence<T>
    {
        public Impl(IndexedSequence<Immutable.Array<T>> seqItems)
            : base(seqItems)
        {
        }
    }

    /// <summary>
    /// The internal sequence that holds all the items and handles thread safety.
    /// </summary>
    private readonly IndexedSequence<Immutable.Array<T>> _items;

    /// <summary>
    /// Whether building is done.
    /// </summary>
    public bool IsDone => _items.IsDone;

    private FlatteningSequence(IndexedSequence<Immutable.Array<T>> items)
    {
        Validation.AssertValue(items);
        _items = items;
    }

    /// <summary>
    /// Create an instance of <see cref="FlatteningSequence{T}"/> using a backing
    /// instance of <see cref="IndexedSequence{Immutable.Array{T}}"/> to hold
    /// the groups of items.
    /// </summary>
    public static IndexedSequence<Immutable.Array<T>>.Builder CreateBuilder(out FlatteningSequence<T> seq)
    {
        var bldr = IndexedSequence<Immutable.Array<T>>.Builder.Create(default, out var items);
        seq = new Impl(items);
        return bldr;
    }

    /// <summary>
    /// Returns an <see cref="IEnumerator"/> for both the values and
    /// their associated keys. The enumeration will be presented in
    /// sorted key order but may not be contiguous (i.e. no groups
    /// are associated with the key). Blocks if the key has not been
    /// added yet.
    /// 
    /// Each enumerator should be used on one thread at a time. Simultaneous
    /// access from multiple threads doesn't make sense.
    /// </summary>
    public IEnumerator<(long key, T value)> GetEnumeratorWithKey()
    {
        return new AtorKey(this);
    }

    /// <summary>
    /// Returns an <see cref="IEnumerator{T}"/> for the values of this sequence.
    /// 
    /// Each enumerator should be used on one thread at a time. Simultaneous
    /// access from multiple threads doesn't make sense.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return new AtorStd(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private abstract class Ator<TCur> : IEnumerator<TCur>
    {
        // The sequence and core enumerator. Set to null on dispose.
        private volatile FlatteningSequence<T> _parent;
        private volatile IEnumerator<Immutable.Array<T>> _atorCore;

        // The current key.
        protected long _keyCur;

        // The current item.
        protected T? _current;

        // The current item group.
        private Immutable.Array<T> _groupCur;

        // The next position within _groupCur.
        private int _iitemNext;

        // Whether calling Current is valid. This means
        // the object isn't disposed, MoveNext has been called,
        // and it hasn't yet returned false.
        private bool _valid;

        public Ator(FlatteningSequence<T> parent)
        {
            Validation.AssertValue(parent);
            _parent = parent;
            _atorCore = parent._items.GetEnumerator();
            _keyCur = -1;
        }

        public TCur Current
        {
            get
            {
                if (_parent == null)
                    throw new ObjectDisposedException(nameof(Ator<TCur>));
                if (!_valid)
                    throw new InvalidOperationException();
                return CurrentCore;
            }
        }
        protected abstract TCur CurrentCore { get; }
        object? IEnumerator.Current => Current;

        public void Dispose()
        {
            _parent = null!;
            _atorCore?.Dispose();
            _atorCore = null!;
            _valid = false;
        }

        public bool MoveNext()
        {
            if (_parent == null)
                throw new ObjectDisposedException(nameof(Ator<TCur>));

            while (_iitemNext >= _groupCur.Length)
            {
                if (!_atorCore.MoveNext())
                {
                    _valid = false;
                    return false;
                }
                _keyCur++;
                _groupCur = _atorCore.Current;
                _iitemNext = 0;
            }

            _current = _groupCur[_iitemNext];
            _iitemNext++;
            _valid = true;
            return true;
        }

        public void Reset() => throw new InvalidOperationException();
    }

    /// <summary>
    /// The standard ordered enumerator. This should not be used simultaneously
    /// on multiple threads. It makes no sense to do so.
    /// </summary>
    private sealed class AtorStd : Ator<T>
    {
        protected override T CurrentCore => _current!;

        public AtorStd(FlatteningSequence<T> parent)
            : base(parent)
        {
        }
    }

    /// <summary>
    /// The enumerator that also produces the associated key for each item.
    /// This should not be used simultaneously on multiple threads. It makes
    /// no sense to do so.
    /// </summary>
    private sealed class AtorKey : Ator<(long, T)>
    {
        protected override (long, T) CurrentCore => (_keyCur, _current!);

        public AtorKey(FlatteningSequence<T> parent)
            : base(parent)
        {
        }
    }
}
