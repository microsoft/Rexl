// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using TypeTuple = Immutable.Array<DType>;

partial struct DType
{
    /// <summary>
    /// This is the _detail field of a Tuple type. Contains an array of DType
    /// as well as cached values for flags and tuple homogeneity.
    /// </summary>
    private sealed class TupleInfo : IEquatable<TupleInfo>
    {
        public static readonly TupleInfo Empty = new TupleInfo(TypeTuple.Empty);

        // Computed lazily and cached.
        private int _hash;

        public TypeTuple Types { get; }

        public DTypeFlags Flags { get; }

        public bool IsHomogeneous { get; }

        private TupleInfo(TypeTuple types)
        {
            Types = types;
            if (Types.Length == 0)
                return;

            IsHomogeneous = true;
            for (int i = 0; i < Types.Length; i++)
            {
                var type = Types[i];
                type.AssertValid();
                Flags |= type.Flags;
                if (IsHomogeneous && type != Types[0])
                    IsHomogeneous = false;
            }
        }

        public static TupleInfo Create(TypeTuple types)
        {
            if (types.Length == 0)
                return Empty;
            return new TupleInfo(types);
        }

        public int Count => Types.Length;

        public DType GetSlotType(int slot)
        {
            Validation.AssertIndex(slot, Count);
            return Types[slot];
        }

        public static bool operator ==(TupleInfo ti1, TupleInfo ti2)
        {
            Validation.AssertValue(ti1);
            Validation.AssertValue(ti2);

            if (ti1.Types.AreIdentical(ti2.Types))
                return true;
            if (ti1.Count != ti2.Count)
                return false;
            for (int i = 0; i < ti1.Types.Length; i++)
            {
                if (ti1.Types[i] != ti2.Types[i])
                    return false;
            }
            return true;
        }

        public static bool operator !=(TupleInfo ti1, TupleInfo ti2)
        {
            return !(ti1 == ti2);
        }

        public bool Equals(TupleInfo? other)
        {
            return other is not null && this == other;
        }

        public override bool Equals(object? obj)
        {
            if (obj is TupleInfo ti)
                return this == ti;
            return false;
        }

        public override int GetHashCode()
        {
            int hash = _hash;
            if (hash != 0)
                return hash;

            var hc = new HashCode();
            hc.Add(0x82F307BE);
            for (int i = 0; i < Types.Length; i++)
                hc.Add(Types[i]);
            hash = hc.ToHashCode();
            if (hash == 0)
                hash = 1;
            return _hash = hash;
        }
    }

    /// <summary>
    /// When RootType is a Tuple type, returns the number of slots/items of the Tuple. Otherwise, returns -1.
    /// </summary>
    public int TupleArity
    {
        get
        {
            AssertValidOrDefault();
            return _kind == DKind.Tuple ? _GetTupleInfo().Count : -1;
        }
    }

    private static TupleInfo _GetTupleInfo(object? detail)
    {
        Validation.Assert(detail is TupleInfo);
        return (TupleInfo)detail;
    }

    private TupleInfo _GetTupleInfo()
    {
        Validation.Assert(_kind == DKind.Tuple);
        Validation.Assert(_detail is TupleInfo);
        return (TupleInfo)_detail;
    }

    public static DType CreateTuple(bool opt, TypeTuple types)
    {
        Validation.BugCheckParam(!types.IsDefault, nameof(types));

        if (types.Length == 0)
            return new DType(DKind.Tuple, opt, 0, TupleInfo.Empty, DTypeFlags.HasTuple);

        for (int slot = 0; slot < types.Length; slot++)
            Validation.BugCheckParam(types[slot].IsValid, nameof(types));
        return new DType(DKind.Tuple, opt, 0, TupleInfo.Create(types));
    }

    public static DType CreateTuple(bool opt, params DType[] types)
    {
        Validation.BugCheckValue(types, nameof(types));

        int count = types.Length;
        if (count == 0)
            return new DType(DKind.Tuple, opt, 0, TupleInfo.Empty, DTypeFlags.HasTuple);

        var bldrTypes = TypeTuple.CreateBuilder(count, init: true);
        for (int slot = 0; slot < count; slot++)
        {
            var type = types[slot];
            Validation.BugCheckParam(type.IsValid, nameof(types));
            bldrTypes[slot] = type;
        }
        return new DType(DKind.Tuple, opt, 0, TupleInfo.Create(bldrTypes.ToImmutable()));
    }

