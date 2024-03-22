// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

// This partial is for record type functionality.
public abstract partial class TypeManager
{
    /// <summary>
    /// Create a record builder factory for the given record type.
    /// </summary>
    public RecordBuilder.Factory CreateRecordFactory(DType typeRec)
    {
        return new RecordBuilder.Factory(this, typeRec);
    }

    /// <summary>
    /// Used to build rexl record instances. They should NOT be built directly using reflection (including dynamic) or
    /// default json deserialization, etc. To get a builder, first create a <see cref="Factory"/>. The factory is specific
    /// to a particular record type and is thread safe. The factory creates builders. Each builder can be used to
    /// construct multiple record instances, but one at a time, so a builder is NOT thread safe. To start a new
    /// instance call <see cref="Open"/>. To finish the record instance building, call <see cref="Close"/>.
    /// </summary>
    public sealed class RecordBuilder
    {
        private readonly Factory _fact;

        public Factory Owner => _fact;

        // The record type information.
        private readonly RrtiImpl _rrti;
        private readonly RecordSysTypeInfo _rsti;

        // The fields that are required (non-opt).
        private readonly Immutable.Array<byte> _bitsReq;
        // The bits that should be pre-set.
        private readonly Immutable.Array<byte> _bitsPreset;

        // The current record being built.
        private RecordBase _rec;
        // Whether the record will be only partially filled in. In this case, final field assignment
        // verification is skipped.
        private bool _partial;
        // The value of the bits fields for the current record being built.
        private byte[] _bits;
        // The fields that have been assigned on the current record.
        private byte[] _assigned;

        private RecordBuilder(Factory fact, RrtiImpl rrti,
            Immutable.Array<byte> bitsPreset, Immutable.Array<byte> bitsReq)
        {
            Validation.AssertValue(fact);
            Validation.AssertValue(rrti);
            Validation.Assert(rrti.Rsti.GroupFields.Length == (fact.Type.FieldCount + 7) / 8);
            Validation.Assert(bitsReq.Length == rrti.Rsti.GroupFields.Length);
            Validation.Assert(bitsPreset.Length == rrti.Rsti.GroupFields.Length);

            _fact = fact;
            _rrti = rrti;
            _rsti = rrti.Rsti;
            _bitsReq = bitsReq;
            _bitsPreset = bitsPreset;
            _bits = new byte[bitsPreset.Length];
            _assigned = new byte[bitsPreset.Length];
        }

        /// <summary>
        /// Start a new record instance. Setting <paramref name="partial"/> to <c>true</c> should
        /// be used very carefully. It causes the final field assignment verification to be skipped.
        /// This should only be used when it is known/guaranteed that unassigned fields will not be
        /// read by anyone, as they may be null in violation of the type system.
        /// </summary>
        public RecordBuilder Open(bool partial = false)
        {
            for (int i = 0; i < _bits.Length; i++)
            {
                _bits[i] = _bitsPreset[i];
                _assigned[i] = 0;
            }

            _partial = partial;

            // REVIEW: Use generated IL rather than Invoke?
            _rec = (RecordBase)_rsti.Ctor.Invoke(Array.Empty<object>());

            return this;
        }

        /// <summary>
        /// Complete the current record instance.
        /// </summary>
        public RecordBase Close()
        {
            Validation.BugCheck(_rec != null);

            var rec = _rec;
            _rec = null;

            if (!_partial)
            {
                // Ensure that all req fields have been filled in properly.
                for (int i = 0; i < _bitsReq.Length; i++)
                {
                    // _bitsReq[i] must be a subset of _bits[i], or there have been missing assignments.
                    Validation.BugCheck((~_bits[i] & _bitsReq[i]) == 0, "Missing assignment(s) for req field(s)");
                    _rsti.GroupFields[i].SetValue(rec, _bits[i]);
                }

                RecordBase.FinRrti.SetValue(rec, _rrti);
            }

            return rec;
        }

