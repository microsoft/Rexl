// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Symbolic;

/// <summary>
/// Maps from named variable symbol (free variable or computed variable) of a particular module to an
/// "entry" holding scalar variable information. The client explicitly specifies which variable symbols
/// to include, since some may be unneeded. In particular, this is NOT auto populated by the constructor.
/// 
/// A main purpose of this is to track "scalar variables" associated with "module free variables".
/// Each scalar variable has an int "id". The id values start at zero and are contiguous. Each
/// module free variable is assigned a contiguous range of scalar variable ids.
/// 
/// This supports multiple kinds of "module free variables":
/// * A scalar value, eg, single number, corresponding to a single variable id.
/// * A subsequence of a sequence domain. The underlying variables are bit/bool, one for
///   each item in the domain. The expansion is:
///     ForEachIf(v:[x0,x1,...], d:Domain, v, d)
/// * A variable that is an item of a sequence/set.
/// * A tensor which corresponds to many variable ids, one for each cell of the tensor.
/// </summary>
public sealed partial class SymbolMap
{
    // This holds a code generator, since the symbol reducer needs one and this invokes
    // symbol reduction in TryAddSymbol.
    private readonly CodeGeneratorBase _codeGen;

    // The runtime module.
    private readonly RuntimeModule _module;

    // The host for reduction.
    private readonly IReducerHost _host;

    /// <summary>
    /// The code generator associated with the <see cref="RuntimeModule"/>.
    /// </summary>
    public CodeGeneratorBase CodeGen => _codeGen;

    /// <summary>
    /// Maps from symbol name (as an <see cref="NPath"/>) to the entry for the symbol.
    /// </summary>
    private readonly Dictionary<DName, SymbolEntry> _entries;

    /// <summary>
    /// Maps from int "id" to <see cref="BndFreeVarNode"/>.
    /// </summary>
    private readonly List<BndFreeVarNode> _idToSvar;

    /// <summary>
    /// The items from the module. Populated if/when needed.
    /// </summary>
    private (DType type, Type st, object val)[] _items;

    /// <summary>
    /// The externals from the module. Populated if/when needed.
    /// </summary>
    private (DType type, Type st, object val)[] _exts;

    /// <summary>
    /// Cache of <see cref="BndConstNode"/> instances for items and externals. Populated if/when needed.
    /// </summary>
    private BndConstNode[] _bcns;

    /// <summary>
    /// Create a <see cref="SymbolMap"/> compatible with the given <see cref="RuntimeModule"/>.
    /// REVIEW: Perhaps this should just accept the code generator, which is all that it
    /// really needs.
    /// </summary>
    public SymbolMap(CodeGeneratorBase codeGen, RuntimeModule module, IReducerHost host)
    {
        Validation.BugCheckValue(codeGen, nameof(codeGen));
        Validation.BugCheckValue(module, nameof(module));
        Validation.BugCheckValueOrNull(host);

        _codeGen = codeGen;
        _module = module;

        _host = host;

        _entries = new Dictionary<DName, SymbolEntry>();
        _idToSvar = new List<BndFreeVarNode>();
    }

    public RuntimeModule Module => _module;

    /// <summary>
    /// The associated reduction host. May be null.
    /// </summary>
    public IReducerHost Host => _host;

    /// <summary>
    /// The total number of variables so far. The variable ids are zero up to this.
    /// </summary>
    public int VarCount => _idToSvar.Count;

    /// <summary>
    /// Whether this contains information for the given symbol name.
    /// </summary>
    public bool HasSymbol(DName name) => _entries.ContainsKey(name);

    /// <summary>
    /// If an entry exists for the given symbol name, produces it and returns true.
    /// </summary>
    public bool TryGetSymbol(DName name, out SymbolEntry entry) => _entries.TryGetValue(name, out entry);

    /// <summary>
    /// Ensure that the variable has been created for the symbol with the given <paramref name="name"/>.
    /// Throws if there is no symbol in the map with the given name or that symbol is not a basic
    /// variable or computed variable.
    /// </summary>
    public BndFreeVarNode EnsureVariable(DName name)
    {
        Validation.BugCheckParam(_entries.TryGetValue(name, out var entry), nameof(name));
        if (entry is BasicVarEntry bve)
            return bve.Svar;
        if (entry is ComputedVarEntry cve && cve.Type.IsPrimitiveXxx)
            return cve.EnsureVariable(this);
        throw Validation.BugExceptParam(nameof(name));
    }

