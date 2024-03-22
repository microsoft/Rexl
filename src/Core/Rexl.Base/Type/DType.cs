// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using Conditional = System.Diagnostics.ConditionalAttribute;
using TypeTuple = Immutable.Array<DType>;

/// <summary>
/// Represents a structural type. These are immutable. The possibilities are:
/// * The default value, default(DType), which is invalid (.IsValid return false).
/// * A primitive type, like text, r8, i8, date, time, etc.
/// * A record type, which is a set of (field-name, type) pairs.
/// * A tuple type, which has zero or more slot types.
/// * A tensor type, which has an item type and rank.
/// * A uri type, which has a flavor.
/// * A nullable primitive, record, tuple, tensor, or uri type.
/// * A sequence type, which has an item type, but no indication of number of items.
/// </summary>
public partial struct DType : IEquatable<DType>
{
    #region Static Basic Types

    public static readonly DType General = new DType(DKind.General, true, 0, null, DTypeFlags.HasGeneral);
    public static readonly DType EmptyRecordReq = new DType(DKind.Record, false, 0, RecordInfo.Empty, DTypeFlags.HasRecord);
    public static readonly DType EmptyRecordOpt = new DType(DKind.Record, true, 0, RecordInfo.Empty, DTypeFlags.HasRecord);
    public static readonly DType EmptyTableReq = new DType(DKind.Record, false, 1, RecordInfo.Empty, DTypeFlags.HasRecord);
    public static readonly DType EmptyTableOpt = new DType(DKind.Record, true, 1, RecordInfo.Empty, DTypeFlags.HasRecord);
    public static readonly DType EmptyModuleReq = new DType(DKind.Module, false, 0, ModuleInfo.Empty, DTypeFlags.HasModule);
    public static readonly DType EmptyModuleOpt = new DType(DKind.Module, true, 0, ModuleInfo.Empty, DTypeFlags.HasModule);
    public static readonly DType EmptyTupleReq = new DType(DKind.Tuple, false, 0, TupleInfo.Empty, DTypeFlags.HasTuple);
    public static readonly DType EmptyTupleOpt = new DType(DKind.Tuple, true, 0, TupleInfo.Empty, DTypeFlags.HasTuple);

    // General (content unaware) Uri type. Use CreateUriType for content aware types.
    public static readonly DType UriGen = new DType(DKind.Uri, true, 0, null, DTypeFlags.HasUri);

    public static readonly DType Vac = new DType(DKind.Vac, false, 0, null, DTypeFlags.HasVac);
    public static readonly DType Null = new DType(DKind.Vac, true, 0, null, DTypeFlags.HasVac);

    public static readonly DType Text = new DType(DKind.Text, true, 0, null, DTypeFlags.HasText);

    public static readonly DType R8Req = new DType(DKind.R8, false, 0, null, DTypeFlags.HasFloat);
    public static readonly DType R8Opt = new DType(DKind.R8, true, 0, null, DTypeFlags.HasFloat);
    public static readonly DType R4Req = new DType(DKind.R4, false, 0, null, DTypeFlags.HasFloat);
    public static readonly DType R4Opt = new DType(DKind.R4, true, 0, null, DTypeFlags.HasFloat);
    public static readonly DType IAReq = new DType(DKind.IA, false, 0, null, DTypeFlags.None);
    public static readonly DType IAOpt = new DType(DKind.IA, true, 0, null, DTypeFlags.None);
    public static readonly DType I8Req = new DType(DKind.I8, false, 0, null, DTypeFlags.None);
    public static readonly DType I8Opt = new DType(DKind.I8, true, 0, null, DTypeFlags.None);
    public static readonly DType I4Req = new DType(DKind.I4, false, 0, null, DTypeFlags.None);
    public static readonly DType I4Opt = new DType(DKind.I4, true, 0, null, DTypeFlags.None);
    public static readonly DType I2Req = new DType(DKind.I2, false, 0, null, DTypeFlags.None);
    public static readonly DType I2Opt = new DType(DKind.I2, true, 0, null, DTypeFlags.None);
    public static readonly DType I1Req = new DType(DKind.I1, false, 0, null, DTypeFlags.None);
    public static readonly DType I1Opt = new DType(DKind.I1, true, 0, null, DTypeFlags.None);
    public static readonly DType U8Req = new DType(DKind.U8, false, 0, null, DTypeFlags.None);
    public static readonly DType U8Opt = new DType(DKind.U8, true, 0, null, DTypeFlags.None);
    public static readonly DType U4Req = new DType(DKind.U4, false, 0, null, DTypeFlags.None);
    public static readonly DType U4Opt = new DType(DKind.U4, true, 0, null, DTypeFlags.None);
    public static readonly DType U2Req = new DType(DKind.U2, false, 0, null, DTypeFlags.None);
    public static readonly DType U2Opt = new DType(DKind.U2, true, 0, null, DTypeFlags.None);
    public static readonly DType U1Req = new DType(DKind.U1, false, 0, null, DTypeFlags.None);
    public static readonly DType U1Opt = new DType(DKind.U1, true, 0, null, DTypeFlags.None);
    public static readonly DType BitReq = new DType(DKind.Bit, false, 0, null, DTypeFlags.None);
    public static readonly DType BitOpt = new DType(DKind.Bit, true, 0, null, DTypeFlags.None);

    public static readonly DType DateReq = new DType(DKind.Date, false, 0, null, DTypeFlags.None);
    public static readonly DType DateOpt = new DType(DKind.Date, true, 0, null, DTypeFlags.None);
    public static readonly DType TimeReq = new DType(DKind.Time, false, 0, null, DTypeFlags.None);
    public static readonly DType TimeOpt = new DType(DKind.Time, true, 0, null, DTypeFlags.None);
    public static readonly DType GuidReq = new DType(DKind.Guid, false, 0, null, DTypeFlags.None);
    public static readonly DType GuidOpt = new DType(DKind.Guid, true, 0, null, DTypeFlags.None);

    #endregion Static Basic Types

