// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Symbolic;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Solve;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public sealed class ModuleTests : BlockTestsBase<bool>
{
    private readonly OperationRegistry _opers;
    private readonly GeneratorRegistry _gens;

    protected override OperationRegistry Operations => _opers;
    protected override GeneratorRegistry Generators => _gens;

    public ModuleTests()
        : base()
    {
        _opers = new AggregateOperationRegistry(TestFunctions.Instance, SolverFunctions.Instance);
        _gens = new AggregateGeneratorRegistry(TestGenerators.Instance, SolverGenerators.Instance);
    }

    private Task ProcessFileReduce(string pathHead, string pathTail, string text, bool options)
    {
        return ProcessFileCoreAsync(pathHead, pathTail, text, withIL: false, options: true);
    }

    protected override ValueWriterConfig CreateValueWriterConfig(bool withReduce)
    {
        return new ValueWriterConfig(showDoms: withReduce);
    }

    protected override bool TryHandleValue(EvalSink sink, CodeGeneratorBase codeGen, bool withReduce,
        DType type, BoundNode bnd, object value, ExecCtx ctx)
    {
        if (!withReduce || value is not RuntimeModule rmod)
            return false;
        sink.WriteLine("*** Reduction ***");

        var symMap = new SymbolMap(codeGen, rmod, new ReducerHost(sink));
        var symbols = rmod.Bnd.Symbols;
        var bsrItems = BndScopeRefNode.Create(rmod.Bnd.ScopeItems);
        for (int i = 0; i < symbols.Length; i++)
        {
            var sym = symbols[i];
            SymbolMap.SymbolEntry entry = default;
            if (sym.IsVariableSym && !symMap.TryAddSymbol(sym, out entry, out var bad))
            {
                if (bad.value is null && bad.bnd is not null && bad.bnd.Type.IsSequence ||
                    bad.value is Array arr && arr.Length == 0)
                {
                    sink.WriteLine("Domain sequence for '{0}' is empty: {1}", sym.Name, bad.bnd);
                }
                else
                    sink.WriteLine("Adding module symbol '{0}' failed", sym.Name);
            }

            BoundNode src;
            BoundNode dst;
            if (entry is SymbolMap.ComputedVarEntry cve)
            {
                src = rmod.Bnd.Items[cve.Symbol.IfmaValue];
                dst = cve.Node;
            }
            else
            {
                src = BndGetSlotNode.Create(sym.IfmaValue, bsrItems);
                dst = SymbolReducer.Simplify(symMap, src, expandSelect: false);
            }
            BoundNode fin = SymbolReducer.Simplify(symMap, dst, expandSelect: true);

            sink.WriteLine("  {0} src: {1}", sym.Name, src);
            sink.WriteLine("  {0} dst: {1}", sym.Name, dst);
            if (fin != dst)
                sink.WriteLine("  {0} fin: {1}", sym.Name, fin);
            sink.WriteLine();
        }
        return true;
    }

    private sealed class ReducerHost : ReducerHostBase
    {
        private readonly EvalSink _sink;

        public ReducerHost(EvalSink sink)
            : base()
        {
            _sink = sink;
        }

        public override void Warn(BoundNode bnd, StringId msg)
        {
            _sink.WriteLine("Reduce warning: {0}", msg.GetString());
        }
    }

    [TestMethod]
    public async Task ModuleReduceBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileReduce, @"Module/Reduce").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ModuleReduceWipBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileReduce, @"Module/ReduceWip").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ModuleOptimizeBaselineTests()
    {
        await DoBaselineTestsAsync(ProcessFileNoIL, @"Module/Optimize").ConfigureAwait(false);
    }
}
