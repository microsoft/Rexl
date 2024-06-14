// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Flow;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using CodeGenerator = CachingEnumerableCodeGenerator;
using FieldMap = Dictionary<DName, DName>;
using FmaTuple = Immutable.Array<(RexlFormula fma, int grp)>;
using TestTupType = TupleImpl<int, bool?, string>;
using TOpts = System.Boolean;

partial class TestDoc
{
    /// <summary>
    /// Create an empty grid node with the given type. Throws if the type is unsupported.
    /// </summary>
    public TestDoc CreateGridNode(TypeManager tm, Guid guid, NPath name, DType type)
    {
        return AddNode(new TestNode(guid, name, null, CreateGridConfig(tm, type), default));
    }

    /// <summary>
    /// Create an empty grid node with the given type. Throws if the type is unsupported.
    /// </summary>
    public TestDoc CreateGridNode(TypeManager tm, Guid guid, NPath name, DType type, FmaTuple extra)
    {
        return AddNode(new TestNode(guid, name, null, CreateGridConfig(tm, type), extra));
    }

    /// <summary>
    /// Create a grid node with the config coming from the given stream.
    /// </summary>
    public TestDoc CreateGridNode(TypeManager tm, Guid guid, NPath name, Stream strm)
    {
        return AddNode(new TestNode(guid, name, null, LoadGridConfig(tm, strm, Guid.NewGuid()), default));
    }
}

partial class DocumentTests
{
    private static readonly DName _name10 = new DName("F10");
    private static readonly DName _name20 = new DName("F20");
    private static readonly DName _name30 = new DName("F30");
    private static readonly DName _name40 = new DName("F40");
    private static readonly DName _name50 = new DName("F50");
    private static readonly DName _name60 = new DName("F60");

    // The code gen.
    private EnumerableCodeGeneratorBase _codeGen;

    // The value writer.
    private TestValueWriter _valueWriter;

    private EnumerableCodeGeneratorBase GetCodeGen()
    {
        return _codeGen ??= new CodeGenerator(new TestEnumTypeManager(), TestGenerators.Instance);
    }

    private TypeManager GetTypeManager()
    {
        return GetCodeGen().TypeManager;
    }

    private TestValueWriter GetValueWriter()
    {
        return _valueWriter ??= new TestValueWriter(new ValueWriterConfig(), Sink, GetTypeManager());
    }

    private void RefreshGridRecords(Guid guid, TestDoc.GridConfig grid)
    {
        Validation.AssertValue(grid);
        int crow = grid.RowCount;

        int[] stale = grid.GetStaleRowIndices().ToArray();
        Assert.IsTrue(stale.Length <= crow);

        if (crow <= 0)
            return;

        if (stale.Length > 0)
        {
            string pre = "; stale rows: ";
            for (int iirowMin = 0; iirowMin < stale.Length;)
            {
                int iirowLim = iirowMin + 1;
                while (iirowLim < stale.Length && stale[iirowLim] == stale[iirowLim - 1] + 1)
                    iirowLim++;
                int irowMin = stale[iirowMin];
                int irowLim = irowMin + iirowLim - iirowMin;
                Assert.AreEqual(irowLim - 1, stale[iirowLim - 1]);
                if (irowLim == irowMin + 1)
                    Sink.Write("{0}{1}", pre, irowMin);
                else
                    Sink.Write("{0}{1}-{2}", pre, irowMin, irowLim);
                pre = ", ";
                iirowMin = iirowLim;
            }
        }

        // Get them over several calls.
        var recsInc = grid.AllocRecordArray(crow);
        var recsDec = grid.AllocRecordArray(crow);
        int crowBatch = 2;
        for (int irowMin = 0; irowMin < crow;)
        {
            int irowLim = irowMin + crowBatch++;
            if (irowLim > crow)
                irowLim = crow;

            // Fill recsDec from the end.
            int irowDec = crow - irowLim;

            grid.GetRecords(recsInc, irowMin, irowMin, irowLim);
            grid.GetRecords(recsDec, irowDec, irowMin, irowLim);

            // Make sure GetRecords didn't write outside what was expected.
            for (int irow = 0; irow < crow; irow++)
            {
                Assert.AreEqual(irow < irowLim, recsInc[irow] != null);
                Assert.AreEqual(irow < irowDec, recsDec[irow] == null);
            }

            // Make sure the fetched records match.
            for (int irow = irowMin; irow < irowLim; irow++)
                Assert.IsTrue(recsInc[irow] == recsDec[irow + irowDec - irowMin]);

            irowMin = irowLim;
        }

        // Get them all at once.
        var recsAll = grid.GetRecords(0, crow);
        Assert.AreEqual(crow, recsAll.Length);

        // Make sure they are the same.
        for (int irow = 0; irow < crow; irow++)
            Assert.IsTrue(recsAll[irow] == recsInc[irow]);
    }

    private void WriteRecords(Guid guid)
    {
        var grid = Doc.GetGridConfig(guid);
        var recs = grid.GetRecords(0, grid.RowCount);
        GetValueWriter().WriteValue(grid.TableType, recs);
        Sink.WriteLine("###");
    }

    /// <summary>
    /// Write the textual representation of the first grid's records. If the
    /// representation of the second grid's records is different also write them
    /// with an error message.
    /// </summary>
    private void WriteSameRecords(Guid guid1, Guid guid2)
    {
        var grid1 = Doc.GetGridConfig(guid1);
        var grid2 = Doc.GetGridConfig(guid2);
        var recs1 = grid1.GetRecords(0, grid1.RowCount);
        var recs2 = grid2.GetRecords(0, grid2.RowCount);
        int ichMin = Sink.Builder.Length;
        var vw = GetValueWriter();
        vw.WriteValue(grid1.TableType, recs1);

        var sb2 = new StringBuilder();
        Sink.SetOut(sb2, out var prev);
        vw.WriteValue(grid2.TableType, recs2);
        Sink.SetOut(prev, out _);
        if (SameText(Sink.Builder, ichMin, sb2, 0))
            Sink.WriteLine("   === Match ===");
        else
        {
            Sink.WriteLine("   *** Mismatch! ***");
            Sink.Write(sb2.ToString());
        }

        Sink.WriteLine("###");
    }

    private bool SameText(StringBuilder sb1, int min1, StringBuilder sb2, int min2)
    {
        int len = sb1.Length - min1;
        if (len != sb2.Length - min2)
            return false;
        for (int ich = 0; ich < len; ich++)
        {
            if (sb1[ich + min1] != sb2[ich + min2])
                return false;
        }
        return true;
    }

    private void WriteValue(DType type, object value)
    {
        GetValueWriter().WriteValue(type, value);
        Sink.WriteLine("###");
    }

    // REVIEW: Need tests that include extra formulas or other nodes referencing a grid.

    private DType GetBasicRecordType(bool withUri)
    {
        var res = DType.CreateRecord(opt: false,
            new TypedName(_name30, DType.I4Req),
            new TypedName(_name40, DType.R8Opt),
            new TypedName(_name50, DType.Text));
        if (withUri)
            res = res.AddNameType(_name60, DType.CreateUriType(XrayFlavor));
        return res;
    }

    private DType GetBasicRecordType2(bool withOpt)
    {
        return DType.CreateRecord(opt: false,
            new TypedName(_name10, DType.Text),
            new TypedName(_name20, DType.U4Req),
            new TypedName(_name40, withOpt ? DType.I4Opt : DType.I4Req),
            new TypedName(_name50, DType.Text));
    }

    private Guid AddBasicTestGrid(bool withUri)
    {
        var typeRec = GetBasicRecordType(withUri);
        var guid = AddTestGrid("A", typeRec);

        var grid = Doc.GetGridConfig(guid);
        Assert.AreEqual(0, grid.GetColIndex(_name30));
        Assert.AreEqual(1, grid.GetColIndex(_name40));
        Assert.AreEqual(2, grid.GetColIndex(_name50));
        if (withUri)
            Assert.AreEqual(3, grid.GetColIndex(_name60));
        Assert.AreEqual(_name30, grid.GetColName(0));
        Assert.AreEqual(_name40, grid.GetColName(1));
        Assert.AreEqual(_name50, grid.GetColName(2));
        if (withUri)
            Assert.AreEqual(_name60, grid.GetColName(3));
        Assert.AreEqual(DType.I4Req, grid.GetColType(0));
        Assert.AreEqual(DType.R8Opt, grid.GetColType(1));
        Assert.AreEqual(DType.Text, grid.GetColType(2));
        if (withUri)
            Assert.AreEqual(DType.CreateUriType(XrayFlavor), grid.GetColType(3));
        Assert.AreEqual(typeof(int), grid.GetColSysType(0));
        Assert.AreEqual(typeof(double?), grid.GetColSysType(1));
        Assert.AreEqual(typeof(string), grid.GetColSysType(2));
        if (withUri)
            Assert.AreEqual(typeof(Link), grid.GetColSysType(3));

        return guid;
    }

    private (RecordBase[], DType) GenerateBasicRecords(bool withUri = false)
    {
        var typeRec = GetBasicRecordType(withUri);

        var fma = CreateFormula(
            withUri ?
                "Range(10)->{ F30: (it + 1)->CastI4(), F40: it + 0.5 if it mod 5 != 2 else null, F50: \"*** \" & ToText(it), F60: Link.Local(\"Image.Jpeg.Xray\", \"Image.Jpeg.Xray/xray\" & ToText(it + 1)), }" :
                "Range(10)->{ F30: (it + 1)->CastI4(), F40: it + 0.5 if it mod 5 != 2 else null, F50: \"*** \" & ToText(it), }");
        Assert.IsFalse(fma.HasDiagnostics);
        var bfma = BoundFormula.Create(fma, new BindHostImpl());
        Assert.IsFalse(bfma.HasDiagnostics);
        Assert.AreEqual(typeRec.ToSequence(), bfma.BoundTree.Type);

        var resCodeGen = GetCodeGen().Run(bfma.BoundTree);
        Assert.AreEqual(0, resCodeGen.Globals.Length);
        var seq = resCodeGen.Func(Array.Empty<object>());

        var arr = ToArray(seq, typeRec);
        return (arr, typeRec);
    }

