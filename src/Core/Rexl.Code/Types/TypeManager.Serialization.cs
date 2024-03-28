// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// To see the serialization generated IL in debug output, define this.
#undef ENABLE_LOGGING

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Rexl.Compression;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

using Conditional = System.Diagnostics.ConditionalAttribute;
using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

// Partial containing serialization code.
public abstract partial class TypeManager
{
    // The serializer instance. Note that it caches the writers and readers.
    private readonly Serializer _ser;

    [Conditional("ENABLE_LOGGING")]
    private static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine(msg);
    }

    [Conditional("ENABLE_LOGGING")]
    private static void Log(string msg, params object[] args)
    {
        System.Diagnostics.Debug.WriteLine(msg, args);
    }

    /// <summary>
    /// Return whether this type manager can read and write values of the given <paramref name="type"/>.
    /// </summary>
    public bool CanReadWrite(DType type)
    {
        return type.IsValid && _ser.CanReadWrite(type);
    }

    /// <summary>
    /// A value reader, which is a wrapper around a <see cref="BinaryReader"/> and version number.
    /// To create one, call <see cref="CreateValueReader(BinaryReader)"/>.
    /// </summary>
    public abstract class ValueReader : IDisposable
    {
        /// <summary>
        /// The version number of the binary format.
        /// </summary>
        public uint Version { get; }

        /// <summary>
        /// The underlying <see cref="BinaryReader"/>.
        /// </summary>
        public BinaryReader Reader { get; }

        private protected ValueReader(uint ver, BinaryReader rdr)
        {
            Validation.AssertValue(rdr);

            Version = ver;
            Reader = rdr;
        }

        public virtual void Dispose() { }
    }

    /// <summary>
    /// Create a value reader around the given <see cref="BinaryReader"/>. When <paramref name="readHeader"/>
    /// is <c>true</c>, this reads and validates header information at the current position. When
    /// <paramref name="readHeader"/> is <c>false</c>, this assumes verion 0x00010004.
    /// </summary>
    public ValueReader CreateValueReader(BinaryReader rdr, bool readHeader = true)
    {
        Validation.BugCheckValue(rdr, nameof(rdr));
        return _ser.CreateSource(rdr, readHeader);
    }

    /// <summary>
    /// A value writer, which is a wrapper around a <see cref="BinaryWriter"/> and version number.
    /// To create one, call <see cref="CreateValueWriter(BinaryWriter)"/>.
    /// </summary>
    public abstract class ValueWriter : IDisposable
    {
        /// <summary>
        /// The version number of the format being written.
        /// </summary>
        public uint Version { get; }

        /// <summary>
        /// The underlying <see cref="BinaryWriter"/>.
        /// </summary>
        public BinaryWriter Writer { get; }

        private protected ValueWriter(uint ver, BinaryWriter wrt)
        {
            Validation.AssertValue(wrt);

            Version = ver;
            Writer = wrt;
        }

        public virtual void Dispose() { }
    }

    /// <summary>
    /// Create a value writer around the given <see cref="BinaryWriter"/>. This writes header information
    /// at the current position.
    /// </summary>
    public ValueWriter CreateValueWriter(BinaryWriter wrt)
    {
        Validation.BugCheckValue(wrt, nameof(wrt));
        return _ser.CreateSink(wrt);
    }

    /// <summary>
    /// Try to get a binary writer action for the indicated type. Note that <typeparamref name="T"/> must
    /// be the system type for the given <paramref name="type"/>.
    /// </summary>
    public bool TryGetWriter<T>(DType type, out Action<ValueWriter, T> fnWrite)
    {
        Validation.BugCheckParam(GetSysTypeOrNull(type) == typeof(T), nameof(type));
        return _ser.TryGetWriter<T>(type, out fnWrite);
    }

    /// <summary>
    /// Try to get a binary reader func for the indicated type. Note that <typeparamref name="T"/> must
    /// be the system type for the given <paramref name="type"/>.
    /// </summary>
    public bool TryGetReader<T>(DType type, out Func<ValueReader, T> fnRead)
    {
        Validation.BugCheckParam(GetSysTypeOrNull(type) == typeof(T), nameof(type));
        return _ser.TryGetReader<T>(type, out fnRead);
    }

    /// <summary>
    /// Write the given <paramref name="value"/> to the given <paramref name="strm"/>. The writing is
    /// driven by the given <paramref name="type"/>, NOT by the runtime type of the <paramref name="value"/>.
    /// Can throw if the <paramref name="value"/> is not compatible with the <paramref name="type"/> or if
    /// there is an IO exception.
    /// 
    /// This method will write a full .rbin encoding of the value, including chunking and compression.
    /// The various settings for this encoding are specified by <paramref name="settings"/>.
    /// 
    /// <paramref name="strm"/> must be seek-able.
    /// 
    /// REVIEW: Add .md file to define the full format.
    /// </summary>
    public bool TryWriteFull(Stream strm, DType type, object value, RbinSettings settings)
    {
        Validation.BugCheckValue(strm, nameof(strm));
        Validation.BugCheckParam(strm.CanSeek, nameof(strm), "Writing rbin requires a seekable stream");
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(TryEnsureSysType(type, out Type st), nameof(type));
        Validation.BugCheckParam(value != null || type.IsOpt, nameof(value));
        Validation.BugCheckParam(value == null || st.IsAssignableFrom(value.GetType()), nameof(value));
        Validation.BugCheckValue(settings, nameof(settings));

        return _ser.TryWrite(strm, type, value, settings);
    }

    /// <summary>
    /// Write the given <paramref name="value"/> to the given <paramref name="strm"/>. The writing is
    /// driven by the given <paramref name="type"/>, NOT by the runtime type of the <paramref name="value"/>.
    /// Can throw if the <paramref name="value"/> is not compatible with the <paramref name="type"/> or if
    /// there is an IO exception.
    /// 
    /// This method will only write the core value serialization, excluding features like chunking and compression.
    /// </summary>
    public bool TryWrite(Stream strm, DType type, object value)
    {
        Validation.BugCheckValue(strm, nameof(strm));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(TryEnsureSysType(type, out Type st), nameof(type));
        Validation.BugCheckParam(value != null || type.IsOpt, nameof(value));
        Validation.BugCheckParam(value == null || st.IsAssignableFrom(value.GetType()), nameof(value));

        return _ser.TryWrite(strm, type, value, null);
    }

    /// <summary>
    /// Read a value of the given <paramref name="type"/> from the given <paramref name="strm"/>. The reading
    /// is driven by the given <paramref name="type"/>, NOT by type information encoded in the serialized form.
    /// Can throw if the <paramref name="type"/> does not match information in the serialized form, or if
    /// there is an IO exception.
    /// </summary>
    public bool TryRead(Stream strm, DType type, out Type st, out object value)
    {
        Validation.BugCheckValue(strm, nameof(strm));
        Validation.BugCheckParam(type.IsValid, nameof(type));

        bool ret = _ser.TryRead(strm, type, out st, out value);
        Validation.Assert(ret == (IsOfType(value, type) != TriState.No));
        return ret;
    }

    /// <summary>
    /// Serializer for rexl values, where sequence is based on IEnumerable or Array.
    /// REVIEW: Sub-classes will need to be able to customize.
    /// </summary>
    private sealed partial class Serializer
    {
        /// <summary>
        /// Control / sanity codes.
        /// </summary>
        private enum Code : byte
        {
            TopHead = 0xA0,
            TopTail = 0xA1,

            NullYes = 0xA2,
            NullNot = 0xA3,

            // REVIEW: These codes add extra bloat but enable potentially useful sanity checks.
            // Should we omit all or some of them?

            StrHead = 0xA4, // Head of string.
            StrTail = 0xA5, // Tail of string.
            IntHead = 0xA6, // Head of BigInteger.
            IntTail = 0xA7, // Tail of BigInteger.
            UriHeadOld = 0xA8, // Head of Uri in old format.
            UriTailOld = 0xA9, // Tail of Uri in old format.
            TypTail = 0xAA, // Tail of DType.
            UriHead = 0xAB, // Head of Uri in new format.
            UriTail = 0xAC, // Tail of Uri in new format.

            RecNull = 0xB0, // Null record.
            RecHead = 0xB1, // Head of record.
            RecItem = 0xB2, // Item of record (field).
            RecTail = 0xBF, // Tail of record.

            SeqNull = 0xC0, // Null sequence.
            SeqHead = 0xC1, // Head of sequence.
            SeqItem = 0xC2, // Item of sequence. Removed as of 0x00010004.
            SeqCust = 0xC3, // Custom sequence seralization. Removed as of 0x00010004.
            SeqChkd = 0xC4, // Head of chunked sequence.
            SeqTail = 0xCF, // Tail of sequence.

            TupNull = 0xD0, // Null tuple.
            TupHead = 0xD1, // Head of tuple.
            TupItem = 0xD2, // Item of tuple.
            TupTail = 0xDF, // Tail of tuple.

            TensNull = 0xE0, // Null tensor.
            TensHead = 0xE1, // Head of tensor
            TensTail = 0xEF, // Tail of tensor.

            ChkHead = 0xF0, // Head of chunk.
            ChkFoot = 0xF1, // End of chunk data.
            ChkTail = 0xF2, // Tail of chunk.
        }

        /// <summary>
        /// Wraps a BinaryWriter and working buffer for writing.
        /// </summary>
        private sealed class Sink : ValueWriter
        {
            private readonly Stream _strm;
            private readonly long _base;

            public readonly byte[] Buf;
            public readonly bool CanSeek;

            /// <summary>
            /// Whether this sink can get the current Position.
            /// </summary>
            public bool CanGetPos => _base >= 0;

            public long Position => CanGetPos ? _strm.Position - _base : -1;

            public Stream BaseStream => _strm;

            public Sink(BinaryWriter wrt, Stream strm)
                : base(k_verSer, wrt)
            {
                // REVIEW: Note we pass in the stream rather than use wrt.BaseStream since that flushes.
                // This isn't good when using a brotli compression stream. We should really use a custom
                // BinaryWriter subclass to change this behavior.
                // _strm = Writer.BaseStream;
                _strm = strm;

                CanSeek = _strm.CanSeek;

                // Try to get the position. If it throws, we set _base to -1 as a signal that Position
                // isn't available. In this case, we also force CanSeek to false. If a stream can't
                // say where it is, it shouldn't claim to be seekable.
                try { _base = _strm.Position; }
                catch { _base = -1; }

                if (!CanGetPos)
                    CanSeek = false;

                // 9 is the maximum buffer size currently needed. This max is for integer compression.
                Buf = new byte[9];
            }

            public void MoveTo(long pos)
            {
                Validation.Assert(CanSeek);
                Validation.AssertIndexInclusive(pos, _strm.Length - _base);
                _strm.Seek(pos + _base, SeekOrigin.Begin);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteByte(byte v)
            {
                _strm.WriteByte(v);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteU2(ushort v)
            {
                Buf[0] = (byte)v;
                Buf[1] = (byte)(v >> 8);
                _strm.Write(Buf, 0, 2);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteU4Fixed(uint v)
            {
                Buf[0] = (byte)v;
                Buf[1] = (byte)(v >> 8);
                Buf[2] = (byte)(v >> 16);
                Buf[3] = (byte)(v >> 24);
                _strm.Write(Buf, 0, 4);
            }

            // For compressing 4 byte (signed or unsigned) integers we use a scheme similar to that for
            // 8 byte integers (described below), except the maximimum number of bytes is 5 and when
            // 5 bytes are used, there are 4 bits that are always zero.
            public void WriteU4Compressed(uint v)
            {
                byte lead = 0;
                if ((int)v < 0)
                {
                    v = ~v;
                    lead = 0x1;
                    Validation.Assert((int)v >= 0);
                }
                Validation.Assert(v < 0x8000_0000U);

                if (v < (1U << 6))
                {
                    // 1 byte, 8 bits: 1 len, 1 sign, 6 value.
                    lead <<= 6;
                    lead |= (byte)v;
                    _strm.WriteByte(lead);
                    return;
                }

                int cb;

                // Binary search.
                if (v < (1U << 20))
                {
                    // 2 or 3 bytes.
                    if (v < (1U << 13))
                    {
                        // 2 bytes, 16 bits: 2 len, 1 sign, 13 value.
                        lead <<= 5;
                        lead |= 0x80;
                        lead |= (byte)(v >> 8);
                        Buf[0] = lead;
                        Buf[1] = (byte)v;
                        cb = 2;
                    }
                    else
                    {
                        // 3 bytes, 24 bits: 3 len, 1 sign, 20 value.
                        lead <<= 4;
                        lead |= 0xC0;
                        lead |= (byte)(v >> 16);
                        Buf[0] = lead;
                        Buf[1] = (byte)(v >> 8);
                        Buf[2] = (byte)v;
                        cb = 3;
                    }
                }
                else
                {
                    // 4 or 5 bytes.
                    if (v < (1U << 27))
                    {
                        // 4 bytes, 32 bits: 4 len, 1 sign, 27 value.
                        lead <<= 3;
                        lead |= 0xE0;
                        lead |= (byte)(v >> 24);
                        Buf[0] = lead;
                        Buf[1] = (byte)(v >> 16);
                        Buf[2] = (byte)(v >> 8);
                        Buf[3] = (byte)v;
                        cb = 4;
                    }
                    else
                    {
                        // 5 bytes, 40 bits: 5 len, 1 sign, 34 value (only 31 used).
                        lead <<= 2;
                        lead |= 0xF0;
                        Buf[0] = lead;
                        Buf[1] = (byte)(v >> 24);
                        Buf[2] = (byte)(v >> 16);
                        Buf[3] = (byte)(v >> 8);
                        Buf[4] = (byte)v;
                        cb = 5;
                    }
                }

                _strm.Write(Buf, 0, cb);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteU4Data(uint v)
            {
                WriteU4Compressed(v);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteU8Fixed(ulong v)
            {
                Buf[0] = (byte)v;
                Buf[1] = (byte)(v >> 8);
                Buf[2] = (byte)(v >> 16);
                Buf[3] = (byte)(v >> 24);
                Buf[4] = (byte)(v >> 32);
                Buf[5] = (byte)(v >> 40);
                Buf[6] = (byte)(v >> 48);
                Buf[7] = (byte)(v >> 56);
                _strm.Write(Buf, 0, 8);
            }

            // For compressing 8 byte (signed or unsigned) integers we use the following scheme:
            // * For negative values, take the bitwise complement. This ensures that the high bit is zero,
            //   so we need to encode a further 63 bits.
            // * For an encoding using k bytes, start with (k - 1) bits set to one. When k < 9, follow with
            //   an additional zero bit. Eg, when an encoding starts with 0b110x_xxxx, the total number of
            //   bytes in the encoding is three.
            // * The next bit is the sign bit with one indicating that we took the bitwise complement and
            //   zero meaning we didn't.
            // * The remaining bits are data bits with most significant bit first.
            // Ranges (inclusive and in hexadecimal) for each number of bytes:
            // * 1 byte:  from -(1 <<  6) = -                 40 to (1 <<  6) - 1 =                  3F.
            // * 2 bytes: from -(1 << 13) = -               2000 to (1 << 13) - 1 =                1FFF.
            // * 3 bytes: from -(1 << 20) = -            10_0000 to (1 << 20) - 1 =             0F_FFFF.
            // * 4 bytes: from -(1 << 27) = -           800_0000 to (1 << 27) - 1 =            7FF_FFFF.
            // * 5 bytes: from -(1 << 34) = -        4_0000_0000 to (1 << 34) - 1 =         3_FFFF_FFFF.
            // * 6 bytes: from -(1 << 41) = -      200_0000_0000 to (1 << 41) - 1 =       1FF_FFFF_FFFF.
            // * 7 bytes: from -(1 << 48) = -   1_0000_0000_0000 to (1 << 48) - 1 =    0_FFFF_FFFF_FFFF.
            // * 8 bytes: from -(1 << 55) = -  80_0000_0000_0000 to (1 << 55) - 1 =   7F_FFFF_FFFF_FFFF.
            // * 9 bytes: from -(1 << 63) = -8000_0000_0000_0000 to (1 << 63) - 1 = 7FFF_FFFF_FFFF_FFFF.
            // For 9 bytes, we don't have a 0 bit in the ninth position. That bit is instead the sign bit.
            // The extra zero bit is "suppressed" so we don't need a 10th byte.
            // Examples:
            // * A non-negative integer <= 63 is encoded as 0b00xx_xxxx, where the x bits are the value bits.
            // * A negative integer >= -64 has its bitwise complement being a non-negative integer <= 63 and
            //   is encoded as 0b01xx_xxxx, where the x bits are the bits of the bitwise complement value.
            // * A value in the 3 byte range (20 value bits) would be encoded as 0b110s_xxxx_xxxx_xxxx_xxxx_xxxx
            //   where s is the sign value and the x bits are the 20 value bits. As always, if the sign bit
            //   is 1, the encoded value is the bitwise complement of the actual value.
            public void WriteU8Compressed(ulong v)
            {
                byte lead = 0;
                if ((long)v < 0)
                {
                    v = ~v;
                    lead = 0x1;
                    Validation.Assert((long)v >= 0);
                }
                Validation.Assert(v < 0x8000_0000_0000_0000UL);

                if (v < (1UL << 6))
                {
                    // 1 byte, 8 bits: 1 len, 1 sign, 6 value.
                    lead <<= 6;
                    lead |= (byte)v;
                    _strm.WriteByte(lead);
                    return;
                }

                int cb;

                // Binary search.
                if (v < (1UL << 34))
                {
                    // 2 to 5 bytes.
                    if (v < (1UL << 20))
                    {
                        // 2 or 3.
                        if (v < (1UL << 13))
                        {
                            // 2 bytes, 16 bits: 2 len, 1 sign, 13 value.
                            lead <<= 5;
                            lead |= 0x80;
                            lead |= (byte)(v >> 8);
                            Buf[0] = lead;
                            Buf[1] = (byte)v;
                            cb = 2;
                        }
                        else
                        {
                            // 3 bytes, 24 bits: 3 len, 1 sign, 20 value.
                            lead <<= 4;
                            lead |= 0xC0;
                            lead |= (byte)(v >> 16);
                            Buf[0] = lead;
                            Buf[1] = (byte)(v >> 8);
                            Buf[2] = (byte)v;
                            cb = 3;
                        }
                    }
                    else
                    {
                        // 4 or 5.
                        if (v < (1UL << 27))
                        {
                            // 4 bytes, 32 bits: 4 len, 1 sign, 27 value.
                            lead <<= 3;
                            lead |= 0xE0;
                            lead |= (byte)(v >> 24);
                            Buf[0] = lead;
                            Buf[1] = (byte)(v >> 16);
                            Buf[2] = (byte)(v >> 8);
                            Buf[3] = (byte)v;
                            cb = 4;
                        }
                        else
                        {
                            // 5 bytes, 40 bits: 5 len, 1 sign, 34 value.
                            lead <<= 2;
                            lead |= 0xF0;
                            lead |= (byte)(v >> 32);
                            Buf[0] = lead;
                            Buf[1] = (byte)(v >> 24);
                            Buf[2] = (byte)(v >> 16);
                            Buf[3] = (byte)(v >> 8);
                            Buf[4] = (byte)v;
                            cb = 5;
                        }
                    }
                }
                else
                {
                    // 6 to 9 bytes.
                    if (v < (1UL << 48))
                    {
                        // 6 or 7.
                        if (v < (1UL << 41))
                        {
                            // 6 bytes, 48 bits: 6 len, 1 sign, 41 value.
                            lead <<= 1;
                            lead |= 0xF8;
                            lead |= (byte)(v >> 40);
                            Buf[0] = lead;
                            Buf[1] = (byte)(v >> 32);
                            Buf[2] = (byte)(v >> 24);
                            Buf[3] = (byte)(v >> 16);
                            Buf[4] = (byte)(v >> 8);
                            Buf[5] = (byte)v;
                            cb = 6;
                        }
                        else
                        {
                            // 7 bytes, 56 bits: 7 len, 1 sign, 48 value.
                            lead |= 0xFC;
                            Buf[0] = lead;
                            Buf[1] = (byte)(v >> 40);
                            Buf[2] = (byte)(v >> 32);
                            Buf[3] = (byte)(v >> 24);
                            Buf[4] = (byte)(v >> 16);
                            Buf[5] = (byte)(v >> 8);
                            Buf[6] = (byte)v;
                            cb = 7;
                        }
                    }
                    else
                    {
                        // 8 or 9.
                        if (v < (1UL << 55))
                        {
                            // 8 bytes, 64 bits: 8 len, 1 sign, 55 value.
                            Buf[0] = (byte)0xFE;
                            lead <<= 7;
                            lead |= (byte)(v >> 48);
                            Buf[1] = lead;
                            Buf[2] = (byte)(v >> 40);
                            Buf[3] = (byte)(v >> 32);
                            Buf[4] = (byte)(v >> 24);
                            Buf[5] = (byte)(v >> 16);
                            Buf[6] = (byte)(v >> 8);
                            Buf[7] = (byte)v;
                            cb = 8;
                        }
                        else
                        {
                            // 9 bytes, 72 bits: 8 len (all ones), 1 sign, 63 value.
                            Buf[0] = (byte)0xFF;
                            lead <<= 7;
                            lead |= (byte)(v >> 56);
                            Buf[1] = lead;
                            Buf[2] = (byte)(v >> 48);
                            Buf[3] = (byte)(v >> 40);
                            Buf[4] = (byte)(v >> 32);
                            Buf[5] = (byte)(v >> 24);
                            Buf[6] = (byte)(v >> 16);
                            Buf[7] = (byte)(v >> 8);
                            Buf[8] = (byte)v;
                            cb = 9;
                        }
                    }
                }

                // Write the bytes.
                _strm.Write(Buf, 0, cb);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteU8Data(ulong v)
            {
                WriteU8Compressed(v);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteR4(float v)
            {
                WriteU4Fixed(v.ToBits());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteR8(double v)
            {
                WriteU8Fixed(v.ToBits());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(byte[] v, int start, int count)
            {
                Validation.AssertValue(v);
                Validation.AssertIndex(start, v.Length);
                Validation.AssertIndexInclusive(count, v.Length - start);
                _strm.Write(v, start, count);
            }
        }

        /// <summary>
        /// Wraps a <see cref="BinaryReader"/>, version number and working buffer for reading.
        /// </summary>
        private sealed class Source : ValueReader
        {
            private readonly Stream _strm;
            private readonly bool _compressedInts;

            public readonly byte[] Buf;

            public Stream BaseStream => Reader.BaseStream;

            public Source(uint ver, BinaryReader rdr)
                : base(ver, rdr)
            {
                _strm = Reader.BaseStream;

                Buf = new byte[8];

                _compressedInts = ver >= k_verIntCompression;
            }

            private Exception PastEof()
            {
                throw new InvalidDataException("Read past end of stream");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCore(byte[] buf, int start, int count)
            {
                Validation.AssertValue(buf);
                Validation.AssertIndex(start, buf.Length);
                Validation.AssertIndex(count - 1, buf.Length - start);

                // REVIEW: Seems unlikely that this will be fully inlined :-(.
                for (; ; )
                {
                    int cb = _strm.Read(buf, start, count);
                    Validation.AssertIndexInclusive(cb, count);
                    if (cb == count)
                        return;
                    if (cb == 0)
                        throw PastEof();
                    start += cb;
                    count -= cb;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte ReadByte()
            {
                int res = _strm.ReadByte();
                if (res < 0)
                    throw PastEof();
                return (byte)res;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ushort ReadU2()
            {
                ReadCore(Buf, 0, 2);
                return (ushort)(((uint)(Buf[1]) << 8) | Buf[0]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint ReadU4Fixed()
            {
                ReadCore(Buf, 0, 4);
                return ((uint)(Buf[3]) << 24) | ((uint)Buf[2] << 16) | ((uint)(Buf[1]) << 8) | Buf[0];
            }

            public uint ReadU4Compressed()
            {
                byte lead = ReadByte();
                uint res;

                // Test for most common case of 1 byte first.
                if ((sbyte)lead >= 0)
                {
                    // 1 byte.
                    res = lead & 0x3FU;
                    if ((lead & 0x40U) == 0)
                        return res;
                    return ~res;
                }

                bool flip;
                int cb;

                // Binary search.
                if (lead < 0b1110_0000)
                {
                    // 2 or 3 bytes.
                    if (lead < 0b1100_0000)
                    {
                        // 2 bytes total, 1 more.
                        flip = (lead & 0x20U) != 0;
                        res = lead & 0x1FU;
                        cb = 1;
                    }
                    else
                    {
                        // 3 bytes total, 2 more.
                        flip = (lead & 0x10U) != 0;
                        res = lead & 0x0FU;
                        cb = 2;
                    }
                    ReadCore(Buf, 0, cb);
                }
                else
                {
                    // 4 or 5 bytes.
                    if (lead < 0b1111_0000)
                    {
                        // 4 bytes total, 3 more.
                        flip = (lead & 0x08U) != 0;
                        res = lead & 0x07U;
                        ReadCore(Buf, 0, cb = 3);
                    }
                    else
                    {
                        // 5 bytes total, 4 more.
                        // Check that the lengh indicated in lead is indeed for 5 bytes and that
                        // the "data bits" in lead are zero.
                        CheckRead((lead & 0x0B) == 0);
                        flip = (lead & 0x04U) != 0;
                        res = 0;
                        ReadCore(Buf, 0, cb = 4);
                        // The remaining 4 bytes encode only 31 bits, since the sign bit is in "flip".
                        CheckRead(Buf[0] < 0x80U);
                    }
                }

                for (int ib = 0; ib < cb; ib++)
                {
                    Validation.Assert(res < 0x0080_0000U);
                    res = (res << 8) | Buf[ib];
                }
                Validation.Assert(res < 0x8000_0000U);

                if (!flip)
                    return res;
                return ~res;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint ReadU4Data()
            {
                return _compressedInts ? ReadU4Compressed() : ReadU4Fixed();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ulong ReadU8Fixed()
            {
                ReadCore(Buf, 0, 8);
                return
                    ((ulong)(Buf[7]) << 56) | ((ulong)Buf[6] << 48) | ((ulong)(Buf[5]) << 40) | ((ulong)(Buf[4]) << 32) |
                    ((ulong)(Buf[3]) << 24) | ((ulong)Buf[2] << 16) | ((ulong)(Buf[1]) << 8) | Buf[0];
            }

            // REVIEW: Whenever the number of bytes is at least two, there are values that are
            // technically not legal (they should have been encoded in fewer bytes). Is there any point
            // in checking for that invariant?
            public ulong ReadU8Compressed()
            {
                byte lead = ReadByte();
                ulong res;

                // Test for most common case of 1 byte first.
                if ((sbyte)lead >= 0)
                {
                    // 1 byte.
                    res = lead & 0x3FU;
                    if ((lead & 0x40U) == 0)
                        return res;
                    return ~res;
                }

                bool flip;
                int cb;
                int ib;

                // Binary search.
                if (lead < 0b1111_1000)
                {
                    // 2 to 5 bytes.
                    if (lead < 0b1110_0000)
                    {
                        // 2 or 3.
                        if (lead < 0b1100_0000)
                        {
                            // 2 bytes total, 1 more.
                            flip = (lead & 0x20U) != 0;
                            res = lead & 0x1FU;
                            cb = 1;
                        }
                        else
                        {
                            // 3 bytes total, 2 more.
                            flip = (lead & 0x10U) != 0;
                            res = lead & 0x0FU;
                            cb = 2;
                        }
                    }
                    else
                    {
                        // 4 or 5.
                        if (lead < 0b1111_0000)
                        {
                            // 4 bytes total, 3 more.
                            flip = (lead & 0x08U) != 0;
                            res = lead & 0x07U;
                            cb = 3;
                        }
                        else
                        {
                            // 5 bytes total, 4 more.
                            flip = (lead & 0x04U) != 0;
                            res = lead & 0x03U;
                            cb = 4;
                        }
                    }
                    ReadCore(Buf, 0, cb);
                    ib = 0;
                }
                else
                {
                    // 6 to 9 bytes.
                    if (lead < 0b1111_1110)
                    {
                        // 6 or 7.
                        if (lead < 0b1111_1100)
                        {
                            // 6 bytes total, 5 more.
                            flip = (lead & 0x02U) != 0;
                            res = lead & 0x01U;
                            cb = 5;
                        }
                        else
                        {
                            // 7 bytes total, 6 more.
                            flip = (lead & 0x01U) != 0;
                            res = 0;
                            cb = 6;
                        }
                        ReadCore(Buf, 0, cb);
                        ib = 0;
                    }
                    else
                    {
                        // 8 or 9.
                        if (lead < 0b1111_1111)
                        {
                            // 8 bytes total, 7 more.
                            Validation.Assert(lead == 0xFE);
                            cb = 7;
                        }
                        else
                        {
                            // 9 bytes total, 8 more.
                            Validation.Assert(lead == 0xFF);
                            cb = 8;
                        }
                        ReadCore(Buf, 0, cb);
                        lead = Buf[0];
                        flip = (lead & 0x80U) != 0;
                        res = lead & 0x7FU;
                        ib = 1;
                    }
                }

                Validation.AssertIndex(ib, cb);
                for (; ib < cb; ib++)
                {
                    Validation.Assert(res < 0x0080_0000_0000_0000UL);
                    res = (res << 8) | Buf[ib];
                }
                Validation.Assert(res < 0x8000_0000_0000_0000UL);

                if (!flip)
                    return res;
                return ~res;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ulong ReadU8Data()
            {
                return _compressedInts ? ReadU8Compressed() : ReadU8Fixed();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float ReadR4()
            {
                return ReadU4Fixed().ToFloat();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double ReadR8()
            {
                return ReadU8Fixed().ToDouble();
            }
        }

        private readonly TypeManager _tm;
        private readonly Encoding _enc;

        // Caches for writer and reader delegates.
        // Each writer is an Action<Sink, T, TryGetCustom>, where T is the value type.
        // Each reader is a  Func<Source, GetCustomSeq, T>, where T is the value type.
        private readonly ConcurrentDictionary<DType, Delegate> _typeToWriter;
        private readonly ConcurrentDictionary<DType, Delegate> _typeToReader;

        // The "signature" for binary encodings produced by this serializer - "REXL".
        private const uint k_sigSer = 0x4C584552;

        // The "signature" for .rbin file encodings - "RBIN", backwards.
        private const uint k_sigRbin = 0x5242494E;

        // The current version number. This should be changed when the format changes.
        // History:
        // * 0x00010001: initial
        // * 0x00010002: uri/link representation changed.
        // * 0x00010003: sequnce mapping added.
        // * 0x00010004: added extra version numbers, changed seq count to long, removed SeqItem.
        // * 0x00010005: uri serialization improved and moved to base type manager.
        // * 0x00010006: integer compression.
        private const uint k_verSer = 0x00010006;
        // Readers back to this can still read this version.
        private const uint k_verBack = 0x00010006;
        // This code can read back to this version.
        private const uint k_verRead = 0x00010003;

        // Version where the k_verBack/k_verRead mechanism was introduced.
        private const uint k_verWithReadBack = 0x00010004;
        // Version where new uri serialization was introduced.
        private const uint k_verNewUri = 0x00010005;
        // Version where integer compression was introduced.
        private const uint k_verIntCompression = 0x00010006;

        // The current version number for rbin files. This should be changed when the file wrapper
        // format changes. It doesn't need to be bumped every time the serialization version is
        // bumped. That is, this is independent of the value serialization.
        // History:
        // * 0x0001: initial
        // * 0x0002: stream/chunk compression introduced
        private const ushort k_verSerRbin = 0x0002;
        // Readers back to this can still read this version.
        private const ushort k_verBackRbin = 0x0002;
        // This code can read back to this version.
        private const ushort k_verReadRbin = 0x0001;

        // Version where steam/chunk compression was introduced.
        private const ushort k_verRbinCompression = 0x0002;

        // Cached reflection infos.
        private readonly MethodInfo _methTryRead;
        private readonly MethodInfo _methMakeReader;
        private readonly MethodInfo _methThrowRead;
        private readonly MethodInfo _methSinkWriteByte;
        private readonly MethodInfo _methSourceReadByte;

        private readonly MethodInfo _methSinkWriteU2;
        private readonly MethodInfo _methSinkWriteU4;
        private readonly MethodInfo _methSinkWriteU8;
        private readonly MethodInfo _methSinkWriteR4;
        private readonly MethodInfo _methSinkWriteR8;

        private readonly MethodInfo _methSourceReadU2;
        private readonly MethodInfo _methSourceReadU4;
        private readonly MethodInfo _methSourceReadU8;
        private readonly MethodInfo _methSourceReadR4;
        private readonly MethodInfo _methSourceReadR8;

        // Hack needed for getting the method info for TryReadCore.
        private delegate bool TryReadDel(Source src, DType type, out object value);

        public Serializer(TypeManager tm)
        {
            Validation.AssertValue(tm);
            _tm = tm;
            _enc = Util.StdUTF8;

            _methTryRead = new TryReadDel(TryReadCore<object>).Method.GetGenericMethodDefinition();
            _methMakeReader = new Func<Source, DType, Action<long>, long, RbinReader>(MakeReaderCore<object>)
                .Method.GetGenericMethodDefinition();
            _methThrowRead = new Func<Exception>(ThrowRead).Method;
            _methSinkWriteByte = typeof(Sink).GetMethod("WriteByte", new[] { typeof(byte) }).VerifyValue();
            _methSourceReadByte = typeof(Source).GetMethod("ReadByte", Type.EmptyTypes).VerifyValue();

            _methSinkWriteU2 = typeof(Sink).GetMethod("WriteU2", new[] { typeof(ushort) }).VerifyValue();
            _methSinkWriteU4 = typeof(Sink).GetMethod("WriteU4Data", new[] { typeof(uint) }).VerifyValue();
            _methSinkWriteU8 = typeof(Sink).GetMethod("WriteU8Data", new[] { typeof(ulong) }).VerifyValue();
            _methSinkWriteR4 = typeof(Sink).GetMethod("WriteR4", new[] { typeof(float) }).VerifyValue();
            _methSinkWriteR8 = typeof(Sink).GetMethod("WriteR8", new[] { typeof(double) }).VerifyValue();
            _methSourceReadU2 = typeof(Source).GetMethod("ReadU2", Type.EmptyTypes).VerifyValue();
            _methSourceReadU4 = typeof(Source).GetMethod("ReadU4Data", Type.EmptyTypes).VerifyValue();
            _methSourceReadU8 = typeof(Source).GetMethod("ReadU8Data", Type.EmptyTypes).VerifyValue();
            _methSourceReadR4 = typeof(Source).GetMethod("ReadR4", Type.EmptyTypes).VerifyValue();
            _methSourceReadR8 = typeof(Source).GetMethod("ReadR8", Type.EmptyTypes).VerifyValue();

            // REVIEW: Should we explicitly implement Opt versions or just use auto-opt wrapping?
            // Explicitly implementing would be more efficient, but more dev work.

            // REVIEW: Need support for General!

            _typeToWriter = new ConcurrentDictionary<DType, Delegate>();
            _typeToWriter.TryAdd(DType.Vac, new Action<Sink, object>(WriteVac));
            _typeToWriter.TryAdd(DType.Null, new Action<Sink, object>(WriteNull));
            _typeToWriter.TryAdd(DType.BitReq, new Action<Sink, bool>(Write));
            _typeToWriter.TryAdd(DType.U1Req, new Action<Sink, byte>(Write));
            _typeToWriter.TryAdd(DType.U2Req, new Action<Sink, ushort>(Write));
            _typeToWriter.TryAdd(DType.U4Req, new Action<Sink, uint>(Write));
            _typeToWriter.TryAdd(DType.U8Req, new Action<Sink, ulong>(Write));
            _typeToWriter.TryAdd(DType.I1Req, new Action<Sink, sbyte>(Write));
            _typeToWriter.TryAdd(DType.I2Req, new Action<Sink, short>(Write));
            _typeToWriter.TryAdd(DType.I4Req, new Action<Sink, int>(Write));
            _typeToWriter.TryAdd(DType.I8Req, new Action<Sink, long>(Write));
            _typeToWriter.TryAdd(DType.IAReq, new Action<Sink, Integer>(Write));
            _typeToWriter.TryAdd(DType.R4Req, new Action<Sink, float>(Write));
            _typeToWriter.TryAdd(DType.R8Req, new Action<Sink, double>(Write));
            _typeToWriter.TryAdd(DType.DateReq, new Action<Sink, Date>(Write));
            _typeToWriter.TryAdd(DType.TimeReq, new Action<Sink, Time>(Write));
            _typeToWriter.TryAdd(DType.Text, new Action<Sink, string>(Write));
            _typeToWriter.TryAdd(DType.GuidReq, new Action<Sink, Guid>(Write));
            _typeToWriter.TryAdd(DType.UriGen, new Action<Sink, Link>(Write));

            _typeToReader = new ConcurrentDictionary<DType, Delegate>();
            _typeToReader.TryAdd(DType.Vac, new Func<Source, object>(ReadVac));
            _typeToReader.TryAdd(DType.Null, new Func<Source, object>(ReadNull));
            _typeToReader.TryAdd(DType.BitReq, new Func<Source, bool>(ReadBool));
            _typeToReader.TryAdd(DType.U1Req, new Func<Source, byte>(ReadU1));
            _typeToReader.TryAdd(DType.U2Req, new Func<Source, ushort>(ReadU2));
            _typeToReader.TryAdd(DType.U4Req, new Func<Source, uint>(ReadU4));
            _typeToReader.TryAdd(DType.U8Req, new Func<Source, ulong>(ReadU8));
            _typeToReader.TryAdd(DType.I1Req, new Func<Source, sbyte>(ReadI1));
            _typeToReader.TryAdd(DType.I2Req, new Func<Source, short>(ReadI2));
            _typeToReader.TryAdd(DType.I4Req, new Func<Source, int>(ReadI4));
            _typeToReader.TryAdd(DType.I8Req, new Func<Source, long>(ReadI8));
            _typeToReader.TryAdd(DType.IAReq, new Func<Source, Integer>(ReadInteger));
            _typeToReader.TryAdd(DType.R4Req, new Func<Source, float>(ReadR4));
            _typeToReader.TryAdd(DType.R8Req, new Func<Source, double>(ReadR8));
            _typeToReader.TryAdd(DType.DateReq, new Func<Source, Date>(ReadDateTime));
            _typeToReader.TryAdd(DType.TimeReq, new Func<Source, Time>(ReadTimeSpan));
            _typeToReader.TryAdd(DType.Text, new Func<Source, string>(ReadString));
            _typeToReader.TryAdd(DType.GuidReq, new Func<Source, Guid>(ReadGuid));
            _typeToReader.TryAdd(DType.UriGen, new Func<Source, Link>(ReadLink));
        }

        /// <summary>
        /// Assert that <typeparamref name="T"/> corresponds to <paramref name="type"/>.
        /// </summary>
        [Conditional("DEBUG")]
        private void AssertSysType<T>(DType type)
        {
            _tm.AssertSysType(type, typeof(T));
        }

        /// <summary>
        /// Return whether this type manager can read and write values of the given <paramref name="type"/>.
        /// </summary>
        public bool CanReadWrite(DType type)
        {
            if (!_tm.TryEnsureSysType(type, out Type st))
                return false;

            var meth = new Func<DType, bool>(CanReadWrite<object>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(st);

            return (bool)meth.Invoke(this, new object[] { type });
        }

        private bool CanReadWrite<T>(DType type)
        {
            var fn1 = GetReaderOrNull<T>(type);
            if (fn1 == null)
                return false;
            var fn2 = GetWriterOrNull<T>(type);
            if (fn2 == null)
                return false;
            return true;
        }

        /// <summary>
        /// Try to get a value writer action for the indicated type. Note that <typeparamref name="T"/> must
        /// be the system type for the given <paramref name="type"/>.
        /// WARNING: The writer does no preamble or version writing. That is done when the
        /// <see cref="ValueWriter"/> is created.
        /// </summary>
        public bool TryGetWriter<T>(DType type, out Action<ValueWriter, T> fnWrite)
        {
            AssertSysType<T>(type);

            var fn = GetWriterOrNull<T>(type);
            if (fn == null)
            {
                fnWrite = null;
                return false;
            }

            fnWrite = (vw, value) =>
            {
                if (vw is Sink sink)
                {
                    fn(sink, value);
                    return;
                }

                throw Validation.BugExceptParam(nameof(vw));
            };

            return true;
        }

        /// <summary>
        /// Try to get a value reader func for the indicated type. Note that <typeparamref name="T"/> must
        /// be the system type for the given <paramref name="type"/>.
        /// WARNING: The writer does no preamble or version writing. That is done when the
        /// <see cref="ValueReader"/> is created.
        /// </summary>
        public bool TryGetReader<T>(DType type, out Func<ValueReader, T> fnRead)
        {
            AssertSysType<T>(type);

            var fn = GetReaderOrNull<T>(type);
            if (fn == null)
            {
                fnRead = null;
                return false;
            }

            fnRead = vr =>
            {
                if (vr is Source src)
                {
                    var value = fn(src);
                    return value;
                }

                throw Validation.BugExceptParam(nameof(vr));
            };

            return true;
        }

        /// <summary>
        /// Try to write the value, according to the given <see cref="DType"/>.
        /// If <paramref name="settings"/> is not null, writes a full "rbin" format, suitable for
        /// a stand alone data file. Otherwise, writes the more compact rexl value format.
        /// May throw an IO exception.
        /// </summary>
        public bool TryWrite(Stream strm, DType type, object value, RbinSettings settings)
        {
            Validation.AssertValue(strm);
            Validation.Assert(type.IsValid);
            Validation.Assert(value != null || type.IsOpt);
            Validation.AssertValueOrNull(settings);
            Validation.Assert(strm.CanSeek || settings == null);

            if (type.HasGeneral || !_tm.TryEnsureSysType(type, out Type st))
                return false;
            Validation.Assert(value == null || st.IsAssignableFrom(value.GetType()));

            var meth = new Func<Stream, DType, object, RbinSettings, bool>(TryWrite<object>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(st);

            bool ret = (bool)meth.Invoke(this, new[] { strm, type, value, settings });
            return ret;
        }

        /// <summary>
        /// Try to write the value, according to the given <see cref="DType"/>.
        /// If <paramref name="settings"/> is not null, writes a full "rbin" format, suitable for
        /// a stand alone data file. Otherwise, writes the more compact rexl value format.
        /// May throw an IO exception.
        /// Asserts that T is the system type for the given <see cref="DType"/>.
        /// </summary>
        public bool TryWrite<T>(Stream strm, DType type, T value, RbinSettings settings)
        {
            AssertSysType<T>(type);
            Validation.AssertValue(strm);
            Validation.Assert(type.IsValid);
            Validation.Assert(value != null || type.IsOpt);
            Validation.Assert(value == null || typeof(T).IsAssignableFrom(value.GetType()));
            Validation.AssertValueOrNull(settings);
            Validation.Assert(strm.CanSeek || settings == null);

            CompressionKind comp;
            if (settings == null)
                comp = 0;
            else
            {
                comp = settings.Compression;
                Validation.Assert(comp.IsValid());

                if (comp != 0 && !comp.IsSupported())
                {
                    // Comp not yet supported. Fall back to Brotli.
                    // REVIEW: Is this a good thing to do? Kind of odd to do it silently.
                    comp = CompressionKind.Brotli;
                    settings = settings.SetCompression(comp);
                }
            }

            Action<Sink, T> fn;
            bool chunked = settings != null && type.IsSequence && settings.IsChunked;
            if (!chunked)
            {
                // Don't write in chunks.
                fn = GetWriterOrNull<T>(type);
            }
            else
            {
                // Also asserted above.
                Validation.Assert(strm.CanSeek);

                // Write top level sequences in chunks.
                DType typeItem = type.ItemTypeOrThis;
                Validation.Assert(typeItem != type);

                _tm.TryEnsureSysType(typeItem, out Type stItem).Verify();
                if (!typeof(IEnumerable<>).MakeGenericType(stItem).IsAssignableFrom(typeof(T)))
                {
                    // We'll hit this if the representation of sequence is not assignable to IEnumerable<T>. In this case,
                    // we should really invoke a virtual method on TypeManager to handle this.
                    Validation.Assert(false);
                    fn = null;
                }
                else
                {
                    var meth = new Func<DType, RbinSettings, Action<Sink, IEnumerable<object>>>(GetChunkedSeqWriterOrNull<object>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                    fn = (Action<Sink, T>)meth.Invoke(this, new object[] { typeItem, settings });
                }

                // We compress each chunk, not at the whole stream level.
                comp = 0;
            }
            if (fn == null)
                return false;

            Stream strmComp = null;
            var wrt = new BinaryWriter(strm, _enc, leaveOpen: true);
            try
            {
                if (settings != null)
                {
                    wrt.Write(k_sigRbin);
                    wrt.Write(k_verSerRbin);

                    // If we're not using compression, then older code can read this.
                    var verBackRbin = k_verBackRbin;
                    if (comp == 0 && verBackRbin == k_verRbinCompression)
                    {
                        Validation.Assert(comp == 0);
                        verBackRbin--;
                    }
                    wrt.Write(verBackRbin);

                    wrt.Write(type.Serialize());
                    wrt.Write((byte)Code.TypTail);
                    wrt.Write((byte)comp);
                }

                Stream strmWrt = strm;
                if (comp != 0)
                {
                    strmComp = comp.CreateWriter(strm, needPosition: false);
                    wrt.Dispose();
                    wrt = null; // In case the next line throws.
                    wrt = new BinaryWriter(strmWrt = strmComp, _enc, leaveOpen: true);
                }
                else
                    comp = 0;

                using var sink = CreateSinkCore(wrt, strmWrt);
                Write(sink, Code.TopHead);
                fn(sink, value);
                Write(sink, Code.TopTail);
                sink.Writer.Flush();
                return true;
            }
            finally
            {
                wrt?.Dispose();
                strmComp?.Dispose();
            }
        }

        /// <summary>
        /// Creates a <see cref="Sink"/> wrapping the given <paramref name="wrt"/>. This writes the
        /// header information at the current position.
        /// </summary>
        public ValueWriter CreateSink(BinaryWriter wrt)
        {
            Validation.AssertValue(wrt);
            return CreateSinkCore(wrt, wrt.BaseStream);
        }

        /// <summary>
        /// Creates a <see cref="Sink"/> wrapping the given <paramref name="wrt"/>. This writes the
        /// header information at the current position.
        /// </summary>
        private Sink CreateSinkCore(BinaryWriter wrt, Stream strm)
        {
            Validation.AssertValue(wrt);

            // Write header information.
            wrt.Write(k_sigSer);
            wrt.Write(k_verSer);
            wrt.Write(k_verBack);

            return new Sink(wrt, strm);
        }

        /// <summary>
        /// Try to read a value of the given <see cref="DType"/>. This assumes just the rexl binary
        /// form for the data, not the full "rbin" file format.
        /// May throw an <see cref="IOException"/> or <see cref="InvalidDataException"/> .
        /// </summary>
        public bool TryRead(Stream strm, DType type, out Type st, out object value)
        {
            Validation.AssertValue(strm);
            Validation.Assert(type.IsValid);

            if (!_tm.TryEnsureSysType(type, out st))
            {
                value = null;
                return false;
            }

            var meth = _methTryRead.MakeGenericMethod(st);
            object[] args;

            // Read and validate the header and create the value reader, aka Source.
            using (var rdr = new BinaryReader(strm, _enc, leaveOpen: true))
            using (var src = CreateSourceCore(rdr))
            {
                args = new object[] { src, type, null };
                bool ret = (bool)meth.Invoke(this, args);
                if (!ret)
                {
                    value = null;
                    return false;
                }
            }

            object val = args[2];
            Validation.Assert(val != null || type.IsOpt);
            Validation.Assert(val == null || st.IsAssignableFrom(val.GetType()));
            value = val;
            return true;
        }

        /// <summary>
        /// Creates a <see cref="Source"/> wrapping the given <paramref name="rdr"/>.
        /// If <paramref name="readHeader"/> is <c>true</c>, this reads and validates header information
        /// at the current location. Otherwise, it assumes version <c>0x00010004</c>.
        /// </summary>
        public ValueReader CreateSource(BinaryReader rdr, bool readHeader)
        {
            Validation.AssertValue(rdr);
            return CreateSourceCore(rdr, readHeader);
        }

        /// <summary>
        /// Creates a <see cref="Source"/> wrapping the given <paramref name="rdr"/>.
        /// If <paramref name="readHeader"/> is <c>true</c>, this reads and validates header information
        /// at the current location. Otherwise, it assumes version <c>0x00010004</c>.
        /// </summary>
        private Source CreateSourceCore(BinaryReader rdr, bool readHeader = true)
        {
            Validation.AssertValue(rdr);

            // Read and check header information.
            uint sig = readHeader ? rdr.ReadUInt32() : k_sigSer;
            CheckRead(sig == k_sigSer, "Bad data signature");
            uint ver = readHeader ? rdr.ReadUInt32() : k_verWithReadBack;
            CheckRead(ver >= k_verRead, "Can't read old data version");

            // Read the "back" version and validate it.
            if (ver >= k_verWithReadBack)
            {
                uint verBack = readHeader ? rdr.ReadUInt32() : k_verWithReadBack;
                CheckRead(verBack <= k_verSer, "New data version not readable by this code");
            }

            return new Source(ver, rdr);
        }

        private bool TryReadCore<T>(Source src, DType type, out T value)
        {
            AssertSysType<T>(type);
            Validation.AssertValue(src);

            var fn = GetReaderOrNull<T>(type);
            if (fn == null)
            {
                value = default;
                return false;
            }

            ReadCodeAndCheck(src, Code.TopHead);
            value = fn(src);
            ReadCodeAndCheck(src, Code.TopTail);
            return true;
        }

        #region Get Writer / Reader

        /// <summary>
        /// Get a writer for the given <paramref name="type"/>, if possible, return null if not possible.
        /// </summary>
        private Delegate GetWriterOrNull(DType type)
        {
            if (!_tm.TryEnsureSysType(type, out Type st))
                return null;

            var meth = new Func<DType, Action<Sink, object>>(GetWriterOrNull<object>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(st);

            var fn = (Delegate)meth.Invoke(this, new object[] { type });
            return fn;
        }

        /// <summary>
        /// Get a reader for the given <paramref name="type"/>, if possible, return null if not possible.
        /// </summary>
        private Delegate GetReaderOrNull(DType type)
        {
            if (!_tm.TryEnsureSysType(type, out Type st))
                return null;

            var meth = new Func<DType, Func<Source, object>>(GetReaderOrNull<object>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(st);

            var fn = (Delegate)meth.Invoke(this, new object[] { type });
            return fn;
        }

        /// <summary>
        /// Get a writer for the given <paramref name="type"/>, if possible, return null if not possible.
        /// </summary>
        private Action<Sink, T> GetWriterOrNull<T>(DType type)
        {
            AssertSysType<T>(type);

            // Strip the flavor from uri types.
            if (type.RootKind == DKind.Uri)
            {
                type = type.StripFlavor();
                AssertSysType<T>(type);
            }

            // First look in the cache.
            if (_typeToWriter.TryGetValue(type, out Delegate fnRaw))
            {
                Validation.Assert(fnRaw == null || fnRaw is Action<Sink, T>);
                return (Action<Sink, T>)fnRaw;
            }

            Action<Sink, T> fn;
            if (type.IsRecordXxx)
                fn = GetRecordWriterOrNull<T>(type);
            else if (type.IsTupleXxx)
                fn = GetTupleWriterOrNull<T>(type);
            else if (type.IsTensorXxx)
            {
                DType typeItem = type.GetTensorItemType();
                Validation.Assert(typeItem != type);

                if (!_tm.TryEnsureSysType(typeItem, out Type stItem))
                    fn = null;
                else
                {
                    Validation.Assert(typeof(Tensor<>).MakeGenericType(stItem).IsAssignableFrom(typeof(T)));
                    var meth = new Func<DType, Action<Sink, Tensor<object>>>(GetTensorWriterOrNull<object>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                    fn = (Action<Sink, T>)meth.Invoke(this, new object[] { type });
                }
            }
            else if (type.SeqCount > 0)
            {
                // Handle sequences.
                DType typeItem = type.ItemTypeOrThis;
                Validation.Assert(typeItem != type);

                if (!_tm.TryEnsureSysType(typeItem, out Type stItem))
                    fn = null;
                else if (!typeof(IEnumerable<>).MakeGenericType(stItem).IsAssignableFrom(typeof(T)))
                {
                    // We'll hit this if the representation of sequence is not assignable to IEnumerable<T>. In this case,
                    // we should really invoke a virtual method on TypeManager to handle this.
                    Validation.Assert(false);
                    fn = null;
                }
                else
                {
                    var meth = new Func<DType, Action<Sink, IEnumerable<object>>>(GetSeqWriterOrNull<object>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                    fn = (Action<Sink, T>)meth.Invoke(this, new object[] { typeItem });
                }
            }
            else if (type.HasReq)
            {
                // Handle opt forms from non-opt form.
                DType typeReq = type.ToReq();
                Validation.Assert(typeReq != type);

                if (!_tm.TryEnsureSysType(typeReq, out Type stReq))
                {
                    Validation.Assert(false);
                    fn = null;
                }
                else if (_tm.IsNullableTypeCore(typeof(T)))
                {
                    Validation.Assert(typeof(T).GetGenericArguments()[0] == stReq);
                    var meth = new Func<DType, Action<Sink, int?>>(GetNullableWriterOrNull<int>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stReq);
                    fn = (Action<Sink, T>)meth.Invoke(this, new object[] { typeReq, });
                }
                else if (stReq == typeof(T))
                    fn = GetOptWriterOrNull<T>(typeReq);
                else
                    fn = null;
            }
            else
            {
                // Can't handle this type.
                fn = null;
            }

            // REVIEW: Should we cache failures/nulls?
            return (Action<Sink, T>)_typeToWriter.GetOrAdd(type, fn);
        }

        /// <summary>
        /// Get a reader for the given <paramref name="type"/>, if possible, return null if not possible.
        /// </summary>
        private Func<Source, T> GetReaderOrNull<T>(DType type)
        {
            AssertSysType<T>(type);

            // Strip the flavor from uri types.
            if (type.RootKind == DKind.Uri)
            {
                type = type.StripFlavor();
                AssertSysType<T>(type);
            }

            // First look in the cache.
            if (_typeToReader.TryGetValue(type, out Delegate fnRaw))
            {
                Validation.Assert(fnRaw == null || fnRaw is Func<Source, T>);
                return (Func<Source, T>)fnRaw;
            }

            Func<Source, T> fn;
            if (type.IsRecordXxx)
                fn = GetRecordReaderOrNull<T>(type);
            else if (type.IsTupleXxx)
                fn = GetTupleReaderOrNull<T>(type);
            else if (type.IsTensorXxx)
            {
                DType typeItem = type.GetTensorItemType();
                Validation.Assert(typeItem != type);

                if (!_tm.TryEnsureSysType(typeItem, out Type stItem))
                    fn = null;
                else
                {
                    Validation.Assert(typeof(T).IsAssignableFrom(typeof(Tensor<>).MakeGenericType(stItem)));
                    var meth = new Func<DType, Func<Source, Tensor<object>>>(GetTensorReaderOrNull<object>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                    fn = (Func<Source, T>)meth.Invoke(this, new object[] { type });
                }
            }
            else if (type.SeqCount > 0)
            {
                // Handle sequences.
                DType typeItem = type.ItemTypeOrThis;
                Validation.Assert(typeItem != type);

                if (!_tm.TryEnsureSysType(typeItem, out Type stItem))
                    fn = null;
                else if (!typeof(T).IsAssignableFrom(stItem.MakeArrayType()))
                {
                    // We'll hit this if the representation of sequence is not assignable from T[]. In this case,
                    // we should really invoke a virtual method on TypeManager to handle this.
                    Validation.Assert(false);
                    fn = null;
                }
                else
                {
                    var meth = new Func<DType, Func<Source, IEnumerable<object>>>(GetSeqReaderOrNull<object>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                    fn = (Func<Source, T>)meth.Invoke(this, new object[] { typeItem });
                }
            }
            else if (type.HasReq)
            {
                // Handle opt forms from non-opt form.
                DType typeReq = type.ToReq();
                Validation.Assert(typeReq != type);

                if (!_tm.TryEnsureSysType(typeReq, out Type stReq))
                {
                    Validation.Assert(false);
                    fn = null;
                }
                else if (_tm.IsNullableTypeCore(typeof(T)))
                {
                    Validation.Assert(typeof(T).GetGenericArguments()[0] == stReq);
                    var meth = new Func<DType, Func<Source, int?>>(GetNullableReaderOrNull<int>)
                        .Method.GetGenericMethodDefinition().MakeGenericMethod(stReq);
                    fn = (Func<Source, T>)meth.Invoke(this, new object[] { typeReq });
                }
                else if (stReq == typeof(T))
                    fn = GetOptReaderOrNull<T>(typeReq);
                else
                    fn = null;
            }
            else
            {
                // Can't handle this type.
                fn = null;
            }

            // REVIEW: Should we cache failures/nulls?
            return (Func<Source, T>)_typeToReader.GetOrAdd(type, fn);
        }

        /// <summary>
        /// Get a writer for a nullable type by wrapping the writer for the non-nullable type.
        /// </summary>
        private Action<Sink, TReq?> GetNullableWriterOrNull<TReq>(DType typeReq)
            where TReq : struct
        {
            Validation.Assert(!typeReq.IsOpt);
            AssertSysType<TReq>(typeReq);
            AssertSysType<TReq?>(typeReq.ToOpt());

            var fnReq = GetWriterOrNull<TReq>(typeReq);
            if (fnReq == null)
                return null;

            return (sink, value) =>
            {
                if (!value.HasValue)
                    Write(sink, Code.NullYes);
                else
                {
                    Write(sink, Code.NullNot);
                    fnReq(sink, value.GetValueOrDefault());
                }
            };
        }

        /// <summary>
        /// Get a reader for a nullable type by wrapping the reader for the non-nullable type.
        /// </summary>
        private Func<Source, TReq?> GetNullableReaderOrNull<TReq>(DType typeReq)
            where TReq : struct
        {
            Validation.Assert(!typeReq.IsOpt);
            AssertSysType<TReq>(typeReq);
            AssertSysType<TReq?>(typeReq.ToOpt());

            var fnReq = GetReaderOrNull<TReq>(typeReq);
            if (fnReq == null)
                return null;

            return src =>
            {
                Code code = ReadCode(src);
                if (code == Code.NullYes)
                    return null;
                CheckRead(code == Code.NullNot);
                return fnReq(src);
            };
        }

        /// <summary>
        /// Get a writer for an opt reference type by wrapping the writer for the non-opt type.
        /// This asserts that the opt and non-opt types share the same system type.
        /// </summary>
        private Action<Sink, T> GetOptWriterOrNull<T>(DType typeReq)
        {
            Validation.Assert(!typeReq.IsOpt);
            AssertSysType<T>(typeReq);
            AssertSysType<T>(typeReq.ToOpt());

            var fnReq = GetWriterOrNull<T>(typeReq);
            if (fnReq == null)
                return null;

            return (sink, value) =>
            {
                if (value == null)
                    Write(sink, Code.NullYes);
                else
                {
                    Write(sink, Code.NullNot);
                    fnReq(sink, value);
                }
            };
        }

        /// <summary>
        /// Get a reader for an opt reference type by wrapping the reader for the non-opt type.
        /// This asserts that the opt and non-opt types share the same system type.
        /// </summary>
        private Func<Source, T> GetOptReaderOrNull<T>(DType typeReq)
        {
            Validation.Assert(!typeReq.IsOpt);
            AssertSysType<T>(typeReq);
            AssertSysType<T>(typeReq.ToOpt());

            var fnReq = GetReaderOrNull<T>(typeReq);
            if (fnReq == null)
                return null;

            return src =>
            {
                Code code = ReadCode(src);
                if (code == Code.NullYes)
                    return default;
                CheckRead(code == Code.NullNot);
                return fnReq(src);
            };
        }

        /// <summary>
        /// Get a writer for a sequence type by wrapping the writer for the item type.
        /// </summary>
        private Action<Sink, IEnumerable<TItem>> GetSeqWriterOrNull<TItem>(DType typeItem)
        {
            AssertSysType<TItem>(typeItem);

            var fnItem = GetWriterOrNull<TItem>(typeItem);
            if (fnItem == null)
                return null;

            return (sink, value) =>
            {
                if (value == null)
                {
                    Write(sink, Code.SeqNull);
                    return;
                }

                Write(sink, Code.SeqHead);
                var col = value as ICollection<TItem>;
                if (col != null || !sink.CanSeek)
                {
                    // If the value is a collection, we can write the count
                    // directly and omit seeking back at the end.
                    col ??= value.ToList();
                    long count = col.Count;
                    WriteFixed(sink, count);
                    long i = 0;
                    foreach (var item in col)
                    {
                        Validation.Assert(i < count);
                        fnItem(sink, item);
                        i++;
                    }
                    Validation.Assert(i == count);
                    Write(sink, Code.SeqTail);
                }
                else
                {
                    // Keep a placeholder for the item count, and write to it
                    // after processing all the elements of the sequence.
                    long posCnt = sink.Position;
                    WriteFixed(sink, -1L);
                    long count = 0;
                    foreach (var item in value)
                    {
                        fnItem(sink, item);
                        count++;
                    }

                    var posTail = sink.Position;
                    sink.MoveTo(posCnt);
                    WriteFixed(sink, count);
                    sink.MoveTo(posTail);
                    Write(sink, Code.SeqTail);
                }
            };
        }

        /// <summary>
        /// Get a writer for a sequence type by wrapping the writer for the item type.
        /// The writer will split the sequence into multiple chunks specified by <paramref name="settings"/>.
        /// 
        /// If the sequence is null, only Code.SeqNull is written. Otherwise the format of the encoding is:
        /// - `SeqChkd`
        /// - Item count (i8)
        /// - Chunk count (i8)
        /// - Number of bytes until the end of the last chunk (i8)
        /// - List of chunks:
        ///   - `ChkHead`
        ///   - Starting index of the first item in the chunk (i8)
        ///   - Chunk item count (i8)
        ///   - Chunk size in bytes (i8)
        ///   - Whether and how the chunk is compressed (u1)
        ///   - List of items
        ///   - `ChkFoot`
        ///   - Number of bytes to seek back to `ChkHead` (i8)
        ///   - `ChkTail`
        /// - `SeqTail`
        /// </summary>
        private Action<Sink, IEnumerable<TItem>> GetChunkedSeqWriterOrNull<TItem>(DType typeItem, RbinSettings settings)
        {
            AssertSysType<TItem>(typeItem);
            Validation.AssertValue(settings);

            if (settings.ChunkSizeItems <= 0 && settings.ChunkSizeBytes <= 0)
                return GetSeqWriterOrNull<TItem>(typeItem);

            var fnItem = GetWriterOrNull<TItem>(typeItem);
            if (fnItem == null)
                return null;

            var limItem = settings.ChunkSizeItems <= 0 ? long.MaxValue : settings.ChunkSizeItems;
            var limSize = settings.ChunkSizeBytes <= 0 ? long.MaxValue : settings.ChunkSizeBytes;

            var comp = settings.Compression;
            return (sink, value) =>
            {
                Validation.Assert(sink.CanSeek);
                if (value == null)
                {
                    Write(sink, Code.SeqNull);
                    return;
                }

                Write(sink, Code.SeqChkd);

                // Keep a placeholder for the metadata, and write to it
                // after processing all the elements of the sequence.
                var posCntSeq = sink.Position;
                WriteFixed(sink, -1L); // Item count.
                WriteFixed(sink, -1L); // Chunk count.
                WriteFixed(sink, -1L); // Bytes until the end of the last chunk.

                using var ator = value.GetEnumerator();
                long countItemSeq = 0;
                long countChunkSeq = 0;
                bool more = ator.MoveNext();
                while (more)
                {
                    Write(sink, Code.ChkHead);
                    WriteFixed(sink, countItemSeq); // The starting index of the chunk.

                    // Save position to come back after writing chunk.
                    var posStartChk = sink.Position;
                    WriteFixed(sink, -1L); // Number of items in chunk.
                    WriteFixed(sink, -1L); // Size of chunk in bytes.

                    long countChk;
                    long posStartData;
                    Stream strmComp = null;
                    BinaryWriter wrtComp = null;
                    Sink sinkComp = null;
                    long sizeData;
                    try
                    {
                        Write(sink, (byte)comp);
                        if (comp != 0)
                        {
                            strmComp = comp.CreateWriter(sink.BaseStream, needPosition: true);
                            wrtComp = new BinaryWriter(strmComp, _enc, leaveOpen: true);
                            sinkComp = new Sink(wrtComp, strmComp);
                            Validation.Assert(sinkComp.CanGetPos);
                        }

                        posStartData = sink.Position;
                        Validation.Assert(posStartData == posStartChk + sizeof(long) * 2 + sizeof(byte));

                        // We use the uncompressed size as the chunk threshold size. This avoids having to flush
                        // just to determine the number of compressed bytes. Doing so can have negative impact
                        // on compression.
                        countChk = 0;
                        var sinkCur = sinkComp ?? sink;
                        var posStartCur = sinkCur.Position;
                        for (long i = 0; i < limItem; i++)
                        {
                            Validation.Assert(more);
                            if (sinkCur.Position - posStartCur >= limSize)
                                break;

                            countChk++;
                            var item = ator.Current;
                            fnItem(sinkCur, item);

                            more = ator.MoveNext();
                            if (!more)
                                break;
                        }
                        Validation.Assert(countChk > 0);
                        sizeData = sinkCur.Position - posStartCur;
                    }
                    finally
                    {
                        sinkComp?.Dispose();
                        wrtComp?.Dispose();
                        strmComp?.Dispose();
                    }

                    var posEnd = sink.Position;
                    Validation.Assert(comp != 0 | sizeData == posEnd - posStartData);
                    sizeData = posEnd - posStartData;

                    sink.MoveTo(posStartChk);
                    WriteFixed(sink, countChk);
                    WriteFixed(sink, sizeData);
                    countItemSeq += countChk;
                    countChunkSeq++;

                    sink.MoveTo(posEnd);
                    // The ChkFoot information allows skipping backwards.
                    Write(sink, Code.ChkFoot);
                    WriteFixed(sink, sizeData + sizeof(long) * 3 + sizeof(byte) * 2); // Go back to ChkHead.
                    Write(sink, Code.ChkTail);
                }

                Validation.Assert(value is not ICollection<TItem> col || countItemSeq == col.Count);
                var posTail = sink.Position;
                sink.MoveTo(posCntSeq);
                WriteFixed(sink, countItemSeq);
                WriteFixed(sink, countChunkSeq);
                WriteFixed(sink, posTail - (posCntSeq + sizeof(long) * 3));
                sink.MoveTo(posTail);
                Write(sink, Code.SeqTail);
            };
        }

        /// <summary>
        /// Get a reader for a sequence type by wrapping the reader for the item type.
        /// </summary>
        private Func<Source, IEnumerable<TItem>> GetSeqReaderOrNull<TItem>(DType typeItem)
        {
            AssertSysType<TItem>(typeItem);

            var fnItem = GetReaderOrNull<TItem>(typeItem);
            if (fnItem == null)
                return null;

            return src =>
            {
                Code code = ReadCode(src);
                if (code == Code.SeqNull)
                    return null;

                // REVIEW: Use a BuildableSequence or GrowableArray to handle very large count?
                TItem[] items;

                // REVIEW: Is there any reason to support chunking here?
                CheckRead(code == Code.SeqHead);

                // Version 0x00010004 changed the count to long.
                var pre0004 = src.Version <= 0x00010003;
                long count = pre0004 ? ReadI4(src) : ReadI8Fixed(src);
                CheckRead(count >= 0);

                items = new TItem[count];
                for (long i = 0; i < count; i++)
                {
                    // Version 0x00010004 removed the SeqItem code.
                    if (pre0004)
                        ReadCodeAndCheck(src, Code.SeqItem);
                    items[i] = fnItem(src);
                }

                ReadCodeAndCheck(src, Code.SeqTail);
                return items;
            };
        }

        /// <summary>
        /// If there is an "easy" write method on <see cref="Sink"/> for the given <paramref name="type"/>,
        /// return the MethodInfo.
        /// </summary>
        private MethodInfo GetEasyWriter(DType type)
        {
            // REVIEW: Handle simple opt types.
            if (!type.IsOpt)
            {
                switch (type.Kind)
                {
                case DKind.I2:
                case DKind.U2:
                    return _methSinkWriteU2;
                case DKind.I4:
                case DKind.U4:
                    return _methSinkWriteU4;
                case DKind.I8:
                case DKind.U8:
                    return _methSinkWriteU8;
                case DKind.R4:
                    return _methSinkWriteR4;
                case DKind.R8:
                    return _methSinkWriteR8;
                }
            }

            return null;
        }

        /// <summary>
        /// If there is an "easy" read method on <see cref="Source"/> for the given <paramref name="type"/>,
        /// return the MethodInfo.
        /// </summary>
        private MethodInfo GetEasyReader(DType type)
        {
            // REVIEW: Handle simple opt types.
            if (!type.IsOpt)
            {
                switch (type.Kind)
                {
                case DKind.I2:
                case DKind.U2:
                    return _methSourceReadU2;
                case DKind.I4:
                case DKind.U4:
                    return _methSourceReadU4;
                case DKind.I8:
                case DKind.U8:
                    return _methSourceReadU8;
                case DKind.R4:
                    return _methSourceReadR4;
                case DKind.R8:
                    return _methSourceReadR8;
                }
            }

            return null;
        }

        /// <summary>
        /// Get a writer for a record type. This handles both req and opt variations.
        /// This uses dynamic code gen for efficiency.
        /// </summary>
        private Action<Sink, T> GetRecordWriterOrNull<T>(DType typeRec)
        {
            Validation.Assert(typeRec.IsRecordXxx);
            AssertSysType<T>(typeRec);

            ILLogKind logKind = ILLogKind.None;
            MethodGenerator gen = new MethodGenerator("write_rec", logKind, null, typeof(object[]), typeof(Sink), typeof(T));

            // The "this" value for the method will be an object[] containing the field writer delegates.
            var consts = new List<object>();

            // Keep track of writers, their assigned slot, and their system type.
            var fldTypeToFnSlot = new Dictionary<DType, (int slot, Type st, MethodInfo meth)>();

            var il = gen.Il;

            if (typeRec.IsOpt)
            {
                // Handle the possibility of null.
                Label labNotNull = default;
                il
                    // Test for null.
                    .Ldarg(2).Brtrue(ref labNotNull)
                    // Write RecNull.
                    .Ldarg(1).Ldc_I4((byte)Code.RecNull).Call(_methSinkWriteByte)
                    .Ret()
                    .MarkLabel(labNotNull);
                typeRec = typeRec.ToReq();
            }
            Validation.Assert(typeRec.IsRecordReq);

            // Write RecHead.
            il.Ldarg(1).Ldc_I4((byte)Code.RecHead).Call(_methSinkWriteByte);

            // Iterate in name order. Note that the order is critical since the serialized form
            // does not embed field names. The writer and reader are driven by DType. That is,
            // the serialized form is NOT self-describing.
            foreach (var tn in typeRec.GetNames())
            {
                DType typeFld = tn.Type;
                Type stFld = _tm.GetSysTypeOrNull(typeFld).VerifyValue();

                // Write RecItem.
                il.Ldarg(1).Ldc_I4((byte)Code.RecItem).Call(_methSinkWriteByte);

                var meth = GetEasyWriter(typeFld);
                if (meth != null)
                {
                    il
                        .Ldarg(1)
                        .Ldarg(2);
                    _tm.GenLoadField(gen, typeRec, typeof(T), tn.Name, typeFld);
                    // Call the writer.
                    il.Call(meth);
                }
                else
                {
                    if (!fldTypeToFnSlot.TryGetValue(typeFld, out var info))
                    {
                        // Get the writer for the field.
                        var fnFld = GetWriterOrNull(typeFld);
                        if (fnFld == null)
                            return null;
                        // REVIEW: Cache the MethodInfo for Action<Sink, T, TryGetCustom>?
                        Type stWrt = typeof(Action<,>).MakeGenericType(typeof(Sink), stFld);
                        var methWrt = stWrt.GetMethod("Invoke", new[] { typeof(Sink), stFld }).VerifyValue();
                        info = (consts.Count, stWrt, methWrt);
                        fldTypeToFnSlot.Add(typeFld, info);
                        consts.Add(fnFld);
                    }

                    il
                        // Load the writer.
                        .Ldarg(0).Ldc_I4(info.slot).Ldelem(info.st)
                        // Prepare for calling the writer.
                        .Ldarg(1)
                        .Ldarg(2);
                    _tm.GenLoadField(gen, typeRec, typeof(T), tn.Name, typeFld);
                    // Call the writer.
                    il.Callvirt(info.meth);
                }
            }

            il
                // Write RecTail.
                .Ldarg(1).Ldc_I4((byte)Code.RecTail).Call(_methSinkWriteByte)
                .Ret();

            // Get the delegate, with the constants array as this.
            var res = gen.CreateInstanceDelegate(typeof(Action<Sink, T>), consts.ToArray());

            // To see the generated IL in debug output, set log = true above and turn on the
            // ENABLE_LOGGING symbol at the top of this source file.
            if (logKind != ILLogKind.None)
            {
                Log(gen.GetHeader());
                var lines = il.ResetLines();
                foreach (var line in lines)
                    Log("  " + line);
                Log("");
            }

            return (Action<Sink, T>)res;
        }

        /// <summary>
        /// Get a reader for a record type. This handles both req and opt variations.
        /// This uses dynamic code gen for efficiency.
        /// </summary>
        private Func<Source, T> GetRecordReaderOrNull<T>(DType typeRec)
        {
            Validation.Assert(typeRec.IsRecordXxx);
            AssertSysType<T>(typeRec);

            ILLogKind logKind = ILLogKind.None;
            MethodGenerator gen = new MethodGenerator("read_rec", logKind, typeof(T), typeof(object[]), typeof(Source));

            var consts = new List<object>();

            // We need "this" for throwing when something goes wrong.
            consts.Add(this);

            // Keep track of readers, their assigned slot, and their system type.
            var fldTypeToFnSlot = new Dictionary<DType, (int slot, Type st, MethodInfo meth)>();

            var ilw = gen.Il;

            // Read the code.
            ilw
                .Ldarg(1)
                .Call(_methSourceReadByte);

            if (typeRec.IsOpt)
            {
                // Test for and handle a RecNull code.
                Label labNotNull = default;
                ilw
                    .Dup().Ldc_I4((byte)Code.RecNull).Bne_Un(ref labNotNull)
                    .Pop().Ldnull().Ret()
                    .MarkLabel(labNotNull);
                typeRec = typeRec.ToReq();
            }
            Validation.Assert(typeRec.IsRecordReq);

            // Test for RecHead and throw if not.
            // We assume that the record generator doesn't affect the stack state.
            Label labBad = default;
            ilw.Ldc_I4((byte)Code.RecHead).Bne_Un(ref labBad);

            void GenLoadRrti(RecordRuntimeTypeInfo rrti)
            {
                Validation.AssertValue(rrti);
                int idx = consts.Count;
                consts.Add(rrti);
                ilw.Ldarg(0).Ldc_I4(idx).Ldelem(typeof(RecordRuntimeTypeInfo));
            }

            // Create the record object and store it in a local.
            using (var rg = _tm.CreateRecordGenerator(gen, typeRec, GenLoadRrti))
            {
                Validation.Assert(rg.RecSysType == typeof(T));

                // Iterate in name order. Note that the order is critical since the serialized form
                // does not embed field names. The writer and reader are driven by DType. That is,
                // the serialized form is NOT self-describing.
                foreach (var tn in typeRec.GetNames())
                {
                    DType typeFld = tn.Type;
                    Type stFld = _tm.GetSysTypeOrNull(typeFld).VerifyValue();

                    // Read a code and compare to RecItem.
                    ilw.Ldarg(1).Call(_methSourceReadByte).Ldc_I4((byte)Code.RecItem).Bne_Un(ref labBad);

                    // Prepare for storing the field value.
                    rg.SetFromStackPre(tn.Name, typeFld);

                    var meth = GetEasyReader(typeFld);
                    if (meth != null)
                        ilw.Ldarg(1).Call(meth);
                    else
                    {
                        if (!fldTypeToFnSlot.TryGetValue(typeFld, out var info))
                        {
                            // Get the reader for the field.
                            var fnFld = GetReaderOrNull(typeFld);
                            if (fnFld == null)
                                return null;
                            // REVIEW: Cache the MethodInfo for Func<Source, T, GetCustomSeq>?
                            Type stRdr = typeof(Func<,>).MakeGenericType(typeof(Source), stFld);
                            var methRdr = stRdr.GetMethod("Invoke", new[] { typeof(Source) }).VerifyValue();
                            info = (consts.Count, stRdr, methRdr);
                            fldTypeToFnSlot.Add(typeFld, info);
                            consts.Add(fnFld);
                        }
                        Validation.Assert(info.slot > 0);

                        ilw
                            // Load the reader.
                            .Ldarg(0).Ldc_I4(info.slot).Ldelem(info.st)
                            // Call the reader.
                            .Ldarg(1).Callvirt(info.meth);
                    }

                    // Store the field value.
                    rg.SetFromStackPost();
                }

                Label labGood = default;
                ilw
                    // Read a code and compare to RecTail.
                    .Ldarg(1).Call(_methSourceReadByte).Ldc_I4((byte)Code.RecTail).Beq(ref labGood)
                    .MarkLabel(labBad)
                    // Bad code, so throw.
                    .Ldarg(0).Ldc_I4(0).Ldelem(typeof(Serializer)).Call(_methThrowRead).Throw()
                    .MarkLabel(labGood);

                rg.Finish();
                ilw.Ret();
            }

            // Get the delegate, with the constants array as this.
            var res = gen.CreateInstanceDelegate(typeof(Func<Source, T>), consts.ToArray());

            // To see the generated IL in debug output, set log = true above and turn on the
            // ENABLE_LOGGING symbol at the top of this source file.
            if (logKind != ILLogKind.None)
            {
                Log(gen.GetHeader());
                var lines = ilw.ResetLines();
                foreach (var line in lines)
                    Log("  " + line);
                Log("");
            }

            return (Func<Source, T>)res;
        }

        /// <summary>
        /// Get a writer for a tuple type. This handles both req and opt variations.
        /// This uses dynamic code gen for efficiency.
        /// </summary>
        private Action<Sink, T> GetTupleWriterOrNull<T>(DType typeTup)
        {
            Validation.Assert(typeTup.IsTupleXxx);
            AssertSysType<T>(typeTup);

            ILLogKind logKind = ILLogKind.None;
            MethodGenerator gen = new MethodGenerator("write_tup", logKind, null, typeof(object[]), typeof(Sink), typeof(T));

            // The "this" value for the method will be an object[] containing the field writer delegates.
            var consts = new List<object>();

            // Keep track of writers, their assigned slot, and their system type.
            var fldTypeToFnSlot = new Dictionary<DType, (int slot, Type st, MethodInfo meth)>();

            var il = gen.Il;

            if (typeTup.IsOpt)
            {
                // Handle the possibility of null.
                Label labNotNull = default;
                il
                    // Test for null.
                    .Ldarg(2).Brtrue(ref labNotNull)
                    // Write TupNull.
                    .Ldarg(1).Ldc_I4((byte)Code.TupNull).Call(_methSinkWriteByte)
                    .Ret()
                    .MarkLabel(labNotNull);
                typeTup = typeTup.ToReq();
            }
            Validation.Assert(typeTup.IsTupleReq);

            // Write TupHead.
            il.Ldarg(1).Ldc_I4((byte)Code.TupHead).Call(_methSinkWriteByte);

            var types = typeTup.GetTupleSlotTypes();
            int arity = types.Length;

            for (int i = 0; i < arity; i++)
            {
                DType typeFld = types[i];
                Type stFld = _tm.GetSysTypeOrNull(typeFld).VerifyValue();

                // Write TupItem.
                il.Ldarg(1).Ldc_I4((byte)Code.TupItem).Call(_methSinkWriteByte);

                var meth = GetEasyWriter(typeFld);
                if (meth != null)
                {
                    il
                        .Ldarg(1)
                        .Ldarg(2);
                    _tm.GenLoadSlot(gen, typeTup, typeof(T), i, typeFld);
                    // Call the writer.
                    il.Call(meth);
                }
                else
                {
                    if (!fldTypeToFnSlot.TryGetValue(typeFld, out var info))
                    {
                        // Get the writer for the field.
                        var fnFld = GetWriterOrNull(typeFld);
                        if (fnFld == null)
                            return null;
                        // REVIEW: Cache the MethodInfo for Action<Sink, T, TryGetCustom>?
                        Type stWrt = typeof(Action<,>).MakeGenericType(typeof(Sink), stFld);
                        var methWrt = stWrt.GetMethod("Invoke", new[] { typeof(Sink), stFld }).VerifyValue();
                        info = (consts.Count, stWrt, methWrt);
                        fldTypeToFnSlot.Add(typeFld, info);
                        consts.Add(fnFld);
                    }

                    il
                        // Load the writer.
                        .Ldarg(0).Ldc_I4(info.slot).Ldelem(info.st)
                        // Prepare for calling the writer.
                        .Ldarg(1)
                        .Ldarg(2);
                    _tm.GenLoadSlot(gen, typeTup, typeof(T), i, typeFld);
                    // Call the writer.
                    il.Callvirt(info.meth);
                }
            }

            il
                // Write TupTail.
                .Ldarg(1).Ldc_I4((byte)Code.TupTail).Call(_methSinkWriteByte)
                .Ret();

            // Get the delegate, with the constants array as this.
            var res = gen.CreateInstanceDelegate(typeof(Action<Sink, T>), consts.ToArray());

            // To see the generated IL in debug output, set log = true above and turn on the
            // ENABLE_LOGGING symbol at the top of this source file.
            if (logKind != ILLogKind.None)
            {
                Log(gen.GetHeader());
                var lines = il.ResetLines();
                foreach (var line in lines)
                    Log("  " + line);
                Log("");
            }

            return (Action<Sink, T>)res;
        }

        /// <summary>
        /// Get a reader for a tuple type. This handles both req and opt variations.
        /// This uses dynamic code gen for efficiency.
        /// </summary>
        private Func<Source, T> GetTupleReaderOrNull<T>(DType typeTup)
        {
            Validation.Assert(typeTup.IsTupleXxx);
            AssertSysType<T>(typeTup);

            ILLogKind logKind = ILLogKind.None;
            MethodGenerator gen = new MethodGenerator("read_tup", logKind, typeof(T), typeof(object[]), typeof(Source));

            var consts = new List<object>();

            // We need "this" for throwing when something goes wrong.
            consts.Add(this);

            // Keep track of readers, their assigned slot, and their system type.
            var fldTypeToFnSlot = new Dictionary<DType, (int slot, Type st, MethodInfo meth)>();

            var il = gen.Il;

            // Read the code.
            il
                .Ldarg(1)
                .Call(_methSourceReadByte);

            if (typeTup.IsOpt)
            {
                // Test for and handle a TupNull code.
                Label labNotNull = default;
                il
                    .Dup().Ldc_I4((byte)Code.TupNull).Bne_Un(ref labNotNull)
                    .Pop().Ldnull().Ret()
                    .MarkLabel(labNotNull);
                typeTup = typeTup.ToReq();
            }
            Validation.Assert(typeTup.IsTupleReq);

            var types = typeTup.GetTupleSlotTypes();
            int arity = types.Length;

            // Test for TupHead and throw if not.
            Label labBad = default;
            il.Ldc_I4((byte)Code.TupHead).Bne_Un(ref labBad);

            // Create the tuple object and store it in a local.
            _tm.GenCreateTuple(gen, typeTup, typeof(T));
            using (var locTup = gen.AcquireLocal(typeof(T)))
            {
                il.Stloc(locTup);

                for (int i = 0; i < arity; i++)
                {
                    DType typeFld = types[i];
                    Type stFld = _tm.GetSysTypeOrNull(typeFld).VerifyValue();

                    // Read a code and compare to TupItem.
                    il.Ldarg(1).Call(_methSourceReadByte).Ldc_I4((byte)Code.TupItem).Bne_Un(ref labBad);

                    // Prepare for storing the field value.
                    il.Ldloc(locTup);

                    var meth = GetEasyReader(typeFld);
                    if (meth != null)
                        il.Ldarg(1).Call(meth);
                    else
                    {
                        if (!fldTypeToFnSlot.TryGetValue(typeFld, out var info))
                        {
                            // Get the reader for the field.
                            var fnFld = GetReaderOrNull(typeFld);
                            if (fnFld == null)
                                return null;
                            // REVIEW: Cache the MethodInfo for Func<Source, GetCustomSeq, T>?
                            Type stRdr = typeof(Func<,>).MakeGenericType(typeof(Source), stFld);
                            var methRdr = stRdr.GetMethod("Invoke", new[] { typeof(Source) }).VerifyValue();
                            info = (consts.Count, stRdr, methRdr);
                            fldTypeToFnSlot.Add(typeFld, info);
                            consts.Add(fnFld);
                        }
                        Validation.Assert(info.slot > 0);

                        il
                            // Load the reader.
                            .Ldarg(0).Ldc_I4(info.slot).Ldelem(info.st)
                            // Call the reader.
                            .Ldarg(1).Callvirt(info.meth);
                    }

                    // Store the field value.
                    _tm.GenStoreSlot(gen, typeTup, typeof(T), i, typeFld);
                }

                Label labGood = default;
                il
                    // Read a code and compare to TupTail.
                    .Ldarg(1).Call(_methSourceReadByte).Ldc_I4((byte)Code.TupTail).Beq(ref labGood)
                    .MarkLabel(labBad)
                    // Bad code, so throw.
                    .Ldarg(0).Ldc_I4(0).Ldelem(typeof(Serializer)).Call(_methThrowRead).Throw()
                    .MarkLabel(labGood)
                    .Ldloc(locTup)
                    .Ret();
            }

            // Get the delegate, with the constants array as this.
            var res = gen.CreateInstanceDelegate(typeof(Func<Source, T>), consts.ToArray());

            // To see the generated IL in debug output, set log = true above and turn on the
            // ENABLE_LOGGING symbol at the top of this source file.
            if (logKind != ILLogKind.None)
            {
                Log(gen.GetHeader());
                var lines = il.ResetLines();
                foreach (var line in lines)
                    Log("  " + line);
                Log("");
            }

            return (Func<Source, T>)res;
        }

        /// <summary>
        /// Get a writer for a tensor type.
        /// </summary>
        private Action<Sink, Tensor<TItem>> GetTensorWriterOrNull<TItem>(DType type)
        {
            Validation.Assert(type.IsTensorXxx);
            var typeItem = type.GetTensorItemType();
            AssertSysType<TItem>(typeItem);

            var fnItem = GetWriterOrNull<TItem>(typeItem);
            if (fnItem == null)
                return null;

            var serializer = new TensorSerializer<TItem>(this, type, fnItem);
            return serializer.Write;
        }

        /// <summary>
        /// Get a reader for a tensor type.
        /// </summary>
        private Func<Source, Tensor<TItem>> GetTensorReaderOrNull<TItem>(DType type)
        {
            Validation.Assert(type.IsTensorXxx);
            var typeItem = type.GetTensorItemType();
            AssertSysType<TItem>(typeItem);

            var fnItem = GetReaderOrNull<TItem>(typeItem);
            if (fnItem == null)
                return null;

            var deserializer = new TensorDeserializer<TItem>(this, type, fnItem);
            return deserializer.Read;
        }

        /// <summary>
        /// Serializer to pass to <see cref="Tensor{T}.Serialize"></see> that writes the tensor
        /// to this <see cref="TypeManager.Serializer"/>.
        /// </summary>
        private class TensorSerializer<TItem> : Tensor<TItem>.Serializer<Sink>
        {
            private readonly Serializer _parent;

            /// <summary>
            /// The writer function to call for the values in the tensor.
            /// </summary>
            private readonly Action<Sink, TItem> _fnWriteItem;

            /// <summary>
            /// Type of the tensor being written.
            /// </summary>
            private DType _type;

            public TensorSerializer(Serializer parent, DType type, Action<Sink, TItem> fnWriteItem)
            {
                Validation.AssertValue(parent, nameof(parent));
                Validation.AssertValue(fnWriteItem, nameof(fnWriteItem));
                Validation.Assert(type.IsTensorXxx);

                _parent = parent;
                _fnWriteItem = fnWriteItem;
                _type = type;
            }

            public override void WriteByte(Sink sink, byte val) => _parent.Write(sink, val);

            public override void WriteI8(Sink sink, long val) => _parent.Write(sink, val);

            public override void WriteShape(Sink sink, Shape shape)
            {
                Validation.AssertValue(sink);
                Validation.BugCheck(shape.Rank == _type.TensorRank);

                // Shapes are serialized as the rank followed by the values of each dimension.
                _parent.Write(sink, shape.Rank);
                foreach (var dim in shape)
                    _parent.Write(sink, dim);
            }

            public override void WriteValue(Sink sink, TItem value)
            {
                Validation.AssertValue(sink);
                _fnWriteItem(sink, value);
            }

            /// <summary>
            /// The writer function returned by GetWriterOrNull.
            /// </summary>
            public void Write(Sink sink, Tensor<TItem> value)
            {
                Validation.AssertValue(sink);
                if (value == null)
                {
                    Validation.BugCheck(_type.IsOpt);
                    _parent.Write(sink, Code.TensNull);
                    return;
                }

                _parent.Write(sink, Code.TensHead);
                value.Serialize(this, sink);
                _parent.Write(sink, Code.TensTail);
            }
        }

        /// <summary>
        /// Deserializer to pass to <see cref="Tensor{T}.Deserialize"></see> that writes the tensor
        /// to this <see cref="TypeManager.Serializer"/>.
        /// </summary>
        private class TensorDeserializer<TItem> : Tensor<TItem>.Deserializer<Source>
        {
            private readonly Serializer _parent;

            /// <summary>
            /// The reader function to call for the values in the tensor.
            /// </summary>
            private readonly Func<Source, TItem> _fnReadItem;

            /// <summary>
            /// Type of the tensor being read.
            /// </summary>
            private DType _type;

            public TensorDeserializer(Serializer parent, DType type, Func<Source, TItem> fnReadItem)
            {
                Validation.AssertValue(parent, nameof(parent));
                Validation.AssertValue(fnReadItem, nameof(fnReadItem));
                Validation.Assert(type.IsTensorXxx);

                _parent = parent;
                _fnReadItem = fnReadItem;
                _type = type;
            }

            public override byte ReadByte(Source src) => _parent.ReadU1(src);

            public override long ReadI8(Source src) => _parent.ReadI8(src);

            public override Shape ReadShape(Source src)
            {
                Validation.AssertValue(src);

                var rank = _parent.ReadI4(src);
                CheckRead(rank == _type.TensorRank);

                var bldr = Shape.CreateBuilder(rank);
                for (int i = 0; i < rank; i++)
                {
                    var dim = _parent.ReadI8(src);
                    CheckRead(dim >= 0);
                    bldr[i] = dim;
                }
                return bldr.ToImmutable();
            }

            public override TItem ReadValue(Source src)
            {
                Validation.AssertValue(src);
                return _fnReadItem(src);
            }

            /// <summary>
            /// The reader function returned by GetReaderOrNull.
            /// </summary>
            public Tensor<TItem> Read(Source src)
            {
                Validation.AssertValue(src);
                Code code = ReadCode(src);

                if (code == Code.TensNull)
                {
                    CheckRead(_type.IsOpt);
                    return null;
                }

                CheckRead(code == Code.TensHead);
                var tensor = Tensor<TItem>.Deserialize(this, src);
                ReadCodeAndCheck(src, Code.TensTail);

                return tensor;
            }

            public override void Check(bool cond, string msg = null)
            {
                CheckRead(cond, msg);
            }
        }

        #endregion Get Writer / Reader

        #region Write Methods

        private void WriteNull(Sink sink, object v)
        {
            Validation.AssertValue(sink);
            Validation.Assert(v == null);
            sink.WriteByte((byte)Code.NullYes);
        }

        private void WriteVac(Sink sink, object v)
        {
            Validation.AssertValue(sink);
            throw Validation.BugExcept("Unexpected Vac");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, Code v) => sink.VerifyValue().WriteByte((byte)v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, byte v) => sink.VerifyValue().WriteByte(v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, sbyte v) => sink.VerifyValue().WriteByte((byte)v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, ushort v) => sink.VerifyValue().WriteU2(v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, short v) => sink.VerifyValue().WriteU2((ushort)v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, uint v) => sink.VerifyValue().WriteU4Data(v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, int v) => sink.VerifyValue().WriteU4Data((uint)v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, ulong v) => sink.VerifyValue().WriteU8Data(v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, long v) => sink.VerifyValue().WriteU8Data((ulong)v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFixed(Sink sink, long v) => sink.VerifyValue().WriteU8Fixed((ulong)v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, bool v) => sink.VerifyValue().WriteByte(v ? (byte)1 : (byte)0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, float v) => sink.VerifyValue().WriteU4Fixed(v.ToBits());
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, double v) => sink.VerifyValue().WriteU8Fixed(v.ToBits());
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, Date v) => sink.VerifyValue().WriteU8Fixed((ulong)v.Ticks);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(Sink sink, Time v) => Write(sink, v.Ticks);

        private void Write(Sink sink, Integer v)
        {
            Validation.AssertValue(sink);
            var data = v.ToByteArray();
            Validation.Assert(data.Length > 0);

            sink.WriteByte((byte)Code.IntHead);
            sink.WriteU4Data((uint)data.Length);
            sink.Write(data, 0, data.Length);
            sink.WriteByte((byte)Code.IntTail);
        }

        private void Write(Sink sink, string v)
        {
            Validation.AssertValue(sink);
            if (v == null)
            {
                Write(sink, Code.NullYes);
                return;
            }
            sink.WriteByte((byte)Code.StrHead);
            sink.Writer.Write(v);
            sink.WriteByte((byte)Code.StrTail);
        }

        private void Write(Sink sink, Guid v)
        {
            Validation.AssertValue(sink);
            var pair = v.ToBits();
            sink.WriteU8Fixed(pair.Item1);
            sink.WriteU8Fixed(pair.Item2);
        }

        private void Write(Sink sink, Link link)
        {
            Validation.AssertValue(sink);
            Validation.AssertValueOrNull(link);

            if (link == null)
            {
                Write(sink, Code.NullYes);
                return;
            }

            var kind = link.Kind;
            Validation.Assert(kind.IsValid());
            bool haveAcct = !string.IsNullOrEmpty(link.AccountId);
            Validation.Assert(haveAcct == kind.NeedsAccount());
            Validation.AssertNonEmpty(link.Path);

            sink.WriteByte((byte)Code.UriHead);

            sink.WriteByte((byte)kind);
            if (haveAcct)
                sink.Writer.Write(link.AccountId);
            sink.Writer.Write(link.Path);

            sink.WriteByte((byte)Code.UriTail);
        }

        #endregion Write Methods

        #region Read Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception ThrowRead()
        {
            throw new InvalidDataException("Bad data stream");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Exception ThrowRead(string msg)
        {
            throw new InvalidDataException(msg ?? "Bad data stream");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckRead(bool cond, string msg = null)
        {
            if (!cond)
                throw ThrowRead(msg);
        }

        private object ReadNull(Source src)
        {
            Validation.AssertValue(src);
            byte b = src.ReadByte();
            CheckRead((Code)b == Code.NullYes);
            return null;
        }

        private object ReadVac(Source src)
        {
            Validation.AssertValue(src);
            throw Validation.BugExcept("Unexpected Vac");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Code ReadCode(Source src)
        {
            Validation.AssertValue(src);
            return (Code)src.ReadByte();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadCodeAndCheck(Source src, Code code)
        {
            Validation.AssertValue(src);
            Code codeRead = (Code)src.ReadByte();
            CheckRead(code == codeRead);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadU1(Source src) => src.VerifyValue().ReadByte();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private sbyte ReadI1(Source src) => (sbyte)src.VerifyValue().ReadByte();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadU2(Source src) => src.VerifyValue().ReadU2();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short ReadI2(Source src) => (short)src.VerifyValue().ReadU2();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadU4(Source src) => src.VerifyValue().ReadU4Data();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadI4(Source src) => (int)src.VerifyValue().ReadU4Data();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadU8(Source src) => src.VerifyValue().ReadU8Data();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadI8(Source src) => (long)src.VerifyValue().ReadU8Data();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadI8Fixed(Source src) => (long)src.VerifyValue().ReadU8Fixed();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReadBool(Source src)
        {
            byte b = ReadU1(src);
            CheckRead(b <= 1);
            return b > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadR4(Source src) => src.VerifyValue().ReadR4();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadR8(Source src) => src.VerifyValue().ReadR8();

        private Date ReadDateTime(Source src)
        {
            Validation.AssertValue(src);
            long ticks = (long)src.ReadU8Fixed();
            CheckRead((ulong)ticks <= ChronoUtil.TicksMax);
            return Date.FromTicks(ticks);
        }

        private Time ReadTimeSpan(Source src)
        {
            Validation.AssertValue(src);

            // These asserts validate the lack of range checks.
            Validation.Assert(Time.MinValue.Ticks == long.MinValue);
            Validation.Assert(Time.MaxValue.Ticks == long.MaxValue);

            // Allow compression.
            long ticks = ReadI8(src);
            // Not needed - see the asserts above.
            // CheckRead(Time.MinValue.Ticks <= ticks);
            // CheckRead(Time.MaxValue.Ticks >= ticks);
            return new Time(ticks);
        }

        private Integer ReadInteger(Source src)
        {
            Validation.AssertValue(src);

            ReadCodeAndCheck(src, Code.IntHead);
            int len = ReadI4(src);
            CheckRead(len > 0);

            byte[] data = src.Reader.ReadBytes(len);
            var res = new Integer(data);

            ReadCodeAndCheck(src, Code.IntTail);
            return res;
        }

        private string ReadString(Source src)
        {
            Validation.AssertValue(src);
            Code code = ReadCode(src);
            if (code == Code.NullYes)
                return null;
            CheckRead(code == Code.StrHead);
            string res = src.Reader.ReadString();
            Validation.Assert(res != null);
            code = ReadCode(src);
            CheckRead(code == Code.StrTail);
            return res;
        }

        private Guid ReadGuid(Source src)
        {
            Validation.AssertValue(src);
            ulong bits0 = src.ReadU8Fixed();
            ulong bits1 = src.ReadU8Fixed();
            return NumUtil.ToGuid(bits0, bits1);
        }

        private Link ReadLink(Source src)
        {
            Validation.AssertValue(src);

            Code code = ReadCode(src);
            if (code == Code.NullYes)
                return null;

            Link res;
            if (src.Version < k_verNewUri)
            {
                // Old more verbose format.
                CheckRead(code == Code.UriHeadOld);

                var kind = (LinkKind)ReadU1(src);
                if (kind == 0)
                {
                    // Zero kind used to produce "blank", now that uri types are opt, produces null.
                    res = null;
                }
                else
                {
                    CheckRead(kind.IsValid());

                    var paddingByte = ReadU1(src);
                    const byte PaddingByteForLegacyData = 255;
                    const byte ObsoleteCategoryLimit = 7;
                    if (paddingByte != PaddingByteForLegacyData)
                        CheckRead(paddingByte < ObsoleteCategoryLimit);

                    bool haveAcct = ReadBool(src);
                    CheckRead(haveAcct == kind.NeedsAccount());

                    string acct = null;
                    if (haveAcct)
                    {
                        acct = src.Reader.ReadString();
                        CheckRead(!string.IsNullOrEmpty(acct));
                    }

                    var path = src.Reader.ReadString();
                    CheckRead(!string.IsNullOrEmpty(path));
                    res = Link.Create(kind, acct, path);
                }

                var kindEnd = (LinkKind)(ReadU1(src) ^ 0xF0);
                CheckRead(kindEnd == kind);

                ReadCodeAndCheck(src, Code.UriTailOld);
            }
            else
            {
                // New format.
                CheckRead(code == Code.UriHead);

                var kind = (LinkKind)ReadU1(src);
                CheckRead(kind.IsValid());

                string acct = null;
                if (kind.NeedsAccount())
                {
                    acct = src.Reader.ReadString();
                    CheckRead(!string.IsNullOrEmpty(acct));
                }

                var path = src.Reader.ReadString();
                CheckRead(!string.IsNullOrEmpty(path));
                res = Link.Create(kind, acct, path);

                ReadCodeAndCheck(src, Code.UriTail);
            }

            return res;
        }

        #endregion Read Methods
    }

    /// <summary>
    /// Class used to specify settings for .rbin file encoding used by
    /// <see cref="TryWriteFull(Stream, DType, object, RbinSettings)"/>.
    /// This includes whether to split top level sequences into chunks, and how large the chunks can be.
    /// It also includes compression settings.
    /// </summary>
    public sealed class RbinSettings
    {
        /// <summary>
        /// Default settings. Chunks sequences by item count of 5000, or 60K bytes.
        /// REVIEW: What should the defaults be? We don't want it too close to the brotli page size
        /// (about 64K) so that we frequently spill slightly into two brotli pages.
        /// </summary>
        public static readonly RbinSettings Default = new RbinSettings(
            chunkSizeBytes: 15L << 12, chunkSizeItems: 5000);

        /// <summary>
        /// No chunking settings.
        /// </summary>
        public static readonly RbinSettings NoChunk = new RbinSettings(-1, -1);

        /// <summary>
        /// If positive, indicates a chunk size by number of bytes.
        /// The chunk may be larger than this value if the last item added
        /// pushes the chunk size past this limit.
        /// 
        /// If both this and <see cref="ChunkSizeItems"/> are positive, the value that is
        /// reached first is used to limit the chunk size.
        /// </summary>
        public long ChunkSizeBytes { get; }

        /// <summary>
        /// If positive, indicates a chunk size by number of items in the sequence.
        /// 
        /// If both this and <see cref="ChunkSizeBytes"/> are positive, the value that is
        /// reached first is used to limit the chunk size.
        /// </summary>
        public long ChunkSizeItems { get; }

        /// <summary>
        /// Whether these settings indicate that chunking should be performed, when the top level type is
        /// a sequence.
        /// </summary>
        public bool IsChunked => ChunkSizeBytes > 0 || ChunkSizeItems > 0;

        /// <summary>
        /// The compression kind to use. This is guaranteed to be valid but not necessarily "supported".
        /// That is, <c>Compression.IsSupported()</c> may be false even when <c>Compression</c> is not zero.
        /// </summary>
        public CompressionKind Compression { get; }

        /// <summary>
        /// Default compression is <see cref="CompressionKind.Brotli"/>.
        /// </summary>
        public RbinSettings(long chunkSizeBytes, long chunkSizeItems,
            CompressionKind comp = CompressionKind.Brotli)
        {
            Validation.BugCheckParam(comp.IsValid(), nameof(comp));

            ChunkSizeBytes = chunkSizeBytes;
            ChunkSizeItems = chunkSizeItems;
            Compression = comp;
        }

        /// <summary>
        /// Return an <see cref="RbinSettings"/> with the indicated compression and getting other options
        /// from this instance.
        /// </summary>
        public RbinSettings SetCompression(CompressionKind comp)
        {
            Validation.BugCheckParam(comp.IsValid(), nameof(comp));

            if (Compression == comp)
                return this;
            return new RbinSettings(ChunkSizeBytes, ChunkSizeItems, comp);
        }
    }
}
