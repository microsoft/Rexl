// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sink;

/// <summary>
/// An abstract class for writing textual information.
/// </summary>
public abstract partial class TextSink
{
    /// <summary>
    /// Don't call ToString on this type.
    /// </summary>
    public new void ToString() { }

    #region Public API.

    /// <summary>
    /// Flush content.
    /// </summary>
    public partial void Flush();

    /// <summary>
    /// Write an integer value.
    /// </summary>
    public partial void Write(int value);

    /// <summary>
    /// Write a long value.
    /// </summary>
    public partial void Write(long value);

    /// <summary>
    /// Write a character.
    /// </summary>
    public partial void Write(char value);

    /// <summary>
    /// Write the given character the given number of times.
    /// </summary>
    public partial void Write(char value, int count);

    /// <summary>
    /// Write the given span.
    /// </summary>
    public partial void Write(ReadOnlySpan<char> value);

    /// <summary>
    /// Write the given string, if it is non-null.
    /// </summary>
    public partial void Write(string? value);

    /// <summary>
    /// Write the given format string and arguments. If <paramref name="fmt"/> is null, this does
    /// nothing. If <paramref name="args"/> is null, this does the same as <see cref="Write(string?)"/>.
    /// </summary>
    public partial void Write(string? fmt, params object?[]? args);

    /// <summary>
    /// Write a new line.
    /// </summary>
    public partial void WriteLine();

    /// <summary>
    /// Combination of <see cref="Write(string?)"/> and <see cref="WriteLine()"/>.
    /// </summary>
    public partial void WriteLine(string? value);

    /// <summary>
    /// Combination of <see cref="WriteLine(string?, object?[]?)"/> and <see cref="WriteLine()"/>.
    /// </summary>
    public partial void WriteLine(string? fmt, params object?[]? args);

    #endregion Public API.

    #region Protected abstract/virtual API.

    protected TextSink() { }

    protected abstract object? MapArg(object? arg);
    protected virtual object?[] MapArgs(object?[] args) => TextSinkUtil.MapArgs(args, MapArg);

    protected virtual void FlushCore() { }
    protected virtual void PostWrite() { }

    protected abstract void WriteCore(int value);
    protected abstract void WriteCore(long value);
    protected abstract void WriteCore(char value);
    protected abstract void WriteCore(char value, int count);
    protected abstract void WriteCore(ReadOnlySpan<char> value);
    protected abstract void WriteCore(string value);
    protected abstract void WriteCore(string fmt, params object?[] args);
    protected abstract void WriteLineCore();
    protected virtual void WriteLineCore(string value) { WriteCore(value); WriteLineCore(); }
    protected virtual void WriteLineCore(string fmt, params object?[] args) { WriteCore(fmt, args); WriteLineCore(); }

    #endregion Protected abstract/virtual API.

    #region Implementation of public API.

    public partial void Flush() => FlushCore();
    public partial void Write(int value)
    {
        WriteCore(value);
        PostWrite();
    }
    public partial void Write(long value)
    {
        WriteCore(value);
        PostWrite();
    }
    public partial void Write(char value)
    {
        WriteCore(value);
        PostWrite();
    }
    public partial void Write(char value, int count)
    {
        if (count <= 0)
            return;
        WriteCore(value, count);
        PostWrite();
    }
    public partial void Write(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
            return;
        WriteCore(value);
        PostWrite();
    }
    public partial void Write(string? value)
    {
        if (value is null)
            return;
        WriteCore(value);
        PostWrite();
    }
    public partial void Write(string? fmt, params object?[]? args)
    {
        if (fmt is null)
            return;
        if (args is null)
            WriteCore(fmt);
        else
            WriteCore(fmt, args);
        PostWrite();
    }
    public partial void WriteLine()
    {
        WriteLineCore();
        PostWrite();
    }
    public partial void WriteLine(string? value)
    {
        if (value is null)
            WriteLineCore();
        else
            WriteLineCore(value);
        PostWrite();
    }
    public partial void WriteLine(string? fmt, params object?[]? args)
    {
        if (fmt is null)
            WriteLineCore();
        else if (args is null)
            WriteLineCore(fmt);
        else
            WriteLineCore(fmt, args);
        PostWrite();
    }

    #endregion Implementation of public API.
}

/// <summary>
/// A convenient <see cref="TextSink"/> wrapper of a <see cref="StringBuilder"/>.
/// </summary>
public partial class SbTextSink : TextSink
{
    protected StringBuilder _sbOut;

    /// <summary>
    /// The current <see cref="StringBuilder"/>.
    /// </summary>
    public StringBuilder Builder => _sbOut;

    public SbTextSink(StringBuilder? sb = null)
        : base()
    {
        Validation.AssertValueOrNull(sb);
        _sbOut = sb ?? new StringBuilder();
    }

