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

using IEC = IEqualityComparer;

// This partial is for synthesis of system types.
public abstract partial class TypeManager
{
    // Assembly and module builders.
    private readonly AssemblyBuilder _bldrAsm;
    private readonly ModuleBuilder _bldrMod;

    // Cache of tuple/record field names, also used for type parameter names (since those don't really matter).
    private readonly ConcurrentDictionary<int, string> _slotToFieldName;
    private readonly ConcurrentDictionary<string, int> _fieldNameToSlot;

    // Cache of tuple/record "bit group" field names.
    private readonly ConcurrentDictionary<int, string> _bitGrpToFieldName;
    private readonly ConcurrentDictionary<string, int> _fieldNameToBitGrp;

    // Map from arity to generic record class definition.
    private readonly object _lockArityToRecDefn;
    private readonly ConcurrentDictionary<int, Type> _arityToRecDefn;

    // Map from arity to generic tuple class definition.
    private readonly object _lockArityToTupDefn;
    private readonly ConcurrentDictionary<int, Type> _arityToTupDefn;

    // Map from generic type defn for a record or tuple to generic type defn for the associated EqCmpXxx class.
    private readonly object _lockAggDefnToEqDefn;
    private readonly ConcurrentDictionary<Type, Type> _aggDefnToEqDefn;

    // Map from system type of the form EqualityComparer<T>, for some T, to the associated equaltiy comparers.
    // The four variants are (1) default, (2) strict/tight, meaning that nan and null values are not considered
    // equal to themselves, (3) case insensitive, and (4) both tight and case insensitive. Some or all of these
    // may be null with the following restrictions:
    // * If eq is null, then all are null.
    // * eqTi is non-null iff eq is non-null and the item type is either a reference type or floating point type.
    //   Note that any equatable reference type has an opt version that uses the same system type, so this is
    //   needed for any such type, whether or not it contains opt or floating point fields.
    // * eqCi is non-null iff the item type "contains" (or is) the text type.
    // * eqTiCi is non-null iff both eqCi and eqTi are non-null.
    // Note that the input type is the desired comparer type, NOT the item type.
    private readonly ConcurrentDictionary<Type, (IEC eq, IEC eqTi, IEC eqCi, IEC eqTiCi)> _stToEqCmpCi;

    // Cache of GetSlotDynamic generic method definitions.
    private readonly object _lockArityToGetSlotDynDefn;
    private readonly ConcurrentDictionary<int, MethodInfo> _arityToGetSlotDynDefn;

    private const string GetSlotDynName = "GetSlotDynamic";

