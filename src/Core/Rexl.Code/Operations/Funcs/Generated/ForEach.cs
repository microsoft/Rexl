// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using O = System.Object;
using IE = IEnumerable<object>;

// REVIEW: How can we implement the general case?
partial class ForEachGen
{
    // We need Func with up to 20 parameters to get the Exec(Ind) methods.
    private delegate TR Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TA, TB, TC, TD, TE, TF, TG, TR>(T0 t0, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, TA ta, TB tb, TC tc, TD td, TE te, TF tf, TG tg);
    private delegate TR Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TA, TB, TC, TD, TE, TF, TG, TH, TR>(T0 t0, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, TA ta, TB tb, TC tc, TD td, TE te, TF tf, TG tg, TH th);
    private delegate TR Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TA, TB, TC, TD, TE, TF, TG, TH, TI, TR>(T0 t0, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, TA ta, TB tb, TC tc, TD td, TE te, TF tf, TG tg, TH th, TI ti);
    private delegate TR Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TA, TB, TC, TD, TE, TF, TG, TH, TI, TJ, TR>(T0 t0, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, TA ta, TB tb, TC tc, TD td, TE te, TF tf, TG tg, TH th, TI ti, TJ tj);

    private static Immutable.Array<MethodInfo> GetExecs()
    {
        var ret = Immutable.Array.CreateBuilder<MethodInfo>(17, init: true);
        ret[1] = new Func<IE, Func<O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[2] = new Func<IE, IE, Func<O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[3] = new Func<IE, IE, IE, Func<O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[4] = new Func<IE, IE, IE, IE, Func<O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[5] = new Func<IE, IE, IE, IE, IE, Func<O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[6] = new Func<IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[7] = new Func<IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[8] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[9] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[10] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[11] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[12] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[13] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[14] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[15] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        ret[16] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(Exec).Method.GetGenericMethodDefinition();
        return ret.ToImmutable();
    }

