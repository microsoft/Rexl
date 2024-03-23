// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This file contains GridNode functionality that maintains a set of "records" for a GridNode "config".
// This is the only part of Document that knows anything about TypeManager.

using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using Conditional = System.Diagnostics.ConditionalAttribute;

partial class DocumentBase
{
    partial class GridConfigImpl
    {
        #region Record cache
        /// <summary>
        /// The core implementation of everything. The <see cref="RecordCache"/> object is just
        /// a wrapper around this. Note that this changes when the grid's record type changes.
        /// </summary>
        private GridConfigImpl.Cache _cache;

        #region Only called by GridNode.Impl

        /// <summary>
        /// Should only be called by <see cref="GridNode.GridConfig"/>.
        /// </summary>
        private void EnsureCacheSpace(ref int cap, int capMin)
        {
            EnsureRecordCache();
            _cache.EnsureSpace(ref cap, capMin);
        }

        /// <summary>
        /// Should only be called by <see cref="GridNode.GridConfig"/>.
        /// </summary>
        private void DirtyRows(int rowMin, int rowLim)
        {
            EnsureRecordCache();

            // If the record type hasn't changed, dirty the rows, otherwise need a new (inner) cache.
            if (_cache.RecordType == _cache.GridImpl.RecordType)
                _cache.DirtyRows(rowMin, rowLim);
            else
                _cache = GridConfigImpl.CreateCache(_cache.TypeManager, _cache.GridImpl);
        }

        #endregion Only called by GridNode.Impl

        /// <summary>
        /// Produces the sequence of row indices that are currently stale. This is mostly for testing.
        /// </summary>
        public override IEnumerable<int> GetStaleRowIndices()
        {
            EnsureRecordCache();
            return _cache.GetStaleRowIndices();
        }

        /// <summary>
        /// Allocate and return an array whose item type is the record system type.
        /// </summary>
        public override RecordBase[] AllocRecordArray(int count)
        {
            Validation.BugCheckParam(count >= 0, nameof(count));
            EnsureRecordCache();

            return _cache.AllocArray(count);
        }

        /// <summary>
        /// Get records for the indicated rows. The returned array's item type will be the
        /// record system type.
        /// </summary>
        public override RecordBase[] GetRecords(int rowMin, int rowLim)
        {
            EnsureRecordCache();
            Validation.BugCheckIndexInclusive(rowLim, _cache.GridImpl.RowCount, nameof(rowLim));
            Validation.BugCheckIndexInclusive(rowMin, rowLim, nameof(rowMin));
            return _cache.GetRecords(rowMin, rowLim);
        }

        /// <summary>
        /// Get records for the indicated rows. To make an array whose item type is the record
        /// system type, use <see cref="AllocRecordArray(int)"/>.
        /// </summary>
        public override void GetRecords(RecordBase[] dst, int rowDst, int rowMin, int rowLim)
        {
            Validation.BugCheckValue(dst, nameof(dst));
            Validation.BugCheckParam(dst.GetType().GetElementType().IsAssignableFrom(_cache.GetRecordSysType()), nameof(dst), "Incompatible type");
            Validation.BugCheckIndexInclusive(rowLim, _cache.GridImpl.RowCount, nameof(rowLim));
            Validation.BugCheckIndex(rowMin, rowLim, nameof(rowMin));
            Validation.BugCheckIndexInclusive(rowLim - rowMin, Util.Size(dst), nameof(rowLim));

            // These ensure that the dst array is large enough.
            Validation.BugCheckIndex(rowDst, Util.Size(dst), nameof(dst));
            Validation.BugCheckIndexInclusive(rowDst + rowLim - rowMin, dst.Length, nameof(dst));

            _cache.GetRecords(dst, rowDst, rowMin, rowLim);
        }

        /// <summary>
        /// Given records compatible with this <see cref="RecordCache"/>, replace the indicated rows
        /// with these record values.
        /// 
        /// Returns true if at least one record was changed.
        /// </summary>
        public bool ReplaceRowsWithRecords(int rowDst, int crowDel, int crowIns, ReadOnly.Array<RecordBase> src, int rowSrc = 0)
        {
            Validation.BugCheckIndexInclusive(rowDst, _cache.GridImpl.RowCount, nameof(rowDst));
            Validation.BugCheckIndexInclusive(crowDel, _cache.GridImpl.RowCount - rowDst, nameof(crowDel));
            Validation.BugCheckIndexInclusive(crowIns, src.Length, nameof(crowIns));
            Validation.BugCheckIndexInclusive(rowSrc, src.Length - crowIns, nameof(rowSrc));

            EnsureRecordCache();

            return _cache.ReplaceRowsWithRecords(rowDst, crowDel, crowIns, src, rowSrc);
        }

