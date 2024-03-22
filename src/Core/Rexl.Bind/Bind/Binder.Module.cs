// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Bind;

using ArgTuple = Immutable.Array<BoundNode>;
using BndSymTuple = Immutable.Array<ModSym>;
using DirTuple = Immutable.Array<Directive>;
using ScopeTuple = Immutable.Array<ArgScope>;

partial class BoundFormula
{
    partial class Binder
    {
        private ModuleBuilder _moduleCur;

        protected override bool PreVisitImpl(ModuleNode node)
        {
            Validation.AssertValue(node);

            int diagCount = Util.Size(_diagnostics);

            if ((_options & BindOptions.ProhibitModule) != 0)
                Error(node, ErrorStrings.ErrModuleNotSupported);

            var bldr = new ModuleBuilder(_moduleCur);
            _moduleCur = bldr;
            _scopeCur = ScopeWrapper.Create(_scopeCur, _moduleCur);
            _scopeCur.Enabled = true;

            foreach (var sym in node.Definitions.Children)
            {
                bldr.SetCurrent(sym);

                int index = bldr.Count;

                ModSym info;
                if (sym is ValueSymDeclNode vsn)
                {
                    // This handles everything except free variables.
                    Validation.Assert(vsn.SymKind.IsValid());
                    Validation.Assert(vsn.SymKind != ModSymKind.FreeVariable);

                    vsn.Value.Accept(this);
                    var bndVal = Pop();
                    DType type = bndVal.Type;
                    SetNodeTypeAndScope(sym, type);

                    var name = vsn.Name.Name;
                    if (bldr.TryFindSym(name, out _))
                    {
                        Error(sym.Name.Token, sym, ErrorStrings.ErrModuleDuplicateSymbolName_Name, name);
                        continue;
                    }

                    int ibnd = bldr.AddItem(bndVal);
                    switch (vsn.SymKind)
                    {
                    // Constant symbols.
                    case ModSymKind.Constant:
                        info = new ModConstant(index, name, type, ibnd, isParm: false);
                        break;
                    case ModSymKind.Parameter:
                        info = new ModConstant(index, name, type, ibnd, isParm: true);
                        break;

                    // Computed variable symbols.
                    case ModSymKind.Constraint:
                        bool isConstraint = type.RootType == DType.BitReq;
                        if (!isConstraint)
                            Error(vsn, ErrorStrings.ErrModuleConMustBeBool_Type, type);
                        info = new ModComputedVar(index, name, type, ibnd, isMeasure: false, isConstraint);
                        break;
                    case ModSymKind.Measure:
                        info = new ModComputedVar(index, name, type, ibnd, isMeasure: true, isConstraint: false);
                        break;
                    default:
                        Validation.Assert(vsn.SymKind == ModSymKind.Let);
                        info = new ModComputedVar(index, name, type, ibnd, isMeasure: false, isConstraint: false);
                        break;
                    }
                }
                else
                {
                    // This handles free variables.
                    Validation.Assert(sym is FreeVarDeclNode);
                    var fvn = (FreeVarDeclNode)sym;

                    // Process the various clauses by binding them and testing for validity.
                    // This code enforces:
                    // * Some clauses can't coexist with others.
                    // * There must be sufficient clauses to determine the variable type.
                    // * The type of this variable suggested by the various clauses must be consistent.

                    // The type of this variable.
                    DType typeVar = default;

                    // The formulas associated with the clauses.
                    BoundNode bndIn = null;
                    BoundNode bndFrom = null;
                    BoundNode bndTo = null;
                    BoundNode bndDef = null;

                    // Whether the variable is declared as "optional" or "required".
                    bool isOpt = fvn.TokOptReq?.Kind == TokKind.KtxOpt;
                    bool isReq = fvn.TokOptReq?.Kind == TokKind.KtxReq;

                    if (fvn.ValueIn is not null)
                    {
                        // Process the "in" clause. The result will be an "item" variable, where the value is an
                        // item in the domain sequence.
                        fvn.ValueIn.Accept(this);
                        var bnd = Pop();
                        var typeCur = bnd.Type;
                        if (typeCur.SeqCount == 0)
                        {
                            Error(fvn.ValueIn, ErrorStrings.ErrModuleFreeInMustBeSeq_Type, typeCur);
                            CheckGeneralType(fvn.ValueIn, ref bnd, typeCur.ToSequence());
                        }
                        else
                            typeCur = typeCur.ItemTypeOrThis;

                        bndIn = bnd;
                        typeVar = typeCur;
                    }

                    var nodeFrom = fvn.ValueFrom;
                    if (nodeFrom is not null)
                    {
                        // Process the "from" clause. This can't coexist with "in". The parser/node ensures this.
                        Validation.Assert(fvn.ValueIn is null);

                        // If the type is a sequence, then the variable is a sub-sequence of that domain sequence.
                        nodeFrom.Accept(this);
                        bndFrom = Pop();
                        typeVar = bndFrom.Type;
                    }
                    if (fvn.ValueTo is not null)
                    {
                        // Process the "to" clause. This can't coexist with "in". The parser/node ensures this.
                        Validation.Assert(fvn.ValueIn is null);

                        fvn.ValueTo.Accept(this);
                        var bnd = Pop();
                        // "to" is illegal if "from" clause specifies a sequence.
                        if (typeVar.IsSequence || bnd.Type.IsSequence)
                        {
                            Error(fvn.TokTo, fvn.ValueTo, ErrorStrings.ErrModuleFreeIllegalTo);
                            if (bndFrom is null)
                            {
                                // Use "to" as the "from".
                                Validation.Assert(!typeVar.IsValid);
                                typeVar = bnd.Type;
                                nodeFrom = fvn.ValueTo;
                                bndFrom = bnd;
                            }
                        }
                        else
                        {
                            bndTo = bnd;
                            typeVar = GetDomainSuperType(this, fvn.ValueTo, typeVar, bndTo.Type);
                        }
                    }
                    if (fvn.ValueDef is not null)
                    {
                        // Process the "default" clause.
                        fvn.ValueDef.Accept(this);
                        bndDef = Pop();
                        typeVar = GetDomainSuperType(this, fvn.ValueDef, typeVar, bndDef.Type);
                    }

                    if (isOpt)
                        typeVar = typeVar.ToOpt();

                    // Convert formulas to the result type.
                    if (bndFrom is not null && bndFrom.Type != typeVar)
                        CheckGeneralType(nodeFrom, ref bndFrom, typeVar);
                    if (bndTo is not null && bndTo.Type != typeVar)
                        CheckGeneralType(fvn.ValueTo, ref bndTo, typeVar);
                    if (bndDef is not null && bndDef.Type != typeVar)
                        CheckGeneralType(fvn.ValueDef, ref bndDef, typeVar);

                    SetNodeTypeAndScope(sym, typeVar);

                    var name = fvn.Name.Name;
                    if (bldr.TryFindSym(name, out _))
                    {
                        Error(sym.Name.Token, sym, ErrorStrings.ErrModuleDuplicateSymbolName_Name, name);
                        continue;
                    }

                    if (bndIn is not null)
                    {
                        Validation.Assert(bndFrom is null);
                        Validation.Assert(bndTo is null);
                        Validation.Assert(typeVar.IsValid);
                        int ifmaIn = bldr.AddItem(bndIn);
                        if (bndDef is null)
                        {
                            ArgTuple args = ArgTuple.Create(bldr.GetItemRef(ifmaIn), BndDefaultNode.Create(typeVar));
                            bndDef = BndCallNode.Create(TakeOneFunc.Instance, typeVar, args,
                                DirTuple.Create(default, Directive.Else));
                        }
                        Validation.Assert(bndDef.Type == typeVar);
                        int ifmaVal = bldr.AddItem(bndDef);
                        info = ModItemVar.Create(index, name, typeVar, ifmaIn, ifmaVal, isOpt);
                    }
                    else
                    {
                        if (isReq && typeVar.IsOpt)
                            Warning(fvn.TokOptReq, fvn, ErrorStrings.WrnModuleBadReq);
                        int ifmaFrom = bldr.AddItem(bndFrom);
                        int ifmaTo = bldr.AddItem(bndTo);
                        if (bndDef is null)
                            bndDef = bldr.GetItemRef(ifmaFrom >= 0 ? ifmaFrom : ifmaTo);
                        int ifmaVal = bldr.AddItem(bndDef);
                        info = ModSimpleVar.Create(index, name, typeVar, ifmaFrom, ifmaTo, ifmaVal);
                    }
                }

                Validation.AssertValue(sym);
                bldr.AddSym(info);
            }
            bldr.SetCurrent(null);

            SetNodeScope(node.Definitions);

            Validation.Assert(_moduleCur == bldr);
            Validation.Assert(_scopeCur.Module == bldr);
            _scopeCur.Enabled = false;
            _scopeCur = _scopeCur.Outer;
            _moduleCur = bldr.Outer;

            bool invalid = false;
            for (int i = Util.Size(_diagnostics); --i >= diagCount;)
            {
                if (_diagnostics[i].IsError)
                {
                    invalid = true;
                    break;
                }
            }

            Push(node, bldr.Create(invalid));
            SetNodeInfo(node);
            return false;

            static DType GetDomainSuperType(Binder binder, RexlNode node, DType typeVar, DType typeCur)
            {
                Validation.Assert(typeCur.IsValid);

                if (!typeVar.IsValid)
                    return typeCur;

                bool toGen = false;
                var type = DType.GetSuperType(typeVar, typeCur, DType.UseUnionOper, ref toGen);
                if (toGen || type.IsSequence && !typeVar.IsSequence)
                {
                    binder.Error(node, ErrorStrings.ErrIncompatibleTypes_Type_Type, typeVar, typeCur);
                    return typeVar;
                }
                return type;
            }
        }

