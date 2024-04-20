// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

using Date = RDate;
using IE = IEnumerable<object>;
using Integer = System.Numerics.BigInteger;
using Time = System.TimeSpan;

/// <summary>
/// Comparer that wraps a comparison delegate.
/// </summary>
internal sealed class Comparer<T> : IComparer<T>
{
    private readonly Func<T, T, int> _cmp;

    public Comparer(Func<T, T, int> cmp)
    {
        Validation.AssertValue(cmp);
        _cmp = cmp;
    }

    public int Compare(T x, T y)
    {
        return _cmp(x, y);
    }
}

/// <summary>
/// Comparer that wraps a comparison delegate, where
/// each object to compare is a pair of items.
/// </summary>
internal sealed class PairComparer<T1, T2> : IComparer<(T1, T2)>
{
    private readonly Func<T1, T2, T1, T2, int> _cmp;

    public PairComparer(Func<T1, T2, T1, T2, int> cmp)
    {
        Validation.AssertValue(cmp);
        _cmp = cmp;
    }

    public int Compare((T1, T2) x, (T1, T2) y)
    {
        return _cmp(x.Item1, x.Item2, y.Item1, y.Item2);
    }
}


/// <summary>
/// Comparer used for GroupBy.
/// REVIEW: Should this derive from <see cref="EqualityComparer{T}"/> rather than just
/// <see cref="IEqualityComparer{T}"/>?
/// </summary>
internal sealed class GroupByComparer<T> : IEqualityComparer<T>
    where T : class
{
    private readonly Func<T, int> _getHash;
    private readonly Func<T, T, bool> _equals;

    public GroupByComparer(Func<T, int> getHash, Func<T, T, bool> equals)
    {
        Validation.AssertValue(getHash);
        Validation.AssertValue(equals);
        _getHash = getHash;
        _equals = equals;
    }

    public bool Equals(T x, T y)
    {
        return _equals(x, y);
    }

    public int GetHashCode(T x)
    {
        return _getHash(x);
    }
}

internal static partial class CodeGenUtil
{
    // REVIEW: It would be nice to not have to create a delegate to get the MethodInfo.

