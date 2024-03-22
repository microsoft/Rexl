// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

using BL = System.Boolean;
using I1 = System.SByte;
using I2 = System.Int16;
using I4 = System.Int32;
using I8 = System.Int64;
using IA = System.Numerics.BigInteger;
using IE = IEnumerable<object>;
using Integer = System.Numerics.BigInteger;
using O = System.Object;
using R4 = System.Single;
using R8 = System.Double;
using U1 = System.Byte;
using U2 = System.UInt16;
using U4 = System.UInt32;
using U8 = System.UInt64;

public sealed class AbsGen : GetMethGen<AbsFunc>
{
    public static readonly AbsGen Instance = new AbsGen();

    private readonly ReadOnly.Dictionary<DKind, MethodInfo> _meths;

    private AbsGen()
    {
        _meths = new Dictionary<DKind, MethodInfo>()
        {
            { DKind.R8, new Func<double, double>(Math.Abs).Method },
            { DKind.R4, new Func<float, float>(Math.Abs).Method },
            { DKind.IA, new Func<Integer, Integer>(Integer.Abs).Method },
            { DKind.I8, new Func<long, long>(AbsFunc.Exec).Method },
            { DKind.I4, new Func<int, int>(AbsFunc.Exec).Method },
            { DKind.I2, new Func<short, short>(AbsFunc.Exec).Method },
            { DKind.I1, new Func<sbyte, sbyte>(AbsFunc.Exec).Method },
        };
    }

    protected override bool TryGetMeth(ICodeGen codeGen, BndCallNode call, out MethodInfo meth)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        DType type = call.Type;
        return _meths.TryGetValue(type.RootKind, out meth);
    }
}

public sealed class R8Gen : GetMethGen<R8Func>
{
    public static readonly R8Gen Instance = new R8Gen();

    private R8Gen()
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

public sealed partial class SumBaseGen : RexlOperationGenerator<SumBaseFunc>
{
    public static readonly SumBaseGen Instance = new SumBaseGen();

    private readonly ReadOnly.Dictionary<uint, MethodInfo> _methMap;

    private SumBaseGen()
    {
        _methMap = BuildMethMap();
    }

    [Flags]
    private enum Variation : byte
    {
        Opt = 0x01,
        Kahan = 0x02,
        Mean = 0x04,
        Ind = 0x08,
    }

    /// <summary>
    /// Combines the args into a unique uint-valued key.
    /// </summary>
    private static uint MakeKey(DKind kindDst, DKind kindSrc, Variation flags, int cscp)
    {
        Validation.Assert((uint)kindDst < 0x100);
        Validation.Assert((uint)kindSrc < 0x100);
        Validation.Assert((uint)cscp < 0x100);
        return ((uint)kindDst << 0) | ((uint)kindSrc << 8) | ((uint)cscp << 16) | ((uint)(byte)flags << 24);
    }

