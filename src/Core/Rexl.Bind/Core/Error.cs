// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl;

/// <summary>
/// Option flags for formatting diagnostics.
/// </summary>
[Flags]
public enum DiagFmtOptions
{
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Include context information from a token or node.
    /// </summary>
    Context = 0x01,

    /// <summary>
    /// Include position information.
    /// </summary>
    Position = 0x02,

    /// <summary>
    /// Include raw position information, namely character indices.
    /// </summary>
    PositionRaw = 0x04,

    /// <summary>
    /// Include both raw position information, namely character indices, and row column information.
    /// </summary>
    PositionBoth = Position | PositionRaw,

    /// <summary>
    /// Include kind information, eg, "Warning" or "Error".
    /// </summary>
    Kind = 0x08,

    /// <summary>
    /// Include kind information, eg, "Warning" or "Error", before the position information.
    /// </summary>
    KindFirst = 0x10,

    /// <summary>
    /// The default for product code.
    /// </summary>
    Default = Position | Kind,

    /// <summary>
    /// Default for tests.
    /// </summary>
    DefaultTest = Context | PositionRaw | KindFirst,
}

/// <summary>
/// Abstract base class for rexl diagnostics.
/// </summary>
public abstract class BaseDiagnostic
{
    /// <summary>
    /// The base diagnostic or null.
    /// </summary>
    public BaseDiagnostic? InnerDiagnostic { get; }

    /// <summary>
    /// An associated raw exception. Typically null.
    /// </summary>
    public virtual Exception? RawException => null;

    /// <summary>
    /// Whether this is a bare wrapper around an exception, with no additional detail.
    /// </summary>
    public virtual bool IsPureException => false;

    /// <summary>
    /// Whether this diagnostic is an error or warning.
    /// REVIEW: Add an enum for severity.
    /// </summary>
    public abstract bool IsError { get; }

    protected BaseDiagnostic()
    {
    }

    protected BaseDiagnostic(BaseDiagnostic? innerDiagnostic)
    {
        Validation.AssertValueOrNull(innerDiagnostic);
        InnerDiagnostic = innerDiagnostic;
    }

    /// <summary>
    /// Append the diagnostic information into the given string builder.
    /// </summary>
    public void Format(TextSink sink, Func<object, object>? argMap = null,
        DiagFmtOptions options = DiagFmtOptions.Default)
    {
        Validation.BugCheckValue(sink, nameof(sink));

        FormatCore(sink, argMap, options);
        FormatInner(sink, argMap, options);
    }

    protected virtual bool WriteKind(TextSink sink)
    {
        sink.Write(IsError ? "Error: " : "Warning: ");
        return true;
    }

    protected virtual bool WritePosition(TextSink sink, DiagFmtOptions options)
    {
        return false;
    }

    protected virtual bool WriteContext(TextSink sink)
    {
        return false;
    }

    protected abstract void WriteMessage(TextSink sink, Func<object?, object?>? argMap);

    /// <summary>
    /// Implemented by sub-classes.
    /// </summary>
    protected virtual void FormatCore(TextSink sink, Func<object, object>? argMap, DiagFmtOptions options)
    {
        Validation.AssertValue(sink);

        if ((options & DiagFmtOptions.KindFirst) != 0)
            WriteKind(sink);
        if ((options & DiagFmtOptions.PositionBoth) != 0)
            WritePosition(sink, options);
        if ((options & DiagFmtOptions.Kind) != 0)
            WriteKind(sink);
        if ((options & DiagFmtOptions.Context) != 0)
        {
            if (WriteContext(sink))
                sink.Write(", Message: ");
        }

        WriteMessage(sink, argMap);
    }

    /// <summary>
    /// Get the content of the error message.
    /// </summary>
    public abstract string GetMessage(Func<object, object>? argMap = null);

