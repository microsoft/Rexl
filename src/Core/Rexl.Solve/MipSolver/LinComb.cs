// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Solve;

using TermBldr = Immutable.Array<LinTerm>.Builder;
using TermTuple = Immutable.Array<LinTerm>;

/// <summary>
/// Represents a "linear term" as a coefficient <see cref="Coef"/> and variable index <see cref="Vid"/>.
/// For a constant, with no variable, the variable index is -1.
/// </summary>
public struct LinTerm
{
    /// <summary>
    /// The variable index. This is -1 for a constant term (no variable).
    /// </summary>
    public readonly int Vid;

    /// <summary>
    /// The coefficient.
    /// </summary>
    public readonly double Coef;

    /// <summary>
    /// Note that the constructors don't check validity. Use the IsValid method to test validity.
    /// </summary>
    public LinTerm(int v, double c)
    {
        Vid = v;
        Coef = c;
    }

    public override string ToString()
    {
        if (Vid < 0)
            return Coef.ToString("R");
        if (Coef == 1)
            return $"V{Vid}";
        if (Coef == -1)
            return $"-V{Vid}";
        return $"{Coef:R} * V{Vid}";
    }
}

/// <summary>
/// A linear combination of variables, represented as an immutable array of <see cref="LinTerm"/>. Each variable
/// is represented as a non-negative integer variable id. There is a special <see cref="Error"/> value.
/// </summary>
public struct LinComb
{
    /// <summary>
    /// The error value.
    /// </summary>
    public static LinComb Error => default;

    /// <summary>
    /// The zero value.
    /// </summary>
    public static readonly LinComb Zero = new LinComb(TermTuple.Empty);

    /// <summary>
    /// The one value.
    /// </summary>
    public static readonly LinComb One = LinComb.Create(1);

    /// <summary>
    /// The terms for this linear combination. This is "default" for an error. Otherwise, it contains
    /// the terms sorted by variable id. Any constant term will be first, with variable id -1.
    /// </summary>
    public readonly TermTuple Terms { get; }

    /// <summary>
    /// Whether this is an error linear combination.
    /// </summary>
    public bool IsError => Terms.IsDefault;

    /// <summary>
    /// Whether this is the zero linear combination.
    /// </summary>
    public bool IsZero => !Terms.IsDefault && Terms.Length == 0;

    /// <summary>
    /// Whether this is a constant linear combination.
    /// </summary>
    public bool IsConstant => !Terms.IsDefault && (Terms.Length == 0 || Terms.Length == 1 && Terms[0].Vid < 0);

    /// <summary>
    /// The constant term.
    /// </summary>
    public double ConstantTerm
    {
        get
        {
            if (IsError)
                return double.NaN;
            if (Terms.Length == 0 || Terms[0].Vid >= 0)
                return 0;
            return Terms[0].Coef;
        }
    }

    /// <summary>
    /// Create a constant linear combination.
    /// </summary>
    public static LinComb Create(double coef)
    {
        if (coef == 0)
            return Zero;
        if (!coef.IsFinite())
            return Error;
        return new LinComb(Immutable.Array.Create(new LinTerm(-1, coef)));
    }

    /// <summary>
    /// Create a single-term linear combination.
    /// </summary>
    public static LinComb Create(int vid, double coef)
    {
        if (vid < -1 || !coef.IsFinite())
            return Error;
        if (coef == 0)
            return Zero;
        return new LinComb(Immutable.Array.Create(new LinTerm(vid, coef)));
    }

    private LinComb(TermTuple terms)
    {
        Validation.Assert(IsNormal(terms));
        Terms = terms;
    }

    /// <summary>
    /// Returns true iff the tuple of terms is "normalized". This means that the terms are sorted by
    /// variable index, the indices are unique and >= -1 (with -1 meaning the constant term), and
    /// the coefficients are all non-zero and finite (not NaN and not an infinity).
    /// </summary>
    private static bool IsNormal(TermTuple terms)
    {
        if (terms.IsDefault)
            return true;

        int v = -2;
        foreach (var term in terms)
        {
            if (v >= term.Vid)
                return false;
            v = term.Vid;
            if (term.Coef == 0 || !term.Coef.IsFinite())
                return false;
        }
        return true;
    }