    /// <summary>
    /// Build a dictionary from "key" to MethodInfo for supported sum types.
    /// </summary>
    private static ReadOnly.Dictionary<uint, MethodInfo> BuildMethMap()
    {
        // First create a system type to DType map.
        var typeMap = new Dictionary<Type, DKind>();

        void AddType<T>(DKind kind)
        {
            typeMap.Add(typeof(T), kind);
        }

        AddType<R8>(DKind.R8);
        AddType<R4>(DKind.R4);
        AddType<I8>(DKind.I8);
        AddType<I4>(DKind.I4);
        AddType<I2>(DKind.I2);
        AddType<I1>(DKind.I1);
        AddType<U8>(DKind.U8);
        AddType<U4>(DKind.U4);
        AddType<U2>(DKind.U2);
        AddType<U1>(DKind.U1);
        AddType<BL>(DKind.Bit);
        AddType<IA>(DKind.IA);

        // Now create the method info map.
        var map = new Dictionary<uint, MethodInfo>();

        // Adds the given (key, meth) to the map.
        void AddMeth(uint key, MethodInfo meth)
        {
            Validation.Assert(!map.ContainsKey(key));
            Validation.AssertValue(meth);
            map.Add(key, meth);
        }

        // Adds the method info from the given delegate to the map. This is used for the
        // non-selector cases with src and dst types different, eg summing i4 to produce i8.
        void AddFunc<TDst, TSrc>(CodeGenUtil.FuncOut<IEnumerable<TSrc>, ExecCtx, int, long, TDst> fn)
        {
            Validation.Assert(typeMap.ContainsKey(typeof(TDst)));
            Validation.Assert(typeMap.ContainsKey(typeof(TSrc)));
            var kindDst = typeMap[typeof(TDst)];
            var kindSrc = typeMap[typeof(TSrc)];
            AddMeth(MakeKey(kindDst, kindSrc, flags: 0, cscp: 0), fn.Method);
        }

        // The nullable/opt version of AddFunc.
        void AddFopt<TDst, TSrc>(CodeGenUtil.FuncOut<IEnumerable<TSrc?>, ExecCtx, int, long, TDst> fn)
            where TDst : struct
            where TSrc : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(TDst)));
            Validation.Assert(typeMap.ContainsKey(typeof(TSrc)));
            var kindDst = typeMap[typeof(TDst)];
            var kindSrc = typeMap[typeof(TSrc)];
            AddMeth(MakeKey(kindDst, kindSrc, flags: Variation.Opt, cscp: 0), fn.Method);
        }

        // Adds the various delegate methods to the map. The first, fn0, is for when the src and dst
        // item types are the same and there is no selector delegate. The rest are for selector delegate
        // cases of various arities. The return type from the selector always matches the dst type.
        void AddSame<T>(
            CodeGenUtil.FuncOut<IEnumerable<T>, ExecCtx, int, long, T> fn0,
            CodeGenUtil.FuncOut<IE, Func<O, T>, ExecCtx, int, long, T> fn1,
            CodeGenUtil.FuncOut<IE, Func<long, O, T>, ExecCtx, int, long, T> fn2)
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, kind, flags: 0, cscp: 0), fn0.Method);
            AddMeth(MakeKey(kind, kind, flags: 0, cscp: 1), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, kind, flags: Variation.Ind, cscp: 1), fn2.Method.GetGenericMethodDefinition());
        }

        // The nullable/opt version of AddSame.
        void AddSopt<T>(
            CodeGenUtil.FuncOut<IEnumerable<T?>, ExecCtx, int, long, T> fn0,
            CodeGenUtil.FuncOut<IE, Func<O, T?>, ExecCtx, int, long, T> fn1,
            CodeGenUtil.FuncOut<IE, Func<long, O, T?>, ExecCtx, int, long, T> fn2)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, kind, flags: Variation.Opt, cscp: 0), fn0.Method);
            AddMeth(MakeKey(kind, kind, flags: Variation.Opt, cscp: 1), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, kind, flags: Variation.Opt | Variation.Ind, cscp: 1), fn2.Method.GetGenericMethodDefinition());
        }

        AddSame<R8>(ExecS, Exec, ExecInd);
        AddSame<I8>(ExecS, Exec, ExecInd);
        AddSame<U8>(ExecS, Exec, ExecInd);
        AddSame<IA>(ExecS, Exec, ExecInd);

        AddSopt<R8>(ExecS, Exec, ExecInd);
        AddSopt<I8>(ExecS, Exec, ExecInd);
        AddSopt<U8>(ExecS, Exec, ExecInd);
        AddSopt<IA>(ExecS, Exec, ExecInd);

        AddFunc<R8, R4>(ExecSum);
        AddFunc<I8, I4>(ExecSum);
        AddFunc<I8, I2>(ExecSum);
        AddFunc<I8, I1>(ExecSum);
        AddFunc<I8, U4>(ExecSum);
        AddFunc<I8, U2>(ExecSum);
        AddFunc<I8, U1>(ExecSum);
        AddFunc<I8, BL>(ExecSum);
        AddFunc<IA, I8>(ExecBig);
        AddFunc<IA, I4>(ExecBig);
        AddFunc<IA, I2>(ExecBig);
        AddFunc<IA, I1>(ExecBig);
        AddFunc<IA, U8>(ExecBig);
        AddFunc<IA, U4>(ExecBig);
        AddFunc<IA, U2>(ExecBig);
        AddFunc<IA, U1>(ExecBig);
        AddFunc<IA, BL>(ExecBig);

        AddFopt<R8, R4>(ExecSum);
        AddFopt<I8, I4>(ExecSum);
        AddFopt<I8, I2>(ExecSum);
        AddFopt<I8, I1>(ExecSum);
        AddFopt<I8, U4>(ExecSum);
        AddFopt<I8, U2>(ExecSum);
        AddFopt<I8, U1>(ExecSum);
        AddFopt<I8, BL>(ExecSum);
        AddFopt<IA, I8>(ExecBig);
        AddFopt<IA, I4>(ExecBig);
        AddFopt<IA, I2>(ExecBig);
        AddFopt<IA, I1>(ExecBig);
        AddFopt<IA, U8>(ExecBig);
        AddFopt<IA, U4>(ExecBig);
        AddFopt<IA, U2>(ExecBig);
        AddFopt<IA, U1>(ExecBig);
        AddFopt<IA, BL>(ExecBig);

        void AddKahn<T>(CodeGenUtil.FuncOut<IEnumerable<T>, ExecCtx, int, long, double> fn0)
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(DKind.R8, kind, flags: Variation.Kahan, cscp: 0), fn0.Method);
        }

        void AddKopt<T>(CodeGenUtil.FuncOut<IEnumerable<T?>, ExecCtx, int, long, double> fn0)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(DKind.R8, kind, flags: Variation.Kahan | Variation.Opt, cscp: 0), fn0.Method);
        }

        AddKahn<R8>(ExecK);
        AddKahn<R4>(ExecK);
        AddKahn<IA>(ExecK);
        AddKahn<I8>(ExecK);
        AddKahn<I4>(ExecK);
        AddKahn<I2>(ExecK);
        AddKahn<I1>(ExecK);
        AddKahn<U8>(ExecK);
        AddKahn<U4>(ExecK);
        AddKahn<U2>(ExecK);
        AddKahn<U1>(ExecK);
        AddKahn<BL>(ExecK);

        AddKopt<R8>(ExecK);
        AddKopt<R4>(ExecK);
        AddKopt<IA>(ExecK);
        AddKopt<I8>(ExecK);
        AddKopt<I4>(ExecK);
        AddKopt<I2>(ExecK);
        AddKopt<I1>(ExecK);
        AddKopt<U8>(ExecK);
        AddKopt<U4>(ExecK);
        AddKopt<U2>(ExecK);
        AddKopt<U1>(ExecK);
        AddKopt<BL>(ExecK);

        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Kahan, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<O, double>, ExecCtx, int, long, double>(ExecK).Method.GetGenericMethodDefinition());
        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Kahan | Variation.Opt, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<O, double?>, ExecCtx, int, long, double>(ExecK).Method.GetGenericMethodDefinition());
        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Kahan | Variation.Ind, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<long, O, double>, ExecCtx, int, long, double>(ExecIndK).Method.GetGenericMethodDefinition());
        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Kahan | Variation.Opt | Variation.Ind, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<long, O, double?>, ExecCtx, int, long, double>(ExecIndK).Method.GetGenericMethodDefinition());

        void AddMean<T>(CodeGenUtil.FuncOut<IEnumerable<T>, ExecCtx, int, long, double> fn0)
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(DKind.R8, kind, flags: Variation.Mean, cscp: 0), fn0.Method);
        }

        void AddMopt<T>(CodeGenUtil.FuncOut<IEnumerable<T?>, ExecCtx, int, long, double> fn0)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(DKind.R8, kind, flags: Variation.Mean | Variation.Opt, cscp: 0), fn0.Method);
        }

        AddMean<R8>(ExecMean);
        AddMean<R4>(ExecMean);
        AddMean<IA>(ExecMean);
        AddMean<I8>(ExecMean);
        AddMean<I4>(ExecMean);
        AddMean<I2>(ExecMean);
        AddMean<I1>(ExecMean);
        AddMean<U8>(ExecMean);
        AddMean<U4>(ExecMean);
        AddMean<U2>(ExecMean);
        AddMean<U1>(ExecMean);
        AddMean<BL>(ExecMean);

        AddMopt<R8>(ExecMean);
        AddMopt<R4>(ExecMean);
        AddMopt<IA>(ExecMean);
        AddMopt<I8>(ExecMean);
        AddMopt<I4>(ExecMean);
        AddMopt<I2>(ExecMean);
        AddMopt<I1>(ExecMean);
        AddMopt<U8>(ExecMean);
        AddMopt<U4>(ExecMean);
        AddMopt<U2>(ExecMean);
        AddMopt<U1>(ExecMean);
        AddMopt<BL>(ExecMean);

        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Mean, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<O, double>, ExecCtx, int, long, double>(ExecMean).Method.GetGenericMethodDefinition());
        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Mean | Variation.Ind, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<long, O, double>, ExecCtx, int, long, double>(ExecIndMean).Method.GetGenericMethodDefinition());
        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Mean | Variation.Opt, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<O, double?>, ExecCtx, int, long, double>(ExecMean).Method.GetGenericMethodDefinition());
        AddMeth(MakeKey(DKind.R8, DKind.R8, flags: Variation.Mean | Variation.Opt | Variation.Ind, cscp: 1),
            new CodeGenUtil.FuncOut<IE, Func<long, O, double?>, ExecCtx, int, long, double>(ExecIndMean).Method.GetGenericMethodDefinition());

        return map;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        int cscp = call.Scopes.Length;
        DType typeRes = call.Type;
        Type stRes = codeGen.GetSystemType(typeRes);
        DType typeAgg = fn.AggFromRes(typeRes);
        Validation.Assert(typeAgg.IsNumericReq);
        Type stAgg = codeGen.GetSystemType(typeAgg);

        Variation flags = 0;
        switch (fn.Kind)
        {
        case SumBaseFunc.SumKind.Kahan:
            flags |= Variation.Kahan;
            break;
        case SumBaseFunc.SumKind.Mean:
            flags |= Variation.Mean;
            break;
        }

        MethodInfo meth;
        if (cscp == 0)
        {
            // No selector delegate.
            Validation.Assert(call.Indices.Length == 0);
            var typeSeq = call.Args[0].Type;
            Validation.Assert(typeSeq.SeqCount == 1);
            var typeItem = typeSeq.ItemTypeOrThis;
            Validation.Assert(typeItem.IsNumericXxx);
            if (typeItem.IsOpt)
                flags |= Variation.Opt;
            uint key = MakeKey(typeAgg.RootKind, typeItem.RootKind, flags, cscp);
            if (!_methMap.TryGetValue(key, out meth))
            {
                Validation.Assert(false);
                stRet = null;
                return false;
            }
        }
        else
        {
            // Selector delegate case. Note that the return type from the selector always
            // matches the agg type, except possibly for opt-ness.
            Validation.Assert(call.Indices.Length == 1);
            DType typeSel = call.Args[cscp].Type;
            Validation.Assert(typeSel.IsNumericXxx);
            Validation.Assert(typeSel.ToReq() == typeAgg);
            if (typeSel.IsOpt)
                flags |= Variation.Opt;
            if (call.Indices[0] != null)
                flags |= Variation.Ind;
            uint key = MakeKey(typeAgg.RootKind, typeSel.RootKind, flags, cscp);
            if (!_methMap.TryGetValue(key, out meth))
            {
                Validation.Assert(false);
                stRet = null;
                return false;
            }

            // The method info is a generic definition. Need the specific instantiation using the
            // src sequence item types.
            Validation.Assert(meth.IsGenericMethodDefinition);
            Validation.Assert(meth.GetGenericArguments().Length == cscp);

            Type[] stsTmp = new Type[cscp];
            for (int i = 0; i < cscp; i++)
            {
                DType typeSeq = call.Args[i].Type;
                Validation.Assert(typeSeq.SeqCount > 0);
                DType typeItemSrc = typeSeq.ItemTypeOrThis;
                Validation.Assert(typeItemSrc == call.Scopes[i].Type);
                stsTmp[i] = codeGen.GetSystemType(typeItemSrc);
            }
            meth = meth.MakeGenericMethod(stsTmp);
        }

        Validation.Assert(meth.GetParameters().Length == call.Args.Length + 3);
        Validation.Assert(meth.ReturnType == stAgg);

        // Need the execution context, for pinging.
        codeGen.GenLoadExecCtxAndId(call);
        var ilw = codeGen.Writer;

        if (fn.WithCount)
        {
            // Result is a record.
            Validation.Assert(typeRes.FieldCount == 2);
            using var rg = codeGen.CreateRecordGenerator(typeRes);
            Validation.Assert(rg.RecSysType == stRes);

            rg.LoadFieldAddr(StatFuncUtil.NameCount);
            ilw.Call(meth);
            using var locVal = codeGen.AcquireLocal(stAgg);
            ilw.Stloc(locVal);
            rg.SetFromLocal(fn.Kind == SumBaseFunc.SumKind.Mean ? StatFuncUtil.NameMean : StatFuncUtil.NameSum, locVal).Finish();
        }
        else
        {
            using var locCount = codeGen.AcquireLocal(typeof(long));
            ilw
                .Ldloca(locCount)
                .Call(meth);
        }

        stRet = stRes;
        return true;
    }
}

