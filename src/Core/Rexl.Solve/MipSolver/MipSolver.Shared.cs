// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Symbolic;

namespace Microsoft.Rexl.Solve;

using Conditional = System.Diagnostics.ConditionalAttribute;
using Integer = System.Numerics.BigInteger;

// This partial handles solving using GLPK and contains shared helper methods.
// REVIEW: Split out the shared functionality from the GLPK specific stuff.
partial class MipSolver
{
    private double GetValue(int ifma, double def)
    {
        if (ifma < 0)
            return def;

        Validation.AssertIndex(ifma, _module.Bnd.Items.Length);
        var value = _symMap.GetItemValue(ifma, out var type, out var st);

        if (value is null)
            return def;

        switch (type.RootKind)
        {
        case DKind.R8:
            Validation.Assert(st == typeof(double));
            return (double)value;
        case DKind.R4:
            Validation.Assert(st == typeof(float));
            return (float)value;
        case DKind.IA:
            Validation.Assert(st == typeof(Integer));
            return (double)(Integer)value;
        case DKind.I8:
            Validation.Assert(st == typeof(long));
            return (long)value;
        case DKind.I4:
            Validation.Assert(st == typeof(int));
            return (int)value;
        case DKind.I2:
            Validation.Assert(st == typeof(short));
            return (short)value;
        case DKind.I1:
            Validation.Assert(st == typeof(sbyte));
            return (sbyte)value;
        case DKind.U8:
            Validation.Assert(st == typeof(ulong));
            return (ulong)value;
        case DKind.U4:
            Validation.Assert(st == typeof(uint));
            return (uint)value;
        case DKind.U2:
            Validation.Assert(st == typeof(ushort));
            return (ushort)value;
        case DKind.U1:
            Validation.Assert(st == typeof(byte));
            return (byte)value;
        case DKind.Bit:
            Validation.Assert(st == typeof(bool));
            return (bool)value ? 1.0 : 0.0;
        }

        // REVIEW: Is this possible?
        Validation.Assert(false);
        return def;
    }

    private T GetValue<T>(int ifma)
    {
        if (ifma < 0)
            return default;

        Validation.AssertIndex(ifma, _module.Bnd.Items.Length);
        var value = _symMap.GetItemValue(ifma, out var type, out var st);

        Validation.Assert(typeof(T).IsAssignableFrom(st));
        return (T)value;
    }

    [Conditional("DEBUG")]
    private static void AssertRng(int cvar, IdRng rng)
    {
        Validation.AssertIndexInclusive(rng.Lim, cvar);
        Validation.AssertIndexInclusive(rng.Min, rng.Lim);
    }

