// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Implements a heap of integers.
/// </summary>
internal sealed class IntHeap
{
    // The heap elements. The 0th element is a dummy.
    private int[] _rgv;
    // The count of active elements, not including the dummy.
    private int _cv;

    public IntHeap()
    {
        _rgv = new int[10];
        _cv = 0;
    }

    public IntHeap(int capacity)
    {
        _rgv = new int[Math.Max(capacity + 1, 10)];
        _cv = 0;
    }

    public IntHeap(IntHeap src)
    {
        _rgv = (int[])src._rgv.Clone();
        _cv = src._cv;
    }

    /// <summary>
    /// Count of elements in the heap.
    /// </summary>
    public int Count { get { return _cv; } }

    private static int Parent(int iv) { return iv >> 1; }
    private static int Left(int iv) { return iv + iv; }
    private static int Right(int iv) { return iv + iv + 1; }

    /// <summary>
    /// Discard all elements currently in the heap.
    /// </summary>
    public void Clear()
    {
        _cv = 0;
    }

    /// <summary>
    /// Produces the items in no particular order.
    /// </summary>
    public IEnumerable<int> GetItems()
    {
        for (int i = 1; i <= _cv; i++)
            yield return _rgv[i];
    }

    /// <summary>
    /// If not empty, pop the first element in the heap and return true.
    /// </summary>
    public bool TryPop(out int v)
    {
        if (_cv > 0)
        {
            v = _rgv[1];
            _rgv[1] = _rgv[_cv--];
            if (_cv > 0)
                BubbleDown(1);
            return true;
        }
        v = default;
        return false;
    }

    /// <summary>
    /// Remove and return the first element in the heap.
    /// </summary>
    public int Pop()
    {
        Validation.BugCheck(_cv > 0);
        int vRes = _rgv[1];
        _rgv[1] = _rgv[_cv--];
        if (_cv > 0)
            BubbleDown(1);
        return vRes;
    }

    /// <summary>
    /// Add a new element to the heap.
    /// </summary>
    public void Add(int v)
    {
        Validation.Assert(_cv <= _rgv.Length - 1);
        if (_cv >= _rgv.Length - 1)
        {
            int lenMin = _cv + 2;
            int len = Util.GetCapTarget(_rgv.Length, lenMin);
            Util.Grow(ref _rgv, ref len, lenMin);
        }
        _rgv[++_cv] = v;
        BubbleUp(_cv);
    }

    private void MoveTo(int v, int iv)
    {
        Validation.Assert(iv > 0);
        _rgv[iv] = v;
    }

    private void BubbleUp(int iv)
    {
        int v = _rgv[iv];
        int ivPar;
        for (; (ivPar = Parent(iv)) > 0 && _rgv[ivPar] > v; iv = ivPar)
            MoveTo(_rgv[ivPar], iv);
        MoveTo(v, iv);
    }

    private void BubbleDown(int iv)
    {
        int ivMax = _cv;
        int v = _rgv[iv];
        int ivChild;
        for (; (ivChild = Left(iv)) <= ivMax; iv = ivChild)
        {
            if (ivChild < ivMax && _rgv[ivChild] > _rgv[ivChild + 1])
                ivChild++;
            if (v <= _rgv[ivChild])
                break;
            MoveTo(_rgv[ivChild], iv);
        }
        MoveTo(v, iv);
    }
}

#if NEEDED
/// <summary>
/// Implements a heap - for implementing priority queues.
/// </summary>
internal class Heap<T>
{
    // The heap elements. The 0th element is a dummy.
    private readonly List<T> _rgv;
    private readonly Func<T, T, bool> _fnReverse;

    /// <summary> A Heap structure gives efficient access to the ordered next element.
    /// </summary>
    /// <param name="fnReverse"> tests true if first element should be after the second </param>
    public Heap(Func<T, T, bool> fnReverse)
    {
        _rgv = new List<T>();
        _rgv.Add(default);
        _fnReverse = fnReverse;
    }

    /// <summary> A Heap structure gives efficient access to the ordered next element.
    /// </summary>
    /// <param name="fnReverse"> tests true if first element should be after the second </param>
    /// <param name="capacity"> the maximum capacity of the heap </param>
    public Heap(Func<T, T, bool> fnReverse, int capacity)
    {
        _rgv = new List<T>(capacity);
        _rgv.Add(default);
        _fnReverse = fnReverse;
    }

    protected List<T> Elements
    {
        get { return _rgv; }
    }

