// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Statement;

namespace Microsoft.Rexl.Harness;

// This partial contains the abstract handlers.
partial class HarnessBase
{
    /// <summary>
    /// The <see cref="RunAsync"/> method is starting its work.
    /// </summary>
    protected virtual void HandleRunBegin(SourceContext source)
    {
        Validation.AssertValue(source);
    }

    /// <summary>
    /// Execution is being resumed.
    /// </summary>
    protected virtual void HandleResume(SourceContext source, Instruction inst, int pos, Stream state)
    {
        Validation.AssertValue(source);
        Validation.AssertValue(inst);
        Validation.AssertValue(state);

        if (IsVerbose)
            Sink.TWriteLine().WriteLine("*** Resumed ***");
    }

    /// <summary>
    /// An exception has been caught by the <see cref="RunAsync"/> method. Returning <c>false</c> means
    /// the exception should be thrown.
    /// </summary>
    protected virtual bool HandleRunException(SourceContext source, Exception ex)
    {
        Validation.AssertValueOrNull(source);
        Validation.AssertValue(ex);

        PostDiagnostic(DiagSource.ExecException, MessageDiag.Exception(ex));

        // Don't throw.
        return true;
    }

    /// <summary>
    /// The <see cref="RunAsync"/> method is finished (with or without errors). Note that
    /// <paramref name="source"/> may be <c>null</c>.
    /// </summary>
    protected virtual void HandleRunFinally(SourceContext source)
    {
        Validation.AssertValueOrNull(source);
        Flush();
    }

    /// <summary>
    /// Parsing produced warnings and/or errors.
    /// </summary>
    protected virtual void HandleParseIssues(Immutable.Array<RexlDiagnostic> issues)
    {
        Validation.Assert(issues.Length > 0);
        foreach (var issue in issues)
            PostDiagnostic(DiagSource.Parse, issue);
    }

    /// <summary>
    /// Creating the <see cref="StmtFlow"/> produced warnings and/or errors.
    /// </summary>
    protected virtual void HandleFlowIssues(Immutable.Array<RexlDiagnostic> issues)
    {
        Validation.Assert(issues.Length > 0);
        foreach (var issue in issues)
            PostDiagnostic(DiagSource.Flow, issue);
    }

    /// <summary>
    /// Binding produced warnings and/or errors.
    /// </summary>
    protected virtual void HandleBindIssues(Immutable.Array<BaseDiagnostic> issues, RexlNode node = null)
    {
        Validation.Assert(issues.Length > 0);
        foreach (var issue in issues)
            PostDiagnostic(DiagSource.Bind, issue);
    }

    protected virtual void HandleStmtIssue(RexlDiagnostic diag, RexlNode node = null)
    {
        Validation.AssertValue(diag);
        PostDiagnostic(DiagSource.Statement, diag, node);
    }

    /// <summary>
    /// A udf declaration was processed. <paramref name="cur"/> will either be same as <paramref name="udo"/>
    /// or <c>null</c>, the latter indicating that the udf for the name and arity was removed. This is
    /// done when the body of <paramref name="udo"/> is just the underscore token.
    /// </summary>
    protected virtual void HandleUserOper(UserOper udo, UserOper prev, UserOper cur)
    {
        Validation.Assert(udo.IsValid);
        Validation.Assert(cur.Oper == udo.Oper || !cur.IsValid);

        if (!IsVerbose)
            return;

        if (prev.IsValid)
        {
            var kind = prev.AsFunc is not null ? "UDF" : "UDP";
            Sink.WriteLine(cur.IsValid ? "Overwriting {0}: {1}" : "Deleting {0}: {1}", kind, prev.Oper.Path);
        }
        if (cur.IsValid)
        {
            var kind = cur.AsFunc is not null ? "UDF" : "UDP";
            Sink.WriteLine("{0} '{1}' has arity {2}", kind, udo.Oper.Path, cur.Arity);
        }
    }

    /// <summary>
    /// A udp declaration was processed. <paramref name="cur"/> will either be same as <paramref name="udp"/>
    /// or <c>null</c>, the latter indicating that the udp for the name and arity was removed.
    /// </summary>
    protected virtual void HandleUserProc(UserProc udp, UserProc prev, UserProc cur)
    {
        Validation.AssertValue(udp);
        Validation.Assert(cur == udp || cur == null);

        if (!IsVerbose)
            return;

        if (prev != null)
            Sink.WriteLine(cur != null ? "Overwriting UDP: {0}" : "Deleting UDP: {0}", prev.Path);
        if (cur != null)
            Sink.WriteLine("UDP '{0}' has arity {1}", udp.Path, cur.Arity);
    }

    /// <summary>
    /// Called to process an exception when executing an expression or import script. This should
    /// return <c>true</c> to indicate that processing should continue and <c>false</c> to indicate
    /// that the exception should be (re)thrown.
    /// </summary>
    protected virtual bool HandleExecException(Exception ex, RexlFormula fma)
    {
        Validation.AssertValue(ex);
        return Sink.PostExecException(ex, fma);
    }

    /// <summary>
    /// Called to process an io exception generated when importing a script.
    /// </summary>
    protected virtual void HandleImportException(IOException ex)
    {
        Validation.AssertValue(ex);

        var msg = ex.InnerException?.Message ?? ex.Message;
        PostDiagnostic(DiagSource.Statement,
            MessageDiag.Error(ex, ErrorStrings.ErrImportException_Msg, msg));
    }