    private static Immutable.Array<MethodInfo> GetExecInds()
    {
        var ret = Immutable.Array.CreateBuilder<MethodInfo>(17, init: true);
        ret[1] = new Func<IE, Func<long, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[2] = new Func<IE, IE, Func<long, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[3] = new Func<IE, IE, IE, Func<long, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[4] = new Func<IE, IE, IE, IE, Func<long, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[5] = new Func<IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[6] = new Func<IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[7] = new Func<IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[8] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[9] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[10] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[11] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[12] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[13] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[14] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        ret[15] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecInd).Method.GetGenericMethodDefinition();
        return ret.ToImmutable();
    }

    private static Immutable.Array<MethodInfo> GetExecIfs()
    {
        var ret = Immutable.Array.CreateBuilder<MethodInfo>(17, init: true);
        ret[1] = new Func<IE, Func<O, bool>, Func<O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[2] = new Func<IE, IE, Func<O, O, bool>, Func<O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[3] = new Func<IE, IE, IE, Func<O, O, O, bool>, Func<O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[4] = new Func<IE, IE, IE, IE, Func<O, O, O, O, bool>, Func<O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[5] = new Func<IE, IE, IE, IE, IE, Func<O, O, O, O, O, bool>, Func<O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[6] = new Func<IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[7] = new Func<IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[8] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[9] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[10] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[11] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[12] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[13] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[14] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[15] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        ret[16] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIf).Method.GetGenericMethodDefinition();
        return ret.ToImmutable();
    }

    private static Immutable.Array<MethodInfo> GetExecIfInds()
    {
        var ret = Immutable.Array.CreateBuilder<MethodInfo>(17, init: true);
        ret[1] = new Func<IE, Func<long, O, bool>, Func<long, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[2] = new Func<IE, IE, Func<long, O, O, bool>, Func<long, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[3] = new Func<IE, IE, IE, Func<long, O, O, O, bool>, Func<long, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[4] = new Func<IE, IE, IE, IE, Func<long, O, O, O, O, bool>, Func<long, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[5] = new Func<IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[6] = new Func<IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[7] = new Func<IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[8] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[9] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[10] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[11] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[12] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[13] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[14] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        ret[15] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, ExecCtx, int, IE>(ExecIfInd).Method.GetGenericMethodDefinition();
        return ret.ToImmutable();
    }

    private static Immutable.Array<MethodInfo> GetExecWhiles()
    {
        var ret = Immutable.Array.CreateBuilder<MethodInfo>(17, init: true);
        ret[1] = new Func<IE, Func<O, bool>, Func<O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[2] = new Func<IE, IE, Func<O, O, bool>, Func<O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[3] = new Func<IE, IE, IE, Func<O, O, O, bool>, Func<O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[4] = new Func<IE, IE, IE, IE, Func<O, O, O, O, bool>, Func<O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[5] = new Func<IE, IE, IE, IE, IE, Func<O, O, O, O, O, bool>, Func<O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[6] = new Func<IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[7] = new Func<IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[8] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[9] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[10] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[11] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[12] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[13] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[14] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[15] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        ret[16] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhile).Method.GetGenericMethodDefinition();
        return ret.ToImmutable();
    }

    private static Immutable.Array<MethodInfo> GetExecWhileInds()
    {
        var ret = Immutable.Array.CreateBuilder<MethodInfo>(17, init: true);
        ret[1] = new Func<IE, Func<long, O, bool>, Func<long, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[2] = new Func<IE, IE, Func<long, O, O, bool>, Func<long, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[3] = new Func<IE, IE, IE, Func<long, O, O, O, bool>, Func<long, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[4] = new Func<IE, IE, IE, IE, Func<long, O, O, O, O, bool>, Func<long, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[5] = new Func<IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[6] = new Func<IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[7] = new Func<IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[8] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[9] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[10] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[11] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[12] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[13] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[14] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        ret[15] = new Func<IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, IE, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, bool>, Func<long, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O, O>, IE>(ExecWhileInd).Method.GetGenericMethodDefinition();
        return ret.ToImmutable();
    }

    public static IEnumerable<TDst> Exec<TSrc, TDst>(
        IEnumerable<TSrc> src,
        Func<TSrc, TDst> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        if (src == null)
            return null;
        return CodeGenUtil.WrapWithCounter(src, Enumerable.Select(src, fn));
    }
    public static IEnumerable<TDst> ExecInd<TSrc, TDst>(
        IEnumerable<TSrc> src,
        Func<long, TSrc, TDst> fn)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(fn);
        if (src == null)
            return null;
        return CodeGenUtil.WrapWithCounter(src, IterInd(src, fn));
    }
    private static IEnumerable<TDst> IterInd<TSrc, TDst>(
        IEnumerable<TSrc> src,
        Func<long, TSrc, TDst> fn)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(fn);

        long idx = 0;
        foreach (var x in src)
            yield return fn(idx++, x);
    }
    public static IEnumerable<TDst> Exec<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null)
            return null;
        return Iter(s0, s1, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext())
            yield return fn(e0.Current, e1.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<T0, T1, T2, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null)
            return null;
        return Iter(s0, s1, s2, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<T0, T1, T2, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<T0, T1, T2, T3, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null)
            return null;
        return Iter(s0, s1, s2, s3, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<T0, T1, T2, T3, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<T0, T1, T2, T3, T4, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<T0, T1, T2, T3, T4, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<T0, T1, T2, T3, T4, T5, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<T0, T1, T2, T3, T4, T5, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<T0, T1, T2, T3, T4, T5, T6, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<T0, T1, T2, T3, T4, T5, T6, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        using var e7 = s7.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        using var e7 = s7.GetEnumerator();
        using var e8 = s8.GetEnumerator();
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext() && e13.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext() && e13.MoveNext() && e14.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current, e14.Current);
    }
    public static IEnumerable<TDst> Exec<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null || s15 == null)
            return null;
        return Iter(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, fn);
    }
    private static IEnumerable<TDst> Iter<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(s15);
        Validation.AssertValue(fn);
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
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext() && e13.MoveNext() && e14.MoveNext() && e15.MoveNext())
            yield return fn(e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current, e14.Current, e15.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, T1, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null)
            return null;
        return IterInd(s0, s1, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, T1, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<long, T0, T1, T2, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null)
            return null;
        return IterInd(s0, s1, s2, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<long, T0, T1, T2, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<long, T0, T1, T2, T3, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null)
            return null;
        return IterInd(s0, s1, s2, s3, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<long, T0, T1, T2, T3, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<long, T0, T1, T2, T3, T4, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<long, T0, T1, T2, T3, T4, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<long, T0, T1, T2, T3, T4, T5, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<long, T0, T1, T2, T3, T4, T5, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<long, T0, T1, T2, T3, T4, T5, T6, TDst> fn)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValue(fn);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<long, T0, T1, T2, T3, T4, T5, T6, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        using var e7 = s7.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(fn);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        using var e7 = s7.GetEnumerator();
        using var e8 = s8.GetEnumerator();
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(fn);
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
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(fn);
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
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(fn);
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
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(fn);
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
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(fn);
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
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext() && e13.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current);
    }
    public static IEnumerable<TDst> ExecInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> fn)
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
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
            return null;
        return IterInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, fn);
    }
    private static IEnumerable<TDst> IterInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> fn)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(fn);
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
        long idx = 0;
        while (e0.MoveNext() && e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext() && e6.MoveNext() && e7.MoveNext() && e8.MoveNext() && e9.MoveNext() && e10.MoveNext() && e11.MoveNext() && e12.MoveNext() && e13.MoveNext() && e14.MoveNext())
            yield return fn(idx++, e0.Current, e1.Current, e2.Current, e3.Current, e4.Current, e5.Current, e6.Current, e7.Current, e8.Current, e9.Current, e10.Current, e11.Current, e12.Current, e13.Current, e14.Current);
    }
    public static IEnumerable<TDst> ExecIf<T0, TDst>(
        IEnumerable<T0> s0,
        Func<T0, bool> pred,
        Func<T0, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null)
            return null;
        return IterIf(s0, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, TDst>(
        IEnumerable<T0> s0,
        Func<T0, bool> pred,
        Func<T0, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext())
                yield break;
            var v0 = e0.Current;
            if (pred(v0))
                yield return sel(v0);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred,
        Func<T0, T1, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null)
            return null;
        return IterIf(s0, s1, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred,
        Func<T0, T1, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            if (pred(v0, v1))
                yield return sel(v0, v1);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<T0, T1, T2, bool> pred,
        Func<T0, T1, T2, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null)
            return null;
        return IterIf(s0, s1, s2, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<T0, T1, T2, bool> pred,
        Func<T0, T1, T2, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            if (pred(v0, v1, v2))
                yield return sel(v0, v1, v2);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<T0, T1, T2, T3, bool> pred,
        Func<T0, T1, T2, T3, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null)
            return null;
        return IterIf(s0, s1, s2, s3, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<T0, T1, T2, T3, bool> pred,
        Func<T0, T1, T2, T3, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            if (pred(v0, v1, v2, v3))
                yield return sel(v0, v1, v2, v3);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<T0, T1, T2, T3, T4, bool> pred,
        Func<T0, T1, T2, T3, T4, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<T0, T1, T2, T3, T4, bool> pred,
        Func<T0, T1, T2, T3, T4, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            if (pred(v0, v1, v2, v3, v4))
                yield return sel(v0, v1, v2, v3, v4);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<T0, T1, T2, T3, T4, T5, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<T0, T1, T2, T3, T4, T5, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            if (pred(v0, v1, v2, v3, v4, v5))
                yield return sel(v0, v1, v2, v3, v4, v5);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6))
                yield return sel(v0, v1, v2, v3, v4, v5, v6);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext() || !e14.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            var v14 = e14.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14);
        }
    }
    public static IEnumerable<TDst> ExecIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null || s15 == null)
            return null;
        return IterIf(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIf<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(s15);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext() || !e14.MoveNext() || !e15.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            var v14 = e14.Current;
            var v15 = e15.Current;
            if (pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15))
                yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, TDst>(
        IEnumerable<T0> s0,
        Func<long, T0, bool> pred,
        Func<long, T0, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null)
            return null;
        return IterIfInd(s0, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, TDst>(
        IEnumerable<T0> s0,
        Func<long, T0, bool> pred,
        Func<long, T0, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext())
                yield break;
            var v0 = e0.Current;
            if (pred(idx, v0))
                yield return sel(idx, v0);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, T1, bool> pred,
        Func<long, T0, T1, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null)
            return null;
        return IterIfInd(s0, s1, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, T1, bool> pred,
        Func<long, T0, T1, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            if (pred(idx, v0, v1))
                yield return sel(idx, v0, v1);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<long, T0, T1, T2, bool> pred,
        Func<long, T0, T1, T2, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null)
            return null;
        return IterIfInd(s0, s1, s2, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<long, T0, T1, T2, bool> pred,
        Func<long, T0, T1, T2, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            if (pred(idx, v0, v1, v2))
                yield return sel(idx, v0, v1, v2);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<long, T0, T1, T2, T3, bool> pred,
        Func<long, T0, T1, T2, T3, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<long, T0, T1, T2, T3, bool> pred,
        Func<long, T0, T1, T2, T3, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            if (pred(idx, v0, v1, v2, v3))
                yield return sel(idx, v0, v1, v2, v3);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<long, T0, T1, T2, T3, T4, bool> pred,
        Func<long, T0, T1, T2, T3, T4, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<long, T0, T1, T2, T3, T4, bool> pred,
        Func<long, T0, T1, T2, T3, T4, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            if (pred(idx, v0, v1, v2, v3, v4))
                yield return sel(idx, v0, v1, v2, v3, v4);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<long, T0, T1, T2, T3, T4, T5, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<long, T0, T1, T2, T3, T4, T5, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5))
                yield return sel(idx, v0, v1, v2, v3, v4, v5);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<long, T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<long, T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13);
        }
    }
    public static IEnumerable<TDst> ExecIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel,
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
            return null;
        return IterIfInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, pred, sel, ctx, id);
    }
    private static IEnumerable<TDst> IterIfInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel,
        ExecCtx ctx, int id)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        Validation.AssertValue(ctx);
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
            ctx.Ping(id);
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext() || !e14.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            var v14 = e14.Current;
            if (pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14))
                yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, TDst>(
        IEnumerable<T0> s0,
        Func<T0, bool> pred,
        Func<T0, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null)
            return null;
        return IterWhile(s0, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, TDst>(
        IEnumerable<T0> s0,
        Func<T0, bool> pred,
        Func<T0, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext())
                yield break;
            var v0 = e0.Current;
            if (!pred(v0))
                yield break;
            yield return sel(v0);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred,
        Func<T0, T1, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null)
            return null;
        return IterWhile(s0, s1, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<T0, T1, bool> pred,
        Func<T0, T1, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext() || !e1.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            if (!pred(v0, v1))
                yield break;
            yield return sel(v0, v1);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<T0, T1, T2, bool> pred,
        Func<T0, T1, T2, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null)
            return null;
        return IterWhile(s0, s1, s2, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<T0, T1, T2, bool> pred,
        Func<T0, T1, T2, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            if (!pred(v0, v1, v2))
                yield break;
            yield return sel(v0, v1, v2);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<T0, T1, T2, T3, bool> pred,
        Func<T0, T1, T2, T3, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<T0, T1, T2, T3, bool> pred,
        Func<T0, T1, T2, T3, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            if (!pred(v0, v1, v2, v3))
                yield break;
            yield return sel(v0, v1, v2, v3);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<T0, T1, T2, T3, T4, bool> pred,
        Func<T0, T1, T2, T3, T4, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<T0, T1, T2, T3, T4, bool> pred,
        Func<T0, T1, T2, T3, T4, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            if (!pred(v0, v1, v2, v3, v4))
                yield break;
            yield return sel(v0, v1, v2, v3, v4);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<T0, T1, T2, T3, T4, T5, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<T0, T1, T2, T3, T4, T5, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            if (!pred(v0, v1, v2, v3, v4, v5))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        for (; ; )
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValueOrNull(s7);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext() || !e14.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            var v14 = e14.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14);
        }
    }
    public static IEnumerable<TDst> ExecWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null || s15 == null)
            return null;
        return IterWhile(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, pred, sel);
    }
    private static IEnumerable<TDst> IterWhile<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14, IEnumerable<T15> s15,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, bool> pred,
        Func<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(s15);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext() || !e14.MoveNext() || !e15.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            var v14 = e14.Current;
            var v15 = e15.Current;
            if (!pred(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15))
                yield break;
            yield return sel(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, TDst>(
        IEnumerable<T0> s0,
        Func<long, T0, bool> pred,
        Func<long, T0, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null)
            return null;
        return IterWhileInd(s0, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, TDst>(
        IEnumerable<T0> s0,
        Func<long, T0, bool> pred,
        Func<long, T0, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext())
                yield break;
            var v0 = e0.Current;
            if (!pred(idx, v0))
                yield break;
            yield return sel(idx, v0);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, T1, bool> pred,
        Func<long, T0, T1, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null)
            return null;
        return IterWhileInd(s0, s1, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1,
        Func<long, T0, T1, bool> pred,
        Func<long, T0, T1, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext() || !e1.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            if (!pred(idx, v0, v1))
                yield break;
            yield return sel(idx, v0, v1);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<long, T0, T1, T2, bool> pred,
        Func<long, T0, T1, T2, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null)
            return null;
        return IterWhileInd(s0, s1, s2, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2,
        Func<long, T0, T1, T2, bool> pred,
        Func<long, T0, T1, T2, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            if (!pred(idx, v0, v1, v2))
                yield break;
            yield return sel(idx, v0, v1, v2);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<long, T0, T1, T2, T3, bool> pred,
        Func<long, T0, T1, T2, T3, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3,
        Func<long, T0, T1, T2, T3, bool> pred,
        Func<long, T0, T1, T2, T3, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            if (!pred(idx, v0, v1, v2, v3))
                yield break;
            yield return sel(idx, v0, v1, v2, v3);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<long, T0, T1, T2, T3, T4, bool> pred,
        Func<long, T0, T1, T2, T3, T4, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4,
        Func<long, T0, T1, T2, T3, T4, bool> pred,
        Func<long, T0, T1, T2, T3, T4, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            if (!pred(idx, v0, v1, v2, v3, v4))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<long, T0, T1, T2, T3, T4, T5, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5,
        Func<long, T0, T1, T2, T3, T4, T5, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<long, T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6,
        Func<long, T0, T1, T2, T3, T4, T5, T6, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        using var e0 = s0.GetEnumerator();
        using var e1 = s1.GetEnumerator();
        using var e2 = s2.GetEnumerator();
        using var e3 = s3.GetEnumerator();
        using var e4 = s4.GetEnumerator();
        using var e5 = s5.GetEnumerator();
        using var e6 = s6.GetEnumerator();
        for (long idx = 0; ; idx++)
        {
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        Validation.AssertValueOrNull(s4);
        Validation.AssertValueOrNull(s5);
        Validation.AssertValueOrNull(s6);
        Validation.AssertValueOrNull(s7);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13);
        }
    }
    public static IEnumerable<TDst> ExecWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel)
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
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
        if (s0 == null || s1 == null || s2 == null || s3 == null || s4 == null || s5 == null || s6 == null || s7 == null || s8 == null || s9 == null || s10 == null || s11 == null || s12 == null || s13 == null || s14 == null)
            return null;
        return IterWhileInd(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, pred, sel);
    }
    private static IEnumerable<TDst> IterWhileInd<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst>(
        IEnumerable<T0> s0, IEnumerable<T1> s1, IEnumerable<T2> s2, IEnumerable<T3> s3, IEnumerable<T4> s4, IEnumerable<T5> s5, IEnumerable<T6> s6, IEnumerable<T7> s7, IEnumerable<T8> s8, IEnumerable<T9> s9, IEnumerable<T10> s10, IEnumerable<T11> s11, IEnumerable<T12> s12, IEnumerable<T13> s13, IEnumerable<T14> s14,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, bool> pred,
        Func<long, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TDst> sel)
    {
        Validation.AssertValue(s0);
        Validation.AssertValue(s1);
        Validation.AssertValue(s2);
        Validation.AssertValue(s3);
        Validation.AssertValue(s4);
        Validation.AssertValue(s5);
        Validation.AssertValue(s6);
        Validation.AssertValue(s7);
        Validation.AssertValue(s8);
        Validation.AssertValue(s9);
        Validation.AssertValue(s10);
        Validation.AssertValue(s11);
        Validation.AssertValue(s12);
        Validation.AssertValue(s13);
        Validation.AssertValue(s14);
        Validation.AssertValue(pred);
        Validation.AssertValue(sel);
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
            if (!e0.MoveNext() || !e1.MoveNext() || !e2.MoveNext() || !e3.MoveNext() || !e4.MoveNext() || !e5.MoveNext() || !e6.MoveNext() || !e7.MoveNext() || !e8.MoveNext() || !e9.MoveNext() || !e10.MoveNext() || !e11.MoveNext() || !e12.MoveNext() || !e13.MoveNext() || !e14.MoveNext())
                yield break;
            var v0 = e0.Current;
            var v1 = e1.Current;
            var v2 = e2.Current;
            var v3 = e3.Current;
            var v4 = e4.Current;
            var v5 = e5.Current;
            var v6 = e6.Current;
            var v7 = e7.Current;
            var v8 = e8.Current;
            var v9 = e9.Current;
            var v10 = e10.Current;
            var v11 = e11.Current;
            var v12 = e12.Current;
            var v13 = e13.Current;
            var v14 = e14.Current;
            if (!pred(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14))
                yield break;
            yield return sel(idx, v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14);
        }
    }
}
