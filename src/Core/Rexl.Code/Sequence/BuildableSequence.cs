// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// This is a thread safe wrapper around <see cref="GrowableArray{T}"/>. It is a sequence that can be built
/// on one thread (by adding items to a builder) while simultaneously read from any number of threads.
/// Reading options include both <see cref="IEnumerator{T}"/> (in-order iteration) and <see cref="ICursor{T}"/>
/// (indexed iteration in any order).The enumerator/cursor <c>MoveX</c> methods block until the needed item
/// is available. These methods will throw if the builder is disposed or garbage collected before calling
/// <see cref="Builder.Done()"/>.
/// </summary>
public sealed partial class BuildableSequence<T> : ICursorable<T>, ICanCount, ICanSnap<T>
{
    /// <summary>
    /// The reader-writer lock protecting the internal growable array.
    /// </summary>
    private readonly ReaderWriterLockSlim _lock;

    /// <summary>
    /// The items as a growable array. Note that growable array is not thread safe, so this
    /// is protected by the <see cref="_lock"/>.
    /// </summary>
    private readonly GrowableArray<T> _items;

    /// <summary>
    /// The chain of reset events to set whenever anything changes. These unblock active enumerators/readers.
    /// This is protected via interlocked operations, NOT by the <see cref="_lock"/>.
    /// </summary>
    private volatile EvtLink? _evts;

    /// <summary>
    /// When building is shut down before <see cref="Builder.Done"/> is called, an exception is recorded here.
    /// If this happens the sequence is considered "bad"/"corrupt".
    /// Protected by the <see cref="_lock"/>.
    /// </summary>
    private Exception? _ex;

    /// <summary>
    /// Whether building is done (successfully).
    /// Protected by the <see cref="_lock"/>.
    /// </summary>
    private bool _isDone;

