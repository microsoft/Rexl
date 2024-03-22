// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using FinTuple = Immutable.Array<FieldInfo>;
using IEC = IEqualityComparer;

// This partial is for generated equality comparers.
public abstract partial class TypeManager
{
    /// <summary>
    /// Create the special equality comparers for an equatable agg type. These include (1) strict/tight (null and
    /// nan are treated as non-matching / bad), (2) case insensitive, and (3) both tight and case insensitive.
    /// If the type doesn't include the text type, the Ci and TiCi variants will be null.
    /// </summary>
    private void CreateSpecialEqCmpsForAgg(Type stAgg, FinTuple fins, out IEC eqTi, out IEC eqCi, out IEC eqTiCi)
    {
        int arity = fins.Length;
        Validation.Assert(arity > 0);

        if (!TryEnsureAggEqDefn(stAgg.GetGenericTypeDefinition(), arity, out Type stEqDefn))
            throw Validation.BugExcept("Couldn't create equality comparer holder");

        var sts = stAgg.GetGenericArguments();
        Validation.Assert(sts.Length == arity);

        // First get the various equality comparers for the fields. Also find the FieldInfos for
        // the _ci and _ti fields of the generic EqCmp<...> type.
        var stEq = stEqDefn.MakeGenericType(sts);
        var flds = stEq.GetFields();
        IEC[] eqs = new IEC[flds.Length];
        IEC[] eqsT = null;
        IEC[] eqsC = null;
        IEC[] eqsTC = null;
        int ifld = 0;
        FieldInfo fldTi = null;
        FieldInfo fldCi = null;
        for (int ifldSrc = 0; ifldSrc < flds.Length; ifldSrc++)
        {
            var fld = flds[ifldSrc];
            var stFld = fld.FieldType;
            if (!stFld.IsGenericType)
            {
                if (stFld == typeof(bool))
                {
                    if (fld.Name == "_ti")
                    {
                        Validation.Assert(fldTi is null);
                        fldTi = fld;
                    }
                    else if (fld.Name == "_ci")
                    {
                        Validation.Assert(fldCi is null);
                        fldCi = fld;
                    }
                }
                continue;
            }
            if (stFld.GetGenericTypeDefinition() != typeof(EqualityComparer<>))
                continue;
            var stItem = stFld.GenericTypeArguments[0];
            if (!stItem.IsAssignableFrom(fins[ifld].FieldType))
                throw Validation.BugExcept("Equality comparer field info unexpected");

            if (!TryGetEqCmps(stFld, stItem, out var e, out var et, out var ec, out var etc))
                throw Validation.BugExcept("Couldn't create equality comparer");

            Validation.Assert(ifld < arity);
            Validation.Assert(e is not null);
            Validation.Assert(stFld.IsAssignableFrom(e.GetType()));
            Validation.Assert((etc is null) == (et is null || ec is null));
            eqs[ifld] = e;
            if (et is not null)
                (eqsT ??= new IEC[arity])[ifld] = et;
            if (ec is not null)
                (eqsC ??= new IEC[arity])[ifld] = ec;
            if (etc is not null)
                (eqsTC ??= new IEC[arity])[ifld] = etc;
            flds[ifld] = fld;
            ifld++;
        }
        Validation.Assert(ifld == arity);
        Validation.Assert(fldCi is not null);
        Validation.Assert(fldTi is not null);

        // Create the special EqCmp<...> instances and fill them from the arrays.
        var eqT = (IEC)Activator.CreateInstance(stEq);
        var eqC = eqsC is not null ? (IEC)Activator.CreateInstance(stEq) : null;
        var eqTC = eqsTC is not null || eqC is not null ? (IEC)Activator.CreateInstance(stEq) : null;
        for (ifld = 0; ifld < arity; ifld++)
        {
            var fld = flds[ifld];
            fld.SetValue(eqT, eqsT?[ifld] ?? eqs[ifld]);
            if (eqC is not null)
                fld.SetValue(eqC, eqsC[ifld] ?? eqs[ifld]);
            if (eqTC is not null)
                fld.SetValue(eqTC, eqsTC?[ifld] ?? eqsC[ifld] ?? eqsT?[ifld] ?? eqs[ifld]);
        }

        fldTi.SetValue(eqT, true);
        if (eqC is not null)
            fldCi.SetValue(eqC, true);
        if (eqTC is not null)
        {
            fldTi.SetValue(eqTC, true);
            fldCi.SetValue(eqTC, true);
        }

        eqTi = eqT;
        eqCi = eqC;
        eqTiCi = eqTC;
    }

