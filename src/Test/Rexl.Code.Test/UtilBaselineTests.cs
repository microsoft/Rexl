// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Compression;
using Microsoft.Rexl.IO;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Sequence;
using Microsoft.Rexl.Utility;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using TOpts = System.Boolean;

[TestClass]
public partial class UtilBaselineTests : RexlTestsBaseType<bool>
{
    [TestMethod]
    public void IndexedSequenceTest()
    {
        int count = DoBaselineTests(Run, @"Util\IndexedSequence.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var indsCurs = new[] { 1, 4, 2, 2, 3, 0, 17, 5, 7, 6 };

            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            var sb3 = new StringBuilder();
            var sb4 = new StringBuilder();
            Thread thd1, thd2, thd3, thd4;
            var bldr = IndexedSequence<string>.Builder.Create("<missing>", out var seq);
            thd1 = new Thread(() =>
            {
                using var ator = seq.GetIndexedEnumerator();
                while (ator.MoveNext())
                    sb1.AppendFormat("{0}) {1}", ator.Index, ator.Value).AppendLine();
            });

            thd2 = new Thread(() =>
            {
                int ind = 0;
                foreach (var v in seq)
                    sb2.AppendFormat("{0}) {1}", ind++, v).AppendLine();
            });

            thd3 = new Thread(() =>
            {
                // Use covariance.
                using var ator = ((IIndexedEnumerable<object>)seq).GetIndexedEnumerator();
                while (ator.MoveNext())
                    sb3.AppendFormat("{0}) {1}", ator.Index, ator.Value).AppendLine();
            });

            thd4 = new Thread(() =>
            {
                // Use a cursor.
                using var curs = seq.GetCursor();
                foreach (var ind in indsCurs)
                {
                    if (curs.MoveTo(ind))
                        sb4.AppendFormat("{0}) {1}", curs.Index, curs.Value).AppendLine();
                    else
                        sb4.AppendFormat("{0}) <failed!>", ind).AppendLine();
                }
            });

            thd1.Start();
            thd2.Start();
            thd3.Start();
            thd4.Start();

            // Add items and signal completion.
            Thread.Sleep(10); bldr.Add(2, "2");
            Thread.Sleep(10); bldr.Add(1, "1");
            Thread.Sleep(10); bldr.Add(4, "4");
            Thread.Sleep(10); bldr.Add(0, "0");
            bldr.Done(7);

            thd1.Join();
            thd2.Join();
            thd3.Join();
            thd4.Join();

            Sink.WriteLine("=== Thread 1:");
            Sink.Write(sb1.ToString());
            Sink.WriteLine("=== Thread 2:");
            Sink.Write(sb2.ToString());
            Sink.WriteLine("=== Thread 3:");
            Sink.Write(sb3.ToString());
            Sink.WriteLine("=== Thread 4:");
            Sink.Write(sb4.ToString());

            Sink.WriteLine("=== Main Thread Indexed:");
            using (var ator = seq.GetIndexedEnumerator())
            {
                while (ator.MoveNext())
                    Sink.WriteLine("{0}) {1}", ator.Index, ator.Value);
            }
            Sink.WriteLine("=== Main Thread Ordered:");
            int i = 0;
            foreach (var v in seq)
                Sink.WriteLine("{0}) {1}", i++, v);
            Sink.WriteLine("=== Main Thread Cursor:");
            using (var curs = seq.GetCursor())
            {
                foreach (var ind in indsCurs)
                {
                    if (curs.MoveTo(ind))
                        Sink.WriteLine("{0}) {1}", curs.Index, curs.Value);
                    else
                        Sink.WriteLine("{0}) <failed!>", ind);
                }
            }
        }
    }

    [TestMethod]
    public void FlatteningSequenceTest()
    {
        int count = DoBaselineTests(
            Run, @"Util\FlatteningSequence.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            Thread thd1, thd2;
            var bldr = FlatteningSequence<string>.CreateBuilder(out var seq);
            thd1 = new Thread(() =>
            {
                using var ator = seq.GetEnumerator();
                int i = 0;
                while (ator.MoveNext())
                    sb1.AppendFormat("{0}) {1}", i++, ator.Current).AppendLine();
                ator.Dispose(); // Test idempotency.
            });

            thd2 = new Thread(() =>
            {
                using var ator = seq.GetEnumeratorWithKey();
                int i = 0;
                while (ator.MoveNext())
                    sb2.AppendFormat("{0}) [{1}] {2}", i++, ator.Current.key, ator.Current.value).AppendLine();
                ator.Dispose(); // Test idempotency.
            });

            thd1.Start();
            thd2.Start();

            void Add(long key, params string[] values)
            {
                bldr.Add(key, Immutable.Array<string>.Create(values));
            }

            // Add items and signal completion.
            Thread.Sleep(10); Add(2, "2.1", "2.2", "2.3");
            Thread.Sleep(10); Add(1);
            Thread.Sleep(10); Add(4, "4.1", "4.2");
            Thread.Sleep(10); Add(0, "0.1");
            bldr.Done(7);

            thd1.Join();
            thd2.Join();

            Sink.WriteLine("=== Thread 1:");
            Sink.Write(sb1.ToString());
            Sink.WriteLine("=== Thread 2:");
            Sink.Write(sb2.ToString());
        }
    }

    [TestMethod]
    public void IndexedSequenceSnapshotTest()
    {
        int count = DoBaselineTests(
            Run, @"Util\IndexedSequenceSnapshot.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            void WriteSnap(IndexedSequence<int> seq)
            {
                var snap = seq.Snap();
                if (snap != null)
                {
                    var icc = snap as ICanCount;
                    Assert.IsNotNull(icc);
                    Assert.IsTrue(icc.TryGetCount(out long c));
                    Sink.WriteLine("    Snapshot count: {0}", c);
                    Sink.WriteLine("    Snapshot: {0}", string.Join(", ", snap));

                    // Test ICursor and IEnumerator consistency.
                    Assert.IsTrue(snap.SequenceEqual(seq.Snap()));
                    var arr = snap.ToArray();
                    var ic = snap as ICursorable<int>;
                    Assert.IsNotNull(ic);
                    var cur = ic.GetCursor();
                    for (long i = c; --i >= 0;)
                    {
                        Assert.IsTrue(cur.MoveTo(i));
                        Assert.AreEqual(arr[i], cur.Value);
                    }
                    Assert.IsFalse(cur.MoveTo(c + 1));
                }
                else
                {
                    Sink.WriteLine("    Snapshot count: 0");
                    Sink.WriteLine("    Snapshot: <null>");
                }
            }

            void Add(IndexedSequence<int>.Builder bldr, IndexedSequence<int> seq, int ind)
            {
                Sink.WriteLine("=== Add {0}", ind);
                bldr.Add(ind, ind);
                WriteSnap(seq);
            }

            {
                var bldr = IndexedSequence<int>.Builder.Create(-1, out var seq);
                Sink.WriteLine("====== Starting");
                WriteSnap(seq);
                Add(bldr, seq, 1);
                Add(bldr, seq, 0);
                Add(bldr, seq, 2);
                Add(bldr, seq, 4);
                Add(bldr, seq, 3);
                Add(bldr, seq, 6);
                bldr.Done(7);

                Sink.WriteLine("=== Final Sequence:");
                using (var ator = seq.GetIndexedEnumerator())
                {
                    while (ator.MoveNext())
                        Sink.WriteLine("{0}) {1}", ator.Index, ator.Value);
                }
                WriteSnap(seq);
            }

            {
                var bldr = IndexedSequence<int>.Builder.Create(-1, out var seq);
                Sink.WriteLine("====== Starting");
                WriteSnap(seq);
                Add(bldr, seq, 4);
                Add(bldr, seq, 1);
                Add(bldr, seq, 0);
                Add(bldr, seq, 2);
                Add(bldr, seq, 3);
                bldr.Done(7);

                Sink.WriteLine("=== Final Sequence:");
                using (var ator = seq.GetIndexedEnumerator())
                {
                    while (ator.MoveNext())
                        Sink.WriteLine("{0}) {1}", ator.Index, ator.Value);
                }
                WriteSnap(seq);
            }
        }

        // Non-baseline tests.
        {
            // Test more elements than fit in a single uint bitset.
            var bldr = IndexedSequence<int>.Builder.Create(-1, out var seq);
            for (int i = 160; --i >= 0;)
                bldr.Add(i, i);

            var snap = seq.Snap();
            Assert.IsTrue(snap.SequenceEqual(Enumerable.Range(0, 160)));
            var snap2 = ((ICanSnap<int>)snap).Snap();
            Assert.AreEqual(snap, snap2);

            bldr.Add(160, 160);
            bldr.Add(162, 162);
            snap = seq.Snap();
            Assert.IsTrue(snap.SequenceEqual(Enumerable.Range(0, 161)));
            Assert.AreNotEqual(snap2, snap);

            bldr.Add(161, 161);
            for (int i = 163; i < 195; i++)
                bldr.Add(i, i);
            bldr.Add(197, 197);
            snap = seq.Snap();
            Assert.AreNotEqual(snap2, snap);
            Assert.IsTrue(snap.SequenceEqual(Enumerable.Range(0, 195)));

            bldr.Done(200);
            snap = seq.Snap();
            Assert.AreNotEqual(snap2, snap);
            snap2 = ((ICanSnap<int>)snap).Snap();
            Assert.AreEqual(snap, snap2);
        }

        {
            // Test snapshot of a failed seq.
            var bldr = IndexedSequence<int>.Builder.Create(-1, out var seq);
            bldr.Add(1, 1);
            bldr.Add(0, 0);
            bldr.Add(4, 4);
            bldr.Quit(new OperationCanceledException("Canceling build"));
            var snap = seq.Snap();
            Assert.AreEqual(seq, snap);
        }

        {
            // Coverage for Snapshot enumerator/cursor.
            var bldr = IndexedSequence<int>.Builder.Create(-1, out var seq);
            bldr.Add(1, 1);
            bldr.Add(0, 0);
            bldr.Add(4, 4);
            bldr.Add(2, 2);

            var snap = seq.Snap();
            var ic = snap as ICursorable<int>;
            Assert.IsNotNull(ic);
            var cur = ic.GetCursor();
            var arr = snap.ToArray();

            for (long i = 3; --i >= 0;)
            {
                Assert.IsTrue(cur.MoveTo(i));
                Assert.AreEqual(i, cur.Index);
                Assert.AreEqual(arr[i], cur.Value);
            }
            Assert.IsFalse(cur.MoveTo(4));
        }
    }

