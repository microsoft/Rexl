// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

using Parquet;
using Parquet.Data;

namespace Microsoft.Rexl.Data;

/// <summary>
/// A parquet to rexl reader with the rexl record system type as type parameter.
/// </summary>
public abstract class RexlParquetReader<TRec> : RexlParquetReader
    where TRec : RecordBase
{
    private protected RexlParquetReader(ParquetInfo info)
        : base(info)
    {
    }
}

/// <summary>
/// A parquet to rexl reader.
/// </summary>
public abstract class RexlParquetReader : IDisposable
{
    private protected readonly ParquetInfo _info;

    /// <summary>
    /// The record type of the result table.
    /// </summary>
    public DType TypeRec => _info._typeRec;

    /// <summary>
    /// The table type of the result.
    /// </summary>
    public DType TypeSeq => _info._typeRec.ToSequence();

    /// <summary>
    /// The system type for the record.
    /// </summary>
    public Type SysTypeRec => _info._stRec;

    /// <summary>
    /// The system type for the sequence.
    /// </summary>
    public Type SysTypeSeq => typeof(IEnumerable<>).MakeGenericType(_info._stRec);

    /// <summary>
    /// The resulting table data. This is populated incrementally as each "row group" is read.
    /// </summary>
    public abstract IEnumerable Data { get; }

    /// <summary>
    /// Load a parquet file from the given stream. To avoid excessive use of nullable, set
    /// <paramref name="suppressOpt"/> to true. When true, mull values for most value types
    /// will be mapped to the default for that type. The exception is floating point, where
    /// null maps to NaN instead.
    /// REVIEW: Seems like parquet files written by pandas/python use "has nulls" set to true.
    /// Not sure how to author parquets with this turned off. Consider an option to inspect the values
    /// and force to non-opt when there are no nulls.
    /// </summary>
    public static (DType, IEnumerable) Read(
        TypeManager tm, Stream stream, bool suppressOpt,
        Action<long> progress = null, long freq = 10,
        Action<DType, IEnumerable> notify = null)
    {
        using var rdr = Create(tm, stream, suppressOpt, progress, freq);
        if (notify != null)
            notify(rdr.TypeSeq, rdr.Data);
        rdr.Run();
        return (rdr.TypeSeq, rdr.Data);
    }

    /// <summary>
    /// Creates a reader to load a parquet file from the given stream. To avoid excessive use
    /// of nullable, set <paramref name="suppressOpt"/> to true. When true, null values for most
    /// value types will be mapped to the default for that type. The exception is floating point,
    /// where nulls to NaN instead.
    /// </summary>
    public static RexlParquetReader Create(
        TypeManager tm, Stream stream, bool suppressOpt,
        Action<long> progress = null, long freq = 10)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckValue(stream, nameof(stream));
        Validation.BugCheckParam(stream.CanRead & stream.CanSeek, nameof(stream),
            "Parquet stream must be readable and seekable");
        Validation.BugCheckValueOrNull(progress);

        // REVIEW: Should this seek to the beginning or read from where it is?
        stream.Seek(0, SeekOrigin.Begin);

        ParquetInfo info = null;
        ParquetReader rdr = new(stream, leaveStreamOpen: false);
        try
        {
            // The info takes ownership of the rdr, so set it to null to avoid disposing below.
            info = new ParquetInfo(tm, rdr, suppressOpt);
            rdr = null;

            var methGen = new Func<ParquetInfo, Action<long>, long, RexlParquetReader<RecordBase>>(CreateCore<RecordBase>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(info._stRec);

            var res = (RexlParquetReader)methGen.Invoke(null, new object[] { info, progress, freq });
            Validation.Assert(res != null);

            // The batch reader took ownership of the info, so set it to null to avoid disposing below.
            info = null;

            return res;
        }
        finally
        {
            info?.Dispose();
            rdr?.Dispose();
        }
    }

    /// <summary>
    /// Strongly typed method to load a table from a parquet file.
    /// </summary>
    private static RexlParquetReader<TRec> CreateCore<TRec>(ParquetInfo info, Action<long> progress, long freq)
        where TRec : RecordBase
    {
        Validation.AssertValue(info);
        Validation.Assert(info._stRec == typeof(TRec));

        // Create the record maker.
        var fn = CreateMaker<TRec>(info);

        return new Impl<TRec>(info, progress, freq, fn);
    }

