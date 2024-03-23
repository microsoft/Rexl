// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using BndSymTuple = Immutable.Array<ModSym>;
using GlobalTable = ReadOnly.Dictionary<NPath, NPath>;
using ScopeMap = Dictionary<ArgScope, ArgScope>;

/// <summary>
/// The result of binding a parsed module.
/// </summary>
public sealed partial class BndModuleNode : BndScopeOwnerNode
{
    // REVIEW: Currently ScopeItems is always non-null even if it is never used.
    // Is it worth allowing it to be null? Probably not.
    public override bool OwnsScopes => true;

    /// <summary>
    /// This is the (optional) scope for the module external references tuple, used for building the module.
    /// Symbol expressions within this module may reference slots of this scope.
    /// </summary>
    public ArgScope ScopeExt { get; }

    /// <summary>
    /// This is the scope for the items tuple, used for building the module.
    /// Symbol expressions within this module may reference fields of this scope.
    /// </summary>
    public ArgScope ScopeItems { get; }

    /// <summary>
    /// Gets whether there were binding errors.
    /// </summary>
    public bool IsInvalid { get; }

    /// <summary>
    /// The mapping from symbol name to index.
    /// REVIEW: The dictionary should be immutable. Perhaps we should use a red-black tree?
    /// </summary>
    public ReadOnly.Dictionary<DName, int> NameToIndex { get; }

    /// <summary>
    /// Maps from item index to one more than the corresponding symbol index. When a symbol has multiple
    /// corresponding items, only the "value" item is mapped with the remaining entries being zero.
    /// </summary>
    public Immutable.Array<int> IfmaToJsym { get; }

    /// <summary>
    /// The symbols. These contain indices into <see cref="Items"/>.
    /// </summary>
    public BndSymTuple Symbols { get; }

    /// <summary>
    /// These are the external references used by the symbol formulas. This may be empty.
    /// </summary>
    public ArgTuple Externals { get; }

    /// <summary>
    /// A tuple type for holding all the external values.
    /// </summary>
    public DType TypeExts => ScopeExt.Type;

    /// <summary>
    /// These are the formulas for the symbols. There is at least one formula for each symbol.
    /// Free variable symbols may have multiple associated items. The symbols contain indices into this.
    /// </summary>
    public ArgTuple Items { get; }

    /// <summary>
    /// A tuple type for holding all the item values.
    /// </summary>
    public DType TypeItems => ScopeItems.Type;

    /// <summary>
    /// The record type corresponding to the module type.
    /// </summary>
    public DType TypeRec { get; }

    public static BndModuleNode Create(ArgScope scopeExt, ArgScope scopeItems, DType type,
            ReadOnly.Dictionary<DName, int> nameToIndex, BndSymTuple symbols,
            ArgTuple externals, ArgTuple items, bool invalid)
    {
        // REVIEW: There is much more validation that could be done.
        if (scopeExt is not null)
        {
            Validation.BugCheckParam(scopeExt.Kind == ScopeKind.With, nameof(scopeExt));
            Validation.BugCheckParam(scopeExt.Type.IsTupleReq, nameof(scopeExt));
            Validation.BugCheckParam(!externals.IsDefaultOrEmpty, nameof(externals));
            Validation.BugCheckParam(externals.Length == scopeExt.Type.TupleArity, nameof(externals));
        }
        else
        {
            Validation.BugCheckParam(externals.IsDefaultOrEmpty, nameof(externals));
            externals = ArgTuple.Empty;
        }

        Validation.BugCheckValue(scopeItems, nameof(scopeItems));
        Validation.BugCheckParam(scopeItems.Kind == ScopeKind.With, nameof(scopeItems));
        Validation.BugCheckParam(scopeItems.Type.IsTupleReq, nameof(scopeItems));

        Validation.BugCheckParam(type.IsModuleReq, nameof(type));
        Validation.BugCheckParam(!nameToIndex.IsDefault, nameof(nameToIndex));
        Validation.BugCheckParam(nameToIndex.Count == type.SymbolCount, nameof(nameToIndex));
        Validation.BugCheckParam(!symbols.IsDefault, nameof(symbols));
        Validation.BugCheckParam(symbols.Length == type.SymbolCount, nameof(symbols));
        Validation.BugCheckParam(!items.IsDefault, nameof(items));
        Validation.BugCheckParam(items.Length == scopeItems.Type.TupleArity, nameof(items));

        if (externals.IsDefault)
            externals = ArgTuple.Empty;

        var bldr = Immutable.Array<int>.CreateBuilder(items.Length, init: true);
        for (int i = 0; i < symbols.Length; i++)
        {
            var sym = symbols[i];
            int ifma = sym.IfmaValue;
            Validation.Assert(bldr[ifma] == 0);
            bldr[ifma] = i + 1;
        }

        return new BndModuleNode(scopeExt, scopeItems, type,
            nameToIndex, symbols, externals, items, bldr.ToImmutable(), invalid);
    }

