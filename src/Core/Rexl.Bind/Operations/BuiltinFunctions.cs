// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using A = Argument;
using Args = Immutable.Array<Argument>;
using Grps = Immutable.Array<Signature.Group>;
using Opt = Signature.OptionalGroup;
using Rep = Signature.RepetitiveGroup;
using S = RexlStrings;
using Sig = Signature;
using Sigs = Immutable.Array<Signature>;

/// <summary>
/// The standard "builtin" functions for rexl.
/// </summary>
public class BuiltinFunctions : OperationRegistry
{
    public static readonly BuiltinFunctions Instance = new BuiltinFunctions();

    private BuiltinFunctions()
    {
        var rep10 = Rep.Create(1, 0);
        var rep11 = Rep.Create(1, 1);
        var opt01 = Opt.Create(0, 1);
        var opt12 = Opt.Create(1, 2);
        var opt23 = Opt.Create(2, 3);
        var opt34 = Opt.Create(3, 4);
        var srcSeq = A.Create(S.ArgSource, S.AboutArgSeqSource);

        {
            var src = A.Create(S.ArgSource, S.AboutForEach_Source);
            var sel = A.Create(S.ArgSelector, S.AboutForEach_Selector);
            var sig = new Sig(RexlStrings.AboutForEach, Args.Create(src, sel), rep10);
            var sigIf = new Sig(S.AboutForEach_WithIfPred, Args.Create(src, A.Create(S.ArgIfPredicate, S.AboutArgIfPredicate), sel), rep10);
            var sigWh = new Sig(S.AboutForEach_WithWhilePred, Args.Create(src, A.Create(S.ArgWhilePredicate, S.AboutArgWhilePredicate), sel), rep10);

            AddOne(ForEachFunc.ForEach, Sigs.Create(sig, sigIf, sigWh));
            AddOne(ForEachFunc.ForEach, "Map");
            AddOne(ForEachFunc.ForEach, "Zip");
            AddOne(ForEachFunc.ForEachIf, sigIf);
            AddOne(ForEachFunc.ForEachWhile, sigWh);
        }

        AddOne(CrossJoinFunc.Instance,
            new Sig(S.AboutCrossJoin,
                Args.Create(
                    A.Create(S.CrossJoinArg0, S.AboutCrossJoinArg0),
                    A.Create(S.CrossJoinArg1, S.AboutCrossJoinArg1),
                    A.Create(S.CrossJoinArg2, S.AboutCrossJoinArg2),
                    A.Create(S.CrossJoinArg3, S.AboutCrossJoinArg3),
                    A.Create(S.CrossJoinArg4, S.AboutCrossJoinArg4),
                    A.Create(S.CrossJoinArg5, S.AboutCrossJoinArg5)),
                Opt.Create(4, 6)));
        AddOne(KeyJoinFunc.Instance,
            new Sig(S.AboutKeyJoin,
                Args.Create(
                    A.Create(S.KeyJoinArg0, S.AboutKeyJoinArg0),
                    A.Create(S.KeyJoinArg1, S.AboutKeyJoinArg1),
                    A.Create(S.KeyJoinArg2, S.AboutKeyJoinArg2),
                    A.Create(S.KeyJoinArg3, S.AboutKeyJoinArg3),
                    A.Create(S.KeyJoinArg4, S.AboutKeyJoinArg4),
                    A.Create(S.KeyJoinArg5, S.AboutKeyJoinArg5),
                    A.Create(S.KeyJoinArg6, S.AboutKeyJoinArg6)),
                Opt.Create(5, 7)));
        AddOne(DistinctFunc.Instance,
            new Sig(S.AboutDistinct,
                Args.Create(
                    A.Create(S.ArgSource, S.AboutDistinct_Source),
                    A.Create(S.ArgKeySelector, S.AboutDistinct_Key)),
                opt12));

        {
            var argOne = A.Create(S.ArgSource, S.AboutSort_SourceOnly);
            var argsTwo = Args.Create(
                A.Create(S.ArgSource, S.AboutSort_Source),
                A.Create(S.ArgSelector, S.AboutSort_Selector));
            AddOne(SortFunc.Sort, Sigs.Create(new Sig(S.AboutSort, argOne), new Sig(S.AboutSort, argsTwo, rep11)));
            AddOne(SortFunc.SortUp, Sigs.Create(new Sig(S.AboutSortUp, argOne), new Sig(S.AboutSortUp, argsTwo, rep11)));
            AddOne(SortFunc.SortDown, Sigs.Create(new Sig(S.AboutSortDown, argOne), new Sig(S.AboutSortDown, argsTwo, rep11)));
        }

        AddOne(ReverseFunc.Instance, Sigs.Create(new Sig(S.AboutReverse, A.Create(S.ArgSource, S.AboutReverse_Source))));

        AddOne(ChainFunc.Instance,
            new Sig(S.AboutChain, A.Create(S.ArgSource, S.AboutChain_Source), rep10));
        AddOne(ChainMapFunc.Instance,
            Sigs.Create(
                new Sig(S.AboutChainMap, A.Create(S.ArgSource, S.AboutChainMap_Source)),
                new Sig(S.AboutChainMapArity2,
                    Args.Create(
                        A.Create(S.ArgSource, S.AboutChainMap_Source),
                        A.Create(S.ArgSelector, S.AboutChainMap_Selector)),
                    rep10)));
        AddOneDep(ChainMapFunc.Instance, "GlueMap"); // Deprecated at the end of 9.1.

        AddOne(TakeAtFunc.Instance,
            new Sig(S.AboutTakeAt,
                Args.Create(
                    srcSeq,
                    A.Create(S.ArgIndex, S.AboutTakeAt_Index),
                    A.Create(S.ArgElse, S.AboutTakeAt_Else)),
                opt23));
        AddOne(TakeOneFunc.TakeOne,
            new Sig(S.AboutTakeOne,
                Args.Create(
                    srcSeq,
                    A.Create(S.ArgPredicate, S.AboutArgPredicate),
                    A.Create(S.ArgElse, S.AboutTakeOne_Else)),
                Opt.Create(1, 3)));
        AddOne(TakeOneFunc.First,
            new Sig(S.AboutFirst,
                Args.Create(
                    srcSeq,
                    A.Create(S.ArgPredicate, S.AboutArgPredicate),
                    A.Create(S.ArgElse, S.AboutTakeOne_Else)),
                Opt.Create(1, 3)));
        AddOne(DropOneFunc.Instance,
            new Sig(S.AboutDropOne,
                Args.Create(
                    srcSeq,
                    A.Create(S.ArgPredicate, S.AboutArgPredicate)),
                opt12));

        {
            var pred = A.Create(S.ArgPredicate, S.AboutArgPredicate);

            var argsIf = Args.Create(srcSeq, A.Create(S.ArgIfPredicate, S.AboutArgIfPredicate));
            var argsWh = Args.Create(srcSeq, A.Create(S.ArgWhilePredicate, S.AboutArgWhilePredicate));
            var predWh = A.Create(S.ArgWhilePredicate, S.AboutArgWhilePredicate);
            var countTake = A.Create(S.ArgCount, S.AboutTake_Count);
            var countDrop = A.Create(S.ArgCount, S.AboutDrop_Count);
            AddOne(TakeDropFunc.Take,
                Sigs.Create(
                    new Sig(S.AboutTakeIf, argsIf),
                    new Sig(S.AboutTakeWhile, argsWh),
                    new Sig(S.AboutTake, Args.Create(srcSeq, countTake)),
                    new Sig(S.AboutTake, Args.Create(srcSeq, countTake, pred)),
                    new Sig(S.AboutTakeCountWhile, Args.Create(srcSeq, countTake, predWh))));
            AddOne(TakeDropFunc.Drop,
                Sigs.Create(
                    new Sig(S.AboutDropIf, argsIf),
                    new Sig(S.AboutDropWhile, argsWh),
                    new Sig(S.AboutDrop, Args.Create(srcSeq, countDrop)),
                    new Sig(S.AboutDrop, Args.Create(srcSeq, countDrop, pred)),
                    new Sig(S.AboutDropCountWhile, Args.Create(srcSeq, countDrop, predWh))));

            var argsPred = Args.Create(srcSeq, pred);
            AddOne(TakeDropCondFunc.TakeIf, new Sig(S.AboutTakeIf, argsPred));
            AddOne(TakeDropCondFunc.TakeIf, "Filter");
            AddOne(TakeDropCondFunc.DropIf, new Sig(S.AboutDropIf, argsPred));
            AddOne(TakeDropCondFunc.TakeWhile, new Sig(S.AboutTakeWhile, argsPred));
            AddOne(TakeDropCondFunc.DropWhile, new Sig(S.AboutDropWhile, argsPred));
        }

        AddOne(RepeatFunc.Instance,
            new Sig(S.AboutRepeat,
                Args.Create(
                    A.Create(S.ArgValue, S.AboutRepeat_Value),
                    A.Create(S.ArgCount, S.AboutRepeat_Count))));
        AddOne(CountFunc.Instance,
            new Sig(S.AboutCount,
                Args.Create(
                    A.Create(S.ArgSource, S.AboutCount_Source),
                    A.Create(S.ArgPredicate, S.AboutCount_Predicate)),
                opt12));
        AddOne(AnyAllFunc.Any,
            Sigs.Create(
                new Sig(S.AboutAny1, A.Create(S.ArgSource, S.AboutAny1_Source)),
                new Sig(S.AboutAny2, A.Create(S.ArgSource, S.AboutAny2_Source), A.Create(S.ArgPredicate, S.AboutAny_Predicate))));
        AddOne(AnyAllFunc.All,
            Sigs.Create(
                new Sig(S.AboutAll1, A.Create(S.ArgSource, S.AboutAll1_Source)),
                new Sig(S.AboutAll2, A.Create(S.ArgSource, S.AboutAll2_Source), A.Create(S.ArgPredicate, S.AboutAll_Predicate))));

        AddOne(MakePairsFunc.Instance, hidden: true);

        {
            var seed = A.Create(S.ArgSeed, RexlStrings.AboutArgSeed);
            AddOne(FoldFunc.Instance,
                new Sig(S.AboutFold,
                    Args.Create(
                        A.Create(S.ArgSource, S.AboutFold_Source),
                        seed,
                        A.Create(S.ArgIterSelector, S.AboutFold_IterSelector),
                        A.Create(S.ArgResSelector, S.AboutFold_ResSelector)),
                    opt34));

            var src = A.Create(S.ArgSource, RexlStrings.AboutScan_Source);
            var sel = A.Create(S.ArgIterSelector, RexlStrings.AboutScan_IterSelector);
            AddOne(ScanFunc.ScanX, new Sig(S.AboutScan, Args.Create(src, seed, sel, A.Create(S.ArgResSelector, S.AboutScanX_ResSelector)), opt34));
            AddOne(ScanFunc.ScanZ, new Sig(S.AboutScan, Args.Create(src, seed, sel, A.Create(S.ArgResSelector, S.AboutScanZ_ResSelector)), opt34));

            var count = A.Create(S.ArgCount, S.AboutGenerate_Count);
            AddOne(GenerateFunc.Instance,
                Sigs.Create(
                    new Sig(S.AboutGenerateNoState,
                        Args.Create(
                            count,
                            A.Create(S.ArgSelector, S.AboutGenerateNoState_Selector))),
                    new Sig(S.AboutGenerate,
                        Args.Create(
                            count,
                            seed,
                            A.Create(S.ArgIterSelector, S.AboutGenerate_IterSelector),
                            A.Create(S.ArgResSelector, S.AboutGenerate_ResSelector)),
                        opt34)));

            // REVIEW: Remove this.
            AddOneDep(GenerateFunc.Instance, "Gen");
        }

        {
            var argsWith = Args.Create(
                A.Create(S.ArgDefinition, S.AboutWith_Definition),
                A.Create(S.ArgSelector, S.AboutWith_Selector));
            var argsGuard = Args.Create(
                A.Create(S.ArgDefinition, S.AboutGuard_Definition),
                A.Create(S.ArgSelector, S.AboutGuard_Selector));
            var sigWith = new Sig(S.AboutWith, argsWith, rep10);
            var sigGuard = new Sig(S.AboutGuard, argsGuard, rep10);
            AddOne(WithFunc.With, sigWith);
            AddOne(WithFunc.Guard, sigGuard);
            AddOne(WithFunc.WithMap, sigWith);
            AddOne(WithFunc.GuardMap, sigGuard);
        }

        AddOne(DefaultValueFunc.Def);
        AddOne(DefaultValueFunc.DefReq);
        AddOne(DefaultValueFunc.DefOpt);
        AddOne(DefaultValueFunc.DefOpt, "Null");
        AddOne(DefaultValueFunc.DefItem);
        AddOne(DefaultValueFunc.DefItemReq);
        AddOne(DefaultValueFunc.DefItemOpt);
        AddOne(DefaultValueFunc.DefItemOpt, "NullItem");
        AddOne(OptFunc.Instance);

        AddOne(LikeFunc.LikeOrNul);
        AddOne(LikeFunc.LikeOrNul, "Like");
        AddOne(LikeFunc.LikeOrDef);
        AddOne(LikeFunc.LikeOrVal);
        AddOne(LikeFunc.LikeOrVal, "LikeOr");

        AddOne(IsNullFunc.Instance, new Sig(S.AboutIsNull, A.Create(S.ArgSource, S.AboutIsNull_Source)));
        AddOne(IsEmptyFunc.Instance, new Sig(S.AboutIsEmpty, A.Create(S.ArgSource, S.AboutIsEmpty_Source)));

        AddOne(IfFunc.Instance,
            new Sig(S.AboutIf,
                Args.Create(
                    A.Create(S.IfFuncCondition, S.AboutIf_Predicate),
                    A.Create(S.IfFuncThenValue, S.AboutIf_TrueValue),
                    A.Create(S.IfFuncElseValue, S.AboutIf_FalseValue)),
                Grps.Create(Rep.Create(1, 0, 2), opt23)));

        AddOne(AbsFunc.Instance, new Sig(S.AboutAbs, A.Create(S.ArgSource, S.AboutAbs_Source)));

        AddOne(DateFunc.Instance,
            new Sig(S.AboutDateArity8Func,
                Args.Create(
                    A.Create(S.DateFuncArg1, S.AboutDateFuncArg1),
                    A.Create(S.DateFuncArg2, S.AboutDateFuncArg2),
                    A.Create(S.DateFuncArg3, S.AboutDateFuncArg3),
                    A.Create(S.DateFuncArg4, S.AboutDateFuncArg4),
                    A.Create(S.DateFuncArg5, S.AboutDateFuncArg5),
                    A.Create(S.DateFuncArg6, S.AboutDateFuncArg6),
                    A.Create(S.DateFuncArg7, S.AboutDateFuncArg7),
                    A.Create(S.DateFuncArg8, S.AboutDateFuncArg8)),
                Opt.Create(3, 8)));
        AddOne(TimeFunc.Instance,
            new Sig(S.AboutTimeArity6Func,
                Args.Create(
                    A.Create(S.TimeFuncArg1, S.AboutTimeFuncArg1),
                    A.Create(S.TimeFuncArg2, S.AboutTimeFuncArg2),
                    A.Create(S.TimeFuncArg3, S.AboutTimeFuncArg3),
                    A.Create(S.TimeFuncArg4, S.AboutTimeFuncArg4),
                    A.Create(S.TimeFuncArg5, S.AboutTimeFuncArg5),
                    A.Create(S.TimeFuncArg6, S.AboutTimeFuncArg6)),
                Opt.Create(1, 6)));

        AddOne(ChronoPartFunc.DateYear, new Sig(S.AboutDateYearFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateMonth, new Sig(S.AboutDateMonthFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateDay, new Sig(S.AboutDateDayFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateHour, new Sig(S.AboutDateHourFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateMinute, new Sig(S.AboutDateMinuteFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateSecond, new Sig(S.AboutDateSecondFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateMillisecond, new Sig(S.AboutDateMillisecondFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateTick, new Sig(S.AboutDateTickFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateTotalTicks, new Sig(S.AboutDateTotalTicksFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateHour, "Hr");
        AddOne(ChronoPartFunc.DateMinute, "Min");
        AddOne(ChronoPartFunc.DateSecond, "Sec");
        AddOne(ChronoPartFunc.DateMillisecond, "Ms");

        AddOne(ChronoPartFunc.DateDayOfYear, new Sig(S.AboutDateDayOfYearFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateDayOfWeek, new Sig(S.AboutDateDayOfWeekFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateStartOfYear, new Sig(S.AboutDateStartOfYearFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateStartOfMonth, new Sig(S.AboutDateStartOfMonthFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateStartOfWeek, new Sig(S.AboutDateStartOfWeekFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateDate, new Sig(S.AboutDateDateFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateTime, new Sig(S.AboutDateTimeFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.DateDate, "StartOfDay");
        AddOne(ChronoPartFunc.DateTime, "TimeOfDay");

        AddOne(ChronoPartFunc.TimeDay, new Sig(S.AboutTimeDayFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeHour, new Sig(S.AboutTimeHourFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeMinute, new Sig(S.AboutTimeMinuteFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeSecond, new Sig(S.AboutTimeSecondFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeMillisecond, new Sig(S.AboutTimeMillisecondFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTick, new Sig(S.AboutTimeTickFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeHour, "Hr");
        AddOne(ChronoPartFunc.TimeMinute, "Min");
        AddOne(ChronoPartFunc.TimeSecond, "Sec");
        AddOne(ChronoPartFunc.TimeMillisecond, "Ms");

        AddOne(ChronoPartFunc.TimeTotalDays, new Sig(S.AboutTimeTotalDaysFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTotalHours, new Sig(S.AboutTimeTotalHoursFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTotalMinutes, new Sig(S.AboutTimeTotalMinutesFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTotalSeconds, new Sig(S.AboutTimeTotalSecondsFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTotalMilliseconds, new Sig(S.AboutTimeTotalMillisecondsFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTotalTicks, new Sig(S.AboutTimeTotalTicksFunc, A.Create(S.ArgValue, S.AboutChronoPart_Value)));
        AddOne(ChronoPartFunc.TimeTotalDays, "TotDays");
        AddOne(ChronoPartFunc.TimeTotalHours, "TotHrs");
        AddOne(ChronoPartFunc.TimeTotalMinutes, "TotMins");
        AddOne(ChronoPartFunc.TimeTotalSeconds, "TotSecs");
        AddOne(ChronoPartFunc.TimeTotalMilliseconds, "TotMs");
        AddOne(ChronoPartFunc.TimeTotalTicks, "TotTicks");

        AddOne(CastChronoFunc.CastDate, new Sig(S.AboutCastDate, A.Create(S.ArgValue, S.AboutCastDate_Value), opt01));
        AddOne(CastChronoFunc.CastTime, new Sig(S.AboutCastTime, A.Create(S.ArgValue, S.AboutCastTime_Value), opt01));
        AddOne(CastChronoFunc.ToDate, new Sig(S.AboutToDate, A.Create(S.ArgValue, S.AboutToDate_Value)));
        AddOne(CastChronoFunc.ToTime, new Sig(S.AboutToTime, A.Create(S.ArgValue, S.AboutToTime_Value)));

        AddOne(DateNowFunc.Rec, new Sig(S.AboutDateNow, Args.Empty, isVol: true));
        AddOne(DateNowFunc.Utc, new Sig(S.AboutDateNowUtc, Args.Empty, isVol: true));
        AddOne(DateNowFunc.Loc, new Sig(S.AboutDateNowLoc, Args.Empty, isVol: true));

        AddOne(CastGuidFunc.Instance, new Sig(S.AboutCastGuid, A.Create(S.ArgValue, S.AboutCastGuid_Value), opt01));
        AddOne(ToGuidFunc.Instance, new Sig(S.AboutToGuid, A.Create(S.ArgValue, S.AboutToGuid_Value)));
        AddOne(MakeGuidFunc.Instance, new Sig(S.AboutMakeGuid));

        AddOne(TextLenFunc.Instance, new Sig(S.AboutStrLen, A.Create(S.ArgSource, S.AboutStrLen_Source)));
        AddOne(TextCaseFunc.Lower, new Sig(S.AboutLower, A.Create(S.ArgSource, S.AboutLower_Source)));
        AddOne(TextCaseFunc.Upper, new Sig(S.AboutUpper, A.Create(S.ArgSource, S.AboutUpper_Source)));
        AddOne(TextConcatFunc.Instance,
            new Sig(S.AboutStrJoin,
                A.Create(S.ArgSource, S.AboutStrJoin_Source),
                A.Create(S.StrJoinFuncArg2, S.AboutStrJoinFuncArg2)));
        {
            var src = A.Create(S.ArgSource, S.AboutStrIndexOf_Source);
            var lkp = A.Create(S.StrIndexOf_LookUp, S.AboutStrIndexOf_LookUp);
            var min = A.Create(S.ArgMinIndex, S.AboutStrIndexOf_MinIndex);
            var lim = A.Create(S.ArgLimIndex, S.AboutStrLastIndexOf_LimIndex);
            var all = Args.Create(src, lkp, min, lim);
            AddOne(TextIndexOfFunc.IndexOf, new Sig(S.AboutStrIndexOf, all, Opt.Create(2, 4)));
            AddOne(TextIndexOfFunc.LastIndexOf,
                Sigs.Create(
                    new Sig(S.AboutStrLastIndexOf, Args.Create(src, lkp, lim), opt23),
                    new Sig(S.AboutStrLastIndexOf_WithRange, all)));
        }
        {
            var args = Args.Create(
                A.Create(S.ArgSource, S.AboutStartsWith_Source),
                A.Create(S.ArgLookUp, S.AboutStartsWith_LookUp));
            AddOne(TextStartsWithFunc.StartsWith, new Sig(S.AboutStartsWith, args));
            AddOne(TextStartsWithFunc.EndsWith, new Sig(S.AboutEndsWith, args));
        }
        AddOne(TextPartFunc.Instance,
            new Sig(S.AboutStrPart,
                Args.Create(
                    A.Create(S.ArgSource, S.AboutStrPart_Source),
                    A.Create(S.ArgMinIndex, S.AboutStrPart_MinIndex),
                    A.Create(S.ArgLimIndex, S.AboutStrPart_LimIndex)),
                opt23));
        AddOne(TextTrimFunc.Trim, new Sig(S.AboutTrim, A.Create(S.ArgSource, S.AboutTrim_Source)));
        AddOne(TextTrimFunc.TrimStart, new Sig(S.AboutTrimStart, A.Create(S.ArgSource, S.AboutTrimStart_Source)));
        AddOne(TextTrimFunc.TrimEnd, new Sig(S.AboutTrimEnd, A.Create(S.ArgSource, S.AboutTrimEnd_Source)));

        AddOne(TextReplaceFunc.Instance, new Sig(S.AboutTextReplace,
            A.Create(S.ArgSource, S.AboutTextReplace_Source),
            A.Create(S.TextReplace_Remove, S.AboutTextReplace_Remove),
            A.Create(S.TextReplace_Insert, S.AboutTextReplace_Insert)));

        AddOne(SetFieldsFunc.AddFields,
            new Sig(S.AboutAddFields,
                Args.Create(
                    A.Create(S.ArgSource, S.AboutAddFields_Source),
                    A.Create(S.ArgDefinition, S.AboutAddFields_Definition)),
                rep11));
        AddOne(SetFieldsFunc.SetFields,
            new Sig(S.AboutSetFields,
                Args.Create(
                    A.Create(S.ArgSource, S.AboutSetFields_Source),
                    A.Create(S.ArgDefinition, S.AboutSetFields_Definition)),
                rep11));

        AddOne(GroupByFunc.Instance,
            new Sig(S.AboutGroupBy,
                Args.Create(
                    A.Create(S.ArgSource, S.AboutGroupBy_Source),
                    A.Create(S.ArgKeySelector, S.AboutGroupBy_Key),
                    A.Create(S.GroupBy_Group, S.AboutGroupBy_Group),
                    A.Create(S.GroupBy_Item, S.AboutGroupBy_Item),
                    A.Create(S.GroupBy_Auto, S.AboutGroupBy_Auto)),
                Grps.Create(rep11,
                    Rep.Create(0, 2),
                    Rep.Create(0, 3),
                    Opt.Create(4, 5))));

        {
            // REVIEW: Should the C variants have a different description? Same for other
            // aggregations (Min, Max, Mean, etc).
            var argsDir = Args.Create(A.Create(S.ArgSource, S.AboutSumDir_Source));
            var argsSel = Args.Create(A.Create(S.ArgSource, S.AboutSumSel_Source), A.Create(S.ArgSelector, S.AboutSumSel_Selector));

            {
                var sigs = Sigs.Create(new Sig(S.AboutSumDir, argsDir), new Sig(S.AboutSumSel, argsSel, rep10));
                AddOne(SumFunc.Sum, sigs);
                AddOne(SumFunc.SumC, sigs);
            }
            {
                var sigs = Sigs.Create(new Sig(S.AboutSumBigDir, argsDir), new Sig(S.AboutSumBigSel, argsSel, rep10));
                AddOne(SumFunc.SumBig, sigs);
                AddOne(SumFunc.SumBigC, sigs);
            }
            {
                var sigs = Sigs.Create(new Sig(S.AboutSumKahDir, argsDir), new Sig(S.AboutSumKahSel, argsSel, rep10));
                AddOne(SumFunc.SumK, sigs);
                AddOne(SumFunc.SumKC, sigs);
            }

            argsDir = Args.Create(A.Create(S.ArgSource, S.AboutMeanDir_Source));
            argsSel = Args.Create(A.Create(S.ArgSource, S.AboutMeanSel_Source), A.Create(S.ArgSelector, S.AboutMeanSel_Selector));
            {
                var sigs = Sigs.Create(new Sig(S.AboutMeanDir, argsDir), new Sig(S.AboutMeanSel, argsSel, rep10));
                AddOne(MeanFunc.Mean, sigs);
                AddOne(MeanFunc.MeanC, sigs);
            }

            argsDir = Args.Create(A.Create(S.ArgSource, S.AboutMinDir_Source));
            argsSel = Args.Create(srcSeq, A.Create(S.ArgSelector, S.AboutMinSel_Selector));
            {
                var sigs = Sigs.Create(new Sig(S.AboutMinDir, argsDir), new Sig(S.AboutMinSel, argsSel, rep10));
                AddOne(MinMaxFunc.Min, sigs);
                AddOne(MinMaxFunc.MinC, sigs);
            }
            argsDir = Args.Create(A.Create(S.ArgSource, S.AboutMaxDir_Source));
            argsSel = Args.Create(srcSeq, A.Create(S.ArgSelector, S.AboutMaxSel_Selector));
            {
                var sigs = Sigs.Create(new Sig(S.AboutMaxDir, argsDir), new Sig(S.AboutMaxSel, argsSel, rep10));
                AddOne(MinMaxFunc.Max, sigs);
                AddOne(MinMaxFunc.MaxC, sigs);
            }
            argsDir = Args.Create(A.Create(S.ArgSource, S.AboutMinMaxDir_Source));
            argsSel = Args.Create(srcSeq, A.Create(S.ArgSelector, S.AboutMinMaxSel_Selector));
            {
                var sigs = Sigs.Create(new Sig(S.AboutMinMaxDir, argsDir), new Sig(S.AboutMinMaxSel, argsSel, rep10));
                AddOne(MinMaxFunc.MinMax, sigs);
                AddOne(MinMaxFunc.MinMaxC, sigs);
            }
        }

        AddOne(RangeFunc.Instance,
            new Sig(S.AboutRangeFunc,
                Args.Create(
                    A.Create(S.RangeFuncArg1, S.AboutRangeFuncArg1),
                    A.Create(S.RangeFuncArg2, S.AboutRangeFuncArg2),
                    A.Create(S.RangeFuncArg3, S.AboutRangeFuncArg3)),
                Grps.Create(opt01, opt23)));
        AddOne(SequenceFunc.Instance);

        AddOne(DivFunc.Instance, new Sig(S.AboutDiv, A.Create(S.ArgX, S.AboutDiv_X), A.Create(S.ArgY, S.AboutDiv_Y)));
        AddOne(ModFunc.Instance, new Sig(S.AboutMod, A.Create(S.ArgX, S.AboutMod_X), A.Create(S.ArgY, S.AboutMod_Y)));
        AddOne(BinFunc.Instance, new Sig(S.AboutBin, A.Create(S.ArgX, S.AboutBin_X), A.Create(S.ArgY, S.AboutBin_Y)));

        {
            var seed = A.Create(S.ArgSeed, S.AboutRandomUniform_Seed);
            var count = A.Create(S.ArgCount, S.AboutRandomUniform_Count);
            var x = A.Create(S.ArgX, S.AboutRandomUniform_X);
            var y = A.Create(S.ArgY, S.AboutRandomUniform_Y);

            AddOne(UniformFunc.Pure,
                Sigs.Create(
                    new Sig(S.AboutRandomUniformUnit, Args.Create(seed, count), opt12),
                    new Sig(S.AboutRandomUniformRange, Args.Create(seed, x, y, count), opt34)));
            AddOne(UniformFunc.Volatile,
                Sigs.Create(
                    new Sig(S.AboutRandomUniformUnit, Args.Create(count), opt01, isVol: true),
                    new Sig(S.AboutRandomUniformRange, Args.Create(x, y, count), opt23, isVol: true)));
        }

        {
            var args = Args.Create(A.Create(S.ArgValue, S.AboutCastXX_Value));
            AddOne(CastFunc.CastI1, new Sig(S.AboutCastI1, args));
            AddOne(CastFunc.CastI2, new Sig(S.AboutCastI2, args));
            AddOne(CastFunc.CastI4, new Sig(S.AboutCastI4, args));
            AddOne(CastFunc.CastI8, new Sig(S.AboutCastI8, args));
            AddOne(CastFunc.CastU1, new Sig(S.AboutCastU1, args));
            AddOne(CastFunc.CastU2, new Sig(S.AboutCastU2, args));
            AddOne(CastFunc.CastU4, new Sig(S.AboutCastU4, args));
            AddOne(CastFunc.CastU8, new Sig(S.AboutCastU8, args));
            AddOne(CastFunc.CastIA, new Sig(S.AboutCastIA, args));
            AddOne(CastFunc.CastR4, new Sig(S.AboutCastR4, args));
            AddOne(CastFunc.CastR8, new Sig(S.AboutCastR8, args));
            AddOne(CastFunc.CastI4, "CastShort");
            AddOne(CastFunc.CastI8, "CastInt");
            AddOne(CastFunc.CastR8, "CastReal");
        }

        {
            var args = Args.Create(
                A.Create(RexlStrings.ArgValue, RexlStrings.AboutToXX_Value),
                A.Create(RexlStrings.ToXXArg2, RexlStrings.AboutToXXArg2));
            AddOne(ToFunc.To, new Sig(S.AboutTo, args));

            AddOne(ToXXFunc.ToI1, new Sig(S.AboutToI1Arity2, args, opt12));
            AddOne(ToXXFunc.ToI2, new Sig(S.AboutToI2Arity2, args, opt12));
            AddOne(ToXXFunc.ToI4, new Sig(S.AboutToI4Arity2, args, opt12));
            AddOne(ToXXFunc.ToI8, new Sig(S.AboutToI8Arity2, args, opt12));
            AddOne(ToXXFunc.ToU1, new Sig(S.AboutToU1Arity2, args, opt12));
            AddOne(ToXXFunc.ToU2, new Sig(S.AboutToU2Arity2, args, opt12));
            AddOne(ToXXFunc.ToU4, new Sig(S.AboutToU4Arity2, args, opt12));
            AddOne(ToXXFunc.ToU8, new Sig(S.AboutToU8Arity2, args, opt12));
            AddOne(ToXXFunc.ToIA, new Sig(S.AboutToIAArity2, args, opt12));
            AddOne(ToXXFunc.ToR4, new Sig(S.AboutToR4Arity2, args, opt12));
            AddOne(ToXXFunc.ToR8, new Sig(S.AboutToR8Arity2, args, opt12));
            AddOne(ToXXFunc.ToI4, "ToShort");
            AddOne(ToXXFunc.ToI8, "ToInt");
            AddOne(ToXXFunc.ToR8, "ToReal");

            args = Args.Create(
                A.Create(RexlStrings.ArgValue, RexlStrings.AboutToText_Value),
                A.Create(RexlStrings.ToTextArg2, RexlStrings.AboutToTextArg2));
            AddOne(ToTextFunc.Instance, new Sig(S.AboutToText, args, opt12));
        }

        {
            var sigs = Sigs.Create(
                new Sig(S.AboutTupleItem, A.Create(S.ArgSource, S.AboutTupleItem_Source)));
            AddOne(TupleItemFunc.Item0, sigs);
            AddOne(TupleItemFunc.Item1, sigs);
            AddOne(TupleItemFunc.Item2, sigs);
            AddOne(TupleItemFunc.Item3, sigs);
            AddOne(TupleItemFunc.Item4, sigs);
            AddOne(TupleItemFunc.Item5, sigs);
            AddOne(TupleItemFunc.Item6, sigs);
            AddOne(TupleItemFunc.Item7, sigs);
            AddOne(TupleItemFunc.Item8, sigs);
            AddOne(TupleItemFunc.Item9, sigs);
        }
        AddOne(TupleLenFunc.Instance, new Sig(S.AboutTupleLen, A.Create(S.ArgSource, S.AboutTupleLen_Source)));

        // Rounding.
        {
            var args = Args.Create(A.Create(S.ArgValue, S.AboutRoundArg));
            AddOne(R8Func.Round, new Sig(S.AboutRound, args));
            AddOne(R8Func.RoundUp, new Sig(S.AboutRoundUp, args));
            AddOne(R8Func.RoundDown, new Sig(S.AboutRoundDown, args));
            AddOne(R8Func.RoundIn, new Sig(S.AboutRoundIn, args));
            AddOne(R8Func.RoundOut, new Sig(S.AboutRoundOut, args));
        }

        // Exps and logs.
        {
            var args = Args.Create(A.Create(S.ArgValue, S.AboutMathFuncArgValue));
            AddOne(R8Func.Sqrt, new Sig(S.AboutSqrt, args));
            AddOne(R8Func.Exp, new Sig(S.AboutExp, A.Create(S.ArgValue, S.AboutExpArg)));
            AddOne(R8Func.Ln, new Sig(S.AboutLn, args));
            AddOne(R8Func.Log10, new Sig(S.AboutLog10, args));
            AddOne(R8Func.Sinh, new Sig(S.AboutSinh, args));
            AddOne(R8Func.Cosh, new Sig(S.AboutCosh, args));
            AddOne(R8Func.Tanh, new Sig(S.AboutTanh, args));
            AddOne(R8Func.Csch, new Sig(S.AboutCsch, args));
            AddOne(R8Func.Sech, new Sig(S.AboutSech, args));
            AddOne(R8Func.Coth, new Sig(S.AboutCoth, args));
        }

        // Trigonometric.
        {
            var argsDeg = Args.Create(A.Create(S.ArgAngle, S.AboutAngleInDegrees));
            var argsRad = Args.Create(A.Create(S.ArgAngle, S.AboutAngleInRadians));
            AddOne(R8Func.Radians, new Sig(S.AboutRadians, argsDeg));
            AddOne(R8Func.Degrees, new Sig(S.AboutDegrees, argsRad));
            AddOne(R8Func.Sin, new Sig(S.AboutSin, argsRad));
            AddOne(R8Func.Cos, new Sig(S.AboutCos, argsRad));
            AddOne(R8Func.Tan, new Sig(S.AboutTan, argsRad));
            AddOne(R8Func.Csc, new Sig(S.AboutCsc, argsRad));
            AddOne(R8Func.Sec, new Sig(S.AboutSec, argsRad));
            AddOne(R8Func.Cot, new Sig(S.AboutCot, argsRad));
            AddOne(R8Func.SinD, new Sig(S.AboutSinD, argsDeg));
            AddOne(R8Func.CosD, new Sig(S.AboutCosD, argsDeg));
            AddOne(R8Func.TanD, new Sig(S.AboutTanD, argsDeg));
            AddOne(R8Func.CscD, new Sig(S.AboutCscD, argsDeg));
            AddOne(R8Func.SecD, new Sig(S.AboutSecD, argsDeg));
            AddOne(R8Func.CotD, new Sig(S.AboutCotD, argsDeg));
            AddOne(R8Func.Asin, new Sig(S.AboutAsin, A.Create(S.ArgValue, S.AboutAsinArg)));
            AddOne(R8Func.Acos, new Sig(S.AboutAcos, A.Create(S.ArgValue, S.AboutAcosArg)));
            AddOne(R8Func.Atan, new Sig(S.AboutAtan, A.Create(S.ArgValue, S.AboutAtanArg)));
        }

        // Integer functions.
        AddOne(IntHexFunc.Instance);

        // Float functions.
        AddOne(FloatIsNanFunc.IsNaN);
        AddOne(FloatIsNanFunc.IsNotNaN);
        AddOne(FloatBitsFunc.Instance);
        AddOne(FloatFromBitsFunc.Instance);

        // Stats.
        AddOne(TTestOneSampleFunc.Instance,
            new Sig(S.AboutTTestOneSample,
                Args.Create(
                    A.Create(S.ArgX, S.AboutTTestOneSample_X),
                    A.Create(S.TTestOneSample_Mean, S.AboutTTestOneSample_Mean)),
                opt12));
        {
            var x = A.Create(S.ArgX, S.AboutTTest_X);
            var y = A.Create(S.ArgY, S.AboutTTest_Y);
            AddOne(TTestTwoSampleFunc.Instance,
                new Sig(S.AboutTTestTwoSample,
                    Args.Create(x, y, A.Create(S.TTestTwoSample_EqualVar, S.AboutTTestTwoSample_EqualVar)),
                    opt23));
            AddOne(TTestPairedFunc.Instance,
                Sigs.Create(
                    new Sig(S.AboutTTestPairedNoSel, Args.Create(x, y)),
                    new Sig(S.AboutTTestPairedSel,
                        Args.Create(
                            A.Create(S.ArgSource, S.AboutTTestPairedSel_Source),
                            A.Create(S.ArgX, S.AboutTTestPairedSel_X),
                            A.Create(S.ArgY, S.AboutTTestPairedSel_Y)))));
        }

        // Tensor.
        AddOne(TensorFillFunc.Instance);
        AddOne(TensorFromFunc.Instance);
        AddOne(TensorBuildFunc.Instance);
        AddOne(TensorRankFunc.Instance);
        AddOne(TensorShapeFunc.Instance);
        AddOne(TensorValuesFunc.Instance);
        AddOne(TensorReshapeFunc.Instance);
        AddOne(TensorExpandDimsFunc.Instance);
        AddOne(TensorTransposeFunc.Instance);
        AddOne(TensorDotFunc.Instance);
        AddOne(TensorPointWiseFunc.Add);
        AddOne(TensorPointWiseFunc.Sub);
        AddOne(TensorPointWiseFunc.Mul);
        AddOne(TensorPointWiseFunc.Min);
        AddOne(TensorPointWiseFunc.Max);
        AddOne(TensorPointWiseFunc.Div);
        AddOne(TensorPointWiseFunc.Divide);
        AddOne(TensorForEachFunc.Eager);
        AddOne(TensorForEachFunc.Eager, "Map");
        AddOne(TensorForEachFunc.Eager, "Zip");
        AddOne(TensorSoftMaxFunc.Instance);

        // Image.
        AddOne(PixelsToPngFunc.Instance);
        AddOne(GetPixelsFunc.Default);
        AddOne(GetPixelsFunc.U4);
        AddOne(ReadPixelsFunc.Default);
        AddOne(ReadPixelsFunc.U4);
        AddOne(ResizePixelsFunc.Instance);
        AddOne(ToBase64Func.Instance);

        // Diagnostic.
        AddOne(GetTypeFunc.Instance, hidden: true);
        AddOne(GetBindInfoFunc.Instance, hidden: true);

        // Link construction functions.
        {
            var flavor = A.Create(S.LinkTo_Flavor, S.AboutLinkTo_Flavor);
            var acct = A.Create(S.LinkTo_AccountId, S.AboutLinkTo_AccountId);
            var url = A.Create(S.LinkTo_Url, S.AboutLinkTo_Url);
            var path = A.Create(S.LinkTo_Path, S.AboutLinkTo_Path);

            foreach (var fn in LinkFunc.Funcs)
            {
                int arity = fn.ArityMin;
                Validation.Assert(fn.ArityMax == arity);

                Sig sig;
                if (fn.NeedsFlavor)
                {
                    Validation.Assert(arity == 2 || arity == 3);
                    sig = arity == 2 ?
                        new Sig(S.AboutLinkTo, flavor, fn.Kind == LinkKind.Http ? url : path) :
                        new Sig(S.AboutLinkTo, flavor, acct, path);
                }
                else
                {
                    Validation.Assert(arity == 1 || arity == 2);
                    sig = arity == 1 ?
                        new Sig(S.AboutLinkTo, fn.Kind == LinkKind.Http ? url : path) :
                        new Sig(S.AboutLinkTo, acct, path);
                }
                AddOne(fn, sig);
                if (!fn.LegacyName.IsRoot)
                {
                    // REVIEW: Should the legacy names be deprecated?
                    AddOne(fn, fn.LegacyName, hidden: fn.IsHidden, deprecated: false, pathAlt: fn.Path);
                }
            }
        }

        // Link properties.
        {
            var arg = A.Create(S.LinkProp_Link, S.AboutLinkProp_Link);
            AddOne(LinkProp.FuncKind, new Sig(S.AboutLinkKind, arg));
            AddOne(LinkProp.FuncAccount, new Sig(S.AboutLinkAccount, arg));
            AddOne(LinkProp.FuncPath, new Sig(S.AboutLinkPath, arg));
        }

        // Read functions.
        AddOne(ReadBytesFunc.ReadAll);
    }
}
