// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace RexlTest;

using IE = IEnumerable<object>;
using IService = WrapCallSvcFromHostFunc.IService;
using O = Object;

internal sealed class TestGenerators : GeneratorRegistry
{
    public static readonly TestGenerators Instance = new TestGenerators();

    private TestGenerators()
        : base(BuiltinGenerators.Instance)
    {
#if WITH_ONNX
        AddParent(Microsoft.Rexl.Onnx.ModelFuncGenerators.Instance);
#endif

        Add(new CastGenFuncGen());

        Add(new WrapFuncGen());
        Add(new WrapLogFuncGen());
        Add(new WrapCallCtxFuncGen());
        Add(new WrapCallSvcFromHostFuncGen());

        // For testing various ArgTrait patterns.
        Add(new FirstNRevFuncGen());
        Add(new DblMapFuncGen());

        // For testing range scope interaction with other loop scopes.
        Add(new TestRngSeqFuncGen());

        // For wrapping a sequence as IndexedSequence<T>.
        Add(new TestWrapSeqFuncGen());

        // For testing sequence functions.
        Add(new TestWrapCollFuncGen());

        Add(new PingFuncGen());

        Add(new ThrowFuncGen());

        // For testing MultiFormOper.
        Add(new TestMultiFormFuncGen());
        Add(new TestMultiFormProcGen());
    }
}

internal sealed class CastGenFuncGen : RexlOperationGenerator<CastGenFunc>
{
    private readonly MethodInfo _meth;

    public CastGenFuncGen()
    {
        _meth = new Func<int, object>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        Type st = codeGen.GetSystemType(call.Args[0].Type);
        stRet = GenCall(codeGen, _meth.MakeGenericMethod(st), sts);
        wrap = SeqWrapKind.DontWrap;
        return true;
    }

    private static object Exec<T>(T value)
    {
        return (object)value;
    }
}

internal sealed class WrapFuncGen : RexlOperationGenerator<WrapFunc>
{
    private readonly MethodInfo _meth;

    public WrapFuncGen()
    {
        _meth = new Func<int, int>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var func = GetOper(call);

        wrap = SeqWrapKind.DontWrap;
        if (func.IsNYI)
        {
            stRet = null;
            return false;
        }

        Type st = codeGen.GetSystemType(call.Type);
        stRet = GenCall(codeGen, _meth.MakeGenericMethod(st), sts);
        return true;
    }

    private static T Exec<T>(T value)
    {
        return value;
    }
}

internal sealed class WrapLogFuncGen : RexlOperationGenerator<WrapLogFunc>
{
    /// <summary>
    /// The Log method on <see cref="ExecCtx"/>.
    /// </summary>
    private MethodInfo _methLog;

    /// <summary>
    /// The string.Format(string, object) method.
    /// </summary>
    private MethodInfo _methFormat;

    public WrapLogFuncGen()
    {
        // Get the Log method of the execution context. Unfortunately we can't use the normal delegate trick,
        // since we don't have an instance of ExecCtx.
        _methLog = typeof(ExecCtx).GetMethod("Log", new[] { typeof(int), typeof(string) }).VerifyValue();

        // Get the string.Format(string, object) method.
        _methFormat = new Func<string, object, string>(string.Format).Method;
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return true;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        string fmt = $"WrapLog<{call.Type}>: " + "{0}";

        Type st = codeGen.GetSystemType(call.Type);
        var ilw = codeGen.Writer;
        using (var loc = codeGen.AcquireLocal(typeof(object)))
        {
            ilw
                .Dup()
                .BoxOpt(st)
                .Stloc(loc);
            codeGen.GenLoadExecCtxAndId(call);
            ilw
                .Ldstr(fmt)
                .Ldloc(loc)
                .Call(_methFormat)
                .Callvirt(_methLog);
        }

        // Leave the value untouched.
        stRet = sts[0];
        wrap = SeqWrapKind.DontWrap;
        return true;
    }
}

internal sealed class WrapCallCtxFuncGen : RexlOperationGenerator<WrapCallCtxFunc>
{
    private readonly MethodInfo _meth;

    public WrapCallCtxFuncGen()
    {
        _meth = new Func<int, ExecCtx, int, int>(Exec).Method.GetGenericMethodDefinition();
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

        DType typeSeq = call.Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        var meth = _meth.MakeGenericMethod(stItem);
        stRet = GenCallCtxId(codeGen, meth, sts, call, 2);
        return true;
    }

