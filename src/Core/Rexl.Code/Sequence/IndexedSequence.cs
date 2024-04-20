// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// This is a sequenced collection with the following features:
/// <list type="bullet">
/// <item>Supports building by adding items in any order and on an arbitrary number of threads, essentially
///   adapting a parallelized "push" world to a "pull" world. To use this, invoke the
///   <see cref="Builder.Create(T, out IndexedSequence{T})"/> method.</item>
/// <item>Alternatively can be populated from an <c>IEnumerable{(long index, T value)}</c>.
///   To use this, invoke the <see cref="Create(T, IEnumerable{(long index, T item)})"/> method.</item>
/// <item>Supports enumeration in index order via <see cref="IEnumerable{T}"/> while building on different
///   threads.</item>
/// <item>Supports indexed (out of order) enumeration via <see cref="IIndexedEnumerable{T}"/> while building
///   on different threads.</item>
/// <item>The enumerator <c>MoveNext</c> methods will block until the needed item is available (any new item
///   for indexed enumeration, the item corresponding to the next index for standard enumeration).</item>
/// <item>The enumerator <c>MoveNext</c> methods will throw if the builder is disposed or garbage collected
///   before calling <see cref="Builder.Done(long)"/>.</item>
/// </list>
/// REVIEW: Is a non-indexed version also useful?
/// </summary>
public abstract class IndexedSequence<T> : IIndexedEnumerable<T>, ICanCount, ICanSnap<T>
{
    /// <summary>
    /// The builder for an <see cref="IndexedSequence{T}"/>. Call <see cref="Add(long, T)"/> to add items.
    /// The <c>index</c> passed to <see cref="Add(long, T)"/> must be non-negative and distinct. This assumes
    /// that indices are "dense" so it is reasonable to allocate an array whose size is larger than all indices.
    /// Call <see cref="Done(long)"/> when finished adding items. The argument to <see cref="Done(long)"/>
    /// should be larger than any index passed to <see cref="Add(long, T)"/>. The sequence will not be
    /// corrupted if this is violated, but <see cref="Done(long)"/> will throw. If the <see cref="Builder"/>
    /// is disposed before <see cref="Done(long)"/> is called, the sequence is considered "corrupt" and
    /// all future access (and any current enumerators) will throw.
    /// </summary>
    public sealed class Builder : IndexedCollectionBuilder<T>
    {
        /// <summary>
        /// The sequence. Set to null when disposed or <see cref="Done(long)"/> is called.
        /// </summary>
        private IndexedSequence<T>? _seq;

        /// <summary>
        /// Create an <see cref="IndexedSequence{T}"/> and associated <see cref="Builder"/>. The sequence can
        /// immediately be passed to client code while the builder is used on separate threads to add items
        /// to the sequence and complete the sequence. The <paramref name="valDef"/> value is used by
        /// standard (in order) enumeration when items are missing. This is only used after <see cref="Done(long)"/>
        /// has been called. The optional parameter <paramref name="countAhead"> is item count of IndexedSequence
        /// which if passed as non-negative integer, client code can get the item count ahead
        /// through TryGetCount or GetCount methods without waiting for sequence enumeration and another optional
        /// parameter <paramref name="counter"/> is ICanCount object to retreive count if <paramref name="countAhead"/>
        /// is not specified. 
        /// The client code should always call either <see cref="Done(long)"/> when building completed successfully
        /// or <see cref="Quit(Exception)"/> when an error happened while building.
        /// </summary>
        public static Builder Create(T valDef, out IndexedSequence<T> seq,
            long countAhead = -1, ICanCount? counter = null)
        {
            seq = new Buildable(valDef, countAhead, counter);
            return new Builder(seq);
        }

        private Builder(IndexedSequence<T> seq)
        {
            Validation.AssertValue(seq);
            _seq = seq;
        }

