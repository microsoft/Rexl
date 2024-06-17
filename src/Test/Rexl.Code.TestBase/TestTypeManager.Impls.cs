// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Code;

// These explicit generic tuple classes are used only by tests.
// * They make it easy to populate tuple objects with values, without having to compile and run rexl formulas.
// * They are at the root namespace to avoid cluttering the baselines (just as the generated ones have no ns).
// * They should be used with caution (and not in the real code). For example, they should not be mutated
//   (after initially created and populated), just as the generated ones are expected to never change
//   (after initial field assignment).
// * Note that we don't assign the field values in the ctor for simplicity of code gen, same as for the
//   generated record classes.
// * These are intended to look (almost) identical to the generated tuple classes. The only differences
//   being that these are pre-compiled rather than generated and the type parameter names are different
//   (the generated ones use the same names as the fields). The latter difference is insignificant, since
//   the type parameter names aren't used for anything and are never displayed.

#region Test-only tuple classes for small arity

/// <summary>
/// The class for the arity-one tuple.
/// Use with caution!
/// </summary>
public sealed class TupleImpl<T0> : TupleBase, IEquatable<TupleImpl<T0>>
{
    public T0 _F0;

    public TupleImpl() { }

    protected override int GetHashCodeCore()
    {
        return HashCode.Combine(1, _F0);
    }

    public static bool Equals2(TupleImpl<T0> a, TupleImpl<T0> b)
    {
        if (a == b)
            return true;
        if (a is null)
            return false;
        if (b is null)
            return false;
        return EqualityComparer<T0>.Default.Equals(a._F0, b._F0);
    }

    public bool Equals(TupleImpl<T0> other) => Equals2(this, other);
    protected override bool EqualsCore(object obj) => Equals2(this, obj as TupleImpl<T0>);
}

/// <summary>
/// The class for the arity-two tuple.
/// Use with caution!
/// </summary>
public sealed class TupleImpl<T0, T1> : TupleBase, IEquatable<TupleImpl<T0, T1>>
{
    public T0 _F0;
    public T1 _F1;

    public TupleImpl() { }

    protected override int GetHashCodeCore()
    {
        return HashCode.Combine(2, _F0, _F1);
    }

    public static bool Equals2(TupleImpl<T0, T1> a, TupleImpl<T0, T1> b)
    {
        if (a == b)
            return true;
        if (a is null)
            return false;
        if (b is null)
            return false;
        return
            EqualityComparer<T0>.Default.Equals(a._F0, b._F0) &&
            EqualityComparer<T1>.Default.Equals(a._F1, b._F1);
    }

    public bool Equals(TupleImpl<T0, T1> other) => Equals2(this, other);
    protected override bool EqualsCore(object obj) => Equals2(this, obj as TupleImpl<T0, T1>);
}

/// <summary>
/// The class for the arity-three tuple.
/// Use with caution!
/// </summary>
public sealed class TupleImpl<T0, T1, T2> : TupleBase, IEquatable<TupleImpl<T0, T1, T2>>
{
    public T0 _F0;
    public T1 _F1;
    public T2 _F2;

    public TupleImpl() { }

    protected override int GetHashCodeCore()
    {
        return HashCode.Combine(3, _F0, _F1, _F2);
    }

    public static bool Equals2(TupleImpl<T0, T1, T2> a, TupleImpl<T0, T1, T2> b)
    {
        if (a == b)
            return true;
        if (a is null)
            return false;
        if (b is null)
            return false;
        return
            EqualityComparer<T0>.Default.Equals(a._F0, b._F0) &&
            EqualityComparer<T1>.Default.Equals(a._F1, b._F1) &&
            EqualityComparer<T2>.Default.Equals(a._F2, b._F2);
    }

    public bool Equals(TupleImpl<T0, T1, T2> other) => Equals2(this, other);
    protected override bool EqualsCore(object obj) => Equals2(this, obj as TupleImpl<T0, T1, T2>);
}

/// <summary>
/// The class for the arity-four tuple.
/// Use with caution!
/// </summary>
public sealed class TupleImpl<T0, T1, T2, T3> : TupleBase, IEquatable<TupleImpl<T0, T1, T2, T3>>
{
    public T0 _F0;
    public T1 _F1;
    public T2 _F2;
    public T3 _F3;

    public TupleImpl() { }

    protected override int GetHashCodeCore()
    {
        return HashCode.Combine(4, _F0, _F1, _F2, _F3);
    }

    public static bool Equals2(TupleImpl<T0, T1, T2, T3> a, TupleImpl<T0, T1, T2, T3> b)
    {
        if (a == b)
            return true;
        if (a is null)
            return false;
        if (b is null)
            return false;
        return
            EqualityComparer<T0>.Default.Equals(a._F0, b._F0) &&
            EqualityComparer<T1>.Default.Equals(a._F1, b._F1) &&
            EqualityComparer<T2>.Default.Equals(a._F2, b._F2) &&
            EqualityComparer<T3>.Default.Equals(a._F3, b._F3);
    }

    public bool Equals(TupleImpl<T0, T1, T2, T3> other) => Equals2(this, other);
    protected override bool EqualsCore(object obj) => Equals2(this, obj as TupleImpl<T0, T1, T2, T3>);
}

#endregion Test-only tuple classes for small arity
