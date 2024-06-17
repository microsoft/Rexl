// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Flow;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using FmaTuple = Immutable.Array<(RexlFormula fma, int grp)>;
using GuidSet = Immutable.Set<Guid>;
using TOpts = System.Boolean;

public sealed class TestNode : DocumentBase.DocExtNode
{
    public TestNode(Guid guid, NPath name, RexlFormula fma, TestDoc.NodeConfig config, FmaTuple extra)
        : base(guid, name, fma, config, extra)
    {
    }
}

public sealed partial class TestDoc : DocumentExt<TestDoc, TestNode>
{
    public string UndoMarker { get; }

    public UserFunc Udf { get; }

    public TestDoc()
        : base()
    {
        UndoMarker = "";
        Udf = null;
    }

    private TestDoc(string marker, in Fields flds, UserFunc udf)
        : base(in flds)
    {
        UndoMarker = marker;
        Udf = udf;
    }

    protected override TestDoc Wrap(in Fields flds)
    {
        AssertValid(in flds);
        if (Match(in flds))
            return this;
        return new TestDoc("", in flds, Udf);
    }

    protected override TestNode SetConfigCore(TestNode node, NodeConfig config)
    {
        return new TestNode(node.Guid, node.Name, node.Formula, config, node.ExtraFormulas);
    }

    protected override TestNode SetExtraCore(TestNode node, Immutable.Array<(RexlFormula fma, int grp)> extra)
    {
        return new TestNode(node.Guid, node.Name, node.Formula, node.Config, extra);
    }

    protected override TestNode SetFormulaCore(TestNode node, RexlFormula fma)
    {
        return new TestNode(node.Guid, node.Name, fma, node.Config, node.ExtraFormulas);
    }

    protected override TestNode SetNameCore(TestNode node, NPath name)
    {
        return new TestNode(node.Guid, name, node.Formula, node.Config, node.ExtraFormulas);
    }

    public TestDoc AddUndoMarker(string msg)
    {
        return new TestDoc(msg, new Fields(this), Udf);
    }

    public TestDoc SetUdf(UserFunc udf)
    {
        return new TestDoc("", new Fields(this), udf);
    }

    public TestDoc CreateFlowNode(NPath name, string code, Guid guid)
    {
        return AddNode(new TestNode(guid, name, RexlFormula.Create(SourceContext.Create(code)), null, default));
    }

    public TestDoc CreateFlowNode(NPath name, RexlFormula fma, Guid guid)
    {
        return AddNode(new TestNode(guid, name, fma, null, default));
    }

    public TestDoc CreateFlowNode(NPath name, NodeConfig config, Guid guid)
    {
        return AddNode(new TestNode(guid, name, null, config, default));
    }

    public TestDoc CreateFlowNode(NPath name, RexlFormula fma, NodeConfig config, Guid guid)
    {
        return AddNode(new TestNode(guid, name, fma, config, default));
    }

    public TestDoc CreateFlowNode(NPath name, RexlFormula fma, NodeConfig config, FmaTuple extra, Guid guid)
    {
        return AddNode(new TestNode(guid, name, fma, config, extra));
    }
}

[TestClass]
public sealed partial class DocumentTests : RexlTestsBaseType<bool>
{
    private readonly GraphHostImpl _host;

    private int _iidPrev;
    private readonly byte[] _guidBytes;

    private TestDoc _doc;
    private readonly Stack<TestDoc> _undo;
    private readonly Stack<TestDoc> _redo;

    private TestDoc Doc => _doc;
    private int UndoCount => _undo.Count;
    private int RedoCount => _redo.Count;

    // The graph.
    private TestDoc.Graph _graph;
    // The udf active when the graph was created.
    private UserFunc _udf;

    private GuidSet _dataChanged;
    private Dictionary<NPath, TestDoc.Namespace> _nss;

    public DocumentTests()
    {
        _doc = new TestDoc();
        _undo = new Stack<TestDoc>();
        _redo = new Stack<TestDoc>();
        _host = new GraphHostImpl(this);

        Assert.AreEqual(0, Doc.NodeCount);
        Assert.AreEqual(0, UndoCount);
        Assert.AreEqual(0, RedoCount);

        _graph = Doc.GetGraph(_host, null);
        _udf = Doc.Udf;
        _nss = new Dictionary<NPath, TestDoc.Namespace>() { { NPath.Root, null } };

        _guidBytes = new byte[8];
    }

    private Guid GuidNext()
    {
        return new Guid(++_iidPrev, 0, 0, _guidBytes);
    }

    private void MarkDataChanged(Guid guid)
    {
        _dataChanged = _dataChanged.Add(guid);
    }

    private void Dump(string kind, TestNode node, bool withConfig = true)
    {
        Sink.Write("{0}: ", kind);
        DumpNode(node);
        if (withConfig)
            DumpConfig(node);
    }

    private void DumpNode(TestNode node)
    {
        string id = node.Guid.ToString();
        Assert.AreEqual(36, id.Length);
        if (id.EndsWith("-0000-0000-0000-000000000000"))
            Sink.Write(id.AsSpan(0, 8));
        else
            Sink.Write(id);
        Sink.TWrite(" [").WriteDottedSyntax(node.Name).Write(']');
    }

    private void DumpConfig(TestNode node)
    {
        if (node.Config != null)
        {
            if (node.Config is TestDoc.GridConfig grid)
                Sink.Write(", <grid>({0} cols, {1} rows)", grid.ColCount, grid.RowCount);
            else
                Sink.Write(", <config>");
        }
    }

    private void HandleGlobalAdded(TestNode node)
    {
        Validation.AssertValue(node);
        Dump("Add", node);
        if (node.Formula != null)
            Sink.Write(", Fma: [{0}]", node.Formula.Text);
        if (node.ExtraFormulas.Length > 0)
        {
            string pre = ", Extra: {";
            for (int i = 0; i < node.ExtraFormulas.Length; i++)
            {
                Sink.Write("{0} [{1}]", pre, node.ExtraFormulas[i].fma.Text);
                pre = ",";
            }
            Sink.Write(" }");
        }
        Sink.WriteLine();
    }

    private void HandleGlobalDeleted(TestNode node)
    {
        Validation.AssertValue(node);
        Dump("Del", node);
        Sink.WriteLine();
    }

    private void HandleGlobalRenamed(TestNode node, NPath namePrev)
    {
        Dump("Ren", node, withConfig: false);
        Sink.TWrite(" <= [").WriteDottedSyntax(namePrev).Write(']');
        DumpConfig(node);
        Sink.WriteLine();
    }

    private void HandleFormulaChanged(TestNode node, RexlFormula fmaPrev, FmaTuple extraPrev)
    {
        Validation.AssertValue(node);

        bool main = node.Formula != fmaPrev;
        Dump("Fma", node);
        if (main)
            Sink.Write(", Main: [{0}] => [{1}]", fmaPrev?.Text, node.Formula?.Text);
        Sink.WriteLine();
        var old = extraPrev;
        var cur = node.ExtraFormulas;
        if (old.Length != cur.Length || old.Zip(cur, (a, b) => a.fma != b.fma || a.grp != b.grp).Any())
        {
            for (int i = 0; i < old.Length; i++)
                Sink.WriteLine("  Old {0}: [{1}:{2}]", i, old[i].grp, old[i].fma.Text);
            for (int i = 0; i < cur.Length; i++)
                Sink.WriteLine("  New {0}: [{1}:{2}]", i, cur[i].grp, cur[i].fma.Text);
        }
    }

    private void HandleConfigChanged(TestNode node, TestDoc.NodeConfig configPrev)
    {
        Dump("Cfg", node);
        Sink.WriteLine();
    }

    private void HandleGraphChange()
    {
        var graphPrev = _graph;
        _graph = _doc.GetGraph(_host, graphPrev, _host.SetUdf(_doc.Udf));

        foreach (var gnOld in graphPrev.GetNodesByGuid())
        {
            if (!_graph.TryGetNode(gnOld.Guid, out _))
                HandleGlobalDeleted(gnOld.FlowNode);
        }

        // Handle additions and modifications.
        var dataChanged = _dataChanged;
        _dataChanged = default;
        Dictionary<NPath, NPath> nameMap = null;
        foreach (var gnNew in _graph.GetNodesByGuid())
        {
            var guid = gnNew.Guid;
            if (!graphPrev.TryGetNode(guid, out var gnOld))
            {
                HandleGlobalAdded(gnNew.FlowNode);
                continue;
            }

            if (gnOld == gnNew)
                continue;

            if (gnOld.Config != gnNew.Config)
            {
                dataChanged = dataChanged.Add(guid);
                HandleConfigChanged(gnNew.FlowNode, gnOld.Config);
            }
            if (gnOld.Name != gnNew.Name)
            {
                Util.Add(ref nameMap, gnOld.Name, gnNew.Name);
                HandleGlobalRenamed(gnNew.FlowNode, gnOld.Name);
            }
            if (gnOld.Formula != gnNew.Formula || !gnOld.ExtraFormulas.AreSame(gnNew.ExtraFormulas))
                HandleFormulaChanged(gnNew.FlowNode, gnOld.Formula, gnOld.ExtraFormulas);
        }

        Sink.WriteLine("Status: {0} nodes, {1} undos, {2} redos", Doc.NodeCount, UndoCount, RedoCount);

        // Handle namespace changes.
        bool nssChanged = false;
        var nss = new Dictionary<NPath, TestDoc.Namespace>();
        foreach (var (name, ns) in _graph.GetNamespaces())
        {
            nss.Add(name, ns);
            if (!_nss.TryGetValue(name, out var nsOld) || nsOld != ns)
                nssChanged = true;
        }
        if (nssChanged || nss.Count < _nss.Count)
        {
            _nss = nss;
            var prefix = "  Namespaces changed: ";
            foreach (var kvp in _nss.OrderBy(p => p.Key.ToString()))
            {
                Sink.Write(prefix);
                if (kvp.Value != null)
                    Sink.Write("<exp>");
                Sink.Write(kvp.Key.ToString());
                prefix = ", ";
            }
            Sink.WriteLine();
        }

        // Get info for each node. Need to use topo order for this, but we want to display in
        // guid order, so need to build up display info in a collection.
        GuidSet badData = default;
        GuidSet seen = default;
        var sb = new StringBuilder("               ");
        var infos = new List<(Guid guid, string status)>();
        foreach (var gnNew in _graph.GetNodes())
        {
            var guid = gnNew.Guid;

            bool added = false;
            bool nameChanged = false;
            bool dataBaseChanged = dataChanged.Contains(guid);
            bool dataFullChanged = false;
            bool typeBaseChanged = false;
            bool typeFullChanged = false;
            bool usesBaseChanged = false;
            bool usesFullChanged = false;
            bool bindMainChanged = false;
            bool bindMoreChanged = false;
            bool codeMainChanged = false;
            bool codeMoreChanged = false;
            bool numFmasChanged = false;
            bool hasError = false;

            if (gnNew.Formula != null && !gnNew.MainBoundFormula.IsGood)
                hasError = true;
            if (gnNew.ExtraFormulas.Length > 0 && gnNew.ExtraBoundFormulas.Any(item => !item.bfma.IsGood))
                hasError = true;

            if (!graphPrev.TryGetNode(guid, out var gnOld))
            {
                added = true;
                typeBaseChanged = true;
                typeFullChanged = true;
                usesBaseChanged = !gnNew.BaseUses.IsEmpty;
                usesFullChanged = !gnNew.Uses.IsEmpty;
                dataBaseChanged = true;
                dataFullChanged = true;
                if (gnNew.Formula != null)
                {
                    bindMainChanged = true;
                    codeMainChanged = true;
                }
                if (gnNew.ExtraFormulas.Length > 0)
                {
                    numFmasChanged = true;
                    bindMoreChanged = true;
                    codeMoreChanged = true;
                }
            }
            else if (gnOld != gnNew)
            {
                if (gnOld.Config != gnNew.Config)
                {
                    dataBaseChanged = true;
                    dataFullChanged = true;
                }
                if (gnOld.Name != gnNew.Name)
                    nameChanged = true;
                if (gnOld.ExtraFormulas.Length != gnNew.ExtraFormulas.Length)
                    numFmasChanged = true;

                if (gnOld.BaseType != gnNew.BaseType)
                    typeBaseChanged = true;
                if (gnOld.Type != gnNew.Type)
                    typeFullChanged = true;
                if (gnOld.BaseUses != gnNew.BaseUses)
                    usesBaseChanged = true;
                if (gnOld.Uses != gnNew.Uses)
                    usesFullChanged = true;
                if (gnOld.MainBoundFormula != gnNew.MainBoundFormula)
                {
                    bindMainChanged = true;
                    var bfma0 = gnOld.MainBoundFormula;
                    var bfma1 = gnNew.MainBoundFormula;
                    if (!bfma0.IsGood || !bfma1.IsGood || !bfma0.BoundTree.Equivalent(bfma1.BoundTree, nameMap))
                        codeMainChanged = true;
                }
                if (gnNew.ExtraBoundFormulas.Length > 0 && !gnOld.ExtraBoundFormulas.AreSame(gnNew.ExtraBoundFormulas))
                {
                    bindMoreChanged = true;
                    if (gnOld.ExtraBoundFormulas.Length != gnNew.ExtraBoundFormulas.Length)
                        codeMoreChanged = true;
                    else
                    {
                        bool good = true;
                        for (int i = 0; i < gnOld.ExtraBoundFormulas.Length && good; i++)
                        {
                            var (bfma0, grp0) = gnOld.ExtraBoundFormulas[i];
                            var (bfma1, grp1) = gnNew.ExtraBoundFormulas[i];
                            // REVIEW: Should the grp affect this at all? Probably doesn't need to.
                            if (grp0 != grp1 || !bfma0.IsGood || !bfma0.IsGood || !bfma0.BoundTree.Equivalent(bfma1.BoundTree, nameMap))
                                good = false;
                        }
                        if (!good)
                            codeMoreChanged = true;
                    }
                }
            }

            if (codeMainChanged)
                dataBaseChanged = true;
            else if (dataChanged.Intersects(gnNew.BaseUses) || !gnNew.BaseUses.IsSubset(seen))
                dataBaseChanged = true;
            else if (numFmasChanged)
                dataFullChanged = true;
            else if (codeMoreChanged)
                dataFullChanged = true;
            else if (dataChanged.Intersects(gnNew.Uses) || !gnNew.Uses.IsSubset(seen))
                dataFullChanged = true;

            dataFullChanged |= dataBaseChanged;

            bool bad = !gnNew.Uses.IsSubset(seen) || badData.Intersects(gnNew.Uses);

            if (added | nameChanged |
                dataBaseChanged | dataFullChanged | typeBaseChanged | typeFullChanged |
                bindMainChanged | bindMoreChanged | codeMainChanged | codeMoreChanged |
                numFmasChanged | bad)
            {
                int ich = 0;
                sb[ich++] = added ? 'A' : ' ';
                sb[ich++] = nameChanged ? 'N' : ' ';
                sb[ich++] = numFmasChanged ? 'F' : ' ';
                sb[ich++] = typeBaseChanged ? 't' : ' ';
                sb[ich++] = typeFullChanged ? 'T' : ' ';
                sb[ich++] = usesBaseChanged ? 'u' : ' ';
                sb[ich++] = usesFullChanged ? 'U' : ' ';
                sb[ich++] = bindMainChanged ? 'B' : ' ';
                sb[ich++] = bindMoreChanged ? 'b' : ' ';
                sb[ich++] = codeMainChanged ? 'C' : ' ';
                sb[ich++] = codeMoreChanged ? 'c' : ' ';
                sb[ich++] = dataBaseChanged ? 'd' : ' ';
                sb[ich++] = dataFullChanged ? 'D' : ' ';
                sb[ich++] = hasError ? 'E' : ' ';
                sb[ich++] = bad ? 'X' : ' ';
                Validation.Assert(ich == sb.Length);

                infos.Add((guid, sb.ToString()));
            }

            seen = seen.Add(guid);
            if (bad)
                badData = badData.Add(guid);
            if (dataFullChanged)
                dataChanged = dataChanged.Add(guid);
        }

        if (infos.Count > 0)
        {
            infos.Sort((pair1, pair2) => pair1.guid.CompareTo(pair2.guid));

            Sink.WriteLine("  Node changes:");
            foreach (var (guid, status) in infos)
            {
                var gn = _graph.GetNode(guid);
                var node = gn.FlowNode;

                Sink.Write("    ");
                DumpNode(node);
                Sink.Write(": [{0}]", status);
                DumpConfig(node);
                var type = gn.BaseType;
                Sink.Write(", Base: {0}", type);
                for (int ifma = 0; ifma < node.ExtraFormulas.Length; ifma++)
                {
                    type = gn.GetExtraDType(ifma);
                    var bfma = gn.GetExtraBoundFormula(ifma);
                    Assert.IsNotNull(bfma);
                    Assert.AreEqual(type, bfma.BoundTree.Type);
                    Sink.Write(", Extra[{0}]: {1}", ifma, type);
                }
                var typeFull = gn.Type;
                Assert.AreEqual(type, typeFull);

                if (node.Config is TestDoc.GridConfig grid)
                    RefreshGridRecords(node.Guid, grid);

                Sink.WriteLine();

                // Write errors and do some validation.
                int cex = node.ExtraFormulas.Length;
                for (int ifma = -1; ifma < cex; ifma++)
                {
                    RexlFormula fma;
                    BoundFormula bfma;
                    if (ifma < 0)
                    {
                        fma = node.Formula;
                        bfma = gn.MainBoundFormula;
                        if (fma == null)
                        {
                            Assert.IsNull(bfma);
                            continue;
                        }
                    }
                    else
                    {
                        fma = node.ExtraFormulas[ifma].fma;
                        bfma = gn.GetExtraBoundFormula(ifma);
                    }
                    Assert.IsNotNull(fma);
                    Assert.IsNotNull(bfma);
                    Assert.AreEqual(fma, bfma.Formula);

                    foreach (var diag in fma.Diagnostics)
                    {
                        if (ifma < 0)
                            Sink.Write("      *) Parse ");
                        else
                            Sink.Write("      {0}) Parse ", ifma);
                        diag.Format(Sink, options: DiagFmtOptions.DefaultTest);
                        Sink.WriteLine();
                    }

                    foreach (var diag in bfma.Diagnostics)
                    {
                        if (ifma < 0)
                            Sink.Write("      *) ");
                        else
                            Sink.Write("      {0}) ", ifma);
                        diag.Format(Sink, options: DiagFmtOptions.DefaultTest);
                        Sink.WriteLine();
                    }
                }
            }
        }

        Sink.WriteLine("###");
    }

