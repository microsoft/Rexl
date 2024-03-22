// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Flow;

using Integer = System.Numerics.BigInteger;

partial class DocumentBase
{
    partial class GridConfigImpl
    {
        private abstract class Converter
        {
            /// <summary>
            /// Converts values from an array <paramref name="src"/> of this converter's source type
            /// to <paramref name="dst"/> using <paramref name="slin"/> and copies the source values
            /// to <paramref name="old"/> starting at <paramref name="minOld"/> for undo purposes.
            /// </summary>
            public abstract void MapValues(ReadOnly.Array src, Array old, int minOld, Array dst, in SlotInfo slin);

            /// <summary>
            /// Converts values from an array <paramref name="src"/> of this converter's source type
            /// using <paramref name="slinSrc"/> to <paramref name="dst"/> using <paramref name="slinDst"/>.
            /// </summary>
            public abstract void MapValues(ReadOnly.Array src, in SlotInfo slinSrc, Array dst, in SlotInfo slinDst);

            /// <summary>
            /// Converts values from an emumerable <paramref name="src"/> of this converter's source type
            /// to <paramref name="dst"/> using <paramref name="slinDst"/>.
            /// </summary>
            public abstract void MapValues(IEnumerable src, Array dst, in SlotInfo slinDst);

            /// <summary>
            /// Converts values from an enumerable <paramref name="src"/> of this converter's source type as nullables
            /// to <paramref name="dst"/> using <paramref name="slinDst"/>
            /// and clears/sets the corresponding flags in <paramref name="flinDst"/>.
            /// </summary>
            public abstract void MapValuesFlagged(IEnumerable src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst);
        }

        private abstract class Converter<TSrc, TDst> : Converter
        {
            public sealed override void MapValues(ReadOnly.Array src, Array old, int minOld, Array dst, in SlotInfo slin)
            {
                Validation.BugCheckParam(src.TryCast<TSrc>(out var srcT), nameof(src));
                Validation.BugCheckParam(old.TryCast<TSrc>(out var oldT), nameof(old));
                Validation.BugCheckParam(dst.TryCast<TDst>(out var dstT), nameof(dst));
                MapValuesCore(srcT, oldT, minOld, dstT, in slin);
            }

            public sealed override void MapValues(ReadOnly.Array src, in SlotInfo slinSrc, Array dst, in SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<TSrc>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TDst>(out var dstT), nameof(dst));
                MapValuesCore(srcT, in slinSrc, dstT, in slinDst);
            }

            public sealed override void MapValues(IEnumerable src, Array dst, in SlotInfo slinDst)
            {
                Validation.BugCheckParam(src.TryCast<TSrc>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TDst>(out var dstT), nameof(dst));
                MapValuesCore(srcT, dstT, in slinDst);
            }

            protected void MapValuesCore(ReadOnly.Array<TSrc> src, TSrc[] old, int minOld, TDst[] dst, in SlotInfo slin)
            {
                Validation.AssertIndexInclusive(minOld, old.Length);
                Validation.Assert(slin.Count <= old.Length - minOld);

                for (int i = 0; i < slin.Count; i++)
                {
                    int slot = slin.GetSlot(i);
                    Validation.AssertIndex(slot, src.Length);
                    Validation.AssertIndex(slot, dst.Length);
                    TSrc s = src[slot];
                    old[i + minOld] = s;
                    // REVIEW: Is there a way to get the JIT to inline this, other than move this entire function
                    // into every subclass?
                    dst[slot] = Conv(s);
                }
            }

            protected void MapValuesCore(ReadOnly.Array<TSrc> src, in SlotInfo slinSrc, TDst[] dst, in SlotInfo slinDst)
            {
                Validation.Assert(slinSrc.Count == slinDst.Count);

                for (int i = 0; i < slinDst.Count; i++)
                {
                    int slotSrc = slinSrc.GetSlot(i);
                    int slotDst = slinDst.GetSlot(i);
                    Validation.AssertIndex(slotSrc, src.Length);
                    Validation.AssertIndex(slotDst, dst.Length);
                    TSrc s = src[slotSrc];
                    // REVIEW: Is there a way to get the JIT to inline this, other than move this function
                    // into every subclass?
                    dst[slotDst] = Conv(s);
                }
            }

