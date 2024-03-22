// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Flow;

using FieldMap = ReadOnly.Dictionary<DName, DName>;
using RowMap = ReadOnly.Array<(int min, int lim)>;

/// <summary>
/// Utilities for creating and getting information from a row map.
/// </summary>
public static class RowMapUtil
{
    /// <summary>
    /// Clip the ranges to <paramref name="limSrc"/> and filter out empty ones.
    /// Replace negative min with 0 and negative lim with <paramref name="limSrc"/>.
    /// </summary>
    public static IEnumerable<(int min, int lim)> CleanRanges(this RowMap rowMap, int limSrc)
    {
        Validation.Assert(!rowMap.IsDefault);
        Validation.Assert(limSrc >= 0);

        foreach (var rng in rowMap)
        {
            var (min, lim) = rng;
            if (min < 0)
                min = 0;
            if ((uint)lim > (uint)limSrc)
                lim = limSrc;
            if (min < lim)
                yield return (min, lim);
        }
    }

    /// <summary>
    /// Given a row map, consisting of ranges, return the min and lim of the union of the ranges.
    /// This interprets a negative min as zero, a negative lim as int.MaxValue, and ignores empty
    /// ranges.
    /// </summary>
    public static (int min, int lim, long count, int numRng) GetUnion(this RowMap rowMap, int limSrc = int.MaxValue)
    {
        Validation.Assert(!rowMap.IsDefault);

        // Get the min and lim across all ranges.
        int minAll = limSrc;
        int limAll = 0;
        long count = 0;
        int numRng = 0;
        foreach (var rng in rowMap.CleanRanges(limSrc))
        {
            var (min, lim) = rng;
            Validation.Assert(0 <= min & min < lim & lim <= limSrc);
            if (minAll > min)
                minAll = min;
            if (limAll < lim)
                limAll = lim;
            count += lim - min;
            numRng++;
        }

        return (minAll, limAll, count, numRng);
    }

    /// <summary>
    /// Create a row map from a params array of ranges.
    /// </summary>
    public static RowMap Create(params (int min, int lim)[] ranges)
    {
        return ranges;
    }
}

/// <summary>
/// Represents a "clipping" of information for copy/paste/clipboard support.
/// The clipping is intended to be immutable, that is, the semantic information
/// of this information must not change over time.
/// </summary>
public abstract class DataClip : ICanCount
{
    private long _dataLength = -1;
    private long _curDataLength = 0;

    /// <summary>
    /// This is the type for the entire data, not just an item (in the case of a sequence type).
    /// This type may include fields/columns that aren't part of the true clip data.
    /// </summary>
    public DType RawDataType { get; }

    /// <summary>
    /// If this clip contains a sequence, this is the item type of the sequence type.
    /// Otherwise it is the same as <see cref="RawDataType"/>.
    /// </summary>
    public DType RawItemType => RawDataType.ItemTypeOrThis;

    /// <summary>
    /// The item type for the clip data. When <see cref="RawItemType"/> is a record type,
    /// this will also be a record type. The fields of this will be drawn from the raw fields,
    /// but potentially renamed and even duplicated (with distinct names).
    /// </summary>
    public abstract DType ClipItemType { get; }

    /// <summary>
    /// Whether this has field mapping. If not, then <see cref="ClipItemType"/> will equal
    /// <see cref="RawItemType"/>.
    /// </summary>
    public abstract bool MapsFields { get; }

    /// <summary>
    /// Whether this clip contains sequence information.
    /// </summary>
    public bool IsSequence => RawDataType.IsSequence;

    /// <summary>
    /// Whether this clip contains a sequence of (required) record or single (required) record.
    /// REVIEW: Any reason to support opt?
    /// </summary>
    public bool HasFields => RawItemType.IsRecordReq;

    protected DataClip(DType typeRaw)
    {
        Validation.BugCheckParam(typeRaw.IsValid, nameof(typeRaw));
        RawDataType = typeRaw;
    }

    /// <summary>
    /// Map from clip item type field name to raw item type field name.
    /// </summary>
    public abstract DName ClipFieldToRawField(DName nameFld);

