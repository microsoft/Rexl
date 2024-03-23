// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Microsoft.Rexl.Compression;
using Microsoft.Rexl.IO;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

// Partial containing rbin file streaming reader.
public abstract partial class TypeManager
{
    /// <summary>
    /// Creates an <see cref="RbinReader"/> to read from the given <paramref name="strm"/>. The reader
    /// will call the optional <paramref name="progress"/> delegate with the indicated frequency.
    /// </summary>
    public RbinReader CreateReader(Stream strm, Action<long> progress, long freq = 10)
    {
        Validation.BugCheckValue(strm, nameof(strm));
        Validation.BugCheckValueOrNull(progress);

        return _ser.CreateReader(strm, progress, freq);
    }

    /// <summary>
    /// This is a reader for an rbin file. It exposes the type and streaming data (when available)
    /// before any values are actually read. This is useful for implementing an <see cref="ActionRunner"/>.
    /// </summary>
    public abstract class RbinReader : IDisposable
    {
        /// <summary>
        /// The type manager used by this reader.
        /// </summary>
        public TypeManager TypeManager { get; }

        /// <summary>
        /// The type of the information in the rbin stream.
        /// </summary>
        public DType Type { get; }

        /// <summary>
        /// The system type of the information in the rbin stream.
        /// </summary>
        public Type SysType { get; }

        /// <summary>
        /// When the type is a sequence type, this will contain a streaming version of the sequence,
        /// meaning that it is built incrementally by the <see cref="Run"/> method. This can be used
        /// as a streaming data value from an <see cref="ActionRunner"/>.
        /// </summary>
        public abstract IEnumerable DataStream { get; }

        private protected RbinReader(TypeManager tm, DType type, Type st)
        {
            Validation.AssertValue(tm);
            Validation.Assert(type.IsValid);
            Validation.AssertValue(st);

            TypeManager = tm;
            Type = type;
            SysType = st;
        }

        /// <summary>
        /// This should be called at most once.
        /// </summary>
        public abstract object Run();

        public abstract void Dispose();
    }

    // This partial is for reading rbin files.
    partial class Serializer
    {
        public RbinReader CreateReader(Stream strm, Action<long> progress, long freq)
        {
            Validation.AssertValue(strm);
            Validation.AssertValueOrNull(progress);

            Stream strmComp = null;
            var rdr = new BinaryReader(strm, _enc, leaveOpen: true);
            try
            {
                // Read and check header information.
                uint sig = rdr.ReadUInt32();
                CheckRead(sig == k_sigRbin, "Bad file signature");
                ushort ver = rdr.ReadUInt16();
                CheckRead(ver >= k_verReadRbin, "Can't read old file version");
                ushort verBack = rdr.ReadUInt16();
                CheckRead(verBack <= k_verSerRbin, "New file version not readable by this code");

                CheckRead(DType.TryDeserialize(rdr.ReadString(), out var type), "Decoding type failed");
                CheckRead(_tm.TryEnsureSysType(type.ItemTypeOrThis, out var stItem),
                    "Unsupported serialized type");

                var code = (Code)rdr.ReadByte();
                CheckRead(code == Code.TypTail);
                var comp = (CompressionKind)rdr.ReadByte();
                CheckRead(comp.IsValid(), "Invalid compression kind");

                if (comp != 0)
                {
                    if (!comp.IsSupported())
                        throw new IOException($"Unsupported compression kind: {comp}");
                    strmComp = comp.CreateReader(strm);
                    rdr.Dispose();
                    // Create a new binary reader that wraps and owns the decompression stream.
                    rdr = new BinaryReader(strmComp, _enc, leaveOpen: false);
                    strmComp = null;
                }

                // Read and validate the header and create the value reader, aka Source.
                using var src = CreateSourceCore(rdr);

                // Rbin files were introduced when value format version was this. The code below assumes
                // we don't have to handle earlier versions.
                CheckRead(src.Version >= 0x00010004);

                // Read the head up front. The tail code is read by the reader impl.
                ReadCodeAndCheck(src, Code.TopHead);

                var meth = _methMakeReader.MakeGenericMethod(stItem);
                var args = new object[] { src, type, progress, freq };
                var ret = (RbinReader)meth.Invoke(this, args);
                Validation.Assert(ret != null);

                // The return result (RbinReader) now owns the BinaryReader.
                rdr = null;

                return ret;
            }
            finally
            {
                rdr?.Dispose();
                strmComp?.Dispose();
            }
        }

