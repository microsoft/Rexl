// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

// REVIEW: Rolling our own statistical library would allow us to
// custom-tailor pinging behavior, directly supported data types, etc.

// For these functions, a dof of NaN indicates insufficient sample count(s)
// to produce a non-negative dof. A dof of either NaN or 0 will produce NaNs
// for the test results.
public abstract class TTestBaseGen<TOper> : RexlOperationGenerator<TOper>
    where TOper : TTestBaseFunc
{
    // For brevity.
    protected const double NaN = double.NaN;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ExecSingleCore(IEnumerable<double> x, double popMean,
        out double pl, out double pr, out double p2,
        out double dof, out double stderr, out double t,
        out long count, out double mean, out double variance)
    {
        Validation.AssertValue(x);

        var stats = new RunningStatistics(x);

        count = stats.Count;
        mean = stats.Mean;
        variance = stats.Variance;

        dof = count >= 1 ? count - 1 : NaN;
        stderr = Math.Sqrt(variance / count);
        t = (stats.Mean - popMean) / stderr;
        GetPVals(dof, t, out pl, out pr, out p2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void GetPVals(double dof, double t, out double pLeft, out double pRight, out double pTwoSided)
    {
        if (double.IsNaN(dof) || double.IsNaN(t))
        {
            pLeft = NaN;
            pRight = NaN;
            pTwoSided = NaN;
            return;
        }

        pLeft = StudentT.CDF(0, 1, dof, t);
        Validation.Assert(0 <= pLeft & pLeft <= 1);
        pRight = 1 - pLeft;
        pTwoSided = 2 * Math.Min(pLeft, pRight);
    }

    public static IEnumerable<double> FilterNulls(IEnumerable<double?> src, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        foreach (var item in src)
        {
            ctx.Ping(id);
            if (item == null)
                continue;
            yield return item.GetValueOrDefault();
        }
    }
}

public sealed partial class TTestOneSampleGen : TTestBaseGen<TTestOneSampleFunc>
{
    public static readonly TTestOneSampleGen Instance = new TTestOneSampleGen();

    private TTestOneSampleGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        var ilw = codeGen.Writer;

        // Default population mean of 0.
        if (sts.Length == 1)
            ilw.Ldc_R8(0);

        var meth = Execs.GetMeth(call.Args[0].Type.ItemTypeOrThis.IsOpt);

        Validation.Assert(fn.TypeRes.FieldCount == meth.GetParameters().Count(p => p.IsOut));
        using var rg = codeGen.CreateRecordGenerator(fn.TypeRes);
        foreach (var fld in fn.FieldsRes)
            rg.LoadFieldAddr(fld.Name);

        codeGen.GenLoadExecCtxAndId(call);
        ilw.Call(meth);
        rg.Finish();
        stRet = rg.RecSysType;
        return true;
    }

    private static partial class Execs
    {
        // Exec definitions are generated in a separate partial.

        public static MethodInfo GetMeth(bool opt)
        {
            return opt ? _methExecOpt : _methExecReq;
        }
    }
}

public sealed partial class TTestTwoSampleGen : TTestBaseGen<TTestTwoSampleFunc>
{
    public static readonly TTestTwoSampleGen Instance = new TTestTwoSampleGen();

    private TTestTwoSampleGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(call);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        var ilw = codeGen.Writer;

        bool isXOpt = call.Args[0].Type.ItemTypeOrThis.IsOpt;
        bool isYOpt = call.Args[1].Type.ItemTypeOrThis.IsOpt;
        var meth = Execs.GetMeth(isXOpt, isYOpt);

        // Assume unequal variances if not given (equal_var = false).
        if (call.Args.Length == 2)
            ilw.Ldc_I4(0);

        Validation.Assert(fn.TypeRes.FieldCount == meth.GetParameters().Count(p => p.IsOut));
        using var rg = codeGen.CreateRecordGenerator(fn.TypeRes);
        foreach (var fld in fn.FieldsRes)
            rg.LoadFieldAddr(fld.Name);

        codeGen.GenLoadExecCtxAndId(call, 2);
        ilw.Call(meth);
        rg.Finish();
        stRet = rg.RecSysType;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecCore(IEnumerable<double> x, IEnumerable<double> y, bool equalVar,
        out double pl, out double pr, out double p2,
        out double dof, out double stderr, out double t,
        out long countX, out long countY,
        out double meanX, out double meanY,
        out double varianceX, out double varianceY)
    {
        Validation.AssertValue(x);
        Validation.AssertValue(y);

        var statsX = new RunningStatistics(x);
        var statsY = new RunningStatistics(y);

        countX = statsX.Count;
        countY = statsY.Count;
        meanX = statsX.Mean;
        meanY = statsY.Mean;
        varianceX = statsX.Variance;
        varianceY = statsY.Variance;

        if (equalVar)
        {
            var count = countX + countY;
            dof = count >= 2 ? count - 2 : NaN;
            stderr = Math.Sqrt(((varianceX * (countX - 1) + varianceY * (countY - 1)) / dof) * ((1.0 / countX) + (1.0 / countY)));
        }
        else
        {
            // Note if either count is less than 2, dof will implicitly become NaN due to division by 0.
            var vcX = varianceX / countX;
            var vcY = varianceY / countY;
            var vcSum = vcX + vcY;

            dof = (vcSum * vcSum) / ((vcX * vcX / (countX - 1)) + (vcY * vcY / (countY - 1)));
            Validation.Assert(double.IsNaN(dof) | (Math.Min(countX - 1, countY - 1) <= dof & dof <= countX + countY - 2));

            stderr = Math.Sqrt(vcSum);
        }

        t = (meanX - meanY) / stderr;
        GetPVals(dof, t, out pl, out pr, out p2);
    }

    private static partial class Execs
    {
        // Exec definitions are generated in a separate partial.

        public static MethodInfo GetMeth(bool isXOpt, bool isYOpt)
        {
            if (isXOpt)
                return isYOpt ? _methExecOpt : _methExecXOpt;
            return isYOpt ? _methExecYOpt : _methExecReq;
        }
    }
}

public sealed partial class TTestPairedGen : TTestBaseGen<TTestPairedFunc>
{
    public static readonly TTestPairedGen Instance = new TTestPairedGen();

    private TTestPairedGen()
    {
    }

    protected override bool NeedsIndexParamCore(BndCallNode call, int slot, int iidx)
    {
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(call.Indices.Length > 0);
        Validation.AssertIndex(slot, call.Args.Length);
        Validation.Assert(call.Traits.IsNested(slot));
        Validation.AssertIndex(iidx, call.Indices.Length);
        return call.Indices[0] != null;
    }

    [Flags]
    private enum MethFlags : byte
    {
        None = 0x00,
        XOpt = 0x01,
        YOpt = 0x02,
        // No need for a selector flag-- whether the call is a selector
        // variant determines which meth map to use.
        Ind = 0x04,
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(call);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        MethodInfo meth;
        if (call.Args.Length == 2)
        {
            var flags = call.Args[0].Type.ItemTypeOrThis.IsOpt ? MethFlags.XOpt : MethFlags.None;
            if (call.Args[1].Type.ItemTypeOrThis.IsOpt)
                flags |= MethFlags.YOpt;
            meth = Execs.GetMeth(flags);
        }
        else
        {
            var flags = call.Args[1].Type.ItemTypeOrThis.IsOpt ? MethFlags.XOpt : MethFlags.None;
            if (call.Args[2].Type.ItemTypeOrThis.IsOpt)
                flags |= MethFlags.YOpt;
            if (call.Indices[0] != null)
                flags |= MethFlags.Ind;

            DType typeItem = call.Args[0].Type.ItemTypeOrThis;
            Type stItem = codeGen.GetSystemType(typeItem);
            meth = Execs.GetMethSel(flags, stItem);
        }

        using var rg = codeGen.CreateRecordGenerator(fn.TypeRes);
        foreach (var fld in fn.FieldsRes)
            rg.LoadFieldAddr(fld.Name);

        codeGen.GenLoadExecCtxAndId(call);
        codeGen.Writer.Call(meth);
        rg.Finish();
        stRet = rg.RecSysType;
        return true;
    }

    private static partial class Execs
    {
        // Exec definitions are generated in a separate partial.

        public static MethodInfo GetMeth(MethFlags flags)
        {
            Validation.Assert((flags & MethFlags.Ind) == 0);
            return (flags switch
            {
                MethFlags.None => _methExecReq,
                MethFlags.XOpt => _methExecXOpt,
                MethFlags.YOpt => _methExecYOpt,
                MethFlags.XOpt | MethFlags.YOpt => _methExecOpt,
                _ => throw Validation.BugExcept()
            }).VerifyValue();
        }

        public static MethodInfo GetMethSel(MethFlags flags, Type stSrcItem)
        {
            Validation.AssertValue(stSrcItem);
            return (flags switch
            {
                MethFlags.None => _methExecReqSel,
                MethFlags.XOpt => _methExecXOptSel,
                MethFlags.YOpt => _methExecYOptSel,
                MethFlags.XOpt | MethFlags.YOpt => _methExecOptSel,

                MethFlags.Ind => _methExecReqSelInd,
                MethFlags.Ind | MethFlags.XOpt => _methExecXOptSelInd,
                MethFlags.Ind | MethFlags.YOpt => _methExecYOptSelInd,
                MethFlags.Ind | MethFlags.XOpt | MethFlags.YOpt => _methExecOptSelInd,
                _ => throw Validation.BugExcept()
            }).VerifyValue()
                .GetGenericMethodDefinition()
                .MakeGenericMethod(stSrcItem);
        }
    }
}
