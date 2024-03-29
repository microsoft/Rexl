// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Highs;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Symbolic;

namespace Microsoft.Rexl.Solve;

// This partial handles solving using HiGHS.
partial class MipSolver
{
    private bool TryRunHighs(bool isMax, out double score, out List<(DName name, object value)> symValues)
    {
        // Map constraints to HiGHS.
        int crow = Util.Size(_lcons);
        var rowlower = new double[crow];
        var rowupper = new double[crow];
        var astart = new int[crow + 1];
        var aindexBldr = new List<int>();
        var avalueBldr = new List<double>();
        for (int irow = 0; irow < crow; irow++)
        {
            var lcon = _lcons[irow];
            rowlower[irow] = lcon.Lo;
            rowupper[irow] = lcon.Hi;
            Validation.Assert(aindexBldr.Count == avalueBldr.Count);
            astart[irow] = avalueBldr.Count;
            foreach (var term in lcon.Lin.Terms)
            {
                aindexBldr.Add(term.Vid);
                avalueBldr.Add(term.Coef);
            }
        }

        // Map bounds to HiGHS.
        int cvar = _symMap.VarCount;
        var collower = new double[cvar];
        var colupper = new double[cvar];
        Array.Fill(collower, double.NegativeInfinity);
        Array.Fill(colupper, double.PositiveInfinity);
        var highs_integrality = new int[cvar];
        bool isMip = false;

        void AddVarInfo(int id, bool isInt, double min, double max)
        {
            Validation.AssertIndex(id, cvar);
            collower[id] = min;
            colupper[id] = max;
            isMip |= isInt;
            highs_integrality[id] = isInt.ToNum();
        }

        List<(IdRng rng, bool optional)> oneOfs = null;
        int cvalOneOfs = 0;
        void AddOneOfConstraint(DName name, IdRng rng, bool optional)
        {
            Validation.Assert(isMip);
            oneOfs ??= new();
            oneOfs.Add((rng, optional));
            cvalOneOfs += rng.Count;
        }

        if (!TryMapSymbolsToSolverVariables(AddVarInfo, AddOneOfConstraint))
        {
            score = double.NaN;
            symValues = null;
            return false;
        }

        if (oneOfs != null)
        {
            // Add the additional "one of" constraints.
            int num = oneOfs.Count;
            Validation.Assert(num > 0);
            Array.Resize(ref rowlower, crow + num);
            Array.Resize(ref rowupper, crow + num);
            Array.Resize(ref astart, crow + num + 1);

            foreach (var (rng, optional) in oneOfs)
            {
                AssertRng(cvar, rng);
                Validation.AssertIndex(crow, rowlower.Length);
                rowlower[crow] = optional ? 0.0 : 1.0;
                rowupper[crow] = 1.0;
                Validation.Assert(aindexBldr.Count == avalueBldr.Count);
                astart[crow] = avalueBldr.Count;
                for (int id = rng.Min; id < rng.Lim; id++)
                {
                    Validation.Assert(highs_integrality[id] != 0);
                    Validation.Assert(collower[id] == 0.0 | collower[id] == 1.0);
                    Validation.Assert(colupper[id] == 0.0 | colupper[id] == 1.0);
                    aindexBldr.Add(id);
                    avalueBldr.Add(1.0);
                }
                crow++;
            }
        }
        Validation.Assert(crow == rowlower.Length);
        Validation.Assert(aindexBldr.Count == avalueBldr.Count);
        astart[crow] = avalueBldr.Count;

        var aindex = aindexBldr.ToArray();
        var avalue = avalueBldr.ToArray();

        // Map the measure to HiGHS.
        var colcost = new double[cvar];
        double offset = 0;
        foreach (var term in _linMsr.Terms)
        {
            // Note that the measure is currently a single term consisting of the measure symbol
            // so the offset assignment isn't currently hittable.
            if (term.Vid >= 0)
                colcost[term.Vid] = term.Coef;
            else
                offset = term.Coef;
        }

        var sense = isMax ? HighsObjectiveSense.kMaximize : HighsObjectiveSense.kMinimize;
        var a_format = HighsMatrixFormat.kRowwise;
        var model = new HighsModel(
            colcost, collower, colupper, rowlower, rowupper,
            astart, aindex, avalue, highs_integrality, offset, a_format, sense);

        using var solver = new HighsLpSolver();

        // REVIEW: What options should be set by default?
        HighsStatus status = isMip ? solver.passMip(model) : solver.passLp(model);
        //solver.setStringOptionValue("solver", "simplex");
        //solver.setStringOptionValue("presolve", "on");
        //solver.setIntOptionValue("mip_min_cliquetable_entries_for_parallelism", 1000);
        //solver.setIntOptionValue("threads", 8);
        //solver.setStringOptionValue("parallel", "on");
        //solver.setIntOptionValue("mip_report_level", 2);
        //solver.setDoubleOptionValue("mip_abs_gap", 1e-8);
        //solver.setDoubleOptionValue("mip_rel_gap", 1e-8);
        //solver.setDoubleOptionValue("mip_abs_gap", 1e-6);
        //solver.setDoubleOptionValue("mip_feasibility_tolerance", 1e-6);
        //solver.setDoubleOptionValue("mip_heuristic_effort", 0.65);
        //solver.setIntOptionValue("mip_detect_symmetry", 1);
        //solver.setIntOptionValue("mip_lp_age_limit", 10);
        //solver.setIntOptionValue("mip_pscost_minreliable", 4);
        //solver.setIntOptionValue("mip_pool_age_limit", 30);
        //solver.setIntOptionValue("mip_max_improving_sols", int.MaxValue);
        //solver.setIntOptionValue("mip_max_improving_sols", 30);
        //solver.setIntOptionValue("mip_detect_symmetry", 0);
        //solver.setIntOptionValue("mip_detect_symmetry", 0);

        // REVIEW: Handle the various status values.
        status = solver.run();
        switch (status)
        {
        default:
            break;
        case HighsStatus.kOk:
            break;
        case HighsStatus.kWarning:
            break;
        case HighsStatus.kError:
            score = double.NaN;
            symValues = null;
            return false;
        }

        var ms = solver.GetModelStatus();
        switch (ms)
        {
        case HighsModelStatus.kOptimal:
            break;

        case HighsModelStatus.kInfeasible:
            _sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverInfeasible));
            goto default;
        case HighsModelStatus.kUnbounded:
            _sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverUnbounded));
            goto default;
        case HighsModelStatus.kUnboundedOrInfeasible:
            _sink.PostDiagnostic(DiagSource.Solver, MessageDiag.Error(ErrorStrings.ErrSolverBadConstraints));
            goto default;

        case HighsModelStatus.kLoadError:
        case HighsModelStatus.kModelError:
        case HighsModelStatus.kPresolveError:
        case HighsModelStatus.kSolveError:
        case HighsModelStatus.kPostsolveError:
        case HighsModelStatus.kModelEmpty:
        case HighsModelStatus.kObjectiveBound:
        case HighsModelStatus.kObjectiveTarget:
        case HighsModelStatus.kTimeLimit:
        case HighsModelStatus.kIterationLimit:
        case HighsModelStatus.kUnknown:
        case HighsModelStatus.kSolutionLimit:
        default:
            score = double.NaN;
            symValues = null;
            return false;
        }

        HighsSolution sln = solver.getSolution();

        if (sln.colvalue == null)
        {
            score = double.NaN;
            symValues = null;
            return false;
        }

        // Map back from HiGHS.
        score = solver.getObjectiveValue();
        TryMapValuesToSymbols(sln.colvalue, out symValues);
        return true;
    }
}
