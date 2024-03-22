// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

namespace Microsoft.Rexl;

static partial class RexlStrings
{

    #region Standard argument strings.
    public static readonly StringId ArgCount = new(nameof(ArgCount), "count");
    public static readonly StringId ArgDefinition = new(nameof(ArgDefinition), "definition");
    public static readonly StringId ArgPredicate = new(nameof(ArgPredicate), "predicate");
    public static readonly StringId ArgIfPredicate = new(nameof(ArgIfPredicate), "[if] predicate");
    public static readonly StringId ArgWhilePredicate = new(nameof(ArgWhilePredicate), "[while] predicate");
    public static readonly StringId ArgElse = new(nameof(ArgElse), "else");
    public static readonly StringId ArgSeed = new(nameof(ArgSeed), "seed");
    public static readonly StringId ArgSelector = new(nameof(ArgSelector), "selector");
    public static readonly StringId ArgKeySelector = new(nameof(ArgKeySelector), "key_selector");
    public static readonly StringId ArgIterSelector = new(nameof(ArgIterSelector), "iteration_selector");
    public static readonly StringId ArgResSelector = new(nameof(ArgResSelector), "result_selector");
    public static readonly StringId ArgSource = new(nameof(ArgSource), "source");
    public static readonly StringId ArgLookUp = new(nameof(ArgLookUp), "lookup");
    public static readonly StringId ArgIndex = new(nameof(ArgIndex), "index");
    public static readonly StringId ArgMinIndex = new(nameof(ArgMinIndex), "min_index");
    public static readonly StringId ArgLimIndex = new(nameof(ArgLimIndex), "lim_index");
    public static readonly StringId ArgValue = new(nameof(ArgValue), "value");
    public static readonly StringId ArgX = new(nameof(ArgX), "x");
    public static readonly StringId ArgY = new(nameof(ArgY), "y");
    public static readonly StringId ArgAngle = new(nameof(ArgAngle), "angle");

    public static readonly StringId AboutArgSeqSource = new(nameof(AboutArgSeqSource), "The source sequence.");
    public static readonly StringId AboutArgPredicate = new(nameof(AboutArgPredicate), "The condition with which to test an item.");
    public static readonly StringId AboutArgIfPredicate = new(nameof(AboutArgIfPredicate), "'[if]' directive, followed by the condition with which to test an item.");
    public static readonly StringId AboutArgWhilePredicate = new(nameof(AboutArgWhilePredicate), "'[while]' directive, followed by the condition with which to test an item.");
    public static readonly StringId AboutArgSeed = new(nameof(AboutArgSeed), "The initial iteration value, typically including a name ('name: value').");
    #endregion Standard argument strings.

    // Function signature and parameter descriptions.
    public static readonly StringId AboutForEach = new(nameof(AboutForEach), "Produces a value for each item from a source sequence or each set of corresponding items from multiple source sequences. The length of the resulting sequence is the length of the shortest source sequence.");
    public static readonly StringId AboutForEach_WithIfPred = new(nameof(AboutForEach_WithIfPred), "Produces a value for each item from a source sequence or each set of corresponding items from multiple source sequences that satisfies the [if] predicate.");
    public static readonly StringId AboutForEach_WithWhilePred = new(nameof(AboutForEach_WithWhilePred), "Produces a value for each item from a source sequence or each set of corresponding items from multiple source sequences, as long as the [while] predicate is satisfied.");
    public static readonly StringId AboutForEach_Source = new(nameof(AboutForEach_Source), "A source sequence, optionally with an item name ('item_name: sequence').");
    public static readonly StringId AboutForEach_Selector = new(nameof(AboutForEach_Selector), "The value to produce.");

    public static readonly StringId AboutRangeFunc = new(nameof(AboutRangeFunc), "Generates an arithmetic sequence of integers within a specified range.");
    public static readonly StringId RangeFuncArg1 = new(nameof(RangeFuncArg1), "min");
    public static readonly StringId AboutRangeFuncArg1 = new(nameof(AboutRangeFuncArg1), "Inclusive lower limit of generated sequence.");
    public static readonly StringId RangeFuncArg2 = new(nameof(RangeFuncArg2), "limit");
    public static readonly StringId AboutRangeFuncArg2 = new(nameof(AboutRangeFuncArg2), "Exclusive upper limit of generated sequence.");
    public static readonly StringId RangeFuncArg3 = new(nameof(RangeFuncArg3), "step");
    public static readonly StringId AboutRangeFuncArg3 = new(nameof(AboutRangeFuncArg3), "Step distance between each generated value in sequence.");

    public static readonly StringId AboutSort = new(nameof(AboutSort), "Sorts the items of the sequence, by the selector value(s), using default order (up for text, down for other types).");
    public static readonly StringId AboutSortUp = new(nameof(AboutSortUp), "Sorts the items of the sequence, by the selector value(s), in small to large order.");
    public static readonly StringId AboutSortDown = new(nameof(AboutSortDown), "Sorts the items of the sequence, by the selector value(s), in large to small order.");
    public static readonly StringId AboutSort_Source = new(nameof(AboutSort_Source), "The sequence to sort.");
    public static readonly StringId AboutSort_SourceOnly = new(nameof(AboutSort_SourceOnly), "The sequence to sort. May be prefixed with a sort directive: [up], [down], [<], [>], [~], [~up], [~down], [~<], [~>].");
    public static readonly StringId AboutSort_Selector = new(nameof(AboutSort_Selector), "A value to sort on. May be prefixed with a sort directive: [up], [down], [<], [>], [~], [~up], [~down], [~<], [~>].");

    public static readonly StringId AboutDistinct = new(nameof(AboutDistinct), "Gets the distinct items contained in the sequence in the original order of their first appearance.");
    public static readonly StringId AboutDistinct_Source = new(nameof(AboutDistinct_Source), "The sequence to remove duplicated items from.");
    public static readonly StringId AboutDistinct_Key = new(nameof(AboutDistinct_Key), "Optional key value to match by.");

    public static readonly StringId AboutChain = new(nameof(AboutChain), "Concatenates sequences.");
    public static readonly StringId AboutChain_Source = new(nameof(AboutChain_Source), "A sequence to concatenate.");

    public static readonly StringId AboutChainMap = new(nameof(AboutChainMap), "Flattens the sequence of sequences into a single sequence.");
    public static readonly StringId AboutChainMapArity2 = new(nameof(AboutChainMapArity2), "Flattens the sequence of sequences into a single sequence after applying the sequence selector.");
    public static readonly StringId AboutChainMap_Source = new(nameof(AboutChainMap_Source), "The sequence to flatten.");
    public static readonly StringId AboutChainMap_Selector = new(nameof(AboutChainMap_Selector), "The sequence selector to apply on the sequence to flatten.");

    public static readonly StringId AboutFirst = new(nameof(AboutFirst), "Gets the first item contained in the sequence that satisfies the optional predicate. Otherwise returns null.");
    public static readonly StringId AboutFirst_Source = new(nameof(AboutFirst_Source), "The sequence from which to extract an item.");
    public static readonly StringId AboutFirst_Predicate = new(nameof(AboutFirst_Predicate), "The condition with which to test an item.");