public sealed partial class MinMaxGen : RexlOperationGenerator<MinMaxFunc>
{
    public static readonly MinMaxGen Instance = new MinMaxGen();

    private readonly ReadOnly.Dictionary<uint, MethodInfo> _methMap;

    private MinMaxGen()
    {
        _methMap = BuildMethMap();
    }

    /// <summary>
    /// Combines the args into a unique uint-valued key.
    /// </summary>
    private static uint MakeKey(DKind kind, bool isOpt, int cscp, MinMaxFunc.Parts parts)
    {
        Validation.Assert((uint)kind < 0x100);
        Validation.Assert((uint)cscp < 0x100);
        return ((uint)kind << 0) | ((uint)cscp << 8) | (isOpt ? 1u << 16 : 0) | ((uint)parts << 24);
    }

    /// <summary>
    /// Build a dictionary from "key" to MethodInfo for supported sum types.
    /// </summary>
    private static ReadOnly.Dictionary<uint, MethodInfo> BuildMethMap()
    {
        // First create a system type to DType map.
        var typeMap = new Dictionary<Type, DKind>();

        void AddType<T>(DKind kind)
        {
            typeMap.Add(typeof(T), kind);
        }

        AddType<R8>(DKind.R8);
        AddType<R4>(DKind.R4);
        AddType<I8>(DKind.I8);
        AddType<I4>(DKind.I4);
        AddType<I2>(DKind.I2);
        AddType<I1>(DKind.I1);
        AddType<U8>(DKind.U8);
        AddType<U4>(DKind.U4);
        AddType<U2>(DKind.U2);
        AddType<U1>(DKind.U1);
        AddType<BL>(DKind.Bit);
        AddType<IA>(DKind.IA);

        // Now create the method info map.
        var map = new Dictionary<uint, MethodInfo>();

        // Adds the given (key, meth) to the map.
        void AddMeth(uint key, MethodInfo meth)
        {
            Validation.Assert(!map.ContainsKey(key));
            Validation.AssertValue(meth);
            map.Add(key, meth);
        }

        // Adds the various MinMaxC delegate methods to the map. The first, fn0, is for
        // when the src and dst item types are the same and there is no selector delegate.
        // The rest are for selector delegate cases of various arities.
        void AddMinMaxC<T>(
            CodeGenUtil.FuncOut2<IEnumerable<T>, ExecCtx, int, T, T, long> fn0,
            CodeGenUtil.FuncOut2<IE, Func<O, T>, ExecCtx, int, T, T, long> fn1,
            CodeGenUtil.FuncOut2<IE, Func<long, O, T>, ExecCtx, int, T, T, long> fn2,
            CodeGenUtil.FuncOut2<IEnumerable<T?>, ExecCtx, int, T, T, long> fn3,
            CodeGenUtil.FuncOut2<IE, Func<O, T?>, ExecCtx, int, T, T, long> fn4,
            CodeGenUtil.FuncOut2<IE, Func<long, O, T?>, ExecCtx, int, T, T, long> fn5)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, false, 0, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.MinMax), fn0.Method);
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.MinMax), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.MinMax | MinMaxFunc.Parts._Indexed), fn2.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 0, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.MinMax), fn3.Method);
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.MinMax), fn4.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.MinMax | MinMaxFunc.Parts._Indexed), fn5.Method.GetGenericMethodDefinition());
        }

        AddMinMaxC<R8>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<R4>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<I8>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<I4>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<I2>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<I1>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<U8>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<U4>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<U2>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<U1>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<BL>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);
        AddMinMaxC<IA>(ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC, ExecMinMaxC, ExecMinMaxC, ExecMinMaxIndC);

        // Adds the various MinC delegate methods to the map. The first, fn0, is for
        // when the src and dst item types are the same and there is no selector delegate.
        // The rest are for selector delegate cases of various arities.
        void AddMinC<T>(
            CodeGenUtil.FuncOut<IEnumerable<T>, ExecCtx, int, T, long> fn0,
            CodeGenUtil.FuncOut<IE, Func<O, T>, ExecCtx, int, T, long> fn1,
            CodeGenUtil.FuncOut<IE, Func<long, O, T>, ExecCtx, int, T, long> fn2,
            CodeGenUtil.FuncOut<IEnumerable<T?>, ExecCtx, int, T, long> fn3,
            CodeGenUtil.FuncOut<IE, Func<O, T?>, ExecCtx, int, T, long> fn4,
            CodeGenUtil.FuncOut<IE, Func<long, O, T?>, ExecCtx, int, T, long> fn5)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, false, 0, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Min), fn0.Method);
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Min), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Min | MinMaxFunc.Parts._Indexed), fn2.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 0, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Min), fn3.Method);
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Min), fn4.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Min | MinMaxFunc.Parts._Indexed), fn5.Method.GetGenericMethodDefinition());
        }

        AddMinC<R8>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<R4>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<I8>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<I4>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<I2>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<I1>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<U8>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<U4>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<U2>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<U1>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<BL>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);
        AddMinC<IA>(ExecMinC, ExecMinC, ExecMinIndC, ExecMinC, ExecMinC, ExecMinIndC);

        // Adds the various MaxC delegate methods to the map. The first, fn0, is for
        // when the src and dst item types are the same and there is no selector delegate.
        // The rest are for selector delegate cases of various arities.
        void AddMaxC<T>(
            CodeGenUtil.FuncOut<IEnumerable<T>, ExecCtx, int, T, long> fn0,
            CodeGenUtil.FuncOut<IE, Func<O, T>, ExecCtx, int, T, long> fn1,
            CodeGenUtil.FuncOut<IE, Func<long, O, T>, ExecCtx, int, T, long> fn2,
            CodeGenUtil.FuncOut<IEnumerable<T?>, ExecCtx, int, T, long> fn3,
            CodeGenUtil.FuncOut<IE, Func<O, T?>, ExecCtx, int, T, long> fn4,
            CodeGenUtil.FuncOut<IE, Func<long, O, T?>, ExecCtx, int, T, long> fn5)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, false, 0, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Max), fn0.Method);
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Max), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Max | MinMaxFunc.Parts._Indexed), fn2.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 0, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Max), fn3.Method);
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Max), fn4.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Count | MinMaxFunc.Parts.Max | MinMaxFunc.Parts._Indexed), fn5.Method.GetGenericMethodDefinition());
        }

        AddMaxC<R8>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<R4>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<I8>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<I4>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<I2>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<I1>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<U8>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<U4>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<U2>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<U1>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<BL>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);
        AddMaxC<IA>(ExecMaxC, ExecMaxC, ExecMaxIndC, ExecMaxC, ExecMaxC, ExecMaxIndC);

        // Adds the various MinMax delegate methods to the map. The first, fn0, is for
        // when the src and dst item types are the same and there is no selector delegate.
        // The rest are for selector delegate cases of various arities.
        void AddMinMax<T>(
            CodeGenUtil.ActOut2<IEnumerable<T>, ExecCtx, int, T, T> fn0,
            CodeGenUtil.ActOut2<IE, Func<O, T>, ExecCtx, int, T, T> fn1,
            CodeGenUtil.ActOut2<IE, Func<long, O, T>, ExecCtx, int, T, T> fn2,
            CodeGenUtil.ActOut2<IEnumerable<T?>, ExecCtx, int, T, T> fn3,
            CodeGenUtil.ActOut2<IE, Func<O, T?>, ExecCtx, int, T, T> fn4,
            CodeGenUtil.ActOut2<IE, Func<long, O, T?>, ExecCtx, int, T, T> fn5)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, false, 0, MinMaxFunc.Parts.MinMax), fn0.Method);
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.MinMax), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.MinMax | MinMaxFunc.Parts._Indexed), fn2.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 0, MinMaxFunc.Parts.MinMax), fn3.Method);
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.MinMax), fn4.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.MinMax | MinMaxFunc.Parts._Indexed), fn5.Method.GetGenericMethodDefinition());
        }

        AddMinMax<R8>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<R4>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<I8>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<I4>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<I2>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<I1>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<U8>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<U4>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<U2>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<U1>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<BL>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);
        AddMinMax<IA>(ExecMinMax, ExecMinMax, ExecMinMaxInd, ExecMinMax, ExecMinMax, ExecMinMaxInd);

        // Adds the various Min delegate methods to the map. The first, fn0, is for
        // when the src and dst item types are the same and there is no selector delegate.
        // The rest are for selector delegate cases of various arities.
        void AddMin<T>(
            Func<IEnumerable<T>, ExecCtx, int, T> fn0,
            Func<IE, Func<O, T>, ExecCtx, int, T> fn1,
            Func<IE, Func<long, O, T>, ExecCtx, int, T> fn2,
            Func<IEnumerable<T?>, ExecCtx, int, T> fn3,
            Func<IE, Func<O, T?>, ExecCtx, int, T> fn4,
            Func<IE, Func<long, O, T?>, ExecCtx, int, T> fn5)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, false, 0, MinMaxFunc.Parts.Min), fn0.Method);
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Min), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Min | MinMaxFunc.Parts._Indexed), fn2.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 0, MinMaxFunc.Parts.Min), fn3.Method);
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Min), fn4.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Min | MinMaxFunc.Parts._Indexed), fn5.Method.GetGenericMethodDefinition());
        }

        AddMin<R8>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<R4>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<I8>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<I4>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<I2>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<I1>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<U8>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<U4>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<U2>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<U1>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<BL>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);
        AddMin<IA>(ExecMin, ExecMin, ExecMinInd, ExecMin, ExecMin, ExecMinInd);

        // Adds the various Max delegate methods to the map. The first, fn0, is for
        // when the src and dst item types are the same and there is no selector delegate.
        // The rest are for selector delegate cases of various arities.
        void AddMax<T>(
            Func<IEnumerable<T>, ExecCtx, int, T> fn0,
            Func<IE, Func<O, T>, ExecCtx, int, T> fn1,
            Func<IE, Func<long, O, T>, ExecCtx, int, T> fn2,
            Func<IEnumerable<T?>, ExecCtx, int, T> fn3,
            Func<IE, Func<O, T?>, ExecCtx, int, T> fn4,
            Func<IE, Func<long, O, T?>, ExecCtx, int, T> fn5)
            where T : struct
        {
            Validation.Assert(typeMap.ContainsKey(typeof(T)));
            var kind = typeMap[typeof(T)];
            AddMeth(MakeKey(kind, false, 0, MinMaxFunc.Parts.Max), fn0.Method);
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Max), fn1.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, false, 1, MinMaxFunc.Parts.Max | MinMaxFunc.Parts._Indexed), fn2.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 0, MinMaxFunc.Parts.Max), fn3.Method);
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Max), fn4.Method.GetGenericMethodDefinition());
            AddMeth(MakeKey(kind, true, 1, MinMaxFunc.Parts.Max | MinMaxFunc.Parts._Indexed), fn5.Method.GetGenericMethodDefinition());
        }

        AddMax<R8>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<R4>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<I8>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<I4>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<I2>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<I1>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<U8>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<U4>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<U2>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<U1>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<BL>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);
        AddMax<IA>(ExecMax, ExecMax, ExecMaxInd, ExecMax, ExecMax, ExecMaxInd);

        return map;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        int cscp = call.Scopes.Length;
        DType typeRes = call.Type;
        DType typeAgg = fn.AggFromRes(typeRes);
        Validation.Assert(typeAgg.IsNumericReq);
        Type stAgg = codeGen.GetSystemType(typeAgg);

        MethodInfo meth;
        if (cscp == 0)
        {
            // No selector delegate.
            var typeSeq = call.Args[0].Type;
            Validation.Assert(typeSeq.SeqCount == 1);
            var typeItem = typeSeq.ItemTypeOrThis;
            Validation.Assert(typeItem.IsNumericXxx);
            Validation.Assert(typeItem.ToReq() == typeAgg);
            Validation.Assert(fn.Kind != MinMaxFunc.Parts.Count);
            uint key = MakeKey(typeAgg.RootKind, typeItem.IsOpt, cscp, fn.Kind);

            if (!_methMap.TryGetValue(key, out meth))
            {
                Validation.Assert(false);
                stRet = null;
                return false;
            }
        }
        else
        {
            // Selector delegate case. Note that the return type from the selector always
            // matches the agg type, except possibly for opt-ness.
            Validation.Assert(call.Indices.Length == 1);
            DType typeSel = call.Args[cscp].Type;
            Validation.Assert(typeSel.IsNumericXxx);
            Validation.Assert(typeSel.ToReq() == typeAgg);
            Validation.Assert(fn.Kind != MinMaxFunc.Parts.Count);

            var parts = fn.Kind;
            if (call.Indices[0] != null)
                parts |= MinMaxFunc.Parts._Indexed;

            uint key = MakeKey(typeSel.RootKind, typeSel.IsOpt, cscp, parts);

            if (!_methMap.TryGetValue(key, out meth))
            {
                Validation.Assert(false);
                stRet = null;
                return false;
            }

            // The method info is a generic definition. Need the specific instantiation using the
            // src sequence item types.
            Validation.Assert(meth.IsGenericMethodDefinition);
            Validation.Assert(meth.GetGenericArguments().Length == cscp);

            Type[] stsTmp = new Type[cscp];
            for (int i = 0; i < cscp; i++)
            {
                DType typeSeq = call.Args[i].Type;
                Validation.Assert(typeSeq.SeqCount > 0);
                DType typeItemSrc = typeSeq.ItemTypeOrThis;
                Validation.Assert(typeItemSrc == call.Scopes[i].Type);
                stsTmp[i] = codeGen.GetSystemType(typeItemSrc);
            }
            meth = meth.MakeGenericMethod(stsTmp);
        }

        if (fn.WithCount)
        {
            Validation.Assert(meth.ReturnType == typeof(long));

            // Need the execution context, for pinging.
            codeGen.GenLoadExecCtxAndId(call);
            var ilw = codeGen.Writer;

            // Result is a record.
            Validation.Assert(typeRes.IsRecordReq);
            using var rg = codeGen.CreateRecordGenerator(typeRes);

            Validation.Assert(meth.GetParameters().Length == call.Args.Length + (fn.WithBoth ? 4 : 3));
            if (fn.WithMin)
                rg.LoadFieldAddr(StatFuncUtil.NameMin);
            if (fn.WithMax)
                rg.LoadFieldAddr(StatFuncUtil.NameMax);

            ilw.Call(meth);

            using var locCount = codeGen.AcquireLocal(typeof(long));
            ilw.Stloc(locCount);
            rg.SetFromLocal(StatFuncUtil.NameCount, locCount);

            stRet = rg.RecSysType;
            rg.Finish();
        }
        else if (fn.WithBoth)
        {
            Validation.Assert(meth.ReturnType == typeof(void));
            Validation.Assert(meth.GetParameters().Length == call.Args.Length + 4);

            // Need the execution context, for pinging.
            codeGen.GenLoadExecCtxAndId(call);
            var ilw = codeGen.Writer;

            // Result is a record.
            Validation.Assert(typeRes.IsRecordReq);
            using var rg = codeGen.CreateRecordGenerator(typeRes);

            rg.LoadFieldAddr(StatFuncUtil.NameMin);
            rg.LoadFieldAddr(StatFuncUtil.NameMax);

            ilw.Call(meth);

            stRet = rg.RecSysType;
            rg.Finish();
        }
        else
        {
            Validation.Assert(typeRes == typeAgg);
            Validation.Assert(meth.ReturnType == stAgg);
            stRet = GenCallCtxId(codeGen, meth, sts, call);
        }

        return true;
    }

    #region Exec on single sequence of number type for non-generated types.

    public static void ExecMinMax(IEnumerable<bool> items, ExecCtx ctx, int id, out bool min, out bool max)
    {
        Validation.AssertValue(ctx);
        if (items == null)
        {
            min = false;
            max = false;
            return;
        }

        using var e = items.GetEnumerator();
        ctx.Ping(id);
        if (!e.MoveNext())
        {
            min = false;
            max = false;
            return;
        }

        bool haveTrue = e.Current;
        bool haveFalse = !e.Current;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            if (e.Current)
            {
                haveTrue = true;
                if (haveFalse)
                    break;
            }
            else
            {
                haveFalse = true;
                if (haveTrue)
                    break;
            }
        }
        min = !haveFalse;
        max = haveTrue;
    }

    public static bool ExecMin(IEnumerable<bool> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items == null)
            return false;

        using var e = items.GetEnumerator();
        ctx.Ping(id); if (!e.MoveNext() || !e.Current) return false;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            if (!e.Current)
                return false;
        }
        return true;
    }

    public static bool ExecMax(IEnumerable<bool> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var item = e.Current;
                if (item)
                    return true;
            }
        }
        return false;
    }

    public static void ExecMinMax(IEnumerable<bool?> items, ExecCtx ctx, int id, out bool min, out bool max)
    {
        Validation.AssertValue(ctx);
        if (items == null)
        {
            min = false;
            max = false;
            return;
        }

        using var e = items.GetEnumerator();
        // Find the first non-null.
        bool? itemOpt;
        for (; ; )
        {
            ctx.Ping(id);
            if (!e.MoveNext())
            {
                min = false;
                max = false;
                return;
            }
            itemOpt = e.Current;
            if (itemOpt != null)
                break;
        }
        bool haveTrue = itemOpt.GetValueOrDefault();
        bool haveFalse = !haveTrue;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            itemOpt = e.Current;
            if (itemOpt == null)
                continue;
            if (itemOpt.GetValueOrDefault())
            {
                haveTrue = true;
                if (haveFalse)
                    break;
            }
            else
            {
                haveFalse = true;
                if (haveTrue)
                    break;
            }
        }
        min = !haveFalse;
        max = haveTrue;
    }

    // REVIEW: Consider returning null when no non-null items are found
    // and make force the return type to opt. Same for all other types.
    public static bool ExecMin(IEnumerable<bool?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items == null)
            return false;

        using var e = items.GetEnumerator();
        // Find the first non-null.
        bool? itemOpt;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) return false;
            itemOpt = e.Current;
            if (itemOpt != null)
                break;
        }
        if (!itemOpt.GetValueOrDefault())
            return false;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            itemOpt = e.Current;
            if (itemOpt == null)
                continue;
            if (!itemOpt.GetValueOrDefault())
                return false;
        }
        return true;
    }

    public static bool ExecMax(IEnumerable<bool?> items, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var itemOpt = e.Current;
                if (itemOpt == null)
                    continue;
                if (itemOpt.GetValueOrDefault())
                    return true;
            }
        }
        return false;
    }

    #endregion

    #region Exec with one sequence and function for non-generated types.

    public static void ExecMinMax<T0>(IEnumerable<T0> items, Func<T0, bool> fn, ExecCtx ctx, int id, out bool min, out bool max)
    {
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);
        if (items == null)
        {
            min = false;
            max = false;
            return;
        }

        using var e = items.GetEnumerator();
        ctx.Ping(id);
        if (!e.MoveNext())
        {
            min = false;
            max = false;
            return;
        }

        bool haveTrue = fn(e.Current);
        bool haveFalse = !haveTrue;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            if (fn(e.Current))
            {
                haveTrue = true;
                if (haveFalse)
                    break;
            }
            else
            {
                haveFalse = true;
                if (haveTrue)
                    break;
            }
        }
        min = !haveFalse;
        max = haveTrue;
    }

    public static bool ExecMin<T0>(IEnumerable<T0> items, Func<T0, bool> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items == null)
            return false;

        using var e = items.GetEnumerator();
        ctx.Ping(id); if (!e.MoveNext() || !fn(e.Current)) return false;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            if (!fn(e.Current))
                return false;
        }
        return true;
    }

    public static bool ExecMax<T0>(IEnumerable<T0> items, Func<T0, bool> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                if (fn(e.Current))
                    return true;
            }
        }
        return false;
    }

    public static void ExecMinMax<T0>(IEnumerable<T0> items, Func<T0, bool?> fn, ExecCtx ctx, int id, out bool min, out bool max)
    {
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);
        if (items == null)
        {
            min = false;
            max = false;
            return;
        }

        using var e = items.GetEnumerator();
        // Find the first non-null.
        bool? itemOpt;
        for (; ; )
        {
            ctx.Ping(id);
            if (!e.MoveNext())
            {
                min = false;
                max = false;
                return;
            }
            itemOpt = fn(e.Current);
            if (itemOpt != null)
                break;
        }
        bool haveTrue = itemOpt.GetValueOrDefault();
        bool haveFalse = !haveTrue;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            itemOpt = fn(e.Current);
            if (itemOpt == null)
                continue;
            if (itemOpt.GetValueOrDefault())
            {
                haveTrue = true;
                if (haveFalse)
                    break;
            }
            else
            {
                haveFalse = true;
                if (haveTrue)
                    break;
            }
        }
        min = !haveFalse;
        max = haveTrue;
    }

    public static bool ExecMin<T0>(IEnumerable<T0> items, Func<T0, bool?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items == null)
            return false;

        using var e = items.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) return false;
            var itemOpt = fn(e.Current);
            if (itemOpt == null)
                continue;
            if (!itemOpt.GetValueOrDefault())
                return false;
            break;
        }
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            var itemOpt = fn(e.Current);
            if (itemOpt == null)
                continue;
            if (!itemOpt.GetValueOrDefault())
                return false;
        }
        return true;
    }

    public static bool ExecMax<T0>(IEnumerable<T0> items, Func<T0, bool?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items != null)
        {
            using var e = items.GetEnumerator();
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var itemOpt = fn(e.Current);
                if (itemOpt == null)
                    continue;
                if (itemOpt.GetValueOrDefault())
                    return true;
            }
        }
        return false;
    }

    #endregion

    #region Indexed versions for non-generated types.

    public static void ExecMinMaxInd<T0>(IEnumerable<T0> items, Func<long, T0, bool> fn, ExecCtx ctx, int id, out bool min, out bool max)
    {
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);
        if (items == null)
        {
            min = false;
            max = false;
            return;
        }

        using var e = items.GetEnumerator();
        ctx.Ping(id);
        if (!e.MoveNext())
        {
            min = false;
            max = false;
            return;
        }

        long idx = 0;
        bool haveTrue = fn(idx++, e.Current);
        bool haveFalse = !haveTrue;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            if (fn(idx++, e.Current))
            {
                haveTrue = true;
                if (haveFalse)
                    break;
            }
            else
            {
                haveFalse = true;
                if (haveTrue)
                    break;
            }
        }
        min = !haveFalse;
        max = haveTrue;
    }

    public static bool ExecMinInd<T0>(IEnumerable<T0> items, Func<long, T0, bool> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items == null)
            return false;

        using var e = items.GetEnumerator();
        long idx = 0;
        ctx.Ping(id); if (!e.MoveNext() || !fn(idx++, e.Current)) return false;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            if (!fn(idx++, e.Current))
                return false;
        }
        return true;
    }

    public static bool ExecMaxInd<T0>(IEnumerable<T0> items, Func<long, T0, bool> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items != null)
        {
            using var e = items.GetEnumerator();
            long idx = 0;
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                if (fn(idx++, e.Current))
                    return true;
            }
        }
        return false;
    }

    public static void ExecMinMaxInd<T0>(IEnumerable<T0> items, Func<long, T0, bool?> fn, ExecCtx ctx, int id, out bool min, out bool max)
    {
        Validation.AssertValue(fn);
        Validation.AssertValue(ctx);
        if (items == null)
        {
            min = false;
            max = false;
            return;
        }

        using var e = items.GetEnumerator();
        long idx = 0;
        // Find the first non-null.
        bool? itemOpt;
        for (; ; )
        {
            ctx.Ping(id);
            if (!e.MoveNext())
            {
                min = false;
                max = false;
                return;
            }
            itemOpt = fn(idx++, e.Current);
            if (itemOpt != null)
                break;
        }
        bool haveTrue = itemOpt.GetValueOrDefault();
        bool haveFalse = !haveTrue;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            itemOpt = fn(idx++, e.Current);
            if (itemOpt == null)
                continue;
            if (itemOpt.GetValueOrDefault())
            {
                haveTrue = true;
                if (haveFalse)
                    break;
            }
            else
            {
                haveFalse = true;
                if (haveTrue)
                    break;
            }
        }
        min = !haveFalse;
        max = haveTrue;
    }

    public static bool ExecMinInd<T0>(IEnumerable<T0> items, Func<long, T0, bool?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items == null)
            return false;

        using var e = items.GetEnumerator();
        long idx = 0;
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) return false;
            var itemOpt = fn(idx++, e.Current);
            if (itemOpt == null)
                continue;
            if (!itemOpt.GetValueOrDefault())
                return false;
            break;
        }
        for (; ; )
        {
            ctx.Ping(id); if (!e.MoveNext()) break;
            var itemOpt = fn(idx++, e.Current);
            if (itemOpt == null)
                continue;
            if (!itemOpt.GetValueOrDefault())
                return false;
        }
        return true;
    }

    public static bool ExecMaxInd<T0>(IEnumerable<T0> items, Func<long, T0, bool?> fn, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);
        if (items != null)
        {
            using var e = items.GetEnumerator();
            long idx = 0;
            for (; ; )
            {
                ctx.Ping(id); if (!e.MoveNext()) break;
                var itemOpt = fn(idx++, e.Current);
                if (itemOpt == null)
                    continue;
                if (itemOpt.GetValueOrDefault())
                    return true;
            }
        }
        return false;
    }

    #endregion
}