        protected override void PostVisitImpl(ModuleNode node)
        {
            // Shouldn't get here - PreVisit should always return false.
            Validation.Assert(false);
            throw new InvalidOperationException("Internal binding error");
        }

        protected override bool PreVisitImpl(ModuleProjectionNode node)
        {
            Validation.AssertValue(node);

            node.Source.Accept(this);
            LiftUnary(LiftKinds.SeqTenOpt, node, Pop(), BindModuleProjectionCore);
            SetNodeInfo(node);
            return false;
        }

        private void BindModuleProjectionCore(ModuleProjectionNode node, BoundNode mod)
        {
            if ((_options & BindOptions.ProhibitModule) != 0)
                Error(node, ErrorStrings.ErrModuleNotSupported);

            var typeSrc = mod.Type;
            Validation.Assert(typeSrc.SeqCount == 0);
            Validation.Assert(!typeSrc.HasReq);

            ScopeWrapper sw = PushScope(ScopeKind.With, mod.Type);
            node.Record.Accept(this);
            Validation.Assert(_scopeCur == sw);
            PopScope();

            BoundNode rec = Pop();

            BoundNode res;
            if (typeSrc.RootKind != DKind.Module)
            {
                // The src isn't a module, so it's an error.
                Error(node.Source, ErrorStrings.ErrNotModule);
                res = BndCallNode.Create(WithFunc.With, rec.Type,
                    ArgTuple.Create(mod, rec), ScopeTuple.Create(sw.Scope));
            }
            else if (rec.Type.RootKind != DKind.Record)
            {
                Error(node.Record, ErrorStrings.ErrNotRecord);
                res = mod;
            }
            else
            {
                var typeMod = mod.Type;
                var typeRecOld = rec.Type.RootType.ToReq();
                var typeRecNew = typeRecOld;
                bool changed = false;
                foreach (var tn in rec.Type.GetNames())
                {
                    if (!typeMod.TryGetSymbolNameType(tn.Name, out var typeSym, out var symKind))
                    {
                        Error(node.Record, ErrorStrings.ErrModuleUnknownSymbol_Name, tn.Name);
                        typeRecNew = typeRecNew.DropName(tn.Name);
                        changed = true;
                    }
                    else if (!symKind.IsSettable())
                    {
                        Error(node.Record, ErrorStrings.ErrModuleSymbolNotSettable_Name, tn.Name);
                        typeRecNew = typeRecNew.DropName(tn.Name);
                        changed = true;
                    }
                    else if (tn.Type != typeSym)
                    {
                        typeRecNew = typeRecNew.SetNameType(tn.Name, typeSym);
                        changed = true;
                    }
                }

                if (changed)
                    CheckGeneralType(node.Record, ref rec, typeRecNew, union: false);

                if (typeRecNew.FieldCount > 0)
                    res = BndModuleProjectionNode.Create(mod, sw.Scope, rec);
                else
                    res = mod;
            }

            Push(node, res);
        }