    // The number of bits to shift right to get from _RawXxx flags to the corresponding special flags.
    private const int kshiftRawFlags = 3;

    /// <summary>
    /// There are two variants of "acceptance" which vary in how they handle records.
    /// 
    /// For a record type R let Fields(R) mean the set of field names for R.
    /// For two record types R and S, write Super(R, S) to mean the "closest" common super
    /// type of the two records. That is, Super(R, S) accepts both R and S and is the
    /// "closest" record type that does that.
    /// 
    /// The two variants of record acceptance are:
    /// * Subset acceptance (intersection super-type): In this variant, record type R accepts record type S
    ///   if Fields(R) is a subset of Fields(S) and if for every field F of R, the type of R.F accepts the
    ///   type of S.F. For this variant, Fields(Super(R, S)) is the intersection of Fields(R) and Fields(S).
    /// * Superset acceptance (union super-type): In this variant, record type R accepts record type S
    ///   if Fields(R) is a superset of Fields(S) and if for every field F of R, either:
    ///   * F is a field of S and the type of R.F accepts the type of S.F, or
    ///   * F is not a field of S and the type of R.F is opt (accepts null).
    /// 
    /// This const defines the default acceptance variant as being Superset acceptance
    /// (union super-type).
    /// 
    /// REVIEW: Can we move to a world with _only_ union/superset acceptance? Currently some of the
    /// functions want intersection/subset acceptance.
    /// </summary>
    public const bool UseUnionDefault = true;

    /// <summary>
    /// This const defines the default acceptance variant for operator application as being superset acceptance,
    /// aka union super-type.
    /// </summary>
    public const bool UseUnionOper = true;

    /// <summary>
    /// This const defines the default acceptance variant for function invoction as being superset acceptance,
    /// aka union super-type.
    /// REVIEW: There are currently a few functions that force subset/intersection. Can we change that
    /// and retire the concept of subset/intersection? Or should those functions allow both dropping fields and
    /// adding opt fields, which would be a hybrid. The dropped fields would only apply at the top level -
    /// nested levels would only support superset/union.
    /// </summary>
    public const bool UseUnionFunc = true;

    private void BugCheckValid()
    {
        Validation.BugCheck(_kind.IsValid());
        AssertValid();
    }

    [Conditional("DEBUG")]
    private void AssertValid()
    {
#if DEBUG
        Validation.Assert(_kind.IsValid());
        switch (_kind)
        {
        case DKind.Sequence:
            // Sequence is represented using _seqCount.
            Validation.Assert(false, "_kind should never be Sequence");
            break;

        case DKind.Record:
            Validation.Assert(_detail is RecordInfo);
            break;
        case DKind.Module:
            Validation.Assert(_detail is ModuleInfo);
            break;
        case DKind.Tuple:
            Validation.Assert(_detail is TupleInfo);
            break;
        case DKind.Tensor:
            Validation.Assert(_detail is TensorInfo);
            break;

        case DKind.Uri:
            // Always opt.
            Validation.Assert(_opt);
            Validation.Assert(NPath.IsBlob(_detail));
            break;

        case DKind.General:
        case DKind.Text:
            // Always opt.
            Validation.Assert(_opt);
            Validation.Assert(_detail == null);
            break;

        case DKind.Vac:
            Validation.Assert(_detail == null);
            break;

        default:
            Validation.Assert(_detail == null);
            break;
        }
#endif
    }

    [Conditional("DEBUG")]
    private void AssertValidOrDefault()
    {
#if DEBUG
        if (_kind == 0)
        {
            Validation.Assert(_seqCount == 0);
            Validation.Assert(_detail == null);
        }
        else
        {
            AssertValid();
        }
#endif
    }

    /// <summary>
    /// Returns the kind of this type. Note that it is OK to call this on default(DType).
    /// </summary>
    public DKind Kind
    {
        get
        {
            AssertValidOrDefault();
            return _seqCount > 0 ? DKind.Sequence : _kind;
        }
    }

    /// <summary>
    /// Returns the kind of the associated root type. See <see cref="RootType"/>. For a non-sequence
    /// type, this is the same as <see cref="Kind"/>. For a sequence type, it is the kind of the
    /// type when all sequence counts are stripped away.
    /// Note that it is OK to call this on default(DType).
    /// </summary>
    public DKind RootKind
    {
        get
        {
            AssertValidOrDefault();
            return _kind;
        }
    }

    /// <summary>
    /// Returns the kind of the associated core type. See <see cref="CoreType"/>. For a non-sequence
    /// non-tensor type, this is the same as <see cref="Kind"/>. For a tensor or sequence type, it is
    /// the kind of the eventual non-sequence non-tensor item type.
    /// Note that it is OK to call this on default(DType).
    /// </summary>
    public DKind CoreKind
    {
        get
        {
            AssertValidOrDefault();
            if (_kind == DKind.Tensor)
                return _GetTensorInfo().ItemType.CoreKind;
            return _kind;
        }
    }

    /// <summary>
    /// Returns whether this type is "optional", meaning that it contains null. This is OK to call
    /// on default(DType).
    /// </summary>
    public bool IsOpt
    {
        get
        {
            AssertValidOrDefault();
            return _opt || _seqCount > 0;
        }
    }

    /// <summary>
    /// Returns whether the root of this type is "optional", meaning that it contains null. This is OK to call
    /// on default(DType).
    /// </summary>
    public bool IsRootOpt
    {
        get
        {
            AssertValidOrDefault();
            return _opt;
        }
    }

    /// <summary>
    /// Returns whether this has a "required" version. This is false if either the type is
    /// not opt or if the <see cref="Kind"/> is always opt.
    /// </summary>
    public bool HasReq
    {
        get
        {
            AssertValidOrDefault();

            if (_seqCount > 0)
                return false;
            if (!_opt)
                return false;

            switch (_kind)
            {
            case DKind.General:
            case DKind.Text:
            case DKind.Uri:
                return false;
            }

            return true;
        }
    }

