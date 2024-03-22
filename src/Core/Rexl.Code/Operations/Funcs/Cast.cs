// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

public sealed class CastGen : RexlOperationGenerator<CastFunc>
{
    public static CastGen Instance = new CastGen();

    private readonly MethodInfo _methStrToInteger;
    private readonly MethodInfo _methStrToI8;
    private readonly MethodInfo _methStrToU8;
    private readonly MethodInfo _methStrToR8;

    private readonly MethodInfo _methIntToI8;
    private readonly MethodInfo _methIntToU8;

    private CastGen()
    {
        _methStrToInteger = new Func<string, Integer>(CastFunc.ExecInteger).Method;
        _methStrToI8 = new Func<string, long>(CastFunc.ExecI8).Method;
        _methStrToU8 = new Func<string, ulong>(CastFunc.ExecU8).Method;
        _methStrToR8 = new Func<string, double>(CastFunc.ExecR8).Method;

        _methIntToI8 = new Func<Integer, long>(ExecI8).Method;
        _methIntToU8 = new Func<Integer, ulong>(ExecU8).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);
        var typeDst = fn.TypeDst;
        Validation.Assert(typeDst.IsNumericReq);

        var arg = call.Args[0];
        var typeSrc = arg.Type;
        Validation.Assert(!typeDst.Accepts(typeSrc, union: DType.UseUnionDefault));

        var kindSrc = typeSrc.RootKind;
        Validation.Assert(kindSrc.IsNumeric() || kindSrc == DKind.Text);

        var kindDst = typeDst.RootKind;
        stRet = fn.SysTypeDst;

        var ilw = codeGen.Writer;

        // Handle special src kinds.
        switch (kindSrc)
        {
        case DKind.Text:
            // Mapping from string.
            switch (kindDst)
            {
            case DKind.R8: ilw.Call(_methStrToR8); return true;
            case DKind.R4: ilw.Call(_methStrToR8).Conv_R4(); return true;
            case DKind.IA: ilw.Call(_methStrToInteger); return true;
            case DKind.I8: ilw.Call(_methStrToI8); return true;
            case DKind.I4: ilw.Call(_methStrToI8).Conv_I4(); return true;
            case DKind.I2: ilw.Call(_methStrToI8).Conv_I2(); return true;
            case DKind.I1: ilw.Call(_methStrToI8).Conv_I1(); return true;
            case DKind.U8: ilw.Call(_methStrToU8); return true;
            case DKind.U4: ilw.Call(_methStrToU8).Conv_U4(); return true;
            case DKind.U2: ilw.Call(_methStrToU8).Conv_U2(); return true;
            case DKind.U1: ilw.Call(_methStrToU8).Conv_U1(); return true;
            }
            Validation.Assert(false);
            return false;

        case DKind.IA:
            // Mapping from Integer.
            switch (kindDst)
            {
            case DKind.I8: ilw.Call(_methIntToI8); return true;
            case DKind.I4: ilw.Call(_methIntToI8).Conv_I4(); return true;
            case DKind.I2: ilw.Call(_methIntToI8).Conv_I2(); return true;
            case DKind.I1: ilw.Call(_methIntToI8).Conv_I1(); return true;
            case DKind.U8: ilw.Call(_methIntToU8); return true;
            case DKind.U4: ilw.Call(_methIntToU8).Conv_U4(); return true;
            case DKind.U2: ilw.Call(_methIntToU8).Conv_U2(); return true;
            case DKind.U1: ilw.Call(_methIntToU8).Conv_U1(); return true;
            }
            Validation.Assert(false);
            return false;

        case DKind.R8:
        case DKind.R4:
            // Mapping from floating point. Note that we do our own conversion, since the CLI
            // spec leaves too much unspecified.
            {
                bool isR8 = kindSrc == DKind.R8;
                Validation.Assert(isR8 || kindSrc == DKind.R4);

                switch (kindDst)
                {
                case DKind.R4:
                    Validation.Assert(isR8);
                    ilw.Conv_R4();
                    return true;
                case DKind.IA:
                    // Map non-finite values to zero.
                    Label labOk = default;
                    ilw
                        .Dup()
                        .Call(isR8 ? CodeGenUtil.R8IsFinite : CodeGenUtil.R4IsFinite)
                        .Brtrue(ref labOk)
                        .Pop()
                        .Ldc_Rx(0, isR8)
                        .MarkLabel(labOk)
                        .Newobj(isR8 ? CodeGenUtil.CtorIntFromR8 : CodeGenUtil.CtorIntFromR4);
                    return true;

                case DKind.I8:
                case DKind.U8:
                    ilw.Call(isR8 ? CodeGenUtil.R8ModToI8 : CodeGenUtil.R4ModToI8);
                    return true;
                case DKind.I4:
                case DKind.I2:
                case DKind.I1:
                    ilw.Call(isR8 ? CodeGenUtil.R8ModToI8 : CodeGenUtil.R4ModToI8);
                    ilw.Conv_XX(kindDst.NumericSize(), uns: false);
                    return true;
                case DKind.U4:
                case DKind.U2:
                case DKind.U1:
                    ilw.Call(isR8 ? CodeGenUtil.R8ModToI8 : CodeGenUtil.R4ModToI8);
                    ilw.Conv_XX(kindDst.NumericSize(), uns: true);
                    return true;
                }
                Validation.Assert(false);
                return false;
            }
        }

