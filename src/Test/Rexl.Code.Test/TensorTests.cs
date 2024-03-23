// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Rexl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using IndexExeption = ArgumentOutOfRangeException;

[TestClass]
public sealed class TensorTests
{
    /// <summary>
    /// This provides code coverage for Transpose that is not possible through code gen.
    /// Stuff coverable by code gen is in the baseline tests.
    /// </summary>
    [TestMethod]
    public void ValidTranspose()
    {
        Tensor<long> ten;
        Tensor<long> tmp;

        ten = Tensor<long>.CreateFill(5);
        tmp = ten.Transpose();
        Assert.AreSame(ten, tmp);

        tmp = ten.Transpose(Array.Empty<int>());
        Assert.AreSame(ten, tmp);

        ten = Tensor<long>.CreateFill(5, 10);
        tmp = ten.Transpose();
        Assert.AreSame(ten, tmp);

        tmp = ten.Transpose(new int[] { 0 });
        Assert.AreSame(ten, tmp);

        ten = Tensor<long>.CreateFill(5, 3, 4);
        tmp = ten.Transpose(0, 1);
        Assert.AreSame(ten, tmp);
        tmp = ten.Transpose(1, 0);
        Assert.AreNotSame(ten, tmp);

        tmp = ten.Transpose(new int[] { 0, 1 });
        Assert.AreSame(ten, tmp);
        tmp = ten.Transpose(new int[] { 1, 0 });
        Assert.AreNotSame(ten, tmp);
    }

