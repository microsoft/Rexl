// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Flow;
using Microsoft.Rexl.Sequence;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using RowMap = ReadOnly.Array<(int min, int lim)>;

[TestClass]
public sealed class DataClipTests
{
    private readonly EnumerableCodeGeneratorBase _codeGen
        = new CachingEnumerableCodeGenerator(new TestEnumTypeManager(), TestGenerators.Instance);

    [TestMethod]
    public void CreateInfiniteDataValueClipFromInfiniteSequence()
    {
        var rowMaps = new RowMap[]
        {
            default,
            new (int, int)[] { (-1, -1) },
            new (int, int)[] { (0, 1), (2, 3), (10, 15), (20, 30), (1000000, -1) },
        };

        long count;
        long length;
        foreach (var rowMap in rowMaps)
        {
            var infiniteClip = DataValueClip.Create(_codeGen.TypeManager, DType.I4Req.ToSequence(), InfiniteEnumerable(), default, rowMap);
            Assert.IsFalse(infiniteClip.TryGetCount(out count));
            Assert.AreEqual(-1, count);
            Assert.IsFalse(infiniteClip.TryGetDataLength(500, out length));
            Assert.IsTrue(length >= 500);
            Assert.IsFalse(infiniteClip.TryGetDataLength(10, out length));
            Assert.IsTrue(length >= 500);
        }
    }

    [TestMethod]
    public void CreateFiniteDataValueClipFromInfiniteSequence()
    {
        var finiteClip = DataValueClip.Create(_codeGen.TypeManager, DType.I4Req.ToSequence(), InfiniteEnumerable(), default, new (int, int)[] { (-1, 6) });
        long count;
        Assert.IsFalse(finiteClip.TryGetCount(out count));
        Assert.AreEqual(-1, count);
        Assert.IsFalse(finiteClip.TryGetDataLength(20, out var _));
        Assert.IsTrue(finiteClip.TryGetDataLength(100, out var length1));
        Assert.IsTrue(finiteClip.TryGetDataLength(20, out var length2));
        Assert.AreEqual(length1, length2);
        Assert.IsTrue(finiteClip.TryGetCount(out count));
        Assert.AreEqual(6, count);
    }

    [TestMethod]
    public void CreateDataValueClipFromUnknownCountFiniteSequence()
    {
        foreach (var caching in new bool[] { true, false })
        {
            IEnumerable<int> seq = caching ? new CachingEnumerable<int>(Enumerable.Range(0, 20)) : Enumerable.Range(0, 20);
            var dataClip = DataValueClip.Create(_codeGen.TypeManager, DType.I4Req.ToSequence(), seq, default, new (int, int)[] { (-1, 3), (5, 7), (15, -1) });
            long count;
            Assert.IsFalse(dataClip.TryGetCount(out count));
            Assert.AreEqual(-1, count);
            Assert.AreEqual(10, dataClip.GetCount(() => { }));
        }
    }

    [TestMethod]
    public void CreateDataValueClipFromKnownCountFiniteSequence()
    {
        var dataClip = DataValueClip.Create(_codeGen.TypeManager, DType.I4Req.ToSequence(), Enumerable.Range(0, 20).ToArray(), default, new (int, int)[] { (10, -1) });
        int expectedCount = 10;
        long count;
        Assert.IsTrue(dataClip.TryGetCount(out count));
        Assert.IsTrue(dataClip.TryGetDataLength(5, out var _));
        Assert.AreEqual(expectedCount, count);
    }

    [TestMethod]
    public void CreateDataValueClipFromICanCountSequence()
    {
        var (type, countableSeq) = TestUtils.ExecuteFormula("Repeat(1, " + long.MaxValue + ")->{A:it, B: it * 1.0}", _codeGen);
        var dataClip = DataValueClip.Create(_codeGen.TypeManager, type, countableSeq, default, new (int, int)[] { (-1, 2), (10, int.MaxValue) });
        int expectedCount = int.MaxValue - 8;
        long count;
        Assert.IsTrue(dataClip.TryGetCount(out count));
        Assert.IsTrue(dataClip.TryGetDataLength(100, out var _));
        Assert.AreEqual(expectedCount, count);
    }

    private static IEnumerable<int> InfiniteEnumerable()
    {
        while (true)
        {
            yield return 1;
        }
    }
}
