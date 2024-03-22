// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

partial class DocumentBase
{
    partial class GridConfigImpl
    {
        /// <summary>
        /// Information and functionality for a column type and its representation.
        /// Each column type has two array representations, namely public and private.
        /// The private representation can optionally require an extra bit per value, for
        /// representing null values. This allows more compact representation of null values
        /// than using <see cref="Nullable{T}"/>.
        /// </summary>
        private abstract partial class TypeInfo
        {
            /// <summary>
            /// The item type of the column.
            /// </summary>
            public readonly DType Type;

            /// <summary>
            /// The public system type. This is forced to match the common .Net representation of the <see cref="DType"/>.
            /// </summary>
            public abstract Type SysTypePub { get; }

            /// <summary>
            /// The private system type. This may be different than the public one. Examples:
            /// <list type="bullet">
            /// <item><c>DType.I4Opt</c> may be represented with public type <c>int?</c> but private type
            ///   <c>int</c>, together with a value flag.</item>
            /// <item><c>DType.BitOpt</c> may be represented with public type <c>bool?</c> but private type
            ///   <c>byte</c>, with <c>null</c> represented by <c>0</c> and non-null values represented by
            ///   <c>0x80</c> and <c>0x81</c>.</item>
            /// <item><c>DType.DateOpt</c> may be represented with public type <c>RDate?</c> but private
            ///   type <c>ulong</c> with <c>null</c> represented by <c>0</c> and non-null values represented by
            ///   <c>(ulong)ticks | 0x8000_0000_0000_0000</c>.</item>
            /// </list>
            /// </summary>
            public abstract Type SysTypePri { get; }

            /// <summary>
            /// Whether this type info needs a value flag, that is, a separate bit for indicating a non-null value.
            /// Note that when this is true, the public type is typically <c>S?</c> where <c>S</c> is the
            /// private type. Note that when this is false, the public and private types need not be the same,
            /// as described in the documentation for <see cref="SysTypePri"/>.
            /// </summary>
            public abstract bool NeedFlag { get; }

            /// <summary>
            /// Whether the default value for this type is the same as the default value of the system type.
            /// This is generally true, but not necessarily true for things like non-opt uri types.
            /// </summary>
            public virtual bool SpecDefault => false;

            protected TypeInfo(DType type)
            {
                Validation.Assert(type.IsValid);
                Validation.Assert(!NeedFlag || type.IsOpt);
                Validation.AssertValue(SysTypePub);
                Validation.AssertValue(SysTypePri);
                // If the two system types are the same, then there shouldn't be a need for a flag!
                Validation.Assert(!NeedFlag || SysTypePub != SysTypePri);

                Type = type;
                KindCode = CodeFromType(type);
            }

            public static byte CodeFromType(DType type)
            {
                byte code;
                switch (type.Kind)
                {
                case DKind.Uri: code = 0x06; break;
                case DKind.Vac: code = 0x0A; break;
                case DKind.Text: code = 0x0B; break;
                case DKind.Bit: code = 0x0C; break;
                case DKind.R8: code = 0x0D; break;
                case DKind.R4: code = 0x0E; break;
                case DKind.IA: code = 0x0F; break;
                case DKind.I8: code = 0x10; break;
                case DKind.I4: code = 0x11; break;
                case DKind.I2: code = 0x12; break;
                case DKind.I1: code = 0x13; break;
                case DKind.U8: code = 0x14; break;
                case DKind.U4: code = 0x15; break;
                case DKind.U2: code = 0x16; break;
                case DKind.U1: code = 0x17; break;
                case DKind.Date: code = 0x18; break;
                case DKind.Time: code = 0x19; break;

                default:
                    code = SpecCode;
                    break;
                }

                Validation.Assert(code < OptFlag);
                return type.HasReq ? (byte)(code | OptFlag) : code;
            }

            public static bool TryGetKindFromCode(byte code, out DKind kind, out bool opt)
            {
                opt = (code & OptFlag) != 0;
                code = (byte)(code & ~OptFlag);

                switch (code)
                {
                case 0x06: kind = DKind.Uri; return true;
                case 0x0A: kind = DKind.Vac; return true;
                case 0x0B: kind = DKind.Text; return true;
                case 0x0C: kind = DKind.Bit; return true;
                case 0x0D: kind = DKind.R8; return true;
                case 0x0E: kind = DKind.R4; return true;
                case 0x0F: kind = DKind.IA; return true;
                case 0x10: kind = DKind.I8; return true;
                case 0x11: kind = DKind.I4; return true;
                case 0x12: kind = DKind.I2; return true;
                case 0x13: kind = DKind.I1; return true;
                case 0x14: kind = DKind.U8; return true;
                case 0x15: kind = DKind.U4; return true;
                case 0x16: kind = DKind.U2; return true;
                case 0x17: kind = DKind.U1; return true;
                case 0x18: kind = DKind.Date; return true;
                case 0x19: kind = DKind.Time; return true;
                case SpecCode: kind = 0; return true;
                }

                kind = default;
                opt = default;
                return false;
            }