    public int SeqCount
    {
        get
        {
            AssertValidOrDefault();
            return _seqCount;
        }
    }

    public static DType GetNumericType(DKind kind, bool opt = false)
    {
        Validation.BugCheckParam(kind.IsNumeric(), nameof(kind));
        return new DType(kind, opt, 0, null, kind.IsRx() ? DTypeFlags.HasFloat : DTypeFlags.None);
    }

    public static DType GetPrimitiveType(DKind kind, bool opt = false)
    {
        Validation.BugCheckParam(kind.IsPrimitive(), nameof(kind));
        // String is the only primitive kind that is always opt.
        return new DType(kind, opt || kind == DKind.Text);
    }

    // These are OK to call on default(DType).
    public bool IsValid => _kind.IsValid();
    public bool IsSequence => _seqCount > 0;
    public bool IsGeneral => _kind == DKind.General && _seqCount == 0;
    public bool IsVacXxx => _kind == DKind.Vac && _seqCount == 0;
    public bool IsVac => _kind == DKind.Vac && _seqCount == 0 && !_opt;
    public bool IsNull => _kind == DKind.Vac && _seqCount == 0 && _opt;
    public bool IsRecordXxx => _kind == DKind.Record && _seqCount == 0;
    public bool IsRecordReq => _kind == DKind.Record && _seqCount == 0 && !_opt;
    public bool IsRecordOpt => _kind == DKind.Record && _seqCount == 0 && _opt;
    public bool IsTableXxx => _kind == DKind.Record && _seqCount == 1;
    public bool IsTableReq => _kind == DKind.Record && _seqCount == 1 && !_opt;
    public bool IsTableOpt => _kind == DKind.Record && _seqCount == 1 && _opt;
    public bool IsModuleXxx => _kind == DKind.Module && _seqCount == 0;
    public bool IsModuleReq => _kind == DKind.Module && _seqCount == 0 && !_opt;
    public bool IsModuleOpt => _kind == DKind.Module && _seqCount == 0 && _opt;
    public bool IsTupleXxx => _kind == DKind.Tuple && _seqCount == 0;
    public bool IsTupleReq => _kind == DKind.Tuple && _seqCount == 0 && !_opt;
    public bool IsTupleOpt => _kind == DKind.Tuple && _seqCount == 0 && _opt;
    // Note that module is NOT considered an agg type.
    public bool IsAggXxx => (_kind == DKind.Record || _kind == DKind.Tuple) && _seqCount == 0;
    public bool IsAggReq => (_kind == DKind.Record || _kind == DKind.Tuple) && _seqCount == 0 && !_opt;
    public bool IsAggOpt => (_kind == DKind.Record || _kind == DKind.Tuple) && _seqCount == 0 && _opt;
    public bool IsTensorXxx => _kind == DKind.Tensor && _seqCount == 0;
    public bool IsTensorReq => _kind == DKind.Tensor && _seqCount == 0 && !_opt;
    public bool IsTensorOpt => _kind == DKind.Tensor && _seqCount == 0 && _opt;
    public bool IsUri => _kind == DKind.Uri && _seqCount == 0;
    public bool IsPrimitiveXxx => _kind.IsPrimitive() && _seqCount == 0;
    public bool IsPrimitiveReq => _kind.IsPrimitive() && _seqCount == 0 && !_opt;
    public bool IsPrimitiveOpt => _kind.IsPrimitive() && _seqCount == 0 && _opt;
    public bool IsNumericXxx => _kind.IsNumeric() && _seqCount == 0;
    public bool IsNumericReq => _kind.IsNumeric() && _seqCount == 0 && !_opt;
    public bool IsNumericOpt => _kind.IsNumeric() && _seqCount == 0 && _opt;
    public bool IsIntegralXxx => _kind.IsIntegral() && _seqCount == 0;
    public bool IsIntegralReq => _kind.IsIntegral() && _seqCount == 0 && !_opt;
    public bool IsIntegralOpt => _kind.IsIntegral() && _seqCount == 0 && _opt;
    public bool IsFractionalXxx => _kind.IsFractional() && _seqCount == 0;
    public bool IsFractionalReq => _kind.IsFractional() && _seqCount == 0 && !_opt;
    public bool IsFractionalOpt => _kind.IsFractional() && _seqCount == 0 && _opt;

    /// <summary>
    /// Get the flags of this type.
    /// </summary>
    public DTypeFlags Flags
    {
        get
        {
            AssertValidOrDefault();
            return _flags & ~DTypeFlags._RawAll;
        }
    }

    /// <summary>
    /// Whether the type includes the general type.
    /// </summary>
    public bool HasGeneral => (Flags & DTypeFlags.HasGeneral) != 0;

    /// <summary>
    /// Whether the type includes the vac type. Note that the null type is v? so this
    /// includes the null type.
    /// </summary>
    public bool HasVac => (Flags & DTypeFlags.HasVac) != 0;

    /// <summary>
    /// Whether the type is opt or includes any opt types.
    /// </summary>
    public bool HasOpt => (Flags & DTypeFlags.HasOpt) != 0;

    /// <summary>
    /// Whether the type includes any uri types.
    /// </summary>
    public bool HasUri => (Flags & DTypeFlags.HasUri) != 0;

    /// <summary>
    /// Whether the type is or includes any record types.
    /// </summary>
    public bool HasRecord => (Flags & DTypeFlags.HasRecord) != 0;

    /// <summary>
    /// Whether the type is or includes any module types.
    /// </summary>
    public bool HasModule => (Flags & DTypeFlags.HasModule) != 0;

    /// <summary>
    /// Whether the type includes the any sequence types.
    /// </summary>
    public bool HasSequence => (Flags & DTypeFlags.HasSequence) != 0;

    /// <summary>
    /// Whether the type is or includes any tuple types.
    /// </summary>
    public bool HasTuple => (Flags & DTypeFlags.HasTuple) != 0;

    /// <summary>
    /// Whether the type is or includes any tensor types.
    /// </summary>
    public bool HasTensor => (Flags & DTypeFlags.HasTensor) != 0;

