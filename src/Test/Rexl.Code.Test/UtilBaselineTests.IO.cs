// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rexl.IO;
using Microsoft.Rexl.Private;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

partial class UtilBaselineTests
{
    [TestMethod]
    public async Task BlockWriteAppendTest()
    {
        int count = await DoBaselineTestsAsync(RunAsync, @"Util\BlockWriteAppend.txt");
        Assert.AreEqual(1, count);

        async Task RunAsync(string pathHead, string pathTail, string text, bool options)
        {
            var buf = new byte[11];

            using var mem = new MemoryStream();
            using (var writer = new TestBlockAppendStream(16, mem))
            {
                Assert.IsFalse(writer.CanSeek);
                Assert.IsFalse(writer.CanRead);
                Assert.IsTrue(writer.CanWrite);
                Assert.IsFalse(writer.CanTimeout);
                Assert.AreEqual(0, writer.Length);
                Assert.AreEqual(0, writer.Position);
                Assert.ThrowsException<NotSupportedException>(() => writer.Position = 5);
                Assert.ThrowsException<NotSupportedException>(() => writer.SetLength(5));
                Assert.ThrowsException<NotSupportedException>(() => writer.Seek(3, SeekOrigin.Begin));
                Assert.ThrowsException<NotSupportedException>(() => writer.Read(buf, 0, 1));

                using (var wrt = new StreamWriter(writer, new UTF8Encoding(false, true), leaveOpen: true))
                {
                    wrt.Write("** Begin ***\r\n");
                    wrt.Flush();
                    wrt.Write("The quick brown fox jumped over the lazy dog.\r\n");
                    wrt.Flush();
                    wrt.Write("Some");
                    wrt.Flush();
                    wrt.Write(" short");
                    wrt.Flush();
                    wrt.Write(" text");
                    wrt.Flush();
                    wrt.Write(".\r\n");
                }

                writer.WriteByte(0xFF);

                for (int i = 0; i < buf.Length; i++)
                    buf[i] = (byte)(16 * (i + 1));

                writer.Write(buf, 0, 0);
                writer.Write(buf, 0, buf.Length);
                writer.Write(buf.AsSpan(0, 0));
                writer.Write(buf);

                await writer.WriteAsync(buf, 0, 0);
                await writer.WriteAsync(buf, 0, buf.Length);
                await writer.WriteAsync(buf, 0, buf.Length);
                await writer.WriteAsync(buf, 0, buf.Length);
                await writer.FlushAsync();
                await writer.WriteAsync(buf.AsMemory(0, 0));
                await writer.WriteAsync(buf);
                await writer.WriteAsync(buf);
                await writer.WriteAsync(buf);
            }

            var bytes = mem.GetBuffer().AsMemory(0, (int)mem.Length);
            WriteBytes(bytes);
        }
    }

