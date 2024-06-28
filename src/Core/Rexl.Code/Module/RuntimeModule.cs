// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.Intrinsics.X86;
using System.Threading;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

/// <summary>
/// Represents a runtime module value.
/// </summary>
public abstract class RuntimeModule
{
    /// <summary>
    /// This is the "template" for the runtime module value.
    /// REVIEW: We really don't need to keep all of this around. We need:
    /// * The mapping from symbol names to item slots.
    /// * Formulas that may be used for symbolic reduction, which are only the (value) formulas for
    ///   computed variables. The formulas for parameters, constants, and free variables will never
    ///   be needed for symbolic reduction.
    /// </summary>
    public readonly BndModuleNode Bnd;

    private protected RuntimeModule(BndModuleNode bnd)
    {
        Validation.AssertValue(bnd);
        Bnd = bnd;
    }

    /// <summary>
    /// Get the symbol values in record form.
    /// </summary>
    /// <returns></returns>
    public abstract RecordBase GetRecord();

    /// <summary>
    /// Get the items tuple.
    /// </summary>
    public abstract TupleBase GetItems();

    /// <summary>
    /// Get the externals tuple.
    /// </summary>
    public abstract TupleBase GetExternals();

    /// <summary>
    /// Update values for the parameters and free variables indicated by <paramref name="names"/>,
    /// getting the new values from the given record. Returns the new runtime module value. This
    /// casts <paramref name="values"/> to the correct record type.
    /// </summary>
    public abstract RuntimeModule UpdateRaw(RecordBase values, HashSet<DName> names);
}

/// <summary>
/// Represents a runtime module value with the associated record system type.
/// </summary>
public abstract class RuntimeModule<TRec> : RuntimeModule
    where TRec : RecordBase
{
    private protected RuntimeModule(BndModuleNode bnd)
        : base(bnd)
    {
    }

    public abstract override TRec GetRecord();

    public sealed override RuntimeModule UpdateRaw(RecordBase values, HashSet<DName> names)
    {
        if (values is not TRec rec)
            throw Validation.BugExceptParam(nameof(values));
        return Update(rec, names);
    }

    public abstract RuntimeModule<TRec> Update(TRec values, HashSet<DName> names);

    public abstract RuntimeModule<TRec> Update(List<(DName name, object value)> symValues);
}

