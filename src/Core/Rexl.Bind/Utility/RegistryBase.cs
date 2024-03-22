// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Abstract base class for a registry of items. This supports looking up items via a key,
/// as well as building from parent registries.
/// </summary>
public abstract class RegistryBase<TReg, TKey, TItem>
    where TReg : RegistryBase<TReg, TKey, TItem>
    where TItem : class
{
    // We structure it this way, so we can keep a record of when symbols are "replaced".
    // We expect this to be rare, so the extra storage shouldn't be significant.
    private readonly List<TItem> _items;
    private readonly Dictionary<TKey, int> _keyToIndex;

    // The minimum index of entries added by this registry (not one of its parents).
    private int _indexMin;

    /// <summary>
    /// Ctor when there are no parent registries.
    /// </summary>
    protected RegistryBase()
    {
        _items = new List<TItem>();
        _keyToIndex = new Dictionary<TKey, int>();
    }

    /// <summary>
    /// Ctor when there is at most one parent registry.
    /// </summary>
    protected RegistryBase(TReg parent)
        : this()
    {
        AddParent(parent);
    }

    /// <summary>
    /// Ctor for any number of parent registries.
    /// </summary>
    protected RegistryBase(IEnumerable<TReg> parents)
        : this()
    {
        Validation.BugCheckValueOrNull(parents);

        if (parents != null)
        {
            foreach (var par in parents)
                AddParent(par);
        }
    }

    /// <summary>
    /// Add a parent. It is illegal to call this after symbols have been added using AddOne.
    /// </summary>
    protected void AddParent(TReg parent)
    {
        Validation.BugCheckValueOrNull(parent);
        Validation.BugCheck(_indexMin == _items.Count, "Can't add a parent after adding symbols");

        if (parent is null)
            return;

        // Note that latter parents win, overriding symbols introduced by earlier ones.
        // REVIEW: Should we warn when one is overridden? Also, should we delete null
        // values or just record them? Currently we just record them.
        int indexMin = _items.Count;
        _items.AddRange(parent._items);
        foreach (var kvp in parent._keyToIndex)
        {
            int index = indexMin + kvp.Value;
#if DEBUG
            Validation.Assert(indexMin <= index & index < _items.Count);
            var info = _items[index];
#endif
            _keyToIndex[kvp.Key] = index;
        }
        _indexMin = _items.Count;
    }

    protected void AddItem(TKey key, TItem info)
    {
        Validation.BugCheckValue(info, nameof(info));

        if (_keyToIndex.TryGetValue(key, out int index))
            Validation.BugCheckParam(0 <= index && index < _indexMin, nameof(key), "Duplicate registration");

        _items.Add(info);
        _keyToIndex[key] = _items.Count - 1;
    }

    protected bool TryGetItem(TKey key, out TItem item)
    {
        if (!_keyToIndex.TryGetValue(key, out int index))
        {
            item = null;
            return false;
        }

        Validation.Assert(0 <= index && index < _items.Count);
        item = _items[index];
        if (item is not null)
            return true;

        return false;
    }

    protected TItem GetItem(TKey key)
    {
        if (!_keyToIndex.TryGetValue(key, out int index))
            return null;
        Validation.Assert(0 <= index && index < _items.Count);
        return _items[index];
    }

    protected IEnumerable<(TKey, TItem)> GetItems()
    {
        foreach (var kvp in _keyToIndex)
        {
            int index = kvp.Value;
            Validation.Assert(0 <= index && index < _items.Count);
            yield return (kvp.Key, _items[index]);
        }
    }
}