    private static ConcurrentDictionary<Type, (IEC eq, IEC eqTi, IEC eqCi, IEC eqTiCi)> InitEqCmps()
    {
        var map = new ConcurrentDictionary<Type, (IEC eq, IEC eqTi, IEC eqCi, IEC eqTiCi)>();
        map.TryAdd(typeof(EqualityComparer<string>), (EqualityComparer<string>.Default, EqCmpStrTi.Instance, EqCmpStrCi.Instance, EqCmpStrTiCi.Instance)).Verify();
        map.TryAdd(typeof(EqualityComparer<Link>), (EqualityComparer<Link>.Default, EqCmpRefTi<Link>.Instance, null, null)).Verify();
        map.TryAdd(typeof(EqualityComparer<double>), (EqualityComparer<double>.Default, EqCmpR8ReqTi.Instance, null, null)).Verify();
        map.TryAdd(typeof(EqualityComparer<double?>), (EqualityComparer<double?>.Default, EqCmpR8OptTi.Instance, null, null)).Verify();
        map.TryAdd(typeof(EqualityComparer<float>), (EqualityComparer<float>.Default, EqCmpR4ReqTi.Instance, null, null)).Verify();
        map.TryAdd(typeof(EqualityComparer<float?>), (EqualityComparer<float?>.Default, EqCmpR4OptTi.Instance, null, null)).Verify();
        return map;
    }

    /// <summary>
    /// Get the equality comparer for the indicated strictness and case-insensitivity. Returns false if
    /// <paramref name="type"/> is not equatable (including the invalid type) or doesn't have a system type.
    /// </summary>
    public bool TryEnsureEqCmp(DType type, bool strict, bool ci, out IEC ec, out Type stEq, out Type stItem)
    {
        if (!type.IsEquatable)
        {
            ec = null;
            stEq = null;
            stItem = null;
            return false;
        }

        if (!TryEnsureSysType(type, out stItem))
        {
            ec = null;
            stEq = null;
            return false;
        }

        stEq = typeof(EqualityComparer<>).MakeGenericType(stItem);
        if (!TryGetEqCmps(stEq, stItem, out var eq, out var eqTi, out var eqCi, out var eqTiCi))
        {
            ec = null;
            return false;
        }
        Validation.Assert(eq != null);

        if (!strict)
            ec = !ci ? eq : eqCi ?? eq;
        else
            ec = !ci ? eqTi ?? eq : eqTiCi ?? eqTi ?? eqCi ?? eq;

        Validation.Assert(ec != null);
        return true;
    }