        protected override void PostVisitImpl(ModuleProjectionNode node)
        {
            // Shouldn't get here - PreVisit should always return false.
            Validation.Assert(false);
            throw new InvalidOperationException("Internal binding error");
        }

        private sealed class ModuleBuilder
        {
            public readonly ModuleBuilder Outer;

            /// <summary>
            /// Cache mapping from a module symbol (possibly owned by an outer) to the
            /// legal reference for it.
            /// </summary>
            private Dictionary<ModSym, BoundNode> _symToRef;

            private ArgTuple.Builder _exts;
            private List<DType> _typesExt;
            private Dictionary<NPath, BoundNode> _globToExtSlotRef;
            private Dictionary<ArgScope, BoundNode> _scopeToExtSlotRef;

            private readonly Dictionary<DName, int> _nameToIndex;
            private readonly BndSymTuple.Builder _symbols;
            private readonly ArgTuple.Builder _items;
            private readonly List<DType> _typesItems;

            private SymbolDeclNode _symCur;
            private DType _typeCur;

            /// <summary>
            /// The <see cref="ArgScope"/> for the module construction external tuple. This
            /// is so <see cref="BndGetSlotNode"/> instances can be constructed to reference
            /// external values (coming from outside the module). These values are computed
            /// when the module is first created, but never "updated". Note that the type of
            /// the <see cref="ArgScope"/> is updated as binding progresses.
            /// </summary>
            private ArgScope _scopeExt;