    public static readonly StringId AboutTakeAt = new(nameof(AboutTakeAt), "Returns the item in the source sequence at the given index. If the index is invalid, returns a default value.");
    public static readonly StringId AboutTakeAt_Index = new(nameof(AboutTakeAt_Index), "The index of the desired item. Negative values index from the end of the sequence.");
    public static readonly StringId AboutTakeAt_Else = new(nameof(AboutTakeAt_Else), "The value to return if the index is invalid.");

    public static readonly StringId AboutTakeOne = new(nameof(AboutTakeOne), "Returns the first item in the source sequence that satisfies the optional predicate. Otherwise returns a default value.");
    public static readonly StringId AboutTakeOne_Else = new(nameof(AboutTakeOne_Else), "The value to return if the sequence is empty or no item satisfies the predicate.");

    public static readonly StringId AboutDropOne = new(nameof(AboutDropOne), "Drops the first item in the source sequence that satisfies the optional predicate.");

    public static readonly StringId AboutTake = new(nameof(AboutTake), "Takes the first 'count' items contained in the source sequence that satisfy the optional predicate.");
    public static readonly StringId AboutDrop = new(nameof(AboutDrop), "Drops the first 'count' items contained in the source sequence that satisfy the optional predicate.");
    public static readonly StringId AboutTake_Count = new(nameof(AboutTake_Count), "The number of items to take.");
    public static readonly StringId AboutDrop_Count = new(nameof(AboutDrop_Count), "The number of items to drop.");

    public static readonly StringId AboutTakeIf = new(nameof(AboutTakeIf), "Takes the items of the source sequence that satisfy the predicate.");
    public static readonly StringId AboutDropIf = new(nameof(AboutDropIf), "Drops the items of the source sequence that satisfy the predicate.");

    public static readonly StringId AboutTakeWhile = new(nameof(AboutTakeWhile), "Takes the initial items contained in the source sequence that satisfy the predicate.");
    public static readonly StringId AboutDropWhile = new(nameof(AboutDropWhile), "Drops the initial items contained in the source sequence that satisfy the predicate.");

    public static readonly StringId AboutTakeCountWhile = new(nameof(AboutTakeCountWhile), "Takes the initial 'count' items contained in the source sequence that satisfy the [while] predicate.");
    public static readonly StringId AboutDropCountWhile = new(nameof(AboutDropCountWhile), "Drops the initial 'count' items contained in the source sequence that satisfy the [while] predicate.");

    public static readonly StringId AboutRepeat = new(nameof(AboutRepeat), "Produces a sequence consisting of the value repeated count times.");
    public static readonly StringId AboutRepeat_Value = new(nameof(AboutRepeat_Value), "The value to repeat.");
    public static readonly StringId AboutRepeat_Count = new(nameof(AboutRepeat_Count), "The number of items to produce.");

    public static readonly StringId AboutCount = new(nameof(AboutCount), "Gets the number of items contained in the source sequence which satisfy the optional predicate.");
    public static readonly StringId AboutCount_Source = new(nameof(AboutCount_Source), "A sequence.");
    public static readonly StringId AboutCount_Predicate = new(nameof(AboutCount_Predicate), "The condition with which to test an item.");

    public static readonly StringId AboutAny1 = new(nameof(AboutAny1), "Determines whether at least one item of the source sequence is true.");
    public static readonly StringId AboutAny2 = new(nameof(AboutAny2), "Determines whether at least one item of the source sequence satisfies the predicate.");
    public static readonly StringId AboutAny1_Source = new(nameof(AboutAny1_Source), "Sequence of booleans to evaluate.");
    public static readonly StringId AboutAny2_Source = new(nameof(AboutAny2_Source), "Sequence of items to evaluate.");
    public static readonly StringId AboutAny_Predicate = new(nameof(AboutAny_Predicate), "The condition with which to test an item.");

    public static readonly StringId AboutAll1 = new(nameof(AboutAll1), "Determines whether all items of the source sequence are true.");
    public static readonly StringId AboutAll2 = new(nameof(AboutAll2), "Determines whether all items of the source sequence satisfy the predicate.");
    public static readonly StringId AboutAll1_Source = new(nameof(AboutAll1_Source), "Sequence of booleans to evaluate.");
    public static readonly StringId AboutAll2_Source = new(nameof(AboutAll2_Source), "Sequence of items to evaluate.");
    public static readonly StringId AboutAll_Predicate = new(nameof(AboutAll_Predicate), "The condition with which to test an item.");

    public static readonly StringId AboutDateArity8Func = new(nameof(AboutDateArity8Func), "Creates a Date value using the specified year, month, day, hour, minute, second, millisecond, and ticks.");
    public static readonly StringId DateFuncArg1 = new(nameof(DateFuncArg1), "year");
    public static readonly StringId AboutDateFuncArg1 = new(nameof(AboutDateFuncArg1), "The year component of the Date.");
    public static readonly StringId DateFuncArg2 = new(nameof(DateFuncArg2), "month");
    public static readonly StringId AboutDateFuncArg2 = new(nameof(AboutDateFuncArg2), "The month component of the Date.");
    public static readonly StringId DateFuncArg3 = new(nameof(DateFuncArg3), "day");
    public static readonly StringId AboutDateFuncArg3 = new(nameof(AboutDateFuncArg3), "The day component of the Date.");
    public static readonly StringId DateFuncArg4 = new(nameof(DateFuncArg4), "hour");
    public static readonly StringId AboutDateFuncArg4 = new(nameof(AboutDateFuncArg4), "The hour component of the Date.");
    public static readonly StringId DateFuncArg5 = new(nameof(DateFuncArg5), "minute");
    public static readonly StringId AboutDateFuncArg5 = new(nameof(AboutDateFuncArg5), "The minute component of the Date.");
    public static readonly StringId DateFuncArg6 = new(nameof(DateFuncArg6), "second");
    public static readonly StringId AboutDateFuncArg6 = new(nameof(AboutDateFuncArg6), "The second component of the Date.");
    public static readonly StringId DateFuncArg7 = new(nameof(DateFuncArg7), "millisecond");
    public static readonly StringId AboutDateFuncArg7 = new(nameof(AboutDateFuncArg7), "The millisecond component of the Date.");
    public static readonly StringId DateFuncArg8 = new(nameof(DateFuncArg8), "ticks");
    public static readonly StringId AboutDateFuncArg8 = new(nameof(AboutDateFuncArg8), "The ticks component of the Date.");