        // Critical note: The CLI stack model doesn't distinguish between signed and unsigned integer
        // values on the stack. The difference is entirely in the instructions operating on those
        // values. Thus, both the source and destination types determine the IL to use, not just
        // the destination type.
        Validation.Assert(kindSrc.IsIxOrUx());
        Validation.Assert(kindDst.IsIxOrUx());

#if DEBUG
        {
            bool unsSrc = kindSrc.IsUx();
            bool unsDst = kindDst.IsUx();
            int cbSrc = kindSrc.NumericSize();
            int cbDst = kindDst.NumericSize();
            Validation.Assert(cbSrc > cbDst || unsDst != unsSrc);
            Validation.Assert(cbSrc >= cbDst || unsDst & !unsSrc);
        }
#endif

        // Note that sizes <= 4 are represented on the stack as 4 bytes.
        // Signed values should be sign extended through those 4 bytes.
        // Unsigned values should be zero extended through those 4 bytes.
        switch (kindDst)
        {
        case DKind.I4:
            Validation.Assert(kindSrc.NumericSize() >= 4);
            // Chop to 4 bytes.
            if (kindSrc.NumericSize() > 4)
                ilw.Conv_I4();
            return true;
        case DKind.I2:
            Validation.Assert(kindSrc.NumericSize() >= 2);
            // Sign extend from 2 to 4 bytes.
            ilw.Conv_I2();
            return true;
        case DKind.I1:
            Validation.Assert(kindSrc.NumericSize() >= 1);
            // Sign extend from 1 to 4 bytes.
            ilw.Conv_I1();
            return true;
        case DKind.U8:
            Validation.Assert(kindSrc.IsIx());
            // Sign extend to 8 bytes.
            if (kindSrc.NumericSize() < 8)
                ilw.Conv_I8();
            return true;
        case DKind.U4:
            Validation.Assert(kindSrc.IsIx() || kindSrc.NumericSize() == 8);
            // Chop to 4 bytes.
            if (kindSrc.NumericSize() > 4)
                ilw.Conv_U4();
            return true;
        case DKind.U2:
            // Zero extend from 2 to 4 bytes.
            ilw.Conv_U2();
            return true;
        case DKind.U1:
            // Zero extend from 1 to 4 bytes.
            ilw.Conv_U1();
            return true;
        }

        throw Validation.BugExcept();
    }

    public static long ExecI8(Integer value)
    {
        // BigInteger throws when a cast operator is invoked and the value is
        // out of bounds. And it doesn't have a good work-around. The fix is
        // to mask with &, but & is very slow (over-allocating arrays), so we
        // try to avoid it, which we can do if the value is within the range of
        // either long or ulong. The tests are arranged so there is at only one
        // "expensive" test. The test against zero is cheap.
        if (value.Sign >= 0)
        {
            if (value <= ulong.MaxValue)
                return (long)(ulong)value;
        }
        else if (value >= long.MinValue)
            return (long)value;

        // The slow case.
        return (long)(ulong)(value & ulong.MaxValue);
    }

    public static ulong ExecU8(Integer value)
    {
        // BigInteger throws when a cast operator is invoked and the value is
        // out of bounds. And it doesn't have a good work-around. The fix is
        // to mask with &, but & is very slow (over-allocating arrays), so we
        // try to avoid it, which we can do if the value is within the range of
        // either long or ulong. The tests are arranged so there is at only one
        // "expensive" test. The test against zero is cheap.
        if (value.Sign >= 0)
        {
            if (value <= ulong.MaxValue)
                return (ulong)value;
        }
        else if (value >= long.MinValue)
            return (ulong)(long)value;

        // The slow case.
        return (ulong)(value & ulong.MaxValue);
    }
}

