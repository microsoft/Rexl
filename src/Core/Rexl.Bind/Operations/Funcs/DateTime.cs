// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;
/// <summary>
/// Creates a DateTime value using a specified year, month, day, and optionally
/// hour, minute, second, millisecond, and ticks. If the arguments are invalid,
/// returns the default DateTime 1/1/0001 00:00:00.
/// </summary>
public sealed partial class DateFunc : RexlOper
{
    public static readonly DateFunc Instance = new DateFunc();

    private DateFunc()
        : base(isFunc: true, new DName("Date"), 3, 8)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.DateReq, Immutable.Array.Fill(DType.I8Req, info.Arity));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.DateReq)
            return false;
        var args = call.Args;
        for (int slot = 0; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Creates a TimeSpan value using a specified number of days, and optionally
/// hours, minutes, seconds, milliseconds, and ticks. On overflow, returns the zero time-span.
/// </summary>
public sealed partial class TimeFunc : RexlOper
{
    public static readonly TimeFunc Instance = new TimeFunc();

    private TimeFunc()
        : base(isFunc: true, new DName("Time"), 1, 6)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.TimeReq, Immutable.Array.Fill(DType.I8Req, info.Arity));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.TimeReq)
            return false;
        var args = call.Args;
        for (int slot = 0; slot < args.Length; slot++)
        {
            if (args[slot].Type != DType.I8Req)
                return false;
        }
        return true;
    }
}

/// <summary>
/// These functions extract information from a DateTime or TimeSpan.
/// </summary>
public sealed partial class ChronoPartFunc : RexlOper
{
    public enum PartKind : byte
    {
        DateYear,
        DateMonth,
        DateDay,
        DateHour,
        DateMinute,
        DateSecond,
        DateMillisecond,
        DateTick,
        DateTotalTicks,
        DateDayOfYear,
        DateDayOfWeek,
        DateStartOfYear,
        DateStartOfMonth,
        DateStartOfWeek,

        /// <summary>
        /// Extract the date portion, dropping the time.
        /// </summary>
        DateDate,

        /// <summary>
        /// Extract the time portion as a TimeSpan.
        /// </summary>
        DateTime,

        TimeDay,
        TimeHour,
        TimeMinute,
        TimeSecond,
        TimeMillisecond,
        TimeTick,
        TimeTotalDays,
        TimeTotalHours,
        TimeTotalMinutes,
        TimeTotalSeconds,
        TimeTotalMilliseconds,
        TimeTotalTicks,
    }

    // Primitive ones.
    public static readonly ChronoPartFunc DateYear = new ChronoPartFunc(PartKind.DateYear, "Year", DType.I4Req);
    public static readonly ChronoPartFunc DateMonth = new ChronoPartFunc(PartKind.DateMonth, "Month", DType.I4Req);
    public static readonly ChronoPartFunc DateDay = new ChronoPartFunc(PartKind.DateDay, "Day", DType.I4Req);
    public static readonly ChronoPartFunc DateHour = new ChronoPartFunc(PartKind.DateHour, "Hour", DType.I4Req);
    public static readonly ChronoPartFunc DateMinute = new ChronoPartFunc(PartKind.DateMinute, "Minute", DType.I4Req);
    public static readonly ChronoPartFunc DateSecond = new ChronoPartFunc(PartKind.DateSecond, "Second", DType.I4Req);
    public static readonly ChronoPartFunc DateMillisecond = new ChronoPartFunc(PartKind.DateMillisecond, "Millisecond", DType.I4Req);
    public static readonly ChronoPartFunc DateTick = new ChronoPartFunc(PartKind.DateTick, "Tick", DType.I4Req);
    public static readonly ChronoPartFunc DateTotalTicks = new ChronoPartFunc(PartKind.DateTotalTicks, "TotalTicks", DType.I8Req);