    private (RecordBase[], DType) GenerateBasicRecords2(bool withOpt = false)
    {
        var typeRec = GetBasicRecordType2(withOpt);

        var fma = CreateFormula(
            withOpt ?
                "Range(10)->{ F10: \"V\" & ToText(it), F20: (it->CastU4() + 51U)->CastU4(), F40: (it + 3)->CastI4() if it mod 3 != 0 else null, F50: \"%%% \" & ToText(it)}" :
                "Range(10)->{ F10: \"V\" & ToText(it), F20: (it->CastU4() + 51U)->CastU4(), F40: (it + 3)->CastI4(), F50: \"%%% \" & ToText(it)}");
        Assert.IsFalse(fma.HasDiagnostics);
        var bfma = BoundFormula.Create(fma, new BindHostImpl());
        Assert.IsFalse(bfma.HasDiagnostics);
        Assert.AreEqual(typeRec.ToSequence(), bfma.BoundTree.Type);

        var resCodeGen = GetCodeGen().Run(bfma.BoundTree);
        Assert.AreEqual(0, resCodeGen.Globals.Length);
        var seq = resCodeGen.Func(Array.Empty<object>());

        var arr = ToArray(seq, typeRec);
        return (arr, typeRec);
    }

    private RecordBase[] ToArray(object seq, DType typeItem)
    {
        if (seq == null)
            return null;

        Assert.IsTrue(GetTypeManager().TryEnsureSysType(typeItem, out var stItem));
        Assert.IsInstanceOfType(seq, typeof(IEnumerable<>).MakeGenericType(stItem));

        var meth = new Func<IEnumerable<object>, object[]>(Enumerable.ToArray)
            .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);

