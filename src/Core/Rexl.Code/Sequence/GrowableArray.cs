// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// This implements a growable array where items can be added at the end (one at a time or from
/// a span of items). Once written, items do not change. Items can be read with the indexer or
/// with GetEnumerator. There is a general implementation as well as several optimized ones
/// (for nullable types, and bool types). To create one use <see cref="GrowableArray.Create{T}(long)"/>
/// or <see cref="GrowableArray.CreateNullable{T}(long)"/>.
/// 
/// Note: this is NOT thread safe for simultaneous building and reading. Client code must ensure
/// proper locks are taken. Of course it supports multiple simultaneous readers, but not simultaneous
/// writing and reading. Because of this, it is best to only expose snapshotted instances to the
/// rexl world. <see cref="BuildableSequence{T}"/> is a thread-safe wrapper.
/// </summary>
public abstract class GrowableArray<T> : IEnumerable<T>, ICursorable<T>, ICanCount, ICanSnap<T>
{
    /// <summary>
    /// Returns the current number of items.
    /// </summary>
    public abstract long Count { get; }

    /// <summary>
    /// The current capacity.
    /// </summary>
    public abstract long Capacity { get; }

    /// <summary>
    /// Whether this is an immutable snapshot.
    /// </summary>
    public abstract bool IsSealed { get; }

    protected GrowableArray()
    {
    }

    /// <summary>
    /// Converts this instance to a "snapshot", so it can no longer be changed.
    /// </summary>
    public abstract void Seal();

    /// <summary>
    /// Returns an immutable snapshot of this array.
    /// </summary>
    public GrowableArray<T> Snap()
    {
        if (IsSealed)
            return this;
        return SnapCore();
    }

    IEnumerable<T>? ICanSnap<T>.Snap() => Snap();
    IEnumerable? ICanSnap.Snap() => Snap();

    /// <summary>
    /// Protected method to do the real work for <see cref="Snap"/>. This is a separate
    /// method so subclasses can both implement the core functionality of and "hide"
    /// <see cref="Snap"/> with one having more specific return type.
    /// </summary>
    protected abstract GrowableArray<T> SnapCore();

    /// <summary>
    /// Add an item to the array. If this is a snapshot it throws.
    /// </summary>
    public abstract long Add(T item);

    /// <summary>
    /// Add multiple items.
    /// </summary>
    public abstract long AddMulti(ReadOnlySpan<T> items);

    /// <summary>
    /// Indexer to access a particular item.
    /// </summary>
    public abstract T this[long index] { get; }

    /// <summary>
    /// Get an enumerator for the items.
    /// </summary>
    public abstract IEnumerator<T> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Get a cursor for the items.
    /// </summary>
    public virtual ICursor<T> GetCursor() => new Cursor(this);

    ICursor ICursorable.GetCursor() => GetCursor();

    /// <summary>
    /// Explicit implementation of <see cref="ICanCount"/>. Note that a non-snap instance is
    /// not guaranteed to be immutable, so best to only expose to the rexl world a snapped
    /// instance of this.
    /// </summary>
    bool ICanCount.TryGetCount(out long count)
    {
        count = Count;
        return true;
    }

    long ICanCount.GetCount(Action? callback) => Count;

    protected sealed class Cursor : ICursor<T>
    {
        // The sequence. Set to null on dispose.
        private GrowableArray<T> _parent;

        // The current item index and value.
        private long _index;
        private T? _value;

        public Cursor(GrowableArray<T> parent)
        {
            Validation.AssertValue(parent);
            _parent = parent;
            _index = -1;
        }

        public long Index => _index;
        public T Value => _value!;
        object? ICursor.Value => Value;
        public T Current => Value;
        object? IEnumerator.Current => Value;

        public void Dispose()
        {
            _parent = null!;
        }

        public bool MoveNext()
        {
            return MoveToCore(_index + 1);
        }

        public bool MoveTo(long index)
        {
            Validation.BugCheckParam(index >= 0, nameof(index));
            return MoveToCore(index);
        }

        // Since GrowableArray doesn't permit simultaneous growing and reading,
        // the callback is ignored.
        public bool MoveTo(long index, Action? callback) => MoveTo(index);

        private bool MoveToCore(long index)
        {
            Validation.Assert(index >= 0);
            var parent = _parent;
            Validation.BugCheck(parent != null, "Disposed");

            if (index == _index)
                return true;
            if (index >= parent.Count)
                return false;

            _value = parent[index];
            _index = index;
            return true;
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }
    }
}

