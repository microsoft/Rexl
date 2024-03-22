// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

public sealed class DateGen : MethArityGen<DateFunc>
{
    public static readonly DateGen Instance = new DateGen();

    // These are the limits of the year field of a Date value.
    private const int _yearMin = 1;
    private const int _yearMax = 9999;

    protected override ReadOnly.Array<MethodInfo> Meths { get; }

    protected override int ArityMin => 3;

    private DateGen()
    {
        Meths = new[]
        {
            new Func<long, long, long, Date>(Exec).Method,
            new Func<long, long, long, long, Date>(Exec).Method,
            new Func<long, long, long, long, long, Date>(Exec).Method,
            new Func<long, long, long, long, long, long, Date>(Exec).Method,
            new Func<long, long, long, long, long, long, long, Date>(Exec).Method,
            new Func<long, long, long, long, long, long, long, long, Date>(Exec).Method,
        };
    }

    public static Date Exec(long year, long month, long day)
    {
        return Date.CreateOrDefault(year, month, day, 0);
    }

    public static Date Exec(long year, long month, long day, long hour)
    {
        if ((ulong)hour >= 24)
            return default;
        long ticks = hour * ChronoUtil.TicksPerHour;
        return Date.CreateOrDefault(year, month, day, ticks);
    }

    public static Date Exec(long year, long month, long day, long hour, long min)
    {
        if ((ulong)hour >= 24 || (ulong)min >= 60)
            return default;
        long ticks = hour * ChronoUtil.TicksPerHour + min * ChronoUtil.TicksPerMinute;
        return Date.CreateOrDefault(year, month, day, ticks);
    }

    public static Date Exec(long year, long month, long day, long hour, long min, long sec)
    {
        if ((ulong)hour >= 24 || (ulong)min >= 60 || (ulong)sec >= 60)
            return default;
        long ticks = hour * ChronoUtil.TicksPerHour + min * ChronoUtil.TicksPerMinute + sec * ChronoUtil.TicksPerSecond;
        return Date.CreateOrDefault(year, month, day, ticks);
    }

    public static Date Exec(long year, long month, long day, long hour, long min, long sec, long ms)
    {
        if ((ulong)hour >= 24 || (ulong)min >= 60 || (ulong)sec >= 60 || (ulong)ms >= 1000)
            return default;
        long ticks = hour * ChronoUtil.TicksPerHour + min * ChronoUtil.TicksPerMinute + sec * ChronoUtil.TicksPerSecond + ms * ChronoUtil.TicksPerMs;
        return Date.CreateOrDefault(year, month, day, ticks);
    }

    public static Date Exec(long year, long month, long day, long hour, long min, long sec, long ms, long tick)
    {
        if ((ulong)hour >= 24 || (ulong)min >= 60 || (ulong)sec >= 60 || (ulong)ms >= 1000 || (ulong)tick >= ChronoUtil.TicksPerMs)
            return default;
        long ticks = hour * ChronoUtil.TicksPerHour + min * ChronoUtil.TicksPerMinute + sec * ChronoUtil.TicksPerSecond + ms * ChronoUtil.TicksPerMs + tick;
        return Date.CreateOrDefault(year, month, day, ticks);
    }
}

public sealed class TimeGen : MethArityGen<TimeFunc>
{
    public static readonly TimeGen Instance = new TimeGen();

    private const long _ticksPerMs = TimeSpan.TicksPerMillisecond;
    private const long _ticksPerSc = _ticksPerMs * 1_000;
    private const long _ticksPerMn = _ticksPerSc * 60;
    private const long _ticksPerHr = _ticksPerMn * 60;
    private const long _ticksPerDy = _ticksPerHr * 24;

    protected override ReadOnly.Array<MethodInfo> Meths { get; }

    protected override int ArityMin => 1;

