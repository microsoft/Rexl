// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

partial class MultiFormOperGen<TOper, TExec, TCookie>
{
    /// <summary>
    /// This is for code gen of the exec operation, both core operation and merging. Note that
    /// if the exec is a procedure, this doesn't handle creating the action runner. That is
    /// done by the caller.
    /// 
    /// If <paramref name="asTup"/> is true, then the generated code will also return the
    /// core operation's output, from before merging and record creation. This should only
    /// be enabled when <paramref name="exec"/> is a merging operation. In that case, the
    /// two values are returned as a ValueTuple (final value, core output).
    /// 
    /// REVIEW: With a bit of work in the code generator we can remove some extraneous
    /// locals created before entering this code.
    /// </summary>
    protected bool TryGenExec(TExec exec, ICodeGen codeGen,
        BndCallNode call, ReadOnly.Array<Type> sts, bool asTup, out Type stRet)
    {
        Validation.AssertValue(exec);
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(call.Oper == exec);
        Validation.Assert(call.Type == exec.TypeCall);
        Validation.Assert(call.Args.Length == exec.Arity);
        Validation.Assert(!asTup || exec.Merges);

        // Check that the types match.
        Validation.Assert(exec.ArgTypes.Zip(call.Args, (t, a) => (t, a)).All(x => x.t == x.a.Type));

        DType typeIn = exec.TypeIn;
        DType typeOut = exec.TypeOut;

        // Get the core function.
        var typeManager = codeGen.TypeManager;
        if (!typeManager.TryEnsureSysType(typeIn, out Type stIn) ||
            !typeManager.TryEnsureSysType(typeOut, out Type stOut))
        {
            stRet = null;
            return false;
        }

        if (!exec.Merges)
        {
            Validation.Assert(exec.TypeDst == typeOut);
            Validation.Assert(exec.Arity == 1);
            Validation.Assert(typeIn == call.Args[0].Type);
            GenCoreCode(codeGen, exec, typeIn, typeOut, stIn, stOut);
            stRet = stOut;
            return true;
        }

        var merge = exec.MergeInfo;
        Validation.Assert(merge != null);
        Validation.Assert(exec.Arity == 3);
        Validation.Assert(exec.TypeSrc == call.Args[0].Type);
        Validation.Assert(typeIn == call.Args[1].Type);
        Validation.Assert(exec.TypeSrc == call.Args[2].Type);
        Validation.Assert(exec.Form.HasMis & exec.Form.HasMos);
        Validation.Assert(merge.TypeSrc == exec.TypeSrc);

        DType typeSrc = exec.TypeSrc;
        DType typeMos = exec.Form.TypeMos;
        DType typeMrg = merge.TypeMrg;
        DType typeDst = merge.TypeDst;
        Validation.Assert(typeSrc.IsSequence);
        Validation.Assert(typeMos.IsSequence);
        Validation.Assert(typeMrg.IsSequence);

        DType typeItemSrc = typeSrc.ItemTypeOrThis;
        DType typeItemMos = typeMos.ItemTypeOrThis;
        DType typeItemMrg = typeMrg.ItemTypeOrThis;

        if (!typeManager.TryEnsureSysType(typeSrc, out Type stSrc) ||
            !typeManager.TryEnsureSysType(typeMrg, out Type stMrg) ||
            !typeManager.TryEnsureSysType(typeDst, out Type stDst) ||
            !typeManager.TryEnsureSysType(typeItemSrc, out Type stItemSrc) ||
            !typeManager.TryEnsureSysType(typeItemMos, out Type stItemMos) ||
            !typeManager.TryEnsureSysType(typeItemMrg, out Type stItemMrg))
        {
            stRet = null;
            return false;
        }

        object valDef = typeManager.TryEnsureDefaultValue(typeItemMrg, out var entryMrg) ? entryMrg.value : null;
        object valDefMos = typeManager.TryEnsureDefaultValue(typeItemMos, out var entryMos) ? entryMos.value : null;

        // Generate the merge item delegate. Note that this should NOT change the current MethodGenerator at all.
        // We pass it the full ICodeGen for its convenience and so IL logging applies.

        // We make mosFirst symbolic and pass it to the GetMergeItemDelegate function so we don't have to change
        // the API if we decide to swap the arg order for the merge function. Note however that code below will
        // need to change, namely, where there is an Assert(mosFirst) or Assert(!mosFirst).
        const bool mosFirst = true;

        var genCur = codeGen.Generator;
        int posCur = genCur.Il.Position;
        Delegate fnMerge = GetMergeItemDelegate(codeGen, merge, typeItemSrc, typeItemMos, typeItemMrg,
            stItemSrc, stItemMos, stItemMrg, mosFirst);
        Validation.BugCheck(genCur == codeGen.Generator);
        Validation.BugCheck(posCur == genCur.Il.Position);

        if (fnMerge == null)
        {
            stRet = null;
            return false;
        }

        // Validate fnMerge.
        Validation.Assert(mosFirst);
        Type stMergeDel = typeof(Func<,,>).MakeGenericType(stItemMos, stItemSrc, stItemMrg);
        Validation.BugCheck(stMergeDel.IsAssignableFrom(fnMerge.GetType()));

        if (exec.Merges)
        {
            // REVIEW: It would be better to call a virtual for this information, but this is sufficient
            // for now.
            bool dstIsMrg = typeDst == typeMrg;
            bool outIsMos = typeOut == typeMos;
            Validation.BugCheck(dstIsMrg || !outIsMos);

            var gen = codeGen.Generator;
            using var locSrc = gen.AcquireLocal(stSrc);

            Label labPopLdNull = default;

            // Store src in a local.
            gen.Il.Stloc(locSrc);

            // If src is null then so is the merged sequence, so
            // we can directly load null and skip other processing.
            // This is only necessary in the proc case. The func case
            // should be protected by a Guard from reduction.
            if (exec.IsProc && exec.MergeInfo.TypeMrg == exec.MergeInfo.TypeDst)
                gen.Il.Ldloc(locSrc).Brfalse(ref labPopLdNull);

            // out = fnCore(in).
            GenCoreCode(codeGen, exec, typeIn, typeOut, stIn, stOut);
            Validation.BugCheck(gen == codeGen.Generator);

            using var locOut = asTup || !outIsMos ? gen.AcquireLocal(stOut) : default;
            if (locOut.IsActive)
            {
                // Store out in a local and load mos from out.
                gen.Il
                    .Dup()
                    .Stloc(locOut);
                GenGetMosFromOut(codeGen, exec, typeOut, typeMos, stOut, stItemMos);
                Validation.BugCheck(gen == codeGen.Generator);
            }

            // Mos is on the stack. Replace it with mrg.
            Label labPostMrg = default;
            {
                // If either mos or src are null, then mrg is null. In theory, their nullness should match,
                // but we have no way to enforce that.
                Label labMakeMrg = default;
                gen.Il
                    .Dup()
                    .Brfalse(ref labPopLdNull)
                    .Ldloc(locSrc)
                    .Dup()
                    .Brtrue(ref labMakeMrg)
                    .Pop()
                    .MarkLabel(labPopLdNull)
                    .Pop()
                    .Ldnull()
                    .Br(ref labPostMrg)
                    .MarkLabel(labMakeMrg);
                locSrc.Dispose();
            }

            Validation.Assert(mosFirst);
            codeGen.GenLoadConst(fnMerge, stMergeDel);

            MethodInfo meth;
            if (exec.Form.MosKind.IsOneMany())
            {
                // mrg = Merge(mos, src, fnMerge, valDefMos, useDef)
                codeGen.GenLoadConst(entryMos.value, stItemMos);
                gen.Il.Ldc_B(exec.Form.MosKind == MosKind.OneManyOuter);
                meth = _methMergeOneMany.MakeGenericMethod(stItemMos, stItemSrc, stItemMrg);
            }
            else
            {
                // mrg = Merge(mos, src, fnMerge, valDef)
                codeGen.GenLoadConst(entryMrg.value, stItemMrg);
                meth = _methMerge.MakeGenericMethod(stItemMos, stItemSrc, stItemMrg);
            }

            gen.Il.Call(meth);
            stRet = codeGen.GenSequenceWrap(meth.ReturnType, stItemMrg);

            gen.Il.MarkLabel(labPostMrg);

            if (!dstIsMrg)
            {
                Validation.Assert(!outIsMos);
                using var locMrg = gen.AcquireLocal(stMrg);
                gen.Il.Stloc(locMrg);
                GenDstFromOutAndMrg(codeGen, exec, locOut, locMrg, typeOut, typeMrg, typeDst, stOut, stMrg, stDst);
                stRet = stDst;
            }

            if (asTup)
            {
                Validation.Assert(locOut.IsActive);
                var methTup = _methCreateTup.MakeGenericMethod(stDst, stOut);
                gen.Il
                    .Ldloc(locOut)
                    .Call(methTup);
                stRet = methTup.ReturnType;
            }

            // Should be in the same function.
            Validation.BugCheck(gen == codeGen.Generator);

            return true;
        }

        stRet = null;
        return false;
    }