    public static readonly StringId AboutTimeArity6Func = new(nameof(AboutTimeArity6Func), "Creates a Time value using the specified days, hours, minutes, seconds, milliseconds, and ticks.");
    public static readonly StringId TimeFuncArg1 = new(nameof(TimeFuncArg1), "days");
    public static readonly StringId AboutTimeFuncArg1 = new(nameof(AboutTimeFuncArg1), "The days component of the Time.");
    public static readonly StringId TimeFuncArg2 = new(nameof(TimeFuncArg2), "hours");
    public static readonly StringId AboutTimeFuncArg2 = new(nameof(AboutTimeFuncArg2), "The hours component of the Time.");
    public static readonly StringId TimeFuncArg3 = new(nameof(TimeFuncArg3), "minutes");
    public static readonly StringId AboutTimeFuncArg3 = new(nameof(AboutTimeFuncArg3), "The minutes component of the Time.");
    public static readonly StringId TimeFuncArg4 = new(nameof(TimeFuncArg4), "seconds");
    public static readonly StringId AboutTimeFuncArg4 = new(nameof(AboutTimeFuncArg4), "The seconds component of the Time.");
    public static readonly StringId TimeFuncArg5 = new(nameof(TimeFuncArg5), "milliseconds");
    public static readonly StringId AboutTimeFuncArg5 = new(nameof(AboutTimeFuncArg5), "The milliseconds component of the Time.");
    public static readonly StringId TimeFuncArg6 = new(nameof(TimeFuncArg6), "ticks");
    public static readonly StringId AboutTimeFuncArg6 = new(nameof(AboutTimeFuncArg6), "The ticks component of the Time.");

    public static readonly StringId AboutDateYearFunc = new(nameof(AboutDateYearFunc), "Extracts the year component from the Date value.");
    public static readonly StringId AboutDateMonthFunc = new(nameof(AboutDateMonthFunc), "Extracts the month component from the Date value.");
    public static readonly StringId AboutDateDayFunc = new(nameof(AboutDateDayFunc), "Extracts the day component from the Date value.");
    public static readonly StringId AboutDateHourFunc = new(nameof(AboutDateHourFunc), "Extracts the hour component from the Date value.");
    public static readonly StringId AboutDateMinuteFunc = new(nameof(AboutDateMinuteFunc), "Extracts the minute component from the Date value.");
    public static readonly StringId AboutDateSecondFunc = new(nameof(AboutDateSecondFunc), "Extracts the second component from the Date value.");
    public static readonly StringId AboutDateMillisecondFunc = new(nameof(AboutDateMillisecondFunc), "Extracts the millisecond component from the Date value.");
    public static readonly StringId AboutDateTickFunc = new(nameof(AboutDateTickFunc), "Extracts the tick component from the Date value.");
    public static readonly StringId AboutDateTotalTicksFunc = new(nameof(AboutDateTotalTicksFunc), "Returns the total ticks of the Date value.");
    public static readonly StringId AboutDateDayOfYearFunc = new(nameof(AboutDateDayOfYearFunc), "Returns the day of year of the Date value.");
    public static readonly StringId AboutDateDayOfWeekFunc = new(nameof(AboutDateDayOfWeekFunc), "Returns the day of week of the Date value.");
    public static readonly StringId AboutDateStartOfYearFunc = new(nameof(AboutDateStartOfYearFunc), "Returns the first Date value in the year of the specified Date value.");
    public static readonly StringId AboutDateStartOfMonthFunc = new(nameof(AboutDateStartOfMonthFunc), "Returns the first Date value in the month of the specified Date value.");
    public static readonly StringId AboutDateStartOfWeekFunc = new(nameof(AboutDateStartOfWeekFunc), "Returns the first Date value in the week of the specified Date value.");
    public static readonly StringId AboutDateDateFunc = new(nameof(AboutDateDateFunc), "Extracts the date portion and drops the time from the Date value.");
    public static readonly StringId AboutDateTimeFunc = new(nameof(AboutDateTimeFunc), "Extracts the time portion as a Time from the Date value.");
    public static readonly StringId AboutTimeDayFunc = new(nameof(AboutTimeDayFunc), "Extracts the day component from the Time value.");
    public static readonly StringId AboutTimeHourFunc = new(nameof(AboutTimeHourFunc), "Extracts the hour component from the Time value.");
    public static readonly StringId AboutTimeMinuteFunc = new(nameof(AboutTimeMinuteFunc), "Extracts the minute component from the Time value.");
    public static readonly StringId AboutTimeSecondFunc = new(nameof(AboutTimeSecondFunc), "Extracts the second component from the Time value.");
    public static readonly StringId AboutTimeMillisecondFunc = new(nameof(AboutTimeMillisecondFunc), "Extracts the millisecond component from the Time value.");
    public static readonly StringId AboutTimeTickFunc = new(nameof(AboutTimeTickFunc), "Extracts the tick component from the Time value.");
    public static readonly StringId AboutTimeTotalDaysFunc = new(nameof(AboutTimeTotalDaysFunc), "Returns the whole and fractional days of the Time value.");
    public static readonly StringId AboutTimeTotalHoursFunc = new(nameof(AboutTimeTotalHoursFunc), "Returns the whole and fractional hours of the Time value.");
    public static readonly StringId AboutTimeTotalMinutesFunc = new(nameof(AboutTimeTotalMinutesFunc), "Returns the whole and fractional minutes of the Time value.");
    public static readonly StringId AboutTimeTotalSecondsFunc = new(nameof(AboutTimeTotalSecondsFunc), "Returns the whole and fractional seconds of the Time value.");
    public static readonly StringId AboutTimeTotalMillisecondsFunc = new(nameof(AboutTimeTotalMillisecondsFunc), "Returns the whole and fractional milliseconds of the Time value.");
    public static readonly StringId AboutTimeTotalTicksFunc = new(nameof(AboutTimeTotalTicksFunc), "Returns the total ticks of the Time value.");

    public static readonly StringId AboutChronoPart_Value = new(nameof(AboutChronoPart_Value), "The value to extract from.");

    public static readonly StringId AboutCastDate = new(nameof(AboutCastDate), "Casts an optional value to Date, or returns the default (1/1/0001) if not possible.");
    public static readonly StringId AboutCastDate_Value = new(nameof(AboutCastDate_Value), "The value to cast to Date.");

    public static readonly StringId AboutToDate = new(nameof(AboutToDate), "Converts the value to Date, or returns null if not possible.");
    public static readonly StringId AboutToDate_Value = new(nameof(AboutToDate_Value), "The value to convert to Date.");

    public static readonly StringId AboutCastTime = new(nameof(AboutCastTime), "Casts an optional value to Time, or returns the default if not possible.");
    public static readonly StringId AboutCastTime_Value = new(nameof(AboutCastTime_Value), "The value to cast to Time.");

    public static readonly StringId AboutToTime = new(nameof(AboutToTime), "Converts the value to Time, or returns null if not possible.");
    public static readonly StringId AboutToTime_Value = new(nameof(AboutToTime_Value), "The value to convert to Time.");

    public static readonly StringId AboutCastGuid = new(nameof(AboutCastGuid), "Converts an optional value to Guid, or returns the default if not possible.");
    public static readonly StringId AboutCastGuid_Value = new(nameof(AboutCastGuid_Value), "The value to cast to Guid.");

    public static readonly StringId AboutToGuid = new(nameof(AboutToGuid), "Converts the value to Guid, or returns null if not possible.");
    public static readonly StringId AboutToGuid_Value = new(nameof(AboutToGuid_Value), "The value to convert to Guid.");

