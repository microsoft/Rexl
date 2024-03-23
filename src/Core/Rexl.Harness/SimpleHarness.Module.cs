// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Harness;

// This partial handles module related functionality.
partial class SimpleHarnessBase
{
    /// <summary>
    /// Optimize the given measure in the given module.
    /// REVIEW: This is a temporary hack. Optimization should really use a task.
    /// </summary>
    protected virtual RuntimeModule Optimize(int id, RuntimeModule modSrc, DName measure, bool isMax, DName solver)
    {
        if (modSrc is null)
            return null;

        modSrc.Bnd.NameToIndex.TryGetValue(measure, out int imsr).Verify();
        Validation.Assert(modSrc.Bnd.Symbols[imsr].IsMeasureSym);

        if (!TryOptimizeMip(isMax, modSrc, imsr, solver, out var score, out var symValues))
            return null;

        // REVIEW: Is there a good way that doesn't use reflection?
        var typeRec = modSrc.Bnd.TypeRec;
        var fact = _codeGen.TypeManager.CreateRecordFactory(typeRec);
        var bldr = fact.Create().Open(partial: true);
        var names = new HashSet<DName>();
        foreach (var pair in symValues)
        {
            names.Add(pair.name).Verify();
            if (pair.value is null)
                continue;
            var setter = fact.GetFieldSetter(pair.name, out _, out var stFld);
            Validation.Assert(stFld.IsAssignableFrom(pair.value.GetType()));
            setter(bldr, pair.value);
        }
        var rec = bldr.Close();

        var modDst = modSrc.UpdateRaw(rec, names);
        return modDst;
    }

    protected virtual bool TryOptimizeMip(bool isMax, RuntimeModule modSrc, int imsr, DName solver,
        out double score, out List<(DName name, object value)> symValues)
    {
        Validation.AssertValue(modSrc);
        Validation.AssertIndex(imsr, modSrc.Bnd.Symbols.Length);
        Validation.Assert(modSrc.Bnd.Symbols[imsr].IsMeasureSym);

        var strSolver = solver.IsValid ? solver.Value : "<default>";
        Sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverUnkown_Name, strSolver));
        score = double.NaN;
        symValues = null;
        return false;
    }
}
