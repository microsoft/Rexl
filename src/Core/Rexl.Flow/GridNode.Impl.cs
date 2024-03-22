// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Conditional = System.Diagnostics.ConditionalAttribute;
using FieldMap = ReadOnly.Dictionary<DName, DName>;
using RowMap = ReadOnly.Array<(int min, int lim)>;

partial class DocumentBase
{
    /// <summary>
    /// The grid contents, containing all the information and values. This is a <see cref="NodeConfig"/>.
    /// </summary>
    private protected sealed partial class GridConfigImpl : GridConfig
    {
        private readonly TypeManager _tm;

        /// <summary>
        /// This indicates if this is a data clip, which is readonly/immutable.
        /// </summary>
        private readonly bool _isClip;

        /// <summary>
        /// Whether this impl is read only (will never change).
        /// </summary>
        public bool IsReadOnly => _isClip;

        /// <summary>
        /// The associated grid node guid, for reporting to analysis and the undo manager.
        /// REVIEW: Would be nice if we didn't need this.
        /// </summary>
        private Guid _guid;

        // The row type of the grid. This is a record type.
        private DType _typeRec;

        // Number of columns and rows.
        private int _ccol;
        private int _crow;

        /// <summary>
        /// Maps from column name to column index.
        /// REVIEW: Should we use a red-black tree for this so it can be shared? Building a dictionary
        /// for each clip might get expensive. On the other hand, we may find that clips end up with a small
        /// subset of columns, in which case building a dictionary vs new red-black tree is a wash.
        /// </summary>
        private Dictionary<DName, int> _nameToCol;

        // These are parallel arrays and contain the name and TypeInfo of each column in order.
        private Immutable.Array<DName> _colToName;
        private Immutable.Array<TypeInfo> _colToTin;

        /// <summary>
        /// These arrays are the (private) values for the columns. This is indexed by col, just as <see cref="_colToName"/>
        /// and <see cref="_colToTin"/> are. That is, this, <see cref="_colToName"/> and <see cref="_colToTin"/>
        /// are parallel arrays. Each value array is indexed by "slot". For columns with <see cref="TypeInfo.NeedFlag"/>
        /// set to true, the column is also assigned a bit in <see cref="_valFlags"/>. See <see cref="_colToFlagInfo"/>.
        /// </summary>
        private Array[] _values;

        /// <summary>
        /// The number of slots in the smallest of the arrays:
        /// * <see cref="_rowToSlot"/>
        /// * All the items in <see cref="_values"/>
        /// * All the items in <see cref="_valFlags"/>
        /// * All the <see cref="Cache{TRec}._recs"/> arrays
        /// Typically the arrays all have the same number of slots, but they may be different sizes
        /// if resizing fails on one of the arrays after having succeeded on others. This value isn't
        /// updated until all resizes succeed.
        /// </summary>
        private int _numAlloced;

        /// <summary>
        /// This ctor is for: writable or readonly, empty, with given type and capacity.
        /// </summary>
        private GridConfigImpl(TypeManager tm, DType typeRec, int capacity, bool isClip)
            : base(Guid.NewGuid())
        {
            // REVIEW: Is there any reason to support RecordOpt?
            Validation.Assert(typeRec.IsRecordReq);
            Validation.Assert(capacity >= 0);
            Validation.AssertValue(tm);

            _typeRec = typeRec;
            Validation.Assert(_typeRec.IsRecordReq);

            _tm = tm;

            // Clips don't need to track free slots.
            _isClip = isClip;
            _slotsFree = isClip ? null : new IntHeap();

            _crow = 0;
            _ccol = _typeRec.FieldCount;
            _rowToSlot = new int[capacity];
            _slotLim = 0;

            _nameToCol = new Dictionary<DName, int>();
            _values = new Array[_ccol];
            _valFlags = Array.Empty<Flag[]>();

            var bldrNames = Immutable.Array.CreateBuilder<DName>(_ccol, init: true);
            var bldrTins = Immutable.Array.CreateBuilder<TypeInfo>(_ccol, init: true);
            var bldrFlag = Immutable.Array.CreateBuilder<(int grp, Flag mask)>(_ccol, init: true);
            int grp = -1;
            Flag mask = 0;
            int col = 0;
            foreach (var tn in _typeRec.GetNames())
            {
                Validation.Assert(col < _ccol);
                TryGetTypeInfo(tn.Type, out var tin, _tm).Verify();
                _nameToCol.Add(tn.Name, col);
                bldrNames[col] = tn.Name;
                bldrTins[col] = tin;
                _values[col] = tin.CreatePriArray(capacity);
                if (tin.NeedFlag)
                {
                    mask = (Flag)((uint)mask << 1);
                    if (mask == 0)
                    {
                        mask = Flag.F0;
                        grp++;
                        Validation.Assert(_valFlags.Length == grp);
                        Array.Resize(ref _valFlags, grp + 1);
                        Validation.Assert(_valFlags[grp] == null);
                        _valFlags[grp] = new Flag[capacity];
                    }
                    bldrFlag[col] = (grp, mask);
                }
                col++;
            }
            Validation.Assert(col == _ccol);
            _colToName = bldrNames.ToImmutable();
            _colToTin = bldrTins.ToImmutable();
            _colToFlagInfo = bldrFlag.ToImmutable();
            _numAlloced = capacity;

            AssertValid(!isClip);
        }

        /// <summary>
        /// This ctor is for: clip, copy from writable, indicated rows, all columns.
        /// This is NOT optimized for multiple instances of the same row.
        /// </summary>
        private GridConfigImpl(GridConfigImpl src, in SlotInfo slinSrc)
            : base(Guid.NewGuid())
        {
            Validation.AssertValue(src);
            src.AssertValid(true);

            _tm = src._tm;

            _isClip = true;
            _slotsFree = null;

            _typeRec = src._typeRec;

            _crow = slinSrc.Count;
            int capacity = _crow;
            _rowToSlot = new int[capacity];
            for (int row = 0; row < _crow; row++)
                _rowToSlot[row] = row;
            _slotLim = _crow;

            _ccol = src._ccol;
            _nameToCol = new Dictionary<DName, int>(_ccol);
            _colToName = src._colToName;
            _colToTin = src._colToTin;
            _colToFlagInfo = src._colToFlagInfo;
            _values = new Array[_ccol];

            // Copy over value flags.
            // REVIEW: Should we allocate mask assignments from scratch or just use those
            // from src? If we allocate, the result might be smaller. However, then each column
            // bit needs to be copied individually, rather than 8 at a time.
            _valFlags = src._valFlags.Length > 0 ? new Flag[src._valFlags.Length][] : src._valFlags;
            for (int grp = 0; grp < _valFlags.Length; grp++)
            {
                Validation.Assert(_valFlags[grp] == null);
                var flagsDst = _valFlags[grp] = new Flag[capacity];
                slinSrc.CopyValuesSrc(src._valFlags[grp], flagsDst);
            }

            for (int col = 0; col < _ccol; col++)
            {
                _nameToCol.Add(_colToName[col], col);
                _values[col] = _colToTin[col].CreatePriArray(capacity, src._values[col], in slinSrc);
            }

            _numAlloced = capacity;

            AssertValid(false);
        }

