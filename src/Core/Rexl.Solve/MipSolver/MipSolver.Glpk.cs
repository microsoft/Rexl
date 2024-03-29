// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Solve.Glpk;
using Microsoft.Rexl.Symbolic;

namespace Microsoft.Rexl.Solve;

// This partial handles solving using GLPK and contains shared helper methods.
// REVIEW: Split out the shared functionality from the GLPK specific stuff.
partial class MipSolver
{
    private bool TryRunGlpk(bool isMax, out double score, out List<(DName name, object value)> symValues)
    {
        // Map constraints to GLPK.
        var cons = new List<GlpkConstraint>();
        if (_lcons != null)
        {
            foreach (var lcon in _lcons)
            {
                var terms = new List<GlpkTerm>();
                foreach (var term in lcon.Lin.Terms)
                    terms.Add(new GlpkTerm(term.Vid, term.Coef));
                cons.Add(new GlpkConstraint(lcon.Lo, lcon.Hi, terms));
            }
        }

        // Map variables and bounds to GLPK.
        int cvar = _symMap.VarCount;
        var vins = new List<GlpkVariableInfo>(cvar);

        void AddVarInfo(int id, bool isInt, double min, double max)
        {
            vins.Add(new GlpkVariableInfo(id, isInt, min, max));
        }

        void AddOneOfConstraint(DName name, IdRng rng, bool optional)
        {
            AssertRng(cvar, rng);
            var terms = new List<GlpkTerm>(rng.Count);
            int idMin = rng.Min;
            int idLim = rng.Lim;
            for (int id = idMin; id < idLim; id++)
                terms.Add(new GlpkTerm(id, 1));
            cons.Add(new GlpkConstraint(optional ? 0.0 : 1.0, 1.0, terms));
        }

        if (!TryMapSymbolsToSolverVariables(AddVarInfo, AddOneOfConstraint))
        {
            score = double.NaN;
            symValues = null;
            return false;
        }

        // Map the measure to GLPK.
        var measure = new List<GlpkTerm>();
        foreach (var term in _linMsr.Terms)
            measure.Add(new GlpkTerm(term.Vid, term.Coef));

        // Run GLPK.
        if (!GlpkApi.TrySolve(cvar,
            new GlpkVariableInfo(-1, false, double.NegativeInfinity, double.PositiveInfinity),
            vins, isMax, measure, cons, out var sln))
        {
            score = double.NaN;
            symValues = null;
            return false;
        }

        // Map back from GLPK.
        score = sln.Score;
        TryMapValuesToSymbols(sln.Values, out symValues);
        return true;
    }
}