        /// <summary>
        /// Called through the <see cref="_methMakeReader"/> MethodInfo. The <typeparamref name="TItem"/> is the system
        /// type of the item type (when a sequence, otherwise the type).
        /// </summary>
        private RbinReader MakeReaderCore<TItem>(Source src, DType type, Action<long> progress, long freq)
        {
            AssertSysType<TItem>(type.ItemTypeOrThis);
            Validation.AssertValue(src);
            Validation.Assert(src.Version >= 0x00010004);

            if (!type.IsSequence)
            {
                // Do normal reading.
                var fn = GetReaderOrNull<TItem>(type);
                if (fn == null)
                    return null;
                return new AtomicReader<TItem>(this, type, src, fn);
            }

            // Sequence type. Read the code. It tells us whether the sequence is null, chunked, or contig.
            Code code = ReadCode(src);
            if (code == Code.SeqNull)
                return new AtomicReader<IEnumerable<TItem>>(this, type, src, src => null);

            // Version 0x0001004 introduced chunking.
            bool chunked = code == Code.SeqChkd;

            if (!chunked)
                CheckRead(code == Code.SeqHead);

            var fnItem = GetReaderOrNull<TItem>(type.ItemTypeOrThis);
            CheckRead(fnItem != null, "Unsupported item type");

            if (!chunked)
                return new ContigSeqReader<TItem>(this, type, src, fnItem, progress, freq);
            return new ChunkedSeqReader<TItem>(this, type, src, fnItem, progress, freq);
        }

        /// <summary>
        /// Base implementation class for the various readers.
        /// </summary>
        private abstract class RbinReaderImpl : RbinReader
        {
            protected readonly Serializer _ser;
            protected readonly Source _src;

            // Incremented when Run is called to ensure that it is called only once.
            private int _called;

            protected RbinReaderImpl(Serializer ser, DType type, Type st, Source src)
                : base(ser.VerifyValue()._tm, type, st)
            {
                Validation.AssertValue(src);
                _ser = ser;
                _src = src;
            }

            public sealed override void Dispose()
            {
                var count = Interlocked.Increment(ref _called);
                if (count == 1)
                    Quit();
                _src.Dispose();
            }

            /// <summary>
            /// Called to quit building when the reader is disposed before <see cref="Run"/> is ever called.
            /// </summary>
            protected abstract void Quit();

            public sealed override object Run()
            {
                Validation.BugCheck(Interlocked.Increment(ref _called) == 1, "Run should only be called once");

                var res = RunCore();
                ReadCodeAndCheck(_src, Code.TopTail);
                return res;
            }

            protected abstract object RunCore();
        }

        /// <summary>
        /// An rbin reader that is atomic - no progress information or incremental results.
        /// </summary>
        private sealed class AtomicReader<TVal> : RbinReaderImpl
        {
            private readonly Func<Source, TVal> _fn;

            public override IEnumerable DataStream => null;

            public AtomicReader(Serializer ser, DType type, Source src, Func<Source, TVal> fn)
                : base(ser, type, typeof(TVal), src)
            {
                Validation.AssertValue(fn);
                _fn = fn;
            }

            protected override void Quit()
            {
                // Nothing to do.
            }

            protected override object RunCore()
            {
                return _fn(_src);
            }
        }