    /// <summary>
    /// Called at the end of processing a script with the elapsed ticks and execution ticks
    /// and whether execution concluded normally.
    /// </summary>
    protected virtual void HandleProcessScript(
        long ticksElapsed, long ticksExe, bool ok, ref Stream suspendState)
    {
        Validation.Assert(ticksElapsed >= 0);
        Validation.Assert(ticksExe >= 0);

        if (_config.ShowTime)
        {
            string dtElap = (ticksElapsed / 10).ToString("N0");

            Sink.WriteLine();
            if (ticksExe > 0)
            {
                string dtExec = (ticksExe / 10).ToString("N0");
                int cch = dtElap.Length - dtExec.Length;
                if (cch > 0)
                    dtExec = new string(' ', cch) + dtExec;
                Sink.WriteLine(" File execute time (μs): {0}", dtExec);
            }
            Sink.TWriteLine(" File elapsed time (μs): {0}", dtElap).WriteLine();
        }
    }

    /// <summary>
    /// Called after a formula has been bound.
    /// </summary>
    protected virtual void HandleBoundFormula(BoundFormula bfma)
    {
        Validation.AssertValue(bfma);

        var fma = bfma.Formula;
        bool showTypedParseTree = _config.ShowTypedParseTree;
        bool showVerboseBoundTree = _config.ShowVerboseBoundTree;
        bool showBoundTree = _config.ShowBoundTree;

        if (showTypedParseTree)
        {
            Sink.WriteLine("=== Typed parse tree:");

            void WriteNote(string prefix, RexlNode nd)
            {
                Sink.Write(prefix);

                // If TryGetNodeDType returns false, we still print default(DType) ('x').
                bfma.TryGetNodeType(nd, out DType type);
                Sink.TWrite("DType:").WriteType(type);

                bfma.TryGetNodeScopeInfo(nd, out var scopes);
                if (scopes == null)
                    return;

                Sink.Write(", Scopes:[");
                string pre = "";
                foreach (var scope in scopes)
                {
                    Sink.Write(pre);
                    pre = ", ";
                    if (scope.Alias.IsValid)
                        Sink.WriteEscapedName(scope.Alias).Write(':');
                    Sink.WriteType(scope.Type);
                }
                Sink.Write(']');
            }

            Sink.WriteLine("Range: {0}", fma.TextRange);
            RexlTreeDumper.Print(Sink, fma.ParseTree, "", fma.Text, WriteNote);
            Sink.WriteLine();
        }

        if (showVerboseBoundTree)
            Sink.TWriteLine("=== Verbose Bound tree:").TWriteLine("  Type: {0}", bfma.BoundTree.Type).TWriteLine("  {0}", bfma.BoundTree).WriteLine();

        if (showBoundTree)
        {
            Sink.TWriteLine("=== Bound tree:").TWriteLine("  Type: {0}", bfma.BoundTree.Type)
                .TWriteLine("  {0}", BndNodePrinter.Run(bfma.BoundTree, BndNodePrinter.Verbosity.Terse)).WriteLine();
        }

        if (bfma.HasDiagnostics)
            HandleBindIssues(bfma.Diagnostics);
    }

    /// <summary>
    /// Called to signal an error from processing a statement, similar to a binding error.
    /// </summary>
    protected virtual void StmtError(RexlNode node, StringId msg)
    {
        Validation.AssertValue(node);
        Validation.Assert(msg.IsValid);
        var err = RexlDiagnostic.Error(node, msg);
        HandleStmtIssue(err, node);
    }

    /// <summary>
    /// Called to signal an error from processing a statement, similar to a binding error.
    /// </summary>
    protected virtual void StmtError(RexlNode node, StringId msg, params object[] args)
    {
        Validation.AssertValue(node);
        Validation.Assert(msg.IsValid);
        Validation.AssertValue(args);
        var err = RexlDiagnostic.Error(node, msg, args);
        HandleStmtIssue(err, node);
    }

    /// <summary>
    /// Called to signal an error from processing a statement, similar to a binding error.
    /// </summary>
    protected virtual void StmtError(Token tok, StringId msg)
    {
        Validation.AssertValue(tok);
        Validation.Assert(msg.IsValid);
        var err = RexlDiagnostic.Error(tok, msg);
        HandleStmtIssue(err);
    }

    /// <summary>
    /// Called to signal an error from processing a statement, similar to a binding error.
    /// </summary>
    protected virtual void StmtError(Token tok, RexlNode node, StringId msg)
    {
        Validation.AssertValueOrNull(tok);
        Validation.AssertValue(node);
        Validation.Assert(msg.IsValid);
        var err = RexlDiagnostic.Error(tok, node, msg);
        HandleStmtIssue(err);
    }

    /// <summary>
    /// Called to signal an error from processing a statement, similar to a binding error.
    /// </summary>
    protected virtual void StmtError(Token tok, RexlNode node, StringId msg, params object[] args)
    {
        Validation.AssertValueOrNull(tok);
        Validation.AssertValue(node);
        Validation.Assert(msg.IsValid);
        Validation.AssertValue(args);
        var err = RexlDiagnostic.Error(tok, node, msg, args);
        HandleStmtIssue(err);
    }
}

/// <summary>
/// Wraps a <see cref="BaseDiagnostic"/> as an <see cref="ApplicationException"/>.
/// </summary>
public sealed class RexlErrorException : ApplicationException
{
    /// <summary>
    /// The underlying <see cref="BaseDiagnostic"/>.
    /// </summary>
    public BaseDiagnostic Error { get; }

    public RexlErrorException(BaseDiagnostic error)
    {
        Validation.BugCheckValue(error, nameof(error));
        Error = error;
    }

    // REVIEW: Include location information in the message?
    public override string Message => Error.GetMessage();
}