    [TestMethod]
    public void BuildableSequenceTest()
    {
        int count = DoBaselineTests(
            Run, @"Util\BuildableSequence.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var indsCurs = new[] { 1, 4, 2, 2, 3, 0, 17, 5, 7, 6 };

            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            var sb3 = new StringBuilder();
            Thread thd1, thd2, thd3, thd4;
            var bldr = BuildableSequence<string>.Builder.Create(2, out var seq);

            // Take a snapshot.
            var snap = seq.Snap();
            Assert.IsNull(snap);

            Assert.IsFalse(seq.TryGetCount(out long c0));
            Assert.AreEqual(-1L, c0);

            thd1 = new Thread(() =>
            {
                int ind = 0;
                foreach (var v in seq)
                    sb1.AppendFormat("{0}) {1}", ind++, v).AppendLine();
            });

            thd2 = new Thread(() =>
            {
                // Use a cursor.
                using var curs = seq.GetCursor();
                foreach (var ind in indsCurs)
                {
                    if (curs.MoveTo(ind))
                        sb2.AppendFormat("{0}) {1}", curs.Index, curs.Value).AppendLine();
                    else
                        sb2.AppendFormat("{0}) <failed!>", ind).AppendLine();
                }
            });

            thd3 = new Thread(() =>
            {
                // Use covariance.
                using var curs = ((ICursorable<object>)seq).GetCursor();
                foreach (var ind in indsCurs)
                {
                    if (curs.MoveTo(ind))
                        sb3.AppendFormat("{0}) {1}", curs.Index, curs.Value).AppendLine();
                    else
                        sb3.AppendFormat("{0}) <failed!>", ind).AppendLine();
                }
            });

            thd4 = new Thread(() =>
            {
                // Call GetCount.
                c0 = seq.GetCount(null);
            });

            thd1.Start();
            thd2.Start();
            thd3.Start();
            thd4.Start();

            // Add items and signal completion.
            Thread.Sleep(10); bldr.Add("A"); bldr.Add("B"); bldr.Add("C");
            Thread.Sleep(10); bldr.Add("D"); bldr.Add("E");

            // Take a snapshot.
            snap = seq.Snap();
            var c1 = snap.Count();
            Sink.WriteLine("Snapshot count: {0}", c1);
            var icc = snap as ICanCount;
            Assert.IsTrue(icc.TryGetCount(out long c2));
            Assert.AreEqual(c1, c2);
            var c3 = icc.GetCount(null);
            Assert.AreEqual(c1, c3);

            using (var curs = (snap as ICursorable<string>).GetCursor())
            {
                Sink.WriteLine("Snapshot contents backwards");
                for (var ind = c1; --ind >= 0;)
                {
                    Assert.IsTrue(curs.MoveTo(ind));
                    Sink.WriteLine("{0}) {1}", curs.Index, curs.Value);
                }
            }

            Thread.Sleep(10); bldr.Add("F");
            Thread.Sleep(10); bldr.Add("G"); bldr.Add("H"); bldr.Add("I");
            bldr.Done();

            Sink.WriteLine("Snapshot count shouldn't change: {0}", snap.Count());

            // Get a couple snapshots. Since we're done, they should be the same.
            var snap1 = seq.Snap();
            var snap2 = seq.Snap();
            Assert.AreEqual(snap1, snap2);
            // The previous snapshot should be different.
            Assert.AreNotEqual(snap, snap2);

            snap2 = ((ICanSnap<string>)snap1).Snap();
            Assert.AreEqual(snap1, snap2);

            thd1.Join();
            thd2.Join();
            thd3.Join();
            thd4.Join();

            Sink.WriteLine("=== Thread 1:");
            Sink.Write(sb1.ToString());
            Sink.WriteLine("=== Thread 2:");
            Sink.Write(sb2.ToString());
            Sink.WriteLine("=== Thread 3:");
            Sink.Write(sb3.ToString());

            Sink.WriteLine("=== Main Thread Ordered:");
            int i = 0;
            foreach (var v in seq)
                Sink.WriteLine("{0}) {1}", i++, v);
            Sink.WriteLine("=== Main Thread Cursor:");
            using (var curs = seq.GetCursor())
            {
                foreach (var ind in indsCurs)
                {
                    if (curs.MoveTo(ind))
                        Sink.WriteLine("{0}) {1}", curs.Index, curs.Value);
                    else
                        Sink.WriteLine("{0}) <failed!>", ind);
                }
            }

            c1 = seq.Count();
            Sink.WriteLine("Full count: {0}", c1);
            Assert.IsTrue(seq.TryGetCount(out c2));
            Assert.AreEqual(c1, c2);
            c3 = seq.GetCount(null);
            Assert.AreEqual(c1, c3);

            // The GetCount call on thd4 should have gotten the same answer.
            Assert.AreEqual(c1, c0);
        }
    }