    /// <summary>
    /// Func tests true if first element should be after the second
    /// </summary>
    protected Func<T, T, bool> FnReverse
    {
        get { return _fnReverse; }
    }

    /// <summary>
    /// Count of elements in the heap.
    /// </summary>
    public int Count { get { return _rgv.Count - 1; } }

    protected static int Parent(int iv) { return iv >> 1; }
    protected static int Left(int iv) { return iv + iv; }
    protected static int Right(int iv) { return iv + iv + 1; }

    // For tracking indices for items.
    protected virtual void MoveTo(T v, int iv)
    {
        Validation.Assert(iv > 0);
        _rgv[iv] = v;
    }
    protected virtual void Delete(T v) { }

    /// <summary> Discard all elements currently in the heap
    /// </summary>
    public void Clear()
    {
        _rgv.RemoveRange(1, _rgv.Count - 1);
    }

    /// <summary> Peek at the first element in the heap
    /// </summary>
    public T Top
    {
        get
        {
            if (_rgv.Count <= 1)
                return default(T);
            return _rgv[1];
        }
    }

    /// <summary> Remove and return the first element in the heap
    /// </summary>
    public T Pop()
    {
        Validation.Assert(_rgv.Count > 1, "Empty heap");
        int cv = _rgv.Count;

        T vRes = _rgv[1];
        Delete(vRes);

        _rgv[1] = _rgv[--cv];
        _rgv.RemoveAt(cv);
        if (cv > 1)
            BubbleDown(1);

        return vRes;
    }

    /// <summary> Add a new element to the heap
    /// </summary>
    public void Add(T v)
    {
        int iv = _rgv.Count;
        _rgv.Add(v);
        BubbleUp(iv);
    }

    /// <summary>
    /// Returns all the current elements of the heap in an array, with no guaranteed order.
    /// </summary>
    public T[] ToArrayUnsorted()
    {
        var contents = new T[Count];
        Validation.Assert(Count == _rgv.Count - 1);
        _rgv.CopyTo(1, contents, 0, Count);
        return contents;
    }

    protected void BubbleUp(int iv)
    {
        T v = _rgv[iv];
        int ivPar;
        for (; (ivPar = Parent(iv)) > 0 && _fnReverse(_rgv[ivPar], v); iv = ivPar)
            MoveTo(_rgv[ivPar], iv);
        MoveTo(v, iv);
    }

    protected void BubbleDown(int iv)
    {
        int cv = _rgv.Count;
        T v = _rgv[iv];
        int ivChild;
        for (; (ivChild = Left(iv)) < cv; iv = ivChild)
        {
            if (ivChild + 1 < cv && _fnReverse(_rgv[ivChild], _rgv[ivChild + 1]))
                ivChild++;
            if (!_fnReverse(v, _rgv[ivChild]))
                break;
            MoveTo(_rgv[ivChild], iv);
        }
        MoveTo(v, iv);
    }
}

/// <summary>
/// Implements a heap of integers that is indexed, to track whether a value
/// is in the heap.
/// </summary>
internal class IndexedHeap : Heap<int>
{
    // REVIEW: Maybe this belongs in SatSolver instead of here. It's pretty
    // specialized.
    private readonly int[] _mpviv; // Map from value to index in _rgv.

    public IndexedHeap(int vLim, Func<int, int, bool> fnReverse)
      : base(fnReverse)
    {
        _mpviv = new int[vLim];
    }

    public bool InHeap(int v)
    {
        Validation.AssertIndex(v, _mpviv.Length);
        return _mpviv[v] > 0;
    }
    public void MoveUp(int v)
    {
        Validation.Assert(InHeap(v));
        BubbleUp(_mpviv[v]);
    }
    public void MoveDown(int v)
    {
        Validation.Assert(InHeap(v));
        BubbleDown(_mpviv[v]);
    }
    public void Remove(int v)
    {
        Validation.Assert(InHeap(v));
        int iv = _mpviv[v];
        Delete(v);
        Elements[iv] = Util.PopList(Elements);
        if (iv > 1 && FnReverse(Elements[Parent(iv)], Elements[iv]))
            BubbleUp(iv);
        else
            BubbleDown(iv);
    }

    // For tracking indices for items.
    protected override void MoveTo(int v, int iv)
    {
        Validation.AssertIndex(v, _mpviv.Length);
        Validation.Assert(0 < iv && iv < Elements.Count);
        Elements[iv] = v;
        _mpviv[v] = iv;
    }
    protected override void Delete(int v)
    {
        Validation.AssertIndex(v, _mpviv.Length);
        _mpviv[v] = 0;
    }
}
#endif