    private static NPath MakeName(string str)
    {
        Validation.BugCheck(LexUtils.TryLexPath(str, out NPath full));
        return full;
    }

    private Guid MakeNode(ref TestDoc doc, string name, string fma)
    {
        var guid = GuidNext();
        doc = doc.CreateFlowNode(MakeName(name), CreateFormula(fma), guid);
        return guid;
    }

    Guid MakeNodeData(ref TestDoc doc, string name, DType type)
    {
        var guid = GuidNext();
        doc = doc.CreateFlowNode(MakeName(name), new DataConfig(type), guid);
        return guid;
    }

    private RexlFormula CreateFormula(string fma)
    {
        Validation.AssertValue(fma);
        return RexlFormula.Create(SourceContext.Create(fma));
    }

    private void Set(TestDoc doc)
    {
        Validation.AssertValue(doc);
        if (doc != _doc)
        {
            _undo.Push(_doc);
            _redo.Clear();
            _doc = doc;
            HandleGraphChange();
        }
        else if (!_dataChanged.IsEmpty)
            HandleGraphChange();
    }

    private void Undo()
    {
        Assert.IsTrue(_undo.Count > 0);
        Sink.WriteLine("*** Undo(1)");
        _redo.Push(_doc);
        _doc = _undo.Pop();
        HandleGraphChange();
    }

    private void Redo()
    {
        Assert.IsTrue(_redo.Count > 0);
        Sink.WriteLine("*** Redo(1)");
        _undo.Push(_doc);
        _doc = _redo.Pop();
        HandleGraphChange();
    }

    private void Undo(int count)
    {
        Assert.IsTrue((uint)count <= (uint)UndoCount);
        Sink.WriteLine($"*** Undo({count})");
        while (--count >= 0)
        {
            _redo.Push(_doc);
            _doc = _undo.Pop();
        }
        HandleGraphChange();
    }

    private void Redo(int count)
    {
        Assert.IsTrue((uint)count <= (uint)RedoCount);
        Sink.WriteLine($"*** Redo({count})");
        while (--count >= 0)
        {
            _undo.Push(_doc);
            _doc = _redo.Pop();
        }
        HandleGraphChange();
    }

    private void UndoAll()
    {
        Sink.WriteLine($"*** UndoAll");
        if (_undo.Count == 0)
            return;
        while (_undo.Count > 0)
        {
            _redo.Push(_doc);
            _doc = _undo.Pop();
        }
        HandleGraphChange();
    }

    private void RedoAll()
    {
        Sink.WriteLine($"*** RedoAll");
        if (_redo.Count == 0)
            return;
        while (_redo.Count > 0)
        {
            _undo.Push(_doc);
            _doc = _redo.Pop();
        }
        HandleGraphChange();
    }

    [TestMethod]
    public void Basic()
    {
        int count = DoBaselineTests(
            Run, @"Doc\Basic.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var guid1 = GuidNext();
            NPath name1 = MakeName("SomeName");
            NPath name2 = MakeName("NewName");
            Assert.IsFalse(Doc.TryGetNode(guid1, out _));
            Set(Doc.CreateFlowNode(name1, CreateFormula("[1, 3i8, 8, -2, 0]"), guid1));
            Assert.IsTrue(Doc.TryGetNode(guid1, out var node));
            Assert.IsTrue(Doc.IsActive(node));
            Assert.IsTrue(Doc.ContainsName(name1));
            Assert.IsFalse(Doc.ContainsName(name2));
            Assert.IsFalse(Doc.TryGetNamespace(name1, out _));
            Assert.IsFalse(Doc.TryGetNamespace(name2, out _));
            Set(Doc.RenameGlobal(guid1, name2));
            Assert.AreEqual(2, UndoCount);
            Assert.IsFalse(Doc.ContainsName(name1));
            Assert.IsTrue(Doc.ContainsName(name2));
            Assert.IsTrue(!Doc.IsActive(node));
            Assert.AreEqual(0, RedoCount);
            Undo();
            Redo();
            Undo();
            Undo();
            Assert.AreEqual(0, UndoCount);
            Assert.AreEqual(2, RedoCount);
            Redo();
            Undo();
            Redo();
            Redo();

            var guid2 = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("Derived"), CreateFormula("NewName->Filter(it mod 2 = 0)"), guid2));
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Count()")));
            Set(Doc.ReplaceExtraFormula(guid2, 0, CreateFormula("this->Sum()")));
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Filter(it >= 0)")));
            Undo(3);
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Filter(it >= 0)")));
            Set(Doc.InsertExtraFormula(guid2, 1, CreateFormula("this->Count()")));
            Set(Doc.ReplaceExtraFormula(guid2, 1, CreateFormula("this->Sum()")));
            Undo(3);
            // Same thing but using two groups.
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Filter(it >= 0)")));
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Count()"), 7));
            Set(Doc.ReplaceExtraFormula(guid2, 0, CreateFormula("this->Sum()"), 7));
            Undo(3);
            // Same thing but the groups are populated in a different order.
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Count()"), 7));
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Filter(it >= 0)")));
            Set(Doc.ReplaceExtraFormula(guid2, 0, CreateFormula("this->Sum()"), 7));