public sealed class RangeGen : MethArityGen<RangeFunc>
{
    public static readonly RangeGen Instance = new RangeGen();

    protected override ReadOnly.Array<MethodInfo> Meths { get; }

    protected override int ArityMin => 1;

    private RangeGen()
    {
        Meths = new[]
        {
            new Func<long, ICachingEnumerable<long>>(Exec).Method,
            new Func<long, long, ICachingEnumerable<long>>(Exec).Method,
            new Func<long, long, long, ICachingEnumerable<long>>(Exec).Method,
        };
    }

    public static ICachingEnumerable<long> Exec(long lim)
    {
        long num = lim < 0 ? 0 : lim;
        return new Impl(0, num, 1);
    }

    public static ICachingEnumerable<long> Exec(long min, long lim)
    {
        long num = Util.GetCount(min, lim);
        return new Impl(min, num, 1);
    }

    public static ICachingEnumerable<long> Exec(long min, long lim, long step)
    {
        long num = Util.GetCount(min, lim, step);
        return new Impl(min, num, step);
    }

    private sealed class Impl : ICursorable<long>, ICanCount
    {
        private readonly long _beg;
        private readonly long _num;
        private readonly long _step;

        public Impl(long beg, long num, long step)
        {
            Validation.Assert(num >= 0);
            _beg = beg;
            _num = num;
            _step = step;
        }

