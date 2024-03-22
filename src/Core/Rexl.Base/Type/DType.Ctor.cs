// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

// This partial contains all fields and constructors. Note that they are all private.
partial struct DType
{
    #region Instance Fields

    // NOTE: The total (layed-out) size of the instance fields should be kept minimal. Complex
    // information should go into the _detail object, whose type depends on the _kind field.
    // Currently, the instance fields occupy 16 bytes. Let's keep it at this.

    // For a sequence, the _kind is the (root) item kind, with _seqCount being the sequence nesting count.
    private readonly DKind _kind;
    private readonly bool _opt;
    private readonly DTypeFlags _flags;
    private readonly int _seqCount;

    // For compound types, like Record, Module, Tuple, and Tensor, there is additional information referenced
    // by the _detail field. For each such compound type there is a class named XxxInfo.
    private readonly object? _detail;

    #endregion Instance Fields

    private DType(DKind kind, bool opt, int seqCount, object? detail, DTypeFlags flags)
    {
        Validation.Assert(kind.IsValid());
        Validation.Assert(kind != DKind.Sequence);
        Validation.Assert(seqCount >= 0);

        // Get the initial special flags from the raw bits.
        Validation.Assert(DTypeFlags._SpecAll == RawToSpec(DTypeFlags._RawAll));
        flags = flags & ~DTypeFlags._SpecAll | RawToSpec(flags);

        if (seqCount > 0)
            flags |= DTypeFlags.HasSequence | DTypeFlags.HasOpt;
        if (opt)
        {
            flags |= DTypeFlags.HasOpt;
            if (kind != DKind.General && kind != DKind.Text && kind != DKind.Uri)
                flags |= DTypeFlags.HasRemovableOpt;
        }

        // The raw bits should be a subset of the spec bits.
        Validation.Assert((flags & DTypeFlags._RawAll & ~SpecToRaw(flags)) == 0);

        _kind = kind;
        _opt = opt;
        _seqCount = seqCount;
        _detail = detail;
        _flags = flags;
#if DEBUG
        AssertValid();

        var flagsSlow = ComputeFlagsSlow();
        Validation.Assert(_flags == flagsSlow);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DType(DKind kind, bool opt = false, int seqCount = 0, object? detail = null)
        : this(kind, opt, seqCount, detail, ComputeFlags(kind, detail))
    {
    }

    private DType(RecordInfo rec, bool opt = false, int seqCount = 0)
        : this(DKind.Record, opt, seqCount, rec, ComputeFlags(DKind.Record, rec))
    {
    }

    private DType(ModuleInfo mod, bool opt = false, int seqCount = 0)
        : this(DKind.Module, opt, seqCount, mod, ComputeFlags(DKind.Module, mod))
    {
    }

    private static DType _MakeGeneral(int seqCount)
    {
        return new DType(DKind.General, true, seqCount, null, DTypeFlags.HasGeneral);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DTypeFlags WithSpecToRaw(DTypeFlags flags)
    {
        return flags | (DTypeFlags)((ushort)(flags & DTypeFlags._SpecAll) << kshiftRawFlags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DTypeFlags SpecToRaw(DTypeFlags flags)
    {
        return (DTypeFlags)((ushort)(flags & DTypeFlags._SpecAll) << kshiftRawFlags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DTypeFlags RawToSpec(DTypeFlags flags)
    {
        return (DTypeFlags)((ushort)(flags & DTypeFlags._RawAll) >> kshiftRawFlags);
    }

    /// <summary>
    /// Computes the flags given <paramref name="kind"/> and <paramref name="detail"/>.
    /// </summary>
    private static DTypeFlags ComputeFlags(DKind kind, object? detail)
    {
        // Compute the flags.
        DTypeFlags flags;
        switch (kind)
        {
        default:
            return DTypeFlags.None;

        case DKind.General: return DTypeFlags.HasGeneral;
        case DKind.Vac: return DTypeFlags.HasVac;
        case DKind.Uri: return DTypeFlags.HasUri;
        case DKind.Text: return DTypeFlags.HasText;

        case DKind.R8:
        case DKind.R4:
            return DTypeFlags.HasFloat;

        case DKind.Record:
            flags = _GetRecordInfo(detail).Flags | DTypeFlags.HasRecord;
            break;
        case DKind.Module:
            flags = _GetModuleInfo(detail).Flags | DTypeFlags.HasModule;
            break;
        case DKind.Tuple:
            flags = _GetTupleInfo(detail).Flags | DTypeFlags.HasTuple;
            break;
        case DKind.Tensor:
            flags = _GetTensorInfo(detail).ItemType.Flags | DTypeFlags.HasTensor;
            break;
        }

        // For constructed types, propagate the special bits to the raw bits.
        Validation.Assert((flags & DTypeFlags._RawAll) == 0);
        return WithSpecToRaw(flags);
    }

#if DEBUG
    /// <summary>
    /// Calculate the flags the slow way.
    /// </summary>
    private DTypeFlags ComputeFlagsSlow()
    {
        DTypeFlags flags = DTypeFlags.None;
        switch (_kind)
        {
        case DKind.General:
            flags |= DTypeFlags.HasGeneral;
            break;
        case DKind.Vac:
            flags |= DTypeFlags.HasVac;
            break;
        case DKind.Uri:
            flags |= DTypeFlags.HasUri;
            break;
        case DKind.Text:
            flags |= DTypeFlags.HasText;
            break;

        case DKind.R8:
        case DKind.R4:
            flags |= DTypeFlags.HasFloat;
            break;

        case DKind.Record:
            {
                // This method runs too slowly for very large field counts, making large
                // tests run for too long. So limit the flag computation to 20 fields.
                var info = _GetRecordInfo(_detail);
                if (info.Count > 20)
                    flags = _flags;
                else
                {
                    foreach (var pair in info.GetPairs())
                        flags |= pair.type.ComputeFlagsSlow();
                    flags = WithSpecToRaw(flags) | DTypeFlags.HasRecord;
                }
            }
            break;
        case DKind.Module:
            {
                // This method runs too slowly for very large field counts, making large
                // tests run for too long. So limit the flag computation to 20 fields.
                var info = _GetModuleInfo(_detail);
                if (info.Count > 20)
                    flags = _flags;
                else
                {
                    foreach (var pair in info.GetPairs())
                        flags |= pair.type.ComputeFlagsSlow();
                    flags = WithSpecToRaw(flags) | DTypeFlags.HasModule;
                }
            }
            break;
        case DKind.Tuple:
            {
                // This method runs too slowly for very large slot counts, making large
                // tests run for too long. So limit the flag computation to 20 slots.
                var info = _GetTupleInfo(_detail);
                if (info.Count > 20)
                    flags = _flags;
                else
                {
                    foreach (var t in info.Types)
                        flags |= t.ComputeFlagsSlow();
                    flags = WithSpecToRaw(flags) | DTypeFlags.HasTuple;
                }
            }
            break;
        case DKind.Tensor:
            flags |= _GetTensorInfo(_detail).ItemType.ComputeFlagsSlow();
            flags = WithSpecToRaw(flags) | DTypeFlags.HasTensor;
            break;
        }

        if (_seqCount > 0)
            flags |= DTypeFlags.HasSequence | DTypeFlags.HasOpt;
        if (_opt)
        {
            flags |= DTypeFlags.HasOpt;
            if (_kind != DKind.General && _kind != DKind.Text && _kind != DKind.Uri)
                flags |= DTypeFlags.HasRemovableOpt;
        }

        // The raw bits should be a subset of the spec bits.
        Validation.Assert((flags & DTypeFlags._RawAll & ~SpecToRaw(flags)) == 0);
        return flags;
    }
#endif
}