    /// <summary>
    /// Returns the <see cref="BndFreeVarNode"/> corresponding to the given <paramref name="id"/>.
    /// Throws if there isn't one.
    /// </summary>
    public BndFreeVarNode GetVariable(int id)
    {
        Validation.BugCheckIndex(id, _idToSvar.Count, nameof(id));
        var svar = _idToSvar[id];
        Validation.Assert(svar.Id == id);
        return svar;
    }

    /// <summary>
    /// Iterates the variables in the map.
    /// </summary>
    public IEnumerable<SymbolEntry> GetEntries()
    {
        foreach (var entry in _entries.Values)
        {
            if (entry.Symbol.IsVariableSym)
                yield return entry;
        }
    }

    /// <summary>
    /// Throws if adding <paramref name="size"/> variables would cause the variable ids to overflow 31 bits.
    /// </summary>
    private void CheckVarSize(long size)
    {
        // REVIEW: What should we do on overflow?
        if (size > int.MaxValue - _idToSvar.Count)
            throw new OutOfMemoryException();
    }

    private BndConstNode GetBcn(DType type, object val)
    {
        if (!_codeGen.TypeManager.TryWrapConst(type, val, out var bcn))
            throw new NotSupportedException("Wrapping const as bcn failed");
        return bcn;
    }

    private (DType type, Type st, object val)[] EnsureItems()
    {
        return _items ??= _codeGen.TypeManager.GetTupleSlotValues(_module.Bnd.TypeItems, _module.GetItems());
    }

    private (DType type, Type st, object val)[] EnsureExternals()
    {
        return _exts ??= _codeGen.TypeManager.GetTupleSlotValues(_module.Bnd.TypeExts, _module.GetExternals());
    }

    /// <summary>
    /// Get a <see cref="BndConstNode"/> for the indicated item.
    /// Note that the type of the result may be non-opt while the type of the item is opt.
    /// </summary>
    public BndConstNode GetItemBcn(int ifma)
    {
        if (ifma < 0)
            return null;

        int citem = _module.Bnd.Items.Length;
        Validation.BugCheckIndex(ifma, citem, nameof(ifma));

        BndConstNode res = null;
        if (_bcns is null)
            _bcns = new BndConstNode[citem + _module.Bnd.Externals.Length];
        else if ((res = _bcns[ifma]) is not null)
            return res;

        // This shouldn't be used for variable values.
        int isym = _module.Bnd.IfmaToJsym[ifma] - 1;
        Validation.BugCheckParam(isym < 0 || _module.Bnd.Symbols[isym].IsConstantSym, nameof(ifma));

        var info = EnsureItems()[ifma];
        res = GetBcn(info.type, info.val);
        Validation.Assert(res is not null);
        Validation.Assert(res.Type == info.type | res.Type.ToOpt() == info.type);
        _bcns[ifma] = res;

        return res;
    }

    /// <summary>
    /// Get a <see cref="BndConstNode"/> for the indicated external.
    /// Note that the type of the result may be non-opt while the type of the external is opt.
    /// </summary>
    public BndConstNode GetExtBcn(int slot)
    {
        int cext = _module.Bnd.Externals.Length;
        Validation.BugCheckIndex(slot, cext, nameof(slot));

        int citem = _module.Bnd.Items.Length;
        BndConstNode res = null;
        if (_bcns is null)
            _bcns = new BndConstNode[citem + _module.Bnd.Externals.Length];
        else if ((res = _bcns[citem + slot]) is not null)
            return res;

        var info = EnsureExternals()[slot];
        res = GetBcn(info.type, info.val);
        Validation.Assert(res is not null);
        Validation.Assert(res.Type == info.type | res.Type.ToOpt() == info.type);
        _bcns[citem + slot] = res;

        return res;
    }

    /// <summary>
    /// Get the value for the given item. If <paramref name="ifma"/> is less than zero,
    /// returns <c>null</c>.
    /// </summary>
    public object GetItemValue(int ifma)
    {
        if (ifma < 0)
            return null;

        int citem = _module.Bnd.Items.Length;
        Validation.BugCheckIndex(ifma, citem, nameof(citem));

        var info = EnsureItems()[ifma];
        return info.val;
    }