    private static T Exec<T>(T value, ExecCtx ctx, int id0)
    {
        int id1 = id0 + 1;

        ctx.Ping();
        ctx.Ping(-1);
        ctx.Ping(int.MinValue);
        ctx.Ping(int.MaxValue);
        ctx.Ping(id0);
        ctx.Ping(id1);

        ctx.Log("Log(msg); Ping(); Ping(x) for x in [-1, int.MinValue, int.MaxValue, id0, id1]");
        ctx.Log("{0}", "Log(msg, args)");
        ctx.Log(-1, "Log(-1, msg)");
        ctx.Log(-1, "{0}", "Log(-1, msg, args)");
        ctx.Log(id0, "Log(id0, msg)");
        ctx.Log(id1, "{0}", "Log(id1, msg, args)");

        return value;
    }
}

internal sealed class WrapCallSvcFromHostFuncGen : RexlOperationGenerator<WrapCallSvcFromHostFunc>
{
    private readonly MethodInfo _meth;

    public WrapCallSvcFromHostFuncGen()
    {
        _meth = new Func<O, IService, ExecCtx, int, O>(Exec).Method.GetGenericMethodDefinition();
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

        DType typeSeq = call.Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        // REVIEW: May need a better mechanism for requesting a capability from the host.
        // The exception type being thrown here if not supported isn't ideal.
        var service = (IService)codeGen.Host;
        stRet = GenCallExtraCtxId(codeGen, _meth.MakeGenericMethod(stItem), sts, service, call);
        return true;
    }

    private static T Exec<T>(T value, IService service, ExecCtx ctx, int id)
    {
        Validation.AssertValue(service);
        Validation.AssertValue(ctx);

        service.Call(value, ctx, id);
        return value;
    }
}

internal sealed class FirstNRevFuncGen : RexlOperationGenerator<FirstNRevFunc>
{
    private readonly MethodInfo _meth;

    public FirstNRevFuncGen()
    {
        _meth = new Func<IE, Func<O, bool>, long, IE>(Exec)
            .Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        DType typeSeq = call.Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        var meth = _meth.MakeGenericMethod(stItem);
        stRet = GenCall(codeGen, meth, sts);
        return true;
    }

    public static IEnumerable<T> Exec<T>(IEnumerable<T> src, Func<T, bool> predicate, long n)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(predicate);
        if (src == null)
            return null;
        if (n <= 0)
            return Enumerable.Empty<T>();
        var res = src.Where(predicate);
        if (n < int.MaxValue)
            res = res.Take((int)n);
        return res;
    }
}

internal sealed class DblMapFuncGen : RexlOperationGenerator<DblMapFunc>
{
    public DblMapFuncGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        // Get the destination and source system types.
        DType typeItemDst = call.Args[1].Type;
        Type stItemDst = codeGen.GetSystemType(typeItemDst);

        DType typeSeq0 = call.Args[0].Type;
        DType typeItemSrc0 = typeSeq0.ItemTypeOrThis;
        DType typeSeq1 = call.Args[2].Type;
        DType typeItemSrc1 = typeSeq1.ItemTypeOrThis;

        Type stItemSrc0 = codeGen.GetSystemType(typeItemSrc0);
        Type stItemSrc1 = codeGen.GetSystemType(typeItemSrc1);

