// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class TextCaseGen : GetMethGen<TextCaseFunc>
{
    public static readonly TextCaseGen Instance = new TextCaseGen();

    private TextCaseGen()
    {
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);
        meth = fn.Map.Method;
        return true;
    }
}

public sealed class TextLenGen : GetMethGen<TextLenFunc>
{
    public static readonly TextLenGen Instance = new TextLenGen();

    private readonly MethodInfo _meth;

    private TextLenGen()
    {
        _meth = new Func<string, long>(Exec).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        meth = _meth;
        return true;
    }

    public static long Exec(string s)
    {
        if (s is null)
            return 0;
        return s.Length;
    }
}

public sealed class TextConcatGen : RexlOperationGenerator<TextConcatFunc>
{
    public static readonly TextConcatGen Instance = new TextConcatGen();

    private readonly MethodInfo _meth;

    private TextConcatGen()
    {
        _meth = new Func<IEnumerable<string>, string, ExecCtx, int, string>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        stRet = GenCallCtxId(codeGen, _meth, sts, call);
        return true;
    }

    public static string Exec(IEnumerable<string> a, string b, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(a);
        Validation.AssertValueOrNull(b);
        Validation.AssertValue(ctx);

        if (a == null)
            return "";

        using var en = a.GetEnumerator();
        ctx.Ping(id);
        if (!en.MoveNext())
            return "";

        var first = en.Current;
        ctx.Ping(id);
        if (!en.MoveNext())
            return first ?? "";

        var sb = new StringBuilder();
        sb.Append(first);

        for (; ; )
        {
            sb.Append(b).Append(en.Current);
            ctx.Ping(id);
            if (!en.MoveNext())
                return sb.ToString();
        }
    }
}

public sealed class TextIndexOfGen : RexlOperationGenerator<TextIndexOfFunc>
{
    public static readonly TextIndexOfGen Instance = new TextIndexOfGen();

    private readonly MethodInfo _meth2;
    private readonly MethodInfo _methLast2;
    private readonly MethodInfo _meth4;
    private readonly MethodInfo _methLast4;

    private TextIndexOfGen()
    {
        _meth2 = new Func<string, string, CompareOptions, long>(TextIndexOfFunc.ExecLast).Method;
        _methLast2 = new Func<string, string, CompareOptions, long>(TextIndexOfFunc.Exec).Method;
        _meth4 = new Func<string, string, long, long, CompareOptions, long>(TextIndexOfFunc.ExecLast).Method;
        _methLast4 = new Func<string, string, long, long, CompareOptions, long>(TextIndexOfFunc.Exec).Method;
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);

        CompareOptions options = Directive.Ci == call.GetDirective(0) || Directive.Ci == call.GetDirective(1) ?
            CompareOptions.OrdinalIgnoreCase : CompareOptions.Ordinal;

        int cur = idx + 1;

        MethodInfo meth;
        var st0 = codeGen.GenCode(call.Args[0], ref cur);
        var st1 = codeGen.GenCode(call.Args[1], ref cur);
        Type st2;
        Type st3;
        switch (call.Args.Length)
        {
        case 2:
            // No indices provided; no default values required.
            meth = fn.IsLast ? _meth2 : _methLast2;
            Validation.Assert(AreCompatible(meth, default, st0, st1, typeof(CompareOptions)));
            break;
        case 3:
            // Only one of indices provided; push default value for the other.
            {
                Validation.Assert(call.Args[2].Type == DType.I8Req);
                if (fn.IsLast)
                {
                    codeGen.Writer.Ldc_I8(0);
                    st2 = typeof(long);
                    st3 = codeGen.GenCode(call.Args[2], ref cur);
                }
                else
                {
                    st2 = codeGen.GenCode(call.Args[2], ref cur);
                    codeGen.Writer.Ldc_I4(int.MaxValue);
                    st3 = typeof(long);
                }
                meth = fn.IsLast ? _meth4 : _methLast4;
                Validation.Assert(AreCompatible(meth, default, st0, st1, st2, st3, typeof(CompareOptions)));
            }
            break;
        default:
            // Both indices provided, no default values required.
            {
                Validation.Assert(call.Args.Length == 4);
                Validation.Assert(call.Args[2].Type == DType.I8Req);
                Validation.Assert(call.Args[3].Type == DType.I8Req);
                st2 = codeGen.GenCode(call.Args[2], ref cur);
                st3 = codeGen.GenCode(call.Args[3], ref cur);
                meth = fn.IsLast ? _meth4 : _methLast4;
                Validation.Assert(AreCompatible(meth, default, st0, st1, st2, st3, typeof(CompareOptions)));
            }
            break;
        }
        Validation.Assert(cur == idx + call.NodeCount);

        codeGen.Writer.Ldc_I4((int)options);
        codeGen.Writer.Call(meth);
        stRet = meth.ReturnType;
        wrap = default;
        return true;
    }
}

public sealed class TextStartsWithGen : RexlOperationGenerator<TextStartsWithFunc>
{
    public static readonly TextStartsWithGen Instance = new TextStartsWithGen();

    private readonly MethodInfo _methStarts;
    private readonly MethodInfo _methEnds;

    private TextStartsWithGen()
    {
        _methStarts = new Func<string, string, CompareOptions, bool>(TextStartsWithFunc.ExecStarts).Method;
        _methEnds = new Func<string, string, CompareOptions, bool>(TextStartsWithFunc.ExecEnds).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        CompareOptions options = Directive.Ci == call.GetDirective(0) || Directive.Ci == call.GetDirective(1) ?
            CompareOptions.OrdinalIgnoreCase : CompareOptions.Ordinal;
        codeGen.Writer.Ldc_I4((int)options);

        var meth = fn.Starts ? _methStarts : _methEnds;
        Validation.Assert(AreCompatible(meth, sts, typeof(CompareOptions)));

        codeGen.Writer.Call(meth);
        stRet = meth.ReturnType;
        return true;
    }
}

public sealed class TextPartGen : GetMethGen<TextPartFunc>
{
    public static readonly TextPartGen Instance = new TextPartGen();

    private readonly MethodInfo _meth2;
    private readonly MethodInfo _meth3;

    private TextPartGen()
    {
        _meth2 = new Func<string, long, string>(TextPartFunc.Exec).Method;
        _meth3 = new Func<string, long, long, string>(TextPartFunc.Exec).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        meth = call.Args.Length == 2 ? _meth2 : _meth3;
        return true;
    }
}

public sealed class TextTrimGen : GetMethGen<TextTrimFunc>
{
    public static readonly TextTrimGen Instance = new TextTrimGen();

    private TextTrimGen()
    {
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);
        meth = fn.Map.Method;
        return true;
    }
}

public sealed class TextReplaceGen : GetMethGen<TextReplaceFunc>
{
    public static readonly TextReplaceGen Instance = new TextReplaceGen();

    private readonly MethodInfo _meth;

    private TextReplaceGen()
    {
        _meth = new Func<string, string, string, string>(TextReplaceFunc.Exec).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        meth = _meth;
        return true;
    }
}

public sealed class TextPadGen : GetMethGen<TextPadFunc>
{
    public static readonly TextPadGen Instance = new TextPadGen();

    private readonly MethodInfo _methLeft;
    private readonly MethodInfo _methRight;

    private TextPadGen()
    {
        _methLeft = new Func<string, long, string>(TextPadFunc.ExecLeft).Method;
        _methRight = new Func<string, long, string>(TextPadFunc.ExecRight).Method;
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);
        meth = fn.IsLeft ? _methLeft : _methRight;
        return true;
    }
}