    [TestMethod]
    public void IndexedSequenceCountAheadTest_MultiLevelInput_Succeed()
    {
        // Multi level sequence with initial countAhead value.
        {
            var countAhead = 3;

            // IndexedSequence with countAhead
            var bldr1 = IndexedSequence<string>.Builder.Create("", out var seq1, countAhead);
            Assert.IsTrue(seq1.TryGetCount(out var count1));
            Assert.AreEqual(countAhead, count1);

            // IndexedSequence with counter.
            var bldr2 = IndexedSequence<string>.Builder.Create("", out var seq2, counter: seq1);
            Assert.IsTrue(seq2.TryGetCount(out var count2));
            Assert.AreEqual(countAhead, count2);

            // IndexedSequence with multi level counter
            var bldr3 = IndexedSequence<string>.Builder.Create("", out var seq3, counter: seq2);
            Assert.IsTrue(seq3.TryGetCount(out var count3));
            Assert.AreEqual(countAhead, count3);

            // Pulling sequence with counter.
            var pullingSequence = IndexedSequence<string>.Create("",
                Enumerable.Range(1, countAhead).Select((v, i) => ((long)i, v.ToString())),
                counter: seq3);
            Assert.IsTrue(pullingSequence.TryGetCount(out var count4));
            Assert.AreEqual(countAhead, count4);
        }
        // Multilevel sequence with initial counter.
        {
            // IndexedSequence with countAhead
            var bldr1 = IndexedSequence<string>.Builder.Create("", out var seq1);
            Assert.IsFalse(seq1.TryGetCount(out var count1));

            // IndexedSequence with counter.
            var bldr2 = IndexedSequence<string>.Builder.Create("", out var seq2, counter: seq1);
            Assert.IsFalse(seq2.TryGetCount(out var count2));

            bldr1.AddValue(0, "0");
            bldr1.Done(1);

            Assert.IsTrue(seq1.TryGetCount(out count1));
            Assert.AreEqual(1, count1);
            Assert.IsTrue(seq2.TryGetCount(out count2));
            Assert.AreEqual(1, count2);

            // Pulling sequence with counter.
            var pullingSequence = IndexedSequence<string>.Create("",
                Enumerable.Range(1, 1).Select((v, i) => ((long)i, v.ToString())),
                counter: seq2);
            Assert.IsTrue(pullingSequence.TryGetCount(out var count3));
            Assert.AreEqual(1, count3);
        }
    }

    [TestMethod]
    public void IndexedSequenceCountAheadTest_BuilderAndPulling_Succeed()
    {
        // Test Buildable sequence: seq count not known ahead.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq);
            Assert.IsFalse(seq.TryGetCount(out var count));
            Assert.AreNotEqual(countAhead, count);

            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(2, "4");

            using var curs = seq.GetCursor();
            for (int i = 2; i >= 0; i--)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual((2 * i).ToString(), curs.Value);
                Assert.IsFalse(seq.TryGetCount(out count));
                Assert.AreNotEqual(countAhead, count);
            }