    public static readonly StringId AboutMakeGuid = new(nameof(AboutMakeGuid), "Generates a Guid. This is a volatile function, not usable in some contexts.");

    public static readonly StringId AboutSetFields = new(nameof(AboutSetFields), "Combines the source record with field definitions to produce a new record, with added, modified, renamed or removed fields.");
    public static readonly StringId AboutSetFields_Source = new(nameof(AboutSetFields_Source), "The source record or sequence of records.");
    public static readonly StringId AboutSetFields_Definition = new(nameof(AboutSetFields_Definition), "A named value ('field_name: value'), defining a field in the result record. If 'value' is the name of a field in the source record, that field is renamed to 'field_name' in the result record. If 'value' is the 'null' literal, then 'field_name' is dropped from the result record.");

    public static readonly StringId AboutAddFields = new(nameof(AboutAddFields), "Combines the source record with field definitions to produce a new record, with added, modified or removed fields.");
    public static readonly StringId AboutAddFields_Source = new(nameof(AboutAddFields_Source), "The source record or sequence of records.");
    public static readonly StringId AboutAddFields_Definition = new(nameof(AboutAddFields_Definition), "A named value ('field_name: value'), defining a field in the result record. If 'value' is the 'null' literal, then 'field_name' is dropped from the result record.");

    public static readonly StringId AboutGroupBy = new(nameof(AboutGroupBy), "Groups the items of the sequence.");
    public static readonly StringId AboutGroupBy_Source = new(nameof(AboutGroupBy_Source), "The sequence whose items to group.");
    public static readonly StringId AboutGroupBy_Key = new(nameof(AboutGroupBy_Key), "Key value to group by. Optionally prefixed with [key] directive to differentiate from 'auto_name' ('[key] name: value').");
    public static readonly StringId GroupBy_Group = new(nameof(GroupBy_Group), "group_selector");
    public static readonly StringId AboutGroupBy_Group = new(nameof(AboutGroupBy_Group), "The value computed from a group. Prefixed with [group] directive ('[group] name: value').");
    public static readonly StringId GroupBy_Item = new(nameof(GroupBy_Item), "item_selector");
    public static readonly StringId AboutGroupBy_Item = new(nameof(AboutGroupBy_Item), "The value computed from each item of a group. Prefixed with [item] directive ('[item] name: value').");
    public static readonly StringId GroupBy_Auto = new(nameof(GroupBy_Auto), "auto_name");
    public static readonly StringId AboutGroupBy_Auto = new(nameof(AboutGroupBy_Auto), "Field name for the group sequence (with any key fields dropped).");

    public static readonly StringId AboutAbs = new(nameof(AboutAbs), "Computes the absolute value of the number.");
    public static readonly StringId AboutAbs_Source = new(nameof(AboutAbs_Source), "The number to get absolute value.");

    public static readonly StringId AboutSqrt = new(nameof(AboutSqrt), "Computes the square root of the specified number.");
    public static readonly StringId AboutMathFuncArgValue = new(nameof(AboutMathFuncArgValue), "The value.");
    public static readonly StringId AboutExp = new(nameof(AboutExp), "Computes e raised to the specified power.");
    public static readonly StringId AboutExpArg = new(nameof(AboutExpArg), "The value specifying the power.");
    public static readonly StringId AboutLn = new(nameof(AboutLn), "Computes the natural logarithm of the specified number.");
    public static readonly StringId AboutLog10 = new(nameof(AboutLog10), "Computes the base 10 logarithm of the specified number.");
    public static readonly StringId AboutRadians = new(nameof(AboutRadians), "Converts the specified angle measured in degrees to an angle measured in radians.");
    public static readonly StringId AboutDegrees = new(nameof(AboutDegrees), "Converts the specified angle measured in radians to an angle measured in degrees.");
    public static readonly StringId AboutSin = new(nameof(AboutSin), "Computes the sine of the specified angle in radians.");
    public static readonly StringId AboutCos = new(nameof(AboutCos), "Computes the cosine of the specified angle in radians.");
    public static readonly StringId AboutTan = new(nameof(AboutTan), "Computes the tangent of the specified angle in radians.");
    public static readonly StringId AboutCsc = new(nameof(AboutCsc), "Computes the cosecant of the specified angle in radians.");
    public static readonly StringId AboutSec = new(nameof(AboutSec), "Computes the secant of the specified angle in radians.");
    public static readonly StringId AboutCot = new(nameof(AboutCot), "Computes the cotangent of the specified angle in radians.");
    public static readonly StringId AboutSinD = new(nameof(AboutSinD), "Computes the sine of the specified angle in degrees.");
    public static readonly StringId AboutCosD = new(nameof(AboutCosD), "Computes the cosine of the specified angle in degrees.");
    public static readonly StringId AboutTanD = new(nameof(AboutTanD), "Computes the tangent of the specified angle in degrees.");
    public static readonly StringId AboutCscD = new(nameof(AboutCscD), "Computes the cosecant of the specified angle in degrees.");
    public static readonly StringId AboutSecD = new(nameof(AboutSecD), "Computes the secant of the specified angle in degrees.");
    public static readonly StringId AboutCotD = new(nameof(AboutCotD), "Computes the cotangent of the specified angle in degrees.");
    public static readonly StringId AboutSinh = new(nameof(AboutSinh), "Computes the hyperbolic sine of the specified value.");
    public static readonly StringId AboutCosh = new(nameof(AboutCosh), "Computes the hyperbolic cosine of the specified value.");
    public static readonly StringId AboutTanh = new(nameof(AboutTanh), "Computes the hyperbolic tangent of the specified value.");
    public static readonly StringId AboutCsch = new(nameof(AboutCsch), "Computes the hyperbolic cosecant of the specified value.");
    public static readonly StringId AboutSech = new(nameof(AboutSech), "Computes the hyperbolic secant of the specified value.");
    public static readonly StringId AboutCoth = new(nameof(AboutCoth), "Computes the hyperbolic cotangent of the specified value.");
    public static readonly StringId AboutAsin = new(nameof(AboutAsin), "Computes the angle in radians whose sine is the specified number.");
    public static readonly StringId AboutAsinArg = new(nameof(AboutAsinArg), "The sine value.");
    public static readonly StringId AboutAcos = new(nameof(AboutAcos), "Computes the angle in radians whose cosine is the specified number.");
    public static readonly StringId AboutAcosArg = new(nameof(AboutAcosArg), "The cosine value.");
    public static readonly StringId AboutAtan = new(nameof(AboutAtan), "Computes the angle in radians whose tangent is the specified number.");
    public static readonly StringId AboutAtanArg = new(nameof(AboutAtanArg), "The tangent value.");
    public static readonly StringId AboutRound = new(nameof(AboutRound), "Rounds the value to the nearest integer, and rounds midpoint values to the nearest even integer.");
    public static readonly StringId AboutRoundArg = new(nameof(AboutRoundArg), "The value to be rounded.");
    public static readonly StringId AboutRoundUp = new(nameof(AboutRoundUp), "Returns the smallest integral value greater than or equal to the specified number.");
    public static readonly StringId AboutRoundDown = new(nameof(AboutRoundDown), "Returns the largest integral value less than or equal to the specified number.");
    public static readonly StringId AboutRoundIn = new(nameof(AboutRoundIn), "Returns to the nearest integer towards zero.");
    public static readonly StringId AboutRoundOut = new(nameof(AboutRoundOut), "Returns to the nearest integer away from zero.");
    public static readonly StringId AboutAngleInRadians = new(nameof(AboutAngleInRadians), "The angle, measured in radians.");
    public static readonly StringId AboutAngleInDegrees = new(nameof(AboutAngleInDegrees), "The angle, measured in degrees.");

