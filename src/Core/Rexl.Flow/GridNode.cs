// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Date = RDate;
using FieldMap = ReadOnly.Dictionary<DName, DName>;
using Integer = System.Numerics.BigInteger;
using RowMap = ReadOnly.Array<(int min, int lim)>;
using Time = System.TimeSpan;

partial class Document<TDoc, TNode>
{
    // REVIEW: After we remove the UndoItems, we should review the grid editing functions to clean up the interface.
    // Currently, DocumentService only uses a few of these functions. Most are only used in UndoManagerImpl and GridTests.

    /// <summary>
    /// Return the node of the given guid. BugChecks that there is one, and that it is a grid node.
    /// </summary>
    public TNode GetGridNode(Guid guid)
    {
        Validation.BugCheck(IsGrid(guid, out var node, out _));
        return node;
    }

    /// <summary>
    /// Gets the <see cref="DocumentBase.GridConfig"/> for the given guid.
    /// </summary>
    public GridConfig GetGridConfig(Guid guid)
    {
        Validation.BugCheck(IsGrid(guid, out _, out var grid));
        return grid;
    }

    /// <summary>
    /// Returns whether the given <paramref name="guid"/> corresponds to a grid node
    /// and if so, sets <paramref name="node"/> and <paramref name="grid"/>.
    /// </summary>
    public bool IsGrid(Guid guid, out TNode node, out GridConfig grid)
    {
        if (TryGetNode(guid, out node) && (grid = node.Config as GridConfig) != null)
            return true;

        node = null;
        grid = null;
        return false;
    }

    #region Record Editing