        /// <summary>
        /// Complete the current record instance.
        /// </summary>
        public bool TryClose(out RecordBase rec)
        {
            Validation.BugCheck(_rec != null);

            // Ensure that all req fields have been filled in properly.
            for (int i = 0; i < _bitsReq.Length; i++)
            {
                // _bitsReq[i] must be a subset of _bits[i], or there have been missing assignments.
                if ((~_bits[i] & _bitsReq[i]) != 0)
                {
                    rec = null;
                    return false;
                }
                _rsti.GroupFields[i].SetValue(_rec, _bits[i]);
            }

            rec = _rec;
            _rec = null;

            RecordBase.FinRrti.SetValue(rec, _rrti);
            return true;
        }

        private void SetAssign(int slot)
        {
            Validation.AssertIndex(slot, _fact.Type.FieldCount);
            int grp = slot >> 3;
            byte mask = (byte)(1 << (slot & 0x07));
            Validation.BugCheck((_assigned[grp] & mask) == 0, "Can't set field more than once");
            _assigned[grp] |= mask;
        }

        private void SetBit(int slot)
        {
            Validation.AssertIndex(slot, _fact.Type.FieldCount);
            int grp = slot >> 3;
            byte mask = (byte)(1 << (slot & 0x07));
            Validation.Assert((_bits[grp] & mask) == 0);
            _bits[grp] |= mask;
        }

        /// <summary>
        /// This is a factory to create record builder instances. See the comment on <see cref="RecordBuilder"/>.
        /// </summary>
        public sealed class Factory
        {
            private readonly TypeManager _tm;
            private readonly RrtiImpl _rrti;
            private readonly RecordSysTypeInfo _rsti;
            private readonly Immutable.Array<byte> _bitsReq;
            private readonly Immutable.Array<byte> _bitsPreset;

            /// <summary>
            /// The record type that this builder is responsible for. Whether this type is opt or req is of no
            /// consequence, but it will always be a record type.
            /// </summary>
            public DType Type { get; }

            /// <summary>
            /// The system type corresponding to the <see cref="Type"/>.
            /// </summary>
            public Type SysType => _rsti.SysType;

            /// <summary>
            /// Constructs a new instance of the builder.
            /// </summary>
            public Factory(TypeManager tm, DType typeRec)
            {
                Validation.BugCheckValue(tm, nameof(tm));
                Validation.BugCheckParam(typeRec.IsRecordXxx, nameof(typeRec));
                Validation.BugCheckParam(tm.TryGetSysTypeCore(typeRec, out var sti), nameof(typeRec));

                _tm = tm;
                _rsti = sti.RecordInfo.VerifyValue();
                Type = typeRec.ToReq();
                _rrti = _tm.EnsureRrti(Type, _rsti);

                int cgrp = (typeRec.FieldCount + 7) >> 3;
                Validation.Assert(cgrp == _rrti.Rsti.GroupFields.Length);

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
                Validation.Assert(slot == typeRec.FieldCount);
                _bitsReq = bldrReq.ToImmutable();
                _bitsPreset = bldrPre.ToImmutable();
            }

            /// <summary>
            /// Creates a new <see cref="RecordBuilder"/> corresponding to this factory.
            /// </summary>
            public RecordBuilder Create()
            {
                return new RecordBuilder(this, _rrti, _bitsPreset, _bitsReq);
            }

