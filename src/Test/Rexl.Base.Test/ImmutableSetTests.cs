// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class ImmutableSetTests
{
    [TestMethod]
    [TestCategory("L0")]
    public void Create_FromEnumerable()
    {
        var values = new List<string> { "a", "b", "c" };
        var set = Immutable.Set<string>.Create(values);

        Assert.IsFalse(set.IsEmpty);
        Assert.AreEqual(3, set.Count);
        Assert.IsTrue(values.All(v => set.Contains(v)));
        Assert.IsFalse(set.Contains("hi"));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void IsSubset_ValidSubset_ReturnsTrue()
    {
        var set1 = Immutable.Set<string>.Create(new List<string> { "a", "b", "c" });
        var set2 = Immutable.Set<string>.Create(new List<string> { "a", "b" });

        Assert.IsTrue(set2.IsSubset(set1));
        Assert.IsFalse(set1.IsSubset(set2));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void IsSubset_EmptySet_ReturnsTrue()
    {
        var set1 = Immutable.Set<string>.Create(new List<string> { "a", "b", "c" });
        var set2 = Immutable.Set<string>.Empty;

        Assert.IsTrue(set2.IsSubset(set1));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void IsSubset_DifferentSets_ReturnsFalse()
    {
        var set1 = Immutable.Set<string>.Create(new List<string> { "a", "b", "c" });
        var set2 = Immutable.Set<string>.Create(new List<string> { "x", "y" });

        Assert.IsFalse(set2.IsSubset(set1));
        Assert.IsFalse(set1.IsSubset(set2));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Intersects_HasIntersection_ReturnsTrue()
    {
        var set1 = Immutable.Set<string>.Create(new List<string> { "a", "b", "c" });
        var set2 = Immutable.Set<string>.Create(new List<string> { "b", "c", "d" });

        Assert.IsTrue(set1.Intersects(set2));
        Assert.IsTrue(set2.Intersects(set1));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Intersects_NoIntersection_ReturnsFalse()
    {
        var set1 = Immutable.Set<string>.Create(new List<string> { "a", "b", "c" });
        var set2 = Immutable.Set<string>.Create(new List<string> { "d", "e", "f" });

        Assert.IsFalse(set1.Intersects(set2));
        Assert.IsFalse(set2.Intersects(set1));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Intersects_EmptySet_ReturnsFalse()
    {
        var set1 = Immutable.Set<string>.Create(new List<string> { "a", "b", "c" });
        var set2 = Immutable.Set<string>.Empty;

        Assert.IsFalse(set1.Intersects(set2));
        Assert.IsFalse(set2.Intersects(set1));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Add_CreatesCopy()
    {
        var values = new List<string> { "a", "b", "c" };
        var set1 = Immutable.Set<string>.Create(values);
        var set2 = set1.Add("d");

        // Original set should be unchanged
        Assert.AreEqual(values.Count, set1.Count);
        Assert.IsTrue(values.All(v => set1.Contains(v)));

        // New set should contain the added value
        values.Add("d");
        Assert.AreEqual(values.Count, set2.Count);
        Assert.IsTrue(values.All(v => set2.Contains(v)));
    }


    [TestMethod]
    [TestCategory("L0")]
    public void Remove_CreatesCopy()
    {
        var values = new List<string> { "a", "b", "c" };
        var set1 = Immutable.Set<string>.Create(values);
        var set2 = set1.Remove("c");

        // Original set should be unchanged
        Assert.AreEqual(values.Count, set1.Count);
        Assert.IsTrue(values.All(v => set1.Contains(v)));

        // New set should not contain the removed value
        values.Remove("c");
        Assert.AreEqual(values.Count, set2.Count);
        Assert.IsTrue(values.All(v => set2.Contains(v)));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Union_DisjointSets_AreCombined()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "d", "e", "f" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);
        var union = set1 | set2;

        Assert.AreEqual(values1.Count + values2.Count, union.Count);
        Assert.IsTrue(values1.All(v => union.Contains(v)));
        Assert.IsTrue(values2.All(v => union.Contains(v)));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Union_IntersectingSets_ShareItems()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "c", "d", "e" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);
        var union = set1 | set2;

        var unionValues = new List<string> { "a", "b", "c", "d", "e" };
        Assert.AreEqual(unionValues.Count, union.Count);
        Assert.IsTrue(unionValues.All(v => union.Contains(v)));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Intersection_DisjointSets_IsEmpty()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "d", "e", "f" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);
        var intersection = set1 & set2;

        Assert.IsTrue(intersection.IsEmpty);
    }


    [TestMethod]
    [TestCategory("L0")]
    public void Intersection_IntersectingSets_ReturnsIntersection()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "b", "c", "d" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);
        var intersection = set1 & set2;

        var intersectionValues = new List<string> { "b", "c" };
        Assert.AreEqual(intersectionValues.Count, intersection.Count);
        Assert.IsTrue(intersectionValues.All(v => intersection.Contains(v)));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void SetDifference_DisjointSets_KeepsOriginalItems()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "d", "e", "f" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);
        var difference = set1 - set2;

        Assert.AreEqual(values1.Count, difference.Count);
        Assert.IsTrue(values1.All(v => difference.Contains(v)));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void SetDifference_IntersectingSets_RemovesItems()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "c", "d", "e" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);
        var difference = set1 - set2;

        var differenceValues = new List<string> { "a", "b" };
        Assert.AreEqual(differenceValues.Count, difference.Count);
        Assert.IsTrue(differenceValues.All(v => difference.Contains(v)));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Equals_IdenticalSets_ReturnsTrue()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "a", "b", "c" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);

        Assert.IsTrue(set1.Equals(set2));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void Equals_DifferentSets_ReturnsFalse()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var values2 = new List<string> { "d", "e", "f" };

        var set1 = Immutable.Set<string>.Create(values1);
        var set2 = Immutable.Set<string>.Create(values2);

        Assert.IsFalse(set1.Equals(set2));
    }

    [TestMethod]
    [TestCategory("L0")]
    public void RemoveAllItems_ThenAdd()
    {
        var values1 = new List<string> { "a", "b", "c" };
        var set = Immutable.Set<string>.Create(values1);

        foreach (var value in values1)
        {
            set = set.Remove(value);
        }
        Assert.IsTrue(set.IsEmpty);

        var values2 = new List<string> { "d", "e", "f" };
        foreach (var value in values2)
        {
            set = set.Add(value);
        }

        Assert.AreEqual(values2.Count, set.Count);
        Assert.IsTrue(values2.All(v => set.Contains(v)));
    }
}