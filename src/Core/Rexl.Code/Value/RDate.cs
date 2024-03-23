// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

/// <summary>
/// Similar to <see cref="System.DateTime"/> but doesn't support the notion of <see cref="DateTimeKind"/>.
/// This is used for the rexl DateTime <see cref="DType"/>.
/// </summary>
public struct RDate : IEquatable<RDate>, IComparable<RDate>
{
    /// <summary>
    /// The only instance field. Contains the number of ticks.
    /// </summary>
    private readonly long _ticks;

    /// <summary>
    /// The culture to use for date parsing and rendering. Critically, this sets the TwoDigitYearMax, so we
    /// get consistency across versions of .Net.
    /// </summary>
    public static readonly CultureInfo Culture = CreateCulture();

    /// <summary>
    /// The style to use for date parsing. Critically, this removes sensitivity to machine time zone
    /// and current date.
    /// </summary>
    public const DateTimeStyles ParseStyle = DateTimeStyles.AdjustToUniversal | DateTimeStyles.NoCurrentDateDefault;

    private static CultureInfo CreateCulture()
    {
        // Base this on invariant.
        var ci = new CultureInfo(CultureInfo.InvariantCulture.LCID);

        // Set the TwoDigitYearMax, since it seems that the invariant culture doesn't have a consistent value.
        // REVIEW: What value should we use? .Net seemed to use 2029 for a long time (in invariant culture),
        // but on latest OS and .Net seems to use 2049. Build/test machines seem to still use 2029, but dev
        // machines seem to use 2049. Perhaps we should insist on 4-digit dates, which means we should set this
        // to 99. Then "1-2-30" would resolve to year 30, not 1930 or 2030. Note that, regardless of this setting,
        // a user can force an early year with "1-2-0030".
        ci.Calendar.TwoDigitYearMax = 2049;

        return ci;
    }

    /// <summary>
    /// The maximum number of ticks legal in an <see cref="RDate"/>.
    /// </summary>
    public static readonly RDate MaxValue = new RDate(ChronoUtil.TicksMax);

    private RDate(long ticks)
    {
        _ticks = ticks;
        Validation.Assert((ulong)_ticks <= (ulong)ChronoUtil.TicksMax);
    }

    /// <summary>
    /// Constructs an <see cref="RDate"/> from the given number of ticks. This BugChecks that
    /// <paramref name="ticks"/> is valid.
    /// </summary>
    public static RDate FromTicks(long ticks)
    {
        Validation.BugCheckParam((ulong)ticks <= (ulong)ChronoUtil.TicksMax, nameof(ticks));
        return new RDate(ticks);
    }

    /// <summary>
    /// Constructs an <see cref="RDate"/> from the given <see cref="System.DateTime"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RDate FromSys(DateTime dt)
    {
        return new RDate(dt.Ticks);
    }

    /// <summary>
    /// Constructs a <see cref="DateTime"/> from an <see cref="RDate"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime ToSys(RDate dt)
    {
        return new DateTime(dt._ticks);
    }

    /// <summary>
    /// Constructs an <see cref="RDate"/> for the given <paramref name="year"/>, <paramref name="month"/>,
    /// <paramref name="day"/>, and <paramref name="ticks"/>. If any are outside their legal range, the
    /// returned value is the default value.
    /// REVIEW: Perhaps we should support a NaD (not a date) value, represented, say, by -1.
    /// </summary>
    public static RDate CreateOrDefault(long year, long month, long day, long ticks)
    {
        return new RDate(ChronoUtil.ToTicks(year, month, day, ticks));
    }

    /// <summary>
    /// Try to parse the string as a date-time. This forces a culture (that is safe) and parse style.
    /// </summary>
    public static bool TryParse(string s, out RDate result)
    {
        if (!DateTime.TryParse(s, Culture, ParseStyle, out var res))
        {
            result = default;
            return false;
        }

        result = FromSys(res);
        return true;
    }

    /// <summary>
    /// Returns the equivalent <see cref="System.DateTime"/> value.
    /// </summary>
    public DateTime SysDateTime => new DateTime(_ticks);

    /// <summary>
    /// The total number of ticks.
    /// </summary>
    public long Ticks => _ticks;

    /// <summary>
    /// Returns the date part, time of day set to zero (midnight).
    /// </summary>
    public RDate Date => new RDate(_ticks - _ticks % ChronoUtil.TicksPerDay);

    /// <summary>
    /// Returns the time span since midnight.
    /// </summary>
    public TimeSpan TimeOfDay => new TimeSpan(_ticks % ChronoUtil.TicksPerDay);

    /// <summary>
    /// Returns the year.
    /// </summary>
    public int Year
    {
        get
        {
            ChronoUtil.GetDateParts(_ticks, out int value, out _, out _, out _);
            return value;
        }
    }

    /// <summary>
    /// Returns the month.
    /// </summary>
    public int Month
    {
        get
        {
            ChronoUtil.GetDateParts(_ticks, out _, out int value, out _, out _);
            return value;
        }
    }