    /// <summary>
    /// Return a <see cref="DataClip"/> that has the fields mapped according to the given
    /// <paramref name="fieldMap"/>. Note that the given field map should map from new
    /// field name to existing clip field name, NOT to existing raw field name.
    /// </summary>
    public abstract DataClip MapFields(FieldMap fieldMap);

    /// <summary>
    /// Return a <see cref="DataClip"/> that has the fields mapped according to the given
    /// <paramref name="fieldMap"/> and the rows indicated in <paramref name="rowMap"/>.
    /// Note that the given field map should map from new field name to existing clip field
    /// name, NOT to existing raw field name. Either or both of the maps may be default.
    /// </summary>
    public abstract DataClip CreateSubClip(FieldMap fieldMap = default, RowMap rowMap = default);

    /// <summary>
    /// Return the number of items, computing it if required.
    /// </summary>
    public abstract long GetCount(Action callback);

    public abstract bool TryGetCount(out long count);

    /// <summary>
    /// Write the serialized clip items to the specified <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream"></param>
    protected abstract void WriteClipItems(Stream stream);

    /// <summary>
    /// Try to get the length in bytes of this clip serialized data.
    /// If this clip already knows its length, sets <paramref name="length"/> to the final
    /// clip data length and returns true.
    /// If this DataValueClip does not already know its length, this method will serialize its
    /// data up to the specified <paramref name="limCheck"/> byte limit.
    /// If the final length was successfully determined, e.g. when it is less than <paramref name="limCheck"/>
    /// the function will return true and set <paramref name="length"/> to the actual clip data length.
    /// If the final length was not successfully determined, e.g. when it is greater or equal to
    /// <paramref name="limCheck"/> the function will return false and set <paramref name="length"/>
    /// to the highest known lower bound of this clip data length.
    /// </summary>
    /// <remarks>
    /// Callers should use this function before accessing the clip data when they only
    /// want to use the clip data if its length in bytes is less than <paramref name="limCheck"/>.
    /// This avoids building the clip data if it is not going to be used, or unecessarily enumerating
    /// over the input sequence.
    /// </remarks>
    public virtual bool TryGetDataLength(long limCheck, out long length)
    {
        // The clip data length is already known.
        if (_dataLength >= 0)
        {
            length = _dataLength;
            return true;
        }

        // The final clip data length is not known but we know it is higher than the requested limit.
        if (_curDataLength >= limCheck)
        {
            length = _curDataLength;
            return false;
        }

        // The count of rows is known and the clip type contains only fixed size numeric fields.
        // No need to try to serialize the stream, the length can be evaluated more efficiently.
        if (TryGetCount(out var count))
        {
            int rowSize = 0;

            foreach (var size in GetFieldsSize(ClipItemType))
            {
                if (size < 0 || size > 8)
                {
                    rowSize = -1;
                    break;
                }
                rowSize += size == 0 ? 1 : size;
            }

            if (rowSize >= 0)
            {
                _dataLength = count * rowSize;
                length = _dataLength;
                return true;
            }
        }

        // Try to serialize the clip data up to the specified limit.
        using var stream = new LengthOnlyStream(limCheck);
        try
        {
            WriteClipItems(stream);
        }
        catch (LengthOnlyStream.LengthExceededException)
        {
            _curDataLength = stream.Length;
            length = _curDataLength;
            return false;
        }
        catch (Exception e) when (e.InnerException is LengthOnlyStream.LengthExceededException)
        {
            _curDataLength = stream.Length;
            length = _curDataLength;
            return false;
        }

        _dataLength = stream.Length;
        length = _dataLength;
        return true;
    }

    protected static IEnumerable<int> GetFieldsSize(DType type)
    {
        if (!type.IsRecordXxx)
        {
            yield return type.Kind.NumericSize();
            yield break;
        }

        foreach (var typedName in type.GetNames())
            yield return typedName.Type.Kind.NumericSize();
    }