            bldr.AddValue(3, null);
            Assert.ThrowsException<InvalidOperationException>(() => bldr.AddValue(4, 3L));
            bldr.Done(countAhead);
            curs.MoveTo(3);
            Assert.IsNull(curs.Value);
            Assert.IsTrue(seq.TryGetCount(out count));
            Assert.AreEqual(countAhead, count);
        }

        // Test Buildable sequence: seq count known ahead.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(2, "4");

            using var curs = seq.GetCursor();
            for (int i = 2; i >= 0; i--)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual((2 * i).ToString(), curs.Value);
                Assert.IsTrue(seq.TryGetCount(out count));
                Assert.AreEqual(countAhead, count);
                Assert.AreEqual(countAhead, seq.GetCount(() => { }));
            }

            bldr.AddValue(3, null);
            Assert.ThrowsException<InvalidOperationException>(() => bldr.AddValue(4, 3L));
            bldr.Done(countAhead);
            curs.MoveTo(3);
            Assert.IsNull(curs.Value);
            Assert.IsTrue(seq.TryGetCount(out count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));
        }

        // Test Pulling sequence: seq count not known ahead.
        {
            int min = 3;
            int countAhead = 20;
            var seq = IndexedSequence<int>.Create(-1, Enumerable.Range(min, countAhead).Select((v, i) => ((long)i, v)));
            Assert.IsFalse(seq.TryGetCount(out var count));
            Assert.AreNotEqual(countAhead, count);

            using var curs = seq.GetCursor();
            foreach (int i in new int[] { 5, 7, 3, 17, 0 })
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual(i + min, curs.Value);
                Assert.IsFalse(seq.TryGetCount(out count));
                Assert.AreNotEqual(countAhead, count);
            }

            // GetCount enumerates the sequence.
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));
            Assert.IsTrue(seq.TryGetCount(out count));
            Assert.AreEqual(countAhead, count);
        }

        // Test Pulling sequence: seq count known ahead.
        {
            int min = 3;
            int countAhead = 20;
            var seq = IndexedSequence<int>.Create(-1, Enumerable.Range(min, countAhead).Select((v, i) => ((long)i, v)), countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);

            // GetCount will not enumerate the sequence.
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            using var curs = seq.GetCursor();
            foreach (int i in new int[] { 5, 7, 3, 17, 0 })
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual(i + min, curs.Value);
                Assert.IsTrue(seq.TryGetCount(out count));
                Assert.AreEqual(countAhead, count);
            }

            Assert.AreEqual(countAhead, seq.GetCount(() => { }));
            Assert.IsTrue(seq.TryGetCount(out count));
            Assert.AreEqual(countAhead, count);
        }

        // Test Buildable sequence: seq count known ahead, item out of order.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(2, "4");
            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(3, "2");
            bldr.Done(countAhead);
            Assert.IsTrue(seq.TryGetCount(out count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));
        }

        // Test Buildable sequence: seq count known ahead, all slots are not filled, throw on Done with less count.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(0, "0");
            bldr.AddValue(3, "2");

            // throw on setting less value.
            Assert.ThrowsException<ArgumentException>(() => bldr.Done(3));
        }

        // Test Buildable sequence: seq count known ahead, all slots are not filled, Done with higher count.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(0, "0");
            bldr.AddValue(2, "2");

            bldr.Done(countAhead);
        }

        // Test Buildable sequence: seq count known ahead, throw on adding at larger index.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(2, "4");
            bldr.AddValue(3, null);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => bldr.AddValue(4, "5"));

            // sequence is corrupted, so adding to bldr throws exception
            Assert.ThrowsException<InvalidOperationException>(() => bldr.Done(countAhead));
        }

        // Test Buildable sequence: seq count known ahead, throw on Done with larger index.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(2, "4");
            bldr.AddValue(3, null);
            Assert.ThrowsException<ArgumentException>(() => bldr.Done(5));
        }

        // Test Buildable sequence: seq count known ahead, throw on Done with smaller index.
        {
            var countAhead = 4;
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq, countAhead);
            Assert.IsTrue(seq.TryGetCount(out var count));
            Assert.AreEqual(countAhead, count);
            Assert.AreEqual(countAhead, seq.GetCount(() => { }));

            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(2, "4");
            bldr.AddValue(3, null);
            Assert.ThrowsException<ArgumentException>(() => bldr.Done(2));
        }

    }

    [TestMethod]
    public void IndexedSequenceAddValueTest()
    {
        {
            var bldr = IndexedSequence<string>.Builder.Create("", out var seq);
            bldr.AddValue(0, "0");
            bldr.AddValue(1, "2");
            bldr.AddValue(2, "4");

            using var curs = seq.GetCursor();
            for (int i = 2; i >= 0; i--)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual((2 * i).ToString(), curs.Value);
            }

            bldr.AddValue(3, null);
            Assert.ThrowsException<InvalidOperationException>(() => bldr.AddValue(4, 3L));
            bldr.Done(4);
            curs.MoveTo(3);
            Assert.IsNull(curs.Value);
        }

        {
            var bldr = IndexedSequence<int>.Builder.Create(-1, out var seq);
            bldr.AddValue(0, 0);
            bldr.AddValue(1, 2);
            bldr.AddValue(2, 4);

            using var curs = seq.GetCursor();
            for (int i = 2; i >= 0; i--)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual(2 * i, curs.Value);
            }

            Assert.ThrowsException<InvalidOperationException>(() => bldr.AddValue(3, null));
            bldr.Quit(null);
        }

        {
            var bldr = IndexedSequence<int?>.Builder.Create(null, out var seq);
            bldr.AddValue(0, 0);
            bldr.AddValue(1, 2);
            bldr.AddValue(2, 4);

            using var curs = seq.GetCursor();
            for (int i = 2; i >= 0; i--)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual(2 * i, curs.Value.Value);
            }

            bldr.AddValue(3, null);
            Assert.ThrowsException<InvalidOperationException>(() => bldr.AddValue(4, 3L));
            bldr.Done(4);
            curs.MoveTo(3);
            Assert.IsNull(curs.Value);
        }
    }

    [TestMethod]
    public void BuildableSequenceAddTest()
    {
        {
            var bldr = BuildableSequence<string>.Builder.Create(-1, out var seq);
            bldr.Add("1");
            bldr.Add("2");

            using var curs = seq.GetCursor();
            for (int i = 2; --i >= 0;)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual((i + 1).ToString(), curs.Value);
            }

            bldr.Add(null);
            bldr.Add("12");
            bldr.Done();

            Assert.IsTrue(curs.MoveTo(2));
            Assert.IsNull(curs.Value);
            Assert.IsTrue(curs.MoveTo(3));
            Assert.AreEqual("12", curs.Value);
        }

        {
            var bldr = BuildableSequence<int>.Builder.Create(-1, out var seq);
            bldr.Add(1);
            bldr.Add(2);

            using var curs = seq.GetCursor();
            for (int i = 2; --i >= 0;)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual(i + 1, curs.Value);
            }

            bldr.Add(-17);
            bldr.Add(12);

            Assert.IsTrue(curs.MoveTo(2));
            Assert.AreEqual(-17, curs.Value);
            Assert.IsTrue(curs.MoveTo(3));
            Assert.AreEqual(12, curs.Value);

            bldr.Quit(null);
            Assert.ThrowsException<InvalidOperationException>(() => curs.MoveTo(1));
        }

        {
            var bldr = BuildableSequence<int?>.Builder.Create(-1, out var seq);
            bldr.Add(1);
            bldr.Add(2);

            using var curs = seq.GetCursor();
            for (int i = 2; --i >= 0;)
            {
                Assert.IsTrue(curs.MoveTo(i));
                Assert.AreEqual(i, curs.Index);
                Assert.AreEqual(i + 1, curs.Value);
            }

            bldr.Add(null);
            bldr.Add(12);
            bldr.Done();

            Assert.IsTrue(curs.MoveTo(2));
            Assert.AreEqual(2, curs.Index);
            Assert.IsNull(curs.Value);
            Assert.IsTrue(curs.MoveNext());
            Assert.AreEqual(3, curs.Index);
            Assert.AreEqual(12, curs.Value);
        }
    }

    [TestMethod]
    public void BuildableSequenceAddWhileIteratingTest()
    {
        var bldr = BuildableSequence<int>.Builder.Create(-1, out var seq);
        bldr.Add(1);
        bldr.Add(2);

        // Tests adding items while iterating.
        long num = 2;
        using (var ator = seq.GetEnumerator())
        {
            for (long i = 0; i < num; i++)
            {
                Assert.IsTrue(ator.MoveNext());
                bldr.Add(-ator.Current);
            }
            num += num;
        }
        using (var ator = seq.GetEnumerator())
        {
            for (long i = 0; i < num; i++)
            {
                Assert.IsTrue(ator.MoveNext());
                bldr.Add(-ator.Current);
            }
            num += num;
        }

        bldr.Done();
        var tot = seq.GetCount(null);
        Assert.AreEqual(num, tot);

        using var curs1 = seq.GetCursor();
        using var curs2 = seq.GetCursor();
        while ((num >>= 1) >= 2)
        {
            curs1.MoveTo(0);
            curs2.MoveTo(num);
            for (long i = 0; i < num; i++)
            {
                Assert.AreEqual(i, curs1.Index);
                Assert.AreEqual(i + num, curs2.Index);
                Assert.AreEqual(-curs1.Value, curs2.Value);
                Assert.IsTrue(curs1.MoveNext());
                Assert.IsTrue(curs2.MoveNext() || i + num + 1 == tot);
            }
        }
    }

    private void AreSamePartial<T>(IEnumerable<T> a, IEnumerable<T> b)
    {
        using var ator1 = a.GetEnumerator();
        using var ator2 = b.GetEnumerator();
        while (ator1.MoveNext() && ator2.MoveNext())
            Assert.AreEqual(ator1.Current, ator2.Current);
    }

    [TestMethod]
    [DataRow("int", 2_000_000, 1_000)]
    [DataRow("intq", 1_000_000, 1_000)]
    [DataRow("bool", 10_000, 1_000)]
    [DataRow("boolq", 10_000, 1_000)]
    public void BuildableSequenceScaleTest(string kind, int num, int numSnap)
    {
        switch (kind)
        {
        case "int":
            {
                var bldr = BuildableSequence<int>.Builder.Create(num, out var seq);
                IEnumerable<int> snap = null;
                for (int i = 0; i < num; i++)
                {
                    if (i == numSnap)
                        snap = seq.Snap();
                    bldr.Add(2 * i + 1);
                }
                bldr.Done();

                Assert.IsTrue(seq.TryGetCount(out var count));
                Assert.AreEqual(num, count);

                // Iterate using enumerator and cursor. Should produce the same values.
                // The latter uses the indexer of the underlying GrowableArray.
                using var ator = seq.GetEnumerator();
                using var curs = seq.GetCursor();
                for (; ; )
                {
                    bool a = ator.MoveNext();
                    bool b = curs.MoveNext();
                    Assert.AreEqual(a, b);
                    if (!a)
                        break;
                    Assert.AreEqual(ator.Current, curs.Value);
                }

                var counter = snap as ICanCount;
                Assert.IsNotNull(counter);
                Assert.IsTrue(counter.TryGetCount(out var countSnap));
                Assert.AreEqual(numSnap, countSnap);

                Assert.IsTrue(curs.MoveTo(0xFF));
                Assert.AreEqual(0x1FF, curs.Value);
                Assert.IsTrue(curs.MoveTo(0x123C4));
                Assert.AreEqual(2 * 0x123C4 + 1, curs.Value);
                Assert.IsTrue(curs.MoveTo(num - 1));
                Assert.AreEqual(2 * num - 1, curs.Value);

                AreSamePartial(seq, snap);
            }
            break;

        case "intq":
            {
                // With nullable.
                var bldr = BuildableSequence<int?>.Builder.Create(num, out var seq);
                IEnumerable<int?> snap = null;
                for (int i = 0; i < num; i++)
                {
                    if (i == numSnap)
                        snap = seq.Snap();
                    bldr.Add(i % 4 == 0 ? null : 2 * i + 1);
                }
                bldr.Done();

                Assert.IsTrue(seq.TryGetCount(out var count));
                Assert.AreEqual(num, count);

                var counter = snap as ICanCount;
                Assert.IsNotNull(counter);
                Assert.IsTrue(counter.TryGetCount(out var countSnap));
                Assert.AreEqual(numSnap, countSnap);

                using var curs = seq.GetCursor();
                Assert.IsTrue(curs.MoveTo(0xFF));
                Assert.AreEqual(0x1FF, curs.Value);
                Assert.IsTrue(curs.MoveTo(0x123C4));
                Assert.IsNull(curs.Value);
                Assert.IsTrue(curs.MoveNext());
                Assert.AreEqual(2 * 0x123C5 + 1, curs.Value);

                AreSamePartial(seq, snap);
            }
            break;

        case "bool":
            {
                // Bools.
                var bldr = BuildableSequence<bool>.Builder.Create(num, out var seq);
                IEnumerable<bool> snap = null;
                for (int i = 0; i < num; i++)
                {
                    if (i == numSnap)
                        snap = seq.Snap();
                    bldr.Add(i % 3 != 0);
                }
                bldr.Done();

                Assert.IsTrue(seq.TryGetCount(out var count));
                Assert.AreEqual(num, count);

                var counter = snap as ICanCount;
                Assert.IsNotNull(counter);
                Assert.IsTrue(counter.TryGetCount(out var countSnap));
                Assert.AreEqual(numSnap, countSnap);

                using var curs = seq.GetCursor();
                Assert.IsTrue(curs.MoveTo(312));
                Assert.AreEqual(false, curs.Value);
                Assert.IsTrue(curs.MoveTo(3_739));
                Assert.AreEqual(true, curs.Value);
                Assert.IsTrue(curs.MoveNext());
                Assert.AreEqual(true, curs.Value);
                Assert.IsTrue(curs.MoveNext());
                Assert.AreEqual(false, curs.Value);

                AreSamePartial(seq, snap);
            }
            break;

        case "boolq":
            {
                // Bool? values.
                var bldr = BuildableSequence<bool?>.Builder.Create(num, out var seq);
                IEnumerable<bool?> snap = null;
                for (int i = 0; i < num; i++)
                {
                    if (i == numSnap)
                        snap = seq.Snap();
                    bldr.Add(i % 3 == 0 ? false : i % 3 == 1 ? null : true);
                }
                bldr.Done();

                Assert.IsTrue(seq.TryGetCount(out var count));
                Assert.AreEqual(num, count);

                var counter = snap as ICanCount;
                Assert.IsNotNull(counter);
                Assert.IsTrue(counter.TryGetCount(out var countSnap));
                Assert.AreEqual(numSnap, countSnap);

                using var curs = seq.GetCursor();
                Assert.IsTrue(curs.MoveTo(312));
                Assert.AreEqual(false, curs.Value);
                Assert.IsTrue(curs.MoveTo(3_739));
                Assert.IsNull(curs.Value);
                Assert.IsTrue(curs.MoveNext());
                Assert.AreEqual(true, curs.Value);
                Assert.IsTrue(curs.MoveNext());
                Assert.AreEqual(false, curs.Value);

                AreSamePartial(seq, snap);
            }
            break;

        default:
            Assert.Fail();
            break;
        }
    }

    [TestMethod]
    public void GrowableArrayCoverage()
    {
        {
            var ga = GrowableArray.Create<bool>(0);
            Assert.AreEqual(0L, ga.Count);
            Assert.IsTrue(ga.Capacity > 0);

            using (var ator = ga.GetEnumerator())
            {
                int i = 0;
                while (ator.MoveNext())
                    i++;
                Assert.AreEqual(0, i);
            }

            int num = 100_000;
            var sa = new bool[num];

            // Start with: false, true.
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = true;
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = default;

            // Set triangular numbers to true.
            int delta = 1;
            for (int i = 0; i < num;)
            {
                sa[i] = true;
                i += delta++;
            }

            ga.AddMulti(sa);
            Assert.AreEqual(num + 2L, ga.Count);
            using (var ator = ga.GetEnumerator())
            {
                Assert.IsTrue(ator.MoveNext());
                Assert.IsTrue(ator.MoveNext());
                int i = 0;
                while (ator.MoveNext())
                {
                    Assert.IsTrue(i < num);
                    Assert.AreEqual(sa[i], ator.Current);
                    i++;
                }
                Assert.AreEqual(num, i);
            }

            ga.AddMulti(sa);
            ga.AddMulti(sa);
            Assert.AreEqual(3L * num + 2, ga.Count);

            Assert.AreEqual(false, ga[0]);
            Assert.AreEqual(true, ga[1]);
            for (int i = 0; i < num; i++)
            {
                Assert.AreEqual(sa[i], ga[2 + i]);
                Assert.AreEqual(sa[i], ga[2 + i + num]);
                Assert.AreEqual(sa[i], ga[2 + i + num + num]);
            }

            var snap = ga.Snap() as GrowableArray<bool>;
            Assert.IsNotNull(snap);
            Assert.AreNotEqual(ga, snap);
            Assert.ThrowsException<InvalidOperationException>(() => snap.Add(true));

            ga.Seal();
            Assert.ThrowsException<InvalidOperationException>(() => ga.Add(true));

            snap = ga.Snap() as GrowableArray<bool>;
            Assert.IsNotNull(snap);
            Assert.AreEqual(ga, snap);
        }
        {
            int num = 100_000;
            var ga = GrowableArray.CreateNullable<bool>(num);
            Assert.AreEqual(0L, ga.Count);
            Assert.IsTrue(ga.Capacity > 0);

            using (var ator = ga.GetEnumerator())
            {
                int i = 0;
                while (ator.MoveNext())
                    i++;
                Assert.AreEqual(0, i);
            }

            var sa = new bool?[num];

            // Start with: null, false, true.
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = false;
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = true;
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = default;

            // Set items not divisible by 7 to false. Multiples of seven are left as null.
            for (int i = 0; i < num; i++)
            {
                if (i % 7 != 0)
                    sa[i] = false;
            }

            // Set triangular numbers to true.
            int delta = 1;
            for (int i = 0; i < num;)
            {
                sa[i] = true;
                i += delta++;
            }

            ga.AddMulti(sa);
            Assert.AreEqual(num + 3L, ga.Count);
            using (var ator = ga.GetEnumerator())
            {
                Assert.IsTrue(ator.MoveNext());
                Assert.IsTrue(ator.MoveNext());
                Assert.IsTrue(ator.MoveNext());
                int i = 0;
                while (ator.MoveNext())
                {
                    Assert.IsTrue(i < num);
                    Assert.AreEqual(sa[i], ator.Current);
                    i++;
                }
                Assert.AreEqual(num, i);
            }

            ga.AddMulti(sa);
            ga.AddMulti(sa);
            Assert.AreEqual(3L * num + 3, ga.Count);

            Assert.AreEqual(null, ga[0]);
            Assert.AreEqual(false, ga[1]);
            Assert.AreEqual(true, ga[2]);
            for (int i = 0; i < num; i++)
            {
                Assert.AreEqual(sa[i], ga[3 + i]);
                Assert.AreEqual(sa[i], ga[3 + i + num]);
                Assert.AreEqual(sa[i], ga[3 + i + num + num]);
            }

            var snap = ga.Snap() as GrowableArray<bool?>;
            Assert.IsNotNull(snap);
            Assert.AreNotEqual(ga, snap);
            Assert.ThrowsException<InvalidOperationException>(() => snap.Add(true));

            ga.Seal();
            Assert.ThrowsException<InvalidOperationException>(() => ga.Add(true));

            snap = ga.Snap() as GrowableArray<bool?>;
            Assert.IsNotNull(snap);
            Assert.AreEqual(ga, snap);
        }
        {
            int num = 100_000;
            var ga = GrowableArray.CreateNullable<TimeSpan>(num);
            Assert.AreEqual(0L, ga.Count);
            Assert.IsTrue(ga.Capacity > 0);

            using (var ator = ga.GetEnumerator())
            {
                int i = 0;
                while (ator.MoveNext())
                    i++;
                Assert.AreEqual(0, i);
            }

            var sa = new TimeSpan?[num];

            // Start with: null, 17.
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = TimeSpan.FromTicks(17);
            ga.AddMulti(sa.AsSpan(0, 1));
            sa[0] = default;

            // Set items not divisible by 7 to TimeSpan(i). Multiples of seven are left as null.
            for (int i = 0; i < num; i++)
            {
                if (i % 7 != 0)
                    sa[i] = TimeSpan.FromTicks(i);
            }

            ga.AddMulti(sa);
            Assert.AreEqual(num + 2L, ga.Count);
            using (var ator = ga.GetEnumerator())
            {
                Assert.IsTrue(ator.MoveNext());
                Assert.IsTrue(ator.MoveNext());
                int i = 0;
                while (ator.MoveNext())
                {
                    Assert.IsTrue(i < num);
                    Assert.AreEqual(sa[i], ator.Current);
                    i++;
                }
                Assert.AreEqual(num, i);
            }

            ga.AddMulti(sa);
            ga.AddMulti(sa);
            Assert.AreEqual(3L * num + 2, ga.Count);

            Assert.AreEqual(null, ga[0]);
            Assert.AreEqual(TimeSpan.FromTicks(17), ga[1]);
            for (int i = 0; i < num; i++)
            {
                Assert.AreEqual(sa[i], ga[2 + i]);
                Assert.AreEqual(sa[i], ga[2 + i + num]);
                Assert.AreEqual(sa[i], ga[2 + i + num + num]);
            }

            var snap = ga.Snap() as GrowableArray<TimeSpan?>;
            Assert.IsNotNull(snap);
            Assert.AreNotEqual(ga, snap);
            Assert.ThrowsException<InvalidOperationException>(() => snap.Add(TimeSpan.FromTicks(37)));

            ga.Seal();
            Assert.ThrowsException<InvalidOperationException>(() => ga.Add(TimeSpan.FromTicks(37)));

            snap = ga.Snap() as GrowableArray<TimeSpan?>;
            Assert.IsNotNull(snap);
            Assert.AreEqual(ga, snap);
        }
    }

    /// <summary>
    /// This tests that advancing the enumerator doesn't block other readers for <see cref="IndexedSequence{T}"/>.
    /// </summary>
    [TestMethod]
    public void IndexedSequenceBlocking()
    {
        int count = DoBaselineTests(
            Run, @"Util\IndexedSequenceBlocking.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var lck = new object();
            var sb = new StringBuilder();
            var seq = IndexedSequence<int>.Create(-1, DelayedSeq(lck, sb).Select(i => ((long)i, i)));

            var thd1 = new Thread(() =>
            {
                foreach (var item in seq)
                {
                    lock (lck)
                        sb.AppendFormat("[FAST] {0}", item).AppendLine();
                }
                lock (lck)
                    sb.AppendLine("[FAST] Finishing");
            });

            var thd2 = new Thread(() =>
            {
                // Wait before consuming.
                Thread.Sleep(100);

                foreach (var item in seq)
                {
                    lock (lck)
                        sb.AppendFormat("[SLOW] {0}", item).AppendLine();
                }

                // Wait before announcing our finish so it comes after FAST.
                Thread.Sleep(100);
                lock (lck)
                    sb.AppendLine("[SLOW] Finishing");
            });

            thd1.Start();
            thd2.Start();
            thd1.Join();
            thd2.Join();

            Sink.WriteLine(sb.ToString());
        }
    }

    /// <summary>
    /// This tests that advancing the enumerator doesn't block other readers for <see cref="CachingEnumerable{T}"/>.
    /// </summary>
    [TestMethod]
    public void CachingEnumerableBlocking()
    {
        int count = DoBaselineTests(
            Run, @"Util\CachingEnumerableBlocking.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var lck = new object();
            var sb = new StringBuilder();
            var seq = new CachingEnumerable<int>(DelayedSeq(lck, sb));

            var thd1 = new Thread(() =>
            {
                foreach (var item in seq)
                {
                    lock (lck)
                        sb.AppendFormat("[FAST] {0}", item).AppendLine();
                }
                lock (lck)
                    sb.AppendLine("[FAST] Finishing");
            });

            var thd2 = new Thread(() =>
            {
                // Wait before consuming.
                Thread.Sleep(100);

                foreach (var item in seq)
                {
                    lock (lck)
                        sb.AppendFormat("[SLOW] {0}", item).AppendLine();
                }

                // Wait before announcing our finish so it comes after FAST.
                Thread.Sleep(100);
                lock (lck)
                    sb.AppendLine("[SLOW] Finishing");
            });

            thd1.Start();
            thd2.Start();
            thd1.Join();
            thd2.Join();

            Sink.WriteLine(sb.ToString());
        }
    }

    private IEnumerable<int> DelayedSeq(object lck, StringBuilder sb)
    {
        lock (lck)
            sb.AppendLine("[SOURCE] Start");
        int count = 3;
        for (int i = 0; i < count; i++)
        {
            lock (lck)
                sb.AppendFormat("[SOURCE] {0}", i).AppendLine();
            yield return i;
        }

        // Wait before finishing.
        lock (lck)
            sb.AppendLine("[SOURCE] Delay");
        Thread.Sleep(1000);
        lock (lck)
            sb.AppendLine("[SOURCE] Finishing");
    }

    /// <summary>
    /// This tests that cursoring arbitrarily far ahead works for <see cref="IndexedSequence{T}"/>.
    /// </summary>
    [TestMethod]
    public void IndexedSequenceCursoring()
    {
        int min = 3;
        int count = 20;
        var seq = IndexedSequence<int>.Create(-1, Enumerable.Range(min, count).Select((v, i) => ((long)i, v)));

        using var curs = seq.GetCursor();
        foreach (int i in new int[] { 5, 7, 3, 17, 0 })
        {
            Assert.IsTrue(curs.MoveTo(i));
            Assert.AreEqual(i, curs.Index);
            Assert.AreEqual(i + min, curs.Value);
        }

        Assert.IsFalse(curs.MoveTo(count));
        Assert.IsTrue(curs.MoveTo(count - 1));
        Assert.AreEqual(count - 1, curs.Index);
        Assert.AreEqual(count - 1 + min, curs.Current);
        Assert.IsTrue(curs.MoveTo(count - 2));
        Assert.AreEqual(count - 2, curs.Index);
        Assert.AreEqual(count - 2 + min, curs.Value);
    }

    /// <summary>
    /// This tests that cursoring arbitrarily far ahead works for <see cref="CachingEnumerable{T}"/>.
    /// </summary>
    [TestMethod]
    public void CachingEnumerableCursoring()
    {
        int min = 3;
        int count = 20;
        var seq = new CachingEnumerable<int>(Enumerable.Range(min, count));

        using var curs = seq.GetCursor();
        foreach (int i in new int[] { 5, 7, 3, 17, 0 })
        {
            Assert.IsTrue(curs.MoveTo(i));
            Assert.AreEqual(i, curs.Index);
            Assert.AreEqual(i + min, curs.Current);
        }

        Assert.IsFalse(curs.MoveTo(count));
        Assert.IsTrue(curs.MoveTo(count - 1));
        Assert.AreEqual(count - 1, curs.Index);
        Assert.AreEqual(count - 1 + min, curs.Value);
        Assert.IsTrue(curs.MoveTo(count - 2));
        Assert.AreEqual(count - 2, curs.Index);
        Assert.AreEqual(count - 2 + min, curs.Value);
    }

    [TestMethod]
    public void ReadSubStreamTests()
    {
        using var mem = new MemoryStream();
        for (int i = 0; i < 1000; i++)
            mem.WriteByte((byte)i);

        {
            var buf = new byte[5];

            mem.Position = 50;
            using var reader = new ReadSubStream(mem, leaveOpen: true, 12);
            Assert.IsFalse(reader.CanSeek);
            Assert.IsTrue(reader.CanRead);
            Assert.IsFalse(reader.CanWrite);
            Assert.IsFalse(reader.CanTimeout);
            Assert.AreEqual(12, reader.Length);
            Assert.AreEqual(0, reader.Position);
            Assert.ThrowsException<NotSupportedException>(() => reader.Position = 5);
            Assert.ThrowsException<NotSupportedException>(() => reader.SetLength(5));
            Assert.ThrowsException<NotSupportedException>(() => reader.Seek(3, SeekOrigin.Begin));
            Assert.ThrowsException<NotSupportedException>(() => reader.Write(buf, 0, 1));

            int cb = reader.Read(buf.AsSpan(0, 0));
            Assert.AreEqual(0, cb);

            cb = reader.Read(buf);
            Assert.AreEqual(5, cb);
            Assert.AreEqual((byte)50, buf[0]);
            Assert.AreEqual((byte)54, buf[4]);

            cb = reader.Read(buf, 0, 5);
            Assert.AreEqual(5, cb);
            Assert.AreEqual((byte)55, buf[0]);
            Assert.AreEqual((byte)59, buf[4]);

            cb = reader.Read(buf);
            Assert.AreEqual(2, cb);
            Assert.AreEqual((byte)60, buf[0]);
            Assert.AreEqual((byte)61, buf[1]);

            cb = reader.Read(buf);
            Assert.AreEqual(0, cb);
            Assert.AreEqual(12, reader.Position);
            Assert.AreEqual(12, reader.GetOuterByteCount());

            cb = reader.Read(buf, 0, 5);
            Assert.AreEqual(0, cb);

            int c = reader.ReadByte();
            Assert.AreEqual(-1, c);
        }

        {
            mem.Position = 990;
            byte b = (byte)mem.Position;

            using var reader = new ReadSubStream(mem, leaveOpen: false, -1);
            Assert.IsFalse(reader.CanSeek);
            Assert.IsTrue(reader.CanRead);
            Assert.IsFalse(reader.CanWrite);
            Assert.ThrowsException<NotSupportedException>(() => reader.Length);
            Assert.AreEqual(0, reader.Position);

            var buf = new byte[5];
            int c = reader.ReadByte();
            Assert.AreEqual((int)b, c);

            int cb = reader.Read(buf);
            Assert.AreEqual(5, cb);
            Assert.AreEqual((byte)(b + 1), buf[0]);
            Assert.AreEqual((byte)(b + 5), buf[4]);

            cb = reader.Read(buf, 0, 5);
            Assert.AreEqual(4, cb);
            Assert.AreEqual((byte)(b + 6), buf[0]);
            Assert.AreEqual((byte)(b + 9), buf[3]);

            c = reader.ReadByte();
            Assert.AreEqual(-1, c);

            // Coverage for double dispose.
            reader.Dispose();
        }
    }

    [TestMethod]
    public void WriteSubStreamTests()
    {
        var store = new byte[20];
        using var mem = new MemoryStream(store, writable: true);
        mem.WriteByte((byte)17);
        Assert.AreEqual(1, mem.Position);
        Assert.AreEqual((byte)17, store[0]);

        {
            var buf = new byte[5] { 1, 2, 3, 4, 5 };

            using var writer = new WriteSubStream(mem, leaveOpen: true);
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

            writer.Write(buf.AsSpan(0, 0));
            Assert.AreEqual(0, writer.Length);

            writer.WriteByte((byte)38);
            Assert.AreEqual(1, writer.Length);
            Assert.AreEqual((byte)38, store[1]);

            writer.Write(buf);
            Assert.AreEqual(6, writer.Length);
            Assert.AreEqual(7, mem.Position);
            Assert.AreEqual((byte)1, store[2]);
            Assert.AreEqual((byte)5, store[6]);

            writer.Write(buf, 0, 5);
            Assert.AreEqual(11, writer.Length);
            Assert.AreEqual((byte)1, store[7]);
            Assert.AreEqual((byte)5, store[11]);
        }

        {
            using var writer = new WriteSubStream(mem, leaveOpen: false);

            writer.WriteByte((byte)45);
            Assert.AreEqual(1, writer.Length);
            Assert.AreEqual((byte)45, store[12]);

            writer.Flush();

            // Coverage for double dispose.
            writer.Dispose();
        }
    }

    [TestMethod]
    [DataRow(CompressionKind.Brotli, false)]
    [DataRow(CompressionKind.Snappy, false)]
    [DataRow(CompressionKind.Deflate, false)]
    [DataRow(CompressionKind.Brotli, true)]
    [DataRow(CompressionKind.Snappy, true)]
    [DataRow(CompressionKind.Deflate, true)]
    public void CodecSubStreamTests(CompressionKind comp, bool wrap)
    {
        using var mem = new MemoryStream();

        byte bHead = (byte)17;
        byte bTail = (byte)18;
        mem.WriteByte(bHead);
        Assert.AreEqual(1, mem.Position);

        Stream write = wrap ? new StreamWrapper(mem, canWrite: true) : mem;
        Stream read = wrap ? new StreamWrapper(mem, canRead: true) : mem;

        // Tracks whether the writer/reader is a CodecSubStream.
        bool std;
        {
            // This needs to be larger than the brotli page size (about 64K).
            int big = 200_000;
            int small = 100;
            var buf = new byte[big];
            for (int i = 0; i < big; i++)
                buf[i] = (byte)(i + 1);

            long len;
            using (var writer = comp.CreateWriter(write, needPosition: true))
            {
                var css = writer as CodecSubStream;
                std = css != null;

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

                writer.Write(buf.AsSpan(0, 0));
                Assert.AreEqual(0, writer.Length);

                writer.Write(buf);
                Assert.AreEqual(big, writer.Length);
                Assert.AreEqual(big, writer.Position);
                if (std)
                    Assert.AreEqual(big, css.GetInnerByteCount());

                writer.Flush();
                writer.Write(buf.AsSpan(0, 1));

                writer.Write(buf, 1, small - 1);
                Assert.AreEqual(big + small, writer.Length);
                Assert.AreEqual(big + small, writer.Position);
                if (std)
                    Assert.AreEqual(big + small, css.GetInnerByteCount());

                writer.Dispose();

                // This is only accurate after dispose.
                if (std)
                    len = css.GetOuterByteCount();
                else
                    len = mem.Length - 1;
            }
            Assert.AreEqual(mem.Length - 1, len);
            mem.WriteByte(bTail);

            mem.Position = 1;
            using (var reader = comp.CreateReader(read, len, out var pulled))
            {
                var css = reader as CodecSubStream;
                Assert.AreEqual(std, css != null);

                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                if (std)
                    Assert.AreEqual(0, reader.Position);
                Assert.ThrowsException<NotSupportedException>(() => reader.Length);
                Assert.ThrowsException<NotSupportedException>(() => reader.Position = 5);
                Assert.ThrowsException<NotSupportedException>(() => reader.SetLength(5));
                Assert.ThrowsException<NotSupportedException>(() => reader.Seek(3, SeekOrigin.Begin));
                if (std)
                    Assert.ThrowsException<NotSupportedException>(() => reader.Write(buf, 0, 1));
                else
                    Assert.ThrowsException<InvalidOperationException>(() => reader.Write(buf, 0, 1));

                var buf2 = new byte[big];
                int cb = reader.Read(buf2, 0, 0);
                Assert.AreEqual(0, cb);

                buf2[0] = (byte)reader.ReadByte();
                cb = 1;
                while (cb < big)
                {
                    int cbCur = reader.Read(buf2, cb, big - cb);
                    Assert.IsTrue(cbCur > 0);
                    cb += cbCur;
                    if (std)
                        Assert.AreEqual(cb, reader.Position);
                }
                Assert.AreEqual(big, cb);
                CollectionAssert.AreEqual(buf, buf2);

                var buf3 = new byte[small];
                cb = 0;
                while (cb < small)
                {
                    int cbCur = reader.Read(buf3, cb, small - cb);
                    Assert.IsTrue(cbCur > 0);
                    cb += cbCur;
                    if (std)
                        Assert.AreEqual(big + cb, reader.Position);
                }
                Assert.AreEqual(small, cb);
                var bufT = buf;
                Array.Resize(ref bufT, small);
                CollectionAssert.AreEqual(bufT, buf3);

                int c = reader.ReadByte();
                Assert.AreEqual(-1, c);
                cb = reader.Read(buf3);
                Assert.AreEqual(0, cb);

                Assert.AreEqual(len, pulled());
                if (std)
                {
                    Assert.AreEqual(big + small, css.GetInnerByteCount());
                    Assert.AreEqual(len, css.GetOuterByteCount());
                }
            }
            Assert.AreEqual(len + 1, mem.Position);

            // Don't tell it the length.
            mem.Position = 1;
            using (var reader = comp.CreateReader(read, -1, out var pulled))
            {
                var css = reader as CodecSubStream;

                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                if (std)
                    Assert.AreEqual(0, reader.Position);
                Assert.ThrowsException<NotSupportedException>(() => reader.Length);
                Assert.ThrowsException<NotSupportedException>(() => reader.Position = 5);
                Assert.ThrowsException<NotSupportedException>(() => reader.SetLength(5));
                Assert.ThrowsException<NotSupportedException>(() => reader.Seek(3, SeekOrigin.Begin));
                if (std)
                    Assert.ThrowsException<NotSupportedException>(() => reader.Write(buf, 0, 1));
                else
                    Assert.ThrowsException<InvalidOperationException>(() => reader.Write(buf, 0, 1));

                var buf2 = new byte[big];
                int cb = reader.Read(buf2, 0, 0);
                Assert.AreEqual(0, cb);

                buf2[0] = (byte)reader.ReadByte();
                cb = 1;
                while (cb < big)
                {
                    int cbCur = reader.Read(buf2, cb, big - cb);
                    Assert.IsTrue(cbCur > 0);
                    cb += cbCur;
                    if (std)
                        Assert.AreEqual(cb, reader.Position);
                }
                Assert.AreEqual(big, cb);
                CollectionAssert.AreEqual(buf, buf2);

                var buf3 = new byte[small];
                cb = 0;
                while (cb < small)
                {
                    int cbCur = reader.Read(buf3, cb, small - cb);
                    Assert.IsTrue(cbCur > 0);
                    cb += cbCur;
                    if (std)
                        Assert.AreEqual(big + cb, reader.Position);
                }
                Assert.AreEqual(small, cb);
                var bufT = buf;
                Array.Resize(ref bufT, small);
                CollectionAssert.AreEqual(bufT, buf3);

                int c = reader.ReadByte();
                Assert.AreEqual(-1, c);
                cb = reader.Read(buf3);
                Assert.AreEqual(0, cb);

                if (std)
                {
                    Assert.AreEqual(big + small, css.GetInnerByteCount());
                    Assert.AreEqual(len, css.GetOuterByteCount());
                    Assert.AreEqual(len, pulled());
                    Assert.AreEqual(len + 1, mem.Position);
                }
                else
                {
                    // REVIEW: Deflate reads the extra byte! Is there a good way to avoid this when
                    // we don't know the size of the sub-stream?
                    Assert.AreEqual(len + 1, pulled());
                    Assert.AreEqual(len + 2, mem.Position);
                }
            }

            // Provide a bad (short) length. Should throw IOException.
            mem.Position = 1;
            using (var reader = comp.CreateReader(read, 100, out var pulled))
            {
                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                if (std)
                {
                    Assert.AreEqual(0, reader.Position);
                    Assert.ThrowsException<IOException>(() => reader.ReadByte());
                }
                else
                    Assert.AreEqual(1, reader.ReadByte());
            }

            // Truncate the memory stream, then try to read. Should throw IOException.
            mem.Position = 1;
            mem.SetLength(100);
            using (var reader = comp.CreateReader(read, len, out var pulled))
            {
                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                if (std)
                {
                    Assert.AreEqual(0, reader.Position);
                    Assert.ThrowsException<IOException>(() => reader.ReadByte());
                }
                else
                    Assert.AreEqual(1, reader.ReadByte());
            }
        }

        if (!std)
            return;

        mem.Position = 1;
        mem.SetLength(1);
        {
            // Test tiny block writes - so compression won't be used.
            long len;
            using (var writer = comp.CreateWriter(write, needPosition: false))
            {
                var css = writer as CodecSubStream;
                Assert.IsNotNull(css);

                writer.WriteByte(57);

                writer.Dispose();

                // This is only accurate after dispose.
                len = css.GetOuterByteCount();
            }
            Assert.AreEqual(mem.Length - 1, len);
            mem.WriteByte((byte)18);

            mem.Position = 1;
            using (var reader = comp.CreateReader(read, len, out var pulled))
            {
                var css = reader as CodecSubStream;
                Assert.IsNotNull(css);

                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                Assert.AreEqual(0, reader.Position);

                int c = reader.ReadByte();
                Assert.AreEqual(57, c);

                Assert.AreEqual(1, reader.Position);
                Assert.AreEqual(len, pulled());
            }
            Assert.AreEqual(len + 1, mem.Position);

            mem.Position = 1;
            using (var reader = comp.CreateReader(read))
            {
                var css = reader as CodecSubStream;
                Assert.IsNotNull(css);

                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                Assert.AreEqual(0, reader.Position);

                int c = reader.ReadByte();
                Assert.AreEqual(57, c);

                Assert.AreEqual(1, reader.Position);
            }
            Assert.AreEqual(len + 1, mem.Position);

            mem.Position = 1;
            using (var reader = comp.CreateReader(read, 0, out var pulled))
            {
                var css = reader as CodecSubStream;
                Assert.IsNotNull(css);

                Assert.IsFalse(reader.CanSeek);
                Assert.IsTrue(reader.CanRead);
                Assert.IsFalse(reader.CanWrite);
                Assert.IsFalse(reader.CanTimeout);
                Assert.AreEqual(0, reader.Position);

                int c = reader.ReadByte();
                Assert.AreEqual(-1, c);
                Assert.AreEqual(0, reader.Position);
            }
        }
    }

    /// <summary>
    /// A stream wrapper that doesn't support seeking, position or length.
    /// </summary>
    private sealed class StreamWrapper : Stream
    {
        private readonly Stream _strm;
        private readonly bool _canRead;
        private readonly bool _canWrite;

        public override bool CanRead => _canRead && _strm.CanRead;
        public override bool CanWrite => _canWrite && _strm.CanWrite;
        public override bool CanSeek => false;
        public override bool CanTimeout => _strm.CanTimeout;

        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public StreamWrapper(Stream strm, bool canRead = false, bool canWrite = false)
        {
            _strm = strm;
            _canRead = canRead;
            _canWrite = canWrite;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => _canRead ? _strm.BeginRead(buffer, offset, count, callback, state) : throw new NotSupportedException();
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => _canWrite ? _strm.BeginWrite(buffer, offset, count, callback, state) : throw new NotSupportedException();
        public override void Close() => _strm.Close();
        public override void CopyTo(Stream destination, int bufferSize)
        {
            if (!_canRead)
                throw new NotSupportedException();
            _strm.CopyTo(destination, bufferSize);
        }
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => _canRead ? _strm.CopyToAsync(destination, bufferSize, cancellationToken) : throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _strm.Dispose();
            base.Dispose(disposing);
        }
        public override ValueTask DisposeAsync() => base.DisposeAsync();
        public override int EndRead(IAsyncResult asyncResult) => _canRead ? _strm.EndRead(asyncResult) : throw new NotSupportedException();
        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (!_canWrite)
                throw new NotSupportedException();
            _strm.EndWrite(asyncResult);
        }
        public override void Flush() => _strm.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _strm.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count)
            => _canRead ? _strm.Read(buffer, offset, count) : throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _canRead ? _strm.ReadAsync(buffer, offset, count, cancellationToken) : throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _canRead ? _strm.ReadAsync(buffer, cancellationToken) : throw new NotSupportedException();
        public override int Read(Span<byte> buffer) => _canRead ? _strm.Read(buffer) : throw new NotSupportedException();
        public override int ReadByte() => _canRead ? _strm.ReadByte() : throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override int ReadTimeout { get => _strm.ReadTimeout; set => _strm.ReadTimeout = value; }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_canWrite)
                throw new NotSupportedException();
            _strm.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!_canWrite)
                throw new NotSupportedException();
            _strm.Write(buffer);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_canWrite)
                throw new NotSupportedException();
            return _strm.WriteAsync(buffer, offset, count, cancellationToken);
        }
        public override void WriteByte(byte value)
        {
            if (!_canWrite)
                throw new NotSupportedException();
            _strm.WriteByte(value);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_canWrite)
                throw new NotSupportedException();
            return _strm.WriteAsync(buffer, cancellationToken);
        }
        public override int WriteTimeout { get => _strm.WriteTimeout; set => _strm.WriteTimeout = value; }
    }
}