    /// <summary>
    /// Get the value for the given item. If <paramref name="ifma"/> is less than zero,
    /// returns <c>null</c>.
    /// </summary>
    public object GetItemValue(int ifma, out DType type, out Type st)
    {
        if (ifma < 0)
        {
            type = default;
            st = null;
            return null;
        }

        int citem = _module.Bnd.Items.Length;
        Validation.BugCheckIndex(ifma, citem, nameof(citem));

        object value;
        (type, st, value) = EnsureItems()[ifma];
        return value;
    }

    /// <summary>
    /// Adds the given <paramref name="sym"/> to this map. If successful, sets <paramref name="entry"/> to the
    /// new entry. Otherwise, sets <paramref name="bad"/> to information that can be used to report the error.
    /// </summary>
    public bool TryAddSymbol(ModSym sym, out SymbolEntry entry, out (BoundNode bnd, object value) bad)
    {
        Validation.BugCheckValue(sym, nameof(sym));
        Validation.BugCheckParam(sym.IsVariableSym, nameof(sym));
        Validation.BugCheckParam(!_entries.ContainsKey(sym.Name), nameof(sym));

        bad = default;

        if (sym is ModSimpleVar svi)
        {
            if (svi.Type.IsSequence)
            {
                // The sequence case shouldn't have a "to", but should have a "from".
                Validation.Assert(svi.FormulaTo < 0);
                Validation.Assert(svi.FormulaFrom >= 0);
                var min = GetItemBcn(svi.FormulaFrom);
                Array arr = null;
                if (min is BndArrConstNode keys && (arr = keys.Items).Length > 0)
                {
                    CheckVarSize(keys.Length);
                    entry = SubsetVarEntry.Create(this, svi, keys);
                    return true;
                }
                entry = null;
                bad = (_module.Bnd.Items[svi.FormulaFrom], arr);
                return false;
            }

            if (svi.Type.IsTensorXxx)
            {
                var tenMin = GetItemValue(svi.FormulaFrom) as Tensor;
                var tenMax = GetItemValue(svi.FormulaTo) as Tensor;
                var tenDef = GetItemValue(svi.FormulaDefault) as Tensor;

                var ten = tenDef ?? tenMin ?? tenMax;
                if (ten is null)
                {
                    entry = null;
                    return false;
                }

                var shape = ten.Shape;
                if (shape.Rank == 0 || !shape.TryGetCount(out long size))
                {
                    entry = null;
                    int index = svi.FormulaFrom;
                    if (index < 0)
                        index = svi.FormulaTo;
                    if (index < 0)
                        index = svi.FormulaDefault;
                    Validation.AssertIndex(index, _module.Bnd.Items.Length);
                    bad = (_module.Bnd.Items[index], null);
                    return false;
                }

                if (tenMin != null && tenMin.Shape != shape)
                {
                    entry = null;
                    bad = (_module.Bnd.Items[svi.FormulaFrom], tenMin);
                    return false;
                }
                if (tenMax != null && tenMax.Shape != shape)
                {
                    entry = null;
                    bad = (_module.Bnd.Items[svi.FormulaTo], tenMax);
                    return false;
                }

                CheckVarSize(size);
                entry = TensorVarEntry.Create(this, svi, shape, (int)size);
                return true;
            }

            entry = BasicVarEntry.Create(this, svi);
            return true;
        }

        if (sym is ModItemVar ivi)
        {
            Validation.Assert(ivi.FormulaIn >= 0);
            var dom = GetItemBcn(ivi.FormulaIn);
            Validation.Assert(dom is not null);
            Array arr = null;
            if (dom is BndArrConstNode keys && (arr = keys.Items).Length > 0)
            {
                CheckVarSize(keys.Length);
                entry = ItemVarEntry.Create(this, ivi, keys);
                return true;
            }
            entry = null;
            bad = (_module.Bnd.Items[ivi.FormulaIn], arr);
            return false;
        }

        if (sym is ModComputedVar cvi)
        {
            // This is a computed variable, so we must use the formula, NOT the current "value".
            var value = SymbolReducer.Simplify(this, _module.Bnd.Items[cvi.IfmaValue], expandSelect: false);
            // REVIEW: Does this work for sequences and tensors?
            // REVIEW: What if it ends up being "constant" (ie, a BndConstNode)?
            entry = ComputedVarEntry.Create(this, cvi, value);
            return true;
        }

        // The above should be exhaustive.
        Validation.Assert(false);
        entry = null;
        return false;
    }
}