        /// <summary>
        /// This ctor is for: clip, copy from writable, indicated rows, mapped columns.
        /// </summary>
        private GridConfigImpl(GridConfigImpl src, in SlotInfo slinSrc, FieldMap fieldMap)
            : this(src.VerifyValue()._tm, src.VerifyValue().GetMappedType(fieldMap), slinSrc.Count, isClip: true)
        {
            src.AssertValid(true);
            Validation.Assert(!fieldMap.IsDefault);
            Validation.Assert(_typeRec.FieldCount == fieldMap.Count);
            Validation.Assert(_ccol == _typeRec.FieldCount);

            _crow = slinSrc.Count;
            int capacity = _crow;
            for (int row = 0; row < _crow; row++)
                _rowToSlot[row] = row;
            _slotLim = _crow;

            var slinDst = GetSlotInfo(0, _crow);

            // Copy over values and flags.
            for (int colDst = 0; colDst < _ccol; colDst++)
            {
                var nameDst = _colToName[colDst];
                var nameSrc = fieldMap[nameDst];
                int colSrc = src._nameToCol[nameSrc];
                var tin = _colToTin[colDst];
                Validation.Assert(src._colToTin[colSrc] == tin);
                tin.CopyValues(src._values[colSrc], in slinSrc, _values[colDst], in slinDst);
                if (tin.NeedFlag)
                    GetFlagInfoWrt(colDst).CopyFrom(src.GetFlagInfoRdo(colSrc), in slinSrc, in slinDst);
            }

            _numAlloced = capacity;

            AssertValid(false);
        }

        /// <summary>
        /// This ctor is for: clip, share info with readonly src, indicated rows, mapped columns.
        /// The shared structure includes the values arrays, the flags arrays, the column information
        /// (if there is no field map) and the <see cref="_rowToSlot"/> array (if <paramref name="rowToSlot"/>
        /// is equal to it).
        /// </summary>
        private GridConfigImpl(GridConfigImpl src, int[] rowToSlot, int rowLim, FieldMap fieldMap)
            : base(Guid.NewGuid())
        {
            Validation.AssertValue(src);
            Validation.Assert(src._isClip);
            src.AssertValid(false);
            Validation.AssertValue(rowToSlot);
            Validation.AssertIndexInclusive(rowLim, rowToSlot.Length);
            Validation.Assert(rowToSlot != src._rowToSlot | rowLim <= src._crow);

            // Don't create a new one if it is identical!
            Validation.Assert(!fieldMap.IsDefault | rowToSlot != src._rowToSlot | rowLim != src._crow);

            _tm = src._tm;

            _isClip = true;
            _slotsFree = null;

            _slotLim = src._slotLim;
            _valFlags = src._valFlags;
            _crow = rowLim;
            _rowToSlot = rowToSlot;

            if (fieldMap.IsDefault)
            {
                _typeRec = src._typeRec;
                _ccol = src._ccol;
                _values = src._values;
                _nameToCol = src._nameToCol;
                _colToName = src._colToName;
                _colToTin = src._colToTin;
                _colToFlagInfo = src._colToFlagInfo;
            }
            else
            {
                _typeRec = src.GetMappedType(fieldMap);
                _ccol = _typeRec.FieldCount;
                _values = new Array[_ccol];

                _nameToCol = new Dictionary<DName, int>();
                var bldrNames = Immutable.Array.CreateBuilder<DName>(_ccol, init: true);
                var bldrTins = Immutable.Array.CreateBuilder<TypeInfo>(_ccol, init: true);
                var bldrFlag = Immutable.Array.CreateBuilder<(int grp, Flag mask)>(_ccol, init: true);
                foreach (var kvp in fieldMap)
                {
                    int col = _nameToCol.Count;
                    Validation.Assert(col < _ccol);
                    _nameToCol.Add(kvp.Key, col);
                    bldrNames[col] = kvp.Key;
                    src._nameToCol.TryGetValue(kvp.Value, out int colSrc).Verify();
                    bldrTins[col] = src._colToTin[colSrc];
                    bldrFlag[col] = src._colToFlagInfo[colSrc];
                    _values[col] = src._values[colSrc];
                }
                _colToName = bldrNames.ToImmutable();
                _colToTin = bldrTins.ToImmutable();
                _colToFlagInfo = bldrFlag.ToImmutable();
            }

            _numAlloced = src._slotLim;

            AssertValid(false);
        }

        private GridConfigImpl(GridConfigImpl src)
            : base(Guid.NewGuid())
        {
            _tm = src._tm;
            _isClip = src._isClip;
            _guid = src._guid;
            _typeRec = src._typeRec;
            _ccol = src._ccol;
            _crow = src._crow;
            _nameToCol = new Dictionary<DName, int>(src._nameToCol);

            // Immutable arrays, so don't deep copy.
            _colToName = src._colToName;
            _colToTin = src._colToTin;
            _colToFlagInfo = src._colToFlagInfo;

            if (src._values != null)
            {
                _values = new Array[src._values.Length];
                for (int i = 0; i < src._values.Length; i++)
                {
                    _values[i] = (Array)src._values[i]?.Clone();
                }
            }
            _numAlloced = src._numAlloced;

            _valFlags = new Flag[src._valFlags.Length][];
            for (int i = 0; i < src._valFlags.Length; i++)
            {
                _valFlags[i] = (Flag[])src._valFlags[i]?.Clone();
            }

            _rowToSlot = (int[])src._rowToSlot?.Clone();
            _slotLim = src._slotLim;
            if (src._slotsFree != null)
                _slotsFree = new IntHeap(src._slotsFree);
            _cache = src._cache?.Clone(_tm, this);
        }

        private DType GetMappedType(FieldMap fieldMap)
        {
            AssertValid(false);
            Validation.Assert(!fieldMap.IsDefault);

            var typeRec = DType.EmptyRecordReq;
            foreach (var kvp in fieldMap)
            {
                Validation.BugCheckParam(kvp.Key.IsValid, nameof(fieldMap));
                Validation.BugCheckParam(_typeRec.TryGetNameType(kvp.Value, out var typeFld), nameof(fieldMap));
                typeRec = typeRec.AddNameType(kvp.Key, typeFld);
            }
            return typeRec;
        }

        /// <summary>
        /// Create a new <see cref="GridConfigImpl"/> with the given record type, but no rows.
        /// </summary>
        public static GridConfigImpl Create(TypeManager tm, DType typeRec, int capacity = 10)
        {
            Validation.AssertValue(tm);
            // REVIEW: Is there any reason to support RecordOpt?
            Validation.Assert(typeRec.IsRecordReq);
            Validation.Assert(capacity > 0);

            return new GridConfigImpl(tm, typeRec, capacity, isClip: false);
        }

        public GridConfigImpl Clone()
        {
            return new GridConfigImpl(this);
        }

        /// <summary>
        /// Create a sub-clip of this clip. Asserts that "this" is a clip.
        /// </summary>
        public GridConfigImpl CreateSubClip(FieldMap fieldMap, RowMap rowMap)
        {
            Validation.Assert(_isClip);
            AssertValid(false);

            // Get the rowToSlot to use for the clip.
            var (rowToSlot, min, lim) = GetSlotArray(rowMap, forceZeroMin: true);
            Validation.Assert(min == 0);

            // If nothing is changing, just use "this".
            if (fieldMap.IsDefault && rowToSlot == _rowToSlot && lim == _crow)
                return this;

            return new GridConfigImpl(this, rowToSlot, lim, fieldMap);
        }

        /// <summary>
        /// Create a clipping from the indicated rows and fields. Note that this copies the data,
        /// so the resulting clipping will not see any subsequent changes to this <see cref="GridConfigImpl"/>.
        /// </summary>
        public override Clip CreateClip(int rowMin, int rowLim, FieldMap fieldMap = default)
        {
            Validation.Assert(!_isClip);
            AssertValid(true);
            var slinSrc = GetSlotInfo(rowMin, rowLim);
            var config = fieldMap.IsDefault ? new GridConfigImpl(this, in slinSrc) : new GridConfigImpl(this, in slinSrc, fieldMap);
            return ClipCore.Wrap(config);
        }

