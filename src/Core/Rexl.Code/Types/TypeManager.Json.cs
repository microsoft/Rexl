// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using Date = RDate;
using Integer = System.Numerics.BigInteger;
using Time = TimeSpan;
using Writer = RawJsonWriter;

partial class TypeManager
{
    private const string TensorJObjectFieldName = "jobj";
    private const string TensorShapeFieldName = "shape";
    private const string TensorBase64FieldName = "base64";
    private const string TensorPngFieldName = "png";
    private const string TensorItemTypeFieldName = "type";

    // UTF8 Versions of the above.
    private static readonly byte[] TensorJObjectFieldNameUtf8 = new byte[] { (byte)'j', (byte)'o', (byte)'b', (byte)'j' };
    private static readonly byte[] TensorShapeFieldNameUtf8 = new byte[] { (byte)'s', (byte)'h', (byte)'a', (byte)'p', (byte)'e' };
    private static readonly byte[] TensorBase64FieldNameUtf8 = new byte[] { (byte)'b', (byte)'a', (byte)'s', (byte)'e', (byte)'6', (byte)'4' };
    private static readonly byte[] TensorPngFieldNameUtf8 = new byte[] { (byte)'p', (byte)'n', (byte)'g' };
    private static readonly byte[] TensorItemTypeFieldNameUtf8 = new byte[] { (byte)'t', (byte)'y', (byte)'p', (byte)'e' };

    /// <summary>
    /// Returns true if <paramref name="kind"/> is supported for Base64 encoding of tensors.
    /// </summary>
    private static bool IsBase64Supported(DKind kind)
    {
        var size = kind.NumericSize();
        // The set of DKinds we support are numeric values with sizes in this range.
        return 1 <= size && size <= 8;
    }

    private delegate T ReadBytesFunc<T>(byte[] buf, int startIndex);
    private delegate bool TryWriteBytesFunc<T>(Span<byte> buf, T value);

    /// <summary>
    /// Type => <see cref="ReadBytesFunc"/> delegate that reads that type from a byte array.
    /// </summary>
    private readonly Dictionary<Type, Delegate> _byteReaders;

    /// <summary>
    /// Type => <see cref="TryWriteBytesFunc"/> delegate that writes that type to a byte span.
    /// </summary>
    private readonly Dictionary<Type, Delegate> _byteWriters;