    private void FormatInner(TextSink sink, Func<object, object>? argMap, DiagFmtOptions optionsOuter)
    {
        Validation.AssertValue(sink);

        if (InnerDiagnostic != null)
        {
            sink.Write(", ");
            InnerDiagnostic.Format(sink, argMap: argMap, options: DiagFmtOptions.None);
        }
    }

    public override string ToString()
    {
        var sink = new SbTextSink();
        Format(sink, options: DiagFmtOptions.Kind);
        return sink.Builder.ToString();
    }

    public string ToString(DiagFmtOptions options)
    {
        var sink = new SbTextSink();
        Format(sink, options: options);
        return sink.Builder.ToString();
    }
}

/// <summary>
/// Abstract base class for rexl diagnostics that have an attached message encoded as a
/// StringId and optional args.
/// </summary>
public abstract class MessageDiagnosticBase : BaseDiagnostic
{
    /// <summary>
    /// The message/format string as a resource StringId.
    /// </summary>
    public StringId Message { get; }

    /// <summary>
    /// The args for the message/format string or null if it has none.
    /// REVIEW: Make this an Immutable.Array.
    /// </summary>
    public object[] Args { get; }

    private protected MessageDiagnosticBase(StringId msg)
    {
        Validation.Assert(msg.IsValid);
        Message = msg;
    }

    private protected MessageDiagnosticBase(StringId msg, params object[] args)
    {
        Validation.Assert(msg.IsValid);
        Validation.AssertValue(args);
        Message = msg;
        Args = args;
    }

    protected override void WriteMessage(TextSink sink, Func<object?, object?>? argMap)
    {
        Validation.AssertValue(sink);

        var msg = Message.GetString();
        if (Args is null)
            sink.Write(msg);
        else
            sink.Write(msg, ArgMapping.MapArgs(Args, argMap));
    }

    public override string GetMessage(Func<object, object>? argMap = null)
    {
        var res = Message.GetString();
        if (Args is null)
            return res;
        return string.Format(res, ArgMapping.MapArgs(Args, argMap));
    }
}

/// <summary>
/// For diagnostics that have an attached message encoded as a StringId and optional args, but are not
/// attached to a location in code.
/// </summary>
public sealed class MessageDiag : MessageDiagnosticBase
{
    public override bool IsError { get; }

    public override Exception? RawException { get; }

    public override bool IsPureException { get; }

    private MessageDiag(Exception ex)
        : base(ErrorStrings.ErrCaughtException)
    {
        Validation.AssertValue(ex);
        IsError = true;
        RawException = ex;
        IsPureException = true;
    }

    private MessageDiag(StringId msg, bool error, Exception? ex = null)
        : base(msg)
    {
        IsError = error;
        RawException = ex;
    }

    private MessageDiag(StringId msg, object[] args, bool error, Exception? ex = null)
        : base(msg, args)
    {
        IsError = error;
        RawException = ex;
    }

    public static MessageDiag Exception(Exception ex)
    {
        Validation.BugCheckValue(ex, nameof(ex));
        return new MessageDiag(ex);
    }

    public static MessageDiag Error(StringId msg)
        => new MessageDiag(msg, error: true);
    public static MessageDiag Error(StringId msg, params object[] args)
        => new MessageDiag(msg, args, error: true);

    public static MessageDiag Error(Exception? ex, StringId msg)
        => new MessageDiag(msg, error: true, ex);
    public static MessageDiag Error(Exception? ex, StringId msg, params object[] args)
        => new MessageDiag(msg, args, error: true, ex);

    public static MessageDiag Warning(StringId msg) => new MessageDiag(msg, error: false);
    public static MessageDiag Warning(StringId msg, params object[] args) => new MessageDiag(msg, args, error: false);
}

/// <summary>
/// A diagnostic object that has associated rexl code, encoded as a token and possibly parse node.
/// </summary>
public sealed class RexlDiagnostic : MessageDiagnosticBase
{
    /// <summary>
    /// An associated parse node or null.
    /// </summary>
    public RexlNode Node { get; }