        public IEnumerator<long> GetEnumerator()
        {
            long cur = _beg;
            for (long i = 0; i < _num; i++, cur += _step)
                yield return cur;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetCount(out long count)
        {
            count = _num;
            return true;
        }

        public long GetCount(Action callback)
        {
            Validation.BugCheckValueOrNull(callback);
            return _num;
        }

        public ICursor<long> GetCursor()
        {
            return new CursorImpl(this);
        }

        ICursor ICursorable.GetCursor()
        {
            return GetCursor();
        }

        private sealed class CursorImpl : ICursor<long>
        {
            private Impl _parent;

            private long _value;
            private long _index;

            public CursorImpl(Impl parent)
            {
                _parent = parent;
                _index = -1;
            }

            public long Index => _index;
            public long Value => _value;
            public long Current => _value;
            object ICursor.Value => _value;
            object IEnumerator.Current => _value;

            public void Dispose()
            {
                _parent = null;
            }

            public bool MoveNext()
            {
                var parent = _parent;
                Validation.BugCheck(parent != null);

                var inext = _index + 1;
                if (inext >= parent._num)
                    return false;

                _value = _index == -1 ? parent._beg : _value + parent._step;
                _index = inext;
                return true;
            }

            public bool MoveTo(long index)
            {
                Validation.BugCheckParam(index >= 0, nameof(index));
                var parent = _parent;
                Validation.BugCheck(parent != null);

                if (index >= parent._num)
                    return false;

                _index = index;
                _value = parent._beg + _index * parent._step;
                return true;
            }

            public void Reset()
            {
                throw new InvalidOperationException();
            }
        }
    }
}

public sealed class SequenceGen : RexlOperationGenerator<SequenceFunc>
{
    public static readonly SequenceGen Instance = new SequenceGen();

