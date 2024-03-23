// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection.Emit;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

// This partial is for generating classes and code to construct record or tuple objects.
partial class TypeManager
{
    /// <summary>
    /// Create a record generator for the given method generator and record type.
    /// </summary>
    public RecordGenerator CreateRecordGenerator(MethodGenerator gen, DType typeRec,
        Action<RecordRuntimeTypeInfo> genLoadRrti)
    {
        return RecordGenerator.Create(this, gen, typeRec, genLoadRrti, partial: false);
    }

    public bool TryEnsureRrti(DType type, out RecordRuntimeTypeInfo rrti)
    {
        Validation.BugCheckParam(type.IsRecordXxx, nameof(type));

        if (!TryGetSysTypeCore(type, out var sti))
        {
            rrti = null;
            return false;
        }

        rrti = EnsureRrti(type, sti.RecordInfo.VerifyValue());
        return true;
    }

    protected RrtiImpl EnsureRrti(DType type, RecordSysTypeInfo rsti)
    {
        Validation.Assert(type.IsRecordXxx);
        Validation.AssertValue(rsti);

        var typeReq = type.GetReqFieldType();
        if (!_rrtiMap.TryGetValue(typeReq, out var rrti))
        {
            rrti = new RrtiImpl(rsti, typeReq);
            rrti = _rrtiMap.GetOrAdd(typeReq, rrti);
        }
        Validation.Assert(rrti.Rsti == rsti);

        return rrti;
    }

    /// <summary>
    /// Used to generate code to create rexl record instances.
    /// </summary>
    public sealed class RecordGenerator : IDisposable
    {
        private readonly TypeManager _tm;
        private readonly MethodGenerator _gen;
        private readonly Action<RecordRuntimeTypeInfo> _genLoadRrti;

        private readonly RrtiImpl _rrti;
        private readonly RecordSysTypeInfo _rsti;

        // The fields that are required (non-opt).
        private readonly Immutable.Array<byte> _bitsReq;
        // The bits that should be pre-set.
        private readonly Immutable.Array<byte> _bitsPreset;

        /// <summary>
        /// The record type that this builder is responsible for. Whether this type is opt or req is of no
        /// consequence, but it will always be a record type.
        /// </summary>
        public DType RecType { get; }

        /// <summary>
        /// The system type corresponding to the <see cref="RecType"/>.
        /// </summary>
        public Type RecSysType => _rsti.SysType;

        /// <summary>
        /// The record runtime type information. The client must get this on the execution stack
        /// immediately before calling 
        /// </summary>
        public RecordRuntimeTypeInfo Rrti => _rrti;

        // The local holding the new record instance.
        private MethodGenerator.Local _locRec;

        // The fields that have been assigned on the current record.
        private byte[] _assigned;

        // Whether the final field assignment validation should be skipped because this is
        // a "partial" assignment of fields.
        private bool _partial;

        private enum State
        {
            None,
            Started,
            WaitingForPost,
            Done,
        }

        private State _state;

        // These are set by "Pre" and used by "Post".
        private DType _typeFld;
        private Type _stFld;
        private int _slotFld;
        private bool _fromReq;

        private RecordGenerator(
            TypeManager tm, MethodGenerator gen, DType typeRec, RrtiImpl rrti,
            Immutable.Array<byte> bitsPreset, Immutable.Array<byte> bitsReq,
            Action<RecordRuntimeTypeInfo> genLoadRrti)
        {
            Validation.AssertValue(tm);
            Validation.AssertValue(gen);
            Validation.Assert(typeRec.IsRecordReq);
            Validation.AssertValue(rrti);
            Validation.Assert(bitsReq.Length == rrti.Rsti.GroupFields.Length);
            Validation.Assert(bitsPreset.Length == rrti.Rsti.GroupFields.Length);
            Validation.AssertValueOrNull(genLoadRrti);

            _tm = tm;
            _gen = gen;
            _genLoadRrti = genLoadRrti;

            _rrti = rrti;
            _rsti = rrti.Rsti;

            _bitsReq = bitsReq;
            _bitsPreset = bitsPreset;

            RecType = typeRec;

            _locRec = _gen.AcquireLocal(RecSysType);
            _assigned = new byte[bitsPreset.Length];

            _state = State.None;
        }