    public static readonly StringId AboutTTestOneSample = new(nameof(AboutTTestOneSample), "Performs a one-sample t-test.");
    public static readonly StringId AboutTTestOneSample_X = new(nameof(AboutTTestOneSample_X), "The sample as a sequence of values.");
    public static readonly StringId TTestOneSample_Mean = new(nameof(TTestOneSample_Mean), "pop_mean");
    public static readonly StringId AboutTTestOneSample_Mean = new(nameof(AboutTTestOneSample_Mean), "The hypothesized population mean. Defaults to 0.");

    public static readonly StringId AboutTTestTwoSample = new(nameof(AboutTTestTwoSample), "Performs a two-sample t-test, also known as an independent samples t-test.");
    public static readonly StringId TTestTwoSample_EqualVar = new(nameof(TTestTwoSample_EqualVar), "equal_var");
    public static readonly StringId AboutTTestTwoSample_EqualVar = new(nameof(AboutTTestTwoSample_EqualVar), "Whether to assume equal population variances. If true, performs a standard Student's t-test. If false, performs a Welch's unequal variances t-test. Defaults to false.");
    public static readonly StringId AboutTTestPairedNoSel = new(nameof(AboutTTestPairedNoSel), "Performs a paired samples t-test, also known as a dependent or related samples t-test. If given samples of unequal length, the longer sample will be truncated to the length of the shorter sample.");
    public static readonly StringId AboutTTest_X = new(nameof(AboutTTest_X), "The first sample as a sequence of values.");
    public static readonly StringId AboutTTest_Y = new(nameof(AboutTTest_Y), "The second sample as a sequence of values.");

    public static readonly StringId AboutTTestPairedSel = new(nameof(AboutTTestPairedSel), "Performs a paired samples t-test, also known as a dependent or related samples t-test. Values in each sample are generated by evaluating selectors against the source sequence.");
    public static readonly StringId AboutTTestPairedSel_Source = new(nameof(AboutTTestPairedSel_Source), "The source sequence.");
    public static readonly StringId AboutTTestPairedSel_X = new(nameof(AboutTTestPairedSel_X), "Selector for each value in the first sample.");
    public static readonly StringId AboutTTestPairedSel_Y = new(nameof(AboutTTestPairedSel_Y), "Selector for each value in the second sample.");

    // "XxxDir" means the direct single arg version, aggregating a sequence of values.
    // "XxxSel" means the source/selector version, aggregating a selector value.

    public static readonly StringId AboutSumDir = new(nameof(AboutSumDir), "Computes the sum of the sequence.");
    public static readonly StringId AboutSumSel = new(nameof(AboutSumSel), "Computes the sum of the selector value over items of the source sequence(s).");
    public static readonly StringId AboutSumBigDir = new(nameof(AboutSumBigDir), "Computes the sum of the sequence and produces the best precision result.");
    public static readonly StringId AboutSumBigSel = new(nameof(AboutSumBigSel), "Computes the sum of the selector value over items of the source sequence(s) and produces the best precision result.");
    public static readonly StringId AboutSumKahDir = new(nameof(AboutSumKahDir), "Computes the sum of the sequence using Kahan floating point compensation.");
    public static readonly StringId AboutSumKahSel = new(nameof(AboutSumKahSel), "Computes the sum of the selector value over items of the source sequence(s) using Kahan floating point compensation.");
    public static readonly StringId AboutSumDir_Source = new(nameof(AboutSumDir_Source), "The sequence to sum.");
    public static readonly StringId AboutSumSel_Source = new(nameof(AboutSumSel_Source), "The source sequence.");
    public static readonly StringId AboutSumSel_Selector = new(nameof(AboutSumSel_Selector), "The value to sum.");

    public static readonly StringId AboutMeanDir = new(nameof(AboutMeanDir), "Computes the average of the sequence.");
    public static readonly StringId AboutMeanSel = new(nameof(AboutMeanSel), "Computes the average of the selector value over items of the source sequence(s).");
    public static readonly StringId AboutMeanDir_Source = new(nameof(AboutMeanDir_Source), "The sequence to average.");
    public static readonly StringId AboutMeanSel_Source = new(nameof(AboutMeanSel_Source), "The source sequence.");
    public static readonly StringId AboutMeanSel_Selector = new(nameof(AboutMeanSel_Selector), "The value to average.");

    public static readonly StringId AboutMinDir = new(nameof(AboutMinDir), "Computes the minimum value of the sequence.");
    public static readonly StringId AboutMaxDir = new(nameof(AboutMaxDir), "Computes the maximum value of the sequence.");
    public static readonly StringId AboutMinMaxDir = new(nameof(AboutMinMaxDir), "Computes the minimum and maximum values of the sequence.");
    public static readonly StringId AboutMinSel = new(nameof(AboutMinSel), "Computes the minimum of the selector value over items of the source sequence(s).");
    public static readonly StringId AboutMaxSel = new(nameof(AboutMaxSel), "Computes the maximum of the selector value over items of the source sequence(s).");
    public static readonly StringId AboutMinMaxSel = new(nameof(AboutMinMaxSel), "Computes the minimum and maximum values of the selector value over items of the source sequence(s).");
    public static readonly StringId AboutMinDir_Source = new(nameof(AboutMinDir_Source), "The sequence to minimize.");
    public static readonly StringId AboutMaxDir_Source = new(nameof(AboutMaxDir_Source), "The sequence to maximize.");
    public static readonly StringId AboutMinMaxDir_Source = new(nameof(AboutMinMaxDir_Source), "The sequence to minimize and maximize.");
    public static readonly StringId AboutMinSel_Selector = new(nameof(AboutMinSel_Selector), "The value to minimize.");
    public static readonly StringId AboutMaxSel_Selector = new(nameof(AboutMaxSel_Selector), "The value to maximize.");
    public static readonly StringId AboutMinMaxSel_Selector = new(nameof(AboutMinMaxSel_Selector), "The value to minimize and maximize.");

    public static readonly StringId AboutDiv = new(nameof(AboutDiv), "Computes 'x' divided by 'y' then rounds toward zero to the nearest integer.");
    public static readonly StringId AboutDiv_X = new(nameof(AboutDiv_X), "The numerator to be divided by denominator.");
    public static readonly StringId AboutDiv_Y = new(nameof(AboutDiv_Y), "The denominator by which numerator is to be divided.");

