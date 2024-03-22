// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

/// <summary>
/// A TypeManager handles mapping from DType to system type, generating and caching system types as needed, as well
/// as related functionality, such as code generation related to the system types, binary serialization, etc.
/// </summary>
public abstract partial class TypeManager
{
    /// <summary>
    /// Returns whether the given system type is a reference type.
    /// </summary>
    public bool IsRefType(Type st)
    {
        Validation.BugCheckValue(st, nameof(st));
        Validation.BugCheckParam(st.IsClass || st.IsInterface || st.IsValueType, nameof(st));
        return !st.IsValueType;
    }

    /// <summary>
    /// Returns whether the given system type is a reference type.
    /// </summary>
    protected bool IsRefTypeCore(Type st)
    {
        Validation.AssertValue(st);
        Validation.Assert(st.IsClass || st.IsInterface || st.IsValueType);
        return !st.IsValueType;
    }

    /// <summary>
    /// Returns whether the given type is a nullable value type wrapper.
    /// </summary>
    public bool IsNullableType(Type st)
    {
        Validation.BugCheckValue(st, nameof(st));
        Validation.BugCheckParam(st.IsValueType || st.IsClass || st.IsInterface, nameof(st));
        return IsNullableTypeCore(st);
    }

    /// <summary>
    /// Returns whether the given type is a nullable value type wrapper.
    /// </summary>
    protected bool IsNullableTypeCore(Type st)
    {
        Validation.AssertValue(st);
        Validation.Assert(st.IsValueType || st.IsClass || st.IsInterface);
        return st.IsValueType && st.IsGenericType && st.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    /// Returns whether the given type is a nullable value type wrapper. If so, sets <paramref name="stReq"/>
    /// to the type argument..
    /// </summary>
    public bool IsNullableTypeCore(Type st, out Type stReq)
    {
        if (!IsNullableTypeCore(st))
        {
            stReq = null;
            return false;
        }
        stReq = st.GetGenericArguments()[0];
        return true;
    }

    protected virtual bool IsKnownRefType(DType type)
    {
        Validation.Assert(type.IsValid);

        if (type.SeqCount > 0)
            return true;

        switch (type.RootKind)
        {
        case DKind.General:
        case DKind.Text:
        case DKind.Record:
        case DKind.Tensor:
        case DKind.Uri:
            return true;
        }

        return false;
    }

    /// <summary>
    /// Load a null of the indicated type.
    /// </summary>
    public virtual void GenNull(MethodGenerator gen, DType type)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(type.IsOpt, nameof(type));

        // For known reference types, we don't need the system type.
        if (IsKnownRefType(type))
        {
            gen.Il.Ldnull();
            return;
        }

        Validation.BugCheck(TryEnsureSysType(type, out Type st));
        if (IsRefTypeCore(st))
        {
            gen.Il.Ldnull();
            return;
        }

        Validation.Assert(IsNullableTypeCore(st));
        var meth = CodeGenUtil.GetDefault(st);
        gen.Il.Call(meth);
    }

