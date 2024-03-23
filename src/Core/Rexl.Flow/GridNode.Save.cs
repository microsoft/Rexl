// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Flow;

using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

partial class DocumentBase
{
    partial class GridConfigImpl
    {
        // For saving and loading, in particular serializing text.
        private static readonly Encoding _enc = Util.StdUTF8;

        private const uint MagicBeg = 0x64315247; // GR1d - note the 1 instead of I and the lower d.
        private const uint MagicEnd = 0x47523164; // d1RG

        // Version history:
        // 0x0001: initial.
        // 0x0002: support for non-primitive column types.
        // 0x0003: embed type manager serialization header.

        // The current version.
        private const ushort VerCur = 0x0003;
        // Readers back to this can still read this version.
        private const ushort VerBack = 0x0003;
        // This code can read back to this version.
        private const ushort VerRead = 0x0001;

        private const ushort VerHasTmHeader = 0x0003;

        /// <summary>
        /// Marks the beginning of column data.
        /// </summary>
        private const byte _codeColBeg = 0xC0;

        /// <summary>
        /// Marks the end of column data.
        /// </summary>
        private const byte _codeColEnd = 0xC1;

        /// <summary>
        /// Indicates a null value.
        /// </summary>
        private const byte _codeNull = 0xD0;

        /// <summary>
        /// Indicates a serialized string - followed by the utf8 encoding.
        /// </summary>
        private const byte _codeStr = 0xD1;

        /// <summary>
        /// Indicates a reference to a previously serialized string - followed by an int row index.
        /// </summary>
        private const byte _codeStrRef = 0xD2;

        /// <summary>
        /// Indicates a serialized special object.
        /// </summary>
        private const byte _codeSpec = 0xD3;

        /// <summary>
        /// Indicates a reference to a previously serialized special object - followed by an int row index.
        /// </summary>
        private const byte _codeSpecRef = 0xD4;

        // Start and end of big integer.
        private const byte _codeIntHead = 0xE0;
        private const byte _codeIntTail = 0xE1;