        public void Dispose()
        {
            if (_state != State.Done)
            {
                _locRec.Dispose();
                _state = State.Done;
            }
        }

        /// <summary>
        /// Create a record generator for the given type manager, method generator, and record type.
        /// If <paramref name="partial"/> is true, the final field assignment verification is skipped.
        /// Warning: this should only be done when absolutely necessary and when it is guaranteed that
        /// there will be no subsequent reads of unassigned fields. If a non-req reference type field
        /// is not properly written, reading from it may violate the type system.
        /// </summary>
        internal static RecordGenerator Create(TypeManager tm, MethodGenerator gen, DType typeRec,
            Action<RecordRuntimeTypeInfo> genLoadRrti, bool partial = false)
        {
            Validation.AssertValue(tm);
            Validation.BugCheckValue(gen, nameof(gen));
            Validation.BugCheckParam(typeRec.IsRecordReq, nameof(typeRec));
            Validation.BugCheckParam(tm.TryGetSysTypeCore(typeRec, out var sti), nameof(typeRec));
            Validation.Assert(sti.RecordInfo != null);
            Validation.BugCheckValueOrNull(genLoadRrti);

            var typeReq = typeRec.GetReqFieldType();
            var rrti = tm.EnsureRrti(typeReq, sti.RecordInfo);
            Validation.Assert(rrti.Rsti == sti.RecordInfo);

            int cval = typeRec.FieldCount;
            int cgrp = (cval + 7) >> 3;

            var bldrReq = Immutable.Array<byte>.CreateBuilder(cgrp, init: true);
            var bldrPre = Immutable.Array<byte>.CreateBuilder(cgrp, init: true);
            int slot = 0;
            foreach (var tn in typeRec.GetNames())
            {
                if (!tn.Type.IsOpt)
                    bldrReq[slot >> 3] |= (byte)(1 << (slot & 0x07));
                if (UseBitPreset(tn.Type))
                    bldrPre[slot >> 3] |= (byte)(1 << (slot & 0x07));
                slot++;
            }
            Validation.Assert(slot == cval);
            var bitsReq = bldrReq.ToImmutable();
            var bitsPreset = bldrPre.ToImmutable();

            return new RecordGenerator(tm, gen, typeRec, rrti, bitsPreset, bitsReq, genLoadRrti)
                .Start(partial);
        }

        private RecordGenerator Start(bool partial)
        {
            Validation.Assert(_state == State.None);

            var ilw = _gen.Il;
            _partial = partial;

            ilw.Newobj(_rsti.Ctor);
            for (int grp = 0; grp < _rsti.GroupFields.Length; grp++)
            {
                var bits = _bitsPreset[grp];
                if (bits != 0)
                    ilw.Dup().Ldc_I4(bits).Stfld(_rsti.GroupFields[grp]);
            }
            ilw.Stloc(_locRec);

            _state = State.Started;
            return this;
        }

        private void SetAssign(int slot)
        {
            Validation.AssertIndex(slot, RecType.FieldCount);
            int grp = slot >> 3;
            byte mask = (byte)(1 << (slot & 0x07));
            Validation.BugCheck((_assigned[grp] & mask) == 0, "Can't set field more than once");
            _assigned[grp] |= mask;
        }