/// <summary>
/// Contains static methods to create <see cref="GrowableArray{T}"/> instances.
/// </summary>
public static class GrowableArray
{
    private static MethodInfo s_methNullable = new Func<long, GrowableArray<int?>>(CreateNullable<int>)
        .Method.GetGenericMethodDefinition();

    /// <summary>
    /// Create a growable array for the given type and with the given capacity hint. Note that
    /// there is no guarantee that the capacity hint will end up being the actual capacity.
    /// </summary>
    public static GrowableArray<T> Create<T>(long capHint)
    {
        // Deal with special cases.
        var st = typeof(T);

        if (st == typeof(bool))
            return (GrowableArray<T>)(object)new ImplBit(capHint);
        if (st == typeof(bool?))
            return (GrowableArray<T>)(object)new ImplBitNullable(capHint);

        if (st.IsValueType && st.IsGenericType && st.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            st = st.GetGenericArguments()[0];
            var meth = s_methNullable.MakeGenericMethod(st);
            return (GrowableArray<T>)meth.Invoke(null, new object[] { capHint })!;
        }

        return new Impl<T>(capHint);
    }

    /// <summary>
    /// Create a growable array for the given nullable type and with the given capacity hint. Note that
    /// there is no guarantee that the capacity hint will end up being the actual capacity.
    /// Note: calling this rather than <see cref="Create{T}(long)"/> is significantly more efficient when
    /// creating the array but they both produce the same kind of array in the end.
    /// </summary>
    public static GrowableArray<T?> CreateNullable<T>(long capHint)
        where T : struct
    {
        if (typeof(T) == typeof(bool))
            return (GrowableArray<T?>)(object)new ImplBitNullable(capHint);
        return new ImplNullable<T>(capHint);
    }

    /// <summary>
    /// General implementation.
    /// </summary>
    private sealed class Impl<T> : GrowableArray<T>
    {
        // Explanation of buffers, buffer groups, and their sizes:
        // * k_numVvvXxx values are 2 raised to the corresponding k_shfVvvXxx value, where vvv is the thing
        //   being counted in the container of type xxx. Eg, k_numValBufFirst is the number of values in the
        //   first buffer and equals 1 << k_shfValBufFirst.
        // * Items are stored in item buffers.
        // * Buffers are organized into buffer groups. Each group except the final consists of k_numBufGrp
        //   buffers. The final group can contain an arbitrary number of buffers.
        // * The buffers in a group are of the same size.
        // * The buffer sizes for the first two groups (groups 0 and 1) are the same, namely k_numValBuf.
        // * For subsequent groups, the buffer size is twice the buffer size for the previous group. For
        //   example, the buffer size for group 2 is 2 * k_numValBufFirst and the buffer size for group 3
        //   is 4 * k_numValBufFirst.
        // * The groups before the final are called the "ramp", since the buffer sizes "ramp up" across these.
        // * Except for the first group and last groups, the capacity (max number of values) in the group equals
        //   the total capacity of all previous groups.

        // Shift and num for values in the first buffer group.
        private const int k_shfValBufFirst = 7;
        private const int k_numValBufFirst = 1 << k_shfValBufFirst;
        // Shift and num for values in the last buffer group.
        private const int k_shfValBufLast = 16;
        private const int k_numValBufLast = 1 << k_shfValBufLast;
        // Number of groups in the ramp (not counting the last group).
        private const int k_numGrpRamp = k_shfValBufLast - k_shfValBufFirst + 1;

        // Shift and num for buffers in a group (except the last).
        private const int k_shfBufGrp = 3;
        private const int k_numBufGrp = 1 << k_shfBufGrp;

        // Number of values in the first group.
        private const int k_numValGrpFirst = k_numValBufFirst << k_shfBufGrp;
        // Number of values in the "ramp". The number of values in the ramp is the same as 
        private const int k_numValRamp = k_numValBufLast << k_shfBufGrp;

        // Number of buffers in the "ramp" (not counting the last group).
        private const int k_numBufRamp = k_numBufGrp * (k_shfValBufLast - k_shfValBufFirst + 1);

        /// <summary>
        /// Whether this instance is "sealed". Can only transition from false to true.
        /// </summary>
        private bool _sealed;

        /// <summary>
        /// The buffers.
        /// </summary>
        private T[][] _bufs;

        /// <summary>
        /// The number of active buffers.
        /// </summary>
        private int _cbuf;

        /// <summary>
        /// The current capacity.
        /// </summary>
        private long _cap;

        /// <summary>
        /// The number of items.
        /// </summary>
        private long _count;

        /// <summary>
        /// The number of items in the last active buffer.
        /// </summary>
        private int _countLast;