        /// <summary>
        /// Create a clipping from the indicated rows and fields. Note that this copies the data,
        /// so the resulting clipping will not see any subsequent changes to this <see cref="GridConfigImpl"/>.
        /// </summary>
        public override Clip CreateClip(FieldMap fieldMap = default, RowMap rowMap = default)
        {
            Validation.Assert(!_isClip);
            AssertValid(true);
            var slinSrc = GetSlotInfo(rowMap);
            var config = fieldMap.IsDefault ? new GridConfigImpl(this, in slinSrc) : new GridConfigImpl(this, in slinSrc, fieldMap);
            return ClipCore.Wrap(config);
        }

        [Conditional("DEBUG")]
        private void AssertValid(bool changeable)
        {
#if DEBUG
            Validation.Assert(!_isClip || !changeable);
            Validation.Assert(!_colToTin.IsDefault);
            Validation.Assert(!_colToFlagInfo.IsDefault);
            Validation.Assert(!_colToName.IsDefault);
            Validation.Assert(_nameToCol != null);
            Validation.Assert(_valFlags != null);
            Validation.Assert(_isClip || _slotsFree != null);

            // Validate basic column counts.
            Validation.Assert(_typeRec.IsRecordReq);
            Validation.Assert(_ccol == _typeRec.FieldCount);
            Validation.Assert(_nameToCol.Count == _ccol);
            Validation.Assert(_colToName.Length == _ccol);
            Validation.Assert(_colToTin.Length == _ccol);
            Validation.Assert(_colToFlagInfo.Length == _ccol);

            // Validate basic row counts.
            Validation.Assert(_crow >= 0);
            Validation.Assert(_numAlloced >= _slotLim);
            if (_isClip)
            {
                // A clip can have multiple rows sharing the same slot, so _crow might be
                // larger than _slotLim.
                Validation.Assert(_rowToSlot.Length >= _crow);
            }
            else
            {
                Validation.Assert(_slotLim >= _crow);
                Validation.Assert(_rowToSlot.Length >= _numAlloced);
                Validation.Assert(_slotsFree.Count + _crow == _slotLim);
            }

            // Validate detailed row/slot information.
            var slotMarked = new bool[_slotLim];
            for (int row = 0; row < _crow; row++)
            {
                int slot = _rowToSlot[row];
                Validation.AssertIndex(slot, _slotLim);
                Validation.Assert(!slotMarked[slot] | _isClip);
                slotMarked[slot] = true;
            }

            // Validate detailed column information.
            var slinAll = GetSlotInfo(0, _crow);
            var flagUsed = _valFlags.Length > 0 ? new Flag[_valFlags.Length] : null;
            for (int col = 0; col < _ccol; col++)
            {
                var name = _colToName[col];
                Validation.Assert(name.IsValid);
                Validation.Assert(_nameToCol.TryGetValue(name, out int colTmp) && colTmp == col);

                var tin = _colToTin[col];
                Validation.Assert(tin != null);
                Validation.Assert(tin.Type == _typeRec.GetNameTypeOrDefault(name));
                Validation.Assert(_values[col] != null);
                Validation.Assert(_values[col].Length >= _numAlloced);
                Validation.Assert(_values[col].GetType() == tin.SysTypePri.MakeArrayType());
                Validation.Assert(tin.IsDefaultPriTail(_values[col], _slotLim));

                (int grp, var mask) = _colToFlagInfo[col];
                if (!tin.NeedFlag)
                {
                    Validation.Assert(mask == 0);
                    Validation.Assert(grp == 0);
                }
                else
                {
                    // Get a flag info. It does a bunch of asserts for us.
                    var flin = GetFlagInfoRdo(col);
                    Validation.Assert(!flin.IsBlank);
                    Validation.Assert(flin.Length >= _numAlloced);

                    // Verify that nulls have default in the values array.
                    Validation.Assert(tin.IsNullDefaultPri(_values[col], in slinAll, in flin));

                    // Mark the flag as being used.
                    Validation.AssertIndex(grp, Util.Size(flagUsed));
                    Validation.Assert((flagUsed[grp] & mask) == 0 | _isClip);
                    flagUsed[grp] |= mask;
                }
            }

            for (int grp = 0; grp < _valFlags.Length; grp++)
            {
                var flags = _valFlags[grp];
                Validation.Assert(flags != null);
                Validation.Assert(flags.Length >= _numAlloced);
                if (!_isClip)
                {
                    for (int slot = _slotLim; slot < flags.Length; slot++)
                        Validation.Assert(flags[slot] == 0);
                    Validation.Assert(!slinAll.TestAny(flags, ~flagUsed[grp]));
                }
            }

            if (!_isClip && _slotsFree.Count > 0)
            {
                var slots = new int[_slotsFree.Count];
                int islot = 0;
                foreach (int slot in _slotsFree.GetItems())
                {
                    Validation.AssertIndex(slot, _slotLim);
                    Validation.Assert(!slotMarked[slot]);
                    slotMarked[slot] = true;
                    slots[islot++] = slot;
                }
                Validation.Assert(islot == slots.Length);

                // Verify that free slots are blank.
                var slin = new SlotInfo(slots, 0, slots.Length);
                for (int col = 0; col < _ccol; col++)
                {
                    var tin = _colToTin[col];
                    Validation.Assert(tin.IsDefaultPri(_values[col], in slin));
                }

                for (int grp = 0; grp < _valFlags.Length; grp++)
                    Validation.Assert(!slin.TestAny(_valFlags[grp], ~default(Flag)));
            }
#endif
        }

        public override int ColCount => _ccol;
        public override int RowCount => _crow;
        public override DType RecordType => _typeRec;

        public override int GetColIndex(DName name)
        {
            AssertValid(false);
            Validation.BugCheck(_nameToCol.TryGetValue(name, out int col));
            Validation.AssertIndex(col, _ccol);
            return col;
        }

        public override DName GetColName(int col)
        {
            AssertValid(false);
            Validation.BugCheckIndex(col, _ccol, nameof(col));
            return _colToName[col];
        }

        public override DType GetColType(int col)
        {
            AssertValid(false);
            Validation.BugCheckIndex(col, _ccol, nameof(col));
            return _colToTin[col].Type;
        }

        public override Type GetColSysType(int col)
        {
            AssertValid(false);
            Validation.BugCheckIndex(col, _ccol, nameof(col));
            return _colToTin[col].SysTypePub;
        }

        public override object GetCellValue(int col, int row)
        {
            AssertValid(false);
            Validation.BugCheckIndex(row, _crow, nameof(row));
            Validation.BugCheckIndex(col, _ccol, nameof(col));

            var tin = _colToTin[col];
            int slot = _rowToSlot[row];
            if (tin.NeedFlag && !TestFlag(col, slot))
                return null;
            return tin.GetValueObject(_values[col], slot);
        }

        public override T GetCellValue<T>(int col, int row)
        {
            AssertValid(false);
            Validation.BugCheckIndex(row, _crow, nameof(row));
            Validation.BugCheckIndex(col, _ccol, nameof(col));
            Validation.BugCheckParam(_colToTin[col].TryCastPub<T>(out var tin), nameof(T));

            int slot = _rowToSlot[row];
            if (tin.NeedFlag && !TestFlag(col, slot))
                return default;
            return tin.GetValuePub(_values[col], slot);
        }