    /// <summary>
    /// A helper <see cref="Stream"/> class that only writes the length.
    /// If the length of the data being written exceeds the specified limit, the <see cref="Write(byte[], int, int)"/>
    /// function will throw a <see cref="LengthExceededException"/> exception.
    /// </summary>
    private sealed class LengthOnlyStream : Stream
    {
        private readonly long _lengthLim;
        private long _length;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public LengthOnlyStream(long lengthLim)
        {
            _lengthLim = lengthLim;
            _length = 0;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Validation.Assert(false);
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Validation.Assert(false);
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            Validation.Assert(false);
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Validation.AssertValue(buffer);
            Validation.Assert(offset >= 0);
            Validation.Assert(0 < count && offset + count <= buffer.Length);

            _length += count;
            if (_length > _lengthLim)
                throw new LengthExceededException();
        }

        public sealed class LengthExceededException : Exception
        {
        }
    }
}

/// <summary>
/// A data clip that exposes a value object, items enumerable, the value and item
/// system types, and the type manager that produced those system types.
/// </summary>
public abstract class DataValueClip : DataClip, ICanCount
{
    public sealed override DType ClipItemType { get; }

    public sealed override bool MapsFields => !_fieldMap.IsDefault;

    /// <summary>
    /// The type manager that produced the system types.
    /// </summary>
    public TypeManager TypeManager { get; }

    /// <summary>
    /// The system type for the raw data value.
    /// </summary>
    public Type RawSysType { get; }

    /// <summary>
    /// The item system type for the raw data. When the data is non-sequence, this is
    /// the same as <see cref="RawSysType"/>.
    /// </summary>
    public Type RawItemSysType { get; }

    /// <summary>
    /// The raw data value. This is either null (if allowed by <see cref="DataClip.RawDataType"/>) or
    /// of type <see cref="RawSysType"/>.
    /// </summary>
    public abstract object RawValueObject { get; }

    /// <summary>
    /// This is always a non-null enumerable. When <see cref="DataClip.RawDataType"/> is not
    /// a sequence type, this is a single element enumerable containing the raw data value. When
    /// <see cref="DataClip.RawDataType"/> is a sequence type, this is the sequence data value,
    /// if it is non-null, or an empty enumerable if the sequence data value is null.
    /// </summary>
    public abstract IEnumerable RawItemsEnumerable { get; }

    /// <summary>
    /// Maps from clip item type field name to raw item type field name.
    /// </summary>
    protected readonly FieldMap _fieldMap;

    protected DataValueClip(TypeManager tm, DType typeRaw, FieldMap fieldMap)
        : base(typeRaw)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(tm.TryEnsureSysType(typeRaw, out Type stRaw), nameof(typeRaw));
        Validation.BugCheckParam(tm.TryEnsureSysType(typeRaw.ItemTypeOrThis, out Type stItemRaw), nameof(typeRaw));

        TypeManager = tm;
        RawSysType = stRaw;
        RawItemSysType = stItemRaw;

