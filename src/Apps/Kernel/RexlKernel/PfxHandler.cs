// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerFx;
using Microsoft.PowerFx.Core;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// The handler for Power Fx.
/// </summary>
public sealed partial class PfxHandler : Handler.Simple
{
    private readonly RecalcEngine _engine;
    private readonly CultureInfo _culture;
    private readonly ParserOptions _popts;

    private bool FormatTable = true;
    private const string OptionFormatTable = "FormatTable";

    private readonly SemaphoreSlim _semaphore;
    private bool _disposedHarness;

    // Execution counter.
    private long _counter;

    // Buffer to collect output.
    private readonly StringBuilder _sbOut;

    public PfxHandler(Logger logger, Publisher pub)
        : base(logger, pub, "pfx")
    {
        var config = new PowerFxConfig(Features.PowerFxV1);

        config.SymbolTable.EnableMutationFunctions();

        // REVIEW: Should Option be special syntax instead of a function?
        config.AddFunction(new OptionFunction(this));

        var OptionsSet = new OptionSet("Options", DisplayNameUtility.MakeUnique(new Dictionary<string, string>() { { OptionFormatTable, OptionFormatTable } }));

        config.AddOptionSet(OptionsSet);

        _engine = new RecalcEngine(config);
        _culture = CultureInfo.InvariantCulture;
        _popts = new ParserOptions(_culture, allowsSideEffects: true, maxExpressionLength: 1_000_000);

        _semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        _sbOut = new StringBuilder();
    }

    public override Task CleanupAsync()
    {
        if (!_disposedHarness)
        {
            try
            {
                // REVIEW: Anything to do?
            }
            finally
            {
                _disposedHarness = true;
                _semaphore.Dispose();
            }
        }
        return Task.CompletedTask;
    }

    private enum StmtKind
    {
        /// <summary>
        /// Not a statement, just an expression.
        /// </summary>
        None,

        /// <summary>
        /// Assign a value to a name.
        /// </summary>
        Set,

        /// <summary>
        /// Define a named formula.
        /// </summary>
        Formula,

        /// <summary>
        /// Import a pfx script.
        /// </summary>
        Import,

        /// <summary>
        /// Display help for the Power Fx (extended) language.
        /// </summary>
        Help,
    }