    private BndModuleNode(ArgScope scopeExt, ArgScope scopeItems, DType type,
            ReadOnly.Dictionary<DName, int> nameToIndex, BndSymTuple symbols,
            ArgTuple externals, ArgTuple items, Immutable.Array<int> ifmaToJsym, bool invalid)
        : base(type, externals.Length + items.Length, GetKinds(externals) | GetKinds(items),
            GetCount(externals) + GetCount(items))
    {
        Validation.AssertValueOrNull(scopeExt);
        Validation.AssertValue(scopeItems);
        Validation.Assert(scopeItems.Type.IsTupleReq);
        Validation.Assert(type.IsModuleReq);
        Validation.Assert(!nameToIndex.IsDefault);
        Validation.Assert(!symbols.IsDefault);
        Validation.Assert(symbols.Length == nameToIndex.Count);
        Validation.Assert(!externals.IsDefault);
        Validation.Assert(!items.IsDefault);
        Validation.Assert(items.Length == scopeItems.Type.TupleArity);
        Validation.Assert(!ifmaToJsym.IsDefault);
        Validation.Assert(ifmaToJsym.Length == items.Length);

        ScopeExt = scopeExt;
        ScopeItems = scopeItems;
        NameToIndex = nameToIndex;
        Symbols = symbols;
        Externals = externals;
        Items = items;
        IfmaToJsym = ifmaToJsym;
        IsInvalid = invalid;
        TypeRec = type.ModuleToRecord();
    }

    /// <summary>
    /// Produce a new instance with updated externals and items.
    /// </summary>
    public BndModuleNode SetItems(ArgTuple externals, ArgTuple items)
    {
        Validation.BugCheckParam(!externals.IsDefault, nameof(externals));
        Validation.BugCheckParam(externals.Length == Externals.Length, nameof(externals));
        Validation.BugCheckParam(!items.IsDefault, nameof(items));
        Validation.BugCheckParam(items.Length == Items.Length, nameof(items));

        for (int i = 0; i < externals.Length; i++)
        {
            var src = Externals[i];
            var dst = externals[i];
            Validation.BugCheckParam(dst.Type == src.Type, nameof(externals));
        }

        for (int i = 0; i < items.Length; i++)
        {
            var src = Items[i];
            var dst = items[i];
            Validation.BugCheckParam(dst.Type == src.Type, nameof(items));
        }

        return new BndModuleNode(ScopeExt, ScopeItems, Type, NameToIndex, Symbols,
            externals, items, IfmaToJsym, IsInvalid);
    }

    /// <summary>
    /// The item dependencies, computed when first needed.
    /// </summary>
    private Immutable.Array<BitSet> _depsItem;

    /// <summary>
    /// Gets an array of bit sets, one for each item. The bit set for an item contains the indices
    /// of earlier items that the item depends on.
    /// </summary>
    public Immutable.Array<BitSet> GetItemDependencies()
    {
        // Note that since immutable array just wraps an array reference, struct tearing is not
        // an issue, so this should be thread safe.
        if (_depsItem.IsDefault)
        {
            int num = Items.Length;
            var bldr = Immutable.Array<BitSet>.CreateBuilder(num, init: true);
            var flags = new bool[num];
            for (int i = 0; i < num; i++)
            {
                DepFinder.Run(ScopeItems, flags, i, Items[i]);
                bldr[i] = new BitSet(flags.AsSpan(0, i));
            }
            _depsItem = bldr.ToImmutable();
        }
        return _depsItem;
    }

    /// <summary>
    /// The variable dependencies, computed when first needed.
    /// </summary>
    private Immutable.Array<BitSet> _depsVar;

