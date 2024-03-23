// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// DKind represents the kinds of values that the computational type system deals with.
/// Note that default(DType) will get an invalid DKind (0), causing the IsValid property
/// to be false.
/// WARNING: These values should NOT be serialized as they are subject to change.
/// </summary>
public enum DKind : byte
{
    /// <summary>
    /// General is the universal super-type.
    /// </summary>
    General = 1,

    /// <summary>
    /// Vac is the universal sub-type. It can be thought of as the absence of a value,
    /// or vacuous, void, or even not possible. It can also be thought of as unknown.
    /// It has no equivalent in the .Net type system. The closest would be the void type,
    /// but that isn't quite right since void can't be used in constructed types and can't
    /// be "converted" to another type, while Vac can be.
    /// REVIEW: Should Vac be considered a primitive type? It probably shouldn't be groupable!
    /// </summary>
    Vac,

    /// <summary>
    /// Sequence is a constructed type. It is never the RootKind of a <see cref="DType"/>.
    /// </summary>
    Sequence,

    /// <summary>
    /// Record has a red-black tree with key <see cref="DName"/> and value <see cref="DType"/>.
    /// </summary>
    Record,

    /// <summary>
    /// Module has a red-black tree with key <see cref="DName"/> and value <see cref="DType"/>.
    /// It uses the "tag" to store the symbol kind.
    /// </summary>
    Module,

    /// <summary>
    /// Tuple has an array of slot DType items.
    /// </summary>
    Tuple,

    /// <summary>
    /// Tensor has an item type and rank.
    /// </summary>
    Tensor,

    /// <summary>
    /// Uri has an <see cref="NPath"/> valued "flavor". Acceptance is based on sub-path.
    /// </summary>
    Uri,

    #region Primitive

    /// <summary>
    /// The text type. This includes the null value.
    /// </summary>
    Text,

    #region Numeric

    // Numeric kinds. NOTE: the order of the numeric kinds must be maintained!
    // See the extension methods in DKindUtil for useful predicates.
    // RX are "fractional". The rest are "integral".
    // IA, I8, I4, I2, and I1 are signed integral.
    R8, R4,
    IA,
    I8, I4, I2, I1,
    U8, U4, U2, U1,
    Bit,

    #endregion Numeric

    /// <summary>
    /// The Date[Time] and Time[Span] types.
    /// </summary>
    Date,
    Time,

    /// <summary>
    /// The Guid type.
    /// REVIEW: Perhaps we should treat Guid as opt, since the all-zero value could be
    /// used as a "null" value. Note that Nullable{Guid} will use a whopping 256 bits (32 bytes)!
    /// </summary>
    Guid,

    #endregion Primitive

    _Lim,
}

/// <summary>
/// Extension methods for DKind.
/// </summary>
public static class DKindUtil
{
    public const DKind Min = DKind.General;
    public const DKind Lim = DKind._Lim;
    public const DKind MinPrimitive = DKind.Text;
    public const DKind LimPrimitive = DKind._Lim;
    public const DKind MinNumeric = DKind.R8;
    public const DKind LimNumeric = DKind.Bit + 1;

    public static bool IsValid(this DKind kind) => Min <= kind && kind < Lim;

    /// <summary>
    /// Returns true if:
    /// * A sequence with this item kind can be treated as a sequence with item kind General.
    /// * If there is non-opt type for <paramref name="kind"/>, a value of the non-opt type can be treated as
    ///   a value of the opt type.
    /// This is generally the case when the kind is represented as a reference type. This is used to avoid unnecessary
    /// Map invocations when casting a sequence of items, that is casting from T* to T?* or T* to g*.
    /// </summary>
    public static bool IsReferenceFriendly(this DKind kind)
    {
        switch (kind)
        {
        case DKind.General:
        case DKind.Sequence:
        case DKind.Text:
        case DKind.Record:
        case DKind.Module:
        case DKind.Tuple:
        case DKind.Tensor:
        case DKind.Uri:
            return true;
        }

        return false;
    }

    public static bool IsPrimitive(this DKind kind) => MinPrimitive <= kind && kind < LimPrimitive;

    public static bool IsNumeric(this DKind kind) => MinNumeric <= kind && kind < LimNumeric;

    public static bool IsSignedNumeric(this DKind kind) => DKind.R8 <= kind && kind <= DKind.I1;

    public static bool IsFractional(this DKind kind) => DKind.R8 <= kind && kind <= DKind.R4;

    public static bool IsIntegral(this DKind kind) => DKind.IA <= kind && kind <= DKind.Bit;

    public static bool IsSignedIntegral(this DKind kind) => DKind.IA <= kind && kind <= DKind.I1;

    public static bool IsRx(this DKind kind) => DKind.R8 <= kind && kind <= DKind.R4;

    public static bool IsIxOrUx(this DKind kind) => DKind.I8 <= kind && kind <= DKind.Bit;

    public static bool IsIx(this DKind kind) => DKind.I8 <= kind && kind <= DKind.I1;

    public static bool IsUx(this DKind kind) => DKind.U8 <= kind && kind <= DKind.Bit;

    public static bool IsChrono(this DKind kind) => kind == DKind.Date || kind == DKind.Time;

    public static bool IsUri(this DKind kind) => kind == DKind.Uri;

    /// <summary>
    /// For sized numeric kinds, returns the size in bytes. For bit/bool, returns 0.
    /// For ia (the only unsized numeric kind), returns a large value for convenience.
    /// For non-numeric kinds, returns -1.
    /// </summary>
    public static int NumericSize(this DKind kind)
    {
        switch (kind)
        {
        case DKind.IA:
            return 1 << 29;

        case DKind.R8:
        case DKind.I8:
        case DKind.U8:
            return 8;
        case DKind.R4:
        case DKind.I4:
        case DKind.U4:
            return 4;
        case DKind.I2:
        case DKind.U2:
            return 2;
        case DKind.I1:
        case DKind.U1:
            return 1;
        case DKind.Bit:
            return 0;

        default:
            Validation.Assert(!kind.IsNumeric());
            return -1;
        }
    }

    internal static string ToStr(this DKind kind, bool opt)
    {
        switch (kind)
        {
        default:
            Validation.Assert(kind == 0);
            return "x";
        case DKind.General:
            Validation.Assert(opt);
            return "g";
        case DKind.Vac:
            return opt ? "o" : "v";
        case DKind.R8:
            return opt ? "r8?" : "r8";
        case DKind.R4:
            return opt ? "r4?" : "r4";
        case DKind.IA:
            return opt ? "i?" : "i";
        case DKind.I8:
            return opt ? "i8?" : "i8";
        case DKind.I4:
            return opt ? "i4?" : "i4";
        case DKind.I2:
            return opt ? "i2?" : "i2";
        case DKind.I1:
            return opt ? "i1?" : "i1";
        case DKind.U8:
            return opt ? "u8?" : "u8";
        case DKind.U4:
            return opt ? "u4?" : "u4";
        case DKind.U2:
            return opt ? "u2?" : "u2";
        case DKind.U1:
            return opt ? "u1?" : "u1";
        case DKind.Bit:
            return opt ? "b?" : "b";
        case DKind.Text:
            Validation.Assert(opt);
            return "s";
        case DKind.Date:
            return opt ? "d?" : "d";
        case DKind.Time:
            return opt ? "t?" : "t";
        case DKind.Guid:
            return opt ? "G?" : "G";

        case DKind.Record:
        case DKind.Module:
        case DKind.Tuple:
        case DKind.Tensor:
        case DKind.Uri:
            Validation.Assert(false);
            return "-";
        }
    }
}