        // REVIEW: Is this needed or helpful in any way? In theory it could
        // dispatch more directly to specific tin types.
        public T? GetCellValueOpt<T>(int col, int row)
            where T : struct
        {
            return GetCellValue<T?>(col, row);
        }

        public object SetCellValue(int col, int row, object value)
        {
            AssertValid(true);

            Validation.BugCheckIndex(col, _ccol, nameof(col));
            Validation.BugCheckIndex(row, _crow, nameof(row));

            int slot = _rowToSlot[row];
            object valueOld = _colToTin[col].SetValueObject(value, _values[col], slot, GetFlagInfoWrt(col));
            DirtyRows(row, row + 1);
            AssertValid(true);
            return valueOld;
        }

        public T SetCellValue<T>(int col, int row, T value)
        {
            AssertValid(true);

            Validation.BugCheckIndex(col, _ccol, nameof(col));
            Validation.BugCheckIndex(row, _crow, nameof(row));

            int slot = _rowToSlot[row];
            T valueOld = _colToTin[col].SetValue<T>(value, _values[col], slot, GetFlagInfoWrt(col));
            DirtyRows(row, row + 1);
            AssertValid(true);
            return valueOld;
        }

        public Immutable.Array SetCellValues(int col, int rowMin, int rowLim, ReadOnly.Array values)
        {
            AssertValid(true);

            Validation.BugCheckIndex(col, _ccol, nameof(col));
            Validation.BugCheckIndexInclusive(rowLim, _crow, nameof(rowLim));
            Validation.BugCheckIndex(rowMin, rowLim, nameof(rowMin));
            Validation.BugCheckParam(values.Length >= rowLim - rowMin, nameof(values));

            var valuesOld = _colToTin[col].SetValues(values, _values[col], GetSlotInfo(rowMin, rowLim), GetFlagInfoWrt(col));
            DirtyRows(rowMin, rowLim);
            AssertValid(true);
            return valuesOld;
        }

        public Immutable.Array<T> SetCellValues<T>(int col, int rowMin, int rowLim, ReadOnly.Array<T> values)
        {
            AssertValid(true);

            Validation.BugCheckIndex(col, _ccol, nameof(col));
            Validation.BugCheckIndexInclusive(rowLim, _crow, nameof(rowLim));
            Validation.BugCheckIndex(rowMin, rowLim, nameof(rowMin));
            int count = rowLim - rowMin;
            Validation.BugCheckParam(values.Length >= count, nameof(values));

            var valuesOld = _colToTin[col].SetValues<T>(values, _values[col], GetSlotInfo(rowMin, rowLim), GetFlagInfoWrt(col));
            DirtyRows(rowMin, rowLim);
            AssertValid(true);
            return valuesOld;
        }

        /// <summary>
        /// Replace rows in the grid. If <paramref name="crowIns"/> is positive but <paramref name="clipIns"/>
        /// is null, the "inserted rows" are blank.
        /// </summary>
        public void ReplaceRows(int row, int crowDel, int crowIns, bool blankDel, GridConfig.Clip clipIns)
        {
            AssertValid(true);
            Validation.BugCheckIndexInclusive(crowDel, _crow, nameof(crowDel));
            Validation.BugCheckIndexInclusive(row, _crow - crowDel, nameof(row));
            Validation.BugCheckValueOrNull(clipIns);

            GridConfigImpl implIns;
            if (clipIns != null)
            {
                // REVIEW: honor field mapping!
                // New rows (if any) are copied from clipIns.
                // ClipCore is the only subclass of the abstract class Clip.
                Validation.Assert(clipIns is ClipCore);
                implIns = ((ClipCore)clipIns).GetImpl();
                Validation.BugCheckParam(implIns._crow >= crowIns, nameof(crowIns));
                Validation.BugCheckParam(implIns._typeRec == _typeRec, nameof(clipIns));
                Validation.Assert(implIns._ccol == _ccol);
                if (crowIns == 0)
                    implIns = null;
            }
            else
            {
                // New rows (if any) are blank.
                implIns = null;
                Validation.BugCheckParam(crowIns >= 0, nameof(crowIns));
            }

            if (crowDel == 0 && crowIns == 0)
                return;

            // Check for overflow.
            int delta = crowIns - crowDel;
            Validation.BugCheckParam(_crow + delta >= 0, nameof(crowIns));

            int rowLimOld = row + crowDel;
            int rowLimNew = row + crowIns;
            GridConfigImpl implDel = null;
            if (crowDel > 0)
            {
                if (!blankDel)
                    implDel = new GridConfigImpl(this, GetSlotInfo(row, rowLimOld));
#if DEBUG
                else
                {
                    // Verify that the rows are "blank".
                    var slin = GetSlotInfo(row, rowLimOld);
                    for (int col = 0; col < _ccol; col++)
                        Validation.Assert(_colToTin[col].IsDefaultPri(_values[col], in slin));
                    for (int grp = 0; grp < _valFlags.Length; grp++)
                        Validation.Assert(!slin.TestAny(_valFlags[grp], ~default(Flag)));
                }
#endif
            }

            // Make sure there is room.
            int numFree = _numAlloced - _crow;
            Validation.Assert(numFree >= 0);
            if (numFree < delta)
            {
                // Buffers are not large enough. Grow them.
                int capMin = _crow + delta;
                int cap = Util.GetCapTarget(_numAlloced, capMin);
                Util.Grow(ref _rowToSlot, ref cap, capMin);
                for (int col = 0; col < _ccol; col++)
                    _colToTin[col].GrowPriArray(ref _values[col], ref cap, capMin);
                for (int grp = 0; grp < _valFlags.Length; grp++)
                    Util.Grow(ref _valFlags[grp], ref cap, capMin);
                EnsureCacheSpace(ref cap, capMin);
                Validation.Assert(cap >= capMin);
                _numAlloced = cap;
                AssertValid(true);
            }

            if (row < rowLimOld)
                DirtyRows(row, rowLimOld);

            if (rowLimNew == 0 && rowLimOld == _crow)
            {
                // Deleting everything.
                Validation.Assert(row == 0);
                Validation.Assert(crowIns == 0);
                Validation.Assert(crowDel == _crow);
                if (!blankDel)
                    ClearSlots(0, _crow);
                _slotsFree.Clear();
                _slotLim = 0;
                _crow = 0;
            }
            else
            {
                // See if we need to blank some "over-written" rows, that is, rows that are
                // to be "deleted" and re-inserted as blanks.
                bool blankIns = implIns == null && !blankDel && crowIns > 0 && crowDel > 0;
                if (blankIns)
                    ClearSlots(row, rowLimOld);

                // Free the slots in [rowLimNew, rowLimOld), which is empty unless delta < 0.
                if (delta < 0)
                {
                    // No need to clear if the deleted rows are already blank or if already done above.
                    if (!blankDel && !blankIns)
                        ClearSlots(rowLimNew, rowLimOld);
                    FreeSlots(rowLimNew, rowLimOld);
                }

                // Slide the tail.
                if (rowLimOld < _crow)
                    Array.Copy(_rowToSlot, rowLimOld, _rowToSlot, rowLimNew, _crow - rowLimOld);

                // Allocate slots in [rowLimOld, rowLimNew), which is empty unless delta > 0.
                // Note that allocated slots are guaranteed to be blank, so no need to clear them
                // explicitly.
                for (int rowCur = rowLimOld; rowCur < rowLimNew; rowCur++)
                {
                    if (!_slotsFree.TryPop(out int slot))
                        slot = _slotLim++;
                    Validation.AssertIndex(slot, _slotLim);
                    _rowToSlot[rowCur] = slot;
                }

                // Adjust the row count.
                _crow += delta;

                if (implIns != null)
                {
                    // Copy values.
                    Validation.Assert(crowIns > 0);
                    for (int colDst = 0; colDst < _ccol; colDst++)
                    {
                        var name = _colToName[colDst];
                        Validation.Assert(implIns._nameToCol.ContainsKey(name));
                        int colSrc = implIns._nameToCol[name];
                        Validation.Assert(implIns._colToTin[colSrc] == _colToTin[colDst]);
                        var tin = _colToTin[colDst];
                        var slinSrc = implIns.GetSlotInfo(0, crowIns);
                        var slinDst = GetSlotInfo(row, row + crowIns);
                        tin.CopyValues(implIns._values[colSrc], in slinSrc, _values[colDst], in slinDst);
                        if (tin.NeedFlag)
                            GetFlagInfoWrt(colDst).CopyFrom(implIns.GetFlagInfoRdo(colSrc), in slinSrc, in slinDst);
                    }
                }
            }

            AssertValid(true);
        }