            /// <summary>
            /// The <see cref="ArgScope"/> for the module construction items tuple. This is so
            /// <see cref="BndGetSlotNode"/> instances can be constructed to reference
            /// other slots. Note that the type of the <see cref="ArgScope"/> is updated as
            /// binding progresses.
            /// </summary>
            private readonly ArgScope _scopeItems;

            /// <summary>
            /// The type updater for the externals tuple scope.
            /// </summary>
            private Action<DType> _updateExt;

            /// <summary>
            /// The type updater for the items tuple scope.
            /// </summary>
            private readonly Action<DType> _updateItems;

            /// <summary>
            /// The <see cref="BndScopeRefNode"/> for <see cref="_scopeExt"/>.
            /// </summary>
            private BndScopeRefNode _scrExt;

            /// <summary>
            /// The <see cref="BndScopeRefNode"/> for <see cref="_scopeItems"/>.
            /// </summary>
            private readonly BndScopeRefNode _scrItems;

            public int Count
            {
                get
                {
                    int count = _typeCur.SymbolCount;
                    Validation.Assert(count == _nameToIndex.Count);
                    Validation.Assert(count == _symbols.Count);
                    Validation.Assert(count >= _scopeItems.Type.FieldCount);
                    return count;
                }
            }

            /// <summary>
            /// The current symbol being bound.
            /// </summary>
            public SymbolDeclNode SymCur => _symCur;

            /// <summary>
            /// The current type, reflecting all the symbols so far.
            /// </summary>
            public DType TypeCur => _typeCur;

            public ModuleBuilder(ModuleBuilder outer)
            {
                Validation.AssertValueOrNull(outer);

                Outer = outer;

                _nameToIndex = new Dictionary<DName, int>();
                _symbols = Immutable.Array.CreateBuilder<ModSym>();
                _items = ArgTuple.CreateBuilder();
                _typesItems = new();
                _typeCur = DType.EmptyModuleReq;

                _scopeItems = ArgScope.CreateModuleTupleScope(out _updateItems);
                _scrItems = _scopeItems.GetReference();
            }

            internal void SetCurrent(SymbolDeclNode symCur)
            {
                Validation.AssertValueOrNull(symCur);
                _symCur = symCur;
            }

            public bool TryFindSym(DName name, out ModSym sym)
            {
                if (_nameToIndex.TryGetValue(name, out int index))
                {
                    sym = _symbols[index];
                    return true;
                }

                sym = null;
                return false;
            }

            private void EnsureItemsType()
            {
                if (_scopeItems.Type.TupleArity < _items.Count)
                {
                    DType type = DType.CreateTuple(opt: false, _typesItems);
                    _updateItems(type);
                    Validation.Assert(_scopeItems.Type == type);
                    Validation.Assert(_scrItems.Type == type);
                }
                Validation.Assert(_scopeItems.Type.TupleArity == _items.Count);
            }

            public BndModuleNode Create(bool invalid)
            {
                Validation.Assert(_exts is null || _scopeExt is not null && _scopeExt.Type.TupleArity == _exts.Count);

                EnsureItemsType();

                return BndModuleNode.Create(
                    _scopeExt, _scopeItems, TypeCur,
                    _nameToIndex, _symbols.ToImmutable(),
                    _exts is null ? default : _exts.ToImmutable(),
                    _items.ToImmutable(), invalid);
            }

            public int AddItem(BoundNode item)
            {
                Validation.Assert(_items.Count == _typesItems.Count);

                if (item is null)
                    return -1;
                _items.Add(item);
                _typesItems.Add(item.Type);

                Validation.Assert(_items.Count == _typesItems.Count);
                return _items.Count - 1;
            }

