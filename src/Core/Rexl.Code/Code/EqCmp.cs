// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using IEC = IEqualityComparer;

/// <summary>
/// Contains an "item type", the instantiated <see cref="EqualityComparer{T}"/> type, its useful method infos,
/// and instances of the associated equality comparers, both case sensitive and case insensitive. When case
/// sensitivity doesn't matter, the case insensitive entry is <c>null</c>.
/// 
/// If the item type is not equatable, all properties other than the item type will be <c>null</c>.
/// 
/// Note that this is a largish <c>struct</c>, so it should generally be passed using <c>ref</c> or <c>in</c>.
/// </summary>
public struct EqCmpInfo
{
    /// <summary>
    /// The system type of the item.
    /// </summary>
    public Type StItem { get; }

    /// <summary>
    /// The system type of the equality comparer(s). This is always <see cref="EqualityComparer{T}"/> where
    /// <c>T</c> is <see cref="StItem"/>.
    /// </summary>
    public Type StEq { get; }

    /// <summary>
    /// The <see cref="MethodInfo"/> for the <see cref="EqualityComparer{T}.Equals(T, T)"/> instance method.
    /// </summary>
    public MethodInfo MethEquals { get; }

    /// <summary>
    /// The <see cref="MethodInfo"/> for the <see cref="EqualityComparer{T}.GetHashCode(T)"/> instance method.
    /// </summary>
    public MethodInfo MethGetHash { get; }

    /// <summary>
    /// The <see cref="MethodInfo"/> for the <see cref="EqualityComparer{T}.get_Default()"/> static method.
    /// </summary>
    public MethodInfo MethGetDefault { get; }

    /// <summary>
    /// The default (case sensitive) equality comparer.
    /// </summary>
    public IEC Eq { get; }

    /// <summary>
    /// The tight/strict equality comparer, which may be null.
    /// </summary>
    public IEC EqTi { get; }

    /// <summary>
    /// The case insensitive equality comparer, which may be null.
    /// </summary>
    public IEC EqCi { get; }

    /// <summary>
    /// The tight/strict case insensitive equality comparer, which may be null.
    /// </summary>
    public IEC EqTiCi { get; }

    private EqCmpInfo(Type stItem, Type stEq, MethodInfo equals, MethodInfo getHash, MethodInfo getDefault,
        IEC eq, IEC eqTi, IEC eqCi, IEC eqTiCi)
    {
        StItem = stItem;
        StEq = stEq;
        MethEquals = equals;
        MethGetHash = getHash;
        MethGetDefault = getDefault;
        Eq = eq;
        EqTi = eqTi;
        EqCi = eqCi;
        EqTiCi = eqTiCi;
    }

    /// <summary>
    /// Create an instance for a non-equatable system type.
    /// </summary>
    public static EqCmpInfo CreateNon(Type stItem)
    {
        Validation.AssertValueOrNull(stItem);
        return new(stItem, null, null, null, null, null, null, null, null);
    }

    /// <summary>
    /// Create an information instance for the given system type and equality comparers.
    /// If <paramref name="eq"/> is <c>null</c>, it is created.
    /// </summary>
    public static EqCmpInfo CreateStd(Type stItem, IEC eqTi, IEC eqCi, IEC eqTiCi)
    {
        Validation.AssertValue(stItem);
        Validation.AssertValueOrNull(eqTi);
        Validation.AssertValueOrNull(eqCi);
        Validation.AssertValueOrNull(eqTiCi);
        Validation.Assert((eqTiCi is null) == (eqTi is null | eqCi is null));

        var stEq = typeof(EqualityComparer<>).MakeGenericType(stItem);
        // Note that these need to be based on stEq, not on an actual type of an eqXxx.
        var equals = stEq.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance,
            new Type[] { stItem, stItem }).VerifyValue();
        var getHash = stEq.GetMethod("GetHashCode", BindingFlags.Public | BindingFlags.Instance,
            new Type[] { stItem }).VerifyValue();
        var getDefault = stEq.GetMethod("get_Default", BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes).VerifyValue();

