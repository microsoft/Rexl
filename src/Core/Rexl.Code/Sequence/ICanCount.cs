// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

/// <summary>
/// An interface for collection-like things that may be able to count their items.
/// </summary>
public interface ICanCount
{
    /// <summary>
    /// If this object already knows its count, sets <paramref name="count"/> and returns true.
    /// Otherwise, returns false.
    /// </summary>
    bool TryGetCount(out long count);

    /// <summary>
    /// Return the count, computing it if it isn't already know. Calls the optional <paramref name="callback"/>
    /// repeatedly while doing "work" (counting items). It is expected that if the caller wants to abort
    /// the operation, the <paramref name="callback"/> will throw when called.
    /// </summary>
    long GetCount(Action? callback);
}

public static class WrapWithCount
{
    /// <summary>
    /// Wraps a sequence together with the number of items in the sequence, which is
    /// <paramref name="count"/> plus the optional <paramref name="delta"/>. If the source
    /// sequence implements <see cref="ICachingEnumerable"/> then the result does also.
    /// </summary>
    public static IEnumerable<T>? Create<T>(long count, IEnumerable<T> seq, long delta = 0)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));
        Validation.BugCheckValue(seq, nameof(seq));

        if (delta != 0)
            count = AdjustCount(count, delta);
        if (count <= 0)
            return null;
        if (seq is ICachingEnumerable<T> cseq)
            return new CachingWithCount<T>(count, cseq);
        return new WithCount<T>(count, seq);
    }

    /// <summary>
    /// Wraps a sequence together with the number of items in the sequence, which is
    /// <paramref name="count"/> plus the optional <paramref name="delta"/>.
    /// </summary>
    public static ICachingEnumerable<T>? Create<T>(long count, ICachingEnumerable<T> seq, long delta = 0)
    {
        Validation.BugCheckParam(count >= 0, nameof(count));
        Validation.BugCheckValue(seq, nameof(seq));

        if (delta != 0)
            count = AdjustCount(count, delta);
        if (count <= 0)
            return null;
        return new CachingWithCount<T>(count, seq);
    }

    /// <summary>
    /// Wraps a sequence together with an implementation of <see cref="ICanCount"/> and optional
    /// <paramref name="delta"/>. The number of items in the result is the number of items in
    /// <paramref name="counter"/> plus <paramref name="delta"/>. If the source sequence implements
    /// <see cref="ICachingEnumerable"/> then the result does also.
    /// </summary>
    public static IEnumerable<T>? Create<T>(ICanCount counter, IEnumerable<T> seq, long delta = 0)
    {
        Validation.BugCheckValue(counter, nameof(counter));
        Validation.BugCheckValue(seq, nameof(seq));

        if (counter.TryGetCount(out long count))
            return Create(count, seq, delta);

        if (seq is ICachingEnumerable<T> cseq)
            return new CachingWithCanCount<T>(counter, cseq, delta);
        return new WithCanCount<T>(counter, seq, delta);
    }

    /// <summary>
    /// Adjust the given <paramref name="count"/> by <paramref name="delta"/> (adding). If the result is
    /// negative, return <see cref="long.MaxValue"/> on overflow and zero otherwise.
    /// </summary>
    private static long AdjustCount(long count, long delta)
    {
        Validation.Assert(count >= 0);

        long tot = count + delta;
        if (tot >= 0)
            return tot;

        // Test for overflow.
        if (delta > 0)
            return long.MaxValue;
        return 0;
    }

    private abstract class WithCountBase<T> : IEnumerable<T>, ICanCount
    {
        private readonly IEnumerable<T> _seq;
        private readonly long _count;

        protected WithCountBase(long count, IEnumerable<T> seq)
        {
            Validation.Assert(count >= 0);
            Validation.AssertValue(seq);
            _count = count;
            _seq = seq;
        }

        public bool TryGetCount(out long count)
        {
            count = _count;
            return true;
        }

        public long GetCount(Action? callback)
        {
            Validation.BugCheckValueOrNull(callback);
            return _count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _seq.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class WithCount<T> : WithCountBase<T>
    {
        public WithCount(long count, IEnumerable<T> seq)
            : base(count, seq)
        {
        }
    }

    private sealed class CachingWithCount<T> : WithCountBase<T>, ICachingEnumerable<T>
    {
        public CachingWithCount(long count, ICachingEnumerable<T> seq)
            : base(count, seq)
        {
        }
    }

    private abstract class WithCanCountBase<T> : IEnumerable<T>, ICanCount
    {
        private readonly ICanCount _counter;
        private readonly long _delta;
        private readonly IEnumerable<T> _seq;

        protected WithCanCountBase(ICanCount counter, IEnumerable<T> seq, long delta)
        {
            Validation.AssertValue(counter);
            Validation.AssertValue(seq);
            _counter = counter;
            _delta = delta;
            _seq = seq;
        }

        public bool TryGetCount(out long count)
        {
            if (!_counter.TryGetCount(out long src))
            {
                count = -1;
                return false;
            }

            count = AdjustCount(src, _delta);
            return true;
        }

        public long GetCount(Action? callback)
        {
            Validation.BugCheckValueOrNull(callback);

            long src = _counter.GetCount(callback);
            Validation.Assert(src >= 0);
            return AdjustCount(src, _delta);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _seq.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class WithCanCount<T> : WithCanCountBase<T>
    {
        public WithCanCount(ICanCount counter, IEnumerable<T> seq, long delta)
             : base(counter, seq, delta)
        {
        }
    }

    private sealed class CachingWithCanCount<T> : WithCanCountBase<T>, ICachingEnumerable<T>
    {
        public CachingWithCanCount(ICanCount counter, ICachingEnumerable<T> seq, long delta)
            : base(counter, seq, delta)
        {
        }
    }
}