public sealed class ToXXGen : RexlOperationGenerator<ToXXFunc>
{
    public static ToXXGen Instance = new ToXXGen();

    private ToXXGen()
    {
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var fn = GetOper(call);

        int cur = idx + 1;

        var arg = call.Args[0];
        var type = arg.Type;
        Validation.BugCheckParam(!type.HasReq && type.SeqCount == 0, nameof(call));
        Validation.BugCheckParam(!fn.IsReducibleToCast(type), nameof(call));

        // The argument kind cannot be bool. The call with bool has been reduced to CastXX.
        var kindSrc = type.RootKind;
        Validation.BugCheckParam(
            kindSrc.IsNumeric() ||
            kindSrc == DKind.Text, nameof(call));
        var typeDst = call.Type;

        var stSrc = codeGen.GenCode(arg, ref cur);
        Validation.Assert(stSrc == codeGen.GetSystemType(type));

        var stDst = codeGen.GetSystemType(typeDst);
        Label labDefault = default;
        var ilw = codeGen.Writer;
        if (call.Args.Length == 1)
        {
            Validation.BugCheckParam(typeDst == fn.TypeOpt, nameof(call));

            GenTo(codeGen, fn, type, ref labDefault);
            codeGen.TypeManager.GenWrapOpt(codeGen.Generator, fn.TypeReq, typeDst);

            Label labDone = default;
            ilw.Br(ref labDone);
            ilw.MarkLabel(labDefault);
            codeGen.TypeManager.GenNull(codeGen.Generator, typeDst);
            ilw.MarkLabel(labDone);
        }
        else
        {
            Validation.BugCheckParam(typeDst.RootKind == fn.KindDst, nameof(call));
            Validation.BugCheckParam(typeDst == call.Args[1].Type, nameof(call));

            GenTo(codeGen, fn, type, ref labDefault);
            if (typeDst.IsOpt)
                codeGen.TypeManager.GenWrapOpt(codeGen.Generator, fn.TypeReq, typeDst);

            Label labDone = default;
            ilw
                .Br(ref labDone)
                .MarkLabel(labDefault);

            var stDef = codeGen.GenCode(call.Args[1], ref cur);
            Validation.Assert(stDef == stDst);

            ilw.MarkLabel(labDone);
        }
        Validation.Assert(cur == idx + call.NodeCount);

        stRet = stDst;
        wrap = default;
        return true;
    }

    private void GenTo(ICodeGen codeGen, ToXXFunc fn, DType typeSrc, ref Label labDefault)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(fn);
        Validation.Assert(typeSrc.RootKind.IsNumeric() || typeSrc.RootKind == DKind.Text);