            /// <summary>
            /// Gets a strongly-typed "setter" for the indicated field. The setter is strongly typed
            /// in the field system type, but not in the record system type.
            /// </summary>
            public Action<RecordBuilder, T> GetFieldSetter<T>(DName nameFld)
            {
                Validation.BugCheckParam(Type.TryGetNameType(nameFld, out var typeFld, out int slot), nameof(nameFld));
                Validation.AssertIndex(slot, Type.FieldCount);

                // REVIEW: Move to using il code gen rather than FieldInfo. This transition shouldn't
                // disrupt client code at all.
                var fin = _rsti.ValueFields[slot].VerifyValue();
                Validation.Assert(fin.FieldType == SysType.GetGenericArguments()[slot]);

                _tm.TryEnsureSysType(typeFld, out var stFld).Verify();
                Validation.BugCheckParam(stFld.IsAssignableFrom(typeof(T)), nameof(T));

                if (!UseBitSet(typeFld))
                {
                    Validation.Assert(fin.FieldType == stFld);

                    // REVIEW: These should be cached on the factory.
                    return (bldr, val) =>
                    {
                        Validation.BugCheckValue(bldr, nameof(bldr));
                        Validation.BugCheckParam(bldr._fact == this, nameof(bldr));
                        Validation.BugCheckParam(bldr._rec != null, nameof(bldr));
                        bldr.SetAssign(slot);
                        fin.SetValue(bldr._rec, val);
                    };
                }
                else
                {
                    Validation.Assert(
                        _tm.IsRefTypeCore(stFld) && fin.FieldType.IsAssignableFrom(typeof(T)) ||
                        stFld.IsGenericType && stFld.GetGenericTypeDefinition() == typeof(Nullable<>) && fin.FieldType == stFld.GenericTypeArguments[0]);

                    // REVIEW: These should be cached on the factory.
                    return (bldr, val) =>
                    {
                        Validation.BugCheckValue(bldr, nameof(bldr));
                        Validation.BugCheckParam(bldr._fact == this, nameof(bldr));
                        Validation.BugCheckParam(bldr._rec != null, nameof(bldr));
                        bldr.SetAssign(slot);
                        if (val != null)
                        {
                            bldr.SetBit(slot);
                            fin.SetValue(bldr._rec, val);
                        }
                    };
                }
            }

            /// <summary>
            /// Gets a weakly typed "setter" for the indicated field.
            /// </summary>
            public Action<RecordBuilder, object?> GetFieldSetter(DName nameFld, out DType typeFld, out Type stFld)
            {
                Validation.BugCheckParam(Type.TryGetNameType(nameFld, out typeFld, out int slot), nameof(nameFld));
                Validation.AssertIndex(slot, Type.FieldCount);

                // REVIEW: Move to using il code gen rather than FieldInfo. This transition shouldn't
                // disrupt client code at all.
                var fin = _rsti.ValueFields[slot].VerifyValue();
                Validation.Assert(fin.FieldType == SysType.GetGenericArguments()[slot]);

                _tm.TryEnsureSysType(typeFld, out stFld).Verify();

                if (!UseBitSet(typeFld))
                {
                    Validation.Assert(fin.FieldType == stFld);

                    // REVIEW: These should be cached on the factory.
                    return (bldr, val) =>
                    {
                        Validation.BugCheckValue(bldr, nameof(bldr));
                        Validation.BugCheckParam(bldr._fact == this, nameof(bldr));
                        Validation.BugCheckParam(bldr._rec != null, nameof(bldr));
                        bldr.SetAssign(slot);
                        fin.SetValue(bldr._rec, val);
                    };
                }
                else
                {
                    Validation.Assert(
                        _tm.IsRefTypeCore(stFld) ||
                        stFld.IsGenericType && stFld.GetGenericTypeDefinition() == typeof(Nullable<>) && fin.FieldType == stFld.GenericTypeArguments[0]);

                    // REVIEW: These should be cached on the factory.
                    return (bldr, val) =>
                    {
                        Validation.BugCheckValue(bldr, nameof(bldr));
                        Validation.BugCheckParam(bldr._fact == this, nameof(bldr));
                        Validation.BugCheckParam(bldr._rec != null, nameof(bldr));
                        bldr.SetAssign(slot);
                        if (val != null)
                        {
                            bldr.SetBit(slot);
                            fin.SetValue(bldr._rec, val);
                        }
                    };
                }
            }
        }
    }