    public static readonly StringId AboutMod = new(nameof(AboutMod), "Computes the remainder when 'x' is divided by 'y' with the quotient rounded toward zero to the nearest integer value. Note that the sign of the result is the same as the sign of 'x'.");
    public static readonly StringId AboutMod_X = new(nameof(AboutMod_X), "The numerator to be divided by denominator.");
    public static readonly StringId AboutMod_Y = new(nameof(AboutMod_Y), "The denominator by which numerator is to be divided.");

    public static readonly StringId AboutBin = new(nameof(AboutBin), "Computes 'x' rounded towards zero to the nearest integer multiple of 'y'.");
    public static readonly StringId AboutBin_X = new(nameof(AboutBin_X), "The number to round toward zero.");
    public static readonly StringId AboutBin_Y = new(nameof(AboutBin_Y), "The bin size. Note that its sign does not affect the result.");

    public static readonly StringId AboutRandomUniformUnit = new(nameof(AboutRandomUniformUnit), "Generates random real numbers distributed uniformly over the unit interval [0, 1).");
    public static readonly StringId AboutRandomUniform_Seed = new(nameof(AboutRandomUniform_Seed), "Seed to use for random number generation.");
    public static readonly StringId AboutRandomUniform_Count = new(nameof(AboutRandomUniform_Count), "If given, generate a sequence of this many random numbers.");
    public static readonly StringId AboutRandomUniformRange = new(nameof(AboutRandomUniformRange), "Generates random real numbers distributed uniformly over the given interval.");
    public static readonly StringId AboutRandomUniform_X = new(nameof(AboutRandomUniform_X), "First bound of the interval.");
    public static readonly StringId AboutRandomUniform_Y = new(nameof(AboutRandomUniform_Y), "Second bound of the interval.");

    public static readonly StringId AboutDateNow = new(nameof(AboutDateNow), "Returns a record with the current date (and time of day) in both 'Utc' and 'Local', together with the time zone 'Offset'");
    public static readonly StringId AboutDateNowUtc = new(nameof(AboutDateNowUtc), "Returns the current UTC date (and time of day)");
    public static readonly StringId AboutDateNowLoc = new(nameof(AboutDateNowLoc), "Returns the current local date (and time of day)");

    public static readonly StringId AboutLower = new(nameof(AboutLower), "Converts text to lowercase.");
    public static readonly StringId AboutLower_Source = new(nameof(AboutLower_Source), "The text to convert to lowercase.");

    public static readonly StringId AboutUpper = new(nameof(AboutUpper), "Converts text to uppercase.");
    public static readonly StringId AboutUpper_Source = new(nameof(AboutUpper_Source), "The text to convert to uppercase.");

    public static readonly StringId AboutStartsWith = new(nameof(AboutStartsWith), "Tests whether the beginning of the source text matches the lookup text.");
    public static readonly StringId AboutStartsWith_Source = new(nameof(AboutStartsWith_Source), "The text to look in.");
    public static readonly StringId AboutStartsWith_LookUp = new(nameof(AboutStartsWith_LookUp), "The text to look for.");

    public static readonly StringId AboutEndsWith = new(nameof(AboutEndsWith), "Tests whether the end of the source text matches the lookup text.");

    public static readonly StringId AboutStrLen = new(nameof(AboutStrLen), "Returns the number of characters in the text value.");
    public static readonly StringId AboutStrLen_Source = new(nameof(AboutStrLen_Source), "The text in which to count the characters.");

    public static readonly StringId AboutStrJoin = new(nameof(AboutStrJoin), "Concatenates the members of the sequence of text values using the specified seperator.");
    public static readonly StringId AboutStrJoin_Source = new(nameof(AboutStrJoin_Source), "The sequence of text values to concatenate.");
    public static readonly StringId StrJoinFuncArg2 = new(nameof(StrJoinFuncArg2), "separator");
    public static readonly StringId AboutStrJoinFuncArg2 = new(nameof(AboutStrJoinFuncArg2), "The separator used for concatenation.");

    public static readonly StringId AboutStrIndexOf = new(nameof(AboutStrIndexOf), "Returns the index of the first occurrence of the lookup text in the source text, within the optional range ['min_index', 'lim_index'). Returns -1 if not found.");
    public static readonly StringId AboutStrLastIndexOf = new(nameof(AboutStrLastIndexOf), "Returns the index of the last occurrence of the lookup text in the source text, ending at the optional lim index. Returns -1 if not found.");
    public static readonly StringId AboutStrLastIndexOf_WithRange = new(nameof(AboutStrLastIndexOf_WithRange), "Returns the index of the last occurrence of the lookup text in the source text, within the range ['min_index', 'lim_index'). Returns -1 if not found.");
    public static readonly StringId AboutStrIndexOf_Source = new(nameof(AboutStrIndexOf_Source), "The text to look in.");
    public static readonly StringId StrIndexOf_LookUp = new(nameof(StrIndexOf_LookUp), "lookup");
    public static readonly StringId AboutStrIndexOf_LookUp = new(nameof(AboutStrIndexOf_LookUp), "The text to look for.");
    public static readonly StringId AboutStrIndexOf_MinIndex = new(nameof(AboutStrIndexOf_MinIndex), "The minimum index of the search range.");
    public static readonly StringId AboutStrLastIndexOf_LimIndex = new(nameof(AboutStrLastIndexOf_LimIndex), "The limit index of the search range.");

    public static readonly StringId AboutStrPart = new(nameof(AboutStrPart), "Returns the segment of the source text starting and ending at the given indices.");
    public static readonly StringId AboutStrPart_Source = new(nameof(AboutStrPart_Source), "The source text containing the segment.");
    public static readonly StringId AboutStrPart_MinIndex = new(nameof(AboutStrPart_MinIndex), "The minimum index of the segment.");
    public static readonly StringId AboutStrPart_LimIndex = new(nameof(AboutStrPart_LimIndex), "The limit index of the segment.");

    public static readonly StringId AboutTrim = new(nameof(AboutTrim), "Trims leading and trailing whitespace characters from text.");
    public static readonly StringId AboutTrim_Source = new(nameof(AboutTrim_Source), "The text to trim.");

    public static readonly StringId AboutTrimStart = new(nameof(AboutTrimStart), "Trims leading whitespace characters from text.");
    public static readonly StringId AboutTrimStart_Source = new(nameof(AboutTrimStart_Source), "The text to trim.");

    public static readonly StringId AboutTrimEnd = new(nameof(AboutTrimEnd), "Trims trailing whitespace characters from text.");
    public static readonly StringId AboutTrimEnd_Source = new(nameof(AboutTrimEnd_Source), "The text to trim.");

    public static readonly StringId AboutTextReplace = new(nameof(AboutTextReplace), "Replaces all instances of 'remove' with 'insert' in the text value 'source'.");
    public static readonly StringId AboutTextReplace_Source = new(nameof(AboutTextReplace_Source), "The text value to be searched.");
    public static readonly StringId AboutTextReplace_Remove = new(nameof(AboutTextReplace_Remove), "The text value to search for and remove.");
    public static readonly StringId AboutTextReplace_Insert = new(nameof(AboutTextReplace_Insert), "The text value to use in place of removed occurrences.");
    public static readonly StringId TextReplace_Remove = new(nameof(TextReplace_Remove), "remove");
    public static readonly StringId TextReplace_Insert = new(nameof(TextReplace_Insert), "insert");

