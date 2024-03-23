// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

using Integer = System.Numerics.BigInteger;

/// <summary>
/// Supports raw json writing (not object based) to UTF8.
/// REVIEW: Can this be replaced with a standard type from System.Text.Json, eg,
/// Utf8JsonWriter?
/// </summary>
public sealed partial class RawJsonWriter : IDisposable
{
    private enum State : byte
    {
        None,
        NeedComma,
        NeedValue,
    }

    // REVIEW: Use the new "..."u8 syntax when available.
    private static readonly byte[] _null = new byte[] { (byte)'n', (byte)'u', (byte)'l', (byte)'l' };
    private static readonly byte[] _true = new byte[] { (byte)'t', (byte)'r', (byte)'u', (byte)'e' };
    private static readonly byte[] _false = new byte[] { (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e' };
    private static readonly byte[] _zeroFlt = new byte[] { (byte)'0', (byte)'.', (byte)'0' };
    private static readonly byte[] _negZeroFlt = new byte[] { (byte)'-', (byte)'0', (byte)'.', (byte)'0' };
    private static readonly byte[] _nan = new byte[] { (byte)'"', (byte)'N', (byte)'a', (byte)'N', (byte)'"' };
    private static readonly byte[] _infStr = new byte[] { (byte)'"', (byte)'I', (byte)'n', (byte)'f', (byte)'i', (byte)'n', (byte)'i', (byte)'t', (byte)'y', (byte)'"' };
    private static readonly byte[] _negInfStr = new byte[] { (byte)'"', (byte)'-', (byte)'I', (byte)'n', (byte)'f', (byte)'i', (byte)'n', (byte)'i', (byte)'t', (byte)'y', (byte)'"' };
    private static readonly byte[] _infSym = new byte[] { (byte)'"', 0xE2, 0x88, 0x9E, (byte)'"' };
    private static readonly byte[] _negInfSym = new byte[] { (byte)'"', (byte)'-', 0xE2, 0x88, 0x9E, (byte)'"' };

    private static readonly byte[] _hex = new byte[] {
        (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
        (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };

    private const byte _comma = (byte)',';
    private const byte _colon = (byte)':';
    private const byte _quote = (byte)'"';
    private const byte _curlyOpen = (byte)'{';
    private const byte _curlyClose = (byte)'}';
    private const byte _squareOpen = (byte)'[';
    private const byte _squareClose = (byte)']';

    private readonly Stream _stream;
    private readonly BinaryWriter _core;
    private readonly List<bool> _stack; // True for object, false for array.
    private readonly int _depthMax;
    private readonly StringBuilder _sb;

    private readonly byte[] _inf;
    private readonly byte[] _negInf;

    private State _state;

    /// <summary>
    /// The current depth in the JSON tree.
    /// </summary>
    public int Depth => _stack.Count;

    public bool NeedValue => _state == State.NeedValue;

    public RawJsonWriter(Stream stream, bool symbolicInfs = false)
    {
        Validation.BugCheckValue(stream, nameof(stream));
        Validation.BugCheckParam(stream.CanWrite, nameof(stream));

        _stream = stream;
        _core = new BinaryWriter(_stream, Util.StdUTF8, leaveOpen: true);
        _stack = new List<bool>();
        _depthMax = 1000;
        _sb = new StringBuilder();

        if (symbolicInfs)
        {
            _inf = _infSym;
            _negInf = _negInfSym;
        }
        else
        {
            _inf = _infStr;
            _negInf = _negInfStr;
        }
    }

    public void Dispose()
    {
        _core.Dispose();
    }

    public void Flush() => _core.Flush();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StartValue()
    {
        if (_state == State.NeedComma)
        {
            Validation.BugCheck(_stack.Count > 0 && !_stack.Peek(), "Not in a json array");
            _core.Write(_comma);
        }
        _state = State.NeedComma;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StartProp()
    {
        Validation.BugCheck(_stack.Count > 0 && _stack.Peek(), "Not in a json object");
        switch (_state)
        {
        case State.NeedComma:
            _core.Write(_comma);
            break;
        case State.NeedValue:
            throw Validation.BugExcept("Already have property name");
        }
        _state = State.NeedValue;
    }

    public void WriteStartObject()
    {
        Validation.Check(_stack.Count < _depthMax);
        StartValue();
        _stack.Add(true);
        _core.Write(_curlyOpen);
        _state = State.None;
    }

    public void WriteStartWrapperObject(string typeWrapper)
    {
        Validation.CheckNonEmpty(typeWrapper, nameof(typeWrapper));
        WriteStartObject();
        StartProp();

        // For the wrapper type's key, we use the empty string since it cannot be confused with a
        // REXL identifier. This ensures the object can never be misinterpreted as a record value.
        _core.Write(_quote);
        _core.Write(_quote);
        _core.Write(_colon);
        WriteStringValue(typeWrapper);
        _state = State.NeedComma;
    }

    public void WriteStartArray()
    {
        Validation.Check(_stack.Count < _depthMax);
        StartValue();
        _stack.Add(false);
        _core.Write(_squareOpen);
        _state = State.None;
    }

    public void WriteEndObject()
    {
        Validation.BugCheck(_state != State.NeedValue);
        Validation.BugCheck(_stack.Count > 0);
        bool isObj = _stack.Pop();
        Validation.BugCheck(isObj);
        _core.Write(_curlyClose);
        _state = State.NeedComma;
    }

    public void WriteEndArray()
    {
        Validation.BugCheck(_state != State.NeedValue);
        Validation.BugCheck(_stack.Count > 0);
        bool isObj = _stack.Pop();
        Validation.BugCheck(!isObj);
        _core.Write(_squareClose);
        _state = State.NeedComma;
    }

    public void WritePropertyName(ReadOnlySpan<byte> name)
    {
        Validation.BugCheckParam(name.Length > 0, nameof(name), "Property name should not be empty");
        StartProp();
        _core.Write(_quote);
        _core.Write(name);
        _core.Write(_quote);
        _core.Write(_colon);
        _state = State.NeedValue;
    }

    public void WritePropertyName(string name)
    {
        Validation.BugCheckNonEmpty(name, nameof(name), "Property name should not be empty");
        StartProp();
        _core.Write(_quote);
        EscapeStr(name);
        _core.Write(_quote);
        _core.Write(_colon);
    }

    public void WriteString(ReadOnlySpan<byte> name, string value)
    {
        WritePropertyName(name);
        Validation.Assert(_state == State.NeedValue);
        WriteStringValue(value);
        Validation.Assert(_state == State.NeedComma);
    }

    public void WriteString(string name, string value)
    {
        WritePropertyName(name);
        Validation.Assert(_state == State.NeedValue);
        WriteStringValue(value);
        Validation.Assert(_state == State.NeedComma);
    }

    public void WriteNullValue()
    {
        StartValue();
        _core.Write(_null);
    }

    public void WriteStringValue(ReadOnlySpan<byte> value)
    {
        StartValue();
        _core.Write(_quote);
        _core.Write(value);
        _core.Write(_quote);
    }

    public void WriteStringValue(string value)
    {
        if (value == null)
            WriteNullValue();
        else
        {
            StartValue();
            _core.Write(_quote);
            EscapeStr(value);
            _core.Write(_quote);
        }
    }

    public void WriteStringValue(Guid value)
    {
        StartValue();
        _core.Write(_quote);
        // REVIEW: Make this more direct for efficiency.
        _core.Write((ReadOnlySpan<char>)value.ToString("D"));
        _core.Write(_quote);
    }

    public void WriteBoolean(ReadOnlySpan<byte> name, bool value)
    {
        WritePropertyName(name);
        Validation.Assert(_state == State.NeedValue);
        WriteBooleanValue(value);
        Validation.Assert(_state == State.NeedComma);
    }

    public void WriteBoolean(string name, bool value)
    {
        WritePropertyName(name);
        Validation.Assert(_state == State.NeedValue);
        WriteBooleanValue(value);
        Validation.Assert(_state == State.NeedComma);
    }

    public void WriteBooleanValue(bool value)
    {
        StartValue();
        _core.Write(value ? _true : _false);
    }

    private void WriteSb()
    {
        StartValue();
        for (int ich = 0; ich < _sb.Length; ich++)
        {
            var ch = _sb[ich];
            Validation.AssertIndex((int)ch, 0x80);
            _core.Write((byte)ch);
        }
    }

    public void WriteNumberValue(long value)
    {
        _sb.Clear().Append(value);
        WriteSb();
    }

    public void WriteNumberValue(int value)
    {
        _sb.Clear().Append(value);
        WriteSb();
    }

    public void WriteNumberValue(ulong value)
    {
        _sb.Clear().Append(value);
        WriteSb();
    }

    public void WriteNumberValue(uint value)
    {
        _sb.Clear().Append(value);
        WriteSb();
    }

    /// <summary>
    /// Write the string form of a floating point value from <see cref="_sb"/>.
    /// This doesn't call <see cref="StartValue"/>.
    /// </summary>
    private void WriteSbFloat()
    {
        Validation.Assert(_sb.Length > 0);
        char ch = _sb[0];
        Validation.Assert('0' <= ch && ch <= '9' || ch == '-' || ch == '+' || ch == '.');
        bool any = ch == '.';
        _core.Write((byte)ch);
        for (int ich = 1; ich < _sb.Length; ich++)
        {
            ch = _sb[ich];
            Validation.Assert('0' <= ch && ch <= '9' || ch == '-' || ch == '+' || ch == '.' || ch == 'e' || ch == 'E');
            if (!any && !('0' <= ch && ch <= '9'))
                any = true;
            _core.Write((byte)ch);
        }
        if (!any)
        {
            // This looks like an integer. Append ".0" so it looks like floating point.
            _core.Write((byte)'.');
            _core.Write((byte)'0');
        }
    }

    public void WriteNumberValue(double value)
    {
        StartValue();
        var kind = value.GetValueKind();
        switch (kind)
        {
        case RxValueKind.Zero:
            _core.Write(_zeroFlt);
            break;
        case RxValueKind.NegZero:
            _core.Write(_negZeroFlt);
            break;
        case RxValueKind.Infinity:
            _core.Write(_inf);
            break;
        case RxValueKind.NegInfinity:
            _core.Write(_negInf);
            break;
        case RxValueKind.NaN:
            _core.Write(_nan);
            break;
        default:
            Validation.Assert(kind == RxValueKind.Standard);
            // REVIEW: Optimize.
            _sb.Clear().AppendFormat("{0:G17}", value);
            WriteSbFloat();
            break;
        }
    }

    public void WriteNumberValue(float value)
    {
        if (_state == State.NeedComma)
            _core.Write((byte)',');
        var kind = value.GetValueKind();
        switch (kind)
        {
        case RxValueKind.Zero:
            _core.Write(_zeroFlt);
            break;
        case RxValueKind.NegZero:
            _core.Write(_negZeroFlt);
            break;
        case RxValueKind.Infinity:
            _core.Write(_inf);
            break;
        case RxValueKind.NegInfinity:
            _core.Write(_negInf);
            break;
        case RxValueKind.NaN:
            _core.Write(_nan);
            break;
        default:
            Validation.Assert(kind == RxValueKind.Standard);
            // REVIEW: Optimize.
            // REVIEW: Convert to double first so process isn't lossy.
            _sb.Clear().AppendFormat("{0:G9}", value);
            WriteSbFloat();
            break;
        }
        _state = State.NeedComma;
    }

    public void WriteNumberValue(Integer value)
    {
        _sb.Clear().Append(value);
        WriteSb();
    }

    public void WriteBase64String(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
    {
        WritePropertyName(name);
        Validation.Assert(_state == State.NeedValue);
        WriteBase64StringValue(value);
        Validation.Assert(_state == State.NeedComma);
    }

    public void WriteBase64String(string name, ReadOnlySpan<byte> value)
    {
        WritePropertyName(name);
        Validation.Assert(_state == State.NeedValue);
        WriteBase64StringValue(value);
        Validation.Assert(_state == State.NeedComma);
    }

    public void WriteBase64StringValue(ReadOnlySpan<byte> value)
    {
        // REVIEW: Is there a better (more direct) way that doesn't require copying at the end?
        int sizeIn = value.Length;
        long cbitOut = 8L * sizeIn;
        long cbBuf = cbitOut / 6 + 2;
        Validation.Check(cbBuf <= int.MaxValue);
        var buf = ArrayPool<byte>.Shared.Rent((int)cbBuf);
        var res = Base64.EncodeToUtf8(value, buf, out int cbIn, out int cbOut);
        Validation.BugCheck(res == System.Buffers.OperationStatus.Done);
        Validation.Assert(cbIn == value.Length);

        WriteStringValue(buf.AsSpan(0, cbOut));
        ArrayPool<byte>.Shared.Return(buf, clearArray: true);
    }

    private void EscapeStr(string str)
    {
        ReadOnlySpan<char> chars = str;
        int ichMin = 0;
        int ich = 0;
        byte[] buf = null;
        for (; ich < chars.Length; ich++)
        {
            var ch = chars[ich];
            if (ch < ' ' || ch == '"' || ch == '\\')
            {
                // Must escape.
                if (ichMin < ich)
                    _core.Write(chars.Slice(ichMin, ich - ichMin));
                buf ??= ArrayPool<byte>.Shared.Rent(6);
                int i = 0;
                buf[i++] = (byte)'\\';
                switch (ch)
                {
                case '\t': buf[i++] = (byte)'t'; break;
                case '\n': buf[i++] = (byte)'n'; break;
                case '\r': buf[i++] = (byte)'r'; break;
                case '\b': buf[i++] = (byte)'b'; break;
                case '\f': buf[i++] = (byte)'f'; break;
                case '"': buf[i++] = (byte)'"'; break;
                case '\\': buf[i++] = (byte)'\\'; break;

                default:
                    {
                        // REVIEW: Optimize to do in one Write call - use a pre-allocated buffer.
                        var val = (ushort)ch;
                        buf[i++] = (byte)'u';
                        buf[i++] = _hex[val >> 12];
                        buf[i++] = _hex[(val >> 8) & 0x0F];
                        buf[i++] = _hex[(val >> 4) & 0x0F];
                        buf[i++] = _hex[val & 0x0F];
                        break;
                    }
                }
                _core.Write(buf.AsSpan(0, i));
                ichMin = ich + 1;
            }
        }

        if (ichMin < ich)
            _core.Write(chars.Slice(ichMin, ich - ichMin));
        if (buf != null)
            ArrayPool<byte>.Shared.Return(buf);
    }
}
