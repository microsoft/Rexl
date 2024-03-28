// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Symbolic;

namespace Microsoft.Rexl.Solve;

// This partial handles linearizing the constraints and objective/measure.
partial class MipSolver
{
    private LinComb _linMsr;
    private List<LinearConstraint> _lcons;

    private void MapToLinear()
    {
        _lcons = new List<LinearConstraint>();

        // Map the measure.
        if (_msr is null)
        {
            // REVIEW: Is this case needed?
            _linMsr = LinComb.Zero;
        }
        else
        {
            // Just use the measure variable as the goal.
            int idMsr = _symMap.EnsureVariable(_msr.Name).Id;
            _linMsr = LinComb.Create(idMsr, 1);
        }

        // Map the constraints.
        foreach (var (sym, eval) in _constraints)
        {
            if (!TryMapConToLinear(eval))
            {
                _hasError = true;
                MessageDiag diag;
                if (!sym.IsConstraintSym &&
                    _symMap.TryGetSymbol(sym.Name, out var entry) &&
                    entry is SymbolMap.ComputedVarEntry cve)
                {
                    diag = MessageDiag.Error(ErrorStrings.ErrSolverDefNotLinear_Name_Bnd, sym.Name, cve.Node);
                }
                else
                    diag = MessageDiag.Error(ErrorStrings.ErrSolverConNotLinear_Name_Bnd, sym.Name, eval);
                _sink.PostDiagnostic(DiagSource.Solver, diag);
            }
        }
    }

    private bool TryMapConToLinear(BoundNode bnd)
    {
        var bndRaw = bnd; // For debugging.
        bnd = SymbolReducer.Simplify(_symMap, bndRaw, expandSelect: true);

        if (bnd.Type.RootType != DType.BitReq)
            return Fail();

        // Handle sequences.
        if (bnd.Type.IsSequence)
            return TryMapConSeq(bnd);

        if (bnd.TryGetBool(out bool v))
        {
            if (v)
                return true;
            // Always false, so add a contradictory constraint.
            Util.Add(ref _lcons, LinearConstraint.Create(LinComb.Zero, 1, 1));
            return true;
        }

        switch (bnd)
        {
        case BndCompareNode cmp:
            return TryMapConCmp(cmp);
        case BndVariadicOpNode bvon:
            if (bvon.Op != BinaryOp.And)
                return Fail();
            foreach (var arg in bvon.Args)
            {
                if (!TryMapConToLinear(arg))
                    return Fail();
            }
            return true;
        }

        return Fail();
    }

    private bool TryMapConSeq(BoundNode bnd)
    {
        Validation.Assert(bnd.Type.RootType == DType.BitReq);

        if (bnd.IsNullValue)
            return true;

        switch (bnd)
        {
        case BndSequenceNode bsn:
            foreach (var item in bsn.Items)
            {
                if (!TryMapConToLinear(item))
                    return Fail();
            }
            return true;
        }

        // REVIEW: Handle more!
        return Fail();
    }