        public override long Count => _count;

        public override bool IsSealed => _sealed;

        public override long Capacity => _cap;

        public Impl(long capHint)
            : base()
        {
            int cbufAlloc;
            long numFirst;
            if (capHint <= k_numValBufFirst >> 1)
            {
                // Start with a small first buffer.
                cbufAlloc = 1;
                numFirst = Math.Max(capHint, 4);
            }
            else
            {
                long ibuf = FindIbuf(capHint - 1, out _);
                cbufAlloc = (int)Math.Min(ibuf + 1, k_numBufRamp);
                numFirst = k_numValBufFirst;
            }
            _bufs = new T[cbufAlloc][];
            _bufs[0] = new T[numFirst];
            _cbuf = 1;
            _cap = numFirst;

            AssertValid();
        }

        private Impl(Impl<T> src)
            : base()
        {
            src.AssertValid();
            Validation.Assert(!src._sealed);

            _sealed = true;
            _bufs = src._bufs;
            _cbuf = src._cbuf;
            _cap = src._cap;
            _count = src._count;
            _countLast = src._countLast;

            AssertValid();
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
#if DEBUG
            Validation.Assert(_bufs != null);
            Validation.AssertIndex(_cbuf - 1, _bufs.Length);

            Validation.Assert(_bufs[0] != null);
            if (_cbuf == 1)
            {
                Validation.Assert(_bufs[0].Length <= k_numValBufFirst);
                Validation.Assert(_countLast == _count);
                // The capacity can be less then _bufs[0].Length when this is a clone of a non-sealed
                // instance that then grows _bufs[0]. Note that when that instance does grow _bufs[0],
                // the old and new buffers will be the same in the slots that this instance uses.
                Validation.Assert(_cap <= _bufs[0].Length);
                Validation.Assert(_cap == _bufs[0].Length | _sealed);
            }
            else
            {
                // Validate buffers in the "ramp".
                int numBufRamp = Math.Min(k_numBufRamp, _cbuf);
                int numGrpRamp = numBufRamp >> k_shfBufGrp;
                long numValBuf = k_numValBufFirst;
                for (int ibuf = 0; ibuf < numBufRamp; ibuf++)
                {
                    if ((ibuf & (k_numBufGrp - 1)) == 0 && ibuf >= 2 * k_numBufGrp)
                    {
                        // Adjust the buffer size.
                        numValBuf <<= 1;
                    }
                    Validation.Assert(_bufs[ibuf] != null);
                    Validation.Assert(_bufs[ibuf].Length == numValBuf);
                }

                // All buffers in the last group should be of max buffer size.
                for (int ibuf = k_numBufRamp; ibuf < _cbuf; ibuf++)
                {
                    Validation.Assert(_bufs[ibuf] != null);
                    Validation.Assert(_bufs[ibuf].Length == k_numValBufLast);
                }

                // Compute the capacity.
                long cap = 0;
                int ibufLast = _cbuf - 1;
                int igrp = Math.Min(ibufLast >> k_shfBufGrp, k_numGrpRamp);
                numValBuf = k_numValBufFirst;
                if (igrp > 0)
                {
                    numValBuf <<= (igrp - 1);
                    // The total capacity of all groups before this one equals the max capacity of this group.
                    cap += numValBuf << k_shfBufGrp;
                }
                Validation.Assert(_bufs[ibufLast].Length == numValBuf);

                // Add in the actual capacity of this group, based on the number of buffers in it.
                int numBufGrp = _cbuf - (igrp << k_shfBufGrp);
                if (igrp < k_numGrpRamp)
                    Validation.Assert(1 <= numBufGrp & numBufGrp <= k_numBufGrp);
                else
                {
                    // There's no limit on the number of buffers in the last group.
                    Validation.Assert(numBufGrp >= 1);
                    Validation.Coverage(numBufGrp > k_numBufGrp ? "big" : "small");
                }

                cap += numBufGrp * numValBuf;

                Validation.Assert(_cap == cap);
                Validation.Assert(numValBuf - _countLast == _cap - _count);
            }
#endif
        }

        /// <summary>
        /// Returns an immutable snapshot of this array.
        /// </summary>
        public new Impl<T> Snap()
        {
            if (_sealed)
                return this;
            return new Impl<T>(this);
        }

        protected override GrowableArray<T> SnapCore()
        {
            Validation.Assert(!_sealed);
            return new Impl<T>(this);
        }

        public override void Seal()
        {
            // REVIEW: Should this also "close up" when there is excessive spare capacity?
            _sealed = true;
        }