        /// <summary>
        /// Set the value of the given field from the value in a local/arg.
        /// </summary>
        public RecordGenerator SetFromLocal(DName nameFld, in LocArgInfo laiSrc)
        {
            Validation.BugCheck(_state == State.Started);
            Validation.BugCheckParam(RecType.TryGetNameType(nameFld, out var typeFld, out int slot), nameof(nameFld));
            _tm.TryEnsureSysType(typeFld, out Type stFld).Verify();
            Validation.BugCheckParam(laiSrc.SysType == stFld, nameof(laiSrc));

            var fld = _rsti.ValueFields[slot].VerifyValue();
            Validation.Assert(fld.IsPublic);
            Validation.Assert(!fld.IsStatic);

            SetAssign(slot);
            if (!UseBitSet(typeFld))
            {
                // The bit is either pre-set or is not used.
                Validation.Assert(!_tm.IsNullableTypeCore(stFld));
                Validation.Assert(fld.FieldType == stFld);
                _gen.Il
                    .Ldloc(_locRec)
                    .LdLocArg(in laiSrc)
                    .Stfld(fld);
            }
            else if (!UseBitGet(typeFld))
            {
                Validation.Assert(_tm.IsRefTypeCore(stFld));
                Validation.Assert(fld.FieldType == stFld);

                // When the value is not null, set both the value and the bit. When the value is null, do nothing.
                var fldB = _rsti.GroupFields[slot >> 3].VerifyValue();
                Label labSkip = default;

                _gen.Il
                    .LdLocArg(in laiSrc)
                    .Brfalse(ref labSkip)
                    .Ldloc(_locRec).Dup().Dup()
                    .Ldfld(fldB).Ldc_I4(1 << (slot & 0x07)).Or().Stfld(fldB)
                    .LdLocArg(in laiSrc)
                    .Stfld(fld)
                    .MarkLabel(labSkip);
            }
            else
            {
                // We want the bit to match the nullness of the value (even for reference types like record).
                Validation.Assert(_tm.IsNullableTypeCore(stFld));
                Validation.Assert(fld.FieldType == stFld.GenericTypeArguments[0]);

                // When the value is not null, set both the value and the bit. When the value is null, do nothing.
                var fldB = _rsti.GroupFields[slot >> 3].VerifyValue();
                Label labSkip = default;

                _tm.GenHasValueCore(_gen, in laiSrc);
                _gen.Il
                    .Brfalse(ref labSkip)
                    .Ldloc(_locRec).Dup().Dup()
                    .Ldfld(fldB).Ldc_I4(1 << (slot & 0x07)).Or().Stfld(fldB);
                _tm.GenGetValueOrDefaultCore(_gen, in laiSrc);
                _gen.Il
                    .Stfld(fld)
                    .MarkLabel(labSkip);
            }

            return this;
        }

        /// <summary>
        /// Set the value of the given field from the value of a field in a source record, where the
        /// source record is in a local/arg.
        /// </summary>
        public RecordGenerator SetFromLocalField(DName nameFld, DType typeRecSrc, DName nameSrc, in LocArgInfo laiSrc)
        {
            Validation.BugCheck(_state == State.Started);
            Validation.BugCheckParam(RecType.TryGetNameType(nameFld, out var typeFld, out int slotDst), nameof(nameFld));
            Validation.BugCheckParam(typeRecSrc.IsRecordReq, nameof(typeRecSrc));
            Validation.BugCheckParam(_tm.TryGetSysTypeCore(typeRecSrc, out var stiRecSrc), nameof(typeRecSrc));
            Validation.BugCheckParam(typeRecSrc.TryGetNameType(nameSrc, out var typeFld2, out int slotSrc), nameof(nameSrc));
            Validation.BugCheckParam(typeFld == typeFld2, nameof(nameSrc));

            var rtiRecSrc = stiRecSrc.RecordInfo.VerifyValue();

            Validation.BugCheckParam(laiSrc.SysType == rtiRecSrc.SysType, nameof(laiSrc));

            _tm.TryEnsureSysType(typeFld, out Type stFld).Verify();

            var fldDst = _rsti.ValueFields[slotDst].VerifyValue();
            Validation.Assert(fldDst.IsPublic);
            Validation.Assert(!fldDst.IsStatic);

            var fldSrc = rtiRecSrc.ValueFields[slotSrc].VerifyValue();
            Validation.Assert(fldSrc.IsPublic);
            Validation.Assert(!fldSrc.IsStatic);

            Validation.Assert(fldDst.FieldType == fldSrc.FieldType);

            SetAssign(slotDst);
            if (!UseBitSet(typeFld))
            {
                // The bit is either pre-set or is not used, so just copy the value.
                Validation.Assert(!_tm.IsNullableTypeCore(stFld));
                Validation.Assert(fldDst.FieldType == stFld);
                _gen.Il
                    .Ldloc(_locRec)
                    .LdLocArg(in laiSrc)
                    .Ldfld(fldSrc)
                    .Stfld(fldDst);
            }
            else if (!UseBitGet(typeFld))
            {
                Validation.Assert(_tm.IsRefTypeCore(stFld));
                Validation.Assert(fldDst.FieldType == stFld);

                var fldBDst = _rsti.GroupFields[slotDst >> 3].VerifyValue();

                Label labSkip = default;
                _gen.Il
                    .LdLocArg(in laiSrc).Ldfld(fldSrc)
                    .Brfalse(ref labSkip)
                    .Ldloc(_locRec).Dup().Dup()
                    .Ldfld(fldBDst).Ldc_I4(1 << (slotDst & 0x07)).Or().Stfld(fldBDst)
                    .LdLocArg(in laiSrc).Ldfld(fldSrc)
                    .Stfld(fldDst)
                    .MarkLabel(labSkip);
            }
            else
            {
                Validation.Assert(_tm.IsNullableTypeCore(stFld));
                Validation.Assert(fldDst.FieldType == stFld.GenericTypeArguments[0]);

                var fldBDst = _rsti.GroupFields[slotDst >> 3].VerifyValue();
                var fldBSrc = rtiRecSrc.GroupFields[slotSrc >> 3].VerifyValue();

                // When the bit is set, copy over both bit and value.
                Label labSkip = default;
                _gen.Il
                    .LdLocArg(in laiSrc).Ldfld(fldBSrc).Ldc_I4(1 << (slotSrc & 0x07)).And().Brfalse(ref labSkip)
                    .Ldloc(_locRec).Dup().Dup()
                    .Ldfld(fldBDst).Ldc_I4(1 << (slotDst & 0x07)).Or().Stfld(fldBDst)
                    .LdLocArg(in laiSrc).Ldfld(fldSrc).Stfld(fldDst)
                    .MarkLabel(labSkip);
            }

            return this;
        }