    /// <summary>
    /// Whether the type is or includes the text type.
    /// </summary>
    public bool HasText => (Flags & DTypeFlags.HasText) != 0;

    /// <summary>
    /// Whether the type is or includes a floating point type.
    /// </summary>
    public bool HasFloat => (Flags & DTypeFlags.HasFloat) != 0;

    /// <summary>
    /// Returns true if the type supports equality testing for both the operators and
    /// for functions like <c>KeyJoin</c> and <c>GroupBy</c>.
    /// </summary>
    public bool IsEquatable
    {
        get
        {
            // The equality operators lift over sequence and tensor, so those are not equatable.
            // Of course, general is also not equatable.
            const DTypeFlags k_flagsBad =
                DTypeFlags.HasVac |
                DTypeFlags.HasGeneral |
                DTypeFlags.HasTensor |
                DTypeFlags.HasSequence;

            if ((_flags & k_flagsBad) != 0)
                return false;
            Validation.Assert(_seqCount == 0);
            return true;
        }
    }

    /// <summary>
    /// Returns true if the type supports ordered comparison for both the operators and
    /// for functions like <c>Sort</c>.
    /// </summary>
    public bool IsComparable
    {
        get
        {
            const DTypeFlags k_flagsBad =
                DTypeFlags.HasVac |
                DTypeFlags.HasGeneral |
                DTypeFlags.HasRecord |
                // REVIEW: Should we make tuples with comparable slot types be comparable?
                // If so, how can we deal with different sort directions for different slots?
                DTypeFlags.HasTuple |
                DTypeFlags.HasUri |
                DTypeFlags.HasTensor |
                DTypeFlags.HasSequence;

            if ((_flags & k_flagsBad) != 0)
                return false;
            Validation.Assert(_seqCount == 0);
            return true;
        }
    }

    /// <summary>
    /// Returns whether the type supports sorting.
    /// </summary>
    public bool IsSortable => IsComparable;

    /// <summary>
    /// Whether the type includes any record types. For non-record types, this is the same
    /// as <see cref="HasRecord"/>. For record types, this indicates whether any fields
    /// of the record type are or include record types.
    /// </summary>
    public bool ContainsRecord
    {
        get
        {
            if ((_flags & DTypeFlags.HasRecord) == 0)
                return false;
            if (_kind != DKind.Record)
                return true;
            return (_GetRecordInfo().Flags & DTypeFlags.HasRecord) != 0;
        }
    }

    /// <summary>
    /// Whether the type includes any module types. For non-module types, this is the same
    /// as <see cref="HasModule"/>. For module types, this indicates whether any fields
    /// of the module type are or include module types.
    /// </summary>
    public bool ContainsModule
    {
        get
        {
            if ((_flags & DTypeFlags.HasModule) == 0)
                return false;
            if (_kind != DKind.Module)
                return true;
            return (_GetModuleInfo().Flags & DTypeFlags.HasModule) != 0;
        }
    }

    /// <summary>
    /// Whether the type includes any tuple types. For non-tuple types, this is the same
    /// as <see cref="HasTuple"/>. For tuple types, this indicates whether any slots
    /// of the tuple type are or include tuple types.
    /// </summary>
    public bool ContainsTuple
    {
        get
        {
            if ((_flags & DTypeFlags.HasTuple) == 0)
                return false;
            if (_kind != DKind.Tuple)
                return true;
            return (_GetTupleInfo().Flags & DTypeFlags.HasTuple) != 0;
        }
    }

    /// <summary>
    /// For a non-sequence type, this is the same as the type. For a sequence type, it is the same as the
    /// RootType of the item type of the sequence type. Note that it is OK to call this on default(DType).
    /// </summary>
    public DType RootType
    {
        get
        {
            AssertValidOrDefault();
            if (_seqCount > 0)
                return new DType(_kind, _opt, 0, _detail, _flags);
            return this;
        }
    }

    /// <summary>
    /// For a non-sequence, non-tensor type, this is the same as the type. For a sequence or tensor type,
    /// it is the same as the CoreType of the item type of the sequence/tensor type. Note that it is OK
    /// to call this on default(DType).
    /// </summary>
    public DType CoreType
    {
        get
        {
            AssertValidOrDefault();
            if (_kind == DKind.Tensor)
                return _GetTensorInfo().ItemType.CoreType;
            if (_seqCount > 0)
                return new DType(_kind, _opt, 0, _detail, _flags);
            return this;
        }
    }

    /// <summary>
    /// Pops off one sequence count, if possible.
    /// </summary>
    public DType ItemTypeOrThis
    {
        get
        {
            AssertValidOrDefault();
            if (_seqCount <= 0)
                return this;
            return new DType(_kind, _opt, _seqCount - 1, _detail, _flags);
        }
    }

    /// <summary>
    /// Returns the type's opt (nullable) form.
    /// </summary>
    public DType ToOpt()
    {
        BugCheckValid();
        if (_seqCount > 0 || _opt)
            return this;
        return new DType(_kind, true, 0, _detail, _flags);
    }

    /// <summary>
    /// If <see cref="HasReq"/> is true, returns the required (non-opt) form. Otherwise,
    /// returns this.
    /// </summary>
    public DType ToReq()
    {
        AssertValidOrDefault();

        if (!HasReq)
            return this;

        Validation.Assert(_seqCount == 0);
        Validation.Assert(_opt);
        return new DType(_kind, false, 0, _detail, _flags);
    }

    /// <summary>
    /// Returns the type (with the same sequence count) whose RootType is the nullable version of this.RootType.
    /// </summary>
    public DType RootToOpt()
    {
        BugCheckValid();
        if (_opt)
            return this;
        return new DType(_kind, true, _seqCount, _detail, _flags);
    }

