// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using Integer = System.Numerics.BigInteger;

[TestClass]
public sealed class CodeGenTests : CodeGenTestBase
{
    private readonly OperationRegistry _opers;

    protected override OperationRegistry Operations => _opers;

    public CodeGenTests()
        : base(new AggregateGeneratorRegistry(TestGenerators.Instance, MultiFormGenerators.Instance))
    {
        _opers = new AggregateOperationRegistry(TestFunctions.Instance, MultiFormOperations.Instance);
    }

    [TestMethod]
    public void GeneralBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/General");
    }

    [TestMethod]
    public void FunctionBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Functions", options: TestCodeOptions.AllowGeneral);
    }

    [TestMethod]
    public void OperatorBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Operators", subDirs: true);
    }

    [TestMethod]
    public void ILBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/IL", subDirs: false, options: TestCodeOptions.WithIL);
    }

    [TestMethod]
    public void CompareILBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/IL/Compare", subDirs: true, options: TestCodeOptions.WithIL);
    }

    [TestMethod]
    public void ModuleILTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Module/IL",
            options: TestCodeOptions.WithIL | TestCodeOptions.SplitBlocks);
    }

    [TestMethod]
    public void SpecialBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Special", options: TestCodeOptions.WithIL);
    }

    [TestMethod]
    public void StreamingBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Streaming", options: TestCodeOptions.Streaming);
    }

    [TestMethod]
    public void SerializationBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Serialization", options: TestCodeOptions.WithBytes);
    }

    [TestMethod]
    public void TensorBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Tensors", subDirs: true, options: TestCodeOptions.TupNewLine);
    }

    [TestMethod]
    public void TensorFuncsBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/TensorFuncs", subDirs: true, options: TestCodeOptions.TupNewLine);
    }

    [TestMethod]
    public void ImageFuncsBaselineTests()
    {
        Config.ShowHex = true;
        DoBaselineTests(ProcessFile, @"CodeGen/ImageFuncs", subDirs: true, options: TestCodeOptions.TupNewLine);
    }

    [TestMethod]
    public void VolatileBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Volatile", options: TestCodeOptions.ShowBndKinds | TestCodeOptions.WithIL);
    }

    /// <summary>
    /// This one is to separate out work in progress. Normally, when pushed to master,
    /// this directory will be empty.
    /// </summary>
    [TestMethod]
    public void WipBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"CodeGen/Wip", options: TestCodeOptions.WithIL);
    }

    [TestMethod]
    public void CachingAbleDelaysGetEnumerator()
    {
        var seq = CreateThrowingSequence<long>();
        var cache = new CachingEnumerable<long>(seq);
        var ator = cache.GetEnumerator();
        Assert.ThrowsException<InvalidOperationException>(() => ator.MoveNext());
    }

    [TestMethod]
    public void IndexedSeqDelaysGetEnumerator()
    {
        var seq = CreateThrowingSequence<(long, long)>();
        var cache = IndexedSequence<long>.Create(-1, seq);
        var ator = cache.GetEnumerator();
        Assert.ThrowsException<InvalidOperationException>(() => ator.MoveNext());
    }

    #region Extra hand crafted tests for code coverage

    [TestMethod]
    public void CompoundComparison()
    {
        // REVIEW: Is this needed anymore? Seems like we can get coverage with
        // normal rexl now that null < non-null is true.
        InitFile();
        SetGlobalTypes(
            DType.EmptyRecordReq
                .AddNameType(new DName("a"), DType.R8Opt)
                .AddNameType(new DName("b"), DType.R8Opt)
                .AddNameType(new DName("c"), DType.R8Opt)
                .AddNameType(new DName("d"), DType.R8Opt)
        );

        var fma = RexlFormula.Create(SourceContext.Create("a @< b @< c @< d"));
        ValidateScript(fma);
        Assert.IsFalse(fma.HasDiagnostics);

        var host = new BindHostImpl(this);
        var bfma = BoundFormula.Create(fma, host);
        ValidateBfma(bfma, host);
        Assert.IsFalse(bfma.HasErrors);

        var res = bfma.BoundTree as BndCompareNode;
        Assert.IsNotNull(res);
        Assert.AreEqual(res.Ops.Length, 3);

        // Create a modified BndCompareNode that has None for its middle op.
        var mod = res.SuppressOp(1);

        // REVIEW: Should we baseline IL?
        var fn = CodeGenDirect.Run(mod).Func;

        bool result;
        result = (bool)fn(new object[] { 1.0, 2.0, 3.0, 4.0 });
        Assert.IsTrue(result);

        result = (bool)fn(new object[] { 3.0, 4.0, 1.0, 2.0 });
        Assert.IsTrue(result);

        result = (bool)fn(new object[] { 2.0, 2.0, 3.0, 4.0 });
        Assert.IsFalse(result);

        result = (bool)fn(new object[] { 1.0, 2.0, 5.0, 4.0 });
        Assert.IsFalse(result);

        result = (bool)fn(new object[] { null, 2.0, 3.0, 4.0 });
        Assert.IsTrue(result);

        result = (bool)fn(new object[] { 1.0, null, 3.0, 4.0 });
        Assert.IsFalse(result);

        result = (bool)fn(new object[] { 1.0, 2.0, null, 4.0 });
        Assert.IsTrue(result);

        result = (bool)fn(new object[] { 1.0, 2.0, 3.0, null });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CancelCount()
    {
        static IEnumerable<long> Forever()
        {
            for (long i = 0; ; i++)
                yield return i;
        }

        InitFile();
        SetGlobalTypes(DType.EmptyRecordReq.AddNameType(new DName("X"), DType.I8Req.ToSequence()));

        var fma = RexlFormula.Create(SourceContext.Create("X->Count()"));
        ValidateScript(fma);
        Assert.IsFalse(fma.HasDiagnostics);

        var host = new BindHostImpl(this);
        var bfma = BoundFormula.Create(fma, host);
        ValidateBfma(bfma, host);
        Assert.IsFalse(bfma.HasErrors);

        var codeGen = new EnumerableCodeGenerator(new TestEnumTypeManager(), TestGenerators.Instance);
        // REVIEW: Should we baseline the IL? Currently we just dump it to Console.
        var resCodeGen = codeGen.Run(bfma.BoundTree, host: null, Console.WriteLine);
        Assert.AreEqual(2, resCodeGen.Globals.Length);
        Assert.IsTrue(resCodeGen.Globals[0].IsCtx);

        var ctx = new TestExecCtx(sb: null, resCodeGen.IdBndMap.Count, 10);

        long result = -1;
        try
        {
            result = (long)resCodeGen.Func(new object[] { ctx, Forever() });
            Assert.Fail();
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            Assert.IsInstanceOfType(ex, typeof(TestExecCtx.ExecException));
        }
        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public void RangeCursoring()
    {
        var range = (RangeGen.Exec(5, 100, 3) as ICursorable<long>).VerifyValue();
        using var cursTo = range.GetCursor();
        using var cursNx = range.GetCursor();

        Assert.IsTrue(cursTo.MoveTo(0));
        Assert.AreEqual(0, cursTo.Index);
        Assert.AreEqual(5, cursTo.Value);
        Assert.AreNotEqual(cursNx.Index, cursTo.Index);
        Assert.AreNotEqual(cursNx.Value, cursTo.Value);
        Assert.AreNotEqual(cursNx.Current, cursTo.Current);

        Assert.IsTrue(cursNx.MoveNext());
        Assert.AreEqual(cursTo.Index, cursNx.Index);
        Assert.AreEqual(cursTo.Value, cursNx.Value);
        Assert.AreEqual(cursTo.Current, cursNx.Current);

        Assert.IsFalse(cursTo.MoveTo(32));
        Assert.IsTrue(cursTo.MoveTo(31));
        Assert.IsFalse(cursTo.MoveNext());

        foreach (var (i, v) in new[] { (17, 56), (1, 8), (30, 95), (24, 77) })
        {
            Assert.IsTrue(cursTo.MoveTo(i));

            Assert.AreEqual(i, cursTo.Index);
            Assert.AreNotEqual(i, cursNx.Index);
            Assert.AreEqual(v, cursTo.Value);
            Assert.AreEqual(v, cursTo.Current);

            Assert.IsTrue(cursNx.MoveTo(0));
            for (int n = 0; n < i; n++)
                Assert.IsTrue(cursNx.MoveNext());

            Assert.AreEqual(i, cursNx.Index);
            Assert.AreEqual(v, cursNx.Value);
            Assert.AreEqual(v, cursNx.Current);

            Assert.IsTrue(cursTo.MoveNext());
            Assert.AreEqual(v + 3, cursTo.Current);
            Assert.IsTrue(cursTo.MoveTo(i - 1));
            Assert.AreEqual(v - 3, cursTo.Current);
        }
    }

    [TestMethod]
    public void SequenceCursoring()
    {
        {
            long num = 32;
            long first = 5;
            long step = 3;

            var range = (SequenceGen.Exec(num, first, step) as ICursorable<long>).VerifyValue();
            using var cursTo = range.GetCursor();
            using var cursNx = range.GetCursor();

            Assert.IsTrue(cursTo.MoveTo(0));
            Assert.AreEqual(0, cursTo.Index);
            Assert.AreEqual(first, cursTo.Value);
            Assert.AreNotEqual(cursNx.Index, cursTo.Index);
            Assert.AreNotEqual(cursNx.Value, cursTo.Value);
            Assert.AreNotEqual(cursNx.Current, cursTo.Current);

            Assert.IsTrue(cursNx.MoveNext());
            Assert.AreEqual(cursTo.Index, cursNx.Index);
            Assert.AreEqual(cursTo.Value, cursNx.Value);
            Assert.AreEqual(cursTo.Current, cursNx.Current);

            Assert.IsFalse(cursTo.MoveTo(num));
            Assert.IsTrue(cursTo.MoveTo(num - 1));
            Assert.IsFalse(cursTo.MoveNext());

            foreach (var (i, v) in new[] { (17, 56), (1, 8), (30, 95), (24, 77) })
            {
                Assert.IsTrue(cursTo.MoveTo(i));

                Assert.AreEqual(i, cursTo.Index);
                Assert.AreNotEqual(i, cursNx.Index);
                Assert.AreEqual(v, cursTo.Value);
                Assert.AreEqual(v, cursTo.Current);

                Assert.IsTrue(cursNx.MoveTo(0));
                for (int n = 0; n < i; n++)
                    Assert.IsTrue(cursNx.MoveNext());

                Assert.AreEqual(i, cursNx.Index);
                Assert.AreEqual(v, cursNx.Value);
                Assert.AreEqual(v, cursNx.Current);

                Assert.IsTrue(cursTo.MoveNext());
                Assert.AreEqual(v + 3, cursTo.Current);
                Assert.IsTrue(cursTo.MoveTo(i - 1));
                Assert.AreEqual(v - 3, cursTo.Current);
            }
        }

        {
            long num = 32;
            ulong first = 5;
            ulong step = 3;

            var range = (SequenceGen.Exec(num, first, step) as ICursorable<ulong>).VerifyValue();
            using var cursTo = range.GetCursor();
            using var cursNx = range.GetCursor();

            Assert.IsTrue(cursTo.MoveTo(0));
            Assert.AreEqual(0, cursTo.Index);
            Assert.AreEqual(first, cursTo.Value);
            Assert.AreNotEqual(cursNx.Index, cursTo.Index);
            Assert.AreNotEqual(cursNx.Value, cursTo.Value);
            Assert.AreNotEqual(cursNx.Current, cursTo.Current);

            Assert.IsTrue(cursNx.MoveNext());
            Assert.AreEqual(cursTo.Index, cursNx.Index);
            Assert.AreEqual(cursTo.Value, cursNx.Value);
            Assert.AreEqual(cursTo.Current, cursNx.Current);

            Assert.IsFalse(cursTo.MoveTo(num));
            Assert.IsTrue(cursTo.MoveTo(num - 1));
            Assert.IsFalse(cursTo.MoveNext());

            foreach (var (i, v) in new[] { (17, 56UL), (1, 8UL), (30, 95UL), (24, 77UL) })
            {
                Assert.IsTrue(cursTo.MoveTo(i));

                Assert.AreEqual(i, cursTo.Index);
                Assert.AreNotEqual(i, cursNx.Index);
                Assert.AreEqual(v, cursTo.Value);
                Assert.AreEqual(v, cursTo.Current);

                Assert.IsTrue(cursNx.MoveTo(0));
                for (int n = 0; n < i; n++)
                    Assert.IsTrue(cursNx.MoveNext());

                Assert.AreEqual(i, cursNx.Index);
                Assert.AreEqual(v, cursNx.Value);
                Assert.AreEqual(v, cursNx.Current);

                Assert.IsTrue(cursTo.MoveNext());
                Assert.AreEqual(v + 3, cursTo.Current);
                Assert.IsTrue(cursTo.MoveTo(i - 1));
                Assert.AreEqual(v - 3, cursTo.Current);
            }
        }

        {
            long num = 32;
            Integer first = 5;
            Integer step = 3;

            var range = (SequenceGen.Exec(num, first, step) as ICursorable<Integer>).VerifyValue();
            using var cursTo = range.GetCursor();
            using var cursNx = range.GetCursor();

            Assert.IsTrue(cursTo.MoveTo(0));
            Assert.AreEqual(0, cursTo.Index);
            Assert.AreEqual(first, cursTo.Value);
            Assert.AreNotEqual(cursNx.Index, cursTo.Index);
            Assert.AreNotEqual(cursNx.Value, cursTo.Value);
            Assert.AreNotEqual(cursNx.Current, cursTo.Current);

            Assert.IsTrue(cursNx.MoveNext());
            Assert.AreEqual(cursTo.Index, cursNx.Index);
            Assert.AreEqual(cursTo.Value, cursNx.Value);
            Assert.AreEqual(cursTo.Current, cursNx.Current);

            Assert.IsFalse(cursTo.MoveTo(num));
            Assert.IsTrue(cursTo.MoveTo(num - 1));
            Assert.IsFalse(cursTo.MoveNext());

            foreach (var (i, v) in new[] { (17, new Integer(56)), (1, 8), (30, 95), (24, 77) })
            {
                Assert.IsTrue(cursTo.MoveTo(i));

                Assert.AreEqual(i, cursTo.Index);
                Assert.AreNotEqual(i, cursNx.Index);
                Assert.AreEqual(v, cursTo.Value);
                Assert.AreEqual(v, cursTo.Current);

                Assert.IsTrue(cursNx.MoveTo(0));
                for (int n = 0; n < i; n++)
                    Assert.IsTrue(cursNx.MoveNext());

                Assert.AreEqual(i, cursNx.Index);
                Assert.AreEqual(v, cursNx.Value);
                Assert.AreEqual(v, cursNx.Current);

                Assert.IsTrue(cursTo.MoveNext());
                Assert.AreEqual(v + 3, cursTo.Current);
                Assert.IsTrue(cursTo.MoveTo(i - 1));
                Assert.AreEqual(v - 3, cursTo.Current);
            }
        }

        {
            long num = 32;
            var first = 5.0;
            var step = 0.1;

            var range = (SequenceGen.Exec(num, first, step) as ICursorable<double>).VerifyValue();
            using var cursTo = range.GetCursor();
            using var cursNx = range.GetCursor();

            Assert.IsTrue(cursTo.MoveTo(0));
            Assert.AreEqual(0, cursTo.Index);
            Assert.AreEqual(first, cursTo.Value);
            Assert.AreNotEqual(cursNx.Index, cursTo.Index);
            Assert.AreNotEqual(cursNx.Value, cursTo.Value);
            Assert.AreNotEqual(cursNx.Current, cursTo.Current);

            Assert.IsTrue(cursNx.MoveNext());
            Assert.AreEqual(cursTo.Index, cursNx.Index);
            Assert.AreEqual(cursTo.Value, cursNx.Value);
            Assert.AreEqual(cursTo.Current, cursNx.Current);

            Assert.IsFalse(cursTo.MoveTo(num));
            Assert.IsTrue(cursTo.MoveTo(num - 1));
            Assert.IsFalse(cursTo.MoveNext());

            var valPrev = double.NaN;
            var valNext = first;
            for (long i = 0; i < num; i++)
            {
                Assert.IsTrue(cursTo.MoveTo(0));
                Assert.AreEqual(0, cursTo.Index);
                Assert.AreEqual(first, cursTo.Value);
                Assert.AreEqual(first, cursTo.Current);

                var val = valNext;
                Assert.IsTrue(cursTo.MoveTo(i));
                Assert.AreEqual(i, cursTo.Index);
                Assert.AreEqual(val, cursTo.Value);
                Assert.AreEqual(val, cursTo.Current);

                Assert.IsTrue(cursNx.MoveTo(0));
                for (int n = 0; n < i; n++)
                    Assert.IsTrue(cursNx.MoveNext());

                Assert.AreEqual(i, cursNx.Index);
                Assert.AreEqual(val, cursNx.Value);
                Assert.AreEqual(val, cursNx.Current);

                if (i < num - 1)
                {
                    valNext = first + (i + 1) * step;
                    Assert.IsTrue(cursTo.MoveNext());
                    Assert.AreEqual(valNext, cursTo.Current);
                }
                if (i > 0)
                {
                    Assert.IsTrue(cursTo.MoveTo(i - 1));
                    Assert.AreEqual(valPrev, cursTo.Current);
                }
                valPrev = val;
            }
        }
    }

    #endregion Extra hand crafted tests for code coverage
}