        var eq = (IEC)getDefault.Invoke(null, null);
        Validation.Assert(eqCi is null || stEq.IsAssignableFrom(eqCi.GetType()));
        Validation.Assert(eqTi is null || stEq.IsAssignableFrom(eqTi.GetType()));
        Validation.Assert(eqTiCi is null || stEq.IsAssignableFrom(eqTiCi.GetType()));

        return new(stItem, stEq, equals, getHash, getDefault, eq, eqTi, eqCi, eqTiCi);
    }
}

/// <summary>
/// Base equality comparer for our custom implementations. Note that this derives from the standard
/// <see cref="EqualityComparer{T}"/>. It's primary purpose is to properly implement the non-generic
/// <see cref="IEqualityComparer.Equals(object?, object?)"/> . We need to re-implement this because
/// the base implementation returns true if the two object references are the same. This is incorrect
/// when doing strict comparison. This also takes the stricter approach of ensuring that the args are
/// of type <see cref="T"/>. For example, the base one will return true when the two args are the same
/// object reference regardless of the type of the reference.
/// </summary>
public abstract class EqCmpBase<T> : EqualityComparer<T>, IEqualityComparer
{
    /// <summary>
    /// Subclasses override to provide correct conversion from object to <see cref="T"/>.
    /// </summary>
    private protected abstract T Convert(object x);

    /// <summary>
    /// Used by subclasses to throw an invalid argument exception when the conversion from object to
    /// <see cref="T"/> fails.
    /// </summary>
    private protected Exception ThrowBad()
    {
        // We invoke the base GetHashCode just for the exception. The extra throw is just insurance.
        ((IEqualityComparer)this).GetHashCode(this);
        throw new InvalidOperationException();
    }

    bool IEqualityComparer.Equals(object? x, object? y) => Equals(Convert(x), Convert(y));
}

/// <summary>
/// Base equality comparer for reference types. See <see cref="EqCmpBase{T}"/> for an explanation.
/// </summary>
public abstract class EqCmpRefBase<T> : EqCmpBase<T>
    where T : class
{
    private protected sealed override T Convert(object x)
    {
        if (x is T res)
            return res;
        if (x is null)
            return null;
        throw ThrowBad();
    }
}

/// <summary>
/// Base equality comparer for req value types. See <see cref="EqCmpBase{T}"/> for an explanation.
/// </summary>
public abstract class EqCmpValReqBase<T> : EqCmpBase<T>
    where T : struct
{
    private protected sealed override T Convert(object x)
    {
        if (x is T res)
            return res;
        throw ThrowBad();
    }
}

/// <summary>
/// Base equality comparer for opt value types. See <see cref="EqCmpBase{T}"/> for an explanation.
/// </summary>
public abstract class EqCmpValOptBase<T> : EqCmpBase<T?>
    where T : struct
{
    private protected sealed override T? Convert(object x)
    {
        if (x is T res)
            return res;
        if (x is null)
            return null;
        throw ThrowBad();
    }
}