    /// <summary>
    /// Should only be called by Document!
    /// 
    /// Given records compatible with this <see cref="RecordCache"/>, replace the indicated rows
    /// with these record values.
    /// </summary>
    public TDoc ReplaceRowsWithRecords(Guid guid, int rowDst, int crowDel, int crowIns, ReadOnly.Array<RecordBase> src, int rowSrc = 0)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        if (!gridConfig.ReplaceRowsWithRecords(rowDst, crowDel, crowIns, src, rowSrc))
            return (TDoc)this;
        return SetConfig(guid, gridConfig);
    }

    #endregion Record Editing

    #region Cell editing

    public TDoc SetCellValue(Guid guid, int col, int row, object value)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.SetCellValue(col, row, value);
        return SetConfig(guid, gridConfig);
    }

    public TDoc SetCellValue<T>(Guid guid, int col, int row, T value)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.SetCellValue<T>(col, row, value);
        return SetConfig(guid, gridConfig);
    }

    public TDoc SetCellValues(Guid guid, int col, int rowMin, int rowLim, ReadOnly.Array values)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.SetCellValues(col, rowMin, rowLim, values);
        return SetConfig(guid, gridConfig);
    }

    public TDoc SetCellValues<T>(Guid guid, int col, int rowMin, int rowLim, T[] values)
    {
        return SetCellValues<T>(guid, col, rowMin, rowLim, new ReadOnly.Array<T>(values));
    }

    public TDoc SetCellValues<T>(Guid guid, int col, int rowMin, int rowLim, ReadOnly.Array<T> values)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.SetCellValues<T>(col, rowMin, rowLim, values);
        return SetConfig(guid, gridConfig);
    }

    public sealed class GridColumnUpdates
    {
        public int Column { get; set; }
        public Type DataType { get; set; }
        public DType ConvertType { get; set; }
        public List<(int row, object value)> CellUpdates { get; set; }
    }

    /// <summary>
    /// Set multiple cell values by applying all of the changes in <paramref name="updates"/>.
    /// </summary>
    public TDoc SetCellValues(Guid guid, IEnumerable<GridColumnUpdates> updates)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        foreach (var columnUpdates in updates)
        {
            if (columnUpdates.ConvertType != default)
                gridConfig.ConvertColumn(columnUpdates.Column, columnUpdates.ConvertType);

            var st = Nullable.GetUnderlyingType(columnUpdates.DataType) ?? columnUpdates.DataType;
            foreach (var cellUpdate in columnUpdates.CellUpdates)
            {
                object value = cellUpdate.value;
                if (value != null && value.GetType() != st)
                    value = Convert.ChangeType(value, st);
                gridConfig.SetCellValue(columnUpdates.Column, cellUpdate.row, value);
            }
        }
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Clear the value of all cells in the <paramref name="columns"/> and <paramref name="rowRanges"/>. if
    /// <paramref name="<paramref name="crowDel"/> is true, then the columns while have their DType changed to
    /// be nullable.
    /// </summary>
    public TDoc ClearCells(Guid guid, IEnumerable<(int rowMin, int rowLim)> rowRanges, IEnumerable<DName> columns, bool allowConversionToOpt)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        var maxRange = rowRanges.Max(x => x.rowLim - x.rowMin);
        foreach (var column in columns)
        {
            var col = gridConfig.GetColIndex(column);
            if (allowConversionToOpt)
            {
                var type = gridConfig.GetColType(col);
                if (!type.IsOpt)
                    gridConfig.ConvertColumn(col, type.ToOpt());
            }

            var values = Array.CreateInstance(gridConfig.GetColSysType(col), maxRange);
            foreach ((int rowMin, int rowLim) range in rowRanges)
                gridConfig.SetCellValues(col, range.rowMin, range.rowLim, values);
        }
        return SetConfig(guid, gridConfig);
    }

    #endregion Cell editing

    #region Row editing
    /// <summary>
    /// Insert blank rows. The new values are the default values of the column system types.
    /// </summary>
    public TDoc InsertBlankRows(Guid guid, int row, int count)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.ReplaceRows(row, 0, count, false, null);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Delete the indicated rows.
    /// </summary>
    public TDoc DeleteRows(Guid guid, int rowMin, int rowLim)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.ReplaceRows(rowMin, rowLim - rowMin, 0, false, null);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Equivalent to deleting <paramref name="crowDel"/> rows starting at <paramref name="row"/> and
    /// then inserting the rows in the (optional) <paramref name="clipIns"/>. If <paramref name="clipIns"/>
    /// is null, then this is the same as <c>DeleteRows(row, row + crowDel)</c>.
    /// </summary>
    public TDoc ReplaceRows(Guid guid, int row, int crowDel, GridConfig.Clip clipIns = null)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.ReplaceRows(row, crowDel, clipIns != null ? clipIns.RowCount : 0, false, clipIns);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Equivalent to deleting <paramref name="crowDel"/> rows starting at <paramref name="row"/> and
    /// then inserting <paramref name="crowIns"/> blank rows.</c>.
    /// </summary>
    public TDoc ReplaceRowsWithBlank(Guid guid, int rowMin, int crowDel, int crowIns)
    {
        return ReplaceRows(guid, rowMin, crowDel, crowIns, false, null);
    }

    /// <summary>
    /// Called internally (from undo) to replace rows. The caller must either pass false for
    /// <paramref name="blankDel"/> or must know for certain that the rows being deleted are blank
    /// already, so we don't need to waste time clearing them.
    /// </summary>
    internal TDoc ReplaceRows(Guid guid, int rowMin, int crowDel, int crowIns, bool blankDel, GridConfig.Clip clipIns)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.ReplaceRows(rowMin, crowDel, crowIns, blankDel, clipIns);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Delete the rows in the provided ranges.
    /// </summary>
    public TDoc DeleteRowRanges(Guid guid, IEnumerable<(int rowMin, int rowLim)> ranges)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        int prevRowMin = int.MaxValue;
        foreach (var range in ranges)
        {
            Validation.Assert(range.rowLim <= prevRowMin && (prevRowMin = range.rowMin) < range.rowLim);
            gridConfig.DeleteRows(range.rowMin, range.rowLim);
        }
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Insert rows with values coming from the given <see cref="Clip"/>.
    /// The type of clipping must match the type of this grid.
    /// </summary>
    public TDoc InsertRows(Guid guid, int row, GridConfig.Clip clip)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        Validation.BugCheckValue(clip, nameof(clip));
        gridConfig.ReplaceRows(row, 0, clip.RowCount, false, clip);
        return SetConfig(guid, gridConfig);
    }

    #endregion Row editing

    #region Column editing
    /// <summary>
    /// Insert a blank column.
    /// </summary>
    public TDoc InsertBlankColumn(Guid guid, DName name, DType type, int col)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.InsertColumn(name, type, col, default);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Delete a column.
    /// </summary>
    public TDoc DeleteColumn(Guid guid, int col)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.DeleteColumn(col);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Delete multiple columns in a gridnode.
    /// </summary>
    public TDoc DeleteColumns(Guid guid, IEnumerable<DName> colNames)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        foreach (var colName in colNames)
            gridConfig.DeleteColumn(gridConfig.GetColIndex(colName));
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Convert the type of the given column to a "wider" type (that accepts the current type).
    /// </summary>
    public TDoc ConvertColumn(Guid guid, int col, DType typeNew)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.ConvertColumn(col, typeNew);
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Convert the type of the given column to a "wider" type (that accepts the current type).
    /// </summary>
    public TDoc ConvertType(Guid guid, DType typeRec)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        gridConfig.PasteRows(0, 0, typeRec, null);
        return SetConfig(guid, gridConfig);
    }

    #endregion Column editing

    #region Paste rows

    /// <summary>
    /// Performs a "paste" operation that converts the record type of this grid to be <paramref name="typeRec"/>,
    /// deletes <paramref name="crowDel"/> rows and inserts rows from <paramref name="clipIns"/> (if not null)
    /// at the given <paramref name="row"/>. Both the current item type and the item type of <paramref name="clipIns"/>
    /// must be accepted by <paramref name="typeRec"/>.
    /// </summary>
    public TDoc PasteRows(Guid guid, int row, int crowDel, DataClip clipIns, DName defaultColName = default)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        if (!gridConfig.PasteRows(row, crowDel, clipIns, defaultColName))
            return (TDoc)this;
        return SetConfig(guid, gridConfig);
    }

    /// <summary>
    /// Performs a "paste" operation that converts the record type of this grid to be <paramref name="typeRec"/>,
    /// deletes <paramref name="crowDel"/> rows and inserts rows from <paramref name="clipIns"/> (if not null)
    /// at the given <paramref name="row"/>. Both the current item type and the item type of <paramref name="clipIns"/>
    /// must be accepted by <paramref name="typeRec"/>.
    /// </summary>
    public TDoc PasteRows(Guid guid, int row, int crowDel, DType typeRec, DataClip clipIns)
    {
        Validation.BugCheckParam(_guidToNode.TryGetValue(guid, out var node), nameof(guid));
        var oldConfig = node.Config as GridConfigImpl;
        Validation.BugCheckParam(oldConfig != null, nameof(guid));
        var gridConfig = oldConfig.Clone();

        if (!gridConfig.PasteRows(row, crowDel, typeRec, clipIns))
            return (TDoc)this;
        return SetConfig(guid, gridConfig);
    }

    #endregion Paste rows
}