    /// <summary>
    /// Sets the current <see cref="StringBuilder"/> to <paramref name="sb"/>. Sets <paramref name="prev"/>
    /// to the previous <see cref="StringBuilder"/>.
    /// </summary>
    public SbTextSink SetOut(StringBuilder sb, out StringBuilder prev)
    {
        Validation.BugCheckValue(sb, nameof(sb));
        prev = _sbOut;
        _sbOut = sb;
        return this;
    }

    protected override object? MapArg(object? arg) => TextSinkUtil.MapArg(arg);

    protected override void WriteCore(int value) => _sbOut.Append(value);
    protected override void WriteCore(long value) => _sbOut.Append(value);
    protected override void WriteCore(char value) => _sbOut.Append(value);
    protected override void WriteCore(char value, int count) => _sbOut.Append(value, count);
    protected override void WriteCore(ReadOnlySpan<char> value) => _sbOut.Append(value);
    protected override void WriteCore(string value) => _sbOut.Append(value.VerifyValue());
    protected override void WriteCore(string fmt, params object?[] args) => _sbOut.AppendFormat(fmt.VerifyValue(), MapArgs(args.VerifyValue()));
    protected override void WriteLineCore() => _sbOut.AppendLine();
}

/// <summary>
/// Extension methods for "chaining" methods of <see cref="TextSink"/>.
/// </summary>
public static class TextSinkExt
{
    public static T TWrite<T>(this T @this, int value) where T : TextSink { @this.VerifyValue().Write(value); return @this; }
    public static T TWrite<T>(this T @this, long value) where T : TextSink { @this.VerifyValue().Write(value); return @this; }
    public static T TWrite<T>(this T @this, char value) where T : TextSink { @this.VerifyValue().Write(value); return @this; }
    public static T TWrite<T>(this T @this, char value, int count) where T : TextSink { @this.VerifyValue().Write(value, count); return @this; }
    public static T TWrite<T>(this T @this, ReadOnlySpan<char> value) where T : TextSink { @this.VerifyValue().Write(value); return @this; }
    public static T TWrite<T>(this T @this, string? value) where T : TextSink { @this.VerifyValue().Write(value); return @this; }
    public static T TWrite<T>(this T @this, string? fmt, params object?[]? args) where T : TextSink { @this.VerifyValue().Write(fmt, args); return @this; }
    public static T TWriteLine<T>(this T @this) where T : TextSink { @this.VerifyValue().WriteLine(); return @this; }
    public static T TWriteLine<T>(this T @this, string? value) where T : TextSink { @this.VerifyValue().WriteLine(value); return @this; }
    public static T TWriteLine<T>(this T @this, string? fmt, params object?[]? args) where T : TextSink { @this.VerifyValue().WriteLine(fmt, args); return @this; }
}

/// <summary>
/// Utilities for <see cref="TextSink"/> including formatted message argument mapping.
/// </summary>
public static class TextSinkUtil
{
    /// <summary>
    /// The default formatted message arg mapper.
    /// </summary>
    public static readonly Func<object?, object?> DefaultArgMapper = MapArg;

    /// <summary>
    /// The default formatted message arg mapper.
    /// REVIEW: Could use a better scheme where these values aren't realized as their own
    /// string but are written directly to the sink.
    /// </summary>
    public static object? MapArg(object? arg)
    {
        switch (arg)
        {
        case string str:
            return str;
        case DName name:
            if (name.IsValid)
                return name.Escape();
            return "<blank>";
        case NPath path:
            if (!path.IsRoot)
                return path.ToDottedSyntax();
            return "<root>";
#if DEBUG
        case DType type: break;
        case long i8: break;
        case int i4: break;

        default:
            // This is useful for code coverage, so we can easily see whether there are types
            // that we don't have cases for.
            break;
#endif
        }
        return arg;
    }

    /// <summary>
    /// Maps an args array to a new one, using the given <paramref name="argMap"/>. If <paramref name="argMap"/>
    /// is null, uses <see cref="DefaultArgMapper"/>.
    /// </summary>
    public static object?[] MapArgs(object?[] args, Func<object?, object?>? argMap)
    {
        Validation.BugCheckValue(args, nameof(args));

        if (args.Length == 0)
            return args;

        argMap ??= DefaultArgMapper;
        object?[]? argsNew = null;
        for (int i = 0; i < args.Length; i++)
        {
            var src = args[i];
            var dst = argMap(src);
            if (argsNew is not null)
                argsNew[i] = dst;
            else if (dst != src)
            {
                argsNew = new object[args.Length];
                if (i > 0)
                    Array.Copy(args, argsNew, i);
                argsNew[i] = dst;
            }
        }
        return argsNew ?? args;
    }
}