    /// <summary>
    /// Get the (up to) four equality comparer variants for the given equality comparer system type.
    /// </summary>
    private bool TryGetEqCmps(Type stEqCmp, Type stItem, out IEC eq, out IEC eqTi, out IEC eqCi, out IEC eqTiCi)
    {
        Validation.Assert(stEqCmp.IsGenericType);
        Validation.Assert(stEqCmp.GetGenericTypeDefinition() == typeof(EqualityComparer<>));
        Validation.Assert(stItem == stEqCmp.GetGenericArguments()[0]);

        if (!_stToEqCmpCi.TryGetValue(stEqCmp, out var cmps))
        {
            IEC e, et, ec, etc;
            if (_aggTypeInfos.TryGetValue(stItem, out var ati))
            {
                e = ati.EqCmpInfo.Eq;
                if (e is null)
                {
                    eq = eqTi = eqCi = eqTiCi = null;
                    return false;
                }
                et = ati.EqCmpInfo.EqTi;
                Validation.Assert(et is not null);
                ec = ati.EqCmpInfo.EqCi;
                etc = ati.EqCmpInfo.EqTiCi;
            }
            else
            {
                Validation.Assert(stItem != typeof(string));
                Validation.Assert(stItem != typeof(double));
                Validation.Assert(stItem != typeof(double?));
                Validation.Assert(stItem != typeof(float));
                Validation.Assert(stItem != typeof(float?));
                Validation.Assert(!stItem.IsSubclassOf(typeof(AggBase)));

                e = GetDefaultEqCmp(stItem);
                if (IsRefType(stItem))
                    et = GetStrictEqCmpRef(stItem);
                else if (IsNullableTypeCore(stItem, out var stReq))
                    et = GetStrictEqCmpNullable(stReq);
                else
                    et = null;
                ec = null;
                etc = null;
            }

            Validation.Assert(e != null);
            Validation.Assert(stEqCmp.IsAssignableFrom(e.GetType()));
            Validation.Assert(et is null || stEqCmp.IsAssignableFrom(et.GetType()));
            Validation.Assert(ec is null || stEqCmp.IsAssignableFrom(ec.GetType()));
            Validation.Assert(etc is null || stEqCmp.IsAssignableFrom(etc.GetType()));
            Validation.Assert((etc is null) == (ec is null || et is null));

            // Remember the eqs.
            cmps = _stToEqCmpCi.GetOrAdd(stEqCmp, (e, et, ec, etc));
        }

        (eq, eqTi, eqCi, eqTiCi) = cmps;
        return true;
    }

    private bool TryEnsureAggEqDefn(Type stAggDefn, int arity, out Type stEqDefn)
    {
        Validation.AssertValue(stAggDefn);
        Validation.Assert(stAggDefn.IsGenericTypeDefinition);
        Validation.Assert(arity > 0);

        if (arity > AggBase.ArityMax)
        {
            stEqDefn = null;
            return false;
        }

        if (_aggDefnToEqDefn.TryGetValue(stAggDefn, out stEqDefn))
            return true;

        lock (_lockAggDefnToEqDefn)
        {
            if (_aggDefnToEqDefn.TryGetValue(stAggDefn, out stEqDefn))
                return true;

            if (!TryEnsureAggEqDefnCore(stAggDefn, arity, out Type st))
            {
                stEqDefn = null;
                return false;
            }
            stEqDefn = _aggDefnToEqDefn.GetOrAdd(stAggDefn, st);
        }

        return true;
    }

