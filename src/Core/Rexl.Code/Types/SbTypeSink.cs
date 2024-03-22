// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sink;

/// <summary>
/// A convenient <see cref="SysTypeSink"/> wrapper of a <see cref="StringBuilder"/>.
/// </summary>
public partial class SbSysTypeSink : SysTypeSink
{
    protected StringBuilder _sbOut;

    /// <summary>
    /// The current <see cref="StringBuilder"/>.
    /// </summary>
    public StringBuilder Builder => _sbOut;

    public SbSysTypeSink(StringBuilder? sb = null)
        : base()
    {
        Validation.AssertValueOrNull(sb);
        _sbOut = sb ?? new StringBuilder();
    }

    /// <summary>
    /// Sets the current <see cref="StringBuilder"/> to <paramref name="sb"/>. Sets <paramref name="prev"/>
    /// to the previous <see cref="StringBuilder"/>.
    /// </summary>
    public SbSysTypeSink SetOut(StringBuilder sb, out StringBuilder prev)
    {
        Validation.BugCheckValue(sb, nameof(sb));
        prev = _sbOut;
        _sbOut = sb;
        return this;
    }

    protected override void WriteCore(int value) => _sbOut.Append(value);
    protected override void WriteCore(long value) => _sbOut.Append(value);
    protected override void WriteCore(char value) => _sbOut.Append(value);
    protected override void WriteCore(char value, int count) => _sbOut.Append(value, count);
    protected override void WriteCore(ReadOnlySpan<char> value) => _sbOut.Append(value);
    protected override void WriteCore(string value) => _sbOut.Append(value.VerifyValue());
    protected override void WriteCore(string fmt, params object[] args) => _sbOut.AppendFormat(fmt.VerifyValue(), MapArgs(args.VerifyValue()));
    protected override void WriteLineCore() => _sbOut.AppendLine();
}