        if (!fieldMap.IsDefault)
        {
            Validation.BugCheckParam(HasFields, nameof(fieldMap));
            DType typeRec = DType.EmptyRecordReq;
            foreach (var kvp in fieldMap)
            {
                Validation.BugCheckParam(typeRaw.TryGetNameType(kvp.Value, out DType typeFld), nameof(fieldMap));
                typeRec = typeRec.SetNameType(kvp.Key, typeFld);
            }
            _fieldMap = fieldMap;
            ClipItemType = typeRec;
        }
        else
            ClipItemType = RawItemType;
    }

    /// <summary>
    /// Create an instance of a data value clip wrapping the given data value (singleton or sequence).
    /// </summary>
    public static DataValueClip Create(TypeManager tm, DType type, object value, FieldMap fieldMap = default, RowMap rowMap = default)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(tm.TryEnsureSysType(type, out Type st), nameof(type));
        Validation.BugCheckParam(value == null || st.IsAssignableFrom(value.GetType()), nameof(value));

        if (!type.IsSequence)
        {
            Validation.BugCheckParam(value != null || type.IsOpt, nameof(value));
            var meth = new Func<TypeManager, DType, object, FieldMap, RowMap, DataValueClip>(CreateOne)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(st);
            return (DataValueClip)meth.Invoke(null, new object[] { tm, type, value, fieldMap, rowMap });
        }
        else
        {
            Validation.BugCheckParam(tm.TryEnsureSysType(type.ItemTypeOrThis, out Type stItem), nameof(type));
            Validation.BugCheckParam(typeof(IEnumerable<>).MakeGenericType(stItem).IsAssignableFrom(st), nameof(type));
            var meth = new Func<TypeManager, DType, IEnumerable<object>, FieldMap, RowMap, DataValueClip>(CreateSeq)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
            return (DataValueClip)meth.Invoke(null, new object[] { tm, type, value, fieldMap, rowMap });
        }
    }

    /// <summary>
    /// Create a singleton data value clip.
    /// </summary>
    public static DataValueClip<T> CreateOne<T>(TypeManager tm, DType type, T value, FieldMap fieldMap = default, RowMap rowMap = default)
    {
        if (rowMap.IsDefaultOrEmpty)
            return new DataValueClipOne<T>(tm, type, value, fieldMap);

        // Get the min and lim across all ranges.
        var (minAll, limAll, count, numRng) = rowMap.GetUnion(limSrc: 1);
        Validation.Assert(0 <= minAll && minAll <= 1);
        Validation.Assert(0 <= limAll && limAll <= 1);

        if (count == 1)
        {
            Validation.Assert(minAll == 0);
            Validation.Assert(limAll == 1);
            Validation.Assert(numRng == 1);
            return new DataValueClipOne<T>(tm, type, value, fieldMap);
        }

        return DataValueClipSeq<T>.Create(tm, type, null, fieldMap);
    }

    /// <summary>
    /// Create a sequence data value clip.
    /// </summary>
    public static DataValueClipSeq<TItem> CreateSeq<TItem>(
        TypeManager tm, DType type, IEnumerable<TItem> value, FieldMap fieldMap = default, RowMap rowMap = default)
    {
        return DataValueClipSeq<TItem>.Create(tm, type, value, fieldMap, rowMap);
    }

    public sealed override DName ClipFieldToRawField(DName nameFld)
    {
        if (_fieldMap.IsDefault)
        {
            Validation.Assert(ClipItemType == RawItemType);
            Validation.BugCheckParam(ClipItemType.Contains(nameFld), nameof(nameFld));
            return nameFld;
        }

        Validation.BugCheckParam(_fieldMap.TryGetValue(nameFld, out var nameRaw), nameof(nameFld));
        Validation.Assert(ClipItemType.Contains(nameFld));
        Validation.Assert(RawItemType.Contains(nameRaw));
        return nameRaw;
    }

    public sealed override bool TryGetDataLength(long limCheck, out long length)
    {
        // REVIEW: This function cannot be called when a field map that is not a 1-1 mapping is defined
        // unless the clip count is known and the field types are all fixed size numeric types.
        Validation.BugCheck(
            _fieldMap.IsDefault ||
            _fieldMap.Select(x => x.Value).Distinct().Count() == RawItemType.FieldCount ||
            TryGetCount(out var _) && GetFieldsSize(ClipItemType).Any(s => s >= 0 && s <= 8));

        return base.TryGetDataLength(limCheck, out length);
    }

    protected FieldMap ComposeFieldMaps(FieldMap fieldMap)
    {
        Validation.Assert(!_fieldMap.IsDefault);
        Validation.Assert(!fieldMap.IsDefault);

        var result = new Dictionary<DName, DName>(fieldMap.Count);
        foreach (var kvp in fieldMap)
        {
            Validation.BugCheckParam(_fieldMap.TryGetValue(kvp.Value, out var nameRaw), nameof(fieldMap));
            result.Add(kvp.Key, nameRaw);
        }
        return result;
    }
}

/// <summary>
/// A data value clip with raw item type as type parameter.
/// </summary>
public abstract class DataValueClip<TItem> : DataValueClip
{
    public sealed override IEnumerable RawItemsEnumerable => RawItems;

    public abstract IEnumerable<TItem> RawItems { get; }

    protected DataValueClip(TypeManager tm, DType type, FieldMap fieldMap)
        : base(tm, type, fieldMap)
    {
    }