    [TestMethod]
    public async Task BlockWriteSeekTest()
    {
        int count = await DoBaselineTestsAsync(RunAsync, @"Util\BlockWriteSeek.txt");
        Assert.AreEqual(1, count);

        async Task RunAsync(string pathHead, string pathTail, string text, bool options)
        {
            var buf = new byte[11];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)(16 * (i + 1));

            using var mem = new MemoryStream();
            await Fill(4, 16, mem, buf, Dump);
            var data = mem.ToArray();

            foreach (int cap in new[] { 8, 4, 2, 1, 5, 11, 256 })
            {
                foreach (var cblk in new[] { 4, 1, 0, 100 })
                {
                    using var mem2 = new MemoryStream();
                    await Fill(cblk, cap, mem2, buf, null);
                    var data2 = mem2.ToArray();

                    int len = data.Length;
                    if (len != data2.Length)
                        Sink.WriteLine("Different lengths for cap={0} cblk={1}: {2} vs {3}", cap, cblk, len, data2.Length);
                    else
                    {
                        for (int i = 0; i < len; i++)
                        {
                            if (data[i] != data2[i])
                            {
                                Sink.WriteLine("Different data for cap={0} cblk={1}:", cap, cblk);
                                Dump(mem2);
                                break;
                            }
                        }
                    }
                }
            }

            void Dump(MemoryStream mem)
            {
                ReadOnlyMemory<byte> bytes = mem.GetBuffer().AsMemory(0, (int)mem.Length);
                WriteBytes(bytes);
            }

            static async Task Fill(int numBlks, int cap, MemoryStream mem, byte[] buf, Action<MemoryStream> dump)
            {
                await using (var writer = new TestBlockSeekStream(numBlks, cap, mem))
                {
                    Assert.IsTrue(writer.CanSeek);
                    Assert.IsFalse(writer.CanRead);
                    Assert.IsTrue(writer.CanWrite);
                    Assert.IsFalse(writer.CanTimeout);
                    Assert.AreEqual(0, writer.Length);
                    Assert.AreEqual(0, writer.Position);
                    Assert.ThrowsException<ArgumentOutOfRangeException>(() => writer.Position = 5);
                    Assert.ThrowsException<NotSupportedException>(() => writer.SetLength(5));
                    Assert.ThrowsException<NotSupportedException>(() => writer.Read(buf, 0, 1));

                    using (var wrt = new StreamWriter(writer, new UTF8Encoding(false, true), leaveOpen: true))
                    {
                        wrt.Write("** Begin ***\r\n");
                        wrt.Flush();
                        wrt.Write("The quick brown fox jumped over the lazy dog.\r\n");
                        wrt.Flush();
                        wrt.Write("Some");
                        wrt.Flush();
                        wrt.Write(" short");
                        wrt.Flush();
                        wrt.Write(" text");
                        wrt.Flush();
                        wrt.Write(".\r\n");
                    }

                    writer.WriteByte(0xFF);

                    writer.Write(buf, 0, 0);
                    writer.Write(buf, 0, buf.Length);
                    writer.Write(buf.AsSpan(0, 0));
                    writer.Write(buf);

                    await writer.WriteAsync(buf, 0, 0);
                    await writer.WriteAsync(buf, 0, buf.Length);
                    await writer.WriteAsync(buf, 0, buf.Length);
                    await writer.WriteAsync(buf, 0, buf.Length);
                    await writer.FlushAsync(force: false, default);
                    await writer.FlushAsync(force: true, default);
                    await writer.WriteAsync(buf.AsMemory(0, 0));
                    await writer.WriteAsync(buf);
                    writer.Flush(force: true);
                    await writer.WriteAsync(buf);
                    await writer.WriteAsync(buf);

                    await writer.FlushAsync(force: true, default);
                    await writer.FlushAsync(force: true, default);
                    dump?.Invoke(mem);

                    long posLim = writer.Length;
                    long pos = 3;
                    var d = new byte[1] { (byte)'X' };
                    bool useAsync = true;
                    for (int i = 7; pos < posLim; pos += ++i)
                    {
                        writer.Seek(pos, SeekOrigin.Begin);
                        if (useAsync)
                            await writer.WriteAsync(d);
                        else
                            writer.WriteByte(d[0]);
                        useAsync = !useAsync;
                    }
                }

                dump?.Invoke(mem);
            }
        }
    }

    private void WriteBytes(ReadOnlyMemory<byte> bytes)
    {
        Sink.WriteLine("Length: {0}", bytes.Length);
        for (int ib = 0; ib < bytes.Length;)
        {
            int lim = Math.Min(ib + 16, bytes.Length);
            for (int i = 0; i < 16; i++)
            {
                int p = ib + i;
                if (p < lim)
                    Sink.Write(" {0:X02}", bytes.Span[ib + i]);
                else
                    Sink.Write("   ");
            }
            Sink.Write(" | ");
            for (; ib < lim; ib++)
            {
                char ch = (char)bytes.Span[ib];
                if (ch < ' ' || (int)ch >= 0x7F)
                    ch = '.';
                Sink.Write(ch);
            }
            Sink.WriteLine();
        }
    }

    [TestMethod]
    public void UserPathTest()
    {
        int count = DoBaselineTests(Run, @"Util\UserPath.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            var sb = new StringBuilder();
            var both = PathFlags.Abs | PathFlags.Rel;
            foreach (var line in SplitLines(text))
            {
                string path;
                string ctx;
                string norm;
                PathFlags flags;

                int ich = line.IndexOf(',');
                if (ich < 0)
                {
                    path = line.Trim();
                    ctx = null;
                    norm = UserPath.Normalize(path, out flags);
                    var norm2 = UserPath.Normalize(path, ctx, out var flags2);
                    Assert.AreEqual(norm, norm2);
                    Assert.AreEqual(flags, flags2);
                }
                else
                {
                    ctx = line.Substring(0, ich).Trim();
                    path = line.Substring(ich + 1).Trim();
                    norm = UserPath.Normalize(path, ctx, out flags);
                }
                Assert.AreNotEqual(both, flags & both);

                Sink.WriteLine("{0}{1}{2}{3}{4} [{5}]'{6}' => '{7}'",
                    (flags & PathFlags.Abs) != 0 ? 'A' : '_',
                    (flags & PathFlags.Rel) != 0 ? 'R' : '_',
                    (flags & PathFlags.Dir) != 0 ? 'D' : '_',
                    (flags & PathFlags.Shr) != 0 ? 'S' : '_',
                    (flags & PathFlags.Pop) != 0 ? 'P' : '_',
                    ctx, path, norm);
            }
        }
    }

    /// <summary>
    /// Used to test the <see cref="BlockWriteStream"/> functionality. When a block is
    /// filled or a hard flush is performed, the dirty portion of the current block is
    /// written. A new block is only requested at the end of the current block.
    /// </summary>
    private sealed class TestBlockAppendStream : BlockWriteStream
    {
        private readonly MemoryStream _mem;

        public TestBlockAppendStream(int cbBlock, MemoryStream mem = null)
            : base(cbBlock)
        {
            _mem = mem ?? new MemoryStream();
        }

        private void PushBlock(Block block)
        {
            Assert.IsNotNull(block);
            Assert.IsTrue(block.Size > 0);
            Assert.IsTrue(block.IsDirty);

            long pos = block.Pos;
            Assert.IsTrue(pos <= _mem.Length & _mem.Length <= pos + block.Size);

            var (min, lim) = block.DirtyRange;
            Assert.AreEqual(_mem.Length, pos + min);
            Assert.AreEqual(block.Size, lim);

            using (var src = block.StreamForRead(min, lim - min))
                src.CopyTo(_mem);

            Assert.AreEqual(pos + block.Size, _mem.Length);
        }

        protected override Block GetBlock(long pos, Block block)
        {
            Assert.AreEqual(Position, pos);
            PushBlock(block);
            block.Reset(pos, 0);
            return block;
        }

        protected override ValueTask<Block> GetBlockAsync(long pos, Block block, CancellationToken ct)
        {
            return ValueTask.FromResult(GetBlock(pos, block));
        }

        protected override void FlushCore(Block block, bool force)
        {
            Assert.IsNotNull(block);
            if (force && block.IsDirty)
            {
                PushBlock(block);
                block.Clean();
            }
        }

        protected override ValueTask FlushCoreAsync(Block block, bool force, CancellationToken ct)
        {
            FlushCore(block, force);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// For testing the <see cref="BlockWriteSeekStream"/> functionality. Multiple blocks are maintained
    /// in a cache, some may be dirty, some may be clean. The stream supports seeking anywhere within
    /// the range of what has been written (to the virtual stream) so far, whether it has been committed
    /// to eventual storage.
    /// </summary>
    private sealed class TestBlockSeekStream : BlockWriteSeekStream
    {
        private readonly MemoryStream _mem;

        public TestBlockSeekStream(int numBlock, int cbBlock, MemoryStream mem = null)
            : base(numBlock, cbBlock)
        {
            _mem = mem ?? new MemoryStream();
        }

        protected override void PushBlock(Block block)
        {
            Assert.IsTrue(block.Size > 0);
            Assert.IsTrue(block.IsDirty);
            long pos = block.Pos;
            Assert.IsTrue(Validation.IsValidIndexInclusive(pos, _mem.Length));

            var (min, lim) = block.DirtyRange;
            Assert.IsTrue(0 <= min & min < lim & lim <= block.Size);
            Assert.IsTrue(pos <= pos + block.Size - lim);

            _mem.Seek(pos + min, SeekOrigin.Begin);
            _mem.Write(block.ForRead(min, lim - min));

            block.Clean();
        }

        protected override ValueTask PushBlockAsync(Block block, CancellationToken ct)
        {
            PushBlock(block);
            return ValueTask.CompletedTask;
        }

        protected override void FetchBlock(long pos, Span<byte> buf)
        {
            Assert.IsTrue(Validation.IsValidIndexInclusive(pos, Length));
            _mem.Seek(pos, SeekOrigin.Begin);
            _mem.Read(buf);
        }

        protected override ValueTask FetchBlockAsync(long pos, Memory<byte> buf, CancellationToken ct)
        {
            Assert.IsTrue(Validation.IsValidIndexInclusive(pos, Length));
            _mem.Seek(pos, SeekOrigin.Begin);
            _mem.Read(buf.Span);
            return ValueTask.CompletedTask;
        }
    }
}