    /// <summary>
    /// Try to get the value for the indicated slot in the given tuple object of the given
    /// tuple type. Returns false if the type isn't a tuple type, or doesn't have the
    /// given slot. Throws if the tuple is invalid (doesn't match the given tuple type).
    /// </summary>
    public bool TryGetTupleSlotValue(DType typeTup, int slot, TupleBase tup, out Type stFld, out object value)
    {
        Validation.BugCheckValue(tup, nameof(tup));

        if (!typeTup.IsTupleXxx || !Validation.IsValidIndex(slot, typeTup.TupleArity))
        {
            value = null;
            stFld = null;
            return false;
        }

        if (!TryGetSysTypeCore(typeTup, out var sti))
        {
            value = null;
            stFld = null;
            return false;
        }
        var tti = sti.TupleInfo.VerifyValue();

        Validation.BugCheckParam(tti.SysType.IsAssignableFrom(tup.GetType()), nameof(tup));

        var fin = tti.ValueFields[slot].VerifyValue();
        Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeTup.GetTupleSlotTypes()[slot]));

        stFld = fin.FieldType;
        value = fin.GetValue(tup);
        return true;
    }

    private bool TryMakeSysTypeCore(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(type.SeqCount == 0);

        // Opt and Req use the same system type for these special types, since we use/assume classes, not structs.
        // REVIEW: This may change when we support fine-scale-error-propagation.
        switch (type.RootKind)
        {
        case DKind.Record:
            if (type.IsOpt)
                return TryGetSysTypeCore(type.ToReq(), out sti);
            return TryMakeRecordType(type, out sti);
        case DKind.Module:
            if (type.IsOpt)
                return TryGetSysTypeCore(type.ToReq(), out sti);
            return TryMakeModuleType(type, out sti);
        case DKind.Tuple:
            if (type.IsOpt)
                return TryGetSysTypeCore(type.ToReq(), out sti);
            return TryMakeTupleType(type, out sti);
        case DKind.Tensor:
            if (type.IsOpt)
                return TryGetSysTypeCore(type.ToReq(), out sti);
            return TryMakeTensorType(type, out sti);

        default:
            // Unhandled kind.
            Validation.Assert(false);
            sti = default;
            return false;
        }
    }

    private bool TryMakeDefaultValueCore(DType type, SysTypeInfo sti, out (object value, bool special) entry)
    {
        Validation.Assert(type.SeqCount == 0);
        Validation.Assert(!type.IsOpt);

        switch (type.RootKind)
        {
        case DKind.Record:
            return TryMakeRecordDefault(type, sti, out entry);
        case DKind.Tuple:
            return TryMakeTupleDefault(type, sti, out entry);
        case DKind.Tensor:
            return TryMakeTensorDefault(type, sti, out entry);

        default:
            // Unhandled kind.
            Validation.Assert(false);
            entry = default;
            return false;
        }
    }

    protected virtual bool TryMakeTensorType(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(type.IsTensorXxx);

        DType typeItem = type.GetTensorItemType();
        if (TryEnsureSysType(typeItem, out Type stItem))
        {
            sti = typeof(Tensor<>).MakeGenericType(stItem);
            return true;
        }

        sti = default;
        return false;
    }

    protected virtual bool TryMakeTensorDefault(DType type, SysTypeInfo sti, out (object value, bool special) entry)
    {
        Validation.Assert(type.IsTensorReq);
        Validation.Assert(sti.Raw is Type);

        var typeItem = type.GetTensorItemType();
        entry = default;
        if (!TryGetSysTypeCore(typeItem, out var stiItem))
            return false;

        if (!TryGetDefaultValueCore(typeItem, out var itemEntry))
            return false;

        var createMethod = sti.SysType.GetMethod(
            nameof(Tensor<object>.CreateFillCore),
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new Type[] { stiItem.SysType, typeof(Shape) },
            null);
        Validation.Assert(createMethod != null);

        // The default value for a tensor is a tensor of the appropriate type and rank with all 0 dimensions.
        // REVIEW: Further discussion is needed to determine if this is the best value to use as the default.
        entry = (createMethod.Invoke(null, new object[] { itemEntry.value, Shape.CreateZero(type.TensorRank) }), true);

        return true;
    }

    private bool TryMakeRecordType(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(type.IsRecordReq);

        var typeReq = type.GetReqFieldType();
        if (_rrtiMap.TryGetValue(typeReq, out var rrti))
        {
            Validation.Assert(rrti.TypeReq == typeReq);
            Validation.Assert(rrti.Rsti.ValueCount == type.FieldCount);
            sti = rrti.Rsti;
            return true;
        }

        int arity = type.FieldCount;
        if (!TryEnsureGenRecord(arity, out Type stGen))
        {
            sti = default;
            return false;
        }

        Type stRec;
        Type[] sts;
        if (arity == 0)
        {
            Validation.Assert(!stGen.IsGenericType);
            stRec = stGen;
            sts = Type.EmptyTypes;
        }
        else
        {
            Validation.Assert(stGen.IsGenericTypeDefinition);
            Validation.Assert(stGen.GetGenericArguments().Length == arity);

            sts = new Type[arity];
            int slot = 0;
            foreach (var tn in type.GetNames())
            {
                if (!TryEnsureSysType(tn.Type, out sts[slot]))
                {
                    sti = default;
                    return false;
                }
                if (IsNullableTypeCore(sts[slot], out Type stReq))
                    sts[slot] = stReq;
                slot++;
            }
            Validation.Assert(slot == arity);
            stRec = stGen.MakeGenericType(sts);
        }
        Validation.Assert(sts.Length == arity);

        if (!_aggTypeInfos.TryGetValue(stRec, out var ati))
        {
            ati = _aggTypeInfos.GetOrAdd(stRec,
                new RecordSysTypeInfo(this, stRec, sts, type.IsEquatable, type.HasText));
        }

        Validation.Assert(ati.ValueCount == arity);
        Validation.Assert(ati is RecordSysTypeInfo);
        var rsti = (RecordSysTypeInfo)ati;
        sti = rsti;

        return true;
    }

    private bool TryMakeModuleType(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(type.IsModuleReq);

        if (TryEnsureSysType(type.ModuleToRecord(), out Type stRec))
        {
            sti = typeof(RuntimeModule<>).MakeGenericType(stRec);
            return true;
        }

        sti = default;
        return false;
    }

    private bool TryEnsureGenRecord(int arity, out Type stGen)
    {
        Validation.Assert(arity >= 0);

        if (arity == 0)
        {
            stGen = typeof(RecordImpl);
            return true;
        }

        if (arity > AggBase.ArityMax)
        {
            stGen = null;
            return false;
        }

        if (_arityToRecDefn.TryGetValue(arity, out stGen))
            return true;

        lock (_lockArityToRecDefn)
        {
            if (_arityToRecDefn.TryGetValue(arity, out stGen))
                return true;

            if (!TryEnsureGenTypeCore("RecordImpl", typeof(RecordBase), arity,
                wantEquatable: true, wantFlags: true, out Type st))
            {
                stGen = null;
                return false;
            }
            stGen = _arityToRecDefn.GetOrAdd(arity, st);
        }

        return true;
    }

    private bool TryMakeRecordDefault(DType type, SysTypeInfo sti, out (object value, bool special) entry)
    {
        Validation.Assert(type.IsRecordReq);
        Validation.Assert(sti.RecordInfo != null);

        var fact = CreateRecordFactory(type);
        var rec = fact.Create().Open();

        int slot = 0;
        foreach (var tn in type.GetNames())
        {
            if (!tn.Type.IsOpt)
            {
                if (!TryEnsureDefaultValue(tn.Type, out var ent))
                {
                    entry = default;
                    return false;
                }
                if (ent.special)
                    fact.GetFieldSetter(tn.Name, out _, out _)(rec, ent.value);
            }
            slot++;
        }
        Validation.Assert(slot == type.FieldCount);

        entry = (rec.Close(), true);
        return true;
    }

    /// <summary>
    /// Constructs a type builder.
    /// </summary>
    private TypeBuilder GetTypeBuilder(string name, Type stBase = null)
    {
        Validation.AssertNonEmpty(name);

        return _bldrMod.DefineType(
            name,
            TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed,
            stBase ?? typeof(object));
    }

    /// <summary>
    /// Map from slot index to field name.
    /// </summary>
    protected string SlotToFieldName(int slot)
    {
        Validation.Assert(slot >= 0);
        if (_slotToFieldName.TryGetValue(slot, out string name))
            return name;
        name = _slotToFieldName.GetOrAdd(slot, string.Format("_F{0}", slot));
        int tmp = _fieldNameToSlot.GetOrAdd(name, slot);
        Validation.Assert(tmp == slot);
        return name;
    }

    /// <summary>
    /// Map from field name to slot. Returns -1 on failure.
    /// </summary>
    protected int FieldNameToSlot(string name)
    {
        if (!name.StartsWith("_F"))
            return -1;
        if (_fieldNameToSlot.TryGetValue(name, out int slot))
            return slot;
        if (!int.TryParse(name.Substring(2), out slot) || slot < 0)
            return -1;
        string tmp = SlotToFieldName(slot);
        if (tmp != name)
            return -1;
        return slot;
    }

    /// <summary>
    /// Map from bit group to field name.
    /// </summary>
    protected string BitGrpToFieldName(int grp)
    {
        Validation.Assert(grp >= 0);
        if (_bitGrpToFieldName.TryGetValue(grp, out string name))
            return name;
        name = _bitGrpToFieldName.GetOrAdd(grp, string.Format("_B{0}", grp));
        int tmp = _fieldNameToBitGrp.GetOrAdd(name, grp);
        Validation.Assert(tmp == grp);
        return name;
    }

    /// <summary>
    /// Map from field name to bit group. Returns -1 on failure.
    /// </summary>
    protected int FieldNameToBitGrp(string name)
    {
        if (!name.StartsWith("_B"))
            return -1;
        if (_fieldNameToBitGrp.TryGetValue(name, out int grp))
            return grp;
        if (!int.TryParse(name.Substring(2), out grp) || grp < 0)
            return -1;
        string tmp = BitGrpToFieldName(grp);
        if (tmp != name)
            return -1;
        return grp;
    }

    private bool TryMakeTupleType(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(type.IsTupleReq);

        var types = type.GetTupleSlotTypes();
        int arity = types.Length;
        if (!TryEnsureGenTuple(arity, out Type stGen))
        {
            sti = default;
            return false;
        }

        Type stTup;
        Type[] sts;
        if (arity == 0)
        {
            Validation.Assert(!stGen.IsGenericType);
            stTup = stGen;
            sts = Type.EmptyTypes;
        }
        else
        {
            Validation.Assert(stGen.IsGenericTypeDefinition);
            Validation.Assert(stGen.GetGenericArguments().Length == arity);

            sts = new Type[arity];
            for (int i = 0; i < arity; i++)
            {
                if (!TryEnsureSysType(types[i], out sts[i]))
                {
                    sti = default;
                    return false;
                }
            }
            stTup = stGen.MakeGenericType(sts);
        }
        Validation.Assert(sts.Length == arity);

        if (!_aggTypeInfos.TryGetValue(stTup, out var ati))
        {
            ati = _aggTypeInfos.GetOrAdd(stTup,
                new TupleSysTypeInfo(this, stTup, sts, type.IsEquatable, type.HasText));
        }

        Validation.Assert(ati.ValueCount == arity);
        Validation.Assert(ati is TupleSysTypeInfo);
        sti = ati;
        return true;
    }

    private bool TryEnsureGenTuple(int arity, out Type stGen)
    {
        Validation.Assert(arity >= 0);

        if (arity == 0)
        {
            stGen = typeof(TupleImpl);
            return true;
        }

        if (arity > AggBase.ArityMax)
        {
            stGen = null;
            return false;
        }

        if (_arityToTupDefn.TryGetValue(arity, out stGen))
            return true;

        lock (_lockArityToTupDefn)
        {
            if (_arityToTupDefn.TryGetValue(arity, out stGen))
                return true;

            if (!TryEnsureGenTypeCore("TupleImpl", typeof(TupleBase), arity,
                wantEquatable: true, wantFlags: false, out Type st))
            {
                stGen = null;
                return false;
            }
            stGen = _arityToTupDefn.GetOrAdd(arity, st);
        }

        return true;
    }

    private static readonly MethodInfo _methSpanGetItem = typeof(Span<byte>)
        .GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public, new[] { typeof(int) }).VerifyValue();

    protected virtual bool TryEnsureGenTypeCore(string name, Type stBase, int arity,
        bool wantEquatable, bool wantFlags,
        out Type stDefn)
    {
        Validation.AssertNonEmpty(name);
        Validation.AssertValue(stBase);
        Validation.Assert(arity > 0);

        var tb = GetTypeBuilder(string.Format("{0}`{1}", name, arity), stBase);

        // Add the type parameters.
        var names = new string[arity];
        for (int slot = 0; slot < arity; slot++)
            names[slot] = SlotToFieldName(slot);
        var tps = tb.DefineGenericParameters(names);

        // REVIEW: Consider using 32 bits per flag for efficiency?
        int numFlags = wantFlags ? (arity + 7) / 8 : 0;
        var flds = new FieldInfo[numFlags + arity];
        for (int i = 0; i < numFlags; i++)
            flds[i] = tb.DefineField(BitGrpToFieldName(i), typeof(byte), FieldAttributes.Public);

        // Add the fields.
        for (int slot = 0; slot < arity; slot++)
            flds[numFlags + slot] = tb.DefineField(names[slot], tps[slot], FieldAttributes.Public);

        // Add the default ctor.
        tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

        if (wantFlags)
        {
            MethodBuilder meth = tb.DefineMethod("FillFlags",
                MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                null, new[] { typeof(Span<byte>) });

            var ilw = new ILWriter(new[] { tb, typeof(Span<byte>) }, meth.GetILGenerator());

            for (int i = 0; i < numFlags; i++)
            {
                ilw
                    .Ldarga(1)
                    .Ldc_I4(i)
                    .Call(_methSpanGetItem)
                    .Ldarg(0)
                    .Ldfld(flds[i])
                    .Stind_I1();
            }

            ilw.Ret();
        }

        if (wantEquatable)
        {
            // Add: protected override int GetHashCodeCore() for the default (case sensitive) equality comparison.
            {
                MethodBuilder meth = tb.DefineMethod("GetHashCodeCore",
                    MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                    typeof(int), Type.EmptyTypes);
                var ilw = new ILWriter(new[] { tb }, meth.GetILGenerator());

                int count = 0;
                var sts = new Type[Math.Min(8, 1 + flds.Length)];

                // Start with one of type int.
                sts[count++] = typeof(int);
                ilw.Ldc_I4(arity);

                // This handles both the flags and value fields.
                for (int ifld = 0; ifld < flds.Length; ifld++)
                {
                    Validation.Assert(count < sts.Length);
                    var fld = flds[ifld];
                    ilw
                        .Ldarg(0)
                        .Ldfld(fld);

                    sts[count++] = fld.FieldType;
                    if (count == 8)
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

                // If the hash just happens to be zero, add one.
                ilw
                    .Dup()
                    .Ldc_I4(0)
                    .Ceq()
                    .Add()
                    .Ret();
            }

            var types2 = new Type[] { tb, tb };

            // Add: public static bool Equals2(type, type).
            MethodBuilder methEq2 = tb.DefineMethod("Equals2",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(bool), types2);
            {
                var ilw = new ILWriter(types2, methEq2.GetILGenerator());
                Label labTrue = default;
                Label labFalse = default;
                ilw
                    // First test for reference equality.
                    .Ldarg(0).Ldarg(1).Beq(ref labTrue)
                    // If either is null, the answer is false.
                    .Ldarg(0).Brfalse(ref labFalse)
                    .Ldarg(1).Brfalse(ref labFalse);

                // First process the flags using direct equality testing.
                if (numFlags > 0)
                {
                    for (int i = 0; i < numFlags; i++)
                    {
                        var fld = flds[i];
                        Validation.Assert(fld.FieldType == typeof(byte));
                        ilw
                            .Ldarg(0)
                            .Ldfld(fld)
                            .Ldarg(1)
                            .Ldfld(fld)
                            .Bne_Un(ref labFalse);
                    }
                }

                // Process the value fields, using EqualityComparer<T>.Default. Note that this is a CLR instrinsic.
                // That is, the JIT optimizes EqualtiyComparer<T>.Default.Equals(...) for non-reference types, so
                // we don't want to cache these eqs. Doing so would harm performance.
                var defnEqCmp = typeof(EqualityComparer<>);
                var methDefault = defnEqCmp
                    .GetMethod("get_Default", BindingFlags.Public | BindingFlags.Static).VerifyValue();
                // REVIEW: Is there a better way to get this? We need the method info on the generic type
                // definition. We can't just look up by name, since there is also the one-arg Equals.
                var methEquals = defnEqCmp.GetMethods()
                    .Where(m => m.Name == "Equals" && m.GetParameters().Length == 2).FirstOrDefault().VerifyValue();
                for (int ifld = numFlags; ifld < flds.Length; ifld++)
                {
                    var fld = flds[ifld];
                    var stCmp = typeof(EqualityComparer<>).MakeGenericType(fld.FieldType);
                    ilw
                        .Call(TypeBuilder.GetMethod(stCmp, methDefault))
                        .Ldarg(0).Ldfld(fld)
                        .Ldarg(1).Ldfld(fld)
                        .Callvirt(TypeBuilder.GetMethod(stCmp, methEquals))
                        .Brfalse(ref labFalse);
                }

                // Fall through to labTrue.
                ilw
                    .MarkLabel(labTrue)
                    .Ldc_I4(1)
                    .Ret()
                    .MarkLabel(labFalse)
                    .Ldc_I4(0)
                    .Ret();
            }

            // Add: public override bool Equals(type), invoking static Equals2(this, arg).
            {
                MethodBuilder meth = tb.DefineMethod("Equals",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                    typeof(bool), new[] { tb });

                var ilw = new ILWriter(types2, meth.GetILGenerator());
                ilw
                    .Ldarg(0).Ldarg(1)
                    .Call(methEq2)
                    .Ret();
            }

            // Add: protected override bool EqualsCore(object), invoking Equals2(this, arg as T).
            {
                // return Equals(obj as T);
                MethodBuilder meth = tb.DefineMethod("EqualsCore",
                    MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                    typeof(bool), new[] { typeof(object) });
                var ilw = new ILWriter(new[] { tb, typeof(object) }, meth.GetILGenerator());
                ilw
                    .Ldarg(0)
                    .Ldarg(1)
                    .Isinst(tb)
                    .Call(methEq2)
                    .Ret();
            }

            tb.AddInterfaceImplementation(typeof(IEquatable<>).MakeGenericType(tb));
        }

        stDefn = tb.CreateTypeInfo();
        return true;
    }

    /// <summary>
    /// Returns the generic method definition for
    /// <c>public static T GetSlotDynamic{T}(TupleImpl{T * arity}, long index, T def)</c>.
    ///
    /// This static method is created in its own separate type-- these definitions can't be
    /// global methods since those don't allow type parameters at construction time,
    /// and they can't all be united under a single type since that type wouldn't be fully
    /// constructed when the call is emitted, which would cause an error.
    /// </summary>
    private MethodInfo GenGetSlotDynDefn(Type stTup, int arity)
    {
        Validation.AssertValue(stTup);
        Validation.Assert(stTup.IsGenericTypeDefinition);
        Validation.Assert(arity > 0);

        var tbGetSlotDyn = GetTypeBuilder(GetSlotDynName + arity);
        MethodBuilder meth = tbGetSlotDyn.DefineMethod(GetSlotDynName,
            MethodAttributes.Public | MethodAttributes.Static);

        var stSlot = meth.DefineGenericParameters("T")[0];
        var stHomTup = stTup.MakeGenericType(Util.Repeat(stSlot, arity));

        var bldrFlds = Immutable.Array<FieldInfo>.CreateBuilder(arity, init: true);
        foreach (var fin in stTup.GetFields())
        {
            int slot = FieldNameToSlot(fin.Name);
            if (slot < 0)
                continue;

            var homTupFin = TypeBuilder.GetField(stHomTup, fin).VerifyValue();
            bldrFlds[slot] = homTupFin;
        }

        var flds = bldrFlds.ToImmutable();
        Validation.Assert(flds.Length == arity);

        var stArgs = new Type[] { stHomTup, typeof(long), stSlot };

        meth.SetParameters(stArgs);
        meth.SetReturnType(stSlot);

        var ilw = new ILWriter(stArgs, meth.GetILGenerator());

        Label labRet = default;
        var labFlds = new Label[arity];
        for (int i = 0; i < arity; i++)
            labFlds[i] = ilw.DefineLabel();

        ilw
            .Ldarg(1)
            .Switch(labFlds)
            .Ldarg(2)
            .Br(ref labRet);

        for (int i = 0; i < arity; i++)
        {
            ilw
                .MarkLabel(labFlds[i])
                .Ldarg(0)
                .Ldfld(flds[i]);
            if (i < arity - 1)
                ilw.Br(ref labRet);
        }
        ilw
            .MarkLabel(labRet)
            .Ret();

        var stGetSlotDyn = tbGetSlotDyn.CreateTypeInfo();
        return stGetSlotDyn.GetMethod(GetSlotDynName).VerifyValue();
    }

    private bool TryMakeTupleDefault(DType type, SysTypeInfo sti, out (object value, bool special) entry)
    {
        Validation.Assert(type.IsTupleReq);
        var tti = sti.TupleInfo.VerifyValue();

        var res = tti.Ctor.Invoke(Type.EmptyTypes);
        var types = type.GetTupleSlotTypes();
        int arity = types.Length;
        for (int slot = 0; slot < arity; slot++)
        {
            var typeSlot = types[slot];
            if (typeSlot.IsOpt)
                continue;

            if (!TryEnsureDefaultValue(typeSlot, out var ent))
            {
                entry = default;
                return false;
            }

            if (ent.special)
            {
                var fin = tti.ValueFields[slot].VerifyValue();
                fin.SetValue(res, ent.value);
            }
        }

        entry = (res, true);
        return true;
    }

    /// <summary>
    /// Yields the field values and types of the given record. Checks that it is of the indicated record type.
    /// This is not yet optimized for production code but is suitable for test and harness usage.
    /// </summary>
    public IEnumerable<(TypedName tn, Type st, object val)> GetRecordFieldValues(DType type, RecordBase rec)
    {
        Validation.BugCheckParam(type.IsRecordXxx, nameof(type));
        Validation.BugCheckParam(TryGetSysTypeCore(type, out var sti), nameof(type));
        var rti = sti.RecordInfo.VerifyValue();

        if (rec != null)
            Validation.BugCheckParam(rti.SysType.IsAssignableFrom(rec.GetType()), nameof(rec));

        if (type.FieldCount == 0)
            return Array.Empty<(TypedName tn, Type st, object val)>();

        Validation.BugCheckValue(rec, nameof(rec));
        return GetRecordFieldValuesCore(type, rti, rec);
    }

    private IEnumerable<(TypedName tn, Type st, object val)> GetRecordFieldValuesCore(
        DType type, RecordSysTypeInfo rti, RecordBase rec)
    {
        Validation.Assert(type.IsRecordXxx);
        Validation.Assert(type.FieldCount > 0);
        Validation.AssertValue(rec);

        int slot = 0;
        foreach (var tn in type.GetNames())
        {
            Validation.Assert(type.TryGetNameType(tn.Name, out var t, out int index) && t == tn.Type && index == slot);
            var fin = rti.ValueFields[slot].VerifyValue();
            TryEnsureSysType(tn.Type, out var stFld).Verify();
            if (UseBitGet(tn.Type))
            {
                Validation.Assert(IsNullableTypeCore(stFld));
                Validation.Assert(fin.FieldType == GetSysTypeOrNull(tn.Type.ToReq()));
                var finBit = rti.GroupFields[slot >> 3];
                Validation.Assert(finBit.FieldType == typeof(byte));
                var bits = (byte)finBit.GetValue(rec);
                if ((bits & (1 << (slot & 0x07))) == 0)
                {
                    // Null value.
                    yield return (tn, stFld, null);
                    slot++;
                    continue;
                }
            }
            else
                Validation.Assert(fin.FieldType == stFld);

            yield return (tn, fin.FieldType, fin.GetValue(rec));
            slot++;
        }
        Validation.Assert(slot == type.FieldCount);
    }

    /// <summary>
    /// Yields the slot values and types of the given tuple. Checks that it is of the indicated tuple type.
    /// This is not yet optimized for production code but is suitable for test and harness usage.
    /// </summary>
    public (DType type, Type st, object val)[] GetTupleSlotValues(DType type, TupleBase tup)
    {
        Validation.BugCheckParam(type.IsTupleXxx, nameof(type));
        Validation.BugCheckParam(TryGetSysTypeCore(type, out var sti), nameof(type));
        var tti = sti.TupleInfo.VerifyValue();

        Validation.BugCheckValue(tup, nameof(tup));
        Validation.BugCheckParam(tti.SysType.IsAssignableFrom(tup.GetType()), nameof(tup));

        var types = type.GetTupleSlotTypes();
        if (types.Length == 0)
            return Array.Empty<(DType type, Type st, object val)>();

        Validation.Assert(tti.SysType.IsGenericType);
        var sts = tti.SysType.GetGenericArguments();
        Validation.Assert(sts.Length == types.Length);

        var res = new (DType type, Type st, object val)[types.Length];
        for (int slot = 0; slot < res.Length; slot++)
        {
            var fld = tti.ValueFields[slot].VerifyValue();
            Validation.Assert(fld.FieldType == sts[slot]);
            object v = fld.GetValue(tup);
            res[slot] = (types[slot], sts[slot], v);
        }
        return res;
    }

    /// <summary>
    /// Returns whether when a field of the given type is read, its corresponding bit should be read to deterimine
    /// whether the value is null.
    /// </summary>
    private static bool UseBitGet(DType type)
    {
        switch (type.Kind)
        {
        case DKind.R8:
        case DKind.R4:
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
        case DKind.Bit:
        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
            return type.IsOpt;

        case DKind.Record:
        case DKind.Module:
        case DKind.Tuple:
        case DKind.Tensor:
            // No need to read the bit since the value is a reference type.
            return false;

        case DKind.Uri:
        case DKind.Text:
            // We don't use the bit for types that are always opt.
            return false;

        case DKind.General:
        case DKind.Sequence:
        case DKind.Vac:
            // These aren't equatable types, so don't use the bit.
            return false;
        }

        // Should cover all kinds in the switch.
        Validation.Assert(false);
        return false;
    }

    /// <summary>
    /// Returns whether when a field of the given type is written, its corresponding bit should be written
    /// (when the value is non-null).
    /// </summary>
    private static bool UseBitSet(DType type)
    {
        switch (type.Kind)
        {
        case DKind.R8:
        case DKind.R4:
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
        case DKind.Bit:
        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
            return type.IsOpt;

        case DKind.Record:
        case DKind.Module:
        case DKind.Tuple:
        case DKind.Tensor:
            return true;

        case DKind.Uri:
        case DKind.Text:
            return false;

        case DKind.General:
        case DKind.Sequence:
        case DKind.Vac:
            return false;
        }

        // Should cover all kinds in the switch.
        Validation.Assert(false);
        return false;
    }

    /// <summary>
    /// Returns whether a field of this type should have its bit pre-set when the record is created.
    /// Such types include:
    /// * Non-opt value types like r8, i4, etc.
    /// * Equatable reference types that are always opt, such as string and Link.
    /// This is not true for types like sequence, record, tuple, tensor.
    /// </summary>
    private static bool UseBitPreset(DType type)
    {
        switch (type.Kind)
        {
        case DKind.R8:
        case DKind.R4:
        case DKind.IA:
        case DKind.I8:
        case DKind.I4:
        case DKind.I2:
        case DKind.I1:
        case DKind.U8:
        case DKind.U4:
        case DKind.U2:
        case DKind.U1:
        case DKind.Bit:
        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
            return !type.IsOpt;

        case DKind.Record:
        case DKind.Module:
        case DKind.Tuple:
        case DKind.Tensor:
            return false;

        case DKind.Uri:
        case DKind.Text:
            // Note: It is critical that these be true. The equality comparer functionality
            // depends on it.
            return true;

        case DKind.General:
        case DKind.Sequence:
        case DKind.Vac:
            return false;
        }

        // Should cover all kinds in the switch.
        Validation.Assert(false);
        return false;
    }
}

