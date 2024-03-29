// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Gurobi;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Symbolic;

namespace Microsoft.Rexl.Solve;

public sealed partial class MipSolver
{
    private bool TryRunGurobi(bool isMax, out double score, out List<(DName name, object value)> symValues)
    {
        GRBEnv env = null;
        try
        {
            try
            {
                env = new GRBEnv(true);

                // REVIEW: Should we log? If so, how/where?
                env.Set("LogFile", "mip1.log");

                env.Start();
            }
            catch (Exception ex)
            {
                _sink.PostDiagnostic(DiagSource.Solver,
                    MessageDiag.Error(ex, ErrorStrings.ErrSolverStartingSolverFailed_Name, "Gurobi"));
                score = double.NaN;
                symValues = null;
                return false;
            }

            // Create empty model
            using var model = new GRBModel(env);

            static string GetName(int id)
            {
                return string.Format("v{0}", id);
            }

            // Map variables and bounds to Gurobi.
            int cvar = _symMap.VarCount;
            var vars = new Dictionary<int, GRBVar>(cvar);

            void AddVarInfo(int id, bool isInt, double min, double max)
            {
                var varKind = GRB.CONTINUOUS;
                if (isInt)
                {
                    if ((min == 0 || min == 1) && (max == 1 || max == 0))
                        varKind = GRB.BINARY;
                    else
                        varKind = GRB.INTEGER;
                }
                vars.Add(id, model.AddVar(min, max, 0, varKind, GetName(id)));
            }

            void AddOneOfConstraint(DName name, IdRng rng, bool optional)
            {
                AssertRng(cvar, rng);

                // Add the var info and the constraint (when needed).
                var terms = new GRBLinExpr();
                int idMin = rng.Min;
                int idLim = rng.Lim;
                for (int id = idMin; id < idLim; id++)
                {
                    vars.TryGetValue(id, out var v);
                    terms.AddTerm(1.0, v);
                }
                model.AddConstr(terms == 1.0, name.Value);
            }

            if (!TryMapSymbolsToSolverVariables(AddVarInfo, AddOneOfConstraint))
            {
                score = double.NaN;
                symValues = null;
                return false;
            }
            Validation.Assert(vars.Count == cvar);

            // Map constraints to Gurobi.
            if (_lcons != null)
            {
                int index = 0;
                foreach (var lcon in _lcons)
                {
                    var name = string.Format("lcon{0}", index++);

                    var terms = new GRBLinExpr();
                    foreach (var term in lcon.Lin.Terms)
                        terms.AddTerm(term.Coef, vars[term.Vid]);

                    if (lcon.Hi == double.PositiveInfinity)
                        model.AddConstr(terms >= lcon.Lo, name);
                    else if (lcon.Lo == double.NegativeInfinity)
                        model.AddConstr(terms <= lcon.Hi, name);
                    else if (lcon.Lo == lcon.Hi)
                        model.AddConstr(terms == lcon.Lo, name);
                    else
                    {
                        // Both upper and lower bounds.
                        var v = model.AddVar(lcon.Lo, lcon.Hi, 0, GRB.CONTINUOUS, "v_" + name);
                        model.AddConstr(terms == v, name);
                    }
                }
            }

            // Map the measure to Gurobi.
            {
                var terms = new GRBLinExpr();
                foreach (var term in _linMsr.Terms)
                    terms.AddTerm(term.Coef, vars[term.Vid]);
                model.SetObjective(terms, isMax ? GRB.MAXIMIZE : GRB.MINIMIZE);
            }

            // Run Gurobi.
            model.Optimize();

            if (model.Status != GRB.Status.OPTIMAL)
            {
                score = double.NaN;
                symValues = null;
                return false;
            }

            // Copy values into an array.
            // REVIEW: Is there a better way to do this? And is this correct? Will all slots be hit?
            var values = new double[cvar];
            for (int id = 0; id < cvar; id++)
                values[id] = vars[id].X;

            // Map back from Gurobi.
            score = model.ObjVal;
            TryMapValuesToSymbols(values, out symValues);
            return true;
        }
        finally
        {
            env?.Dispose();
        }
    }
}