        /// <summary>
        /// Prepare to set the value of the given field from a value that will be on the stack.
        /// This is called before the value for the field is placed on the stack.
        /// </summary>
        public RecordGenerator SetFromStackPre(DName nameFld, DType typeFld, bool fromReq = false)
        {
            Validation.BugCheck(_state == State.Started);
            Validation.BugCheckParam(RecType.TryGetNameType(nameFld, out _typeFld, out _slotFld), nameof(nameFld));
            Validation.BugCheckParam(_typeFld == typeFld, nameof(typeFld));

            SetAssign(_slotFld);
            _fromReq = fromReq;
            if (_fromReq)
            {
                Validation.BugCheckParam(_typeFld.HasReq, nameof(fromReq));
                Validation.Assert(UseBitSet(_typeFld));
                _typeFld = _typeFld.ToReq();
            }
            _tm.TryEnsureSysType(_typeFld, out _stFld).Verify();

            if (!UseBitSet(_typeFld) || _fromReq || _tm.IsRefTypeCore(_stFld))
                _gen.Il.Ldloc(_locRec);

            _state = State.WaitingForPost;
            return this;
        }

        /// <summary>
        /// Set the value of the field from the value on the stack. This is called after the value for the field
        /// is placed on the stack.
        /// </summary>
        public RecordGenerator SetFromStackPost()
        {
            Validation.BugCheck(_state == State.WaitingForPost);
            _tm.TryEnsureSysType(_typeFld, out Type stFld).Verify();

            var fld = _rsti.ValueFields[_slotFld].VerifyValue();
            Validation.Assert(fld.IsPublic);
            Validation.Assert(!fld.IsStatic);

            if (!_fromReq && !UseBitSet(_typeFld))
            {
                // The bit is either pre-set or is not used.
                Validation.Assert(!_fromReq);
                Validation.Assert(!_tm.IsNullableTypeCore(stFld));
                Validation.Assert(fld.FieldType == stFld);

                // The record and value are on the stack.
                _gen.Il.Stfld(fld);
            }
            else
            {
                var fldB = _rsti.GroupFields[_slotFld >> 3].VerifyValue();

                if (_fromReq)
                {
                    // Pre set _typeFld to be the req.
                    Validation.Assert(!_typeFld.HasReq);
                    Validation.Assert(!_tm.IsNullableTypeCore(stFld));
                    Validation.Assert(fld.FieldType == stFld);

                    // We know the value is not null, so set both the value and the bit.
                    // The record and (req) value are on the stack.
                    _gen.Il
                        .Stfld(fld)
                        .Ldloc(_locRec).Dup()
                        .Ldfld(fldB).Ldc_I4(1 << (_slotFld & 0x07)).Or().Stfld(fldB);
                }
                else if (_tm.IsRefTypeCore(stFld))
                {
                    // The reference case. We still want the bit to match the null-ness of the value.
                    Validation.Assert(!_tm.IsNullableTypeCore(stFld));
                    Validation.Assert(fld.FieldType == stFld);

                    // When the value is not null, set both the value and the bit. When the value is null, just clean
                    // up the stack.
                    Label labPop = default;
                    Label labDone = default;

                    // The record and (ref) value are on the stack.
                    _gen.Il
                        .Dup()
                        .Brfalse(ref labPop)
                        .Stfld(fld)
                        .Ldloc(_locRec).Dup()
                        .Ldfld(fldB).Ldc_I4(1 << (_slotFld & 0x07)).Or().Stfld(fldB)
                        .Br(ref labDone)
                        .MarkLabel(labPop)
                        .Pop().Pop()
                        .MarkLabel(labDone);
                }
                else
                {
                    // The Nullable<T> case.
                    Validation.Assert(_tm.IsNullableTypeCore(stFld));
                    Validation.Assert(fld.FieldType == stFld.GenericTypeArguments[0]);

                    // When the value is not null, set both the value and the bit. When the value is null, do nothing.
                    Label labPop = default;
                    using var loc = _gen.AcquireLocal(stFld);

                    // The (nullable) value is on the stack, but the record is not.
                    _gen.Il.Stloc(loc);
                    _tm.GenHasValueCore(_gen, loc);
                    _gen.Il
                        .Brfalse(ref labPop)
                        .Ldloc(_locRec);
                    _tm.GenGetValueOrDefaultCore(_gen, loc);
                    _gen.Il
                        .Stfld(fld)
                        .Ldloc(_locRec).Dup()
                        .Ldfld(fldB).Ldc_I4(1 << (_slotFld & 0x07)).Or().Stfld(fldB)
                        .MarkLabel(labPop);
                }
            }

            _state = State.Started;
            return this;
        }

