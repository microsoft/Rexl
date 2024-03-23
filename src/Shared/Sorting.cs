// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Rexl.Private;

internal static class Sorting
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    public static void RemoveDupsFromSorted<T>(T[] rgv, int ivMin, ref int ivLim, Func<T, T, bool> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivMin && ivMin <= ivLim && ivLim <= rgv.Length);
        Validation.AssertValue(cmp);

        if (ivLim - ivMin <= 1)
            return;

        int ivDst = ivMin + 1;
        for (int ivSrc = ivMin + 1; ivSrc < ivLim; ivSrc++)
        {
            T itemCur = rgv[ivSrc];
            if (!cmp(rgv[ivDst - 1], itemCur))
            {
                if (ivDst < ivSrc)
                    rgv[ivDst] = itemCur;
                ivDst++;
            }
        }
        ivLim = ivDst;
    }

    public static void SortAndRemoveDups<T>(T[] rgv, int ivMin, ref int ivLim, Comparison<T> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivMin && ivMin <= ivLim && ivLim <= rgv.Length);
        Validation.AssertValue(cmp);

        if (ivLim - ivMin <= 1)
            return;
        QuickSort(rgv, ivMin, ivLim - 1, cmp);
        int ivDst = ivMin + 1;
        for (int ivSrc = ivMin + 1; ivSrc < ivLim; ivSrc++)
        {
            T itemCur = rgv[ivSrc];
            if (cmp(rgv[ivDst - 1], itemCur) != 0)
            {
                if (ivDst < ivSrc)
                    rgv[ivDst] = itemCur;
                ivDst++;
            }
        }
        ivLim = ivDst;
    }

    public static bool TryFindItemSorted<T>(T[] rgv, int ivMin, int ivLim, Comparison<T> cmp, T item, out int iv)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivMin && ivMin <= ivLim && ivLim <= rgv.Length);
        Validation.AssertValue(cmp);

        int ivMinCur = ivMin;
        int ivLimCur = ivLim;
        while (ivMinCur < ivLimCur)
        {
            // The casts handle the extremely unlikely possibility of overflow.
            int ivCur = (int)((uint)(ivMinCur + ivLimCur) >> 1);
            int n = cmp(rgv[ivCur], item);
            if (n < 0)
                ivMinCur = ivCur + 1;
            else
                ivLimCur = ivCur;
        }

        Validation.Assert(ivMin <= ivMinCur && ivMinCur == ivLimCur && ivLimCur <= ivLim);
        Validation.Assert(ivMin == ivMinCur || cmp(rgv[ivMinCur - 1], item) < 0);
        Validation.Assert(ivMinCur == ivLim || cmp(item, rgv[ivMinCur]) <= 0);

        iv = ivMinCur;
        return ivMinCur < ivLim && cmp(item, rgv[ivMinCur]) == 0;
    }

    public static void Sort<T>(T[] rgv, int ivFirst, int ivLim, Comparison<T> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivFirst && ivFirst < ivLim && ivLim <= rgv.Length);
        Validation.AssertValue(cmp);

        QuickSort(rgv, ivFirst, ivLim - 1, cmp);
    }

    private static void SwapIfGreater<T>(T[] rgv, int iv1, int iv2, Comparison<T> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= iv1 && iv1 < rgv.Length);
        Validation.Assert(0 <= iv2 && iv2 < rgv.Length);
        Validation.AssertValue(cmp);

        if (iv1 != iv2 && cmp(rgv[iv1], rgv[iv2]) > 0)
        {
            T val = rgv[iv1];
            rgv[iv1] = rgv[iv2];
            rgv[iv2] = val;
        }
    }

    public static void QuickSort<T>(T[] rgv, int ivFirst, int ivLast, Comparison<T> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivFirst && ivFirst <= ivLast && ivLast < rgv.Length);
        Validation.AssertValue(cmp);

        if (ivLast - ivFirst < 10)
        {
            QuadSort(rgv, ivFirst, ivLast, cmp);
            return;
        }

        while (ivFirst < ivLast)
        {
            int ivMin = ivFirst;
            int ivLim = ivLast;
            // The casts handle the extremely unlikely possibility of overflow.
            int ivCur = (int)((uint)(ivMin + ivLim) >> 1);

            // Pre-sort the low, middle (pivot), and high values in place.
            // This improves performance in the face of multiple sorted runs appended together.
            SwapIfGreater(rgv, ivMin, ivCur, cmp); // swap the low with the mid point
            SwapIfGreater(rgv, ivMin, ivLim, cmp); // swap the low with the high
            SwapIfGreater(rgv, ivCur, ivLim, cmp); // swap the middle with the high

            T vCur = rgv[ivCur];
            do
            {
                while (cmp(vCur, rgv[ivMin]) > 0)
                    ivMin++;
                while (cmp(vCur, rgv[ivLim]) < 0)
                    ivLim--;
                Validation.Assert(ivFirst <= ivMin && ivLim <= ivLast);
                if (ivMin > ivLim)
                    break;
                if (ivMin < ivLim)
                    Swap(ref rgv[ivMin], ref rgv[ivLim]);
                ivMin++;
                ivLim--;
            }
            while (ivMin <= ivLim);
            if (ivLim - ivFirst <= ivLast - ivMin)
            {
                if (ivFirst < ivLim)
                    QuickSort(rgv, ivFirst, ivLim, cmp);
                ivFirst = ivMin;
            }
            else
            {
                if (ivMin < ivLast)
                    QuickSort(rgv, ivMin, ivLast, cmp);
                ivLast = ivLim;
            }
        }
    }

    /// <summary>
    /// Stable sort (in place) using a quadratic algorithm. Should only be used for smallish arrays.
    /// </summary>
    public static void QuadSort<T>(T[] rgv, int ivFirst, int ivLast, Comparison<T> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivFirst && ivFirst <= ivLast && ivLast < rgv.Length);
        Validation.AssertValue(cmp);

        for (int iv1 = ivLast; iv1 > ivFirst; iv1--)
        {
            int ivBest = iv1;
            T vBest = rgv[ivBest];
            for (int iv2 = iv1; --iv2 >= ivFirst;)
            {
                if (cmp(vBest, rgv[iv2]) < 0)
                {
                    ivBest = iv2;
                    vBest = rgv[ivBest];
                }
            }
            if (ivBest != iv1)
                Swap(ref rgv[ivBest], ref rgv[iv1]);
        }
    }

    /// <summary>
    /// Stable sort (in place) using a quadratic algorithm. Should only be used for smallish lists.
    /// </summary>
    public static void QuadSort<T>(this List<T>? rgv, Comparison<T> cmp)
    {
        if (rgv == null || rgv.Count <= 1)
            return;
        rgv.QuadSort(0, rgv.Count - 1, cmp);
    }

    /// <summary>
    /// Stable sort (in place) using a quadratic algorithm. Should only be used for smallish lists.
    /// </summary>
    public static void QuadSort<T>(this List<T> rgv, int ivFirst, int ivLast, Comparison<T> cmp)
    {
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= ivFirst && ivFirst <= ivLast && ivLast < rgv.Count);
        Validation.AssertValue(cmp);

        for (int iv1 = ivLast; iv1 > ivFirst; iv1--)
        {
            int ivBest = iv1;
            T vBest = rgv[ivBest];
            for (int iv2 = iv1; --iv2 >= ivFirst;)
            {
                if (cmp(vBest, rgv[iv2]) < 0)
                {
                    ivBest = iv2;
                    vBest = rgv[ivBest];
                }
            }
            if (ivBest != iv1)
            {
                T tmp = rgv[ivBest];
                rgv[ivBest] = rgv[iv1];
                rgv[iv1] = tmp;
            }
        }
    }

    public static void QuickSortIndirect(int[] rgiv, int[] rgv, int iivFirst, int iivLast)
    {
        Validation.AssertValue(rgiv);
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= iivFirst && iivFirst <= iivLast && iivLast < rgiv.Length);

        if (iivLast - iivFirst < 20)
        {
            QuadSortIndirect(rgiv, rgv, iivFirst, iivLast);
            return;
        }

        while (iivFirst < iivLast)
        {
            int iivMin = iivFirst;
            int iivLim = iivLast;
            int iivCur = (iivMin + iivLim) >> 1;
            Validation.Assert(0 <= rgiv[iivCur] && rgiv[iivCur] < rgv.Length);
            int vCur = rgv[rgiv[iivCur]];
            do
            {
                while (vCur > rgv[rgiv[iivMin]])
                    iivMin++;
                while (vCur < rgv[rgiv[iivLim]])
                    iivLim--;
                Validation.Assert(iivFirst <= iivMin && iivLim <= iivLast);
                if (iivMin > iivLim)
                    break;
                if (iivMin < iivLim)
                    Swap(ref rgiv[iivMin], ref rgiv[iivLim]);
                iivMin++;
                iivLim--;
            }
            while (iivMin <= iivLim);
            if (iivLim - iivFirst <= iivLast - iivMin)
            {
                if (iivFirst < iivLim)
                    QuickSortIndirect(rgiv, rgv, iivFirst, iivLim);
                iivFirst = iivMin;
            }
            else
            {
                if (iivMin < iivLast)
                    QuickSortIndirect(rgiv, rgv, iivMin, iivLast);
                iivLast = iivLim;
            }
        }
    }

    public static void QuadSortIndirect(int[] rgiv, int[] rgv, int iivFirst, int iivLast)
    {
        Validation.AssertValue(rgiv);
        Validation.AssertValue(rgv);
        Validation.Assert(0 <= iivFirst && iivFirst <= iivLast && iivLast < rgiv.Length);

        for (int iiv1 = iivLast; iiv1 > iivFirst; iiv1--)
        {
            int iivBest = iiv1;
            int vBest = rgv[rgiv[iivBest]];
            for (int iiv2 = iiv1; --iiv2 >= iivFirst;)
            {
                if (vBest < rgv[rgiv[iiv2]])
                {
                    iivBest = iiv2;
                    vBest = rgv[rgiv[iivBest]];
                }
            }
            if (iivBest != iiv1)
                Swap(ref rgiv[iivBest], ref rgiv[iiv1]);
        }
    }
}