    protected sealed override void WriteClipItems(Stream strm)
    {
        Validation.AssertValue(strm);
        // REVIEW: Add support in TypeManager serializer for serializing only a
        // subset of fields for objects where the ItemTypeOrThis is a record type.
        Validation.Assert(_fieldMap.IsDefault || _fieldMap.Select(x => x.Value).Distinct().Count() == RawItemType.FieldCount);

        // REVIEW: Can this be improved?
        // We need to split the input sequence in smaller batches, otherwise when trying to serialize the clip
        // items the serializer will just try to eagerly enumerate the whole sequence.
        const int batchSize = 1000;
        foreach (var itemsBatch in Batch(RawItems, batchSize))
            TypeManager.TryWrite(strm, RawItemType.ToSequence(), itemsBatch);
    }

    private static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> source, int batchSize)
    {
        Validation.AssertValue(source);
        Validation.Assert(batchSize > 0);

        static IEnumerable<T> YieldBatchItems(IEnumerator<T> source, int batchSize)
        {
            yield return source.Current;
            for (int i = 1; i <= batchSize && source.MoveNext(); i++)
                yield return source.Current;
        }

        using var ator = source.GetEnumerator();
        while (ator.MoveNext())
            yield return YieldBatchItems(ator, batchSize);
    }
}

/// <summary>
/// A non-sequence data value clip.
/// </summary>
public sealed class DataValueClipOne<T> : DataValueClip<T>
{
    /// <summary>
    /// The (non-sequence) data value.
    /// </summary>
    public T RawValue { get; }

    public override object RawValueObject => RawValue;

    public override IEnumerable<T> RawItems
    {
        get { yield return RawValue; }
    }

    public override long GetCount(Action callback)
    {
        Validation.BugCheckValueOrNull(callback);
        return 1;
    }

    public override bool TryGetCount(out long count)
    {
        count = 1;
        return true;
    }

    public DataValueClipOne(TypeManager tm, DType type, T value, FieldMap fieldMap = default)
        : base(tm, type, fieldMap)
    {
        Validation.BugCheckParam(!IsSequence, nameof(type));
        Validation.Assert(RawSysType == RawItemSysType);
        Validation.BugCheckParam(typeof(T).IsAssignableFrom(RawSysType), nameof(type));
        Validation.BugCheckParam(value != null || type.IsOpt, nameof(value));

        RawValue = value;
    }

    public override DataClip MapFields(FieldMap fieldMap)
    {
        if (fieldMap.IsDefault)
            return this;
        if (!_fieldMap.IsDefault)
            fieldMap = ComposeFieldMaps(fieldMap);
        return new DataValueClipOne<T>(TypeManager, RawDataType, RawValue, fieldMap);
    }

    public override DataClip CreateSubClip(FieldMap fieldMap = default, RowMap rowMap = default)
    {
        if (rowMap.IsDefault && fieldMap.IsDefault)
            return this;
        return CreateOne<T>(TypeManager, RawDataType, RawValue, fieldMap, rowMap);
    }
}

/// <summary>
/// A sequence data value clip.
/// This clip data is built lazily when its items are requested, and its construction is thread safe.
/// </summary>
public abstract class DataValueClipSeq<TItem> : DataValueClip<TItem>, ICanCount
{
    private long _count;

    private DataValueClipSeq(TypeManager tm, DType type, FieldMap fieldMap, long count)
        : base(tm, type, fieldMap)
    {
        _count = count;
    }

    /// <summary>
    /// The raw (sequence) data value.
    /// </summary>
    public abstract IEnumerable<TItem> RawValue { get; }