            /// <summary>
            /// Try to cast this <see cref="TypeInfo"/> to <see cref="TypeInfoPub{T}"/>.
            /// </summary>
            public bool TryCastPub<T>(out TypeInfoPub<T> tin)
            {
                tin = this as TypeInfoPub<T>;
                return tin != null;
            }

            /// <summary>
            /// Does type testing to ensure that <paramref name="src"/> is a valid public array.
            /// </summary>
            public abstract bool IsGoodPub(ReadOnly.Array src);

            /// <summary>
            /// Create a blank private array of the given length.
            /// </summary>
            public abstract Array CreatePriArray(int len);

            /// <summary>
            /// Grow the given private array, which must be an array of <see cref="SysTypePri"/>.
            /// </summary>
            public abstract void GrowPriArray(ref Array arr, ref int len, int lenMin);

            /// <summary>
            /// Create a private array of the given <paramref name="len"/>, copying values from the indicated
            /// slots of private array <paramref name="src"/>.
            /// Note that the count of values copied may be less than <paramref name="len"/>.
            /// </summary>
            public abstract Array CreatePriArray(int len, ReadOnly.Array src, in SlotInfo slinSrc);

            /// <summary>
            /// Create a public array of the given <paramref name="len"/>, copying values from the indicated
            /// slots of private array <paramref name="src"/>.
            /// Note that the count of values copied may be less than <paramref name="len"/>.
            /// </summary>
            public abstract Array CreatePubArray(int len, ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc);

            /// <summary>
            /// Return whether all of the indicated <paramref name="slots"/> are the default value.
            /// The array must be an array of <see cref="SysTypePri"/>.
            /// </summary>
            public abstract bool IsDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc);

            /// <summary>
            /// Return whether all of the slots from <paramref name="slot"/> upward are the default value.
            /// The array must be an array of <see cref="SysTypePri"/>.
            /// </summary>
            public abstract bool IsDefaultPriTail(ReadOnly.Array src, int slot);

            /// <summary>
            /// Return whether slots marked null, as indicated by the flags, have default value.
            /// The array must be an array of <see cref="SysTypePri"/>.
            /// This is only called when <see cref="NeedFlag"/> is true.
            /// </summary>
            public abstract bool IsNullDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc);

            /// <summary>
            /// Get the value at the given <paramref name="slot"/> of the array.
            /// The array must be an array of <see cref="SysTypePri"/>.
            /// When <see cref="NeedFlag"/> is true, this is only invoked if the corresponding flag is true.
            /// </summary>
            public abstract object GetValueObject(ReadOnly.Array src, int slot);

            /// <summary>
            /// Set the value at the given <paramref name="slotDst"/> of the array to the given value.
            /// The array must be an array of the private system type. The value must be a (boxed)
            /// instance of the public system type, or null if allowed.
            /// </summary>
            public abstract object SetValueObject(object value, Array dst, int slotDst, in FlagInfoWrt flinDst);

            /// <summary>
            /// Set values from <paramref name="src"/> in <paramref name="dst"/> using <paramref name="slinDst"/>.
            /// The <paramref name="dst"/> destination must be an array of the private system type.
            /// The <paramref name="src"/> source must be an enumerable of instances of the public system type
            /// or its non nullable representation when the public type is nullable.
            /// </summary>
            public abstract void SetValues(IEnumerable src, Array dst, SlotInfo slinDst);

            /// <summary>
            /// Set values from <paramref name="src"/> in <paramref name="dst"/> using <paramref name="slinDst"/>.
            /// The <paramref name="dst"/> destination must be an array of the private system type.
            /// The <paramref name="src"/> source must be an enumerable of instances of the public system type.
            /// </summary>
            public abstract void SetValuesFlagged(IEnumerable src, Array dst, SlotInfo slinDst, in FlagInfoWrt flinDst);

            /// <summary>
            /// Set the value at the given <paramref name="slotDst"/> of the array to the given value.
            /// This returns the overwritten value in public form.
            /// </summary>
            public T SetValue<T>(T value, Array dst, int slotDst, in FlagInfoWrt flinDst)
            {
                // Move from method type parameter to class type parameter.
                Validation.BugCheckParam(TryCastPub<T>(out var tinT), nameof(T));
                return tinT.SetValuePub(value, dst, slotDst, flinDst);
            }

            /// <summary>
            /// Copies values from <paramref name="src"/> to <paramref name="dst"/> using <paramref name="slinDst"/>
            /// to map from index to slot. This returns the overwritten values in public form.
            /// </summary>
            public abstract Immutable.Array SetValues(ReadOnly.Array src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst);

            /// <summary>
            /// Copies values from <paramref name="src"/> to <paramref name="dst"/> using <paramref name="slinDst"/>
            /// to map from index to slot. This returns the overwritten values in public form.
            /// </summary>
            public Immutable.Array<T> SetValues<T>(ReadOnly.Array<T> src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                // Move from method type parameter to class type parameter.
                Validation.BugCheckParam(TryCastPub<T>(out var tinT), nameof(T));
                return tinT.SetValuesPub(src, dst, in slinDst, in flinDst);
            }

