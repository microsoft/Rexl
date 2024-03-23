// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

using System;
using System.Globalization;
using System.Text.Json;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using Writer = RawJsonWriter;

partial class TypeManager
{
    public abstract partial class JsonReader
    {
        private void AddNumericReaders()
        {
            _typeToReader[DType.I8Req] = (TryReadFunc<long>)TryReadI8;
            _typeToReader[DType.I4Req] = (TryReadFunc<int>)TryReadI4;
            _typeToReader[DType.I2Req] = (TryReadFunc<short>)TryReadI2;
            _typeToReader[DType.I1Req] = (TryReadFunc<sbyte>)TryReadI1;
            _typeToReader[DType.U8Req] = (TryReadFunc<ulong>)TryReadU8;
            _typeToReader[DType.U4Req] = (TryReadFunc<uint>)TryReadU4;
            _typeToReader[DType.U2Req] = (TryReadFunc<ushort>)TryReadU2;
            _typeToReader[DType.U1Req] = (TryReadFunc<byte>)TryReadU1;
            _typeToReader[DType.R8Req] = (TryReadFunc<double>)TryReadR8;
            _typeToReader[DType.R4Req] = (TryReadFunc<float>)TryReadR4;
        }

        private bool TryReadNumeric(DType type, JsonElement jelm, out object value)
        {
            switch (type.Kind)
            {
            case DKind.I8:
                if (TryReadI8(type, jelm, out var valueI8))
                {
                    value = valueI8;
                    return true;
                }
                break;
            case DKind.I4:
                if (TryReadI4(type, jelm, out var valueI4))
                {
                    value = valueI4;
                    return true;
                }
                break;
            case DKind.I2:
                if (TryReadI2(type, jelm, out var valueI2))
                {
                    value = valueI2;
                    return true;
                }
                break;
            case DKind.I1:
                if (TryReadI1(type, jelm, out var valueI1))
                {
                    value = valueI1;
                    return true;
                }
                break;
            case DKind.U8:
                if (TryReadU8(type, jelm, out var valueU8))
                {
                    value = valueU8;
                    return true;
                }
                break;
            case DKind.U4:
                if (TryReadU4(type, jelm, out var valueU4))
                {
                    value = valueU4;
                    return true;
                }
                break;
            case DKind.U2:
                if (TryReadU2(type, jelm, out var valueU2))
                {
                    value = valueU2;
                    return true;
                }
                break;
            case DKind.U1:
                if (TryReadU1(type, jelm, out var valueU1))
                {
                    value = valueU1;
                    return true;
                }
                break;
            case DKind.R8:
                if (TryReadR8(type, jelm, out var valueR8))
                {
                    value = valueR8;
                    return true;
                }
                break;
            case DKind.R4:
                if (TryReadR4(type, jelm, out var valueR4))
                {
                    value = valueR4;
                    return true;
                }
                break;
            }

            value = null;
            return false;
        }