        /// <summary>
        /// Load the address of the field for assignment as an out parameter. This is only valid
        /// for req (non-opt) non-reference types, eg, numeric types.
        /// </summary>
        public RecordGenerator LoadFieldAddr(DName nameFld)
        {
            Validation.BugCheck(_state == State.Started);
            Validation.BugCheckParam(RecType.TryGetNameType(nameFld, out var typeFld, out int slot), nameof(nameFld));
            Validation.BugCheckParam(!UseBitSet(typeFld), nameof(typeFld));

            var fld = _rsti.ValueFields[slot].VerifyValue();
            Validation.Assert(fld.IsPublic);
            Validation.Assert(!fld.IsStatic);

            SetAssign(slot);
            _gen.Il
                .Ldloc(_locRec)
                .Ldflda(fld);

            return this;
        }

        /// <summary>
        /// Complete the current record instance and leave it on the stack.
        /// </summary>
        public RecordGenerator Finish()
        {
            Validation.BugCheck(_state == State.Started);

            if (!_partial)
            {
                // Verify that all fields that need to be set have been set.
                Validation.Assert(_assigned.Length == _bitsReq.Length);
                for (int i = 0; i < _assigned.Length; i++)
                {
                    uint need = (uint)_bitsReq[i] & ~((uint)_bitsPreset[i] | _assigned[i]);
                    Validation.BugCheck(need == 0);
                }
            }

            _gen.Il.Ldloc(_locRec);
            if (_genLoadRrti is not null)
            {
                _gen.Il.Dup();
                _genLoadRrti(_rrti);
                _gen.Il.Stfld(RecordBase.FinRrti);
            }

            Dispose();
            _state = State.Done;

            return this;
        }
    }
}
