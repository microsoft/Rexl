// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Rexl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public partial class BitSetTests : RexlTestsBaseText<bool>
{
    [TestMethod]
    public void BaselineTests()
    {
        int count = DoBaselineTests(
            DoBitSetTest, @"BitSet.txt");
        Assert.AreEqual(1, count);
    }

    private void DoBitSetTest(string pathHead, string pathTail, string text, bool opts)
    {
        // REVIEW: The contents of the file isn't used. The presence of the file just
        // triggers execution of this test, which both asserts things and records output for
        // baselining. Should we bother with a separate entry point in RexlTestsBase?

        Sink.WriteLine("=== SetBit ===");
        for (int i = 0; i < 1000; i++)
        {
            var a = default(BitSet).SetBit(i);
            Validate(a);
            Assert.IsTrue(!a.IsEmpty);
            Assert.AreEqual(1, a.Count);
            Assert.AreEqual(i, a.SlotMin);
            Assert.AreEqual(i, a.SlotMax);
            Assert.IsTrue(a.ClearBit(i).IsEmpty);
            Assert.IsTrue(a.ClearBit(i + 1) == a);
            Assert.IsTrue(a.ClearAtAndAbove(i).IsEmpty);
            Assert.IsTrue(a.ClearAtAndAbove(i + 1) == a);
            Assert.IsTrue(i < 1 || a.ClearAtAndAbove(i - 1).IsEmpty);
            Assert.IsTrue(i < 64 || a.ClearAtAndAbove(i - 64).IsEmpty);

            RoundTrip(a);

            if (i % 50 == 0)
                Sink.WriteLine("[{0:000}]: {1}", i, a);
        }

        Sink.WriteLine("=== SetBit 2 ===");
        for (int i = 0; i < 200; i++)
        {
            var a = default(BitSet).SetBit(i);

            if (i % 20 == 0)
                Sink.WriteLine("[{0:000}]: {1} {2}", i, a.Count, a);

            for (int j = 0; j < 200; j++)
            {
                bool isSame = i == j;
                var b = a.SetBit(j);
                Validate(b);
                Assert.IsTrue(!b.IsEmpty);
                Assert.AreEqual(isSame ? 1 : 2, b.Count);
                Assert.AreEqual(Math.Min(i, j), b.SlotMin);
                Assert.AreEqual(Math.Max(i, j), b.SlotMax);
                Assert.IsTrue(a.IsSubset(b));
                Assert.IsTrue(!b.IsSubset(a) || isSame);
                Assert.IsTrue(a.Intersects(b));
                Assert.IsTrue(b.Intersects(a));

                if (i % 20 == 0 && j % 20 == 0)
                    Sink.WriteLine("  [{0:000}]: {1} {2}", j, b.Count, b);

                var c = a.ClearBit(j);
                Assert.IsTrue(c.IsEmpty == isSame);
                Assert.IsTrue(isSame ^ (c == a));

                var d = b.ClearBit(j);
                Assert.IsTrue(d.IsEmpty == isSame);
                Assert.IsTrue(isSame ^ (d == a));

                var a2 = a << j;
                Assert.IsTrue(a2 == default(BitSet).SetBit(i + j));
                var b2 = b << j;
                Assert.IsTrue(b2 == a2.SetBit(j * 2));
            }
        }

        Sink.WriteLine("=== GetMask ===");
        for (int i = 0; i < 1000; i++)
        {
            var a = BitSet.GetMask(i);
            Validate(a);
            Assert.IsTrue(a.IsEmpty == (i == 0));
            Assert.AreEqual(i, a.Count);
            Assert.AreEqual(Math.Min(0, i - 1), a.SlotMin);
            Assert.AreEqual(i - 1, a.SlotMax);
            RoundTrip(a);

            Assert.AreEqual(a, a.ClearAtAndAbove(i));
            Assert.IsTrue(a.ClearAtAndAbove(0).IsEmpty);
            for (int j = i; (j -= 10) >= 0;)
                Assert.AreEqual(BitSet.GetMask(j), a.ClearAtAndAbove(j));

            if (i % 50 == 0)
                Sink.WriteLine("{0,3}: {1}", a.Count, a);
        }

        Sink.WriteLine("=== GetMask 2 ===");
        for (int i = 0; i < 200; i++)
        {
            if (i % 20 == 0)
                Sink.WriteLine("[{0:000}]", i);

            var t = BitSet.GetMask(i, 200);
            Validate(t);
            Assert.AreEqual(200 - i, t.Count);
            Assert.AreEqual(i, t.SlotMin);
            Assert.AreEqual(199, t.SlotMax);
            RoundTrip(t);

            for (int j = 0; j <= i; j++)
            {
                bool isEmpty = j == i;
                var a = BitSet.GetMask(j, i);
                Assert.IsTrue(a.IsEmpty == isEmpty);
                Assert.AreEqual(i - j, a.Count);
                Assert.AreEqual(j < i ? j : -1, a.SlotMin);
                Assert.AreEqual(j < i ? i - 1 : -1, a.SlotMax);
                Assert.IsTrue(!a.Intersects(t));
                Assert.IsTrue(!t.Intersects(a));

                var b = BitSet.GetMask(j);
                Assert.IsTrue(!a.Intersects(b));
                Assert.IsTrue(!b.Intersects(a));
                Assert.IsTrue(!b.Intersects(t));
                Assert.IsTrue(!t.Intersects(b));
                Assert.IsTrue(a.IsSubset(b) == isEmpty);
                Assert.IsTrue(b.IsSubset(a) == (j == 0));

                Assert.IsTrue(!a.Intersects(t));
                Assert.IsTrue(!t.Intersects(a));
                Assert.IsTrue(a.IsSubset(t) == isEmpty);
                Assert.IsTrue(!t.IsSubset(a));

                var c = b | t;
                var d = t | b;
                Validate(c);
                Validate(d);
                Assert.AreEqual(c, d);
                Assert.IsTrue(c == d);
                Assert.IsTrue(c != b);
                Assert.IsTrue(b != c);
                Assert.IsTrue((c == t) == b.IsEmpty);
                Assert.IsTrue((t == c) == b.IsEmpty);
                Assert.IsTrue(!a.Intersects(c));
                Assert.IsTrue(!c.Intersects(a));
                Assert.IsTrue(a.IsSubset(c) == isEmpty);
                Assert.IsTrue(!c.IsSubset(a));

                if (i % 20 == 0 && j % 20 == 0)
                    Sink.WriteLine("  [{0:000}]: {1,3} {2}", j, a.Count, a);

                for (int k = 0; k < 200; k++)
                {
                    Assert.IsTrue(a.TestBit(k) == (j <= k && k < i));
                    Assert.IsTrue(a.TestAtOrAbove(k) == (j < i && k < i));
                }
            }
        }

        Sink.WriteLine("=== Insert ===");
        for (int i = 0; i < 200; i++)
        {
            var a = default(BitSet).SetBit(i);

            if (i % 20 == 0)
                Sink.WriteLine("[{0:000}]: {1} {2}", i, a.Count, a);

            for (int j = 0; j < 200; j++)
            {
                bool isSame = i == j;
                var b = a.Insert(j, true);
                Validate(b);
                Assert.IsTrue(!b.IsEmpty);
                Assert.AreEqual(2, b.Count);
                Assert.AreEqual(Math.Min(i, j), b.SlotMin);
                Assert.AreEqual(Math.Max(i + 1, j), b.SlotMax);
                if (i < j)
                    Assert.IsTrue(a.IsSubset(b));
                else
                    Assert.IsTrue((a << 1).IsSubset(b));

                if (i % 20 == 0 && j % 20 == 0)
                    Sink.WriteLine("  [{0:000}]: {1} {2}", j, b.Count, b);

                var c = a.Delete(j);
                if (i < j)
                    Assert.IsTrue(c == a);
                else if (i > j)
                    Assert.IsTrue((c << 1) == a);
                else
                    Assert.IsTrue(c.IsEmpty);

                var d = b.Delete(j);
                Assert.IsFalse(d.IsEmpty);
                Assert.IsTrue(d == a);
            }
        }

        Sink.WriteLine("=== Insert2 ===");
        for (int i = 0; i < 200; i++)
        {
            var a = BitSet.GetMask(i);

            if (i % 20 == 0)
                Sink.WriteLine("[{0:000}]: {1} {2}", i, a.Count, a);

            for (int j = 0; j < 200; j++)
            {
                bool isSame = i == j;
                var b = a.Insert(j, false);
                Validate(b);
                Assert.AreEqual(i, b.Count);
                if (i <= j)
                    Assert.IsTrue(a == b);

                if (i % 20 == 0 && j % 20 == 0)
                    Sink.WriteLine("  [{0:000}]: {1} {2}", j, b.Count, b);

                var c = a.Delete(j);
                if (i <= j)
                    Assert.IsTrue(c == a);
                else
                    Assert.IsTrue(((c << 1) | 1) == a);

                var d = b.Delete(j);
                Assert.IsTrue(d == a);
            }
        }

        {
            var bools = new bool[210];
            bools[40] = true;
            bools[199] = true;
            var a = new BitSet(new ReadOnlySpan<bool>(bools));
            Sink.WriteLine("{0} {1}", a.Count, a);
        }

        {
            var bits = new byte[30];
            bits[2] = 0xC0;
            bits[24] = 0x81;
            var a = new BitSet(new ReadOnlySpan<byte>(bits));
            Sink.WriteLine("{0} {1}", a.Count, a);
        }
    }

    private void Validate(BitSet a)
    {
        var b = a & a;
        Assert.AreEqual(a, b);
        b = a | a;
        Assert.AreEqual(a, b);
        b = a - a;
        Assert.IsTrue(b.IsEmpty);
        var m = BitSet.GetMask(a.SlotMax + 100);
        Assert.AreEqual(a.SlotMax + 100, m.Count);
        Assert.AreEqual(m.Count - 1, m.SlotMax);
        b = a - m;
        Assert.IsTrue(b.IsEmpty);
        m -= a;
        Assert.AreEqual(a.SlotMax + 100 - a.Count, m.Count);
        b = a - m;
        Assert.AreEqual(a, b);
        b = a & m;
        Assert.IsTrue(b.IsEmpty);
    }

    private void RoundTrip(BitSet a)
    {
        var b = new BitSet(GetBools(a));
        Assert.AreEqual(a, b);
    }

    private IEnumerable<bool> GetBools(BitSet a)
    {
        int slot = 0;
        foreach (var next in a)
        {
            Assert.IsTrue(next >= slot);
            for (; slot < next; slot++)
                yield return false;
            yield return true;
            slot++;
        }
    }
}