        public override long Add(T item)
        {
            AssertValid();
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");

            var index = _count;
            if (index >= _cap)
            {
                Grow();
                Validation.Assert(_count < _cap);
            }

            _bufs[_cbuf - 1][_countLast] = item;
            _countLast++;
            _count++;

            AssertValid();
            return index;
        }

        public override long AddMulti(ReadOnlySpan<T> items)
        {
            AssertValid();
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");

            var index = _count;
            while (items.Length > 0)
            {
                if (_count >= _cap)
                    Grow();
                var free = _bufs[_cbuf - 1].Length - _countLast;
                Validation.Assert(free == _cap - _count);
                var num = Math.Min(free, items.Length);
                Validation.Assert(num > 0);
                items.Slice(0, num).CopyTo(_bufs[_cbuf - 1].AsSpan(_countLast, num));
                _countLast += num;
                _count += num;
                items = items.Slice(num);
            }

            AssertValid();
            return index;
        }

        public override T this[long index]
        {
            get
            {
                AssertValid();
                Validation.BugCheckIndex(index, _count, nameof(index));

                long ibuf = FindIbuf(index, out int indRel);
                Validation.AssertIndex(ibuf, _cbuf);
                Validation.AssertIndex(indRel, _bufs[ibuf].Length);
                return _bufs[ibuf][indRel];
            }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            AssertValid();

            long count = _count;
            if (count == 0)
                yield break;

            var bufs = _bufs;
            int cbuf = _cbuf;
            long index = 0;
            for (int ibuf = 0; ; ibuf++)
            {
                Validation.Assert(ibuf < cbuf);
                var buf = bufs[ibuf];
                Validation.Assert(buf != null);
                for (int iv = 0; iv < buf.Length; iv++)
                {
                    yield return buf[iv];
                    if (++index >= count)
                        yield break;
                }
            }
        }

        /// <summary>
        /// Find the buffer index for the given item index. Also set <paramref name="ival"/> to the
        /// index of the value within that buffer.
        /// </summary>
        private static long FindIbuf(long index, out int ival)
        {
            Validation.Assert(index >= 0);

            if (index < k_numValGrpFirst)
            {
                // In the first group, when igrp is zero. This is the most common case.
                ival = (int)index & (k_numValBufFirst - 1);
                return index >> k_shfValBufFirst;
            }

            // Binary search to find the group. Note that there are currently 10 possible
            // outcomes, so this does at most 4 iterations. Of course, if the constants
            // are changed, the max number of iterations would also change, but the number of
            // iterations would still be log_2(k). For fixed values, this loop could be a set
            // of nested if's.
            // The notation "hgrp" means one less than a group index ("igrp - 1"), so hgrp zero
            // is igrp one. The case when igrp is zero is handled above.
            int hgrp = 0;
            int hgrpLim = k_numGrpRamp - 1;
            // Start with the buffer index computed as if all the buffers were of the smallest size.
            // We'll adjust this later.
            long ibuf = index >> k_shfValBufFirst;
            // Divide by the number of buffers per group to get a target number of "smallest" groups
            // to find.
            long key = ibuf >> k_shfBufGrp;
            // Do the binary search.
            while (hgrp < hgrpLim)
            {
                int hgrpMid = (hgrp + hgrpLim) >> 1;
                long keyMid = 2L << hgrpMid;
                if (key >= keyMid)
                    hgrp = hgrpMid + 1;
                else
                    hgrpLim = hgrpMid;
            }
            Validation.Assert(hgrp == hgrpLim);
            Validation.Assert(index >= (long)k_numValGrpFirst << hgrp);
            Validation.Assert(index < (long)k_numValGrpFirst << (hgrp + 1) | hgrp == k_numGrpRamp - 1);

            // Update the buffer index. First adjust for the increased buffer size.
            ibuf >>= hgrp;
            // Now adjust for the actual number of buffers in previous groups.
            ibuf += hgrp << k_shfBufGrp;

            // Get number of values in this buffer and mask to get the index within the buffer.
            var numValBuf = k_numValBufFirst << hgrp;
            ival = (int)index & (numValBuf - 1);
            return ibuf;
        }