        /// <summary>
        /// Delete the indicated rows.
        /// </summary>
        public void DeleteRows(int rowMin, int rowLim)
        {
            ReplaceRows(rowMin, rowLim - rowMin, 0, false, null);
        }

        private void ClearSlots(int rowMin, int rowLim)
        {
            Validation.AssertIndexInclusive(rowLim, _crow);
            Validation.AssertIndexInclusive(rowMin, rowLim);

            if (rowMin >= rowLim)
                return;

            // Clear the values.
            var slin = GetSlotInfo(rowMin, rowLim);
            for (int col = 0; col < _ccol; col++)
                _colToTin[col].ClearValues(_values[col], in slin);
            for (int grp = 0; grp < _valFlags.Length; grp++)
                slin.ClearValues(_valFlags[grp]);
        }

        private void FreeSlots(int rowMin, int rowLim)
        {
            Validation.Assert(!_isClip);
            Validation.AssertIndexInclusive(rowLim, _crow);
            Validation.AssertIndex(rowMin, rowLim);

            // Release the slots.
            for (int rowCur = rowLim; --rowCur >= rowMin;)
            {
                int slot = _rowToSlot[rowCur];
                Validation.AssertIndex(slot, _slotLim);
                if (slot == _slotLim - 1)
                    _slotLim = slot;
                else
                    _slotsFree.Add(slot);
            }
        }

        public void InsertColumn(DName name, DType type, int col, ReadOnly.Array src)
        {
            AssertValid(true);
            Validation.BugCheckParam(name.IsValid, nameof(name));
            Validation.BugCheckParam(!_nameToCol.ContainsKey(name), nameof(name));
            Validation.BugCheckParam(TryGetTypeInfo(type, out var tin, _tm), nameof(type));
            Validation.BugCheckIndexInclusive(col, _ccol, nameof(col));
            Validation.BugCheckParam(src.IsDefault || tin.IsGoodPub(src), nameof(src));
            Validation.BugCheckParam(src.IsDefault || src.Length >= _crow, nameof(src));

            Validation.Assert(_values.Length == _ccol);
            Validation.Assert(_colToName.Length == _ccol);
            Validation.Assert(_colToTin.Length == _ccol);
            Validation.Assert(_nameToCol.Count == _ccol);
            Validation.Assert(_typeRec.FieldCount == _ccol);

            // Insert items into the column indexed arrays.
            var typeRec = _typeRec.AddNameType(name, type);
            var colToName = _colToName.Insert(col, name);
            var colToTin = _colToTin.Insert(col, tin);
            var colToFlagInfo = _colToFlagInfo.Insert(col, tin.NeedFlag ? AllocFlagBit() : default);

            var values = new Array[_ccol + 1];
            if (col > 0)
                Array.Copy(_values, values, col);
            if (_ccol > col)
                Array.Copy(_values, col, values, col + 1, _ccol - col);
            values[col] = tin.CreatePriArray(_numAlloced);

            // Whether we're using default values.
            bool blank = src.IsDefault || _crow == 0;

            // This is the one thing that might throw (possible oom) - do it first.
            _nameToCol.Add(name, col);

            // Adjust the name to column mapping.
            for (int c = col + 1; c < colToName.Length; c++)
            {
                var n = colToName[c];
                Validation.Assert(_nameToCol.ContainsKey(n));
                Validation.Assert(_nameToCol[n] == c - 1);
                _nameToCol[n] = c;
            }

            _colToName = colToName;
            _colToTin = colToTin;
            _colToFlagInfo = colToFlagInfo;
            _values = values;
            _ccol += 1;
            _typeRec = typeRec;

            // Copy the values, if needed. This shouldn't throw, since we did the src type testing above.
            if (!blank)
                tin.CopyValues(src, _values[col], GetSlotInfo(0, _crow), GetFlagInfoWrt(col));

            DirtyRows(0, _crow);

            AssertValid(true);
        }

        public void DeleteColumn(int col)
        {
            AssertValid(true);
            Validation.BugCheckIndex(col, _ccol, nameof(col)); ;

            var name = _colToName[col];
            var colToName = _colToName.RemoveAt(col);
            var colToTin = _colToTin.RemoveAt(col);
            var colToFlagInfo = _colToFlagInfo.RemoveAt(col);
            var values = new Array[_ccol - 1];
            if (col > 0)
                Array.Copy(_values, values, col);
            if (values.Length > col)
                Array.Copy(_values, col + 1, values, col, values.Length - col);
            var typeRec = _typeRec.DropName(name);

            var tin = _colToTin[col];

            Array old = null;
            var slin = GetSlotInfo(0, _crow);
            var flin = GetFlagInfoWrt(col);
            // Can't use "in" since flin is being converted to read-only.
            old = tin.CreatePubArray(_crow, _values[col], in slin, /*in*/ flin);
            flin.Clear(in slin);

            _nameToCol.Remove(name);

            // Adjust the name to column mapping.
            for (int c = col; c < colToName.Length; c++)
            {
                var n = colToName[c];
                Validation.Assert(_nameToCol.ContainsKey(n));
                Validation.Assert(_nameToCol[n] == c + 1);
                _nameToCol[n] = c;
            }

            _colToName = colToName;
            _colToTin = colToTin;
            _colToFlagInfo = colToFlagInfo;
            _values = values;
            _ccol -= 1;
            _typeRec = typeRec;
            DirtyRows(0, _crow);

            AssertValid(true);
        }

        public void ConvertColumn(int col, DType typeNew)
        {
            AssertValid(true);
            Validation.BugCheckIndex(col, _ccol, nameof(col));

            var tinCur = _colToTin[col];
            if (typeNew == tinCur.Type)
                return;

            Validation.BugCheckParam(typeNew.Accepts(tinCur.Type, DType.UseUnionDefault), nameof(typeNew));
            Validation.BugCheckParam(TryGetTypeInfo(typeNew, out var tinNew, _tm), nameof(typeNew));

            // Get new column indexed arrays.
            var name = _colToName[col];
            var typeRec = _typeRec.SetNameType(name, typeNew);
            var colToTin = _colToTin.SetItem(col, tinNew);

            // REVIEW: This assumes a lot about how type infos are implemented. Clean this up.
            Validation.Assert(!tinCur.NeedFlag || tinNew.NeedFlag);
            bool setFlags = tinNew.NeedFlag && !tinCur.NeedFlag;

            Array valuesOld;
            Array valuesNew;
            if (setFlags && tinCur.SysTypePri == tinNew.SysTypePri)
            {
                // This happens when going from T to T? with T? using a flag to indicate opt-ness.
                Validation.Assert(tinCur.Type.ToOpt() == typeNew);
                Validation.Assert(!tinCur.SpecDefault);
                Validation.Assert(!tinNew.SpecDefault);
                valuesNew = _values[col];
                valuesOld = null;
            }
            else
            {
                // Need to map the values. First try to get the converter.
                Validation.BugCheckParam(TryGetConverter(tinCur, tinNew, out var conv), nameof(typeNew));
                valuesNew = tinNew.CreatePriArray(_numAlloced);
                valuesOld = tinCur.CreatePriArray(_crow);
                conv.MapValues(_values[col], valuesOld, 0, valuesNew, GetSlotInfo(0, _crow));
            }

            var colToFlagInfo = setFlags ? _colToFlagInfo.SetItem(col, AllocFlagBit()) : _colToFlagInfo;

            _colToTin = colToTin;
            _colToFlagInfo = colToFlagInfo;
            _values[col] = valuesNew;
            _typeRec = typeRec;

            if (setFlags)
                GetFlagInfoWrt(col).Set(GetSlotInfo(0, _crow));

            DirtyRows(0, _crow);

            AssertValid(true);
        }

