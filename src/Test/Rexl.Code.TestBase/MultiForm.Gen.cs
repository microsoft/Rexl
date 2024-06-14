// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace RexlTest;

using IE = IEnumerable<object>;
using O = Object;

public sealed class MultiFormGenerators : GeneratorRegistry
{
    public static readonly MultiFormGenerators Instance = new MultiFormGenerators();

    private MultiFormGenerators()
    {
        // For testing MultiFormOper.
        Add(new TestMultiFormFuncGen());
        Add(new TestMultiFormProcGen());
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