    private bool TryMapConCmp(BndCompareNode cmp)
    {
        Validation.Assert(cmp.Type == DType.BitReq);
        int count = cmp.Args.Length;
        Validation.Assert(count >= 2);
        bool isInt = cmp.Args[0].Type.IsIntegralXxx;

        var lins = new LinComb[count];
        for (int i = 0; i < count; i++)
        {
            var arg = cmp.Args[i];
            Validation.Assert(isInt == arg.Type.IsIntegralXxx);
            var linCur = lins[i] = LinearExtractor.Run(_symMap, arg);
            if (linCur.IsError)
                return Fail();
        }

        for (int i = 0; i < cmp.Ops.Length; i++)
        {
            // Clear/ignore strict.
            var op = cmp.Ops[i].ClearStrict().SimplifyForTotalOrder();
            if (op == CompareOp.None)
                continue;

            var lhs = lins[i];
            var rhs = lins[i + 1];
            LinearConstraint lcon;
            var (root, mods) = op.GetParts();
            if (mods.IsNot())
            {
                Validation.Assert(root == CompareRoot.Equal);
                // REVIEW: Perhaps we should handle this when the type is bit?
                return Fail();
            }

            if (root != CompareRoot.Equal &&
                i + 1 < cmp.Ops.Length && cmp.Ops[i + 1] == op && lhs.IsConstant && lins[i + 2].IsConstant)
            {
                // Combine adjacent ones, like -3 <= x - y <= 10.
                double a = lhs.ConstantTerm;
                double b = lins[i + 2].ConstantTerm;
                double lo, hi;
                switch (root)
                {
                default:
                    return Fail();
                case CompareRoot.LessEqual:
                    lo = a; hi = b;
                    break;
                case CompareRoot.GreaterEqual:
                    lo = b; hi = a;
                    break;
                case CompareRoot.Less:
                    if (!isInt)
                        return Fail();
                    lo = a + 1; hi = b - 1;
                    break;
                case CompareRoot.Greater:
                    if (!isInt)
                        return Fail();
                    lo = b + 1; hi = a - 1;
                    break;
                }
                lcon = LinearConstraint.Create(rhs, lo, hi);
            }
            else
            {
                switch (root)
                {
                default:
                    return Fail();
                case CompareRoot.Equal:
                    lcon = LinearConstraint.CreateEQ(lhs, rhs);
                    break;
                case CompareRoot.LessEqual:
                    lcon = LinearConstraint.CreateLE(lhs, rhs);
                    break;
                case CompareRoot.GreaterEqual:
                    lcon = LinearConstraint.CreateGE(lhs, rhs);
                    break;
                case CompareRoot.Less:
                    if (!isInt)
                        return Fail();
                    lcon = LinearConstraint.CreateLE(lhs, rhs - LinComb.One);
                    break;
                case CompareRoot.Greater:
                    if (!isInt)
                        return Fail();
                    lcon = LinearConstraint.CreateGE(lhs - LinComb.One, rhs);
                    break;
                }
            }
            Util.Add(ref _lcons, lcon);
        }
        return true;
    }

    // This is to make it easy to set a break point on all failure cases.
    private bool Fail()
    {
        return false;
    }

    private sealed class LinearConstraint
    {
        public readonly LinComb Lin;
        public readonly double Lo;
        public readonly double Hi;

        public bool IsTrue => Lin.IsZero && Lo <= 0 && 0 <= Hi;
        public bool IsFalse => Lo > Hi || Lin.IsZero && !(Lo <= 0 && 0 <= Hi);

        private LinearConstraint(LinComb lin, double lo, double hi)
        {
            Validation.Assert(!lin.IsError);
            Validation.Assert(lin.ConstantTerm == 0);
            Validation.Assert(!double.IsNaN(lo));
            Validation.Assert(!double.IsNaN(hi));
            Validation.Assert(lo >= 0 || hi > 0);

            Lin = lin;
            Lo = lo;
            Hi = hi;
        }

        public static LinearConstraint Create(LinComb lin, double lo, double hi)
        {
            Validation.Assert(!lin.IsError);
            Validation.Assert(!double.IsNaN(lo));
            Validation.Assert(!double.IsNaN(hi));

            // Normalize so the constant term in lin is zero and so we favor positive values.
            var value = lin.ConstantTerm;
            if (value != 0 || lo < 0 && hi <= 0)
            {
                lo -= value;
                hi -= value;
                if (lo < 0 && hi <= 0)
                {
                    // Negate everything.
                    lin = LinComb.Create(value) - lin;
                    var tmp = lo;
                    lo = -hi;
                    hi = -tmp;
                }
                else
                    lin -= LinComb.Create(value);
            }

            if (lo <= hi)
                return new LinearConstraint(lin, lo, hi);
            // Always false.
            return new LinearConstraint(LinComb.Zero, 1, 1);
        }

        public static LinearConstraint CreateEQ(LinComb lin0, LinComb lin1)
        {
            return Create(lin0 - lin1, 0, 0);
        }

        public static LinearConstraint CreateLE(LinComb lin0, LinComb lin1)
        {
            return Create(lin0 - lin1, double.NegativeInfinity, 0);
        }

        public static LinearConstraint CreateGE(LinComb lin0, LinComb lin1)
        {
            return Create(lin0 - lin1, 0, double.PositiveInfinity);
        }
    }
}