        return (RecordBase[])meth.Invoke(null, new object[] { seq });
    }

    private Guid AddTestGrid(string name, DType typeRec, Immutable.Array<(RexlFormula fma, int grp)> extra = default)
    {
        var guid = GuidNext();
        Set(Doc.CreateGridNode(GetTypeManager(), guid, MakeName(name), typeRec.ToSequence(), extra));
        return guid;
    }

    [TestMethod]
    public void GridBasic()
    {
        int count = DoBaselineTests(
            Run, @"Grid\Basic.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            for (int n = 0; n < 2; n++)
            {
                bool withUri = n > 0;

                var guid = AddBasicTestGrid(withUri);

                Undo();
                Redo();
                var node = Doc.GetGridNode(guid);
                Assert.IsTrue(Doc.IsActive(node));

                Set(Doc.InsertBlankRows(guid, 0, 15));
                WriteRecords(guid);
                Undo();
                Redo();

                Set(Doc.InsertBlankRows(guid, 3, 7));
                WriteRecords(guid);
                Undo();
                Redo();

                Set(Doc.DeleteNode(guid));
                Undo();

                int row0 = 2;
                int row1 = 5;
                int row2 = Doc.GetGridConfig(guid).RowCount - 1;
                int row3 = 7;
                int col0 = 0;
                int col1 = 1;
                int col2 = 2;
                int col3 = 3;

                Assert.IsTrue(GetTypeManager().TryEnsureDefaultValue(DType.CreateUriType(XrayFlavor), out var entry));
                Assert.IsFalse(entry.special);
                Assert.IsNull(entry.value);

                var grid = Doc.GetGridConfig(guid);
                Assert.AreEqual(0, grid.GetCellI4(col0, row0));
                Assert.AreEqual(0, grid.GetCellI4(col0, row1));
                Assert.AreEqual(0, grid.GetCellI4(col0, row2));
                Assert.AreEqual(0, grid.GetCellI4(col0, row3));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, 0));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row1));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row2));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row3));
                Assert.AreEqual(null, grid.GetCellText(col2, row0));
                Assert.AreEqual(null, grid.GetCellText(col2, row1));
                Assert.AreEqual(null, grid.GetCellText(col2, row2));
                Assert.AreEqual(null, grid.GetCellText(col2, row3));
                if (withUri)
                {
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row0));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row1));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row2));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row3));
                }

                Assert.AreEqual(0, grid.GetCellValue(col0, row0));
                Assert.AreEqual(0, grid.GetCellValue(col0, row1));
                Assert.AreEqual(0, grid.GetCellValue(col0, row2));
                Assert.AreEqual(0, grid.GetCellValue(col0, row3));
                Assert.AreEqual(null, grid.GetCellValue(col1, 0));
                Assert.AreEqual(null, grid.GetCellValue(col1, row1));
                Assert.AreEqual(null, grid.GetCellValue(col1, row2));
                Assert.AreEqual(null, grid.GetCellValue(col1, row3));
                Assert.AreEqual(null, grid.GetCellValue(col2, row0));
                Assert.AreEqual(null, grid.GetCellValue(col2, row1));
                Assert.AreEqual(null, grid.GetCellValue(col2, row2));
                Assert.AreEqual(null, grid.GetCellValue(col2, row3));
                if (withUri)
                {
                    Assert.IsNull(grid.GetCellValue(col3, row0));
                    Assert.IsNull(grid.GetCellValue(col3, row1));
                    Assert.IsNull(grid.GetCellValue(col3, row2));
                    Assert.IsNull(grid.GetCellValue(col3, row3));
                }

                WriteRecords(guid);
                int i4 = -17;
                double? r8q = 3.5;
                string s = " *** Hello ***";
                var xray = MakeLink("femur.jpg", JpegFlavor);

                var doc = Doc;
                doc = doc.SetCellValue<int>(guid, col0, row0, i4);
                doc = doc.SetCellValue<double?>(guid, col1, row1, r8q);
                doc = doc.SetCellValue<string>(guid, col2, row2, s);
                if (withUri)
                    doc = doc.SetCellValue<Link>(guid, col3, row3, xray);
                Set(doc);
                grid = Doc.GetGridConfig(guid);
                Assert.AreEqual(i4, grid.GetCellI4(col0, row0));
                Assert.AreEqual(0, grid.GetCellI4(col0, row1));
                Assert.AreEqual(0, grid.GetCellI4(col0, row2));
                Assert.AreEqual(0, grid.GetCellI4(col0, row3));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row0));
                Assert.AreEqual(r8q, grid.GetCellR8Opt(col1, row1));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row2));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row3));
                Assert.AreEqual(null, grid.GetCellText(col2, row0));
                Assert.AreEqual(null, grid.GetCellText(col2, row1));
                Assert.AreEqual(s, grid.GetCellText(col2, row2));
                Assert.AreEqual(null, grid.GetCellText(col2, row3));
                if (withUri)
                {
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row0));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row1));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row2));
                    Assert.AreEqual(xray, grid.GetCellValue<Link>(col3, row3));
                }

                Assert.AreEqual(i4, grid.GetCellValue(col0, row0));
                Assert.AreEqual(0, grid.GetCellValue(col0, row1));
                Assert.AreEqual(0, grid.GetCellValue(col0, row2));
                Assert.AreEqual(0, grid.GetCellValue(col0, row3));
                Assert.AreEqual(null, grid.GetCellValue(col1, row0));
                Assert.AreEqual(r8q, grid.GetCellValue(col1, row1));
                Assert.AreEqual(null, grid.GetCellValue(col1, row2));
                Assert.AreEqual(null, grid.GetCellValue(col1, row3));
                Assert.AreEqual(null, grid.GetCellValue(col2, row0));
                Assert.AreEqual(null, grid.GetCellValue(col2, row1));
                Assert.AreEqual(s, grid.GetCellValue(col2, row2));
                Assert.AreEqual(null, grid.GetCellValue(col2, row3));
                if (withUri)
                {
                    Assert.IsNull(grid.GetCellValue(col3, row0));
                    Assert.IsNull(grid.GetCellValue(col3, row1));
                    Assert.IsNull(grid.GetCellValue(col3, row2));
                    Assert.AreEqual(xray, grid.GetCellValue(col3, row3));
                }

                WriteRecords(guid);

                Undo();
                grid = Doc.GetGridConfig(guid);
                Assert.AreEqual(0, grid.GetCellI4(col0, row0));
                Assert.AreEqual(0, grid.GetCellI4(col0, row1));
                Assert.AreEqual(0, grid.GetCellI4(col0, row2));
                Assert.AreEqual(0, grid.GetCellI4(col0, row3));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, 0));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row1));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row2));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row3));
                Assert.AreEqual(null, grid.GetCellText(col2, row0));
                Assert.AreEqual(null, grid.GetCellText(col2, row1));
                Assert.AreEqual(null, grid.GetCellText(col2, row2));
                Assert.AreEqual(null, grid.GetCellText(col2, row3));
                if (withUri)
                {
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row0));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row1));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row2));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row3));
                }

                Redo();
                grid = Doc.GetGridConfig(guid);
                Assert.AreEqual(i4, grid.GetCellI4(col0, row0));
                Assert.AreEqual(0, grid.GetCellI4(col0, row1));
                Assert.AreEqual(0, grid.GetCellI4(col0, row2));
                Assert.AreEqual(0, grid.GetCellI4(col0, row3));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row0));
                Assert.AreEqual(r8q, grid.GetCellR8Opt(col1, row1));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row2));
                Assert.AreEqual(null, grid.GetCellR8Opt(col1, row3));
                Assert.AreEqual(null, grid.GetCellText(col2, row0));
                Assert.AreEqual(null, grid.GetCellText(col2, row1));
                Assert.AreEqual(s, grid.GetCellText(col2, row2));
                Assert.AreEqual(null, grid.GetCellText(col2, row3));
                if (withUri)
                {
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row0));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row1));
                    Assert.IsNull(grid.GetCellValue<Link>(col3, row2));
                    Assert.AreEqual(xray, grid.GetCellValue<Link>(col3, row3));
                }

                UndoAll();
                RedoAll();
                Assert.IsTrue(Doc.IsGrid(guid, out node, out grid));
                WriteRecords(guid);

                grid = node.GetGridConfig();
                doc = Doc;
                for (int i = 0; i < grid.RowCount; i++)
                {
                    // Skip one.
                    if (i == grid.RowCount / 2)
                        continue;
                    int v = i + 1;
                    if (i % 2 == 0)
                    {
                        doc = doc.SetCellValue<int>(guid, col0, i, v);
                        doc = doc.SetCellValue<double?>(guid, col1, i, v + 0.5);
                        doc = doc.SetCellValue<string>(guid, col2, i, $"*** {v}");
                        if (withUri)
                            doc = doc.SetCellValue<Link>(guid, col3, i, MakeLink($"file{v}.jpg", JpegFlavor));
                    }
                    else
                    {
                        doc = doc.SetCellValue(guid, col0, i, (object)v);
                        doc = doc.SetCellValue(guid, col1, i, (object)(v + 0.5));
                        doc = doc.SetCellValue(guid, col2, i, (object)$"*** {v}");
                        if (withUri)
                            doc = doc.SetCellValue(guid, col3, i, MakeLink($"file{v}.jpg", JpegFlavor));
                    }
                }
                Set(doc);
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);

                // Create some value arrays.
                int crow = 10;
                var xs = new int[crow];
                var ys = new double?[crow];
                var zs = new string[crow];
                var us = new Link[crow];
                for (int i = 0; i < crow; i++)
                {
                    int v = i + 1;
                    xs[i] = v;
                    ys[i] = v + 0.5;
                    zs[i] = $"*** {v}";
                    us[i] = MakeLink($"file{v}.jpg", JpegFlavor);
                }

                // Set those values at various places.
                doc = Doc;
                doc = doc.SetCellValues(guid, col0, 5, 5 + crow / 2, xs);
                doc = doc.SetCellValues(guid, col1, 3, 3 + crow, ys);
                doc = doc.SetCellValues(guid, col2, 11, 11 + crow, zs);
                if (withUri)
                    doc = doc.SetCellValues(guid, col3, 7, 7 + crow, us);
                Set(doc);
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);

                // Now use the non-static-typed version.
                doc = Doc;
                doc = doc.SetCellValues(guid, col0, 1, 1 + crow, new ReadOnly.Array(xs));
                doc = doc.SetCellValues(guid, col1, 7, 7 + crow / 2, new ReadOnly.Array(ys));
                doc = doc.SetCellValues(guid, col2, 12, 12 + crow, new ReadOnly.Array(zs));
                if (withUri)
                    doc = doc.SetCellValues(guid, col3, 9, 9 + crow, new ReadOnly.Array(us));
                Set(doc);
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);

                // Delete some rows.
                Set(Doc.DeleteRows(guid, 8, 14));
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);

                UndoAll();
                RedoAll();
                Assert.IsTrue(Doc.IsGrid(guid, out node, out grid));
                WriteRecords(guid);

                Set(Doc.ReplaceRows(guid, 0, grid.RowCount));
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);
                Undo();

                grid = Doc.GetGridConfig(guid);
                var clip = grid.CreateClip(2, grid.RowCount - 2);

                Set(Doc.InsertRows(guid, grid.RowCount / 2, clip));
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);
                Undo();

                grid = Doc.GetGridConfig(guid);
                Set(Doc.ReplaceRows(guid, grid.RowCount / 2, 3, clip));
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);
                Undo();

                grid = Doc.GetGridConfig(guid);
                Set(Doc.ReplaceRowsWithBlank(guid, grid.RowCount / 2, 3, 8));
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);
                Undo();

                // Setting wrong value type.
                bool caught;
                caught = false;
                try { Doc.SetCellValue(guid, 0, 0, "hello"); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                caught = false;
                try { Doc.SetCellValue(guid, 0, 0, (object)true); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                caught = false;
                try { Doc.SetCellValue(guid, 1, 0, (object)"hello"); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                caught = false;
                try { Doc.SetCellValue(guid, 2, 0, (object)5); }
                catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
                Assert.IsTrue(caught);

                UndoAll();
            }
        }
    }

    [TestMethod]
    public void GridActive()
    {
        int count = DoBaselineTests(
            Run, @"Grid\Active.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var nameX = new DName("X");
            var nameY = new DName("Y");
            var nameZ = new DName("Z");

            var typeRec = DType.CreateRecord(opt: false,
                new TypedName(nameX, DType.I4Req),
                new TypedName(nameY, DType.R8Opt),
                new TypedName(nameZ, DType.Text));

            var guid = GuidNext();
            var name1 = MakeName("A");
            Set(Doc.CreateGridNode(GetTypeManager(), guid, name1, typeRec.ToSequence()));
            var grid = Doc.GetGridConfig(guid);
            Assert.IsNotNull(grid);
            Assert.AreEqual(1, grid.GetColIndex(nameY));
            Assert.AreEqual(3, grid.ColCount);
            Assert.AreEqual(0, grid.RowCount);
            Assert.AreEqual(typeRec, grid.RecordType);

            var name2 = MakeName("B");
            Set(Doc.RenameGlobal(guid, name2));

            Set(Doc.InsertBlankRows(guid, 0, 15));
            grid = Doc.GetGridConfig(guid);
            Assert.AreEqual(1, grid.GetColIndex(nameY));
            Assert.AreEqual(3, grid.ColCount);
            Assert.AreEqual(15, grid.RowCount);
            Assert.AreEqual(typeRec, grid.RecordType);
        }
    }

    [TestMethod]
    public void GridBadType()
    {
        int count = DoBaselineTests(
            Run, @"Grid\BadType.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var nameX = new DName("X");
            var nameY = new DName("Y");
            var nameZ = new DName("Z");

            var typeRec = DType.CreateRecord(opt: false,
                new TypedName(nameX, DType.I4Req),
                new TypedName(nameY, DType.R8Opt),
                new TypedName(nameZ, DType.Text));

            var guid1 = GuidNext();
            var name1 = MakeName("A");
            Set(Doc.CreateGridNode(GetTypeManager(), guid1, name1, typeRec.ToSequence()));
            Undo();

            // General isn't allowed.
            typeRec = typeRec.SetNameType(nameX, DType.General);

            Sink.WriteLine("*** Trying with general column type");
            bool caught;
            caught = false;
            try { Doc.CreateGridNode(GetTypeManager(), guid1, name1, typeRec.ToSequence()); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);
            Sink.WriteLine("*** Blocked general column type");
        }
    }

    [TestMethod]
    public void GridReplaceRowsWithRecords()
    {
        int count = DoBaselineTests(
            Run, @"Grid\ReplaceRowsWithRecords.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            (var arr, var typeRec) = GenerateBasicRecords(withUri: true);
            var guid = AddTestGrid("A", typeRec);
            Assert.AreEqual(typeRec, Doc.GetGridConfig(guid).RecordType);
            WriteValue(typeRec.ToSequence(), arr);

            Set(Doc.ReplaceRowsWithRecords(guid, 0, 0, arr.Length, arr));
            WriteRecords(guid);

            // Null out some array elements and make another call.
            arr[1] = null;
            arr[3] = null;
            Set(Doc.ReplaceRowsWithRecords(guid, 3, 5, arr.Length, arr));
            WriteRecords(guid);

            Undo();
            WriteRecords(guid);

            Undo();
            WriteRecords(guid);

            Redo();
            WriteRecords(guid);

            Redo();
            WriteRecords(guid);

            Sink.WriteLine("*** Nop replace rows with records");
            Set(Doc.ReplaceRowsWithRecords(guid, 7, 0, 0, arr));
            WriteRecords(guid);

            Sink.WriteLine("*** Replace rows with records with only deletion");
            Set(Doc.ReplaceRowsWithRecords(guid, 7, 3, 0, arr));
            WriteRecords(guid);

            Undo();
            WriteRecords(guid);

            Redo();
            WriteRecords(guid);

            // Try ReplaceRowsWithRecords when the node is deleted.
            Set(Doc.DeleteNode(guid));
            bool caught;
            caught = false;
            try { Doc.ReplaceRowsWithRecords(guid, 2, 0, arr.Length, arr); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);
            Undo();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out _));
            WriteRecords(guid);

            Set(Doc.ReplaceRowsWithRecords(guid, 2, 0, arr.Length, arr));
            WriteRecords(guid);
        }
    }

    [TestMethod]
    public void GridSaveLoad()
    {
        int count = DoBaselineTests(
            Run, @"Grid\SaveLoad.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var typeImage = DType.CreateUriType(NPath.Root.Append(new DName("Image")));
            var typeRec = DType.CreateRecord(opt: false,
                new TypedName(new DName("A"), DType.I4Req),
                new TypedName(new DName("B"), DType.R8Opt),
                new TypedName(new DName("C"), DType.Text),
                new TypedName(new DName("D"), DType.IAReq),
                new TypedName(new DName("E"), DType.DateReq),
                new TypedName(new DName("F"), DType.TimeOpt),
                new TypedName(new DName("G"), DType.BitOpt),
                new TypedName(new DName("H"), typeImage),
                new TypedName(new DName("I"), typeImage.ToOpt()),
                new TypedName(new DName("J"), DType.CreateTuple(false, DType.I8Req, DType.BitOpt, DType.Text)),
                new TypedName(new DName("K"), DType.CreateTuple(true, DType.I8Req, DType.BitOpt, DType.Text)));

            var guid1 = GuidNext();
            var name1 = MakeName("A");
            Set(Doc.CreateGridNode(GetTypeManager(), guid1, name1, typeRec.ToSequence()));

            var fma = CreateFormula(
                "ForEach(n: Range(20), { " +
                "A: n + 1 | CastI4(_), " +
                "B: n + 0.5 if n mod 5 != 2 else null, " +
                "C: \"*** \" & ToText(n mod 4) if n mod 6 != 4 else null, " +
                "D: n + 1ia if n mod 2 = 0 else -n, " +
                "E: Date(2020, 10, n), " +
                "F: With(t: Time(n, n + 10, n + 20, n + 30) if n mod 7 != 1 else null, t if n mod 2 = 0 else Time(0) - t), " +
                "G: false if n mod 3 = 0 else true if n mod 3 = 1 else null, " +
                "H: With(name: \"file\" & ToText(n mod 6), Link.BlobImage(\"acct\", name if n mod 3 != 2 else null)), " +
                "I: With(name: \"file\" & ToText(n mod 6), Link.LocalImage(name if n mod 3 != 2 else null)), " +
                "J: (n, n mod 2 = 0 if n mod 5 != 0 else null, \"Test\" & ToText(n mod 7)), " +
                "K: (-n, n mod 2 = 0 if n mod 5 != 0 else null, \"Test\" & ToText(n mod 6)) if n mod 7 != 2 else null, " +
                "})");
            Assert.IsFalse(fma.HasDiagnostics);
            var bfma = BoundFormula.Create(fma, new BindHostImpl());
            Assert.IsFalse(bfma.HasDiagnostics);
            Assert.AreEqual(typeRec.ToSequence(), bfma.BoundTree.Type);

            var resCodeGen = GetCodeGen().Run(bfma.BoundTree);
            Assert.AreEqual(0, resCodeGen.Globals.Length);
            var seq = resCodeGen.Func(Array.Empty<object>());
            WriteValue(bfma.BoundTree.Type, seq);

            var arr = ToArray((IEnumerable<RecordBase>)seq, typeRec);
            Set(Doc.ReplaceRowsWithRecords(guid1, 0, 0, arr.Length, arr));
            WriteRecords(guid1);

            using var strm = new MemoryStream();
            // Clone grid by saving the config and creating a new grid from it.
            Doc.GetGridConfig(guid1).Save(strm);

            // Verify that Save left the position at the end.
            Assert.AreEqual(strm.Length, strm.Position);

            // Dump the bytes.
            Sink.WriteLine("*** Saved as:");
            var len = strm.Length;
            var bytes = strm.GetBuffer();
            for (long pos = 0; pos < len; pos += 32)
            {
                long lim = Math.Min(len, pos + 32);
                for (long p = pos; p < lim; p++)
                    Sink.Write(" {0:X02}", bytes[p]);
                for (long p = lim; p < pos + 32; p++)
                    Sink.Write("   ");
                Sink.Write("  ");
                for (long p = pos; p < lim; p++)
                {
                    var b = bytes[p];
                    Sink.Write(" {0}", 0x20 <= b && b < 0x7F ? (char)b : '.');
                }
                Sink.WriteLine();
            }
            Sink.WriteLine("Total: {0} bytes", len);

            // Create the new grid from the stream.
            strm.Position = 0;
            var guid2 = GuidNext();
            Set(Doc.CreateGridNode(GetTypeManager(), guid2, MakeName("B"), strm));
            // Verify that Load left the position at the end.
            Assert.AreEqual(strm.Length, strm.Position);
            WriteRecords(guid2);
            Assert.AreEqual(_graph.GetNode(guid1).Type, _graph.GetNode(guid2).Type);
            Assert.AreEqual(Doc.GetGridConfig(guid1).ColCount, Doc.GetGridConfig(guid2).ColCount);
            Assert.AreEqual(Doc.GetGridConfig(guid1).RowCount, Doc.GetGridConfig(guid2).RowCount);

            // Change the version, but leave the back version. Should still work.
            strm.Position = sizeof(uint);
            var ver = strm.ReadByte() | (strm.ReadByte() << 8);
            strm.Position = sizeof(uint);
            strm.WriteByte(0xFF);
            strm.WriteByte(0xFF);
            strm.Position = 0;
            guid2 = GuidNext();
            Set(Doc.CreateGridNode(GetTypeManager(), guid2, MakeName("C"), strm));

            // Verify that Load left the position at the end.
            Assert.AreEqual(strm.Length, strm.Position);
            WriteRecords(guid2);
            Assert.AreEqual(_graph.GetNode(guid1).Type, _graph.GetNode(guid2).Type);
            Assert.AreEqual(Doc.GetGridConfig(guid1).ColCount, Doc.GetGridConfig(guid2).ColCount);
            Assert.AreEqual(Doc.GetGridConfig(guid1).RowCount, Doc.GetGridConfig(guid2).RowCount);

            // Change the final byte. Should throw.
            strm.Position = strm.Length - 1;
            var cur = strm.ReadByte();
            strm.Position = strm.Length - 1;
            strm.WriteByte((byte)(cur + 1));
            strm.Position = 0;

            bool caught;
            caught = false;
            try { Doc.CreateGridNode(GetTypeManager(), GuidNext(), MakeName("D"), strm); }
            catch (InvalidDataException) { caught = true; }
            Assert.IsTrue(caught);

            // Change the final byte back to the original. Should work.
            strm.Position = strm.Length - 1;
            strm.WriteByte((byte)(cur));
            strm.Position = 0;
            guid2 = GuidNext();
            Set(Doc.CreateGridNode(GetTypeManager(), guid2, MakeName("E"), strm));
            // Verify that Load left the position at the end.
            Assert.AreEqual(strm.Length, strm.Position);
            WriteRecords(guid2);
            Assert.AreEqual(_graph.GetNode(guid1).Type, _graph.GetNode(guid2).Type);
            Assert.AreEqual(Doc.GetGridConfig(guid1).ColCount, Doc.GetGridConfig(guid2).ColCount);
            Assert.AreEqual(Doc.GetGridConfig(guid1).RowCount, Doc.GetGridConfig(guid2).RowCount);

            // Change the back version. Should throw.
            strm.Position = sizeof(uint) + sizeof(ushort);
            strm.WriteByte((byte)(ver + 1));
            strm.WriteByte((byte)((ver + 1) >> 8));
            strm.Position = 0;
            caught = false;
            try { Doc.CreateGridNode(GetTypeManager(), GuidNext(), MakeName("F"), strm); }
            catch (InvalidDataException) { caught = true; }
            Assert.IsTrue(caught);
        }
    }

    [TestMethod]
    public void GridEditColumns()
    {
        int count = DoBaselineTests(
            Run, @"Grid\EditColumns.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            (var arr, var typeRec) = GenerateBasicRecords();
            var guid = AddTestGrid("A", typeRec);
            Assert.AreEqual(typeRec, Doc.GetGridConfig(guid).RecordType);

            Set(Doc.ReplaceRowsWithRecords(guid, 0, 0, arr.Length, arr));
            WriteRecords(guid);

            Set(Doc.InsertBlankColumn(guid, new DName("C"), DType.TimeReq, 0));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Set(Doc.SetCellValue(guid, 0, 3, new TimeSpan(1, 2, 3, 4, 5) + new TimeSpan(6)));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Set(Doc.DeleteColumn(guid, 0));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            Sink.WriteLine("*** Delete a non-nullable column");
            Set(Doc.DeleteColumn(guid, 3));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            Sink.WriteLine("*** Delete a nullable column");
            Set(Doc.DeleteColumn(guid, 2));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            Sink.WriteLine("*** Insert a blank nullable column");
            Set(Doc.InsertBlankColumn(guid, new DName("D"), DType.TimeOpt, 1));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            var t1 = new TimeSpan(7, 2, 3, 4, 5) + new TimeSpan(6);
            var t2 = new TimeSpan(9, 2, 3, 4, 5) + new TimeSpan(6);
            Set(Doc.SetCellValues(guid, 1, 7, 10, new TimeSpan?[] { t1, null, t2 }));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            var grid = Doc.GetGridConfig(guid);
            Assert.AreEqual(t1, grid.GetCellTimeOpt(1, 7));
            Assert.IsNull(grid.GetCellTimeOpt(1, 8));
            Assert.AreEqual(t2, grid.GetCellTimeOpt(1, 9));

            Assert.AreEqual(t1, grid.GetCellValue<TimeSpan?>(1, 7));
            Assert.IsNull(grid.GetCellValue<TimeSpan?>(1, 8));
            Assert.AreEqual(t2, grid.GetCellValue<TimeSpan?>(1, 9));

            Sink.WriteLine("*** Insert a blank uri column");
            int col = grid.ColCount;
            Set(Doc.InsertBlankColumn(guid, new DName("G"), DType.CreateUriType(ImageFlavor), col));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Set(Doc.DeleteColumn(guid, col));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
        }
    }

    [TestMethod]
    public void GridEditColumnsExtraFormulas()
    {
        int count = DoBaselineTests(
            Run, @"Grid\EditColumnsExtraFormulas.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            (var arr, var typeRec) = GenerateBasicRecords();
            var guid = AddTestGrid(
                "A",
                typeRec,
                Immutable.Array.Create(
                    (CreateFormula("this->Filter(F40 > 1.0)"), 0),
                    (CreateFormula("this->{F60: F30 * 2}"), 0)));
            Assert.AreEqual(typeRec, Doc.GetGridConfig(guid).RecordType);

            Set(Doc.ReplaceRowsWithRecords(guid, 0, 0, arr.Length, arr));

            Set(Doc.DeleteColumn(guid, 1));
            Undo();
            Redo();
            Undo();

            Set(Doc.DeleteColumn(guid, 2));
            Undo();
            Redo();

            Set(Doc.ConvertColumn(guid, 0, DType.I8Opt));
            Undo();
            Redo();

            Set(Doc.DeleteColumn(guid, 0));
            Undo();
            Redo();
        }
    }

    [TestMethod]
    public void GridConvertColumns()
    {
        int count = DoBaselineTests(
            Run, @"Grid\ConvertColumns.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            for (int n = 0; n < 2; n++)
            {
                bool withUri = n > 0;

                (var arr, var typeRec) = GenerateBasicRecords(withUri);
                var guid = AddTestGrid("A", typeRec);
                Assert.AreEqual(typeRec, Doc.GetGridConfig(guid).RecordType);

                Set(Doc.ReplaceRowsWithRecords(guid, 0, 0, arr.Length, arr));
                WriteRecords(guid);

                Set(Doc.ConvertColumn(guid, 0, DType.I4Opt));
                WriteRecords(guid);
                Undo();
                WriteRecords(guid);
                Redo();
                WriteRecords(guid);
                Set(Doc.SetCellValue<int?>(guid, 0, 3, null));
                WriteRecords(guid);
                Undo(2);
                WriteRecords(guid);

                if (withUri)
                {
                    Set(Doc.ConvertColumn(guid, 3, DType.UriGen));
                    WriteRecords(guid);
                    Undo();
                    WriteRecords(guid);
                    Redo();
                    WriteRecords(guid);
                    Set(Doc.SetCellValue<Link>(guid, 3, 3, null));
                    Set(Doc.SetCellValue<Link>(guid, 3, 5, MakeLink("general.txt", GenFlavor)));
                    WriteRecords(guid);
                    Undo(2);
                    WriteRecords(guid);
                }
                else
                {
                    Set(Doc.ConvertColumn(guid, 0, DType.R8Opt));
                    WriteRecords(guid);
                    Undo();
                    WriteRecords(guid);
                    Redo();
                    WriteRecords(guid);
                    Set(Doc.SetCellValue<double?>(guid, 0, 3, null));
                    Set(Doc.SetCellValue<double?>(guid, 0, 5, 3.5));
                    WriteRecords(guid);
                    Undo(2);
                    WriteRecords(guid);
                }

                UndoAll();
            }
        }
    }

    [TestMethod]
    public void GridPromote()
    {
        int count = DoBaselineTests(
            Run, @"Grid\Promote.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            (var arr, var typeRec) = GenerateBasicRecords(withUri: true);
            var guid = AddTestGrid("A", typeRec);
            Assert.AreEqual(typeRec, Doc.GetGridConfig(guid).RecordType);

            Set(Doc.ReplaceRowsWithRecords(guid, 0, 0, arr.Length, arr));
            WriteRecords(guid);

            Sink.WriteLine("*** Promote col to opt");
            var typeNew = typeRec
                .SetNameType(_name30, DType.I4Opt);
            Set(Doc.ConvertType(guid, typeNew));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            Sink.WriteLine("*** Promote col to bigger");
            typeNew = typeRec
                .SetNameType(_name30, DType.I8Req);
            Set(Doc.ConvertType(guid, typeNew));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            Sink.WriteLine("*** Promote col to bigger opt");
            typeNew = typeRec
                .SetNameType(_name30, DType.I8Opt);
            Set(Doc.ConvertType(guid, typeNew));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            Sink.WriteLine("*** Promote existing and add new col");
            typeNew = typeRec
                .SetNameType(_name20, DType.TimeOpt)
                .SetNameType(_name30, DType.I8Req)
                .SetNameType(_name60, DType.CreateUriType(ImageFlavor));
            Set(Doc.ConvertType(guid, typeNew));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
        }
    }

    /// <summary>
    /// Paste from <see cref="Document.GridConfig.Clip"/>.
    /// </summary>
    [TestMethod]
    public void GridPaste()
    {
        int count = DoBaselineTests(
            Run, @"Grid\Paste.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            for (int n = 0; n < 2; n++)
            {
                bool withUri = n > 0;

                // Create some nodes.
                (var arr1, var typeRec1) = GenerateBasicRecords(withUri);
                (var arr2, var typeRec2) = GenerateBasicRecords2();
                var guid1 = AddTestGrid("A", typeRec1);
                Assert.AreEqual(typeRec1, Doc.GetGridConfig(guid1).RecordType);
                var guid2 = AddTestGrid("B", typeRec2);
                Assert.AreEqual(typeRec2, Doc.GetGridConfig(guid2).RecordType);

                // Doc.GetGridConfig(guid3) (C) has no columns, but will have some rows.
                var typeRec3 = DType.EmptyRecordReq;
                var guid3 = AddTestGrid("C", typeRec3);
                Assert.AreEqual(typeRec3, Doc.GetGridConfig(guid3).RecordType);

                // Doc.GetGridConfig(guid4) (D) has no opt columns.
                var typeRec4 = DType.EmptyRecordReq.SetNameType(_name30, DType.I2Req);
                var guid4 = AddTestGrid("D", typeRec4);
                Assert.AreEqual(typeRec4, Doc.GetGridConfig(guid4).RecordType);

                // Add some rows to the grids.
                Set(Doc.ReplaceRowsWithRecords(guid1, 0, 0, arr1.Length, arr1));
                WriteRecords(guid1);
                Set(Doc.ReplaceRowsWithRecords(guid2, 0, 0, arr2.Length, arr2));
                WriteRecords(guid2);
                Set(Doc.InsertBlankRows(guid3, 0, 3));
                WriteRecords(guid3);
                var doc = Doc;
                doc = doc.InsertBlankRows(guid4, 0, 3);
                doc = doc.SetCellValue<short>(guid4, 0, 0, -1);
                doc = doc.SetCellValue<short>(guid4, 0, 1, -2);
                doc = doc.SetCellValue<short>(guid4, 0, 2, -3);
                Set(doc);
                WriteRecords(guid4);

                // Create some clips.
                var clip1 = Doc.GetGridConfig(guid1).CreateClip();
                var clip2 = Doc.GetGridConfig(guid2).CreateClip();
                var clip3 = Doc.GetGridConfig(guid3).CreateClip();
                var clip4 = Doc.GetGridConfig(guid4).CreateClip();
                var clipEmp1 = Doc.GetGridConfig(guid1).CreateClip(0, 0);
                var clipEmp2 = Doc.GetGridConfig(guid2).CreateClip(5, 5);
                var clipEmp3 = Doc.GetGridConfig(guid3).CreateClip(0, 0);
                var clipEmp4 = Doc.GetGridConfig(guid4).CreateClip(0, 0);

                // Check the lengths of the clips
                Sink.WriteLine("*** Get clip length");
                WriteClipDataLength(clip1, 1);
                WriteClipDataLength(clip1);
                WriteClipDataLength(clip2);
                WriteClipDataLength(clip3);
                WriteClipDataLength(clip4);

                // No-op.
                Sink.WriteLine("*** No-op pastes");
                Set(Doc.PasteRows(guid1, 5, 0, Doc.GetGridConfig(guid1).RecordType, null));
                WriteRecords(guid1);
                // Without uri, no type conversion, so no-op.
                Set(Doc.PasteRows(guid1, 5, 0, clipEmp4));
                WriteRecords(guid1);
                Set(Doc.PasteRows(guid2, 5, 0, clipEmp2));
                WriteRecords(guid1);
                Set(Doc.PasteRows(guid3, 0, 0, clipEmp3));
                WriteRecords(guid3);
                Set(Doc.PasteRows(guid4, 0, 0, clipEmp4));
                WriteRecords(guid4);

                // Delete some.
                Sink.WriteLine("*** Delete some paste");
                Set(Doc.PasteRows(guid1, 3, 5, clipEmp1));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);
                Undo();

                // Delete some and promote with no opt in sight.
                Sink.WriteLine("*** Delete some, promote non-opt paste");
                Set(Doc.PasteRows(guid4, 1, 1, typeRec4.SetNameType(_name30, DType.R4Req), clipEmp4));
                WriteRecords(guid4);
                Undo();
                WriteRecords(guid4);
                Redo();
                WriteRecords(guid4);
                Undo();

                // Delete everything.
                Sink.WriteLine("*** Delete all paste");
                Set(Doc.PasteRows(guid1, 0, Doc.GetGridConfig(guid1).RowCount, clipEmp1));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);

                // Paste into empty.
                Sink.WriteLine("*** Paste into empty");
                Set(Doc.PasteRows(guid1, 0, 0, clip2));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);
                Undo(2);

                // Append paste.
                Sink.WriteLine("*** Append paste");
                Set(Doc.PasteRows(guid1, Doc.GetGridConfig(guid1).RowCount, 0, clip2));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);
                Undo();

                Sink.WriteLine("*** Append paste");
                Set(Doc.PasteRows(guid2, Doc.GetGridConfig(guid2).RowCount, 0, clip1));
                WriteRecords(guid2);
                Undo();
                WriteRecords(guid2);
                Redo();
                WriteRecords(guid2);
                Undo();

                // Delete some (less than insert) while pasting.
                Sink.WriteLine("*** Delete fewer paste");
                Set(Doc.PasteRows(guid1, 5, 3, clip2));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);

                // Delete lots (more than insert) while pasting, with no type change.
                Sink.WriteLine("*** Delete more paste, no type change");
                Set(Doc.PasteRows(guid1, 2, 11, clip2));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);

                // Delete some while pasting, with no type change.
                Sink.WriteLine("*** Delete fewer paste, no type change");
                Set(Doc.PasteRows(guid1, Doc.GetGridConfig(guid1).RowCount - 1, 1, clip2));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);

                UndoAll();
            }
        }
    }

    private void WriteClipDataLength(DocumentBase.GridConfig.Clip clip, long maxLength = 10000)
    {
        var isUnderMaxLength = clip.TryGetDataLength(maxLength, out var actualLength);
        Sink.WriteLine($"Clip length: {actualLength}, did not exceed {maxLength}: {isUnderMaxLength}");
    }

    /// <summary>
    /// Paste from <see cref="Document.GridConfig.Clip"/> with column mapping.
    /// </summary>
    [TestMethod]
    public void GridPasteMapped()
    {
        int count = DoBaselineTests(
            Run, @"Grid\PasteMapped.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Create some nodes.
            (var arrBase, var typeRecBase) = GenerateBasicRecords();
            var guid1 = AddTestGrid("A", typeRecBase);
            var guid2 = AddTestGrid("B", typeRecBase);

            // Add some rows to the grids.
            var doc = Doc;
            doc = doc.ReplaceRowsWithRecords(guid1, 0, 0, arrBase.Length, arrBase);
            doc = doc.ReplaceRowsWithRecords(guid2, 0, 0, arrBase.Length, arrBase);
            Set(doc);
            WriteSameRecords(guid1, guid2);

            foreach (var yAsOpt in new[] { true, false })
            {
                (var arrClip, var typeRecClip) = GenerateBasicRecords2(yAsOpt);

                var guidClip = AddTestGrid(yAsOpt ? "C_WithOpt" : "C", typeRecClip);

                // Add some rows to the grid used for clipping.
                Set(Doc.ReplaceRowsWithRecords(guidClip, 0, 0, arrClip.Length, arrClip));
                WriteSameRecords(guid1, guid2);
                WriteRecords(guidClip);

                var gridClip = Doc.GetGridConfig(guidClip);
                DataClip dataFull = gridClip.CreateClip();
                DataClip data1;
                DataClip data2;
                FieldMap map;

                Sink.Write("*** No mapping: ");
                map = default;
                data1 = gridClip.CreateClip(map);
                data2 = dataFull.MapFields(map);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write("*** No columns: ");
                map = new FieldMap();
                data1 = gridClip.CreateClip(map);
                data2 = dataFull.MapFields(map);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write("*** One column: ");
                map = new FieldMap() { { new DName("A"), _name20 } };
                data1 = gridClip.CreateClip(fieldMap: map);
                data2 = dataFull.MapFields(map);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write("*** One column: ");
                map = new FieldMap() { { _name30, _name20 } };
                data1 = gridClip.CreateClip(fieldMap: map);
                data2 = dataFull.MapFields(map);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write("*** Two columns: ");
                map = new FieldMap() { { _name30, _name40 }, { _name50, _name10 } };
                data1 = gridClip.CreateClip(fieldMap: map);
                data2 = dataFull.MapFields(map);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write("*** Three columns, including dup: ");
                map = new FieldMap() { { _name30, _name20 }, { _name50, _name50 }, { _name40, _name20 } };
                data1 = gridClip.CreateClip(fieldMap: map);
                data2 = dataFull.MapFields(map);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();
            }
        }
    }

    /// <summary>
    /// Paste from <see cref="Document.GridConfig.Clip"/> with column mapping.
    /// </summary>
    [TestMethod]
    public void GridPasteSubClip()
    {
        int count = DoBaselineTests(
            Run, @"Grid\PasteSubClip.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Create some nodes.
            (var arrBase, var typeRecBase) = GenerateBasicRecords();
            (var arrClip, var typeRecClip) = GenerateBasicRecords2();

            var guid1 = AddTestGrid("A", typeRecBase);
            var guid2 = AddTestGrid("B", typeRecBase);
            var guidClip = AddTestGrid("C", typeRecClip);

            // Add some rows to the grids.
            var doc = Doc;
            doc = doc.ReplaceRowsWithRecords(guid1, 0, 0, arrBase.Length, arrBase);
            doc = doc.ReplaceRowsWithRecords(guid2, 0, 0, arrBase.Length, arrBase);
            doc = doc.ReplaceRowsWithRecords(guidClip, 0, 0, arrClip.Length, arrClip);
            Set(doc);
            var gridClip = Doc.GetGridConfig(guidClip);
            WriteSameRecords(guid1, guid2);
            WriteRecords(guidClip);

            DataClip dataFull = gridClip.CreateClip();
            DataClip data1;
            DataClip data2;
            FieldMap map;

            var rowMapInfos = new[]
            {
                ("No row map", default),
                ("All rows", RowMapUtil.Create((0, -1))),
                ("No rows", RowMapUtil.Create()),
                ("One range", RowMapUtil.Create((2, 5))),
                ("Two ranges", RowMapUtil.Create((2, 5), (7, -1))),
                ("Overlapping ranges", RowMapUtil.Create((2, 5), (3, 7))),
                ("More clip than src", RowMapUtil.Create((5, 7), (4, -1), (1, 3), (7, -1))),
                ("Range past end src", RowMapUtil.Create((1_000_000_000, -1))),
            };

            // We assert below that the types are the same for each row map. This gets assigned
            // the first (stringized) set of types.
            string typesPrev = null;

            // In the undo groups below, we post a marker undo item. This avoids worrying about whether an
            // undo item/group was posted at all by the operation (when the operation is otherwise a no-op).
            foreach (var info in rowMapInfos)
            {
                var (str, rowMap) = info;

                Sink.WriteLine();
                Sink.WriteLine("$$$$$ Row map section: {0} $$$$$", str);
                Sink.WriteLine();

                var types = new StringBuilder();

                string msg;
                Sink.Write(msg = "*** No column map: ");
                map = new FieldMap();
                data1 = gridClip.CreateClip(rowMap: rowMap);
                data2 = dataFull.CreateSubClip(default, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** No columns: ");
                map = new FieldMap();
                data1 = gridClip.CreateClip(map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** One column: ");
                map = new FieldMap() { { new DName("A"), _name20 } };
                data1 = gridClip.CreateClip(map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** One column: ");
                map = new FieldMap() { { _name30, _name20 } };
                data1 = gridClip.CreateClip(map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** Two columns: ");
                map = new FieldMap() { { _name30, _name40 }, { _name50, _name10 } };
                data1 = gridClip.CreateClip(map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);

                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** Three columns, including dup: ");
                map = new FieldMap() { { _name30, _name20 }, { _name50, _name50 }, { _name40, _name20 } };
                data1 = gridClip.CreateClip(map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                // The types should be the same across the various row maps.
                if (typesPrev == null)
                    typesPrev = types.ToString();
                else
                    Assert.AreEqual(typesPrev, types.ToString());
            }
        }
    }

    /// <summary>
    /// Paste from a <see cref="DataValueClip"/> rather than <see cref="Document.GridConfig.Clip"/>.
    /// </summary>
    [TestMethod]
    public void GridPasteData()
    {
        int count = DoBaselineTests(
            Run, @"Grid\PasteData.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            for (int n = 0; n < 2; n++)
            {
                bool withUri = n > 0;
                // Create some data clips.
                (var arr1, var typeRec1) = GenerateBasicRecords(withUri);
                (var arr2, var typeRec2) = GenerateBasicRecords2();

                var tm = GetCodeGen().TypeManager;
                var data1 = DataValueClip.Create(tm, typeRec1.ToSequence(), arr1);
                var data2 = DataValueClip.Create(tm, typeRec2.ToSequence(), arr2);

                var guid1 = AddTestGrid("A", typeRec1);

                // No conversion needed for this one.
                Set(Doc.PasteRows(guid1, 0, 0, data1));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);

                // This does conversion.
                Set(Doc.PasteRows(guid1, 5, 3, data2));
                WriteRecords(guid1);
                Undo();
                WriteRecords(guid1);
                Redo();
                WriteRecords(guid1);

                UndoAll();
            }
        }
    }

    /// <summary>
    /// Paste from a <see cref="DataValueClip"/> rather than <see cref="Document.GridConfig.Clip"/>,
    /// with column mapping. This should produce output very similar to that of <see cref="GridPasteMapped"/>.
    /// </summary>
    [TestMethod]
    public void GridPasteDataMapped()
    {
        int count = DoBaselineTests(
            Run, @"Grid\PasteDataMapped.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Create some data clips.
            (var arrBase, var typeRecBase) = GenerateBasicRecords();
            (var arrClip, var typeRecClip) = GenerateBasicRecords2();

            var tm = GetCodeGen().TypeManager;

            var guid1 = AddTestGrid("A", typeRecBase);
            var guid2 = AddTestGrid("B", typeRecBase);

            DataClip dataFull = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip);
            DataClip data1;
            DataClip data2;

            // Add base rows.
            var doc = Doc;
            data1 = DataValueClip.Create(tm, typeRecBase.ToSequence(), arrBase);
            doc = doc.PasteRows(guid1, 0, 0, data1);
            doc = doc.PasteRows(guid2, 0, 0, data1);
            Set(doc);
            WriteSameRecords(guid1, guid2);

            FieldMap map;

            Sink.Write("*** No column map: ");
            map = default;
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

            Sink.Write("*** No columns: ");
            map = new FieldMap();
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

            Sink.Write("*** One column: ");
            map = new FieldMap() { { new DName("A"), _name20 } };
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

            Sink.Write("*** One column: ");
            map = new FieldMap() { { _name30, _name20 } };
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

            Sink.Write("*** Two columns: ");
            map = new FieldMap() { { _name30, _name40 }, { _name50, _name10 } };
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

            Sink.Write("*** Three columns, including dup: ");
            map = new FieldMap() { { _name30, _name20 }, { _name50, _name50 }, { _name40, _name20 } };
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

            // For testing composition of field maps.
            Sink.WriteLine("*** Field map composition:");
            Sink.WriteLine("   Type: {0}", dataFull.ClipItemType);
            dataFull = dataFull.MapFields(
                new FieldMap() { { new DName("A"), _name10 }, { new DName("B"), _name20 }, { new DName("C"), _name40 }, { new DName("D"), _name50 }, { new DName("E"), _name10 } });
            Sink.WriteLine("   Type: {0}", dataFull.ClipItemType);
            dataFull = dataFull.MapFields(
                new FieldMap() { { _name20, new DName("B") }, { _name50, new DName("D") } });
            Sink.WriteLine("   Type: {0}", dataFull.ClipItemType);

            Sink.Write("*** Three columns, including dup, composed map: ");
            map = new FieldMap() { { _name30, _name20 }, { _name50, _name50 }, { _name40, _name20 } };
            data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map);
            data2 = dataFull.MapFields(map);
            Sink.WriteLine("{0}", data1.ClipItemType);
            doc = Doc;
            doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
            doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            Undo();
            WriteSameRecords(guid1, guid2);
            Redo();
            WriteSameRecords(guid1, guid2);
            Undo();

        }
    }

    /// <summary>
    /// Paste from <see cref="Document.GridConfig.Clip"/> with column mapping.
    /// </summary>
    [TestMethod]
    public void GridPasteDataSubClip()
    {
        int count = DoBaselineTests(
            Run, @"Grid\PasteDataSubClip.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Create some data clips.
            (var arrBase, var typeRecBase) = GenerateBasicRecords();
            (var arrClip, var typeRecClip) = GenerateBasicRecords2();

            var tm = GetCodeGen().TypeManager;

            var guid1 = AddTestGrid("A", typeRecBase);
            var guid2 = AddTestGrid("B", typeRecBase);

            DataClip dataFull = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip);
            DataClip data1;
            DataClip data2;

            // Add base rows.
            var doc = Doc;
            data1 = DataValueClip.Create(tm, typeRecBase.ToSequence(), arrBase);
            doc = doc.PasteRows(guid1, 0, 0, data1);
            doc = doc.PasteRows(guid2, 0, 0, data1);
            Set(doc);
            WriteSameRecords(guid1, guid2);
            FieldMap map;

            var rowMapInfos = new[]
            {
                ("No row map", default),
                ("All rows", RowMapUtil.Create((0, -1))),
                ("No rows", RowMapUtil.Create()),
                ("One range", RowMapUtil.Create((2, 5))),
                ("Two ranges", RowMapUtil.Create((2, 5), (7, -1))),
                ("Overlapping ranges", RowMapUtil.Create((2, 5), (3, 7))),
                ("More clip than src", RowMapUtil.Create((5, 7), (4, -1), (1, 3), (7, -1))),
                ("Range past end src", RowMapUtil.Create((1_000_000_000, -1))),
            };

            // We assert below that the types are the same for each row map. This gets assigned
            // the first (stringized) set of types.
            string typesPrev = null;

            // In the undo groups below, we post a marker undo item. This avoids worrying about whether an
            // undo item/group was posted at all by the operation (when the operation is otherwise a no-op).
            foreach (var info in rowMapInfos)
            {
                var (str, rowMap) = info;

                Sink.WriteLine();
                Sink.WriteLine("$$$$$ Row map section: {0} $$$$$", str);
                Sink.WriteLine();

                var types = new StringBuilder();

                string msg;
                Sink.Write(msg = "*** No column map: ");
                data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, default, rowMap);
                data2 = dataFull.CreateSubClip(default, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** No columns: ");
                map = new FieldMap();
                data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** One column: ");
                map = new FieldMap() { { new DName("A"), _name20 } };
                data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** One column: ");
                map = new FieldMap() { { _name30, _name20 } };
                data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** Two columns: ");
                map = new FieldMap() { { _name30, _name40 }, { _name50, _name10 } };
                data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                Sink.Write(msg = "*** Three columns, including dup: ");
                map = new FieldMap() { { _name30, _name20 }, { _name50, _name50 }, { _name40, _name20 } };
                data1 = DataValueClip.Create(tm, typeRecClip.ToSequence(), arrClip, map, rowMap);
                data2 = dataFull.CreateSubClip(map, rowMap);
                Sink.WriteLine("{0}", data1.ClipItemType);
                doc = Doc;
                doc = doc.AddUndoMarker(msg);
                doc = doc.PasteRows(guid1, doc.GetGridConfig(guid1).RowCount, 0, data1);
                doc = doc.PasteRows(guid2, doc.GetGridConfig(guid2).RowCount, 0, data2);
                Set(doc);
                types.AppendLine(Doc.GetGridConfig(guid1).RecordType.Serialize());
                WriteSameRecords(guid1, guid2);
                Undo();
                WriteSameRecords(guid1, guid2);
                Redo();
                WriteSameRecords(guid1, guid2);
                Undo();

                // The types should be the same across the various row maps.
                if (typesPrev == null)
                    typesPrev = types.ToString();
                else
                    Assert.AreEqual(typesPrev, types.ToString());
            }
        }
    }

    /// <summary>
    /// Paste from a <see cref="DataValueClip"/> of non record items with a default column name.
    /// </summary>
    [TestMethod]
    public void GridPasteDataNonRecords()
    {
        int count = DoBaselineTests(
            Run, @"Grid\PasteDataNonRecords.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            // Create the base grid data.
            (var arrBase, var typeRecBase) = GenerateBasicRecords();

            var tm = GetCodeGen().TypeManager;
            var guid = AddTestGrid("A", DType.EmptyRecordReq.AddNameType(new DName("X"), DType.I4Req));

            Sink.WriteLine("No conversion and no new column needed for this one");
            var dataClip = DataValueClip.Create(tm, DType.I4Req.ToSequence(), new int[] { 1, 2, 3 });
            Set(Doc.PasteRows(guid, 0, 0, dataClip, new DName("X")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Sink.WriteLine("This converts the column type and merges the clip values with existing column");
            dataClip = DataValueClip.Create(tm, DType.R8Req.ToSequence(), new double[] { 4.0, 5.0 });
            Set(Doc.PasteRows(guid, 1, 1, dataClip, new DName("X")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Sink.WriteLine("This converts the data clip values to the column type and merges with existing column");
            dataClip = DataValueClip.Create(
                tm,
                DType.I4Req.ToSequence(),
                new int[] { 6 });
            Set(Doc.PasteRows(guid, 2, 0, dataClip, new DName("X")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Sink.WriteLine("This converts the column type to opt and sets some null values");
            dataClip = DataValueClip.Create(
                tm,
                DType.I4Opt.ToSequence(),
                new int?[] { 10, 11, null, 13 },
                default,
                RowMapUtil.Create((1, 3)));
            Set(Doc.PasteRows(guid, 2, 0, dataClip, new DName("X")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Sink.WriteLine("This copies from a clip of required values to a destination column of different and optional type");
            dataClip = DataValueClip.Create(
                tm,
                DType.I4Req.ToSequence(),
                new int[] { 20 });
            Set(Doc.PasteRows(guid, 0, 0, dataClip, new DName("X")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Sink.WriteLine("This creates a new destination column from a boolean data clip with a single value");
            dataClip = DataValueClip.Create(tm, DType.BitReq, true);
            Set(Doc.PasteRows(guid, 3, 0, dataClip, new DName("Y")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            Sink.WriteLine("This creates a new destination column from a string data clip");
            dataClip = DataValueClip.Create(tm, DType.Text.ToSequence(), new string[] { "a", null, "c" });
            Set(Doc.PasteRows(guid, 3, 0, dataClip, new DName("Z")));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
        }
    }

    [TestMethod]
    public void GridWithUri()
    {
        int count = DoBaselineTests(
            Run, @"Grid\WithUri.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var nameA = new DName("A");
            var nameB = new DName("B");

            var typeImage = DType.CreateUriType(NPath.Root.Append(new DName("Image")));
            var typeRec = DType.CreateRecord(opt: false,
                new TypedName(nameA, DType.I4Req),
                new TypedName(nameB, typeImage));

            Assert.IsTrue(GetTypeManager().TryEnsureDefaultValue(typeImage, out var entryImage));
            var defImage = entryImage.value;

            var guid = AddTestGrid("A", typeRec);

            var grid = Doc.GetGridConfig(guid);
            Assert.AreEqual(0, grid.GetColIndex(nameA));
            Assert.AreEqual(1, grid.GetColIndex(nameB));
            Assert.AreEqual(nameA, grid.GetColName(0));
            Assert.AreEqual(nameB, grid.GetColName(1));
            Assert.AreEqual(DType.I4Req, grid.GetColType(0));
            Assert.AreEqual(typeImage, grid.GetColType(1));
            Assert.AreEqual(typeof(int), grid.GetColSysType(0));
            Assert.AreEqual(typeof(Link), grid.GetColSysType(1));

            Undo();
            Redo();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out _));

            Set(Doc.InsertBlankRows(guid, 0, 15));
            WriteRecords(guid);
            Undo();
            Redo();

            Set(Doc.InsertBlankRows(guid, 3, 7));
            WriteRecords(guid);
            Undo();
            Redo();

            Set(Doc.DeleteNode(guid));
            Undo();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));

            int row0 = 2;
            int row1 = 5;
            int row2 = grid.RowCount - 1;
            int colA = 0;
            int colB = 1;

            Assert.AreEqual(0, grid.GetCellI4(colA, row0));
            Assert.AreEqual(0, grid.GetCellI4(colA, row1));
            Assert.AreEqual(0, grid.GetCellI4(colA, row2));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row0));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row1));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row2));

            WriteRecords(guid);
            int i4 = -17;
            var image = MakeLink("puppy.bmp", ImageFlavor);

            var doc = Doc;
            doc = doc.SetCellValue<int>(guid, colA, row0, i4);
            doc = doc.SetCellValue<Link>(guid, colB, row2, image);
            Set(doc);
            grid = Doc.GetGridConfig(guid);
            Assert.AreEqual(i4, grid.GetCellI4(colA, row0));
            Assert.AreEqual(i4, grid.GetCellValue(colA, row0));
            Assert.AreEqual(0, grid.GetCellI4(colA, row1));
            Assert.AreEqual(0, grid.GetCellValue(colA, row1));
            Assert.AreEqual(0, grid.GetCellI4(colA, row2));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row0));
            Assert.AreEqual(defImage, grid.GetCellValue(colB, row0));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row1));
            Assert.AreEqual(defImage, grid.GetCellValue(colB, row1));
            Assert.AreEqual(image, grid.GetCellValue<Link>(colB, row2));
            Assert.AreEqual(image, grid.GetCellValue(colB, row2));
            WriteRecords(guid);

            Undo();
            grid = Doc.GetGridConfig(guid);
            Assert.AreEqual(0, grid.GetCellI4(colA, row0));
            Assert.AreEqual(0, grid.GetCellI4(colA, row1));
            Assert.AreEqual(0, grid.GetCellI4(colA, row2));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row0));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row1));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row2));

            Redo();
            grid = Doc.GetGridConfig(guid);
            Assert.AreEqual(i4, grid.GetCellI4(colA, row0));
            Assert.AreEqual(0, grid.GetCellI4(colA, row1));
            Assert.AreEqual(0, grid.GetCellI4(colA, row2));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row0));
            Assert.AreEqual(defImage, grid.GetCellValue<Link>(colB, row1));
            Assert.AreEqual(image, grid.GetCellValue<Link>(colB, row2));

            UndoAll();
            RedoAll();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));
            WriteRecords(guid);

            doc = Doc;
            for (int i = 0; i < grid.RowCount; i++)
            {
                // Skip one.
                if (i == grid.RowCount / 2)
                    continue;
                int v = i + 1;
                if (i % 2 == 0)
                {
                    doc = doc.SetCellValue<int>(guid, colA, i, v);
                    doc = doc.SetCellValue<Link>(guid, colB, i, MakeLink($"{v}.jpg", JpegFlavor));
                }
                else
                {
                    doc = doc.SetCellValue(guid, colA, i, (object)v);
                    doc = doc.SetCellValue(guid, colB, i, (object)MakeLink($"{v}.jpg", JpegFlavor));
                }
            }
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            // Create some value arrays.
            int crow = 10;
            var xs = new int[crow];
            var ys = new Link[crow];
            for (int i = 0; i < crow; i++)
            {
                int v = i + 1;
                xs[i] = v;
                ys[i] = MakeLink($"{v}.bmp", ImageFlavor);
            }

            // Set those values at various places.
            doc = Doc;
            doc = doc.SetCellValues(guid, colA, 5, 5 + crow / 2, xs);
            doc = doc.SetCellValues(guid, colB, 11, 11 + crow, ys);
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            // Now use the non-static-typed version.
            doc = Doc;
            doc = doc.SetCellValues(guid, colA, 1, 1 + crow, new ReadOnly.Array(xs));
            doc = doc.SetCellValues(guid, colB, 12, 12 + crow, new ReadOnly.Array(ys));
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            // Delete some rows.
            Set(Doc.DeleteRows(guid, 8, 14));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            UndoAll();
            RedoAll();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));
            WriteRecords(guid);

            Set(Doc.ReplaceRows(guid, 0, grid.RowCount));
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            grid = Doc.GetGridConfig(guid);
            var clip = grid.CreateClip(2, grid.RowCount - 2);

            Set(Doc.InsertRows(guid, grid.RowCount / 2, clip));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            grid = Doc.GetGridConfig(guid);
            Set(Doc.ReplaceRows(guid, grid.RowCount / 2, 3, clip));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            grid = Doc.GetGridConfig(guid);
            Set(Doc.ReplaceRowsWithBlank(guid, grid.RowCount / 2, 3, 8));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            // Setting wrong value type.
            bool caught;
            caught = false;
            try { Doc.SetCellValue(guid, 0, 0, "hello"); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            caught = false;
            try { Doc.SetCellValue(guid, 0, 0, (object)true); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            caught = false;
            try { Doc.SetCellValue(guid, 1, 0, (object)5); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);
        }
    }

    [TestMethod]
    public void GridWithCompound()
    {
        int count = DoBaselineTests(
            Run, @"Grid\WithCompound.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, TOpts opts)
        {
            var nameA = new DName("A");
            var nameB = new DName("B");

            var typeTup = DType.CreateTuple(false, DType.I4Req, DType.BitOpt, DType.Text);
            var typeRec = DType.CreateRecord(opt: false,
                new TypedName(nameA, DType.I4Req),
                new TypedName(nameB, typeTup));

            var tm = GetTypeManager();
            Assert.IsTrue(tm.TryEnsureSysType(typeTup, out Type stTup));
            Assert.AreEqual(typeof(TestTupType), stTup);
            Assert.IsTrue(tm.TryEnsureDefaultValue(typeTup, out var entryTup));
            Assert.IsTrue(entryTup.special);
            Assert.IsInstanceOfType(entryTup.value, typeof(TestTupType));
            var defTup = (TestTupType)entryTup.value;

            var guid = GuidNext();
            Set(Doc.CreateGridNode(tm, guid, MakeName("A"), typeRec.ToSequence()));
            var grid = Doc.GetGridConfig(guid);

            Assert.AreEqual(0, grid.GetColIndex(nameA));
            Assert.AreEqual(1, grid.GetColIndex(nameB));
            Assert.AreEqual(nameA, grid.GetColName(0));
            Assert.AreEqual(nameB, grid.GetColName(1));
            Assert.AreEqual(DType.I4Req, grid.GetColType(0));
            Assert.AreEqual(typeTup, grid.GetColType(1));
            Assert.AreEqual(typeof(int), grid.GetColSysType(0));
            Assert.AreEqual(stTup, grid.GetColSysType(1));

            Undo();
            Redo();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));

            Set(Doc.InsertBlankRows(guid, 0, 15));
            WriteRecords(guid);
            Undo();
            Redo();

            Set(Doc.InsertBlankRows(guid, 3, 7));
            WriteRecords(guid);
            Undo();
            Redo();

            Set(Doc.DeleteNode(guid));
            Undo();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));

            int row0 = 2;
            int row1 = 5;
            int row2 = grid.RowCount - 1;
            int colA = 0;
            int colB = 1;

            Assert.AreEqual(0, grid.GetCellI4(colA, row0));
            Assert.AreEqual(0, grid.GetCellI4(colA, row1));
            Assert.AreEqual(0, grid.GetCellI4(colA, row2));
            Assert.AreEqual(defTup, grid.GetCellValue<TestTupType>(colB, row0));
            Assert.AreEqual(defTup, grid.GetCellValue<TestTupType>(colB, row1));
            Assert.AreEqual(defTup, grid.GetCellValue<TestTupType>(colB, row2));

            WriteRecords(guid);

            int i4 = -17;
            TestTupType tup = new TestTupType() { _F0 = 3, _F1 = true, _F2 = "hello" };

            var doc = Doc;
            doc = doc.SetCellValue<int>(guid, colA, row0, i4);
            doc = doc.SetCellValue<TestTupType>(guid, colB, row2, tup);
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            UndoAll();
            RedoAll();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));
            WriteRecords(guid);

            doc = Doc;
            for (int i = 0; i < grid.RowCount; i++)
            {
                // Skip one.
                if (i == grid.RowCount / 2)
                    continue;
                int v = i + 1;
                if (i % 2 == 0)
                {
                    doc = doc.SetCellValue<int>(guid, colA, i, v);
                    doc = doc.SetCellValue<TestTupType>(guid, colB, i, new TestTupType() { _F0 = -i, _F1 = (i & 1) == 0, _F2 = $"{i * 2}" });
                }
                else
                {
                    doc = doc.SetCellValue(guid, colA, i, (object)v);
                    doc = doc.SetCellValue(guid, colB, i, (object)new TestTupType() { _F0 = -i, _F1 = (i & 1) == 0, _F2 = $"{i * 2}" });
                }
            }
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            // Create some value arrays.
            int crow = 10;
            var xs = new int[crow];
            var ys = new TestTupType[crow];
            for (int i = 0; i < crow; i++)
            {
                int v = i + 1;
                xs[i] = v;
                ys[i] = new TestTupType() { _F0 = -i, _F1 = (i & 1) == 0, _F2 = $"{i * 2}" };
            }

            // Set those values at various places.
            doc = Doc;
            doc = doc.SetCellValues(guid, colA, 5, 5 + crow / 2, xs);
            doc = doc.SetCellValues(guid, colB, 11, 11 + crow, ys);
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);

            // Now use the non-static-typed version.
            doc = Doc;
            doc = doc.SetCellValues(guid, colA, 1, 1 + crow, new ReadOnly.Array(xs));
            doc = doc.SetCellValues(guid, colB, 8, 8 + crow, new ReadOnly.Array(ys));
            Set(doc);
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            // Delete some rows.
            Set(Doc.DeleteRows(guid, 11, 17));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);

            UndoAll();
            RedoAll();
            Assert.IsTrue(Doc.IsGrid(guid, out _, out grid));
            WriteRecords(guid);

            Set(Doc.ReplaceRows(guid, 0, grid.RowCount));
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            var clip = grid.CreateClip(2, grid.RowCount - 2);

            Set(Doc.InsertRows(guid, grid.RowCount / 2, clip));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            Set(Doc.ReplaceRows(guid, grid.RowCount / 2, 3, clip));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            Set(Doc.ReplaceRowsWithBlank(guid, grid.RowCount / 3, 3, 8));
            WriteRecords(guid);
            Undo();
            WriteRecords(guid);
            Redo();
            WriteRecords(guid);
            Undo();

            // Promoting fails.
            bool caught;
            caught = false;
            var typeTup2 = DType.CreateTuple(false, DType.R8Req, DType.BitOpt, DType.Text);
            try { Doc.ConvertColumn(guid, 1, typeTup2); }
            catch (Exception ex) when (ex.Data.Contains("IsBug")) { caught = true; }
            Assert.IsTrue(caught);

            // Promoting to opt succeeds.
            Set(Doc.ConvertColumn(guid, 1, typeTup.ToOpt()));
            WriteRecords(guid);
            Undo();
            Redo();
        }
    }

    private sealed class BindHostImpl : MinBindHost
    {
        public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            if (user | fuzzy)
            {
                info = null;
                return false;
            }

            info = TestFunctions.Instance.GetInfo(name);
            Validation.Assert(info is null || info.Oper is not null);
            return info != null;
        }
    }
}