            protected void MapValuesCore(IEnumerable<TSrc> src, TDst[] dst, in SlotInfo slinDst)
            {
                int i = 0;
                foreach (var item in src)
                {
                    int slotDst = slinDst.GetSlot(i++);
                    Validation.AssertIndex(slotDst, dst.Length);
                    dst[slotDst] = Conv(item);
                }
                Validation.Assert(i == slinDst.Count);
            }

            // REVIEW: Perhaps this should also have Unconv to convert back, as well as a property
            // indicating whether Conv is lossless. For example, u2 to i8 is lossless, so it is safe to
            // Unconv back, while u4 to r4 is lossy, so Unconv wouldn't be safe. This could save allocating
            // some of the arrays for undo (for Paste).
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected abstract TDst Conv(TSrc src);
        }

        /// <summary>
        /// Implements "conversion" between compatible class types.
        /// </summary>
        private sealed class ConverterClsType<TSrc, TDst> : Converter<TSrc, TDst>
            where TSrc : TDst
            where TDst : class
        {
            public override void MapValuesFlagged(IEnumerable src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                throw Validation.BugExcept("Shouldn't be called");
            }

            protected override TDst Conv(TSrc src)
            {
                return src;
            }
        }

        /// <summary>
        /// Implements "conversion" between compatible value types.
        /// </summary>
        private abstract class ConverterValType<TSrc, TDst> : Converter<TSrc, TDst>
            where TSrc : struct
            where TDst : struct
        {
            public sealed override void MapValuesFlagged(IEnumerable src, Array dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(src.TryCast<TSrc?>(out var srcT), nameof(src));
                Validation.BugCheckParam(dst.TryCast<TDst>(out var dstT), nameof(dst));
                Validation.Check(!flinDst.IsBlank);
                MapValuesCore(srcT, dstT, in slinDst, in flinDst);
            }

            protected void MapValuesCore(IEnumerable<TSrc?> src, TDst[] dst, in SlotInfo slinDst, in FlagInfoWrt flinDst)
            {
                Validation.Assert(!flinDst.IsBlank);

                int i = 0;
                foreach (var item in src)
                {
                    int slotDst = slinDst.GetSlot(i++);
                    Validation.AssertIndex(slotDst, dst.Length);
                    if (item == null)
                    {
                        dst[slotDst] = default;
                        flinDst.Clear(slotDst);
                    }
                    else
                    {
                        dst[slotDst] = Conv(item.Value);
                        flinDst.Set(slotDst);
                    }
                }
                Validation.Assert(i == slinDst.Count);
            }
        }

        private sealed class ConvR4ToR8 : ConverterValType<float, double> { protected override double Conv(float src) => src; }
        private sealed class ConvIAToR8 : ConverterValType<Integer, double> { protected override double Conv(Integer src) => src.ToR8(); }
        private sealed class ConvI8ToR8 : ConverterValType<long, double> { protected override double Conv(long src) => src; }
        private sealed class ConvI4ToR8 : ConverterValType<int, double> { protected override double Conv(int src) => src; }
        private sealed class ConvI2ToR8 : ConverterValType<short, double> { protected override double Conv(short src) => src; }
        private sealed class ConvI1ToR8 : ConverterValType<sbyte, double> { protected override double Conv(sbyte src) => src; }
        private sealed class ConvU8ToR8 : ConverterValType<ulong, double> { protected override double Conv(ulong src) => src.ToR8(); }
        private sealed class ConvU4ToR8 : ConverterValType<uint, double> { protected override double Conv(uint src) => src; }
        private sealed class ConvU2ToR8 : ConverterValType<ushort, double> { protected override double Conv(ushort src) => src; }
        private sealed class ConvU1ToR8 : ConverterValType<byte, double> { protected override double Conv(byte src) => src; }
        private sealed class ConvBToR8 : ConverterValType<bool, double> { protected override double Conv(bool src) => src.ToNum(); }