            public void AddSym(ModSym sym)
            {
                Validation.AssertValue(sym);
                int count = Count;
                Validation.Assert(sym.Index == Count);

                _nameToIndex.Add(sym.Name, count);
                _symbols.Add(sym);
                _typeCur = _typeCur.AddNameType(sym.Name, sym.Type, sym.SymKind);
                Validation.Assert(Count == count + 1);
            }

            private void EnsureExts()
            {
                if (_scopeExt is null)
                {
                    Validation.Assert(_exts is null);
                    Validation.Assert(_typesExt is null);
                    Validation.Assert(_scrExt is null);
                    Validation.Assert(_globToExtSlotRef is null);
                    Validation.Assert(_scopeToExtSlotRef is null);

                    _scopeExt = ArgScope.CreateModuleTupleScope(out _updateExt);
                    _exts = ArgTuple.CreateBuilder();
                    _typesExt = new();
                    _scrExt = _scopeExt.GetReference();
                }

                Validation.Assert(_scopeExt is not null);
                Validation.Assert(_exts is not null);
                Validation.Assert(_typesExt is not null);
                Validation.Assert(_scrExt is not null);
            }

            private BoundNode AddExt(BoundNode ext)
            {
                Validation.AssertValue(ext);
                Validation.Assert(_exts.Count == Util.Size(_typesExt));

                int slot = _exts.Count;
                _exts.Add(ext);
                _typesExt.Add(ext.Type);

                var type = DType.CreateTuple(opt: false, _typesExt);
                _updateExt(type);
                Validation.Assert(_scopeExt.Type == type);
                Validation.Assert(_scrExt.Type == type);
                Validation.Assert(_scopeExt.Type.TupleArity == _exts.Count);

                return BndGetSlotNode.Create(slot, _scrExt);
            }

            public BoundNode GetItemRef(int ifma)
            {
                Validation.AssertIndex(ifma, _items.Count);
                Validation.Assert(_items.Count == _typesItems.Count);

                EnsureItemsType();
                var res = BndGetSlotNode.Create(ifma, _scrItems);
                Validation.Assert(res.Type == _items[ifma].Type);
                return res;
            }

            public BoundNode GetSymbolRef(ModuleBuilder parent, ModSym sym)
            {
                Validation.AssertValue(parent);
                Validation.AssertValue(sym);
                Validation.AssertIndex(sym.Index, parent._symbols.Count);
                Validation.Assert(sym == parent._symbols[sym.Index]);

                if (Util.TryGetValue(_symToRef, sym, out var res))
                {
                    Validation.Assert(res.Type == sym.Type);
                    return res;
                }

                if (parent == this)
                {
                    res = GetItemRef(sym.IfmaValue);
                    Validation.Assert(res.Type == sym.Type);
                }
                else
                {
                    Validation.Assert(Outer is not null);

                    var ext = Outer.GetSymbolRef(parent, sym);
                    EnsureExts();
                    res = AddExt(ext);
                }
                Validation.Assert(res.Type == sym.Type);

                Util.Add(ref _symToRef, sym, res);
                return res;
            }

            public BoundNode GetScopeRefForExt(ArgScope scope, ModuleBuilder outer)
            {
                Validation.AssertValue(scope);
                Validation.AssertValueOrNull(outer);
                Validation.Assert(outer != this);

                if (Util.TryGetValue(_scopeToExtSlotRef, scope, out var res))
                {
                    Validation.Assert(res.Type == scope.Type);
                    return res;
                }

                EnsureExts();

                BoundNode ext;
                if (outer != Outer && Outer is not null)
                    ext = Outer.GetScopeRefForExt(scope, outer);
                else
                {
                    // If Outer is null, but outer is non-null, this assert should go off.
                    // That indicates that the caller has a bug.
                    Validation.Assert(outer == Outer);
                    ext = BndScopeRefNode.Create(scope);
                }

                res = AddExt(ext);
                Validation.Assert(res.Type == scope.Type);

                Util.Add(ref _scopeToExtSlotRef, scope, res);
                return res;
            }

            public BoundNode GetGlobalRef(NPath name, DType type)
            {
                Validation.Assert(type.IsValid);

                if (Util.TryGetValue(_globToExtSlotRef, name, out var res))
                {
                    Validation.Assert(res.Type == type);
                    return res;
                }

                EnsureExts();

                BoundNode ext = Outer is not null ?
                    Outer.GetGlobalRef(name, type) :
                    BndGlobalNode.Create(name, type);

                res = AddExt(ext);
                Validation.Assert(res.Type == type);

                Util.Add(ref _globToExtSlotRef, name, res);
                return res;
            }
        }
    }
}