        var meth = new Func<IE, Func<O, O>, IE, Func<O, O>, IE>(Exec)
            .Method.GetGenericMethodDefinition().MakeGenericMethod(stItemSrc0, stItemSrc1, stItemDst);
        stRet = GenCall(codeGen, meth, sts);
        return true;
    }

    public static IEnumerable<TDst> Exec<TSrc0, TSrc1, TDst>(IEnumerable<TSrc0> src0, Func<TSrc0, TDst> fn0, IEnumerable<TSrc1> src1, Func<TSrc1, TDst> fn1)
    {
        Validation.AssertValueOrNull(src0);
        Validation.AssertValue(fn0);
        Validation.AssertValueOrNull(src1);
        Validation.AssertValue(fn1);

        var dst0 = src0 == null ? null : Enumerable.Select(src0, fn0);
        var dst1 = src1 == null ? null : Enumerable.Select(src1, fn1);

        if (dst0 == null)
            return dst1;
        if (dst1 == null)
            return dst0;

        return Weave(dst0, dst1);
    }

    private static IEnumerable<TDst> Weave<TDst>(IEnumerable<TDst> dst0, IEnumerable<TDst> dst1)
    {
        Validation.AssertValue(dst0);
        Validation.AssertValue(dst1);

        using var ator0 = dst0.GetEnumerator();
        using var ator1 = dst1.GetEnumerator();
        var a0 = ator0;
        var a1 = ator1;
        for (; ; )
        {
            if (a0 != null)
            {
                if (!a0.MoveNext())
                {
                    a0 = null;
                    if (a1 == null)
                        break;
                }
                else
                    yield return a0.Current;
            }
            if (a1 != null)
            {
                if (!a1.MoveNext())
                {
                    a1 = null;
                    if (a0 == null)
                        break;
                }
                else
                    yield return a1.Current;
            }
        }
    }
}

internal sealed class TestMultiFormFuncGen : TestMultiFormOperGen<TestMultiFormFunc, TestMultiFormFunc.ExecutionOper>
{
    public TestMultiFormFuncGen()
    {
    }

    protected override void GenCreateRunner<TIn, TDst>(ICodeGen codeGen, TestMultiFormFunc.ExecutionOper exec)
    {
        throw new InvalidOperationException("No runner for funcs");
    }

    protected override void GenCreateRunner<TIn, TSrc, TDst, TOut>(ICodeGen codeGen, TestMultiFormFunc.ExecutionOper exec)
    {
        throw new InvalidOperationException("No runner for funcs");
    }
}