    /// <summary>
    /// Gets an array of bit sets, one for each symbol. For constants and free variables, the
    /// corresponding bit set is empty. For computed variables, the bit set contains the set of
    /// variable symbol indices that the computed variable depends on.
    /// </summary>
    public Immutable.Array<BitSet> GetVarDependencies()
    {
        // Note that since immutable array just wraps an array reference, struct tearing is not
        // an issue, so this should be thread safe.
        if (_depsVar.IsDefault)
        {
            int num = Symbols.Length;
            var bldr = Immutable.Array<BitSet>.CreateBuilder(num, init: true);
            var flags = new bool[num];
            var depsItem = GetItemDependencies();
            for (int i = 0; i < Symbols.Length; i++)
            {
                var sym = Symbols[i];
                if (sym.IsConstantSym)
                    continue;

                int ifma = sym.IfmaValue;
                if (!sym.IsComputedVarSym)
                    continue;

                Array.Clear(flags);
                var dep = depsItem[ifma];
                Validation.Assert(!dep.TestAtOrAbove(ifma));
                foreach (var k in dep)
                {
                    Validation.AssertIndex(k, ifma);
                    int isym = IfmaToJsym[k] - 1;
                    Validation.AssertIndex(isym, i);
                    if (Symbols[isym].IsVariableSym)
                        flags[isym] = true;
                }
                bldr[i] = new BitSet(flags.AsSpan(0, i));
            }
            _depsVar = bldr.ToImmutable();
        }
        return _depsVar;
    }

    /// <summary>
    /// Finds dependencies on items.
    /// </summary>
    private sealed class DepFinder : NoopBoundTreeVisitor
    {
        private readonly ArgScope _scope;
        private readonly bool[] _flags;
        private readonly int _lim;

        public static void Run(ArgScope scope, bool[] flags, int lim, BoundNode bnd)
        {
            Array.Clear(flags);
            var impl = new DepFinder(scope, flags, lim);
            bnd.Accept(impl, 0);
        }

        private DepFinder(ArgScope scope, bool[] flags, int lim)
            : base()
        {
            _scope = scope;
            _flags = flags;
            _lim = lim;
        }

        protected override bool PreVisitCore(BndParentNode bnd, int idx)
        {
            // Need both scope ref and get slot.
            const BndNodeKindMask mask = BndNodeKindMask.GetSlot | BndNodeKindMask.ArgScopeRef;
            if ((bnd.ChildKinds & mask) != mask)
                return false;
            return true;
        }

        protected override bool PreVisitImpl(BndGetSlotNode bnd, int idx)
        {
            if (bnd.Slot >= _lim)
                return true;
            if (bnd.Tuple is not BndScopeRefNode bsr)
                return true;
            if (bsr.Scope == _scope)
                _flags[bnd.Slot] = true;
            return false;
        }
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        foreach (var ext in Externals)
            cur = ext.Accept(visitor, cur);
        foreach (var item in Items)
            cur = item.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndModuleNode;
        Validation.Assert(other != null);

        if (IsInvalid != other.IsInvalid)
            return false;
        if (Symbols.Length != other.Symbols.Length)
            return false;
        if (Externals.Length != other.Externals.Length)
            return false;
        if (Items.Length != other.Items.Length)
            return false;

        if ((ScopeExt is null) != (other.ScopeExt is null))
            return false;
        if (ScopeExt is not null && ScopeExt.Type != other.ScopeExt.Type)
            return false;
        if (ScopeItems.Type != other.ScopeItems.Type)
            return false;

        if (!Symbols.AreIdentical(other.Symbols))
        {
            for (int i = 0; i < Symbols.Length; i++)
            {
                var sym0 = Symbols[i];
                var sym1 = other.Symbols[i];
                // The set of name/kind pairs is the same because the types are the same,
                // but they may be permuted. When permuted, we assume they are different
                // in some meaningful way.
                if (sym0.SymKind != sym1.SymKind)
                    return false;
                if (sym0.Name != sym1.Name)
                    return false;
                if (sym0.FmaCount != sym1.FmaCount)
                    return false;
                Validation.Assert(sym0.Type == sym1.Type);
                switch (sym0)
                {
                case ModFmaSym mfs0:
                    if (sym1 is not ModFmaSym mfs1)
                        return false;
                    if (mfs0.IfmaValue != mfs1.IfmaValue)
                        return false;
                    break;
                case ModItemVar miv0:
                    if (sym1 is not ModItemVar miv1)
                        return false;
                    if (miv0.IsOpt != miv1.IsOpt)
                        return false;
                    if (miv0.FormulaIn != miv1.FormulaIn)
                        return false;
                    if (miv0.FormulaDefault != miv1.FormulaDefault)
                        return false;
                    break;
                case ModSimpleVar msv0:
                    if (sym1 is not ModSimpleVar msv1)
                        return false;
                    if (msv0.IsOpt != msv1.IsOpt)
                        return false;
                    if (msv0.FormulaFrom != msv1.FormulaFrom)
                        return false;
                    if (msv0.FormulaTo != msv1.FormulaTo)
                        return false;
                    if (msv0.FormulaDefault != msv1.FormulaDefault)
                        return false;
                    break;
                }
            }
        }

