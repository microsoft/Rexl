// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

public abstract class TextFuncOne : OneToOneFunc
{
    public static readonly NPath NsText = NPath.Root.Append(new DName("Text"));

    protected TextFuncOne(DName name)
        : base(name, NsText)
    {
    }

    protected virtual DType RetType => DType.Text;

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll);
    }

    public override bool IsProperty(DType typeThis)
    {
        return typeThis == DType.Text;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        return (RetType, Immutable.Array.Create(DType.Text));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != RetType)
            return false;
        if (call.Args[0].Type != DType.Text)
            return false;
        return true;
    }
}

public sealed partial class TextCaseFunc : TextFuncOne
{
    public static readonly TextCaseFunc Lower = new TextCaseFunc(true);
    public static readonly TextCaseFunc Upper = new TextCaseFunc(false);

    /// <summary>
    /// Whether this is the <c>Lower</c> function rather than <c>Upper</c>.
    /// </summary>
    public bool IsLower { get; }

    /// <summary>
    /// Delegate to perform the operation on a string value.
    /// </summary>
    public Func<string, string> Map { get; }

    private TextCaseFunc(bool isLower)
        : base(new DName(isLower ? "Lower" : "Upper"))
    {
        IsLower = isLower;
        if (isLower)
            Map = ExecLower;
        else
            Map = ExecUpper;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (arg.TryGetString(out var str))
        {
            if (string.IsNullOrEmpty(str))
                return arg;
            return BndStrNode.Create(Map(str));
        }

        return call;
    }

    public static string ExecLower(string s)
    {
        if (s == null)
            return null;
        return s.ToLowerInvariant();
    }

    public static string ExecUpper(string s)
    {
        if (s == null)
            return null;
        return s.ToUpperInvariant();
    }
}

public sealed partial class TextLenFunc : TextFuncOne
{
    public static readonly TextLenFunc Instance = new TextLenFunc();

    private TextLenFunc()
        : base(new DName("Len"))
    {
    }

    protected override DType RetType => DType.I8Req;

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        if (args[0].TryGetString(out var str))
            return BndIntNode.CreateI8(Util.Size(str));

        return call;
    }
}

public sealed partial class TextConcatFunc : RexlOper
{
    public static readonly TextConcatFunc Instance = new TextConcatFunc();

    private TextConcatFunc()
        : base(isFunc: true, new DName("Concat"), TextFuncOne.NsText, 2, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        return ArgTraitsSimple.Create(this, eager: true, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        return (DType.Text, Immutable.Array.Create(DType.Text.ToSequence(), DType.Text));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Text)
            return false;
        if (call.Args[0].Type != DType.Text.ToSequence())
            return false;
        if (call.Args[1].Type != DType.Text)
            return false;
        return true;
    }

    // REVIEW: Implement Reduce?

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

public sealed partial class TextIndexOfFunc : RexlOper
{
    public static readonly TextIndexOfFunc IndexOf = new TextIndexOfFunc(last: false);
    public static readonly TextIndexOfFunc LastIndexOf = new TextIndexOfFunc(last: true);

    public bool IsLast { get; }

    private TextIndexOfFunc(bool last)
        : base(isFunc: true, new DName((last ? "LastIndexOf" : "IndexOf")), TextFuncOne.NsText, 2, 4)
    {
        IsLast = last;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        var maskOpt = BitSet.GetMask(2, carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskOpt);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot > 1)
            return false;
        if (dir == Directive.Ci)
            return true;
        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        switch (info.Arity)
        {
        case 2:
            return (DType.I8Req, Immutable.Array.Create(DType.Text, DType.Text));
        case 3:
            return (DType.I8Req, Immutable.Array.Create(DType.Text, DType.Text, DType.I8Req));
        default:
            Validation.Assert(info.Arity == 4);
            return (DType.I8Req, Immutable.Array.Create(DType.Text, DType.Text, DType.I8Req, DType.I8Req));
        }
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.I8Req)
            return false;
        var args = call.Args;
        if (args[0].Type != DType.Text)
            return false;
        if (args[1].Type != DType.Text)
            return false;
        if (args.Length > 2 && args[2].Type != DType.I8Req)
            return false;
        if (args.Length > 3 && args[3].Type != DType.I8Req)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        CompareOptions options = Directive.Ci == call.GetDirective(0) || Directive.Ci == call.GetDirective(1) ?
            CompareOptions.OrdinalIgnoreCase : CompareOptions.Ordinal;