            bool caught = false;
            try { Doc.ReplaceExtraFormula(guid2, 1, CreateFormula("this + 5"), 7); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            caught = false;
            try { Doc.InsertExtraFormula(guid2, 2, CreateFormula("this + 5"), 7); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            caught = false;
            try { Doc.RemoveExtraFormula(guid2, 1, 7); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            // This one should work.
            Set(Doc.InsertExtraFormula(guid2, 1, CreateFormula("this + 5"), 7));
            Undo();

            var doc = Doc;
            Sink.WriteLine("*** Rename twice, back to original");
            doc = doc.RenameGlobal(guid1, name1);
            doc = doc.RenameGlobal(guid1, name2);
            Set(doc);
            Undo();

            doc = Doc;
            Sink.WriteLine("*** Rename twice, to different");
            doc = doc.RenameGlobal(guid1, name1);
            doc = doc.RenameGlobal(guid1, MakeName("X"));
            Set(doc);
            Undo();

            // This should make guid2 be an error.
            Set(Doc.DeleteNode(guid1));

            // This makes guid2 no longer have an error and changes the type from i8 to i4.
            var guid3 = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("NewName"), CreateFormula("Range(-5, 100)"), guid3));

            Assert.AreEqual(8, UndoCount);
            Assert.AreEqual(0, RedoCount);
            Undo(8);
            Redo(3);
            Redo(4);
            Assert.AreEqual(7, UndoCount);
            Assert.AreEqual(1, RedoCount);
            Undo(7);
            Redo(8);
            Assert.AreEqual(8, UndoCount);
            Assert.AreEqual(0, RedoCount);

            // Back up two, then do something else.
            Undo(2);
            Assert.AreEqual(6, UndoCount);
            Assert.AreEqual(2, RedoCount);

            Set(Doc.SetFormula(guid1, "Range(10)"));

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void Transitive()
    {
        int count = DoBaselineTests(
            Run, @"Doc\Transitive.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Coverage: transitive closure of DataFull change bits.
            var doc = Doc;
            var guid1 = GuidNext();
            NPath name1 = MakeName("A");
            doc = doc.CreateFlowNode(name1, CreateFormula("3"), guid1);
            var guid2 = GuidNext();
            NPath name2 = MakeName("B");
            doc = doc.CreateFlowNode(name2, CreateFormula("A"), guid2);
            var guid3 = GuidNext();
            NPath name3 = MakeName("C");
            doc = doc.CreateFlowNode(name3, CreateFormula("Range(100)"), null,
                Immutable.Array.Create(
                    (CreateFormula("this"), 0),
                    (CreateFormula("this->Filter(it mod B = 0)"), 0)),
                guid3);
            Set(doc);

            Set(Doc.SetFormula(guid1, "7"));

            // Coverage: truncate the extra set, without changing anything else.
            Set(Doc.InsertExtraFormula(guid3, 2, CreateFormula("this->Sum()")));
            Set(Doc.RemoveExtraFormula(guid3, 2));

            // Coverage: multiple formulas within a single node referencing a node that is renamed.
            Set(Doc.InsertExtraFormula(guid3, 2, CreateFormula("this->Sum(it * B)")));
            NPath nameDst;
            var changes = _graph.GetRenameChanges(guid2, nameDst = MakeName("X"));
            Set(Doc.RenameGlobal(guid2, nameDst, changes));
            Undo();
            Redo();

            // Coverage: in the same analysis, rename referenced node and remove tail extra formula.
            changes = _graph.GetRenameChanges(guid2, nameDst = MakeName("Y"));
            doc = Doc;
            doc = doc.RenameGlobal(guid2, nameDst, changes);
            doc = doc.RemoveExtraFormula(guid3, 2);
            Set(doc);
            Undo();
            Redo();

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void ExtraWithRefs()
    {
        int count = DoBaselineTests(
            Run, @"Doc\ExtraWithRefs.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var guid1 = GuidNext();
            var guid2 = GuidNext();
            NPath name1 = MakeName("A");
            NPath name2 = MakeName("B");
            Set(Doc.CreateFlowNode(name1, CreateFormula("3"), guid1));
            Set(Doc.CreateFlowNode(name2, CreateFormula("[1, 3i8, 8, -2, 0]"), null,
                Immutable.Array.Create(
                    (CreateFormula("this->Filter(it > A)"), 0),
                    (CreateFormula("this->Sort()"), 0)),
                guid2));

            Set(Doc.SetFormula(guid1, CreateFormula("1000i1")));

            Set(Doc.DeleteNode(guid1));
            Undo();

            NPath name;
            var changes = _graph.GetRenameChanges(guid1, name = MakeName("X"));
            Set(Doc.RenameGlobal(guid1, name, changes));
            Undo();
            Redo();
            changes = _graph.GetRenameChanges(guid1, name = MakeName("N.X"));
            Set(Doc.RenameGlobal(guid1, name, changes));
            Undo();
            Redo();
        }
    }

    [TestMethod]
    public void Named()
    {
        int count = DoBaselineTests(
            Run, @"Doc\Named.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var name1 = MakeName("SomeName");
            Assert.IsFalse(Doc.HasNameConflict(name1));

            var fma = CreateFormula("[1i4, 3i4, 8i4, -2i4, 0i4]");
            Assert.IsFalse(fma.HasDiagnostics);

            var typeSeq = DType.I4Req.ToSequence();
            var typeSum = DType.I8Req;

            TestNode node0 = null;
            TestNode node1 = null;

            // Add a node, then undo/redo.
            var guid1 = GuidNext();
            Set(Doc.CreateFlowNode(name1, fma, guid1));
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(name1, node1.Name);
            Assert.AreEqual(fma, node1.Formula);
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(1, Doc.NodeCount);
            Assert.AreEqual(node1, Doc.GetNode(name1));
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            Undo();
            Assert.AreEqual(0, Doc.NodeCount);

            node0 = node1;
            node1 = null;

            Redo();
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(1, Doc.NodeCount);
            node1 = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(node1));
            Assert.AreEqual(name1, node1.Name);
            Assert.AreEqual(fma, node1.Formula);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            // Add an extra formula, then undo/undo/redo/redo.
            Assert.AreEqual(0, node1.ExtraFormulas.Length);
            var fmaSum = CreateFormula("this->Sum()");
            Assert.IsFalse(fmaSum.HasDiagnostics);
            Set(Doc.InsertExtraFormula(guid1, 0, fmaSum));
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(1, node1.ExtraFormulas.Length);
            Assert.AreEqual(fmaSum, node1.ExtraFormulas[0].fma);
            Assert.AreEqual(typeSum, _graph.GetNode(guid1).Type);

            Undo();
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(0, node1.ExtraFormulas.Length);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            Undo();
            Assert.AreEqual(0, Doc.NodeCount);

            node0 = node1;
            node1 = null;

            Redo();
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(1, Doc.NodeCount);
            node1 = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(node1));
            Assert.AreEqual(name1, node1.Name);
            Assert.AreEqual(fma, node1.Formula);
            Assert.AreEqual(0, node1.ExtraFormulas.Length);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            Redo();
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(1, node1.ExtraFormulas.Length);
            Assert.AreEqual(fmaSum, node1.ExtraFormulas[0].fma);
            Assert.AreEqual(typeSum, _graph.GetNode(guid1).Type);

            // Change the formula, then undo(3)/redo(3)
            var fmaSort = CreateFormula("this->Sort()");
            Assert.IsFalse(fmaSort.HasDiagnostics);
            Set(Doc.ReplaceExtraFormula(guid1, 0, fmaSort));
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(1, node1.ExtraFormulas.Length);
            Assert.AreEqual(fmaSort, node1.ExtraFormulas[0].fma);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            Undo();
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(1, node1.ExtraFormulas.Length);
            Assert.AreEqual(fmaSum, node1.ExtraFormulas[0].fma);
            Assert.AreEqual(typeSum, _graph.GetNode(guid1).Type);

            Undo();
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(0, node1.ExtraFormulas.Length);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            Undo();
            Assert.AreEqual(0, Doc.NodeCount);

            node0 = node1;
            node1 = null;

            Redo();
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(1, Doc.NodeCount);
            node1 = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(node1));
            Assert.AreEqual(name1, node1.Name);
            Assert.AreEqual(fma, node1.Formula);
            Assert.AreEqual(0, node1.ExtraFormulas.Length);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            Redo();
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(1, node1.ExtraFormulas.Length);
            Assert.AreEqual(fmaSum, node1.ExtraFormulas[0].fma);
            Assert.AreEqual(typeSum, _graph.GetNode(guid1).Type);

            Redo();
            node1 = Doc.GetNode(guid1);
            Assert.AreEqual(1, node1.ExtraFormulas.Length);
            Assert.AreEqual(fmaSort, node1.ExtraFormulas[0].fma);
            Assert.AreEqual(typeSeq, _graph.GetNode(guid1).Type);

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void Unnamed()
    {
        int count = DoBaselineTests(
            Run, @"Doc\Unnamed.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var fma = CreateFormula("[1, 3, 8, -2, 0]");
            var guid1 = GuidNext();

            // We no longer support unnamed nodes.
            Assert.ThrowsException<ArgumentException>(() =>
                Doc.CreateFlowNode(default, fma, guid1));

            Assert.AreEqual(0, Doc.NodeCount);

            var name1 = MakeName("SomeName");
            Set(Doc.CreateFlowNode(name1, fma, guid1));
            var node1 = Doc.GetNode(guid1);
            Assert.AreEqual(name1, node1.Name);
            Assert.AreEqual(fma, node1.Formula);
            Assert.AreEqual(1, Doc.NodeCount);

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void WithData()
    {
        int count = DoBaselineTests(
            Run, @"Doc\WithData.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var typeRec = DType.CreateRecord(false,
                new TypedName(new DName("A"), DType.I4Req),
                new TypedName(new DName("B"), DType.BitOpt),
                new TypedName(new DName("C"), DType.R8Opt)
            );
            var typeRec2 = DType.CreateRecord(false,
                new TypedName(new DName("A"), DType.U8Opt),
                new TypedName(new DName("B"), DType.BitReq),
                new TypedName(new DName("C"), DType.R8Req)
            );
            var typeTbl = typeRec.ToSequence();
            var typeTbl2 = typeRec2.ToSequence();
            DType typeTmp;

            var guidA = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("X"), new DataConfig(typeTbl), guidA));

            Sink.WriteLine("*** MarkDataChanged(nodeA)");
            MarkDataChanged(guidA);
            HandleGraphChange();

            Sink.WriteLine("*** ChangeConfig to same type");
            Set(Doc.SetConfig(guidA, new DataConfig(typeTbl)));
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).Type);
            Undo();
            Redo();
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).Type);

            Sink.WriteLine("*** MarkDataChanged(nodeA)");
            MarkDataChanged(guidA);
            Set(Doc);

            var doc = Doc;
            Sink.WriteLine("*** Rename(nodeA, Y)");
            doc = doc.RenameGlobal(guidA, MakeName("Y"));
            Sink.WriteLine("*** MarkDataChanged(nodeA)");
            MarkDataChanged(guidA);
            Set(doc);
            Undo();
            Redo();

            Sink.WriteLine("*** Add extra formulas");
            Set(Doc.InsertExtraFormula(guidA, 0, CreateFormula("this+>{ D: C * 1 }")));
            var typeTblEx = typeRec.AddNameType(new DName("D"), DType.R8Opt).ToSequence();
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);

            Assert.AreEqual(4, UndoCount);
            Assert.AreEqual(0, RedoCount);
            Undo();
            Redo();
            Undo();
            Undo();
            Assert.AreEqual(2, UndoCount);
            Assert.AreEqual(2, RedoCount);
            Redo();
            Undo();
            Redo(2);
            Undo(4);
            Redo(4);
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);

            Set(Doc.InsertExtraFormula(guidA, 0, CreateFormula("this->Filter(A < 10u1)")));
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);
            Undo();
            Redo();
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);

            Sink.WriteLine("*** ChangeConfig to same type with extra formulas");
            Set(Doc.SetConfig(guidA, new DataConfig(typeTbl)));
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);
            Undo();
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);
            Redo();
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);

            Sink.WriteLine("*** ChangeConfig to different type with extra formulas");
            Set(Doc.SetConfig(guidA, new DataConfig(typeTbl2)));
            var typeTbl2Ex = typeRec2.AddNameType(new DName("D"), DType.R8Req).ToSequence();
            Assert.AreEqual(typeTbl2, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTbl2Ex, typeTmp = _graph.GetNode(guidA).Type);

            Undo();
            Assert.AreEqual(typeTbl, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTblEx, typeTmp = _graph.GetNode(guidA).Type);
            Redo();
            Assert.AreEqual(typeTbl2, typeTmp = _graph.GetNode(guidA).BaseType);
            Assert.AreEqual(typeTbl2Ex, typeTmp = _graph.GetNode(guidA).Type);
            Undo(2);
            // Keep the original type.
            Redo(1);

            Sink.WriteLine("*** Add Y");
            var guid2 = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("Z"), CreateFormula("Y->Filter(C < 8u1)"), guid2));
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Count()")));
            Set(Doc.ReplaceExtraFormula(guid2, 0, CreateFormula("this->Sum(A)")));
            Set(Doc.InsertExtraFormula(guid2, 0, CreateFormula("this->Filter(B = true)")));

            // This should make guid2 be an error.
            Set(Doc.DeleteNode(guidA));
            Assert.AreEqual(0, RedoCount);
            Undo();
            Redo();
            Assert.AreEqual(0, RedoCount);
            UndoAll();
            RedoAll();

            // This makes guid2 no longer have an error and changes some types.
            var guid3 = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("Y"), new DataConfig(typeTbl2), guid3));
            Undo();
            Redo();

            UndoAll();
            RedoAll();

            // Test changing the config.
            Set(Doc.SetConfig(guid3, new DataConfig(typeTbl)));
            Undo();
            Redo();

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void WithData2()
    {
        int count = DoBaselineTests(
            Run, @"Doc\WithData2.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var typeRec = DType.CreateRecord(false,
                new TypedName(new DName("A"), DType.I4Req),
                new TypedName(new DName("B"), DType.BitOpt),
                new TypedName(new DName("C"), DType.I8Opt)
            );
            var typeRec2 = DType.CreateRecord(false,
                new TypedName(new DName("A"), DType.U8Opt),
                new TypedName(new DName("B"), DType.BitReq),
                new TypedName(new DName("C"), DType.I8Req)
            );
            var typeRec3 = DType.CreateRecord(false,
                new TypedName(new DName("C"), DType.I8Req)
            );
            var typeTbl = typeRec.ToSequence();
            var typeTbl2 = typeRec2.ToSequence();
            var typeTbl3 = typeRec3.ToSequence();
            var typeTblEx = typeRec.AddNameType(new DName("D"), DType.I8Opt).ToSequence();
            var typeTbl2Ex = typeRec2.AddNameType(new DName("D"), DType.I8Req).ToSequence();
            var typeTbl3Ex = typeRec3.AddNameType(new DName("D"), DType.I8Req).ToSequence();

            var guid1 = GuidNext();
            var guid2 = GuidNext();
            var guid3 = GuidNext();
            var doc = Doc;
            doc = doc.CreateFlowNode(MakeName("X"), new DataConfig(typeTbl), guid1);
            doc = doc.CreateFlowNode(MakeName("Y"), new DataConfig(typeTbl), guid2);
            doc = doc.CreateFlowNode(MakeName("Z"), new DataConfig(DType.I8Req), guid3);
            Set(doc);

            Sink.WriteLine("*** Add extra formulas");
            doc = Doc;
            doc = doc.InsertExtraFormula(guid1, 0, CreateFormula("this+>{ D: C * 1 }"));
            doc = doc.InsertExtraFormula(guid2, 0, CreateFormula("this+>{ D: C * 1 }"));
            Set(doc);
            Assert.AreEqual(typeTbl, _graph.GetNode(guid1).BaseType);
            Assert.AreEqual(typeTblEx, _graph.GetNode(guid1).Type);
            Assert.AreEqual(typeTbl, _graph.GetNode(guid2).BaseType);
            Assert.AreEqual(typeTblEx, _graph.GetNode(guid2).Type);
            Undo();
            Redo();

            doc = Doc;
            doc = doc.InsertExtraFormula(guid1, 0, CreateFormula("this+>{ C: C * Z }"));
            // This extra formula doesn't use "this". Technically this could mean fewer status bits
            // when the base config is changed, but we currently don't optimize for this case.
            doc = doc.InsertExtraFormula(guid2, 0, CreateFormula("[{C: Z}]"));
            Set(doc);
            Assert.AreEqual(typeTbl, _graph.GetNode(guid1).BaseType);
            Assert.AreEqual(typeTblEx, _graph.GetNode(guid1).Type);
            Assert.AreEqual(typeTbl, _graph.GetNode(guid2).BaseType);
            Assert.AreEqual(typeTbl3Ex, _graph.GetNode(guid2).Type);
            Undo();
            Redo();

            Sink.WriteLine("*** ChangeConfig to same type with extra formulas");
            doc = Doc;
            doc = doc.SetConfig(guid1, new DataConfig(typeTbl));
            doc = doc.SetConfig(guid2, new DataConfig(typeTbl));
            Set(doc);
            Undo();
            Redo();

            Sink.WriteLine("*** ChangeConfig to different type with extra formulas");
            doc = Doc;
            doc = doc.SetConfig(guid1, new DataConfig(typeTbl2));
            doc = doc.SetConfig(guid2, new DataConfig(typeTbl2));
            Set(doc);
            Undo();
            Redo();

            Sink.WriteLine("*** ChangeConfig of referenced node to different type");
            Set(Doc.SetConfig(guid3, new DataConfig(DType.R8Opt)));
            Undo();
            Redo();
        }
    }

    [TestMethod]
    public void WithData3()
    {
        int count = DoBaselineTests(
            Run, @"Doc\WithData3.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Test an esoteric case where, in the same undo group, both the base type is changed
            // and the number of extra formulas goes from 0 to positive, with the extra formula(s)
            // preserving type.
            var typeRec = DType.CreateRecord(false,
                new TypedName(new DName("A"), DType.I4Req),
                new TypedName(new DName("B"), DType.BitOpt),
                new TypedName(new DName("C"), DType.I8Opt)
            );
            var typeRec2 = DType.CreateRecord(false,
                new TypedName(new DName("A"), DType.U8Opt),
                new TypedName(new DName("B"), DType.BitReq),
                new TypedName(new DName("C"), DType.I8Req)
            );
            var typeTbl = typeRec.ToSequence();
            var typeTbl2 = typeRec2.ToSequence();

            var guid1 = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("X"), new DataConfig(typeTbl), guid1));

            Sink.WriteLine("*** Change type and add a type-preserving extra formula");
            var doc = Doc;
            doc = doc.SetConfig(guid1, new DataConfig(typeTbl2));
            doc = doc.InsertExtraFormula(guid1, 0, CreateFormula("this->Filter(A < 10u1)"));
            Set(doc);
        }
    }

    [TestMethod]
    public void EquivalentFormulas()
    {
        int count = DoBaselineTests(
            Run, @"Doc\EquivalentFormulas.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var guidA = GuidNext();
            var nameA = MakeName("A");
            var fmaA = "Range(10)->Sort([>]it)";
            Set(Doc.CreateFlowNode(MakeName("A"), fmaA, guidA));

            Sink.WriteLine("*** Change whitespace only");
            Set(Doc.SetFormula(guidA, "Range(10)->Sort( [>] it )"));
            Undo();
            Doc.GetNode(guidA);

            Sink.WriteLine("*** Change directive syntax but not kind");
            Set(Doc.SetFormula(guidA, "Range(10)->Sort([down] it)"));
            Undo();
            Doc.GetNode(guidA);

            Sink.WriteLine("*** Change directive kind");
            Set(Doc.SetFormula(guidA, "Range(10)->Sort([up] it)"));
            Undo();

            Set(Doc.SetFormula(guidA, "Range(10)"));
            Set(Doc.InsertExtraFormula(guidA, 0, CreateFormula("Sort([>] this)")));

            Sink.WriteLine("*** Change extra formula whitespace only");
            Set(Doc.ReplaceExtraFormula(guidA, 0, CreateFormula("Sort( [>] this )")));
            Undo();

            Sink.WriteLine("*** Change extra formula directive syntax but not kind");
            Set(Doc.ReplaceExtraFormula(guidA, 0, CreateFormula("Sort([down] this)")));
            Undo();

            Sink.WriteLine("*** Change extra formula directive kind");
            Set(Doc.ReplaceExtraFormula(guidA, 0, CreateFormula("Sort([<] this)")));
            Undo();

            Sink.WriteLine("*** Test name equivalency");
            Set(Doc.SetFormula(guidA, "Map(x: Range(1), x+1)"));
            Set(Doc.SetFormula(guidA, "Map(y: Range(1), y+1)"));
            Undo(2);

            Sink.WriteLine("*** Parse errors");
            Set(Doc.SetFormula(guidA, "Range(10"));
            Set(Doc.SetFormula(guidA, "Range(10)"));
            Set(Doc.ReplaceExtraFormula(guidA, 0, CreateFormula("Sort([>] this")));
            Undo(3);

            Sink.WriteLine("*** Bind errors");
            Set(Doc.SetFormula(guidA, "Range(\"s\")"));
            Set(Doc.SetFormula(guidA, "Range(10)"));
            Set(Doc.ReplaceExtraFormula(guidA, 0, CreateFormula("Sort([>] \"s\")")));
            Undo(3);

            Set(Doc.CreateFlowNode(MakeName("B"), "Count(A)", GuidNext()));
            var doc = Doc;
            doc = doc.DeleteNode(guidA);
            doc = doc.CreateFlowNode(nameA, fmaA, GuidNext());
            Set(doc);
        }
    }

    [TestMethod]
    public void SmartRename()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartRename.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var guid1 = GuidNext();
            var nameA = MakeName("A");
            Set(Doc.CreateFlowNode(nameA, CreateFormula("1+1"), guid1));
            var nodeA = Doc.GetNode(guid1);
            Assert.AreEqual(nameA, nodeA.Name);
            Assert.AreEqual("1+1", nodeA.Formula.Text);
            Assert.IsTrue(Doc.ContainsNode(nameA));
            Assert.AreEqual(nodeA, Doc.GetNode(nameA));

            var guid2 = GuidNext();
            var nameB = MakeName("B");
            Set(Doc.CreateFlowNode(nameB, CreateFormula("A+1"), guid2));
            var nodeB = Doc.GetNode(guid2);
            Assert.AreEqual(nameB, nodeB.Name);
            Assert.AreEqual("A+1", nodeB.Formula.Text);
            Assert.IsTrue(Doc.ContainsNode(nameB));
            Assert.AreEqual(nodeB, Doc.GetNode(nameB));

            var guid3 = GuidNext();
            var nameC = MakeName("C");
            Set(Doc.CreateFlowNode(nameC, CreateFormula("x + Map(x: Range(10), x+A)"), guid3));
            var nodeC = Doc.GetNode(guid3);
            Assert.AreEqual(nameC, nodeC.Name);
            Assert.AreEqual("x + Map(x: Range(10), x+A)", nodeC.Formula.Text);
            Assert.IsTrue(Doc.ContainsNode(nameC));
            Assert.AreEqual(nodeC, Doc.GetNode(nameC));
            Assert.IsTrue(_graph.GetNode(guid3).MainBoundFormula.HasErrors);

            var nameX = MakeName("x");
            var changes = _graph.GetRenameChanges(guid1, nameX);
            Assert.AreEqual(2, changes?.Count);
            Assert.AreEqual("x+1", changes[(guid2, -1)]);
            Assert.AreEqual("x + Map(x: Range(10), x+@x)", changes[(guid3, -1)]);

            Set(Doc.RenameGlobal(guid1, nameX, changes));
            Assert.IsTrue(Doc.ContainsNode(nameX));
            Assert.IsFalse(Doc.ContainsNode(nameA));
            Assert.AreEqual("x+1", Doc.GetNode(guid2).Formula.Text);
            Assert.AreEqual("x + Map(x: Range(10), x+@x)", Doc.GetNode(guid3).Formula.Text);
            // The error went away when we renamed A to x.
            Assert.IsFalse(_graph.GetNode(guid3).MainBoundFormula.HasErrors);

            Undo();
            Assert.IsFalse(Doc.ContainsNode(nameX));
            Assert.IsTrue(Doc.ContainsNode(nameA));
            Assert.AreEqual("A+1", Doc.GetNode(guid2).Formula.Text);
            Assert.AreEqual("x + Map(x: Range(10), x+A)", Doc.GetNode(guid3).Formula.Text);
            Assert.IsTrue(_graph.GetNode(guid3).MainBoundFormula.HasErrors);

            Redo();
            Assert.IsTrue(Doc.ContainsNode(nameX));
            Assert.IsFalse(Doc.ContainsNode(nameA));
            Assert.AreEqual("x+1", Doc.GetNode(guid2).Formula.Text);
            Assert.AreEqual("x + Map(x: Range(10), x+@x)", Doc.GetNode(guid3).Formula.Text);
            // The error went away when we renamed A to x.
            Assert.IsFalse(_graph.GetNode(guid3).MainBoundFormula.HasErrors);

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void SmartRenameMore()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartRenameMore.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            {
                var doc = Doc;
                var guidA = MakeNode(ref doc, "A", "1+1");
                var guidB = MakeNode(ref doc, "B", "A+1");
                var guidC = MakeNode(ref doc, "C", "A + Map(X: Range(10), X+A)");
                var guidD = MakeNode(ref doc, "D", "A'A'");
                var guidE = MakeNode(ref doc, "E", "B+D");
                var guidF = MakeNode(ref doc, "F", "Map([{X:1}], X+A)");
                var guidG = MakeNode(ref doc, "G", "Map([[{X:1}]], X+A)");
                var guidH = MakeNode(ref doc, "H", "Map(x:[{X:1}], Map(x:[{Y:2}], X+Y+A))"); // x is shadowed but X is still in scope.
                var guidI = MakeNode(ref doc, "I", "Guard(A, A*A)"); // A is an implicit name.
                var guidJ = MakeNode(ref doc, "J", "[Guard(A, A*A), Guard(A, it*it)]");
                var guidK = MakeNode(ref doc, "K", "A->Guard(A*A)"); // A is not an implicit name because of pipe form.
                var guidL = MakeNode(ref doc, "L", "A->Guard(it*it)");
                var guidM = MakeNode(ref doc, "M", "{B, A, C:A}");
                var guidN = MakeNode(ref doc, "N", "Map(X:[{}], {A, X})");
                var guidP = MakeNode(ref doc, "P", "Q+1");
                var guidQ = MakeNode(ref doc, "Q", "A+1");
                Set(doc);

                var changes = _graph.GetRenameChanges(guidA, MakeName("X"));
                Assert.AreEqual(13, changes?.Count);
                Assert.AreEqual("X+1", changes[(guidB, -1)]);
                Assert.AreEqual("X + Map(X: Range(10), X+@X)", changes[(guidC, -1)]);
                Assert.AreEqual("X X", changes[(guidD, -1)]);
                Assert.IsFalse(changes.ContainsKey((guidE, -1)));
                Assert.AreEqual("Map([{X:1}], X+@X)", changes[(guidF, -1)]);
                Assert.AreEqual("Map([[{X:1}]], X+@X)", changes[(guidG, -1)]);
                Assert.AreEqual("Map(x:[{X:1}], Map(x:[{Y:2}], X+Y+@X))", changes[(guidH, -1)]);
                Assert.AreEqual("Guard(A: X, A*A)", changes[(guidI, -1)]);
                Assert.AreEqual("[Guard(A: X, A*A), Guard(X, it*it)]", changes[(guidJ, -1)]);
                Assert.AreEqual("X->Guard(X*X)", changes[(guidK, -1)]);
                Assert.AreEqual("X->Guard(it*it)", changes[(guidL, -1)]);
                Assert.AreEqual("{B, A: X, C:X}", changes[(guidM, -1)]);
                Assert.AreEqual("Map(X:[{}], {A: @X, X})", changes[(guidN, -1)]);
                Assert.AreEqual("X+1", changes[(guidQ, -1)]);
                Set(Doc.RenameGlobal(guidA, MakeName("X"), changes));
                Undo();

                changes = _graph.GetRenameChanges(guidA, MakeName("X.Y"));
                Assert.AreEqual(13, changes?.Count);
                Assert.AreEqual("X.Y+1", changes[(guidB, -1)]);
                Assert.AreEqual("X.Y + Map(X: Range(10), X+@X.Y)", changes[(guidC, -1)]);
                Assert.AreEqual("X.Y X.Y", changes[(guidD, -1)]);
                Assert.IsFalse(changes.ContainsKey((guidE, -1)));
                Assert.AreEqual("Map([{X:1}], X+@X.Y)", changes[(guidF, -1)]);
                Assert.AreEqual("Map([[{X:1}]], X+@X.Y)", changes[(guidG, -1)]);
                Assert.AreEqual("Map(x:[{X:1}], Map(x:[{Y:2}], X+Y+@X.Y))", changes[(guidH, -1)]);
                Assert.AreEqual("Guard(A: X.Y, A*A)", changes[(guidI, -1)]);
                Assert.AreEqual("[Guard(A: X.Y, A*A), Guard(X.Y, it*it)]", changes[(guidJ, -1)]);
                Assert.AreEqual("X.Y->Guard(X.Y*X.Y)", changes[(guidK, -1)]);
                Assert.AreEqual("X.Y->Guard(it*it)", changes[(guidL, -1)]);
                Assert.AreEqual("{B, A: X.Y, C:X.Y}", changes[(guidM, -1)]);
                Assert.AreEqual("Map(X:[{}], {A: @X.Y, X})", changes[(guidN, -1)]);
                Assert.AreEqual("X.Y+1", changes[(guidQ, -1)]);
                Set(Doc.RenameGlobal(guidA, MakeName("X.Y"), changes));
                Undo();
                Redo();

                // Tests when the renamed guid comes after the dependent guid.
                // The output shouldn't have any data changes.
                changes = _graph.GetRenameChanges(guidQ, MakeName("Z"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("Z+1", changes[(guidP, -1)]);
                Set(Doc.RenameGlobal(guidQ, MakeName("Z"), changes));
                Undo();
                Redo();
                Undo();

                UndoAll();
                RedoAll();
                UndoAll();
            }

            {
                var doc = Doc;
                var guidA1 = MakeNode(ref doc, "A1", "1+1");
                var guidB1 = MakeNode(ref doc, "B1", "1+A1 + 1");
                var guidC1 = MakeNode(ref doc, "C1", "'A B.@#$%.C' + Map('A B.@#$%.C': Range(10), 'A B.@#$%.C'+A1)");
                Set(doc);

                var changes = _graph.GetRenameChanges(guidA1, MakeName("'A B.@#$%.C'"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("1+'A B.@#$%.C' + 1", changes[(guidB1, -1)]);
                Assert.AreEqual("'A B.@#$%.C' + Map('A B.@#$%.C': Range(10), 'A B.@#$%.C'+@'A B.@#$%.C')", changes[(guidC1, -1)]);
                Set(Doc.RenameGlobal(guidA1, MakeName("'A B.@#$%.C'"), changes));
                Undo();
                Redo();

                UndoAll();
                RedoAll();
                UndoAll();
            }

            {
                var doc = Doc;
                var guidNX_X = MakeNode(ref doc, "Ns.'X X'", "1");
                var guidNY = MakeNode(ref doc, "Ns.Y", "{ 'X X' }");
                Set(doc);

                var changes = _graph.GetRenameChanges(guidNX_X, MakeName("Ns.Z"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("{ 'X X': Z }", changes[(guidNY, -1)]);
                Set(Doc.RenameGlobal(guidNX_X, MakeName("Ns.Z"), changes));
                Undo();
                Redo();

                UndoAll();
                RedoAll();
                UndoAll();
            }

            {
                var doc = Doc;
                var guidA_B = MakeNode(ref doc, "'A B'", "\"he\"");
                var guidA2 = MakeNode(ref doc, "A2", "\"hello\" has'A B'");
                var guidB2 = MakeNode(ref doc, "B2", "'A B'has \"h\"");
                var guidC2 = MakeNode(ref doc, "C2", "[\"hello\" has'A B', 'A B'has \"h\"]");
                Set(doc);

                var changes = _graph.GetRenameChanges(guidA_B, MakeName("Z2"));
                Assert.AreEqual(3, changes?.Count);
                Assert.AreEqual("\"hello\" has Z2", changes[(guidA2, -1)]);
                Assert.AreEqual("Z2 has \"h\"", changes[(guidB2, -1)]);
                Assert.AreEqual("[\"hello\" has Z2, Z2 has \"h\"]", changes[(guidC2, -1)]);
                Set(Doc.RenameGlobal(guidA_B, MakeName("Z2"), changes));
                Undo();
                Redo();

                UndoAll();
                RedoAll();
                UndoAll();
            }

            {
                var doc = Doc;
                var guidA = MakeNodeData(ref doc, "A", DType.Text.ToSequence());
                MakeNode(ref doc, "B", "A->Count()");
                Set(doc);

                var changes = _graph.GetRenameChanges(guidA, MakeName("X"));
                Set(Doc.RenameGlobal(guidA, MakeName("X"), changes));
                Undo();
                Redo();

                UndoAll();
                RedoAll();
                UndoAll();
            }
        }
    }

    [TestMethod]
    public void RenameIntoNamespace()
    {
        int count = DoBaselineTests(
            Run, @"Doc\RenameIntoNamespace.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            Set(Doc.CreateFlowNode(MakeName("A"), CreateFormula("1"), GuidNext()));
            var guid2 = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("X"), CreateFormula("A"), guid2));
            Set(Doc.CreateFlowNode(MakeName("N.A"), CreateFormula("true"), GuidNext()));

            Set(Doc.RenameGlobal(guid2, MakeName("N.X")));

            Undo();
            Undo();
            Undo();
            Undo();
            Redo();
            Redo();
            Redo();
            Redo();

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void NewInnerNamespace()
    {
        int count = DoBaselineTests(
            Run, @"Doc\NewInnerNamespace.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            Set(Doc.CreateFlowNode(MakeName("A.X"), CreateFormula("1"), GuidNext()));
            Set(Doc.CreateFlowNode(MakeName("A.Y"), CreateFormula("A.X + @A.X"), GuidNext()));

            // This should cause the formula in A.Y to rebind.
            Set(Doc.CreateFlowNode(MakeName("A.A.X"), CreateFormula("2i8"), GuidNext()));

            Undo();
            Undo();
            Undo();
            Redo();
            Redo();
            Redo();

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void GnarlyRename()
    {
        int count = DoBaselineTests(
            Run, @"Doc\GnarlyRename.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var doc = Doc;
            var nodeB = MakeNode(ref doc, "B", "1+1");
            var guidXYB = MakeNode(ref doc, "X.Y.B", "1");
            var guidXYC = MakeNode(ref doc, "X.Y.C", "Y.B + @X.Y.B + B + @B");
            var guidF = MakeNode(ref doc, "F", "X.Y.B + B");
            var guidXYG = MakeNode(ref doc, "X.Y.G", "B");
            Set(doc);

            Guid guid;
            NPath name;
            var changes = _graph.GetRenameChanges(guid = guidXYB, name = MakeName("Y.B"));
            Assert.AreEqual(3, changes?.Count);
            Assert.AreEqual("@Y.B + @Y.B + @Y.B + @B", changes[(guidXYC, -1)]);
            Assert.AreEqual("Y.B + B", changes[(guidF, -1)]);
            Assert.AreEqual("@Y.B", changes[(guidXYG, -1)]);
            Set(Doc.RenameGlobal(guid, name, changes));
            Undo();

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void SmartRenameWithNs()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartRenameWithNs.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            {
                var doc = Doc;
                var guidA = MakeNode(ref doc, "A", "1+1");
                var guidB = MakeNode(ref doc, "B", "1+1");
                var guidC = MakeNode(ref doc, "C", "1+1");
                var guidXYB = MakeNode(ref doc, "X.Y.B", "A + 1 + @B");
                var guidXYC = MakeNode(ref doc, "X.Y.C", "Y.B + @X.Y.B + B + @B");
                var guidF = MakeNode(ref doc, "F", "X.Y.B + B");
                Sink.WriteLine("*** Expected error in X.Y.D for \"X.Y.D\"");
                var guidXYD = MakeNode(ref doc, "X.Y.D", "X.Y.D + Y.D + D + @X.Y.D");
                var guidXYXYB = MakeNode(ref doc, "X.Y.X.Y.B", "A + 1 + A");
                var guidXYG = MakeNode(ref doc, "X.Y.G", "B");
                var guidG = MakeNode(ref doc, "G", "X.Y.C + C");
                Set(doc);

                // Check that referenced globals with namespaces are correctly shortened.
                Assert.IsNull(_graph.GetRenameChanges(guidA, MakeName("X.A")));
                Assert.IsNull(_graph.GetRenameChanges(guidA, MakeName("X.Y.A")));

                Guid guid;
                NPath name;
                var changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("X.Y.Z.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("Z.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("Z.A + 1 + Z.A", changes[(guidXYXYB, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("X.Y.Z.A.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("Z.A.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("Z.A.A + 1 + Z.A.A", changes[(guidXYXYB, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("Z.Y.X.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("Z.Y.X.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("Z.Y.X.A + 1 + Z.Y.X.A", changes[(guidXYXYB, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidXYB, name = MakeName("Y.B"));
                Assert.AreEqual(3, changes?.Count);
                Assert.AreEqual("@Y.B + @Y.B + @Y.B + @B", changes[(guidXYC, -1)]);
                Assert.AreEqual("Y.B + B", changes[(guidF, -1)]);
                Assert.AreEqual("@Y.B", changes[(guidXYG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidXYB, name = MakeName("X.B"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("B + @X.B + B + @B", changes[(guidXYC, -1)]);
                Assert.AreEqual("X.B + B", changes[(guidF, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidXYB, name = MakeName("M.N.O"));
                Assert.AreEqual(3, changes?.Count);
                Assert.AreEqual("M.N.O + @M.N.O + M.N.O + @B", changes[(guidXYC, -1)]);
                Assert.AreEqual("M.N.O + B", changes[(guidF, -1)]);
                Assert.AreEqual("M.N.O", changes[(guidXYG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                // Check that the referenced globals of the renamed node are correctly renamed
                // with respect to the new namespace.
                changes = _graph.GetRenameChanges(guid = guidXYC, name = MakeName("X.C"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("Y.B + @X.Y.B + Y.B + @B", changes[(guidXYC, -1)]);
                Assert.AreEqual("X.C + C", changes[(guidG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidXYC, name = MakeName("C2"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("X.Y.B + @X.Y.B + X.Y.B + @B", changes[(guidXYC, -1)]);
                Assert.AreEqual("C2 + C", changes[(guidG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidXYB, name = MakeName("X.Y.Z.B"));
                Assert.AreEqual(3, changes?.Count);
                Assert.AreEqual("Z.B + @X.Y.Z.B + Z.B + @B", changes[(guidXYC, -1)]);
                Assert.AreEqual("X.Y.Z.B + B", changes[(guidF, -1)]);
                Assert.AreEqual("Z.B", changes[(guidXYG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                // Check that shadowed references are correctly at'ed.
                changes = _graph.GetRenameChanges(guid = guidF, name = MakeName("X.Y.F"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("B + @B", changes[(guidF, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidG, name = MakeName("X.Y.X.Y.G"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("C + @C", changes[(guidG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidXYG, name = MakeName("X.Y.X.Y.G"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("@X.Y.B", changes[(guidXYG, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                changes = _graph.GetRenameChanges(guid = guidB, name = MakeName("D"));
                Assert.AreEqual(3, changes?.Count);
                Assert.AreEqual("Y.B + @X.Y.B + B + @D", changes[(guidXYC, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                // Check that there is no weirdness with namespaces that slightly overlap with each other.
                changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("A.B.X.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("A.B.X.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("A.B.X.A + 1 + A.B.X.A", changes[(guidXYXYB, -1)]);
                bool caught = false;
                try { Doc.RenameGlobal(guid, name, changes); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("A.B.X.Y.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("A.B.X.Y.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("A.B.X.Y.A + 1 + A.B.X.Y.A", changes[(guidXYXYB, -1)]);
                caught = false;
                try { Doc.RenameGlobal(guid, name, changes); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("A.B.C.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("A.B.C.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("A.B.C.A + 1 + A.B.C.A", changes[(guidXYXYB, -1)]);
                caught = false;
                try { Doc.RenameGlobal(guid, name, changes); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                changes = _graph.GetRenameChanges(guid = guidA, name = MakeName("A.B.C.D.A"));
                Assert.AreEqual(2, changes?.Count);
                Assert.AreEqual("A.B.C.D.A + 1 + @B", changes[(guidXYB, -1)]);
                Assert.AreEqual("A.B.C.D.A + 1 + A.B.C.D.A", changes[(guidXYXYB, -1)]);
                caught = false;
                try { Doc.RenameGlobal(guid, name, changes); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                // Check self reference.
                // Note that in the original source, "X.Y.D" is not a self reference, since it references the non-existent
                // "X.Y.X.Y.D". That is, since "X.Y.X" is a namespace, it is found when binding just "X" within the namespace
                // "X.Y". Hence these produce an error for "X.Y.D".
                Sink.WriteLine("*** Expected error on X.Y.D");
                changes = _graph.GetRenameChanges(guid = guidXYD, name = MakeName("D"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("X.Y.D + D + D + @D", changes[(guidXYD, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                Sink.WriteLine("*** Expected error on X.Y.D");
                changes = _graph.GetRenameChanges(guid = guidXYD, name = MakeName("X.D"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("X.Y.D + D + D + @X.D", changes[(guidXYD, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                Sink.WriteLine("*** Expected error on X.Y.D");
                changes = _graph.GetRenameChanges(guid = guidXYD, name = MakeName("Y.D"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("X.Y.D + D + D + @Y.D", changes[(guidXYD, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                Sink.WriteLine("*** Expected error on X.Y.D");
                changes = _graph.GetRenameChanges(guid = guidXYD, name = MakeName("X.E"));
                Assert.AreEqual(1, changes?.Count);
                Assert.AreEqual("X.Y.D + E + E + @X.E", changes[(guidXYD, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                UndoAll();
                RedoAll();
                UndoAll();
            }

            {
                var doc = Doc;
                doc = doc.CreateFlowNode(MakeName("X.Y.Z.A"), "1", GuidNext());
                doc = doc.CreateFlowNode(MakeName("X.Y.Z.B"), "1", GuidNext());
                var guidXYZC = GuidNext();
                doc = doc.CreateFlowNode(MakeName("X.Y.Z.C"), "Y.Z.A + Y.Z.B", guidXYZC);
                Set(doc);

                Guid guid;
                NPath name;
                var changes = _graph.GetRenameChanges(guid = guidXYZC, name = MakeName("X.Y.Z.X.C"));
                Assert.AreEqual(1, changes?.Count);
                // REVIEW: Keep as "Y.Z.A + Y.Z.B"?
                Assert.AreEqual("A + B", changes[(guidXYZC, -1)]);
                Set(Doc.RenameGlobal(guid, name, changes));
                Undo();

                UndoAll();
            }
        }
    }

    [TestMethod]
    public void SmartRenameChained()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartRenameChained.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var doc = Doc;
            var guidNXA = MakeNode(ref doc, "N.X.A", "1");
            var guidNXB = MakeNode(ref doc, "N.X.B", "2");
            var guidNXC = MakeNode(ref doc, "N.X.C", "A + B");
            Set(doc);

            Guid guid;
            NPath name;
            var changes = _graph.GetRenameChanges(guid = guidNXA, name = MakeName("N.P.A"));
            Set(Doc.RenameGlobal(guid, name, changes));
            changes = _graph.GetRenameChanges(guid = guidNXC, name = MakeName("N.P.C"));
            Assert.AreEqual(1, changes?.Count);
            Assert.AreEqual("A + X.B", changes[(guidNXC, -1)]);
            Set(Doc.RenameGlobal(guid, name, changes));
            Undo(2);

            Set(Doc.SetFormula(guidNXC, "Map(X: Range(5), A + B)"));

            changes = _graph.GetRenameChanges(guid = guidNXA, name = MakeName("N.P.A"));
            Set(Doc.RenameGlobal(guid, name, changes));
            changes = _graph.GetRenameChanges(guid = guidNXC, name = MakeName("N.P.C"));
            Assert.AreEqual(1, changes?.Count);
            Assert.AreEqual("Map(X: Range(5), A + N.X.B)", changes[(guidNXC, -1)]);
            Set(Doc.RenameGlobal(guid, name, changes));
            Undo(2);

            Set(Doc.CreateFlowNode(MakeName("W"), "X", GuidNext()));

            changes = _graph.GetRenameChanges(guid = guidNXA, name = MakeName("N.P.A"));
            Set(Doc.RenameGlobal(guid, name, changes));
            changes = _graph.GetRenameChanges(guid = guidNXC, name = MakeName("N.P.C"));
            Assert.AreEqual(1, changes?.Count);
            Assert.AreEqual("Map(X: Range(5), A + N.X.B)", changes[(guidNXC, -1)]);
            Set(Doc.RenameGlobal(guid, name, changes));
            Undo(2);

            // Test for null node in GetRenameChanges while referencing its properties.
            Set(Doc.DeleteNode(guidNXA));
            Set(Doc.DeleteNode(guidNXC));
            changes = _graph.GetRenameChanges(guidNXB, MakeName("N.P.C"));
            Assert.IsNull(changes);

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void SmartRenameNs()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartRenameNs.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var doc = Doc;
            doc = doc.CreateExplicitNamespace(out var nsXY, MakeName("X.Y"), null);
            doc = doc.CreateExplicitNamespace(out var nsXZ, MakeName("X.Z"), null);
            Set(doc);

            doc = Doc;
            MakeNode(ref doc, "A", "1+1");
            MakeNode(ref doc, "B", "1+1");
            MakeNode(ref doc, "C", "1+1");
            var guidXYB = MakeNode(ref doc, "X.Y.B", "A + 1 + @B");
            var guidXYC = MakeNode(ref doc, "X.Y.C", "Y.B + @X.Y.B + B + @B");
            var guidXYD = MakeNode(ref doc, "X.Y.D", "X.Y.D + Y.D + D + @X.Y.D");
            var guidXZC = MakeNode(ref doc, "X.Z.C", "Y.B + C + E");
            var guidXZE = MakeNode(ref doc, "X.Z.E", "Y.B + C + A");
            MakeNode(ref doc, "X.Y.Z.F", "A + B + C + D");
            var guidF = MakeNode(ref doc, "F", "X.Y.B + B");
            Set(doc);

            NPath nameSrc, nameDst;
            var changes = _graph.GetNamespaceRenameChanges(nameSrc = MakeName("X.Y"), nameDst = MakeName("Y"));
            Assert.AreEqual(3, changes?.Count);
            Assert.AreEqual("B + @Y.B + B + @B", changes[(guidXYC, -1)]);
            Assert.AreEqual("Y.B + B", changes[(guidF, -1)]);
            Assert.AreEqual("D + D + D + @Y.D", changes[(guidXYD, -1)]);
            Set(Doc.RenameNamespace(nameSrc, nameDst, changes));
            Undo(1);

            // Can't rename to X because it is already created.
            changes = _graph.GetNamespaceRenameChanges(nameSrc = MakeName("X.Y"), nameDst = MakeName("X"));
            Assert.IsNull(changes);

            changes = _graph.GetNamespaceRenameChanges(nameSrc = MakeName("X.Y"), nameDst = MakeName("X.Y.Q"));
            Assert.AreEqual(5, changes?.Count);
            Assert.AreEqual("B + @X.Y.Q.B + B + @B", changes[(guidXYC, -1)]);
            Assert.AreEqual("X.Y.Q.B + B", changes[(guidF, -1)]);
            Assert.AreEqual("D + D + D + @X.Y.Q.D", changes[(guidXYD, -1)]);
            Assert.AreEqual("Y.Q.B + C + E", changes[(guidXZC, -1)]);
            Assert.AreEqual("Y.Q.B + C + A", changes[(guidXZE, -1)]);
            // Renaming fails because it is illegal for nameDst to be a child of nameSrc.
            bool caught = false;
            try { Doc.RenameNamespace(nameSrc, nameDst, changes); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            changes = _graph.GetNamespaceRenameChanges(nameSrc = MakeName("X.Y"), nameDst = MakeName("X.Q"));
            Assert.AreEqual(5, changes?.Count);
            Assert.AreEqual("B + @X.Q.B + B + @B", changes[(guidXYC, -1)]);
            Assert.AreEqual("D + D + D + @X.Q.D", changes[(guidXYD, -1)]);
            Assert.AreEqual("Q.B + C + E", changes[(guidXZC, -1)]);
            Assert.AreEqual("Q.B + C + A", changes[(guidXZE, -1)]);
            Assert.AreEqual("X.Q.B + B", changes[(guidF, -1)]);
            Set(Doc.RenameNamespace(nameSrc, nameDst, changes));
            Undo(1);

            changes = _graph.GetNamespaceRenameChanges(nameSrc = NPath.Root, nameDst = MakeName("X.Q"));
            Assert.AreEqual(3, changes?.Count);
            Assert.AreEqual("A + 1 + @X.Q.B", changes[(guidXYB, -1)]);
            Assert.AreEqual("B + @X.Q.X.Y.B + B + @X.Q.B", changes[(guidXYC, -1)]);
            Assert.AreEqual("D + D + D + @X.Q.X.Y.D", changes[(guidXYD, -1)]);
            // Renaming fails because it is illegal for nameDst to be a child of nameSrc.
            caught = false;
            try { Doc.RenameNamespace(nameSrc, nameDst, changes); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            Undo();
            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void SmartDuplicate()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartDuplicate.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            Set(Doc.CreateFlowNode(MakeName("A"), CreateFormula("1"), GuidNext()));
            var nameX = MakeName("X");
            Set(Doc.CreateFlowNode(nameX, CreateFormula("A"), GuidNext()));
            var nodeX = Doc.GetNode(nameX);

            var nameY = MakeName("Y");
            string renamedFormula = _graph.GetRenamedFormula(nodeX.Guid, nameY);
            var formula = string.IsNullOrEmpty(renamedFormula) ? nodeX.Formula : CreateFormula(renamedFormula);
            Set(Doc.CreateFlowNode(nameY, formula, nodeX.Config, GuidNext()));
            var nodeY = Doc.GetNode(nameY);

            Assert.IsTrue(Doc.ContainsNode(nameX));
            Assert.IsTrue(Doc.ContainsNode(nameY));
            Assert.AreEqual("A", Doc.GetNode(nodeY.Name).Formula.Text);

            Undo();
            Assert.IsFalse(Doc.ContainsNode(nameY));

            Redo();
            Assert.IsTrue(Doc.ContainsNode(nameY));

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void SmartDuplicateIntoNamespace()
    {
        int count = DoBaselineTests(
            Run, @"Doc\SmartDuplicateIntoNamespace.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            Set(Doc.CreateFlowNode(MakeName("A"), CreateFormula("1"), GuidNext()));
            Set(Doc.CreateFlowNode(MakeName("N.A"), CreateFormula("true"), GuidNext()));
            Set(Doc.CreateFlowNode(MakeName("X"), CreateFormula("A"), GuidNext()));
            var nodeX = Doc.GetNode(MakeName("X"));

            var nameNX = MakeName("N.X");
            string renamedFormula = _graph.GetRenamedFormula(nodeX.Guid, nameNX);
            var formula = string.IsNullOrEmpty(renamedFormula) ? nodeX.Formula : CreateFormula(renamedFormula);
            Set(Doc.CreateFlowNode(nameNX, formula, nodeX.Config, GuidNext()));
            var nodeNX = Doc.GetNode(nameNX);
            Assert.AreEqual("@A", Doc.GetNode(nodeNX.Name).Formula.Text);

            Undo();
            Undo();
            Undo();
            Undo();
            Redo();
            Redo();
            Redo();
            Redo();

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void TestCycles()
    {
        int count = DoBaselineTests(
            Run, @"Doc\Cycles.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var guidA = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("A"), CreateFormula("A + 1 | CastI4(_)"), guidA));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            var cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.IsTrue(cuts.Contains(guidA));

            Set(Doc.SetFormula(guidA, CreateFormula("17i4")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(0, cuts.Count);
            Assert.IsFalse(cuts.Contains(guidA));

            var guidB = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("B"), CreateFormula("A + 1 | CastI4(_)"), guidB));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(0, cuts.Count);

            Set(Doc.SetFormula(guidA, CreateFormula("B + 1 | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            var gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);

            Set(Doc.SetFormula(guidA, CreateFormula("17i4")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(0, cuts.Count);

            Set(Doc.SetFormula(guidA, CreateFormula("A + B | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);

            // Note: it would also be legal to only have one "cut", namely A, if A were first in topo.
            cuts = _graph.CycleCuts;
            Assert.AreEqual(2, cuts.Count);
            gns = _graph.GetNodes();
            Assert.AreEqual(guidB, gns[0].Guid);
            Assert.AreEqual(guidA, gns[1].Guid);

            // Recomputing the graph demonstrates the above note.
            var graph = Doc.GetGraph(_host, _graph);
            cuts = graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            gns = graph.GetNodes();
            Assert.AreEqual(guidA, gns[0].Guid);
            Assert.AreEqual(guidB, gns[1].Guid);

            Set(Doc.SetFormula(guidA, CreateFormula("A + A | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.AreEqual(cuts.First, guidA);
            gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);

            Set(Doc.SetFormula(guidB, CreateFormula("A + A | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.AreEqual(cuts.First, guidA);
            gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);

            var guidC = GuidNext();
            var guidD = GuidNext();
            var doc = Doc;
            doc = doc.SetFormula(guidA, CreateFormula("D"));
            doc = doc.SetFormula(guidB, CreateFormula("A"));
            doc = doc.CreateFlowNode(MakeName("C"), CreateFormula("B"), guidC);
            doc = doc.CreateFlowNode(MakeName("D"), CreateFormula("C"), guidD);
            Set(doc);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidC).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidD).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);

            Set(Doc.SetFormula(guidB, CreateFormula("A + D | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidC).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidD).Type);

            // Note: There are several options for "cuts".
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);
            Assert.AreEqual(2, _graph.GetNode(guidB).Uses.Count);

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void TestCyclesWithErrors()
    {
        int count = DoBaselineTests(
            Run, @"Doc\CyclesWithErrors.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var guidA = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("A"), CreateFormula("A +  | CastI4(_)"), guidA));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            var cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.IsTrue(cuts.Contains(guidA));

            Set(Doc.SetFormula(guidA, CreateFormula("17 +  | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(0, cuts.Count);

            var guidB = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("B"), CreateFormula("A + 1 | CastI4(_)"), guidB));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(0, cuts.Count);

            Set(Doc.SetFormula(guidA, CreateFormula("B +  | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.IsTrue(cuts.Contains(guidB));

            Set(Doc.SetFormula(guidA, CreateFormula("17 +  | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(0, cuts.Count);

            Set(Doc.SetFormula(guidA, CreateFormula("A + B +  | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(2, cuts.Count);
            Assert.IsTrue(cuts.Contains(guidA));
            Assert.IsTrue(cuts.Contains(guidB));

            Set(Doc.SetFormula(guidA, CreateFormula("A + A +  | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.IsTrue(cuts.Contains(guidA));

            Set(Doc.SetFormula(guidB, CreateFormula("A + A | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            Assert.IsTrue(cuts.Contains(guidA));

            var guidC = GuidNext();
            var guidD = GuidNext();
            var doc = Doc;
            doc = doc.SetFormula(guidA, CreateFormula("D if Wrap(true) else "));
            doc = doc.SetFormula(guidB, CreateFormula("A"));
            doc = doc.CreateFlowNode(MakeName("C"), CreateFormula("B"), guidC);
            doc = doc.CreateFlowNode(MakeName("D"), CreateFormula("C"), guidD);
            Set(doc);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidC).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidD).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            var gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);

            Set(Doc.SetFormula(guidB, CreateFormula("A + D | CastI4(_)")));
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidA).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidB).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidC).Type);
            Assert.AreEqual(DType.I4Req, _graph.GetNode(guidD).Type);
            cuts = _graph.CycleCuts;
            Assert.AreEqual(1, cuts.Count);
            gns = _graph.GetNodes();
            Assert.AreEqual(cuts.First, gns[0].Guid);

            UndoAll();
            RedoAll();
        }
    }

    [TestMethod]
    public void AddNs()
    {
        int count = DoBaselineTests(
            Run, @"Doc\AddNs.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);
            Assert.IsNull(nss[0].ns);
            Assert.AreEqual(NPath.Root, nss[0].name);

            var name1 = MakeName("SomeName");
            Assert.IsFalse(Doc.HasNameConflict(name1));

            Sink.WriteLine("*** Create node");
            var fma1 = CreateFormula("[1i4, 3i4, 8i4, -2i4, 0i4]");
            Set(Doc.CreateFlowNode(name1, fma1, GuidNext()));
            var node1 = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(node1));
            Assert.AreEqual(name1, node1.Name);
            Assert.AreEqual(fma1, node1.Formula);
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(1, Doc.NodeCount);
            Assert.AreEqual(node1, Doc.GetNode(name1));
            Assert.AreEqual(1, Doc.GetNamespaces().Count);

            name1 = MakeName("A.X");
            Assert.IsFalse(Doc.HasNameConflict(name1));

            Set(Doc.RenameGlobal(node1.Guid, name1));
            node1 = Doc.GetNode(node1.Guid);
            Assert.IsTrue(Doc.IsActive(node1));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNull(nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Undo();
            Assert.AreEqual(1, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Redo();
            Assert.AreEqual(1, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNull(nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Sink.WriteLine("*** Create exp ns");
            Set(Doc.CreateExplicitNamespace(out var nsA, MakeName("A"), null));
            Assert.IsTrue(Doc.IsActive(nsA));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.AreEqual(nsA, nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Undo();
            Assert.IsTrue(!Doc.IsActive(nsA));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNull(nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Redo();
            Assert.IsTrue(Doc.IsActive(nsA));
            nsA = Doc.GetNamespace(MakeName("A"));
            Assert.IsNotNull(nsA);
            Assert.IsTrue(Doc.IsActive(nsA));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.AreEqual(nsA, nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Sink.WriteLine("*** Delete exp ns and contents");
            Set(Doc.DeleteExplicitNamespace(nsA, true));
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.IsTrue(Doc.IsActive(nsA));
            var nodes = Doc.GetFlowNodes().ToList();
            Assert.AreEqual(1, nodes.Count);
            Assert.IsTrue(Doc.IsActive(nodes[0]));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsTrue(Doc.IsActive(nss[1].ns));
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Redo();
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            nodes = Doc.GetFlowNodes().ToList();
            Assert.AreEqual(1, nodes.Count);
            Assert.IsTrue(Doc.IsActive(nodes[0]));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsTrue(Doc.IsActive(nss[1].ns));
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Sink.WriteLine("*** Delete exp ns only");
            Set(Doc.DeleteExplicitNamespace(nss[1].ns, false));
            nodes = Doc.GetFlowNodes().ToList();
            Assert.AreEqual(1, nodes.Count);
            Assert.IsTrue(Doc.IsActive(nodes[0]));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNull(nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Undo();
            nodes = Doc.GetFlowNodes().ToList();
            Assert.AreEqual(1, nodes.Count);
            Assert.IsTrue(Doc.IsActive(nodes[0]));
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsTrue(Doc.IsActive(nss[1].ns));
            Assert.AreEqual(MakeName("A"), nss[1].name);

            var name2 = MakeName("A.B.X");
            Assert.IsFalse(Doc.HasNameConflict(name2));

            Sink.WriteLine("*** Create node");
            var fma2 = CreateFormula("A.X * 3");
            Set(Doc.CreateFlowNode(name2, fma2, GuidNext()));
            var node2 = Doc.GetNode(name2);
            Assert.IsTrue(Doc.IsActive(node2));
            Assert.AreEqual(name2, node2.Name);
            Assert.AreEqual(fma2, node2.Formula);
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.AreEqual(node2, Doc.GetNode(name2));
            var nodeT = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(3, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);

            Assert.IsTrue(Doc.HasNameConflict(MakeName("A")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.B")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.B.X")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));

            Undo();
            Assert.AreEqual(1, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(2, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);

            Assert.IsTrue(Doc.HasNameConflict(MakeName("A")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X.Y")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.X")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));

            Redo();
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(2, Doc.NodeCount);
            nodeT = Doc.GetNode(name2);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node2.Guid, nodeT.Guid);
            Assert.AreEqual(fma2, nodeT.Formula);
            nodeT = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(3, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            nsA = nss[1].ns;

            Assert.IsTrue(Doc.HasNameConflict(MakeName("A")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.B")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.B.X")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));

            Sink.WriteLine("*** Delete named ns only");
            Assert.IsTrue(Doc.IsActive(nsA));
            Set(Doc.DeleteNamespace(MakeName("A"), deleteContents: false));
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.AreEqual(2, Doc.NodeCount);
            nodeT = Doc.GetNode(name2);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node2.Guid, nodeT.Guid);
            Assert.AreEqual(fma2, nodeT.Formula);
            nodeT = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(3, nss.Count);
            Assert.IsNull(nss[1].ns);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);

            Assert.IsTrue(Doc.HasNameConflict(MakeName("A")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.X.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.B")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));
            Assert.IsTrue(Doc.HasNameConflict(MakeName("A.B.X")));
            Assert.IsTrue(!Doc.HasNameConflict(MakeName("A.B.Y")));

            Undo();
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(2, Doc.NodeCount);
            nodeT = Doc.GetNode(name2);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node2.Guid, nodeT.Guid);
            Assert.AreEqual(fma2, nodeT.Formula);
            node2 = nodeT;
            nodeT = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            node1 = nodeT;
            nss = Doc.GetNamespaces();
            Assert.AreEqual(3, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            nsA = nss[1].ns;

            Sink.WriteLine("*** Delete named ns and contents");
            Assert.IsTrue(Doc.IsActive(nsA));
            Set(Doc.DeleteNamespace(MakeName("A"), deleteContents: true));
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(2, Doc.NodeCount);
            nodeT = Doc.GetNode(name2);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node2.Guid, nodeT.Guid);
            Assert.AreEqual(fma2, nodeT.Formula);
            node2 = nodeT;
            nodeT = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            node1 = nodeT;
            nss = Doc.GetNamespaces();
            Assert.AreEqual(3, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);

            Redo();
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            Assert.AreEqual(2, Doc.NodeCount);
            nodeT = Doc.GetNode(name2);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node2.Guid, nodeT.Guid);
            Assert.AreEqual(fma2, nodeT.Formula);
            node2 = nodeT;
            nodeT = Doc.GetNode(name1);
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            node1 = nodeT;
            nss = Doc.GetNamespaces();
            Assert.AreEqual(3, nss.Count);
            Assert.IsNotNull(nss[1].ns);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            nsA = nss[1].ns;

            Sink.WriteLine("*** Create exp ns");
            Set(Doc.CreateExplicitNamespace(out var nsACD, MakeName("A.C.D"), null));
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsTrue(Doc.IsActive(nsACD));
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            nss = Doc.GetNamespaces();
            nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));
            Assert.AreEqual(5, nss.Count);
            Assert.AreEqual(nsA, nss[1].ns);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            Assert.IsNull(nss[3].ns);
            Assert.AreEqual(MakeName("A.C"), nss[3].name);
            Assert.AreEqual(nsACD, nss[4].ns);
            Assert.AreEqual(MakeName("A.C.D"), nss[4].name);

            Sink.WriteLine("*** Delete exp ns and contents");
            Set(Doc.DeleteExplicitNamespace(nsA, deleteContents: true));
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.IsTrue(!Doc.IsActive(nsACD));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            nss = Doc.GetNamespaces();
            nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));
            Assert.AreEqual(5, nss.Count);
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsNotNull(nss[1].ns);
            Assert.AreEqual(nsA.Guid, nss[1].ns.Guid);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            Assert.IsNull(nss[3].ns);
            Assert.AreEqual(MakeName("A.C"), nss[3].name);
            Assert.IsTrue(Doc.IsActive(nsACD));
            Assert.IsNotNull(nss[4].ns);
            Assert.AreEqual(nsACD.Guid, nss[4].ns.Guid);
            Assert.AreEqual(MakeName("A.C.D"), nss[4].name);
            var nsT = Doc.GetNamespace(MakeName("A"));
            Assert.AreEqual(nsA.Guid, nsT.Guid);
            nsT = Doc.GetNamespace(MakeName("A.C.D"));
            Assert.AreEqual(nsACD.Guid, nsT.Guid);

            Redo();
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.IsTrue(!Doc.IsActive(nsACD));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            nss = Doc.GetNamespaces();
            nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));
            Assert.AreEqual(5, nss.Count);
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsNotNull(nss[1].ns);
            Assert.AreEqual(nsA.Guid, nss[1].ns.Guid);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            Assert.IsNull(nss[3].ns);
            Assert.AreEqual(MakeName("A.C"), nss[3].name);
            Assert.IsTrue(Doc.IsActive(nsACD));
            Assert.IsNotNull(nss[4].ns);
            Assert.AreEqual(nsACD.Guid, nss[4].ns.Guid);
            Assert.AreEqual(MakeName("A.C.D"), nss[4].name);
            nsT = Doc.GetNamespace(MakeName("A"));
            Assert.AreEqual(nsA.Guid, nsT.Guid);
            nsT = Doc.GetNamespace(MakeName("A.C.D"));
            Assert.AreEqual(nsACD.Guid, nsT.Guid);

            Sink.WriteLine("*** Delete named ns and contents");
            Set(Doc.DeleteNamespace(MakeName("A")));
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.IsTrue(!Doc.IsActive(nsACD));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            nss = Doc.GetNamespaces();
            nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));
            Assert.AreEqual(5, nss.Count);
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsNotNull(nss[1].ns);
            Assert.AreEqual(nsA.Guid, nss[1].ns.Guid);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            Assert.IsNull(nss[3].ns);
            Assert.AreEqual(MakeName("A.C"), nss[3].name);
            Assert.IsTrue(Doc.IsActive(nsACD));
            Assert.IsNotNull(nss[4].ns);
            Assert.AreEqual(nsACD.Guid, nss[4].ns.Guid);
            Assert.AreEqual(MakeName("A.C.D"), nss[4].name);
            nsT = Doc.GetNamespace(MakeName("A"));
            Assert.AreEqual(nsA.Guid, nsT.Guid);
            nsT = Doc.GetNamespace(MakeName("A.C.D"));
            Assert.AreEqual(nsACD.Guid, nsT.Guid);

            Redo();
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.IsTrue(!Doc.IsActive(nsACD));
            Assert.AreEqual(0, Doc.NodeCount);
            nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);

            Undo();
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.IsTrue(Doc.ContainsNode(name2));
            Assert.IsTrue(Doc.ContainsNode(name1));
            nss = Doc.GetNamespaces();
            nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));
            Assert.AreEqual(5, nss.Count);
            Assert.IsTrue(Doc.IsActive(nsA));
            Assert.IsNotNull(nss[1].ns);
            Assert.AreEqual(nsA.Guid, nss[1].ns.Guid);
            Assert.AreEqual(MakeName("A"), nss[1].name);
            Assert.IsNull(nss[2].ns);
            Assert.AreEqual(MakeName("A.B"), nss[2].name);
            Assert.IsNull(nss[3].ns);
            Assert.AreEqual(MakeName("A.C"), nss[3].name);
            Assert.IsNotNull(nss[4].ns);
            Assert.IsTrue(Doc.IsActive(nsACD));
            Assert.AreEqual(nsACD.Guid, nss[4].ns.Guid);
            Assert.AreEqual(MakeName("A.C.D"), nss[4].name);
            nsT = Doc.GetNamespace(MakeName("A"));
            Assert.AreEqual(nsA.Guid, nsT.Guid);
            nsT = Doc.GetNamespace(MakeName("A.C.D"));
            Assert.AreEqual(nsACD.Guid, nsT.Guid);

            // Doesn't fix up formulas.
            Sink.WriteLine("*** Rename ns without fixing up formulas, hence the errors.");
            Set(Doc.RenameNamespace(MakeName("A"), MakeName("N.P")));
            Assert.AreEqual(2, Doc.NodeCount);
            Assert.IsTrue(Doc.ContainsNode(MakeName("N.P.B.X")));
            Assert.IsTrue(Doc.ContainsNode(MakeName("N.P.X")));
            nodeT = Doc.GetNode(MakeName("N.P.B.X"));
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node2.Guid, nodeT.Guid);
            Assert.AreEqual(fma2, nodeT.Formula);
            nodeT = Doc.GetNode(MakeName("N.P.X"));
            Assert.IsTrue(Doc.IsActive(nodeT));
            Assert.AreEqual(node1.Guid, nodeT.Guid);
            Assert.AreEqual(fma1, nodeT.Formula);
            nss = Doc.GetNamespaces();
            nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));
            Assert.AreEqual(6, nss.Count);
            Assert.IsNull(nss[1].ns);
            Assert.AreEqual(MakeName("N"), nss[1].name);
            Assert.IsTrue(!Doc.IsActive(nsA));
            Assert.IsNotNull(nss[2].ns);
            Assert.AreEqual(nsA.Guid, nss[2].ns.Guid);
            Assert.AreEqual(MakeName("N.P"), nss[2].name);
            Assert.IsNull(nss[3].ns);
            Assert.AreEqual(MakeName("N.P.B"), nss[3].name);
            Assert.IsNull(nss[4].ns);
            Assert.AreEqual(MakeName("N.P.C"), nss[4].name);
            Assert.IsTrue(!Doc.IsActive(nsACD));
            Assert.IsNotNull(nss[5].ns);
            Assert.AreEqual(nsACD.Guid, nss[5].ns.Guid);
            Assert.AreEqual(MakeName("N.P.C.D"), nss[5].name);
            nsT = Doc.GetNamespace(MakeName("N.P"));
            Assert.AreEqual(nsA.Guid, nsT.Guid);
            nsT = Doc.GetNamespace(MakeName("N.P.C.D"));
            Assert.AreEqual(nsACD.Guid, nsT.Guid);
        }
    }

    [TestMethod]
    public void NameGuesses()
    {
        int count = DoBaselineTests(
            Run, @"Doc\NameGuesses.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var nss = Doc.GetNamespaces();
            Assert.AreEqual(1, nss.Count);
            Assert.IsNull(nss[0].ns);
            Assert.AreEqual(NPath.Root, nss[0].name);

            Sink.WriteLine("*** Create node 'N.X'");
            Set(Doc.CreateFlowNode(MakeName("N.X"), CreateFormula("[1, 3, 8, -2, 0]->{ A: it, B: it * it }"), GuidNext()));

            Sink.WriteLine("*** Create node 'Test'");
            var guid = GuidNext();
            Set(Doc.CreateFlowNode(MakeName("Test"), CreateFormula("N.X"), guid));

            string fma = "N.x";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            fma = "n.X";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            fma = "M.X";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            fma = "sum(N.X, b)";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            fma = "Sum(Item: N.X, item.B)";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            Sink.WriteLine("*** Create node 'n.Y'");
            Set(Doc.CreateFlowNode(MakeName("n.Y"), CreateFormula("true"), GuidNext()));

            Sink.WriteLine("*** Cross-namespace fuzzy match");
            fma = "n.X";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            fma = "N.Y";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));

            Sink.WriteLine("*** Create node 'N.'X 2''");
            Set(Doc.CreateFlowNode(MakeName("N.'X 2'"), CreateFormula("3"), GuidNext()));

            Sink.WriteLine("*** Cross-namespace fuzzy match with escaping");
            fma = "n.'X 2'";
            Sink.WriteLine("*** Fma: {0}", fma);
            Set(Doc.SetFormula(guid, fma));
            Sink.WriteLine(BndNodePrinter.Run(_graph.GetNode(guid).MainBoundFormula.BoundTree, BndNodePrinter.Verbosity.Terse));
        }
    }

    [TestMethod]
    public void ReplaceNsConfig()
    {
        int count = DoBaselineTests(
            Run, @"Doc\ReplaceNsConfig.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var name = MakeName("A.B");
            Set(Doc.CreateExplicitNamespace(out var ns, name, null));
            Assert.IsNull(ns.Config);

            Sink.WriteLine("*** Set namespace config");
            var config = new NsConfig("val1");
            Set(Doc.SetNamespaceConfig(out ns, ns.Guid, config));
            Assert.AreEqual(config.Contents, (ns.Config as NsConfig).Contents);

            Sink.WriteLine("*** Undo");
            Undo(1);
            ns = Doc.GetNamespace(name);
            Assert.IsNull(ns.Config);

            Sink.WriteLine("*** Redo");
            Redo(1);
            ns = Doc.GetNamespace(name);
            Assert.AreEqual(config.Contents, (ns.Config as NsConfig).Contents);

            Sink.WriteLine("*** Delete and undo");
            Set(Doc.DeleteNamespace(name));
            Undo(1);
            ns = Doc.GetNamespace(name);
            Assert.AreEqual(config.Contents, (ns.Config as NsConfig).Contents);
        }
    }

    [TestMethod]
    public void FuncChanges()
    {
        int count = DoBaselineTests(
            Run, @"Doc\FuncChanges.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var doc = Doc;
            var guid1 = GuidNext();
            NPath name1 = MakeName("A");
            doc = doc.CreateFlowNode(name1, CreateFormula("\"Hello\""), guid1);
            var guid2 = GuidNext();
            NPath name2 = MakeName("B");
            doc = doc.CreateFlowNode(name2, CreateFormula("Text.Len(A, 17)"), guid2);
            var guid3 = GuidNext();
            NPath name3 = MakeName("C");
            doc = doc.CreateFlowNode(name3, CreateFormula("A->Len()"), guid3);
            var guid4 = GuidNext();
            NPath name4 = MakeName("D");
            doc = doc.CreateFlowNode(name4, CreateFormula("A->Len(32)"), guid4);
            var guid5 = GuidNext();
            NPath name5 = MakeName("E");
            doc = doc.CreateFlowNode(name5, CreateFormula("A->text.lEN(32)"), guid5);
            var guid6 = GuidNext();
            NPath name6 = MakeName("F");
            doc = doc.CreateFlowNode(name6, CreateFormula("A->teXT.leN(32)"), guid6);
            var guid7 = GuidNext();
            NPath name7 = MakeName("G");
            doc = doc.CreateFlowNode(name7, CreateFormula("otHer(32)"), guid7);
            var guid8 = GuidNext();
            NPath name8 = MakeName("H");
            doc = doc.CreateFlowNode(name8, CreateFormula("OtHer(32, 17)"), guid8);
            var guid9 = MakeNodeData(ref doc, "I", DType.Text.ToSequence());
            doc = doc.InsertExtraFormula(guid9, 0, CreateFormula("this->Text.Len(3)"));
            Set(doc);

            var fma = RexlFormula.Create(SourceContext.Create("Text.Len(x) + y"));
            var udf = UserFunc.Create(
                MakeName("Text.Len"), NPath.Root,
                Immutable.Array<DName>.Create(new DName("x"), new DName("y")),
                fma, false);

            var udf2 = UserFunc.Create(
                MakeName("text.lEN"), NPath.Root,
                Immutable.Array<DName>.Create(new DName("x"), new DName("y")),
                fma, false);

            var udf3 = UserFunc.Create(
                MakeName("Other"), NPath.Root,
                Immutable.Array<DName>.Create(new DName("x"), new DName("y")),
                RexlFormula.Create(SourceContext.Create("x * y")), false);

            // Add a udf.
            Set(Doc.SetUdf(udf));
            Undo();
            Redo();

            // Remove the udf.
            Set(Doc.SetUdf(null));
            Undo();
            Redo();

            // Add the udf again.
            Set(Doc.SetUdf(udf));
            Undo();
            Redo();

            // Go back two, which shouldn't show function changes.
            Undo(2);
            Undo();
            Redo(2);
            Redo();

            Set(Doc.SetUdf(udf2));
            Undo();
            Redo();

            Set(Doc.SetUdf(null));
            Undo();
            Redo();

            Sink.WriteLine("*** Other udf");
            Set(Doc.SetUdf(udf3));
            Undo();
            Redo();
        }
    }

    /// <summary>
    /// For testing nodes that have a data config.
    /// </summary>
    private sealed class DataConfig : TestDoc.NodeConfig
    {
        public readonly DType Type;

        public DataConfig(DType type)
            : base()
        {
            Validation.Assert(type.IsValid);
            Type = type;
        }
    }

    private sealed class NsConfig : TestDoc.NamespaceConfig
    {
        public readonly string Contents;

        public NsConfig(string contents)
        {
            Contents = contents;
        }
    }

    private sealed class GraphHostImpl : TestDoc.GraphHost
    {
        private readonly DocumentTests _parent;
        private Dictionary<string, OperInfo> _lowerPathToFin;

        private OperInfo _infoUdf;
        private string _lowerUdf;

        public GraphHostImpl(DocumentTests parent)
        {
            _parent = parent;
        }

        public override BindOptions GetBindOptions() => default;

        /// <summary>
        /// Sets the udf to the given one (may be null) and returns true if it is different than the
        /// previous one.
        /// </summary>
        public bool SetUdf(UserFunc udf)
        {
            if (udf == _infoUdf?.Oper)
                return false;

            if (udf is null)
            {
                _infoUdf = null;
                _lowerUdf = null;
            }
            else
            {
                _infoUdf = new OperInfo(udf.Path, udf);
                _lowerUdf = udf.Path.ToDottedSyntax().ToLowerInvariant();
            }
            return true;
        }

        public override FmaTuple GetExtraFormulas(TestNode node, DType typeBase, TestDoc.GraphNode gnPrev)
        {
            Validation.AssertValue(node);
            Validation.Assert(typeBase.IsValid);
            Validation.AssertValueOrNull(gnPrev);
            return node.ExtraFormulas;
        }

        public override bool TryGetBaseType(TestNode node, out DType type)
        {
            Validation.AssertValue(node);
            Validation.Assert(node.Formula == null);

            if (node.Config is DataConfig dc)
            {
                type = dc.Type;
                return true;
            }

            if (node.IsGridNode(out var gc))
            {
                type = gc.TableType;
                return true;
            }

            // Shouldn't get here.
            Validation.Assert(false);
            type = default;
            return false;
        }

        public override bool TryGetGlobalFuzzyMatch(NPath ns, DName name, out DName nameGuess)
        {
            int count = ns.NameCount + 1;
            foreach (var node in _parent.Doc.GetFlowNodes())
            {
                var path = node.Name;
                if (path.NameCount == count && IsFuzzyMatch(path.Leaf, name) && path.Parent == ns)
                {
                    nameGuess = path.Leaf;
                    return true;
                }
            }
            return base.TryGetGlobalFuzzyMatch(ns, name, out nameGuess);
        }

        public override bool TryGetNamespaceFuzzyMatch(NPath ns, DName name, out DName nameGuess)
        {
            int count = ns.NameCount + 1;
            foreach (var (path, _) in _parent.Doc.GetNamespaces())
            {
                if (path.NameCount == count && IsFuzzyMatch(path.Leaf, name) && path.Parent == ns)
                {
                    nameGuess = path.Leaf;
                    return true;
                }
            }
            return base.TryGetNamespaceFuzzyMatch(ns, name, out nameGuess);
        }

        public override bool TryGetOperInfo(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            if (!fuzzy)
            {
                if (!user)
                {
                    info = TestFunctions.Instance.GetInfo(name);
                    Validation.Assert(info is null || info.Oper is not null);
                    return info != null;
                }

                if (_infoUdf != null && _infoUdf.Path == name)
                {
                    info = _infoUdf;
                    return true;
                }

                info = null;
                return false;
            }
            else
            {
                var key = name.ToDottedSyntax().ToLowerInvariant();
                if (!user)
                {
                    if (_lowerPathToFin == null)
                    {
                        _lowerPathToFin = new Dictionary<string, OperInfo>();
                        foreach (var item in TestFunctions.Instance.GetInfos(includeHidden: true, includeDeprecated: true))
                            _lowerPathToFin[item.Path.ToDottedSyntax().ToLowerInvariant()] = item;
                    }
                    return _lowerPathToFin.TryGetValue(key, out info);
                }

                if (key == _lowerUdf)
                {
                    info = _infoUdf;
                    return true;
                }

                info = null;
                return false;
            }
        }
    }
}