    public static readonly StringId AboutIsNull = new(nameof(AboutIsNull), "Tests whether the source value is null.");
    public static readonly StringId AboutIsNull_Source = new(nameof(AboutIsNull_Source), "The source value to be tested.");

    public static readonly StringId AboutIsEmpty = new(nameof(AboutIsEmpty), "Tests whether the sequence or text is empty or null.");
    public static readonly StringId AboutIsEmpty_Source = new(nameof(AboutIsEmpty_Source), "The source sequence or text to be tested.");

    public static readonly StringId AboutGuard = new(nameof(AboutGuard), "Evaluates selector in the context of definition(s). A null definition value will result in a null result.");
    public static readonly StringId AboutGuard_Definition = new(nameof(AboutGuard_Definition), "An optionally named value ('name: value') in scope in subsequent definitions and in the selector.");
    public static readonly StringId AboutGuard_Selector = new(nameof(AboutGuard_Selector), "The value to produce.");

    public static readonly StringId AboutWith = new(nameof(AboutWith), "Evaluates selector in the context of definition(s).");
    public static readonly StringId AboutWith_Definition = new(nameof(AboutWith_Definition), "An optionally named value ('name: value') in scope in subsequent definitions and in the selector.");
    public static readonly StringId AboutWith_Selector = new(nameof(AboutWith_Selector), "The value to produce.");

    public static readonly StringId AboutIf = new(nameof(AboutIf), "Tests the provided conditions until one is true and returns the corresponding value.");
    public static readonly StringId IfFuncCondition = new(nameof(IfFuncCondition), "condition");
    public static readonly StringId AboutIf_Predicate = new(nameof(AboutIf_Predicate), "The condition to test.");
    public static readonly StringId IfFuncThenValue = new(nameof(IfFuncThenValue), "value");
    public static readonly StringId AboutIf_TrueValue = new(nameof(AboutIf_TrueValue), "The value returned when the condition is true.");
    public static readonly StringId IfFuncElseValue = new(nameof(IfFuncElseValue), "else_value");
    public static readonly StringId AboutIf_FalseValue = new(nameof(AboutIf_FalseValue), "The value returned when no condition is true.");