    private bool TryEnsureAggEqDefnCore(Type stAggDefn, int arity, out Type stEqDefn)
    {
        Validation.AssertValue(stAggDefn);
        Validation.Assert(arity > 0);
        Validation.Assert(stAggDefn.IsGenericTypeDefinition);

        // Get the type parameters on the agg generic type definition.
        var tpsAggDefn = stAggDefn.GetGenericArguments();
        Validation.Assert(tpsAggDefn.Length == arity);

        bool isRec = stAggDefn.IsSubclassOf(typeof(RecordBase));
        Validation.Assert(isRec || stAggDefn.IsSubclassOf(typeof(TupleBase)));

        // Create the type builder.
        var tb = GetTypeBuilder(string.Format("{0}`{1}", isRec ? "EqCmpRec" : "EqCmpTup", arity));

        // Add the type parameters.
        var names = new string[arity];
        for (int slot = 0; slot < arity; slot++)
            names[slot] = SlotToFieldName(slot);
        var tps = tb.DefineGenericParameters(names);

        // Set the base class. Note that we use a custom base class derived from EqualityComparer<T> to
        // properly implement IEqualityComparer.Equals(object, object).
        var stAgg = stAggDefn.MakeGenericType(tps);
        tb.SetParent(typeof(EqCmpRefBase<>).MakeGenericType(stAgg));

        // Add the fields: bool _ci and bool _ti.
        var fldCi = tb.DefineField("_ci", typeof(bool), FieldAttributes.Public);
        var fldTi = tb.DefineField("_ti", typeof(bool), FieldAttributes.Public);

        // Add the fields for the equality comparers, parallel to the value fields of the agg.
        var stCmpDefn = typeof(EqualityComparer<>);
        var eqs = new FieldInfo[arity];
        for (int slot = 0; slot < arity; slot++)
            eqs[slot] = tb.DefineField(names[slot], stCmpDefn.MakeGenericType(tps[slot]), FieldAttributes.Public);

        // Get the fields for the agg.
        int numFlags = isRec ? (arity + 7) >> 3 : 0;
        var fldsVal = new FieldInfo[arity];
        var fldsGrp = numFlags > 0 ? new FieldInfo[numFlags] : null;
        int cv = 0;
        int cg = 0;
        foreach (var fldDefn in stAggDefn.GetFields())
        {
            int ind;
            if ((ind = FieldNameToSlot(fldDefn.Name)) >= 0)
            {
                Validation.AssertIndex(ind, arity);
                Validation.Assert(fldDefn.FieldType == tpsAggDefn[ind]);
                Validation.Assert(fldsVal[ind] == null);
                fldsVal[ind] = TypeBuilder.GetField(stAgg, fldDefn);
                cv++;
            }
            else if (isRec && (ind = FieldNameToBitGrp(fldDefn.Name)) >= 0)
            {
                Validation.AssertIndex(ind, numFlags);
                Validation.Assert(fldsGrp[ind] == null);
                Validation.Assert(fldDefn.FieldType == typeof(byte));
                fldsGrp[ind] = TypeBuilder.GetField(stAgg, fldDefn);
                cg++;
            }
        }
        Validation.Assert(cv == arity);
        Validation.Assert(cg == numFlags);

        // Get the Equals(T, T) and GetHashCode(T) methods from EqualityComparer<T>.
        // REVIEW: Is there a better way to get these? We need the method info on the generic type
        // definition. We can't just look up by name, since there is also the one-arg Equals and zero arg GetHashCode.
        var methEquals = stCmpDefn.GetMethods()
            .Where(m => m.Name == "Equals" && m.GetParameters().Length == 2).FirstOrDefault().VerifyValue();
        var methGetHashCode = stCmpDefn.GetMethods()
            .Where(m => m.Name == "GetHashCode" && m.GetParameters().Length == 1).FirstOrDefault().VerifyValue();

        // Add: protected int GetHashCodeCore(TAgg).
        // The outer public GetHashCode handles the null test and caching the result in the "_hashXx" field.
        MethodBuilder methGetHashCore = tb.DefineMethod("GetHashCodeCore",
            MethodAttributes.Private | MethodAttributes.HideBySig,
            typeof(int), new Type[] { stAgg });
        {
            var ilw = new ILWriter(new[] { tb, stAgg }, methGetHashCore.GetILGenerator());

            int count = 0;
            var sts = new Type[Math.Min(8, 1 + arity + numFlags)];

            // Start with one of type int.
            sts[count++] = typeof(int);
            ilw.Ldc_I4(arity);

            for (int ifld = 0; ifld < numFlags; ifld++)
            {
                Validation.Assert(count < sts.Length);
                var fld = fldsGrp[ifld];
                ilw.Ldarg(1).Ldfld(fld);
                sts[count] = typeof(bool);
                if (++count == 8)
                {
                    ilw.Call(CodeGenUtil.HashCombine8.MakeGenericMethod(sts));
                    count = 1;
                }
            }

            for (int ifld = 0; ifld < fldsVal.Length; ifld++)
            {
                Validation.Assert(count < sts.Length);
                var fld = fldsVal[ifld];
                var eq = eqs[ifld];
                Validation.Assert(fld.Name == eq.Name);
                ilw
                    .Ldarg(0).Ldfld(eq)
                    .Ldarg(1).Ldfld(fld)
                    .Callvirt(TypeBuilder.GetMethod(eq.FieldType, methGetHashCode));
                sts[count] = typeof(int);
                if (++count == 8)
                {
                    ilw.Call(CodeGenUtil.HashCombine8.MakeGenericMethod(sts));
                    count = 1;
                }
            }

            Validation.Assert(1 <= count & count < 8);
            if (count > 1)
            {
                MethodInfo methComb = CodeGenUtil.GetHashCombine(count);
                Array.Resize(ref sts, count);
                ilw.Call(methComb.MakeGenericMethod(sts));
            }

            // If the hash just happens to be zero, add one, so we don't do this computation
            // over and over.
            ilw
                .Dup()
                .Ldc_I4(0)
                .Ceq()
                .Add()
                .Ret();
        }

        // Add: public override int GetHashCode(TAgg).
        MethodBuilder methGetHash = tb.DefineMethod("GetHashCode",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            typeof(int), new Type[] { stAgg });
        {
            var ilw = new ILWriter(new[] { tb, stAgg }, methGetHash.GetILGenerator());

            var methObjGetHash = typeof(object).GetMethod("GetHashCode",
                BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes).VerifyValue();

            var fldHashCi = typeof(AggBase)
                .GetField("_hashCi", BindingFlags.Public | BindingFlags.Instance)
                .VerifyValue();

            Label labNotNull = default;
            Label labCi = default;
            Label labDone = default;
            ilw
                // Test for null agg. If it is, return zero.
                .Ldarg(1)
                .Brtrue(ref labNotNull)
                .Ldc_I4(0)
                .Br(ref labDone)

                // Agg isn't null.
                .MarkLabel(labNotNull)
                // Test for ci.
                .Ldarg(0).Ldfld(fldCi)
                .Brtrue(ref labCi)

                // Not ci, so call the standard GetHashCode.
                .Ldarg(1)
                .Callvirt(methObjGetHash)
                .Br(ref labDone)

                // The ci case. Load the _hashCi field of the agg. If it isn't zero, use it.
                .MarkLabel(labCi)
                .Ldarg(1).Ldfld(fldHashCi)
                .Dup()
                .Brtrue(ref labDone)

                // Pop the useless zero, call the GetHashCodeCore method, store the result in the
                // _hashCi field and return it.
                .Pop()
                .Ldarg(1)
                .Ldarg(0).Ldarg(1)
                .Call(methGetHashCore)
                .Stfld(fldHashCi)
                .Ldarg(1)
                .Ldfld(fldHashCi)

                .MarkLabel(labDone)
                .Ret();
        }

        // Add: public override bool Equals(TAgg, TAgg)
        MethodBuilder methEq = tb.DefineMethod("Equals",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            typeof(bool), new Type[] { stAgg, stAgg });
        {
            var ilw = new ILWriter(new Type[] { tb, stAgg, stAgg }, methEq.GetILGenerator());
            Label labNotTi = default;
            Label labMain = default;
            Label labTrue = default;
            Label labFalse = default;

            // REVIEW: if the hash codes are known and different, we could short cut to false.
            // Could even ensure the hash codes are known and test. Is that worth doing? Of course,
            // this would need to be done after the null tests.
            // ilw
            //     .Ldarg(0).Ldarg(1).Callvirt(methGetHash)
            //     .Ldarg(0).Ldarg(2).Callvirt(methGetHash)
            //     .Bne_Un(ref labFalse);

            ilw
                // For strict, null => false and reference equality is irrelevant.
                .Ldarg(0).Ldfld(fldTi).Brfalse(ref labNotTi)
                .Ldarg(1).Brfalse(ref labFalse)
                .Ldarg(2).Brfalse(ref labFalse);

            // For strict, first look at the flags (if any). If any are clear (indicating null), this is
            // a bad key value. Note that string and Link have their flags always set so nulls for them
            // are not detected here.
            if (isRec)
            {
                for (int igrp = 0; igrp < fldsGrp.Length; igrp++)
                {
                    var fld = fldsGrp[igrp];
                    int cbit = Math.Min(8, arity - (igrp << 3));
                    byte grp = (byte)((1 << cbit) - 1);
                    ilw.Ldarg(1).Ldfld(fld).Ldc_I4(grp).Bne_Un(ref labFalse);
                    ilw.Ldarg(2).Ldfld(fld).Ldc_I4(grp).Bne_Un(ref labFalse);
                }
            }

            ilw
                .Br(ref labMain)
                .MarkLabel(labNotTi)
                // Test for reference equality. This also handles the case of both being null.
                .Ldarg(1).Ldarg(2).Beq(ref labTrue)
                .Ldarg(1).Brfalse(ref labFalse)
                .Ldarg(2).Brfalse(ref labFalse);

            if (isRec)
            {
                for (int igrp = 0; igrp < fldsGrp.Length; igrp++)
                {
                    var fld = fldsGrp[igrp];
                    ilw
                        .Ldarg(1).Ldfld(fld)
                        .Ldarg(2).Ldfld(fld)
                        .Bne_Un(ref labFalse);
                }
            }

            ilw.MarkLabel(labMain);

            for (int slot = 0; slot < fldsVal.Length; slot++)
            {
                var fld = fldsVal[slot];

                Validation.Assert(slot < arity);
                var eq = eqs[slot];
                Validation.Assert(fld.Name == eq.Name);
                ilw
                    // Load the EqualityComparer<T>.
                    .Ldarg(0).Ldfld(eq)
                    // Load the corresponding field values from the two items.
                    .Ldarg(1).Ldfld(fld)
                    .Ldarg(2).Ldfld(fld)
                    // Call EqualityComparer<T>.Equals(T,T)
                    .Callvirt(TypeBuilder.GetMethod(eq.FieldType, methEquals))
                    .Brfalse(ref labFalse);
            }

            ilw
                .MarkLabel(labTrue)
                .Ldc_I4(1)
                .Ret()
                .MarkLabel(labFalse)
                .Ldc_I4(0)
                .Ret();
        }

        // Add the default ctor.
        tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

        tb.AddInterfaceImplementation(typeof(IEqualityComparer<>).MakeGenericType(stAgg));

        stEqDefn = tb.CreateTypeInfo();
        Validation.Assert(stEqDefn.IsGenericTypeDefinition);
        return true;
    }

