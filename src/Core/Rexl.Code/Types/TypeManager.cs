// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using Conditional = System.Diagnostics.ConditionalAttribute;
using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

/// <summary>
/// A TypeManager handles mapping from DType to system type, generating and caching system types as needed, as well
/// as related functionality, such as code generation related to the system types, binary serialization, etc.
/// </summary>
public abstract partial class TypeManager
{
    /// <summary>
    /// Wraps either a <see cref="Type"/> or a <see cref="AggSysTypeInfo"/>.
    /// </summary>
    protected struct SysTypeInfo
    {
        /// <summary>
        /// The raw information in this struct. This is either a <see cref="Type"/> or a <see cref="AggSysTypeInfo"/>.
        /// </summary>
        public readonly object Raw;

        /// <summary>
        /// The system type. This won't be null unless this struct is the default value.
        /// </summary>
        public Type SysType => Raw as Type ?? AggInfo?.SysType;

        /// <summary>
        /// The <see cref="Raw"/> value as a <see cref="AggSysTypeInfo"/>, so may be null.
        /// </summary>
        public AggSysTypeInfo AggInfo => Raw as AggSysTypeInfo;

        /// <summary>
        /// The <see cref="Raw"/> value as a <see cref="RecordSysTypeInfo"/>, so may be null.
        /// </summary>
        public RecordSysTypeInfo RecordInfo => Raw as RecordSysTypeInfo;

        /// <summary>
        /// The <see cref="Raw"/> value as a <see cref="TupleSysTypeInfo"/>, so may be null.
        /// </summary>
        public TupleSysTypeInfo TupleInfo => Raw as TupleSysTypeInfo;

        /// <summary>
        /// Construct from a <see cref="Type"/>. The <paramref name="st"/> should not be a record or
        /// tuple system type (this asserts, doesn't check).
        /// </summary>
        public SysTypeInfo(Type st)
        {
            Validation.AssertValue(st);
            Validation.Assert(!st.IsSubclassOf(typeof(RecordBase)));
            Validation.Assert(!st.IsSubclassOf(typeof(TupleBase)));
            Raw = st;
        }

        /// <summary>
        /// Construct from an <see cref="AggSysTypeInfo"/>.
        /// </summary>
        public SysTypeInfo(AggSysTypeInfo ati)
        {
            Validation.AssertValue(ati);
            Raw = ati;
        }

        public static implicit operator SysTypeInfo(Type st)
        {
            return new SysTypeInfo(st);
        }

        public static implicit operator SysTypeInfo(AggSysTypeInfo ati)
        {
            return new SysTypeInfo(ati);
        }
    }

    /// <summary>
    /// The generic definition of the opt type, eg, <see cref="Nullable{T}"/>.
    /// </summary>
    private readonly Type _stOpt;

    /// <summary>
    /// The generic definition for opt system types.
    /// </summary>
    public Type OptSysType => _stOpt;

    /// <summary>
    /// This is seeded with standard types, including opt versions. This will grow with constructed types, such
    /// as record, sequence, uri, tuple, and tensor types.
    /// </summary>
    private readonly ConcurrentDictionary<DType, SysTypeInfo> _typeMap;
    private readonly ConcurrentDictionary<DType, (object value, bool special)> _defValueMap;
    private readonly ConcurrentDictionary<Type, AggSysTypeInfo> _aggTypeInfos;

    // Maps from req record type with req field types to corresponding runtime type information instance.
    private readonly ConcurrentDictionary<DType, RrtiImpl> _rrtiMap;

    /// <summary>
    /// Used to avoid manufacturing multiple equivalent types.
    /// </summary>
    private readonly object _lock;

