// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Microsoft.Rexl.Utility;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public partial class JitIssues
{
    /// <summary>
    /// This tests for the presence of a JIT constant folding optimization bug where long -> float is not
    /// rounding correctly. When this test fails in release builds, it's a good sign that the JIT bug has
    /// been fixed and we can add the aggressive inlining attribute to <see cref="NumUtil.ToR4(long)"/>.
    /// See the comments on that method.
    /// </summary>
    [TestMethod]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void JitI8ToR4FoldBug()
    {
        long value = 0x4000_0040_0000_0001L;
        float res = (float)value;
#if DEBUG
        Assert.AreEqual(0x5E800001U, res.ToBits());
#else
        Assert.AreEqual(0x5E800000U, res.ToBits());
#endif
    }
}