/// <summary>
/// Represents a runtime module value with the associated record and items tuple system types.
/// </summary>
public abstract class RuntimeModuleBase<TRec, TItems> : RuntimeModule<TRec>
    where TRec : RecordBase
    where TItems : TupleBase
{
    /// <summary>
    /// This makes the record value from the items tuple.
    /// </summary>
    protected readonly Func<TItems, TRec> _makeRec;

    /// <summary>
    /// The items tuple.
    /// </summary>
    protected readonly TItems _items;

    /// <summary>
    /// These are the item indices that are "locked" and should not be recomputed when projecting.
    /// </summary>
    protected readonly BitSet _locked;

    /// <summary>
    /// The associated record value. Created lazily only when needed.
    /// </summary>
    private volatile TRec _rec;

    private protected RuntimeModuleBase(BndModuleNode bnd, Func<TItems, TRec> makeRec, TItems items, BitSet locked)
        : base(bnd)
    {
        Validation.AssertValue(makeRec);
        Validation.AssertValue(items);
        _makeRec = makeRec;
        _items = items;
        _locked = locked;
    }

    public sealed override TupleBase GetItems() => _items;

    public sealed override TRec GetRecord()
    {
        var rec = _rec;
        if (rec is not null)
            return rec;
        Interlocked.CompareExchange(ref _rec, _makeRec(_items), null);
        return _rec;
    }

    /// <summary>
    /// Compute the flags needed by the update delegate. The resulting array of bools has one slot
    /// for each item and one slot for each symbol. True in an item slot means "don't compute" that item.
    /// True in a symbol slot means "copy the symbol value from the input record".
    /// </summary>
    protected bool[] GetFlags(HashSet<DName> names, out BitSet locked)
    {
        Validation.AssertValue(names);

        // Flags contains two blocks:
        // * A bool for each item, with true meaning don't recompute the item.
        // * A bool for each symbol, with true meaning that the symbol value should be copied from
        //   the input record.
        int citem = Bnd.Items.Length;
        var flags = new bool[Bnd.Items.Length + Bnd.Symbols.Length];
        locked = _locked;
        for (int isym = 0; isym < Bnd.Symbols.Length; isym++)
        {
            var sym = Bnd.Symbols[isym];
            Validation.Assert(sym.Index == isym);
            if (!names.Contains(sym.Name))
                continue;
            int ifma = sym.IfmaValue;
            switch (sym.SymKind)
            {
            case ModSymKind.Parameter:
            case ModSymKind.FreeVariable:
                flags[citem + isym] = true;
                flags[ifma] = true;
                locked = locked.SetBit(ifma);
                break;
            default:
                Validation.Assert(false);
                break;
            }
        }

        CloseFlags(flags, locked);
        return flags;
    }

    private void CloseFlags(bool[] flags, BitSet locked)
    {
        int citem = Bnd.Items.Length;
        var chgs = new BitSet(flags.AsSpan(0, citem));
        var deps = Bnd.GetItemDependencies();
        for (int i = 0; i < citem; i++)
        {
            Validation.Assert(chgs.TestBit(i) == flags[i]);
            if (locked.TestBit(i))
            {
                flags[i] = true;
                continue;
            }
            var dep = deps[i];
            Validation.Assert(!dep.TestAtOrAbove(i));
            if (dep.Intersects(chgs))
                chgs = chgs.SetBit(i);
            else
                flags[i] = true;
        }
    }

    protected bool[] SetSlots(List<(DName name, object value)> symValues,
        out TItems items, out BitSet locked, out int count)
    {
        int citem = Bnd.Items.Length;
        var flags = new bool[Bnd.Items.Length + Bnd.Symbols.Length];
        var sets = new List<(int ifma, object value)>();
        locked = _locked;
        count = 0;
        foreach (var (name, value) in symValues)
        {
            if (!Bnd.NameToIndex.TryGetValue(name, out int isym))
            {
                // Bad name.
                Validation.Assert(false);
                continue;
            }

            Validation.AssertIndex(isym, Bnd.Symbols.Length);
            var sym = Bnd.Symbols[isym];
            int ifma = sym.IfmaValue;
            Validation.AssertIndex(ifma, Bnd.Items.Length);

            if (flags[ifma])
            {
                // Duplicate entry.
                continue;
            }

            switch (sym.SymKind)
            {
            case ModSymKind.Parameter:
            case ModSymKind.FreeVariable:
                flags[ifma] = true;
                locked = locked.SetBit(ifma);
                count++;
                break;
            default:
                // Not settable.
                Validation.Assert(false);
                continue;
            }

            sets.Add((ifma, value));
        }

        if (count == 0)
        {
            items = _items;
            return flags;
        }

        items = (TItems)_items.Clone();
        SetSlotsCore(items, sets);

        CloseFlags(flags, locked);
        return flags;
    }

    // REVIEW: SetSlotsCore should be code-gened somewhere and passed in as a delegate.
    private void SetSlotsCore(TItems items, IEnumerable<(int ifma, object value)> symValues)
    {
        foreach (var (ifma, value) in symValues)
        {
            var fin = typeof(TItems).GetField("_F" + ifma).VerifyValue();
            fin.SetValue(items, value);
        }
    }
}

