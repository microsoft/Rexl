// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sink;

/// <summary>
/// Abstract class for reporting information from rexl script execution.
/// </summary>
public abstract partial class EvalSink : ValueSink
{
    /// <summary>
    /// Post an execution exception. If this returns false, the caller should throw.
    /// If it returns true, the caller should not throw.
    /// </summary>
    public bool PostExecException(Exception? ex, RexlFormula fma)
    {
        if (ex is null)
            return false;
        Validation.AssertValue(fma);
        return PostExecExceptionCore(ex, fma);
    }

    /// <summary>
    /// Post a diagnostic, indicating the source and optional context.
    /// </summary>
    public void PostDiagnostic(DiagSource src, BaseDiagnostic? diag, RexlNode? nodeCtx = null)
    {
        if (diag is not null)
            PostDiagnosticCore(src, diag, nodeCtx);
    }

    /// <summary>
    /// Post a value.
    /// </summary>
    public void PostValue(DType type, object? value)
    {
        PostValueCore(type, value);
    }

    protected EvalSink() : base() { }

    protected virtual bool PostExecExceptionCore(Exception ex, RexlFormula fma)
    {
        Validation.AssertValue(ex);
        Validation.AssertValue(fma);
        return false;
    }

    protected abstract void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode? nodeCtx);

    protected abstract void PostValueCore(DType type, object? value);
}

/// <summary>
/// An <see cref="EvalSink"/> that just drops the information. This is a convenient
/// base class for minimal implementations.
/// </summary>
public abstract class BlankEvalSink : EvalSink
{
    protected BlankEvalSink() : base() { }

    protected override void WriteCore(int value) { }
    protected override void WriteCore(long value) { }
    protected override void WriteCore(char value) { }
    protected override void WriteCore(char value, int count) { }
    protected override void WriteCore(ReadOnlySpan<char> value) { }
    protected override void WriteCore(string value) { }
    protected override void WriteCore(string fmt, params object[] args) { }
    protected override void WriteLineCore() { }
    protected override void WriteLineCore(string value) { }
    protected override void WriteLineCore(string fmt, params object[] args) { }

    protected override void WriteTypeCore(DType value) { }
    protected override void WritePrettyTypeCore(Type? value) { }
    protected override void WriteRawTypeCore(Type? value) { }

    protected override void WriteDiagCore(DiagSource src, BaseDiagnostic diag) { }

    protected override void WriteValueCore(DType type, object? value, int max) { }

    protected override void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode? nodeCtx) { }

    protected override void PostValueCore(DType type, object? value) { }
}

/// <summary>
/// An <see cref="EvalSink"/> that writes to a <see cref="StringBuilder"/>.
/// </summary>
public abstract class SbEvalSink : EvalSink
{
    protected readonly StringBuilder _sbOut;

    public override long CchOut => _sbOut.Length;

    protected SbEvalSink(StringBuilder? sb)
        : base()
    {
        _sbOut = sb ?? new StringBuilder();
    }

    // The block below was copied and pasted from SbTextSink.
    #region StringBuilder usage.
    protected override void WriteCore(int value) => _sbOut.Append(value);
    protected override void WriteCore(long value) => _sbOut.Append(value);
    protected override void WriteCore(char value) => _sbOut.Append(value);
    protected override void WriteCore(char value, int count) => _sbOut.Append(value, count);
    protected override void WriteCore(ReadOnlySpan<char> value) => _sbOut.Append(value);
    protected override void WriteCore(string value) => _sbOut.Append(value.VerifyValue());
    protected override void WriteCore(string fmt, params object[] args) => _sbOut.AppendFormat(fmt.VerifyValue(), MapArgs(args.VerifyValue()));
    protected override void WriteLineCore() => _sbOut.AppendLine();
    #endregion StringBuilder usage.
}

/// <summary>
/// An <see cref="EvalSink"/> that writes to a <see cref="StringBuilder"/>, where the flush operation
/// forwords the contents of the string builder to the abstract <see cref="Dump(StringBuilder)"/> method
/// and then clears the string builder.
/// </summary>
public abstract class FlushEvalSink : SbEvalSink
{
    /// <summary>
    /// The number of characters that have been "dumped" out of <see cref="SbEvalSink._sbOut"/>.
    /// </summary>
    private long _cchOut;

    public override long CchOut => _cchOut + _sbOut.Length;

    protected FlushEvalSink(StringBuilder? sb = null)
        : base(sb)
    {
    }

    protected override void PostWrite()
    {
        if (_sbOut.Length > 1000)
            Flush();
    }

    /// <summary>
    /// Write from the string builder to "real" output.
    /// </summary>
    protected abstract void Dump(StringBuilder sb);

    protected override void FlushCore()
    {
        if (_sbOut.Length > 0)
        {
            _cchOut += _sbOut.Length;
            Dump(_sbOut);
            _sbOut.Clear();
        }
    }
}