    /// <summary>
    /// Return the difference of two linear combinations.
    /// </summary>
    public static LinComb operator -(LinComb a, LinComb b)
    {
        if (a.IsError)
            return Error;
        if (b.IsError)
            return Error;

        if (b.IsConstant)
        {
            var k = b.ConstantTerm;
            Validation.Assert(k.IsFinite());
            if (k == 0)
                return a;
            if (a.IsConstant)
            {
                Validation.Assert(a.ConstantTerm.IsFinite());
                return Create(a.ConstantTerm - k);
            }

            if (a.Terms[0].Vid >= 0)
                return new LinComb(a.Terms.Insert(0, new LinTerm(-1, -k)));
            k = a.Terms[0].Coef - k;
            if (k == 0)
                return new LinComb(a.Terms.RemoveAt(0));
            if (!k.IsFinite())
                return Error;
            return new LinComb(a.Terms.SetItem(0, new LinTerm(-1, k)));
        }

        Validation.Assert(!b.IsConstant);
        TermBldr bldr;
        if (a.IsConstant)
        {
            bldr = Neg(b.Terms.ToBuilder());
            var k = a.ConstantTerm;
            Validation.Assert(k.IsFinite());
            if (k != 0)
            {
                if (bldr[0].Vid >= 0)
                    bldr.Insert(0, new LinTerm(-1, -k));
                else
                {
                    k = a.Terms[0].Coef - k;
                    if (k == 0)
                        bldr.RemoveAt(0);
                    else if (!k.IsFinite())
                        return Error;
                    else
                        bldr[0] = new LinTerm(-1, k);
                }
            }
        }
        else
        {
            // The general case: merge the two.
            var termsA = a.Terms;
            var termsB = b.Terms;
            int lenA = termsA.Length;
            int lenB = termsB.Length;
            bldr = TermTuple.CreateBuilder(lenA + lenB);
            int ia = 0;
            int ib = 0;
            var termA = termsA[ia];
            var termB = termsB[ib];
            for (; ; )
            {
                Validation.Assert(ia < lenA);
                Validation.Assert(ib < lenB);
                Validation.Assert(termA.Coef.IsFinite());
                Validation.Assert(termB.Coef.IsFinite());
                Validation.Assert(termA.Vid == termsA[ia].Vid);
                Validation.Assert(termA.Coef == termsA[ia].Coef);
                Validation.Assert(termB.Vid == termsB[ib].Vid);
                Validation.Assert(termB.Coef == termsB[ib].Coef);

                int dv = termA.Vid - termB.Vid;
                if (dv == 0)
                {
                    var k = termA.Coef - termB.Coef;
                    if (!k.IsFinite())
                        return Error;
                    if (k != 0)
                        bldr.Add(new LinTerm(termA.Vid, k));
                    ia++;
                    ib++;
                    if (ia >= lenA || ib >= lenB)
                        break;
                    termA = termsA[ia];
                    termB = termsB[ib];
                    continue;
                }

                if (dv < 0)
                {
                    bldr.Add(termA);
                    if (++ia >= lenA)
                        break;
                    termA = termsA[ia];
                }
                else
                {
                    bldr.Add(new LinTerm(termB.Vid, -termB.Coef));
                    if (++ib >= lenB)
                        break;
                    termB = termsB[ib];
                }
            }

            while (ia < lenA)
                bldr.Add(termsA[ia++]);
            while (ib < lenB)
            {
                termB = termsB[ib++];
                bldr.Add(new LinTerm(termB.Vid, -termB.Coef));
            }
        }

        return new LinComb(bldr.ToImmutable());
    }

    /// <summary>
    /// Multiple a linear combination times a constant.
    /// </summary>
    public static LinComb operator *(LinComb a, double b)
    {
        if (a.IsError)
            return Error;
        if (!b.IsFinite())
            return Error;
        if (b == 1)
            return a;
        if (b == 0)
            return Zero;

        var terms = Mul(a.Terms.ToBuilder(), b).ToImmutable();
        if (!IsNormal(terms))
            return Error;
        return new LinComb(terms);
    }

    /// <summary>
    /// Divide a linear combination by a constant.
    /// </summary>
    public static LinComb operator /(LinComb a, double b)
    {
        if (a.IsError)
            return Error;
        if (b == 0 || !b.IsFinite())
            return Error;
        if (b == 1)
            return a;

        var terms = Div(a.Terms.ToBuilder(), b).ToImmutable();
        if (!IsNormal(terms))
            return Error;
        return new LinComb(terms);
    }