        /// <summary>
        /// An rbin reader for a sequence.
        /// </summary>
        private abstract class SeqReader<TItem> : RbinReaderImpl
        {
            protected readonly Func<Source, TItem> _fnItem;
            protected readonly Action<long> _progress;
            protected readonly long _freq;

            protected SeqReader(Serializer ser, DType type, Source src, Func<Source, TItem> fnItem,
                    Action<long> progress, long freq)
                : base(ser, type, typeof(IEnumerable<TItem>), src)
            {
                Validation.AssertValue(fnItem);
                Validation.AssertValueOrNull(progress);
                _fnItem = fnItem;
                _progress = progress;
                _freq = freq;
                if (_freq <= 0)
                    _freq = 1;
            }
        }

        /// <summary>
        /// An rbin reader for a contiguous (non-chunked) sequence.
        /// </summary>
        private sealed class ContigSeqReader<TItem> : SeqReader<TItem>
        {
            private readonly long _count;
            private readonly BuildableSequence<TItem>.Builder _bldr;
            private readonly BuildableSequence<TItem> _seq;

            public override IEnumerable DataStream => _seq;

            public ContigSeqReader(Serializer ser, DType type, Source src, Func<Source, TItem> fnItem,
                    Action<long> progress, long freq)
                : base(ser, type, src, fnItem, progress, freq)
            {
                Validation.Assert(_src.Version >= 0x00010004);

                _count = _ser.ReadI8Fixed(_src);
                CheckRead(_count >= 0);

                _bldr = BuildableSequence<TItem>.Builder.Create(_count, out _seq);
            }

            protected override void Quit()
            {
                // RunCore will never be called, so need to quit the builder.
                _bldr.Quit(null);
            }

            protected override object RunCore()
            {
                var bldr = _bldr;

                try
                {
                    // Use locals for a bunch of fields.
                    var fnItem = _fnItem;
                    var src = _src;
                    var count = _count;
                    var progress = _progress;
                    long freq = _freq;

                    long counter = freq;
                    for (int i = 0; i < count; i++)
                    {
                        bldr.Add(fnItem(src));

                        if (progress != null && --counter <= 0)
                        {
                            progress(i + 1);
                            counter = freq;
                        }
                    }
                    ReadCodeAndCheck(src, Code.SeqTail);
                    progress?.Invoke(count);
                }
                catch (Exception ex)
                {
                    bldr.Quit(ex);
                    Validation.Assert(!bldr.IsActive);
                    throw;
                }

                Validation.Assert(bldr.IsActive);
                bldr.Done();
                Validation.Assert(!bldr.IsActive);
                return _seq;
            }
        }

        /// <summary>
        /// An rbin reader for a chunked sequence.
        /// </summary>
        private sealed class ChunkedSeqReader<TItem> : SeqReader<TItem>
        {
            private readonly long _countItemAll;
            private readonly long _countChunkAll;
            private readonly long _bytesUntilFoot;

            // REVIEW: Fix BuildableSequence to handle arbitrarily large sequences....
            private readonly BuildableSequence<TItem>.Builder _bldr;
            private readonly BuildableSequence<TItem> _seq;

            public override IEnumerable DataStream => _seq;

            public ChunkedSeqReader(Serializer ser, DType type, Source src, Func<Source, TItem> fnItem,
                    Action<long> progress, long freq)
                : base(ser, type, src, fnItem, progress, freq)
            {
                _countItemAll = _ser.ReadI8Fixed(_src);
                CheckRead(_countItemAll >= 0);
                _countChunkAll = _ser.ReadI8Fixed(_src);
                CheckRead(_countChunkAll >= 0);
                CheckRead(_countChunkAll <= _countItemAll);
                _bytesUntilFoot = _ser.ReadI8Fixed(_src);
                CheckRead(_bytesUntilFoot >= 0);

                _bldr = BuildableSequence<TItem>.Builder.Create(_countItemAll, out _seq);
            }

            protected override void Quit()
            {
                // RunCore will never be called, so need to quit the builder.
                _bldr.Quit(null);
            }