    private bool TryMapSymbolsToSolverVariables(
        Action<int, bool, double, double> addVarInfo,
        Action<DName, IdRng, bool> addOneOfConstraint)
    {
        int cvar = _symMap.VarCount;
        foreach (var se in _symMap.GetEntries())
        {
            if (se is SymbolMap.BasicVarEntry bve)
            {
                var min = GetValue(bve.Symbol.FormulaFrom, double.NegativeInfinity);
                var max = GetValue(bve.Symbol.FormulaTo, double.PositiveInfinity);
                int id = bve.Svar.Id;
                // _rpt.WriteLine("Bounds: {0} <= V{1} <= {2}", min, id, max);
                Validation.AssertIndex(id, cvar);
                addVarInfo(id, bve.Type.IsIntegralXxx, min, max);
            }
            else if (se is SymbolMap.TensorVarEntry tve)
            {
                var min = GetValue<Tensor>(tve.Symbol.FormulaFrom);
                var max = GetValue<Tensor>(tve.Symbol.FormulaTo);
                bool isInt = tve.TypeVar.IsIntegralReq;

                if (min != null || max != null || isInt)
                {
                    AssertRng(cvar, tve.Rng);
                    int idMin = tve.Rng.Min;
                    int idLim = tve.Rng.Lim;

                    // REVIEW: Handle additional numeric types.
                    switch (tve.TypeVar.Kind)
                    {
                    default:
                        _sink.PostDiagnostic(DiagSource.Solver,
                            MessageDiag.Error(ErrorStrings.ErrSolverBadTensorItemType_Name_Type, se.Symbol.Name, tve.TypeVar));
                        return false;

                    case DKind.R8:
                        {
                            var tenMin = min as Tensor<double>;
                            var tenMax = max as Tensor<double>;
                            for (int id = idMin; id < idLim; id++)
                            {
                                double dblMin = tenMin != null ? tenMin.GetAtIndex(id - idMin) : double.NegativeInfinity;
                                double dblMax = tenMax != null ? tenMax.GetAtIndex(id - idMin) : double.PositiveInfinity;
                                addVarInfo(id, false, dblMin, dblMax);
                            }
                        }
                        break;
                    case DKind.I8:
                        {
                            var tenMin = min as Tensor<long>;
                            var tenMax = max as Tensor<long>;
                            for (int id = idMin; id < idLim; id++)
                            {
                                double dblMin = tenMin != null ? tenMin.GetAtIndex(id - idMin) : double.NegativeInfinity;
                                double dblMax = tenMax != null ? tenMax.GetAtIndex(id - idMin) : double.PositiveInfinity;
                                addVarInfo(id, true, dblMin, dblMax);
                            }
                        }
                        break;
                    case DKind.Bit:
                        {
                            var tenMin = min as Tensor<bool>;
                            var tenMax = max as Tensor<bool>;
                            for (int id = idMin; id < idLim; id++)
                            {
                                double dblMin = tenMin != null && tenMin.GetAtIndex(id - idMin) ? 1.0 : 0.0;
                                double dblMax = tenMax == null || tenMax.GetAtIndex(id - idMin) ? 1.0 : 0.0;
                                addVarInfo(id, true, dblMin, dblMax);
                            }
                        }
                        break;
                    }
                }
            }
            else if (se is SymbolMap.IndicatorVarEntry ive)
            {
                // Add the var info and the constraint.
                Validation.Assert(ive.TypeVar.IsIntegralXxx);
                AssertRng(cvar, ive.Rng);
                int idMin = ive.Rng.Min;
                int idLim = ive.Rng.Lim;

                for (int id = idMin; id < idLim; id++)
                    addVarInfo(id, true, 0.0, 1.0);
                // REVIEW: Currently there doesn't seem to be a way to use an optional item var
                // in a measure. Any attempt to use it makes the measure non-linear. Should address this
                // by handling coalesce and IsNull in the symbol reducer.
                if (ive is SymbolMap.ItemVarEntry)
                    addOneOfConstraint(ive.Symbol.Name, ive.Rng, ive.Symbol.Type.IsOpt);
            }
            else if (se is SymbolMap.ComputedVarEntry cve)
            {
                if (cve.SvarOpt != null)
                {
                    int id = cve.SvarOpt.Id;
                    addVarInfo(id, cve.Type.IsIntegralXxx, double.NegativeInfinity, double.PositiveInfinity);
                }
            }
            else
            {
                // Shouldn't happen.
                Validation.Assert(false);
                _sink.PostDiagnostic(DiagSource.Solver,
                    MessageDiag.Error(ErrorStrings.ErrSolverUnhandledVar_Name, se.Symbol.Name));
                return false;
            }
        }

        return true;
    }