        ~Builder()
        {
            // REVIEW: The try-catch was added when we were using a concurrent bag to hold the events
            // since the bag may have been finalized before getting here (so pulling items from it could throw).
            // I don't think the try-catch is needed anymore, but also shouldn't hurt, unless it masks other
            // issues. Hence the assert.
            try
            {
                var seq = Interlocked.Exchange(ref _seq, null);
                if (seq != null)
                    seq.QuitBuilding(null);
            }
            catch
            {
                // Shouldn't get here.
                Validation.Assert(false);
            }
        }

        /// <summary>
        /// A flag indicating whether this builder is active.
        /// </summary>
        public override bool IsActive => _seq != null;

        /// <summary>
        /// Add an item to the sequence.
        /// </summary>
        public override void Add(long index, T value)
        {
            // Note that there is a race condition here - Receive may be called after DoneBuilding
            // or QuitBuilding is called. The sequence object needs to deal with that possibility.
            var seq = _seq;
            Validation.BugCheck(seq != null, "Builder inactive");

            try
            {
                seq.Receive(index, value);
            }
            catch (Exception ex)
            {
                // Shut down building, if it isn't already.
                seq = Interlocked.Exchange(ref _seq, null);
                if (seq != null)
                    seq.QuitBuilding(ex);
                throw;
            }
        }

