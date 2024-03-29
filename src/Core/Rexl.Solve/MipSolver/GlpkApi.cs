// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Solve.Glpk;

using Conditional = System.Diagnostics.ConditionalAttribute;
using Native = GlpkNativeApi.GlpkNative;

/// <summary>
/// Represents a solution to a GLPK optimization problem. If the problem couldn't be solved,
/// the <see cref="Values"/> array will be null.
/// </summary>
internal struct GlpkSolution
{
    /// <summary>
    /// The objective value.
    /// </summary>
    public readonly double Score;

    /// <summary>
    /// The variable values.
    /// </summary>
    public readonly double[] Values;

    public GlpkSolution(double score, double[] values)
    {
        Score = score;
        Values = values;
    }
}

/// <summary>
/// A term in a GLPK constraint or objective.
/// </summary>
internal struct GlpkTerm
{
    /// <summary>
    /// The variable id, with -1 meaning a constant term.
    /// </summary>
    public readonly int Vid;

    /// <summary>
    /// The coefficient.
    /// </summary>
    public readonly double Coef;

    public GlpkTerm(int v, double c)
    {
        Vid = v;
        Coef = c;
    }
}

/// <summary>
/// Information about a GLPK variable.
/// </summary>
internal struct GlpkVariableInfo
{
    /// <summary>
    /// The variable index.
    /// </summary>
    public readonly int Vid;

    /// <summary>
    /// Whether the variable is restricted to being integer valued.
    /// </summary>
    public readonly bool IsInt;

    /// <summary>
    /// The lower bound.
    /// </summary>
    public readonly double Min;

    /// <summary>
    /// The upper bound.
    /// </summary>
    public readonly double Max;

    public GlpkVariableInfo(int vid, bool integer, double min, double max)
    {
        Vid = vid;
        IsInt = integer;
        Min = min;
        Max = max;
    }
}

/// <summary>
/// Represents a GLPK constraint.
/// </summary>
internal struct GlpkConstraint
{
    // Note that this is intended to be immutable, but we trust the caller not
    // to be modifying the list while we're active, and we won't modify it either.
    // It's not worth copying the items to an Immutable.Array<GlpkTerm>.
    public readonly List<GlpkTerm> Terms;
    public readonly double Min;
    public readonly double Max;

    public GlpkConstraint(double min, double max, List<GlpkTerm> terms)
    {
        Min = min;
        Max = max;
        Terms = terms;
    }
}

/// <summary>
/// The GLPK Api.
/// </summary>
internal static class GlpkApi
{
    /// <summary>
    /// See if the raw GLPK library can be loaded.
    /// </summary>
    public static bool TryLoad(out Exception ex)
    {
        try
        {
            using var prob = new GlpkProblem();
        }
        catch (Exception exCur)
        {
            ex = exCur;
            return false;
        }

        ex = null;
        return true;
    }