        private void Grow()
        {
            AssertValid();
            Validation.Assert(_cap == _count);

            if (_cbuf == 1 && _bufs[0].Length < k_numValBufFirst)
            {
                int cval = Math.Min((int)_count * 2, k_numValBufFirst);
                Array.Resize(ref _bufs[0], cval);
                _cap = cval;
                Validation.Assert(_cap > _count);
            }
            else
            {
                Validation.Assert(_countLast == _bufs[_cbuf - 1].Length);
                if (_bufs.Length <= _cbuf)
                    Array.Resize(ref _bufs, 2 * _cbuf);

                int ibuf = _cbuf;
                int igrp = ibuf >> k_shfBufGrp;
                int cval = k_numValBufFirst;
                if (igrp >= 2)
                {
                    if (igrp >= k_numGrpRamp)
                        cval = k_numValBufLast;
                    else
                        cval <<= (igrp - 1);
                }
                _bufs[_cbuf] = new T[cval];
                _cbuf++;
                _cap += cval;
                _countLast = 0;
            }

            AssertValid();
        }
    }

    /// <summary>
    /// Implementation for bool.
    /// </summary>
    private abstract class ImplBitBase<T> : GrowableArray<T>
    {
        // Each unit is a ulong, so 64 = 2^6 bits.
        protected const int k_shiftUnit = 6;
        protected const int k_cbitUnit = 1 << k_shiftUnit;
        // To map item index to the ibit into a unit mask with this.
        protected const long k_maskIbit = k_cbitUnit - 1;

        // Each buffer is 1024 = 2^10 units so there are 2^16 bits in a buffer. Note that if there
        // is only one buffer, it may be smaller.
        protected const int k_shiftBuf = 10;
        protected const int k_cunitBuf = 1 << k_shiftBuf;
        // To map item index to buffer index shift right by this.
        protected const int k_shiftIbuf = k_shiftBuf + k_shiftUnit;
        protected const int k_cbitBuf = 1 << k_shiftIbuf;

        /// <summary>
        /// Whether this is for <c>bool?</c> rather than <c>bool</c>.
        /// </summary>
        protected readonly bool _nullable;

        /// <summary>
        /// Whether this instance is "sealed". Can only transition from false to true.
        /// </summary>
        protected volatile bool _sealed;

        /// <summary>
        /// The buffers.
        /// </summary>
        protected ulong[][] _bufs;

        /// <summary>
        /// The number of active buffers.
        /// </summary>
        protected int _cbuf;

        /// <summary>
        ///  Current capacity.
        /// </summary>
        protected long _cap;

        /// <summary>
        /// The number of items.
        /// </summary>
        protected long _count;

        public sealed override bool IsSealed => _sealed;

        protected ImplBitBase(long capHint)
            : base()
        {
            Validation.Assert(typeof(T) == typeof(bool) || typeof(T) == typeof(bool?));

            _nullable = typeof(T) == typeof(bool?);

            int cbufAlloc;
            long numFirst;
            if (capHint <= k_cbitBuf >> 1)
            {
                // Start with a small first buffer.
                cbufAlloc = 1;
                numFirst = Math.Max(capHint >> k_shiftUnit, 4);
            }
            else
            {
                long ibuf = (capHint - 1) >> k_shiftIbuf;
                cbufAlloc = (int)Math.Min(ibuf + 1, 8);
                numFirst = k_cunitBuf;
            }
            _bufs = new ulong[cbufAlloc][];
            _bufs[0] = new ulong[numFirst];
            _cbuf = 1;
            _cap = numFirst * k_cbitUnit;

            AssertValid();
        }

        protected ImplBitBase(ImplBitBase<T> src)
            : base()
        {
            src.AssertValid();
            Validation.Assert(!src._sealed);

            _nullable = src._nullable;
            _sealed = true;
            _bufs = src._bufs;
            _cbuf = src._cbuf;
            _count = src._count;
            _cap = src._cap;

            AssertValid();
        }

        [Conditional("DEBUG")]
        protected void AssertValid()
        {
#if DEBUG
            Validation.AssertIndexInclusive(_count, _cap);
            Validation.Assert(_bufs != null);
            Validation.AssertIndex(_cbuf - 1, _bufs.Length);
            Validation.Assert(_count <= _cbuf * k_cbitBuf);
            Validation.Assert((_cbuf - 1) * k_cbitBuf <= _count);

            // The nullable ones need to have even count.
            Validation.Assert(!_nullable || (_count & 1) == 0);

            Validation.Assert(_bufs[0] != null);
            if (_cbuf == 1)
            {
                Validation.Assert(_bufs[0].Length <= k_cunitBuf);
                var cap = (long)_bufs[0].Length * k_cbitUnit;
                Validation.Assert(_cap <= cap);
                Validation.Assert(_cap == cap | _sealed);
            }
            else
            {
                Validation.Assert(_cap == _cbuf * k_cbitBuf);
                for (int ibuf = 0; ibuf < _cbuf; ibuf++)
                {
                    Validation.Assert(_bufs[ibuf] != null);
                    Validation.Assert(_bufs[ibuf].Length == k_cunitBuf);
                }
            }
#endif
        }