    public static DType CreateTuple(bool opt, List<DType> types)
    {
        Validation.BugCheckValue(types, nameof(types));

        int count = types.Count;
        if (count == 0)
            return new DType(DKind.Tuple, opt, 0, TupleInfo.Empty, DTypeFlags.HasTuple);

        var bldrTypes = TypeTuple.CreateBuilder(count, init: true);
        for (int slot = 0; slot < count; slot++)
        {
            var type = types[slot];
            Validation.BugCheckParam(type.IsValid, nameof(types));
            bldrTypes[slot] = type;
        }
        return new DType(DKind.Tuple, opt, 0, TupleInfo.Create(bldrTypes.ToImmutable()));
    }

    public static DType CreateTuple(bool opt, IEnumerable<DType> types)
    {
        Validation.BugCheckValue(types, nameof(types));

        var bldrTypes = TypeTuple.CreateBuilder();
        foreach (var type in types)
        {
            Validation.BugCheckParam(type.IsValid, nameof(types));
            bldrTypes.Add(type);
        }
        return new DType(DKind.Tuple, opt, 0, TupleInfo.Create(bldrTypes.ToImmutable()));
    }

    /// <summary>
    /// Checks that the root type is a tuple type and returns the slot types.
    /// </summary>
    public TypeTuple GetTupleSlotTypes()
    {
        Validation.BugCheck(_kind == DKind.Tuple);
        return _GetTupleInfo().Types;
    }

    /// <summary>
    /// Checks that the root type is a homogeneous tuple and returns the slot type.
    /// </summary>
    public DType GetHomTupleSlotType()
    {
        Validation.BugCheck(Kind == DKind.Tuple);
        var ti = _GetTupleInfo();
        Validation.BugCheck(ti.IsHomogeneous);
        return ti.GetSlotType(0);
    }

    /// <summary>
    /// Checks whether the root type is a homogeneous tuple. A homogeneous tuple
    /// is a tuple that has at least one element, with all elements of the same type.
    /// </summary>
    public bool IsHomTuple()
    {
        return Kind == DKind.Tuple && _GetTupleInfo().IsHomogeneous;
    }

    /// <summary>
    /// Checks whether the root type is a homogeneous tuple. A homogeneous tuple
    /// is a tuple that has at least one element, with all elements of the same type.
    /// </summary>
    public bool IsHomTuple(out DType typeItem)
    {
        TupleInfo ti;
        if (Kind != DKind.Tuple || !(ti = _GetTupleInfo()).IsHomogeneous)
        {
            typeItem = default;
            return false;
        }
        typeItem = ti.GetSlotType(0);
        return true;
    }

    /// <summary>
    /// Return true if this and <paramref name="type"/> are both required tuple types
    /// and the slots of this are a prefix of the slots of <paramref name="type"/>.
    /// </summary>
    public bool IsPrefixTuple(DType type)
    {
        if (!IsTupleReq)
            return false;
        if (!type.IsTupleReq)
            return false;

        var infoSml = _GetTupleInfo();
        var infoBig = type._GetTupleInfo();
        if (infoSml == infoBig)
            return true;

        int arity = infoSml.Count;
        if (arity == 0)
            return true;
        if (arity > infoBig.Count)
            return false;

        for (int i = 0; i < arity; i++)
        {
            if (infoSml.Types[i] != infoBig.Types[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Implements acceptance for tuple types.
    /// </summary>
    private static bool _TupleAccepts(TupleInfo infoDst, TupleInfo infoSrc, bool union)
    {
        if (infoSrc == infoDst)
            return true;

        if (infoSrc.Count != infoDst.Count)
        {
            // REVIEW: Should we support "extending" to opt slot types?
            return false;
        }

        for (int i = 0; i < infoDst.Count; i++)
        {
            if (!infoDst.GetSlotType(i).Accepts(infoSrc.GetSlotType(i), union))
                return false;
        }

        return true;
    }

    private static DType _GetTupleSuperType(TupleInfo info1, TupleInfo info2, bool opt, int seqCount, bool union, ref bool toGen)
    {
        int arity = info1.Count;
        if (arity != info2.Count)
        {
            toGen = true;
            return _MakeGeneral(seqCount);
        }

        // This is only called when neither accepts the other, so zero-arity won't happen.
        Validation.Assert(arity > 0);

        var bldrTypes = TypeTuple.CreateBuilder(arity, init: true);
        for (int slot = 0; slot < arity; slot++)
        {
            var type1 = info1.GetSlotType(slot);
            var type2 = info2.GetSlotType(slot);
            if (type1 == type2)
                bldrTypes[slot] = type1;
            else
                bldrTypes[slot] = GetSuperTypeCore(type1, type2, union, ref toGen);
        }
        return new DType(DKind.Tuple, opt, seqCount, TupleInfo.Create(bldrTypes.ToImmutable()));
    }
}