internal abstract partial class TestMultiFormOperGen<TOper, TExec> : MultiFormOperGen<TOper, TExec, MultiFormFuncTypes>
    where TOper : TestMultiFormOper
    where TExec : TestMultiFormOper.ExecutionOper
{
    protected override void GenCoreCode(ICodeGen codeGen, TExec exec, DType typeIn, DType typeOut, Type stIn, Type stOut)
    {
        Validation.BugCheckValue(codeGen, nameof(codeGen));
        Validation.BugCheckParam(Handles(exec), nameof(exec));

        var par = GetParent(exec);

        DType typeMis;
        if (typeIn.IsRecordXxx)
        {
            Validation.BugCheck(typeIn.IsRecordReq);
            Validation.BugCheck(typeIn.TryGetNameType(TestMultiFormOper._nameMis, out typeMis));
        }
        else
            typeMis = typeIn;

        DType typeMos;
        if (typeOut.IsRecordXxx)
        {
            Validation.Assert(typeOut.IsRecordReq);
            Validation.BugCheck(typeOut.TryGetNameType(TestMultiFormOper._nameMos, out typeMos));
        }
        else
            typeMos = typeOut;

        // Because of the forms we've used:
        Validation.BugCheck(typeMis.IsTableReq || typeMis.ItemTypeOrThis.IsTableReq);
        Validation.BugCheck(typeMos.IsTableReq);

        DType typeItemMis = typeMis.ItemTypeOrThis;
        if (typeItemMis.IsTableReq)
            typeItemMis = typeItemMis.ItemTypeOrThis;
        DType typeItemMos = typeMos.ItemTypeOrThis;

        Type stMis = codeGen.GetSystemType(typeMis);
        Type stItemMis = codeGen.GetSystemType(typeItemMis);
        Type stItemOut = codeGen.GetSystemType(typeItemMos);
        Validation.BugCheck(codeGen.TypeManager.TryEnsureDefaultValue(typeItemMos, out var entryDef));
        Validation.Assert(entryDef.value != null && stItemOut.IsAssignableFrom(entryDef.value.GetType()));

        var types = exec.Form.Cookie;

        int cookie = codeGen.StartFunction("mis_item_to_mos_item", stItemOut, typeof(string), typeof(long), stItemMis);
        {
            // Create the output record object.
            var rg = codeGen.CreateRecordGenerator(typeItemMos);

            // Prepare to set the field.
            rg.SetFromStackPre(TestMultiFormOper._nameB, TestMultiFormOper._typeBBase.ToSequence());

            var ilw = codeGen.Writer;
            ilw
                // Load the suffix and cch.
                .Ldarg(1)
            .Ldarg(2)
            // Load the field(s).
                .Ldstr(TestMultiFormOper._nameA.Value)
                .Ldarg(3);
            codeGen.GenLoadField(typeItemMis, stItemMis, TestMultiFormOper._nameA, types.TypeA);
            if (types.TypeA.IsSequence)
                ilw.Call(new Func<IEnumerable<string>, string>(Concat).Method);

            if (!typeItemMis.Contains(TestMultiFormOper._nameB) && !typeItemMis.Contains(TestMultiFormOper._nameC))
            {
                Validation.BugCheck(codeGen.GetSystemType(types.TypeA.ItemTypeOrThis) == typeof(string));
                ilw.Call(new Func<string, long, string, string, IEnumerable<string>>(HandleOne).Method);
            }
            else
            {
                if (typeItemMis.Contains(TestMultiFormOper._nameB))
                {
                    ilw
                        .Ldstr(TestMultiFormOper._nameB.Value)
                        .Ldarg(3);
                    codeGen.GenLoadField(typeItemMis, stItemMis, TestMultiFormOper._nameB, types.TypeB);
                    if (types.TypeB.IsSequence)
                        ilw.Call(new Func<IEnumerable<string>, string>(Concat).Method);
                }

                if (typeItemMis.Contains(TestMultiFormOper._nameC))
                {
                    Type stC = codeGen.GetSystemType(types.TypeC);
                    ilw
                        .Ldstr(TestMultiFormOper._nameC.Value)
                        .Ldarg(3);
                    codeGen.GenLoadField(typeItemMis, stItemMis, TestMultiFormOper._nameC, types.TypeC);
                    var labNull = ilw.DefineLabel();
                    var labDone = ilw.DefineLabel();
                    ilw
                        .Dup()
                        .Brfalse(ref labNull)
                        .Call(new Func<IE, int>(Enumerable.Count)
                            .GetMethodInfo()
                        // ** REVIEW: For some reason, things fail at runtime if we do this
                        // ** (entry point not found). Doesn't make sense!
                        //.GetGenericMethodDefinition()
                        //.MakeGenericMethod(stC)
                        )
                        .Br_Non(labDone)
                        .MarkLabel(labNull)
                        .Pop()
                        .Ldc_I8(0)
                        .MarkLabel(labDone);
                    using var loc = codeGen.AcquireLocal(typeof(long));
                    ilw
                        .Stloc(loc)
                        .Ldloca(loc)
                        .CallVirtAsNonVirt(typeof(long).GetMethod("ToString", new Type[] { }));
                }

                if (typeItemMis.Contains(TestMultiFormOper._nameB) && typeItemMis.Contains(TestMultiFormOper._nameC))
                    ilw.Call(new Func<string, string, string>(string.Concat).Method);

                Validation.BugCheck(codeGen.GetSystemType(types.TypeA.ItemTypeOrThis) == typeof(string));
                Validation.BugCheck(codeGen.GetSystemType(types.TypeB.ItemTypeOrThis) == typeof(string));
                ilw.Call(new Func<string, long, string, string, string, string, IEnumerable<string>>(HandleTwo).Method);
            }
            codeGen.GenSequenceWrap(typeof(IEnumerable<string>), typeof(string));

            rg.SetFromStackPost();
            rg.Finish();
        }
        (Type stDel, Delegate fnItem) = codeGen.EndFunction(cookie);

        Validation.Assert(typeof(Func<,,,>).MakeGenericType(typeof(string), typeof(long), stItemMis, stItemOut) == stDel);
        Validation.Assert(stDel.IsAssignableFrom(fnItem.GetType()));

        // Note that the IL produced here isn't "optimal" in all cases, but we don't care for this test function.
        using (var locMis = codeGen.AcquireLocal(stMis))
        using (var locS1 = codeGen.AcquireLocal(typeof(string)))
        using (var locS2 = codeGen.AcquireLocal(typeof(long)))
        {
            var ilw = codeGen.Writer;

            // Put mis, S1, and S2 values in locals. Note that "in" is on the stack.
            if (!typeIn.IsRecordXxx)
            {
                ilw
                    .Stloc(locMis)
                    .Ldstr("")
                    .Stloc(locS1)
                    .Ldc_I8(long.MaxValue)
                    .Stloc(locS2);
            }
            else
            {
                Validation.BugCheck(codeGen.GetSystemType(types.TypeS1.ItemTypeOrThis) == typeof(string));
                Validation.BugCheck(codeGen.GetSystemType(types.TypeS2.ItemTypeOrThis) == typeof(long));

                ilw.Dup();
                codeGen.GenLoadField(typeIn, stIn, TestMultiFormOper._nameMis, typeMis);
                ilw
                    .Stloc(locMis)
                    .Dup();

                if (typeIn.Contains(TestMultiFormOper._nameS1))
                {
                    codeGen.GenLoadField(typeIn, stIn, TestMultiFormOper._nameS1, types.TypeS1);
                    if (types.TypeS1.IsSequence)
                        ilw.Call(new Func<IEnumerable<string>, string>(Concat).Method);
                    ilw.Stloc(locS1);
                }
                else
                    ilw.Ldstr("").Stloc(locS1);

                if (typeIn.Contains(TestMultiFormOper._nameS2))
                {
                    codeGen.GenLoadField(typeIn, stIn, TestMultiFormOper._nameS2, types.TypeS2);
                    if (types.TypeS2.IsSequence)
                        ilw.Call(new Func<IEnumerable<long>, long>(Sum).Method);
                    ilw.Stloc(locS2);
                }
                else
                    ilw.Ldc_I8(long.MaxValue).Stloc(locS2);
            }

            using var rg = typeOut.IsRecordXxx ? codeGen.CreateRecordGenerator(typeOut.ToReq()) : null;

            // Prepare to wrap the result in the out record.
            if (rg != null)
                rg.SetFromStackPre(TestMultiFormOper._nameMos, typeMos);

            // Invoke Exec to map over mis to produce mos.
            ilw.Ldloc(locMis);
            if (typeMis.ItemTypeOrThis.IsTableReq)
                ilw.Call(new Func<IEnumerable<IE>, IE>(Flatten).Method.GetGenericMethodDefinition().MakeGenericMethod(stItemMis));
            ilw
                .Ldloc(locS1)
                .Ldloc(locS2);
            codeGen.GenLoadConst(fnItem, stDel);

            MethodInfo meth;
            if (exec.Form.MosKind.IsOneMany())
                meth = new Func<IE, string, long, Func<string, long, O, O>, IE>(ExecOneMany).Method;
            else
            {
                codeGen.GenLoadConst(entryDef.value, stItemOut);
                meth = new Func<IE, string, long, Func<string, long, O, O>, O, IE>(Exec).Method;
            }
            meth = meth.GetGenericMethodDefinition().MakeGenericMethod(stItemMis, stItemOut);
            ilw.Call(meth);
            if (!exec.Merges)
                codeGen.GenSequenceWrap(meth.ReturnType, stItemOut);

            if (rg != null)
            {
                // Wrap the mos and settings in the out record.
                rg.SetFromStackPost();

                rg.SetFromStackPre(TestMultiFormOper._nameS1, types.TypeS1);
                ilw.Ldloc(locS1);
                rg.SetFromStackPost();

                rg.SetFromStackPre(TestMultiFormOper._nameS2, types.TypeS2);
                ilw.Ldloc(locS2);
                rg.SetFromStackPost();

                rg.Finish();
            }
        }
    }

    protected override void GenMergeItemCore(ICodeGen codeGen, TestMultiFormOper.MergeInfo merge, DType typeSrc, DType typeMos, DType typeMrg, Type stSrc, Type stMos, Type stMrg, bool mosFirst)
    {
        Validation.Assert(false);
        throw new InvalidOperationException("Unexpected kind of merging");
    }

    public static string Concat(IEnumerable<string> ie)
    {
        if (ie == null)
            return "";
        return string.Join(", ", ie);
    }

    public static long Sum(IEnumerable<long> ie)
    {
        if (ie == null)
            return 0;
        return Enumerable.Sum(ie);
    }

    public static IEnumerable<T> Flatten<T>(IEnumerable<IEnumerable<T>> ies)
    {
        if (ies == null)
            return null;
        return ies.SelectMany(x => x);
    }

    public static IEnumerable<TItemMos> Exec<TItemMis, TItemMos>(
        IEnumerable<TItemMis> mis, string suffix, long cch,
        Func<string, long, TItemMis, TItemMos> fn, TItemMos def)
    {
        if (mis == null)
            return null;

        var src = (mis as TItemMis[]) ?? mis.ToArray();
        var bldr = IndexedSequence<TItemMos>.Builder.Create(def, out var seq);
        var rems = new[] { 1, 3, 0, 2 };
        var task = Task.Run(() =>
        {
            // Add items to the builder in some contrived order....
            int len = src.Length;
            try
            {
                foreach (int rem in rems)
                {
                    for (int i = rem; i < len; i += rems.Length)
                        bldr.Add(i, fn(suffix, cch, src[i]));
                }
            }
            catch (Exception e)
            {
                bldr.Quit(e);
                return;
            }

            bldr.Done(len);
        });

        return seq;
    }

    public static IEnumerable<TItemMos> ExecOneMany<TItemMis, TItemMos>(
        IEnumerable<TItemMis> mis, string suffix, long cch,
        Func<string, long, TItemMis, TItemMos> fn)
    {
        if (mis == null)
            return null;

        var src = (mis as TItemMis[]) ?? mis.ToArray();
        var bldr = FlatteningSequence<TItemMos>.CreateBuilder(out var seq);
        var rems = new[] { 1, 3, 0, 2 };
        var task = Task.Run(() =>
        {
            // Add items to the builder in some contrived order and omit some.
            int len = src.Length;
            try
            {
                foreach (int rem in rems)
                {
                    // Arbitrarily remove items with index 0 mod the length to get
                    // empty outputs.
                    if (rem == 0)
                        continue;
                    for (int i = rem; i < len; i += rems.Length)
                    {
                        // Repeat the item proportionally to the index.
                        var item = fn(suffix, cch, src[i]);
                        var group = Immutable.Array<TItemMos>.Create(Enumerable.Repeat(item, i + 1));
                        bldr.Add(i, group);
                    }
                }
            }
            catch (Exception e)
            {
                bldr.Quit(e);
                return;
            }

            bldr.Done(len);
        });

        return seq;
    }

    public static string HandleStr(string suf, long cch, string name, string a)
    {
        if (a == null)
            return name + ": <null>" + suf;
        if (cch < a.Length)
            a = a.Substring(0, (int)cch);
        return name + ": [" + a + suf + "]";
    }

    public static IEnumerable<string> HandleOne(string suf, long cch, string name, string a)
    {
        yield return HandleStr(suf, cch, name, a);
    }

    public static IEnumerable<string> HandleTwo(string suf, long cch, string name0, string a, string name1, string b)
    {
        yield return HandleStr(suf, cch, name0, a);
        yield return HandleStr(suf, cch, name1, b);
    }
}