    /// <summary>
    /// Try to solve.
    /// </summary>
    public static bool TrySolve(
        int numVars, GlpkVariableInfo vinDef, List<GlpkVariableInfo> vins,
        bool max, List<GlpkTerm> score, List<GlpkConstraint> cons,
        out GlpkSolution sln)
    {
        Validation.Assert(numVars > 0);

        using (var prob = new GlpkProblem())
        {
            // Add the columns.
            prob.AddCols(numVars);

            // First deal with "vins".
            var vids = new BitArray(numVars);
            if (vins != null)
            {
                foreach (var vin in vins)
                {
                    int vid = vin.Vid;
                    Validation.Assert(vid < numVars);
                    Validation.Assert(!vids.Get(vid));
                    vids.Set(vid, true);
                    prob.SetColBounds(vid, vin.Min, vin.Max, vin.IsInt);
                }
            }

            // Set defaults.
            // REVIEW: is this needed?
            if (vins == null || vins.Count < numVars)
            {
                for (int vid = 0; vid < numVars; vid++)
                {
                    if (!vids.Get(vid))
                        prob.SetColBounds(vid, vinDef.Min, vinDef.Max, vinDef.IsInt);
                }
            }

            // Buffers for building terms.
            // WARNING: GLPK uses 1-based indexing for everything, so we need an extra "0" slot that
            // is never used. Most of the GlpkProb API hides this anomaly, but SetRowCoefs cannot.
            // Notationally, "vid" is our variable id while "cid" is a GLPK column id, one more than
            // the corresponding vid.
            var inds = new int[numVars + 1];
            var vals = new double[numVars + 1];

            prob.SetMaximize(max);
            double valScore = 0;
            if (score != null)
            {
                // Record and accumulate values in vals, indexed by vid. Use vids and inds to track which variables have a value set.
                vids.SetAll(false);
                int count = 0;
                foreach (var term in score)
                {
                    int vid = term.Vid;
                    Validation.Assert(-1 <= vid && vid < numVars);

                    double val = term.Coef;
                    if (val == 0)
                        continue;

                    if (vid == -1)
                    {
                        valScore += val;
                        continue;
                    }

                    if (!vids.Get(vid))
                    {
                        vals[vid] = val;
                        inds[count++] = vid;
                        vids.Set(vid, true);
                    }
                    else
                        vals[vid] += val;
                }

                for (int iv = 0; iv < count; iv++)
                {
                    int vid = inds[iv];
                    double val = vals[vid];
                    if (val != 0)
                        prob.SetObjCoef(vid, val);
                }
            }

            // Add the rows.
            if (cons != null)
            {
                prob.AddRows(cons.Count);

                for (int row = 0; row < cons.Count; row++)
                {
                    var con = cons[row];
                    double valConst = 0;
                    if (con.Terms != null && con.Terms.Count > 0)
                    {
                        // Record and accumulate values in vals, indexed by vid. Use vids to track which variables have a value set.
                        vids.SetAll(false);
                        int count = 0;
                        valConst = 0;
                        foreach (var term in con.Terms)
                        {
                            int vid = term.Vid;
                            Validation.Assert(-1 <= vid && vid < numVars);

                            double val = term.Coef;
                            if (val == 0)
                                continue;

                            if (vid == -1)
                            {
                                valConst += val;
                                continue;
                            }

                            int cid = vid + 1;
                            if (!vids.Get(vid))
                            {
                                vals[cid] = val;
                                inds[++count] = cid;
                                vids.Set(vid, true);
                            }
                            else
                                vals[cid] += val;
                        }

                        if (count > 0)
                        {
                            // Sort the cids.
                            Array.Sort(inds, 1, count);

                            // Now move the values to the corresponding positions. Since we sorted, we are guaranteed that
                            // iv <= inds[iv], so won't overwrite a value before it is used.
                            int num = 0;
                            for (int ivSrc = 1; ivSrc <= count; ivSrc++)
                            {
                                int cid = inds[ivSrc];
                                Validation.Assert(num <= cid && cid <= numVars);

                                if (vals[cid] == 0)
                                    continue;

                                num++;
                                vals[num] = vals[cid];
                                inds[num] = cid;
                            }
                            Validation.Assert(0 <= num && num <= count);

                            if (num > 0)
                                prob.SetRowCoefs(row, num, inds, vals);
                        }
                    }

                    prob.SetRowBounds(row, con.Min - valConst, con.Max - valConst);
                }
            }

            if (!prob.TryRun())
            {
                sln = default;
                return false;
            }

            double scoreBest = prob.GetObjectiveValue();
            var values = new double[numVars];
            for (int col = 0; col < numVars; col++)
                values[col] = prob.GetColValue(col);

            sln = new GlpkSolution(scoreBest + valScore, values);
            return true;
        }
    }

    /// <summary>
    /// This is a "safe" wrapper around a native GLPK problem instance.
    /// </summary>
    private sealed class GlpkProblem : IDisposable
    {
        private IntPtr _prob;
        private bool _isMip;

        public GlpkProblem()
        {
            _prob = Native.glp_create_prob();
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            Validation.Assert(_prob != default);
        }

        ~GlpkProblem()
        {
            DisposeCore();
        }

        public void Dispose()
        {
            DisposeCore();
            GC.SuppressFinalize(this);
        }