    /// <summary>
    /// Gets a strongly-typed field value "getter" for the given field of the given record type.
    /// The getter is strongly typed in the field type, but not in the record type.
    /// </summary>
    public Func<RecordBase, T> GetFieldGetter<T>(DType typeRec, DName nameFld)
    {
        return GetFieldGetter<T>(typeRec, nameFld, out _, out _);
    }

    /// <summary>
    /// Gets a strongly-typed field value "getter" for the given field of the given record type.
    /// The getter is strongly typed in the field type, but not in the record type.
    /// </summary>
    public Func<RecordBase, T> GetFieldGetter<T>(DType typeRec, DName nameFld, out DType typeFld, out Type stFld)
    {
        Validation.BugCheckParam(typeRec.IsRecordXxx, nameof(typeRec));
        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti), nameof(typeRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out typeFld, out int slot), nameof(nameFld));
        Validation.AssertIndex(slot, typeRec.FieldCount);

        // REVIEW: Move to using il code gen rather than FieldInfo. This transition shouldn't
        // disrupt client code at all.
        var rti = sti.RecordInfo.VerifyValue();
        var fin = rti.ValueFields[slot].VerifyValue();

        TryEnsureSysType(typeFld, out stFld).Verify();
        Validation.BugCheckParam(typeof(T).IsAssignableFrom(stFld), nameof(T));

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            // REVIEW: Consider caching these somewhere.
            byte mask = (byte)(1 << (slot & 0x07));
            return rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                var bits = (byte)finBit.GetValue(rec);
                if ((bits & mask) == 0)
                    return default(T);
                return (T)fin.GetValue(rec);
            };
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);

            // REVIEW: Consider caching these somewhere.
            return rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                return (T)fin.GetValue(rec);
            };
        }
    }

    /// <summary>
    /// Gets a weakly-typed field value "getter" for the given field of the given record type.
    /// </summary>
    public Func<RecordBase, object> GetFieldGetter(DType typeRec, DName nameFld, out DType typeFld, out Type stFld)
    {
        Validation.BugCheckParam(typeRec.IsRecordXxx, nameof(typeRec));
        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti), nameof(typeRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out typeFld, out int slot), nameof(nameFld));
        Validation.AssertIndex(slot, typeRec.FieldCount);

        // REVIEW: Move to using il code gen rather than FieldInfo. This transition shouldn't
        // disrupt client code at all.
        var rti = sti.RecordInfo.VerifyValue();
        var fin = rti.ValueFields[slot].VerifyValue();

        TryEnsureSysType(typeFld, out stFld).Verify();

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            // REVIEW: Consider caching these somewhere.
            byte mask = (byte)(1 << (slot & 0x07));
            return rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                var bits = (byte)finBit.GetValue(rec);
                if ((bits & mask) == 0)
                    return null;
                return fin.GetValue(rec);
            };
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);

            // REVIEW: Consider caching these somewhere.
            return rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                return fin.GetValue(rec);
            };
        }
    }

    /// <summary>
    /// Tries to get a strongly-typed field value "getter" for the given field of the given record type.
    /// The getter is strongly typed in the field type, but not in the record type.
    /// This returns false if the type is not a record type, there is no such field, mapping to system
    /// types fails, type type parameter is invalid, etc.
    /// </summary>
    public bool TryGetFieldGetter<T>(DType typeRec, DName nameFld, out Func<RecordBase, T> getter)
    {
        getter = default;

        if (!typeRec.IsRecordXxx)
            return false;
        if (!TryGetSysTypeCore(typeRec, out var sti))
            return false;
        if (!typeRec.TryGetNameType(nameFld, out var typeFld, out int slot))
            return false;
        Validation.AssertIndex(slot, typeRec.FieldCount);

        if (!TryEnsureSysType(typeFld, out var stFld))
            return false;
        if (!typeof(T).IsAssignableFrom(stFld))
            return false;

        // REVIEW: Move to using il code gen rather than FieldInfo. This transition shouldn't
        // disrupt client code at all.
        var rti = sti.RecordInfo.VerifyValue();
        var fin = rti.ValueFields[slot].VerifyValue();

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            // REVIEW: Consider caching these somewhere.
            byte mask = (byte)(1 << (slot & 0x07));
            getter = rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                var bits = (byte)finBit.GetValue(rec);
                if ((bits & mask) == 0)
                    return default(T);
                return (T)fin.GetValue(rec);
            };
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);

            // REVIEW: Consider caching these somewhere.
            getter = rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                return (T)fin.GetValue(rec);
            };
        }

        return true;
    }

    /// <summary>
    /// Tries to get a weakly-typed field value "getter" for the given field of the given record type.
    /// This returns false if the type is not a record type, there is no such field, mapping to system
    /// types fails, type type parameter is invalid, etc.
    /// </summary>
    public bool TryGetFieldGetter(DType typeRec, DName nameFld, out DType typeFld, out Type stFld, out Func<RecordBase, object> getter)
    {
        typeFld = default;
        stFld = default;
        getter = default;

        if (!typeRec.IsRecordXxx)
            return false;
        if (!TryGetSysTypeCore(typeRec, out var sti))
            return false;
        if (!typeRec.TryGetNameType(nameFld, out typeFld, out int slot))
            return false;
        Validation.AssertIndex(slot, typeRec.FieldCount);

        // REVIEW: Move to using il code gen rather than FieldInfo. This transition shouldn't
        // disrupt client code at all.
        var rti = sti.RecordInfo.VerifyValue();
        var fin = rti.ValueFields[slot].VerifyValue();

        TryEnsureSysType(typeFld, out stFld).Verify();

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            // REVIEW: Consider caching these somewhere.
            byte mask = (byte)(1 << (slot & 0x07));
            getter = rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                var bits = (byte)finBit.GetValue(rec);
                if ((bits & mask) == 0)
                    return null;
                return fin.GetValue(rec);
            };
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);

            // REVIEW: Consider caching these somewhere.
            getter = rec =>
            {
                // REVIEW: Asserts should be fine here (rather than Check) since if these fail the
                // fin.GetValue call should throw. Verify that it does.
                Validation.AssertValue(rec);
                Validation.Assert(rti.SysType.IsAssignableFrom(rec.GetType()));
                return fin.GetValue(rec);
            };
        }

        return true;
    }

    /// <summary>
    /// Tries to get the field value for the indicated field in the given record object of the given
    /// record type. Returns false if the type isn't a record type, or doesn't have a field with the
    /// given name. Throws if the record is invalid (doesn't match the given record type).
    /// </summary>
    public bool TryGetFieldValue(DType typeRec, DName nameFld, RecordBase rec, out Type stFld, out object value)
    {
        return TryGetFieldValueCore(typeRec, nameFld, rec, out _, out stFld, out value);
    }

    /// <summary>
    /// Tries to get the field value for the indicated field in the given record object of the given
    /// record type. Returns false if the type isn't a record type, or doesn't have a field with the
    /// given name. Throws if the record is invalid (doesn't match the given record type).
    /// </summary>
    public bool TryGetFieldValue(DType typeRec, DName nameFld, RecordBase rec, out DType typeFld, out Type stFld, out object value)
    {
        return TryGetFieldValueCore(typeRec, nameFld, rec, out typeFld, out stFld, out value);
    }

    protected bool TryGetFieldValueCore(DType typeRec, DName nameFld, RecordBase rec, out DType typeFld, out Type stFld, out object value)
    {
        Validation.BugCheckValue(rec, nameof(rec));

        if (!typeRec.IsRecordXxx || !typeRec.TryGetNameType(nameFld, out typeFld, out int slot))
        {
            value = null;
            typeFld = default;
            stFld = null;
            return false;
        }

        Validation.AssertIndex(slot, typeRec.FieldCount);
        if (!TryGetSysTypeCore(typeRec, out var sti))
        {
            value = null;
            typeFld = default;
            stFld = null;
            return false;
        }
        var rti = sti.RecordInfo.VerifyValue();
        Validation.BugCheckParam(rti.SysType.IsAssignableFrom(rec.GetType()), nameof(rec));

        var fin = rti.ValueFields[slot].VerifyValue();

        TryEnsureSysType(typeFld, out stFld).Verify();

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            // REVIEW: Consider caching these somewhere.
            byte mask = (byte)(1 << (slot & 0x07));
            var bits = (byte)finBit.GetValue(rec);
            if ((bits & mask) == 0)
                value = null;
            else
                value = fin.GetValue(rec);
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);
            value = fin.GetValue(rec);
        }

        return true;
    }

    /// <summary>
    /// Gets the field value for the indicated field in the given record object of the given
    /// record type. Throws on failure.
    /// </summary>
    public T GetFieldValue<T>(DType typeRec, DName nameFld, RecordBase rec)
    {
        Validation.BugCheckValue(rec, nameof(rec));
        Validation.BugCheckParam(typeRec.IsRecordXxx, nameof(typeRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out var typeFld, out int slot), nameof(nameFld));
        Validation.AssertIndex(slot, typeRec.FieldCount);

        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti), nameof(typeRec));
        var rti = sti.RecordInfo.VerifyValue();

        Validation.BugCheckParam(rti.SysType.IsAssignableFrom(rec.GetType()), nameof(rec));

        var fin = rti.ValueFields[slot].VerifyValue();

        TryEnsureSysType(typeFld, out var stFld).Verify();
        Validation.BugCheckParam(typeof(T).IsAssignableFrom(stFld), nameof(T));

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            byte mask = (byte)(1 << (slot & 0x07));
            var bits = (byte)finBit.GetValue(rec);
            if ((bits & mask) == 0)
                return default(T);
            return (T)fin.GetValue(rec);
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);
            return (T)fin.GetValue(rec);
        }
    }

    /// <summary>
    /// Gets the field value for the indicated field in the given record object of the given
    /// record type. Throws on failure.
    /// </summary>
    public object GetFieldValue(DType typeRec, DName nameFld, RecordBase rec, out DType typeFld, out Type stFld)
    {
        Validation.BugCheckValue(rec, nameof(rec));
        Validation.BugCheckParam(typeRec.IsRecordXxx, nameof(typeRec));
        Validation.BugCheckParam(typeRec.TryGetNameType(nameFld, out typeFld, out int slot), nameof(nameFld));
        Validation.AssertIndex(slot, typeRec.FieldCount);

        Validation.BugCheckParam(TryGetSysTypeCore(typeRec, out var sti), nameof(typeRec));
        var rti = sti.RecordInfo.VerifyValue();

        Validation.BugCheckParam(rti.SysType.IsAssignableFrom(rec.GetType()), nameof(rec));

        var fin = rti.ValueFields[slot].VerifyValue();

        TryEnsureSysType(typeFld, out stFld).Verify();

        if (UseBitGet(typeFld))
        {
            Validation.Assert(IsNullableTypeCore(stFld));
            Validation.Assert(fin.FieldType == GetSysTypeOrNull(typeFld.ToReq()));
            var finBit = rti.GroupFields[slot >> 3];
            Validation.Assert(finBit.FieldType == typeof(byte));

            byte mask = (byte)(1 << (slot & 0x07));
            var bits = (byte)finBit.GetValue(rec);
            if ((bits & mask) == 0)
                return null;
            return fin.GetValue(rec);
        }
        else
        {
            Validation.Assert(fin.FieldType == stFld);
            return fin.GetValue(rec);
        }
    }
}