/// <summary>
/// The strict string equality comparer.
/// </summary>
public sealed class EqCmpStrTi : EqCmpRefBase<string>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpStrTi Instance = new();

    /// <summary>
    /// This is the cached <see cref="FieldInfo"/> for the <see cref="Instance"/> field.
    /// </summary>
    public static readonly FieldInfo FldInstance = typeof(EqCmpStrTi)
        .GetField(nameof(Instance), BindingFlags.Public | BindingFlags.Static).VerifyValue();

    private EqCmpStrTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(string x, string y)
    {
        if (x is null)
            return false;
        if (y is null)
            return false;
        return EqualityComparer<string>.Default.Equals(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(string x) => EqualityComparer<string>.Default.GetHashCode(x);
}

/// <summary>
/// The case insensitive string equality comparer.
/// </summary>
public sealed class EqCmpStrCi : EqCmpRefBase<string>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpStrCi Instance = new();

    /// <summary>
    /// This is the cached <see cref="FieldInfo"/> for the <see cref="Instance"/> field.
    /// </summary>
    public static readonly FieldInfo FldInstance = typeof(EqCmpStrCi)
        .GetField(nameof(Instance), BindingFlags.Public | BindingFlags.Static).VerifyValue();

    /// <summary>
    /// This is the cached <see cref="MethodInfo"/> for a static method that performs case insensitive
    /// comparison on two string arguments.
    /// </summary>
    public static readonly MethodInfo MethEqualsStatic =
        new Func<string, string, bool>(StrComparer.EqCi).Method;

    /// <summary>
    /// This is the cached <see cref="MethodInfo"/> for a static method that computes a case insensitive
    /// has on a string argument.
    /// </summary>
    public static readonly MethodInfo MethGetHashStatic =
        new Func<string, int>(StrComparer.GetHashCodeCi).Method;

    private EqCmpStrCi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(string x, string y) => StrComparer.EqCi(x, y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(string x) => StrComparer.GetHashCodeCi(x);
}

/// <summary>
/// The strict case insensitive string equality comparer.
/// </summary>
public sealed class EqCmpStrTiCi : EqCmpRefBase<string>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpStrTiCi Instance = new();

    /// <summary>
    /// This is the cached <see cref="FieldInfo"/> for the <see cref="Instance"/> field.
    /// </summary>
    public static readonly FieldInfo FldInstance = typeof(EqCmpStrTiCi)
        .GetField(nameof(Instance), BindingFlags.Public | BindingFlags.Static).VerifyValue();

    private EqCmpStrTiCi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(string x, string y)
    {
        if (x is null)
            return false;
        if (y is null)
            return false;
        return StrComparer.EqCi(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(string x) => StrComparer.GetHashCodeCi(x);
}

/// <summary>
/// The strict double equality comparer.
/// </summary>
public sealed class EqCmpR8ReqTi : EqCmpValReqBase<double>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpR8ReqTi Instance = new();

    private EqCmpR8ReqTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(double x, double y) => x == y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(double x) => x.GetHashCode();
}

/// <summary>
/// The strict double? equality comparer.
/// </summary>
public sealed class EqCmpR8OptTi : EqCmpValOptBase<double>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpR8OptTi Instance = new();

    private EqCmpR8OptTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(double? x, double? y)
    {
        if (x.GetValueOrDefault() != y.GetValueOrDefault())
            return false;
        if (!x.HasValue)
            return false;
        if (!y.HasValue)
            return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(double? x) => x.GetHashCode();
}

/// <summary>
/// The strict float equality comparer.
/// </summary>
public sealed class EqCmpR4ReqTi : EqCmpValReqBase<float>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpR4ReqTi Instance = new();

    private EqCmpR4ReqTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(float x, float y) => x == y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(float x) => x.GetHashCode();
}

/// <summary>
/// The strict float? equality comparer.
/// </summary>
public sealed class EqCmpR4OptTi : EqCmpValOptBase<float>
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpR4OptTi Instance = new();

    private EqCmpR4OptTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(float? x, float? y)
    {
        if (x.GetValueOrDefault() != y.GetValueOrDefault())
            return false;
        if (!x.HasValue)
            return false;
        if (!y.HasValue)
            return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(float? x) => x.GetHashCode();
}

/// <summary>
/// The strict equality comparer for a reference type such as Link. Note that aggregates
/// (record/tuple) use a custom implementation since strictness propagates into the fields.
/// </summary>
public sealed class EqCmpRefTi<T> : EqCmpRefBase<T>
    where T : class
{
    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly EqCmpRefTi<T> Instance = new();

    /// <summary>
    /// This is the cached <see cref="FieldInfo"/> for the <see cref="Instance"/> field.
    /// </summary>
    public static readonly FieldInfo FldInstance = typeof(EqCmpRefTi<T>)
        .GetField(nameof(Instance), BindingFlags.Public | BindingFlags.Static).VerifyValue();

    private EqCmpRefTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(T x, T y)
    {
        if (x is null)
            return false;
        if (y is null)
            return false;
        return EqualityComparer<T>.Default.Equals(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(T x) => EqualityComparer<T>.Default.GetHashCode(x);
}

/// <summary>
/// The tight/strict equality comparer for T? (nullable).
/// </summary>
public sealed class EqCmpValOptTi<T> : EqCmpValOptBase<T>
    where T : struct
{
    public EqCmpValOptTi() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(T? x, T? y)
    {
        if (x.HasValue && y.HasValue)
            return EqualityComparer<T>.Default.Equals(x.GetValueOrDefault(), y.GetValueOrDefault());
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(T? x) => EqualityComparer<T?>.Default.GetHashCode(x);
}