        /// <summary>
        /// Complete successful building of the sequence. <paramref name="indexLim"/> is the
        /// limit of indices for the entire sequence.
        /// </summary>
        public override void Done(long indexLim)
        {
            var seq = Interlocked.Exchange(ref _seq, null);
            Validation.BugCheck(seq != null, "Builder inactive");
            seq.DoneBuilding(indexLim);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Complete failed building of the sequence.
        /// </summary>
        public override void Quit(Exception? ex)
        {
            var seq = Interlocked.Exchange(ref _seq, null);
            Validation.BugCheck(seq != null, "Builder inactive");
            seq.QuitBuilding(ex);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Create and return the sequence with the given <paramref name="src"/> as source,
    /// <paramref name="valDef"/> as default value, optional parameter <paramref name="countAhead"/>
    /// as item count of IndexedSequence which if passed as non-negative integer, client
    /// code can get the item counts ahead through TryGetCount or GetCount methods without
    /// waiting for sequence enumeration. Another optional parameter <paramref name="counter"/>
    /// is ICanCount object to retreive count if <paramref name="countAhead"/> is not specified.
    /// </summary>
    public static IndexedSequence<T> Create(T valDef, IEnumerable<(long index, T item)> src,
        long countAhead = -1, ICanCount? counter = null)
    {
        Validation.BugCheckValue(src, nameof(src));
        return new Pulling(valDef, src, countAhead, counter);
    }

    /// <summary>
    /// The sub-class that uses a builder.
    /// </summary>
    private sealed class Buildable : IndexedSequence<T>
    {
        public Buildable(T valDef, long countAhead, ICanCount? counter)
            : base(valDef, countAhead, counter)
        {
        }
    }

    /// <summary>
    /// The sub-class that uses a source enumerator. Note that this doesn't explicitly touch
    /// the base class reader/writer lock, nor any other state of the base class. It relies
    /// on the <see cref="Receive(long, T)"/>, <see cref="DoneBuilding(long)"/> and
    /// <see cref="QuitBuilding(Exception)"/> methods, just as the builder above does.
    /// </summary>
    private sealed class Pulling : IndexedSequence<T>
    {
        // The lock to protect the ator.
        private readonly object _lockAtor;

        // The source enumerable.
        private IEnumerable<(long index, T value)>? _able;
        // The source enumerator.
        private IEnumerator<(long index, T value)>? _ator;

        public Pulling(T valDef, IEnumerable<(long index, T value)> src, long countAhead, ICanCount? counter)
            : base(valDef, countAhead, counter)
        {
            Validation.BugCheckValue(src, nameof(src));
            _lockAtor = new object();
            _able = src;
            _ator = null;
        }

        ~Pulling()
        {
            Quit(null);
        }

        private void Quit(Exception? ex)
        {
            lock (_lockAtor)
            {
                _able = null;
                if (_ator != null)
                {
                    try { _ator.Dispose(); }
                    catch { }
                    _ator = null;
                    QuitBuilding(ex);
                }
            }
        }

        protected override void Slice(ManualResetEventSlim evt)
        {
            base.Slice(evt);

            // May need to advance the ator.
            if (evt.IsSet)
                return;

            lock (_lockAtor)
            {
                // There is a possibility that another thread has already advanced the ator.
                if (evt.IsSet)
                    return;

                if (_ator == null)
                {
                    if (_able == null)
                    {
                        Validation.Assert(_isDone || _ex != null);
                        SignalReaders();
                        return;
                    }
                    _ator = _able.GetEnumerator();
                    _able = null;
                }

                // Try to advance.
                Validation.Assert(!_isDone);
                Validation.Assert(_ex == null);
                try
                {
                    if (_ator.MoveNext())
                    {
                        var cur = _ator.Current;
                        Receive(cur.index, cur.value);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Quit(ex);
                    GC.SuppressFinalize(this);
                    return;
                }

                // No more items so throw away the ator.
                Validation.Assert(_able == null);
                if (_ator != null)
                {
                    try { _ator.Dispose(); }
                    catch { }
                    _ator = null;
                }

                if (TryGetExpectedCount(out long expectedCount))
                {
                    Validation.Assert(_indLim <= expectedCount);
                    DoneBuilding(expectedCount);
                }
                else
                {
                    // REVIEW: What should be used for indexLim? This assumes no "tail" items
                    // were skipped, which may not be a valid assumption. But there doesn't seem to be
                    // good way from the src to communicate a better value.
                    DoneBuilding(_indLim);
                }
            }
        }
    }

    /// <summary>
    /// The number of items in sequence collection if known in advance. If value is negative,
    /// count is not known, otherwise this value is used to get sequence count through ICanCount
    /// interface for certain scenarios, e.g 1:1 model where output sequence count is known
    /// ahead without enumerating input sequence.
    /// It can change from negative (unknown) to >=0 when <paramref name="_counter"/> provides a value,
    /// so thread safety is needed to read/write this field.
    /// </summary>
    private long _countAhead;

    /// <summary>
    /// In certain scenarios, no of items in sequence collection can be retrieved from this
    /// counter if specified. e.g 1:1 model where output sequence count will be same as input
    /// sequence count. It will be set to null when it is no longer needed.
    /// </summary>
    private volatile ICanCount? _counter;

    /// <summary>
    /// When <paramref name="_countAhead"/> is unknown and <paramref name="_counter"/> is also not set during
    /// intantiation, this field reduces overhead to get expected count as it cannot be determined.
    /// </summary>
    private readonly bool _canGetExpectedCount;

    /// <summary>
    /// The reader-writer lock protecting the internal collection state.
    /// </summary>
    private readonly ReaderWriterLockSlim _lock;

    /// <summary>
    /// Linked list of events. The events are to unblock active enumerators/readers.
    /// </summary>
    private sealed class EvtLink
    {
        public readonly ManualResetEventSlim Evt;
        public readonly EvtLink? Next;

        public EvtLink(ManualResetEventSlim evt, EvtLink? next)
        {
            Validation.AssertValue(evt);
            Evt = evt;
            Next = next;
        }
    }

    /// <summary>
    /// The chain of reset events to set whenever anything changes. These unblock active enumerators/readers.
    /// </summary>
    private volatile EvtLink? _evts;

    /// <summary>
    /// The default value to use for non-added indices. This is only relevant after <see cref="_isDone"/>
    /// has been set to true and when enumerating in order.
    /// REVIEW: This is currently set in the ctor but isn't needed until after <see cref="_isDone"/>
    /// is set to true, so could be passed to <see cref="Builder.Done(long)"/> instead of
    /// <see cref="Builder.Create(T, out IndexedSequence{T})"/>.
    /// </summary>
    private readonly T _valDef;

    // REVIEW: What kind of data structure should be used for items? Is there a good
    // option for long-indexing? Is it at all practical?

    /// <summary>
    /// The capacity of the arrays. Always at least <see cref="_indLim"/>.
    /// </summary>
    private int _cap;

    /// <summary>
    /// The items added. This is indexed by the, uh, index. Items that have been set are all at slots
    /// below <see cref="_indLim"/>, and have the corresponding bit set in <see cref="_flags"/>.
    /// </summary>
    private T[] _items;

    /// <summary>
    /// The indices in the order the items were added. There are <see cref="_count"/> of these.
    /// </summary>
    private int[] _inds;

    /// <summary>
    /// This is a bit array indicating which indices have been added so far. Exactly <see cref="_count"/>
    /// bits should be set and the rest clear. Of course, the set bits should correspond to the values
    /// in <see cref="_inds"/>.
    /// </summary>
    private uint[] _flags;

    /// <summary>
    /// One more than the largest added index.
    /// </summary>
    private int _indLim;

    /// <summary>
    /// The number of items added so far. This is the number of active values in <see cref="_inds"/>.
    /// </summary>
    private int _count;

    /// <summary>
    /// When building is shut down before <see cref="Builder.Done(long)"/> is called, an exception is recorded here.
    /// If this happens the sequence is considered "bad"/"corrupt".
    /// </summary>
    private volatile Exception? _ex;

    /// <summary>
    /// Whether building is done.
    /// </summary>
    private volatile bool _isDone;

    /// <summary>
    /// The latest obtained snapshot.
    /// </summary>
    private volatile Snapshot? _snapshot;

    /// <summary>
    /// The final size declared when <see cref="Builder.Done(long)"/> is called.
    /// </summary>
    private long _indexLim;

    // Constants for accessing a bit in _flags.
    private const int _logCbitGrp = 5;
    private const int _cbitGrp = 1 << _logCbitGrp;

    private IndexedSequence(T valDef, long countAhead, ICanCount? counter)
    {
        _lock = new ReaderWriterLockSlim();
        _evts = null;
        _valDef = valDef;

        _cap = 32;
        _items = new T[_cap];
        _inds = new int[_cap];
        _flags = new uint[(_cap + _cbitGrp - 1) / _cbitGrp];
        _indLim = 0;
        _count = 0;

        _ex = null;
        _isDone = false;
        _indexLim = -1;
        _countAhead = countAhead;
        _counter = countAhead >= 0 ? null : counter;
        _canGetExpectedCount = countAhead >= 0 || counter != null;
    }

    /// <summary>
    /// Building has failed somehow. The builder was disposed or finalized without
    /// <see cref="Builder.Done(long)"/> having been called.
    /// </summary>
    private void QuitBuilding(Exception? ex)
    {
        _lock.EnterWriteLock();
        try
        {
            Validation.Assert(_ex == null);
            Validation.Assert(!_isDone);

            _ex = ex ?? new OperationCanceledException("Builder not completed");
            SignalReaders();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Finished adding items.
    /// </summary>
    private void DoneBuilding(long indexLim)
    {
        // We use expected count here if available and validate it against indexLim at the end (they should match).
        // We do the validation at the end so the sequence is properly completed regardless.
        long count = indexLim;
        _lock.EnterWriteLock();
        try
        {
            Validation.Assert(_ex == null);
            Validation.Assert(!_isDone);

            if (TryGetExpectedCount(out long expectedCount))
                count = expectedCount;
            _isDone = true;
            _indexLim = Math.Max(_indLim, count);
            _counter = null;

            SignalReaders();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        Validation.BugCheckParam(count == indexLim, nameof(indexLim));
        Validation.BugCheckParam(indexLim >= _indLim, nameof(indexLim));
    }

    /// <summary>
    /// Called to receive a new item.
    /// </summary>
    private void Receive(long index, T value)
    {
        if (TryGetExpectedCount(out long expectedCount))
            Validation.BugCheckIndex(index, expectedCount, nameof(index));
        else
            Validation.BugCheckParam(index >= 0, nameof(index));

        _lock.EnterWriteLock();
        try
        {
            Validation.BugCheck(_ex == null);
            Validation.BugCheck(!_isDone);
            ReceiveCore(index, value);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Asserts that the write lock is held and that neither done nor quit have
    /// previously been called. This <b><em>may</em></b> throw. If so, the builder
    /// (if there is one) should be shut-down (with a quit operator).
    /// </summary>
    private void ReceiveCore(long index, T value)
    {
        Validation.Assert(_lock.IsWriteLockHeld);
        Validation.Assert(_ex == null);
        Validation.Assert(!_isDone);

        var igrp = index >> _logCbitGrp;
        uint bit = 1u << ((int)index & (_cbitGrp - 1));
        if (index < _cap)
        {
            // If the bit is already set, then this is a duplicate, which is illegal.
            Validation.BugCheckParam((_flags[igrp] & bit) == 0, nameof(index), "Duplicate index");
        }
        else
        {
            // Make sure our data structures are large enough.
            // REVIEW: Will we need to support larger than MaxInt? If so, how?
            Validation.CheckParam(index < int.MaxValue, nameof(index));

            int lenMin = (int)index + 1;
            int len = Util.GetCapTarget(_cap, lenMin);
            Util.Grow(ref _items, ref len, lenMin);
            Util.Grow(ref _inds, ref len, lenMin);
            Array.Resize(ref _flags, (len + _cbitGrp - 1) / _cbitGrp);
            _cap = len;
        }

        int ind = (int)index;
        Validation.Assert(ind == index);
        if (_indLim <= ind)
            _indLim = ind + 1;
        _inds[_count++] = ind;
        _items[ind] = value;
        _flags[igrp] |= bit;

        SignalReaders();
    }

    /// <summary>
    /// Set the events of the readers. This should only be called within a write lock.
    /// </summary>
    private void SignalReaders()
    {
        Validation.Assert(_lock.IsWriteLockHeld);

        // Signal to readers that something has changed.
        // REVIEW: What's the best way to do this?
        for (var link = Interlocked.Exchange(ref _evts, null); link != null; link = link.Next)
        {
            try
            {
                link.Evt.Set();
            }
            catch
            {
                // Can get here if the event has been finalized by the GC, which could happen when
                // we're in the finalizer of the builder (though unlikely).
            }
        }
    }

    /// <summary>
    /// Record the reader event, so we can set it (to unblock the reader) whenever anything changes.
    /// This should only be called within a read lock.
    /// </summary>
    private void AddReaderEvt(ManualResetEventSlim evt)
    {
        Validation.Assert(_lock.IsReadLockHeld);

        // Add it to the head of the linked list in a thread-safe way.
        for (; ; )
        {
            var next = _evts;
            var prev = Interlocked.CompareExchange(ref _evts, new EvtLink(evt, next), next);
            if (prev == next)
                break;
        }
    }

    /// <summary>
    /// Called by enumerators before waiting to give this some time, if needed.
    /// Generally, when using a builder, this does nothing. When using a source
    /// enumerator, we pull another item.
    /// </summary>
    protected virtual void Slice(ManualResetEventSlim evt)
    {
        Validation.AssertValue(evt);
        Validation.Assert(!_lock.IsReadLockHeld);
        Validation.Assert(!_lock.IsWriteLockHeld);
    }

    /// <summary>
    /// If building quit, throw.
    /// </summary>
    private void ThrowIfBad()
    {
        var ex = _ex;
        if (ex != null)
            throw new InvalidOperationException("Sequence abandoned", ex);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Gets a flag indicating whether the sequence is done building.
    /// </summary>
    public bool IsDone => _isDone;

    public bool TryGetCount(out long count)
    {
        _lock.EnterReadLock();
        try
        {
            ThrowIfBad();
            if (_isDone)
            {
                count = _indexLim;
                return true;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // If sequence is not done yet, try get expected count.
        if (TryGetExpectedCount(out long expectedCount))
        {
            count = expectedCount;
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

        using var evt = new ManualResetEventSlim();
        for (; ; )
        {
            callback?.Invoke();

            _lock.EnterReadLock();
            try
            {
                ThrowIfBad();
                if (_isDone)
                    return _indexLim;
                evt.Reset();

                // Need to wait.
                AddReaderEvt(evt);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Allow some work.
            Slice(evt);

            // Wait until something changes. On timeout, ping the callback and wait more.
            while (!evt.Wait(100))
                callback?.Invoke();
        }
    }

    private bool TryGetExpectedCount(out long expectedCount)
    {
        expectedCount = -1;
        if (!_canGetExpectedCount)
        {
            Validation.Assert(_countAhead < 0 && _counter == null);
            return false;
        }

        var cAhead = Interlocked.Read(ref _countAhead);
        if (cAhead >= 0)
        {
            expectedCount = cAhead;
            return true;
        }

        var ctr = _counter;
        if (ctr != null)
        {
            if (ctr.TryGetCount(out long count))
            {
                expectedCount = count;
                var cnt = Interlocked.Exchange(ref _countAhead, count);
                Validation.Assert(cnt == count || cnt < 0);
                _counter = null;
                return true;
            }
            return false;
        }

        /// Check again if <paramref name="countAhead"> would have set by <paramref name="counter"> in another thread.
        cAhead = Interlocked.Read(ref _countAhead);
        if (cAhead >= 0)
        {
            expectedCount = cAhead;
            return true;
        }

        return false;
    }

    IEnumerable? ICanSnap.Snap()
    {
        return Snap();
    }

    public IEnumerable<T>? Snap()
    {
        if (_isDone || _ex != null)
            return this;

        _lock.EnterReadLock();
        try
        {
            if (_isDone || _ex != null)
                return this;
            if (_count == 0)
                return null;

            var snapshot = _snapshot;
            if (snapshot != null && !TestBit(snapshot._lim))
                return snapshot;
            if (_count == _indLim)
                snapshot = new Snapshot(_items, _indLim);
            else
            {
                long iflag = snapshot == null ? 0 : (snapshot._lim >> _logCbitGrp);
                uint flag = 0;
                for (; iflag < _flags.Length; iflag++)
                {
                    flag = ~_flags[iflag];
                    if (flag != 0)
                        break;
                }
                long lim = (iflag << _logCbitGrp) + Util.IbitLow(flag);
                if (lim == 0)
                    return null;
                Validation.Assert(lim > (snapshot == null ? 0 : snapshot._lim));
                Validation.Assert(TestBit(lim - 1));
                Validation.Assert(!TestBit(lim));
                snapshot = new Snapshot(_items, lim);
            }

            Validation.Assert(snapshot != _snapshot);
            var cur = _snapshot;
            if (cur != null && cur._lim >= snapshot._lim)
                return cur;
            for (; ; )
            {
                Interlocked.CompareExchange(ref _snapshot, snapshot, cur);
                cur = _snapshot;
                Validation.AssertValue(cur);
                if (cur._lim >= snapshot._lim)
                    return cur;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get a standard (in order) enumerator. Each enumerator should be used on one thread
    /// at a time. Simultaneous access from multiple threads doesn't make sense.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        ThrowIfBad();
        // REVIEW: If the _isDone flag is set, use a simpler ator, since no
        // locking is required from then on.
        return new Ator(this);
    }

    /// <summary>
    /// Get a seekable enumerator. Each enumerator should be used on one thread
    /// at a time. Simultaneous access from multiple threads doesn't make sense.
    /// </summary>
    public ICursor<T> GetCursor()
    {
        ThrowIfBad();
        // REVIEW: If the _isDone flag is set, use a simpler cursor, since no
        // locking is required from then on.
        return new Ator(this);
    }

    /// <summary>
    /// Get an indexed (out of order) enumerator. Each enumerator should be used on one thread
    /// at a time. Simultaneous access from multiple threads doesn't make sense.
    /// </summary>
    public IIndexedEnumerator<T> GetIndexedEnumerator()
    {
        ThrowIfBad();
        // REVIEW: If the _isDone flag is set, use a simpler ator, since no
        // locking is required from then on.
        return new AtorInd(this);
    }

    /// <summary>
    /// Get a seekable non-generic enumerator. Each enumerator should be used on one thread
    /// at a time. Simultaneous access from multiple threads doesn't make sense.
    /// </summary>
    ICursor ICursorable.GetCursor()
    {
        return GetCursor();
    }

    /// <summary>
    /// Get an indexed (out of order) non-generic enumerator. Each enumerator should be used on one thread
    /// at a time. Simultaneous access from multiple threads doesn't make sense.
    /// </summary>
    IIndexedEnumerator IIndexedEnumerable.GetIndexedEnumerator()
    {
        return GetIndexedEnumerator();
    }

    private bool TestBit(long index)
    {
        Validation.Assert(index >= 0);

        if (index >= _indLim)
            return false;

        int igrp = (int)index >> _logCbitGrp;
        uint bit = 1u << ((int)index & (_cbitGrp - 1));
        return (_flags[igrp] & bit) != 0;
    }

    /// <summary>
    /// The indexed (out of order) enumerator. This should not be used simultaneously on multiple threads.
    /// It makes no sense to do so.
    /// </summary>
    private sealed class AtorInd : IIndexedEnumerator<T>
    {
        private readonly ManualResetEventSlim _evt;

        // The sequence. Set to null on dispose.
        private volatile IndexedSequence<T> _parent;

        /// <summary>
        /// The current position in <see cref="_inds"/>.
        /// </summary>
        private int _iindNext;

        // The current value and index, for the public properties.
        private T? _value;
        private long _index;

        public AtorInd(IndexedSequence<T> parent)
        {
            Validation.AssertValue(parent);
            _evt = new ManualResetEventSlim();
            _parent = parent;
        }

        public T Value => _value!;
        public long Index => _index;
        object? IIndexedEnumerator.Value => _value;

        public void Dispose()
        {
            _parent = null!;
            _evt.Set();
        }

        public bool MoveNext()
        {
            // REVIEW: This assumes that this enumerator isn't being used simultaneously
            // on multiple threads. Trying to do so would make no sense.
            for (; ; )
            {
                var parent = _parent;
                Validation.BugCheck(parent != null, "Disposed");

                parent._lock.EnterReadLock();
                try
                {
                    // If sequence building was quit, throw.
                    parent.ThrowIfBad();

                    // Reset the event.
                    _evt.Reset();

                    Validation.AssertIndexInclusive(_iindNext, parent._count);
                    if (_iindNext < parent._count)
                    {
                        int ind = parent._inds[_iindNext++];
                        Validation.Assert(parent.TestBit(ind));
                        _value = parent._items[ind];
                        _index = ind;
                        return true;
                    }
                    if (parent._isDone)
                        return false;

                    parent.AddReaderEvt(_evt);
                }
                finally
                {
                    parent._lock.ExitReadLock();
                }

                // Let the parent do some work, if needed.
                parent.Slice(_evt);

                // Wait until something changes.
                _evt.Wait();
            }
        }
    }

    /// <summary>
    /// The standard (in order) enumerator/cursor. This should not be used simultaneously on multiple threads.
    /// It makes no sense to do so.
    /// </summary>
    private sealed class Ator : ICursor<T>
    {
        private readonly ManualResetEventSlim _evt;

        // The sequence. Set to null on dispose.
        private volatile IndexedSequence<T> _parent;

        // The current index and value.
        private long _index;
        private T? _value;

        public Ator(IndexedSequence<T> parent)
        {
            Validation.AssertValue(parent);
            _evt = new ManualResetEventSlim();
            _parent = parent;
            _index = -1;
        }

        public T Current => _value!;

        public T Value => _value!;

        public long Index => _index;

        object? ICursor.Value => _value;

        object? IEnumerator.Current => _value;

        public void Dispose()
        {
            _parent = null!;
        }

        public bool MoveNext()
        {
            return MoveToCore(_index + 1, null);
        }

        public bool MoveTo(long index) => MoveTo(index, null);

        public bool MoveTo(long index, Action? callback)
        {
            Validation.BugCheckParam(index >= 0, nameof(index));
            Validation.BugCheckValueOrNull(callback);

            return MoveToCore(index, callback);
        }

        private bool MoveToCore(long index, Action? callback)
        {
            Validation.Assert(index >= 0);

            if (index == _index)
                return true;

            for (; ; )
            {
                var parent = _parent;
                Validation.BugCheck(parent != null, "Disposed");

                parent._lock.EnterReadLock();
                try
                {
                    // If sequence building was quit, throw.
                    parent.ThrowIfBad();

                    // Reset the event.
                    _evt.Reset();

                    if (parent.TestBit(index))
                    {
                        // Have the value.
                        _value = parent._items[_index = index];
                        return true;
                    }

                    if (parent._isDone)
                    {
                        if (index >= parent._indexLim)
                            return false;

                        // Value wasn't set. Yield the default.
                        _value = parent._valDef;
                        _index = index;
                        return true;
                    }

                    // Need to wait.
                    parent.AddReaderEvt(_evt);
                }
                finally
                {
                    parent._lock.ExitReadLock();
                }

                callback?.Invoke();

                // Let the parent do some work, if needed.
                parent.Slice(_evt);

                // Wait until something changes.
                _evt.Wait(100);
            }
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// A snapshot of the current sequence. Represents the continuous built
    /// prefix of the sequence.
    /// </summary>
    private sealed class Snapshot : IEnumerable<T>, IEnumerator<T>, ICursorable<T>, ICursor<T>, ICanCount, ICanSnap<T>
    {
        private readonly T[] _items;
        public readonly long _lim;

        private int _used; // 0 indicates this is safe to use as an enumerator.
        private long _index;

        public T Value => _items[_index];
        public T Current => Value;
        object? IEnumerator.Current => Value;

        object? ICursor.Value => Value;

        public long Index => _index;

        public Snapshot(T[] items, long lim)
            : this(items, lim, 0)
        {
        }

        private Snapshot(T[] items, long lim, int used)
        {
            Validation.AssertValue(items);
            Validation.AssertIndex(lim - 1, items.LongLength);
            _items = items;
            _lim = lim;
            _used = used;
            _index = -1;
        }

        public void Dispose()
        {
        }

        public bool TryGetCount(out long count)
        {
            count = _lim;
            return true;
        }

        public long GetCount(Action? callback) => _lim;

        public IEnumerator<T> GetEnumerator()
        {
            if (Interlocked.Exchange(ref _used, 1) == 0)
                return this;

            return new Snapshot(_items, _lim, 1);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public ICursor<T> GetCursor()
        {
            if (Interlocked.Exchange(ref _used, 1) == 0)
                return this;

            return new Snapshot(_items, _lim, 1);
        }

        ICursor ICursorable.GetCursor() => GetCursor();

        public bool MoveNext()
        {
            if (_index + 1 >= _lim)
                return false;

            _index++;
            return true;
        }

        public bool MoveTo(long index)
        {
            Validation.BugCheckParam(index >= 0, nameof(index));
            if (index >= _lim)
                return false;

            _index = index;
            return true;
        }

        public bool MoveTo(long index, Action? callback) => MoveTo(index);

        public void Reset() => throw new InvalidOperationException();

        public IEnumerable<T>? Snap() => this;
        IEnumerable? ICanSnap.Snap() => this;
    }
}
