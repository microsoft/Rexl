// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

using Microsoft.Rexl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

// REVIEW: This doesn't yet fully cover the red black tree code. Much of rb tree code coverage
// comes from tests of other functionality. This should really be augmented to fully cover it.
[TestClass]
public partial class RedBlackTreeTests
{
    [TestMethod]
    public void Basic()
    {
        var empty = TestRedBlackTree.Empty;
        Assert.AreEqual(0, empty.Count);
        Assert.IsNull(empty.FirstKey);
        Assert.AreEqual(empty.FirstVal, 0);
        Assert.AreEqual(empty.First, ((string)null, 0));

        var e = empty.Create(null);
        Assert.IsTrue((object)e == (object)empty);
        e = empty.Create(Enumerable.Empty<(string, int)>());
        Assert.IsTrue((object)e == (object)empty);
        e = empty.Create(Enumerable.Empty<(string, int)>(), out int[] _);
        Assert.IsTrue((object)e == (object)empty);

        var one = empty.Create(("hi", 3));
        Assert.AreEqual(1, one.Count);

        var tree0 = empty.Create(("", 0), ("hi", 3), ("whatever", -12));
        Assert.AreEqual(3, tree0.Count);
        Assert.AreEqual(("", 0), tree0.First);
        var tree1 = empty.SetItems(("hi", 3), ("whatever", -12), ("", 0));
        Assert.AreEqual(3, tree1.Count);
        var tree2 = empty.Create(("whatever", -12), ("", 17), ("hi", 3), ("", 0));
        Assert.AreEqual(3, tree2.Count);

        Assert.IsTrue(tree0.SameRoot(tree0));
        Assert.IsFalse(tree0.SameRoot(null));
        Assert.IsFalse(tree0.SameRoot(empty));
        Assert.IsFalse(tree0.SameRoot(tree1));
        Assert.IsFalse(tree1.SameRoot(tree2));
        Assert.IsFalse(tree2.SameRoot(tree0));

        Assert.IsFalse(TestRedBlackTree.Equals(empty, tree0));
        Assert.IsFalse(tree0.Equals(empty));
        Assert.IsFalse(TestRedBlackTree.Equals(null, tree0));
        Assert.IsFalse(tree0.Equals(null));
        Assert.IsTrue(TestRedBlackTree.Equals(tree0, tree1));
        Assert.IsTrue(tree0.Equals(tree1));
        Assert.IsTrue(TestRedBlackTree.Equals(tree1, tree2));
        Assert.IsTrue(tree1.Equals(tree2));

        var hash0 = tree0.GetHashCode();
        var hash1 = tree1.GetHashCode();
        var hash2 = tree2.GetHashCode();
        Assert.AreEqual(hash0, hash1);
        Assert.AreEqual(hash1, hash2);

        var tmp = tree0.SetItems(null);
        Assert.AreEqual(3, tmp.Count);
        Assert.IsTrue(tree0.SameRoot(tmp));

        tmp = tree0.RemoveItem("missing");
        Assert.IsTrue((object)tmp == (object)tree0);

        // Coverage of Create (and CreateFromArraySorted).
        // Note: len should be prime so the fill algorithm hits everything.
        const int len = 91;
        var items = new (string, int)[len];
        for (int k = 0, index = 0; k < items.Length; k++, index = (index + 5) % len)
            items[index] = (string.Format("item_{0:000}", index), index);

        for (int num = 0; num <= len; num++)
        {
            var cur = items;
            Array.Resize(ref cur, num);
            var tree = empty.Create(cur);

            Array.Sort(cur, (a, b) => StringComparer.Ordinal.Compare(a.Item1, b.Item1));
            int index = 0;
            foreach (var pair in tree.GetPairs())
            {
                Assert.AreEqual(cur[index].Item1, pair.key);
                Assert.AreEqual(cur[index].Item2, pair.val);
                index++;
            }
        }

        // RemoveKnownItem asserts, does not check.
#if DEBUG
        Exception ex = null;
        try { tree0.RemoveKnownItem("missing"); }
        catch (Exception exTmp) { ex = exTmp; }
        Assert.IsInstanceOfType(ex, typeof(AssertFailedException));
#else
        tmp = tree0.RemoveKnownItem("missing");
        Assert.IsTrue((object)tmp == (object)tree0);
#endif
    }

    [TestMethod]
    public void RedBlackTreeChecks()
    {
        var tree = TestRedBlackTree.Empty;
        Assert.AreEqual(0, tree.Count);

        ShouldBugExcept(() => { tree.SetItem(null, 3); });
        ShouldBugExcept(() => { tree.SetItems((null, 3)); });
        ShouldBugExcept(() => { tree.SetItems(("hi", 2), (null, 3)); });
        ShouldBugExcept(() => { tree.SetItem("a", int.MinValue); });
        ShouldBugExcept(() => { tree.SetItems(("a", 2), ("b", int.MinValue)); });
    }

    private void ShouldBugExcept(Action act)
    {
        bool caught = false;
        try { act(); }
        catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
        Assert.IsTrue(caught);
    }

    private sealed class TestRedBlackTree : RedBlackTree<TestRedBlackTree, string, int>
    {
        public static TestRedBlackTree Empty = new TestRedBlackTree(null);

        private TestRedBlackTree(Node root)
            : base(root)
        {
        }

        protected override int KeyCompare(string key0, string key1)
        {
            return StringComparer.Ordinal.Compare(key0, key1);
        }

        protected override bool KeyEquals(string key0, string key1)
        {
            return key0 == key1;
        }

        protected override int KeyHash(string key)
        {
            return key.GetHashCode();
        }

        protected override bool KeyIsValid(string key)
        {
            return key != null;
        }

        protected override bool ValEquals(int val0, int val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(int val)
        {
            return val;
        }

        protected override bool ValIsValid(int val)
        {
            return val != int.MinValue;
        }

        protected override TestRedBlackTree Wrap(Node root)
        {
            return root == _root ? this : root == null ? Empty : new TestRedBlackTree(root);
        }
    }
}