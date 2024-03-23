// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

/// <summary>
/// BoundNode extension methods.
/// </summary>
internal static class BoundNodeUtil
{
    /// <summary>
    /// If this compare node is not equivalent to a comparison of <paramref name="scopeIndex"/> against a constant i8 value
    /// of the form <c><paramref name="scopeIndex"/> <paramref name="op"/> <paramref name="value"/></c>, returns false.
    ///
    /// Otherwise, if recognized as such, returns true and populates <paramref name="op"/> and <paramref name="value"/>
    /// appropriately, normalizing to order <paramref name="scopeIndex"/> first in the comparison. <paramref name="op"/>
    /// is normalized to be one of <see cref="CompareOp.Less"/>, <see cref="CompareOp.LessEqual"/>, <see cref="CompareOp.Equal"/>,
    /// <see cref="CompareOp.NotEqual"/>, <see cref="CompareOp.GreaterEqual"/>, or <see cref="CompareOp.Greater"/>.
    ///
    /// REVIEW: How much of this logic belongs to the reducer?
    /// </summary>
    public static bool TryGetConstIndexCheck(this BndCompareNode cmp, ArgScope scopeIndex, out CompareOp op, out long value)
    {
        Validation.AssertValue(scopeIndex, nameof(scopeIndex));
        Validation.Assert(scopeIndex.IsIndex, nameof(scopeIndex));

        op = default;
        value = -1;

        if (cmp.Ops.Length != 1)
            return false;

        var args = cmp.Args;
        Validation.Assert(args.Length == 2);

        op = cmp.Ops[0];
        BoundNode argInd = null;
        bool reversed = false;
        for (int iarg = 0; iarg < 2; iarg++)
        {
            if (args[iarg] is BndScopeRefNode bsrn && bsrn.Scope == scopeIndex)
            {
                Validation.Assert(bsrn.Type == DType.I8Req);

                if (!args[1 - iarg].TryGetI8(out value))
                    return false;

                reversed = iarg == 1;
                argInd = args[iarg];
                break;
            }
        }

        if (argInd == null)
            return false;

        op = op.ClearCi().ClearStrict().SimplifyForTotalOrder();
        if (reversed)
            op = op.GetReverse();
#if DEBUG
        var (root, mods) = op.GetParts();
        Validation.Assert(!mods.IsNot() || root == CompareRoot.Equal, "Bad op normalization");
#endif
        return true;
    }
}
