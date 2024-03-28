// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Solve;

using VarID = System.Int32;

/// <summary>
/// Struct for a literal. A literal is a Boolean variable or the negation of a Boolean variable
/// </summary>
public struct Literal : IComparable<Literal>
{
    private int _id;

    /// <summary>
    /// Null literal
    /// </summary>
    public static readonly Literal Nil = new Literal(-1);

    public Literal(VarID var, bool fSense)
    {
        _id = var << 1;
        if (fSense)
            _id |= 1;
    }

    internal Literal(int id)
    {
        _id = id;
    }

    /// <summary>
    /// Get the ID of the literal.
    /// </summary>
    public int Id { get { return _id; } }

    /// <summary>
    /// Get the Boolean variable that forms this literal.
    /// </summary>
    public VarID Var { get { return _id >> 1; } }

    /// <summary>
    /// Get the sign of the literal.
    /// </summary>
    public bool Sense { get { return (_id & 1) != 0; } }

    /// <summary>
    /// Whether this literal is a null literal.
    /// </summary>
    public bool IsNil { get { return _id < 0; } }

    /// <summary>
    /// Construct the dual literal of lit.
    /// </summary>
    public static Literal operator ~(Literal lit)
    {
        Validation.Assert(!lit.IsNil);
        return new Literal(lit._id ^ 1);
    }

    public static bool operator ==(Literal lit1, Literal lit2) { return lit1._id == lit2._id; }
    public static bool operator !=(Literal lit1, Literal lit2) { return lit1._id != lit2._id; }
    public static bool operator <(Literal lit1, Literal lit2) { return lit1._id < lit2._id; }
    public static bool operator >(Literal lit1, Literal lit2) { return lit1._id > lit2._id; }
    public static bool operator <=(Literal lit1, Literal lit2) { return lit1._id <= lit2._id; }
    public static bool operator >=(Literal lit1, Literal lit2) { return lit1._id >= lit2._id; }

    public override string ToString()
    {
        if (!Sense)
            return "~" + Var;
        return Var.ToString(CultureInfo.InvariantCulture);
    }

    public override bool Equals(object obj)
    {
        return obj is Literal && ((Literal)obj)._id == _id;
    }

    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

    public int CompareTo(Literal lit)
    {
        return _id.CompareTo(lit._id);
    }
}