    public override async Task ExecAsync(ExecuteMessage msg, int min, int lim)
    {
        Validation.AssertValue(msg);

        var code = msg.Request.Code;
        Validation.AssertIndexInclusive(min, lim);
        Validation.AssertIndexInclusive(lim, code.Length);

        if (min >= lim)
            return;
        if (min > 0 || lim < code.Length)
            code = code[min..lim];
        if (string.IsNullOrWhiteSpace(code))
            return;

        try
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                _counter++;
                await DoCodeAsync(depth: 0, msg, code).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception e)
        {
            Validation.Assert(e != null);
            _pub.PublishError(msg, $"*** Exception: {e}");
        }
    }

    private async Task DoCodeAsync(int depth, ExecuteMessage msg, string code)
    {
        if (++depth > 10)
            throw new InvalidOperationException("Imports nested too deeply");

        // Tokenize so we can handle special forms. Note that the repl sample uses regex, which doesn't
        // properly handle quoted identifiers, among other things.
        var tokens = _engine.Tokenize(code, _culture).Where(t => t.Kind != TokKind.Whitespace).ToList();

        // Split on semicolons.
        for (int itokMin = 0, itokLim = 0; itokMin < tokens.Count; itokMin = itokLim + 1)
        {
            _sbOut.Clear();

            for (itokLim = itokMin; itokLim < tokens.Count; itokLim++)
            {
                if (tokens[itokLim].Kind == TokKind.Semicolon)
                    break;
            }

            if (itokLim <= itokMin)
                continue;

            var kind = GetStmtKind(code, tokens, itokMin, itokLim, out var name, out var expr);
            switch (kind)
            {
            case StmtKind.Set:
                await DoSetAsync(name, expr, msg.Ct).ConfigureAwait(false);
                break;
            case StmtKind.Formula:
                await DoFmaAsync(name, expr, msg.Ct).ConfigureAwait(false);
                break;
            case StmtKind.Import:
                Validation.Assert(name is null);
                await DoImportAsync(depth, msg, expr).ConfigureAwait(false);
                break;
            case StmtKind.Help:
                Validation.Assert(name is null);
                await DoHelpAsync(msg).ConfigureAwait(false);
                break;

            default:
                await DoExprAsync(code[tokens[itokMin].Span.Min..tokens[itokLim - 1].Span.Lim], msg.Ct)
                    .ConfigureAwait(false);
                break;
            }

            if (_sbOut.Length > 0)
                _pub.PublishData(msg, _sbOut.ToString());
        }
    }

    private async Task DoExprAsync(string expr, CancellationToken ct)
    {
        // Eval and print everything else
        var res = await _engine.EvalAsync(expr, ct, options: _popts).ConfigureAwait(false);

        if (res is ErrorValue errorValue)
            _sbOut.Append("Error: ").Append(errorValue.Errors[0].Message);
        else
            _sbOut.AppendLine(PrintResult(res));
    }

    private async Task DoSetAsync(string name, string expr, CancellationToken ct)
    {
        Validation.AssertNonEmpty(name);
        Validation.AssertNonEmpty(expr);

        var res = await _engine.EvalAsync(expr, ct, options: _popts).ConfigureAwait(false);
        _engine.UpdateVariable(name, res);
    }

    private Task DoFmaAsync(string name, string expr, CancellationToken ct)
    {
        Validation.AssertNonEmpty(name);
        Validation.AssertNonEmpty(expr);

        _engine.SetFormula(name, expr, OnUpdate);
        return Task.CompletedTask;
    }

    private async Task DoImportAsync(int depth, ExecuteMessage msg, string expr)
    {
        Validation.AssertValue(msg);
        Validation.AssertNonEmpty(expr);

        var res = _engine.Eval(expr, options: _popts);
        if (res is ErrorValue errorValue)
            throw new InvalidOperationException(errorValue.Errors[0].Message);
        if (res is not StringValue sv)
            throw new InvalidOperationException("Import needs a text value");

        string path = sv.Value;

        // REVIEW: Load file contents and call DoCode.
        string code = await File.ReadAllTextAsync(path, msg.Ct).ConfigureAwait(false);
        await DoCodeAsync(depth, msg, code).ConfigureAwait(false);
    }

    private StmtKind GetStmtKind(
        string code,
        List<Token> tokens, int itokMin, int itokLim,
        out string name, out string expr)
    {
        Validation.AssertValue(tokens);
        Validation.AssertIndexInclusive(itokLim, tokens.Count);
        Validation.AssertIndex(itokMin, itokLim);

        name = null;
        expr = null;

        int itok = itokMin;
        if (tokens[itok] is not IdentToken ident0)
            return StmtKind.None;
        if (++itok >= itokLim)
            return StmtKind.None;

        bool isSet = false;
        switch (ident0.Name.Value)
        {
        case "Import":
            if (tokens[itok].Kind != TokKind.ParenOpen)
                goto default;
            if (tokens[itokLim - 1].Kind != TokKind.ParenClose)
                return StmtKind.None;
            itokLim--;
            if (itok + 1 >= itokLim)
                return StmtKind.None;
            expr = code[tokens[itok + 1].Span.Min..tokens[itokLim - 1].Span.Lim];
            return StmtKind.Import;

        case "set":
        case "let":
            // REVIEW: Should we support quoted form or just standard form?
            if (ident0.Span.Lim - ident0.Span.Min != 3)
                goto default;
            if (tokens[itok] is not IdentToken ident2)
                goto default;
            if (++itok >= itokLim)
                return StmtKind.None;
            if (ident0.Name.Value == "set")
            {
                isSet = true;
                // Be forgiving if they do `set X := <expr>`.
                if (tokens[itok].Kind == TokKind.Colon)
                    itok++;
            }
            ident0 = ident2;
            break;

        case "Help":
            if (itok + 2 != itokLim)
                goto default;
            if (tokens[itok].Kind != TokKind.ParenOpen)
                goto default;
            if (tokens[itok + 1].Kind != TokKind.ParenClose)
                goto default;
            return StmtKind.Help;

        default:
            // REVIEW: The repl allows `X = <expr>` for defining a named formula. That conflicts
            // with the equality operator, so we don't do that here. We do however support `X := <expr>`
            // as short hand for "set".
            if (tokens[itok].Kind != TokKind.Colon)
                return StmtKind.None;
            itok++;
            isSet = true;
            break;
        }

        // The name is in ident0 and `isSet` indicates whether it is "set" vs "let".
        if (itok + 1 >= itokLim)
            return StmtKind.None;
        if (tokens[itok].Kind != TokKind.Equ)
            return StmtKind.None;
        name = ident0.Name.Value;
        expr = code[tokens[itok + 1].Span.Min..tokens[itokLim - 1].Span.Lim];
        return isSet ? StmtKind.Set : StmtKind.Formula;
    }

    private void OnUpdate(string name, FormulaValue newValue)
    {
        _sbOut.Append($"{name}: ");
        if (newValue is ErrorValue errorValue)
            _sbOut.Append("Error: ").AppendLine(errorValue.Errors[0].Message);
        else
        {
            if (newValue is TableValue)
                _sbOut.AppendLine();
            _sbOut.AppendLine(PrintResult(newValue));
        }
    }

    // REVIEW: Change to write to a string builder.
    private string PrintResult(object value, Boolean minimal = false)
    {
        StringBuilder sb;

        if (value is BlankValue)
            return minimal ? "" : "Blank()";
        if (value is ErrorValue errorValue)
            return minimal ? "<error>" : "<Error: " + errorValue.Errors[0].Message + ">";
        if (value is UntypedObjectValue)
            return minimal ? "<untyped>" : "<Untyped: Use Value, Text, Boolean, or other functions to establish the type>";
        if (value is StringValue sv)
            return minimal ? sv.ToObject().ToString() : "\"" + sv.ToObject().ToString().Replace("\"", "\"\"") + "\"";
        if (value is RecordValue record)
        {
            if (minimal)
                return "<record>";
            var separator = "";
            sb = new StringBuilder("{");
            foreach (var field in record.Fields)
            {
                sb.Append(separator).Append(field.Name).Append(":");
                sb.Append(PrintResult(field.Value));
                separator = ", ";
            }
            return sb.Append("}").ToString();
        }
        if (value is TableValue table)
        {
            if (minimal)
                return "<table>";

            var fieldNames = table.Type.FieldNames.ToArray();
            if (fieldNames.Length == 0)
                return "Table()";

            if (fieldNames.Length == 1 && fieldNames[0] == "Value")
            {
                var separator = "";
                sb = new StringBuilder("[");
                foreach (var row in table.Rows)
                {
                    sb.Append(separator);
                    sb.Append(PrintResult(row.Value.Fields.First().Value, false));
                    separator = ", ";
                }
                return sb.Append("]").ToString();
            }

            if (!FormatTable)
            {
                // table without formatting 
                var separator = string.Empty;
                sb = new StringBuilder("[");
                foreach (var row in table.Rows)
                {
                    sb.Append(separator);
                    sb.Append(PrintResult(row.Value));
                    separator = ", ";
                }
                return sb.Append(']').ToString();
            }

            var colMap = new Dictionary<string, int>();
            var columnWidth = new int[fieldNames.Length];
            for (int i = 0; i < fieldNames.Length; i++)
            {
                var name = fieldNames[i];
                colMap[name] = i;
                columnWidth[i] = name.Length;
            }

            const int MaxTableRows = 30;
            var maxRows = MaxTableRows;
            foreach (var row in table.Rows)
            {
                if (maxRows-- <= 0)
                    break;
                if (row.Value is null)
                    continue;
                foreach (var field in row.Value.Fields)
                {
                    if (colMap.TryGetValue(field.Name, out int column))
                    {
                        int len = PrintResult(field.Value, true).Length;
                        columnWidth[column] = Math.Max(columnWidth[column], len);
                    }
                }
            }

            sb = new StringBuilder("\n ");
            for (int i = 0; i < fieldNames.Length; i++)
            {
                var name = fieldNames[i];
                sb.Append(' ', columnWidth[i] - name.Length + 1).Append(name).Append("  ");
            }

            sb.Append("\n ");
            foreach (var width in columnWidth)
                sb.Append('=', width + 2).Append(" ");

            maxRows = MaxTableRows;
            foreach (var row in table.Rows)
            {
                if (maxRows-- <= 0)
                    break;

                sb.Append("\n ");
                if (row.Value is null)
                {
                    sb.Append(row.IsError ? row.Error?.Errors?[0].Message : "Blank()");
                    continue;
                }

                int col = 0;
                var pairs = row.Value.Fields
                    .Select(f => (f, colMap.TryGetValue(f.Name, out int c) ? c : -1))
                    .Where(p => p.Item2 >= 0)
                    .OrderBy(p => p.Item2);

                foreach (var (field, column) in pairs)
                {
                    while (col < column)
                    {
                        sb.Append(' ', columnWidth[col] + 3);
                        col++;
                    }

                    sb.Append(' ');
                    var str = PrintResult(field.Value, true);
                    int cch = columnWidth[column] - str.Length;
                    if (cch > 0)
                        sb.Append(' ', cch);

                    sb.Append(str);
                    sb.Append("  ");
                    col++;
                }
            }

            if (maxRows < 0)
                sb.Append($"\n (showing first {MaxTableRows} records)");

            return sb.ToString();
        }

        // must come last, as everything is a formula value
        if (value is FormulaValue fv)
        {
            if (fv.Type is BooleanType)
                return fv.AsBoolean() ? "true" : "false";
            return fv.ToObject().ToString();
        }

        throw new Exception("unexpected type in PrintResult");
    }

    private Task DoHelpAsync(ExecuteMessage msg)
    {
        Validation.AssertValue(msg);

        int column = 0;
        string funcList = "";

        // REVIEW: What's the proper way to get these? And why isn't Distinct listed?
        // And why is Set listed?
#pragma warning disable CS0618 // Type or member is obsolete
        var funcNames = _engine.Config.FunctionInfos.Select(x => x.Name).Distinct();
#pragma warning restore CS0618 // Type or member is obsolete

        foreach (string func in funcNames.OrderBy(s => s))
        {
            funcList += $"  {func,-14}";
            if (++column % 5 == 0)
                funcList += "\n";
        }

        _pub.PublishData(msg,
            @"
<expr> alone is evaluated and the result displayed.
    Example: 1+1 or ""Hello, World""
Set(<name>, <expr>) creates or changes a variable's value.
    Example: Set(X, 17)
set <name> = <expr> also creates or changes a variable's value.
    Example: set X = X+1
<name> := <expr> also creates or changes a variable's value.
    Example: X := true
let <name> = <expr> defines a named formula with automatic recalc.
    Example: let F = m * a

Available functions (case sensitive):
" + funcList + @"

Operators: = <> <= >= + - * / % && And || Or ! Not in exactin 

Record syntax is { <name>: <expr>, ... }.
    Example: { Name: ""Joe"", Age: 29, Company: DefaultCompany }
Use the Table function for a list of records.
    Example: Table({ Name: ""Joe"" }, { Name: ""Sally"" })
Use [ <value>, ... ] for a single column table, field name is ""Value"".
    Example: [ 1, 2, 3 ]
Records and Tables can be arbitrarily nested.

Use Option(Options.FormatTable, false) to disable table formatting.

WARNING: Once a named formula is defined or a variable's type is defined, it cannot be changed.
"
        );

        return Task.CompletedTask;
    }

    private class OptionFunction : ReflectionFunction
    {
        private readonly PfxHandler _parent;

        // explicit constructor needed so that the return type from Execute can be FormulaValue and acoomodate both booleans and errors.
        public OptionFunction(PfxHandler parent)
            : base("Option", FormulaType.Boolean, new[] { FormulaType.String, FormulaType.Boolean })
        {
            Validation.AssertValue(parent);
            _parent = parent;
        }

        public FormulaValue Execute(StringValue option, BooleanValue value)
        {
            if (option.Value.ToLowerInvariant() == OptionFormatTable.ToLowerInvariant())
            {
                _parent.FormatTable = value.Value;
                return value;
            }
            else
            {
                return FormulaValue.NewError(new ExpressionError()
                {
                    Kind = ErrorKind.InvalidArgument,
                    Severity = ErrorSeverity.Critical,
                    Message = $"Invalid option name: {option.Value}."
                }
                );
            }
        }
    }
}