    private static IEC GetDefaultEqCmp(Type stItem)
    {
        var meth = new Func<EqualityComparer<string>>(GetDefaultEqCmp<string>).Method
            .GetGenericMethodDefinition().MakeGenericMethod(stItem);
        var res = (IEC)meth.Invoke(null, Array.Empty<object>());
        Validation.Assert(res is not null);
        Validation.Assert(typeof(EqualityComparer<>).MakeGenericType(stItem).IsAssignableFrom(res.GetType()));
        return res;
    }

    private static EqualityComparer<T> GetDefaultEqCmp<T>() => EqualityComparer<T>.Default;

    private static IEC GetStrictEqCmpRef(Type stItem)
    {
        var meth = new Func<EqualityComparer<Link>>(GetStrictEqCmpRef<Link>).Method
            .GetGenericMethodDefinition().MakeGenericMethod(stItem);
        var res = (IEC)meth.Invoke(null, Array.Empty<object>());
        Validation.Assert(res is not null);
        Validation.Assert(typeof(EqualityComparer<>).MakeGenericType(stItem).IsAssignableFrom(res.GetType()));
        return res;
    }

    private static EqualityComparer<T> GetStrictEqCmpRef<T>()
        where T : class
    {
        return EqCmpRefTi<T>.Instance;
    }

    private static IEC GetStrictEqCmpNullable(Type stReq)
    {
        var meth = new Func<EqualityComparer<int?>>(GetStrictEqCmpNullable<int>).Method
            .GetGenericMethodDefinition().MakeGenericMethod(stReq);
        var res = (IEC)meth.Invoke(null, Array.Empty<object>());
        Validation.Assert(res is not null);
        Validation.Assert(
            typeof(EqualityComparer<>).MakeGenericType(typeof(Nullable<>).MakeGenericType(stReq))
                .IsAssignableFrom(res.GetType()));
        return res;
    }

    private static EqualityComparer<T?> GetStrictEqCmpNullable<T>()
        where T : struct
    {
        return new EqCmpValOptTi<T>();
    }
}