    /// <summary>
    /// Initialize <see cref="_byteWriters"/> and <see cref="_byteReaders"/> to be able to read
    /// and write all of the types in <see cref="Base64SupportedKinds"/>.
    /// </summary>
    private void InitializeJsonTensorSerializers()
    {
        _byteReaders.Add(typeof(long), (ReadBytesFunc<long>)BitConverter.ToInt64);
        _byteReaders.Add(typeof(int), (ReadBytesFunc<int>)BitConverter.ToInt32);
        _byteReaders.Add(typeof(short), (ReadBytesFunc<short>)BitConverter.ToInt16);
        _byteReaders.Add(typeof(sbyte), (ReadBytesFunc<sbyte>)((buf, startIndex) => (sbyte)buf[startIndex]));
        _byteReaders.Add(typeof(ulong), (ReadBytesFunc<ulong>)BitConverter.ToUInt64);
        _byteReaders.Add(typeof(uint), (ReadBytesFunc<uint>)BitConverter.ToUInt32);
        _byteReaders.Add(typeof(ushort), (ReadBytesFunc<ushort>)BitConverter.ToUInt16);
        _byteReaders.Add(typeof(byte), (ReadBytesFunc<byte>)((buf, startIndex) => buf[startIndex]));
        _byteReaders.Add(typeof(double), (ReadBytesFunc<double>)BitConverter.ToDouble);
        _byteReaders.Add(typeof(float), (ReadBytesFunc<float>)BitConverter.ToSingle);

        _byteWriters.Add(typeof(long), (TryWriteBytesFunc<long>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(int), (TryWriteBytesFunc<int>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(short), (TryWriteBytesFunc<short>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(sbyte), (TryWriteBytesFunc<sbyte>)TryWriteI1);
        _byteWriters.Add(typeof(ulong), (TryWriteBytesFunc<ulong>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(uint), (TryWriteBytesFunc<uint>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(ushort), (TryWriteBytesFunc<ushort>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(byte), (TryWriteBytesFunc<byte>)TryWriteU1);
        _byteWriters.Add(typeof(double), (TryWriteBytesFunc<double>)BitConverter.TryWriteBytes);
        _byteWriters.Add(typeof(float), (TryWriteBytesFunc<float>)BitConverter.TryWriteBytes);
    }

    /// <summary>
    /// Write an I1 value to a byte buffer.
    /// </summary>
    private static bool TryWriteI1(Span<byte> buf, sbyte value)
    {
        if (buf.Length >= 1)
        {
            buf[0] = (byte)value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Write a U1 value to a byte buffer.
    /// </summary>
    private static bool TryWriteU1(Span<byte> buf, byte value)
    {
        if (buf.Length >= 1)
        {
            buf[0] = value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Implements translation from json to rexl runtime values. Generally, instances need to be constructed
    /// by a specific type manager, since encoding can vary. There is no "default" encoding for all kinds
    /// of rexl runtime values, particularly for <see cref="DKind.Uri"/> objects and perhaps others.
    /// </summary>
    public abstract partial class JsonReader
    {
        protected readonly TypeManager _parent;

        private delegate bool TryReadFunc<T>(DType type, JsonElement jelm, out T value);

        /// <summary>
        /// Dictionary from DType -> <see cref="TryReadFunc{T}"/> to read that type.
        /// 
        /// REVIEW: These functions only live as long as the Reader/Writer. Currently, we don't reuse
        /// or share JsonReaders and JsonWriters, but they should be thread safe. We should only create a single
        /// instance per TypeManager, like we do for the binary serializer.
        /// </summary>
        private readonly ConcurrentDictionary<DType, Delegate> _typeToReader;

        protected JsonReader(TypeManager parent)
        {
            Validation.BugCheckValue(parent, nameof(parent));
            _parent = parent;
            _typeToReader = new ConcurrentDictionary<DType, Delegate>();
            AddNumericReaders();
        }

        /// <summary>
        /// Tries to translate from a json string to a rexl runtime object of the given <paramref name="type"/>.
        /// </summary>
        public bool TryRead(DType type, string json, out object value)
        {
            Validation.BugCheckParam(type.IsValid, nameof(type));

            if (string.IsNullOrWhiteSpace(json))
            {
                value = null;
                return type.IsOpt;
            }

            var doc = JsonDocument.Parse(json, options: default);
            return TryReadCore(type, doc.RootElement, out value);
        }

        /// <summary>
        /// Tries to translate from a <see cref="JsonElement"/> to a rexl runtime object of the
        /// given <paramref name="type"/>.
        /// </summary>
        public bool TryRead(DType type, JsonElement jelm, out object value)
        {
            Validation.BugCheckParam(type.IsValid, nameof(type));
            return TryReadCore(type, jelm, out value);
        }

        protected bool TryReadCore(DType type, JsonElement jelm, out object value)
        {
            Validation.Assert(type.IsValid);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                value = null;
                return type.IsOpt;
            }

            if (TryReadNumeric(type, jelm, out value))
                return true;

            switch (type.Kind)
            {
            case DKind.Sequence:
                return TryReadSeq(type.ItemTypeOrThis, jelm, out value);
            case DKind.Record:
                return TryReadRec(type, jelm, out value);
            case DKind.Tuple:
                return TryReadTup(type, jelm, out value);
            case DKind.Tensor:
                return TryWithReader(type, jelm, out value);
            case DKind.Uri:
                if (TryReadUri(type, jelm, out var link))
                {
                    value = link;
                    return true;
                }
                break;
            case DKind.Date:
                if (TryReadDate(type, jelm, out var date))
                {
                    value = date;
                    return true;
                }
                break;
            case DKind.Time:
                if (TryReadTime(type, jelm, out var time))
                {
                    value = time;
                    return true;
                }
                break;
            case DKind.IA:
                if (TryReadIA(type, jelm, out var ia))
                {
                    value = ia;
                    return true;
                }
                break;
            case DKind.Text:
                if (TryReadString(type, jelm, out var str))
                {
                    value = str;
                    return true;
                }
                break;
            case DKind.Guid:
                if (TryReadGuid(type, jelm, out var guid))
                {
                    value = guid;
                    return true;
                }
                break;
            case DKind.Bit:
                if (TryReadBit(type, jelm, out var bit))
                {
                    value = bit;
                    return true;
                }
                break;

            case DKind.General:
            case DKind.Vac:
            default:
                // REVIEW: Handle these.
                break;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Try to read a value using a strongly typed reader from <see cref="TryGetReader"/>.
        /// 
        /// REVIEW: This function is only temporary, while we move towards making this entire class strongly
        /// typed. Eventually, this function should become strongly typed and all reads should go through it.
        /// </summary>
        private bool TryWithReader(DType type, JsonElement jelm, out object value)
        {
            value = null;
            if (!TryGetReader(type, out var fn))
                return false;
            Validation.Assert(fn.GetType() == typeof(TryReadFunc<>).MakeGenericType(_parent.GetSysTypeOrNull(type)));

            var readParams = new object[] { type, jelm, null };
            var result = fn.Method.Invoke(this, readParams);
            value = readParams[2];
            return (bool)result;
        }

        /// <summary>
        /// Get a strongly typed function to read a JSON object of a given DType, or create one if possible.
        /// </summary>
        private bool TryGetReader(DType type, out Delegate fn)
        {
            if (_typeToReader.TryGetValue(type, out fn))
                return true;

            if (type.Kind == DKind.Tensor)
            {
                if (!_parent.TryEnsureSysType(type, out var st))
                    return false;

                if (!_parent.TryEnsureSysType(type.GetTensorItemType(), out var stItem))
                    return false;

                var meth = new TryReadFunc<Tensor<object>>(TryReadTen<object>)
                    .Method.GetGenericMethodDefinition()
                    .MakeGenericMethod(stItem);

                var delType = typeof(TryReadFunc<>)
                    .MakeGenericType(st);

                fn = _typeToReader.GetOrAdd(type, Delegate.CreateDelegate(delType, this, meth));
                return true;
            }

            // REVIEW: Add readers for all types.
            return false;
        }

        protected virtual bool TryReadSeq(DType typeItem, JsonElement jelm, out object value)
        {
            Validation.Assert(typeItem.IsValid);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            value = null;
            if (jelm.ValueKind != JsonValueKind.Array)
                return false;

            int len = jelm.GetArrayLength();
            Validation.Assert(len >= 0);

            // Note that null is a valid representation of an empty sequence, so we can leave
            // value == null.
            if (len == 0)
                return true;

            if (!_parent.TryEnsureSysType(typeItem, out var st))
                return false;

            var arr = Array.CreateInstance(st, len);
            for (int i = 0; i < len; i++)
            {
                var jitem = jelm[i];
                if (!TryReadCore(typeItem, jitem, out var item))
                    return false;
                arr.SetValue(item, i);
            }

            value = arr;
            return true;
        }

        protected virtual bool TryReadRec(DType type, JsonElement jelm, out object value)
        {
            Validation.Assert(type.IsRecordXxx);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            value = null;
            if (jelm.ValueKind != JsonValueKind.Object)
                return false;

            // REVIEW: We should cache record factories in an appropriate place.
            // Perhaps there should be an json to rexl factory for this.
            var fact = _parent.CreateRecordFactory(type);
            var rec = fact.Create().Open();

            BitSet hit = default;
            foreach (var jprop in jelm.EnumerateObject())
            {
                // REVIEW: Should we just ignore invalid properties?
                if (!DName.TryWrap(jprop.Name, out var nameFld))
                    continue;
                if (!type.TryGetNameType(nameFld, out var typeFld, out int slot))
                    continue;

                // Only keep the first. REVIEW: Can there be multiple in json?
                if (hit.TestBit(slot))
                    continue;

                hit = hit.SetBit(slot);
                if (!TryReadCore(typeFld, jprop.Value, out var val))
                    return false;

                var setter = fact.GetFieldSetter(nameFld, out var typeFld2, out var stFld);
                Validation.Assert(val == null || stFld.IsAssignableFrom(val.GetType()));
                setter(rec, val);
            }

            // Ensure that all req fields were assigned or have a default that matches the CLR's default.
            if (hit != BitSet.GetMask(type.FieldCount))
            {
                int slot = 0;
                foreach (var tn in type.GetNames())
                {
                    if (hit.TestBit(slot++))
                        continue;
                    if (tn.Type.IsOpt)
                        continue;
                    switch (tn.Type.RootKind)
                    {
                    case DKind.Record:
                    case DKind.Tuple:
                    case DKind.Tensor:
                        // REVIEW: Should we manufacture a default instance and assign it?
                        return false;
                    }
                }
            }

            // This fails if there are required fields that weren't specified.
            if (!rec.TryClose(out var tmp))
                return false;

            value = tmp;
            return true;
        }

        protected virtual bool TryReadTup(DType typeTup, JsonElement jelm, out object value)
        {
            Validation.Assert(typeTup.IsTupleXxx);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            value = null;
            if (jelm.ValueKind != JsonValueKind.Array)
                return false;

            int len = jelm.GetArrayLength();
            if (len != typeTup.TupleArity)
                return false;

            if (!_parent.TryGetSysTypeCore(typeTup, out var sti))
                return false;
            Validation.Assert(len == sti.TupleInfo.ValueFields.Length);

            var types = typeTup.GetTupleSlotTypes();
            Validation.Assert(len == types.Length);

            value = sti.TupleInfo.Ctor.Invoke(null);

            for (var i = 0; i < types.Length; i++)
            {
                if (!TryRead(types[i], jelm[i], out var slotValue))
                    return false;

                sti.TupleInfo.ValueFields[i].SetValue(value, slotValue);
            }

            return true;
        }

        protected virtual bool TryReadTen<T>(DType type, JsonElement jelm, out Tensor<T> value)
        {
            Validation.Assert(type.IsTensorXxx);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            value = default;
            if (jelm.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetTensorShape(jelm, type.TensorRank, out var shape))
                return false;

            var typeItem = type.GetTensorItemType();

            if (!typeItem.IsOpt &&
                IsBase64Supported(typeItem.Kind) &&
                jelm.TryGetProperty(TensorBase64FieldName, out var base64Tok))
            {
                if (base64Tok.ValueKind != JsonValueKind.String)
                    return false;

                if (!jelm.TryGetProperty(TensorItemTypeFieldName, out var typeTok))
                    return false;

                if (typeTok.ValueKind != JsonValueKind.String ||
                    !DType.TryDeserialize(typeTok.GetString(), out var typeFromObj))
                {
                    return false;
                }

                if (typeFromObj != typeItem)
                    return false;

                var base64 = base64Tok.GetString();
                return TryReadBase64Tensor<T>(base64, typeItem, shape, out value);
            }

            if (jelm.TryGetProperty(TensorJObjectFieldName, out var jobjElm))
            {
                // Don't check the return value; if we fail to get the reader, we can pass a null value to
                // TryReadNestedTensor.
                TryGetReader(typeItem, out var fnItemReader);
                Validation.Assert(fnItemReader == null || fnItemReader is TryReadFunc<T>);

                return TryReadNestedTensor(jobjElm, type, shape, (TryReadFunc<T>)fnItemReader, out value);
            }

            return false;
        }

        private bool TryReadBase64Tensor<T>(string base64Val, DType typeItem, Shape shape, out Tensor<T> tensor)
        {
            _parent.AssertSysType(typeItem, typeof(T));

            tensor = null;
            if (!shape.TryGetCount(out var count))
                return false;

            var itemSize = typeItem.Kind.NumericSize();
            Validation.Assert(1 <= itemSize & itemSize <= 8);
            var buf = new byte[count * itemSize];
            if (!Convert.TryFromBase64String(base64Val, buf, out var bytesWritten))
                return false;

            if (buf.Length != bytesWritten)
                return false;

            _parent._byteReaders.TryGetValue(typeof(T), out var del).Verify();
            Validation.Assert(del is ReadBytesFunc<T>);
            var reader = (ReadBytesFunc<T>)del;

            var bldr = Tensor<T>.Builder.Create(shape);
            for (int byteIndex = 0, itemIndex = 0; byteIndex < buf.Length; byteIndex += itemSize, itemIndex++)
                bldr.Set(itemIndex, reader(buf, byteIndex));

            tensor = bldr.BuildGeneric();
            return true;
        }

        /// <summary>
        /// Read a tensor that was serialized as nested arrays.
        /// </summary>
        private bool TryReadNestedTensor<T>(JsonElement jelm, DType typeTensor, Shape shape, TryReadFunc<T> fnItemReader,
            out Tensor<T> tensor)
        {
            tensor = null;
            var typeItem = typeTensor.GetTensorItemType();
            var bldr = Tensor<T>.Builder.Create(shape);

            long index = 0;
            if (typeTensor.TensorRank == 0)
            {
                if (fnItemReader != null)
                {
                    if (!fnItemReader(typeItem, jelm, out var val))
                        return false;
                    bldr.Set(0, val);
                }
                else
                {
                    if (!TryRead(typeItem, jelm, out var objVal))
                        return false;
                    Validation.Assert(objVal is T);
                    bldr.Set(0, (T)objVal);
                }
            }
            else
            {
                if (!TryFillTensorBuilder(0, jelm, typeTensor, typeItem, bldr, fnItemReader, ref index))
                    return false;
            }

            tensor = bldr.BuildGeneric();
            return true;
        }

        private bool TryFillTensorBuilder<T>(int dim, JsonElement jelm, DType typeTensor, DType typeItem,
            Tensor<T>.Builder bldr, TryReadFunc<T> fnItemReader, ref long index)
        {
            Validation.AssertIndex(dim, typeTensor.TensorRank);

            if (jelm.ValueKind != JsonValueKind.Array)
                return false;

            // Check whether the nested array has the correct length.
            int len = jelm.GetArrayLength();
            if (len != bldr.Shape[dim])
                return false;

            if (dim < typeTensor.TensorRank - 1)
            {
                foreach (var child in jelm.EnumerateArray())
                {
                    if (!TryFillTensorBuilder(dim + 1, child, typeTensor, typeItem, bldr, fnItemReader, ref index))
                        return false;
                }
            }
            else if (fnItemReader is not null)
            {
                // Store the values with an item reader.
                foreach (var child in jelm.EnumerateArray())
                {
                    if (!fnItemReader(typeItem, child, out var val))
                        return false;
                    bldr.Set(index, val);
                    index++;
                }
            }
            else
            {
                // Store the values without an item reader.
                foreach (var child in jelm.EnumerateArray())
                {
                    if (!TryRead(typeItem, child, out var obj))
                        return false;
                    Validation.Assert(obj is T);
                    bldr.Set(index, (T)obj);
                    index++;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the shape for a Tensor represented as JSON.
        /// </summary>
        private bool TryGetTensorShape(JsonElement jelm, int rank, out Shape shape)
        {
            Validation.Assert(jelm.ValueKind == JsonValueKind.Object);

            shape = default;
            if (jelm.TryGetProperty(TensorShapeFieldName, out var shapeTok))
            {
                if (shapeTok.ValueKind != JsonValueKind.Array)
                    return false;

                int len = shapeTok.GetArrayLength();
                if (len != rank)
                    return false;

                var shapeBuilder = Shape.CreateBuilder(rank);
                for (var i = 0; i < rank; i++)
                {
                    var item = shapeTok[i];
                    if (!TryReadI8(DType.I8Req, item, out var val))
                        return false;
                    shapeBuilder[i] = val;
                }

                shape = shapeBuilder.ToImmutable();
                return true;
            }

            return false;
        }

        protected abstract bool TryReadUri(DType type, JsonElement jelm, out Link value);

        protected virtual bool TryReadDate(DType type, JsonElement jelm, out Date value)
        {
            Validation.Assert(type.Kind == DKind.Date);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            if (jelm.ValueKind == JsonValueKind.String)
            {
                var str = jelm.GetString();
                if (Date.TryParse(str, out var res))
                {
                    value = res;
                    return true;
                }
            }
            else if (jelm.TryGetDateTime(out var dt))
            {
                value = Date.FromSys(dt);
                return true;
            }

            value = default;
            return false;
        }

        protected virtual bool TryReadTime(DType type, JsonElement jelm, out Time value)
        {
            Validation.Assert(type.Kind == DKind.Time);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            if (jelm.ValueKind == JsonValueKind.String)
            {
                var str = jelm.GetString();
                if (Time.TryParse(str, out var time))
                {
                    value = time;
                    return true;
                }
            }

            value = default;
            return false;
        }

        protected virtual bool TryReadIA(DType type, JsonElement jelm, out Integer value)
        {
            Validation.Assert(type.Kind == DKind.IA);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            string str;
            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                str = jelm.GetRawText();
                break;
            case JsonValueKind.String:
                str = jelm.GetString();
                break;
            default:
                value = default;
                return false;
            }

            if (Integer.TryParse(str, out value))
                return true;

            value = default;
            return false;
        }

        protected virtual bool TryReadString(DType type, JsonElement jelm, out string value)
        {
            Validation.Assert(type.Kind == DKind.Text);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            if (jelm.ValueKind == JsonValueKind.String)
            {
                value = jelm.GetString();
                return true;
            }

            value = default;
            return false;
        }

        protected virtual bool TryReadGuid(DType type, JsonElement jelm, out Guid value)
        {
            Validation.Assert(type.Kind == DKind.Guid);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            if (jelm.ValueKind == JsonValueKind.String)
            {
                var str = jelm.GetString();
                if (Guid.TryParse(str, out value))
                    return true;
            }

            value = default;
            return false;
        }

        protected virtual bool TryReadBit(DType type, JsonElement jelm, out bool value)
        {
            Validation.Assert(type.Kind == DKind.Bit);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Undefined);
            Validation.Assert(jelm.ValueKind != JsonValueKind.Null);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.True:
                value = true;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Implements translation from rexl runtime values to utf8 json.
    /// </summary>
    public abstract partial class JsonWriter
    {
        /// <summary>
        /// Options to customize how to write values.
        /// </summary>
        [Flags]
        public enum EncodingOptions : byte
        {
            None = 0x00,

            /// <summary>
            /// Tensors for supported numeric types are serialized as base64 encoded strings.
            /// </summary>
            Base64Tensors = 0x01,

            /// <summary>
            /// Integers that cannot fit in doubles without losing precision are serialized as strings.
            /// </summary>
            StringNum = 0x02,

            /// <summary>
            /// Floating point infinities are serialized using the symbol &#x221E;.
            /// </summary>
            SymbolicInfs = 0x04,

            /// <summary>
            /// Tensors that seem to be pixels as png encoded as base64 strings.
            /// </summary>
            PngTensors = 0x08,
        }

        /// <summary>
        /// A read-only view into a <see cref="JsonWriter"/> type stack, for use by a customizer.
        /// A type entry is added to the stack whenever starting to write a value with a
        /// compound type, and the entry is popped when writing finishes. This means the
        /// customizer will always see the top of the stack correspond to the type of the
        /// value it is handling.
        /// </summary>
        public sealed class TypeStack
        {
            private readonly List<(DType type, Type st)> _stack;

            /// <summary>
            /// Number of entries on the stack.
            /// </summary>
            public int Count => _stack.Count;

            internal TypeStack(List<(DType type, Type st)> typeStack)
            {
                Validation.AssertValue(typeStack);
                _stack = typeStack;
            }

            /// <summary>
            /// Peeks the entry at the top of the stack.
            /// </summary>
            public (DType type, Type st) Peek()
            {
                Validation.BugCheck(_stack.Count > 0);
                return _stack.Peek();
            }

            /// <summary>
            /// Peeks the entry at the given index, where index 0 is the top of the stack.
            /// </summary>
            public (DType type, Type st) Peek(int index)
            {
                Validation.BugCheckIndex(index, _stack.Count, nameof(index));
                return _stack[_stack.Count - 1 - index];
            }

            /// <summary>
            /// Yields all stack entries in "push" order; i.e. starting from the root of the stack and moving up toward the top.
            /// </summary>
            public IEnumerable<(DType type, Type st)> WalkUp()
            {
                for (int i = 0; i < _stack.Count; i++)
                    yield return _stack[i];
            }

            /// <summary>
            /// Yields all stack entries in "pop" order; i.e. starting from the top of the stack and moving down toward the root.
            /// </summary>
            public IEnumerable<(DType type, Type st)> WalkDown()
            {
                for (int i = _stack.Count; --i >= 0;)
                    yield return _stack[i];
            }
        }

        // We start with a relatively small capacity, since it is not uncommon to call this
        // on very simple values. The default capacity of 256 is larger than needed. Note that
        // the first time this needs to grow, it will grow to 256, so we don't want to make the
        // initial capacity too small.
        private const int MemStreamCapacity = 32;

        // The max value in which an integer is guaranteed to fit in a double. Outside of the interval
        // [-2^53, 2^53], the value is subject to precision loss (but it may still retain its value).
        private const long DoubleMax = 1L << 53;

        /// <summary>
        /// The delegate type for customization. When one is provided, it will be called for anything
        /// that is "compound", e.g. sequences, records, tuples, and tensors. This includes null values
        /// for opt forms of these compound types.
        /// Should return false to indicate that default serialization should be done, true to indicate
        /// that it was "handled". One  outcome of "handling" it is that "fail" can be set to true
        /// indicating that the writer should "fail" the entire writing call.
        ///
        /// REVIEW: When provided, write methods are no longer guaranteed to be thread-safe due
        /// to tracking of the type stack as internal state.
        /// </summary>
        public delegate bool Customizer(Writer wrt, TypeStack stack, object value, out bool fail);

        protected readonly TypeManager _parent;

        protected readonly EncodingOptions _options;

        private delegate bool TryWriteFunc<T>(Writer wrt, DType type, Type st, T value);

        /// <summary>
        /// Dictionary from DType -> <see cref="TryWriteFunc{T}"/> for that type.
        /// </summary>
        private readonly ConcurrentDictionary<DType, Delegate> _typeToWriter;

        /// <summary>
        /// The optional <see cref="Customizer"/> to use for compound value customization.
        /// This should only be invoked by <see cref="TryCustomize"/> to handle the type stack correctly.
        /// </summary>
        private readonly Customizer _customizer;

        /// <summary>
        /// Tracks nesting through compound types when a customizer is present.
        /// </summary>
        private readonly List<(DType, Type)> _typeStack;
        private readonly TypeStack _stackView;

        protected JsonWriter(TypeManager parent, Customizer customizer = null, EncodingOptions options = EncodingOptions.None)
        {
            Validation.BugCheckValue(parent, nameof(parent));
            Validation.BugCheckValueOrNull(customizer);

            _parent = parent;
            _customizer = customizer;
            _options = options;
            _typeToWriter = new ConcurrentDictionary<DType, Delegate>();
            if (_customizer != null)
            {
                _typeStack = new List<(DType, Type)>();
                _stackView = new TypeStack(_typeStack);
            }

            AddNumericWriters();
        }

        /// <summary>
        /// Tries to translate from a rexl runtime object of the given <paramref name="type"/> to json.
        /// </summary>
        public bool TryWrite(Writer wrt, DType type, object value)
        {
            Validation.BugCheckValue(wrt, nameof(wrt));
            Validation.BugCheckParam(type.IsValid, nameof(type));
            Validation.BugCheckParam(_parent.TryEnsureSysType(type, out var st), nameof(type));
            Validation.Assert(_typeStack == null || _typeStack.Count == 0);

            int depth = wrt.Depth;
            if (value == null)
            {
                if (!TryWriteNullValue(wrt, type, st))
                    return false;
            }
            else
            {
                Validation.BugCheckParam(st.IsAssignableFrom(value.GetType()), nameof(value));
                if (!TryWriteCore(wrt, type, st, value))
                    return false;
            }

            Validation.Assert(wrt.Depth == depth);
            Validation.Assert(!wrt.NeedValue);
            Validation.Assert(_typeStack == null || _typeStack.Count == 0);
            return true;
        }

        /// <summary>
        /// Tries to translate from a rexl runtime object of the given <paramref name="type"/> to a string containing json.
        /// </summary>
        public bool TryWriteToUtf8(DType type, object value, out Str8 result)
        {
            using var mem = new MemoryStream(MemStreamCapacity);
            using var wrt = new Writer(mem, symbolicInfs: (_options & EncodingOptions.SymbolicInfs) != 0);
            if (!TryWrite(wrt, type, value))
            {
                result = null;
                return false;
            }
            wrt.Flush();
            mem.Flush();
            result = new Str8(mem.GetBuffer(), (int)mem.Length);
            return true;
        }

        /// <summary>
        /// Tries to translate from a rexl runtime object of the given <paramref name="type"/> to a string containing json.
        /// </summary>
        public bool TryWriteToString(DType type, object value, out string result)
        {
            using var mem = new MemoryStream(MemStreamCapacity);
            using var wrt = new Writer(mem);
            if (!TryWrite(wrt, type, value))
            {
                result = null;
                return false;
            }
            wrt.Flush();
            mem.Flush();
            result = Encoding.UTF8.GetString(mem.GetBuffer().AsSpan(0, (int)mem.Length));
            return true;
        }

        protected bool TryWriteCore(Writer wrt, DType type, Type st, object value)
        {
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));

            if (value == null)
                return TryWriteNullValue(wrt, type, st);

            // Should be ensured by the caller (and type consistency).
            Validation.Assert(st.IsAssignableFrom(value.GetType()));

            switch (type.Kind)
            {
            case DKind.Sequence: return TryWriteSeq(wrt, type, st, value);
            case DKind.Record: return TryWriteRec(wrt, type, st, value);
            case DKind.Tuple: return TryWriteTup(wrt, type, st, value);
            case DKind.Tensor: return TryWithWriter(wrt, type, st, value);
            case DKind.Uri: return TryWriteUri(wrt, type, st, (Link)value);

            case DKind.Text: return TryWriteString(wrt, type, st, (string)value);

            case DKind.R8: return TryWriteR8(wrt, type, st, (double)value);
            case DKind.R4: return TryWriteR4(wrt, type, st, (float)value);
            case DKind.IA: return TryWriteIA(wrt, type, st, (Integer)value);
            case DKind.I8: return TryWriteI8(wrt, type, st, (long)value);
            case DKind.I4: return TryWriteI4(wrt, type, st, (int)value);
            case DKind.I2: return TryWriteI2(wrt, type, st, (short)value);
            case DKind.I1: return TryWriteI1(wrt, type, st, (sbyte)value);
            case DKind.U8: return TryWriteU8(wrt, type, st, (ulong)value);
            case DKind.U4: return TryWriteU4(wrt, type, st, (uint)value);
            case DKind.U2: return TryWriteU2(wrt, type, st, (ushort)value);
            case DKind.U1: return TryWriteU1(wrt, type, st, (byte)value);
            case DKind.Bit: return TryWriteBit(wrt, type, st, (bool)value);

            case DKind.Date: return TryWriteDate(wrt, type, st, (Date)value);
            case DKind.Time: return TryWriteTime(wrt, type, st, (Time)value);
            case DKind.Guid: return TryWriteGuid(wrt, type, st, (Guid)value);

            case DKind.General:
                // REVIEW: Short-term fix to unblock user requests with g is to serialize as null.
                // Need long-term decision on how to handle g.
                wrt.WriteNullValue();
                return true;
            case DKind.Vac:
            default:
                // REVIEW: Handle this?
                return false;
            }
        }

        /// <summary>
        /// Try to write a value using a strongly typed writer from <see cref="TryGetWriter"/>.
        /// 
        /// REVIEW: This function is only temporary, while we move towards making this entire class
        /// strongly typed. Eventually, this function should become strongly typed and all writes should go through it.
        /// </summary>
        private bool TryWithWriter(Writer wrt, DType type, Type st, object value)
        {
            if (!TryGetWriter(type, out var fn))
                return false;
            Validation.Assert(fn.GetType() == typeof(TryWriteFunc<>).MakeGenericType(st));

            var writeParams = new object[] { wrt, type, st, value };
            var result = fn.Method.Invoke(this, writeParams);
            return (bool)result;
        }

        /// <summary>
        /// Get a strongly typed function to write a JSON object of a given DType, creating one if possible.
        /// </summary>
        private bool TryGetWriter(DType type, out Delegate fn)
        {
            if (_typeToWriter.TryGetValue(type, out fn))
                return true;

            if (type.Kind == DKind.Tensor)
            {
                if (!_parent.TryEnsureSysType(type, out var st))
                    return false;

                if (!_parent.TryEnsureSysType(type.GetTensorItemType(), out var stItem))
                    return false;

                var meth = new TryWriteFunc<Tensor<object>>(TryWriteTen<object>)
                    .Method.GetGenericMethodDefinition()
                    .MakeGenericMethod(stItem);

                var delType = typeof(TryWriteFunc<>).MakeGenericType(st);
                fn = _typeToWriter.GetOrAdd(type, Delegate.CreateDelegate(delType, this, meth));
                return true;
            }

            // REVIEW: Add writers for all other types.
            return false;
        }

        /// <summary>
        /// Pushes the given type entry, and runs the customizer with the given values, passing the results to the caller.
        /// The type stack is adjusted according to the result of the customizer:
        /// <list type="bullet">
        ///   <item>If the customizer handled serialization (returned true), the type entry is popped, preserving the state
        ///     of the type stack before this call.</item>
        ///   <item>If the customizer falls back to default serialization (returned false), the type entry is <b>not</b>
        ///     popped. This ensures the entry is available on the stack for any nested customizer invocations. This case
        ///     must always be handled by calling <see cref="PopTypeEntry"/> before the caller returns.</item>
        /// </list>
        /// </summary>
        protected bool TryCustomize(Writer wrt, DType type, Type st, object value, out bool fail)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(!type.IsPrimitiveXxx);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));
            Validation.Assert(value == null || st.IsAssignableFrom(value.GetType()));

            Validation.AssertValue(_customizer);
            Validation.AssertValue(_typeStack);
            Validation.AssertValue(_stackView);

            _typeStack.Add((type, st));
            if (!_customizer(wrt, _stackView, value, out fail))
                return false;
            PopTypeEntryCore(type, st);
            return true;
        }

        /// <summary>
        /// If this writer is tracking types, pops the given type entry from the stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void PopTypeEntry(DType type, Type st)
        {
            Validation.AssertValueOrNull(_typeStack);
            Validation.Assert((_typeStack == null) == (_stackView == null));
            Validation.Assert((_stackView == null) == (_customizer == null));
            Validation.Assert(_typeStack == null || _typeStack.Count > 0);
            Validation.Assert(type.IsValid);
            Validation.AssertValue(st);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));

            if (_typeStack != null)
                PopTypeEntryCore(type, st);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopTypeEntryCore(DType type, Type st)
        {
            Validation.AssertValue(_typeStack);
            Validation.Assert(_typeStack.Count > 0);
            Validation.Assert(type.IsValid);
            Validation.AssertValue(st);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));

            var (typeTop, stTop) = _typeStack.Pop();

            Validation.Assert(typeTop == type);
            Validation.Assert(stTop == st);
        }

        /// <summary>
        /// Tries to write a null value of the given type. If the type is compound,
        /// the null will be passed to the customizer, if available.
        /// </summary>
        protected bool TryWriteNullValue(Writer wrt, DType type, Type st)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.IsOpt);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));

            switch (type.Kind)
            {
            case DKind.Sequence:
                if (_customizer != null)
                {
                    if (TryCustomize(wrt, type, st, value: null, out bool failSeq))
                        return !failSeq;
                    PopTypeEntryCore(type, st);
                }
                wrt.WriteStartArray();
                wrt.WriteEndArray();
                return true;

            case DKind.Record:
            case DKind.Tuple:
            case DKind.Tensor:
                if (_customizer != null)
                {
                    if (TryCustomize(wrt, type, st, value: null, out bool fail))
                        return !fail;
                    PopTypeEntryCore(type, st);
                }
                goto default;

            default:
                wrt.WriteNullValue();
                return type.IsOpt;
            }
        }

        protected virtual bool TryWriteSeq(Writer wrt, DType type, Type st, object value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.IsSequence);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));
            Validation.AssertValue(value);
            Validation.Assert(st.IsAssignableFrom(value.GetType()));

            if (_customizer != null && TryCustomize(wrt, type, st, value, out bool fail))
                return !fail;

            var typeItem = type.ItemTypeOrThis;
            _parent.TryEnsureSysType(typeItem, out var stItem).Verify();

            StartSeq(wrt);
            foreach (var item in (IEnumerable)value)
            {
                if (!TryWriteCore(wrt, typeItem, stItem, item))
                {
                    PopTypeEntry(type, st);
                    return false;
                }
            }
            EndSeq(wrt);

            PopTypeEntry(type, st);
            return true;
        }

