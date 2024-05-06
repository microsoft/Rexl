// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Symbolic;

namespace Microsoft.Rexl.Solve;

/// <summary>
/// Encapsulate solving using a mip (mixed integer program) solver.
/// </summary>
public sealed partial class MipSolver
{
    // Filled in by the ctor.
    private readonly EvalSink _sink;
    private readonly RuntimeModule _module;
    private readonly ModComputedVar _msr;

    // Allocated in ctor, but filled later.
    private readonly SymbolMap _symMap;
    private readonly List<(ModComputedVar sym, BoundNode eval)> _constraints;
    private bool _hasError;

    private MipSolver(EvalSink sink, CodeGeneratorBase codeGen, RuntimeModule module, ModComputedVar msr)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(codeGen);
        Validation.AssertValue(module);
        Validation.AssertValue(msr);
        Validation.AssertIndex(msr.Index, module.Bnd.Symbols.Length);
        Validation.Assert(module.Bnd.Symbols[msr.Index] == msr);
        Validation.Assert(msr.SymKind == ModSymKind.Measure);

        _sink = sink;
        _module = module;
        _msr = msr;

        // REVIEW: reduction host?
        _symMap = new SymbolMap(codeGen, _module, null);
        _constraints = new List<(ModComputedVar sym, BoundNode eval)>();
    }

    /// <summary>
    /// Try to optimize the indicated measure in the given module. If successful, sets <paramref name="moduleRes"/>
    /// to the resulting specialized module.
    /// </summary>
    public static bool TryOptimize(EvalSink output, CodeGeneratorBase codeGen, bool isMax,
        RuntimeModule module, int isymMsr, DName solver,
        out double score, out List<(DName name, object value)> symValues)
    {
        Validation.BugCheckValue(output, nameof(output));
        Validation.BugCheckValue(codeGen, nameof(codeGen));
        Validation.BugCheckValue(module, nameof(module));
        Validation.BugCheckIndex(isymMsr, module.Bnd.Symbols.Length, nameof(isymMsr));

        var msr = module.Bnd.Symbols[isymMsr] as ModComputedVar;
        Validation.BugCheckParam(msr is not null, nameof(isymMsr));
        Validation.BugCheckParam(msr.SymKind == ModSymKind.Measure, nameof(isymMsr));

        var impl = new MipSolver(output, codeGen, module, msr);

        return impl.TryOptimizeCore(isMax, solver.ValueOrNull ?? "highs", out score, out symValues);
    }

    private bool TryOptimizeCore(bool isMax, string solver,
        out double score, out List<(DName name, object value)> symValues)
    {
        // Determine the variables (free and computed) that are needed.
        var needed = DetermineNeededVars();

        // Build up the additional information.
        ProcessSymbols(needed);

        if (!_hasError)
        {
            // Map to a linear model.
            MapToLinear();
        }

        // If there are errors, bail out.
        if (_hasError)
        {
            score = double.NaN;
            symValues = null;
            return false;
        }

        bool success;
        switch (solver?.ToLowerInvariant())
        {
        case "glpk":
            _sink.WriteLine("Solver: GLPK");
            success = TryRunGlpk(isMax, out score, out symValues);
            break;
        case "highs":
            _sink.WriteLine("Solver: HiGHS");
            success = TryRunHighs(isMax, out score, out symValues);
            break;
        case "gurobi":
            _sink.WriteLine("Solver: Gurobi");
            success = TryRunGurobi(isMax, out score, out symValues);
            break;
        default:
            _sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverUnkown_Name, solver));
            score = double.NaN;
            symValues = null;
            return false;
        }

        if (!success)
        {
            _sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverSolvingFailed));
            return false;
        }

        return true;
    }

    /// <summary>
    /// From the given measure and the constraints in the module, determine the variables (free or computed)
    /// that are needed.
    /// </summary>
    private bool[] DetermineNeededVars()
    {
        int num = _module.Bnd.Symbols.Length;
        var needed = new bool[num];

        // The measure is needed.
        Validation.Assert(_msr.IsComputedVarSym);
        needed[_msr.Index] = true;

        // Mark all constraints as needed.
        for (int i = 0; i < num; i++)
        {
            var sym = _module.Bnd.Symbols[i];
            if (sym.SymKind != ModSymKind.Constraint)
                continue;
            Validation.Assert(sym.IsComputedVarSym);
            needed[sym.Index] = true;
        }

        // Need transitive closure.
        var deps = _module.Bnd.GetVarDependencies();
        Validation.Assert(deps.Length == num);

        for (int i = needed.Length; --i >= 0;)
        {
            if (!needed[i])
                continue;

            Validation.Assert(_module.Bnd.Symbols[i].IsVariableSym);
            var dep = deps[i];
            Validation.Assert(!dep.TestAtOrAbove(i));
            if (dep.IsEmpty)
                continue;

            Validation.Assert(_module.Bnd.Symbols[i].IsComputedVarSym);
            foreach (var ibit in dep)
            {
                Validation.AssertIndex(ibit, i);
                Validation.Assert(_module.Bnd.Symbols[ibit].IsVariableSym);
                needed[ibit] = true;
            }
        }

        return needed;
    }

    private void ProcessSymbols(bool[] needed)
    {
        Validation.Assert(needed.Length == _module.Bnd.Symbols.Length);

        for (int i = 0; i < needed.Length; i++)
        {
            var sym = _module.Bnd.Symbols[i];
            Validation.Assert(!_symMap.HasSymbol(sym.Name));

            if (!needed[i])
                continue;

            Validation.Assert(sym.IsVariableSym);

            if (!_symMap.TryAddSymbol(sym, out var entry, out var bad))
            {
                _hasError = true;
                MessageDiag diag;
                if (bad.value is null && bad.bnd is not null && bad.bnd.Type.IsSequence ||
                    bad.value is Array arr && arr.Length == 0)
                {
                    diag = MessageDiag.Error(ErrorStrings.ErrSolverDomSeqEmpty_Name_Bnd, sym.Name, bad.bnd);
                }
                else
                    diag = MessageDiag.Error(ErrorStrings.ErrSolverAddSymbolFailed_Name, sym.Name);
                _sink.PostDiagnostic(DiagSource.Solver, diag);
                continue;
            }

            // Record constraints.
            if (sym is ModComputedVar mcv)
            {
                if (entry is SymbolMap.ComputedVarEntry cve)
                {
                    var value = cve.Node;
                    if (mcv.IsConstraintSym)
                    {
                        Validation.Assert(value.Type.RootType == DType.BitReq);
                        _constraints.Add((mcv, value));
                    }
                    else if (value.Type.IsEquatable)
                    {
                        // Add a constraint for the formula. REVIEW: Is this the best way?
                        var free = _symMap.EnsureVariable(cve.Symbol.Name);
                        _constraints.Add((mcv, BndCompareNode.Create(CompareOp.Equal, free, value)));
                    }
                    else
                    {
                        // Computed variable is not equatable so it's value is directly substituted when needed.
                        // That is, we won't associate any BndFreeVarNode instances with it.
                    }
                }
            }
        }
    }
}