    public static readonly ChronoPartFunc TimeDay = new ChronoPartFunc(PartKind.TimeDay, "Day", DType.I4Req);
    public static readonly ChronoPartFunc TimeHour = new ChronoPartFunc(PartKind.TimeHour, "Hour", DType.I4Req);
    public static readonly ChronoPartFunc TimeMinute = new ChronoPartFunc(PartKind.TimeMinute, "Minute", DType.I4Req);
    public static readonly ChronoPartFunc TimeSecond = new ChronoPartFunc(PartKind.TimeSecond, "Second", DType.I4Req);
    public static readonly ChronoPartFunc TimeMillisecond = new ChronoPartFunc(PartKind.TimeMillisecond, "Millisecond", DType.I4Req);
    public static readonly ChronoPartFunc TimeTick = new ChronoPartFunc(PartKind.TimeTick, "Tick", DType.I4Req);
    public static readonly ChronoPartFunc TimeTotalDays = new ChronoPartFunc(PartKind.TimeTotalDays, "TotalDays", DType.R8Req);
    public static readonly ChronoPartFunc TimeTotalHours = new ChronoPartFunc(PartKind.TimeTotalHours, "TotalHours", DType.R8Req);
    public static readonly ChronoPartFunc TimeTotalMinutes = new ChronoPartFunc(PartKind.TimeTotalMinutes, "TotalMinutes", DType.R8Req);
    public static readonly ChronoPartFunc TimeTotalSeconds = new ChronoPartFunc(PartKind.TimeTotalSeconds, "TotalSeconds", DType.R8Req);
    public static readonly ChronoPartFunc TimeTotalMilliseconds = new ChronoPartFunc(PartKind.TimeTotalMilliseconds, "TotalMilliseconds", DType.R8Req);
    public static readonly ChronoPartFunc TimeTotalTicks = new ChronoPartFunc(PartKind.TimeTotalTicks, "TotalTicks", DType.I8Req);

    // Extra ones.
    public static readonly ChronoPartFunc DateDayOfYear = new ChronoPartFunc(PartKind.DateDayOfYear, "DayOfYear", DType.I4Req);
    public static readonly ChronoPartFunc DateDayOfWeek = new ChronoPartFunc(PartKind.DateDayOfWeek, "DayOfWeek", DType.I4Req);
    public static readonly ChronoPartFunc DateStartOfYear = new ChronoPartFunc(PartKind.DateStartOfYear, "StartOfYear", DType.DateReq);
    public static readonly ChronoPartFunc DateStartOfMonth = new ChronoPartFunc(PartKind.DateStartOfMonth, "StartOfMonth", DType.DateReq);
    public static readonly ChronoPartFunc DateStartOfWeek = new ChronoPartFunc(PartKind.DateStartOfWeek, "StartOfWeek", DType.DateReq);
    public static readonly ChronoPartFunc DateDate = new ChronoPartFunc(PartKind.DateDate, "Date", DType.DateReq);
    public static readonly ChronoPartFunc DateTime = new ChronoPartFunc(PartKind.DateTime, "Time", DType.TimeReq);

    /// <summary>
    /// The kind of part that this function extracts.
    /// </summary>
    public PartKind Kind { get; }

    /// <summary>
    /// Whether this is for date vs time.
    /// </summary>
    public bool IsDate { get; }

    /// <summary>
    /// The source type.
    /// </summary>
    public DType TypeSrc { get; }

    /// <summary>
    /// The return type.
    /// </summary>
    public DType TypeRet { get; }

    private ChronoPartFunc(PartKind kind, string name, DType type)
        : base(isFunc: true, new DName(name), kind < PartKind.TimeDay ? BindUtil.DateNs : BindUtil.TimeNs, 1, 1)
    {
        Kind = kind;
        IsDate = kind < PartKind.TimeDay;
        TypeSrc = IsDate ? DType.DateReq : DType.TimeReq;
        TypeRet = type;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftTen: 0x01, maskLiftOpt: 0x01);
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis == TypeSrc;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 1);

        return (TypeRet, Immutable.Array.Create(TypeSrc));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeRet)
            return false;
        if (call.Args[0].Type != (IsDate ? DType.DateReq : DType.TimeReq))
            return false;
        return true;
    }
}

/// <summary>
/// Converts a value to a DateTime or TimeSpan. If conversion fails, returns either
/// the default value or null. The 0 arity overload returns the default or (typed) null.
/// </summary>
public sealed partial class CastChronoFunc : RexlOper
{
    public static readonly CastChronoFunc CastDate = new CastChronoFunc("CastDate", DType.DateReq, date: true, def: true);
    public static readonly CastChronoFunc CastTime = new CastChronoFunc("CastTime", DType.TimeReq, date: false, def: true);
    public static readonly CastChronoFunc ToDate = new CastChronoFunc("ToDate", DType.DateOpt, date: true, def: false);
    public static readonly CastChronoFunc ToTime = new CastChronoFunc("ToTime", DType.TimeOpt, date: false, def: false);

    /// <summary>
    /// The return type.
    /// </summary>
    public DType TypeRet { get; }

    /// <summary>
    /// Whether date vs time.
    /// </summary>
    public bool IsDate { get; }

