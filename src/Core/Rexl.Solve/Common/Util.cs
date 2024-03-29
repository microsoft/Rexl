// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Solve;

internal static class Util
{
    public static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    public static int Size<T>(T[] rgv)
    {
        if (rgv == null)
            return 0;
        return rgv.Length;
    }

    public static int Size<T>(IList<T> list)
    {
        if (list == null)
            return 0;
        return list.Count;
    }

    public static void Add<T>(ref List<T> list, T item)
    {
        list ??= new List<T>();
        list.Add(item);
    }

    public static void TrimList<T>(List<T> list, int cv)
    {
        list.RemoveRange(cv, list.Count - cv);
    }

    public static T PopList<T>(List<T> list)
    {
        Validation.Assert(list.Count > 0);
        int index = list.Count - 1;
        T v = list[index];
        list.RemoveAt(index);
        return v;
    }

    public static void MoveItem<T>(T[] rgv, int ivSrc, int ivDst)
    {
        Validation.AssertIndex(ivSrc, rgv.Length);
        Validation.AssertIndex(ivDst, rgv.Length);
        if (ivSrc == ivDst)
            return;
        T vTmp = rgv[ivSrc];
        if (ivSrc < ivDst)
            Array.Copy(rgv, ivSrc + 1, rgv, ivSrc, ivDst - ivSrc);
        else
            Array.Copy(rgv, ivDst, rgv, ivDst + 1, ivSrc - ivDst);
        rgv[ivDst] = vTmp;
    }

    public static void EnsureArraySize<T>(ref T[] rgv, int cv)
    {
        if (rgv.Length < cv)
            Array.Resize(ref rgv, Math.Max(cv, rgv.Length + rgv.Length / 2));
    }
}
