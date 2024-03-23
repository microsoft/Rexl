// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Harness;

namespace Microsoft.Rexl.RexlRun;

using StopWatch = System.Diagnostics.Stopwatch;

internal sealed class Program
{
    private enum CodeGenKind
    {
        Caching,
        Enumerable,
    }

    public static Task Main(string[] args)
    {
        CodeGenKind cgk = CodeGenKind.Caching;
        var argsNew = new List<string>();
        foreach (string arg in args)
        {
            switch (arg)
            {
            case "-c":
            case "+c":
            case "/c":
            case "-C":
            case "+C":
            case "/C":
                cgk = CodeGenKind.Caching;
                break;
            case "-e":
            case "+e":
            case "/e":
                cgk = CodeGenKind.Enumerable;
                break;
            default:
                argsNew.Add(arg);
                break;
            }
        }

        var gens = new RexlRunGenerators();
        CodeGeneratorBase codeGen;
        switch (cgk)
        {
        default:
        case CodeGenKind.Caching:
            codeGen = new CachingEnumerableCodeGenerator(new StdEnumerableTypeManager(), gens);
            break;

        case CodeGenKind.Enumerable:
            codeGen = new EnumerableCodeGenerator(new StdEnumerableTypeManager(), gens);
            break;
        }

        // REVIEW: Perhaps add some command line options for the config settings.
        var config = new HarnessConfig(showTime: true);

        var prog = new Program(config, codeGen);
        return prog.ProcessFiles(argsNew.ToArray());
    }

    private readonly SinkImpl _sink;
    private readonly SimpleHarness _harness;

    private Program(IHarnessConfig config, CodeGeneratorBase codeGen)
    {
        _sink = new SinkImpl(codeGen.TypeManager);
        _harness = new SimpleHarness(config, new RexlRunOperations(), codeGen);
    }

    private sealed class SinkImpl : FlushEvalSink
    {
        private readonly ValueWriterConfig _config;
        private readonly StdValueWriter _valueWriter;

        public SinkImpl(TypeManager tm)
            : base()
        {
            Validation.AssertValue(tm);

            _config = new ValueWriterConfig();
            _valueWriter = new StdValueWriter(_config, this, tm);
        }

        protected override void Dump(StringBuilder sb)
        {
            Validation.AssertValue(sb);
            Console.Write(sb.ToString());
        }

        protected override void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode? nodeCtx = null)
        {
            Validation.AssertValue(diag);
            WriteDiag(src, diag);
        }

        protected override void PostValueCore(DType type, object value)
        {
            Validation.Assert(type.IsValid);
            WriteValue(type, value);
        }

        protected override void WriteValueCore(DType type, object? value, int max)
        {
            Validation.Assert(type.IsValid);
            _config.Max = max;
            _valueWriter.WriteValue(type, value);
        }
    }

    private async Task ProcessFiles(string[] paths)
    {
        var swElap = StopWatch.StartNew();

        var timeExec = _harness.ExecutionTime;

        foreach (string path in paths)
        {
            try
            {
                if (!await ProcessFileAsync(path).ConfigureAwait(false))
                    break;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("*** Exception! ***");
                for (var exCur = ex; exCur != null; exCur = exCur.InnerException)
                {
                    Console.WriteLine();
                    Console.WriteLine(exCur.GetType().ToString());
                    Console.WriteLine(exCur.Message);
                    Console.WriteLine(exCur.StackTrace);
                }
                break;
            }
        }

        timeExec = _harness.ExecutionTime - timeExec;

        Validation.Assert(swElap.IsRunning);
        swElap.Stop();

        string dtExec = (timeExec.Ticks / 10).ToString("N0");
        string dtElap = (swElap.ElapsedTicks / 10).ToString("N0");
        int cch = dtElap.Length - dtExec.Length;
        if (cch > 0)
            dtExec = new string(' ', cch) + dtExec;

        _sink
            .TWriteLine("Total execute time (μs): {0}", dtExec)
            .TWriteLine("Total elapsed time (μs): {0}", dtElap)
            .TWriteLine().Flush();

        await _harness.CleanupAsync().ConfigureAwait(false);
    }

    private async Task<bool> ProcessFileAsync(string path)
    {
        Validation.CheckValue(path, nameof(path));

        string script = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var link = Link.CreateGeneric(path);
        var src = SourceContext.Create(link, path, script);
        var (ret, state) = await _harness.RunAsync(_sink, src, resetBefore: false).ConfigureAwait(false);
        state?.Dispose();
        return ret;
    }
}

/// <summary>
/// A simple harness implementation.
/// </summary>
public sealed class SimpleHarness : SimpleHarnessWithSinkStack
{
    public SimpleHarness(IHarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen,
            Storage storage = null)
        : base(config, opers, codeGen, storage)
    {
    }

#if WITH_SOLVE
    protected override bool TryOptimizeMip(bool isMax, RuntimeModule modSrc, int imsr, DName solver,
        out double score, out List<(DName name, object value)> symValues)
    {
        Validation.AssertValue(modSrc);
        Validation.AssertIndex(imsr, modSrc.Bnd.Symbols.Length);
        Validation.Assert(modSrc.Bnd.Symbols[imsr].IsMeasureSym);

        return Solve.MipSolver.TryOptimize(Sink, _codeGen, isMax, modSrc, imsr, solver, out score, out symValues);
    }
#endif
}