    /// <summary>
    /// An associated token, never null.
    /// </summary>
    public Token Tok { get; }

    /// <summary>
    /// Whether the token was explicitly provided when the diagnostic was created,
    /// indicating that the token has special importance and should generally be
    /// highlighted to the user.
    /// </summary>
    public bool ShowTok { get; }

    /// <summary>
    /// A possible replacement within the text to resolve this diagnostic. May be null.
    /// </summary>
    public string Guess { get; }

    /// <summary>
    /// The range within the text to replace with the guess. If <see cref="Guess"/>
    /// is null, this is default (all zeros).
    /// </summary>
    public SourceRange RngGuess { get; }

    /// <summary>
    /// Whether this diagnostic is an error or a warning.
    /// </summary>
    public override bool IsError { get; }

    private RexlDiagnostic(bool isError, bool showTok, Token tok, RexlNode node, StringId msg, string guess, SourceRange rngGuess)
        : base(msg)
    {
        Validation.AssertValue(tok);
        Validation.AssertValueOrNull(node);
        Validation.AssertValueOrNull(guess);

        Node = node;
        Tok = tok;
        ShowTok = showTok;
        IsError = isError;
        Guess = guess;
        RngGuess = rngGuess;
    }

    private RexlDiagnostic(bool isError, bool showTok, Token tok, RexlNode node, StringId msg, string guess, SourceRange rngGuess, object[] args)
        : base(msg, args)
    {
        Validation.AssertValue(tok);
        Validation.AssertValueOrNull(node);
        Validation.AssertValueOrNull(guess);

        Node = node;
        Tok = tok;
        ShowTok = showTok;
        IsError = isError;
        Guess = guess;
        RngGuess = rngGuess;
    }

    protected override bool WritePosition(TextSink sink, DiagFmtOptions options)
    {
        switch (options & DiagFmtOptions.PositionBoth)
        {
        default:
            return false;

        case DiagFmtOptions.PositionBoth:
            {
                var (lineMin, colMin) = Tok.Range.Source.GetLineCol(Tok.Range.Min);
                var (lineLim, colLim) = Tok.Range.Source.GetLineCol(Tok.Range.Lim, end: true);
                var min = Tok.Range.Source.GetCleanPosition(Tok.Range.Min);
                var lim = Tok.Range.Source.GetCleanPosition(Tok.Range.Lim);
                sink.Write(
                    ErrorStrings.FormatRange_Min_Lim_LineMin_ColMin_LineLim_ColLim.GetString(),
                    min, lim, lineMin + 1, colMin + 1, lineLim + 1, colLim + 1);
                return true;
            }
        case DiagFmtOptions.Position:
            {
                var (lineMin, colMin) = Tok.Range.Source.GetLineCol(Tok.Range.Min);
                var (lineLim, colLim) = Tok.Range.Source.GetLineCol(Tok.Range.Lim, end: true);
                sink.Write(
                    ErrorStrings.FormatRange_LineMin_ColMin_LineLim_ColLim.GetString(),
                    lineMin + 1, colMin + 1, lineLim + 1, colLim + 1);
                return true;
            }
        case DiagFmtOptions.PositionRaw:
            {
                var min = Tok.Range.Source.GetCleanPosition(Tok.Range.Min);
                var lim = Tok.Range.Source.GetCleanPosition(Tok.Range.Lim);
                sink.Write(ErrorStrings.FormatSpan_Min_Lim.GetString(), min, lim);
            }
            return true;
        }
    }

    protected override bool WriteContext(TextSink sink)
    {
        if (Node == null)
            sink.Write(ErrorStrings.InfoTok_Tok.GetString(), Tok.GetTextString());
        else if (ShowTok)
            sink.Write(ErrorStrings.InfoTokNode_Tok_Node.GetString(), Node, Tok.GetTextString());
        else
            sink.Write(ErrorStrings.InfoNode_Node.GetString(), Node);
        return true;
    }