        // Critical note: The CLI stack model doesn't distinguish between signed and unsigned integer
        // values on the stack. The difference is entirely in the instructions operating on those
        // values. Thus, both the source and destination types determine the IL to use, not just
        // the destination type.
        DKind kindSrc = typeSrc.RootKind;
        if (kindSrc == DKind.IA || kindSrc == DKind.Text)
            GenFromSpecial(codeGen, fn, typeSrc.RootKind, ref labDefault);
        else if (typeSrc.RootKind.IsFractional())
            GenFromFractional(codeGen, fn, typeSrc.RootKind, ref labDefault);
        else
            GenFromIxOrUx(codeGen, fn, typeSrc, ref labDefault);
    }

    private void GenFromSpecial(ICodeGen codeGen, ToXXFunc fn, DKind kindSrc, ref Label labDefault)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(fn);
        Validation.Assert(kindSrc.IsNumeric() || kindSrc == DKind.Text);

        // Handle special src kinds.
        var ilw = codeGen.Writer;
        switch (kindSrc)
        {
        case DKind.Text:
            switch (fn.KindDst)
            {
            case DKind.R8:
            case DKind.R4:
                using (var locResult = codeGen.AcquireLocal(typeof(double)))
                {
                    ilw
                        .Ldloca(locResult)
                        .Call(CodeGenUtil.StrTryParseR8)
                        .Brfalse(ref labDefault)
                        .Ldloc(locResult);
                }
                if (fn.KindDst == DKind.R4)
                    ilw.Conv_R4();
                break;

            case DKind.IA:
                using (var locResult = codeGen.AcquireLocal(typeof(Integer)))
                {
                    ilw
                        .Ldloca(locResult)
                        .Call(CodeGenUtil.StrTryParseInt)
                        .Brfalse(ref labDefault)
                        .Ldloc(locResult);
                }
                break;

            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                using (var locResult = codeGen.AcquireLocal(typeof(long)))
                {
                    ilw
                        .Ldloca(locResult)
                        .Call(CodeGenUtil.StrTryParseI8)
                        .Brfalse(ref labDefault)
                        .Ldloc(locResult);
                }
                GenFromIxOrUx(codeGen, fn, DType.I8Req, ref labDefault);
                break;
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                using (var locResult = codeGen.AcquireLocal(typeof(ulong)))
                {
                    ilw
                        .Ldloca(locResult)
                        .Call(CodeGenUtil.StrTryParseU8)
                        .Brfalse(ref labDefault)
                        .Ldloc(locResult);
                }
                GenFromIxOrUx(codeGen, fn, DType.U8Req, ref labDefault);
                break;
            }
            break;

        case DKind.IA:
            // Mapping from Integer.
            switch (fn.KindDst)
            {
            case DKind.R8:
            case DKind.R4:
                // This case has been reduced to CastRX.
                Validation.Assert(false);
                break;

            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                using (var locResult = codeGen.AcquireLocal(typeof(long)))
                {
                    ilw
                        .Ldc_I8(fn.Min)
                        .Ldc_I8((long)fn.Max)
                        .Ldloca(locResult)
                        .Call(CodeGenUtil.IntTryToI8)
                        .Brfalse(ref labDefault)
                        .Ldloc(locResult);
                }
                if (fn.KindDst != DKind.I8)
                    ilw.Conv_I4();
                break;
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
                using (var locResult = codeGen.AcquireLocal(typeof(ulong)))
                {
                    ilw
                        .Ldc_U8(fn.Max)
                        .Ldloca(locResult)
                        .Call(CodeGenUtil.IntTryToU8)
                        .Brfalse(ref labDefault)
                        .Ldloc(locResult);
                }
                if (fn.KindDst != DKind.U8)
                    ilw.Conv_U4();
                break;
            }
            break;
        }
    }

    private void GenFromFractional(ICodeGen codeGen, ToXXFunc fn, DKind kindSrc, ref Label labDefault)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(fn);
        Validation.Assert(kindSrc.IsFractional());

        var ilw = codeGen.Writer;
        bool isSrcR4 = (kindSrc == DKind.R4);

        if (fn.KindDst == DKind.IA)
        {
            Label labOk = default;
            ilw
                .Dup()
                .Call(isSrcR4 ? CodeGenUtil.R4IsFinite : CodeGenUtil.R8IsFinite)
                .Brtrue(ref labOk)
                .Pop()
                .Br(ref labDefault)
                .MarkLabel(labOk)
                .Newobj(isSrcR4 ? CodeGenUtil.CtorIntFromR4 : CodeGenUtil.CtorIntFromR8);
        }
        else
        {
            Validation.Assert(fn.KindDst.IsIxOrUx());
            using var locResult = codeGen.AcquireLocal(fn.KindDst.IsIx() ? typeof(long) : typeof(ulong));
            ilw.Ldloca(locResult);

            if (fn.KindDst.IsIx())
                ilw.Call(isSrcR4 ? CodeGenUtil.R4TryToI8 : CodeGenUtil.R8TryToI8);
            else
                ilw.Call(isSrcR4 ? CodeGenUtil.R4TryToU8 : CodeGenUtil.R8TryToU8);

            ilw.Brfalse(ref labDefault);
            ilw.Ldloc(locResult);
            GenFromIxOrUx(codeGen, fn, fn.KindDst.IsIx() ? DType.I8Req : DType.U8Req, ref labDefault);
        }
    }

    private void GenFromIxOrUx(ICodeGen codeGen, ToXXFunc fn, DType typeSrc, ref Label labDefault)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(fn);

        DKind kindSrc = typeSrc.RootKind;
        Validation.Assert(kindSrc.IsIxOrUx());

        if (kindSrc == fn.KindDst)
            return;

        bool unsSrc = kindSrc.IsUx();
        bool unsDst = fn.KindDst.IsUx();
        var sizeSrc = kindSrc.NumericSize();
        var sizeDst = fn.KindDst.NumericSize();
        Validation.Assert(
            (unsSrc == unsDst && sizeSrc > sizeDst) ||
            (unsSrc && !unsDst && sizeSrc >= sizeDst) ||
            (!unsSrc && unsDst));

        // When the conversion is narrowing a signed type to an unsigned
        // type, treat the source as unsigned. The maximum check
        // simultaneously serves as the minimum check.
        if (!unsSrc && unsDst && sizeSrc > sizeDst)
            unsSrc = true;

        var stSrc = codeGen.GetSystemType(typeSrc);
        var ilw = codeGen.Writer;
        using var locSrc = codeGen.AcquireLocal(stSrc);
        ilw.Stloc(locSrc);

        // Checks the minimum of the destination type.
        // The check is needed when converting from a signed type 1) to
        // an unsigned type or 2) to a narrower signed type.
        if (!unsSrc)
        {
            Validation.Assert(unsDst || sizeSrc > sizeDst);
            ilw.Ldc_Ix(fn.Min, sizeSrc == 8);
            ilw.Ldloc(locSrc);
            ilw.Bgt(ref labDefault);
        }

        // Checks the maximum of the destination type.
        // The check is needed when 1) the source type is wider or 2)
        // the unsigned source is as wide as an signed destination.
        if (sizeSrc > sizeDst || unsSrc)
        {
            Validation.Assert(sizeSrc >= sizeDst);
            ilw.Ldloc(locSrc);
            ilw.Ldc_Ux(fn.Max, sizeSrc == 8);
            if (!unsSrc)
                ilw.Bgt(ref labDefault);
            else
                ilw.Bgt_Un(ref labDefault);
        }

        ilw.Ldloc(locSrc);
        if (sizeSrc < sizeDst)
            ilw.Conv_XX(sizeDst, unsSrc);
    }
}