    private bool TryMapValuesToSymbols(double[] values, out List<(DName name, object value)> symValues)
    {
        int cvar = _symMap.VarCount;
        Validation.Assert(values.Length == cvar);

        // Map back from solver values to symbols.
        var res = new List<(DName name, object value)>(cvar);
        symValues = null;

        foreach (var sym in _module.Bnd.Symbols)
        {
            if (!sym.IsFreeVarSym)
                continue;

            if (!_symMap.TryGetSymbol(sym.Name, out var se))
                continue;

            object value;
            BndFreeVarNode svar;
            int id;
            if ((se is SymbolMap.BasicVarEntry bve && (svar = bve.Svar) != null ||
                    se is SymbolMap.ComputedVarEntry cve && (svar = cve.SvarOpt) != null) &&
                (uint)(id = svar.Id) < (uint)cvar)
            {
                double dbl = values[id];
                Validation.Assert(sym.Type.IsNumericReq);
                // REVIEW: Handle additional numeric types.
                switch (sym.Type.Kind)
                {
                case DKind.R8:
                    value = dbl;
                    break;
                case DKind.I8:
                    dbl = Math.Round(dbl);
                    var n = (long)dbl;
                    if (n != dbl)
                    {
                        _sink.PostDiagnostic(DiagSource.Solver,
                            MessageDiag.Warning(ErrorStrings.WrnSolverBadIntValue_Name_Val, sym.Name, dbl));
                    }
                    value = n;
                    break;

                default:
                    _sink.PostDiagnostic(DiagSource.Solver,
                        MessageDiag.Warning(ErrorStrings.WrnSolverVarBadType_Name_Val, sym.Name, sym.Type));
                    continue;
                }
            }
            else if (se is SymbolMap.TensorVarEntry tve)
            {
                AssertRng(cvar, tve.Rng);
                // REVIEW: Handle additional numeric types.
                switch (tve.TypeVar.Kind)
                {
                case DKind.R8:
                    value = Tensor<double>.CreateFrom(values.AsSpan(tve.Rng.Min, tve.Rng.Count), tve.Shape.ToArray());
                    break;
                case DKind.I8:
                    {
                        var vals = ToI8(values, tve.Rng, sym.Name);
                        if (vals == null)
                            continue;
                        value = Tensor<long>.CreateFrom(vals, tve.Shape);
                    }
                    break;
                case DKind.Bit:
                    {
                        var vals = ToBool(values, tve.Rng, sym.Name);
                        if (vals == null)
                            continue;
                        value = Tensor<bool>.CreateFrom(vals, tve.Shape);
                    }
                    break;

                default:
                    _sink.PostDiagnostic(DiagSource.Solver,
                        MessageDiag.Warning(ErrorStrings.WrnSolverVarBadType_Name_Val, sym.Name, tve.TypeVar));
                    continue;
                }
            }
            else if (se is SymbolMap.ItemVarEntry ive)
            {
                AssertRng(cvar, ive.Rng);
                int idFnd = -1;
                for (int idCur = ive.Rng.Min; idCur < ive.Rng.Lim; idCur++)
                {
                    var val = values[idCur];
                    Validation.Assert(val == 0 || val == 1);
                    if (val == 1)
                    {
                        Validation.Assert(idFnd < 0);
                        idFnd = idCur;
                    }
                }
                if (idFnd < 0)
                {
                    if (ive.Symbol.Type.IsOpt)
                        value = null;
                    else
                    {
                        // REVIEW: This shouldn't be possible if the solution is feasible.
                        Validation.Assert(false);
                        value = ive.Keys.GetValue(0);
                    }
                }
                else
                {
                    Validation.Assert(ive.Rng.Min <= idFnd && idFnd < ive.Rng.Lim);
                    value = ive.Keys.GetValue(idFnd - ive.Rng.Min);
                }
            }
            else if (se is SymbolMap.SubsetVarEntry sve)
            {
                AssertRng(cvar, sve.Rng);
                var stItem = sve.KeysConst.ItemSysType;
                int num = 0;
                for (int idCur = sve.Rng.Min; idCur < sve.Rng.Lim; idCur++)
                {
                    var val = values[idCur];
                    Validation.Assert(val == 0 || val == 1);
                    if (val == 1)
                        num++;
                }

                var arr = Array.CreateInstance(stItem, num);
                int i = 0;
                for (int idCur = sve.Rng.Min; idCur < sve.Rng.Lim; idCur++)
                {
                    var val = values[idCur];
                    Validation.Assert(val == 0 || val == 1);
                    if (val == 1)
                    {
                        Validation.Assert(i < num);
                        arr.SetValue(sve.Keys.GetValue(idCur - sve.Rng.Min), i);
                        i++;
                    }
                }
                Validation.Assert(i == num);
                value = arr;
            }
            else
            {
                // If this fires, the pre-solve code above handles something that this post-solve code doesn't.
                Validation.Assert(false);
                continue;
            }

            if (sym.IsFreeVarSym)
                res.Add((sym.Name, value));
        }

        symValues = res;
        return true;
    }

    private long[] ToI8(double[] values, IdRng rng, DName name)
    {
        AssertRng(values.Length, rng);
        var res = new long[rng.Count];
        for (int i = rng.Min; i < rng.Lim; i++)
        {
            var dbl = values[i];
            dbl = Math.Round(dbl);
            var n = (long)dbl;
            if (n != dbl)
            {
                _sink.PostDiagnostic(DiagSource.Solver,
                    MessageDiag.Warning(ErrorStrings.WrnSolverBadIntValue_Name_Val, name, dbl));
            }
            res[i - rng.Min] = n;
        }
        return res;
    }

    private bool[] ToBool(double[] values, IdRng rng, DName name)
    {
        AssertRng(values.Length, rng);
        var res = new bool[rng.Count];
        for (int i = rng.Min; i < rng.Lim; i++)
        {
            var dbl = values[i];
            dbl = Math.Round(dbl);
            if (dbl == 0.0)
                res[i - rng.Min] = false;
            else if (dbl == 1.0)
                res[i - rng.Min] = true;
            else
            {
                _sink.PostDiagnostic(DiagSource.Solver,
                    MessageDiag.Warning(ErrorStrings.WrnSolverBadBoolValue_Name_Val, name, dbl));
            }
        }
        return res;
    }
}