        private bool TryReadI8(DType type, JsonElement jelm, out long value)
        {
            Validation.Assert(type.Kind == DKind.I8);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetInt64(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (long.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadI4(DType type, JsonElement jelm, out int value)
        {
            Validation.Assert(type.Kind == DKind.I4);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetInt32(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (int.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadI2(DType type, JsonElement jelm, out short value)
        {
            Validation.Assert(type.Kind == DKind.I2);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetInt16(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (short.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadI1(DType type, JsonElement jelm, out sbyte value)
        {
            Validation.Assert(type.Kind == DKind.I1);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetSByte(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (sbyte.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadU8(DType type, JsonElement jelm, out ulong value)
        {
            Validation.Assert(type.Kind == DKind.U8);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetUInt64(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (ulong.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadU4(DType type, JsonElement jelm, out uint value)
        {
            Validation.Assert(type.Kind == DKind.U4);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetUInt32(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (uint.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadU2(DType type, JsonElement jelm, out ushort value)
        {
            Validation.Assert(type.Kind == DKind.U2);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetUInt16(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (ushort.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadU1(DType type, JsonElement jelm, out byte value)
        {
            Validation.Assert(type.Kind == DKind.U1);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetByte(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (byte.TryParse(str, out value))
                    return true;
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadR8(DType type, JsonElement jelm, out double value)
        {
            Validation.Assert(type.Kind == DKind.R8);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetDouble(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (double.TryParse(str, out value))
                    return true;
                switch (str)
                {
                case "∞":
                case "Infinity":
                    value = double.PositiveInfinity;
                    return true;
                case "-∞":
                case "-Infinity":
                    value = double.NegativeInfinity;
                    return true;
                case "NaN":
                    value = double.NaN;
                    return true;
                }
                break;
            }

            value = default;
            return false;
        }

        private bool TryReadR4(DType type, JsonElement jelm, out float value)
        {
            Validation.Assert(type.Kind == DKind.R4);

            switch (jelm.ValueKind)
            {
            case JsonValueKind.Number:
                if (jelm.TryGetSingle(out value))
                    return true;
                break;
            case JsonValueKind.String:
                var str = jelm.GetString();
                if (float.TryParse(str, out value))
                    return true;
                switch (str)
                {
                case "∞":
                case "Infinity":
                    value = float.PositiveInfinity;
                    return true;
                case "-∞":
                case "-Infinity":
                    value = float.NegativeInfinity;
                    return true;
                case "NaN":
                    value = float.NaN;
                    return true;
                }
                break;
            }

            value = default;
            return false;
        }
    }

    public abstract partial class JsonWriter
    {
        private void AddNumericWriters()
        {
            _typeToWriter[DType.I8Req] = (TryWriteFunc<long>)TryWriteI8;
            _typeToWriter[DType.I4Req] = (TryWriteFunc<int>)TryWriteI4;
            _typeToWriter[DType.I2Req] = (TryWriteFunc<short>)TryWriteI2;
            _typeToWriter[DType.I1Req] = (TryWriteFunc<sbyte>)TryWriteI1;
            _typeToWriter[DType.U8Req] = (TryWriteFunc<ulong>)TryWriteU8;
            _typeToWriter[DType.U4Req] = (TryWriteFunc<uint>)TryWriteU4;
            _typeToWriter[DType.U2Req] = (TryWriteFunc<ushort>)TryWriteU2;
            _typeToWriter[DType.U1Req] = (TryWriteFunc<byte>)TryWriteU1;
            _typeToWriter[DType.R8Req] = (TryWriteFunc<double>)TryWriteR8;
            _typeToWriter[DType.R4Req] = (TryWriteFunc<float>)TryWriteR4;
        }

        private bool TryWriteI8(Writer wrt, DType type, Type st, long value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.I8);
            Validation.Assert(st == (type.IsOpt ? typeof(long?) : typeof(long)));
            if ((_options & EncodingOptions.StringNum) != 0 && (value < -DoubleMax || value > DoubleMax))
            {
                wrt.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteI4(Writer wrt, DType type, Type st, int value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.I4);
            Validation.Assert(st == (type.IsOpt ? typeof(int?) : typeof(int)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteI2(Writer wrt, DType type, Type st, short value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.I2);
            Validation.Assert(st == (type.IsOpt ? typeof(short?) : typeof(short)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteI1(Writer wrt, DType type, Type st, sbyte value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.I1);
            Validation.Assert(st == (type.IsOpt ? typeof(sbyte?) : typeof(sbyte)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteU8(Writer wrt, DType type, Type st, ulong value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.U8);
            Validation.Assert(st == (type.IsOpt ? typeof(ulong?) : typeof(ulong)));
            if ((_options & EncodingOptions.StringNum) != 0 && value > DoubleMax)
            {
                wrt.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteU4(Writer wrt, DType type, Type st, uint value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.U4);
            Validation.Assert(st == (type.IsOpt ? typeof(uint?) : typeof(uint)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteU2(Writer wrt, DType type, Type st, ushort value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.U2);
            Validation.Assert(st == (type.IsOpt ? typeof(ushort?) : typeof(ushort)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteU1(Writer wrt, DType type, Type st, byte value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.U1);
            Validation.Assert(st == (type.IsOpt ? typeof(byte?) : typeof(byte)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteR8(Writer wrt, DType type, Type st, double value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.R8);
            Validation.Assert(st == (type.IsOpt ? typeof(double?) : typeof(double)));
            wrt.WriteNumberValue(value);
            return true;
        }

        private bool TryWriteR4(Writer wrt, DType type, Type st, float value)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(type.Kind == DKind.R4);
            Validation.Assert(st == (type.IsOpt ? typeof(float?) : typeof(float)));
            wrt.WriteNumberValue(value);
            return true;
        }
    }
}