public sealed class ToTextGen : RexlOperationGenerator<ToTextFunc>
{
    public static readonly ToTextGen Instance = new ToTextGen();

    private readonly MethodInfo _methR4;
    private readonly MethodInfo _methR8;
    private readonly MethodInfo _methDate;
    private readonly MethodInfo _methDate2;
    private readonly MethodInfo _methTime2;
    private readonly MethodInfo _methGuid2;

    private ToTextGen()
    {
        _methR4 = new Func<float, string>(NumUtil.ToStr).Method;
        _methR8 = new Func<double, string>(NumUtil.ToStr).Method;
        _methDate = new Func<Date, string>(Exec).Method;
        _methDate2 = new Func<Date, string, string>(Exec).Method;
        _methTime2 = new Func<Time, string, string>(Exec).Method;
        _methGuid2 = new Func<Guid, string, string>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var type = call.Args[0].Type;
        Validation.Assert(type.IsPrimitiveReq);
        Validation.Assert(type != DType.Text);
        var il = codeGen.Writer;
        var st = codeGen.GetSystemType(type);
        Validation.Assert(st.IsValueType);

        if (call.Args.Length == 2)
        {
            switch (type.Kind)
            {
            case DKind.Date:
                stRet = GenCall(codeGen, _methDate2, sts);
                return true;
            case DKind.Time:
                stRet = GenCall(codeGen, _methTime2, sts);
                return true;
            case DKind.Guid:
                stRet = GenCall(codeGen, _methGuid2, sts);
                return true;
            default:
                Validation.Assert(false);
                stRet = null;
                return false;
            }
        }

        switch (type.Kind)
        {
        case DKind.R8:
            stRet = GenCall(codeGen, _methR8, sts);
            return true;
        case DKind.R4:
            stRet = GenCall(codeGen, _methR4, sts);
            return true;
        case DKind.Date:
            stRet = GenCall(codeGen, _methDate, sts);
            return true;
        case DKind.Bit:
            {
                Label labFalse = default;
                Label labDone = default;
                il
                    .Brfalse(ref labFalse)
                    .Ldstr("true")
                    .Br(ref labDone)
                    .MarkLabel(labFalse)
                    .Ldstr("false")
                    .MarkLabel(labDone);
            }
            stRet = typeof(string);
            return true;
        }

        stRet = typeof(string);
        using var loc = codeGen.AcquireLocal(st);

        // Note that only value types are possible, so we use ldloca / call
        // instead of ldloc / callvirt.
        Validation.Assert(st.IsValueType);
        il
            .Stloc(loc)
            .Ldloca(loc)
            .CallVirtAsNonVirt(st.GetMethod("ToString", new Type[] { }).VerifyValue());
        return true;
    }

    public static string Exec(Date value)
    {
        try
        {
            return value.ToString();
        }
        catch (ArgumentOutOfRangeException)
        {
            // REVIEW: This exception is currently not possible in Rexl but may be when
            // we add culture changing functions.
            return null;
        }
    }

    public static string Exec(Date value, string format)
    {
        try
        {
            return value.ToString(format);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static string Exec(Time value, string format)
    {
        try
        {
            return value.ToString(format, Date.Culture);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static string Exec(Guid guid, string format)
    {
        try
        {
            return guid.ToString(format);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