    private BuildableSequence(long citemInit)
    {
        _lock = new ReaderWriterLockSlim();
        _items = GrowableArray.Create<T>(citemInit);
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
            // REVIEW: Is this needed? Not that it costs much. In theory, we shouldn't
            // ever hand this out so it shouldn't really matter.
            _items.Seal();
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
    private void DoneBuilding()
    {
        _lock.EnterWriteLock();
        try
        {
            Validation.Assert(_ex == null);
            Validation.Assert(!_isDone);
            _items.Seal();
            _isDone = true;
            SignalReaders();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Called to receive a new item.
    /// </summary>
    private long Receive(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            Validation.BugCheck(_ex == null);
            Validation.BugCheck(!_isDone);

            long index = _items.Add(item);
            SignalReaders();
            return index;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Called to receive some new items.
    /// </summary>
    private long Receive(ReadOnlySpan<T> items)
    {
        _lock.EnterWriteLock();
        try
        {
            Validation.BugCheck(_ex == null);
            Validation.BugCheck(!_isDone);

            long index = _items.AddMulti(items);
            SignalReaders();
            return index;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
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
    /// If building quit, throw.
    /// </summary>
    private void ThrowIfBad()
    {
        var ex = _ex;
        if (ex != null)
            throw new InvalidOperationException("Sequence abandoned", ex);
    }

    public IEnumerable<T>? Snap()
    {
        _lock.EnterReadLock();
        try
        {
            ThrowIfBad();
            if (_items.Count == 0)
                return null;
            // REVIEW: Should we cache the current one?
            return _items.Snap();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable? ICanSnap.Snap() => Snap();

    public bool TryGetCount(out long count)
    {
        _lock.EnterReadLock();
        try
        {
            ThrowIfBad();
            if (_isDone)
            {
                count = _items.Count;
                return true;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        count = -1;
        return false;
    }

    public long GetCount(Action? callback)
    {
        Validation.BugCheckValueOrNull(callback);

        _lock.EnterReadLock();
        try
        {
            ThrowIfBad();
            if (_isDone)
                return _items.Count;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        using var evt = new ManualResetEventSlim();
        for (; ; )
        {
            callback?.Invoke();

            _lock.EnterReadLock();
            try
            {
                ThrowIfBad();
                if (_isDone)
                    return _items.Count;
                evt.Reset();

                // Need to wait.
                AddReaderEvt(evt);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Wait until something changes. On timeout, ping the callback and wait more.
            while (!evt.Wait(100))
                callback?.Invoke();
        }
    }

    /// <summary>
    /// We keep a linked list of events. The events are to unblock active readers (enumerators/cursors).
    /// </summary>
    private sealed class EvtLink
    {

        /// <summary>
        /// Note that this class doesn't own the event - it is owned by a "reader", aka enumerator/cursor.
        /// </summary>
        public readonly ManualResetEventSlim Evt;

        /// <summary>
        /// The next event in the linked list.
        /// </summary>
        public readonly EvtLink? Next;

        public EvtLink(ManualResetEventSlim evt, EvtLink? next)
        {
            Validation.AssertValue(evt);
            Evt = evt;
            Next = next;
        }
    }
}

// This partial contains the builder.
partial class BuildableSequence<T>
{
    /// <summary>
    /// The builder for a <see cref="BuildableSequence{T}"/>. Call <see cref="Add(T)"/> to add items.
    /// Call <see cref="Done"/> when finished adding items. If the <see cref="Builder"/> is disposed before
    /// <see cref="Done"/> is called, the sequence is considered "corrupt" and all future access (and any
    /// current enumerators) will throw.
    /// </summary>
    public sealed class Builder
    {
        /// <summary>
        /// The sequence. Set to null when disposed or <see cref="Done"/> is called.
        /// </summary>
        private volatile BuildableSequence<T>? _seq;

        /// <summary>
        /// Create a <see cref="BuildableSequence{T}"/> and associated <see cref="Builder"/>. The sequence
        /// can immediately be passed to client code while the builder is used on a separate thread to add
        /// items to the sequence and complete the sequence. The client code should always call either
        /// <see cref="Done"/> when building completed successfully or <see cref="Quit(Exception)"/> when
        /// an error happened while building.
        /// </summary>
        public static Builder Create(long citemInit, out BuildableSequence<T> seq)
        {
            seq = new BuildableSequence<T>(citemInit);
            return new Builder(seq);
        }

        private Builder(BuildableSequence<T> seq)
        {
            Validation.AssertValue(seq);
            _seq = seq;
        }

        // REVIEW: Should we really have this? All clients should really call either Done or Quit,
        // in which case this isn't needed. Of course, those also suppress finalization so this won't be
        // invoked when the client does the right thing.
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
        public bool IsActive => _seq != null;

        /// <summary>
        /// Add an item to the sequence. Returns the index of the item.
        /// </summary>
        public long Add(T item)
        {
            // Note that there is a race condition here - Receive may be called after DoneBuilding
            // or QuitBuilding is called. The sequence object needs to deal with that possibility.
            var seq = _seq;
            Validation.BugCheck(seq != null, "Builder inactive");

            try
            {
                return seq.Receive(item);
            }
            catch (Exception ex)
            {
                // Shut down building, if it isn't already. Use `Quit` so the finalizer is suppressed.
                Quit(ex);
                throw;
            }
        }

        /// <summary>
        /// Add multiple items to the sequence. Returns the index of the first item added.
        /// </summary>
        public long AddMulti(ReadOnlySpan<T> items)
        {
            // Note that there is a race condition here - Receive may be called after DoneBuilding
            // or QuitBuilding is called. The sequence object needs to deal with that possibility.
            var seq = _seq;
            Validation.BugCheck(seq != null, "Builder inactive");
            Validation.BugCheckParam(items.Length > 0, nameof(items));

            try
            {
                return seq.Receive(items);
            }
            catch (Exception ex)
            {
                // Shut down building, if it isn't already. Use `Quit` so the finalizer is suppressed.
                Quit(ex);
                throw;
            }
        }

        /// <summary>
        /// Complete successful building of the sequence.
        /// </summary>
        public void Done()
        {
            var seq = Interlocked.Exchange(ref _seq, null);
            if (seq != null)
                seq.DoneBuilding();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Complete failed building of the sequence.
        /// </summary>
        public void Quit(Exception ex)
        {
            var seq = Interlocked.Exchange(ref _seq, null);
            if (seq != null)
                seq.QuitBuilding(ex);
            GC.SuppressFinalize(this);
        }
    }
}

// This partial is for iteration (cursor and enumerator functionality).
partial class BuildableSequence<T>
{
    /// <summary>
    /// Get a seekable enumerator. Each enumerator should be used on one thread
    /// at a time. Simultaneous access from multiple threads doesn't make sense.
    /// </summary>
    public ICursor<T> GetCursor()
    {
        // REVIEW: Do we really need this read lock?
        bool done;
        _lock.EnterReadLock();
        try
        {
            done = _isDone;
            ThrowIfBad();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        if (done)
            return _items.GetCursor();

        return new AtorGen(this);
    }

    ICursor ICursorable.GetCursor() => GetCursor();

    public IEnumerator<T> GetEnumerator()
    {
        // REVIEW: Do we really need this read lock?
        bool done;
        _lock.EnterReadLock();
        try
        {
            done = _isDone;
            ThrowIfBad();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // This differs from GetCursor here - the growable array's enumerator implementation
        // may be simpler than its cursor implementation.
        if (done)
            return _items.GetEnumerator();

        return new AtorGen(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// An instance of this should not be used simultaneously on multiple threads.
    /// It makes no sense to do so.
    /// </summary>
    private sealed class AtorGen : ICursor<T>
    {
        // The sequence and its item cursor.
        // WARNING: These are typed as non-nullable. However Dispose sets them to null, after
        // which uses of them will throw a null reference exception. The totally proper way to
        // do this is to mark these as nullable and make the property getters test for null and
        // throw an object disposed exception. However, that is extra run-time cost that we don't
        // want to pay. Since 99% of the time enumerators are handled by foreach and anyone that
        // is using a cursor needs to be super careful, the downside of throwing null-ref instead
        // of object-disposed is worth the savings.
        private volatile BuildableSequence<T> _parent;
        private volatile ICursor<T> _inner;

        // To synchronize with the parent (wait for more items).
        private readonly ManualResetEventSlim _evt;

        public long Index => _inner.Index;
        public T Value => _inner.Value;
        object? ICursor.Value => _inner.Value;
        public T Current => _inner.Current;
        object? IEnumerator.Current => _inner.Current;

        public AtorGen(BuildableSequence<T> parent)
        {
            Validation.AssertValue(parent);
            _parent = parent;
            _inner = parent._items.GetCursor();
            _evt = new ManualResetEventSlim();
        }

        public void Dispose()
        {
            // See the comment on these fields about why we are doing this. Generally null! should
            // be considered a no-no, but the alternative (testing for null and throwing disposed
            // exception in the property getters) is too costly.
            _inner?.Dispose();
            _inner = null!;
            _parent = null!;

            // Note that this event may still be held by the sequence (to "signal readers"), but
            // that code has try-catch around setting this event, so this dispose should be ok.
            _evt.Dispose();
        }

        public bool MoveNext()
        {
            return MoveToCore(_inner.Index + 1, null);
        }

        public bool MoveTo(long index)
        {
            Validation.BugCheckParam(index >= 0, nameof(index));
            return MoveToCore(index, null);
        }

        public bool MoveTo(long index, Action? callback)
        {
            Validation.BugCheckParam(index >= 0, nameof(index));
            Validation.BugCheckValueOrNull(callback);

            return MoveToCore(index, callback);
        }

        /// <summary>
        /// This can block if the index is not yet available.
        /// </summary>
        private bool MoveToCore(long index, Action callback)
        {
            Validation.Assert(index >= 0);
            if (index == _inner.Index)
                return true;

            for (; ; )
            {
                var parent = _parent;
                var inner = _inner;
                Validation.BugCheck(parent != null, "Disposed");
                Validation.BugCheck(inner != null, "Disposed");

                parent._lock.EnterReadLock();
                try
                {
                    // If sequence building was quit, throw.
                    parent.ThrowIfBad();

                    // Reset the event.
                    _evt.Reset();

                    if (index < parent._items.Count)
                    {
                        // Don't need to pass the callback as this should be immediate.
                        inner.MoveTo(index);
                        return true;
                    }

                    Validation.Assert(index >= parent._items.Count);
                    if (parent._isDone)
                        return false;

                    // Need to wait.
                    parent.AddReaderEvt(_evt);
                }
                finally
                {
                    parent._lock.ExitReadLock();
                }

                callback?.Invoke();

                // Wait until something changes.
                // REVIEW: Should we use a timeout? If so, what value?
                _evt.Wait(100);
            }
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }
    }
}