    private readonly MethodInfo _methI8;
    private readonly MethodInfo _methU8;
    private readonly MethodInfo _methIA;
    private readonly MethodInfo _methR8;

    private readonly ReadOnly.Array<Type> _stsI8;
    private readonly ReadOnly.Array<Type> _stsU8;
    private readonly ReadOnly.Array<Type> _stsIA;
    private readonly ReadOnly.Array<Type> _stsR8;

    private SequenceGen()
    {
        _methI8 = new Func<long, long, long, ICachingEnumerable<long>>(Exec).Method;
        _methU8 = new Func<long, ulong, ulong, ICachingEnumerable<ulong>>(Exec).Method;
        _methIA = new Func<long, Integer, Integer, ICachingEnumerable<Integer>>(Exec).Method;
        _methR8 = new Func<long, double, double, ICachingEnumerable<double>>(Exec).Method;
        _stsI8 = new[] { typeof(long), typeof(long), typeof(long) };
        _stsU8 = new[] { typeof(long), typeof(ulong), typeof(ulong) };
        _stsIA = new[] { typeof(long), typeof(Integer), typeof(Integer) };
        _stsR8 = new[] { typeof(long), typeof(double), typeof(double) };
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(call.Type.SeqCount == 1);
        Validation.Assert(call.Type.ItemTypeOrThis.IsNumericReq);
        Validation.Assert(call.Type.RootKind.NumericSize() >= 8);

        wrap = SeqWrapKind.DontWrap;

        MethodInfo meth;
        var ilw = codeGen.Writer;
        var kindRes = call.Type.RootKind;
        int arity = call.Args.Length;
        switch (arity)
        {
        default:
            Validation.Assert(false);
            stRet = default;
            return false;

        case 1:
            Validation.Assert(kindRes == DKind.I8);
            Validation.Assert(sts[0] == typeof(long));
            ilw.Ldc_I8(1).Ldc_I8(1);
            stRet = GenCall(codeGen, _methI8, _stsI8);
            return true;

        case 2:
        case 3:
            break;
        }

        switch (kindRes)
        {
        default:
            Validation.Assert(false);
            stRet = default;
            return false;

        case DKind.I8:
            meth = _methI8;
            Validation.Assert(sts[0] == _stsI8[0]);
            Validation.Assert(sts[1] == _stsI8[1]);
            if (arity == 2)
            {
                ilw.Ldc_I8(1);
                sts = _stsI8;
            }
            break;
        case DKind.U8:
            meth = _methU8;
            Validation.Assert(sts[0] == _stsU8[0]);
            Validation.Assert(sts[1] == _stsU8[1]);
            if (arity == 2)
            {
                ilw.Ldc_U8(1);
                sts = _stsU8;
            }
            break;
        case DKind.IA:
            meth = _methIA;
            Validation.Assert(sts[0] == _stsIA[0]);
            Validation.Assert(sts[1] == _stsIA[1]);
            if (arity == 2)
            {
                ilw.Ldc_I4(1).Newobj(CodeGenUtil.CtorIntFromI4);
                sts = _stsIA;
            }
            break;
        case DKind.R8:
            meth = _methR8;
            Validation.Assert(sts[0] == _stsR8[0]);
            Validation.Assert(sts[1] == _stsR8[1]);
            if (arity == 2)
            {
                ilw.Ldc_R8(1);
                sts = _stsR8;
            }
            break;
        }

        stRet = GenCall(codeGen, meth, sts);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICachingEnumerable<long> Exec(long num, long start, long step)
    {
        if (num <= 0)
            return null;
        return new ImplI8(num, start, step);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICachingEnumerable<ulong> Exec(long num, ulong start, ulong step)
    {
        if (num <= 0)
            return null;
        return new ImplU8(num, start, step);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICachingEnumerable<Integer> Exec(long num, Integer start, Integer step)
    {
        if (num <= 0)
            return null;
        return new ImplIA(num, start, step);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICachingEnumerable<double> Exec(long num, double start, double step)
    {
        if (num <= 0)
            return null;
        return new ImplR8(num, start, step);
    }

    private sealed class ImplI8 : Impl<long>
    {
        public ImplI8(long num, long beg, long step)
            : base(num, beg, step)
        {
        }

        public override IEnumerator<long> GetEnumerator()
        {
            var cur = _beg;
            for (long i = 0; i < _num; i++, cur += _step)
                yield return cur;
        }

        protected override long Next(long cur, long index) => cur + _step;
        protected override long FromIndex(long index)
        {
            Validation.AssertIndex(index, _num);
            return _beg + index * _step;
        }
    }

    private sealed class ImplU8 : Impl<ulong>
    {
        public ImplU8(long num, ulong beg, ulong step)
            : base(num, beg, step)
        {
        }

        public override IEnumerator<ulong> GetEnumerator()
        {
            var cur = _beg;
            for (long i = 0; i < _num; i++, cur += _step)
                yield return cur;
        }

        protected override ulong Next(ulong cur, long index) => cur + _step;
        protected override ulong FromIndex(long index)
        {
            Validation.AssertIndex(index, _num);
            return _beg + (ulong)index * _step;
        }
    }

    private sealed class ImplIA : Impl<Integer>
    {
        public ImplIA(long num, Integer beg, Integer step)
            : base(num, beg, step)
        {
        }

        public override IEnumerator<Integer> GetEnumerator()
        {
            var cur = _beg;
            for (long i = 0; i < _num; i++, cur += _step)
                yield return cur;
        }

        protected override Integer Next(Integer cur, long index) => cur + _step;
        protected override Integer FromIndex(long index)
        {
            Validation.AssertIndex(index, _num);
            return _beg + index * _step;
        }
    }

    private sealed class ImplR8 : Impl<double>
    {
        public ImplR8(long num, double beg, double step)
            : base(num, beg, step)
        {
        }

        public override IEnumerator<double> GetEnumerator()
        {
            // Note: this is different than the others because we want to avoid repeated
            // rounding error. For example, Sequence(5, 0.5, 0.1) should get close to correct
            // values and not get cumulative rounding error.
            yield return _beg;
            for (long i = 1; i < _num; i++)
                yield return _beg + i * _step;
        }

        /// <summary>
        /// This delegates to <see cref="FromIndex(I8)"/> for consistency. Just adding the step value
        /// could produce different results than <see cref="GetEnumerator"/> (due to rounding).
        /// </summary>
        protected override double Next(double cur, long index) => FromIndex(index);

        protected override double FromIndex(long index)
        {
            Validation.AssertIndex(index, _num);

            if (index == 0)
                return _beg;
            return _beg + index * _step;
        }
    }

    private abstract class Impl<T> : ICursorable<T>, ICanCount
        where T : struct
    {
        protected readonly long _num;
        protected readonly T _beg;
        protected readonly T _step;

        protected Impl(long num, T beg, T step)
        {
            Validation.Assert(num > 0);
            _num = num;
            _beg = beg;
            _step = step;
        }

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetCount(out long count)
        {
            count = _num;
            return true;
        }

        public long GetCount(Action callback)
        {
            Validation.BugCheckValueOrNull(callback);
            return _num;
        }

        public ICursor<T> GetCursor() => new CursorImpl(this);

        ICursor ICursorable.GetCursor() => GetCursor();

        /// <summary>
        /// Compute the next value given the current value and next index.
        /// Some subclasses may just use one or the other.
        /// </summary>
        protected abstract T Next(T cur, long index);

        /// <summary>
        /// Compute the value from the index.
        /// </summary>
        protected abstract T FromIndex(long index);

        private sealed class CursorImpl : ICursor<T>
        {
            private Impl<T> _parent;
            private T _value;
            private long _index;

            public CursorImpl(Impl<T> parent)
            {
                _parent = parent;
                _index = -1;
            }

            public long Index => _index;
            public T Value => _value;
            public T Current => _value;
            object ICursor.Value => _value;
            object IEnumerator.Current => _value;

            public void Dispose()
            {
                _parent = null;
            }

            public bool MoveNext()
            {
                var parent = _parent;
                Validation.BugCheck(parent != null);

                var inext = _index + 1;
                if (inext >= parent._num)
                    return false;

                _value = _index == -1 ? parent._beg : _parent.Next(_value, inext);
                _index = inext;
                return true;
            }

            public bool MoveTo(long index)
            {
                Validation.BugCheckParam(index >= 0, nameof(index));
                var parent = _parent;
                Validation.BugCheck(parent != null);

                if (index >= parent._num)
                    return false;

                _index = index;
                _value = parent.FromIndex(_index);
                return true;
            }

            public void Reset()
            {
                throw new InvalidOperationException();
            }
        }
    }
}

public sealed class DivGen : RexlOperationGenerator<DivFunc>
{
    public static readonly DivGen Instance = new DivGen();

    public MethodInfo MethInt { get; }
    private readonly MethodInfo _methTruncR8;

    private DivGen()
    {
        MethInt = new Func<Integer, Integer, Integer>(DivFunc.Exec).Method;
        _methTruncR8 = new Func<double, double>(Math.Truncate).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var kind = call.Type.Kind;
        if (kind == DKind.IA)
        {
            stRet = GenCall(codeGen, MethInt, sts);
            return true;
        }

        var ilw = codeGen.Writer;
        if (kind.IsFractional())
        {
            Validation.Assert(kind == DKind.R8);
            ilw
                .Div()
                .Call(_methTruncR8);
            stRet = typeof(double);
            return true;
        }

        Validation.Assert(kind == DKind.I8 || kind == DKind.U8);
        bool signed = kind == DKind.I8;

        Label labDone = default;

        // If y == 0, return 0.
        Label labNonZero = default;
        ilw
            .Dup()
            .Brtrue(ref labNonZero)
            .Pop()
            .Pop()
            .Ldc_I8(0)
            .Br(ref labDone)
            .MarkLabel(labNonZero);

        // We special case on y == -1 to avoid an arithmetic overflow exception when x is the evil value.
        // This is only necessary for ix types.
        // When y == -1, negate x.
        if (signed)
        {
            Label labNeMinus1 = default;
            ilw
                .Dup()
                .Ldc_Ix(-1, kind == DKind.I8)
                .Bne_Un(ref labNeMinus1)
                .Pop()
                .Neg()
                .Br(ref labDone)
                .MarkLabel(labNeMinus1)
                .Div();
        }
        else
            ilw.Div_Un();

        ilw.MarkLabel(labDone);
        stRet = codeGen.GetSystemType(call.Type);
        return true;
    }
}

public sealed class ModGen : RexlOperationGenerator<ModFunc>
{
    public static readonly ModGen Instance = new ModGen();

    public MethodInfo MethInt { get; }

    private ModGen()
    {
        MethInt = new Func<Integer, Integer, Integer>(ModFunc.Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var kind = call.Type.Kind;
        if (kind == DKind.IA)
        {
            stRet = GenCall(codeGen, MethInt, sts);
            return true;
        }

        var ilw = codeGen.Writer;

        Label labRem = default;
        Label labDone = default;

        if (kind.IsFractional())
        {
            Validation.Assert(kind == DKind.R8);

            // We define Mod(x,0.0) to be sign(x) * 0.0.
            Label labLe0 = default;
            Label labEq0 = default;

            ilw
                .Dup()
                .Ldc_R8(0)
                .Bne_Un(ref labRem)
                .Pop()

                .Dup() // If x > 0.0, return 0.0.
                .Ldc_R8(0)
                .Ble_Un(ref labLe0)
                .Pop()
                .Ldc_R8(0)
                .Br(ref labDone)
                .MarkLabel(labLe0)

                .Dup() // If x < 0.0, return -0.0.
                .Ldc_R8(0)
                .Bge_Un(ref labEq0)
                .Pop()
                .Ldc_R8(-0d)
                .Br(ref labDone)
                .MarkLabel(labEq0)

                // Otherwise return x*0.0 to get the sign of x.
                .Ldc_R8(0)
                .Mul()
                .Br(ref labDone)
                .MarkLabel(labRem);
        }
        else
        {
            Validation.Assert(kind == DKind.I8 || kind == DKind.U8);
            bool signed = kind == DKind.I8;

            // If -1 <= y <= 1 , return 0.
            ilw
                .Dup()
                .Ldc_I8(1);
            if (signed)
            {
                ilw
                    .Add()
                    .Ldc_I8(2);
            }
            ilw
                .Bgt_Un(ref labRem)
                .Pop()
                .Pop()
                .Ldc_I8(0)
                .Br(ref labDone)
                .MarkLabel(labRem);
        }

        if (kind == DKind.U8)
            ilw.Rem_Un();
        else
            ilw.Rem();

        ilw.MarkLabel(labDone);
        stRet = codeGen.GetSystemType(call.Type);
        return true;
    }
}

public sealed class BinGen : RexlOperationGenerator<BinFunc>
{
    public static readonly BinGen Instance = new BinGen();

    private readonly MethodInfo _methIA;
    private readonly MethodInfo _methR8;

    private BinGen()
    {
        _methIA = new Func<Integer, Integer, Integer>(BinFunc.Exec).Method;
        _methR8 = new Func<double, double, double>(BinFunc.Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var kind = call.Type.Kind;
        if (kind == DKind.IA)
        {
            stRet = GenCall(codeGen, _methIA, sts);
            return true;
        }

        // REVIEW: We don't yet have the proper logic for this case, so
        // we'll invoke the Exec method in the meantime.
        if (kind.IsFractional())
        {
            Validation.Assert(kind == DKind.R8);
            stRet = GenCall(codeGen, _methR8, sts);
            return true;
        }

        var ilw = codeGen.Writer;

        Label labDone = default;
        Label labBin = default;

        Validation.Assert(kind == DKind.I8 || kind == DKind.U8);
        bool signed = kind == DKind.I8;

        using (var loc = codeGen.AcquireLocal(codeGen.GetSystemType(call.Type)))
        {
            // If -1 <= y <= 1 , return x.
            ilw
                .Stloc(loc)
                .Ldloc(loc)
                .Ldc_I8(1);
            if (signed)
            {
                ilw
                    .Bgt(ref labBin) // If y > 1 we don't need to check for y < -1.
                    .Ldloc(loc)
                    .Ldc_I8(-1)
                    .Blt(ref labBin);
            }
            else
                ilw.Bgt_Un(ref labBin);
            ilw
                .Br(ref labDone) // x is already on the stack so we can just return.
                .MarkLabel(labBin)
                .Ldloc(loc);

            // Compute (x/y) * y.
            if (signed)
                ilw.Div();
            else
                ilw.Div_Un();
            ilw
                .Ldloc(loc)
                .Mul()
                .MarkLabel(labDone);
        }

        stRet = codeGen.GetSystemType(call.Type);
        return true;
    }
}
