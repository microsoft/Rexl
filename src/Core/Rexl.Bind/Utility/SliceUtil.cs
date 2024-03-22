// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;

namespace Microsoft.Rexl;

/// <summary>
/// Calculates range, i.e. (start, step, count) values that appear within a slicing operation.
/// </summary>
public static class SliceUtil
{
    /// <summary>
    /// Calcuates range for the given values and flags. If <paramref name="flags"/> has the
    /// <see cref="SliceItemFlags.Start"/> and/or <see cref="SliceItemFlags.Stop"/> bits clear,
    /// the corresponding argument is ignored. This ignores the <see cref="SliceItemFlags.Step"/>
    /// bit, with the assumption that a zero step is equivalent to a null/missing step.
    /// </summary>
    public static (long start, long step, long count) GetRange(long len, SliceItemFlags flags,
        long start, long stop, long step)
    {
        if ((flags & SliceItemFlags.Stop) == 0)
        {
            // Don't have a stop value.
            if ((flags & SliceItemFlags.Start) != 0)
                return GetRangeR_(len, flags, start, step);
            return GetRange__(len, step);
        }

        if ((flags & SliceItemFlags.StopStar) == 0)
        {
            // Have a real stop value.
            if ((flags & SliceItemFlags.Start) != 0)
                return GetRangeRR(len, flags, start, stop, step);
            return GetRange_R(len, flags, stop, step);
        }

        // The "stop" value is really a count, possibly a delta.
        // First compute as if we have no stop value (since we really don't).
        var rng = (flags & SliceItemFlags.Start) != 0 ?
            GetRangeR_(len, flags, start, step) :
            GetRange__(len, step);

        if (rng.count <= 0)
            return rng;

        if ((flags & SliceItemFlags.StopBack) == 0)
        {
            // At most "stop" items.
            if (rng.count <= stop)
                return rng;
            if (stop <= 0)
                return (rng.start, 0, 0);
            return (rng.start, rng.step, stop);
        }

        // Decrease the count by "stop".
        if (stop <= 0)
            return rng;
        if (rng.count <= stop)
            return (rng.start, 0, 0);
        return (rng.start, rng.step, rng.count - stop);
    }

    /// <summary>
    /// Calculates range when neither <paramref name="start"/> nor <paramref name="stop"/> is null.
    /// </summary>
    private static (long start, long step, long count) GetRangeRR(long len, SliceItemFlags flags,
        long start, long stop, long step)
    {
        // Adjust according to the back values.
        start = (flags & SliceItemFlags.StartBack) != 0 ? len - start : start;
        stop = (flags & SliceItemFlags.StopBack) != 0 ? len - stop : stop;

        // If step is "automatic", base it on which of start/stop is larger.
        if (step == 0)
            step = start <= stop ? 1 : -1;

        if (step > 0)
        {
            start = Util.Clamp(start, 0, len);
            stop = Util.Clamp(stop, 0, len);
        }
        else
        {
            start = Util.Clamp(start, -1, len - 1);
            stop = Util.Clamp(stop, -1, len - 1);
        }

        return (start, step, Util.GetCount(start, stop, step));
    }

    /// <summary>
    /// Calculates range when <paramref name="start"/> is given and stop is not.
    /// </summary>
    private static (long start, long step, long count) GetRangeR_(long len, SliceItemFlags flags,
        long start, long step)
    {
        start = (flags & SliceItemFlags.StartBack) != 0 ? len - start : start;

        if (step == 0)
            step = 1;

        long stop;
        if (step > 0)
        {
            start = Util.Clamp(start, 0, len);
            stop = len;
        }
        else
        {
            start = Util.Clamp(start, -1, len - 1);
            stop = -1;
        }

        return (start, step, Util.GetCount(start, stop, step));
    }

    /// <summary>
    /// Calculates range when start is not given and <paramref name="stop"/> is given.
    /// </summary>
    private static (long start, long step, long count) GetRange_R(long len, SliceItemFlags flags,
        long stop, long step)
    {
        stop = (flags & SliceItemFlags.StopBack) != 0 ? len - stop : stop;

        if (step == 0)
            step = 1;

        long start;
        if (step > 0)
        {
            start = 0;
            stop = Util.Clamp(stop, 0, len);
        }
        else
        {
            start = len - 1;
            stop = Util.Clamp(stop, -1, len - 1);
        }

        return (start, step, Util.GetCount(start, stop, step));
    }

    /// <summary>
    /// Calculates range when neither start nor stop is given.
    /// </summary>
    private static (long start, long step, long count) GetRange__(long len, long step)
    {
        if (step == 0)
            step = 1;

        long start;
        long stop;
        if (step > 0)
        {
            start = 0;
            stop = len;
        }
        else
        {
            start = len - 1;
            stop = -1;
        }

        return (start, step, Util.GetCount(start, stop, step));
    }
}