        var args = call.Args;
        if (args[1].TryGetString(out var lookup))
        {
            if (string.IsNullOrEmpty(lookup) && args.Length == 2)
            {
                return IsLast ?
                    reducer.Reduce(BndCallNode.Create(TextLenFunc.Instance, call.Type, Immutable.Array<BoundNode>.Create(args[0]))) :
                    BndIntNode.CreateI8(0);
            }
            if (args[0].TryGetString(out var source))
            {
                switch (args.Length)
                {
                case 2:
                    return BndIntNode.CreateI8(IsLast ? ExecLast(source, lookup, options) : Exec(source, lookup, options));
                case 3:
                    if (args[2].TryGetIntegral(out var rangeIndex))
                    {
                        Validation.Assert(long.MinValue <= rangeIndex & rangeIndex <= long.MaxValue);
                        return BndIntNode.CreateI8(IsLast ? ExecLast(source, lookup, 0, (long)rangeIndex, options) :
                            Exec(source, lookup, (long)rangeIndex, int.MaxValue, options));
                    }
                    break;
                case 4:
                    if (args[2].TryGetIntegral(out var min) && args[3].TryGetIntegral(out var lim))
                    {
                        Validation.Assert(long.MinValue <= min & min <= long.MaxValue);
                        Validation.Assert(long.MinValue <= lim & lim <= long.MaxValue);
                        return BndIntNode.CreateI8(IsLast ? ExecLast(source, lookup, (long)min, (long)lim, options) :
                            Exec(source, lookup, (long)min, (long)lim, options));
                    }
                    break;
                }
            }
        }
        return base.ReduceCore(reducer, call);
    }

    public static long Exec(string source, string lookup, CompareOptions options)
    {
        if (string.IsNullOrEmpty(lookup))
            return 0;
        if (Util.Size(source) < lookup.Length)
            return -1;
        return CultureUtil.CompareInfo.IndexOf(source, lookup, options);
    }

    public static long ExecLast(string source, string lookup, CompareOptions options)
    {
        int cchSrc = Util.Size(source);
        if (string.IsNullOrEmpty(lookup))
            return cchSrc;
        if (cchSrc < lookup.Length)
            return -1;
        return CultureUtil.CompareInfo.LastIndexOf(source, lookup, options);
    }

    public static long Exec(string source, string lookup, long min, long lim, CompareOptions options)
    {
        int cchSrc = Util.Size(source);
        int cchLookup = Util.Size(lookup);
        // We are mimicking python behavior here.
        // I.e. Negative indices have the length added.
        if (min < 0)
            min += cchSrc;
        if (lim < 0)
            lim += cchSrc;
        // Negative min is set to zero and lim greater than length is set to the length.
        if (min < 0)
            min = 0;
        if (lim > cchSrc)
            lim = cchSrc;
        // Return -1 when range is invalid.
        if (lim - min < cchLookup)
            return -1;
        Validation.Assert(0 <= min & min <= lim & lim <= cchSrc);
        if (cchLookup == 0)
            return min;
        return CultureUtil.CompareInfo.IndexOf(source, lookup, (int)min, (int)(lim - min), options);
    }

    public static long ExecLast(string source, string lookup, long min, long lim, CompareOptions options)
    {
        int cchSrc = Util.Size(source);
        int cchLookup = Util.Size(lookup);
        // We are mimicking python behavior here.
        // I.e. Negative indices have the length added.
        if (min < 0)
            min += cchSrc;
        if (lim < 0)
            lim += cchSrc;
        // Negative min is set to zero and lim greater than length is set to the length.
        if (min < 0)
            min = 0;
        if (lim > cchSrc)
            lim = cchSrc;
        // Return -1 when range is invalid.
        if (lim - min < cchLookup)
            return -1;
        Validation.Assert(0 <= min & min <= lim & lim <= cchSrc);
        if (cchLookup == 0)
            return lim;
        return CultureUtil.CompareInfo.LastIndexOf(source, lookup, (int)(lim - 1), (int)(lim - min), options);
    }
}

public sealed partial class TextStartsWithFunc : RexlOper
{
    public static readonly TextStartsWithFunc StartsWith = new TextStartsWithFunc(starts: true);
    public static readonly TextStartsWithFunc EndsWith = new TextStartsWithFunc(starts: false);

    /// <summary>
    /// Whether this is <c>StartsWith</c> vs <c>EndsWith</c>.
    /// </summary>
    public bool Starts { get; }