    /// <summary>
    /// This is for code gen to extract the mos from the out value.
    /// The out value is on the stack. This should leave the mos value on the stack.
    /// </summary>
    protected virtual void GenGetMosFromOut(ICodeGen codeGen, TExec exec, DType typeOut, DType typeMos, Type stOut, Type stMos)
    {
        Validation.Assert(exec.Form.HasMos);

        // The default implementation handles when typeOut is a record and the mos is a field of the record.
        if (typeOut == typeMos)
            return;

        DName nameMos = exec.Form.NameMos;
        Validation.BugCheck(typeOut.IsRecordReq);
        Validation.BugCheck(typeOut.TryGetNameType(nameMos, out DType typeFld));
        Validation.BugCheck(typeFld == typeMos);

        codeGen.GenLoadField(typeOut, stOut, nameMos, typeMos);
    }

    /// <summary>
    /// This is for code gen to create the dst from the out and mrg values.
    /// The out and mrg values are in the given locals. This should leave the dst value on the stack.
    /// </summary>
    protected virtual void GenDstFromOutAndMrg(ICodeGen codeGen, TExec exec, MethodGenerator.Local locOut, MethodGenerator.Local locMrg,
        DType typeOut, DType typeMrg, DType typeDst, Type stOut, Type stMrg, Type stDst)
    {
        Validation.Assert(exec.Form.HasMos);

        if (typeDst == typeMrg)
        {
            codeGen.Writer.Ldloc(locMrg);
            return;
        }

        // The default implementation handles when typeOut and typeDst are records and the mos/mrg is a field of those records.
        // The mos and mrg fields have the same name, but generally different types.
        DName nameMos = exec.Form.NameMos;
        Validation.BugCheck(typeOut.IsRecordReq);
        Validation.BugCheck(typeDst.IsRecordReq);
        Validation.BugCheck(typeDst.TryGetNameType(nameMos, out DType typeFld));
        Validation.BugCheck(typeFld == typeMrg);

        using var rg = codeGen.CreateRecordGenerator(typeDst);
        foreach (var tn in typeDst.GetNames())
        {
            if (tn.Name == nameMos)
                continue;

            // We currently require the types to match exactly, no promotion.
            Validation.BugCheck(typeOut.TryGetNameType(tn.Name, out typeFld) && typeFld == tn.Type);

            rg.SetFromStackPre(tn.Name, tn.Type);
            codeGen.Writer.Ldloc(locOut);
            codeGen.GenLoadField(typeOut, stOut, tn.Name, tn.Type);
            rg.SetFromStackPost();
        }

        rg.SetFromStackPre(nameMos, typeMrg);
        codeGen.Writer.Ldloc(locMrg);
        rg.SetFromStackPost();

        rg.Finish();
    }