/// <summary>
/// Represents a runtime module value for a module that has no external references.
/// </summary>
public sealed class RuntimeModule<TRec, TItems> : RuntimeModuleBase<TRec, TItems>
    where TRec : RecordBase
    where TItems : TupleBase
{
    /// <summary>
    /// This fills in the items tuple according to the given flags array and input record.
    /// The flags contains two blocks:
    /// * A bool for each item, with true meaning don't recompute the item.
    /// * A bool for each symbol, with true meaning that the symbol value should be copied from
    ///   the input record.
    /// If the symbols flags are all false, then the input record may be null.
    /// </summary>
    private readonly Func<bool[], TItems, TRec, TItems> _setItems;

    // Code generation depends on this signature. Do not delete it!
    public RuntimeModule(
            Func<bool[], TItems, TRec, TItems> setItems, TItems items,
            Func<TItems, TRec> makeRec, BndModuleNode bnd)
        : this(setItems, items, default, makeRec, bnd)
    {
    }

    private RuntimeModule(
            Func<bool[], TItems, TRec, TItems> setItems, TItems items, BitSet locked,
            Func<TItems, TRec> makeRec, BndModuleNode bnd)
        : base(bnd, makeRec, items, locked)
    {
        Validation.AssertValue(setItems);
        _setItems = setItems;
    }

    public override TupleBase GetExternals() => null;

    public override RuntimeModule<TRec, TItems> Update(TRec recVals, HashSet<DName> names)
    {
        Validation.AssertValue(recVals);
        Validation.AssertValue(names);

        var flags = GetFlags(names, out var locked);
        var items = _setItems(flags, (TItems)_items.Clone(), recVals);
        return new RuntimeModule<TRec, TItems>(_setItems, items, locked, _makeRec, Bnd);
    }

    public override RuntimeModule<TRec, TItems> Update(List<(DName name, object value)> symValues)
    {
        Validation.AssertValue(symValues);

        var flags = SetSlots(symValues, out var items, out var locked, out int count);
        if (count == 0)
            return this;

        items = _setItems(flags, items, null);
        return new RuntimeModule<TRec, TItems>(_setItems, items, locked, _makeRec, Bnd);
    }
}

public sealed class RuntimeModule<TRec, TItems, TExt> : RuntimeModuleBase<TRec, TItems>
    where TRec : RecordBase
    where TItems : TupleBase
    where TExt : TupleBase
{
    /// <summary>
    /// This fills in the items tuple according to the given flags array, input record, and external values.
    /// The flags contains two blocks:
    /// * A bool for each item, with true meaning don't recompute the item.
    /// * A bool for each symbol, with true meaning that the symbol value should be copied from
    ///   the input record.
    /// If the symbols flags are all false, then the input record may be null.
    /// </summary>
    private readonly Func<bool[], TItems, TRec, TExt, TItems> _setItems;

    public readonly TExt Externals;

    // Code generation depends on this signature. Do not delete it!
    public RuntimeModule(
            Func<bool[], TItems, TRec, TExt, TItems> setItems, TItems items,
            Func<TItems, TRec> makeRec, BndModuleNode bnd, TExt ext)
        : this(setItems, items, default, makeRec, bnd, ext)
    {
    }

    private RuntimeModule(
            Func<bool[], TItems, TRec, TExt, TItems> setItems, TItems items, BitSet locked,
            Func<TItems, TRec> makeRec, BndModuleNode bnd, TExt ext)
        : base(bnd, makeRec, items, locked)
    {
        Validation.AssertValue(setItems);
        Validation.AssertValue(ext);
        _setItems = setItems;
        Externals = ext;
    }

    public override TupleBase GetExternals() => Externals;

    public override RuntimeModule<TRec, TItems, TExt> Update(TRec recVals, HashSet<DName> names)
    {
        Validation.AssertValue(recVals);
        Validation.AssertValue(names);
        var flags = GetFlags(names, out var locked);
        var items = _setItems(flags, (TItems)_items.Clone(), recVals, Externals);
        return new RuntimeModule<TRec, TItems, TExt>(_setItems, items, locked, _makeRec, Bnd, Externals);
    }

    public override RuntimeModule<TRec, TItems, TExt> Update(List<(DName name, object value)> symValues)
    {
        Validation.AssertValue(symValues);

        var flags = SetSlots(symValues, out var items, out var locked, out int count);
        if (count == 0)
            return this;

        items = _setItems(flags, items, null, Externals);
        return new RuntimeModule<TRec, TItems, TExt>(_setItems, items, locked, _makeRec, Bnd, Externals);
    }
}