        /// <summary>
        /// Performs a "paste" operation that converts the record type of this grid to be <paramref name="typeRec"/>,
        /// deletes <paramref name="crowDel"/> rows and inserts rows from <paramref name="clipIns"/> (if not null)
        /// at the given <paramref name="row"/>. Both the current item type and the item type of <paramref name="clipIns"/>
        /// must be accepted by <paramref name="typeRec"/>.
        /// </summary>
        public bool PasteRows(int row, int crowDel, DType typeRec, DataClip clipIns, DName defaultColName = default)
        {
            AssertValid(true);
            Validation.BugCheckIndexInclusive(crowDel, _crow, nameof(crowDel));
            Validation.BugCheckIndexInclusive(row, _crow - crowDel, nameof(row));
            Validation.BugCheckParam(GridConfig.IsValidGridType(typeRec.ToSequence(), _tm), nameof(typeRec));
            Validation.BugCheckParam(typeRec.Accepts(_typeRec, union: true), nameof(typeRec));

            // REVIEW: Support pasting a subset of the columns in clip.
            DType typeIns;
            if (clipIns == null)
                typeIns = DType.EmptyRecordReq;
            else
            {
                typeIns = clipIns.ClipItemType;
                if (!clipIns.HasFields)
                {
                    Validation.BugCheckParam(defaultColName.IsValid, nameof(defaultColName));
                    typeIns = DType.EmptyRecordReq.AddNameType(defaultColName, typeIns);
                }

                Validation.BugCheckParam(typeRec.Accepts(typeIns, union: true), nameof(clipIns));
            }

            int crowIns = 0;
            GridConfigImpl implIns = null;
            DataValueClip dvcIns = null;
            if (clipIns != null)
            {
                // REVIEW: honor field mapping!
                if (clipIns is ClipCore core)
                {
                    implIns = core.GetImpl();
                    Validation.Assert(typeIns == implIns.RecordType);
                    crowIns = implIns.RowCount;
                    if (crowIns == 0)
                        implIns = null;
                }
                else if ((dvcIns = clipIns as DataValueClip) != null &&
                    typeof(DataValueClip<>).MakeGenericType(dvcIns.RawItemSysType).IsAssignableFrom(dvcIns.GetType()))
                {
                    // The clip is an instance of DataValueClip<TRec> where TRec is the item system type.
                    crowIns = (int)dvcIns.GetCount(() => { });
                    if (crowIns == 0)
                        dvcIns = null;
                }
                else
                {
                    // REVIEW: Are there other kinds of clips that we'll want to handle?
                    throw Validation.BugExceptParam(nameof(clipIns), "Unsupported clip type");
                }
            }

            if (crowDel == 0 && crowIns == 0 && typeRec == _typeRec)
                return false;

            // Check for overflow.
            int delta = crowIns - crowDel;
            Validation.BugCheckParam(_crow + delta >= 0, nameof(crowIns));

            int rowLimOld = row + crowDel;
            int rowLimNew = row + crowIns;
            GridConfigImpl implDel = null;
            if (crowDel > 0)
                implDel = new GridConfigImpl(this, GetSlotInfo(row, rowLimOld));

            // Make sure there is room.
            int capMin = _numAlloced;
            int cap = capMin;
            Validation.Assert(cap >= _crow);
            if (cap - _crow < delta)
            {
                // Buffers are not large enough. Grow them.
                capMin = _crow + delta;
                cap = Util.GetCapTarget(cap, capMin);
                Util.Grow(ref _rowToSlot, ref cap, capMin);
                for (int grp = 0; grp < _valFlags.Length; grp++)
                    Util.Grow(ref _valFlags[grp], ref cap, capMin);
                EnsureCacheSpace(ref cap, capMin);
                Validation.Assert(cap >= capMin);
            }

            var typeOld = _typeRec;
            bool typeChange = typeRec != typeOld;
            var colToTin = _colToTin;
            var colToFlagInfo = _colToFlagInfo;
            var colToName = _colToName;
            var nameToCol = _nameToCol;
            var values = _values;
            Array[] valuesOld = null;
            // This is used to record flags that should be set for existing columns being promoted
            // from non-opt to opt. The flags will be set in bulk.
            Flag[] masksSet = null;
            int ccol = typeRec.FieldCount;
            Validation.Assert(ccol >= _ccol);
            if (typeChange)
            {
                // Some column types changed and/or columns were added.
                var colToTinBldr = _colToTin.ToBuilder();
                var colToFlagInfoBldr = ccol > _ccol ? _colToFlagInfo.ToBuilder() : null;
                var colToNameBldr = ccol > _ccol ? _colToName.ToBuilder() : null;
                if (ccol > _ccol)
                    nameToCol = new Dictionary<DName, int>(nameToCol);

                values = new Array[ccol];
                valuesOld = new Array[_ccol];
                masksSet = _valFlags.Length > 0 ? new Flag[_valFlags.Length] : null;
                foreach (var tn in typeRec.GetNames())
                {
                    Validation.Assert(_typeRec.Contains(tn.Name) == _nameToCol.ContainsKey(tn.Name));
                    TryGetTypeInfo(tn.Type, out var tinDst, _tm).Verify();
                    Validation.Assert(tinDst.Type == tn.Type);
                    TypeInfo tinSrc;

                    if (!_nameToCol.TryGetValue(tn.Name, out int col))
                    {
                        // New column.
                        Validation.Assert(ccol > _ccol);
                        col = colToTinBldr.Count;
                        Validation.Assert(col < ccol);
                        colToTinBldr.Add(tinDst);
                        colToNameBldr.Add(tn.Name);
                        nameToCol.Add(tn.Name, col);
                        colToFlagInfoBldr.Add(tinDst.NeedFlag ? AllocFlagBit(colToFlagInfoBldr, cap) : default);
                        tinDst.GrowPriArray(ref values[col], ref cap, capMin);
                    }
                    else if ((tinSrc = _colToTin[col]) != tinDst)
                    {
                        Validation.Assert(tinSrc.Type != tinDst.Type);
                        Validation.Assert(tinDst.Type.Accepts(tinSrc.Type, union: true));

                        Validation.Assert(tinDst.NeedFlag || !tinSrc.NeedFlag);
                        bool setFlags = tinDst.NeedFlag && !tinSrc.NeedFlag;
                        if (setFlags)
                        {
                            colToFlagInfoBldr ??= _colToFlagInfo.ToBuilder();
                            var (grp, mask) = colToFlagInfoBldr[col] = AllocFlagBit(colToFlagInfoBldr, cap);
                            if (grp >= Util.Size(masksSet))
                                masksSet = new Flag[grp + 1];
                            masksSet[grp] |= mask;
                        }

                        colToTinBldr[col] = tinDst;
                        if (setFlags && tinSrc.SysTypePri == tinDst.SysTypePri)
                        {
                            // This happens when going from T to T? with T? using a flag to indicate opt-ness,
                            // or T a "special" type like uri or tensor.
                            Validation.Assert(tinSrc.Type.ToOpt() == tinDst.Type);
                            Validation.Assert(!tinSrc.SpecDefault);
                            Validation.Assert(!tinDst.SpecDefault);
                            values[col] = _values[col];
                            tinDst.GrowPriArray(ref values[col], ref cap, capMin);
                        }
                        else
                        {
                            // Need to map the values.
                            tinDst.GrowPriArray(ref values[col], ref cap, capMin);
                            // REVIEW: When conv is lossless, could avoid allocating the array and instead apply "Unconv" to restore.
                            valuesOld[col] = tinSrc.CreatePriArray(_crow);
                            Validation.BugCheckParam(TryGetConverter(tinSrc, tinDst, out var conv), nameof(clipIns));
                            if (row > 0)
                                conv.MapValues(_values[col], valuesOld[col], 0, values[col], GetSlotInfo(0, row));
                            if (rowLimOld < _crow)
                                conv.MapValues(_values[col], valuesOld[col], row, values[col], GetSlotInfo(rowLimOld, _crow));
                        }
                    }
                    else
                    {
                        values[col] = _values[col];
                        tinDst.GrowPriArray(ref values[col], ref cap, capMin);
                    }
                }

                colToTin = colToTinBldr.ToImmutable();
                if (colToFlagInfoBldr != null)
                    colToFlagInfo = colToFlagInfoBldr.ToImmutable();
                if (colToNameBldr != null)
                    colToName = colToNameBldr.ToImmutable();
            }
            else if (cap > _numAlloced)
            {
                // Make sure there is room.
                for (int col = 0; col < _ccol; col++)
                    _colToTin[col].GrowPriArray(ref values[col], ref cap, capMin);
            }

            Validation.Assert(colToTin.Length == ccol);
            Validation.Assert(colToFlagInfo.Length == ccol);
            Validation.Assert(colToName.Length == ccol);
            Validation.Assert(nameToCol.Count == ccol);
            Validation.Assert(values.Length == ccol);
            Validation.Assert(valuesOld == null || valuesOld.Length == _ccol);
            Validation.Assert(cap >= _numAlloced);
            Validation.Assert(cap - _crow >= delta);

            Flag[] masksSetIns = null;
            if (crowIns > 0 && typeIns != typeRec)
            {
                // Assert that we have all the converters we need to handle clipIns. Also,
                // determine insert columns that are promoted from non-opt to opt. The flags
                // will be set in bulk.
                masksSetIns = _valFlags.Length > 0 ? new Flag[_valFlags.Length] : null;
                foreach (var tn in typeIns.GetNames())
                {
                    Validation.Assert(nameToCol.ContainsKey(tn.Name));
                    int colDst = nameToCol[tn.Name];
                    var tinDst = colToTin[colDst];
                    Validation.Assert(tinDst.Type.Accepts(tn.Type, union: true));
                    if (tinDst.Type == tn.Type)
                        continue;

                    TryGetTypeInfo(tn.Type, out var tinSrc, _tm).Verify();

                    // Verify that we've implemented the proper converter.
                    Validation.Assert(
                        tinDst.SysTypePri == tinSrc.SysTypePri ||
                        TryGetConverter(tinSrc, tinDst, out _));

                    if (tinDst.NeedFlag && !tinSrc.NeedFlag)
                    {
                        var (grp, mask) = colToFlagInfo[colDst];
                        Validation.Assert(mask != 0);
                        Validation.AssertIndex(grp, masksSetIns.Length);
                        Validation.Assert((masksSetIns[grp] & mask) == 0);
                        masksSetIns[grp] |= mask;
                    }
                }
            }

            if (!typeChange && row < rowLimOld)
                DirtyRows(row, rowLimOld);

            _colToTin = colToTin;
            _colToFlagInfo = colToFlagInfo;
            _colToName = colToName;
            _nameToCol = nameToCol;
            _values = values;
            _ccol = ccol;
            _typeRec = typeRec;
            _numAlloced = cap;

            if (rowLimNew == 0 && rowLimOld == _crow)
            {
                // Deleting everything.
                Validation.Assert(row == 0);
                Validation.Assert(crowIns == 0);
                Validation.Assert(crowDel == _crow);
                ClearSlots(0, _crow);
                _slotsFree.Clear();
                _slotLim = 0;
                _crow = 0;
            }
            else
            {
                if (crowDel > 0)
                {
                    if (masksSet != null)
                    {
                        // Set the flags for columns being promoted from non-opt to opt.
                        if (row > 0)
                            GetSlotInfo(0, row).SetFlags(_valFlags, masksSet);
                        if (rowLimOld < _crow)
                            GetSlotInfo(rowLimOld, _crow).SetFlags(_valFlags, masksSet);
                    }

                    // Free the slots in [rowLimNew, rowLimOld), which is empty unless delta < 0.
                    if (delta < 0)
                    {
                        ClearSlots(rowLimNew, rowLimOld);
                        FreeSlots(rowLimNew, rowLimOld);
                    }

                    // If there are columns that aren't in clipIns, they need cleared in [row, rowLimOld).
                    if (crowIns > 0 && typeIns.FieldCount < _ccol)
                    {
                        var slin = GetSlotInfo(row, delta < 0 ? rowLimNew : rowLimOld);
                        for (int colDst = 0; colDst < _ccol; colDst++)
                        {
                            var name = _colToName[colDst];
                            if (!typeIns.Contains(name))
                            {
                                _colToTin[colDst].ClearValues(_values[colDst], in slin);
                                GetFlagInfoWrt(colDst).Clear(in slin);
                            }
                        }
                    }
                }
                else if (masksSet != null)
                {
                    // Set the flags for columns being promoted from non-opt to opt.
                    GetSlotInfo(0, _crow).SetFlags(_valFlags, masksSet);
                }

                // Slide the tail.
                if (rowLimOld < _crow && delta != 0)
                    Array.Copy(_rowToSlot, rowLimOld, _rowToSlot, rowLimNew, _crow - rowLimOld);

                // Allocate slots in [rowLimOld, rowLimNew), which is empty unless delta > 0.
                // Note that allocated slots are guaranteed to be blank, so no need to clear them
                // explicitly.
                for (int rowCur = rowLimOld; rowCur < rowLimNew; rowCur++)
                {
                    if (!_slotsFree.TryPop(out int slot))
                        slot = _slotLim++;
                    Validation.AssertIndex(slot, _slotLim);
                    _rowToSlot[rowCur] = slot;
                }

                // Adjust the row count.
                _crow += delta;

                if (masksSetIns != null)
                    GetSlotInfo(row, rowLimNew).SetFlags(_valFlags, masksSetIns);

                if (implIns != null)
                {
                    // Copy/convert values.
                    Validation.Assert(crowIns > 0);
                    Validation.Assert(dvcIns == null);
                    ConvertValues(implIns, row, crowIns);
                }
                else if (dvcIns != null)
                {
                    // Copy/convert values.
                    Validation.Assert(crowIns > 0);
                    if (dvcIns.HasFields)
                        ConvertValues(dvcIns, row, crowIns);
                    else
                    {
                        Validation.Assert(typeIns.FieldCount == 1 && typeIns.Contains(defaultColName));
                        ConvertValues(dvcIns, row, crowIns, defaultColName);
                    }
                }
                else
                    Validation.Assert(crowIns == 0);
            }

            if (typeChange)
                DirtyRows(0, _crow);

            AssertValid(true);

            return true;
        }

