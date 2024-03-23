// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// The generic tensor class.
/// </summary>
public sealed partial class Tensor<T> : Tensor
{
    /// <summary>
    /// Code used to identify the type of tensor serialization that was used. This can also be used
    /// for versioning if we change the format of serialization.
    /// </summary>
    private enum SerializationCode : byte
    {
        /// <summary>
        /// The tensor's strides can be computed directly from its shape, so they aren't serialized.
        /// </summary>
        UsesDefaultStrides = 0x91,
        /// <summary>
        /// The tensor had shared values, so the serialized tensor contains a bitfield indicating which dimensions had a stride of 0.
        /// </summary>
        ContainsSerializedStrides = 0x92
    }

    /// <summary>
    /// Serialize the tensor to <paramref name="serializer"/>.
    /// 
    /// <typeparamref name="TSink"/> is opaque to this code and is blindly passed back to all function calls on the
    /// <paramref name="serializer"/>.
    /// </summary>
    public void Serialize<TSink>(Serializer<TSink> serializer, TSink sink)
    {
        Validation.BugCheckValue(serializer, nameof(serializer));

        if (_count == 0)
        {
            serializer.WriteByte(sink, (byte)SerializationCode.UsesDefaultStrides);
            serializer.WriteShape(sink, _shape);
            serializer.WriteI8(sink, 0);
        }
        else if (!HasSharedValues())
        {
            Validation.Assert(!Shape.AnyZero);

            serializer.WriteByte(sink, (byte)SerializationCode.UsesDefaultStrides);
            serializer.WriteShape(sink, _shape);
            serializer.WriteI8(sink, _count);
            if (_regular)
            {
                if (_delta == 0)
                {
                    // If there is no value sharing and the _delta is 0, then that means there can only be a single value.
                    Validation.Assert(_count == 1);
                    serializer.WriteValue(sink, _buf[_root]);
                }
                else
                {
                    foreach (var value in IterateValues1(_buf, _count, _delta, _root))
                        serializer.WriteValue(sink, value);
                }
            }
            else
            {
                foreach (var value in IterateValues(_buf, _count, _shape, _strides, _root))
                    serializer.WriteValue(sink, value);
            }
        }
        else
        {
            Validation.Assert(!Shape.AnyZero);

            serializer.WriteByte(sink, (byte)SerializationCode.ContainsSerializedStrides);
            serializer.WriteShape(sink, _shape);

            WriteZeroStrideBitfield<TSink>(serializer, sink, _strides);

            if (_regular)
            {
                // The only way a tensor with shared values could be regular is if it has a delta of 0, and contains a single repeated value.
                Validation.Assert(_delta == 0);
                serializer.WriteI8(sink, 1);
                serializer.WriteValue(sink, _buf[_root]);
            }
            else
            {
                // Output the values for the tensor. We don't want to output repeated values (with 0 strides)
                // multiple times, so we only output the values in the compressed shape.
                Shape shapeCompressed = _shape;
                Shape stridesCompressed = _strides;
                GetCompressedShapeAndStrides(ref shapeCompressed, ref stridesCompressed);
                shapeCompressed.TryGetCount(out var count).Verify();
                serializer.WriteI8(sink, count);

                foreach (var value in IterateValues(_buf, count, shapeCompressed, stridesCompressed, _root))
                    serializer.WriteValue(sink, value);
            }
        }
    }

    /// <summary>
    /// Returns true if any values in the tensor are shared between multiple indices.
    /// </summary>
    private bool HasSharedValues()
    {
        for (int i = 0; i < Rank; i++)
        {
            if (_strides[i] == 0 && _shape[i] != 1)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Write a bitfield where the value of the nth bit is 0 if the stride at index n is 0, otherwise it is 1.
    /// 
    /// The bytes are enumerated in little-endian order.
    /// </summary>
    private void WriteZeroStrideBitfield<TSink>(Serializer<TSink> serializer, TSink sink, Shape strides)
    {
        byte field = 0;
        byte mask = 1;
        var len = strides.Items.Length;
        for (int i = 0; i < len; i++)
        {
            if (strides[i] != 0)
                field |= mask;

            mask <<= 1;
            // If we've shifted the bit of the mask past the end, then we've reached the end of the byte, so write it.
            if (mask == 0)
            {
                serializer.WriteByte(sink, field);
                field = 0;
                mask = 1;
            }
        }

        Validation.Assert(mask == 1 << (len & 07));
        if (mask > 1)
            serializer.WriteByte(sink, field);
    }

    /// <summary>
    /// Deserialize a tensor from <paramref name="deserializer"/>.
    ///
    /// <typeparamref name="TSource"/> is opaque to this code and is blindly passed back to all function calls on the
    /// <paramref name="deserializer"/>.
    /// </summary>
    public static Tensor<T> Deserialize<TSource>(Deserializer<TSource> deserializer, TSource src)
    {
        Validation.BugCheckValue(deserializer, nameof(deserializer));

        var type = (SerializationCode)deserializer.ReadByte(src);
        var shape = deserializer.ReadShape(src);

        Shape strides;
        long countExpected;
        if (type == SerializationCode.ContainsSerializedStrides)
            strides = ReadStrideBitfield(deserializer, src, shape, out countExpected);
        else if (type == SerializationCode.UsesDefaultStrides)
        {
            countExpected = 1;
            deserializer.Check(shape.TryMakeStrides(ref countExpected, out strides), "Invalid shape in deserialized tensor.");
        }
        else
        {
            // Unknown tensor type; input is likely corrupt.
            deserializer.Check(false, "Unknown tensor encoding.");
            return null;
        }

        var count = deserializer.ReadI8(src);
        deserializer.Check(countExpected == count, "Incorrect number of elements in deserialized tensor.");

        var buf = new T[count];
        for (long i = 0; i < count; i++)
            buf[i] = deserializer.ReadValue(src);

        return _CreateRaw(shape, strides, buf, 0);
    }

    /// <summary>
    /// Read the strides from a bitfield where the value of the nth bit is 1 if the stride at index n is 0.
    /// 
    /// The bytes are read in little-endian order.
    /// </summary>
    private static Shape ReadStrideBitfield<TSource>(Deserializer<TSource> deserializer, TSource src, Shape shape, out long count)
    {
        var bldr = Shape.CreateBuilder(shape.Rank);
        byte field = 0;

        // Build a shape with 1 for any non-zero dimension, so we can generate the correct strides with TryMakeStridesLike.
        for (int i = 0; i < shape.Rank; i++)
        {
            // Read another byte if the index is at the beginning of one.
            if ((i & 0x07) == 0)
                field = deserializer.ReadByte(src);

            bldr[i] = field & 1;
            field >>= 1;
        }
        deserializer.Check(field == 0, "Stride bitfield had too many values.");

        Shape strides = bldr.ToImmutable();
        deserializer.Check(shape.TryMakeStridesLike(ref strides, out count), "Invalid shape in deserialized tensor.");
        return strides;
    }

    public abstract class Serializer<TSink>
    {
        public abstract void WriteByte(TSink sink, byte val);
        public abstract void WriteI8(TSink sink, long val);
        public abstract void WriteShape(TSink sink, Shape val);
        public abstract void WriteValue(TSink sink, T val);
    }

    public abstract class Deserializer<TSource>
    {
        public abstract byte ReadByte(TSource src);
        public abstract long ReadI8(TSource src);
        public abstract Shape ReadShape(TSource src);
        public abstract T ReadValue(TSource src);
        public abstract void Check(bool cond, string msg = null);
    }
}