    /// <summary>
    /// Since merging is a per-item operation, that is, it uses Zip, we use a delegate for processing
    /// a single item. The type parameters are the item types, not sequence types.
    /// 
    /// The base implementation uses the StartFunction/EndFunction functionality of <paramref name="codeGen"/>,
    /// invoking <see cref="GenMergeItemCore(ICodeGen, DType, DType, DType, Type, Type, Type)"/> to fill in
    /// the details.
    /// </summary>
    public virtual Delegate GetMergeItemDelegate(ICodeGen codeGen, MultiFormOper<TCookie>.MergeInfo merge,
        DType typeSrc, DType typeMos, DType typeMrg,
        Type stSrc, Type stMos, Type stMrg, bool mosFirst = true)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(typeSrc.ToSequence() == merge.TypeSrc);
        Validation.Assert(typeMrg.ToSequence() == merge.TypeMrg);
        Validation.Assert(codeGen.TypeManager.GetSysTypeOrNull(typeSrc) == stSrc);
        Validation.Assert(codeGen.TypeManager.GetSysTypeOrNull(typeMos) == stMos);
        Validation.Assert(codeGen.TypeManager.GetSysTypeOrNull(typeMrg) == stMrg);

        Type st1 = mosFirst ? stMos : stSrc;
        Type st2 = mosFirst ? stSrc : stMos;
        int cookie = codeGen.StartFunction("merge", stMrg, st1, st2);