internal sealed class TestWrapSeqFuncGen : RexlOperationGenerator<TestWrapSeqFunc>
{
    private readonly MethodInfo _methNoop;
    private readonly MethodInfo _methSeq;

    public TestWrapSeqFuncGen()
    {
        _methNoop = new Func<O, O>(ExecNoop).Method.GetGenericMethodDefinition();
        _methSeq = new Func<IE, O, IE>(ExecSeq).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        DType type = call.Type;
        DType typeItem = type.ItemTypeOrThis;
        var st = codeGen.GetSystemType(type);
        var stItem = codeGen.GetSystemType(typeItem);

        // If the type manager uses some more specific type than IEnumerable<T>, such as T[], we can't wrap.
        if (!st.IsAssignableFrom(typeof(IEnumerable<>).MakeGenericType(stItem)))
        {
            stRet = GenCall(codeGen, _methNoop.MakeGenericMethod(st), sts);
            return true;
        }

        var meth = _methSeq.MakeGenericMethod(stItem);
        stRet = GenCallDefault(codeGen, meth, sts, typeItem);
        return true;
    }

    private static T ExecNoop<T>(T value)
    {
        return value;
    }

    private static IEnumerable<T> ExecSeq<T>(IEnumerable<T> value, T valDef)
    {
        if (value == null)
            return null;

        var arr = (value as T[]) ?? value.ToArray();
        var rems = new[] { 3, 1, 4, 0, 2 };
        var bldr = IndexedSequence<T>.Builder.Create((T)valDef, out var seq);

        // REVIEW: We used to wrap the following block in a task and
        // let it run asynchronously. Unfortunately that could make ping counts
        // non-deterministic, making test output volatile.
        {
            // Add items to the builder in some contrived order, and skip some....
            int len = arr.Length;
            try
            {
                foreach (int rem in rems)
                {
                    for (int i = rem; i < len; i += rems.Length)
                        bldr.Add(i, i == 4 ? valDef : arr[i]);
                }
            }
            catch (Exception e)
            {
                bldr.Quit(e);
            }

            bldr.Done(len);
        }

        return seq;
    }
}