/// <summary>
/// The class for the arity-zero record.
/// REVIEW: This could be a singleton, but probably not worth special-casing code gen.
/// </summary>
public sealed class RecordImpl : RecordBase, IEquatable<RecordImpl>
{
    public RecordImpl()
        : base()
    {
    }

    protected override int GetHashCodeCore()
    {
        int hash = typeof(RecordImpl).GetHashCode();
        if (hash != 0)
            return hash;
        return 1;
    }

    protected override bool EqualsCore(object? obj) => Equals2(this, obj as RecordImpl);

    // All instances are considered equal.
    public static bool Equals2(RecordImpl a, RecordImpl b) => (a is null) == (b is null);
    public bool Equals(RecordImpl other) => Equals2(this, other);

    protected override void FillFlags(Span<byte> span)
    {
        Validation.Assert(span.Length == 0);
    }
}

/// <summary>
/// The class for the arity-zero tuple.
/// REVIEW: This could be a singleton, but probably not worth special-casing code gen.
/// REVIEW: Should we even support arity 0 and arity 1 tuples?
/// </summary>
public sealed class TupleImpl : TupleBase, IEquatable<TupleImpl>
{
    public TupleImpl()
        : base()
    {
    }

    protected override int GetHashCodeCore()
    {
        int hash = typeof(TupleImpl).GetHashCode();
        if (hash != 0)
            return hash;
        return 1;
    }

    protected override bool EqualsCore(object? obj) => Equals2(this, obj as TupleImpl);

    // All instances are considered equal.
    public static bool Equals2(TupleImpl a, TupleImpl b) => (a is null) == (b is null);
    public bool Equals(TupleImpl other) => Equals2(this, other);
}