    /// <summary>
    /// This covers bad invocations of the Transpose overloads of <see cref="Tensor{T}"/>.
    /// </summary>
    [TestMethod]
    public void BadTranspose()
    {
        Tensor<long> ten;

        ten = Tensor<long>.CreateFill(5, 10);
        Assert.ThrowsException<ArgumentException>(() => ten.Transpose(new int[] { 1 }));

        ten = Tensor<long>.CreateFill(5, 3, 4);
        ten.Transpose(1, 0);
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(-1, 0));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(0, -1));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(2, 0));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(0, 2));
        Assert.ThrowsException<InvalidOperationException>(() => ten.Transpose(0, 0));
        Assert.ThrowsException<InvalidOperationException>(() => ten.Transpose(1, 1));

        ten = Tensor<long>.CreateFill(5, 2, 3, 2);
        ten.Transpose(1, 0, 2);
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(-1, 0, 2));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(1, -1, 2));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(1, 0, 3));
        Assert.ThrowsException<InvalidOperationException>(() => ten.Transpose(1, 0, 0));
        Assert.ThrowsException<InvalidOperationException>(() => ten.Transpose(1, 0, 1));

        ten = Tensor<long>.CreateFill(5, 2, 3, 2, 1);
        ten.Transpose(1, 0, 3, 2);
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(-1, 0, 3, 2));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(1, -1, 3, 2));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(1, 0, 4, 2));
        Assert.ThrowsException<IndexExeption>(() => ten.Transpose(1, 0, 4, -1));
        Assert.ThrowsException<InvalidOperationException>(() => ten.Transpose(1, 0, 3, 0));
        Assert.ThrowsException<InvalidOperationException>(() => ten.Transpose(1, 0, 3, 1));
    }

    /// <summary>
    /// This provides code coverage for ExpandDims that is not possible through code gen.
    /// Stuff coverable by code gen is in the baseline tests.
    /// </summary>
    [TestMethod]
    public void ValidExpandDims()
    {
        var ten = Tensor<long>.CreateFill(5, 2, 3, 2);
        Assert.AreEqual(Shape.Create(2, 3, 2), ten.Shape);

        Tensor<long> tmp;
        tmp = ten.ExpandDims(Array.Empty<int>());
        Assert.AreSame(ten, tmp);

        tmp = ten.ExpandDims(default(BitSet));
        Assert.AreSame(ten, tmp);

        tmp = ten.ExpandDims(1, 3, 4, 5, 6);
        Assert.AreEqual(Shape.Create(2, 1, 3, 1, 1, 1, 1, 2), tmp.Shape);
    }

    /// <summary>
    /// This covers bad invocations of the expand dims overloads of <see cref="Tensor{T}"/>.
    /// </summary>
    [TestMethod]
    public void BadExpandDims()
    {
        Tensor<long> ten;

        ten = Tensor<long>.CreateFill(5, 2, 3, 2);
        Assert.AreEqual(Shape.Create(2, 3, 2), ten.Shape);

        ten.ExpandDims(3);
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(-1)); // Too small.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(4)); // Too big.

        ten.ExpandDims(0, 4);
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(-1, 4)); // Too small.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(4, 0)); // Not sorted.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(2, 2)); // Duplicate.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(4, 5)); // Too big.

        ten.ExpandDims(0, 2, 5);
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(-1, 2, 5)); // Too small.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 5, 2)); // Not sorted.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 2, 2)); // Duplicate.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 2, 6)); // Too big.

        ten.ExpandDims(0, 2, 5, 6);
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(-1, 2, 5, 6)); // Too small.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 5, 2, 6)); // Not sorted.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 2, 2, 6)); // Duplicate.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 2, 5, 7)); // Too big.

        ten.ExpandDims(0, 2, 4, 5, 7);
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(-1, 2, 4, 5, 7)); // Too small.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 4, 2, 5, 7)); // Not sorted.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 2, 4, 4, 7)); // Duplicate.
        Assert.ThrowsException<IndexExeption>(() => ten.ExpandDims(0, 2, 4, 5, 8)); // Too big.
    }

    /// <summary>
    /// This provides code coverage for slicing that is not possible through code gen.
    /// Stuff coverable by code gen is in the baseline tests.
    /// </summary>
    [TestMethod]
    public void Slice()
    {
        Tensor<int> ten;
        var ind0 = SliceItem.CreateIndex(0, default);

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4), 4).GetSlice(ind0, out _);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4 * 4), 4, 4).GetSlice(ind0, ind0, out _);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4 * 4 * 4), 4, 4, 4).GetSlice(ind0, ind0, ind0, out _);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4 * 4 * 4 * 4), 4, 4, 4, 4).GetSlice(ind0, ind0, ind0, ind0, out _);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4 * 4 * 4 * 4 * 4), 4, 4, 4, 4, 4).GetSlice(ind0, ind0, ind0, ind0, ind0, out _);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4 * 4 * 4 * 4 * 4 * 4), 4, 4, 4, 4, 4, 4).GetSlice(new SliceItem[] { ind0, ind0, ind0, ind0, ind0, ind0 }, out _);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());
    }

    /// <summary>
    /// This provides code coverage for CreateFrom that is not possible through code gen.
    /// Stuff coverable by code gen is in the baseline tests.
    /// </summary>
    [TestMethod]
    public void CreateFrom()
    {
        Tensor<int> ten;

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4));
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(null);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(0, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(-1, Enumerable.Range(1, 4));
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(-1, null);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(-1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(-1, Enumerable.Range(1, 4), 0);
        Assert.IsNotNull(ten);
        Assert.AreEqual(1, ten.Rank);
        Assert.AreEqual(0, ten.Count);

        ten = Tensor<int>.CreateFrom(-1, null, 1);
        Assert.IsNotNull(ten);
        Assert.AreEqual(1, ten.Rank);
        Assert.AreEqual(-1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(null, Shape.Scalar);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(0, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(Enumerable.Range(1, 4), Shape.Scalar);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(-1, null, Shape.Scalar);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(-1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(-1, Enumerable.Range(1, 4), Shape.Scalar);
        Assert.IsNotNull(ten);
        Assert.AreEqual(0, ten.Rank);
        Assert.AreEqual(1, ten.GetFirst());

        ten = Tensor<int>.CreateFrom(-1, null, 4, 4);
        Assert.IsNotNull(ten);
        Assert.AreEqual(2, ten.Rank);
        Assert.AreEqual(16, ten.Count);
        Assert.IsTrue(ten.GetValues().All(i => i == -1));
    }

    /// Tests that <see cref="Tensor{T}.Builder"/> can be used for multiple creations without affecting
    /// the values in already built tensors.
    /// </summary>
    [TestMethod]
    public void TensorBuilderIsResuable()
    {
        var size = 10;
        var expectedBefore = Enumerable.Range(0, size).Select(val => val * 10).ToArray();

        var bldr = Tensor<int>.Builder.Create(Shape.Create(size));

        for (var i = 0; i < size; i++)
            bldr.Set(i, expectedBefore[i]);

        var tenBefore = bldr.BuildGeneric();

        // Change one value in the builder and build a new tensor.
        var changedIndex = 5;
        var expectedAfter = (int[])expectedBefore.Clone();
        expectedAfter[changedIndex] = 1000;

        bldr.Set(changedIndex, expectedAfter[changedIndex]);
        var tenAfter = bldr.BuildGeneric();

        for (var i = 0; i < size; i++)
        {
            Assert.AreEqual(expectedBefore[i], tenBefore.GetAtIndex(i));
            Assert.AreEqual(expectedAfter[i], tenAfter.GetAtIndex(i));
        }
    }
}