        var gen = codeGen.Generator;
        int pos = gen.Il.Position;
        GenMergeItem(codeGen, merge, typeSrc, typeMos, typeMrg, stSrc, stMos, stMrg, mosFirst);
        Validation.Assert(gen == codeGen.Generator);
        Validation.Assert(pos < gen.Il.Position);

        (Type stDel, Delegate fn) = codeGen.EndFunction(cookie);
        Validation.Assert(fn != null);
        Validation.Assert(stDel == typeof(Func<,,>).MakeGenericType(st1, st2, stMrg));
        Validation.Assert(stDel.IsAssignableFrom(fn.GetType()));

        return fn;
    }

    /// <summary>
    /// Generate the code for item merging. Assumes the current <see cref="MethodGenerator"/> of
    /// <paramref name="codeGen"/> has the src item in arg 1 and mos item in arg2. Arg 0 is for "constants",
    /// should any be needed.
    /// </summary>
    protected virtual void GenMergeItem(ICodeGen codeGen,
        MultiFormOper<TCookie>.MergeInfo merge,
        DType typeSrc, DType typeMos, DType typeMrg,
        Type stSrc, Type stMos, Type stMrg, bool mosFirst)
    {
        Validation.AssertValue(codeGen);
        Validation.AssertValue(merge);

        if (merge is MultiFormOper<TCookie>.RecordMergeInfo rec)
            GenMergeItemRec(codeGen, rec, typeSrc, typeMos, typeMrg, stSrc, stMos, stMrg, mosFirst);
        else
            GenMergeItemCore(codeGen, merge, typeSrc, typeMos, typeMrg, stSrc, stMos, stMrg, mosFirst);
    }

    protected abstract void GenMergeItemCore(ICodeGen codeGen,
        MultiFormOper<TCookie>.MergeInfo merge,
        DType typeSrc, DType typeMos, DType typeMrg,
        Type stSrc, Type stMos, Type stMrg, bool mosFirst);

    protected virtual void GenMergeItemRec(ICodeGen codeGen,
        MultiFormOper<TCookie>.RecordMergeInfo merge,
        DType typeSrc, DType typeMos, DType typeMrg,
        Type stSrc, Type stMos, Type stMrg, bool mosFirst)
    {
        // REVIEW: Generalize to support opt records?
        Validation.AssertValue(codeGen);
        Validation.Assert(typeSrc.IsRecordReq);
        Validation.Assert(typeMos.IsRecordReq);
        Validation.Assert(typeMrg.IsRecordReq);

        // Create the mrg record object.
        using var rg = codeGen.CreateRecordGenerator(typeMrg);

        int iargSrc = mosFirst ? 2 : 1;
        int iargMos = mosFirst ? 1 : 2;

        // Copy fields from typeSrc.
        foreach (var tn in typeSrc.GetNames())
        {
            Validation.Assert(typeMrg.TryGetNameType(tn.Name, out var typeFld) && typeFld == tn.Type);
            rg.SetFromStackPre(tn.Name, tn.Type);
            codeGen.Writer.Ldarg(iargSrc);
            codeGen.GenLoadField(typeSrc, stSrc, tn.Name, tn.Type);
            rg.SetFromStackPost();
        }

        // Copy fields from typeOut.
        foreach (var tn in typeMos.GetNames())
        {
            DName name = merge.MosToMrgName(tn.Name);
            Validation.Assert(typeMrg.TryGetNameType(name, out var typeFld) && typeFld == tn.Type);
            rg.SetFromStackPre(name, tn.Type);
            codeGen.Writer.Ldarg(iargMos);
            codeGen.GenLoadField(typeMos, stMos, tn.Name, tn.Type);
            rg.SetFromStackPost();
        }

        // Leave the new mrg record on the stack.
        rg.Finish();
    }
}

