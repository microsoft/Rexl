// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

// REVIEW: How can we implement the general case?
partial class ChainMapGen
{
    private Immutable.Array<MethodInfo> GetExecs()
    {
        const string name = nameof(Execs.Exec);

        // Number of extra arguments besides the sequences.
        // This also handles the single sequence, no selector case.
        const int extra = 3;

        var ret = Immutable.Array<MethodInfo>.CreateBuilder(17, init: true);
        foreach (var meth in typeof(Execs).GetMethods())
        {
            if (meth.Name != name)
                continue;
            var prms = meth.GetParameters();
            Validation.AssertIndex(prms.Length - extra, ret.Count);
            ret[prms.Length - extra] = meth;
        }
        return ret.ToImmutable();
    }

    private Immutable.Array<MethodInfo> GetExecInds()
    {
        const string name = nameof(ExecInds.ExecInd);
        const int extra = 3; // Number of extra arguments besides the sequences.

        var ret = Immutable.Array<MethodInfo>.CreateBuilder(17, init: true);
        foreach (var meth in typeof(ExecInds).GetMethods())
        {
            if (meth.Name != name)
                continue;
            var prms = meth.GetParameters();
            Validation.AssertIndex(prms.Length - extra, ret.Count);
            ret[prms.Length - extra] = meth;
        }
        return ret.ToImmutable();
    }

    private static partial class Execs
    {
        public static IEnumerable<TDst> Exec<T0, TDst>(
            IEnumerable<T0> s0,
            Func<T0, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                var seq = fn(e0.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1,
            Func<T0, T1, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
            Func<T0, T1, T2, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
            Func<T0, T1, T2, T3, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
            Func<T0, T1, T2, T3, T4, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
            Func<T0, T1, T2, T3, T4, T5, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
            Func<T0, T1, T2, T3, T4, T5, T6, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValueOrNull(s13);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            using var e13 = s13.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                ctx.Ping(id); if (!e13.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValueOrNull(s13);
            Validation.AssertValueOrNull(s14);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            using var e13 = s13.GetEnumerator();
            using var e14 = s14.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                ctx.Ping(id); if (!e13.MoveNext()) yield break;
                ctx.Ping(id); if (!e14.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current, e14.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
            Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValueOrNull(s13);
            Validation.AssertValueOrNull(s14);
            Validation.AssertValueOrNull(s15);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null || s15 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            using var e13 = s13.GetEnumerator();
            using var e14 = s14.GetEnumerator();
            using var e15 = s15.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                ctx.Ping(id); if (!e13.MoveNext()) yield break;
                ctx.Ping(id); if (!e14.MoveNext()) yield break;
                ctx.Ping(id); if (!e15.MoveNext()) yield break;
                var seq = fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current, e14.Current, e15.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

    }

    private static class ExecInds
    {
        public static IEnumerable<TDst> ExecInd<T0, TDst>(
            IEnumerable<T0> s0,
            Func<long, T0, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                var seq = fn(idx, e0.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1,
            Func<long, T0, T1, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
            Func<long, T0, T1, T2, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
            Func<long, T0, T1, T2, T3, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
            Func<long, T0, T1, T2, T3, T4, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
            Func<long, T0, T1, T2, T3, T4, T5, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
            Func<long, T0, T1, T2, T3, T4, T5, T6, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValueOrNull(s13);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            using var e13 = s13.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                ctx.Ping(id); if (!e13.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

        public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
            IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
            Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, IEnumerable<TDst>> fn,
            ExecCtx ctx, int id)
        {
            Validation.AssertValueOrNull(s0);
            Validation.AssertValueOrNull(s1);
            Validation.AssertValueOrNull(s2);
            Validation.AssertValueOrNull(s3);
            Validation.AssertValueOrNull(s4);
            Validation.AssertValueOrNull(s5);
            Validation.AssertValueOrNull(s6);
            Validation.AssertValueOrNull(s7);
            Validation.AssertValueOrNull(s8);
            Validation.AssertValueOrNull(s9);
            Validation.AssertValueOrNull(s10);
            Validation.AssertValueOrNull(s11);
            Validation.AssertValueOrNull(s12);
            Validation.AssertValueOrNull(s13);
            Validation.AssertValueOrNull(s14);
            Validation.AssertValue(fn);
            Validation.AssertValue(ctx);

            if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
                yield break;

            using var e0 = s0.GetEnumerator();
            using var e1 = s1.GetEnumerator();
            using var e2 = s2.GetEnumerator();
            using var e3 = s3.GetEnumerator();
            using var e4 = s4.GetEnumerator();
            using var e5 = s5.GetEnumerator();
            using var e6 = s6.GetEnumerator();
            using var e7 = s7.GetEnumerator();
            using var e8 = s8.GetEnumerator();
            using var e9 = s9.GetEnumerator();
            using var e10 = s10.GetEnumerator();
            using var e11 = s11.GetEnumerator();
            using var e12 = s12.GetEnumerator();
            using var e13 = s13.GetEnumerator();
            using var e14 = s14.GetEnumerator();
            for (long idx = 0; ; idx++)
            {
                ctx.Ping(id); if (!e0.MoveNext()) yield break;
                ctx.Ping(id); if (!e1.MoveNext()) yield break;
                ctx.Ping(id); if (!e2.MoveNext()) yield break;
                ctx.Ping(id); if (!e3.MoveNext()) yield break;
                ctx.Ping(id); if (!e4.MoveNext()) yield break;
                ctx.Ping(id); if (!e5.MoveNext()) yield break;
                ctx.Ping(id); if (!e6.MoveNext()) yield break;
                ctx.Ping(id); if (!e7.MoveNext()) yield break;
                ctx.Ping(id); if (!e8.MoveNext()) yield break;
                ctx.Ping(id); if (!e9.MoveNext()) yield break;
                ctx.Ping(id); if (!e10.MoveNext()) yield break;
                ctx.Ping(id); if (!e11.MoveNext()) yield break;
                ctx.Ping(id); if (!e12.MoveNext()) yield break;
                ctx.Ping(id); if (!e13.MoveNext()) yield break;
                ctx.Ping(id); if (!e14.MoveNext()) yield break;
                var seq = fn(idx, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current, e14.Current);
                if (seq == null)
                    continue;
                foreach (var item in seq)
                    yield return item;
            }
        }

    }
}