        private sealed class ConvIAToR4 : ConverterValType<Integer, float> { protected override float Conv(Integer src) => src.ToR4(); }
        private sealed class ConvI8ToR4 : ConverterValType<long, float> { protected override float Conv(long src) => src; }
        private sealed class ConvI4ToR4 : ConverterValType<int, float> { protected override float Conv(int src) => src; }
        private sealed class ConvI2ToR4 : ConverterValType<short, float> { protected override float Conv(short src) => src; }
        private sealed class ConvI1ToR4 : ConverterValType<sbyte, float> { protected override float Conv(sbyte src) => src; }
        private sealed class ConvU8ToR4 : ConverterValType<ulong, float> { protected override float Conv(ulong src) => src.ToR4(); }
        private sealed class ConvU4ToR4 : ConverterValType<uint, float> { protected override float Conv(uint src) => src; }
        private sealed class ConvU2ToR4 : ConverterValType<ushort, float> { protected override float Conv(ushort src) => src; }
        private sealed class ConvU1ToR4 : ConverterValType<byte, float> { protected override float Conv(byte src) => src; }
        private sealed class ConvBToR4 : ConverterValType<bool, float> { protected override float Conv(bool src) => src.ToNum(); }

        private sealed class ConvI8ToIA : ConverterValType<long, Integer> { protected override Integer Conv(long src) => src; }
        private sealed class ConvI4ToIA : ConverterValType<int, Integer> { protected override Integer Conv(int src) => src; }
        private sealed class ConvI2ToIA : ConverterValType<short, Integer> { protected override Integer Conv(short src) => src; }
        private sealed class ConvI1ToIA : ConverterValType<sbyte, Integer> { protected override Integer Conv(sbyte src) => src; }
        private sealed class ConvU8ToIA : ConverterValType<ulong, Integer> { protected override Integer Conv(ulong src) => src; }
        private sealed class ConvU4ToIA : ConverterValType<uint, Integer> { protected override Integer Conv(uint src) => src; }
        private sealed class ConvU2ToIA : ConverterValType<ushort, Integer> { protected override Integer Conv(ushort src) => src; }
        private sealed class ConvU1ToIA : ConverterValType<byte, Integer> { protected override Integer Conv(byte src) => src; }
        private sealed class ConvBToIA : ConverterValType<bool, Integer> { protected override Integer Conv(bool src) => src.ToNum(); }

        private sealed class ConvI4ToI8 : ConverterValType<int, long> { protected override long Conv(int src) => src; }
        private sealed class ConvI2ToI8 : ConverterValType<short, long> { protected override long Conv(short src) => src; }
        private sealed class ConvI1ToI8 : ConverterValType<sbyte, long> { protected override long Conv(sbyte src) => src; }
        private sealed class ConvU8ToI8 : ConverterValType<ulong, long> { protected override long Conv(ulong src) => (long)src; }
        private sealed class ConvU4ToI8 : ConverterValType<uint, long> { protected override long Conv(uint src) => src; }
        private sealed class ConvU2ToI8 : ConverterValType<ushort, long> { protected override long Conv(ushort src) => src; }
        private sealed class ConvU1ToI8 : ConverterValType<byte, long> { protected override long Conv(byte src) => src; }
        private sealed class ConvBToI8 : ConverterValType<bool, long> { protected override long Conv(bool src) => src.ToNum(); }

        private sealed class ConvI2ToI4 : ConverterValType<short, int> { protected override int Conv(short src) => src; }
        private sealed class ConvI1ToI4 : ConverterValType<sbyte, int> { protected override int Conv(sbyte src) => src; }
        private sealed class ConvU2ToI4 : ConverterValType<ushort, int> { protected override int Conv(ushort src) => src; }
        private sealed class ConvU1ToI4 : ConverterValType<byte, int> { protected override int Conv(byte src) => src; }
        private sealed class ConvBToI4 : ConverterValType<bool, int> { protected override int Conv(bool src) => src.ToNum(); }