        public sealed override void Seal()
        {
            // REVIEW: Should this also "close up" when there is excessive spare capacity?
            _sealed = true;
        }

        /// <summary>
        /// Ensure that there is some spare capacity.
        /// </summary>
        public void EnsureSpace()
        {
            AssertValid();
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");

            if (_count >= _cap)
                Grow();
        }

        /// <summary>
        /// Grows the capacity. Asserts that we're currently at full capacity.
        /// </summary>
        protected void Grow()
        {
            AssertValid();
            Validation.Assert(!_sealed);
            Validation.Assert(_count == _cap);

            if (_cbuf == 1)
            {
                int cunit = _bufs[0].Length;
                if (cunit < k_cunitBuf)
                {
                    int cv = Math.Min(cunit << 1, k_cunitBuf);
                    Array.Resize(ref _bufs[0], cv);
                    _cap = cv * k_cbitUnit;
                    AssertValid();
                    Validation.Assert(_cap - _count >= k_cbitUnit);
                    return;
                }

                // Fall through to add a buffer.
                Validation.Assert(cunit == k_cunitBuf);
                Validation.Assert(_count == k_cbitBuf);
            }

            // Add a new buffer.
            if (_bufs.Length <= _cbuf)
                Array.Resize(ref _bufs, 2 * _cbuf);

            _bufs[_cbuf] = new ulong[k_cunitBuf];
            _cbuf++;
            _cap += k_cbitBuf;
            Validation.Assert(_cap == _cbuf * k_cbitBuf);

            AssertValid();
            Validation.Assert(_cap - _count == k_cbitBuf);
        }

        protected long AddBits(ulong bits, int cbit)
        {
            AssertValid();
            Validation.Assert(!_sealed);
            Validation.Assert(0 < cbit & cbit <= k_cbitUnit);
            Validation.Assert((bits & (~0UL << cbit)) == 0 | cbit == k_cbitUnit);

            var index = _count;
            Validation.Assert(index <= _cap - cbit);
#if DEBUG
            if (_nullable)
            {
                Validation.Assert((cbit & 1) == 0);
                // The bit pattern `0x3` is illegal. That means bits & (bits >> 1) should have
                // no even bit positions set.
                Validation.Assert((bits & (bits >> 1) & 0x5555_5555_5555_5555) == 0);
            }

            {
                // Assert that the indices are all correct and that the current bits are all zero.
                int ibuf = (int)(index >> k_shiftIbuf);
                Validation.AssertIndex(ibuf, _cbuf);
                int iunit = (int)(index >> k_shiftUnit) & (k_cunitBuf - 1);
                Validation.AssertIndex(iunit, _bufs[ibuf].Length);
                var unit = _bufs[ibuf][iunit];
                int ibit = (int)(index & k_maskIbit);
                Validation.Assert(ibit + cbit <= k_cbitUnit);
                Validation.Assert((unit >> ibit) == 0);
            }
#endif
            if (bits != 0)
            {
                int ibuf = (int)(index >> k_shiftIbuf);
                Validation.AssertIndex(ibuf, _cbuf);
                int iunit = (int)(index >> k_shiftUnit) & (k_cunitBuf - 1);
                Validation.AssertIndex(iunit, _bufs[ibuf].Length);
                int ibit = (int)(index & k_maskIbit);
                Validation.Assert(ibit + cbit <= k_cbitUnit);
                _bufs[ibuf][iunit] |= (ulong)bits << ibit;
            }
            _count += cbit;

            AssertValid();
            return index;
        }

        protected ulong GetBits(long index)
        {
            AssertValid();
            Validation.AssertIndex(index, _count);

            int ibuf = (int)(index >> k_shiftIbuf);
            Validation.AssertIndex(ibuf, _cbuf);
            int iunit = (int)(index >> k_shiftUnit) & (k_cunitBuf - 1);
            Validation.AssertIndex(iunit, _bufs[ibuf].Length);
            int ibit = (int)(index & k_maskIbit);

            return (_bufs[ibuf][iunit] >> ibit);
        }
    }

    /// <summary>
    /// Implementation for bool.
    /// </summary>
    private sealed class ImplBit : ImplBitBase<bool>
    {
        public override long Count => _count;

        public override long Capacity => _cap;

        public ImplBit(long capHint)
            : base(capHint)
        {
            Validation.Assert(!_nullable);
            Validation.Assert(!_sealed);
        }