    public static DataValueClipSeq<TItem> Create(
        TypeManager tm, DType type, IEnumerable<TItem> seq, FieldMap fieldMap = default, RowMap rowMap = default)
    {
        if (seq == null)
            return new SimpleDataValueClipSeq(tm, type, seq, fieldMap, 0);

        var cursorableSeq = seq as ICursorable<TItem>;
        long seqCount = -1;
        if (seq is ICollection<TItem> col)
            seqCount = col.Count;
        else if (seq is ICanCount countable && countable.TryGetCount(out var c))
            seqCount = c;
        if (rowMap.IsDefault)
        {
            if (cursorableSeq == null)
                return new SimpleDataValueClipSeq(tm, type, seq, fieldMap, seqCount);
            else
                return new SimpleCachingDataValueClipSeq(tm, type, cursorableSeq, fieldMap, seqCount);
        }

        // Get the info across all ranges in the row map.
        // REVIEW: Make the RowMap accept long for ranges.
        var (minAll, limAll, count, numRng) = rowMap.GetUnion(seqCount >= 0 ? (int)Math.Min(seqCount, int.MaxValue) : int.MaxValue);
        Validation.Assert(minAll >= 0);
        Validation.Assert(limAll >= 0);
        Validation.Assert(count >= 0);
        if (seqCount >= 0)
            Validation.BugCheckParam(count <= int.MaxValue, nameof(rowMap), "Too many rows");

        if (count == 0)
            return new SimpleDataValueClipSeq(tm, type, Array.Empty<TItem>(), fieldMap, 0);
        else if (count == seqCount)
        {
            if (cursorableSeq == null)
                return new SimpleDataValueClipSeq(tm, type, seq, fieldMap, count);
            else
                return new SimpleCachingDataValueClipSeq(tm, type, cursorableSeq, fieldMap, count);
        }

        return new CachingDataValueClipSeq(tm, type, seq, rowMap, fieldMap, seqCount >= 0 ? count : -1);
    }

    public override long GetCount(Action callback)
    {
        Validation.BugCheckValueOrNull(callback);

        if (_count >= 0)
            return _count;
        if (RawItems is ICanCount countable)
            _count = countable.GetCount(callback);
        else
        {
            Validation.AssertValue(RawValue);
            _count = 0;
            foreach (var _ in RawValue)
            {
                _count++;
                if (callback != null)
                    callback();
            }
        }
        return _count;
    }

    public override bool TryGetCount(out long count)
    {
        if (_count >= 0)
        {
            count = _count;
            return true;
        }
        else if (RawItems is ICanCount countable && countable.TryGetCount(out long c))
        {
            count = c;
            _count = count;
            return true;
        }

        count = -1;
        return false;
    }

    public override DataClip MapFields(FieldMap fieldMap)
    {
        if (fieldMap.IsDefault)
            return this;
        if (!_fieldMap.IsDefault)
            fieldMap = ComposeFieldMaps(fieldMap);
        return RawValue is ICursorable<TItem> cursorableSeq ?
            (DataClip)new SimpleCachingDataValueClipSeq(TypeManager, RawDataType, cursorableSeq, fieldMap, _count) :
            (DataClip)new SimpleDataValueClipSeq(TypeManager, RawDataType, RawValue, fieldMap, _count);
    }

    public override DataClip CreateSubClip(FieldMap fieldMap = default, ReadOnly.Array<(int min, int lim)> rowMap = default)
    {
        if (fieldMap.IsDefault && rowMap.IsDefault)
            return this;
        return DataValueClip.CreateSeq<TItem>(TypeManager, RawDataType, RawItems, fieldMap, rowMap);
    }

    /// <summary>
    /// A simple sequence data value with no row mapping specified.
    /// The clip data is just the input sequence it is built from.
    /// </summary>
    private sealed class SimpleDataValueClipSeq : DataValueClipSeq<TItem>
    {
        private readonly IEnumerable<TItem> _rawValue;
        private readonly IEnumerable<TItem> _rawItems;

        public override IEnumerable<TItem> RawValue => _rawValue;

        public override object RawValueObject => RawValue;

        public override IEnumerable<TItem> RawItems => _rawItems;

        public SimpleDataValueClipSeq(TypeManager tm, DType type, IEnumerable<TItem> seq, FieldMap fieldMap, long count)
            : base(tm, type, fieldMap, count)
        {
            Validation.Assert(IsSequence, nameof(type));
            Validation.Assert(typeof(IEnumerable<TItem>).IsAssignableFrom(RawSysType), nameof(type));
            Validation.Assert(seq == null || RawSysType.IsAssignableFrom(seq.GetType()), nameof(seq));
            Validation.Assert(!(seq is ICursorable<TItem>));
            _rawValue = seq;
            _rawItems = seq ?? Array.Empty<TItem>();
        }
    }