    /// <summary>
    /// Load a null of the indicated nullable type by initializing the specified local.
    /// It does not leave a null on the stack.
    /// </summary>
    public virtual void GenSetNull(MethodGenerator gen, DType type, in MethodGenerator.Local local)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(type.IsOpt && !IsKnownRefType(type), nameof(type));
        Validation.BugCheck(TryEnsureSysType(type, out Type st));
        Validation.BugCheck(IsNullableTypeCore(st));
        Validation.BugCheckParam(local.Type == st, nameof(local));
        gen.Il
            .Ldloca(local)
            .Initobj(st);
    }

    /// <summary>
    /// Wrap the current value on the execution stack, of the given type, as a nullable, if needed.
    /// </summary>
    public virtual void GenWrapOpt(MethodGenerator gen, DType typeSrc, DType typeDst)
    {
        Validation.BugCheckParam(typeSrc.IsValid, nameof(typeSrc));
        Validation.BugCheckParam(typeDst.IsValid, nameof(typeDst));
        Validation.BugCheckParam(typeSrc == typeDst || !typeSrc.IsOpt && typeSrc.ToOpt() == typeDst, nameof(typeDst));

        if (typeSrc == typeDst)
            return;

        // For known reference types, we don't need the system type to know that we don't need to wrap.
        if (IsKnownRefType(typeDst))
            return;

        Validation.BugCheck(TryEnsureSysType(typeDst, out Type stDst));
        if (IsRefTypeCore(stDst))
            return;

        GenWrapOptCore(gen, stDst);
    }

    private void GenWrapOptCore(MethodGenerator gen, Type st)
    {
        Validation.Assert(IsNullableTypeCore(st));
        gen.Il.Newobj(st.GetConstructor(st.GetGenericArguments()));
    }

    /// <summary>
    /// Unwraps the current value on the execution stack, of the given nullable type, as a non-nullable, if needed.
    /// </summary>
    internal virtual void GenUnwrapOpt(MethodGenerator gen, DType typeSrc, DType typeDst)
    {
        Validation.Assert(typeSrc.IsValid);
        Validation.Assert(typeDst.IsValid);
        Validation.Assert(typeSrc == typeDst || !typeDst.IsOpt && typeDst.ToOpt() == typeSrc);

        if (typeSrc == typeDst)
            return;

        // For known reference types, we don't need the system type to know that we don't need to unwrap.
        if (IsKnownRefType(typeSrc))
            return;

        Validation.BugCheck(TryEnsureSysType(typeSrc, out Type stSrc));
        if (IsRefTypeCore(stSrc))
            return;

        Validation.Assert(IsNullableTypeCore(stSrc));
        using var loc = gen.AcquireLocal(stSrc);
        gen.Il.Stloc(loc);
        GenGetValueOrDefaultCore(gen, loc);
    }

    /// <summary>
    /// Generate code to invoke the GetValueOrDefault function on a nullable.
    /// </summary>
    /// <returns>The underlying type of the nullable type.</returns>
    public virtual Type GenGetValueOrDefault(MethodGenerator gen, DType type, in LocArgInfo lai)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(type.IsPrimitiveOpt, nameof(type));
        Validation.BugCheckParam(type.HasReq, nameof(type));
        Validation.BugCheck(TryEnsureSysType(type, out var st));
        Validation.BugCheck(st == lai.SysType);
        return GenGetValueOrDefaultCore(gen, in lai);
    }

    private Type GenGetValueOrDefaultCore(MethodGenerator gen, in LocArgInfo lai)
    {
        Validation.Assert(IsNullableTypeCore(lai.SysType));
        var meth = lai.SysType.GetMethod("GetValueOrDefault", Type.EmptyTypes).VerifyValue();
        gen.Il
            .LdLocArgA(in lai)
            .Call(meth);
        return meth.ReturnType;
    }

    /// <summary>
    /// Generate code to invoke the HasValue property on a nullable.
    /// </summary>
    public virtual void GenHasValue(MethodGenerator gen, DType type, in LocArgInfo lai)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(type.IsPrimitiveOpt, nameof(type));
        Validation.BugCheckParam(type.HasReq, nameof(type));
        Validation.BugCheck(TryEnsureSysType(type, out var st));
        Validation.BugCheck(st == lai.SysType);
        GenHasValueCore(gen, in lai);
    }

    private void GenHasValueCore(MethodGenerator gen, in LocArgInfo lai)
    {
        Validation.Assert(IsNullableTypeCore(lai.SysType));
        gen.Il
            .LdLocArgA(in lai)
            .Call(lai.SysType.GetProperty("HasValue", Type.EmptyTypes).GetGetMethod());
    }

    internal virtual void GenIsNull(MethodGenerator gen, DType type)
    {
        Validation.AssertValue(gen);
        Validation.Assert(type.IsOpt);

        if (IsKnownRefType(type))
        {
            gen.Il
                .Ldnull()
                .Ceq();
            return;
        }

        Validation.BugCheck(TryEnsureSysType(type, out Type st));
        if (IsRefTypeCore(st))
        {
            gen.Il
                .Ldnull()
                .Ceq();
            return;
        }

        Validation.Assert(IsNullableTypeCore(st));
        using var loc = gen.AcquireLocal(st);
        gen.Il.Stloc(loc);
        GenHasValueCore(gen, loc);
        gen.Il
            .Ldc_I4(0)
            .Ceq();
    }

    internal virtual void GenIsNotNull(MethodGenerator gen, DType type)
    {
        Validation.AssertValue(gen);
        Validation.Assert(type.IsOpt);

        if (IsKnownRefType(type))
        {
            gen.Il
                .Ldnull()
                .Cgt_Un();
            return;
        }

        Validation.BugCheck(TryEnsureSysType(type, out Type st));
        if (IsRefTypeCore(st))
        {
            gen.Il
                .Ldnull()
                .Cgt_Un();
            return;
        }

        Validation.Assert(IsNullableTypeCore(st));
        using var loc = gen.AcquireLocal(st);
        gen.Il.Stloc(loc);
        GenHasValueCore(gen, loc);
    }

    /// <summary>
    /// Generate IL to load the given field from the indicated record. On entry, the record object is
    /// at the top of the execution stack. Note that we're passing all the various type information
    /// mostly so we can assert that the caller and this code are "on the same page", meaning, that
    /// there hasn't been some mishandling of types in the CodeGenerator or Binder.
    /// </summary>
    public virtual void GenLoadField(MethodGenerator gen, DType typeRec, Type stRec, DName nameFld, DType typeFld)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeRec.IsRecordReq, nameof(typeRec));
        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti) && sti.SysType == stRec, nameof(stRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out var typeFld2, out int slot), nameof(nameFld));
        Validation.BugCheckParam(typeFld == typeFld2, nameof(typeFld));
        TryEnsureSysType(typeFld, out Type stFld).Verify();

        var rti = sti.RecordInfo.VerifyValue();
        var fin = rti.ValueFields[slot];
        Validation.Assert(fin != null);
        Validation.Assert(fin.IsPublic);
        Validation.Assert(!fin.IsStatic);

        if (UseBitGet(typeFld))
        {
            Validation.Assert(typeFld.HasReq);
            Validation.Assert(fin.FieldType == stFld.GenericTypeArguments[0]);

            var fldB = rti.GroupFields[slot >> 3].VerifyValue();
            Label labDef = default;
            Label labDone = default;
            gen.Il
                .Dup().Ldfld(fldB).Ldc_I4(1 << (slot & 0x07)).And().Brfalse(ref labDef)
                .Ldfld(fin);
            GenWrapOptCore(gen, stFld);
            gen.Il
                .Br(ref labDone)
                .MarkLabel(labDef)
                .Pop();
            GenNull(gen, typeFld);
            gen.Il.MarkLabel(labDone);
        }
        else
        {
            Validation.Assert(!IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == stFld);
            gen.Il.Ldfld(fin);
        }
    }

    /// <summary>
    /// Generate IL to load the req field value. For example, if the field type is i8?, this will load
    /// the underlying i8 value.
    /// </summary>
    public virtual void GenLoadFieldReq(MethodGenerator gen, DType typeRec, Type stRec, DName nameFld, DType typeFldReq)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeRec.IsRecordReq, nameof(typeRec));
        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti) && sti.SysType == stRec, nameof(stRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out var typeFldOpt, out int slot), nameof(nameFld));
        Validation.BugCheckParam(!typeFldReq.IsOpt && typeFldReq.ToOpt() == typeFldOpt, nameof(typeFldReq));
        Validation.BugCheckParam(UseBitGet(typeFldOpt), nameof(typeFldReq));

        TryEnsureSysType(typeFldReq, out Type stFldReq).Verify();

        var rti = sti.RecordInfo.VerifyValue();
        var fld = rti.ValueFields[slot];
        Validation.Assert(fld != null);
        Validation.Assert(fld.IsPublic);
        Validation.Assert(!fld.IsStatic);
        Validation.Assert(fld.FieldType == stFldReq);

        gen.Il.Ldfld(fld);
    }

    /// <summary>
    /// Generate IL to load the "bit" for the given field from the indicated record. On entry, the
    /// record object is at the top of the execution stack. On exit the record object is gone and
    /// the stack has a byte with at most one bit set. That byte will be non-zero iff the field value
    /// is not null. This is only valid to be called when the field type has a required form and isn't
    /// a reference type, ie, when the system type is Nullable{T} for some T.
    /// 
    /// Note that we're passing all the various type information mostly so we can assert that the
    /// caller and this code are "on the same page", meaning, that there hasn't been some mishandling
    /// of types in the CodeGenerator or Binder.
    /// </summary>
    public virtual void GenLoadFieldBit(MethodGenerator gen, DType typeRec, Type stRec, DName nameFld, DType typeFld)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeRec.IsRecordReq, nameof(typeRec));
        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti) && sti.SysType == stRec, nameof(stRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out var typeFld2, out int slot), nameof(nameFld));
        Validation.BugCheckParam(typeFld == typeFld2, nameof(typeFld));
        Validation.BugCheckParam(UseBitGet(typeFld), nameof(typeFld));
        Validation.Assert(typeFld.HasReq);

        var rti = sti.RecordInfo.VerifyValue();
        var fldB = rti.GroupFields[slot >> 3].VerifyValue();
        gen.Il.Ldfld(fldB).Ldc_I4(1 << (slot & 0x07)).And();
    }

    /// <summary>
    /// Get the system type and static <c>Equals2</c> method info for the given agg type.
    /// Returns <c>false</c> if the system type can't be generated or the system type is not equatable.
    /// </summary>
    public virtual bool TryGetAggEquals2(DType typeAgg, out Type stAgg, out MethodInfo meth)
    {
        Validation.BugCheckParam(typeAgg.IsAggXxx, nameof(typeAgg));
        if (TryGetSysTypeCore(typeAgg, out var sti))
        {
            var ati = sti.AggInfo.VerifyValue();
            stAgg = ati.SysType;
            meth = ati.Equals2;
            return meth != null;
        }

        stAgg = null;
        meth = null;
        return false;
    }

    /// <summary>
    /// Generate IL to map a null input record to a null output record. On entry, the source
    /// record is on the top of the evaluation stack. On exit the same should be true for the
    /// the fall through case. For the "done" case (when src is null), the src should be gone
    /// and a null destination record should be on the top of the execution stack.
    /// 
    /// Note that the type manager handles this so it could "wrap" the record object in a
    /// wrapper object that is never null. We may want to do this, for example, to transport
    /// meta information through, such as errors. Of course, for that to work, we'll also need
    /// to delegate record object creation to the type manager, etc.
    /// </summary>
    internal virtual void GenMapNullRecordToNullRecord(
        MethodGenerator gen, Label labDone,
        DType typeSrc, Type stSrc, DType typeDst, Type stDst)
    {
        Validation.AssertValue(gen);
        Validation.Assert(typeSrc.IsRecordOpt);
        Validation.AssertValue(stSrc);
        Validation.Assert(GetSysTypeOrNull(typeSrc) == stSrc);
        Validation.Assert(typeDst.IsRecordOpt);
        Validation.AssertValue(stDst);
        Validation.Assert(GetSysTypeOrNull(typeDst) == stDst);

        // Note: we could just do IL.Dup().Brfalse(labDone), but then we'd be lying to the JIT
        // about types. Better to pop the src null and push a dst null.
        Label labGood = default;
        gen.Il
            .Dup()
            .Brtrue(ref labGood)
            .Pop()
            .Ldnull()
            .Br(ref labDone)
            .MarkLabel(labGood);
    }

    /// <summary>
    /// Generate IL to load the given slot from the indicated tuple. On entry, the tuple object is
    /// at the top of the execution stack. Note that we're passing all the various type information
    /// mostly so we can assert that the caller and this code are "on the same page", meaning, that
    /// there hasn't been some mishandling of types in the CodeGenerator or Binder.
    /// </summary>
    public virtual void GenLoadSlot(MethodGenerator gen, DType typeTup, Type stTup, int slot, DType typeFld, bool addr = false)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeTup.IsTupleReq, nameof(typeTup));
        Validation.BugCheckParam(TryGetSysTypeCore(typeTup, out var sti) && sti.SysType == stTup, nameof(stTup));
        var types = typeTup.GetTupleSlotTypes();
        Validation.BugCheckIndex(slot, types.Length, nameof(slot));
        Validation.BugCheckParam(types[slot] == typeFld, nameof(typeFld));

        var tti = sti.TupleInfo.VerifyValue();
        var fld = tti.ValueFields[slot];
        Validation.Assert(fld != null);
        Validation.Assert(fld.IsPublic);
        Validation.Assert(!fld.IsStatic);
        if (addr)
            gen.Il.Ldflda(fld);
        else
            gen.Il.Ldfld(fld);
    }

    /// <summary>
    /// Generate IL to store a value to the given slot. On entry, the tuple object and
    /// value to store are at the top of the execution stack.
    /// </summary>
    public virtual void GenStoreSlot(MethodGenerator gen, DType typeTup, Type stTup, int slot, DType typeFld)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeTup.IsTupleReq, nameof(typeTup));
        Validation.BugCheckParam(TryGetSysTypeCore(typeTup, out var sti) && sti.SysType == stTup, nameof(stTup));
        var types = typeTup.GetTupleSlotTypes();
        Validation.BugCheckIndex(slot, types.Length, nameof(slot));
        Validation.BugCheckParam(types[slot] == typeFld, nameof(typeFld));

        var tti = sti.TupleInfo.VerifyValue();
        var fld = tti.ValueFields[slot];
        Validation.Assert(fld != null);
        Validation.Assert(fld.IsPublic);
        Validation.Assert(!fld.IsStatic);
        gen.Il.Stfld(fld);
    }

    public virtual void GenCreateTuple(MethodGenerator gen, DType typeTup, Type stTup)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeTup.IsTupleXxx, nameof(typeTup));
        Validation.BugCheckValue(stTup, nameof(stTup));
        Validation.BugCheckParam(GetSysTypeOrNull(typeTup) == stTup, nameof(stTup));

        var ctor = stTup.GetConstructor(Type.EmptyTypes);
        Validation.AssertValue(ctor);
        gen.Il.Newobj(ctor);
    }

    public virtual void GenCastNum(MethodGenerator gen, DType typeSrc, DType typeDst)
    {
        Validation.BugCheckValue(gen, nameof(gen));
        Validation.BugCheckParam(typeSrc.IsNumericReq, nameof(typeSrc));
        Validation.BugCheckParam(typeDst.IsNumericReq, nameof(typeDst));

        // The destination type must accept the source type. For numeric types, the acceptance
        // mode is irrelevant.
        Validation.Assert(typeDst.Accepts(typeSrc, DType.UseUnionOper));

        Type stSrc = GetSysTypeOrNull(typeSrc).VerifyValue();
        Type stDst = GetSysTypeOrNull(typeDst).VerifyValue();

        // If typeSrc and typeDst have the same underlying System.Type representation, we don't need conversion.
        // For example, R8 and Number are both represented using System.Double.
        // REVIEW: This shouldn't happen anymore unless typeSrc == typeDst.
        if (stSrc == stDst)
            return;

        var kindSrc = typeSrc.Kind;
        var kindDst = typeDst.Kind;
        if (kindSrc == DKind.IA)
        {
            Validation.Assert(typeDst.Kind.IsRx());

            switch (kindDst)
            {
            case DKind.R8:
                gen.Il.Call(CodeGenUtil.IntToR8);
                return;
            case DKind.R4:
                gen.Il.Call(CodeGenUtil.IntToR4);
                return;
            }
            throw Validation.BugExcept();
        }

        if (kindDst == DKind.IA)
        {
            switch (kindSrc)
            {
            case DKind.I8:
                gen.Il.Newobj(CodeGenUtil.CtorIntFromI8);
                return;
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
                gen.Il.Newobj(CodeGenUtil.CtorIntFromI4);
                return;
            case DKind.U8:
                gen.Il.Newobj(CodeGenUtil.CtorIntFromU8);
                return;
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
            case DKind.Bit:
                gen.Il.Newobj(CodeGenUtil.CtorIntFromU4);
                return;
            }
            throw Validation.BugExcept();
        }

        // Critical note: The CLI stack model doesn't distinguish between signed and unsigned integer
        // values on the stack. The difference is entirely in the instructions operating on those
        // values. Thus, both the source and destination types determine the IL to use, not just
        // the destination type.

        if (kindDst.IsRx())
        {
            switch (kindSrc)
            {
            case DKind.U8:
                // Standard conversion from ulong to double/float is incorrect if the high bit is set.
                // See the comments on NumUtil.ToR8(ulong) and NumUtil.ToR4(ulong).
                switch (kindDst)
                {
                case DKind.R8: gen.Il.Call(new Func<ulong, double>(NumUtil.ToR8).Method); return;
                case DKind.R4: gen.Il.Call(new Func<ulong, float>(NumUtil.ToR4).Method); return;
                default:
                    throw Validation.BugExcept();
                }
            case DKind.U4:
                // REVIEW: Does this suffer a flaw similar to ulong? It doesn't seem to.
                gen.Il.Conv_R_Un();
                break;
            case DKind.U2:
            case DKind.U1:
                // These don't need the prefix since they are on the stack as i4/u4 values with zero
                // for the high byte.
                break;
            case DKind.I8:
                // When the JIT is constant folding, long to float (via conv.r4) can be incorrect.
                // See the comments on NumUtil.ToR4(long).
                if (kindDst == DKind.R4)
                {
                    gen.Il.Call(new Func<long, float>(NumUtil.ToR4).Method);
                    return;
                }
                break;
            }
            switch (kindDst)
            {
            case DKind.R8:
                gen.Il.Conv_R8();
                return;
            case DKind.R4:
                gen.Il.Conv_R4();
                return;
            }
            throw Validation.BugExcept();
        }

        Validation.Assert(kindSrc.IsIxOrUx());
        Validation.Assert(kindDst.IsIxOrUx());
        int sizeSrc = kindSrc.NumericSize();
        int sizeDst = kindDst.NumericSize();

        // We're down to fixed sized integer types. The src size should
        // be less than the dst size, except when dst is I8. All fixed
        // sized integer types are accepted by I8.
        Validation.Assert((sizeSrc & (sizeSrc - 1)) == 0);
        Validation.Assert((sizeDst & (sizeDst - 1)) == 0);
        Validation.Assert(0 <= sizeSrc & sizeSrc <= sizeDst & sizeDst <= 8);
        if (sizeSrc == 8)
        {
            // U8 to I8 is a no-op at the IL level. The JIT/verifier only tracks
            // size (4 or 8 bytes) of integer values, not their signed-ness.
            Validation.Assert(sizeDst == 8);
            Validation.Assert(kindSrc == DKind.U8);
            Validation.Assert(kindDst == DKind.I8);
            return;
        }

        Validation.Assert(sizeSrc < sizeDst);

        bool signedSrc = kindSrc.IsIx();
        bool signedDst = kindDst.IsIx();
        Validation.Assert(!signedSrc || signedDst);
        switch (sizeDst)
        {
        case 1:
        case 2:
        case 4:
            // Src is already sign extended to 4 bytes, so don't need to do anything.
            return;
        case 8:
            // Sign extend to 8 bytes.
            if (signedSrc)
                gen.Il.Conv_I8();
            else
                gen.Il.Conv_U8();
            return;
        }

        throw Validation.BugExcept();
    }

    /// <summary>
    /// Try to get equality comparer information for the given agg (record/tuple) type. Returns false if
    /// either the system type for the agg type can't be created or the type is not equatable. In the latter
    /// case, the <paramref name="info"/> will have the item system type filled in but everything else
    /// will be <c>null</c>.
    /// </summary>
    public virtual bool TryGetAggEqCmp(DType typeAgg, out EqCmpInfo info)
    {
        Validation.BugCheckParam(typeAgg.IsAggXxx, nameof(typeAgg));

        if (!TryGetSysTypeCore(typeAgg, out var sti))
        {
            info = default;
            return false;
        }

        var ati = sti.AggInfo.VerifyValue();
        info = ati.EqCmpInfo;
        return ati.IsEquatable;
    }
}
