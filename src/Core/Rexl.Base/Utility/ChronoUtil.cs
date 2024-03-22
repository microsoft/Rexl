// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Utility;

/// <summary>
/// Utilities for the date and time types.
/// </summary>
public static class ChronoUtil
{
    // Number of 100ns ticks per time unit.

    /// <summary>
    /// Number of 100ns ticks per millisecond.
    /// </summary>
    public const long TicksPerMs = TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Number of 100ns ticks per second.
    /// </summary>
    public const long TicksPerSecond = TicksPerMs * 1000;

    /// <summary>
    /// Number of 100ns ticks per minute.
    /// </summary>
    public const long TicksPerMinute = TicksPerSecond * 60;

    /// <summary>
    /// Number of 100ns ticks per hour.
    /// </summary>
    public const long TicksPerHour = TicksPerMinute * 60;

    /// <summary>
    /// Number of 100ns ticks per day.
    /// </summary>
    public const long TicksPerDay = TicksPerHour * 24;

    /// <summary>
    /// Number of days in a non-leap year.
    /// </summary>
    public const int DaysPerYear = 365;

    /// <summary>
    /// Number of days in a normal 4 year period, where one is a leap year.
    /// </summary>
    public const int DaysPer4Years = DaysPerYear * 4 + 1;       // 1461

    /// <summary>
    /// Number of days in a normal 100 year period, where there are 24 leap years.
    /// </summary>
    public const int DaysPer100Years = DaysPer4Years * 25 - 1;  // 36524

    /// <summary>
    /// Number of days in a normal 400 year period, where there are 97 leap years.
    /// </summary>
    public const int DaysPer400Years = DaysPer100Years * 4 + 1; // 146097

    /// <summary>
    /// Number of days from 1/1/0001 to 12/31/9999.
    /// </summary>
    public const int DaysTo10000 = DaysPer400Years * 25 - 366;  // 3652059

    /// <summary>
    /// Maximum number of ticks in a date value. This is one less than the number of
    /// ticks in <see cref="DaysTo10000"/> days.
    /// </summary>
    public const long TicksMax = DaysTo10000 * TicksPerDay - 1;

    // These hold the min and lim day numbers for the months in regular and leap years.
    private static readonly int[] _daysToMonth365 = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };
    private static readonly int[] _daysToMonth366 = { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };

    /// <summary>
    /// Returns whether the given year number is a leap year.
    /// A leap year is divisible by 4, but not divisible by 100 unless it is also divisible by 400.
    /// </summary>
    public static bool IsLeapYear(long year)
    {
        // Leap years are divisible by 4 and either not divisible by 100 or divisible by 400.
        // That's equivalent to "divisible by 4 and (not divisible by 25 or divisble by 16).

        // The form with & is a bit faster than using %. It is equivalent to:
        //     (year % 4) == 0 && ((year % 16) == 0 || (year % 25) != 0).
        return (year & 3) == 0 && ((year & 15) == 0 || (year % 25) != 0);
    }

    /// <summary>
    /// Computes the total number of ticks given component values. If any components are illegal, this
    /// returns zero.
    /// </summary>
    public static long ToTicks(long year, long month, long day, long ticks)
    {
        if ((ulong)(year - 1) >= 9999 || (ulong)(month - 1) >= 12 || (ulong)(day - 1) >= 31 || (ulong)ticks >= TicksPerDay)
            return 0;

        int[] days = IsLeapYear(year) ? _daysToMonth366 : _daysToMonth365;
        day = day - 1 + days[month - 1];
        if (day >= days[month])
            return 0;

        var y = year - 1;
        var n = y * 365 + y / 4 - y / 100 + y / 400 + day;
        return n * TicksPerDay + ticks;
    }

    /// <summary>
    /// Gets the date parts, namely year, month, day-in-month, and day-in-year. All are 1-based.
    /// </summary>
    public static void GetDateParts(long ticks, out int year, out int month, out int dayInMonth, out int dayInYear)
    {
        if ((ulong)ticks > (ulong)TicksMax)
        {
            Validation.Assert(false, "Shouldn't call GetDateParts with invalid ticks");
            ticks = ticks < 0 ? 0 : TicksMax;
        }

        // Note: this code comes from the .Net DateTime code.

        // n = number of days since 1/1/0001.
        int n = (int)(ticks / TicksPerDay);

        // y400 = number of whole 400-year periods since 1/1/0001.
        int y400 = n / DaysPer400Years;

        // n = day number within 400-year period
        n -= y400 * DaysPer400Years;

        // y100 = number of whole 100-year periods within 400-year period.
        int y100 = n / DaysPer100Years;

        // Last 100-year period has an extra day, so decrement result if 4.
        if (y100 == 4) y100 = 3;

        // n = day number within 100-year period
        n -= y100 * DaysPer100Years;

        // y4 = number of whole 4-year periods within 100-year period.
        int y4 = n / DaysPer4Years;

        // n = day number within 4-year period.
        n -= y4 * DaysPer4Years;

        // y1 = number of whole years within 4-year period.
        int y1 = n / DaysPerYear;

        // Last year has an extra day, so decrement result if 4.
        if (y1 == 4) y1 = 3;

        year = y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1;

        // n = day number within year.
        n -= y1 * DaysPerYear;

        dayInYear = n + 1;

        // Leap year calculation looks different from IsLeapYear since y1, y4,
        // and y100 are relative to year 1, not year 0
        bool leapYear = y1 == 3 && (y4 != 24 || y100 == 3);

        int[] days = leapYear ? _daysToMonth366 : _daysToMonth365;

        // All months have less than 32 days, so n >> 5 is a good conservative
        // estimate for the month.
        int m = (n >> 5) + 1;

        // m = 1-based month number
        while (n >= days[m]) m++;

        month = m;

        dayInMonth = n - days[m - 1] + 1;
    }
}