    /// <summary>
    /// Returns the type (with the same sequence count) whose RootType is the non-nullable version of this.RootType,
    /// when such a type is legal. For example, for Null, General, and String, this returns the same type.
    /// </summary>
    public DType RootToReq()
    {
        BugCheckValid();
        if (!_opt)
            return this;

        switch (_kind)
        {
        case DKind.General:
        case DKind.Text:
        case DKind.Uri:
            return this;
        }

        return new DType(_kind, false, _seqCount, _detail, _flags);
    }

    /// <summary>
    /// Return a Sequence type with this type as its item type.
    /// </summary>
    public DType ToSequence()
    {
        BugCheckValid();
        return new DType(_kind, _opt, _seqCount + 1, _detail, _flags);
    }

    /// <summary>
    /// Return a Sequence type with this type as its item type.
    /// </summary>
    public DType ToSequence(int count)
    {
        BugCheckValid();
        Validation.BugCheckParam(count >= 0, nameof(count));

        if (count <= 0)
            return this;

        // Check against overflow.
        Validation.BugCheckParam(_seqCount + count >= _seqCount, nameof(count));
        return new DType(_kind, _opt, _seqCount + count, _detail, _flags);
    }

    /// <summary>
    /// Returns whether "this" type includes <paramref name="type"/>.
    /// A type T1 includes type T2 when at least one of the following is true:
    /// * T1 is the same as T2.
    /// * T1 is the general type, g.
    /// * Both (are opt and) have required forms and the required form of T1 includes the required form of T2.
    /// * Both are sequence types and the item type of T1 includes the item type of T2.
    /// * Both are tensor types with the same rank and the item type of T1 includes the item type of T2.
    /// * Both are record types with the same field names and each field type of T1 includes the corresponding field
    ///   type of T2.
    /// * Both are tuple types where each slot type of T1 includes the corresponding slot type of T2.
    /// </summary>
    public bool Includes(DType type)
    {
        BugCheckValid();
        type.BugCheckValid();

        return IncludesCore(type);
    }

    private bool IncludesCore(DType type)
    {
        AssertValid();
        type.AssertValid();

        if (this == type)
            return true;
        if (!HasGeneral)
            return false;

        if (_seqCount > type._seqCount)
            return false;
        if (_seqCount < type._seqCount)
            return _kind == DKind.General;
        Validation.Assert(_seqCount == type._seqCount);

        if (_kind == DKind.General)
            return true;
        if (_kind != type._kind)
            return false;
        if (_opt != type._opt)
            return false;

        switch (_kind)
        {
        case DKind.Tensor:
            {
                var info1 = _GetTensorInfo();
                var info2 = type._GetTensorInfo();
                if (info1.Rank != info2.Rank)
                    return false;
                return info1.ItemType.IncludesCore(info2.ItemType);
            }
        case DKind.Tuple:
            {
                var types1 = _GetTupleInfo().Types;
                var types2 = type._GetTupleInfo().Types;
                if (types1.Length != types2.Length)
                    return false;
                for (int i = 0; i < types1.Length; i++)
                {
                    if (!types1[i].IncludesCore(types2[i]))
                        return false;
                }
                return true;
            }
        case DKind.Record:
            {
                var info1 = _GetRecordInfo();
                var info2 = type._GetRecordInfo();
                if (info1.Count != info2.Count)
                    return false;
                using var ator1 = info1.GetPairs().GetEnumerator();
                using var ator2 = info2.GetPairs().GetEnumerator();
                while (ator1.MoveNext())
                {
                    ator2.MoveNext().Verify();
                    var tup1 = ator1.Current;
                    var tup2 = ator2.Current;
                    if (tup1.name != tup2.name)
                        return false;
                    if (!tup1.type.IncludesCore(tup2.type))
                        return false;
                }
                return true;
            }
        case DKind.Module:
            {
                var info1 = _GetModuleInfo();
                var info2 = type._GetModuleInfo();
                if (info1.Count != info2.Count)
                    return false;
                using var ator1 = info1.GetInfos().GetEnumerator();
                using var ator2 = info2.GetInfos().GetEnumerator();
                while (ator1.MoveNext())
                {
                    ator2.MoveNext().Verify();
                    var tup1 = ator1.Current;
                    var tup2 = ator2.Current;
                    if (tup1.sk != tup2.sk)
                        return false;
                    if (tup1.name != tup2.name)
                        return false;
                    if (!tup1.type.IncludesCore(tup2.type))
                        return false;
                }
                return true;
            }
        }

        // Shouldn't get here.
        Validation.Assert(false);
        return false;
    }

    /// <summary>
    /// Get the type included by this type that is "closest" to <paramref name="type"/>.
    /// </summary>
    public DType GetIncludedType(DType type)
    {
        BugCheckValid();
        type.BugCheckValid();

        var res = GetIncludedTypeCore(type);
        Validation.Assert(Includes(res));
        return res;
    }