    /// <summary>
    /// Returns the day within the month.
    /// </summary>
    public int Day
    {
        get
        {
            ChronoUtil.GetDateParts(_ticks, out _, out _, out int value, out _);
            return value;
        }
    }

    /// <summary>
    /// Returns the day within the year.
    /// </summary>
    public int DayOfYear
    {
        get
        {
            ChronoUtil.GetDateParts(_ticks, out _, out _, out _, out int value);
            return value;
        }
    }

    /// <summary>
    /// Returns the day-of-week. The returned value is an integer between 0 and 6, where
    /// 0 indicates Sunday, 1 indicates Monday, etc.
    /// </summary>
    public int DayOfWeek => (int)((_ticks / ChronoUtil.TicksPerDay + 1) % 7);

    /// <summary>
    /// Returns the hour number within the day.
    /// </summary>
    public int Hour => (int)((_ticks / ChronoUtil.TicksPerHour) % 24);

    /// <summary>
    /// Returns the minute number within the hour.
    /// </summary>
    public int Minute => (int)((_ticks / ChronoUtil.TicksPerMinute) % 60);

    /// <summary>
    /// Returns the second number within the minute.
    /// </summary>
    public int Second => (int)((_ticks / ChronoUtil.TicksPerSecond) % 60);

    /// <summary>
    /// Returns the millisecond number within the second.
    /// </summary>
    public int Millisecond => (int)((_ticks / ChronoUtil.TicksPerMs) % 1000);

    /// <summary>
    /// D + T => D. Overflow produces default.
    /// </summary>
    public static RDate Add(RDate x, TimeSpan y)
    {
        long xt = x._ticks;
        long yt = y.Ticks;
        Validation.Assert(xt >= 0);

        long rt = xt + yt;

        // Overflow can only happen when the two values have the same sign.
        // Since xt >= 0, overflow can only happen when yt > 0 and the result
        // is negative. The result being negative is bad regardless. And this
        // condition covers that situation.
        if ((ulong)rt > (ulong)ChronoUtil.TicksMax)
            rt = 0;

        return new RDate(rt);
    }

    /// <summary>
    /// D - T => D. Overflow produces default.
    /// </summary>
    public static RDate Sub(RDate x, TimeSpan y)
    {
        long xt = x._ticks;
        long yt = y.Ticks;
        Validation.Assert(xt >= 0);

        long rt = xt - yt;

        // Overflow can only happen when the two values have the opposite sign.
        // Since xt >= 0, overflow can only happen when yt < 0 and the result
        // is negative. The result being negative is bad regardless. And this
        // condition covers that situation.
        if ((ulong)rt > (ulong)ChronoUtil.TicksMax)
            rt = 0;

        return new RDate(rt);
    }

    /// <summary>
    /// D - D => T.
    /// </summary>
    public static TimeSpan Sub(RDate x, RDate y)
    {
        long xt = x._ticks;
        long yt = y._ticks;
        Validation.Assert(xt >= 0);
        Validation.Assert(yt >= 0);

        long rt = xt - yt;
        return new TimeSpan(rt);
    }

    public static RDate operator +(RDate a, TimeSpan b) => Add(a, b);

    public static RDate operator +(TimeSpan a, RDate b) => Add(b, a);

    public static TimeSpan operator -(RDate a, RDate b) => Sub(a, b);

    public static RDate operator -(RDate a, TimeSpan b) => Sub(a, b);

    public static bool operator ==(RDate a, RDate b) => a._ticks == b._ticks;

    public static bool operator !=(RDate a, RDate b) => a._ticks != b._ticks;

    public static bool operator <(RDate a, RDate b) => a._ticks < b._ticks;

    public static bool operator <=(RDate a, RDate b) => a._ticks <= b._ticks;

    public static bool operator >=(RDate a, RDate b) => a._ticks >= b._ticks;

    public static bool operator >(RDate a, RDate b) => a._ticks > b._ticks;

    /// <summary>
    /// Test for equality.
    /// </summary>
    public bool Equals(RDate other) => _ticks == other._ticks;

    /// <summary>
    /// Return negative, zero, or positive according to whether t1 is less, equal, or greater than t2,
    /// respectively.
    /// </summary>
    public static int Compare(RDate t1, RDate t2) => t1._ticks.CompareTo(t2._ticks);

    /// <summary>
    /// Return negative, zero, or positive according to whether this is less, equal, or greater than other,
    /// respectively.
    /// </summary>
    public int CompareTo(RDate other) => _ticks.CompareTo(other._ticks);

    /// <summary>
    /// Convert to an ISO8601 string, suitable for (loss-less) serialization.
    /// </summary>
    public string ToISO()
    {
        return SysDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFF", Culture);
    }

    public override string ToString()
    {
        return SysDateTime.ToString(Culture);
    }

    public string ToString(string format)
    {
        return SysDateTime.ToString(format, Culture);
    }

    public override int GetHashCode()
    {
        long ticks = _ticks;
        return (int)ticks ^ (int)(ticks >> 32);
    }

    public override bool Equals(object? value)
    {
        return value is RDate other && _ticks == other._ticks;
    }
}
