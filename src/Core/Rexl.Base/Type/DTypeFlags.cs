// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Rexl;

/// <summary>
/// Flags to represent attributes of <see cref="DType"/>. Note that this packs into 16 bits.
/// This is critical, since it matches what <see cref="RedBlackTree{TTree, TKey, TVal}"/> allows
/// and keeps a <see cref="DType"/> at a total of 16 bytes.
/// </summary>
[Flags]
public enum DTypeFlags : ushort
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0x0000,

    /// <summary>
    /// Is or contains a general type.
    /// </summary>
    HasGeneral = 0x0001,

    /// <summary>
    /// Is or contains a vac type.
    /// </summary>
    HasVac = 0x0002,

    /// <summary>
    /// Is or contains a uri type.
    /// </summary>
    HasUri = 0x0004,

    /// <summary>
    /// Is or contains a record type.
    /// </summary>
    HasRecord = 0x0008,

    /// <summary>
    /// Is or contains a module type.
    /// </summary>
    HasModule = 0x0010,

    /// <summary>
    /// Is or contains a tuple type.
    /// </summary>
    HasTuple = 0x0020,

    /// <summary>
    /// Is or contains a tensor type.
    /// </summary>
    HasTensor = 0x0040,

    /// <summary>
    /// Is or contains the text type.
    /// </summary>
    HasText = 0x0080,

    /// <summary>
    /// Is or contains a floating point type.
    /// </summary>
    HasFloat = 0x0100,

    /// <summary>
    /// Currently unused.
    /// </summary>
    _Unused = 0x0200,

    #region Special propagation flags
    // These require special propagation from flags determined by (kind, detail).

    /// <summary>
    /// Is or contains a sequence.
    /// </summary>
    HasSequence = 0x0400,

    /// <summary>
    /// Is or contains an opt type.
    /// </summary>
    HasOpt = 0x0800,

    /// <summary>
    /// Is or contains an opt type that has a corresponding non-opt type.
    /// Note that sequence, text, and uri are opt but don't have a corresponding non-opt.
    /// </summary>
    HasRemovableOpt = 0x1000,

    // All the special propagation bits.
    _SpecAll = 0x1C00,
    #endregion Special propagation flags

    #region Raw flags
    // These mirror the special propagation flags and are "private". More precisely, they are
    // stored in the _flags field, but not returned by the Flags property. They reflect only
    // information determined by the _kind and _detail fields, and not by the other fields of
    // DType.

    _RawHasSequence = 0x2000,
    _RawHasOpt = 0x4000,
    _RawHasRemovableOpt = 0x8000,

    // All the raw bits.
    _RawAll = 0xE000,
    #endregion Excluded flags
}