        private sealed class ConvI1ToI2 : ConverterValType<sbyte, short> { protected override short Conv(sbyte src) => src; }
        private sealed class ConvU1ToI2 : ConverterValType<byte, short> { protected override short Conv(byte src) => src; }
        private sealed class ConvBToI2 : ConverterValType<bool, short> { protected override short Conv(bool src) => src.ToNum(); }

        private sealed class ConvBToI1 : ConverterValType<bool, sbyte> { protected override sbyte Conv(bool src) => (sbyte)src.ToNum(); }

        private sealed class ConvU4ToU8 : ConverterValType<uint, ulong> { protected override ulong Conv(uint src) => src; }
        private sealed class ConvU2ToU8 : ConverterValType<ushort, ulong> { protected override ulong Conv(ushort src) => src; }
        private sealed class ConvU1ToU8 : ConverterValType<byte, ulong> { protected override ulong Conv(byte src) => src; }
        private sealed class ConvBToU8 : ConverterValType<bool, ulong> { protected override ulong Conv(bool src) => src.ToNum(); }

        private sealed class ConvU2ToU4 : ConverterValType<ushort, uint> { protected override uint Conv(ushort src) => src; }
        private sealed class ConvU1ToU4 : ConverterValType<byte, uint> { protected override uint Conv(byte src) => src; }
        private sealed class ConvBToU4 : ConverterValType<bool, uint> { protected override uint Conv(bool src) => src.ToNum(); }

        private sealed class ConvU1ToU2 : ConverterValType<byte, ushort> { protected override ushort Conv(byte src) => src; }
        private sealed class ConvBToU2 : ConverterValType<bool, ushort> { protected override ushort Conv(bool src) => src.ToNum(); }

        private sealed class ConvBToU1 : ConverterValType<bool, byte> { protected override byte Conv(bool src) => src.ToNum(); }

        private static readonly ReadOnly.Dictionary<uint, Converter> _convMap = BuildConvMap();

        private static uint GetConvCode(DKind kindSrc, DKind kindDst)
        {
            Validation.BugCheckParam(kindSrc.IsNumeric(), nameof(kindSrc));
            Validation.BugCheckParam(kindDst.IsNumeric(), nameof(kindDst));
            return ((uint)kindSrc << 16) | (uint)kindDst;
        }

        private static Type KindToSt(DKind kind)
        {
            switch (kind)
            {
            case DKind.R8: return typeof(double);
            case DKind.R4: return typeof(float);
            case DKind.IA: return typeof(Integer);
            case DKind.I8: return typeof(long);
            case DKind.I4: return typeof(int);
            case DKind.I2: return typeof(short);
            case DKind.I1: return typeof(sbyte);
            case DKind.U8: return typeof(ulong);
            case DKind.U4: return typeof(uint);
            case DKind.U2: return typeof(ushort);
            case DKind.U1: return typeof(byte);
            case DKind.Bit: return typeof(bool);
            }

            throw Validation.BugExcept();
        }

