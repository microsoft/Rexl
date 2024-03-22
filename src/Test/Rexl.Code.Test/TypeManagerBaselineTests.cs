// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using CodeGenerator = CachingEnumerableCodeGenerator;
using Integer = System.Numerics.BigInteger;

[TestClass]
public sealed class TypeManagerBaselineTests : RexlTestsBaseType<bool>
{
    private EnumerableCodeGeneratorBase _codeGen;
    private EnumerableTypeManager _typeManager;

    private TypeManager GetTypeManager()
    {
        return _typeManager ??= new TestEnumTypeManager();
    }

    private EnumerableCodeGeneratorBase GetCodeGen()
    {
        return _codeGen ??= new CodeGenerator(_typeManager ??= new TestEnumTypeManager(), TestGenerators.Instance);
    }

    private (DType type, object value) Eval(string formula)
    {
        return TestUtils.ExecuteFormula(formula, GetCodeGen());
    }

    [TestMethod]
    public void Consts()
    {
        int count = DoBaselineTests(
            Run, @"TypeManager\Consts.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            DoOne(DType.Text, "Howdy");
            DoOne(DType.R8Req, 5d);
            DoOne(DType.R4Req, 5f);
            DoOne(DType.IAReq, new Integer(5L));
            DoOne(DType.I8Req, 5L);
            DoOne(DType.I4Req, 5);
            DoOne(DType.I2Req, (short)5);
            DoOne(DType.I1Req, (sbyte)5);
            DoOne(DType.U8Req, 5UL);
            DoOne(DType.U4Req, 5U);
            DoOne(DType.U2Req, (ushort)5);
            DoOne(DType.U1Req, (byte)5);
            DoOne(DType.BitReq, true);

            DoOne(DType.R8Opt, 5d);
            DoOne(DType.R4Opt, 5f);
            DoOne(DType.IAOpt, new Integer(5L));
            DoOne(DType.I8Opt, 5L);
            DoOne(DType.I4Opt, 5);
            DoOne(DType.I2Opt, (short)5);
            DoOne(DType.I1Opt, (sbyte)5);
            DoOne(DType.U8Opt, 5UL);
            DoOne(DType.U4Opt, 5U);
            DoOne(DType.U2Opt, (ushort)5);
            DoOne(DType.U1Opt, (byte)5);
            DoOne(DType.BitOpt, true);

            DoOne(DType.Text, null);
            DoOne(DType.R8Opt, null);
            DoOne(DType.R4Opt, null);
            DoOne(DType.IAOpt, null);
            DoOne(DType.I8Opt, null);
            DoOne(DType.I4Opt, null);
            DoOne(DType.I2Opt, null);
            DoOne(DType.I1Opt, null);
            DoOne(DType.U8Opt, null);
            DoOne(DType.U4Opt, null);
            DoOne(DType.U2Opt, null);
            DoOne(DType.U1Opt, null);
            DoOne(DType.BitOpt, null);

            DoOne(DType.IAReq.ToSequence(), new[] { new Integer(5L) });
            DoOne(DType.I8Req.ToSequence(), new[] { 5L });
            DoOne(DType.I4Req.ToSequence(), new[] { 5 });
            DoOne(DType.I2Req.ToSequence(), new[] { (short)5 });
            DoOne(DType.I1Req.ToSequence(), new[] { (sbyte)5 });
            DoOne(DType.U8Req.ToSequence(), new[] { 5UL });
            DoOne(DType.U4Req.ToSequence(), new[] { 5U });
            DoOne(DType.U2Req.ToSequence(), new[] { (ushort)5 });
            DoOne(DType.U1Req.ToSequence(), new[] { (byte)5 });
            DoOne(DType.BitReq.ToSequence(), new[] { true });

            DoOne(DType.I8Req.ToSequence(), Enumerable.Repeat<long>(5L, 3));

            DoOne(DType.UriGen, MakeLink("blah", ImageFlavor));
            DoOne(DType.UriGen, null);

            DoOne(DType.CreateTuple(false, DType.I8Req, DType.Text), new TupleImpl<long, string>());
            DoOne(DType.CreateTuple(true, DType.I8Req, DType.Text), new TupleImpl<long, string>());
            DoOne(DType.CreateTuple(true, DType.I8Req, DType.Text), null);

            DoOne(DType.R4Req.ToTensor(false, 3), Tensor<float>.CreateFill(1.5f, 2, 3, 4));
            DoOne(DType.R4Req.ToTensor(true, 3), Tensor<float>.CreateFill(1.5f, 2, 3, 4));
            DoOne(DType.R4Req.ToTensor(true, 3), null);

            // Failures:
            DoOne(DType.I8Req, 5);
            DoOne(DType.UriGen, "https://blah");

            // Record cases:
            var (type, val) = Eval("{A:3, B:true, C:\"Blah\"}");
            Assert.IsTrue(type.IsRecordReq);
            DoOne(type, val);

            (type, val) = Eval("{A:3, B:true, C:\"Blah\"}->Opt()");
            Assert.IsTrue(type.IsRecordOpt);
            DoOne(type, val);
            DoOne(type, null);
            DoOne(type.ToReq(), val);
        }

        void DoOne(DType type, object val)
        {
            var tm = GetTypeManager();

            Sink.TWrite("Type: ").TWriteType(type).TWrite(", SysType: ").TWritePrettyType(val?.GetType()).WriteLine();
            if (!tm.TryWrapConst(type, val, out var bcn))
                Sink.WriteLine("Failed");
            else
            {
                Assert.IsNotNull(bcn);
                Sink.WriteLine("Type: {0}, Kind: {1}", bcn.Type.Serialize(), bcn.Kind.ToString());

                if (type.IsOpt && val != null)
                    Assert.AreEqual(type.ToReq(), bcn.Type);
                else
                    Assert.AreEqual(type, bcn.Type);

                if (bcn is BndTmConstNode btm)
                    Assert.AreSame(tm, btm.TypeManager);
                if (bcn is BndArrConstNode barr && val is Array arr)
                {
                    Assert.AreEqual(arr.Length, barr.Length);
                    var res = barr.SetItems(arr);
                    Assert.AreEqual(barr, res);
                    res = barr.SetItems((Array)arr.Clone());
                    Assert.AreNotEqual(barr, res);
                }
                if (bcn is BndTenConstNode bten)
                    Assert.IsTrue(val is Tensor ten && bten.Tensor == ten);
            }
            Sink.WriteLine("###");
        }
    }
}