            /// <summary>
            /// Copies values from <paramref name="src"/> to <paramref name="dst"/>, using <paramref name="slinSrc"/> and
            /// <paramref name="slinDst"/> to map from indices to slots. The arrays must be arrays of <see cref="SysTypePri"/>.
            /// The <see cref="SlotInfo"/> structs must have the same count.
            /// </summary>
            public abstract void CopyValues(ReadOnly.Array src, in SlotInfo slinSrc, Array dst, in SlotInfo slinDst);

            /// <summary>
            /// Copies values from <paramref name="src"/> to <paramref name="dst"/> using <paramref name="slinDst"/> to map from
            /// row to destination slot.
            /// The <paramref name="src"/> array must be an array of <see cref="SysTypePub"/>.
            /// The <paramref name="dst"/> array must be an array of <see cref="SysTypePri"/>.
            /// </summary>
            public abstract void CopyValues(ReadOnly.Array src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst);

            /// <summary>
            /// Copies values and flags from <paramref name="src"/> and <paramref name="flinSrc"/> using <paramref name="slinSrc"/>
            /// to map from row to source slot, to <paramref name="dst"/> and <paramref name="flinDst"/> using identity order.
            /// The arrays must be arrays of <see cref="SysTypePri"/>.
            /// </summary>
            public abstract void CopyValues(ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc, Array dst, in FlagInfoWrt flinDst);

            /// <summary>
            /// Copies values from <paramref name="src"/> to <paramref name="dst"/> using <paramref name="slinDst"/> to map from
            /// row to destination slot. Both arrays have item type <see cref="SysTypePri"/>.
            /// </summary>
            public abstract void CopyValuesPri(ReadOnly.Array src, int min, Array dst, in SlotInfo slinDst);

            /// <summary>
            /// Clears (sets to blank) values of <paramref name="dst"/> indicated by <paramref name="slinDst"/>.
            /// The <paramref name="dst"/> array must be an array of <see cref="SysTypePri"/>.
            /// </summary>
            public abstract void ClearValues(Array dst, in SlotInfo slinDst);

            /// <summary>
            /// Generate IL converting an instance of <see cref="SysTypePri"/> to an instance of <see cref="SysTypePub"/>.
            /// </summary>
            public abstract void GenConvertPriToPub(MethodGenerator gen);

            /// <summary>
            /// Generate IL converting the value portion of <see cref="SysTypePub"/> to an instance of <see cref="SysTypePri"/>.
            /// If <see cref="NeedFlag"/> is true, the flag is handled separately and we assume <see cref="SysTypePub"/> is
            /// <see cref="Nullable{T}"/>. In that case, this should convert from a value of T, not T?. That is,
            /// <see cref="Nullable{T}.GetValueOrDefault"/> has already been called.
            /// </summary>
            public abstract void GenConvertPubValToPri(MethodGenerator gen);

            protected static T[] CreateArrCore<T>(int len)
            {
                Validation.Assert(len >= 0);
                if (len > 0)
                    return new T[len];
                return Array.Empty<T>();
            }