    /// <summary>
    /// A sequence data value clip with caching.
    /// </summary>
    private class SimpleCachingDataValueClipSeq : DataValueClipSeq<TItem>, ICursorable<TItem>
    {
        private readonly ICursorable<TItem> _rawValue;

        public override IEnumerable<TItem> RawValue => _rawValue;

        public override object RawValueObject => _rawValue;

        public override IEnumerable<TItem> RawItems => _rawValue;

        public SimpleCachingDataValueClipSeq(TypeManager tm, DType type, ICursorable<TItem> seq, FieldMap fieldMap, long count = -1)
            : base(tm, type, fieldMap, count)
        {
            Validation.Assert(IsSequence, nameof(type));
            Validation.Assert(typeof(IEnumerable<TItem>).IsAssignableFrom(RawSysType), nameof(type));
            Validation.AssertValue(seq, nameof(seq));
            Validation.Assert(RawSysType.IsAssignableFrom(seq.GetType()), nameof(seq));
            _rawValue = seq;
        }

        public ICursor<TItem> GetCursor() => _rawValue.GetCursor();

        ICursor ICursorable.GetCursor() => _rawValue.GetCursor();

        public IEnumerator<TItem> GetEnumerator() => _rawValue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _rawValue.GetEnumerator();
    }

    /// <summary>
    /// A sequence data value clip where the input sequence with the row selection applied
    /// is cached in a <see cref="CachingEnumerable{TItem}"/> 
    /// </summary>
    private sealed class CachingDataValueClipSeq : SimpleCachingDataValueClipSeq
    {
        public CachingDataValueClipSeq(TypeManager tm, DType type, IEnumerable<TItem> seq, RowMap rowMap, FieldMap fieldMap, long count = -1)
            : base(tm, type, new CachingEnumerable<TItem>(GetRawItemsCore(seq, rowMap)), fieldMap, count)
        {
            Validation.AssertValue(seq, nameof(seq));
            Validation.Assert(RawSysType.IsAssignableFrom(seq.GetType()), nameof(seq));
            Validation.Assert(!rowMap.IsDefaultOrEmpty);
        }

        private static IEnumerable<TItem> GetRawItemsCore(IEnumerable<TItem> seq, RowMap rowMap)
        {
            Validation.AssertValue(seq);
            Validation.Assert(!rowMap.IsDefaultOrEmpty);

            var ranges = rowMap.CleanRanges(int.MaxValue);
            Validation.Assert(ranges.Any());
            if (seq is ICursorable<TItem> cursorableSeq)
            {
                var cursor = cursorableSeq.GetCursor();
                foreach (var range in ranges)
                {
                    if (!cursor.MoveTo(range.min))
                        continue;

                    for (int i = range.min; i < range.lim; i++)
                    {
                        yield return cursor.Current;
                        if (!cursor.MoveNext())
                            break;
                    }
                }

                yield break;
            }

            var curId = 0;
            var curPos = seq.GetEnumerator();
            curPos.MoveNext();
            foreach (var range in ranges)
            {
                // REVIEW: Improve this.
                // Currently runtime only sends sorted non overlapping intervals, so this won't happen.
                // But if scenarios require this, we should optimize for it.
                if (curId > range.min)
                {
                    curPos.Dispose();
                    curPos = seq.GetEnumerator();
                    curPos.MoveNext();
                    curId = 0;
                }

                // REVIEW: Support pinging call back to handle time outs for operations taking too long.
                // Get to the range position.
                bool end = false;
                while (curId < range.min)
                {
                    if (!curPos.MoveNext())
                    {
                        end = true;
                        break;
                    }
                    ++curId;
                }

                if (end)
                    continue;

                Validation.Assert(curId == range.min);
                while (curId < range.lim)
                {
                    yield return curPos.Current;
                    if (!curPos.MoveNext())
                        break;
                    ++curId;
                }
            }
            curPos.Dispose();
        }
    }
}