internal sealed class TestWrapCollFuncGen : RexlOperationGenerator<TestWrapCollFunc>
{
    public TestWrapCollFuncGen()
    {
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        DType typeSeq = call.Type;
        DType typeItem = typeSeq.ItemTypeOrThis;
        Type stItem = codeGen.GetSystemType(typeItem);

        MethodInfo meth;
        if (call.Oper == TestWrapCollFunc.CantCount)
            meth = new Func<IE, IE>(ExecCantCount).Method;
        else if (call.Oper == TestWrapCollFunc.LazyCount)
            meth = new Func<IE, IE>(ExecLazyCount).Method;
        else if (call.Oper == TestWrapCollFunc.WrapList)
            meth = new Func<IE, IE>(ExecWrapList).Method;
        else if (call.Oper == TestWrapCollFunc.WrapColl)
            meth = new Func<IE, IE>(ExecWrapColl).Method;
        else if (call.Oper == TestWrapCollFunc.WrapArr)
            meth = new Func<IE, IE>(ExecWrapArr).Method;
        else
        {
            Validation.Assert(call.Oper == TestWrapCollFunc.WrapCurs);
            meth = new Func<IE, IE>(ExecWrapCurs).Method;
        }

        meth = meth.GetGenericMethodDefinition().MakeGenericMethod(stItem);
        stRet = GenCall(codeGen, meth, sts);
        wrap = SeqWrapKind.DontWrap;
        return true;
    }