    private DType GetIncludedTypeCore(DType type)
    {
        AssertValid();
        type.AssertValid();

        if (!HasGeneral)
            return this;
        if (IncludesCore(type))
            return type;

        // If the src seqCount is too small, bump it up.
        int dseq = _seqCount - type._seqCount;
        if (dseq > 0)
            return GetIncludedTypeCore(type.ToSequence(dseq));

        if (dseq < 0)
            return GetIncludedTypeCore(type.RootType.ToSequence(_seqCount));
        Validation.Assert(_seqCount == type._seqCount);

        if (_opt != type._opt)
        {
            if (_opt)
                return GetIncludedType(type.RootToOpt());
            var t = type.RootToReq();
            if (t != type)
                return GetIncludedType(t);
        }

        switch (_kind)
        {
        case DKind.Tensor:
            {
                var info1 = _GetTensorInfo();
                if (type._kind == DKind.Tensor)
                    type = type._GetTensorInfo().ItemType;
                else
                    type = type.RootType;
                return info1.ItemType.GetIncludedTypeCore(type).ToTensor(_opt, info1.Rank);
            }
        case DKind.Tuple:
            {
                var types1 = _GetTupleInfo().Types;
                if (type._kind != DKind.Tuple)
                    type = DType.CreateTuple(_opt, type.RootType);
                var types2 = type._GetTupleInfo().Types;

                int num = Math.Min(types1.Length, types2.Length);
                TypeTuple.Builder? bldrTypes = null;
                for (int i = 0; i < num; i++)
                {
                    var cur = types1[i];
                    var res = cur.GetIncludedTypeCore(types2[i]);
                    if (cur != res)
                    {
                        bldrTypes ??= types1.ToBuilder();
                        bldrTypes[i] = res;
                    }
                }
                if (bldrTypes == null)
                    return this;
                return new DType(DKind.Tuple, _opt, _seqCount, TupleInfo.Create(bldrTypes.ToImmutable()));
            }
        case DKind.Record:
            if (type._kind != DKind.Record)
                return this;

            {
                var info1 = _GetRecordInfo();
                var info2 = type._GetRecordInfo();

                var infoRes = info1;
                foreach (var tup in info1.GetPairs())
                {
                    if (!info2.TryGetValue(tup.name, out var t))
                        continue;
                    t = tup.type.GetIncludedTypeCore(t);
                    if (t != tup.type)
                        infoRes = infoRes.SetItem(tup.name, t);
                }
                return new DType(infoRes, _opt, _seqCount);
            }
        case DKind.Module:
            if (type._kind != DKind.Module)
                return this;

            {
                var info1 = _GetModuleInfo();
                var info2 = type._GetModuleInfo();

                var infoRes = info1;
                foreach (var tup in info1.GetInfos())
                {
                    if (!info2.TryGetValue(tup.name, out var t, out ModSymKind sk))
                        continue;
                    if (sk != tup.sk)
                        continue;
                    t = tup.type.GetIncludedTypeCore(t);
                    if (t != tup.type)
                        infoRes = infoRes.SetItem(tup.name, t, sk);
                }
                return new DType(infoRes, _opt, _seqCount);
            }
        }

        // Shouldn't get here.
        Validation.Assert(false);
        return this;
    }

    /// <summary>
    /// Returns whether "this" type accepts <paramref name="type"/>, with the <paramref name="union"/> setting.
    /// For example, when union is false, a record type can accept a record type containing additional fields,
    /// while, when union is true, a record type can accept a record type with fewer fields as long as the
    /// "extra" fields have opt type. In either case, for a field in common the field type in "this" must
    /// accept the field type in <paramref name="type"/>. See the comment on <see cref="UseUnionDefault"/>.
    /// 
    /// * We say that T1 is a super-type of T2 iff T1.Accepts(T2). With this definition, "super-type" is a
    ///   reflexive, transitive, anti-symmetric relation, that is, a partial order.
    /// * <see cref="DKind.General"/> accepts every type.
    /// * <see cref="DKind.Vac"/> is accepted by every type.
    /// * With union false, the empty record type accepts every record type. For union true, there isn't such
    ///   a universal record super-type, nor is there a universal record sub-type.
    /// </summary>
    public bool Accepts(DType type, bool union)
    {
        BugCheckValid();
        type.BugCheckValid();

        if (_seqCount != type._seqCount)
        {
            if (_seqCount > type._seqCount)
            {
                // The destination type is a deeper sequence than the source type. This only works if
                // the source root type is Vac.
                return type._kind == DKind.Vac;
            }
            // The destination type is a shallower sequence than the source type. This only works if
            // the destination root type is General.
            return _kind == DKind.General;
        }

        Validation.Assert(_seqCount == type._seqCount);

        if (!_opt && type._opt)
            return false;
        if (type._kind == DKind.Vac)
            return true;

        switch (_kind)
        {
        case DKind.General:
            // General is a super-type of any.
            Validation.Assert(_opt);
            return true;

        case DKind.Record:
            switch (type._kind)
            {
            case DKind.Record:
                return _RecordAccepts(_GetRecordInfo(), type._GetRecordInfo(), union);
            case DKind.Module:
                return _RecordAccepts(_GetRecordInfo(), type._GetModuleInfo().GetRecordInfo(), union);
            default:
                return false;
            }

        case DKind.Module:
            // A module type only accepts itself.
            switch (type._kind)
            {
            case DKind.Module:
                return _GetModuleInfo().Equals(type._GetModuleInfo());
            default:
                return false;
            }

        case DKind.Tuple:
            switch (type._kind)
            {
            case DKind.Tuple:
                return _TupleAccepts(_GetTupleInfo(), type._GetTupleInfo(), union);
            default:
                return false;
            }

        case DKind.Tensor:
            switch (type._kind)
            {
            case DKind.Tensor:
                return _TensorAccepts(_GetTensorInfo(), type._GetTensorInfo(), union);
            default:
                return false;
            }

        case DKind.Uri:
            switch (type._kind)
            {
            case DKind.Uri:
                NPath flavor = GetRootUriFlavor();
                NPath flavorOther = type.GetRootUriFlavor();
                return flavorOther.StartsWith(flavor);
            default:
                return false;
            }

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
            switch (type._kind)
            {
            case DKind.Date:
            case DKind.Time:
                // REVIEW: Should some numeric types accept Date and/or Time? If so, which? PowerApps allows it.
                return false;

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
                return CanPromoteNum(_kind, type._kind);
            }
            return false;

        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
            return type._kind == _kind;

        case DKind.Text:
            switch (type._kind)
            {
            case DKind.Text:
                Validation.Assert(_opt);
                Validation.Assert(type._opt);
                return true;
            }
            return false;

        case DKind.Vac:
            return false;

        default:
            Validation.Assert(false);
            return type._kind == _kind;
        }
    }

    /// <summary>
    /// Produces the closest common super-type of the two specified types, using the given
    /// <paramref name="union"/> setting.
    /// </summary>
    public static DType GetSuperType(DType type1, DType type2, bool union)
    {
        type1.BugCheckValid();
        type2.BugCheckValid();

        bool toGen = false;
        var res = GetSuperTypeCore(type1, type2, union, ref toGen);
        Validation.Assert(res.Accepts(type1, union));
        Validation.Assert(res.Accepts(type2, union));
        return res;
    }