        if (!EquivArgs(Externals, other.Externals, scopeMap, globalTable))
            return false;

        int count = Util.Size(scopeMap);
        if (ScopeExt is not null)
            Util.Add(ref scopeMap, ScopeExt, other.ScopeExt);
        Util.Add(ref scopeMap, ScopeItems, other.ScopeItems);
        try
        {
            if (!EquivArgs(Items, other.Items, scopeMap, globalTable))
                return false;
        }
        finally
        {
            if (ScopeExt is not null)
                scopeMap.Remove(ScopeExt);
            scopeMap.Remove(ScopeItems);
            Validation.Assert(Util.Size(scopeMap) == count);
        }

        return true;
    }
}

/// <summary>
/// Converts a module to its record of values.
/// </summary>
public sealed partial class BndModToRecNode : BndCastNode
{
    private BndModToRecNode(BoundNode mod)
        : base(mod, mod.VerifyValue().Type.ModuleToRecord())
    {
    }

    public static BndModToRecNode Create(BoundNode mod)
    {
        Validation.BugCheckValue(mod, nameof(mod));
        Validation.BugCheckParam(mod.Type.IsModuleReq, nameof(mod));

        return new BndModToRecNode(mod);
    }

    private protected override BoundNode SetChildCore(BoundNode child, IReducerHost host = null)
    {
        Validation.AssertValue(child, nameof(child));
        Validation.Assert(child.Type == Child.Type);

        return new BndModToRecNode(child);
    }
}

/// <summary>
/// Handles setting parameter and free variable values in a module (to produce a new module).
/// </summary>
public sealed partial class BndModuleProjectionNode : BndScopeOwnerNode
{
    public override bool OwnsScopes => true;

    /// <summary>
    /// The source module.
    /// </summary>
    public BoundNode Module { get; }

    /// <summary>
    /// The scope for evaluation of the symbol values.
    /// </summary>
    public ArgScope Scope { get; }

    /// <summary>
    /// The record containing the new symbol values.
    /// </summary>
    public BoundNode Record { get; }

    private BndModuleProjectionNode(BoundNode mod, ArgScope scope, BoundNode rec)
        : base(mod.VerifyValue().Type, 2, GetKinds(mod) | GetKinds(rec), GetCount(mod) + GetCount(rec))
    {
        Module = mod;
        Scope = scope;
        Record = rec;
    }

    public static BoundNode Create(BoundNode mod, ArgScope scope, BoundNode rec)
    {
        Validation.BugCheckValue(mod, nameof(mod));
        Validation.BugCheckParam(mod.Type.IsModuleReq, nameof(mod));
        Validation.BugCheckValue(scope, nameof(scope));
        Validation.BugCheckParam(scope.Kind == ScopeKind.With && scope.Type == mod.Type, nameof(scope));
        Validation.BugCheckValue(rec, nameof(rec));
        Validation.BugCheckParam(rec.Type.IsSubRecord(mod.Type.ModuleToRecord()), nameof(rec));

        var typeSrc = mod.Type;
        var typeRec = rec.Type;
        foreach (var tn in typeRec.GetNames())
        {
            Validation.BugCheckParam(typeSrc.TryGetSymbolNameType(tn.Name, out var typeSym, out var symKind), nameof(rec));
            Validation.BugCheckParam(symKind.IsSettable(), nameof(rec));
            Validation.BugCheckParam(typeSym == tn.Type, nameof(rec));
        }

        // REVIEW: This asserts if something is wrong but it should really check.
        VerifyScopeDecls(rec, default, scope);

        return new BndModuleProjectionNode(mod, scope, rec);
    }

    protected override int AcceptChildren(BoundTreeVisitor visitor, int cur)
    {
        cur = Module.Accept(visitor, cur);
        cur = Record.Accept(visitor, cur);
        return cur;
    }

    private protected override bool Equiv(BoundNode bnd, ScopeMap scopeMap, GlobalTable globalTable)
    {
        var other = bnd as BndModuleProjectionNode;
        Validation.Assert(other != null);
        Validation.Assert(other.Type == Type);

        if (Record.Type != other.Record.Type)
            return false;
        if (!Module.Equivalent(other.Module, scopeMap, globalTable))
            return false;
        Validation.Assert(Scope != null);
        Validation.Assert(other.Scope != null);

        int count = Util.Size(scopeMap);
        Util.Add(ref scopeMap, Scope, other.Scope);
        try
        {
            if (!Record.Equivalent(other.Record, scopeMap, globalTable))
                return false;
        }
        finally
        {
            scopeMap.Remove(Scope);
            Validation.Assert(count == Util.Size(scopeMap));
        }

        return true;
    }
}