    /// <summary>
    /// Negate all terms in a builder.
    /// </summary>
    private static TermBldr Neg(TermBldr bldr)
    {
        for (int i = 0; i < bldr.Count; i++)
        {
            var term = bldr[i];
            bldr[i] = new LinTerm(term.Vid, -term.Coef);
        }
        return bldr;
    }

    /// <summary>
    /// Multiply all terms in a builder by a constant.
    /// </summary>
    private static TermBldr Mul(TermBldr bldr, double d)
    {
        Validation.Assert(d.IsFinite());
        Validation.Assert(d != 0);

        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < bldr.Count; ivSrc++)
        {
            var term = bldr[ivSrc];
            var c = term.Coef * d;
            if (c == 0)
                continue;
            bldr[ivDst++] = new LinTerm(term.Vid, c);
        }
        if (ivDst < bldr.Count)
            bldr.RemoveTail(ivDst);
        return bldr;
    }

    /// <summary>
    /// Divide all terms in a builder by a constant.
    /// </summary>
    private static TermBldr Div(TermBldr bldr, double d)
    {
        Validation.Assert(d.IsFinite());
        Validation.Assert(d != 0);

        int ivDst = 0;
        for (int i = 0; i < bldr.Count; i++)
        {
            var term = bldr[i];
            var c = term.Coef / d;
            if (c == 0)
                continue;
            bldr[ivDst++] = new LinTerm(term.Vid, c);
        }
        return bldr;
    }

    public override string ToString()
    {
        if (IsError)
            return "<Error>";
        if (IsZero)
            return "0";
        var sb = new StringBuilder();
        foreach (var term in Terms)
        {
            if (sb.Length > 0)
                sb.Append(" + ");
            sb.Append(term);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Create a linear combination builder with the given <paramref name="capacity"/>.
    /// </summary>
    public static Builder CreateBuilder(int capacity)
    {
        return new Impl(capacity);
    }

    /// <summary>
    /// A linear combination builder.
    /// </summary>
    public abstract class Builder
    {
        private protected Builder()
        {
        }

        /// <summary>
        /// Add or subtract the given linear combination according to <paramref name="sub"/>.
        /// Returns false if the result is an error.
        /// </summary>
        public abstract bool TryAdd(LinComb lin, bool sub);

        /// <summary>
        /// Construct the result linear combination.
        /// </summary>
        public abstract LinComb Make();
    }

    /// <summary>
    /// The implementation of the linear combination builder.
    /// </summary>
    private sealed class Impl : Builder
    {
        /// <summary>
        /// The non-zero terms.
        /// </summary>
        private readonly Dictionary<int, double> _terms;

        /// <summary>
        /// Whether the result is an error.
        /// </summary>
        private bool _error;

        public Impl(int capacity)
        {
            _terms = new Dictionary<int, double>(capacity);
        }

        public override bool TryAdd(LinComb lin, bool sub)
        {
            if (_error |= lin.IsError)
                return false;
            if (lin.IsZero)
                return true;
            foreach (var term in lin.Terms)
            {
                _terms.TryGetValue(term.Vid, out var coef);
                coef = sub ? coef - term.Coef : coef + term.Coef;
                if (!coef.IsFinite())
                    return false;
                // Store it even when the coef is zero. Make will filter out zeros.
                _terms[term.Vid] = coef;
            }
            return true;
        }

        public override LinComb Make()
        {
            if (_error)
                return Error;
            if (_terms.Count == 0)
                return Zero;

            var bldr = TermTuple.CreateBuilder(_terms.Count, init: true);
            int ivDst = 0;
            foreach (var kvp in _terms)
            {
                if (kvp.Value == 0)
                    continue;
                Validation.Assert(kvp.Value.IsFinite());
                var c = kvp.Value;

                // Insertion sort.
                int key = kvp.Key;
                int iv = ivDst++;
                while (iv > 0 && bldr[iv - 1].Vid > key)
                {
                    Validation.Assert(bldr[iv - 1].Vid != key);
                    bldr[iv] = bldr[--iv];
                }
                bldr[iv] = new LinTerm(kvp.Key, c);
            }

            if (ivDst < bldr.Count)
                bldr.RemoveTail(ivDst);
            return new LinComb(bldr.ToImmutable());
        }
    }
}