    // Delegate type with last parameter being "out".
    public delegate R FuncOut<T1, T2, R>(T1 t1, out T2 t2);
    public delegate R FuncOut<T1, T2, T3, R>(T1 t1, T2 t2, out T3 t3);
    public delegate R FuncOut<T1, T2, T3, T4, R>(T1 t1, T2 t2, T3 t3, out T4 t4);
    public delegate R FuncOut<T1, T2, T3, T4, T5, R>(T1 t1, T2 t2, T3 t3, T4 t4, out T5 t5);
    public delegate R FuncOut<T1, T2, T3, T4, T5, T6, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, out T6 t6);
    public delegate R FuncOut<T1, T2, T3, T4, T5, T6, T7, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, out T7 t7);

    // Delegate type with last two parameters being "out".
    public delegate R FuncOut2<T1, T2, T3, R>(T1 t1, out T2 t2, out T3 t3);
    public delegate R FuncOut2<T1, T2, T3, T4, R>(T1 t1, T2 t2, out T3 t3, out T4 t4);
    public delegate R FuncOut2<T1, T2, T3, T4, T5, R>(T1 t1, T2 t2, T3 t3, out T4 t4, out T5 t5);
    public delegate R FuncOut2<T1, T2, T3, T4, T5, T6, R>(T1 t1, T2 t2, T3 t3, T4 t4, out T5 t5, out T6 t6);

    // Delegate type with no return type and last two parameters being "out".
    public delegate void ActOut2<T1, T2, T3, T4>(T1 t1, T2 t2, out T3 t3, out T4 t4);
    public delegate void ActOut2<T1, T2, T3, T4, T5>(T1 t1, T2 t2, T3 t3, out T4 t4, out T5 t5);
    public delegate void ActOut2<T1, T2, T3, T4, T5, T6>(T1 t1, T2 t2, T3 t3, T4 t4, out T5 t5, out T6 t6);

    // Non-homogeneous.
    public static MethodInfo GetMethodInfo0<R>(Func<R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo0<R>(Func<R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo1<T1, R>(Func<T1, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo1<T1, R>(Func<T1, R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo2<T1, T2, R>(Func<T1, T2, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo2<T1, T2, R>(Func<T1, T2, R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo3<T1, T2, T3, R>(Func<T1, T2, T3, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo3<T1, T2, T3, R>(Func<T1, T2, T3, R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo4<T1, T2, T3, T4, R>(Func<T1, T2, T3, T4, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo4<T1, T2, T3, T4, R>(Func<T1, T2, T3, T4, R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo5<T1, T2, T3, T4, T5, R>(Func<T1, T2, T3, T4, T5, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo5<T1, T2, T3, T4, T5, R>(Func<T1, T2, T3, T4, T5, R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo6<T1, T2, T3, T4, T5, T6, R>(Func<T1, T2, T3, T4, T5, T6, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo6<T1, T2, T3, T4, T5, T6, R>(Func<T1, T2, T3, T4, T5, T6, R> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo7<T1, T2, T3, T4, T5, T6, T7, R>(Func<T1, T2, T3, T4, T5, T6, T7, R> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo7<T1, T2, T3, T4, T5, T6, T7, R>(Func<T1, T2, T3, T4, T5, T6, T7, R> fn, params Type[] sts) { return Make(fn.Method, sts); }

    // Homogeneous.
    public static MethodInfo GetMethodInfo1<T>(Func<T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo1<T>(Func<T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo2<T>(Func<T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo2<T>(Func<T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo3<T>(Func<T, T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo3<T>(Func<T, T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo4<T>(Func<T, T, T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo4<T>(Func<T, T, T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo5<T>(Func<T, T, T, T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo5<T>(Func<T, T, T, T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo6<T>(Func<T, T, T, T, T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo6<T>(Func<T, T, T, T, T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo7<T>(Func<T, T, T, T, T, T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo7<T>(Func<T, T, T, T, T, T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }
    public static MethodInfo GetMethodInfo8<T>(Func<T, T, T, T, T, T, T, T, T> fn) { return fn.Method; }
    public static MethodInfo GetMethodInfo8<T>(Func<T, T, T, T, T, T, T, T, T> fn, params Type[] sts) { return Make(fn.Method, sts); }

    private static MethodInfo Make(MethodInfo meth, params Type[] sts)
    {
        Validation.AssertValue(meth);
        if (Util.Size(sts) == 0)
            return meth;
        return meth.GetGenericMethodDefinition().MakeGenericMethod(sts);
    }

    public const string SysValTupItem1 = nameof(ValueTuple<int, int>.Item1);
    public const string SysValTupItem2 = nameof(ValueTuple<int, int>.Item2);

    #region Cached method infos

    private static MethodInfo _methEqR8;
    public static MethodInfo R8Eq => _methEqR8 ??= GetMethodInfo2<double, double, bool>(NumUtil.Eq);

    private static MethodInfo _methLtR8;
    public static MethodInfo R8Lt => _methLtR8 ??= GetMethodInfo2<double, double, bool>(NumUtil.Lt);

    private static MethodInfo _methGtR8;
    public static MethodInfo R8Gt => _methGtR8 ??= GetMethodInfo2<double, double, bool>(NumUtil.Gt);

    private static MethodInfo _methLeR8;
    public static MethodInfo R8Le => _methLeR8 ??= GetMethodInfo2<double, double, bool>(NumUtil.Le);

    private static MethodInfo _methGeR8;
    public static MethodInfo R8Ge => _methGeR8 ??= GetMethodInfo2<double, double, bool>(NumUtil.Ge);

    private static MethodInfo _methEqR4;
    public static MethodInfo R4Eq => _methEqR4 ??= GetMethodInfo2<float, float, bool>(NumUtil.Eq);

    private static MethodInfo _methLtR4;
    public static MethodInfo R4Lt => _methLtR4 ??= GetMethodInfo2<float, float, bool>(NumUtil.Lt);

    private static MethodInfo _methGtR4;
    public static MethodInfo R4Gt => _methGtR4 ??= GetMethodInfo2<float, float, bool>(NumUtil.Gt);

    private static MethodInfo _methLeR4;
    public static MethodInfo R4Le => _methLeR4 ??= GetMethodInfo2<float, float, bool>(NumUtil.Le);

    private static MethodInfo _methGeR4;
    public static MethodInfo R4Ge => _methGeR4 ??= GetMethodInfo2<float, float, bool>(NumUtil.Ge);

    private static MethodInfo _methStrEq; public static MethodInfo StrEq => _methStrEq ??= GetMethodInfo2<string, string, bool>(string.Equals);
    private static MethodInfo _methStrLt; public static MethodInfo StrLt => _methStrLt ??= GetMethodInfo2<string, string, bool>(StrComparer.Lt);
    private static MethodInfo _methStrLe; public static MethodInfo StrLe => _methStrLe ??= GetMethodInfo2<string, string, bool>(StrComparer.Le);
    private static MethodInfo _methStrGt; public static MethodInfo StrGt => _methStrGt ??= GetMethodInfo2<string, string, bool>(StrComparer.Gt);
    private static MethodInfo _methStrGe; public static MethodInfo StrGe => _methStrGe ??= GetMethodInfo2<string, string, bool>(StrComparer.Ge);

    private static MethodInfo _methStrEqCi; public static MethodInfo StrEqCi => _methStrEqCi ??= GetMethodInfo2<string, string, bool>(StrComparer.EqCi);
    private static MethodInfo _methStrLtCi; public static MethodInfo StrLtCi => _methStrLtCi ??= GetMethodInfo2<string, string, bool>(StrComparer.LtCi);
    private static MethodInfo _methStrLeCi; public static MethodInfo StrLeCi => _methStrLeCi ??= GetMethodInfo2<string, string, bool>(StrComparer.LeCi);
    private static MethodInfo _methStrGtCi; public static MethodInfo StrGtCi => _methStrGtCi ??= GetMethodInfo2<string, string, bool>(StrComparer.GtCi);
    private static MethodInfo _methStrGeCi; public static MethodInfo StrGeCi => _methStrGeCi ??= GetMethodInfo2<string, string, bool>(StrComparer.GeCi);

    private static MethodInfo _methStrEqTi; public static MethodInfo StrEqTi => _methStrEqTi ??= GetMethodInfo2<string, string, bool>(StrComparer.EqTi);
    private static MethodInfo _methStrLtTi; public static MethodInfo StrLtTi => _methStrLtTi ??= GetMethodInfo2<string, string, bool>(StrComparer.LtTi);
    private static MethodInfo _methStrLeTi; public static MethodInfo StrLeTi => _methStrLeTi ??= GetMethodInfo2<string, string, bool>(StrComparer.LeTi);
    private static MethodInfo _methStrGtTi; public static MethodInfo StrGtTi => _methStrGtTi ??= GetMethodInfo2<string, string, bool>(StrComparer.GtTi);
    private static MethodInfo _methStrGeTi; public static MethodInfo StrGeTi => _methStrGeTi ??= GetMethodInfo2<string, string, bool>(StrComparer.GeTi);

    private static MethodInfo _methStrEqCiTi; public static MethodInfo StrEqCiTi => _methStrEqCiTi ??= GetMethodInfo2<string, string, bool>(StrComparer.EqTiCi);
    private static MethodInfo _methStrLtCiTi; public static MethodInfo StrLtCiTi => _methStrLtCiTi ??= GetMethodInfo2<string, string, bool>(StrComparer.LtTiCi);
    private static MethodInfo _methStrLeCiTi; public static MethodInfo StrLeCiTi => _methStrLeCiTi ??= GetMethodInfo2<string, string, bool>(StrComparer.LeTiCi);
    private static MethodInfo _methStrGtCiTi; public static MethodInfo StrGtCiTi => _methStrGtCiTi ??= GetMethodInfo2<string, string, bool>(StrComparer.GtTiCi);
    private static MethodInfo _methStrGeCiTi; public static MethodInfo StrGeCiTi => _methStrGeCiTi ??= GetMethodInfo2<string, string, bool>(StrComparer.GeTiCi);

    public static MethodInfo GetStrCmpOp(CompareOp op)
    {
        if (op.IsStrict)
        {
            if (op.IsCi)
            {
                switch (op.Root)
                {
                case CompareRoot.Equal: return StrEqCiTi;
                case CompareRoot.Less: return StrLtCiTi;
                case CompareRoot.LessEqual: return StrLeCiTi;
                case CompareRoot.Greater: return StrGtCiTi;
                case CompareRoot.GreaterEqual: return StrGeCiTi;
                }
            }
            else
            {
                switch (op.Root)
                {
                case CompareRoot.Equal: return StrEqTi;
                case CompareRoot.Less: return StrLtTi;
                case CompareRoot.LessEqual: return StrLeTi;
                case CompareRoot.Greater: return StrGtTi;
                case CompareRoot.GreaterEqual: return StrGeTi;
                }
            }
        }
        else
        {
            if (op.IsCi)
            {
                switch (op.Root)
                {
                case CompareRoot.Equal: return StrEqCi;
                case CompareRoot.Less: return StrLtCi;
                case CompareRoot.LessEqual: return StrLeCi;
                case CompareRoot.Greater: return StrGtCi;
                case CompareRoot.GreaterEqual: return StrGeCi;
                }
            }
            else
            {
                switch (op.Root)
                {
                case CompareRoot.Equal: return StrEq;
                case CompareRoot.Less: return StrLt;
                case CompareRoot.LessEqual: return StrLe;
                case CompareRoot.Greater: return StrGt;
                case CompareRoot.GreaterEqual: return StrGe;
                }
            }
        }

        throw Validation.BugExcept();
    }

    private static MethodInfo _methPowDbl;
    public static MethodInfo PowDbl => _methPowDbl ??= GetMethodInfo2<double>(Math.Pow);

    private static MethodInfo _methPowFlt;
    public static MethodInfo PowFlt => _methPowFlt ??= GetMethodInfo2<float>(PowFltImpl);

    private static MethodInfo _methPowI8;
    public static MethodInfo PowI8 => _methPowI8 ??= GetMethodInfo2<long, ulong, long>(NumUtil.IntPow);

    private static MethodInfo _methPowU8;
    public static MethodInfo PowU8 => _methPowU8 ??= GetMethodInfo2<ulong, ulong, ulong>(NumUtil.IntPow);

    private static MethodInfo _methStrConcat2;
    public static MethodInfo StrConcat2 => _methStrConcat2 ??= GetMethodInfo2<string>(string.Concat);

    private static MethodInfo _methStrConcat3;
    public static MethodInfo StrConcat3 => _methStrConcat3 ??= GetMethodInfo3<string>(string.Concat);

    private static MethodInfo _methStrConcat4;
    public static MethodInfo StrConcat4 => _methStrConcat4 ??= GetMethodInfo4<string>(string.Concat);

    private static MethodInfo _methStrConcatArr;
    public static MethodInfo StrConcatArr => _methStrConcatArr ??= GetMethodInfo1<string[], string>(string.Concat);

    private static MethodInfo _methStrHas;
    public static MethodInfo StrHas => _methStrHas ??= GetMethodInfo2<string, string, bool>(StrHasImpl);

    private static MethodInfo _methStrHasCi;
    public static MethodInfo StrHasCi => _methStrHasCi ??= GetMethodInfo2<string, string, bool>(StrHasCiImpl);

    private static MethodInfo _methSeqConcat2;
    public static MethodInfo SeqConcat2(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methSeqConcat2 ??= GetMethodInfo2<IE, IE, IE>(SeqConcat).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methSeqConcat3;
    public static MethodInfo SeqConcat3(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methSeqConcat3 ??= GetMethodInfo3<IE, IE, IE, IE>(SeqConcat).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methSeqConcat4;
    public static MethodInfo SeqConcat4(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methSeqConcat4 ??= GetMethodInfo4<IE, IE, IE, IE, IE>(SeqConcat).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methSeqConcatArr;
    public static MethodInfo SeqConcatArr(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methSeqConcatArr ??= GetMethodInfo1<IE[], IE>(SeqConcat).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methEnumerableToArray;
    public static MethodInfo EnumerableToArray(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methEnumerableToArray ??= GetMethodInfo1<IE, object[]>(EnumerableToArray).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methEnumerableToCaching;
    public static MethodInfo EnumerableToCaching(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methEnumerableToCaching ??= GetMethodInfo1<IE>(EnumerableToCaching).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    /// <summary>
    /// This is distinct from the non-forced one mostly so it shows up differently in IL.
    /// </summary>
    private static MethodInfo _methEnumerableToCachingForced;
    public static MethodInfo EnumerableToCachingForced(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methEnumerableToCachingForced ??= GetMethodInfo1<IE>(EnumerableToCachingForced).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methEnumerableToPinging;
    public static MethodInfo EnumerableToPinging(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methEnumerableToPinging ??= GetMethodInfo3<IE, ExecCtx, int, IE>(EnumerableToPinging).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methWrapWithCounter;
    public static MethodInfo WrapWithCounter(Type stItemSrc, Type stItemDst)
    {
        Validation.AssertValue(stItemSrc);
        Validation.AssertValue(stItemDst);
        var meth = _methWrapWithCounter ??= GetMethodInfo2<IE, IE, IE>(WrapWithCounter).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItemSrc, stItemDst);
    }

    private static MethodInfo _methWrapIndPairs;
    public static MethodInfo WrapIndPairs(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methWrapIndPairs ??= GetMethodInfo1<IE, IEnumerable<(object, long)>>(WrapIndPairs).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methUnwrapIndPairsToItems;
    public static MethodInfo UnwrapIndPairsToItems(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methUnwrapIndPairsToItems ??= GetMethodInfo1<IEnumerable<(object, long)>, IE>(UnwrapIndPairsToItems).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methSort;
    private static MethodInfo _methSortNoKey;
    private static MethodInfo _methIndSort;
    private static MethodInfo _methIndSortNoKey;

    /// <summary>
    /// Gets the appropriate sort method according to the given parameters. The method accepts the
    /// source sequence, (optional) key selector, and comparison delegate that compares the keys.
    /// If <paramref name="stKey"/> is null, the returned sort method does not accept a key selector
    /// and the source items are compared directly. If <paramref name="isIndexed"/> is set, there
    /// are two cases: without a key selector, the comparison delegate accepts both the items and
    /// the items' indices; with a key selector, the key selector accepts the item and its index.
    /// </summary>
    public static MethodInfo GetSortMeth(bool isIndexed, Type stItem, Type stKey = null)
    {
        Validation.AssertValue(stItem);
        Validation.AssertValueOrNull(stKey);

        MethodInfo meth;
        if (stKey == null)
        {
            meth = isIndexed ?
                _methIndSortNoKey ??= GetMethodInfo2<IEnumerable<int>, Func<int, long, int, long, int>, IEnumerable<int>>(IndSortNoKey) :
                _methSortNoKey ??= GetMethodInfo2<IEnumerable<int>, Func<int, int, int>, IEnumerable<int>>(SortNoKey);

            return meth.GetGenericMethodDefinition().MakeGenericMethod(stItem);
        }

        meth = isIndexed ?
            _methIndSort ??= GetMethodInfo3<IEnumerable<int>, Func<int, long, int>, Func<int, int, int>, IEnumerable<int>>(IndSort) :
            _methSort ??= GetMethodInfo3<IEnumerable<int>, Func<int, int>, Func<int, int, int>, IEnumerable<int>>(Sort);

        return meth.GetGenericMethodDefinition().MakeGenericMethod(stItem, stKey);
    }

    private static MethodInfo _methStrCmpCi;
    private static MethodInfo _methStrCmpDecCi;
    private static readonly ConcurrentDictionary<(Type, bool dec), MethodInfo> _methCmps = new ConcurrentDictionary<(Type, bool), MethodInfo>();

    public static string GetCompareMethName(bool isDown)
    {
        if (isDown)
            return nameof(Cmp.CompareDec);
        return nameof(Cmp.Compare);
    }

    /// <summary>
    /// Tries to get a compare method for the given type. This returns true and populates <paramref name="meth"/>
    /// with the method only if <paramref name="stKey"/> is a comparable type. Note this will return false for
    /// required numeric types less than 8 bytes, since those should be compared via IL subtraction instead. The
    /// caller should handle that case separately.
    /// </summary>
    public static bool TryGetCompareMeth(Type stKey, bool ci, bool isDown, out MethodInfo meth)
    {
        Validation.AssertValue(stKey);

        if (ci && stKey == typeof(string))
        {
            meth = isDown ?
                _methStrCmpDecCi ??= GetMethodInfo2<string, string, int>(Cmp.CompareCiDec) :
                _methStrCmpCi ??= GetMethodInfo2<string, string, int>(Cmp.CompareCi);
        }
        else if (!_methCmps.TryGetValue((stKey, isDown), out meth))
        {
            // A value of null in the dictionary means there is no compare method for this type.
            meth = typeof(Cmp).GetMethod(GetCompareMethName(isDown), new Type[] { stKey, stKey });
            _methCmps.GetOrAdd((stKey, isDown), meth);
        }

        return meth != null;
    }

    // REVIEW: Since this cache is keyed by MethodInfo, it could be leveraged to cache other delegates as well.
    private static readonly ConcurrentDictionary<MethodInfo, Delegate> _fnCmps = new ConcurrentDictionary<MethodInfo, Delegate>();

    /// <summary>
    /// Tries to return a delegate corresponding to the method info returned from
    /// <see cref="TryGetCompareMeth"/> if it exists. Otherwise returns false.
    /// </summary>
    public static bool TryGetCompareDel(Type stKey, bool ci, bool isDown, out Delegate fn, out Type stFn)
    {
        Validation.AssertValue(stKey);

        if (!TryGetCompareMeth(stKey, ci, isDown, out var meth))
        {
            fn = null;
            stFn = null;
            return false;
        }

        stFn = typeof(Func<,,>).MakeGenericType(stKey, stKey, typeof(int));
        if (!_fnCmps.TryGetValue(meth, out fn))
        {
            // REVIEW: Instance delegate invocation would be faster
            // than static invocation.
            fn = _fnCmps.GetOrAdd(meth, Delegate.CreateDelegate(stFn, meth));
        }

        Validation.Assert(stFn.IsAssignableFrom(fn.GetType()));
        return true;
    }

    private static MethodInfo _methIdentity;
    public static MethodInfo Identity(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methIdentity ??= GetMethodInfo2<object>(IdentityImpl).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methIn;
    private static MethodInfo _methInEqCmp;
    public static MethodInfo InEnumerable(Type stItem, bool withEqCmp)
    {
        Validation.AssertValue(stItem);

        MethodInfo meth;
        if (withEqCmp)
        {
            meth = _methInEqCmp ??=
                GetMethodInfo5<string, IEnumerable<string>, EqualityComparer<string>, ExecCtx, int, bool>(InImpl)
                    .GetGenericMethodDefinition();
        }
        else
        {
            meth = _methIn ??=
                GetMethodInfo4<string, IEnumerable<string>, ExecCtx, int, bool>(InImpl)
                    .GetGenericMethodDefinition();
        }
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methDateEq;
    public static MethodInfo DateEq => _methDateEq ??= typeof(Date).GetMethod("op_Equality", new[] { typeof(Date), typeof(Date) }).VerifyValue();

    private static MethodInfo _methDateNe;
    public static MethodInfo DateNe => _methDateNe ??= typeof(Date).GetMethod("op_Inequality", new[] { typeof(Date), typeof(Date) }).VerifyValue();

    private static MethodInfo _methDateLt;
    public static MethodInfo DateLt => _methDateLt ??= typeof(Date).GetMethod("op_LessThan", new[] { typeof(Date), typeof(Date) }).VerifyValue();

    private static MethodInfo _methDateLe;
    public static MethodInfo DateLe => _methDateLe ??= typeof(Date).GetMethod("op_LessThanOrEqual", new[] { typeof(Date), typeof(Date) }).VerifyValue();

    private static MethodInfo _methDateGe;
    public static MethodInfo DateGe => _methDateGe ??= typeof(Date).GetMethod("op_GreaterThanOrEqual", new[] { typeof(Date), typeof(Date) }).VerifyValue();

    private static MethodInfo _methDateGt;
    public static MethodInfo DateGt => _methDateGt ??= typeof(Date).GetMethod("op_GreaterThan", new[] { typeof(Date), typeof(Date) }).VerifyValue();

    private static MethodInfo _methTimeEq;
    public static MethodInfo TimeEq => _methTimeEq ??= typeof(Time).GetMethod("op_Equality", new[] { typeof(Time), typeof(Time) }).VerifyValue();

    private static MethodInfo _methTimeNe;
    public static MethodInfo TimeNe => _methTimeNe ??= typeof(Time).GetMethod("op_Inequality", new[] { typeof(Time), typeof(Time) }).VerifyValue();

    private static MethodInfo _methTimeLt;
    public static MethodInfo TimeLt => _methTimeLt ??= typeof(Time).GetMethod("op_LessThan", new[] { typeof(Time), typeof(Time) }).VerifyValue();

    private static MethodInfo _methTimeLe;
    public static MethodInfo TimeLe => _methTimeLe ??= typeof(Time).GetMethod("op_LessThanOrEqual", new[] { typeof(Time), typeof(Time) }).VerifyValue();

    private static MethodInfo _methTimeGe;
    public static MethodInfo TimeGe => _methTimeGe ??= typeof(Time).GetMethod("op_GreaterThanOrEqual", new[] { typeof(Time), typeof(Time) }).VerifyValue();

    private static MethodInfo _methTimeGt;
    public static MethodInfo TimeGt => _methTimeGt ??= typeof(Time).GetMethod("op_GreaterThan", new[] { typeof(Time), typeof(Time) }).VerifyValue();

    private static MethodInfo _methIntEq;
    public static MethodInfo IntEq => _methIntEq ??= typeof(Integer).GetMethod("op_Equality", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntNe;
    public static MethodInfo IntNe => _methIntNe ??= typeof(Integer).GetMethod("op_Inequality", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntLt;
    public static MethodInfo IntLt => _methIntLt ??= typeof(Integer).GetMethod("op_LessThan", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntLe;
    public static MethodInfo IntLe => _methIntLe ??= typeof(Integer).GetMethod("op_LessThanOrEqual", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntGe;
    public static MethodInfo IntGe => _methIntGe ??= typeof(Integer).GetMethod("op_GreaterThanOrEqual", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntGt;
    public static MethodInfo IntGt => _methIntGt ??= typeof(Integer).GetMethod("op_GreaterThan", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntNeg;
    public static MethodInfo IntNeg => _methIntNeg ??= typeof(Integer).GetMethod("op_UnaryNegation", new[] { typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntNot;
    public static MethodInfo IntNot => _methIntNot ??= typeof(Integer).GetMethod("op_OnesComplement", new[] { typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntAdd;
    public static MethodInfo IntAdd => _methIntAdd ??= typeof(Integer).GetMethod("op_Addition", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntSub;
    public static MethodInfo IntSub => _methIntSub ??= typeof(Integer).GetMethod("op_Subtraction", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntMul;
    public static MethodInfo IntMul => _methIntMul ??= typeof(Integer).GetMethod("op_Multiply", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntOr;
    public static MethodInfo IntOr => _methIntOr ??= typeof(Integer).GetMethod("op_BitwiseOr", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntXor;
    public static MethodInfo IntXor => _methIntXor ??= typeof(Integer).GetMethod("op_ExclusiveOr", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntAnd;
    public static MethodInfo IntAnd => _methIntAnd ??= typeof(Integer).GetMethod("op_BitwiseAnd", new[] { typeof(Integer), typeof(Integer) }).VerifyValue();

    private static MethodInfo _methIntShl;
    public static MethodInfo IntShl => _methIntShl ??= typeof(Integer).GetMethod("op_LeftShift", new[] { typeof(Integer), typeof(int) }).VerifyValue();

    private static MethodInfo _methIntShr;
    public static MethodInfo IntShr => _methIntShr ??= typeof(Integer).GetMethod("op_RightShift", new[] { typeof(Integer), typeof(int) }).VerifyValue();

    private static MethodInfo _methClipShift;
    public static MethodInfo ClipShift => _methClipShift ??= typeof(BindUtil).GetMethod("ClipShift", new[] { typeof(long) }).VerifyValue();

    private static MethodInfo _methIntToR8;
    public static MethodInfo IntToR8 => _methIntToR8 ??= GetMethodInfo1<Integer, double>(NumUtil.ToR8);

    private static MethodInfo _methIntToR4;
    public static MethodInfo IntToR4 => _methIntToR4 ??= GetMethodInfo1<Integer, float>(NumUtil.ToR4);

    private static ConstructorInfo _ctorIntI4;
    public static ConstructorInfo CtorIntFromI4 => _ctorIntI4 ??= typeof(Integer).GetConstructor(new[] { typeof(int) }).VerifyValue();

    private static ConstructorInfo _ctorIntI8;
    public static ConstructorInfo CtorIntFromI8 => _ctorIntI8 ??= typeof(Integer).GetConstructor(new[] { typeof(long) }).VerifyValue();

    private static ConstructorInfo _ctorIntU4;
    public static ConstructorInfo CtorIntFromU4 => _ctorIntU4 ??= typeof(Integer).GetConstructor(new[] { typeof(uint) }).VerifyValue();

    private static ConstructorInfo _ctorIntU8;
    public static ConstructorInfo CtorIntFromU8 => _ctorIntU8 ??= typeof(Integer).GetConstructor(new[] { typeof(ulong) }).VerifyValue();

    private static ConstructorInfo _ctorIntR4;
    public static ConstructorInfo CtorIntFromR4 => _ctorIntR4 ??= typeof(Integer).GetConstructor(new[] { typeof(float) }).VerifyValue();

    private static ConstructorInfo _ctorIntR8;
    public static ConstructorInfo CtorIntFromR8 => _ctorIntR8 ??= typeof(Integer).GetConstructor(new[] { typeof(double) }).VerifyValue();

    private static MethodInfo _methR8IsFinite;
    public static MethodInfo R8IsFinite => _methR8IsFinite ??= GetMethodInfo1<double, bool>(NumUtil.IsFinite);

    private static MethodInfo _methR4IsFinite;
    public static MethodInfo R4IsFinite => _methR4IsFinite ??= GetMethodInfo1<float, bool>(NumUtil.IsFinite);

    private static MethodInfo _methR8ModToI8;
    public static MethodInfo R8ModToI8 => _methR8ModToI8 ??= GetMethodInfo1<double, long>(NumUtil.ModToI8);

    private static MethodInfo _methR4ModToI8;
    public static MethodInfo R4ModToI8 => _methR4ModToI8 ??= GetMethodInfo1<float, long>(NumUtil.ModToI8);

    private static MethodInfo _methR8ToDateTicks;
    public static MethodInfo R8ToDateTicks => _methR8ToDateTicks ??= GetMethodInfo1<double, long>(NumUtil.ToDateTicks);

    private static MethodInfo _methR4ToDateTicks;
    public static MethodInfo R4ToDateTicks => _methR4ToDateTicks ??= GetMethodInfo1<float, long>(NumUtil.ToDateTicks);

    private static MethodInfo _methR8TryToI8;
    public static MethodInfo R8TryToI8 => _methR8TryToI8 ??= new FuncOut<double, long, bool>(NumUtil.TryToI8).Method;

    private static MethodInfo _methR4TryToI8;
    public static MethodInfo R4TryToI8 => _methR4TryToI8 ??= new FuncOut<float, long, bool>(NumUtil.TryToI8).Method;

    private static MethodInfo _methR8TryToU8;
    public static MethodInfo R8TryToU8 => _methR8TryToU8 ??= new FuncOut<double, ulong, bool>(NumUtil.TryToU8).Method;

    private static MethodInfo _methR4TryToU8;
    public static MethodInfo R4TryToU8 => _methR4TryToU8 ??= new FuncOut<float, ulong, bool>(NumUtil.TryToU8).Method;

    private static MethodInfo _methIntTryToI8;
    public static MethodInfo IntTryToI8 => _methIntTryToI8 ??= new FuncOut<Integer, long, long, long, bool>(CastUtil.TryToI8).Method;

    private static MethodInfo _methIntTryToU8;
    public static MethodInfo IntTryToU8 => _methIntTryToU8 ??= new FuncOut<Integer, ulong, ulong, bool>(CastUtil.TryToU8).Method;

    private static MethodInfo _methStrTryParseI8;
    public static MethodInfo StrTryParseI8 => _methStrTryParseI8 ??= new FuncOut<string, long, bool>(CastUtil.TryParseI8).Method;

    private static MethodInfo _methStrTryParseU8;
    public static MethodInfo StrTryParseU8 => _methStrTryParseU8 ??= new FuncOut<string, ulong, bool>(CastUtil.TryParseU8).Method;

    private static MethodInfo _methStrTryParseInt;
    public static MethodInfo StrTryParseInt => _methStrTryParseInt ??= new FuncOut<string, Integer, bool>(CastUtil.TryParseInt).Method;

    private static MethodInfo _methStrTryParseR8;
    public static MethodInfo StrTryParseR8 => _methStrTryParseR8 ??= new FuncOut<string, double, bool>(CastUtil.TryParseR8).Method;

    private static MethodInfo _methR4Min;
    public static MethodInfo R4Min => _methR4Min ??= GetMethodInfo2<float, float, float>(Math.Min);

    private static MethodInfo _methR8Min;
    public static MethodInfo R8Min => _methR8Min ??= GetMethodInfo2<double, double, double>(Math.Min);

    private static MethodInfo _methIntMin;
    public static MethodInfo IntMin => _methIntMin ??= GetMethodInfo2<Integer, Integer, Integer>(Integer.Min);

    private static MethodInfo _methStrMin;
    public static MethodInfo StrMin => _methStrMin ??= GetMethodInfo2<string, string, string>(StrComparer.Min);

    private static MethodInfo _methR4Max;
    public static MethodInfo R4Max => _methR4Max ??= GetMethodInfo2<float, float, float>(Math.Max);

    private static MethodInfo _methR8Max;
    public static MethodInfo R8Max => _methR8Max ??= GetMethodInfo2<double, double, double>(Math.Max);

    private static MethodInfo _methIntMax;
    public static MethodInfo IntMax => _methIntMax ??= GetMethodInfo2<Integer, Integer, Integer>(Integer.Max);

    private static MethodInfo _methStrMax;
    public static MethodInfo StrMax => _methStrMax ??= GetMethodInfo2<string, string, string>(StrComparer.Max);

    private static MethodInfo _methDateTimeAdd;
    public static MethodInfo DateTimeAdd => _methDateTimeAdd ??= GetMethodInfo2<Date, Time, Date>(Date.Add);

    private static MethodInfo _methDateSubTime;
    public static MethodInfo DateSubTime => _methDateSubTime ??= GetMethodInfo2<Date, Time, Date>(Date.Sub);

    private static MethodInfo _methDateSubDate;
    public static MethodInfo DateSubDate => _methDateSubDate ??= GetMethodInfo2<Date, Date, Time>(Date.Sub);

    private static MethodInfo _methTimeAddTime;
    public static MethodInfo TimeAddTime => _methTimeAddTime ??= GetMethodInfo2<Time, Time, Time>(Add);

    private static MethodInfo _methTimeSubTime;
    public static MethodInfo TimeSubTime => _methTimeSubTime ??= GetMethodInfo2<Time, Time, Time>(Sub);

    private static MethodInfo _methTimeMulR8;
    public static MethodInfo TimeMulR8 => _methTimeMulR8 ??= GetMethodInfo2<Time, double, Time>(Mul);

    private static MethodInfo _methTimeMulI8;
    public static MethodInfo TimeMulI8 => _methTimeMulI8 ??= GetMethodInfo2<Time, long, Time>(Mul);

    private static MethodInfo _methTimeDivR8;
    public static MethodInfo TimeDivR8 => _methTimeDivR8 ??= GetMethodInfo2<Time, double, Time>(Div);

    private static MethodInfo _methTimeDivTime;
    public static MethodInfo TimeDivTime => _methTimeDivTime ??= GetMethodInfo2<Time, Time, double>(Div);

    private static MethodInfo _methTimeIntDivI8;
    public static MethodInfo TimeIntDivI8 => _methTimeIntDivI8 ??= GetMethodInfo2<Time, long, Time>(IntDiv);

    private static MethodInfo _methTimeIntDivTime;
    public static MethodInfo TimeIntDivTime => _methTimeIntDivTime ??= GetMethodInfo2<Time, Time, long>(IntDiv);

    private static MethodInfo _methTimeIntModTime;
    public static MethodInfo TimeIntModTime => _methTimeIntModTime ??= GetMethodInfo2<Time, Time, Time>(IntMod);

    private static MethodInfo _methTextIndex;
    public static MethodInfo TextIndex => _methTextIndex ??= GetMethodInfo3<string, long, IndexFlags, ushort>(IndexText);

    private static MethodInfo _methTextSlice;
    public static MethodInfo TextSlice => _methTextSlice ??= GetMethodInfo2<string, SliceItem, string>(SliceText);

    private static MethodInfo _methTenGetCount;
    public static MethodInfo TenGetCount => _methTenGetCount ??= GetMethodInfo1<Tensor, long>(Tensor._GetCountStatic);

    private static MethodInfo _methTenGetShape;
    public static MethodInfo TenGetShape => _methTenGetShape ??= GetMethodInfo1<Tensor, Shape>(Tensor._GetShapeStatic);

    private static MethodInfo _methTenShapeItem;
    public static MethodInfo TenShapeItem => _methTenShapeItem ??= typeof(Shape).GetProperty("Item", new[] { typeof(int) }).GetGetMethod();

    private static MethodInfo _methTenGetFirst;
    public static MethodInfo TenGetFirst => _methTenGetFirst ??= GetMethodInfo1<Tensor<byte>, byte>(Tensor._GetFirstStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenGetAt1;
    public static MethodInfo TenGetItem1 => _methTenGetAt1 ??= GetMethodInfo2<Tensor<byte>, long, byte>(Tensor._GetAtStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenGetAt2;
    public static MethodInfo TenGetItem2 => _methTenGetAt2 ??= GetMethodInfo3<Tensor<byte>, long, long, byte>(Tensor._GetAtStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenGetAt3;
    public static MethodInfo TenGetItem3 => _methTenGetAt3 ??= GetMethodInfo4<Tensor<byte>, long, long, long, byte>(Tensor._GetAtStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenGetAt4;
    public static MethodInfo TenGetItem4 => _methTenGetAt4 ??= GetMethodInfo5<Tensor<byte>, long, long, long, long, byte>(Tensor._GetAtStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenGetAtGen;
    public static MethodInfo TenGetItemArr => _methTenGetAtGen ??= GetMethodInfo2<Tensor<byte>, long[], byte>(Tensor._GetAtStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenSlice1;
    public static MethodInfo TenSlice1 => _methTenSlice1 ??= GetMethodInfo3<Tensor<byte>, byte, SliceItem, Tensor<byte>>(Tensor._GetSliceStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenSlice2;
    public static MethodInfo TenSlice2 => _methTenSlice2 ??= GetMethodInfo4<Tensor<byte>, byte, SliceItem, SliceItem, Tensor<byte>>(Tensor._GetSliceStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenSlice3;
    public static MethodInfo TenSlice3 => _methTenSlice3 ??= GetMethodInfo5<Tensor<byte>, byte, SliceItem, SliceItem, SliceItem, Tensor<byte>>(Tensor._GetSliceStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenSlice4;
    public static MethodInfo TenSlice4 => _methTenSlice4 ??= GetMethodInfo6<Tensor<byte>, byte, SliceItem, SliceItem, SliceItem, SliceItem, Tensor<byte>>(Tensor._GetSliceStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenSlice5;
    public static MethodInfo TenSlice5 => _methTenSlice5 ??= GetMethodInfo7<Tensor<byte>, byte, SliceItem, SliceItem, SliceItem, SliceItem, SliceItem, Tensor<byte>>(Tensor._GetSliceStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenSliceGen;
    public static MethodInfo TenSliceArr => _methTenSliceGen ??= GetMethodInfo3<Tensor<byte>, byte, SliceItem[], Tensor<byte>>(Tensor._GetSliceStatic).GetGenericMethodDefinition();

    private static MethodInfo _methTenMap;
    public static MethodInfo TenMap => _methTenMap ??= GetMethodInfo2<Tensor<byte>, Func<byte, sbyte>, Tensor<sbyte>>(Tensor._Map).GetGenericMethodDefinition();

    private static MethodInfo _methTenMapLazy;
    public static MethodInfo TenMapLazy => _methTenMapLazy ??= GetMethodInfo2<Tensor<byte>, Func<byte, sbyte>, Tensor<sbyte>>(Tensor._MapLazy).GetGenericMethodDefinition();

    private static MethodInfo _methTenZip2;
    public static MethodInfo TenZip2 => _methTenZip2 ??= new FuncOut<Tensor<byte>, Tensor<byte>, Func<byte, byte, sbyte>, bool, Tensor<sbyte>>(Tensor._Zip).Method.GetGenericMethodDefinition();

    private static MethodInfo _methTenZip3;
    public static MethodInfo TenZip3 => _methTenZip3 ??= new FuncOut<Tensor<byte>, Tensor<byte>, Tensor<byte>, Func<byte, byte, byte, sbyte>, bool, Tensor<sbyte>>(Tensor._Zip).Method.GetGenericMethodDefinition();

    private static MethodInfo _methSliceInd;
    public static MethodInfo SliceCreateInd => _methSliceInd ??= GetMethodInfo2<long, IndexFlags, SliceItem>(SliceItem.CreateIndex);

    private static MethodInfo _methSliceRR;
    public static MethodInfo SliceCreateRR => _methSliceRR ??= GetMethodInfo4<long, long, long, SliceItemFlags, SliceItem>(SliceItem.CreateRR);

    private static MethodInfo _methSliceRO;
    public static MethodInfo SliceCreateRO => _methSliceRO ??= GetMethodInfo4<long, long?, long, SliceItemFlags, SliceItem>(SliceItem.CreateRO);

    private static MethodInfo _methSliceR_;
    public static MethodInfo SliceCreateR_ => _methSliceR_ ??= GetMethodInfo3<long, long, SliceItemFlags, SliceItem>(SliceItem.CreateR_);

    private static MethodInfo _methSliceOR;
    public static MethodInfo SliceCreateOR => _methSliceOR ??= GetMethodInfo4<long?, long, long, SliceItemFlags, SliceItem>(SliceItem.CreateOR);

    private static MethodInfo _methSliceOO;
    public static MethodInfo SliceCreateOO => _methSliceOO ??= GetMethodInfo4<long?, long?, long, SliceItemFlags, SliceItem>(SliceItem.CreateOO);

    private static MethodInfo _methSliceO_;
    public static MethodInfo SliceCreateO_ => _methSliceO_ ??= GetMethodInfo3<long?, long, SliceItemFlags, SliceItem>(SliceItem.CreateO_);

    private static MethodInfo _methSlice_R;
    public static MethodInfo SliceCreate_R => _methSlice_R ??= GetMethodInfo3<long, long, SliceItemFlags, SliceItem>(SliceItem.Create_R);

    private static MethodInfo _methSlice_O;
    public static MethodInfo SliceCreate_O => _methSlice_O ??= GetMethodInfo3<long?, long, SliceItemFlags, SliceItem>(SliceItem.Create_O);

    private static MethodInfo _methSlice__;
    public static MethodInfo SliceCreate__ => _methSlice__ ??= GetMethodInfo1<long, SliceItem>(SliceItem.Create__);

    private static MethodInfo _methEqVal;
    public static MethodInfo EquatableEqualsVal(Type st)
    {
        Validation.AssertValue(st);
        Validation.Assert(typeof(IEquatable<>).MakeGenericType(st).IsAssignableFrom(st));
        Validation.Assert(st.IsValueType);

        MethodInfo meth;
        meth = _methEqVal ??= GetMethodInfo2<double, double, bool>(EqVal<double>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(st);
    }

    private static MethodInfo _methEq;
    private static MethodInfo _methEqTi;
    public static MethodInfo EquatableEqualsOpt(Type st, bool strict)
    {
        Validation.AssertValue(st);
        Validation.Assert(typeof(IEquatable<>).MakeGenericType(st).IsAssignableFrom(st));
        Validation.Assert(st.IsClass | st.IsInterface);

        MethodInfo meth;
        if (strict)
            meth = _methEqTi ??= GetMethodInfo2<string, string, bool>(EqStrict<string>).GetGenericMethodDefinition();
        else
            meth = _methEq ??= GetMethodInfo2<string, string, bool>(EqOpt<string>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(st);
    }

    private static MethodInfo _methCmpVal;
    public static MethodInfo ComparableCmpVal(Type st)
    {
        Validation.AssertValue(st);
        Validation.Assert(typeof(IComparable<>).MakeGenericType(st).IsAssignableFrom(st));
        Validation.Assert(st.IsValueType);

        MethodInfo meth;
        meth = _methCmpVal ??= GetMethodInfo2<double, double, int>(CmpVal<double>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(st);
    }

    private static MethodInfo _methToVal;
    public static MethodInfo ToVal(Type stItem)
    {
        Validation.AssertValue(stItem);
        Validation.Assert(stItem.IsValueType);
        var meth = _methToVal ??= GetMethodInfo1<object, int>(ToVal<int>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methToOpt;
    public static MethodInfo ToOpt(Type stItem)
    {
        Validation.AssertValue(stItem);
        Validation.Assert(stItem.IsValueType);
        var meth = _methToOpt ??= GetMethodInfo1<object, int?>(ToOpt<int>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methToRef;
    public static MethodInfo ToRef(Type stItem)
    {
        Validation.AssertValue(stItem);
        Validation.Assert(stItem.IsClass | stItem.IsInterface);
        var meth = _methToRef ??= GetMethodInfo1<object, string>(ToRef<string>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    private static MethodInfo _methGetDefault;
    public static MethodInfo GetDefault(Type stItem)
    {
        Validation.AssertValue(stItem);
        var meth = _methGetDefault ??= GetMethodInfo0<object>(GetDefault<object>).GetGenericMethodDefinition();
        return meth.MakeGenericMethod(stItem);
    }

    // MethodInfos for the hash combine methods.
    private static MethodInfo _methHashCombine2;
    private static MethodInfo _methHashCombine3;
    private static MethodInfo _methHashCombine4;
    private static MethodInfo _methHashCombine5;
    private static MethodInfo _methHashCombine6;
    private static MethodInfo _methHashCombine7;
    private static MethodInfo _methHashCombine8;
    public static MethodInfo HashCombine2 => _methHashCombine2 ??= new Func<int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();
    public static MethodInfo HashCombine3 => _methHashCombine3 ??= new Func<int, int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();
    public static MethodInfo HashCombine4 => _methHashCombine4 ??= new Func<int, int, int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();
    public static MethodInfo HashCombine5 => _methHashCombine5 ??= new Func<int, int, int, int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();
    public static MethodInfo HashCombine6 => _methHashCombine6 ??= new Func<int, int, int, int, int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();
    public static MethodInfo HashCombine7 => _methHashCombine7 ??= new Func<int, int, int, int, int, int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();
    public static MethodInfo HashCombine8 => _methHashCombine8 ??= new Func<int, int, int, int, int, int, int, int, int>(HashCode.Combine).Method.GetGenericMethodDefinition();

    public static MethodInfo GetHashCombine(int arity)
    {
        switch (arity)
        {
        case 2: return HashCombine2;
        case 3: return HashCombine3;
        case 4: return HashCombine4;
        case 5: return HashCombine5;
        case 6: return HashCombine6;
        case 7: return HashCombine7;
        case 8: return HashCombine8;
        }

        Validation.Assert(false);
        return null;
    }

    #endregion Cached method infos

    #region Implementation functions

    public static float PowFltImpl(float a, float b)
    {
        return (float)Math.Pow(a, b);
    }

    // This is used by GroupBy and Sort code gen. Note the unused "this" value. Instance delegate invocation is faster
    // than static delegate invocation, so we pretend this is an instance method on a null object.
    public static T IdentityImpl<T>(object unused, T value)
    {
        return value;
    }

    public static bool StrHasImpl(string a, string b)
    {
        if (string.IsNullOrEmpty(b))
            return true;
        if (string.IsNullOrEmpty(a))
            return false;
        return a.IndexOf(b, StringComparison.Ordinal) >= 0;
    }

    public static bool StrHasCiImpl(string a, string b)
    {
        if (string.IsNullOrEmpty(b))
            return true;
        if (string.IsNullOrEmpty(a))
            return false;
        return a.IndexOf(b, StringComparison.InvariantCultureIgnoreCase) >= 0;
    }

    public static T[] EnumerableToArray<T>(IEnumerable<T> src)
    {
        if (src is null)
            return null;
        if (src is T[] arr)
            return arr;
        return Enumerable.ToArray(src);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> EnumerableToCaching<T>(IEnumerable<T> src)
    {
        return EnumerableToCachingCore(src);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> EnumerableToCachingForced<T>(IEnumerable<T> src)
    {
        return EnumerableToCachingCore(src);
    }

    private static IEnumerable<T> EnumerableToCachingCore<T>(IEnumerable<T> src)
    {
        if (src is null)
            return null;
        if (src is ICachingEnumerable<T>)
            return src;
        if (src is IList<T>)
            return src;
        return new CachingEnumerable<T>(src);
    }

    public static IEnumerable<T> EnumerableToPinging<T>(IEnumerable<T> src, ExecCtx ctx, int id)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(ctx);
        if (src is null)
            return null;
        return EnumerableToPingingCore(src, ctx, id);
    }

    public static IEnumerable<T> EnumerableToPingingCore<T>(IEnumerable<T> src, ExecCtx ctx, int id)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(ctx);

        using var ator = src.GetEnumerator();
        for (; ; )
        {
            ctx.Ping(id);
            if (!ator.MoveNext())
                yield break;
            yield return ator.Current;
        }
    }

    public static IEnumerable<TDst> WrapWithCounter<TSrc, TDst>(IEnumerable<TSrc> src, IEnumerable<TDst> seq)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValueOrNull(seq);
        if (seq is null)
            return null;
        if (src is IReadOnlyCollection<TSrc> col)
            return WrapWithCount.Create<TDst>(col.Count, seq);
        if (src is ICanCount can)
            return WrapWithCount.Create<TDst>(can, seq);
        return seq;
    }

    public static IEnumerable<(T, long)> WrapIndPairs<T>(IEnumerable<T> src)
    {
        Validation.AssertValue(src);
        long i = 0;
        foreach (var item in src)
            yield return (item, i++);
    }

    public static IEnumerable<T> UnwrapIndPairsToItems<T>(IEnumerable<(T, long)> src)
    {
        Validation.AssertValue(src);
        return src.Select(pair => pair.Item1);
    }

    public static IEnumerable<TDst> WrapWithCounter<TSrc, TDst>(IEnumerable<TSrc> src, long delta, IEnumerable<TDst> seq)
    {
        Validation.AssertValueOrNull(src);
        Validation.AssertValue(seq);

        if (src is IReadOnlyCollection<TSrc> col)
            return WrapWithCount.Create<TDst>(col.Count, seq, delta);
        if (src is ICanCount can)
            return WrapWithCount.Create<TDst>(can, seq, delta);

        return seq;
    }

    public static bool InImpl<T>(T value, IEnumerable<T> src, ExecCtx ctx, int id)
    {
        Validation.AssertValue(ctx);

        if (src == null)
            return false;

        foreach (var item in src)
        {
            // Note that EqualityComparer<T>.Default is an intrinsic and doing it this way
            // rather than lifting EqualityComparer<T>.Default out of the loop benefits from
            // devirtualization and likely inlining.
            if (EqualityComparer<T>.Default.Equals(item, value))
                return true;
            ctx.Ping(id);
        }
        return false;
    }

    public static bool InImpl<T>(T value, IEnumerable<T> src, IEqualityComparer<T> eq, ExecCtx ctx, int id)
    {
        Validation.AssertValue(eq);
        Validation.AssertValue(ctx);

        if (src == null)
            return false;
        foreach (var item in src)
        {
            if (eq.Equals(item, value))
                return true;
            ctx.Ping(id);
        }
        return false;
    }

    #region Sort functions
    // REVIEW: We should move away from OrderBy since our assumptions are much tighter than Linq's. A common use case
    // for sorting, especially with the UI's limited view, is to only get the top k elements from the sorted enumerable. This
    // could be done more efficiently with an indirect heapsort. Note the sort needs to be stable.

    // REVIEW: We pass in a compare delegate to match the signature of an ideal sort implementation that doesn't require
    // a comparer. However, OrderBy requires a comparer object, so we need to create one inside these functions. This should change.

    // REVIEW: Using identity key selectors when the comparison logic is offloaded to the comparison function is an
    // artifact of the OrderBy contract. In this case the key selector doesn't need to be called at all.

    // REVIEW: With nontrivial comparisons where ties are broken by evaluating subsequent selectors, repeated comparisons
    // involving the same element don't benefit from caching the "keys" produced by each selector. Some caching might be desirable here.
    // For example, if we compare A to B, then compare A to C, we currently don't reuse the result of the selector transformation(s) on A.

    public static IEnumerable<TItem> Sort<TItem, TKey>(IEnumerable<TItem> src, Func<TItem, TKey> fn, Func<TKey, TKey, int> cmp)
    {
        if (src == null)
            return null;

        return Enumerable.OrderBy(src, fn, new Comparer<TKey>(cmp));
    }

    public static IEnumerable<TItem> SortNoKey<TItem>(IEnumerable<TItem> src, Func<TItem, TItem, int> cmp)
    {
        if (src == null)
            return null;

        return Enumerable.OrderBy(src, x => x, new Comparer<TItem>(cmp));
    }

    public static IEnumerable<TItem> IndSort<TItem, TKey>(IEnumerable<TItem> src, Func<TItem, long, TKey> fn, Func<TKey, TKey, int> cmp)
    {
        if (src == null)
            return null;

        var pairs = WrapIndPairs(src);
        var comparer = new Comparer<TKey>(cmp);
        return Enumerable.OrderBy(pairs, pair => fn(pair.Item1, pair.Item2), comparer).Select(pair => pair.Item1);
    }

    public static IEnumerable<TItem> IndSortNoKey<TItem>(IEnumerable<TItem> src, Func<TItem, long, TItem, long, int> cmp)
    {
        if (src == null)
            return null;

        var pairs = WrapIndPairs(src);
        var comparer = new PairComparer<TItem, long>(cmp);
        return Enumerable.OrderBy(pairs, x => x, comparer).Select(pair => pair.Item1);
    }

    #endregion Sort functions

    /// <summary>
    /// T + T => T.
    /// </summary>
    public static Time Add(Time x, Time y)
    {
        long xt = x.Ticks;
        long yt = y.Ticks;
        long rt = xt + yt;

        // REVIEW: Detect overflow? Or should we just allow it, like with integer arithmetic?
        return new Time(rt);
    }

    /// <summary>
    /// T - T => T.
    /// </summary>
    public static Time Sub(Time x, Time y)
    {
        long xt = x.Ticks;
        long yt = y.Ticks;
        long rt = xt - yt;

        // REVIEW: Detect overflow? Or should we just allow it, like with integer arithmetic?
        return new Time(rt);
    }

    /// <summary>
    /// T * r8 => T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Mul(Time x, double scalar)
    {
        return new Time(NumUtil.ModToI8(x.Ticks * scalar));
    }

    /// <summary>
    /// T * i8 => T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Mul(Time x, long scalar)
    {
        return new Time(x.Ticks * scalar);
    }

    /// <summary>
    /// T / r8 => T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time Div(Time x, double scalar)
    {
        return new Time(NumUtil.ModToI8(x.Ticks / scalar));
    }

    /// <summary>
    /// T / T => r8
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Div(Time x, Time y)
    {
        return (double)x.Ticks / y.Ticks;
    }

    /// <summary>
    /// T div i8 => T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time IntDiv(Time x, long y)
    {
        // Avoid divide by zero.
        if (y == 0)
            return default;

        long num = x.Ticks;

        // Avoid exception when dividing the evil value by -1.
        if (y == -1)
            return new Time(-num);

        return new Time(num / y);
    }

    /// <summary>
    /// T div T => i8
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long IntDiv(Time x, Time y)
    {
        long den = y.Ticks;

        // Avoid divide by zero.
        if (den == 0)
            return 0;

        long num = x.Ticks;

        // Avoid exception when dividing the evil value by -1.
        if (den == -1)
            return -num;

        return num / den;
    }

    /// <summary>
    /// T mod T => T
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time IntMod(Time x, Time y)
    {
        long den = y.Ticks;

        // Avoid divide by zero.
        // Avoid exception when dividing the evil value by -1.
        if (den == 0 || den == -1)
            return default;

        long num = x.Ticks;
        return new Time(num % den);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToVal<T>(object obj)
        where T : struct
    {
        if (obj == null)
            return default;
        Validation.Assert(obj is T);
        return (T)obj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? ToOpt<T>(object obj)
        where T : struct
    {
        if (obj == null)
            return default;
        Validation.Assert(obj is T);
        return (T)obj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToRef<T>(object obj)
        where T : class
    {
        Validation.Assert(obj == null || obj is T);
        return (T)obj;
    }

    public static IEnumerable<T> SeqConcat<T>(IEnumerable<T> s0, IEnumerable<T> s1)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        if (s0 != null)
        {
            foreach (var x in s0)
                yield return x;
        }
        if (s1 != null)
        {
            foreach (var x in s1)
                yield return x;
        }
    }

    public static IEnumerable<T> SeqConcat<T>(IEnumerable<T> s0, IEnumerable<T> s1, IEnumerable<T> s2)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        if (s0 != null)
        {
            foreach (var x in s0)
                yield return x;
        }
        if (s1 != null)
        {
            foreach (var x in s1)
                yield return x;
        }
        if (s2 != null)
        {
            foreach (var x in s2)
                yield return x;
        }
    }

    public static IEnumerable<T> SeqConcat<T>(IEnumerable<T> s0, IEnumerable<T> s1, IEnumerable<T> s2, IEnumerable<T> s3)
    {
        Validation.AssertValueOrNull(s0);
        Validation.AssertValueOrNull(s1);
        Validation.AssertValueOrNull(s2);
        Validation.AssertValueOrNull(s3);
        if (s0 != null)
        {
            foreach (var x in s0)
                yield return x;
        }
        if (s1 != null)
        {
            foreach (var x in s1)
                yield return x;
        }
        if (s2 != null)
        {
            foreach (var x in s2)
                yield return x;
        }
        if (s3 != null)
        {
            foreach (var x in s3)
                yield return x;
        }
    }

    public static IEnumerable<T> SeqConcat<T>(params IEnumerable<T>[] ss)
    {
        Validation.AssertValue(ss);
        foreach (var s in ss)
        {
            if (s == null)
                continue;
            foreach (var x in s)
                yield return x;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetDefault<T>() => default;

    #region Comparison functions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqVal<T>(T a, T b)
        where T : struct, IEquatable<T>
    {
        return a.Equals(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqOpt<T>(T a, T b)
        where T : class, IEquatable<T>
    {
        if (a is null)
            return b is null;
        return a.Equals(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqStrict<T>(T a, T b)
        where T : class, IEquatable<T>
    {
        if (a is null)
            return false;
        if (b is null)
            return false;
        return a.Equals(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CmpVal<T>(T a, T b)
        where T : struct, IComparable<T>
    {
        return a.CompareTo(b);
    }

    /// <summary>
    /// Contains static versions of comparison functions for sortable types,
    /// with nullable and descending forms. Most implementations are adapted
    /// from .NET runtime source <c>CompareTo</c> methods.
    /// </summary>
    private static class Cmp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(double x, double y)
        {
            if (x < y) return -1;
            if (x > y) return 1;
            if (x == y) return 0;

            // At least one of the values is NaN.
            if (double.IsNaN(x))
                return double.IsNaN(y) ? 0 : -1;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(double x, double y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(float x, float y)
        {
            if (x < y) return -1;
            if (x > y) return 1;
            if (x == y) return 0;

            // At least one of the values is NaN.
            if (float.IsNaN(x))
                return float.IsNaN(y) ? 0 : -1;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(float x, float y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Integer x, Integer y)
        {
            // REVIEW: .NET uses some internal members for
            // the comparison. Can we avoid the call to CompareTo?
            return x.CompareTo(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Integer x, Integer y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(long x, long y)
        {
            // Need to use compare because subtraction will wrap
            // to positive for very large neg numbers, etc.
            if (x < y) return -1;
            if (x > y) return 1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(long x, long y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(int x, int y)
        {
            // Need to use compare because subtraction will wrap
            // to positive for very large neg numbers, etc.
            if (x < y) return -1;
            if (x > y) return 1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(int x, int y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(ulong x, ulong y)
        {
            // Need to use compare because subtraction will wrap
            // to positive for very large neg numbers, etc.
            if (x < y) return -1;
            if (x > y) return 1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(ulong x, ulong y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(uint x, uint y)
        {
            // Need to use compare because subtraction will wrap
            // to positive for very large neg numbers, etc.
            if (x < y) return -1;
            if (x > y) return 1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(uint x, uint y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Date x, Date y)
        {
            long xTicks = x.Ticks;
            long yTicks = y.Ticks;
            if (xTicks > yTicks) return 1;
            if (xTicks < yTicks) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Date x, Date y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Time x, Time y)
        {
            long xTicks = x.Ticks;
            long yTicks = y.Ticks;
            if (xTicks > yTicks) return 1;
            if (xTicks < yTicks) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Time x, Time y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Guid x, Guid y) => x.CompareTo(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Guid x, Guid y) => y.CompareTo(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(string x, string y)
        {
            return StrComparer.Cs.Compare(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(string x, string y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareCi(string x, string y)
        {
            return StrComparer.Ci.Compare(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareCiDec(string x, string y) => CompareCi(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(bool? x, bool? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue)
                {
                    bool xVal = x.GetValueOrDefault();
                    if (xVal == y.GetValueOrDefault()) return 0;
                    if (!xVal) return -1;
                    return 1;
                }
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(bool? x, bool? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(double? x, double? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(double? x, double? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(float? x, float? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(float? x, float? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Integer? x, Integer? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Integer? x, Integer? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(long? x, long? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(long? x, long? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(int? x, int? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(int? x, int? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(short? x, short? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return x.GetValueOrDefault() - y.GetValueOrDefault();
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(short? x, short? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(sbyte? x, sbyte? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return x.GetValueOrDefault() - y.GetValueOrDefault();
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(sbyte? x, sbyte? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(ulong? x, ulong? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(ulong? x, ulong? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(uint? x, uint? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault(), y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(uint? x, uint? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(ushort? x, ushort? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return x.GetValueOrDefault() - y.GetValueOrDefault();
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(ushort? x, ushort? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(byte? x, byte? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return x.GetValueOrDefault() - y.GetValueOrDefault();
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(byte? x, byte? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Date? x, Date? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault().Ticks, y.GetValueOrDefault().Ticks);
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Date? x, Date? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Time? x, Time? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return Compare(x.GetValueOrDefault().Ticks, y.GetValueOrDefault().Ticks);
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Time? x, Time? y) => Compare(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(Guid? x, Guid? y)
        {
            if (x.HasValue)
            {
                if (y.HasValue) return x.GetValueOrDefault().CompareTo(y.GetValueOrDefault());
                return 1;
            }
            if (y.HasValue) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareDec(Guid? x, Guid? y) => Compare(y, x);
    }

    #endregion Comparison functions

    /// <summary>
    /// Return the code point at the given location.
    /// </summary>
    public static ushort IndexText(string src, long index, IndexFlags flags)
    {
        Validation.Assert(flags.IsValid());

        if (src == null)
            return 0;

        int len = src.Length;
        if (len == 0)
            return 0;

        long idx = flags.AdjustIndex(index, len);
        if ((ulong)idx >= (ulong)len)
            return 0;
        return src[(int)idx];
    }

    /// <summary>
    /// Slice the <paramref name="src"/> text value according to the <paramref name="slice"/> value.
    /// </summary>
    public static string SliceText(string src, SliceItem slice)
    {
        Validation.Assert(!slice.IsIndex);

        if (src is null)
            return null;

        int len = src.Length;
        if (len == 0)
            return src;

        slice.IsRange(len, out var rng).Verify();
        var (start, step, count) = rng;
        if (count == 0)
            return "";
        if (step == 1)
            return src.Substring((int)start, (int)count);

        var res = string.Create((int)count, (src, (int)start, (int)step), static (dst, state) =>
        {
            var (src, ich, dich) = state;
            for (int i = 0; i < dst.Length; i++)
            {
                Validation.AssertIndex(ich, src.Length);
                dst[i] = src[ich];
                ich += dich;
            }
        });

        return res;
    }

    #endregion Implementation functions
}