        /// <summary>
        /// Performs a "paste" operation that converts the record type of this grid so it can
        /// accept the record type of <paramref name="clipIns"/>, deletes <paramref name="crowDel"/>
        /// rows and inserts rows from <paramref name="clipIns"/> at the given <paramref name="row"/>.
        /// If <paramref name="clipIns"/> is a clip of non record value, <paramref name="defaultColName"/>
        /// will be used as the default column name for the clipped values.
        /// Note that the <paramref name="defaultColName"/> can be an existing column name of this grid.
        /// 
        /// Returns true if any rows were pasted or removed.
        /// </summary>
        internal bool PasteRows(int row, int crowDel, DataClip clipIns, DName defaultColName = default)
        {
            Validation.BugCheckValue(clipIns, nameof(clipIns));
            Validation.BugCheckParam(clipIns.HasFields || defaultColName.IsValid, nameof(clipIns));
            var typeClipRec = clipIns.HasFields ?
                clipIns.ClipItemType :
                DType.EmptyRecordReq.AddNameType(defaultColName, clipIns.ClipItemType);
            var typeRec = DType.GetSuperType(RecordType, typeClipRec, union: true);
            return PasteRows(row, crowDel, typeRec, clipIns, defaultColName);
        }

        /// <summary>
        /// Copy and convert values from <paramref name="implIns"/> into this starting at <paramref name="row"/>.
        /// This is called by <see cref="PasteRows(int, int, DType, DataClip)"/>. This assumes that for columns
        /// going from !NeedFlag to NeedFlag, that flags are set elsewhere.
        /// </summary>
        private void ConvertValues(GridConfigImpl implIns, int row, int crowIns)
        {
            Validation.AssertValue(implIns);
            Validation.Assert(_typeRec.Accepts(implIns.RecordType, union: true));
            Validation.AssertIndexInclusive(crowIns, implIns.RowCount);
            Validation.AssertIndexInclusive(row, _crow);
            Validation.AssertIndexInclusive(crowIns, _crow - row);

            var slinSrc = implIns.GetSlotInfo(0, crowIns);
            var slinDst = GetSlotInfo(row, row + crowIns);
            for (int colDst = 0; colDst < _ccol; colDst++)
            {
                var name = _colToName[colDst];
                if (!implIns._nameToCol.TryGetValue(name, out int colSrc))
                    continue;

                var tinSrc = implIns._colToTin[colSrc];
                var tinDst = _colToTin[colDst];
                Validation.Assert(tinDst.Type.Accepts(tinSrc.Type, union: true));
                Validation.Assert(!tinSrc.NeedFlag || tinDst.NeedFlag);

                if (tinSrc.SysTypePri == tinDst.SysTypePri)
                {
                    // Note that this works fine for req uri to opt uri, no need to special case like elsewhere.
                    tinDst.CopyValues(implIns._values[colSrc], in slinSrc, _values[colDst], in slinDst);
                }
                else
                {
                    // The caller should ensure that this will succeed.
                    TryGetConverter(tinSrc, tinDst, out var conv).Verify();
                    conv.MapValues(implIns._values[colSrc], in slinSrc, _values[colDst], in slinDst);
                }

                // Copy flags, if needed.
                if (tinSrc.NeedFlag)
                    GetFlagInfoWrt(colDst).CopyFrom(implIns.GetFlagInfoRdo(colSrc), in slinSrc, in slinDst);
            }
        }