    /// <summary>
    /// Produces the closest common super-type of the two specified types, using the given
    /// <paramref name="union"/> setting. If two non-general types combine to get the general type,
    /// sets <paramref name="toGen"/> to true.
    /// </summary>
    public static DType GetSuperType(DType type1, DType type2, bool union, ref bool toGen)
    {
        type1.BugCheckValid();
        type2.BugCheckValid();

        var res = GetSuperTypeCore(type1, type2, union, ref toGen);
        Validation.Assert(res.Accepts(type1, union));
        Validation.Assert(res.Accepts(type2, union));
        return res;
    }

    private static DType GetSuperTypeCore(DType type1, DType type2, bool union, ref bool toGen)
    {
        type1.AssertValid();
        type2.AssertValid();

        if (type1 == type2)
            return type1;

        // Any sequence can soak up vac.
        if (type1._kind == DKind.Vac && type1._seqCount < type2._seqCount)
            return type2;
        if (type2._kind == DKind.Vac && type2._seqCount < type1._seqCount)
            return type1;

        // General can soak up extra seq counts.
        if (type1._kind == DKind.General && type1._seqCount <= type2._seqCount)
            return type1;
        if (type2._kind == DKind.General && type2._seqCount <= type1._seqCount)
            return type2;

        // Now that Vac and General are out of the way, if the seqCounts don't match,
        // the result is General.
        if (type1._seqCount != type2._seqCount)
        {
            toGen = true;
            return _MakeGeneral(Math.Min(type1._seqCount, type2._seqCount));
        }

        if (type1._opt != type2._opt)
        {
            // One root is nullable and the other isn't, so the result must have nullable root.
            type1 = type1.RootToOpt();
            type2 = type2.RootToOpt();
        }

        // Numeric types have special rules.
        if (type1._kind.IsNumeric() && type2._kind.IsNumeric())
        {
            var kind = SuperNum(type1._kind, type2._kind);
            return new DType(kind, type1._opt, type1._seqCount, null,
                kind.IsRx() ? DTypeFlags.HasFloat : DTypeFlags.None);
        }

        if (type1._kind == DKind.Module)
            type1 = new DType(type1._GetModuleInfo().GetRecordInfo(), type1._opt, type1._seqCount);
        if (type2._kind == DKind.Module)
            type2 = new DType(type2._GetModuleInfo().GetRecordInfo(), type2._opt, type2._seqCount);

        if (type1.Accepts(type2, union))
            return type1;
        if (type2.Accepts(type1, union))
            return type2;

        if (type1._kind == DKind.Record && type2._kind == DKind.Record)
            return new DType(_GetRecordSuperType(type1._GetRecordInfo(), type2._GetRecordInfo(), union, ref toGen), type1._opt, type1._seqCount);

        if (type1._kind == DKind.Tuple && type2._kind == DKind.Tuple)
            return _GetTupleSuperType(type1._GetTupleInfo(), type2._GetTupleInfo(), type1._opt, type1._seqCount, union, ref toGen);

        if (type1._kind == DKind.Tensor && type2._kind == DKind.Tensor)
            return _GetTensorSuperType(type1._GetTensorInfo(), type2._GetTensorInfo(), type1._opt, type1._seqCount, union, ref toGen);

        if (type1._kind == DKind.Uri && type2._kind == DKind.Uri)
            return _GetUriSuperType(type1.GetRootUriFlavor(), type2.GetRootUriFlavor(), type1._seqCount);

        toGen = true;
        return _MakeGeneral(type1._seqCount);
    }

    /// <summary>
    /// Returns whether numeric kindDst accepts numeric kindSrc.
    /// </summary>
    public static bool CanPromoteNum(DKind kindDst, DKind kindSrc)
    {
        Validation.BugCheck(kindDst.IsNumeric());
        Validation.BugCheck(kindSrc.IsNumeric());

        // If the kinds are the same, true.
        if (kindDst == kindSrc)
            return true;

        // Since kinds are in the order { fractional, signed integral, unsigned integral } and within each category
        // kinds are ordered by decreasing size, and since no integral accepts fractional and no unsigned accepts signed,
        // if kindDst is larger than kindSrc, acceptance fails.
        if (kindDst > kindSrc)
            return false;

        // Now for kindDst < kindSrc.
        Validation.Assert(kindDst < kindSrc);

        // All fractional accept all smaller fractional and all integral.
        if (kindDst.IsFractional())
            return true;

        Validation.Assert(kindDst.IsIntegral());
        Validation.Assert(kindDst.IsIntegral());

        // i8 accepts u8. Otherwise, it's all based on size.
        if (kindDst == DKind.I8 && kindSrc != DKind.IA)
            return true;

        // Note: The remaining cases (integral) are all based on size, with acceptance iff size(kindDst) > size(kindSrc).
        return kindDst.NumericSize() > kindSrc.NumericSize();
    }

    /// <summary>
    /// Returns the common super-kind of two numeric kinds. See TypeSystem.md for details.
    /// </summary>
    public static DKind SuperNum(DKind kind0, DKind kind1)
    {
        Validation.BugCheck(kind0.IsNumeric());
        Validation.BugCheck(kind1.IsNumeric());

        #region Mimics CanPromoteNum

        if (kind0 == kind1)
            return kind0;

        if (kind0 > kind1)
            Util.Swap(ref kind0, ref kind1);

        Validation.Assert(kind0 < kind1);

        // All fractional accept all smaller fractional as well as all integral.
        if (kind0.IsFractional())
            return kind0;

        // Note: The remaining cases are all integral.
        Validation.Assert(kind0.IsIntegral());
        Validation.Assert(kind1.IsIntegral());

        // We have acceptance iff size(kind0) > size(kind1).
        int size0 = kind0.NumericSize();
        int size1 = kind1.NumericSize();
        if (size0 > size1)
            return kind0;

        #endregion Mimics CanPromoteNum

        // At this point, kind0 must be Ix and kind1 must be Ux.
        Validation.Assert(kind0.IsIx());
        Validation.Assert(kind1.IsUx());

        // Return the smallest signed integral type with strictly larger size, capping at I8.
        Validation.Assert(size1 >= size0);
        if (size1 >= 4)
            return DKind.I8;
        if (size1 == 2)
            return DKind.I4;
        Validation.Assert(size1 == 1);
        return DKind.I2;
    }

