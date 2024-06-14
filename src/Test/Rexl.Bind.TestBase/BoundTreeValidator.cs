// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Bind;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

/// <summary>
/// Does a complete visit over the bound tree and ensures correctness of the
/// ChildCount, ChildKinds, and AllKinds properties. Issues test failures otherwise.
/// </summary>
public sealed class BoundTreeValidator : NoopBoundTreeVisitor
{
    private readonly Stack<(int count, BndNodeKindMask kinds)> _stack;
    private int _count;
    private BndNodeKindMask _kinds;

    private BoundTreeValidator()
    {
        _stack = new Stack<(int count, BndNodeKindMask kinds)>();
    }

    public static void Run(BoundNode tree, bool hasErrors)
    {
        // If any children are impure then an error should have been generated.
        Assert.IsTrue((tree.ChildKinds & BndNodeKindMask.CallProcedure) == 0 || hasErrors);

        var impl = new BoundTreeValidator();
        int num = tree.Accept(impl, 0);
        Assert.AreEqual(tree.NodeCount, num);

        Assert.AreEqual(0, impl._stack.Count);
        Assert.AreEqual(1, impl._count);
        Assert.AreEqual(impl._kinds, tree.AllKinds);

        // If no errors were reported, then the tree shouldn't have embedded errors.
        Assert.IsTrue(!tree.HasErrors || hasErrors);
    }

    protected override void VisitCore(BndLeafNode bnd, int idx)
    {
        Assert.IsTrue(bnd.ChildCount == 0);
        Assert.AreEqual((BndNodeKindMask)0, bnd.ChildKinds);
        var kinds = bnd.ThisKindMask;
        Assert.AreEqual(kinds, bnd.AllKinds);

        _count++;
        _kinds |= kinds;
    }

    protected override bool PreVisitCore(BndParentNode bnd, int idx)
    {
        if (bnd.OwnsScopes)
        {
            Assert.IsTrue(bnd is BndScopeOwnerNode);
            Assert.IsTrue((bnd.ThisKindMask & BndNodeKindMask.ScopeOwner) != 0);
        }
        else
            Assert.IsTrue((bnd.ThisKindMask & BndNodeKindMask.ScopeOwner) == 0);

        _stack.Push((_count, _kinds));
        _count = 0;
        _kinds = 0;
        return true;
    }

    protected override void PostVisitCore(BndParentNode bnd, int idx)
    {
        Assert.AreEqual(_count, bnd.ChildCount);
        Assert.AreEqual(_kinds, bnd.ChildKinds);
        var kinds = _kinds | bnd.ThisKindMask;
        Assert.AreEqual(kinds, bnd.AllKinds);

        (_count, _kinds) = _stack.Pop();
        _count++;
        _kinds |= kinds;
    }
}