        protected virtual bool TryWriteRec(Writer wrt, DType type, Type st, object value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.IsRecordXxx);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));
            Validation.AssertValue(value);
            Validation.Assert(st.IsAssignableFrom(value.GetType()));
            Validation.Assert(value is RecordBase);

            if (_customizer != null && TryCustomize(wrt, type, st, value, out bool fail))
                return !fail;

            var rec = (RecordBase)value;
            StartRecord(wrt);
            var fieldsToWrite = GetRecFieldsToWrite(type);
            foreach (var tn in fieldsToWrite)
            {
                var valFld = _parent.GetFieldValue(type, tn.Name, rec, out var typeFld, out var stFld);
                Validation.Assert(typeFld == tn.Type);
                wrt.WritePropertyName(tn.Name.Value);
                if (!TryWriteCore(wrt, typeFld, stFld, valFld))
                {
                    PopTypeEntry(type, st);
                    return false;
                }
            }
            EndRecord(wrt);

            PopTypeEntry(type, st);
            return true;
        }

        protected virtual IEnumerable<TypedName> GetRecFieldsToWrite(DType type)
        {
            Validation.Assert(type.IsRecordXxx);
            return type.GetNames();
        }

        protected virtual bool TryWriteTup(Writer wrt, DType type, Type st, object value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.IsTupleXxx);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));
            Validation.AssertValue(value);
            Validation.Assert(st.IsAssignableFrom(value.GetType()));
            Validation.Assert(value is TupleBase);

            if (_customizer != null && TryCustomize(wrt, type, st, value, out bool fail))
                return !fail;

            var tuple = value as TupleBase;
            StartTuple(wrt);
            foreach (var (slotType, slotSt, slotValue) in _parent.GetTupleSlotValues(type, tuple))
            {
                if (!TryWriteCore(wrt, slotType, slotSt, slotValue))
                {
                    PopTypeEntry(type, st);
                    return false;
                }
            }
            EndTuple(wrt);
            PopTypeEntry(type, st);
            return true;
        }

        protected virtual bool TryWriteTen<T>(Writer wrt, DType type, Type st, Tensor<T> tensor)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.IsTensorXxx);
            Validation.Assert(st == _parent.GetSysTypeOrNull(type));
            Validation.AssertValue(tensor);
            Validation.Assert(st.IsAssignableFrom(tensor.GetType()));

            if (_customizer != null && TryCustomize(wrt, type, st, tensor, out bool fail))
                return !fail;

            var typeItem = type.GetTensorItemType();
            bool ret;
            if ((_options & EncodingOptions.PngTensors) != 0 &&
                TensorUtil.IsPixTypeReq(type.ToReq()) &&
                Tensor.TryGetPngFromPixels(tensor, out var png) &&
                Tensor.TryGetSpan(png, out var span))
            {
                ret = TryWriteTensorPng<T>(wrt, tensor, span, typeItem);
            }
            else if ((_options & EncodingOptions.Base64Tensors) != 0 && IsBase64Supported(typeItem.Kind) && !typeItem.IsOpt)
                ret = TryWriteTensorBase64<T>(wrt, tensor, typeItem);
            else
                ret = TryWriteTensorNested<T>(wrt, tensor, typeItem);

            PopTypeEntry(type, st);
            return ret;
        }

        private bool TryWriteTensorPng<T>(Writer wrt, Tensor<T> tensor, ReadOnlySpan<byte> png, DType typeItem)
        {
            Validation.AssertValue(wrt);
            Validation.AssertValue(tensor);
            Validation.Assert(png.Length > 0);

            wrt.WriteStartObject();
            wrt.WriteBase64String(TensorPngFieldNameUtf8, png);
            wrt.WriteString(TensorItemTypeFieldNameUtf8, typeItem.Serialize());

            wrt.WritePropertyName(TensorShapeFieldNameUtf8);
            wrt.WriteStartArray();
            foreach (var dim in tensor.Shape.Items)
                wrt.WriteNumberValue(dim);
            wrt.WriteEndArray();

            wrt.WriteEndObject();
            return true;
        }

        private bool TryWriteTensorBase64<T>(Writer wrt, Tensor<T> tensor, DType typeItem)
        {
            Validation.AssertValue(wrt);
            Validation.AssertValue(tensor);

            var itemSize = typeItem.Kind.NumericSize();
            Validation.Assert(itemSize > 0);
            var buf = new Span<byte>(new byte[tensor.Count * itemSize]);

            int i = 0;
            _parent._byteWriters.TryGetValue(typeof(T), out var del).Verify();
            Validation.Assert(del is TryWriteBytesFunc<T>);
            var tryWrite = (TryWriteBytesFunc<T>)del;

            foreach (var val in tensor.GetValues())
            {
                if (!tryWrite(buf.Slice(i, itemSize), val))
                    return false;
                i += itemSize;
            }

            wrt.WriteStartObject();
            wrt.WriteBase64String(TensorBase64FieldNameUtf8, buf);
            wrt.WriteString(TensorItemTypeFieldNameUtf8, typeItem.Serialize());

            wrt.WritePropertyName(TensorShapeFieldNameUtf8);
            wrt.WriteStartArray();
            foreach (var dim in tensor.Shape.Items)
                wrt.WriteNumberValue(dim);
            wrt.WriteEndArray();

            wrt.WriteEndObject();
            return true;
        }

        private bool TryWriteTensorNested<T>(Writer wrt, Tensor<T> tensor, DType typeItem)
        {
            Validation.AssertValue(wrt);
            Validation.AssertValue(tensor);

            wrt.WriteStartObject();
            wrt.WritePropertyName(TensorJObjectFieldNameUtf8);
            if (tensor.Rank == 0)
            {
                // Rank 0 tensors are scalar values, so serialize its value directly, not in an array.
                Validation.Assert(tensor.Count == 1);
                if (!TryWrite(wrt, typeItem, tensor.GetValues().First()))
                    return false;
            }
            else
            {
                // Don't check the return value; if we fail to get the writer, we can pass a null value to TryWriteNestedArrays.
                TryGetWriter(typeItem, out var fnItemWriter);
                Validation.Assert(fnItemWriter == null || fnItemWriter is TryWriteFunc<T>);
                if (!TryWriteNestedArrays(wrt, tensor.Shape, typeItem, 0, tensor.GetValues().GetEnumerator(), (TryWriteFunc<T>)fnItemWriter))
                    return false;
            }

            wrt.WritePropertyName(TensorShapeFieldNameUtf8);
            wrt.WriteStartArray();
            foreach (var dim in tensor.Shape.Items)
                wrt.WriteNumberValue(dim);
            wrt.WriteEndArray();

            wrt.WriteEndObject();
            return true;
        }

        private bool TryWriteNestedArrays<T>(Writer wrt, Shape shape, DType typeItem, int dimStart, IEnumerator<T> ator, TryWriteFunc<T> fnItemWriter)
        {
            Validation.AssertValue(wrt);
            Validation.AssertIndex(dimStart, shape.Rank);

            wrt.WriteStartArray();

            var dimSize = shape[dimStart];
            // If we reach a size 0 dimension, then output an empty array, and stop recursing.
            if (dimSize > 0)
            {
                // If we've reached the innermost dimension, convert the objects
                var dimNext = dimStart + 1;
                if (dimNext < shape.Rank)
                {
                    for (int i = 0; i < dimSize; i++)
                    {
                        if (!TryWriteNestedArrays(wrt, shape, typeItem, dimNext, ator, fnItemWriter))
                            return false;
                    }
                }
                else
                {
                    for (var i = 0; i < dimSize; i++)
                    {
                        ator.MoveNext().Verify();
                        if (fnItemWriter != null)
                        {
                            if (!fnItemWriter(wrt, typeItem, typeof(T), ator.Current))
                                return false;
                        }
                        else
                        {
                            if (!TryWrite(wrt, typeItem, ator.Current))
                                return false;
                        }
                    }
                }
            }

            wrt.WriteEndArray();
            return true;
        }

        protected abstract bool TryWriteUri(Writer wrt, DType type, Type st, Link value);

        protected virtual bool TryWriteDate(Writer wrt, DType type, Type st, Date value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.Date);
            Validation.Assert(st == (type.IsOpt ? typeof(Date?) : typeof(Date)));

            wrt.WriteStringValue(value.ToISO());
            return true;
        }

        protected virtual bool TryWriteTime(Writer wrt, DType type, Type st, Time value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.Time);
            Validation.Assert(st == (type.IsOpt ? typeof(Time?) : typeof(Time)));

            // Use the constant (invariant) timespan format.
            wrt.WriteStringValue(value.ToString("c"));
            return true;
        }

        protected virtual bool TryWriteIA(Writer wrt, DType type, Type st, Integer value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.IA);
            Validation.Assert(st == (type.IsOpt ? typeof(Integer?) : typeof(Integer)));
            if ((_options & EncodingOptions.StringNum) != 0 && (value < -DoubleMax || value > DoubleMax))
            {
                wrt.WriteStringValue(value.ToString());
                return true;
            }

            // REVIEW: Utf8JsonWriter does not yet support BigInteger.
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteString(Writer wrt, DType type, Type st, string value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.Text);
            Validation.Assert(st == typeof(string));

            wrt.WriteStringValue(value);
            return true;
        }

        private bool TryWriteGuid(Writer wrt, DType type, Type st, Guid value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.Guid);
            Validation.Assert(st == (type.IsOpt ? typeof(Guid?) : typeof(Guid)));

            wrt.WriteStringValue(value);
            return true;
        }

        private bool TryWriteBit(Writer wrt, DType type, Type st, bool value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.Bit);
            Validation.Assert(st == (type.IsOpt ? typeof(bool?) : typeof(bool)));

            wrt.WriteBooleanValue(value);
            return true;
        }

        protected virtual void StartRecord(Writer wrt)
        {
            Validation.AssertValue(wrt);
            wrt.WriteStartObject();
        }

        protected virtual void EndRecord(Writer wrt)
        {
            Validation.AssertValue(wrt);
            wrt.WriteEndObject();
        }

        protected virtual void StartTuple(Writer wrt)
        {
            Validation.AssertValue(wrt);
            wrt.WriteStartArray();
        }

        protected virtual void EndTuple(Writer wrt)
        {
            Validation.AssertValue(wrt);
            wrt.WriteEndArray();
        }

        protected virtual void StartSeq(Writer wrt)
        {
            Validation.AssertValue(wrt);
            wrt.WriteStartArray();
        }

        protected virtual void EndSeq(Writer wrt)
        {
            Validation.AssertValue(wrt);
            wrt.WriteEndArray();
        }
    }
}

partial class StdEnumerableTypeManager
{
    public JsonWriter CreateJsonWriter()
    {
        return new JsonWriterImpl(this);
    }

    private sealed class JsonWriterImpl : JsonWriter
    {
        public JsonWriterImpl(StdEnumerableTypeManager parent)
            : base(parent)
        {
        }

        protected override bool TryWriteUri(Writer writer, DType type, Type st, Link value)
        {
            Validation.AssertValue(writer);

            // REVIEW: Implement uri serialization eventually.
            return false;
        }
    }
}
