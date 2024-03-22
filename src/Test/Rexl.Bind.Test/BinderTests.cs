// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using ArgTuple = Immutable.Array<BoundNode>;
using DirTuple = Immutable.Array<Directive>;
using NameTuple = Immutable.Array<DName>;
using ScopeTuple = Immutable.Array<ArgScope>;

[TestClass]
public sealed class BinderTests : BinderTestBase
{
    /// <summary>
    /// This one is to separate out work in progress. Normally, when pushed to master,
    /// this directory will be empty.
    /// </summary>
    [TestMethod]
    public void WipBaselineTests()
    {
        ShouldPrintTypedParseTree = true;
        DoBaselineTests(ProcessFile, @"Binder/Wip");
    }

    [TestMethod]
    public void GeneralBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/General", options: TestOptions.ProhibitModule);
    }

    [TestMethod]
    public void WithBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/With",
            options: TestOptions.Replicate | TestOptions.AllowVolatile);
    }

    [TestMethod]
    public void UdfBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Udf");
    }

    [TestMethod]
    public void OperatorBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Operators", subDirs: true);
    }

    [TestMethod]
    public void CallBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Call");
    }

    [TestMethod]
    public void FunctionBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Functions", options: TestOptions.AllowGeneral);
    }

    [TestMethod]
    public void FunctionVerboseBaselineTests()
    {
        Verb = BndNodePrinter.Verbosity.Default;
        DoBaselineTests(ProcessFile, @"Binder/Functions/Verbose");
    }

    [TestMethod]
    public void ProcedureBaselineTests()
    {
        Verb = BndNodePrinter.Verbosity.Default;
        DoBaselineTests(ProcessFile, @"Binder/Procedures", options: TestOptions.AllowProcedure);
    }

    [TestMethod]
    public void TensorFuncsBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/TensorFuncs");
    }

    [TestMethod]
    public void XtremeBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Xtreme");
    }

    [TestMethod]
    public void OptimizationTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Optimization");
    }

    [TestMethod]
    public void ModuleTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Module", options: TestOptions.SplitBlocks);
    }

    [TestMethod]
    public void VolatileBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Volatile",
            options: TestOptions.ShowBndKinds | TestOptions.AllowVolatile);
    }

    [TestMethod]
    public void DeprecationBaselineTests()
    {
        ShouldIncludeDiagLogInfo = true;
        DoBaselineTests(ProcessFile, @"Binder/Deprecations");
    }

    [TestMethod]
    public void GetItemCountBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/GetItemCount",
            options: TestOptions.ShowItemCount | TestOptions.AllowVolatile);
    }

    [TestMethod]
    public void StreamingBaselineTests()
    {
        DoBaselineTests(ProcessFile, @"Binder/Streaming", options: TestOptions.Streaming);
    }

    [TestMethod]
    public void TensorConversionTest()
    {
        Verb = BndNodePrinter.Verbosity.Default;
        int count = DoBaselineTests(Run, @"Binder/Special/TensorConversion.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TestOptions options)
        {
            var fma = RexlFormula.Create(SourceContext.Create("[ 3 + 5, 7 ]"));
            ValidateScript(fma);

            var host = new BindHostImpl(this);
            var bfma = BoundFormula.Create(fma, host, BindOptions.DontReduce);
            ValidateBfma(bfma, host);

            var seq = bfma.BoundTree as BndSequenceNode;
            Assert.IsNotNull(seq);
            var type = seq.Type.ItemTypeOrThis.ToTensor(false, 1);
            var ten = BndTensorNode.Create(type, seq.Items, Shape.Create(seq.Items.Length));
            Sink.TWrite("Source   : ").WriteBndNode(ten).WriteLine();
            var res = Conversion.CastBnd(NoopReducerHost.Instance, ten, DType.R8Req.ToTensor(false, 1), DType.UseUnionDefault);
            Sink.TWrite("Converted: ").WriteBndNode(res).WriteLine();
            WriteReductions(res);
        }
    }

    private void WriteBnd(BoundNode bnd)
    {
        Sink.TWrite("Value: ")
            .WriteBndNode(bnd)
            .WriteLine(" : {0}", bnd.Type);
    }

    private static BndGlobalNode Global(string name, DType type)
    {
        Assert.IsTrue(type.IsValid);
        return BndGlobalNode.Create(NPath.Root.Append(new DName(name)), type);
    }

    [TestMethod]
    public void CallCertificationTest()
    {
        Verb = BndNodePrinter.Verbosity.Default;
        int count = DoBaselineTests(Run, @"Binder/Special/CallCertification.txt");
        Assert.AreEqual(1, count);

        BndCallNode GoodAll(bool full, RexlOper func, DType type, ArgTuple args, ScopeTuple scopes, ScopeTuple indices,
            DirTuple dirs = default, NameTuple names = default)
        {
            var res = BndCallNode.Create(func, type, args, scopes, indices, dirs, names);
            Assert.IsTrue(res.Certified);
            Sink.TWrite("Good: ")
                .WriteBndNode(res)
                .WriteLine(" : {0} : {1}", res.Type, res.CertifiedFull);
            Assert.AreEqual(full, res.CertifiedFull);
            return res;
        }

        BndCallNode Good(bool full, RexlOper func, DType type, ArgTuple args,
            DirTuple dirs = default, NameTuple names = default)
        {
            return GoodAll(full, func, type, args, ScopeTuple.Empty, ScopeTuple.Empty, dirs, names);
        }

        void ThrowsAll(string msg, RexlOper func, DType type, ArgTuple args, ScopeTuple scopes, ScopeTuple indices,
            DirTuple dirs = default, NameTuple names = default)
        {
            Sink.TWrite("Error: ").TWrite(msg).WriteLine(".");
            bool caught = false;
            BndCallNode call = null;
            try { call = BndCallNode.Create(func, type, args, scopes, indices, dirs, names); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);
        }

        void Throws(string msg, RexlOper func, DType type, ArgTuple args,
            DirTuple dirs = default, NameTuple names = default)
        {
            ThrowsAll(msg, func, type, args, ScopeTuple.Empty, ScopeTuple.Empty, dirs, names);
        }

        void Run(string pathHead, string pathTail, string text, TestOptions options)
        {
            var A = new DName("A");
            var B = new DName("B");
            var C = new DName("C");

            var typeRec = DType.CreateRecord(false, new TypedName(A, DType.I8Req));
            var typeTab = typeRec.ToSequence();

            var str = Global("str", DType.Text);
            var i8 = Global("i8", DType.I8Req);
            var si8 = Global("si8", DType.I8Req.ToSequence());
            var table = Global("table", typeTab);

            var map = ArgScope.Create(ScopeKind.SeqItem, DType.I8Req);
            var with = ArgScope.Create(ScopeKind.With, DType.I8Req);
            var mapTab = ArgScope.Create(ScopeKind.SeqItem, typeRec);
            var index = ArgScope.CreateIndex();

            var mapRef = BndScopeRefNode.Create(map);
            var withRef = BndScopeRefNode.Create(with);
            var mapTabRef = BndScopeRefNode.Create(mapTab);
            var indexRef = BndScopeRefNode.Create(index);

            WriteBnd(str);
            WriteBnd(i8);
            WriteBnd(si8);
            WriteBnd(table);

            // GetType is resolved by the reducer, so is not fully certified.
            Good(false, GetTypeFunc.Instance, DType.Text, ArgTuple.Create(i8));

            Good(true, IsEmptyFunc.Instance, DType.BitReq, ArgTuple.Create(str));

            Good(true, SortFunc.Sort, si8.Type, ArgTuple.Create(si8));
            Good(true, SortFunc.Sort, si8.Type, ArgTuple.Create(si8), DirTuple.Create(Directive.Up));
            Good(true, SortFunc.Sort, si8.Type, ArgTuple.Create(si8), DirTuple.Create(Directive.DownCi));
            GoodAll(true, SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create(map), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.None, Directive.Up));

            // Non-sortable.
            Good(false, SortFunc.Sort, typeTab, ArgTuple.Create(table));
            GoodAll(false, SortFunc.Sort, typeTab, ArgTuple.Create(table, mapTabRef), ScopeTuple.Create(mapTab), ScopeTuple.Empty);

            var getA = BndGetFieldNode.Create(A, mapTabRef);
            GoodAll(true, SortFunc.Sort, typeTab, ArgTuple.Create(table, getA), ScopeTuple.Create(mapTab), ScopeTuple.Empty);

            // Errors.
            Throws("Null arg", IsEmptyFunc.Instance, DType.BitReq, ArgTuple.Create((BoundNode)null));
            Throws("Wrong return type", IsEmptyFunc.Instance, DType.I8Req, ArgTuple.Create(str));
            Throws("Wrong arg type", IsEmptyFunc.Instance, DType.I8Req, ArgTuple.Create(i8));

            ThrowsAll("Null scope", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create((ArgScope)null), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.None, Directive.Up));
            ThrowsAll("Wrong scope kind", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, withRef), ScopeTuple.Create(with), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.None, Directive.Up));
            ThrowsAll("Wrong index scope", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, indexRef), ScopeTuple.Create(index), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.None, Directive.Up));

            ThrowsAll("Wrong number of directives", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create(map), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.Up));
            ThrowsAll("Directive on seq in Sort", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create(map), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.Up, Directive.Up));
            ThrowsAll("Key dir on sel in Sort", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create(map), ScopeTuple.Empty,
                dirs: DirTuple.Create(Directive.None, Directive.Key));

            ThrowsAll("Name on seq in Sort", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create(map), ScopeTuple.Empty,
                names: NameTuple.Create(A, default));
            ThrowsAll("Name on sel in Sort", SortFunc.Sort, si8.Type, ArgTuple.Create(si8, mapRef), ScopeTuple.Create(map), ScopeTuple.Empty,
                names: NameTuple.Create(default, A));

            // ThrowsAll("Bad source", SortFunc.Sort, typeTab, ArgTuple.Create(rec, getA), ScopeTuple.Create(mapTab), ScopeTuple.Empty);
            ThrowsAll("Non-seq ret type", SortFunc.Sort, typeRec, ArgTuple.Create(table, getA), ScopeTuple.Create(mapTab), ScopeTuple.Empty);
            ThrowsAll("Wrong ret type", SortFunc.Sort, DType.I8Req.ToSequence(), ArgTuple.Create(table, getA), ScopeTuple.Create(mapTab), ScopeTuple.Empty);
        }
    }

    [TestMethod]
    public void CreateSetFieldsTest()
    {
        Verb = BndNodePrinter.Verbosity.Default;
        int count = DoBaselineTests(Run, @"Binder/Special/CreateSetFields.txt");
        Assert.AreEqual(1, count);

        BoundNode Good(DType type, BoundNode source, ArgScope scope, params (DName Key, BoundNode Value)[] items)
        {
            var res = BndSetFieldsNode.Create(type, source, scope, NamedItems.Empty.Create(items));
            Sink.TWrite("Good: ")
                .WriteBndNode(res)
                .WriteLine(" : {0}", res.Type);
            return res;
        }

        void Throws(string msg, DType type, BoundNode source, ArgScope scope, params (DName Key, BoundNode Value)[] items)
        {
            Sink.TWrite("Error: ").TWrite(msg).TWrite("; ")
                .TWriteType(type).TWrite(" ([").TWriteType(scope?.Type).TWrite("] ")
                .WriteBndNodeOpt(source);
            foreach (var (name, val) in items)
                Sink.TWrite(", {0}:", name).WriteBndNodeOpt(val);
            Sink.WriteLine(")");
            bool caught = false;
            BoundNode res = null;
            try { res = BndSetFieldsNode.Create(type, source, scope, NamedItems.Empty.Create(items)); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);
        }

        void Run(string pathHead, string pathTail, string text, TestOptions options)
        {
            var A = new DName("A");
            var B = new DName("B");
            var C = new DName("C");

            var typeRec = DType.CreateRecord(false, new TypedName(A, DType.I8Req));
            var typeTab = typeRec.ToSequence();

            var i8 = Global("i8", DType.I8Req);
            var str = Global("str", DType.Text);
            var rec = Global("rec", typeRec);

            var with = ArgScope.Create(ScopeKind.With, typeRec);
            var withRef = BndScopeRefNode.Create(with);

            WriteBnd(str);
            WriteBnd(rec);

            var getA = BndGetFieldNode.Create(A, withRef);
            Good(typeRec, rec, with);
            Good(typeRec, rec, null);
            Good(DType.EmptyRecordReq, rec, with);
            Good(DType.EmptyRecordReq, rec, null);
            Good(typeRec.AddNameType(B, DType.Text), rec, with, (B, str));
            Good(typeRec.AddNameType(B, DType.I8Req), rec, with, (B, getA));
            Good(DType.EmptyRecordReq.AddNameType(B, DType.Text), rec, with, (B, str));
            Good(DType.EmptyRecordReq.AddNameType(B, DType.I8Req), rec, with, (B, getA));

            // Unassigned field of primitive or opt type is OK.
            Good(typeRec.AddNameType(B, DType.Text), rec, with);
            Good(typeRec.AddNameType(B, DType.I8Req), rec, with);
            Good(typeRec.AddNameType(B, DType.I8Opt), rec, with);
            Good(typeRec.AddNameType(B, typeRec.ToOpt()), rec, with);

            // REVIEW: Should this one be accepted?
            Throws("Missing scope unused", typeRec.AddNameType(B, DType.Text), rec, null, (B, str));
            Throws("Missing scope used", typeRec.AddNameType(B, DType.I8Req), rec, null, (B, getA));

            Throws("Wrong type", typeRec.AddNameType(B, DType.Text), rec, with, (B, getA));
            Throws("Wrong type 2", DType.EmptyRecordReq.AddNameType(B, DType.Text), rec, with, (B, i8));
            Throws("Wrong type 3", typeRec.AddNameType(B, DType.I8Req), rec, with, (C, getA));
            Throws("Wrong type 4", DType.I8Req, rec, with, (B, str));

            Throws("Unassigned field of rec type", typeRec.AddNameType(B, typeRec), rec, with);

            Throws("Bad source", typeRec.AddNameType(B, DType.Text), null, with, (B, str));
            Throws("Bad source type", typeRec.AddNameType(B, DType.Text), i8, with, (B, str));
        }
    }

    /// <summary>
    /// Tests printing of bound nodes in esoteric/bad/illegal cases. The bound node printer should NOT
    /// require well-formed scope referencing.
    /// </summary>
    [TestMethod]
    public void PrintBadBndTest()
    {
        int count = DoBaselineTests(Run, @"Binder/Special/PrintBadBnd.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TestOptions options)
        {
            void Dump(BoundNode bnd)
            {
                Verb = BndNodePrinter.Verbosity.Default;
                Sink.WriteBndNode(bnd).WriteLine();
                Verb = BndNodePrinter.Verbosity.Terse;
                Sink.WriteBndNode(bnd).WriteLine();
                Sink.WriteLine("###");
            }

            {
                var fma = RexlFormula.Create(SourceContext.Create("Sum(Range(10), it * it)"));
                ValidateScript(fma);

                var host = new BindHostImpl(this);
                var bfma = BoundFormula.Create(fma, host, BindOptions.DontReduce);
                ValidateBfma(bfma, host);

                var bndSum = bfma.BoundTree as BndCallNode;
                Assert.IsNotNull(bndSum);
                Assert.IsTrue(bndSum.Scopes.Length == 1);
                var badScope = BndScopeRefNode.Create(bndSum.Scopes[0]);

                foreach (var args in new[]
                {
                    ArgTuple.Create(badScope, bndSum),
                    ArgTuple.Create(bndSum, badScope),
                    ArgTuple.Create(badScope, bndSum, badScope)
                })
                {
                    var bnd = BndVariadicOpNode.Create(DType.I8Req, BinaryOp.Add, args, default);
                    Dump(bnd);
                }
            }

            {
                // Bad sharing of scopes. Note that this now throws when the call is created.
                var seq = BndGlobalNode.Create(NPath.Root.Append(new DName("S")), DType.I8Req.ToSequence());
                var scope = ArgScope.Create(ScopeKind.SeqItem, DType.I8Req);
                var sr = BndScopeRefNode.Create(scope);
                var inner = BndCallNode.Create(
                    ForEachFunc.ForEach, DType.I8Req.ToSequence(),
                    ArgTuple.Create(seq, BndVariadicOpNode.Create(DType.I8Req, BinaryOp.Mul, ArgTuple.Create(sr, sr), default)),
                    ScopeTuple.Create(scope));
                Dump(inner);
                BoundNode outer;
                bool caught = false;
                try
                {
                    outer = BndCallNode.Create(
                        ForEachFunc.ForEach, DType.I8Req.ToSequence(2),
                        ArgTuple.Create(seq, inner), ScopeTuple.Create(scope));
                }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);
            }

            {
                // For Take, the scope isn't pushed for the count arg. Should show up as external.
                var seq = BndGlobalNode.Create(NPath.Root.Append(new DName("S")), DType.I8Req.ToSequence());
                var scope = ArgScope.Create(ScopeKind.SeqItem, DType.I8Req);
                var sr = BndScopeRefNode.Create(scope);
                var bnd = BndCallNode.Create(
                    TakeDropFunc.Take, seq.Type,
                    ArgTuple.Create(seq, sr, BndCompareNode.Create(CompareOp.FromParts(CompareRoot.Less, default), sr, BndIntNode.Create(DType.I8Req, 10))),
                    ScopeTuple.Create(scope));
                Dump(bnd);
            }

            {
                // KeyJoin has some scopes pushed but inactive. Should show up as external.
                // The scoped visitor tracks when scopes are active so this should work properly.
                var seq1 = BndGlobalNode.Create(NPath.Root.Append(new DName("S1")), DType.I8Req.ToSequence());
                var seq2 = BndGlobalNode.Create(NPath.Root.Append(new DName("S2")), DType.I8Req.ToSequence());
                var scope1 = ArgScope.Create(ScopeKind.SeqItem, DType.I8Req);
                var scope2 = ArgScope.Create(ScopeKind.SeqItem, DType.I8Req);
                var sr1 = BndScopeRefNode.Create(scope1);
                var sr2 = BndScopeRefNode.Create(scope2);
                var bnd = BndCallNode.Create(
                    KeyJoinFunc.Instance, seq1.Type,
                    // The keys are backwards.
                    ArgTuple.Create(seq1, seq2, sr2, sr1, sr1),
                    ScopeTuple.Create(scope1, scope2));
                Dump(bnd);
            }
        }
    }

    [TestMethod]
    public void ScopeDeclValidationTests()
    {
        // Just use I8 for everything.
        var type = DType.I8Req;

        // Some scopes and their references.
        var w1 = ArgScope.Create(ScopeKind.With, type);
        var w2 = ArgScope.Create(ScopeKind.With, type);
        var wr1 = BndScopeRefNode.Create(w1);
        var wr2 = BndScopeRefNode.Create(w2);
        var m1 = ArgScope.Create(ScopeKind.SeqItem, type);
        var m2 = ArgScope.Create(ScopeKind.SeqItem, type);
        var mr1 = BndScopeRefNode.Create(m1);
        var mr2 = BndScopeRefNode.Create(m2);
        var i1 = ArgScope.CreateIndex();
        var i2 = ArgScope.CreateIndex();
        var ir1 = BndScopeRefNode.Create(i1);
        var ir2 = BndScopeRefNode.Create(i2);

        // Some globals.
        var g1 = BndGlobalNode.Create(NPath.Root.Append(new DName("G1")), type);
        var g2 = BndGlobalNode.Create(NPath.Root.Append(new DName("G2")), type);
        var s1 = BndGlobalNode.Create(NPath.Root.Append(new DName("S1")), type.ToSequence());
        var s2 = BndGlobalNode.Create(NPath.Root.Append(new DName("S2")), type.ToSequence());

        static ArgTuple Args(params BoundNode[] args) => ArgTuple.Create(args);
        static ScopeTuple Scps(params ArgScope[] scopes) => ScopeTuple.Create(scopes);
        static BndTupleNode Tup(params BoundNode[] args) => BndTupleNode.Create(Args(args));
        static BndCallNode With(ArgTuple args, ScopeTuple scopes)
        {
            Assert.AreEqual(args.Length, scopes.Length + 1);
            return BndCallNode.Create(WithFunc.With, args[^1].Type, args, scopes);
        }
        static BndCallNode Mod(BoundNode a, BoundNode b) =>
            BndCallNode.Create(ModFunc.Instance, DType.I8Req, Args(a, b));
        static BndCallNode Map(ArgTuple args, ScopeTuple scopes, ArgScope ind = null)
        {
            Assert.AreEqual(args.Length, scopes.Length + 1);
            return BndCallNode.Create(ForEachFunc.ForEach, args[^1].Type.ToSequence(),
                args, scopes, ScopeTuple.Create(ind));
        }

        static BoundNode Good(BoundNode bnd)
        {
            Assert.IsNotNull(bnd);
            return bnd;
        }

        static void Fail(Func<BoundNode> act)
        {
            bool caught = false;
            try { act(); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);
        }

        Fail(() => With(Args(g1, g1, wr1), Scps(w1, w1)));

        Good(With(Args(g1, wr1), Scps(w1)));
        Good(With(Args(Mod(g1, g2), wr1), Scps(w1)));

        BoundNode bnd1 = Good(With(Args(g1, wr1, wr2), Scps(w1, w2)));

        Good(Tup(bnd1, bnd1));
        Good(Tup(bnd1, Tup(bnd1, bnd1)));
        Good(Tup(bnd1, Tup(bnd1, bnd1), g1, bnd1));

        Fail(() => With(Args(bnd1, wr1), Scps(w1)));
        Fail(() => With(Args(bnd1, wr2), Scps(w2)));
        Fail(() => With(Args(g1, bnd1), Scps(w1)));

        BoundNode bnd2 = With(Args(g1, Mod(wr1, g2), wr2), Scps(w1, w2));

        Good(bnd2);
        Good(Tup(bnd2, bnd2));

        Fail(() => With(Args(bnd2, wr1), Scps(w1)));
        Fail(() => With(Args(bnd2, wr2), Scps(w2)));
        Fail(() => With(Args(g1, bnd2), Scps(w1)));

        // This is so anything with it is bad.
        Fail(() => With(Args(bnd1, wr1), Scps(w1)));
        Fail(() => Tup(With(Args(bnd1, wr1), Scps(w1)), bnd1));
        Fail(() => Tup(With(Args(bnd1, wr1), Scps(w1)), Tup(bnd1, bnd1)));
        Fail(() => Tup(With(Args(bnd1, wr1), Scps(w1)), Tup(g1, g1)));
        Fail(() => Tup(bnd1, With(Args(bnd1, wr1), Scps(w1))));
        Fail(() => Tup(Tup(g1, g1), With(Args(bnd1, wr1), Scps(w1))));

        // Index scope cases.
        Good(Map(Args(s1, s2, Mod(mr1, mr2)), Scps(m1, m2)));
        Good(Map(Args(s1, s2, Mod(mr1, ir1)), Scps(m1, m2), i1));
        Good(Map(Args(s1, s2, ir1), Scps(m1, m2), i1));
        Good(Map(Args(s1, Map(Args(s2, Mod(mr1, mr2)), Scps(m2))), Scps(m1)));
        Good(Map(Args(s1, Map(Args(s2, Mod(ir1, mr2)), Scps(m2))), Scps(m1), i1));
        Good(Map(Args(s1, Map(Args(s2, Mod(mr1, ir2)), Scps(m2), i2)), Scps(m1)));
        Good(Map(Args(s1, Map(Args(s2, Mod(ir1, ir2)), Scps(m2), i2)), Scps(m1), i1));
        Good(Map(Args(s1, Map(Args(s2, mr2), Scps(m2))), Scps(m1)));
        Good(Map(Args(s1, Map(Args(s2, mr2), Scps(m2))), Scps(m1), i1));
        Good(Map(Args(s1, Map(Args(s2, ir2), Scps(m2), i2)), Scps(m1)));
        Good(Map(Args(s1, Map(Args(s2, ir2), Scps(m2), i2)), Scps(m1), i1));

        Fail(() => Map(Args(s1, Map(Args(s2, ir2), Scps(m2), i2)), Scps(m1), i2));

        var na = new DName("A");
        var nb = new DName("B");
        DType typeRec = DType.CreateRecord(false, new TypedName(na, type));
        DType typeRec2 = typeRec.AddNameType(nb, type);
        var r1 = ArgScope.Create(ScopeKind.With, typeRec);
        var rr1 = BndScopeRefNode.Create(r1);
        var gr = BndGlobalNode.Create(NPath.Root.Append(new DName("R")), typeRec);
        var gfn = BndGetFieldNode.Create(na, rr1);
        Good(BndSetFieldsNode.Create(typeRec2, gr, r1, NamedItems.Empty));
        Good(BndSetFieldsNode.Create(typeRec2, gr, r1, NamedItems.Empty.SetItem(nb, gfn)));
        Fail(() => BndSetFieldsNode.Create(typeRec2, gr, r1, NamedItems.Empty.SetItem(nb, With(Args(gr, gfn), Scps(r1)))));
    }
}