    private protected RexlParquetReader(ParquetInfo info)
    {
        Validation.AssertValue(info);
        _info = info;
    }

    public void Dispose()
    {
        _info.Dispose();
    }

    /// <summary>
    /// Call this to populate the output sequence. Iteration of the output sequence will be
    /// blocked until this is called. This is typically called on a worker thread while
    /// consumers are on other threads.
    /// </summary>
    public abstract void Run();

    /// <summary>
    /// Information about the parquet file. This has an open low level parquet reader,
    /// so is disposable.
    /// </summary>
    private protected sealed class ParquetInfo : IDisposable
    {
        public readonly TypeManager _tm;
        public readonly ParquetReader _rdr;
        public readonly DataField[] _flds;
        public readonly int _cgrp;
        public readonly List<(DName name, int index, MethodInfo meth)> _map;
        public readonly DType _typeRec;
        public readonly Type _stRec;

        public ParquetInfo(TypeManager tm, ParquetReader rdr, bool suppressOpt)
        {
            Validation.AssertValue(tm);
            Validation.AssertValue(rdr);

            _tm = tm;
            _rdr = rdr;
            _flds = rdr.Schema.GetDataFields();
            _cgrp = _rdr.RowGroupCount;

            _map = new List<(DName name, int index, MethodInfo meth)>(_flds.Length);
            var tns = new List<TypedName>();

            for (int i = 0; i < _flds.Length; i++)
            {
                var fld = _flds[i];
                if (!DName.TryWrap(fld.Name, out var name))
                    continue;
                var typeFld = FromParquetField(fld, out Type stSrc);
                if (!typeFld.IsValid)
                    continue;

                // REVIEW: Support sequence?
                if (typeFld.IsSequence)
                    continue;

                MethodInfo meth;
                tm.TryEnsureSysType(typeFld, out var stFld).Verify();
                if (!typeFld.HasReq)
                {
                    if (stFld == typeof(RDate))
                    {
                        Validation.Assert(typeof(DateTimeOffset) == fld.ClrType);
                        meth = new Func<DateTimeOffset, RDate>(FromDto).Method;
                    }
                    else
                    {
                        Validation.Assert(stFld == fld.ClrType);
                        meth = null;
                    }
                }
                else if (suppressOpt && stFld.IsGenericType && stFld.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    typeFld = typeFld.ToReq();
                    switch (typeFld.Kind)
                    {
                    case DKind.Date:
                        Validation.Assert(stSrc == typeof(DateTimeOffset?));
                        Validation.Assert(stFld == typeof(RDate?));
                        stFld = typeof(RDate);
                        meth = new Func<DateTimeOffset?, RDate>(FromDtoDef).Method;
                        break;
                    case DKind.R8:
                        Validation.Assert(stSrc == typeof(double?));
                        Validation.Assert(stFld == typeof(double?));
                        stFld = typeof(double);
                        meth = new Func<double?, double>(FromR8Q).Method;
                        break;
                    case DKind.R4:
                        Validation.Assert(stSrc == typeof(float?));
                        Validation.Assert(stFld == typeof(float?));
                        Validation.Assert(stSrc == stFld);
                        stFld = typeof(float);
                        meth = new Func<float?, float>(FromR4Q).Method;
                        break;
                    default:
                        Validation.Assert(stFld == fld.ClrNullableIfHasNullsType);
                        tm.TryEnsureSysType(typeFld, out stFld).Verify();
                        Validation.Assert(stFld == fld.ClrType);
                        meth = new Func<long?, long>(FromValQ).Method;
                        if (stFld != typeof(long))
                            meth = meth.GetGenericMethodDefinition().MakeGenericMethod(stFld);
                        break;
                    }
                }
                else
                {
                    Validation.Assert(fld.HasNulls);
                    if (stFld == typeof(RDate?))
                    {
                        Validation.Assert(stSrc == typeof(DateTimeOffset?));
                        meth = new Func<DateTimeOffset?, RDate?>(FromDto).Method;
                    }
                    else
                    {
                        Validation.Assert(stFld == stSrc);
                        meth = null;
                    }
                }

                _map.Add((name, i, meth));
                tns.Add(new TypedName(name, typeFld));
            }

            _typeRec = DType.CreateRecord(opt: false, tns);
            _tm.TryEnsureSysType(_typeRec, out _stRec).Verify();
        }

