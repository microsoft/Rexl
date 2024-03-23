// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sink;

/// <summary>
/// An abstract class for writing type and textual information.
/// </summary>
public abstract partial class TypeSink : TextSink
{
    protected override object? MapArg(object? arg) => ArgMapping.MapArg(arg);

    #region Public API.

    /// <summary>
    /// Write the given <see cref="DType"/>.
    /// </summary>
    public partial void WriteType(DType value);

    /// <summary>
    /// If the given <see cref="DType?"/> value is not null, write it.
    /// </summary>
    public partial void WriteType(DType? value);

    #endregion Public API.

    #region Protected abstract/virtual API.

    protected TypeSink() : base() { }

    protected virtual void WriteTypeCore(DType value) => value.AppendTo(this);

    #endregion Protected abstract/virtual API.

    #region Implementation of public API.

    public partial void WriteType(DType value)
    {
        WriteTypeCore(value);
        PostWrite();
    }
    public partial void WriteType(DType? value)
    {
        if (value is null)
            return;
        WriteTypeCore(value.GetValueOrDefault());
        PostWrite();
    }

    #endregion Implementation of public API.
}

/// <summary>
/// Extension methods for "chaining" methods of <see cref="TypeSink"/>.
/// </summary>
public static class TypeSinkExt
{
    public static T TWriteType<T>(this T @this, DType value) where T : TypeSink { @this.VerifyValue().WriteType(value); return @this; }
    public static T TWriteType<T>(this T @this, DType? value) where T : TypeSink { @this.VerifyValue().WriteType(value); return @this; }
}
