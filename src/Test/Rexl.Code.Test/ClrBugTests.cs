// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

/// <summary>
/// These tests track known bugs in the CLR. The tests should fail when the tests
/// no longer reproduce.
/// </summary>
[TestClass]
public class ClrBugTests
{
    /// <summary>
    /// Dynamic code gen computes the max stack size incorrectly, easily overflowing
    /// 16 bits. This can cause an <see cref="InvalidProgramException"/>.
    /// </summary>
    [TestMethod]
    public void MaxStackDepthOverflow()
    {
        Run((1 << 14) - 1);
        Assert.ThrowsException<InvalidProgramException>(() => Run((1 << 14) - 0)); // Throws!
        Run((1 << 14) + 1);

        void Run(int num)
        {
            Console.WriteLine("Start: {0}", num);
            var fn = GetCode(num);
            var val = fn(0);
            Console.WriteLine("  Val: {0}", val);
        }
    }

    /// <summary>
    /// The <paramref name="num"/> parameter is the number of basic blocks. Each has a max stack
    /// depth of four. There is one final basic block with max stack of one. The ILGenerator
    /// erroneously adds these, so the final value can overflow 2^16. When that result mod 2^16
    /// is less than required, the CLR throws an <see cref="InvalidProgramException"/>.
    /// </summary>
    private static Func<long, long> GetCode(int num)
    {
        var module = typeof(ClrBugTests).Module;
        var sts = new[] { typeof(object), typeof(long) };
        var gen = new DynamicMethod("code", typeof(long), sts, module);
        var ilg = gen.GetILGenerator();

        var loc = ilg.DeclareLocal(typeof(long));
        ilg.Emit(OpCodes.Ldarg_1);
        ilg.Emit(OpCodes.Stloc, loc);

        for (int i = 0; i < num; i++)
        {
            ilg.Emit(OpCodes.Ldloc, loc);
            ilg.Emit(OpCodes.Ldc_I4_1);
            ilg.Emit(OpCodes.Ldc_I4_1);
            ilg.Emit(OpCodes.Ldc_I4_2);
            ilg.Emit(OpCodes.Add);
            ilg.Emit(OpCodes.Add);
            ilg.Emit(OpCodes.Add);
            ilg.Emit(OpCodes.Stloc, loc);

            // Unconditional jump to next block.
            var labNext = ilg.DefineLabel();
            ilg.Emit(OpCodes.Br, labNext);
            ilg.MarkLabel(labNext);
        }

        ilg.Emit(OpCodes.Ldloc, loc);
        ilg.Emit(OpCodes.Ret);

        var fin = typeof(ILGenerator).GetField("m_maxStackSize", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(fin);
        Assert.AreEqual(typeof(int), fin.FieldType);
        var stack = (int)fin.GetValue(ilg);
        Console.WriteLine("  Stk: {0}, {1}", stack, stack & 0xFFFF);

        var res = gen.CreateDelegate(typeof(Func<long, long>), null);
        return (Func<long, long>)res;
    }
}