    private TextStartsWithFunc(bool starts)
        : base(isFunc: true, new DName(starts ? "StartsWith" : "EndsWith"), TextFuncOne.NsText, 2, 2)
    {
        Starts = starts;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot > 1)
            return false;
        if (dir == Directive.Ci)
            return true;

        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.Assert(info.Arity == 2);

        return (DType.BitReq, Immutable.Array.Create(DType.Text, DType.Text));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.BitReq)
            return false;

        var args = call.Args;
        if (args[0].Type != DType.Text)
            return false;
        if (args[1].Type != DType.Text)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        CompareOptions options = Directive.Ci == call.GetDirective(0) || Directive.Ci == call.GetDirective(1) ?
            CompareOptions.OrdinalIgnoreCase : CompareOptions.Ordinal;

        var args = call.Args;
        if (args[1].TryGetString(out var lookup))
        {
            if (string.IsNullOrEmpty(lookup))
                return BndIntNode.True;

            if (args[0].TryGetString(out var source))
            {
                return BndIntNode.CreateBit(Starts ?
                    ExecStarts(source, lookup, options) :
                    ExecEnds(source, lookup, options));
            }
        }
        return base.ReduceCore(reducer, call);
    }

    public static bool ExecStarts(string source, string lookup, CompareOptions options)
    {
        if (string.IsNullOrEmpty(lookup))
            return true;
        if (string.IsNullOrEmpty(source))
            return false;
        if (source.Length < lookup.Length)
            return false;
        return CultureUtil.CompareInfo.IsPrefix(source, lookup, options);
    }

    public static bool ExecEnds(string source, string lookup, CompareOptions options)
    {
        if (string.IsNullOrEmpty(lookup))
            return true;
        if (string.IsNullOrEmpty(source))
            return false;
        if (source.Length < lookup.Length)
            return false;
        return CultureUtil.CompareInfo.IsSuffix(source, lookup, options);
    }
}

public sealed partial class TextPartFunc : RexlOper
{
    public static readonly TextPartFunc Instance = new TextPartFunc();

    private TextPartFunc()
        : base(isFunc: true, new DName("Part"), TextFuncOne.NsText, 2, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        var maskOpt = maskAll.ClearBit(0);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll, maskLiftOpt: maskOpt);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        if (info.Arity == 2)
            return (DType.Text, Immutable.Array.Create(DType.Text, DType.I8Req));
        Validation.Assert(info.Arity == 3);
        return (DType.Text, Immutable.Array.Create(DType.Text, DType.I8Req, DType.I8Req));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Text)
            return false;
        var args = call.Args;
        if (args[0].Type != DType.Text)
            return false;
        if (args[1].Type != DType.I8Req)
            return false;
        if (args.Length > 2 && args[2].Type != DType.I8Req)
            return false;
        return true;
    }

    // REVIEW: It's tempting to make this reduce to the text slice operation but this
    // currently uses the negative index convention (ala Python), which text slicing doesn't.
    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        if (args[0].TryGetString(out var source))
        {
            if (string.IsNullOrEmpty(source))
                return args[0];
            if (args[1].TryGetI8(out var start))
            {
                if (args.Length == 2)
                    return BndStrNode.Create(Exec(source, start));
                if (args[2].TryGetI8(out var end))
                    return BndStrNode.Create(Exec(source, start, end));
            }
        }
        return call;
    }

    public static string Exec(string str, long start)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        if (start >= str.Length)
            return "";

        // We are mimicking python's behavior where negative indices are treated
        // as starting from the back of the string. The regular python call will
        // throw an exception if a negative start value has absolute value greater 
        // than the length of the string, so we deviate by returning the entire string.
        if (start < 0)
        {
            start += str.Length;
            if (start < 0)
                return str;
        }
        Validation.Assert(0 <= start & start < str.Length);

        return str.Substring((int)start);
    }

    public static string Exec(string str, long start, long end)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        if (start >= str.Length)
            return "";
        if (start < 0)
        {
            start += str.Length;
            if (start < 0)
                start = 0;
        }
        Validation.Assert(0 <= start && start < str.Length);

        if (end >= str.Length)
            return str.Substring((int)start);
        if (end < 0)
        {
            end += str.Length;
            if (end < 0)
                end = 0;
        }

        if (end <= start)
            return "";
        Validation.Assert(start < end && end < str.Length);

        return str.Substring((int)start, (int)(end - start));
    }
}