        internal static NodeConfig LoadConfig(TypeManager tm, Stream strm, Guid version)
        {
            return Load(tm, strm, version);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception ThrowRead()
        {
            throw new InvalidDataException("Bad data stream");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckRead(bool cond)
        {
            if (!cond)
                throw ThrowRead();
        }

        private static void WritePath(BinaryWriter wrt, NPath path)
        {
            wrt.Write(path.NameCount);
            while (!path.IsRoot)
            {
                path = path.PopOne(out var name);
                wrt.Write(name.Value);
            }
        }

        private static NPath ReadPath(BinaryReader rdr)
        {
            int count = rdr.ReadInt32();
            CheckRead(count >= 0);
            var path = NPath.Root;
            while (--count >= 0)
            {
                var name = rdr.ReadString();
                CheckRead(DName.TryWrap(name, out var dn));
                path = path.Append(dn);
            }
            return path;
        }

        public override void Save(Stream strm)
        {
            Validation.BugCheckValue(strm, nameof(strm));

            using var wrt = new BinaryWriter(strm, _enc, leaveOpen: true);
            wrt.Write(MagicBeg);
            wrt.Write(VerCur);
            wrt.Write(VerBack);

            wrt.Write(_ccol);
            wrt.Write(_crow);

            // Create the value writer and write its header information.
            Validation.Assert(VerCur >= VerHasTmHeader);
            using var vw = _tm.CreateValueWriter(wrt);

            // Write the column type information and name.
            for (int col = 0; col < _ccol; col++)
            {
                var tin = _colToTin[col];
                wrt.Write(tin.KindCode);
                if (tin.Type.IsUri)
                    WritePath(wrt, tin.Type.GetRootUriFlavor());
                else if (!tin.Type.IsPrimitiveXxx)
                    wrt.Write(tin.Type.Serialize(compact: true));
                wrt.Write(_colToName[col].Value);
            }

            // Write each column.
            var slin = GetSlotInfo(0, _crow);
            for (int col = 0; col < _ccol; col++)
            {
                wrt.Write(_codeColBeg);
                wrt.Write(col);
                _colToTin[col].WriteValues(vw, _values[col], in slin, GetFlagInfoRdo(col));
                wrt.Write(_codeColEnd);
            }

            wrt.Write(MagicEnd);
        }

        public static GridConfigImpl Load(TypeManager tm, Stream strm, Guid version)
        {
            Validation.AssertValue(tm);
            return new GridConfigImpl(tm, strm, version);
        }

        private GridConfigImpl(TypeManager tm, Stream strm, Guid version)
            : base(version)
        {
            Validation.AssertValue(tm);
            Validation.BugCheckValue(strm, nameof(strm));
            _tm = tm;
            using var rdr = new BinaryReader(strm, _enc, leaveOpen: true);
            // Verify magic sig and version information.
            var mag = rdr.ReadUInt32();
            CheckRead(mag == MagicBeg);
            var ver = rdr.ReadUInt16();
            CheckRead(ver >= VerRead);
            var verBack = rdr.ReadUInt16();
            CheckRead(verBack <= VerCur);

            _ccol = rdr.ReadInt32();
            _crow = rdr.ReadInt32();
            CheckRead(_ccol >= 0);
            CheckRead(_crow >= 0);

            // Create the value reader and read its header information (if the version wrote it).
            using var vr = _tm.CreateValueReader(rdr, ver >= VerHasTmHeader);

            int capacity = Math.Max(10, _crow);
            _rowToSlot = new int[capacity];
            for (int row = 0; row < _crow; row++)
                _rowToSlot[row] = row;
            _slotLim = _crow;
            _slotsFree = new IntHeap();

            _nameToCol = new Dictionary<DName, int>();
            _values = new Array[_ccol];
            _valFlags = Array.Empty<Flag[]>();

            var bldrNames = Immutable.Array.CreateBuilder<DName>(_ccol, init: true);
            var bldrTins = Immutable.Array.CreateBuilder<TypeInfo>(_ccol, init: true);
            var bldrFlag = Immutable.Array.CreateBuilder<(int grp, Flag mask)>(_ccol, init: true);

            // Read the column information and build the type.
            int grpCur = -1;
            Flag maskCur = 0;
            var typeRec = DType.EmptyRecordReq;
            for (int col = 0; col < _ccol; col++)
            {
                // Get and validate column type.
                byte code = rdr.ReadByte();

                CheckRead(TypeInfo.TryGetKindFromCode(code, out var kind, out var opt));

                DType typeCol;
                if (kind.IsPrimitive())
                    typeCol = DType.GetPrimitiveType(kind, opt);
                else if (kind == DKind.Uri)
                {
                    var flavor = ReadPath(rdr);
                    typeCol = DType.CreateUriType(flavor);
                }
                else
                {
                    CheckRead((code & ~TypeInfo.OptFlag) == TypeInfo.SpecCode);
                    var strType = rdr.ReadString();
                    CheckRead(DType.TryDeserialize(strType, out typeCol));
                }

                CheckRead(TryGetTypeInfo(typeCol, out var tin, _tm));
                CheckRead(tin.KindCode == code);

                bldrTins[col] = tin;
                _values[col] = tin.CreatePriArray(capacity);
                if (tin.NeedFlag)
                {
                    maskCur = (Flag)((uint)maskCur << 1);
                    if (maskCur == 0)
                    {
                        maskCur = Flag.F0;
                        grpCur++;
                        Validation.Assert(_valFlags.Length == grpCur);
                        Array.Resize(ref _valFlags, grpCur + 1);
                        Validation.Assert(_valFlags[grpCur] == null);
                        _valFlags[grpCur] = new Flag[capacity];
                    }
                    bldrFlag[col] = (grpCur, maskCur);
                }

                // Get and validate the name.
                var str = rdr.ReadString();
                CheckRead(DName.TryWrap(str, out var name));
                CheckRead(!_nameToCol.ContainsKey(name));
                bldrNames[col] = name;
                _nameToCol.Add(name, col);

                Validation.Assert(!typeRec.Contains(name));
                typeRec = typeRec.AddNameType(name, typeCol);
            }

            _typeRec = typeRec;
            _colToName = bldrNames.ToImmutable();
            _colToTin = bldrTins.ToImmutable();
            _colToFlagInfo = bldrFlag.ToImmutable();
            _numAlloced = capacity;

            // Should be valid but "blank" at this point.
            AssertValid(true);

            // Read the column data.
            for (int col = 0; col < _ccol; col++)
            {
                byte code = rdr.ReadByte();
                CheckRead(code == _codeColBeg);
                int tmp = rdr.ReadInt32();
                CheckRead(tmp == col);
                _colToTin[col].ReadValues(vr, _values[col], _crow, GetFlagInfoWrt(col));
                code = rdr.ReadByte();
                CheckRead(code == _codeColEnd);
            }

            AssertValid(true);

            mag = rdr.ReadUInt32();
            CheckRead(mag == MagicEnd);
        }

        private abstract partial class TypeInfo
        {
            public const byte OptFlag = 0x80;
            public const byte SpecCode = 0x70;

            public byte KindCode { get; }

            /// <summary>
            /// Write the indicated values to the <see cref="BinaryWriter"/>.
            /// </summary>
            public abstract void WriteValues(TypeManager.ValueWriter wrt, ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc);

            /// <summary>
            /// Read the values from the <see cref="BinaryReader"/>.
            /// </summary>
            public abstract void ReadValues(TypeManager.ValueReader rdr, Array dst, int crowDst, in FlagInfoWrt flinDst);

            #region Core Write and Read functionality.

            protected sealed class Writers
            {
                public static readonly Writers Instance = new Writers();

                public readonly Action<TypeManager.ValueWriter, double> DoR8;
                public readonly Action<TypeManager.ValueWriter, float> DoR4;
                public readonly Action<TypeManager.ValueWriter, Integer> DoBI;
                public readonly Action<TypeManager.ValueWriter, long> DoI8;
                public readonly Action<TypeManager.ValueWriter, int> DoI4;
                public readonly Action<TypeManager.ValueWriter, short> DoI2;
                public readonly Action<TypeManager.ValueWriter, sbyte> DoI1;
                public readonly Action<TypeManager.ValueWriter, ulong> DoU8;
                public readonly Action<TypeManager.ValueWriter, uint> DoU4;
                public readonly Action<TypeManager.ValueWriter, ushort> DoU2;
                public readonly Action<TypeManager.ValueWriter, byte> DoU1;
                public readonly Action<TypeManager.ValueWriter, bool> DoBool;
                public readonly Action<TypeManager.ValueWriter, Date> DoDT;
                public readonly Action<TypeManager.ValueWriter, Time> DoTS;
                public readonly Action<TypeManager.ValueWriter, Guid> DoG;

                private readonly ReadOnly.Dictionary<Type, Delegate> _writerMap;

                private Writers()
                {
                    var writerMap = new Dictionary<Type, Delegate>();
                    writerMap.Add(typeof(double), DoR8 = Write);
                    writerMap.Add(typeof(float), DoR4 = Write);
                    writerMap.Add(typeof(Integer), DoBI = Write);
                    writerMap.Add(typeof(long), DoI8 = Write);
                    writerMap.Add(typeof(int), DoI4 = Write);
                    writerMap.Add(typeof(short), DoI2 = Write);
                    writerMap.Add(typeof(sbyte), DoI1 = Write);
                    writerMap.Add(typeof(ulong), DoU8 = Write);
                    writerMap.Add(typeof(uint), DoU4 = Write);
                    writerMap.Add(typeof(ushort), DoU2 = Write);
                    writerMap.Add(typeof(byte), DoU1 = Write);
                    writerMap.Add(typeof(bool), DoBool = Write);
                    writerMap.Add(typeof(Date), DoDT = Write);
                    writerMap.Add(typeof(Time), DoTS = Write);
                    writerMap.Add(typeof(Guid), DoG = Write);
                    _writerMap = writerMap;
                }

                public Action<TypeManager.ValueWriter, T> GetWriter<T>()
                {
                    Validation.Assert(_writerMap.ContainsKey(typeof(T)));
                    Validation.Assert(_writerMap[typeof(T)] is Action<TypeManager.ValueWriter, T>);
                    return (Action<TypeManager.ValueWriter, T>)_writerMap[typeof(T)];
                }

                public void Write(TypeManager.ValueWriter wrt, double value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, float value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter vw, Integer value)
                {
                    Validation.AssertValue(vw);
                    var data = value.ToByteArray();
                    Validation.Assert(data.Length > 0);

                    var wrt = vw.Writer;
                    wrt.Write(_codeIntHead);
                    wrt.Write(data.Length);
                    wrt.Write(data, 0, data.Length);
                    wrt.Write(_codeIntTail);
                }
                public void Write(TypeManager.ValueWriter wrt, long value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, int value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, short value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, sbyte value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, ulong value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, uint value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, ushort value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, byte value) => wrt.Writer.Write(value);
                public void Write(TypeManager.ValueWriter wrt, bool value) => wrt.Writer.Write(value ? (byte)1 : (byte)0);
                public void Write(TypeManager.ValueWriter wrt, Date value) => wrt.Writer.Write(value.Ticks);
                public void Write(TypeManager.ValueWriter wrt, Time value) => wrt.Writer.Write(value.Ticks);
                public void Write(TypeManager.ValueWriter vw, Guid value)
                {
                    var pair = value.ToBits();
                    var wrt = vw.Writer;
                    wrt.Write(pair.Item1);
                    wrt.Write(pair.Item2);
                }
            }

            protected sealed class Readers
            {
                public static readonly Readers Instance = new Readers();

                public readonly Func<TypeManager.ValueReader, double> DoR8;
                public readonly Func<TypeManager.ValueReader, float> DoR4;
                public readonly Func<TypeManager.ValueReader, Integer> DoBI;
                public readonly Func<TypeManager.ValueReader, long> DoI8;
                public readonly Func<TypeManager.ValueReader, int> DoI4;
                public readonly Func<TypeManager.ValueReader, short> DoI2;
                public readonly Func<TypeManager.ValueReader, sbyte> DoI1;
                public readonly Func<TypeManager.ValueReader, ulong> DoU8;
                public readonly Func<TypeManager.ValueReader, uint> DoU4;
                public readonly Func<TypeManager.ValueReader, ushort> DoU2;
                public readonly Func<TypeManager.ValueReader, byte> DoU1;
                public readonly Func<TypeManager.ValueReader, bool> DoBool;
                public readonly Func<TypeManager.ValueReader, Date> DoDT;
                public readonly Func<TypeManager.ValueReader, Time> DoTS;
                public readonly Func<TypeManager.ValueReader, Guid> DoG;

                private readonly ReadOnly.Dictionary<Type, Delegate> _readerMap;

                private Readers()
                {
                    var readerMap = new Dictionary<Type, Delegate>();
                    readerMap.Add(typeof(double), DoR8 = ReadR8);
                    readerMap.Add(typeof(float), DoR4 = ReadR4);
                    readerMap.Add(typeof(Integer), DoBI = ReadBI);
                    readerMap.Add(typeof(long), DoI8 = ReadI8);
                    readerMap.Add(typeof(int), DoI4 = ReadI4);
                    readerMap.Add(typeof(short), DoI2 = ReadI2);
                    readerMap.Add(typeof(sbyte), DoI1 = ReadI1);
                    readerMap.Add(typeof(ulong), DoU8 = ReadU8);
                    readerMap.Add(typeof(uint), DoU4 = ReadU4);
                    readerMap.Add(typeof(ushort), DoU2 = ReadU2);
                    readerMap.Add(typeof(byte), DoU1 = ReadU1);
                    readerMap.Add(typeof(bool), DoBool = ReadBool);
                    readerMap.Add(typeof(Date), DoDT = ReadDT);
                    readerMap.Add(typeof(Time), DoTS = ReadTS);
                    readerMap.Add(typeof(Guid), DoG = ReadG);
                    _readerMap = readerMap;
                }

                public Func<TypeManager.ValueReader, T> GetReader<T>()
                {
                    Validation.Assert(_readerMap.ContainsKey(typeof(T)));
                    Validation.Assert(_readerMap[typeof(T)] is Func<TypeManager.ValueReader, T>);
                    return (Func<TypeManager.ValueReader, T>)_readerMap[typeof(T)];
                }

                public double ReadR8(TypeManager.ValueReader rdr) => rdr.Reader.ReadDouble();
                public float ReadR4(TypeManager.ValueReader rdr) => rdr.Reader.ReadSingle();
                public Integer ReadBI(TypeManager.ValueReader vr)
                {
                    Validation.AssertValue(vr);
                    var rdr = vr.Reader;
                    byte code = rdr.ReadByte();
                    CheckRead(code == _codeIntHead);
                    int cb = rdr.ReadInt32();
                    CheckRead(cb > 0);
                    var data = rdr.ReadBytes(cb);
                    code = rdr.ReadByte();
                    CheckRead(code == _codeIntTail);
                    return new Integer(data);
                }
                public long ReadI8(TypeManager.ValueReader rdr) => rdr.Reader.ReadInt64();
                public int ReadI4(TypeManager.ValueReader rdr) => rdr.Reader.ReadInt32();
                public short ReadI2(TypeManager.ValueReader rdr) => rdr.Reader.ReadInt16();
                public sbyte ReadI1(TypeManager.ValueReader rdr) => rdr.Reader.ReadSByte();
                public ulong ReadU8(TypeManager.ValueReader rdr) => rdr.Reader.ReadUInt64();
                public uint ReadU4(TypeManager.ValueReader rdr) => rdr.Reader.ReadUInt32();
                public ushort ReadU2(TypeManager.ValueReader rdr) => rdr.Reader.ReadUInt16();
                public byte ReadU1(TypeManager.ValueReader rdr) => rdr.Reader.ReadByte();

                public bool ReadBool(TypeManager.ValueReader rdr)
                {
                    byte v = rdr.Reader.ReadByte();
                    CheckRead(v <= 1);
                    return v > 0;
                }

                public Date ReadDT(TypeManager.ValueReader rdr)
                {
                    long ticks = rdr.Reader.ReadInt64();
                    CheckRead((ulong)ticks <= (ulong)ChronoUtil.TicksMax);
                    return Date.FromTicks(ticks);
                }

                public Time ReadTS(TypeManager.ValueReader rdr) => new Time(rdr.Reader.ReadInt64());

                public Guid ReadG(TypeManager.ValueReader vr)
                {
                    Validation.AssertValue(vr);
                    var rdr = vr.Reader;
                    ulong bits0 = rdr.ReadUInt64();
                    ulong bits1 = rdr.ReadUInt64();
                    return NumUtil.ToGuid(bits0, bits1);
                }
            }

            #endregion Core Write and Read functionality.
        }

        private abstract partial class TypeInfoSame<T> : TypeInfoSansFlag<T, T>
        {
            public sealed override void WriteValues(TypeManager.ValueWriter wrt, ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.Assert(flinSrc.IsBlank);
                wrt.Writer.Write(KindCode);
                wrt.Writer.Write(slinSrc.Count);
                if (slinSrc.Count > 0)
                    WriteValuesCore(wrt, srcT, in slinSrc);
            }

            protected abstract void WriteValuesCore(TypeManager.ValueWriter wrt, ReadOnly.Array<T> src, in SlotInfo slinSrc);

            public sealed override void ReadValues(TypeManager.ValueReader rdr, Array dst, int crowDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.AssertIndexInclusive(crowDst, dstT.Length);
                Validation.Assert(flinDst.IsBlank);
                var kind = rdr.Reader.ReadByte();
                CheckRead(kind == KindCode);
                int tmp = rdr.Reader.ReadInt32();
                CheckRead(tmp == crowDst);
                if (crowDst > 0)
                    ReadValuesCore(rdr, dstT, crowDst);
            }

            protected abstract void ReadValuesCore(TypeManager.ValueReader rdr, T[] dst, int crowDst);
        }

        private sealed partial class TypeInfoText : TypeInfoSame<string>
        {
            protected override void WriteValuesCore(TypeManager.ValueWriter vw, ReadOnly.Array<string> src, in SlotInfo slinSrc)
            {
                var wrt = vw.Writer;

                var refMap = new Dictionary<string, int>();
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    var value = slinSrc.GetValue(src, i);
                    if (value == null)
                        wrt.Write(_codeNull);
                    else if (refMap.TryGetValue(value, out int rowSrc))
                    {
                        wrt.Write(_codeStrRef);
                        wrt.Write(rowSrc);
                    }
                    else
                    {
                        wrt.Write(_codeStr);
                        wrt.Write(value);
                        refMap.Add(value, i);
                    }
                }
            }

            protected override void ReadValuesCore(TypeManager.ValueReader rdr, string[] dst, int crow)
            {
                Validation.AssertIndex(crow - 1, dst.Length);

                for (int row = 0; row < crow; row++)
                {
                    // This code assumes the array is blank.
                    Validation.Assert(dst[row] == null);

                    byte code = rdr.Reader.ReadByte();
                    switch (code)
                    {
                    default:
                        CheckRead(code == _codeNull);
                        break;
                    case _codeStrRef:
                        int rowSrc = rdr.Reader.ReadInt32();
                        CheckRead(Validation.IsValidIndex(rowSrc, row));
                        dst[row] = dst[rowSrc];
                        break;
                    case _codeStr:
                        dst[row] = rdr.Reader.ReadString();
                        break;
                    }
                }
            }
        }

        private sealed partial class TypeInfoCls<T> : TypeInfoSame<T>
            where T : class
        {
            protected override void WriteValuesCore(TypeManager.ValueWriter vw, ReadOnly.Array<T> src, in SlotInfo slinSrc)
            {
                var wrt = vw.Writer;

                var refMap = new Dictionary<T, int>();
                for (int i = 0; i < slinSrc.Count; i++)
                {
                    var value = slinSrc.GetValue(src, i);
                    if (value == null)
                        wrt.Write(_codeNull);
                    else if (refMap.TryGetValue(value, out int rowSrc))
                    {
                        wrt.Write(_codeSpecRef);
                        wrt.Write(rowSrc);
                    }
                    else
                    {
                        wrt.Write(_codeSpec);
                        _fnWrite(vw, value);
                        refMap.Add(value, i);
                    }
                }
            }

            protected override void ReadValuesCore(TypeManager.ValueReader vr, T[] dst, int crow)
            {
                Validation.AssertIndex(crow - 1, dst.Length);

                var rdr = vr.Reader;
                for (int row = 0; row < crow; row++)
                {
                    // This code assumes the array is blank.
                    Validation.Assert(dst[row] == _defPri);

                    byte code = rdr.ReadByte();
                    switch (code)
                    {
                    default:
                        CheckRead(code == _codeNull);
                        CheckRead(Type.IsOpt);
                        dst[row] = null;
                        break;
                    case _codeSpecRef:
                        int rowSrc = rdr.ReadInt32();
                        CheckRead(Validation.IsValidIndex(rowSrc, row));
                        dst[row] = dst[rowSrc];
                        break;
                    case _codeSpec:
                        dst[row] = _fnRead(vr);
                        break;
                    }
                }
            }
        }

        private sealed partial class TypeInfoReq<T> : TypeInfoSame<T>
            where T : struct, IEquatable<T>
        {
            // REVIEW: This would be significantly more efficient if it didn't depend on these delegates.
            // Perhaps we should have an explicit WriteValues and ReadValues for each element type? Doing so would also
            // allow us to specialize things, for example, bool really only needs 1 bit per value, rather than a full byte.
            private readonly Action<TypeManager.ValueWriter, T> _write = Writers.Instance.GetWriter<T>();
            private readonly Func<TypeManager.ValueReader, T> _read = Readers.Instance.GetReader<T>();

            protected override void WriteValuesCore(TypeManager.ValueWriter wrt, ReadOnly.Array<T> src, in SlotInfo slinSrc)
            {
                for (int i = 0; i < slinSrc.Count; i++)
                    _write(wrt, slinSrc.GetValue(src, i));
            }

            protected override void ReadValuesCore(TypeManager.ValueReader rdr, T[] dst, int crow)
            {
                Validation.AssertIndex(crow - 1, dst.Length);

                for (int row = 0; row < crow; row++)
                    dst[row] = _read(rdr);
            }
        }

        private sealed partial class TypeInfoFlagOpt<T> : TypeInfoWithFlag<T?, T>
            where T : struct, IEquatable<T>
        {
            private readonly Action<TypeManager.ValueWriter, T> _write = Writers.Instance.GetWriter<T>();
            private readonly Func<TypeManager.ValueReader, T> _read = Readers.Instance.GetReader<T>();

            public override void WriteValues(TypeManager.ValueWriter vw, ReadOnly.Array src, in SlotInfo slinSrc, in FlagInfoRdo flinSrc)
            {
                Validation.BugCheckParam(src.TryCast<T>(out var srcT), nameof(src));
                Validation.Assert(!flinSrc.IsBlank);

                var wrt = vw.Writer;
                wrt.Write(KindCode);
                wrt.Write(slinSrc.Count);

                // Process in batches of 8, so we can use a single bit for null.
                for (int row = 0; row < slinSrc.Count;)
                {
                    int count = Math.Min(8, slinSrc.Count - row);
                    uint nulls = 0;
                    uint cur = 0x1;
                    for (int i = 0; i < count; i++, cur <<= 1)
                    {
                        if (!flinSrc.Test(slinSrc.GetSlot(row + i)))
                            nulls |= cur;
                    }
                    wrt.Write((byte)nulls);
                    cur = 0x1;
                    for (int i = 0; i < count; i++, cur <<= 1)
                    {
                        if ((nulls & cur) == 0)
                            _write(vw, slinSrc.GetValue(srcT, row + i));
                    }
                    row += count;
                }
            }

            public override void ReadValues(TypeManager.ValueReader rdr, Array dst, int crowDst, in FlagInfoWrt flinDst)
            {
                Validation.BugCheckParam(dst.TryCast<T>(out var dstT), nameof(dst));
                Validation.AssertIndexInclusive(crowDst, dstT.Length);
                Validation.Assert(!flinDst.IsBlank);
                Validation.AssertIndexInclusive(crowDst, flinDst.Length);

                var kind = rdr.Reader.ReadByte();
                CheckRead(kind == KindCode);
                int tmp = rdr.Reader.ReadInt32();
                CheckRead(tmp == crowDst);

                // Process in batches of 8, so we can use a single bit for null.
                for (int row = 0; row < crowDst;)
                {
                    int count = Math.Min(8, crowDst - row);
                    uint nulls = rdr.Reader.ReadByte();
                    uint cur = 0x1;
                    for (int i = 0; i < count; i++, cur <<= 1)
                    {
                        Validation.Assert(!flinDst.Test(row + i));
                        if ((nulls & cur) == 0)
                        {
                            dstT[row + i] = _read(rdr);
                            flinDst.Set(row + i);
                        }
                    }
                    row += count;
                }
            }
        }
    }
}
