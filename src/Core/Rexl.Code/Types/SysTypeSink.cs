// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sink;

/// <summary>
/// An abstract class for writing type and textual information.
/// </summary>
public abstract partial class SysTypeSink : TypeSink
{
    protected override object? MapArg(object? arg) => ArgMapping.MapArg(arg);

    #region Public API.

    /// <summary>
    /// Write the given system type in a friendly representation.
    /// REVIEW: Better name?
    /// </summary>
    public partial void WritePrettyType(Type? value);

    /// <summary>
    /// Write the given system type in a more "raw" representation, usually for low level
    /// things like dumping IL.
    /// </summary>
    public partial void WriteRawType(Type? value);

    #endregion Public API.

    #region Protected abstract/virtual API.

    protected SysTypeSink() : base() { }

    protected virtual void WritePrettyTypeCore(Type? value) => this.AppendPrettyType(value);
    protected virtual void WriteRawTypeCore(Type? value) => this.AppendRawType(value);

    #endregion Protected abstract/virtual API.

    #region Implementation of public API.

    public partial void WritePrettyType(Type? value)
    {
        WritePrettyTypeCore(value);
        PostWrite();
    }
    public partial void WriteRawType(Type? value)
    {
        WriteRawTypeCore(value);
        PostWrite();
    }

    #endregion Implementation of public API.
}

/// <summary>
/// Indicates a kind for a diagnostic.
/// </summary>
public enum DiagSource
{
    None = 0,

    Parse,
    Flow,
    Bind,
    Statement,
    Solver,
    Session,
    ExecException,
}

/// <summary>
/// Adds methods to write diagnostics and values, including modules.
/// </summary>
public abstract partial class ValueSink : SysTypeSink
{
    #region Public API.

    /// <summary>
    /// Write the givan diagnostic.
    /// </summary>
    public partial void WriteDiag(DiagSource src, BaseDiagnostic? value);

    /// <summary>
    /// Write the given valuel
    /// </summary>
    public partial void WriteValue(DType type, object? value, int max = 32);

    #endregion Public API.

    #region Protected abstract/virtual API.

    /// <summary>
    /// The source of the last diagnostic written with banner.
    /// </summary>
    protected DiagSource _diagSrcCur;

    /// <summary>
    /// The ending character position of the last diagnostic written with banner.
    /// </summary>
    protected long _cchDiagCur;

    /// <summary>
    /// Whether diagnostic banners should be written when diags are written.
    /// </summary>
    protected virtual bool ShowDiagBanners => false;

    /// <summary>
    /// The number of characters written to output so far. This only needs to be
    /// accurate if <see cref="ShowDiagBanners"/> is true.
    /// </summary>
    public virtual long CchOut => -1;

    /// <summary>
    /// The format options to use for displaying diagnostics.
    /// </summary>
    protected virtual DiagFmtOptions DiagOptions => DiagFmtOptions.Default;

    protected ValueSink()
        : base()
    {
    }

    /// <summary>
    /// Writes a diagnostic to output. The default implementation handles writing banners when
    /// <see cref="ShowDiagBanners"/> is true. If <paramref name="src"/> is <see cref="DiagSource.None"/>,
    /// no banner or indentation is written.
    /// </summary>
    protected virtual void WriteDiagCore(DiagSource src, BaseDiagnostic diag)
    {
        Validation.AssertValue(diag);

        if (!ShowDiagBanners || src == DiagSource.None)
        {
            WriteDiagRaw(diag);
            return;
        }

        if (src != _diagSrcCur || CchOut != _cchDiagCur)
        {
            switch (src)
            {
            case DiagSource.Parse:
                WriteLine("*** Parse diagnostics:");
                break;
            case DiagSource.Bind:
                WriteLine("*** Bind diagnostics:");
                break;
            case DiagSource.Statement:
                WriteLine("*** Statement diagnostics:");
                break;
            case DiagSource.Flow:
                WriteLine("*** Flow diagnostics:");
                break;
            case DiagSource.Solver:
                WriteLine("*** Solver diagnostics:");
                break;
            }
            _diagSrcCur = src;
        }

        this.TWrite("  ").WriteDiagRaw(diag);

        _cchDiagCur = CchOut;
    }

    /// <summary>
    /// Write a diagnostic. The default implementation writes path information, if available, followed
    /// by the formatted diagnostic, followed by any exception information.
    /// </summary>
    protected virtual void WriteDiagRaw(BaseDiagnostic diag)
    {
        Validation.AssertValue(diag);

        if (diag is RexlDiagnostic rd)
            DumpPath(rd.Tok.Stream.Source);
        diag.Format(this, MapDiagArg, DiagOptions);
        WriteLine();
        for (Exception ex = diag.RawException; ex is not null; ex = ex.InnerException)
        {
            this.TWrite("    Exception (").TWriteRawType(ex.GetType()).TWrite("): ")
                .WriteLine(ex.Message);
        }
    }

    /// <summary>
    /// Writes path information, with a trailing space if anything is added.
    /// </summary>
    protected virtual void DumpPath(SourceContext src)
    {
        Validation.AssertValue(src);
        var path = src.LinkCtx.GetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            Write("[");
            Write(path);
            Write("] ");
        }
    }

    /// <summary>
    /// For mapping diagnostic message args. The default is to do the same mapping as for
    /// other formatted args.
    /// </summary>
    protected virtual object MapDiagArg(object arg) => MapArg(arg);

    protected abstract void WriteValueCore(DType type, object? value, int max);

    #endregion Protected abstract/virtual API.

    #region Implementation of public API.

    public partial void WriteDiag(DiagSource src, BaseDiagnostic? value)
    {
        if (value is null)
            return;
        WriteDiagCore(src, value);
        PostWrite();
    }
    public partial void WriteValue(DType type, object? value, int max)
    {
        WriteValueCore(type, value, max);
        PostWrite();
    }

    #endregion Implementation of public API.
}

/// <summary>
/// Extension methods for "chaining" methods of <see cref="TypeSink"/>.
/// </summary>
public static class SysTypeSinkExt
{
    public static T TWritePrettyType<T>(this T @this, Type? value) where T : SysTypeSink { @this.VerifyValue().WritePrettyType(value); return @this; }
    public static T TWriteRawType<T>(this T @this, Type? value) where T : SysTypeSink { @this.VerifyValue().WriteRawType(value); return @this; }

    public static T TWriteDiag<T>(this T @this, DiagSource src, BaseDiagnostic? value) where T : ValueSink { @this.VerifyValue().WriteDiag(src, value); return @this; }
    public static T TWriteValue<T>(this T @this, DType type, object? value, int max = 32) where T : ValueSink { @this.VerifyValue().WriteValue(type, value, max); return @this; }
}