partial class DocumentBase
{
    public static GridConfig CreateGridConfig(TypeManager tm, DType type)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(GridConfig.IsValidGridType(type, tm), nameof(type));
        return GridConfigImpl.Create(tm, type.ItemTypeOrThis);
    }

    public static GridConfig CreateGridConfig(TypeManager tm, DType type, IEnumerable<RecordBase> records)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(GridConfig.IsValidGridType(type, tm), nameof(type));
        Validation.CheckValue(records, nameof(records));

        var config = GridConfigImpl.Create(tm, type.ItemTypeOrThis);
        int count = records.Count();
        var src = config.AllocRecordArray(count);
        int i = 0;
        foreach (var record in records)
            src[i++] = record;
        Validation.Assert(count == i);
        config.ReplaceRowsWithRecords(0, 0, count, src);

        return config;
    }

    public static GridConfig LoadGridConfig(TypeManager tm, Stream strm, Guid version)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckValue(strm, nameof(strm));
        return GridConfigImpl.Load(tm, strm, version);
    }

    /// <summary>
    /// The public interface for reading GridNode data. This should only contain functions to read the data
    /// from the GridNode. Anything methods that modify the GridNode should be added directly to the Document,
    /// so the Document will be able to update its internal tree with the newly created GridNode.
    /// </summary>
    public abstract partial class GridConfig : NodeConfig
    {
        /// <summary>
        /// Unique version ID for this grid config.
        /// </summary>
        public Guid Version { get; }

        /// <summary>
        /// Return the number of columns.
        /// </summary>
        public abstract int ColCount { get; }

        /// <summary>
        /// Return the number of rows.
        /// </summary>
        public abstract int RowCount { get; }

        /// <summary>
        /// Return the record <see cref="DType"/>.
        /// </summary>
        public abstract DType RecordType { get; }

        /// <summary>
        /// Return the table <see cref="DType"/>, which is sequence of <see cref="RecordType"/>.
        /// </summary>
        public DType TableType => RecordType.ToSequence();

        /// <summary>
        /// Get the column index from the column name. All other column related methods use
        /// column index to reference a column.
        /// </summary>
        public abstract int GetColIndex(DName name);

        /// <summary>
        /// Get the name of the indicated column.
        /// </summary>
        public abstract DName GetColName(int col);

        /// <summary>
        /// Get the <see cref="DType"/> of the indicated column.
        /// </summary>
        public abstract DType GetColType(int col);

        /// <summary>
        /// Get the system type of the indicated column.
        /// </summary>
        public abstract Type GetColSysType(int col);

        public abstract object GetCellValue(int col, int row);

        public abstract T GetCellValue<T>(int col, int row);

        public abstract string GetCellText(int col, int row);

        public abstract double GetCellR8(int col, int row);
        public abstract float GetCellR4(int col, int row);
        public abstract Integer GetCellInteger(int col, int row);
        public abstract long GetCellI8(int col, int row);
        public abstract int GetCellI4(int col, int row);
        public abstract short GetCellI2(int col, int row);
        public abstract sbyte GetCellI1(int col, int row);
        public abstract ulong GetCellU8(int col, int row);
        public abstract uint GetCellU4(int col, int row);
        public abstract ushort GetCellU2(int col, int row);
        public abstract byte GetCellU1(int col, int row);
        public abstract bool GetCellBool(int col, int row);
        public abstract Date GetCellDate(int col, int row);
        public abstract Time GetCellTime(int col, int row);
        public abstract Guid GetCellGuid(int col, int row);

        public abstract double? GetCellR8Opt(int col, int row);
        public abstract float? GetCellR4Opt(int col, int row);
        public abstract Integer? GetCellIntegerOpt(int col, int row);
        public abstract long? GetCellI8Opt(int col, int row);
        public abstract int? GetCellI4Opt(int col, int row);
        public abstract short? GetCellI2Opt(int col, int row);
        public abstract sbyte? GetCellI1Opt(int col, int row);
        public abstract ulong? GetCellU8Opt(int col, int row);
        public abstract uint? GetCellU4Opt(int col, int row);
        public abstract ushort? GetCellU2Opt(int col, int row);
        public abstract byte? GetCellU1Opt(int col, int row);
        public abstract bool? GetCellBoolOpt(int col, int row);
        public abstract Date? GetCellDateOpt(int col, int row);
        public abstract Time? GetCellTimeOpt(int col, int row);
        public abstract Guid? GetCellGuidOpt(int col, int row);

        public abstract RecordBase[] GetRecords(int rowMin, int rowLim);

        // REVIEW: This modified the GridNode, so it should be removed from the public interface. Currently,
        // it is only used by tests.
        public abstract RecordBase[] AllocRecordArray(int count);

        /// <summary>
        /// Create a <see cref="Clip"/> that contains the values in the indicated rows with the given field mapping.
        /// </summary>
        public abstract Clip CreateClip(int rowMin, int rowLim, FieldMap fieldMap = default);

        /// <summary>
        /// Create a <see cref="Clip"/> that contains the values in the indicated rows with the given field mapping.
        /// </summary>
        public abstract Clip CreateClip(FieldMap fieldMap = default, RowMap rowMap = default);

        /// <summary>
        /// Produces the sequence of row indices that are currently stale. This is mostly for testing.
        /// </summary>
        public abstract IEnumerable<int> GetStaleRowIndices();

        /// <summary>
        /// Get records for the indicated rows. To make an array whose item type is the record
        /// system type, use <see cref="AllocRecordArray(int)"/>.
        /// REVIEW: This is only used by tests right now. Consider removing it.
        /// </summary>
        public abstract void GetRecords(RecordBase[] dst, int rowDst, int rowMin, int rowLim);

        public abstract void Save(Stream strm);

        /// <summary>
        /// Check that the <see cref="DType"/> is valid for a <see cref="GridNode">.
        /// </summary>
        public static bool IsValidGridType(DType type, TypeManager tm)
        {
            // REVIEW: Is there any reason to support TableOpt?
            if (!type.IsTableReq)
                return false;

            var typeRec = type.ItemTypeOrThis;
            foreach (var tn in typeRec.GetNames())
            {
                if (!IsValidColumnType(tn.Type, tm))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that the <see cref="DType"/> is a valid item type for a <see cref="GridNode">.
        /// </summary>
        public static bool IsStdColumnType(DType type)
        {
            return GridConfigImpl.IsStdColumnType(type);
        }

        /// <summary>
        /// Check that the <see cref="DType"/> is a valid item type for a <see cref="GridNode">.
        /// </summary>
        public static bool IsValidColumnType(DType type, TypeManager tm)
        {
            return GridConfigImpl.IsValidColumnType(type, tm);
        }

        public GridConfig(Guid version)
        {
            Version = version;
        }
    }

    partial class GridConfigImpl
    {
        public static GridConfigImpl CreateConfig(TypeManager tm, DType type)
        {
            Validation.BugCheckValue(tm, nameof(tm));
            Validation.BugCheckParam(IsValidGridType(type, tm), nameof(type));
            return GridConfigImpl.Create(tm, type.ItemTypeOrThis);
        }

        public override string GetCellText(int col, int row) { return GetCellValue<string>(col, row); }

        public override double GetCellR8(int col, int row) { return GetCellValue<double>(col, row); }
        public override float GetCellR4(int col, int row) { return GetCellValue<float>(col, row); }
        public override Integer GetCellInteger(int col, int row) { return GetCellValue<Integer>(col, row); }
        public override long GetCellI8(int col, int row) { return GetCellValue<long>(col, row); }
        public override int GetCellI4(int col, int row) { return GetCellValue<int>(col, row); }
        public override short GetCellI2(int col, int row) { return GetCellValue<short>(col, row); }
        public override sbyte GetCellI1(int col, int row) { return GetCellValue<sbyte>(col, row); }
        public override ulong GetCellU8(int col, int row) { return GetCellValue<ulong>(col, row); }
        public override uint GetCellU4(int col, int row) { return GetCellValue<uint>(col, row); }
        public override ushort GetCellU2(int col, int row) { return GetCellValue<ushort>(col, row); }
        public override byte GetCellU1(int col, int row) { return GetCellValue<byte>(col, row); }
        public override bool GetCellBool(int col, int row) { return GetCellValue<bool>(col, row); }
        public override Date GetCellDate(int col, int row) { return GetCellValue<Date>(col, row); }
        public override Time GetCellTime(int col, int row) { return GetCellValue<Time>(col, row); }
        public override Guid GetCellGuid(int col, int row) { return GetCellValue<Guid>(col, row); }

        public override double? GetCellR8Opt(int col, int row) { return GetCellValueOpt<double>(col, row); }
        public override float? GetCellR4Opt(int col, int row) { return GetCellValueOpt<float>(col, row); }
        public override Integer? GetCellIntegerOpt(int col, int row) { return GetCellValueOpt<Integer>(col, row); }
        public override long? GetCellI8Opt(int col, int row) { return GetCellValueOpt<long>(col, row); }
        public override int? GetCellI4Opt(int col, int row) { return GetCellValueOpt<int>(col, row); }
        public override short? GetCellI2Opt(int col, int row) { return GetCellValueOpt<short>(col, row); }
        public override sbyte? GetCellI1Opt(int col, int row) { return GetCellValueOpt<sbyte>(col, row); }
        public override ulong? GetCellU8Opt(int col, int row) { return GetCellValueOpt<ulong>(col, row); }
        public override uint? GetCellU4Opt(int col, int row) { return GetCellValueOpt<uint>(col, row); }
        public override ushort? GetCellU2Opt(int col, int row) { return GetCellValueOpt<ushort>(col, row); }
        public override byte? GetCellU1Opt(int col, int row) { return GetCellValueOpt<byte>(col, row); }
        public override bool? GetCellBoolOpt(int col, int row) { return GetCellValueOpt<bool>(col, row); }
        public override Date? GetCellDateOpt(int col, int row) { return GetCellValueOpt<Date>(col, row); }
        public override Time? GetCellTimeOpt(int col, int row) { return GetCellValueOpt<Time>(col, row); }
        public override Guid? GetCellGuidOpt(int col, int row) { return GetCellValueOpt<Guid>(col, row); }
    }
}