        #endregion Record cache

        // REVIEW: Once GridNodes are immutable, remove this function and initialize in the constructor.
        public void EnsureRecordCache()
        {
            if (_cache == null)
            {
                _cache = GridConfigImpl.CreateCache(_tm, this);
            }
        }

        public static Cache CreateCache(TypeManager typeManager, GridConfigImpl impl)
        {
            Validation.AssertValue(typeManager);
            Validation.AssertValue(impl);
            Validation.BugCheck(typeManager.TryEnsureSysType(impl.RecordType, out Type stRec));
            Validation.Assert(stRec.IsSubclassOf(typeof(RecordBase)));

            var meth = new Func<TypeManager, GridConfigImpl, GridConfigImpl.Cache<RecordBase>>(CreateCache<RecordBase>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(stRec);
            return (GridConfigImpl.Cache)meth.Invoke(null, new object[] { typeManager, impl });
        }

        private static Cache<TRec> CreateCache<TRec>(TypeManager typeManager, GridConfigImpl impl)
            where TRec : RecordBase
        {
            Validation.AssertValue(typeManager);
            Validation.AssertValue(impl);
            Validation.Assert(typeManager.GetSysTypeOrNull(impl.RecordType) == typeof(TRec));

            return new Cache<TRec>(typeManager, impl);
        }

        /// <summary>
        /// The real implementation of <see cref="RecordCache"/>. This is public within (private) <see cref="GridConfigImpl"/>
        /// so <see cref="RecordCache"/> can see it.
        /// </summary>
        public abstract class Cache
        {
            public TypeManager TypeManager { get; }
            public GridConfigImpl GridImpl { get; }
            public DType RecordType { get; }

            protected Cache(TypeManager typeManager, GridConfigImpl impl)
            {
                Validation.AssertValue(typeManager);
                Validation.AssertValue(impl);

                TypeManager = typeManager;
                GridImpl = impl;
                RecordType = impl.RecordType;
            }

            /// <summary>
            /// Grow the record array.
            /// </summary>
            public abstract void EnsureSpace(ref int cap, int capMin);

            /// <summary>
            /// Invalid the indicated rows/records.
            /// </summary>
            public abstract void DirtyRows(int rowMin, int rowLim);

            public abstract Type GetRecordSysType();

            public abstract IEnumerable<int> GetStaleRowIndices();

            public abstract RecordBase[] AllocArray(int count);

            public abstract RecordBase[] GetRecords(int rowMin, int rowLim);

            public abstract void GetRecords(RecordBase[] dst, int rowDst, int rowMin, int rowLim);

            public abstract bool ReplaceRowsWithRecords(int rowDst, int crowDel, int crowIns, ReadOnly.Array<RecordBase> src, int rowSrc);

            public abstract Cache Clone(TypeManager typeManager, GridConfigImpl impl);
        }

        /// <summary>
        /// The type-specific record cache implementation. This maintains an array of <typeparamref name="TRec"/>,
        /// holding the record objects. When a row changes, the corresponding record is set to null indicating
        /// that it needs to be rebuilt (when needed).
        /// </summary>
        /// <typeparam name="TRec">The record system type</typeparam>
        private sealed class Cache<TRec> : Cache
            where TRec : RecordBase
        {
            /// <summary>
            /// The record cache, indexed by slot (not row).
            /// Its length will always be at least <see cref="GridNode.GridConfig._numAlloced"/>.
            /// Unused slots are null.
            /// Slots that are dirty are also null, indicating that the record needs to be built when needed.
            /// </summary>
            private TRec[] _recs;

            /// <summary>
            /// An array used when invoking the <see cref="_recordMaker"/> and <see cref="_recordCopier"/> functions,
            /// holding the slot indices that need records made or copied from.
            /// </summary>
            private int[] _slotsBuf;

            /// <summary>
            /// This is a generated function used to make records. Its signature is:
            /// <code>
            ///   Make(TRec[] recs, int[] slots, int count, Array[] values, Flag[][] valFlags)
            /// </code>
            /// It builds records for the indicated slots (count of them), using the indicated column
            /// values. The new records are stored in the indicated slots of the recs array.
            /// </summary>
            private Action<TRec[], int[], int, Array[], Flag[][]> _recordMaker;

            /// <summary>
            /// This is a generated function used to copy values from the records in <see cref="_recs"/> to the
            /// <see cref="GridConfigImpl._values"/> arrays. A null record means skip copying values. The function signature is:
            /// <code>
            ///   Copy(Array[] values, Flag[][] valFlags, int[] slots, int count, TRec[] recs)
            /// </code>
            /// </summary>
            private Action<Array[], Flag[][], int[], int, TRec[]> _recordCopier;

            public Cache(TypeManager typeManager, GridConfigImpl impl)
                : base(typeManager, impl)
            {
                Validation.Assert(TypeManager.GetSysTypeOrNull(RecordType) == typeof(TRec));
                _recs = new TRec[GridImpl._numAlloced];
            }

            public override Cache Clone(TypeManager tm, GridConfigImpl impl)
            {
                var res = new Cache<TRec>(tm, impl);
                res._recs = (TRec[])_recs.Clone();
                res._slotsBuf = (int[])_slotsBuf?.Clone();
                res._recordMaker = _recordMaker;
                res._recordCopier = _recordCopier;
                return res;
            }

            [Conditional("DEBUG")]
            void AssertValid()
            {
                Validation.Assert(Util.Size(_recs) >= GridImpl._numAlloced);
            }

            public override void EnsureSpace(ref int cap, int capMin)
            {
                AssertValid();
                Util.Grow(ref _recs, ref cap, capMin);
                AssertValid();
            }

            public override void DirtyRows(int rowMin, int rowLim)
            {
                AssertValid();
                Validation.Assert(RecordType == GridImpl._typeRec);
                GridImpl.GetSlotInfo(rowMin, rowLim).ClearValues(_recs);
            }

            public override Type GetRecordSysType()
            {
                return typeof(TRec);
            }

            public override IEnumerable<int> GetStaleRowIndices()
            {
                AssertValid();

                for (int row = 0; row < GridImpl._crow; row++)
                {
                    int slot = GridImpl._rowToSlot[row];
                    Validation.AssertIndex(slot, GridImpl._slotLim);

                    if (_recs[slot] == null)
                        yield return row;
                }
            }

            public override RecordBase[] AllocArray(int count)
            {
                Validation.BugCheckParam(count >= 0, nameof(count));
                return new TRec[count];
            }

            public override RecordBase[] GetRecords(int rowMin, int rowLim)
            {
                AssertValid();
                Validation.AssertIndexInclusive(rowLim, GridImpl.RowCount);
                Validation.AssertIndexInclusive(rowMin, rowLim);

                var dst = new TRec[rowLim - rowMin];
                GetRecords(dst, 0, rowMin, rowLim);
                return dst;
            }

            public override void GetRecords(RecordBase[] dst, int rowDst, int rowMin, int rowLim)
            {
                AssertValid();
                Validation.AssertValue(dst);
                Validation.AssertIndexInclusive(rowLim, GridImpl.RowCount);
                Validation.AssertIndexInclusive(rowMin, rowLim);
                Validation.AssertIndexInclusive(rowDst, dst.Length);
                Validation.AssertIndexInclusive(rowDst + rowLim - rowMin, dst.Length);

                // Fill the output with records we already have and record the slots
                // that need to be built.
                int cslot = 0;
                for (int row = rowMin; row < rowLim; row++)
                {
                    int slot = GridImpl._rowToSlot[row];
                    Validation.AssertIndex(slot, GridImpl._slotLim);

                    var rec = _recs[slot];
                    if (rec != null)
                    {
                        dst[row - rowMin + rowDst] = rec;
                        continue;
                    }

                    // Need to make this record.
                    if (_slotsBuf == null)
                    {
                        Validation.Assert(cslot == 0);
                        _slotsBuf = new int[Util.GetCapTarget(0, rowLim - row)];
                    }
                    if (_slotsBuf.Length <= cslot)
                    {
                        int capMin = cslot + rowLim - row;
                        int cap = Util.GetCapTarget(cslot, capMin);
                        Util.Grow(ref _slotsBuf, ref cap, capMin);
                    }
                    Validation.AssertIndex(cslot, _slotsBuf.Length);
                    _slotsBuf[cslot++] = slot;
                }

                if (cslot == 0)
                {
                    // We have all the requested records.
                    return;
                }

                if (_recordMaker == null)
                    _recordMaker = CreateMaker<TRec>(GridImpl, TypeManager);

                _recordMaker(_recs, _slotsBuf, cslot, GridImpl._values, GridImpl._valFlags);

                // Fill the output buffer, dst.
                int islot = 0;
                for (int row = rowMin; row < rowLim; row++)
                {
                    int slot = GridImpl._rowToSlot[row];
                    Validation.AssertIndex(slot, GridImpl._slotLim);

                    var rec = _recs[slot];
                    Validation.Assert(rec != null);

                    var recOld = dst[row - rowMin + rowDst];
                    if (recOld == rec)
                    {
                        Validation.Assert(recOld != null);
                        Validation.Assert(islot >= cslot || _slotsBuf[islot] != slot);
                    }
                    else
                    {
                        Validation.Assert(recOld == null);
                        Validation.Assert(islot < cslot);
                        Validation.Assert(_slotsBuf[islot] == slot);
                        dst[row - rowMin + rowDst] = rec;
                        islot++;
                    }
                }
                Validation.Assert(islot == cslot);
            }

            public override bool ReplaceRowsWithRecords(int rowDst, int crowDel, int crowIns, ReadOnly.Array<RecordBase> src, int rowSrc)
            {
                AssertValid();
                Validation.BugCheckParam(src.TryCast<TRec>(out var srcT), nameof(src), "Incompatible types");
                Validation.AssertIndexInclusive(rowDst, GridImpl._crow);
                Validation.AssertIndexInclusive(crowDel, GridImpl._crow - rowDst);
                Validation.AssertIndexInclusive(crowIns, src.Length);
                Validation.AssertIndexInclusive(rowSrc, src.Length - crowIns);

                if (crowIns == 0)
                {
                    if (crowDel > 0)
                    {
                        GridImpl.ReplaceRows(rowDst, crowDel, 0, false, null);
                        return true;
                    }
                    return false;
                }

                if (_recordCopier == null)
                    _recordCopier = CreateCopier<TRec>(GridImpl, TypeManager);

                if (_slotsBuf == null)
                    _slotsBuf = new int[Util.GetCapTarget(0, crowIns)];
                if (_slotsBuf.Length < crowIns)
                {
                    int cap = Util.GetCapTarget(_slotsBuf.Length, crowIns);
                    Util.Grow(ref _slotsBuf, ref cap, crowIns);
                }

                // This gets the size correct and also ensures the new rows are default,
                // which we assume.
                // REVIEW: It could be quite wasteful to force values to default. Is there
                // a better way? Note that this only matters when crowDel > 0, since new rows
                // are guaranteed to have default values.
                GridImpl.ReplaceRows(rowDst, crowDel, crowIns, false, null);

                // Put the records in our cache and record the slots with non-null records.
                int cslot = 0;
                for (int i = 0; i < crowIns; i++)
                {
                    int slot = GridImpl._rowToSlot[rowDst + i];
                    Validation.AssertIndex(slot, GridImpl._slotLim);
                    var rec = _recs[slot] = srcT[rowSrc + i];
                    if (rec != null)
                        _slotsBuf[cslot++] = slot;
                }

                // Copy from the records to values arrays.
                if (cslot > 0)
                    _recordCopier(GridImpl._values, GridImpl._valFlags, _slotsBuf, cslot, _recs);
                return true;
            }
        }

        /// <summary>
        /// Create a "maker" function with the following signature:
        /// <code>
        ///   Make(TRec[] recs, int[] slots, int count, Array[] values, Flag[][] valFlags)
        /// </code>
        /// The function is wrapped in a delegate and returned. The function creates and fills
        /// records from the values for the indicated slots.
        /// </summary>
        private static Action<TRec[], int[], int, Array[], Flag[][]> CreateMaker<TRec>(GridConfigImpl impl, TypeManager tm)
        {
            // If the Flag enum changes size, this code needs to change.
            Validation.Assert(sizeof(Flag) == sizeof(byte));

            // Set this to Position or Size to dump the IL to console.
            ILLogKind logKind = ILLogKind.None;

            // We use an "instance" delegate, with the RecordRuntimeTypeInfo object as the "this".
            MethodGenerator gen = new MethodGenerator("make", logKind, null, typeof(RecordRuntimeTypeInfo),
                typeof(TRec[]), typeof(int[]), typeof(int), typeof(Array[]), typeof(Flag[][]));
            Type stDel = typeof(Action<TRec[], int[], int, Array[], Flag[][]>);

            RecordRuntimeTypeInfo rrtiThis = null;

            // First grab the locals we'll use a lot so they have small indices, to reduce
            // the IL size.
            {
                // The record generator will re-grab this for real later.
                using var locRec = gen.AcquireLocal(typeof(TRec));
            }
            using var locIslot = gen.AcquireLocal(typeof(int));
            using var locSlot = gen.AcquireLocal(typeof(int));
            bool needFlags = impl._valFlags.Length > 0;
            using var locFlags = needFlags ? gen.AcquireLocal(typeof(Flag[])) : default;

            // Store the arrays in strongly typed locals to avoid type testing on every value.
            // REVIEW: Do we need to be concerned about this limiting the number of columns?
            var arrs = new MethodGenerator.Local[impl._ccol];
            for (int col = 0; col < impl._ccol; col++)
            {
                var tin = impl._colToTin[col];
                Type stArr = tin.SysTypePri.MakeArrayType();
                arrs[col] = gen.AcquireLocal(stArr);
                gen.Il
                    // vals = values[col];
                    .Ldarg(4)
                    .Ldc_I4(col)
                    .Ldelem(stArr)
                    .Stloc(arrs[col]);
            }

            // Start the loop and create the record.
            Label labTop = gen.Il.DefineLabel();
            Label labTest = default;
            gen.Il
                // islot = 0;
                .Ldc_I4(0)
                .Stloc(locIslot)
                // goto LTest;
                .Br(ref labTest)
                // LTop:
                .MarkLabel(labTop)
                // slot = slots[islot];
                .Ldarg(2)
                .Ldloc(locIslot)
                .Ldelem_I4()
                .Stloc(locSlot);

            void GenLoadRrti(RecordRuntimeTypeInfo rrti)
            {
                Validation.Assert(rrtiThis is null);
                Validation.AssertValue(rrti);
                rrtiThis = rrti;
                gen.Il.Ldarg(0);
            }

            using var rg = tm.CreateRecordGenerator(gen, impl.RecordType, GenLoadRrti);
            Validation.Assert(rg.RecSysType == typeof(TRec));

            // For each column, copy the column/flag values to the field of the record.
            for (int col = 0; col < impl._ccol; col++)
            {
                var name = impl._colToName[col];
                var tin = impl._colToTin[col];
                Type stPub = tin.SysTypePub;
                Type stPri = tin.SysTypePri;
                Type stArr = stPri.MakeArrayType();
                Label labNextCol = default;

                if (tin.NeedFlag)
                {
                    var (grp, mask) = impl._colToFlagInfo[col];
                    Validation.AssertIndex(grp, impl._valFlags.Length);
                    Validation.Assert(mask != 0);
                    Validation.Assert((mask & (mask - 1)) == 0);
                    gen.Il
                        // if ((flags[slot] & mask) == 0) goto LNext;
                        .Ldarg(5)
                        .Ldc_I4(grp)
                        .Ldelem(typeof(Flag[]))
                        .Ldloc(locSlot)
                        // This is where the code knows the size of Flag.
                        .Ldelem_U1()
                        .Ldc_U4((uint)mask)
                        .And()
                        .Brfalse(ref labNextCol);
                }
                else
                    Validation.Assert(stPub == stPri);

                rg.SetFromStackPre(name, tin.Type, fromReq: tin.NeedFlag);
                gen.Il
                    // vals[slot];
                    .Ldloc(arrs[col])
                    .Ldloc(locSlot)
                    .Ldelem(stPri);
                rg.SetFromStackPost();

                gen.Il.MarkLabelIfUsed(labNextCol);
            }

            gen.Il
                // recs[slots[islot]] = <record>;
                .Ldarg(1)
                .Ldloc(locSlot);
            rg.Finish();
            gen.Il
                .Stelem(typeof(TRec))
                // islot++;
                .Ldloc(locIslot)
                .Ldc_I4(1)
                .Add()
                .Stloc(locIslot)
                // LTest:
                .MarkLabel(labTest)
                // if (islot < count) goto LTop;
                .Ldloc(locIslot)
                .Ldarg(3)
                .Blt(ref labTop);

            gen.Il.Ret();

            if (logKind != ILLogKind.None)
            {
                var code = gen.GetIL();
                Console.WriteLine(code);
            }

            // Get the delegate, with null as "this".
            return (Action<TRec[], int[], int, Array[], Flag[][]>)gen.CreateInstanceDelegate(stDel, rrtiThis);
        }

        /// <summary>
        /// Create a "copier" function with the following signature:
        /// <code>
        ///   Copy(Array[] values, Flag[][] valFlags, int[] slots, int count, TRec[] recs)
        /// </code>
        /// The function is wrapped in a delegate and returned. The function copies values from
        /// the records to the values arrays for the indicated slots.
        /// </summary>
        private static Action<Array[], Flag[][], int[], int, TRec[]> CreateCopier<TRec>(GridConfigImpl impl, TypeManager tm)
        {
            // If the Flag enum changes size, this code needs to change.
            Validation.Assert(sizeof(Flag) == sizeof(byte));

            // Set this to Position or Size to dump the IL to console.
            ILLogKind logKind = ILLogKind.None;

            // We use an "instance" delegate, with a null object as the "this".
            MethodGenerator gen = new MethodGenerator("copy", logKind, null, typeof(object), typeof(Array[]), typeof(Flag[][]), typeof(int[]), typeof(int), typeof(TRec[]));
            Type stDel = typeof(Action<Array[], Flag[][], int[], int, TRec[]>);

            // Create the record objects.
            using var locIslot = gen.AcquireLocal(typeof(int));
            using var locSlot = gen.AcquireLocal(typeof(int));
            Label labTop = gen.Il.DefineLabel();
            Label labTest = default;

            // For each column, get the record field values and set the array/flag values.
            bool needFlags = impl._valFlags.Length > 0;
            using var locFlags = needFlags ? gen.AcquireLocal(typeof(Flag[])) : default;
            for (int col = 0; col < impl._ccol; col++)
            {
                var name = impl._colToName[col];
                var tin = impl._colToTin[col];
                Type stPub = tin.SysTypePub;
                Type stPri = tin.SysTypePri;
                Type stArr = stPri.MakeArrayType();

                int grp = -1;
                Flag mask = 0;
                Flag clear = ~mask;
                if (tin.NeedFlag)
                {
                    Validation.Assert(stPub.GetGenericTypeDefinition() == typeof(Nullable<>));
                    (grp, mask) = impl._colToFlagInfo[col];
                    Validation.AssertIndex(grp, impl._valFlags.Length);
                    Validation.Assert(mask != 0);
                    Validation.Assert((mask & (mask - 1)) == 0);
                    clear = ~mask;
                    gen.Il
                        .Ldarg(2)
                        .Ldc_I4(grp)
                        .Ldelem(typeof(Flag[]))
                        .Stloc(locFlags);
                }

                using var locVals = gen.AcquireLocal(stArr);
                labTop = gen.Il.DefineLabel();
                labTest = default;
                gen.Il
                    // vals = values[col];
                    .Ldarg(1)
                    .Ldc_I4(col)
                    .Ldelem(stArr)
                    .Stloc(locVals)
                    // islot = 0;
                    .Ldc_I4(0)
                    .Stloc(locIslot)
                    // goto LTest;
                    .Br(ref labTest)
                    // LTop:
                    .MarkLabel(labTop)
                    // vals, slot = slots[islot]: prep for assignment.
                    .Ldloc(locVals)
                    .Ldarg(3)
                    .Ldloc(locIslot)
                    .Ldelem_I4()
                    .Dup()
                    .Stloc(locSlot)
                    //  recs[slot].Fld
                    .Ldarg(5)
                    .Ldloc(locSlot)
                    .Ldelem(typeof(TRec));
                tm.GenLoadField(gen, impl.RecordType, typeof(TRec), name, tin.Type);

                if (tin.NeedFlag)
                {
                    using var locOpt = gen.AcquireLocal(stPub);
                    gen.Il
                        .Stloc(locOpt)
                        // Prep for flags[slot] = ...
                        .Ldloc(locFlags)
                        .Ldloc(locSlot)
                        // Load flags[slot] & ~mask: clear mask
                        .Ldloc(locFlags)
                        .Ldloc(locSlot)
                        // This is where the code knows the size of Flag.
                        .Ldelem_U1()
                        .Ldc_U4((uint)clear)
                        .And()
                        // flag | (mask & -x.HasValue): set mask if HasValue else no change. This assumes
                        // that HasValue returns 0 (false) or 1 (true).
                        .Ldc_U4((uint)mask)
                        .Ldloca(locOpt)
                        .Call(stPub.GetMethod("get_HasValue").VerifyValue())
                        .Neg()
                        .And()
                        .Or()
                        // flags[slot] = <top>
                        .Stelem_I1()
                        // Get the inner (non-opt) value.
                        .Ldloca(locOpt)
                        .Call(stPub.GetMethod("GetValueOrDefault", Type.EmptyTypes).VerifyValue());
                }

                // Convert from public value to private. Note that if tin.NeedFlag is true, we assume
                // that the public type is T? for some type T. In that case, a value of "T" is on the
                // stack, rather than a value of T?. That is, we've already called GetValueOrDefault().
                tin.GenConvertPubValToPri(gen);

                gen.Il
                    // vals[slot] = <top>
                    .Stelem(stPri)
                    // islot++;
                    .Ldloc(locIslot)
                    .Ldc_I4(1)
                    .Add()
                    .Stloc(locIslot)
                    // LTest:
                    .MarkLabel(labTest)
                    // if (islot < count) goto LTop;
                    .Ldloc(locIslot)
                    .Ldarg(4)
                    .Blt(ref labTop);
            }

            gen.Il.Ret();

            if (logKind != ILLogKind.None)
            {
                var code = gen.GetIL();
                Console.WriteLine(code);
            }

            // Get the delegate, with null as "this".
            return (Action<Array[], Flag[][], int[], int, TRec[]>)gen.CreateInstanceDelegate(stDel, null);
        }

        /// <summary>
        /// Create a "converter" function with the following signature:
        /// <code>
        ///   Convert(Array[] values, Flag[][] valFlags, int[] rowToSlot, int row, int count, TRecRaw[] recs)
        /// </code>
        /// The function is wrapped in a delegate and returned. The function copies values from
        /// the records, in standard order, to the values arrays for the indicated slots. The field
        /// types must be convertible to the array item types and need not cover all the arrays.
        /// For non-opt source columns that are opt in the destination (using flags), this assumes that
        /// other code sets the flags, presumably in bulk.
        /// </summary>
        private static Action<Array[], Flag[][], int[], int, int, TRecRaw[]> CreateConverter<TRecRaw>(
            GridConfigImpl implDst, TypeManager tmRec, DType typeRecRaw, DType typeRecMapped, Func<DName, DName> fieldMap)
        {
            // If the Flag enum changes size, this code needs to change.
            Validation.Assert(sizeof(Flag) == sizeof(byte));

            Validation.AssertValue(implDst);
            Validation.AssertValue(tmRec);
            Validation.Assert(typeRecRaw.IsRecordReq);
            Validation.Assert(implDst.RecordType.Accepts(typeRecMapped, union: true));
            Validation.Assert(tmRec.GetSysTypeOrNull(typeRecRaw) == typeof(TRecRaw));

            // Set this to Position or Size to dump the IL to console.
            ILLogKind logKind = ILLogKind.None;

            // We use an "instance" delegate, with a null object as the "this".
            MethodGenerator gen = new MethodGenerator("convert", logKind, null, typeof(object), typeof(Array[]), typeof(Flag[][]), typeof(int[]), typeof(int), typeof(int), typeof(TRecRaw[]));
            Type stDel = typeof(Action<Array[], Flag[][], int[], int, int, TRecRaw[]>);

            // Create the record objects.
            using var locIslot = gen.AcquireLocal(typeof(int));
            using var locSlot = gen.AcquireLocal(typeof(int));
            Label labTop = gen.Il.DefineLabel();
            Label labTest = default;

            // For each column, get the record field values and set the array/flag values.
            for (int col = 0; col < implDst._ccol; col++)
            {
                var nameDst = implDst._colToName[col];
                if (!typeRecMapped.TryGetNameType(nameDst, out DType typeSrc))
                    continue;
                var nameRaw = fieldMap(nameDst);

                Validation.BugCheck(TryGetTypeInfo(typeSrc, out var tinSrc, tmRec));
                var tinDst = implDst._colToTin[col];
                Validation.Assert(!tinSrc.NeedFlag || tinDst.NeedFlag);

                Type stPubSrc = tinSrc.SysTypePub;
                Type stPriSrc = tinSrc.SysTypePri;
                Type stPriDst = tinDst.SysTypePri;
                Type stArr = stPriDst.MakeArrayType();

                int grp = -1;
                Flag mask = 0;
                Flag clear = ~mask;
                using var locFlags = tinDst.NeedFlag ? gen.AcquireLocal(typeof(Flag[])) : default;
                if (tinDst.NeedFlag)
                {
                    Validation.Assert(tinDst.SysTypePub.GetGenericTypeDefinition() == typeof(Nullable<>));
                    (grp, mask) = implDst._colToFlagInfo[col];
                    Validation.AssertIndex(grp, implDst._valFlags.Length);
                    Validation.Assert(mask != 0);
                    Validation.Assert((mask & (mask - 1)) == 0);
                    clear = ~mask;
                    gen.Il
                        .Ldarg(2)
                        .Ldc_I4(grp)
                        .Ldelem(typeof(Flag[]))
                        .Stloc(locFlags);
                }

                using var locVals = gen.AcquireLocal(stArr);
                labTop = gen.Il.DefineLabel();
                labTest = default;
                gen.Il
                    // vals = values[col];
                    .Ldarg(1)
                    .Ldc_I4(col)
                    .Ldelem(stArr)
                    .Stloc(locVals)
                    // islot = 0;
                    .Ldc_I4(0)
                    .Stloc(locIslot)
                    // goto LTest;
                    .Br(ref labTest)
                    // LTop:
                    .MarkLabel(labTop)
                    // vals, slot = slots[islot + row]: prep for assignment.
                    .Ldloc(locVals)
                    .Ldarg(3)
                    .Ldloc(locIslot)
                    .Ldarg(4)
                    .Add()
                    .Ldelem_I4()
                    .Dup()
                    .Stloc(locSlot)
                    //  recs[islot].Fld
                    .Ldarg(6)
                    .Ldloc(locIslot)
                    .Ldelem(typeof(TRecRaw));

                tmRec.GenLoadField(gen, typeRecRaw, typeof(TRecRaw), nameRaw, tinSrc.Type);

                if (tinSrc.NeedFlag)
                {
                    using var locOpt = gen.AcquireLocal(stPubSrc);
                    gen.Il
                        .Stloc(locOpt)
                        // Prep for flags[slot] = ...
                        .Ldloc(locFlags)
                        .Ldloc(locSlot)
                        // Load flags[slot] & ~mask: clear mask
                        .Ldloc(locFlags)
                        .Ldloc(locSlot)
                        // This is where the code knows the size of Flag.
                        .Ldelem_U1()
                        .Ldc_U4((uint)clear)
                        .And()
                        // flag | (mask & -x.HasValue): set mask if HasValue else no change. This assumes
                        // that HasValue returns 0 (false) or 1 (true).
                        .Ldc_U4((uint)mask)
                        .Ldloca(locOpt)
                        .Call(stPubSrc.GetMethod("get_HasValue").VerifyValue())
                        .Neg()
                        .And()
                        .Or()
                        // flags[slot] = <top>
                        .Stelem_I1()
                        // Get the inner (non-opt) value.
                        .Ldloca(locOpt)
                        .Call(stPubSrc.GetMethod("GetValueOrDefault", Type.EmptyTypes).VerifyValue());
                }

                if (stPriSrc != stPriDst)
                {
                    Validation.Assert(tinSrc.Type.IsNumericXxx);
                    Validation.Assert(tinDst.Type.IsNumericXxx);
                    tmRec.GenCastNum(gen, tinSrc.Type.ToReq(), tinDst.Type.ToReq());
                }

                // Convert from public value to private. Note that if tin.NeedFlag is true, we assume
                // that the public type is T? for some type T. In that case, a value of "T" is on the
                // stack, rather than a value of T?. That is, we've already called GetValueOrDefault().
                tinDst.GenConvertPubValToPri(gen);

                gen.Il
                    // vals[slot] = <top>
                    .Stelem(stPriDst)
                    // islot++;
                    .Ldloc(locIslot)
                    .Ldc_I4(1)
                    .Add()
                    .Stloc(locIslot)
                    // LTest:
                    .MarkLabel(labTest)
                    // if (islot < count) goto LTop;
                    .Ldloc(locIslot)
                    .Ldarg(5)
                    .Blt(ref labTop);
            }

            gen.Il.Ret();

            if (logKind != ILLogKind.None)
            {
                var code = gen.GetIL();
                Console.WriteLine(code);
            }

            // Get the delegate, with null as "this".
            return (Action<Array[], Flag[][], int[], int, int, TRecRaw[]>)gen.CreateInstanceDelegate(stDel, null);
        }

        /// <summary>
        /// This copies/converts values from <paramref name="dvcIns"/> into this starting at
        /// <paramref name="row"/>. This is called by <see cref="PasteRows(int, int, DType, DataClip)"/>.
        /// It is in this partial because it depends on a <see cref="TypeManager"/>.
        /// </summary>
        private void ConvertValues(DataValueClip dvcIns, int row, int crowIns)
        {
            Validation.AssertValue(dvcIns);
            Validation.Assert(typeof(DataValueClip<>).MakeGenericType(dvcIns.RawItemSysType).IsAssignableFrom(dvcIns.GetType()));
            Validation.AssertValue(dvcIns.TypeManager);
            Validation.Assert(_typeRec.Accepts(dvcIns.ClipItemType, union: true));
            Validation.AssertIndexInclusive(crowIns, dvcIns.GetCount(() => { }));
            Validation.AssertIndexInclusive(row, _crow);
            Validation.AssertIndexInclusive(crowIns, _crow - row);
            Validation.Assert(crowIns > 0);

            var meth = new Action<DataValueClip<RecordBase>, int, int>(ConvertValuesCore)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(dvcIns.RawItemSysType);
            meth.Invoke(this, new object[] { dvcIns, row, crowIns });
        }

        private void ConvertValuesCore<TRec>(DataValueClip<TRec> dvcIns, int row, int crowIns)
            where TRec : RecordBase
        {
            Validation.AssertValue(dvcIns);
            Validation.BugCheckValue(dvcIns.TypeManager, nameof(dvcIns));
            Validation.Assert(_typeRec.Accepts(dvcIns.ClipItemType, union: true));
            Validation.AssertIndexInclusive(crowIns, dvcIns.GetCount(() => { }));
            Validation.AssertIndexInclusive(row, _crow);
            Validation.AssertIndexInclusive(crowIns, _crow - row);
            Validation.Assert(crowIns > 0);

            // Get the records into an array.
            var recs = new TRec[crowIns];
            int i = 0;
            foreach (var rec in dvcIns.RawItems)
            {
                recs[i++] = rec;
                if (i >= crowIns)
                    break;
            }
            Validation.Assert(i == crowIns);

            var converter = CreateConverter<TRec>(this, dvcIns.TypeManager, dvcIns.RawItemType, dvcIns.ClipItemType, dvcIns.ClipFieldToRawField);
            converter(_values, _valFlags, _rowToSlot, row, crowIns, recs);
        }
    }
}