        public void Dispose()
        {
            _rdr.Dispose();
        }

        /// <summary>
        /// Get the <see cref="DType"/> equivalent of a parquet <see cref="DataType"/>.
        /// </summary>
        private static DType FromParquetType(DataType dt)
        {
            switch (dt)
            {
            default:
            case DataType.Unspecified:
            case DataType.Int96:
            case DataType.ByteArray:
            case DataType.Decimal:
            case DataType.Interval:
                return default;

            case DataType.DateTimeOffset:
                return DType.DateReq;
            case DataType.TimeSpan:
                return DType.TimeReq;

            case DataType.Boolean:
                return DType.BitReq;
            case DataType.Byte:
            case DataType.UnsignedByte:
                return DType.U1Req;
            case DataType.SignedByte:
                return DType.I1Req;
            case DataType.Short:
            case DataType.Int16:
                return DType.I2Req;
            case DataType.UnsignedShort:
            case DataType.UnsignedInt16:
                return DType.U2Req;
            case DataType.Int32:
                return DType.I4Req;
            case DataType.UnsignedInt32:
                return DType.U4Req;
            case DataType.Int64:
                return DType.I8Req;
            case DataType.UnsignedInt64:
                return DType.U8Req;
            case DataType.String:
                return DType.Text;
            case DataType.Float:
                return DType.R4Req;
            case DataType.Double:
                return DType.R8Req;
            }
        }