/// <summary> Trims (leading/trailing/both) whitespace.
/// <example> Examples:
/// <code>Trim(" a a ")</code>  --> "a a"
/// <code>TrimStart(" a a ")</code>  --> "a a "
/// <code>TrimEnd(" a a ")</code>  --> " a a"
/// </example>
/// </summary> 
public sealed partial class TextTrimFunc : TextFuncOne
{
    public enum TrimKind : byte
    {
        // Trim both leading and trailing whitespace.
        All,
        // Trim only leading whitespace.
        Start,
        // Trim only trailing whitespace.
        End
    }

    public static readonly TextTrimFunc Trim = new TextTrimFunc(TrimKind.All, "Trim");
    public static readonly TextTrimFunc TrimStart = new TextTrimFunc(TrimKind.Start, "TrimStart");
    public static readonly TextTrimFunc TrimEnd = new TextTrimFunc(TrimKind.End, "TrimEnd");

    /// <summary>
    /// The trim kind.
    /// </summary>
    public TrimKind Kind { get; }

    /// <summary>
    /// A delegate that performs the operation on a string value.
    /// </summary>
    public Func<string, string> Map { get; }

    private TextTrimFunc(TrimKind kind, string name)
        : base(new DName(name))
    {
        switch (kind)
        {
        case TrimKind.Start:
            Map = ExecStart;
            break;
        case TrimKind.End:
            Map = ExecEnd;
            break;
        case TrimKind.All:
        default:
            Map = Exec;
            break;
        }
        Kind = kind;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var arg = call.Args[0];
        if (arg.TryGetString(out var str))
        {
            if (string.IsNullOrEmpty(str))
                return arg;
            return BndStrNode.Create(Map(str));
        }

        return call;
    }

    public static string Exec(string src)
    {
        // Null source maps to null output.
        if (src == null)
            return src;

        return src.Trim();
    }

    public static string ExecStart(string src)
    {
        if (src == null)
            return src;

        return src.TrimStart();
    }

    public static string ExecEnd(string src)
    {
        if (src == null)
            return src;

        return src.TrimEnd();
    }
}

public sealed partial class TextPadLeftFunc: RexlOper
{
    public static readonly TextPadLeftFunc Instance = new TextPadLeftFunc();

    private TextPadLeftFunc()
        : base(isFunc: true, new DName("PadLeft"), BindUtil.TextNs, 1, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        var maskOpt = maskAll.ClearBit(0);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll);
    }



    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        if (args[0].TryGetString(out var str))
            return BndIntNode.CreateI8(Util.Size(str));

        return call;
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Text)
            return false;
        var args = call.Args;
        if (args[0].Type != DType.I8Req)
            return false;
        if (args[1].Type != DType.Text)
            return false;
        return true;
    }

    public static string Exec(string src, int padding_len, char padding_char = ' ')
    {
        if (string.IsNullOrEmpty(src))
            return src;
        if (padding_len == 0)
            return src;
        return src.PadLeft(padding_len, padding_char);
    }
}

public sealed partial class TextReplaceFunc : RexlOper
{
    public static readonly TextReplaceFunc Instance = new TextReplaceFunc();

    private TextReplaceFunc()
        : base(isFunc: true, new DName("Replace"), BindUtil.TextNs, 3, 3)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        var maskAll = BitSet.GetMask(carg);
        var maskOpt = maskAll.ClearBit(0);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: maskAll, maskLiftTen: maskAll);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.Assert(info.Arity == 3);

        return (DType.Text, Immutable.Array.Create(DType.Text, DType.Text, DType.Text));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Type != DType.Text)
            return false;
        var args = call.Args;
        if (args[0].Type != DType.Text)
            return false;
        if (args[1].Type != DType.Text)
            return false;
        if (args[2].Type != DType.Text)
            return false;
        return true;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        // If src is null or empty, produce src.
        var bndSrc = call.Args[0];
        var s = bndSrc.TryGetString(out var src);
        if (s && string.IsNullOrEmpty(src))
            return bndSrc;

        // If rem is null or empty, produce src.
        var r = call.Args[1].TryGetString(out var rem);
        if (r && string.IsNullOrEmpty(rem))
            return bndSrc;

        // If both src and rem are constants, try more.
        if (s && r)
        {
            // If src doesn't contain rem, produce src.
            if (!src.Contains(rem))
                return bndSrc;
            // If all are constant, reduce.
            if (call.Args[2].TryGetString(out var ins))
                return BndStrNode.Create(Exec(src, rem, ins));
        }

        return call;
    }

    public static string Exec(string src, string remove, string insert)
    {
        if (string.IsNullOrEmpty(src))
            return src;
        if (string.IsNullOrEmpty(remove))
            return src;
        return src.Replace(remove, insert);
    }
}