    /// <summary>
    /// Whether failure produces default value vs null.
    /// </summary>
    public bool UseDef { get; }

    private CastChronoFunc(string name, DType type, bool date, bool def)
        : base(isFunc: true, new DName(name), def ? 0 : 1, 1)
    {
        Validation.Assert(type.Kind.IsChrono());
        TypeRet = type;
        IsDate = date;
        UseDef = def;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity <= 1);

        if (info.Arity == 0)
            return (TypeRet, Immutable.Array<DType>.Empty);

        var type = info.Args[0].Type;
        Validation.Assert(!type.HasReq);
        Validation.Assert(type.SeqCount == 0);

        var kind = type.RootKind;
        if (kind != DKind.Text && !kind.IsNumeric())
            type = DType.I8Req;

        // REVIEW: Seems like ToTime should produce non-opt for Ix or Ux except U8.
        // Similarly, ToDate could produce non-opt for Ux except U8.
        return (TypeRet, Immutable.Array.Create(type));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeRet)
            return false;
        var args = call.Args;
        if (args.Length == 0)
        {
            // Default is reduced.
            full = false;
        }
        else
        {
            var typeSrc = args[0].Type;
            if (typeSrc.IsSequence)
                return false;
            if (typeSrc.HasReq)
                return false;
            var kind = typeSrc.RootKind;
            if (!kind.IsNumeric() && kind != DKind.Text)
                return false;
        }
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode bnd)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(bnd));

        if (bnd.Args.Length == 0)
        {
            Validation.Assert(UseDef);
            return BndDefaultNode.Create(bnd.Type);
        }

        return base.ReduceCore(reducer, bnd);
    }
}

/// <summary>
/// Volatile functions that produce current date/time information. There are three variants:
/// <list type="bullet">
/// <item><c>Date.Now()</c> produces a record with <c>Utc</c>, <c>Local</c> and <c>Offset</c> fields.</item>
/// <item><c>Date.Now.Utc()</c> produces the utc value.</item>
/// <item><c>Date.Now.Local()</c> produces the local value.</item>
/// </list>
/// </summary>
public sealed partial class DateNowFunc : RexlOper
{
    public enum ResultKind : byte
    {
        Record,
        Utc,
        Local,
    }

    public const string UtcStr = "Utc";
    public const string LocStr = "Local";
    public const string OffStr = "Offset";

    private static readonly NPath NowPath = NPath.Root.Append(new DName("Date")).Append(new DName("Now"));
    private static readonly DType All = DType.CreateRecord(opt: false,
        new TypedName(UtcStr, DType.DateReq),
        new TypedName(LocStr, DType.DateReq),
        new TypedName(OffStr, DType.TimeReq));

    public static readonly DateNowFunc Rec = new DateNowFunc(ResultKind.Record, NowPath.Parent, NowPath.Leaf, All);
    public static readonly DateNowFunc Utc = new DateNowFunc(ResultKind.Utc, NowPath, new DName(UtcStr), DType.DateReq);
    public static readonly DateNowFunc Loc = new DateNowFunc(ResultKind.Local, NowPath, new DName(LocStr), DType.DateReq);

    /// <summary>
    /// The kind of date now function.
    /// </summary>
    public ResultKind Kind { get; }

    /// <summary>
    /// The return type.
    /// </summary>
    public DType TypeRes { get; }

    private DateNowFunc(ResultKind kind, NPath ns, DName name, DType type)
        : base(isFunc: true, name, ns, 0, 0)
    {
        Kind = kind;
        TypeRes = type;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(carg == 0);
        return ArgTraitsSimple.Create(this, eager: false, 0);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.Arity == 0);
        return (TypeRes, Immutable.Array<DType>.Empty);
    }

    public override bool IsVolatile(ArgTraits traits, DType type, Immutable.Array<BoundNode> args,
        Immutable.Array<ArgScope> scopes, Immutable.Array<ArgScope> indices,
        Immutable.Array<Directive> dirs, Immutable.Array<DName> names)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.Assert(type.IsValid);
        Validation.Assert(!args.IsDefault);
        Validation.Assert(traits.SlotCount == args.Length);
        Validation.Assert(!scopes.IsDefault);
        Validation.Assert(scopes.Length == traits.ScopeCount);
        Validation.Assert(!indices.IsDefault);
        Validation.Assert(indices.Length == traits.ScopeIndexCount);

        return true;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != TypeRes)
            return false;
        return true;
    }
}