    public static readonly StringId AboutCastR8 = new(nameof(AboutCastR8), "Converts the value to 8-byte float without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastR4 = new(nameof(AboutCastR4), "Converts the value to 4-byte float without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastIA = new(nameof(AboutCastIA), "Converts the value to arbitrary precision integer without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastI8 = new(nameof(AboutCastI8), "Converts the value to 8-byte int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastI4 = new(nameof(AboutCastI4), "Converts the value to 4-byte int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastI2 = new(nameof(AboutCastI2), "Converts the value to 2-byte int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastI1 = new(nameof(AboutCastI1), "Converts the value to 1-byte int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastU8 = new(nameof(AboutCastU8), "Converts the value to 8-byte unsigned int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastU4 = new(nameof(AboutCastU4), "Converts the value to 4-byte unsigned int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastU2 = new(nameof(AboutCastU2), "Converts the value to 2-byte unsigned int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastU1 = new(nameof(AboutCastU1), "Converts the value to 1-byte unsigned int without boundary checks. Returns 0 if the conversion is not possible.");
    public static readonly StringId AboutCastXX_Value = new(nameof(AboutCastXX_Value), "The value to convert.");

    public static readonly StringId AboutToR8Arity2 = new(nameof(AboutToR8Arity2), "Converts the value to 8-byte float, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToR4Arity2 = new(nameof(AboutToR4Arity2), "Converts the value to 4-byte float, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToIAArity2 = new(nameof(AboutToIAArity2), "Converts the value to arbitrary precision integer, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToI8Arity2 = new(nameof(AboutToI8Arity2), "Converts the value to 8-byte int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToI4Arity2 = new(nameof(AboutToI4Arity2), "Converts the value to 4-byte int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToI2Arity2 = new(nameof(AboutToI2Arity2), "Converts the value to 2-byte int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToI1Arity2 = new(nameof(AboutToI1Arity2), "Converts the value to 1-byte int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToU8Arity2 = new(nameof(AboutToU8Arity2), "Converts the value to 8-byte unsigned int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToU4Arity2 = new(nameof(AboutToU4Arity2), "Converts the value to 4-byte unsigned int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToU2Arity2 = new(nameof(AboutToU2Arity2), "Converts the value to 2-byte unsigned int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutToU1Arity2 = new(nameof(AboutToU1Arity2), "Converts the value to 1-byte unsigned int, or returns the else value (or null) if not possible.");
    public static readonly StringId AboutTo = new(nameof(AboutTo), "Converts the value to the type of the default value. Returns the else value (or null) if not possible.");
    public static readonly StringId AboutToXX_Value = new(nameof(AboutToXX_Value), "The value to convert.");
    public static readonly StringId ToXXArg2 = new(nameof(ToXXArg2), "default");
    public static readonly StringId AboutToXXArg2 = new(nameof(AboutToXXArg2), "The default value to return if the conversion is not possible.");

    public static readonly StringId AboutToText = new(nameof(AboutToText), "Converts the value to a text representation.");
    public static readonly StringId AboutToText_Value = new(nameof(AboutToText_Value), "The value to represent.");
    public static readonly StringId ToTextArg2 = new(nameof(ToTextArg2), "format");
    public static readonly StringId AboutToTextArg2 = new(nameof(AboutToTextArg2), "The format to represent the value.");

    public static readonly StringId AboutCrossJoin = new(nameof(AboutCrossJoin), "Joins two sequences via the match predicate and selector, with optional unmatched left and right selectors.");
    public static readonly StringId CrossJoinArg0 = new(nameof(CrossJoinArg0), "left_sequence");
    public static readonly StringId CrossJoinArg1 = new(nameof(CrossJoinArg1), "right_sequence");
    public static readonly StringId CrossJoinArg2 = new(nameof(CrossJoinArg2), "match_predicate");
    public static readonly StringId CrossJoinArg3 = new(nameof(CrossJoinArg3), "match_selector");
    public static readonly StringId CrossJoinArg4 = new(nameof(CrossJoinArg4), "unmatched_left_selector");
    public static readonly StringId CrossJoinArg5 = new(nameof(CrossJoinArg5), "unmatched_right_selector");
    public static readonly StringId AboutCrossJoinArg0 = new(nameof(AboutCrossJoinArg0), "The left sequence to join.");
    public static readonly StringId AboutCrossJoinArg1 = new(nameof(AboutCrossJoinArg1), "The right sequence to join.");
    public static readonly StringId AboutCrossJoinArg2 = new(nameof(AboutCrossJoinArg2), "The match condition.");
    public static readonly StringId AboutCrossJoinArg3 = new(nameof(AboutCrossJoinArg3), "The selector for a match.");
    public static readonly StringId AboutCrossJoinArg4 = new(nameof(AboutCrossJoinArg4), "The selector for an unmatched value from the left sequence.");
    public static readonly StringId AboutCrossJoinArg5 = new(nameof(AboutCrossJoinArg5), "The selector for an unmatched value from the right sequence.");

    public static readonly StringId AboutKeyJoin = new(nameof(AboutKeyJoin), "Joins two sequences using match keys and selector, with optional unmatched left and right selectors.");
    public static readonly StringId KeyJoinArg0 = new(nameof(KeyJoinArg0), "left_sequence");
    public static readonly StringId KeyJoinArg1 = new(nameof(KeyJoinArg1), "right_sequence");
    public static readonly StringId KeyJoinArg2 = new(nameof(KeyJoinArg2), "left_match_key");
    public static readonly StringId KeyJoinArg3 = new(nameof(KeyJoinArg3), "right_match_key");
    public static readonly StringId KeyJoinArg4 = new(nameof(KeyJoinArg4), "match_selector");
    public static readonly StringId KeyJoinArg5 = new(nameof(KeyJoinArg5), "unmatched_left_selector");
    public static readonly StringId KeyJoinArg6 = new(nameof(KeyJoinArg6), "unmatched_right_selector");
    public static readonly StringId AboutKeyJoinArg0 = new(nameof(AboutKeyJoinArg0), "The left sequence to join.");
    public static readonly StringId AboutKeyJoinArg1 = new(nameof(AboutKeyJoinArg1), "The right sequence to join.");
    public static readonly StringId AboutKeyJoinArg2 = new(nameof(AboutKeyJoinArg2), "The left key value.");
    public static readonly StringId AboutKeyJoinArg3 = new(nameof(AboutKeyJoinArg3), "The right key value.");
    public static readonly StringId AboutKeyJoinArg4 = new(nameof(AboutKeyJoinArg4), "The selector for a match.");
    public static readonly StringId AboutKeyJoinArg5 = new(nameof(AboutKeyJoinArg5), "The selector for an unmatched value from the left sequence.");
    public static readonly StringId AboutKeyJoinArg6 = new(nameof(AboutKeyJoinArg6), "The selector for an unmatched value from the right sequence.");

    public static readonly StringId AboutTupleItem = new(nameof(AboutTupleItem), "Extracts a slot value from a tuple");
    public static readonly StringId AboutTupleItem_Source = new(nameof(AboutTupleItem_Source), "The tuple from which to extract a slot value.");

    public static readonly StringId AboutTupleLen = new(nameof(AboutTupleLen), "Returns the number of slots in a tuple.");
    public static readonly StringId AboutTupleLen_Source = new(nameof(AboutTupleLen_Source), "The source tuple.");

    public static readonly StringId AboutFold = new(nameof(AboutFold), "Computes a value from the source sequence and the seed by repeatedly evaluating an iteration selector.");
    public static readonly StringId AboutFold_Source = new(nameof(AboutFold_Source), "The sequence to compute from, typically including an item name ('name: sequence').");
    public static readonly StringId AboutFold_IterSelector = new(nameof(AboutFold_IterSelector), "The new iteration value computed from the current item of the sequence and the previous iteration value.");
    public static readonly StringId AboutFold_ResSelector = new(nameof(AboutFold_ResSelector), "The final result computed from the last iteration value.");

    public static readonly StringId AboutScan = new(nameof(AboutScan), "Computes a sequence of values from the source sequence and the seed by repeatedly evaluating an iteration selector.");
    public static readonly StringId AboutScan_Source = new(nameof(AboutScan_Source), "The sequence to compute from, typically including an item name ('name: sequence').");
    public static readonly StringId AboutScan_IterSelector = new(nameof(AboutScan_IterSelector), "The new iteration value computed from the current item of the sequence and the previous iteration value.");
    public static readonly StringId AboutScanX_ResSelector = new(nameof(AboutScanX_ResSelector), "The result value computed from the iteration value.");

    public static readonly StringId AboutScanZ_ResSelector = new(nameof(AboutScanZ_ResSelector), "The result value computed from the iteration value and source item.");

    public static readonly StringId AboutGenerateNoState = new(nameof(AboutGenerateNoState), "Generates a sequence of 'count' values, where each result value is computed from its index.");
    public static readonly StringId AboutGenerateNoState_Selector = new(nameof(AboutGenerateNoState_Selector), "The result value computed from the current index.");

    public static readonly StringId AboutGenerate = new(nameof(AboutGenerate), "Generates a sequence of 'count' values, where each result value is computed from an iteration value. The iteration value is initialized to 'seed' and is updated by repeatedly evaluating an iteration selector with access to the current index.");
    public static readonly StringId AboutGenerate_Count = new(nameof(AboutGenerate_Count), "The number of items to generate.");
    public static readonly StringId AboutGenerate_IterSelector = new(nameof(AboutGenerate_IterSelector), "The new iteration value computed from the current index and the previous iteration value.");
    public static readonly StringId AboutGenerate_ResSelector = new(nameof(AboutGenerate_ResSelector), "The result value computed from the iteration value.");

    // UDF signature and parameter default descriptions.
    public static readonly StringId AboutUdf = new(nameof(AboutUdf), "User defined function.");
    public static readonly StringId ArgUdf = new(nameof(ArgUdf), "Udf parameter.");

    public static readonly StringId AboutLinkTo = new(nameof(AboutLinkTo), "Produces a link to a resource or null.");
    public static readonly StringId LinkTo_Url = new(nameof(LinkTo_Url), "url");
    public static readonly StringId AboutLinkTo_Url = new(nameof(AboutLinkTo_Url), "URL of the resource.");
    public static readonly StringId LinkTo_AccountId = new(nameof(LinkTo_AccountId), "account_id");
    public static readonly StringId AboutLinkTo_AccountId = new(nameof(AboutLinkTo_AccountId), "Account ID.");
    public static readonly StringId LinkTo_Path = new(nameof(LinkTo_Path), "path");
    public static readonly StringId AboutLinkTo_Path = new(nameof(AboutLinkTo_Path), "The path of the resource.");

    public static readonly StringId AboutLinkKind = new(nameof(AboutLinkKind), "Extracts the kind of the link.");
    public static readonly StringId AboutLinkAccount = new(nameof(AboutLinkAccount), "Extracts the account of the link or null if it doesn't have one.");
    public static readonly StringId AboutLinkPath = new(nameof(AboutLinkPath), "Extracts the path of the link.");
    public static readonly StringId LinkProp_Link = new(nameof(LinkProp_Link), "link");
    public static readonly StringId AboutLinkProp_Link = new(nameof(AboutLinkProp_Link), "The link.");
    public static readonly StringId LinkTo_Flavor = new(nameof(LinkTo_Flavor), "flavor");
    public static readonly StringId AboutLinkTo_Flavor = new(nameof(AboutLinkTo_Flavor), "Type of the resource, e.g. \"Image\", \"Video\", \"Text\", \"Image.Xray\", etc.");

}