/// <summary>
/// Helper functions for code gen related to multi-form-func.
/// </summary>
internal static class MultiFormFuncHelper
{
    /// <summary>
    /// <paramref name="src1"/> and <paramref name="src2"/> must be the same length.
    /// Length of <paramref name="src2"/> should be known before <paramref name="src1"/>.
    /// </summary>
    public static IEnumerable<TOut> Merge<TIn1, TIn2, TOut>(IEnumerable<TIn1> src1, IEnumerable<TIn2> src2, Func<TIn1, TIn2, TOut> fn, TOut valDef)
    {
        Validation.AssertValue(src1);
        Validation.AssertValue(src2);

        // src2 is input, get count and counter upfront if available.
        var counter = src2 as ICanCount;
        long countAhead = -1;
        if (counter != null && counter.TryGetCount(out var count))
            countAhead = count;
        else if (src2 is IReadOnlyCollection<TIn2> ic)
            countAhead = ic.Count;

        // If src1 is an indexed enumerable and src2 is cursorable, then produce an indexed enumerable and
        // process items as they come from src1.
        if (valDef != null && src1 is IIndexedEnumerable<TIn1> ind && src2 is ICursorable<TIn2> cur)
            return IndexedSequence<TOut>.Create(valDef, MergeCore(ind, cur, fn), countAhead, counter);

        var res = Enumerable.Zip(src1, src2, fn);
        if (countAhead >= 0)
            return WrapWithCount.Create(countAhead, res);
        if (counter != null)
            return WrapWithCount.Create(counter, res);
        return res;
    }

    private static IEnumerable<(long index, TOut value)> MergeCore<TIn1, TIn2, TOut>(IIndexedEnumerable<TIn1> ind, ICursorable<TIn2> cur, Func<TIn1, TIn2, TOut> fn)
    {
        using var ator = ind.GetIndexedEnumerator();
        using var curs = cur.GetCursor();

        while (ator.MoveNext())
        {
            if (!curs.MoveTo(ator.Index))
            {
                Validation.Assert(false);
                yield break;
            }
            Validation.Assert(curs.Index == ator.Index);
            yield return (ator.Index, fn(ator.Value, curs.Value));
        }
    }

    /// <summary>
    /// Merging for one-many function calls. Matches items in <paramref name="src1"/> using their key to
    /// the index of items in <paramref name="src2"/>. Hence, the max key should be less than the count of
    /// <paramref name="src2"/>.
    /// 
    /// If <paramref name="useDef"/> is true, then items in <paramref name="src2"/> with 0 corresponding
    /// items in <paramref name="src1"/> will use <paramref name="valDefMos"/> for merging. Otherwise,
    /// the item will be omitted from the final output.
    /// </summary>
    public static IEnumerable<TOut> MergeOneMany<TIn1, TIn2, TOut>(FlatteningSequence<TIn1> src1, IEnumerable<TIn2> src2,
        Func<TIn1, TIn2, TOut> fn, TIn1 valDefMos, bool useDef)
    {
        if (!useDef && src2 is ICursorable<TIn2> cur)
            return MergeOneManyCore(src1, cur, fn);
        return MergeOneManyCore(src1, src2, fn, valDefMos, useDef);
    }

    private static IEnumerable<TOut> MergeOneManyCore<TIn1, TIn2, TOut>(FlatteningSequence<TIn1> src1, IEnumerable<TIn2> src2,
        Func<TIn1, TIn2, TOut> fn, TIn1 valDefMos, bool useDef)
    {
        using var ator1 = src1.GetEnumeratorWithKey();
        using var ator2 = src2.GetEnumerator();
        int ind = -1;
        while (ator1.MoveNext())
        {
            var (key, val) = ator1.Current;
            while (ind < key)
            {
                ind++;
                ator2.MoveNext().Verify();
                if (ind != key && useDef)
                    yield return fn(valDefMos, ator2.Current);
            }

            yield return fn(val, ator2.Current);
        }

        if (!useDef)
            yield break;
        while (ator2.MoveNext())
            yield return fn(valDefMos, ator2.Current);
    }

    private static IEnumerable<TOut> MergeOneManyCore<TIn1, TIn2, TOut>(FlatteningSequence<TIn1> src1, ICursorable<TIn2> src2,
        Func<TIn1, TIn2, TOut> fn)
    {
        using var ator1 = src1.GetEnumeratorWithKey();
        using var cur2 = src2.GetCursor();
        while (ator1.MoveNext())
        {
            var (key, val) = ator1.Current;
            cur2.MoveTo(key).Verify();
            yield return fn(val, cur2.Current);
        }
    }
}