            protected override object RunCore()
            {
                var bldr = _bldr;

                try
                {
                    // Use locals for a bunch of fields.
                    var fnItem = _fnItem;
                    var ser = _ser;
                    var src = _src;
                    var countItemAll = _countItemAll;
                    var countChunkAll = _countChunkAll;
                    var progress = _progress;
                    long freq = _freq;

                    long iitem = 0;
                    long counter = freq;
                    long cbTot = 0;

                    for (long ichunk = 0; ichunk < countChunkAll; ichunk++)
                    {
                        ReadCodeAndCheck(src, Code.ChkHead);
                        cbTot += sizeof(byte);
                        var indChunk = ser.ReadI8Fixed(src);
                        cbTot += sizeof(long);
                        CheckRead(indChunk == iitem);

                        var countChunk = ser.ReadI8Fixed(src);
                        cbTot += sizeof(long);
                        CheckRead(countChunk > 0);
                        CheckRead(countChunk <= countItemAll - iitem);
                        var sizeChunk = ser.ReadI8Fixed(src);
                        cbTot += sizeof(long);
                        CheckRead(sizeChunk > 0);

                        var comp = (CompressionKind)ser.ReadU1(src);
                        cbTot += sizeof(byte);
                        CheckRead(comp.IsValid(), "Invalid compression kind");

                        {
                            ReadSubStream strmChk = null;
                            Stream strmComp = null;
                            BinaryReader rdrChk = null;
                            Source srcChk = null;
                            try
                            {
                                Func<long> getPos;
                                if (comp != 0)
                                {
                                    if (!comp.IsSupported())
                                        throw new IOException($"Unsupported compression kind: {comp}");
                                    strmComp = comp.CreateReader(src.BaseStream, sizeChunk, out getPos);
                                }
                                else
                                {
                                    // Create a "sub-stream" that enforces that the bounds of the chunk aren't
                                    // violated and so we can verify the correct position at the end of the chunk.
                                    strmChk = new ReadSubStream(src.BaseStream, leaveOpen: true, sizeChunk);
                                    getPos = strmChk.GetOuterByteCount;
                                }

                                // Create the chunk reader and source.
                                rdrChk = new BinaryReader(strmComp ?? strmChk, _ser._enc, leaveOpen: true);
                                srcChk = new Source(src.Version, rdrChk);

                                for (long i = 0; i < countChunk; i++)
                                {
                                    long index = bldr.Add(fnItem(srcChk));
                                    Validation.Assert(iitem == index);
                                    iitem++;
                                    if (progress != null && --counter <= 0)
                                    {
                                        progress(iitem);
                                        counter = freq;
                                    }
                                }

                                long cbRead = getPos();
                                CheckRead(sizeChunk == cbRead);
                                cbTot += cbRead;
                            }
                            finally
                            {
                                srcChk?.Dispose();
                                rdrChk?.Dispose();
                                strmComp?.Dispose();
                                strmChk?.Dispose();
                            }
                        }

                        ReadCodeAndCheck(src, Code.ChkFoot);
                        cbTot += sizeof(byte);
                        var sizeFoot = ser.ReadI8Fixed(src);
                        cbTot += sizeof(long);
                        CheckRead(sizeFoot == sizeChunk + sizeof(long) * 3 + sizeof(byte) * 2);
                        ReadCodeAndCheck(src, Code.ChkTail);
                        cbTot += sizeof(byte);
                    }
                    CheckRead(iitem == countItemAll);
                    CheckRead(_bytesUntilFoot == cbTot);

                    ReadCodeAndCheck(src, Code.SeqTail);
                    progress?.Invoke(countItemAll);
                }
                catch (Exception ex)
                {
                    bldr.Quit(ex);
                    Validation.Assert(!bldr.IsActive);
                    throw;
                }

                Validation.Assert(bldr.IsActive);
                bldr.Done();
                Validation.Assert(!bldr.IsActive);
                return _seq;
            }
        }
    }
}