    /// <summary>
    /// Given two kinds, returns the resulting type for a typical arithmetic operator (Add, Mul, Div, Pow).
    /// Note that the returned type is not necessarily a super type of the input kinds, except when
    /// both are numeric or vac.
    /// </summary>
    public static DType GetNumericBinaryType(DKind kind0, DKind kind1, bool forceFrac = false)
    {
        if (forceFrac)
            return R8Req;

        DKind kindRes;
        bool isNum0 = kind0.IsNumeric();
        bool isNum1 = kind1.IsNumeric();
        if (isNum0)
            kindRes = isNum1 ? SuperNum(kind0, kind1) : kind0;
        else if (isNum1)
            kindRes = kind1;
        else
            kindRes = DKind.I8;

        return GetNumericBinaryTypeCore(kindRes);
    }

    /// <summary>
    /// Given a kind, returns the resulting type for a typical arithmetic operator (Add, Mul, Div, Pow).
    /// Note that the returned type is not necessarily a super type of the input kind, except when
    /// it is numeric or vac.
    /// </summary>
    public static DType GetNumericBinaryType(DKind kind)
    {
        return GetNumericBinaryTypeCore(kind.IsNumeric() ? kind : DKind.I8);
    }

    /// <summary>
    /// Given a numeric kind, returns the resulting type for a typical arithmetic operator (Add, Mul, Div, Pow).
    /// </summary>
    private static DType GetNumericBinaryTypeCore(DKind kindRes)
    {
        Validation.Assert(kindRes.IsNumeric());

        var flags = DTypeFlags.None;
        switch (kindRes)
        {
        case DKind.I1:
        case DKind.I2:
        case DKind.I4:
        case DKind.Bit:
        case DKind.U1:
        case DKind.U2:
        case DKind.U4:
            kindRes = DKind.I8;
            break;
        case DKind.R4:
            kindRes = DKind.R8;
            flags = DTypeFlags.HasFloat;
            break;
        case DKind.R8:
            flags = DTypeFlags.HasFloat;
            break;
        }
        Validation.Assert(kindRes.IsNumeric());
        Validation.Assert(kindRes.NumericSize() >= 8);

        return new DType(kindRes, false, 0, null, flags);
    }

    /// <summary>
    /// Given two kinds, returns the resulting type for a typical integer operator (Div, Mod, BitXxx).
    /// Note that the returned type is not necessarily a super type of the input kinds, except when
    /// both are integral or vac. The <paramref name="forceSize"/> flag indicates whether the result
    /// should be promoted to standard arithmetic sizes. This is typically false for bitwise operators
    /// and true for others.
    /// </summary>
    public static DType GetIntegerBinaryType(DKind kind0, DKind kind1, bool forceSize)
    {
        DKind kindRes;
        bool isInt0 = kind0.IsIntegral();
        bool isInt1 = kind1.IsIntegral();
        if (isInt0)
            kindRes = isInt1 ? SuperNum(kind0, kind1) : kind0;
        else if (isInt1)
            kindRes = kind1;
        else
            kindRes = DKind.I8;
        Validation.Assert(kindRes.IsIntegral());

        if (forceSize)
        {
            switch (kindRes)
            {
            case DKind.I1:
            case DKind.I2:
            case DKind.I4:
            case DKind.Bit:
            case DKind.U1:
            case DKind.U2:
            case DKind.U4:
                kindRes = DKind.I8;
                break;
            }
        }
        Validation.Assert(kindRes.IsIntegral());

        return new DType(kindRes, false, 0, null, DTypeFlags.None);
    }

    public bool Equals(DType other)
    {
        // This needs to work on default(DType).
        AssertValidOrDefault();
        other.AssertValidOrDefault();

        if (_kind != other._kind)
            return false;
        if (_seqCount != other._seqCount)
            return false;
        if (_opt != other._opt)
            return false;

        switch (_kind)
        {
        default:
            return true;

        case DKind.Record:
            return RecordInfo.Equals(_GetRecordInfo(), other._GetRecordInfo());

        case DKind.Module:
            return ModuleInfo.Equals(_GetModuleInfo(), other._GetModuleInfo());

        case DKind.Tuple:
            return _GetTupleInfo() == other._GetTupleInfo();

        case DKind.Tensor:
            return _GetTensorInfo().Equals(other._GetTensorInfo());

        case DKind.Uri:
            return GetRootUriFlavor() == other.GetRootUriFlavor();
        }
    }

    public static bool operator ==(DType type1, DType type2)
    {
        return type1.Equals(type2);
    }

    public static bool operator !=(DType type1, DType type2)
    {
        return !type1.Equals(type2);
    }

    public override int GetHashCode()
    {
        AssertValidOrDefault();

        var hc = new HashCode();
        hc.Add(_kind);
        hc.Add(_seqCount);
        hc.Add(_opt);
        switch (_kind)
        {
        case DKind.Record:
            hc.Add(_GetRecordInfo());
            break;
        case DKind.Module:
            hc.Add(_GetModuleInfo());
            break;
        case DKind.Tuple:
            hc.Add(_GetTupleInfo());
            break;
        case DKind.Tensor:
            hc.Add(_GetTensorInfo());
            break;
        case DKind.Uri:
            hc.Add(GetRootUriFlavor());
            break;
        }
        return hc.ToHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not DType other)
            return false;
        return Equals(other);
    }

    // Viewing default(DType) in the debugger should be allowed
    // so this code doesn't assert if the kind is invalid.
    public override string ToString()
    {
        return ToStringCore(compact: false);
    }
}