            protected static void SetValuesCore<T>(IEnumerable<T> src, T[] dst, SlotInfo slinDst)
            {
                int i = 0;
                foreach (var value in src)
                {
                    int slotDst = slinDst.GetSlot(i++);
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = value;
                }
                Validation.Assert(i == slinDst.Count);
            }
        }

        /// <summary>
        /// This has the public system type as a type parameter.
        /// </summary>
        private abstract partial class TypeInfoPub<TPub> : TypeInfo
        {
            public sealed override Type SysTypePub => typeof(TPub);

            protected TypeInfoPub(DType type)
                : base(type)
            {
            }

            public sealed override bool IsGoodPub(ReadOnly.Array src)
            {
                return src.TryCast<TPub>(out _);
            }

            public abstract TPub GetValuePub(ReadOnly.Array src, int slot);

            public abstract TPub SetValuePub(TPub value, Array dst, int slotDst, in FlagInfoWrt flinDst);

            public abstract Immutable.Array<TPub> SetValuesPub(ReadOnly.Array<TPub> src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst);
        }

        /// <summary>
        /// This has the public and private system types as type parameters.
        /// </summary>
        private abstract partial class TypeInfo<TPub, TPri> : TypeInfoPub<TPub>
        {
            public sealed override Type SysTypePri => typeof(TPri);

            protected readonly TPri _defPri;

            protected TypeInfo(DType type, TPri defPri)
                : base(type)
            {
                _defPri = defPri;
            }

            public override Array CreatePriArray(int len)
            {
                return CreateArrCore<TPri>(len);
            }

            public override void GrowPriArray(ref Array arr, ref int len, int lenMin)
            {
                Validation.Assert(arr is TPri[] || arr == null);
                var a = (TPri[])arr;
                Util.Grow(ref a, ref len, lenMin);
                arr = a;
            }

            public sealed override Array CreatePriArray(int len, ReadOnly.Array src, in SlotInfo slinSrc)
            {
                Validation.BugCheckParam(src.TryCast<TPri>(out var srcT), nameof(src));
                return CreatePriArray(len, srcT, in slinSrc);
            }

            protected TPri[] CreatePriArray(int len, ReadOnly.Array<TPri> src, in SlotInfo slinSrc)
            {
                var dst = CreateArrCore<TPri>(len);
                slinSrc.CopyValuesSrc(src, dst);
                return dst;
            }

            public sealed override TPub GetValuePub(ReadOnly.Array src, int slot)
            {
                // Move from general array to specific array.
                Validation.BugCheckParam(src.TryCast<TPri>(out var srcT), nameof(src));
                return GetValueCore(srcT, slot);
            }

            protected abstract TPub GetValueCore(ReadOnly.Array<TPri> src, int slot);

            public sealed override void CopyValues(ReadOnly.Array src, in SlotInfo slinSrc, Array dst, in SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPri>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(slinSrc.Count == slinDst.Count);
                slinDst.CopyValues(srcT, in slinSrc, dstT);
            }

            public sealed override void CopyValuesPri(ReadOnly.Array src, int min, Array dst, in SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPri>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.BugCheckIndexInclusive(min, srcT.Length, nameof(min));
                Validation.BugCheckParam(slinDst.Count <= srcT.Length - min, nameof(src));
                slinDst.CopyValues(srcT, min, dstT);
            }

            public sealed override void ClearValues(Array dst, in SlotInfo slinDst)
            {
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                slinDst.ClearValues(dstT, _defPri);
            }
        }

        /// <summary>
        /// This is for column types that don't need a value flag.
        /// </summary>
        private abstract partial class TypeInfoSansFlag<TPub, TPri> : TypeInfo<TPub, TPri>
        {
            public sealed override bool NeedFlag => false;

            protected TypeInfoSansFlag(DType type, TPri defPri)
                : base(type, defPri)
            {
            }

            public sealed override TPub SetValuePub(TPub value, Array dst, int slotDst, in FlagInfoWrt flinDst)
            {
                // Move from general array to specific array.
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);
                return SetValueCore(dstT, slotDst, value);
            }

            public sealed override Immutable.Array SetValues(ReadOnly.Array src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPub>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);
                return SetValuesCore(srcT, dstT, in slinDst);
            }

            public sealed override Immutable.Array<TPub> SetValuesPub(ReadOnly.Array<TPub> src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                // Move from general array to specific array.
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);
                return SetValuesCore(src, dstT, in slinDst);
            }

            public sealed override void CopyValues(ReadOnly.Array src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPub>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);
                CopyValuesCore(srcT, dstT, in slinDst);
            }

            public sealed override void CopyValues(ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc, Array dst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPri>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(flinSrc.IsBlank);
                Validation.Assert(flinDst.IsBlank);
                CopyValuesCore(srcT, in slinSrc, dstT);
            }

            protected abstract TPub SetValueCore(TPri[] dst, int slot, TPub value);

            protected abstract Immutable.Array<TPub> SetValuesCore(ReadOnly.Array<TPub> src, TPri[] dst, in SlotInfo slinDst);

            protected abstract void CopyValuesCore(ReadOnly.Array<TPub> src, TPri[] dst, in SlotInfo slinDst);

            protected abstract void CopyValuesCore(ReadOnly.Array<TPri> src, in SlotInfo slinSrc, TPri[] dst);

            public override bool IsNullDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc)
            {
                Validation.Assert(flinSrc.IsBlank);
                return true;
            }
        }

        /// <summary>
        /// This is for column types that need a value flag.
        /// </summary>
        private abstract partial class TypeInfoWithFlag<TPub, TPri> : TypeInfo<TPub, TPri>
        {
            public sealed override bool NeedFlag => true;

            protected TypeInfoWithFlag(DType type, TPri defPri)
                : base(type, defPri)
            {
            }

            public sealed override TPub SetValuePub(TPub value, Array dst, int slotDst, in FlagInfoWrt flinDst)
            {
                // Move from general array to specific array.
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(!flinDst.IsBlank);
                return SetValueCore(value, dstT, slotDst, in flinDst);
            }

            public sealed override Immutable.Array SetValues(ReadOnly.Array src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPub>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(!flinDst.IsBlank);
                return SetValuesCore(srcT, dstT, in slinDst, in flinDst);
            }

            public sealed override Immutable.Array<TPub> SetValuesPub(ReadOnly.Array<TPub> src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(!flinDst.IsBlank);
                return SetValuesCore(src, dstT, in slinDst, in flinDst);
            }

            public sealed override void CopyValues(ReadOnly.Array src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPub>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(!flinDst.IsBlank);
                CopyValuesCore(srcT, dstT, in slinDst, in flinDst);
            }

            public sealed override void CopyValues(ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc, Array dst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TPri>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TPri>(out var dstT), nameof(dst));
                Validation.Assert(!flinSrc.IsBlank);
                Validation.Assert(!flinDst.IsBlank);
                CopyValuesCore(srcT, in slinSrc, in flinSrc, dstT, in flinDst);
            }

            protected abstract TPub SetValueCore(TPub value, TPri[] dst, int slotDst, in FlagInfoWrt flin);

            protected abstract Immutable.Array<TPub> SetValuesCore(ReadOnly.Array<TPub> src, TPri[] dst, in SlotInfo slinDst, in FlagInfoWrt flinDst);

            protected abstract void CopyValuesCore(ReadOnly.Array<TPub> src, TPri[] dst, in SlotInfo slinDst, in FlagInfoWrt flinDst);

            protected abstract void CopyValuesCore(ReadOnly.Array<TPri> src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc, TPri[] dst, in FlagInfoWrt flinDst);
        }

        /// <summary>
        /// An item-type specific TypeInfo with identical public and private system types.
        /// </summary>
        private abstract partial class TypeInfoSame<T> : TypeInfoSansFlag<T, T>
        {
            protected TypeInfoSame(DType type, T def = default)
                : base(type, def)
            {
            }

            public sealed override Array CreatePubArray(int len, ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc)
            {
                // Our public and private representations are the same.
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.Assert(flinSrc.IsBlank);
                return CreatePriArray(len, srcT, in slinSrc);
            }

            public sealed override object GetValueObject(ReadOnly.Array src, int slot)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.AssertIndex(slot, srcT.Length);
                return srcT[slot];
            }

            protected sealed override T GetValueCore(ReadOnly.Array<T> src, int slot)
            {
                Validation.AssertIndex(slot, src.Length);
                return src[slot];
            }

            protected sealed override T SetValueCore(T[] dst, int slot, T value)
            {
                Validation.AssertValue(dst);
                Validation.AssertIndex(slot, dst.Length);
                var res = dst[slot];
                dst[slot] = value;
                return res;
            }

            protected sealed override Immutable.Array<T> SetValuesCore(ReadOnly.Array<T> src, T[] dst, in SlotInfo slinDst)
            {
                return slinDst.SetValuesDst(src, dst);
            }

            protected sealed override void CopyValuesCore(ReadOnly.Array<T> src, T[] dst, in SlotInfo slinDst)
            {
                slinDst.CopyValuesDst(src, dst);
            }

            protected sealed override void CopyValuesCore(ReadOnly.Array<T> src, in SlotInfo slinSrc, T[] dst)
            {
                slinSrc.CopyValuesSrc(src, dst);
            }

            public sealed override void GenConvertPriToPub(MethodGenerator gen)
            {
                // They are the same, so no conversion code.
                Validation.AssertValue(gen);
            }

            public sealed override void GenConvertPubValToPri(MethodGenerator gen)
            {
                // They are the same, so no conversion code.
                Validation.AssertValue(gen);
            }
        }

        /// <summary>
        /// Type info for the text type.
        /// </summary>
        private sealed partial class TypeInfoText : TypeInfoSame<string>
        {
            public TypeInfoText()
                : base(DType.Text)
            {
            }

            public override bool IsDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc)
            {
                Validation.BugCheckParam(src.TryCast<string>(out var srcT), nameof(src));
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    if (slinSrc.GetValue(srcT, i) != null)
                        return false;
                }
                return true;
            }

            public override bool IsDefaultPriTail(ReadOnly.Array src, int slot)
            {
                Validation.BugCheckParam(src.TryCast<string>(out var srcT), nameof(src));
                int slotLim = srcT.Length;
                for (; slot < slotLim; slot++)
                {
                    if (srcT[slot] != null)
                        return false;
                }
                return true;
            }

            public override object SetValueObject(object value, Array dst, int slotDst, in FlagInfoWrt flin)
            {
                Validation.BugCheckParam(dst.TryCast<string>(out var dstT), nameof(dst));
                Validation.Assert(flin.IsBlank);
                var res = dstT[slotDst];
                if (value == null)
                    dstT[slotDst] = null;
                else if (value is string v)
                    dstT[slotDst] = v;
                else
                    throw Validation.BugExceptParam(nameof(value));
                return res;
            }

            public override void SetValues(IEnumerable src, Array dst, SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<string>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<string>(out var dstT), nameof(dst));

                SetValuesCore(srcT, dstT, slinDst);
            }

            public override void SetValuesFlagged(IEnumerable src, Array dst, SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<string>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<string>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);

                SetValuesCore(srcT, dstT, slinDst);
            }
        }

        /// <summary>
        /// Type info for a non-opt type.
        /// </summary>
        private sealed partial class TypeInfoReq<T> : TypeInfoSame<T>
            where T : struct, IEquatable<T>
        {
            public TypeInfoReq(DType type)
                : base(type)
            {
                Validation.Assert(typeof(T).IsValueType);
                Validation.Assert(!typeof(T).IsGenericType);
                Validation.Assert(!type.IsOpt);
            }

            public override bool IsDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    // REVIEW: Is the .Equals call fast enough? Can it be made faster?
                    if (!default(T).Equals(slinSrc.GetValue(srcT, i)))
                        return false;
                }
                return true;
            }

            public override bool IsDefaultPriTail(ReadOnly.Array src, int slot)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                int slotLim = srcT.Length;
                for (; slot < slotLim; slot++)
                {
                    // REVIEW: Is this fast enough? Can it be made faster?
                    if (!default(T).Equals(srcT[slot]))
                        return false;
                }
                return true;
            }

            public override object SetValueObject(object value, Array dst, int slotDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);
                var res = dstT[slotDst];
                if (value is T v)
                    dstT[slotDst] = v;
                else
                    throw Validation.BugExceptParam(nameof(value));
                return res;
            }

            public override void SetValues(IEnumerable src, Array dst, SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));

                SetValuesCore(srcT, dstT, slinDst);
            }

            public override void SetValuesFlagged(IEnumerable src, Array dst, SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);

                SetValuesCore(srcT, dstT, slinDst);
            }
        }

        /// <summary>
        /// Type info for an opt value type that uses a flag to indicate that there is a value.
        /// </summary>
        private sealed partial class TypeInfoFlagOpt<T> : TypeInfoWithFlag<T?, T>
            where T : struct, IEquatable<T>
        {
            private readonly ConstructorInfo _ctor;

            public TypeInfoFlagOpt(DType type)
                : base(type, default)
            {
                Validation.Assert(typeof(T).IsValueType);
                Validation.Assert(!typeof(T).IsGenericType);
                Validation.Assert(type.HasReq);

                _ctor = typeof(T?).GetConstructor(new Type[] { SysTypePri });
            }

            public override Array CreatePubArray(int len, ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.AssertIndexInclusive(slinSrc.Count, len);
                Validation.Assert(!flinSrc.IsBlank);

                var res = CreateArrCore<T?>(len);
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    int slotSrc = slinSrc.GetSlot(i);
                    Validation.AssertIndex(slotSrc, srcT.Length);
                    // We can just ignore nulls, since the array is initialized to all null.
                    if (flinSrc.Test(slotSrc))
                        res[i] = srcT[slotSrc];
                }
                return res;
            }

            public override bool IsDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    // REVIEW: Is the .Equals call fast enough? Can it be made faster?
                    if (!default(T).Equals(slinSrc.GetValue(srcT, i)))
                        return false;
                }
                return true;
            }

            public override bool IsDefaultPriTail(ReadOnly.Array src, int slot)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                int slotLim = srcT.Length;
                for (; slot < slotLim; slot++)
                {
                    // REVIEW: Is this fast enough? Can it be made faster?
                    if (!default(T).Equals(srcT[slot]))
                        return false;
                }
                return true;
            }

            public override bool IsNullDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.Assert(!flinSrc.IsBlank);
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    int slot = slinSrc.GetSlot(i);
                    Validation.AssertIndex(slot, srcT.Length);
                    // REVIEW: Is the .Equals call fast enough? Can it be made faster?
                    if (!flinSrc.Test(slot) && !default(T).Equals(srcT[slot]))
                        return false;
                }
                return true;
            }

            public override object GetValueObject(ReadOnly.Array src, int slot)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.AssertIndex(slot, srcT.Length);
                return srcT[slot];
            }

            protected override T? GetValueCore(ReadOnly.Array<T> src, int slot)
            {
                Validation.AssertIndex(slot, src.Length);
                return src[slot];
            }

            public override object SetValueObject(object value, Array dst, int slotDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.AssertIndex(slotDst, dstT.Length);
                Validation.Assert(!flinDst.IsBlank);

                var res = flinDst.Test(slotDst) ? (object)dstT[slotDst] : null;
                if (value == null)
                {
                    flinDst.Clear(slotDst);
                    dstT[slotDst] = default;
                }
                else if (value is T v)
                {
                    flinDst.Set(slotDst);
                    dstT[slotDst] = v;
                }
                else
                    throw Validation.BugExceptParam(nameof(value));
                return res;
            }

            public override void SetValues(IEnumerable src, Array dst, SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));

                SetValuesCore(srcT, dstT, slinDst);
            }

            public override void SetValuesFlagged(IEnumerable src, Array dst, SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<T?>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.Assert(!flinDst.IsBlank);

                SetValuesCore(srcT, dstT, slinDst, flinDst);
            }

            private void SetValuesCore(IEnumerable<T?> src, T[] dst, SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.Assert(!flinDst.IsBlank);

                int i = 0;
                foreach (var value in src)
                {
                    int slotDst = slinDst.GetSlot(i++);
                    Validation.AssertIndex(slotDst, dst.Length);
                    if (value == null)
                    {
                        flinDst.Clear(slotDst);
                        dst[slotDst] = default;
                    }
                    else
                    {
                        flinDst.Set(slotDst);
                        dst[slotDst] = value.Value;
                    }
                }
                Validation.Assert(i == slinDst.Count);
            }

            protected override T? SetValueCore(T? value, T[] dst, int slotDst, in FlagInfoWrt flinDst)
            {
                var res = flinDst.Test(slotDst) ? (T?)dst[slotDst] : null;
                Validation.AssertIndex(slotDst, dst.Length);

                dst[slotDst] = value.GetValueOrDefault();
                flinDst.Set(slotDst, value.HasValue);
                return res;
            }

            protected override Immutable.Array<T?> SetValuesCore(ReadOnly.Array<T?> src, T[] dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.AssertValue(dst);
                Validation.AssertIndexInclusive(slinDst.Count, src.Length);
                Validation.Assert(!flinDst.IsBlank);

                var bldr = Immutable.Array.CreateBuilder<T?>(slinDst.Count, init: true);
                for (int i = 0; i < slinDst.Count; i++)
                {
                    int slotDst = slinDst.GetSlot(i);
                    Validation.AssertIndex(slotDst, dst.Length);
                    if (flinDst.Test(slotDst))
                        bldr[i] = dst[slotDst];
                    var s = src[i];
                    dst[slotDst] = s.GetValueOrDefault();
                    flinDst.Set(slotDst, s.HasValue);
                }
                return bldr.ToImmutable();
            }

            protected override void CopyValuesCore(ReadOnly.Array<T?> src, T[] dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.AssertIndexInclusive(slinDst.Count, src.Length);
                Validation.Assert(!flinDst.IsBlank);

                for (int i = 0; i < slinDst.Count; i++)
                {
                    var s = src[i];
                    int slotDst = slinDst.GetSlot(i);
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = s.GetValueOrDefault();
                    flinDst.Set(slotDst, s.HasValue);
                }
            }

            protected override void CopyValuesCore(ReadOnly.Array<T> src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc, T[] dst, in FlagInfoWrt flinDst)
            {
                Validation.AssertIndexInclusive(slinSrc.Count, Util.Size(dst));
                Validation.Assert(!flinSrc.IsBlank);
                Validation.Assert(!flinDst.IsBlank);

                for (int i = 0; i < slinSrc.Count; i++)
                {
                    int slotSrc = slinSrc.GetSlot(i);
                    Validation.AssertIndex(slotSrc, src.Length);
                    dst[i] = src[slotSrc];
                    flinDst.Set(i, flinSrc.Test(slotSrc));
                }
            }

            public override void GenConvertPriToPub(MethodGenerator gen)
            {
                Validation.AssertValue(gen);
                // The req type is on the stack. Construct the nullable.
                gen.Il.Newobj(_ctor);
            }

            public override void GenConvertPubValToPri(MethodGenerator gen)
            {
                Validation.AssertValue(gen);
                // The result of GetValueOrDefault() is already on the stack, so nothing to do.
            }
        }

        private sealed partial class TypeInfoCls<T> : TypeInfoSame<T>
            where T : class
        {
            public override bool SpecDefault => _defPri != null;

            private readonly Action<TypeManager.ValueWriter, T> _fnWrite;
            private readonly Func<TypeManager.ValueReader, T> _fnRead;

            public TypeInfoCls(DType type, T def, Action<TypeManager.ValueWriter, T> fnWrite, Func<TypeManager.ValueReader, T> fnRead)
                : base(type, def)
            {
                Validation.Assert(type.IsOpt == (def == null));
                Validation.AssertValue(fnWrite);
                Validation.AssertValue(fnRead);

                _fnWrite = fnWrite;
                _fnRead = fnRead;
            }

            public override Array CreatePriArray(int len)
            {
                var res = CreateArrCore<T>(len);
                var def = _defPri;
                if (def != null)
                {
                    for (int i = 0; i < len; i++)
                        res[i] = def;
                }
                return res;
            }

            public override void GrowPriArray(ref Array arr, ref int len, int lenMin)
            {
                Validation.Assert(arr is T[] || arr == null);
                var a = (T[])arr;
                Util.Grow(ref a, ref len, lenMin);

                int lenOld = Util.Size(arr);
                var def = _defPri;
                if (def != null && len > lenOld)
                {
                    for (int i = lenOld; i < len; i++)
                        a[i] = def;
                }
                arr = a;
            }

            public override bool IsDefaultPri(ReadOnly.Array src, in SlotInfo slinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                var def = _defPri;
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    if (slinSrc.GetValue(srcT, i) != def)
                        return false;
                }
                return true;
            }

            public override bool IsDefaultPriTail(ReadOnly.Array src, int slot)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                int slotLim = srcT.Length;
                var def = _defPri;
                for (; slot < slotLim; slot++)
                {
                    if (srcT[slot] != def)
                        return false;
                }
                return true;
            }

            public override object SetValueObject(object value, Array dst, int slotDst, in FlagInfoWrt flin)
            {
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.Assert(flin.IsBlank);
                var res = dstT[slotDst];
                if (value == null)
                    dstT[slotDst] = null;
                else if (value is T v)
                    dstT[slotDst] = v;
                else
                    throw Validation.BugExceptParam(nameof(value));
                return res;
            }

            public override void SetValues(IEnumerable src, Array dst, SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));

                SetValuesCore(srcT, dstT, slinDst);
            }

            public override void SetValuesFlagged(IEnumerable src, Array dst, SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.Assert(flinDst.IsBlank);

                SetValuesCore(srcT, dstT, slinDst);
            }
        }

        /// <summary>
        /// The mapping from <see cref="DType"/> to <see cref="TypeInfo"/> for primitive types.
        /// </summary>
        private static readonly ReadOnly.Dictionary<DType, TypeInfo> _typeMap = BuildTypeMap();

        /// <summary>
        /// Builds the static readonly type map.
        /// </summary>
        private static ReadOnly.Dictionary<DType, TypeInfo> BuildTypeMap()
        {
            var map = new Dictionary<DType, TypeInfo>();

            void Add<T>(DType type)
                where T : struct, IEquatable<T>
            {
                Validation.Assert(!type.IsOpt);
                var typeOpt = type.ToOpt();
                map.Add(type, new TypeInfoReq<T>(type));
                map.Add(typeOpt, new TypeInfoFlagOpt<T>(typeOpt));
            }

            map.Add(DType.Text, new TypeInfoText());

            Add<double>(DType.R8Req);
            Add<float>(DType.R4Req);
            Add<Integer>(DType.IAReq);
            Add<long>(DType.I8Req);
            Add<int>(DType.I4Req);
            Add<short>(DType.I2Req);
            Add<sbyte>(DType.I1Req);
            Add<ulong>(DType.U8Req);
            Add<uint>(DType.U4Req);
            Add<ushort>(DType.U2Req);
            Add<byte>(DType.U1Req);
            Add<bool>(DType.BitReq);
            Add<Date>(DType.DateReq);
            Add<Time>(DType.TimeReq);
            Add<Guid>(DType.GuidReq);

            return map;
        }

        /// <summary>
        /// Check that the <see cref="DType"/> is a valid item type for a <see cref="GridNode">.
        /// </summary>
        new public static bool IsStdColumnType(DType type)
        {
            return _typeMap.ContainsKey(type);
        }

        /// <summary>
        /// Check that the <see cref="DType"/> is a valid item type for a <see cref="GridNode">.
        /// </summary>
        new public static bool IsValidColumnType(DType type, TypeManager tm)
        {
            if (_typeMap.ContainsKey(type))
                return true;

            if (tm == null)
                return false;
            if (type.HasGeneral)
                return false;

            // REVIEW: Attempt to support non-standard types as non-editable and non-promotable entities.
            if (tm.TryEnsureSysType(type, out _) && tm.TryEnsureDefaultValue(type, out _) && tm.CanReadWrite(type))
                return true;
            return false;
        }

        /// <summary>
        /// The mapping from <see cref="DType"/> and <see cref="Type"/> to <see cref="TypeInfo"/> for
        /// constructed types.
        /// </summary>
        private static volatile ConcurrentDictionary<(DType type, Type st), TypeInfo> _specMap;

        private static bool TryGetTypeInfo(DType type, out TypeInfo tin, TypeManager tm)
        {
            Validation.AssertValue(tm);

            if (_typeMap.TryGetValue(type, out tin))
                return true;

            tin = null;

            if (type.HasGeneral)
                return false;
            if (!tm.TryEnsureSysType(type, out var st))
                return false;

            if (_specMap == null)
                Interlocked.CompareExchange(ref _specMap, new ConcurrentDictionary<(DType type, Type st), TypeInfo>(), null);

            var key = (type, st);
            if (!_specMap.TryGetValue(key, out tin))
            {
                var meth = new Func<TypeManager, DType, TypeInfoCls<object>>(GetClsTypeInfoOrNull<object>)
                    .Method.GetGenericMethodDefinition().MakeGenericMethod(st);

                var tmp = (TypeInfo)meth.Invoke(null, new object[] { tm, type });
                if (tmp == null)
                    return false;

                tin = _specMap.GetOrAdd(key, tmp);
            }

            Validation.Assert(tin.GetType() == typeof(TypeInfoCls<>).MakeGenericType(st));
            Validation.Assert(tin.Type == type);
            Validation.Assert(tin.SysTypePub == st);
            Validation.Assert(tin.SysTypePri == st);
            Validation.Assert(!tin.NeedFlag);
            return true;
        }

        private static TypeInfoCls<T> GetClsTypeInfoOrNull<T>(TypeManager tm, DType type)
            where T : class
        {
            Validation.AssertValue(tm);
            Validation.Assert(type.IsValid);
            Validation.Assert(!type.HasGeneral);
#if DEBUG
            Validation.Assert(tm.TryEnsureSysType(type, out Type st) && st == typeof(T));
#endif

            if (!tm.TryEnsureDefaultValue(type, out var entry))
                return null;
            Validation.Assert((entry.value == null) == type.IsOpt);
            Validation.Assert(entry.special == (entry.value != null));
            Validation.Assert(entry.value == null || entry.value is T);

            if (!tm.TryGetWriter<T>(type.ToReq(), out var fnWrite))
                return null;
            if (!tm.TryGetReader<T>(type.ToReq(), out var fnRead))
                return null;

            return new TypeInfoCls<T>(type, (T)entry.value, fnWrite, fnRead);
        }
    }
}