        private static ReadOnly.Dictionary<uint, Converter> BuildConvMap()
        {
            var map = new Dictionary<uint, Converter>();

            void Add<TSrc, TDst>(DKind kindSrc, DKind kindDst, ConverterValType<TSrc, TDst> conv)
                where TSrc : struct
                where TDst : struct
            {
                Validation.BugCheck(typeof(TSrc) == KindToSt(kindSrc));
                Validation.BugCheck(typeof(TDst) == KindToSt(kindDst));
                var code = GetConvCode(kindSrc, kindDst);
                Validation.Assert(!map.ContainsKey(code));
                map.Add(code, conv);
            }

            Add(DKind.R4, DKind.R8, new ConvR4ToR8());
            Add(DKind.IA, DKind.R8, new ConvIAToR8());
            Add(DKind.I8, DKind.R8, new ConvI8ToR8());
            Add(DKind.I4, DKind.R8, new ConvI4ToR8());
            Add(DKind.I2, DKind.R8, new ConvI2ToR8());
            Add(DKind.I1, DKind.R8, new ConvI1ToR8());
            Add(DKind.U8, DKind.R8, new ConvU8ToR8());
            Add(DKind.U4, DKind.R8, new ConvU4ToR8());
            Add(DKind.U2, DKind.R8, new ConvU2ToR8());
            Add(DKind.U1, DKind.R8, new ConvU1ToR8());
            Add(DKind.Bit, DKind.R8, new ConvBToR8());

            Add(DKind.IA, DKind.R4, new ConvIAToR4());
            Add(DKind.I8, DKind.R4, new ConvI8ToR4());
            Add(DKind.I4, DKind.R4, new ConvI4ToR4());
            Add(DKind.I2, DKind.R4, new ConvI2ToR4());
            Add(DKind.I1, DKind.R4, new ConvI1ToR4());
            Add(DKind.U8, DKind.R4, new ConvU8ToR4());
            Add(DKind.U4, DKind.R4, new ConvU4ToR4());
            Add(DKind.U2, DKind.R4, new ConvU2ToR4());
            Add(DKind.U1, DKind.R4, new ConvU1ToR4());
            Add(DKind.Bit, DKind.R4, new ConvBToR4());

            Add(DKind.I8, DKind.IA, new ConvI8ToIA());
            Add(DKind.I4, DKind.IA, new ConvI4ToIA());
            Add(DKind.I2, DKind.IA, new ConvI2ToIA());
            Add(DKind.I1, DKind.IA, new ConvI1ToIA());
            Add(DKind.U8, DKind.IA, new ConvU8ToIA());
            Add(DKind.U4, DKind.IA, new ConvU4ToIA());
            Add(DKind.U2, DKind.IA, new ConvU2ToIA());
            Add(DKind.U1, DKind.IA, new ConvU1ToIA());
            Add(DKind.Bit, DKind.IA, new ConvBToIA());

            Add(DKind.I4, DKind.I8, new ConvI4ToI8());
            Add(DKind.I2, DKind.I8, new ConvI2ToI8());
            Add(DKind.I1, DKind.I8, new ConvI1ToI8());
            Add(DKind.U8, DKind.I8, new ConvU8ToI8());
            Add(DKind.U4, DKind.I8, new ConvU4ToI8());
            Add(DKind.U2, DKind.I8, new ConvU2ToI8());
            Add(DKind.U1, DKind.I8, new ConvU1ToI8());
            Add(DKind.Bit, DKind.I8, new ConvBToI8());

            Add(DKind.I2, DKind.I4, new ConvI2ToI4());
            Add(DKind.I1, DKind.I4, new ConvI1ToI4());
            Add(DKind.U2, DKind.I4, new ConvU2ToI4());
            Add(DKind.U1, DKind.I4, new ConvU1ToI4());
            Add(DKind.Bit, DKind.I4, new ConvBToI4());

            Add(DKind.I1, DKind.I2, new ConvI1ToI2());
            Add(DKind.U1, DKind.I2, new ConvU1ToI2());
            Add(DKind.Bit, DKind.I2, new ConvBToI2());

            Add(DKind.Bit, DKind.I1, new ConvBToI1());

            Add(DKind.U4, DKind.U8, new ConvU4ToU8());
            Add(DKind.U2, DKind.U8, new ConvU2ToU8());
            Add(DKind.U1, DKind.U8, new ConvU1ToU8());
            Add(DKind.Bit, DKind.U8, new ConvBToU8());

            Add(DKind.U2, DKind.U4, new ConvU2ToU4());
            Add(DKind.U1, DKind.U4, new ConvU1ToU4());
            Add(DKind.Bit, DKind.U4, new ConvBToU4());

            Add(DKind.U1, DKind.U2, new ConvU1ToU2());
            Add(DKind.Bit, DKind.U2, new ConvBToU2());

            Add(DKind.Bit, DKind.U1, new ConvBToU1());

            return map;
        }
    }
}