        /// <summary>
        /// This copies/converts values from a clip of non-record values <paramref name="dvcIns"/> into this starting
        /// at <paramref name="row"/>. The provided <paramref name="nameCol"/> is the column name for the clip.
        /// This is called by <see cref="PasteRows(int, int, DType, DataClip, DName)"/>.
        /// </summary>
        private void ConvertValues(DataValueClip dvcIns, int row, int crowIns, DName nameCol)
        {
            Validation.AssertValue(dvcIns);
            Validation.Assert(!dvcIns.HasFields);
            Validation.Assert(nameCol.IsValid);
            Validation.Assert(_typeRec.Accepts(DType.EmptyRecordReq.AddNameType(nameCol, dvcIns.ClipItemType), union: true));
            Validation.AssertIndexInclusive(crowIns, dvcIns.GetCount(() => { }));
            Validation.AssertIndexInclusive(row, _crow);
            Validation.AssertIndexInclusive(crowIns, _crow - row);
            Validation.Assert(crowIns > 0);
            Validation.Assert(dvcIns.ClipItemType == dvcIns.RawItemType);

            _nameToCol.TryGetValue(nameCol, out int col).Verify();
            var slinDst = GetSlotInfo(row, row + crowIns);
            var tinDst = _colToTin[col];
            var flinDst = GetFlagInfoWrt(col);
            var values = _values[col];
            if (dvcIns.ClipItemType == tinDst.Type || dvcIns.ClipItemType == tinDst.Type.ToReq())
            {
                if (dvcIns.ClipItemType.HasReq)
                    tinDst.SetValuesFlagged(dvcIns.RawItemsEnumerable, values, slinDst, flinDst);
                else
                {
                    tinDst.SetValues(dvcIns.RawItemsEnumerable, values, slinDst);
                    flinDst.Set(in slinDst);
                }
            }
            else
            {
                TryGetTypeInfo(dvcIns.ClipItemType, out var tinSrc, _tm).Verify();
                TryGetConverter(tinSrc, tinDst, out var conv).Verify();
                if (dvcIns.ClipItemType.HasReq)
                    conv.MapValuesFlagged(dvcIns.RawItemsEnumerable, values, slinDst, flinDst);
                else
                {
                    conv.MapValues(dvcIns.RawItemsEnumerable, values, slinDst);
                    flinDst.Set(in slinDst);
                }
            }
        }

        private bool TryGetConverter(TypeInfo tinSrc, TypeInfo tinDst, out Converter conv)
        {
            Validation.AssertValue(tinSrc);
            Validation.AssertValue(tinDst);
            Validation.Assert(tinSrc != tinDst);
            Validation.Assert(tinDst.Type.Accepts(tinSrc.Type, union: DType.UseUnionDefault));

            if (tinSrc.Type.IsNumericXxx && tinDst.Type.IsNumericXxx)
            {
                var code = GetConvCode(tinSrc.Type.Kind, tinDst.Type.Kind);
                return _convMap.TryGetValue(code, out conv);
            }

            if (tinSrc.SysTypePub.IsClass && tinDst.SysTypePub.IsClass)
            {
                Validation.Assert(!tinSrc.NeedFlag);
                Validation.Assert(!tinDst.NeedFlag);
                Validation.Assert(tinSrc.SysTypePri == tinSrc.SysTypePub);
                Validation.Assert(tinDst.SysTypePri == tinDst.SysTypePub);

                if (tinDst.SysTypePub.IsAssignableFrom(tinSrc.SysTypePub))
                {
                    // REVIEW: Should we cache these somewhere? They are very light-weight as objects, but going through
                    // reflection to instantiate them isn't that cheap.
                    Type stConv = typeof(ConverterClsType<,>).MakeGenericType(tinSrc.SysTypePub, tinDst.SysTypePub);
                    conv = (Converter)Activator.CreateInstance(stConv);
                    return true;
                }
            }

            conv = null;
            return false;
        }
    }
}