    private TimeGen()
    {
        Meths = new[]
        {
            new Func<long, Time>(Exec).Method,
            new Func<long, long, Time>(Exec).Method,
            new Func<long, long, long, Time>(Exec).Method,
            new Func<long, long, long, long, Time>(Exec).Method,
            new Func<long, long, long, long, long, Time>(Exec).Method,
            new Func<long, long, long, long, long, long, Time>(Exec).Method,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Exec(long days)
    {
        // REVIEW: Is there a better way than try-catch?
        try { return new Time(checked(days * _ticksPerDy)); }
        catch (OverflowException) { return default; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Exec(long days, long hours)
    {
        try { return new Time(checked(days * _ticksPerDy + hours * _ticksPerHr)); }
        catch (OverflowException) { return default; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Exec(long days, long hours, long mins)
    {
        try { return new Time(checked(days * _ticksPerDy + hours * _ticksPerHr + mins * _ticksPerMn)); }
        catch (OverflowException) { return default; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Exec(long days, long hours, long mins, long secs)
    {
        try { return new Time(checked(days * _ticksPerDy + hours * _ticksPerHr + mins * _ticksPerMn + secs * _ticksPerSc)); }
        catch (OverflowException) { return default; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Exec(long days, long hours, long mins, long secs, long mss)
    {
        try { return new Time(checked(days * _ticksPerDy + hours * _ticksPerHr + mins * _ticksPerMn + secs * _ticksPerSc + mss * _ticksPerMs)); }
        catch (OverflowException) { return default; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Exec(long days, long hours, long mins, long secs, long mss, long ticks)
    {
        try { return new Time(checked(days * _ticksPerDy + hours * _ticksPerHr + mins * _ticksPerMn + secs * _ticksPerSc + mss * _ticksPerMs + ticks)); }
        catch (OverflowException) { return default; }
    }
}

public sealed class ChronoPartGen : RexlOperationGenerator<ChronoPartFunc>
{
    public static readonly ChronoPartGen Instance = new ChronoPartGen();

    private readonly MethodInfo _methTicks;
    private readonly MethodInfo _methFromTicks;

    private readonly ReadOnly.Dictionary<ChronoPartFunc.PartKind, MethodInfo> _methGet;

    private ChronoPartGen()
    {
        _methTicks = typeof(Date).GetMethod("get_Ticks").VerifyValue();
        _methFromTicks = typeof(Date).GetMethod("FromTicks").VerifyValue();

        _methGet = new Dictionary<ChronoPartFunc.PartKind, MethodInfo>()
        {
            // These all produce an int.
            { ChronoPartFunc.PartKind.DateYear, typeof(Date).GetMethod("get_Year").VerifyValue() },
            { ChronoPartFunc.PartKind.DateMonth, typeof(Date).GetMethod("get_Month").VerifyValue() },
            { ChronoPartFunc.PartKind.DateDay, typeof(Date).GetMethod("get_Day").VerifyValue() },
            { ChronoPartFunc.PartKind.DateHour, typeof(Date).GetMethod("get_Hour").VerifyValue() },
            { ChronoPartFunc.PartKind.DateMinute, typeof(Date).GetMethod("get_Minute").VerifyValue() },
            { ChronoPartFunc.PartKind.DateSecond, typeof(Date).GetMethod("get_Second").VerifyValue() },
            { ChronoPartFunc.PartKind.DateMillisecond, typeof(Date).GetMethod("get_Millisecond").VerifyValue() },
            { ChronoPartFunc.PartKind.DateDayOfWeek, typeof(Date).GetMethod("get_DayOfWeek").VerifyValue() },
            { ChronoPartFunc.PartKind.DateDayOfYear, typeof(Date).GetMethod("get_DayOfYear").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeDay, typeof(Time).GetMethod("get_Days").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeHour, typeof(Time).GetMethod("get_Hours").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeMinute, typeof(Time).GetMethod("get_Minutes").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeSecond, typeof(Time).GetMethod("get_Seconds").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeMillisecond, typeof(Time).GetMethod("get_Milliseconds").VerifyValue() },

            // These produce DateTime and TimeSpan respectively.
            { ChronoPartFunc.PartKind.DateDate, typeof(Date).GetMethod("get_Date").VerifyValue() },
            { ChronoPartFunc.PartKind.DateTime, typeof(Date).GetMethod("get_TimeOfDay").VerifyValue() },

            // These produce double.
            { ChronoPartFunc.PartKind.TimeTotalDays, typeof(Time).GetMethod("get_TotalDays").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeTotalHours, typeof(Time).GetMethod("get_TotalHours").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeTotalMinutes, typeof(Time).GetMethod("get_TotalMinutes").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeTotalSeconds, typeof(Time).GetMethod("get_TotalSeconds").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeTotalMilliseconds, typeof(Time).GetMethod("get_TotalMilliseconds").VerifyValue() },

            // These produce long.
            { ChronoPartFunc.PartKind.DateTotalTicks, typeof(Date).GetMethod("get_Ticks").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeTotalTicks, typeof(Time).GetMethod("get_Ticks").VerifyValue() },

            // Special code gen: mod by ticks per ms.
            { ChronoPartFunc.PartKind.DateTick, typeof(Date).GetMethod("get_Ticks").VerifyValue() },
            { ChronoPartFunc.PartKind.TimeTick, typeof(Time).GetMethod("get_Ticks").VerifyValue() },

            // Special code gen: first apply get_Date, then subtract the indicated number of days, perhaps minus one.
            { ChronoPartFunc.PartKind.DateStartOfYear, typeof(Date).GetMethod("get_DayOfYear").VerifyValue() },
            { ChronoPartFunc.PartKind.DateStartOfMonth, typeof(Date).GetMethod("get_Day").VerifyValue() },
            { ChronoPartFunc.PartKind.DateStartOfWeek, typeof(Date).GetMethod("get_DayOfWeek").VerifyValue() },
        };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        var stSrc = fn.IsDate ? typeof(Date) : typeof(Time);
        Validation.Assert(codeGen.GetSystemType(fn.TypeSrc) == stSrc);
        Validation.Assert(stSrc.IsAssignableFrom(sts[0]));

        if (!_methGet.TryGetValue(fn.Kind, out var methGet))
        {
            Validation.Assert(false);
            stRet = default;
            return false;
        }

        var ilw = codeGen.Writer;
        using var loc = codeGen.AcquireLocal(stSrc);
        ilw.Stloc(loc);

        switch (fn.Kind)
        {
        case ChronoPartFunc.PartKind.DateStartOfYear:
        case ChronoPartFunc.PartKind.DateStartOfMonth:
        case ChronoPartFunc.PartKind.DateStartOfWeek:
            ilw
                .Ldloca(loc).Call(_methTicks)
                .Dup()
                .Ldc_I8(ChronoUtil.TicksPerDay)
                .Rem()
                .Sub();
            break;
        }

        ilw
            .Ldloca(loc)
            .Call(methGet);
        stRet = methGet.ReturnType;

        switch (fn.Kind)
        {
        default:
            Validation.Assert(codeGen.GetSystemType(fn.TypeRet) == methGet.ReturnType);
            break;

        case ChronoPartFunc.PartKind.DateStartOfYear:
        case ChronoPartFunc.PartKind.DateStartOfMonth:
            // No chance of underflow for these two. Also, these are both 1-based.
            ilw
                .Ldc_I4(1)
                .Sub()
                .Conv_I8()
                .Ldc_I8(ChronoUtil.TicksPerDay)
                .Mul()
                .Sub()
                .Call(_methFromTicks);
            Validation.Assert(codeGen.GetSystemType(fn.TypeRet) == _methFromTicks.ReturnType);
            stRet = _methFromTicks.DeclaringType;
            break;

        case ChronoPartFunc.PartKind.DateStartOfWeek:
            ilw
                .Conv_I8()
                .Ldc_I8(ChronoUtil.TicksPerDay)
                .Mul()
                .Sub();
            // Need to protect against underflow.
            ilw
                .Dup()
                .Dup()
                .Ldc_I4(63)
                .Shr()
                .And()
                .Sub()
                .Call(_methFromTicks);
            Validation.Assert(codeGen.GetSystemType(fn.TypeRet) == _methFromTicks.ReturnType);
            stRet = _methFromTicks.DeclaringType;
            break;

        case ChronoPartFunc.PartKind.DateTick:
        case ChronoPartFunc.PartKind.TimeTick:
            Validation.Assert(methGet.ReturnType == typeof(long));
            ilw
                .Ldc_I8(ChronoUtil.TicksPerMs)
                .Rem()
                .Conv_I4();
            Validation.Assert(codeGen.GetSystemType(fn.TypeRet) == typeof(int));
            stRet = typeof(int);
            break;
        }

        return true;
    }
}

public sealed class CastChronoGen : RexlOperationGenerator<CastChronoFunc>
{
    public static readonly CastChronoGen Instance = new CastChronoGen();

    private readonly MethodInfo _methStrDate;
    private readonly MethodInfo _methStrTime;
    private readonly MethodInfo _methStrDateOpt;
    private readonly MethodInfo _methStrTimeOpt;

    private readonly MethodInfo _methIntDate;
    private readonly MethodInfo _methIntTime;
    private readonly MethodInfo _methIntDateOpt;
    private readonly MethodInfo _methIntTimeOpt;

    private readonly MethodInfo _methDateFromTicks;
    private readonly ConstructorInfo _ctorTimeFromTicks;

    private CastChronoGen()
    {
        _methStrDate = new Func<string, Date>(ExecDate).Method;
        _methStrDateOpt = new Func<string, Date?>(ExecDateOpt).Method;
        _methStrTime = new Func<string, Time>(ExecTime).Method;
        _methStrTimeOpt = new Func<string, Time?>(ExecTimeOpt).Method;
        _methIntDate = new Func<Integer, Date>(ExecDate).Method;
        _methIntDateOpt = new Func<Integer, Date?>(ExecDateOpt).Method;
        _methIntTime = new Func<Integer, Time>(ExecTime).Method;
        _methIntTimeOpt = new Func<Integer, Time?>(ExecTimeOpt).Method;

        _methDateFromTicks = new Func<long, Date>(Date.FromTicks).Method;
        _ctorTimeFromTicks = typeof(Time).GetConstructor(new Type[] { typeof(long) }).VerifyValue();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(call.Args.Length == 1);

        Validation.BugCheckParam(call.Args.Length == 1, nameof(call));
        var typeSrc = call.Args[0].Type;
        Validation.BugCheckParam(typeSrc.IsNumericReq || typeSrc == DType.Text, nameof(call));

        var fn = GetOper(call);
        var stDst = codeGen.GetSystemType(fn.TypeRet);

        var kindSrc = typeSrc.RootKind;
        MethodInfo meth;
        switch (kindSrc)
        {
        case DKind.Text:
            meth = fn.IsDate ?
                fn.UseDef ? _methStrDate : _methStrDateOpt :
                fn.UseDef ? _methStrTime : _methStrTimeOpt;
            stRet = GenCall(codeGen, meth, sts);
            Validation.Assert(stRet == stDst);
            return true;
        case DKind.IA:
            meth = fn.IsDate ?
                fn.UseDef ? _methIntDate : _methIntDateOpt :
                fn.UseDef ? _methIntTime : _methIntTimeOpt;
            stRet = GenCall(codeGen, meth, sts);
            Validation.Assert(stRet == stDst);
            return true;
        }

        // Get an I8 on the stack or jump to labBad. Anything out of range jumps to labBad.
        var ilw = codeGen.Writer;
        Label labBad = default;
        Label labBadWithoutPop = default;
        if (fn.IsDate)
        {
            switch (kindSrc)
            {
            case DKind.I8:
            case DKind.U8:
                ilw.Dup().Ldc_I8(ChronoUtil.TicksMax);
                if (fn.UseDef)
                {
                    Label labOk = default;
                    ilw
                        .Ble_Un(ref labOk)
                        .Dup().Xor()
                        .MarkLabel(labOk);
                }
                else
                    ilw.Bgt_Un(ref labBad);
                break;

            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                ilw.Conv_I8().Dup().Ldc_I8(0);
                if (fn.UseDef)
                {
                    Label labOk = default;
                    ilw
                        .Bge(ref labOk)
                        .Dup().Xor()
                        .MarkLabel(labOk);
                }
                else
                    ilw.Blt(ref labBad);
                break;

            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                ilw.Conv_U8();
                break;

            case DKind.R4:
            case DKind.R8:
                ilw
                    .Call(kindSrc == DKind.R8 ? CodeGenUtil.R8ToDateTicks : CodeGenUtil.R4ToDateTicks)
                    .Dup().Ldc_I8(0);
                if (fn.UseDef)
                {
                    Label labOk = default;
                    ilw
                        .Bge(ref labOk)
                        .Dup().Xor()
                        .MarkLabel(labOk);
                }
                else
                    ilw.Blt(ref labBad);
                break;
            }
        }
        else
        {
            switch (kindSrc)
            {
            case DKind.I8:
                break;

            case DKind.U8:
                // U8 values that are too large map to default.
                ilw.Dup().Ldc_I8(0);
                if (fn.UseDef)
                {
                    Label labOk = default;
                    ilw
                        .Bge(ref labOk)
                        .Dup().Xor()
                        .MarkLabel(labOk);
                }
                else
                    ilw.Blt(ref labBad);
                break;

            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                ilw.Conv_I8();
                break;

            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                ilw.Conv_U8();
                break;

            case DKind.R4:
            case DKind.R8:
                using (var locResult = codeGen.AcquireLocal(typeof(long)))
                {
                    ilw
                        .Ldloca(locResult)
                        .Call(kindSrc == DKind.R8 ? CodeGenUtil.R8TryToI8 : CodeGenUtil.R4TryToI8);
                    // When R8TryToI8 or R4TryToI8 fails, locResult is set
                    // to zero which is expected to continue.
                    if (fn.UseDef)
                        ilw.Pop();
                    else
                        ilw.Brfalse(ref labBadWithoutPop);
                    ilw.Ldloc(locResult);
                }
                break;
            }
        }

        // Construct the Date/Time.
        if (fn.IsDate)
            ilw.Call(_methDateFromTicks);
        else
            ilw.Newobj(_ctorTimeFromTicks);

        Validation.Assert(labBad == default || !fn.UseDef);
        if (!fn.UseDef)
        {
            // Wrap in a nullable.
            codeGen.TypeManager.GenWrapOpt(codeGen.Generator, fn.TypeRet.ToReq(), fn.TypeRet);

            if (labBad != default || labBadWithoutPop != default)
            {
                // Handle the bad case - generate null.
                Label labDone = default;
                ilw.Br(ref labDone);
                if (labBad != default)
                {
                    ilw
                        .MarkLabel(labBad)
                        .Pop();
                }
                ilw.MarkLabelIfUsed(labBadWithoutPop);
                codeGen.GenDefValType(stDst);
                ilw.MarkLabel(labDone);
            }
        }

        stRet = stDst;
        return true;
    }

    public static Date ExecDate(string arg)
    {
        if (Date.TryParse(arg, out var value))
            return value;
        return default;
    }

    public static Date? ExecDateOpt(string arg)
    {
        if (Date.TryParse(arg, out var value))
            return value;
        return default;
    }

    public static Time ExecTime(string arg)
    {
        if (Time.TryParse(arg, Date.Culture, out var value))
            return value;
        return default;
    }

    public static Time? ExecTimeOpt(string arg)
    {
        if (Time.TryParse(arg, Date.Culture, out var value))
            return value;
        return default;
    }

    public static Date ExecDate(Integer arg)
    {
        if (arg.Sign < 0 || arg > ChronoUtil.TicksMax)
            return default;
        return Date.FromTicks((long)arg);
    }

    public static Date? ExecDateOpt(Integer arg)
    {
        if (arg.Sign < 0 || arg > ChronoUtil.TicksMax)
            return default;
        return Date.FromTicks((long)arg);
    }

    public static Time ExecTime(Integer arg)
    {
        if (arg.Sign > 0 ? arg > long.MaxValue : arg < long.MinValue)
            return default;
        return new Time((long)arg);
    }

    public static Time? ExecTimeOpt(Integer arg)
    {
        if (arg.Sign > 0 ? arg > long.MaxValue : arg < long.MinValue)
            return default;
        return new Time((long)arg);
    }
}

public sealed class DateNowGen : RexlOperationGenerator<DateNowFunc>
{
    public static readonly DateNowGen Instance = new DateNowGen();

    private readonly MethodInfo _methAll;
    private readonly MethodInfo _methUtc;
    private readonly MethodInfo _methLoc;

    private DateNowGen()
    {
        _methAll = typeof(DateNowGen).GetMethod(nameof(Exec), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        _methUtc = new Func<ExecCtx, int, Date>(ExecUtc).Method;
        _methLoc = new Func<ExecCtx, int, Date>(ExecLoc).Method;
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return true;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        var ilw = codeGen.Writer;
        MethodInfo meth;
        switch (fn.Kind)
        {
        case DateNowFunc.ResultKind.Utc:
            meth = _methUtc;
            break;
        case DateNowFunc.ResultKind.Local:
            meth = _methLoc;
            break;

        default:
            Validation.Assert(fn.Kind == DateNowFunc.ResultKind.Record);
            using (var rg = codeGen.CreateRecordGenerator(fn.TypeRes))
            {
                codeGen.GenLoadExecCtxAndId(call);
                rg.LoadFieldAddr(new DName(DateNowFunc.UtcStr));
                rg.LoadFieldAddr(new DName(DateNowFunc.LocStr));
                rg.LoadFieldAddr(new DName(DateNowFunc.OffStr));
                ilw.Call(_methAll);
                stRet = rg.RecSysType;
                rg.Finish();
            }
            return true;
        }

        Validation.Assert(meth.ReturnType == typeof(Date));
        stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    private static void Exec(ExecCtx ctx, int id, out Date utc, out Date loc, out Time offset)
    {
        var dto = ctx.GetDateTimeOffset(id);
        utc = Date.FromTicks(dto.UtcTicks);
        // dto.LocalDateTime doesn't seem to use the offset in the DateTimeOffset! Instead it uses the
        // current ("local") time zone. We want to use the offset in dto. Also, dto.LocalDateTime or
        // dto.DateTime can throw if the utc value is too close to the range end points.
        loc = Date.FromTicks(dto.UtcTicks + dto.Offset.Ticks);
        offset = dto.Offset;
    }

    private static Date ExecUtc(ExecCtx ctx, int id)
    {
        var dto = ctx.GetDateTimeOffset(id);
        return Date.FromTicks(dto.UtcTicks);
    }

    private static Date ExecLoc(ExecCtx ctx, int id)
    {
        var dto = ctx.GetDateTimeOffset(id);
        // dto.LocalDateTime doesn't seem to use the offset in the DateTimeOffset! Instead it uses the
        // current ("local") time zone. We want to use the offset in dto. Also, dto.LocalDateTime or
        // dto.DateTime can throw if the utc value is too close to the range end points.
        return Date.FromTicks(dto.UtcTicks + dto.Offset.Ticks);
    }
}