    private static IEnumerable<T> ExecCantCount<T>(IEnumerable<T> src)
    {
        if (src == null)
            return null;
        return new CantCountImpl<T>(src);
    }

    private static IEnumerable<T> ExecLazyCount<T>(IEnumerable<T> src)
    {
        if (src == null)
            return null;
        return new LazyCountImpl<T>(src);
    }

    private static IEnumerable<T> ExecWrapList<T>(IEnumerable<T> src)
    {
        if (src == null)
            return null;
        return src.ToList();
    }

    private static IEnumerable<T> ExecWrapColl<T>(IEnumerable<T> src)
    {
        if (src == null)
            return null;
        return new CollectionImpl<T>(src);
    }

    private static IEnumerable<T> ExecWrapArr<T>(IEnumerable<T> src)
    {
        if (src == null)
            return null;
        return src.ToArray();
    }

    private static IEnumerable<T> ExecWrapCurs<T>(IEnumerable<T> src)
    {
        if (src == null)
            return null;
        return new CursorableImpl<T>(src);
    }

    private sealed class CantCountImpl<T> : ICachingEnumerable<T>
    {
        private readonly IEnumerable<T> _src;

        public CantCountImpl(IEnumerable<T> src)
        {
            _src = src;
        }

        public IEnumerator<T> GetEnumerator() => _src.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class LazyCountImpl<T> : ICanCount, IEnumerable<T>
    {
        private readonly List<T> _list;

        public LazyCountImpl(IEnumerable<T> src)
        {
            _list = src.ToList();
        }

        public long GetCount(Action callback)
        {
            Validation.BugCheckValueOrNull(callback);
            return _list.Count;
        }

        public bool TryGetCount(out long count)
        {
            count = -1;
            return false;
        }

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CollectionImpl<T> : IReadOnlyCollection<T>
    {
        private readonly List<T> _list;

        public CollectionImpl(IEnumerable<T> src)
        {
            _list = src.ToList();
        }

        public int Count => _list.Count;

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CursorableImpl<T> : ICursorable<T>
    {
        private readonly IEnumerable<T> _src;

        public CursorableImpl(IEnumerable<T> src)
        {
            _src = src;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<T> GetEnumerator() => _src.GetEnumerator();

        public ICursor<T> GetCursor() => new CursorImpl(_src);

        ICursor ICursorable.GetCursor() => GetCursor();

        private sealed class CursorImpl : ICursor<T>
        {
            private readonly List<T> _list;

            private T _value;
            private long _index;

            object IEnumerator.Current => _value;

            public T Current => _value;

            public T Value => _value;

            public long Index => _index;

            object ICursor.Value => _value;

            public CursorImpl(IEnumerable<T> src)
            {
                _list = src.ToList();
            }

            public bool MoveTo(long index)
            {
                Validation.BugCheckParam(index >= 0, nameof(index));
                if (!Validation.IsValidIndex(index, _list.Count))
                    return false;

                _index = index;
                _value = _list[(int)index];
                return true;
            }

            public bool MoveTo(long index, Action? callback) => MoveTo(index);

            public bool MoveNext() => MoveTo(_index + 1);

            public void Reset() => throw new InvalidOperationException();

            public void Dispose()
            {
            }
        }
    }
}

internal sealed class TestRngSeqFuncGen : RexlOperationGenerator<TestRngSeqFunc>
{
    private readonly MethodInfo _methInd;
    private readonly MethodInfo _methNon;

    public TestRngSeqFuncGen()
    {
        _methInd = new Func<long, IE, long, Func<long, long, O, long, O>, ExecCtx, int, IE>(ExecInd).Method.GetGenericMethodDefinition();
        _methNon = new Func<long, IE, long, Func<long, O, long, O>, ExecCtx, int, IE>(Exec).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(call);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(call.Args.Length == sts.Length);

        var stSrc = codeGen.GetSystemType(call.Args[1].Type.ItemTypeOrThis);
        var stDst = codeGen.GetSystemType(call.Args[3].Type);
        bool indexed = call.Indices[0] != null;

        var meth = (indexed ? _methInd : _methNon).MakeGenericMethod(stSrc, stDst);
        stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    private static IEnumerable<TDst> Exec<TSrc, TDst>(long rng0, IEnumerable<TSrc> seq, long rng1,
        Func<long, TSrc, long, TDst> fn, ExecCtx ctx, int id)
    {
        if (seq == null)
            yield break;

        for (long r0 = 0; r0 < rng0; r0++)
        {
            foreach (var item in seq)
            {
                for (long r1 = 0; r1 < rng1; r1++)
                {
                    ctx.Ping(id);
                    yield return fn(r0, item, r1);
                }
            }
        }
    }

    private static IEnumerable<TDst> ExecInd<TSrc, TDst>(long rng0, IEnumerable<TSrc> seq, long rng1,
        Func<long, long, TSrc, long, TDst> fn, ExecCtx ctx, int id)
    {
        if (seq == null)
            yield break;

        for (long r0 = 0; r0 < rng0; r0++)
        {
            long iseq = 0;
            foreach (var item in seq)
            {
                for (long r1 = 0; r1 < rng1; r1++)
                {
                    ctx.Ping(id);
                    yield return fn(r0, iseq, item, r1);
                }
                iseq++;
            }
        }
    }
}

internal sealed class PingFuncGen : RexlOperationGenerator<PingFunc>
{
    private readonly MethodInfo _meth;

    public PingFuncGen()
    {
        _meth = new Func<ExecCtx, long>(Exec).Method;
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return true;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call,
        ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        stRet = GenCallCtx(codeGen, _meth, sts);
        return true;
    }

    private static long Exec(ExecCtx ctx)
    {
        Validation.AssertValue(ctx);
        ctx.Ping();

        if (ctx is TotalPingsExecCtx tpec)
            return tpec.PingCount;
        if (ctx is PingsPerIdExecCtx ppiec)
            return ppiec.PingCountNoId;
        return -1;
    }
}

internal sealed class ThrowFuncGen : RexlOperationGenerator<ThrowFunc>
{
    private readonly MethodInfo _meth;

    public ThrowFuncGen()
    {
        _meth = new Func<object>(Exec<object>).Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var meth = _meth.MakeGenericMethod(codeGen.GetSystemType(call.Type));
        stRet = GenCall(codeGen, meth, sts);
        return true;
    }

    private static T Exec<T>()
    {
        throw new RexlThrowException();
    }

    private class RexlThrowException : Exception
    {
        public RexlThrowException()
            : base()
        {
        }
    }
}