        private ImplBit(ImplBit src)
            : base(src)
        {
            Validation.Assert(!_nullable);
            Validation.Assert(_sealed);
        }

        /// <summary>
        /// Returns an immutable snapshot of this array.
        /// </summary>
        public new ImplBit Snap()
        {
            if (_sealed)
                return this;
            return new ImplBit(this);
        }

        protected override GrowableArray<bool> SnapCore()
        {
            Validation.Assert(!_sealed);
            return new ImplBit(this);
        }

        public override long Add(bool item)
        {
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");
            return AddCore(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long AddCore(bool item)
        {
            if (_count >= _cap)
                Grow();
            return AddBits(item ? 1U : 0U, 1);
        }

        public override long AddMulti(ReadOnlySpan<bool> items)
        {
            AssertValid();
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");

            var index = _count;
            if (items.Length == 1)
                return AddCore(items[0]);

            int iitem = 0;
            while (iitem < items.Length)
            {
                if (_count >= _cap)
                    Grow();
                int cbit = k_cbitUnit - (int)(_count & k_maskIbit);
                int cbitLeft = items.Length - iitem;
                if (cbit > cbitLeft)
                    cbit = cbitLeft;
                ulong bits = 0;
                for (int ibit = 0; ibit < cbit; ibit++)
                {
                    if (items[iitem++])
                        bits |= 1UL << ibit;
                }
                AddBits(bits, cbit);
                AssertValid();
            }

            return index;
        }

        public override bool this[long index]
        {
            get
            {
                AssertValid();
                Validation.BugCheckIndex(index, _count, nameof(index));
                return (GetBits(index) & 1) != 0;
            }
        }

        public override IEnumerator<bool> GetEnumerator()
        {
            AssertValid();

            long count = _count;
            if (count == 0)
                yield break;

            var bufs = _bufs;
            int cbuf = _cbuf;
            long index = 0;
            for (int ibuf = 0; ibuf < cbuf; ibuf++)
            {
                var buf = bufs[ibuf];
                for (int iunit = 0; iunit < k_cunitBuf; iunit++)
                {
                    Validation.AssertIndex(iunit, buf.Length);
                    var unit = buf[iunit];
                    for (int ibit = 0; ibit < k_cbitUnit; ibit++, unit >>= 1)
                    {
                        yield return (unit & 1) != 0;
                        if (++index >= count)
                            yield break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Implementation for bool? (nullable).
    /// </summary>
    private sealed class ImplBitNullable : ImplBitBase<bool?>
    {
        public override long Count => _count >> 1;

        public override long Capacity => _cap >> 1;

        public ImplBitNullable(long capHint)
            : base(Math.Min(k_cbitBuf, int.MaxValue) << 1)
        {
            Validation.Assert(_nullable);
            Validation.Assert(!_sealed);
        }

        private ImplBitNullable(ImplBitNullable src)
            : base(src)
        {
            Validation.Assert(_nullable);
            Validation.Assert(_sealed);
        }

        protected override GrowableArray<bool?> SnapCore()
        {
            Validation.Assert(!_sealed);
            return new ImplBitNullable(this);
        }

        public override long Add(bool? item)
        {
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");
            return AddCore(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long AddCore(bool? item)
        {
            if (_count >= _cap)
                Grow();
            var index = AddBits(BoolQToBits(item), 2);
            Validation.Assert((index & 1) == 0);
            return index >> 1;
        }

        public override long AddMulti(ReadOnlySpan<bool?> items)
        {
            AssertValid();
            Validation.BugCheck(!_sealed, "Can't modify sealed instance");

            if (items.Length == 1)
                return AddCore(items[0]);

            var index = _count >> 1;
            int iitem = 0;
            while (iitem < items.Length)
            {
                if (_count >= _cap)
                    Grow();
                int cbit = k_cbitUnit - (int)(_count & k_maskIbit);
                int cbitLeft = (items.Length - iitem) << 1;
                if (cbit > cbitLeft)
                    cbit = cbitLeft;
                Validation.Assert((cbit & 1) == 0);
                ulong bits = 0;
                for (int ibit = 0; ibit < cbit; ibit += 2)
                    bits |= (ulong)BoolQToBits(items[iitem++]) << ibit;
                AddBits(bits, cbit);
                AssertValid();
            }

            return index;
        }

        public override bool? this[long index]
        {
            get
            {
                AssertValid();
                Validation.BugCheckIndex(index, _count, nameof(index));
                var bits = (uint)GetBits(index << 1) & 0x3;
                return BitsToBoolQ(bits);
            }
        }

        public override IEnumerator<bool?> GetEnumerator()
        {
            AssertValid();

            long count = _count;
            Validation.Assert((count & 1) == 0);
            count >>= 1;
            if (count == 0)
                yield break;

            var bufs = _bufs;
            int cbuf = _cbuf;
            long index = 0;
            for (int ibuf = 0; ibuf < cbuf; ibuf++)
            {
                var buf = bufs[ibuf];
                for (int iunit = 0; iunit < k_cunitBuf; iunit++)
                {
                    Validation.AssertIndex(iunit, buf.Length);
                    var unit = buf[iunit];
                    for (int ibit = 0; ibit < k_cbitUnit; ibit += 2, unit >>= 2)
                    {
                        yield return BitsToBoolQ((uint)unit & 0x3);
                        if (++index >= count)
                            yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Maps a <c>bool?</c> value to two bits.
        /// <c>null</c> maps to <c>0b10</c> with the high bit set and low bit clear,
        /// <c>true</c> maps to <c>0b01</c> with the high bit clear and low bit set,
        /// <c>false</c> maps to <c>0b00</c>, with both bits clear.
        /// The bit pattern <c>0b11</c> is not used.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BoolQToBits(bool? val)
        {
            var bits = !val.HasValue ? 0b10U : val.GetValueOrDefault() ? 0b01U : 0b00U;
            Validation.Assert(bits <= 0b10U);
            return bits;
        }

        /// <summary>
        /// Maps from two bits to the corresponding <c>bool?</c> value. This is the inverse of
        /// <see cref="BoolQToBits(bool?)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool? BitsToBoolQ(uint bits)
        {
            Validation.Assert(bits <= 0b10U);
            return bits == 0b00U ? false : bits == 0b01U ? true : null;
        }
    }

    /// <summary>
    /// Implementation for nullable types. This saves storage by using a bit array to track null
    /// values.
    /// </summary>
    private sealed class ImplNullable<T> : GrowableArray<T?>
        where T : struct
    {
        private readonly Impl<T> _values;
        private readonly ImplBit _flags;

        public override long Count => _values.Count;

        public override long Capacity => Math.Min(_values.Capacity, _flags.Capacity);

        public override bool IsSealed => _values.IsSealed;

        public ImplNullable(long capHint)
        {
            _values = new Impl<T>(capHint);
            _flags = new ImplBit(_values.Capacity);
            AssertValid();
        }

        private ImplNullable(ImplNullable<T> src)
        {
            src.AssertValid();
            Validation.Assert(!src.IsSealed);

            _values = src._values.Snap();
            _flags = src._flags.Snap();

            AssertValid();
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
#if DEBUG
            Validation.Assert(_values.IsSealed == IsSealed);
            Validation.Assert(_flags.IsSealed == IsSealed);
            Validation.Assert(_values.Count == _flags.Count);
#endif
        }

        protected override GrowableArray<T?> SnapCore()
        {
            Validation.Assert(!_values.IsSealed);
            return new ImplNullable<T>(this);
        }

        public override void Seal()
        {
            _values.Seal();
            _flags.Seal();
        }

        public override long Add(T? item)
        {
            AssertValid();
            Validation.BugCheck(!IsSealed);

            _flags.EnsureSpace();
            var ind1 = _values.Add(item.GetValueOrDefault());
            var ind2 = _flags.Add(!item.HasValue);
            Validation.Assert(ind1 == ind2);

            AssertValid();
            return ind1;
        }

        public override long AddMulti(ReadOnlySpan<T?> items)
        {
            AssertValid();
            Validation.BugCheck(!IsSealed);

            var index = Count;
            foreach (var item in items)
            {
                // Make sure there is space in _flags before adding to _values so we don't risk
                // getting them out of sync.
                _flags.EnsureSpace();
                var ind1 = _values.Add(item.GetValueOrDefault());
                var ind2 = _flags.Add(!item.HasValue);
                Validation.Assert(ind1 == ind2);
                AssertValid();
            }

            AssertValid();
            return index;
        }

        /// <summary>
        /// Indexer to access a particular item.
        /// </summary>
        public override T? this[long index]
        {
            get
            {
                AssertValid();
                Validation.BugCheckIndex(index, Count, nameof(index));

                if (_flags[index])
                    return null;
                return _values[index];
            }
        }

        public override IEnumerator<T?> GetEnumerator()
        {
            AssertValid();

            using var ator1 = _values.GetEnumerator();
            using var ator2 = _flags.GetEnumerator();
            while (ator1.MoveNext() && ator2.MoveNext())
            {
                yield return ator2.Current ? null : ator1.Current;
            }
        }
    }
}