        private void DisposeCore()
        {
            var prob = Interlocked.Exchange(ref _prob, default);
            if (prob != default)
                Native.glp_delete_prob(prob);
        }

        public int AddCols(int ccol)
        {
            AssertValid();
            Validation.Assert(ccol > 0);

            int ccolOld = Native.glp_get_num_cols(_prob);
            int ret = Native.glp_add_cols(_prob, ccol);
            int ccolNew = Native.glp_get_num_cols(_prob);
            Validation.Assert(ccolNew == ccolOld + ccol);
            Validation.Assert(ret == ccolOld + 1);
            return ret - 1;
        }

        public int AddRows(int crow)
        {
            AssertValid();
            Validation.Assert(crow > 0);

            int crowOld = Native.glp_get_num_rows(_prob);
            int ret = Native.glp_add_rows(_prob, crow);
            int crowNew = Native.glp_get_num_rows(_prob);
            Validation.Assert(crowNew == crowOld + crow);
            Validation.Assert(ret == crowOld + 1);
            return ret - 1;
        }

        private static int GetBndKind(double min, double max)
        {
            Validation.Assert(min <= max);
            Validation.Assert(min < double.PositiveInfinity);
            Validation.Assert(max > double.NegativeInfinity);

            return
              min == double.NegativeInfinity ?
                max == double.PositiveInfinity ? Native.GLP_FR : Native.GLP_UP :
                max == double.PositiveInfinity ? Native.GLP_LO : min < max ? Native.GLP_DB : Native.GLP_FX;
        }

        public void SetColBounds(int col, double min, double max, bool isInt)
        {
            AssertValid();
            Validation.AssertIndex(col, Native.glp_get_num_cols(_prob));

            Native.glp_set_col_bnds(_prob, col + 1, GetBndKind(min, max), min, max);
            if (isInt)
            {
                Native.glp_set_col_kind(_prob, col + 1, Native.GLP_IV);
                _isMip = true;
            }
        }

        public void SetRowBounds(int row, double min, double max)
        {
            AssertValid();
            Validation.AssertIndex(row, Native.glp_get_num_rows(_prob));

            Native.glp_set_row_bnds(_prob, row + 1, GetBndKind(min, max), min, max);
        }

        public void SetMaximize(bool maximize)
        {
            AssertValid();
            Native.glp_set_obj_dir(_prob, maximize ? Native.GLP_MAX : Native.GLP_MIN);
        }

        public void SetObjCoef(int col, double coef)
        {
            AssertValid();
            Validation.AssertIndex(col, Native.glp_get_num_cols(_prob));

            Native.glp_set_obj_coef(_prob, col + 1, coef);
        }

        public void SetRowCoefs(int row, int count, int[] inds, double[] vals)
        {
            AssertValid();
            Validation.AssertIndex(row, Native.glp_get_num_rows(_prob));
            Validation.Assert(count > 0);

            Native.glp_set_mat_row(_prob, row + 1, count, inds, vals);
        }

        public bool TryRun()
        {
            AssertValid();

            if (_isMip)
            {
                Native.glp_init_iocp(out var iocp);
                iocp.presolve = Native.GLP_ON;
                // REVIEW: Test the return value?
                Native.glp_intopt(_prob, ref iocp);
                return Native.glp_mip_status(_prob) == Native.GLP_OPT;
            }
            else
            {
                Native.glp_init_smcp(out var smcp);
                smcp.presolve = Native.GLP_ON;
                // REVIEW: Test the return value?
                Native.glp_simplex(_prob, ref smcp);
                int res = Native.glp_get_status(_prob);
                return res == Native.GLP_OPT;
            }
        }

        public double GetObjectiveValue()
        {
            AssertValid();

            return _isMip ? Native.glp_mip_obj_val(_prob) : Native.glp_get_obj_val(_prob);
        }

        public double GetColValue(int col)
        {
            AssertValid();
            Validation.AssertIndex(col, Native.glp_get_num_cols(_prob));

            return _isMip ? Native.glp_mip_col_val(_prob, col + 1) : Native.glp_get_col_prim(_prob, col + 1);
        }
    }
}