    protected TypeManager()
    {
        // REVIEW: Eventually, don't assume Nullable<> for opt.
        _stOpt = typeof(Nullable<>);
        _typeMap = new ConcurrentDictionary<DType, SysTypeInfo>();
        _defValueMap = new ConcurrentDictionary<DType, (object value, bool special)>();
        _aggTypeInfos = new ConcurrentDictionary<Type, AggSysTypeInfo>();
        _rrtiMap = new ConcurrentDictionary<DType, RrtiImpl>();
        _lock = new object();

        void AddStd(DType type, Type st, object def)
        {
            Validation.Assert(!type.IsOpt);
            Validation.AssertValue(st);
            Validation.Assert(st.IsValueType);
            Validation.AssertValue(def);
            Validation.Assert(def.GetType() == st);

            TryAddDefValue(type, (def, false)).Verify();
            TryCacheType(type, st).Verify();
            TryCacheType(type.RootToOpt(), _stOpt.MakeGenericType(st)).Verify();
        }

        // REVIEW: There isn't a real good system type for Vac, so we just use object.
        TryCacheType(DType.Vac, typeof(object)).Verify();
        TryCacheType(DType.Null, typeof(object)).Verify();

        // Always opt types.
        TryCacheType(DType.General, typeof(object)).Verify();
        TryCacheType(DType.Text, typeof(string)).Verify();

        // Standard types with struct sys types.
        AddStd(DType.BitReq, typeof(bool), default(bool));
        AddStd(DType.R8Req, typeof(double), default(double));
        AddStd(DType.R4Req, typeof(float), default(float));
        AddStd(DType.IAReq, typeof(Integer), default(Integer));
        AddStd(DType.I8Req, typeof(long), default(long));
        AddStd(DType.I4Req, typeof(int), default(int));
        AddStd(DType.I2Req, typeof(short), default(short));
        AddStd(DType.I1Req, typeof(sbyte), default(sbyte));
        AddStd(DType.U8Req, typeof(ulong), default(ulong));
        AddStd(DType.U4Req, typeof(uint), default(uint));
        AddStd(DType.U2Req, typeof(ushort), default(ushort));
        AddStd(DType.U1Req, typeof(byte), default(byte));
        AddStd(DType.DateReq, typeof(Date), default(Date));
        AddStd(DType.TimeReq, typeof(Time), default(Time));
        AddStd(DType.GuidReq, typeof(Guid), default(Guid));

        // Root Uri type.
        TryCacheType(DType.UriGen, typeof(Link)).Verify();

        _bldrAsm = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("__TypeManager_Internal__"), AssemblyBuilderAccess.RunAndCollect);
        _bldrMod = _bldrAsm.DefineDynamicModule("MainModule");

        _slotToFieldName = new ConcurrentDictionary<int, string>();
        _fieldNameToSlot = new ConcurrentDictionary<string, int>();
        _bitGrpToFieldName = new ConcurrentDictionary<int, string>();
        _fieldNameToBitGrp = new ConcurrentDictionary<string, int>();

        _lockArityToRecDefn = new object();
        _arityToRecDefn = new ConcurrentDictionary<int, Type>();

        _lockArityToTupDefn = new object();
        _arityToTupDefn = new ConcurrentDictionary<int, Type>();

        _lockAggDefnToEqDefn = new object();
        _aggDefnToEqDefn = new ConcurrentDictionary<Type, Type>();
        _stToEqCmpCi = InitEqCmps();

        _lockArityToGetSlotDynDefn = new object();
        _arityToGetSlotDynDefn = new ConcurrentDictionary<int, MethodInfo>();

        _ser = new Serializer(this);