    public static RexlDiagnostic Error(RexlNode node, StringId msg)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);

        return new RexlDiagnostic(true, false, node.Token, node, msg, null, default);
    }

    public static RexlDiagnostic Error(RexlNode node, StringId msg, params object[] args)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(true, false, node.Token, node, msg, null, default, args);
    }

    public static RexlDiagnostic ErrorGuess(RexlNode node, StringId msg, string guess, SourceRange rngGuess, params object[] args)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);
        Validation.BugCheckValue(guess, nameof(guess));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(true, false, node.Token, node, msg, guess, rngGuess, args);
    }

    public static RexlDiagnostic Error(Token tok, StringId msg)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));

        return new RexlDiagnostic(true, true, tok, null, msg, null, default);
    }

    public static RexlDiagnostic Error(Token tok, StringId msg, params object[] args)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(true, true, tok, null, msg, null, default, args);
    }

    public static RexlDiagnostic ErrorGuess(Token tok, StringId msg, string guess, SourceRange rngGuess)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));

        return new RexlDiagnostic(true, true, tok, null, msg, guess, rngGuess);
    }

    public static RexlDiagnostic ErrorGuess(Token tok, StringId msg, string guess, SourceRange rngGuess, params object[] args)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(true, true, tok, null, msg, guess, rngGuess, args);
    }

    public static RexlDiagnostic Error(Token tok, RexlNode node, StringId msg, params object[] args)
    {
        Validation.BugCheckValueOrNull(tok);
        Validation.BugCheckValue(node, nameof(node));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(true, true, tok ?? node.Token, node, msg, null, default, args);
    }

    public static RexlDiagnostic ErrorGuess(Token tok, RexlNode node, StringId msg, string guess, SourceRange rngGuess, params object[] args)
    {
        Validation.BugCheckValueOrNull(tok);
        Validation.BugCheckValue(node, nameof(node));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(args, nameof(args));
        Validation.BugCheckValue(guess, nameof(guess));

        return new RexlDiagnostic(true, true, tok ?? node.Token, node, msg, guess, rngGuess, args);
    }

    public static RexlDiagnostic Warning(RexlNode node, StringId msg)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);

        return new RexlDiagnostic(false, false, node.Token, node, msg, null, default);
    }

    public static RexlDiagnostic WarningGuess(RexlNode node, StringId msg, string guess, SourceRange rngGuess)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);
        Validation.BugCheckValue(guess, nameof(guess));

        return new RexlDiagnostic(false, false, node.Token, node, msg, guess, rngGuess);
    }

    public static RexlDiagnostic Warning(RexlNode node, StringId msg, params object[] args)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(false, false, node.Token, node, msg, null, default, args);
    }

    public static RexlDiagnostic WarningGuess(RexlNode node, StringId msg, string guess, SourceRange rngGuess, params object[] args)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);
        Validation.BugCheckValue(guess, nameof(guess));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(false, false, node.Token, node, msg, guess, rngGuess, args);
    }

    public static RexlDiagnostic WarningGuess(Token tok, RexlNode node, StringId msg, string guess, SourceRange rngGuess, params object[] args)
    {
        Validation.BugCheckValue(node, nameof(node));
        Validation.AssertValue(node.Token, nameof(node.Token));
        Validation.BugCheck(msg.IsValid);
        Validation.BugCheckValue(guess, nameof(guess));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(false, true, tok, node, msg, guess, rngGuess, args);
    }

    public static RexlDiagnostic Warning(Token tok, StringId msg, params object[] args)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(false, true, tok, null, msg, null, default, args);
    }

    public static RexlDiagnostic WarningGuess(Token tok, StringId msg, string guess, SourceRange rngGuess)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(guess, nameof(guess));

        return new RexlDiagnostic(false, true, tok, null, msg, guess, rngGuess);
    }

    public static RexlDiagnostic WarningGuess(Token tok, StringId msg, string guess, SourceRange rngGuess, params object[] args)
    {
        Validation.BugCheckValue(tok, nameof(tok));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(guess, nameof(guess));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(false, true, tok, null, msg, guess, rngGuess, args);
    }

    public static RexlDiagnostic Warning(Token tok, RexlNode node, StringId msg, params object[] args)
    {
        Validation.BugCheckValueOrNull(tok);
        Validation.BugCheckValue(node, nameof(node));
        Validation.BugCheckParam(msg.IsValid, nameof(msg));
        Validation.BugCheckValue(args, nameof(args));

        return new RexlDiagnostic(false, true, tok ?? node.Token, node, msg, null, default, args);
    }

    /// <summary>
    /// Split the diagnostics into errors and warnings and collect up suggestions/modifications, sorting them by min, with tie-breaker lim.
    /// </summary>
    public static (Immutable.Array<T> errs, Immutable.Array<T> wrns, Immutable.Array<RexlDiagnostic> mods) Partition<T>(Immutable.Array<T> diags)
        where T : BaseDiagnostic
    {
        var errs = Immutable.Array<T>.Empty;
        var wrns = Immutable.Array<T>.Empty;
        var mods = Immutable.Array<RexlDiagnostic>.Empty;

        if (diags.Length > 0)
        {
            var bldrErrs = diags.ToBuilder();
            var bldrWrns = diags.ToBuilder();
            int ierr = 0;
            int iwrn = 0;

            List<RexlDiagnostic> bldrMods = null;
            for (int idiag = 0; idiag < diags.Length; idiag++)
            {
                var diag = diags[idiag];
                if (diag.IsError)
                {
                    if (ierr < idiag)
                        bldrErrs[ierr] = diag;
                    ierr++;
                }
                else
                {
                    if (iwrn < idiag)
                        bldrWrns[iwrn] = diag;
                    iwrn++;
                }

                // Collection up the modifications.
                if (diag is RexlDiagnostic rd && rd.Guess != null)
                    Util.Add(ref bldrMods, rd);
            }
            Validation.Assert(bldrErrs.Count == diags.Length);
            Validation.Assert(bldrWrns.Count == diags.Length);
            Validation.AssertIndexInclusive(ierr, bldrErrs.Count);
            Validation.AssertIndexInclusive(iwrn, bldrWrns.Count);
            Validation.Assert(ierr + iwrn == diags.Length);

            if (ierr > 0)
            {
                if (ierr < bldrErrs.Count)
                    bldrErrs.RemoveTail(ierr);
                errs = bldrErrs.ToImmutable();
            }
            if (iwrn > 0)
            {
                if (iwrn < bldrWrns.Count)
                    bldrWrns.RemoveTail(iwrn);
                wrns = bldrWrns.ToImmutable();
            }

            if (bldrMods != null)
            {
                SortGuesses(bldrMods);
                mods = bldrMods.ToImmutableArray();
            }
        }

        return (errs, wrns, mods);
    }

    /// <summary>
    /// Used to sort diagnostics that include a suggested change.
    /// </summary>
    public static int CompareSuggestions(RexlDiagnostic a, RexlDiagnostic b)
    {
        int d = a.RngGuess.Min - b.RngGuess.Min;
        return d != 0 ? d : a.RngGuess.Lim - b.RngGuess.Lim;
    }

    public static void SortGuesses(List<RexlDiagnostic> mods)
    {
        if (Util.Size(mods) <= 1)
            return;

        // Do bubble sort, since we want a stable sort.
        // REVIEW: Is there a standard stable sort that can be used? O(n^2) should be ok, but
        // O(n ln(n)) might be safer / better.
        for (; ; )
        {
            bool changed = false;
            for (int i = 1; i < mods.Count; i++)
            {
                var a = mods[i - 1];
                var b = mods[i];
                int d = CompareSuggestions(a, b);
                if (d > 0)
                {
                    mods[i - 1] = b;
                    mods[i] = a;
                    changed = true;
                }
            }
            if (!changed)
                return;
        }
    }
}
