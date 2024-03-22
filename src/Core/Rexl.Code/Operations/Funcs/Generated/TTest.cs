// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Rexl.Private;


namespace Microsoft.Rexl.Code;

partial class TTestOneSampleGen
{
    partial class Execs
    {
        private static readonly MethodInfo _methExecReq = typeof(Execs).GetMethod(nameof(ExecReq), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecOpt = typeof(Execs).GetMethod(nameof(ExecOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();

        private static void ExecReq(IEnumerable<double> x, double popMean,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            if (x == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(CodeGenUtil.EnumerableToPingingCore(x, ctx, id), popMean,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);
        }
        private static void ExecOpt(IEnumerable<double?> x, double popMean,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            if (x == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(FilterNulls(x, ctx, id), popMean,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);
        }
    }
}

partial class TTestTwoSampleGen
{
    partial class Execs
    {
        private static readonly MethodInfo _methExecReq = typeof(Execs).GetMethod(nameof(ExecReq), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecXOpt = typeof(Execs).GetMethod(nameof(ExecXOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecYOpt = typeof(Execs).GetMethod(nameof(ExecYOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecOpt = typeof(Execs).GetMethod(nameof(ExecOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();

        private static void ExecReq(IEnumerable<double> x, IEnumerable<double> y, bool equalVar,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long countX, out long countY,
            out double meanX, out double meanY,
            out double varianceX, out double varianceY,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                countX = 0; countY = 0;
                meanX = NaN; meanY = NaN;
                varianceX = NaN; varianceY = NaN;
                return;
            }

            ExecCore(CodeGenUtil.EnumerableToPingingCore(x, ctx, id), CodeGenUtil.EnumerableToPingingCore(y, ctx, id + 1), equalVar,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out countX, out countY,
                out meanX, out meanY,
                out varianceX, out varianceY);
        }
        private static void ExecXOpt(IEnumerable<double?> x, IEnumerable<double> y, bool equalVar,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long countX, out long countY,
            out double meanX, out double meanY,
            out double varianceX, out double varianceY,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                countX = 0; countY = 0;
                meanX = NaN; meanY = NaN;
                varianceX = NaN; varianceY = NaN;
                return;
            }

            ExecCore(FilterNulls(x, ctx, id), CodeGenUtil.EnumerableToPingingCore(y, ctx, id + 1), equalVar,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out countX, out countY,
                out meanX, out meanY,
                out varianceX, out varianceY);
        }
        private static void ExecYOpt(IEnumerable<double> x, IEnumerable<double?> y, bool equalVar,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long countX, out long countY,
            out double meanX, out double meanY,
            out double varianceX, out double varianceY,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                countX = 0; countY = 0;
                meanX = NaN; meanY = NaN;
                varianceX = NaN; varianceY = NaN;
                return;
            }

            ExecCore(CodeGenUtil.EnumerableToPingingCore(x, ctx, id), FilterNulls(y, ctx, id + 1), equalVar,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out countX, out countY,
                out meanX, out meanY,
                out varianceX, out varianceY);
        }
        private static void ExecOpt(IEnumerable<double?> x, IEnumerable<double?> y, bool equalVar,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long countX, out long countY,
            out double meanX, out double meanY,
            out double varianceX, out double varianceY,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                countX = 0; countY = 0;
                meanX = NaN; meanY = NaN;
                varianceX = NaN; varianceY = NaN;
                return;
            }

            ExecCore(FilterNulls(x, ctx, id), FilterNulls(y, ctx, id + 1), equalVar,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out countX, out countY,
                out meanX, out meanY,
                out varianceX, out varianceY);
        }
    }
}

partial class TTestPairedGen
{
    partial class Execs
    {
        private static readonly MethodInfo _methExecReq = typeof(Execs).GetMethod(nameof(ExecReq), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecXOpt = typeof(Execs).GetMethod(nameof(ExecXOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecYOpt = typeof(Execs).GetMethod(nameof(ExecYOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecOpt = typeof(Execs).GetMethod(nameof(ExecOpt), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();

        private static readonly MethodInfo _methExecReqSel = typeof(Execs).GetMethod(nameof(ExecReqSel), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecXOptSel = typeof(Execs).GetMethod(nameof(ExecXOptSel), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecYOptSel = typeof(Execs).GetMethod(nameof(ExecYOptSel), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecOptSel = typeof(Execs).GetMethod(nameof(ExecOptSel), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();

        private static readonly MethodInfo _methExecReqSelInd = typeof(Execs).GetMethod(nameof(ExecReqSelInd), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecXOptSelInd = typeof(Execs).GetMethod(nameof(ExecXOptSelInd), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecYOptSelInd = typeof(Execs).GetMethod(nameof(ExecYOptSelInd), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();
        private static readonly MethodInfo _methExecOptSelInd = typeof(Execs).GetMethod(nameof(ExecOptSelInd), BindingFlags.NonPublic | BindingFlags.Static).VerifyValue();

        private static void ExecReq(IEnumerable<double> x, IEnumerable<double> y,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(x, y, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<double> x, IEnumerable<double> y,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(x);
                Validation.AssertValue(y);

                using var atorX = x.GetEnumerator();
                using var atorY = y.GetEnumerator();
                while (atorX.MoveNext() && atorY.MoveNext())
                {
                    ctx.Ping(id);
                    yield return atorX.Current - atorY.Current;
                }
            }
        }
        private static void ExecXOpt(IEnumerable<double?> x, IEnumerable<double> y,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(x, y, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<double?> x, IEnumerable<double> y,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(x);
                Validation.AssertValue(y);

                using var atorX = x.GetEnumerator();
                using var atorY = y.GetEnumerator();
                while (atorX.MoveNext() && atorY.MoveNext())
                {
                    ctx.Ping(id);
                    var xCur = atorX.Current;
                    var yCur = atorY.Current;
                    if (xCur != null)
                        yield return xCur.GetValueOrDefault() - yCur;
                }
            }
        }
        private static void ExecYOpt(IEnumerable<double> x, IEnumerable<double?> y,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(x, y, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<double> x, IEnumerable<double?> y,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(x);
                Validation.AssertValue(y);

                using var atorX = x.GetEnumerator();
                using var atorY = y.GetEnumerator();
                while (atorX.MoveNext() && atorY.MoveNext())
                {
                    ctx.Ping(id);
                    var xCur = atorX.Current;
                    var yCur = atorY.Current;
                    if (yCur != null)
                        yield return xCur - yCur.GetValueOrDefault();
                }
            }
        }
        private static void ExecOpt(IEnumerable<double?> x, IEnumerable<double?> y,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(x);
            Validation.AssertValueOrNull(y);

            if (x == null || y == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(x, y, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<double?> x, IEnumerable<double?> y,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(x);
                Validation.AssertValue(y);

                using var atorX = x.GetEnumerator();
                using var atorY = y.GetEnumerator();
                while (atorX.MoveNext() && atorY.MoveNext())
                {
                    ctx.Ping(id);
                    var xCur = atorX.Current;
                    var yCur = atorY.Current;
                    if (xCur != null && yCur != null)
                        yield return xCur.GetValueOrDefault() - yCur.GetValueOrDefault();
                }
            }
        }
        private static void ExecReqSel<T>(IEnumerable<T> src, Func<T, double> fnX, Func<T, double> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<T, double> fnX, Func<T, double> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                foreach (var item in src)
                {
                    ctx.Ping(id);
                    yield return fnX(item) - fnY(item);
                }
            }
        }
        private static void ExecXOptSel<T>(IEnumerable<T> src, Func<T, double?> fnX, Func<T, double> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<T, double?> fnX, Func<T, double> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                foreach (var item in src)
                {
                    ctx.Ping(id);
                    var xCur = fnX(item);
                    if (xCur == null)
                        continue;
                    var yCur = fnY(item);
                    yield return xCur.GetValueOrDefault() - yCur;
                }
            }
        }
        private static void ExecYOptSel<T>(IEnumerable<T> src, Func<T, double> fnX, Func<T, double?> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<T, double> fnX, Func<T, double?> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                foreach (var item in src)
                {
                    ctx.Ping(id);
                    var yCur = fnY(item);
                    if (yCur == null)
                        continue;
                    var xCur = fnX(item);
                    yield return xCur - yCur.GetValueOrDefault();
                }
            }
        }
        private static void ExecOptSel<T>(IEnumerable<T> src, Func<T, double?> fnX, Func<T, double?> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<T, double?> fnX, Func<T, double?> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                foreach (var item in src)
                {
                    ctx.Ping(id);
                    var xCur = fnX(item);
                    if (xCur == null)
                        continue;
                    var yCur = fnY(item);
                    if (yCur != null)
                        yield return xCur.GetValueOrDefault() - yCur.GetValueOrDefault();
                }
            }
        }
        private static void ExecReqSelInd<T>(IEnumerable<T> src, Func<long, T, double> fnX, Func<long, T, double> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<long, T, double> fnX, Func<long, T, double> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                long i = -1;
                foreach (var item in src)
                {
                    ctx.Ping(id);
                    ++i;
                    yield return fnX(i, item) - fnY(i, item);
                }
            }
        }
        private static void ExecXOptSelInd<T>(IEnumerable<T> src, Func<long, T, double?> fnX, Func<long, T, double> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<long, T, double?> fnX, Func<long, T, double> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                long i = -1;
                foreach (var item in src)
                {
                    ctx.Ping(id);
                    ++i;
                    var xCur = fnX(i, item);
                    if (xCur == null)
                        continue;
                    var yCur = fnY(i, item);
                    yield return xCur.GetValueOrDefault() - yCur;
                }
            }
        }
        private static void ExecYOptSelInd<T>(IEnumerable<T> src, Func<long, T, double> fnX, Func<long, T, double?> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<long, T, double> fnX, Func<long, T, double?> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                long i = -1;
                foreach (var item in src)
                {
                    ctx.Ping(id);
                    ++i;
                    var yCur = fnY(i, item);
                    if (yCur == null)
                        continue;
                    var xCur = fnX(i, item);
                    yield return xCur - yCur.GetValueOrDefault();
                }
            }
        }
        private static void ExecOptSelInd<T>(IEnumerable<T> src, Func<long, T, double?> fnX, Func<long, T, double?> fnY,
            out double pl, out double pr, out double p2,
            out double dof, out double stderr, out double t,
            out long count, out double mean, out double variance,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(src);
            Validation.AssertValue(fnX);
            Validation.AssertValue(fnY);

            if (src == null)
            {
                pl = NaN; pr = NaN; p2 = NaN;
                dof = NaN; stderr = NaN; t = NaN;
                count = 0; mean = NaN; variance = NaN;
                return;
            }

            ExecSingleCore(Diffs(src, fnX, fnY, ctx, id), popMean: 0,
                out pl, out pr, out p2,
                out dof, out stderr, out t,
                out count, out mean, out variance);

            static IEnumerable<double> Diffs(IEnumerable<T> src, Func<long, T, double?> fnX, Func<long, T, double?> fnY,
                ExecCtx ctx, int id)
            {
                Validation.AssertValue(src);
                Validation.AssertValue(fnX);
                Validation.AssertValue(fnY);

                long i = -1;
                foreach (var item in src)
                {
                    ctx.Ping(id);
                    ++i;
                    var xCur = fnX(i, item);
                    if (xCur == null)
                        continue;
                    var yCur = fnY(i, item);
                    if (yCur != null)
                        yield return xCur.GetValueOrDefault() - yCur.GetValueOrDefault();
                }
            }
        }
    }
}