        /// <summary>
        /// Get the <see cref="DType"/> for a parquet <see cref="DataField"/>.
        /// </summary>
        private static DType FromParquetField(DataField fld, out Type stSrc)
        {
            Validation.BugCheckValue(fld, nameof(fld));
            var type = FromParquetType(fld.DataType);
            if (!type.IsValid)
            {
                stSrc = null;
                return type;
            }

            stSrc = fld.ClrType;
            if (fld.HasNulls && !type.IsOpt)
            {
                type = type.ToOpt();
                stSrc = fld.ClrNullableIfHasNullsType;
            }
            if (fld.IsArray)
                type = type.ToSequence();
            return type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RDate FromDto(DateTimeOffset dto)
        {
            return RDate.FromSys(dto.ToUniversalTime().DateTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RDate? FromDto(DateTimeOffset? dto)
        {
            if (dto == null)
                return null;
            return RDate.FromSys(dto.GetValueOrDefault().ToUniversalTime().DateTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RDate FromDtoDef(DateTimeOffset? dto)
        {
            if (dto == null)
                return default;
            return RDate.FromSys(dto.GetValueOrDefault().ToUniversalTime().DateTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double FromR8Q(double? value)
        {
            if (value == null)
                return double.NaN;
            return value.GetValueOrDefault();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FromR4Q(float? value)
        {
            if (value == null)
                return float.NaN;
            return value.GetValueOrDefault();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T FromValQ<T>(T? value)
            where T : struct
        {
            return value.GetValueOrDefault();
        }
    }

    /// <summary>
    /// Get the names of the columns/fields in the order stored in the parquet file.
    /// </summary>
    public Immutable.Array<DName> GetFieldNames()
    {
        var bldr = Immutable.Array.CreateBuilder<DName>(_info._map.Count, init: true);
        for (int i = 0; i < bldr.Count; i++)
            bldr[i] = _info._map[i].name;
        return bldr.ToImmutable();
    }

    /// <summary>
    /// The implementation of the reader. This posts the items in a <see cref="BuildableSequence{T}"/>.
    /// </summary>
    private sealed class Impl<TRec> : RexlParquetReader<TRec>
        where TRec : RecordBase
    {
        private readonly Action<long> _progress;
        private readonly int _freq;

        /// <summary>
        /// Builds records from column arrays.
        /// <code>
        /// void Make(TRec[] recs, int count, int rowSrc, Array[] values)
        /// </code>
        /// </summary>
        private readonly Action<TRec[], int, int, Array[]> _maker;

        private readonly BuildableSequence<TRec> _seq;
        private readonly BuildableSequence<TRec>.Builder _bldr;

        private long _crowTot;

        public override IEnumerable Data => _seq;

        public IEnumerable<TRec> Items => _seq;

        public Impl(ParquetInfo info, Action<long> progress, long freq, Action<TRec[], int, int, Array[]> maker)
            : base(info)
        {
            Validation.AssertValueOrNull(progress);
            Validation.AssertValue(maker);

            _progress = progress;

            // Map frequency to a reasonable range.
            const int freqMax = 256;
            const int freqMin = 8;
            if (progress == null || freq <= 0 || freq > freqMax)
                _freq = freqMax;
            else if (freq < freqMin)
                _freq = freqMin;
            else
                _freq = (int)freq;
            Validation.Assert(freqMin <= _freq & _freq <= freqMax);

            _maker = maker;
            if (_info._cgrp > 0)
                _bldr = BuildableSequence<TRec>.Builder.Create(0, out _seq);
        }

        public override void Run()
        {
            var cols = new Array[_info._map.Count];
            try
            {
                int cgrp = _info._cgrp;
                if (cgrp <= 0)
                    return;

                var buf = new TRec[_freq];
                for (int grp = 0; grp < cgrp; grp++)
                {
                    using var grdr = _info._rdr.OpenRowGroupReader(grp);
                    var crowBig = grdr.RowCount;
                    if (crowBig <= 0)
                    {
                        Validation.Assert(crowBig == 0);
                        continue;
                    }

                    if (crowBig > int.MaxValue)
                    {
                        // REVIEW: Do we need to handle this?
                        Validation.Assert(false);
                        crowBig = int.MaxValue;
                    }
                    int crow = (int)crowBig;

                    for (int icol = 0; icol < _info._map.Count; icol++)
                    {
                        if (_progress != null)
                            _progress(_crowTot);
                        var dcol = grdr.ReadColumn(_info._flds[_info._map[icol].index]);
                        var arr = dcol.Data;
                        Validation.Assert(arr.LongLength >= crow);
                        cols[icol] = arr;
                    }

                    // Post items to the bldr on every call back to progress.
                    for (int rowSrc = 0; rowSrc < crow;)
                    {
                        int crowCur = Math.Min(crow - rowSrc, buf.Length);
                        _maker(buf, crowCur, rowSrc, cols);
                        _bldr.AddMulti(buf.AsSpan(0, crowCur));
                        _crowTot += crowCur;
                        _progress?.Invoke(_crowTot);
                        rowSrc += crowCur;
                    }
                }
            }
            catch (Exception ex)
            {
                _bldr.Quit(ex);
                Validation.Assert(!_bldr.IsActive);
                throw;
            }

            Validation.Assert(_bldr.IsActive);
            _bldr.Done();
            Validation.Assert(!_bldr.IsActive);
        }
    }

    /// <summary>
    /// Create a "maker" function with the following signature:
    /// <code>
    /// void Make(TRec[] recs, int count, int rowSrc, Array[] values)
    /// </code>
    /// The function is wrapped in a delegate and returned. The function creates and fills
    /// records from the values for the indicated slots.
    /// </summary>
    private static Action<TRec[], int, int, Array[]> CreateMaker<TRec>(ParquetInfo info)
        where TRec : RecordBase
    {
        // Set this to Position or Size to dump the IL to console.
        ILLogKind logKind = ILLogKind.None;

        // We use an "instance" delegate, with the RecordRuntimeTypeInfo object as the "this".
        MethodGenerator gen = new MethodGenerator("make", logKind, null, typeof(RecordRuntimeTypeInfo),
            typeof(TRec[]), typeof(int), typeof(int), typeof(Array[]));
        Type stDel = typeof(Action<TRec[], int, int, Array[]>);

        RecordRuntimeTypeInfo rrtiThis = null;

        // First grab the locals we'll use a lot so they have small indices, to reduce the IL size.
        {
            // The record generator will re-grab this for real later.
            using var locRec = gen.AcquireLocal(typeof(TRec));
        }

        int ccol = info._map.Count;

        var ilw = gen.Il;

        // Store the arrays in strongly typed locals to avoid type testing on every value.
        // REVIEW: Do we need to be concerned about this limiting the number of columns?
        var arrs = new MethodGenerator.Local[ccol];
        for (int col = 0; col < ccol; col++)
        {
            var (name, index, meth) = info._map[col];
            Validation.Assert(meth == null || meth.IsStatic);
            Validation.Assert(meth == null || meth.GetParameters().Length == 1);

            info._typeRec.TryGetNameType(name, out var typeFld).Verify();
            info._tm.TryEnsureSysType(typeFld, out var stFldDst).Verify();
            Validation.Assert(meth == null || meth.ReturnType == stFldDst);

            var stFldSrc = meth == null ? stFldDst : meth.GetParameters()[0].ParameterType;
            Type stArr = stFldSrc.MakeArrayType();
            arrs[col] = gen.AcquireLocal(stArr);
            ilw
                // vals = values[col];
                .Ldarg(4)
                .Ldc_I4(col)
                .Ldelem(stArr)
                .Stloc(arrs[col]);
        }

        // Start the loop and create the record.
        using var locSlot = gen.AcquireLocal(typeof(int));
        Label labTop = ilw.DefineLabel();
        Label labTest = default;
        ilw
            // slot = 0;
            .Ldc_I4(0)
            .Stloc(locSlot)
            // goto LTest;
            .Br(ref labTest)
            // LTop:
            .MarkLabel(labTop);

        void GenLoadRrti(RecordRuntimeTypeInfo rrti)
        {
            Validation.Assert(rrtiThis is null);
            Validation.AssertValue(rrti);
            rrtiThis = rrti;
            ilw.Ldarg(0);
        }

        using var rg = info._tm.CreateRecordGenerator(gen, info._typeRec, GenLoadRrti);
        Validation.Assert(rg.RecSysType == typeof(TRec));

        // For each column, copy the column/flag values to the field of the record.
        for (int col = 0; col < ccol; col++)
        {
            var (name, index, meth) = info._map[col];
            info._typeRec.TryGetNameType(name, out var typeFld).Verify();
            info._tm.TryEnsureSysType(typeFld, out var stFldDst).Verify();

            var stFldSrc = meth == null ? stFldDst : meth.GetParameters()[0].ParameterType;
            Type stArr = stFldSrc.MakeArrayType();
            Label labNextCol = default;

            rg.SetFromStackPre(name, typeFld);
            ilw
                // vals[rowSrc];
                .Ldloc(arrs[col])
                .Ldarg(3);

            ilw.Ldelem(stFldSrc);
            if (meth != null)
                ilw.Call(meth);
            rg.SetFromStackPost();

            ilw.MarkLabelIfUsed(labNextCol);
        }

        ilw
            // recs[slot] = <record>;
            .Ldarg(1)
            .Ldloc(locSlot);
        rg.Finish();
        ilw
            .Stelem(typeof(TRec))
            // rowSrc++;
            .Ldarg(3)
            .Ldc_I4(1)
            .Add()
            .Starg(3)
            // slot++;
            .Ldloc(locSlot)
            .Ldc_I4(1)
            .Add()
            .Stloc(locSlot)
            // LTest:
            .MarkLabel(labTest)
            // if (slot < count) goto LTop;
            .Ldloc(locSlot)
            .Ldarg(2)
            .Blt(ref labTop);

        ilw.Ret();

        if (logKind != ILLogKind.None)
        {
            var code = gen.GetIL();
            Console.WriteLine(code);
        }

        // Get the delegate, with "progress" as "this".
        return (Action<TRec[], int, int, Array[]>)gen.CreateInstanceDelegate(stDel, rrtiThis);
    }
}

/// <summary>
/// Class to write a parquet file from a rexl table. This is bootstrap quality, not ready for shipping.
/// REVIEW: Need to refine/polish this.
/// </summary>
public static class RexlParquetWriter
{
    /// <summary>
    /// Create a parquet file writer delegate for the given table <paramref name="type"/>.
    /// The signature of the delegate is:
    /// <code>
    /// long Write(IEnumerable&lt;TRec&gt; table, Stream stream, Action&lt;long&gt; progress, long freq)
    /// </code>
    /// where <c>TRec</c> is the system type for the record items of the table type, and the
    /// return value is the number of groups in the parquet file.
    /// </summary>
    public static Delegate GetWriter(TypeManager tm, DType type, int recsPerGroup = 5000)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(type.IsTableXxx, nameof(type));
        var typeRec = type.ItemTypeOrThis;
        Validation.BugCheckParam(tm.TryEnsureSysType(typeRec, out var stRec), nameof(type));

        if (recsPerGroup <= 0)
            recsPerGroup = 5000;
        else if (recsPerGroup > 1_000_000)
            recsPerGroup = 1_000_000;

        var meth = new Func<TypeManager, DType, int, Func<IEnumerable<RecordBase>, Stream, Action<long>, long, long>>(GetWriter<RecordBase>).Method
            .GetGenericMethodDefinition().MakeGenericMethod(stRec);

        return (Delegate)meth.Invoke(null, new object[] { tm, type, recsPerGroup });
    }

    private static Func<IEnumerable<TRec>, Stream, Action<long>, long, long> GetWriter<TRec>(TypeManager tm, DType type, int recsPerGroup)
        where TRec : RecordBase
    {
        Validation.AssertValue(tm);
        Validation.Assert(type.IsTableXxx);
        Validation.Assert(recsPerGroup > 0);

        var typeRec = type.ItemTypeOrThis;
        var mapping = new List<(DName name, MethodInfo meth)>(typeRec.FieldCount);
        var flds = new List<DataField>(typeRec.FieldCount);
        foreach (var tn in typeRec.GetNames())
        {
            var typeFld = tn.Type;
            if (!typeFld.IsPrimitiveXxx)
                continue;

            // REVIEW: Support opt types.
            if (!tm.TryEnsureSysType(typeFld, out Type stFld))
                continue;

            Type stFldPar = stFld;
            MethodInfo meth = null;
            switch (typeFld.Kind)
            {
            default:
                continue;

            // REVIEW: Support other primitives.
            case DKind.Text:
            case DKind.R8:
            case DKind.R4:
            case DKind.I8:
            case DKind.I4:
            case DKind.I2:
            case DKind.I1:
            case DKind.U8:
            case DKind.U4:
            case DKind.U2:
            case DKind.U1:
            case DKind.Bit:
            case DKind.Time:
                break;

            case DKind.Date:
                // Need to translate for date.
                if (!typeFld.IsOpt)
                {
                    Validation.Assert(stFld == typeof(RDate));
                    stFldPar = typeof(DateTimeOffset);
                    meth = new Func<RDate, DateTimeOffset>(ToDto).Method;
                }
                else
                {
                    Validation.Assert(stFld == typeof(RDate?));
                    stFldPar = typeof(DateTimeOffset?);
                    meth = new Func<RDate?, DateTimeOffset?>(ToDto).Method;
                }
                break;
            }

#if DEBUG
            if (meth == null)
                Validation.Assert(meth == null);
            else
            {
                Validation.Assert(meth.IsStatic);
                Validation.Assert(meth.ReturnType == stFldPar);
                var parms = meth.GetParameters();
                Validation.Assert(parms.Length == 1);
                Validation.Assert(parms[0].ParameterType == stFld);
            }
#endif

            mapping.Add((tn.Name, meth));
            flds.Add(new DataField(tn.Name.Value, stFldPar));
        }
        Validation.Assert(flds.Count == mapping.Count);

        var schema = new Schema(flds);
        var fn = CreateExtractor<TRec>(tm, typeRec, mapping);

        return (seq, stream, progress, freq) =>
        {
            // Note: since the parquet writer is only instantiated if this function is
            // called, there is no cleanup to be done if it is never called.
            using var wrt = new ParquetWriter(schema, stream);
            var recs = new TRec[recsPerGroup];
            long grp = 0;
            int crow = 0;
            long crowTot = 0;
            if (seq == null)
                seq = Array.Empty<TRec>();
            using var ator = seq.GetEnumerator();
            for (; ; )
            {
                Validation.AssertIndexInclusive(crow, recs.Length);
                bool more = ator.MoveNext();
                if (crow >= recs.Length || !more && (crow > 0 || grp == 0))
                {
                    // Create a new row group.
                    var arrs = fn(recs, crow);
                    Validation.Assert(arrs.Length == flds.Count);
                    using var gwrt = wrt.CreateRowGroup();
                    for (int i = 0; i < flds.Count; i++)
                        gwrt.WriteColumn(new DataColumn(flds[i], arrs[i]));
                    crow = 0;
                    grp++;
                    if (progress != null)
                        progress(crowTot);
                }
                if (!more)
                    break;
                if (progress != null && (freq <= 1 || crowTot % freq == 0))
                    progress(crowTot);
                recs[crow++] = ator.Current;
                crowTot++;
            }
            return grp;
        };
    }

    /// <summary>
    /// Create an "extractor" function with the following signature:
    /// <code>
    ///   Array[] Extract(TRec[] recs, int count)
    /// </code>
    /// The function is wrapped in a delegate and returned. The function creates and fills
    /// column arrays from the given records.
    /// </summary>
    private static Func<TRec[], int, Array[]> CreateExtractor<TRec>(TypeManager tm, DType typeRec,
            List<(DName name, MethodInfo meth)> mapping)
        where TRec : RecordBase
    {
        // Set this to Position or Size to dump the IL to console.
        ILLogKind logKind = ILLogKind.None;

        // We use an "instance" delegate, with a null object as the "this".
        MethodGenerator gen = new MethodGenerator("extract", logKind, typeof(Array[]), typeof(object), typeof(TRec[]), typeof(int));
        Type stDel = typeof(Func<TRec[], int, Array[]>);

        // The local for the current record.
        using var locRec = gen.AcquireLocal(typeof(TRec));

        int ccol = mapping.Count;

        var ilw = gen.Il;

        // Store the arrays in strongly typed locals to avoid type testing on every value.
        // REVIEW: Do we need to be concerned about this limiting the number of columns?
        using var locRes = gen.AcquireLocal(typeof(Array[]));
        ilw
            .Ldc_I4(ccol)
            .Newarr(typeof(Array));

        var arrs = new MethodGenerator.Local[ccol];
        for (int col = 0; col < ccol; col++)
        {
            var (name, meth) = mapping[col];
            typeRec.TryGetNameType(name, out var typeFld).Verify();
            tm.TryEnsureSysType(typeFld, out var stFld).Verify();
            Type stFldDst = meth != null ? meth.ReturnType : stFld;

            Type stArr = stFld.MakeArrayType();
            arrs[col] = gen.AcquireLocal(stArr);
            ilw
                // res[col] = new T[count];
                .Dup()
                .Ldc_I4(col)
                .Ldarg(2)
                .Newarr(stFldDst)
                .Dup()
                .Stloc(arrs[col])
                .Stelem(typeof(Array));
        }

        ilw.Stloc(locRes);

        // Start the loop and get the record.
        using var locSlot = gen.AcquireLocal(typeof(int));
        Label labTop = ilw.DefineLabel();
        Label labNextRec = default;
        Label labTest = default;
        ilw
            // slot = 0;
            .Ldc_I4(0)
            .Stloc(locSlot)
            // goto LTest;
            .Br(ref labTest)
            // LTop:
            .MarkLabel(labTop)
            // Put the current record in the local.
            .Ldarg(1)
            .Ldloc(locSlot)
            .Ldelem(typeof(TRec))
            .Dup()
            .Stloc(locRec)
            .Brfalse(ref labNextRec);

        // For each column, copy the column/flag values to the field of the record.
        for (int col = 0; col < ccol; col++)
        {
            var (name, meth) = mapping[col];
            typeRec.TryGetNameType(name, out var typeFld).Verify();
            tm.TryEnsureSysType(typeFld, out var stFld).Verify();
            Type stFldDst = meth != null ? meth.ReturnType : stFld;

            // vals[slot] = rec.Fld
            ilw
                .Ldloc(arrs[col])
                .Ldloc(locSlot)
                .Ldloc(locRec);
            tm.GenLoadField(gen, typeRec, typeof(TRec), name, typeFld);

            if (meth != null)
                ilw.Call(meth);
            ilw.Stelem(stFldDst);
        }

        ilw
            .MarkLabel(labNextRec)
            // slot++;
            .Ldloc(locSlot)
            .Ldc_I4(1)
            .Add()
            .Stloc(locSlot)
            // LTest:
            .MarkLabel(labTest)
            // if (slot < count) goto LTop;
            .Ldloc(locSlot)
            .Ldarg(2)
            .Blt(ref labTop);

        ilw
            .Ldloc(locRes)
            .Ret();

        if (logKind != ILLogKind.None)
        {
            var code = gen.GetIL();
            Console.WriteLine(code);
        }

        // Get the delegate, with null as "this".
        return (Func<TRec[], int, Array[]>)gen.CreateInstanceDelegate(stDel, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTimeOffset ToDto(RDate dt)
    {
        return new DateTimeOffset(RDate.ToSys(dt), default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTimeOffset? ToDto(RDate? dt)
    {
        if (dt == null)
            return null;
        return new DateTimeOffset(RDate.ToSys(dt.GetValueOrDefault()), default);
    }
}