        _byteReaders = new Dictionary<Type, Delegate>();
        _byteWriters = new Dictionary<Type, Delegate>();
        InitializeJsonTensorSerializers();
    }

    /// <summary>
    /// BugCheck that <paramref name="st"/> corresponds to <paramref name="type"/>.
    /// </summary>
    protected void BugCheckSysType(DType type, Type st)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckValue(st, nameof(st));
        Validation.BugCheckParam(TryGetType(Reduce(type), out var sti), nameof(type));
        Validation.BugCheckParam(st == sti.SysType, nameof(st));
    }

    /// <summary>
    /// Assert that <paramref name="st"/> corresponds to <paramref name="type"/>.
    /// </summary>
    [Conditional("DEBUG")]
    protected void AssertSysType(DType type, Type st)
    {
#if DEBUG
        Validation.Assert(type.IsValid);
        Validation.AssertValue(st);
        TryGetType(type.StripFlavor(), out var sti).Verify();
        Validation.Assert(st == sti.SysType, nameof(st));
#endif
    }

    /// <summary>
    /// Create the system type for a sequence with the given item type. The default assumes
    /// <see cref="IEnumerable{T}"/> for sequence.
    /// </summary>
    public virtual Type MakeSequenceType(Type stItem)
    {
        Validation.AssertValue(stItem);
        return typeof(IEnumerable<>).MakeGenericType(stItem);
    }

    /// <summary>
    /// Given an arity and slot type, tries to get the <c>static GetSlotDynamic(homTup, index, default)</c>
    /// method to index dynamically into homogeneous instances of the corresponding tuple system type.
    /// </summary>
    public bool TryEnsureGetSlotDynamic(int arity, DType typeSlot, out MethodInfo getSlotDyn)
    {
        if (!TryEnsureSysType(typeSlot, out var stSlot))
        {
            getSlotDyn = null;
            return false;
        }

        var gsdDefn = GetGetSlotDynDefn(arity);

        Validation.Assert(gsdDefn.IsGenericMethodDefinition);
        Validation.Assert(gsdDefn.GetGenericArguments().Length == 1);

        getSlotDyn = gsdDefn.MakeGenericMethod(stSlot);
        return true;
    }

    private MethodInfo GetGetSlotDynDefn(int arity)
    {
        if (_arityToGetSlotDynDefn.TryGetValue(arity, out var gsdDefn))
            return gsdDefn;

        lock (_lockArityToGetSlotDynDefn)
        {
            if (_arityToGetSlotDynDefn.TryGetValue(arity, out gsdDefn))
                return gsdDefn;

            Validation.BugCheckParam(arity > 0, nameof(arity));
            Validation.BugCheck(_arityToTupDefn.TryGetValue(arity, out var stTup));

            gsdDefn = GenGetSlotDynDefn(stTup, arity);
            _arityToGetSlotDynDefn[arity] = gsdDefn;
            return gsdDefn;
        }
    }

    /// <summary>
    /// There are certain <see cref="DType"/>s that we "reduce" before doing a lookup in the
    /// type maps. This does the reduction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DType Reduce(DType type)
    {
        return type.RootKind != DKind.Uri ? type : type.StripFlavor();
    }

    #region _typeMap accessors // These are the only methods that should touch _typeMap.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAddType(DType type, SysTypeInfo sti)
    {
        Validation.Assert(Reduce(type) == type);
        return _typeMap.TryAdd(type, sti);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SysTypeInfo GetOrAddType(DType type, SysTypeInfo sti)
    {
        Validation.Assert(Reduce(type) == type);
        return _typeMap.GetOrAdd(type, sti);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetType(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(Reduce(type) == type);
        return _typeMap.TryGetValue(type, out sti);
    }

    #endregion _typeMap accessors

    #region _defValueMap accessors // These are the only methods that should touch _defValueMap.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAddDefValue(DType type, (object value, bool special) entry)
    {
        Validation.Assert(Reduce(type) == type);
        return _defValueMap.TryAdd(type, entry);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetDefValue(DType type, out (object value, bool special) entry)
    {
        Validation.Assert(Reduce(type) == type);
        return _defValueMap.TryGetValue(type, out entry);
    }

    #endregion _defValueMap accessors

    /// <summary>
    /// Constructors can use this to pre-populate type map. Not intended for
    /// record or tuple types.
    /// </summary>
    protected bool TryCacheType(DType type, Type st)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(!type.IsAggXxx, nameof(type));
        Validation.BugCheckParam(Reduce(type) == type, nameof(type));
        Validation.BugCheckValue(st, nameof(st));

        return TryAddType(type, st);
    }

    /// <summary>
    /// Constructors can use this to pre-populate type and default value maps. Not intended for
    /// record or tuple types.
    /// </summary>
    protected bool TryCacheTypeAndDefValue(DType type, Type st, object def, bool spec)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(!type.IsOpt, nameof(type));
        Validation.BugCheckParam(!type.IsAggXxx, nameof(type));
        Validation.BugCheckParam(Reduce(type) == type, nameof(type));
        Validation.BugCheckValue(st, nameof(st));

        return TryAddType(type, st) && TryAddDefValue(type, (def, spec));
    }

    /// <summary>
    /// Get the system type info for the given <paramref name="type"/>.
    /// </summary>
    protected bool TryGetSysTypeCore(DType type, out SysTypeInfo sti)
    {
        Validation.Assert(type.IsValid);

        type = Reduce(type);

        // First unwrap sequence until we find one or hit non-seq.
        var typeCur = type;
        int cseqCur = 0;
        SysTypeInfo stiCur;
        for (; ; )
        {
            if (TryGetType(typeCur, out stiCur))
                break;
            if (typeCur.SeqCount == 0)
            {
                lock (_lock)
                {
                    if (TryGetType(typeCur, out stiCur))
                        break;
                    if (!TryMakeSysTypeCore(typeCur, out stiCur))
                    {
                        sti = default;
                        return false;
                    }
                    TryAddType(typeCur, stiCur).Verify();
                }
                break;
            }
            typeCur = typeCur.ItemTypeOrThis;
            cseqCur++;
        }

        // Rewrap sequence.
        for (; cseqCur > 0; cseqCur--)
        {
            typeCur = typeCur.ToSequence();
            stiCur = GetOrAddType(typeCur, MakeSequenceType(stiCur.SysType));
        }
        Validation.Assert(type == typeCur);

        sti = stiCur;
        return true;
    }

    /// <summary>
    /// Get the default value for the given <paramref name="type"/>.
    /// </summary>
    protected bool TryGetDefaultValueCore(DType type, out (object value, bool special) entry)
    {
        Validation.Assert(type.IsValid);

        if (type.IsVac)
        {
            // Vac has no default value.
            entry = default;
            return false;
        }

        if (type.IsOpt)
        {
            entry = default;
            Validation.Assert(IsOfType(entry.value, type) != TriState.No);
            return true;
        }

        type = Reduce(type);

        if (TryGetDefValue(type, out entry))
            return true;

        if (!TryGetSysTypeCore(type, out var sti))
            return false;

        // Need to create and record a new default value, so grab the lock (to avoid duplicate work).
        lock (_lock)
        {
            // See if another thread got there first.
            if (TryGetDefValue(type, out entry))
                return true;
            if (!TryMakeDefaultValueCore(type, sti, out entry))
                return false;

            Validation.Assert(entry.value == null || sti.SysType.IsAssignableFrom(entry.value.GetType()));
            Validation.Assert(entry.value != null || !sti.SysType.IsValueType);
            Validation.Assert(entry.special || entry.value == null || sti.SysType.IsValueType);

            Validation.Assert(IsOfType(entry.value, type) != TriState.No);
            TryAddDefValue(type, entry).Verify();
            return true;
        }
    }

    /// <summary>
    /// If there is a system type in the cache for the given DType, returns it. Otherwise, returns null.
    /// Note: This is dangerous! Intended primarily for use by the type manager code generator.
    /// REVIEW: consider changing this to internal.
    /// </summary>
    public Type GetSysTypeOrNull(DType type)
    {
        // We allow default(DType) here.
        if (TryGetType(Reduce(type), out var sti))
            return sti.SysType;
        return null;
    }

    /// <summary>
    /// Tries to ensure/create (and cache) a system type for the given DType.
    /// </summary>
    public bool TryEnsureSysType(DType type, out Type st)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        if (TryGetSysTypeCore(type, out var sti))
        {
            st = sti.SysType;
            return true;
        }
        st = null;
        return false;
    }

    /// <summary>
    /// Tries to ensure/create (and cache) a default value for the given DType, together with
    /// a boolean indicating whether the value is distinct from C#'s concept of "default".
    /// For example, the default for a req (non-opt) record type will be a non-null object,
    /// so special will be true, since the default for such a system type is null.
    /// </summary>
    public bool TryEnsureDefaultValue(DType type, out (object value, bool special) entry)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return TryGetDefaultValueCore(type, out entry);
    }

    /// <summary>
    /// Return whether the given value is valid for the given type.
    /// </summary>
    public TriState IsOfType(object value, DType type)
    {
        if (!type.IsValid)
            return TriState.No;
        if (value == null)
            return type.IsOpt ? TriState.Yes : TriState.No;

        if (!TryGetSysTypeCore(type, out var sti))
            return TriState.No;

        if (type.IsPrimitiveXxx)
        {
            Validation.Assert(sti.AggInfo == null);
            var st = sti.SysType;
            var stReq = type.HasReq && st.IsGenericType && st.GetGenericTypeDefinition() == typeof(Nullable<>) ? st.GetGenericArguments()[0] : st;
            return value.GetType() == stReq ? TriState.Yes : TriState.No;
        }

        switch (type.Kind)
        {
        case DKind.Vac:
            // There are no instances of vac.
            return TriState.No;

        case DKind.General:
            // There are lots of system types that are not legal rexl values, so we can't say "yes".
            // REVIEW: Would be nice to improve this.
            return TriState.Maybe;

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
            // The test for primitive should have handled these.
            Validation.Assert(false);
            return TriState.Yes;

        case DKind.Sequence:
            if (!sti.SysType.IsAssignableFrom(value.GetType()))
                return TriState.No;

            // If the root kind is primitive, the answer is yes.
            if (type.RootKind.IsPrimitive())
                return TriState.Yes;

            // REVIEW: Saying yes would require testing each value in the sequence.
            // Should there be an option for this?
            return TriState.Maybe;

        case DKind.Record:
            {
                var rti = sti.RecordInfo.VerifyValue();
                if (value.GetType() != rti.SysType)
                    return TriState.No;

                if (type.FieldCount == 0)
                    return TriState.Yes;

                // Look at all fields, including testing opt bits, similar to tuple.
                // REVIEW: Should also consider storing an array of field names in the record instance,
                // so we can test for field name match.
                // REVIEW: When we can test field names, we can change this initialization to yes.
                TriState res = TriState.Maybe;

                int slot = 0;
                foreach (var tn in type.GetNames())
                {
                    Validation.Assert(type.TryGetNameType(tn.Name, out var t, out int index) && t == tn.Type && index == slot);

                    var typeOpt = tn.Type.ToOpt();
                    if (UseBitSet(typeOpt))
                    {
                        var finBit = rti.GroupFields[slot >> 3];
                        Validation.Assert(finBit.FieldType == typeof(byte));
                        var bits = (byte)finBit.GetValue(value);
                        if ((bits & (1 << (slot & 0x07))) == 0)
                        {
                            // Value is null.
                            if (!tn.Type.IsOpt)
                                return TriState.No;
                            if (!UseBitGet(typeOpt))
                            {
                                // The field is a reference type so verify that the field value is null.
                                var fin = rti.ValueFields[slot].VerifyValue();
                                Validation.Assert(fin.FieldType == GetSysTypeOrNull(tn.Type));
                                var valCur = fin.GetValue(value);
                                if (valCur != null)
                                {
                                    // REVIEW: If this happens, the record wasn't properly constructed!
                                    return TriState.No;
                                }
                            }
                            slot++;
                            continue;
                        }
                    }
                    else
                        Validation.Assert(!UseBitGet(typeOpt));

                    if (!tn.Type.RootKind.IsPrimitive())
                    {
                        if (tn.Type.IsSequence)
                            res = TriState.Maybe;
                        else
                        {
                            var fin = rti.ValueFields[slot].VerifyValue();
                            Validation.Assert(fin.FieldType == GetSysTypeOrNull(tn.Type.ToReq()));
                            var valCur = fin.GetValue(value);
                            var resCur = IsOfType(valCur, tn.Type);
                            if (resCur != TriState.Yes)
                            {
                                if (resCur == TriState.No)
                                    return TriState.No;
                                res = resCur;
                            }
                        }
                    }
                    slot++;
                }
                Validation.Assert(slot == type.FieldCount);
                Validation.Assert(res == TriState.Yes || res == TriState.Maybe);
                return res;
            }

        case DKind.Module:
            {
                Validation.Assert(typeof(RuntimeModule).IsAssignableFrom(sti.SysType));
                if (value is not RuntimeModule rm)
                    return TriState.No;
                if (rm.Bnd.Type != type.ToReq())
                    return TriState.No;
                return TriState.Yes;
            }

        case DKind.Tuple:
            {
                var tti = sti.TupleInfo.VerifyValue();
                if (value.GetType() != tti.SysType)
                    return TriState.No;

                if (type.TupleArity == 0)
                    return TriState.Yes;

                int arity = type.TupleArity;

                TriState res = TriState.Yes;
                var types = type.GetTupleSlotTypes();
                Validation.Assert(types.Length == arity);
                for (int slot = 0; slot < arity; slot++)
                {
                    if (types[slot].RootKind.IsPrimitive())
                        continue;
                    var fin = tti.ValueFields[slot].VerifyValue();
                    Validation.Assert(fin.FieldType == GetSysTypeOrNull(types[slot]));
                    var valCur = fin.GetValue(value);
                    var resCur = IsOfType(valCur, types[slot]);
                    if (resCur != TriState.Yes)
                    {
                        if (resCur == TriState.No)
                            return TriState.No;
                        res = resCur;
                    }
                }
                Validation.Assert(res == TriState.Yes || res == TriState.Maybe);
                return res;
            }

        case DKind.Tensor:
            Validation.Assert(sti.AggInfo == null);
            if (value.GetType() != sti.SysType)
                return TriState.No;

            {
                var ten = value as Tensor;
                Validation.Assert(ten != null);

                if (type.TensorRank != ten.Rank)
                    return TriState.No;

                var typeItem = type.GetTensorItemType();
                if (typeItem.RootKind.IsPrimitive())
                    return TriState.Yes;

                // REVIEW: Saying yes would require testing each value in the tensor.
                // Should there be an option for this?
                return TriState.Maybe;
            }

        case DKind.Uri:
            Validation.Assert(sti.AggInfo == null);
            Validation.Assert(sti.SysType == typeof(Link));
            if (!(value is Link))
                return TriState.No;
            return type.GetRootUriFlavor().IsRoot ? TriState.Yes : TriState.Maybe;

        default:
            // Un-implemented kind. This code needs to be augmented.
            Validation.Assert(false);
            throw new NotImplementedException();
        }
    }
}

/// <summary>
/// Type manager where sequence means IEnumerable.
/// </summary>
public abstract class EnumerableTypeManager : TypeManager
{
    protected EnumerableTypeManager()
        : base()
    {
    }
}

/// <summary>
/// Type manager where sequence means IEnumerable.
/// </summary>
public sealed partial class StdEnumerableTypeManager : EnumerableTypeManager
{
    public StdEnumerableTypeManager()
        : base()
    {
    }
}
