// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Symbolic;

using ArgTuple = Immutable.Array<BoundNode>;
using IE = IEnumerable<object>;
using ScopeTuple = Immutable.Array<ArgScope>;
using VarTuple = Immutable.Array<BndFreeVarNode>;

/// <summary>
/// This reduces a <see cref="BoundNode"/> according to information in a <see cref="SymbolMap"/>.
/// Global references in the node are assumed to reference symbols in the symbol map. The way a
/// symbol reduces depends on the kind of symbol. For example, a constant symbol reference is
/// replaced by the actual value of the symbol (as a <see cref="BoundNode"/>) while a free variable
/// reference is replaced with a <see cref="BoundNode"/> containing the references to the
/// <see cref="BndFreeVarNode"/> instance(s) corresponding to that variable.
/// </summary>
public static class SymbolReducer
{
    /// <summary>
    /// Simplify the given <paramref name="src"/> node according to the given <paramref name="symMap"/>.
    /// The <paramref name="expandSelect"/> parameter indicates whether invocations of the various
    /// <see cref="SelectFunc"/> instances should be expanded/unrolled.
    /// </summary>
    public static BoundNode Simplify(SymbolMap symMap, BoundNode src, bool expandSelect)
    {
        Validation.BugCheckValue(symMap, nameof(symMap));
        Validation.BugCheckValue(src, nameof(src));
        return Impl.Run(symMap, src, expandSelect);
    }

    private sealed class Impl : ReducerVisitor
    {
        private readonly RuntimeModule _module;
        private readonly ArgScope _scopeItems;
        private readonly ArgScope _scopeExt;

        private readonly SymbolMap _symMap;
        private readonly CodeGeneratorBase _codeGen;
        private readonly TypeManager _typeManager;
        private readonly bool _expandSelect;

        // REVIEW: Should we use a reducer host?
        // REVIEW: Can't memoize when expandSelect is true since it re-reduces the same bound
        // nodes in different contexts.
        private Impl(SymbolMap symMap, bool expandSelect)
            : base(symMap.VerifyValue().Host, memoize: !expandSelect)
        {
            Validation.AssertValue(symMap);

            _module = symMap.Module;
            _scopeItems = _module.Bnd.ScopeItems;
            _scopeExt = _module.Bnd.ScopeExt;

            _symMap = symMap;
            _codeGen = _symMap.CodeGen;
            _typeManager = _codeGen.TypeManager;
            _expandSelect = expandSelect;
        }

        public static BoundNode Run(SymbolMap symMap, BoundNode src, bool expandSelect)
        {
            Validation.AssertValue(src);
            Validation.AssertValue(symMap);
            var impl = new Impl(symMap, expandSelect);
            var num = src.Accept(impl, 0);
            Validation.Assert(num == src.NodeCount);
            Validation.Assert(impl.StackDepth == 1);
            return impl.Pop();
        }

        protected override void VisitImpl(BndGlobalNode bnd, int idx)
        {
            // There shouldn't be any global references.
            Validation.Assert(false);
        }

        private bool TryEnsureBndFromItem(int slot, DType type, out BoundNode res)
        {
            var bmod = _module.Bnd;
            Validation.AssertIndex(slot, bmod.Items.Length);
            Validation.Assert(type == bmod.Items[slot].Type);

            int jsym = bmod.IfmaToJsym[slot];
            if (jsym <= 0)
                throw Validation.BugExcept("Unexpected item reference");

            int isym = jsym - 1;
            Validation.AssertIndex(isym, bmod.Symbols.Length);
            var sym = bmod.Symbols[isym];
            if (sym.IsConstantSym)
            {
                var bcn = _symMap.GetItemBcn(slot);
                Validation.AssertValue(bcn);
                Validation.Assert(bcn.Type == type | bcn.Type == type.ToReq());
                if (bcn.Type == type)
                    res = bcn;
                else
                    res = BndCastOptNode.Create(bcn);
                return true;
            }

            if (!_symMap.TryGetSymbol(sym.Name, out var entry))
            {
                res = null;
                return false;
            }

            if (entry is SymbolMap.ComputedVarEntry cve)
            {
                var svar = cve.SvarOpt;
                if (svar != null || cve.Type.IsPrimitiveXxx && (svar = cve.EnsureVariable(_symMap)) != null)
                    res = svar;
                else
                    res = cve.Node;
                return true;
            }

            if (entry is SymbolMap.BasicVarEntry bve)
            {
                res = bve.Svar;
                return true;
            }

            if (entry is SymbolMap.TensorVarEntry tve)
            {
                Validation.Assert(tve.Type == type);
                // Need to "cast" from VarTuple to ArgTuple.
                var args = ArgTuple.Cast(tve.Svars);
                res = BndTensorNode.Create(tve.Type.ToReq(), args, tve.Shape);
                Validation.Assert(res.Type == type | res.Type == type.ToReq());
                if (res.Type != type)
                    res = BndCastOptNode.Create(res);
                return true;
            }

            if (entry is SymbolMap.ItemVarEntry ive)
            {
                Validation.Assert(ive.TypeVar == DType.BitReq);
                Validation.Assert(ive.Type == ive.TypeKey || ive.Type == ive.TypeKey.ToOpt() && ive.Symbol.IsOpt);
                Validation.Assert(ive.Keys.Length == ive.Rng.Count);
                var vars = GetVars(ive.TypeVar, ive.Svars);
                res = BndCallNode.Create(ive.Symbol.IsOpt ? SelectFunc.Opt : SelectFunc.Req,
                    ive.Type, ArgTuple.Create(vars, ive.KeysConst));
                return true;
            }

            if (entry is SymbolMap.SubsetVarEntry sve)
            {
                Validation.Assert(sve.TypeVar == DType.BitReq);
                Validation.Assert(sve.Type == sve.KeysConst.Type);
                Validation.Assert(sve.Keys.Length == sve.Rng.Count);
                var vars = GetVars(sve.TypeVar, sve.Svars);
                res = BndCallNode.Create(SelectFunc.Seq, sve.Type, ArgTuple.Create(vars, sve.KeysConst));
                return true;
            }

            // The above cases should be exhaustive.
            Validation.Assert(false);
            res = null;
            return false;
        }

        private bool TryEnsureBndFromExt(int slot, DType type, out BoundNode res)
        {
            var bmod = _module.Bnd;
            Validation.AssertIndex(slot, bmod.Externals.Length);
            Validation.Assert(type == bmod.Externals[slot].Type);

            var bcn = _symMap.GetExtBcn(slot);
            Validation.AssertValue(bcn);
            Validation.Assert(bcn.Type == type | bcn.Type == type.ToReq());
            if (bcn.Type == type)
                res = bcn;
            else
                res = BndCastOptNode.Create(bcn);
            return true;
        }

        protected override bool PreVisitImpl(BndGetSlotNode bnd, int idx)
        {
            Validation.AssertValue(bnd);
            BoundNode result = bnd;

            int cur = idx + 1;
            var with = new WithInfo(this);
            var tup = with.Process(bnd.Tuple, ref cur);
            if (tup != bnd.Tuple)
                result = BndGetSlotNode.Create(bnd.Slot, tup);
            Validation.Assert(cur == idx + bnd.NodeCount);
            Validation.Coverage(with.HasAny ? 0 : 1);

            if (tup is BndScopeRefNode bsr)
            {
                if (bsr.Scope == _scopeItems)
                {
                    if (TryEnsureBndFromItem(bnd.Slot, bnd.Type, out var res))
                        result = res;
                }
                else if (bsr.Scope == _scopeExt)
                {
                    if (TryEnsureBndFromExt(bnd.Slot, bnd.Type, out var res))
                        result = res;
                }
            }

            Push(bnd, with.Apply(result));
            return false;
        }

        /// <summary>
        /// Wrap the given variables as a <see cref="BndSequenceNode"/>.
        /// </summary>
        private BoundNode GetVars(DType typeVar, VarTuple vars)
        {
#if DEBUG
            for (int i = 0; i < vars.Length; i++)
                Validation.Assert(typeVar == vars[i].Type);
#endif
            // Cast from VarTuple to ArgTuple.
            return BndSequenceNode.Create(typeVar.ToSequence(), ArgTuple.Cast(vars));
        }

        protected override bool PreVisitImpl(BndCallNode bnd, int idx)
        {
            bool ret = base.PreVisitImpl(bnd, idx);
            Validation.Assert(!ret);

            if (!(Peek() is BndCallNode call))
                return false;

            BoundNode dst = call;
            if (_expandSelect && call.Oper is SelectFunc fnSel)
                dst = ReduceSelect(call, fnSel);
            else if (call.Oper == ForEachFunc.ForEach)
                dst = ReduceForEach(call);
            else if (call.Oper == SumFunc.Sum)
                dst = ReduceSum(call);
            else if (call.Oper == CountFunc.Instance)
                dst = ReduceCount(call);
            else if (call.Oper == AnyAllFunc.All)
                dst = ReduceAll(call);
            else if (call.Oper == KeyJoinFunc.Instance)
                dst = ReduceKeyJoin(call);
            else if (call.Oper == TensorValuesFunc.Instance)
                dst = ReduceTensorValues(call);

            Validation.Assert(dst != null);
            Validation.Assert(dst.Type == call.Type);
            if (dst != call)
            {
                Pop();
                int num = dst.Accept(this, 0);
                Validation.Assert(num == dst.NodeCount);
            }
            return false;
        }

        // REVIEW: Need to reduce these for Zip to be more functional. See the test
        // file LinearZip.txt.
        // REVIEW: Should we even support the Zip form? It seems to be much more
        // complex than using tensors with indexing.

        //  ForEach(
        //      #1: Compound<{Have:i8, Per_P:{Need:r8, PID:s}*, RID:s}*>,
        //      With(
        //          !2: SetFields(
        //              !2: #1,
        //              Used : Sum(
        //                  KeyJoin(
        //                      #3: !2.Per_P,
        //                      #4: ForEach(
        //                          #3: [$0, $1],
        //                          #4: Compound<{PID:s, Profit:r8, Want:r8}*>,
        //                          {K:#4, V:#3}
        //                      ),
        //                      #3.PID,
        //                      #4.K.PID,
        //                      Mul(#3.Need, #4.V)
        //                  )
        //              )
        //          ),
        //          Num<r8>(!2.Have) >= !2.Used
        //      )
        //  )

        //  ForEach(
        //      #1: Compound<{Have:i8, Per_P:{Need:r8, PID:s}*, RID:s}*>,
        //      Num<r8>(#1.Have) >= Sum(
        //          KeyJoin(
        //              #2: #1.Per_P,
        //              #3: ForEach(
        //                  #2: [$0, $1],
        //                  #3: Compound<{PID:s, Profit:r8, Want:r8}*>,
        //                  {K:#3, V:#2}
        //              ),
        //              #2.PID,
        //              #3.K.PID,
        //              Mul(#2.Need, #3.V)
        //          )
        //      )
        //  )

        //  Sum(
        //      KeyJoin(
        //          #2: #1.Per_P,
        //          #3: ForEach(
        //              #2: [$0, $1],
        //              #3: Compound<{PID:s, Profit:r8, Want:r8}*>,
        //              {K:#3, V:#2}
        //          ),
        //          #2.PID,
        //          #3.K.PID,
        //          Mul(#2.Need, #3.V)
        //      )
        //  )

        //  ForEach(
        //      #1: Compound<i8*>,
        //      #2: Compound<{Need:r8, PID:s}**>,
        //      Num<r8>(#1) >= Sum(
        //          KeyJoin(
        //              #3: #2,
        //              #4: ForEach(
        //                  #3: [$0, $1],
        //                  #4: Compound<{PID:s, Profit:r8, Want:r8}*>,
        //                  {K:#4, V:#3}
        //              ),
        //              #3.PID,
        //              #4.K.PID,
        //              Mul(#3.Need, #4.V)
        //          )
        //      )
        //  )

        //  [
        //      20 >= Sum(
        //          KeyJoin(
        //              #1: Compound<{Need:r8, PID:s}*>,
        //              #2: [
        //                  {K:Compound<{PID:s, Profit:r8, Want:r8}>, V:$0},
        //                  {K:Compound<{PID:s, Profit:r8, Want:r8}>, V:$1}
        //              ],
        //              #1.PID,
        //              #2.K.PID,
        //              Mul(#1.Need, #2.V)
        //          )
        //      ),
        //
        //      30 >= Sum(
        //          KeyJoin(
        //              #1/1: Compound<{Need:r8, PID:s}*>,
        //              #2/2: [
        //                  {K:Compound<{PID:s, Profit:r8, Want:r8}>, V:$0},
        //                  {K:Compound<{PID:s, Profit:r8, Want:r8}>, V:$1}
        //              ],
        //              #1.PID,
        //              #2.K.PID,
        //              Mul(#1.Need, #2.V)
        //          )
        //      )
        //  ]

        // R_Use3:
        //  KeyJoin(
        //      #1: Compound<{Need:r8, PID:s, RID:s}*>,
        //      #2: ForEach(
        //          #1: [$0, $1],
        //          #2: Compound<{PID:s, Profit:r8, Want:r8}*>,
        //          {K:#2, V:#1}
        //      ),
        //      #1.PID,
        //      #2.K.PID,
        //      {Need:Mul(#1.Need, #2.V), RID:#1.RID}
        //  )

        //  GroupBy(
        //      #1: ForEach(
        //          #1: [$0, $0, $1, $1],
        //          #2: Compound<r8*>,
        //          #3: Compound<s*>,
        //          {Need:Mul(#2, #1), RID:#3}
        //      ),
        //      [key] #1.RID
        //  )

        private BoundNode ReduceSelect(BndCallNode call, SelectFunc fnSel)
        {
            Validation.AssertValue(fnSel);
            Validation.Assert(fnSel.IsValidCall(call));

            // REVIEW: Also handle SelKind.Opt.
            if (fnSel.Kind != SelectFunc.SelKind.One)
                return call;

            // Reduce to a sum of values times the indicators. This only works for numeric.
            var typeVals = call.Args[1].Type;
            Validation.Assert(typeVals.IsSequence);
            var typeItem = typeVals.ItemTypeOrThis;
            if (!typeItem.IsNumericReq)
                return call;

            if (!(call.Args[0] is BndSequenceNode bsn) || !(call.Args[1] is BndArrConstNode vals))
                return call;
            if (!TryComposeSum(bsn, typeItem, vals.Items, out var sum))
                return call;
            return sum;
        }

        private BoundNode ReduceTensorValues(BndCallNode call)
        {
            Validation.Assert(TensorValuesFunc.Instance.IsValidCall(call));

            var arg = call.Args[0];

            // REVIEW: Also handle BndTenConstNode when needed.
            if (arg is BndTensorNode btn)
                return BndSequenceNode.Create(btn.Type.GetTensorItemType().ToSequence(), btn.Items);

            return call;
        }

        private BoundNode ReduceForEach(BndCallNode call)
        {
            Validation.Assert(ForEachFunc.ForEach.IsValidCall(call));

            if (call.Indices[0] != null)
            {
                // REVIEW: Support indexing?
                return call;
            }

            if (ForEachFunc.HasPredicate(call))
            {
                // REVIEW: Should this support predicates?
                return call;
            }

            int cseq = call.Scopes.Length;
            var sel = call.Args[cseq];

            BitSet isArr = default;
            BitSet isBsn = default;
            int lenMin = int.MaxValue;
            bool allFixed = true;
            for (int i = 0; i < cseq; i++)
            {
                if (call.Args[i] is BndArrConstNode arr)
                {
                    isArr = isArr.SetBit(i);
                    if (lenMin > arr.Length)
                        lenMin = arr.Length;
                }
                else if (call.Args[i] is BndSequenceNode bsn)
                {
                    isBsn = isBsn.SetBit(i);
                    if (lenMin > bsn.Items.Length)
                        lenMin = bsn.Items.Length;
                }
                else
                    allFixed = false;
            }

            if (allFixed && lenMin == 0)
                return BndSequenceNode.Create(call.Type, ArgTuple.Empty);

            if (!isArr.IsEmpty)
            {
                if (isArr == BitSet.GetMask(cseq))
                {
                    if (References.OnlyScopes(call.Args[cseq], call.Scopes))
                    {
                        // All are constant arrays and the selector references nothing external, so evaluate to a "constant".
                        if (!_typeManager.TryEnsureSysType(sel.Type, out Type stItem))
                            return call;

                        if (!TryDoMap(call, stItem, out var arr))
                            return call;

                        return arr;
                    }
                }

                for (int i = cseq; --i >= 0;)
                {
                    // This loop can modify call. Ensure that everything was updated as expected.
                    Validation.Assert(ForEachFunc.ForEach.IsValidCall(call));
                    Validation.Assert(!ForEachFunc.HasPredicate(call));
                    Validation.Assert(call.Scopes.Length == cseq);
                    Validation.Assert(call.Args[cseq] == sel);

                    var seq = call.Args[i];
                    Validation.Assert(seq.Type.IsSequence);
                    var typeItem = seq.Type.ItemTypeOrThis;
                    if (!(seq is BndArrConstNode arr))
                        continue;
                    if (!typeItem.IsAggReq)
                        continue;

                    // See if the selector uses only fields/slots of the seq items.
                    // Count the number of scope references and the number of such references that
                    // are NOT field/slot retrievals. Also records the field/slot usage.
                    var scope = call.Scopes[i];
                    Dictionary<DName, BndGetFieldNode> fields = null;
                    Dictionary<int, BndGetSlotNode> slots = null;
                    int all = 0;
                    int non = 0;
                    void HandleRef(ArgScope s, BndScopeRefNode b, ScopeFinder.IContext ctx)
                    {
                        if (s != scope)
                            return;
                        all++;
                        var parent = ctx[0];
                        if (parent is BndGetFieldNode gf)
                            Util.Set(ref fields, gf.Name, gf);
                        else if (parent is BndGetSlotNode gs)
                            Util.Set(ref slots, gs.Slot, gs);
                        else
                            non++;
                    }
                    ScopeFinder.Run(sel, HandleRef);

                    // REVIEW: Handle no references.
                    // REVIEW: Can all ever be zero here? Under normal circumstances, ForEach would have eliminated
                    // one that isn't used, unless its size was smaller than all others.
                    if (non > 0 || all == 0)
                        continue;

                    Validation.Assert((fields == null) ^ (slots == null));
                    int countNew = fields != null ? fields.Count : slots.Count;
                    Validation.Assert(countNew > 0);

                    var bldr = ArgTuple.CreateBuilder(cseq + countNew, init: true);
                    var bldrScopes = ScopeTuple.CreateBuilder(cseq + countNew - 1, init: true);

                    Dictionary<DName, ArgScope> fieldMap = null;
                    Dictionary<int, ArgScope> slotMap = null;
                    int index = 0;
                    if (fields != null)
                    {
                        fieldMap = new Dictionary<DName, ArgScope>(fields.Count);
                        foreach (var kvp in fields.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
                        {
                            arr.Type.TryGetNameType(kvp.Key, out var type).Verify();
                            fieldMap.Add(kvp.Key, bldrScopes[i + index] = ArgScope.Create(ScopeKind.SeqItem, type));
                            var valsNew = arr;
                            if (!TryMapSeq(ref valsNew, kvp.Value, scope))
                                goto LNext;
                            bldr[i + index] = valsNew;
                            index++;
                        }
                    }
                    else
                    {
                        Validation.Assert(slots != null);
                        slotMap = new Dictionary<int, ArgScope>(slots.Count);
                        var types = arr.Type.ItemTypeOrThis.GetTupleSlotTypes();
                        foreach (var kvp in slots.OrderBy(pair => pair.Key))
                        {
                            slotMap.Add(kvp.Key, bldrScopes[i + index] = ArgScope.Create(ScopeKind.SeqItem, types[kvp.Key]));
                            var valsNew = arr;
                            if (!TryMapSeq(ref valsNew, kvp.Value, scope))
                                return call;
                            bldr[i + index] = valsNew;
                            index++;
                        }
                    }
                    Validation.Assert(index == countNew);

                    for (int j = 0; j < i; j++)
                    {
                        bldr[j] = call.Args[j];
                        bldrScopes[j] = call.Scopes[j];
                    }

                    for (int j = i + 1; j < call.Scopes.Length; j++)
                    {
                        bldr[j + countNew - 1] = call.Args[j];
                        bldrScopes[j + countNew - 1] = call.Scopes[j];
                    }

                    var selNew = MapScopeFields(sel, scope, fieldMap, slotMap);
                    bldr[cseq + countNew - 1] = selNew;
                    call = BndCallNode.Create(ForEachFunc.ForEach, call.Type, bldr.ToImmutable(), bldrScopes.ToImmutable());
                    cseq = call.Scopes.Length;
                    sel = call.Args[cseq];
                    Validation.Assert(call.Args.Length == cseq + 1);
                    Validation.Assert(call.Type == sel.Type.ToSequence());

                LNext:
                    ;
                }
            }

            if (_expandSelect && allFixed)
            {
                // Substitute.
                Validation.Assert(lenMin > 0);
                cseq = call.Scopes.Length;
                Validation.Assert(call.Args.Length == cseq + 1);
                sel = call.Args[cseq];
                Validation.Assert(call.Type == sel.Type.ToSequence());

                var bldr = ArgTuple.CreateBuilder(lenMin, init: true);
                if (cseq == 1)
                {
                    // REVIEW: It would be nice to make this iterative replacement faster.
                    // The logical path is "decoded" repeatedly.
                    var seq = call.Args[0];
                    var scope = call.Scopes[0];
                    if (seq is BndArrConstNode arr)
                    {
                        Validation.Assert(arr.Items.Length >= lenMin);
                        int tmp = 0;
                        for (int i = 0; i < lenMin; i++)
                        {
                            var val = arr.Items.GetValue(i);
                            if (!_typeManager.TryWrapConst(scope.Type, val, out var bcn))
                                return call;
                            var (item, num) = ScopeReplacer.Run(_host, sel, scope, bcn);
                            item = Reduce(item, ref tmp);
                            bldr[i] = item;
                        }
                    }
                    else
                    {
                        Validation.Assert(seq is BndSequenceNode);
                        var bsn = (BndSequenceNode)seq;
                        Validation.Assert(bsn.Items.Length >= lenMin);
                        int tmp = 0;
                        for (int i = 0; i < lenMin; i++)
                        {
                            var (item, num) = ScopeReplacer.Run(_host, sel, scope, bsn.Items[i]);
                            item = Reduce(item, ref tmp);
                            bldr[i] = item;
                        }
                    }
                }
                else
                {
                    // REVIEW: It would be nice to make this iterative replacement faster.
                    // The logical path is "decoded" repeatedly.
                    var map = new Dictionary<ArgScope, BoundNode>();
                    for (int i = 0; i < lenMin; i++)
                    {
                        for (int j = 0; j < cseq; j++)
                        {
                            var seq = call.Args[j];
                            var scope = call.Scopes[j];
                            if (seq is BndArrConstNode arr)
                            {
                                Validation.Assert(arr.Items.Length >= lenMin);
                                var val = arr.Items.GetValue(i);
                                if (!_typeManager.TryWrapConst(scope.Type, val, out var bcn))
                                    return call;
                                map[scope] = bcn;
                            }
                            else
                            {
                                Validation.Assert(seq is BndSequenceNode);
                                var bsn = (BndSequenceNode)seq;
                                Validation.Assert(bsn.Items.Length >= lenMin);
                                map[scope] = bsn.Items[i];
                            }
                        }

                        var (cur, num) = ScopeGenReplacer.Run(_host, sel, map);
                        int tmp = 0;
                        cur = Reduce(cur, ref tmp);
                        bldr[i] = cur;
                    }
                }

                return BndSequenceNode.Create(call.Type, bldr.ToImmutable());
            }

            if (call.Args.Length == 2)
            {
                var seq = call.Args[0];
                var scope = call.Scopes[0];

                // Reduce ForEach over a SelectSeq.
                if (seq is BndCallNode inner && inner.Oper == SelectFunc.Seq && inner.Args[1] is BndArrConstNode bacn)
                {
                    if (!References.OnlyScope(sel, call.Scopes[0]))
                        return call;
                    if (!TryMapSeq(ref bacn, sel, call.Scopes[0]))
                        return call;
                    return BndCallNode.Create(SelectFunc.Seq, call.Type, ArgTuple.Create(inner.Args[0], bacn));
                }

                return call;
            }

            // Reduce ForEach over two sequences, with the 2nd being a "constant", and with
            // a separable selector.
            if (call.Args.Length == 3)
            {
                if (!(call.Args[1] is BndArrConstNode vals))
                    return call;

                var scopes = call.Scopes;
                Validation.Assert(scopes.Length == 2);

                if (!References.OnlyScopes(sel, scopes[0], scopes[1]))
                    return call;

                // Try to split the selector.
                if (!TrySplit(sel, scopes[0], out var a, out var b, out var bop))
                    return call;
                Validation.Assert(References.OnlyScope(a, scopes[0]));
                Validation.Assert(References.OnlyScope(b, scopes[1]));
                if (b is BndScopeRefNode bsrn)
                {
                    Validation.Assert(bsrn.Scope == scopes[1]);
                    return call;
                }
                if (!TryMapSeq(ref vals, b, scopes[1]))
                    return call;

                // REVIEW: Improve this by substituting across the vars sequence, if it is of known length.
                var scopeVal = ArgScope.Create(ScopeKind.SeqItem, b.Type);
                var selNew = BndVariadicOpNode.Create(sel.Type, bop, ArgTuple.Create(a, BndScopeRefNode.Create(scopeVal)), default);
                return BndCallNode.Create(ForEachFunc.ForEach, call.Type, ArgTuple.Create(call.Args[0], vals, selNew), ScopeTuple.Create(scopes[0], scopeVal));
            }

            return call;
        }

        private BoundNode ReduceCount(BndCallNode call)
        {
            Validation.Assert(CountFunc.Instance.IsValidCall(call));

            // REVIEW: Implement the two-arg case.
            if (call.Args.Length != 1)
                return call;

            // REVIEW: Generalize this to invocations of ForEach, ForEachIf, etc.
            // Also to non-indicators.

            // When inds are indicators, reduce:
            // * Count(SelectSeq(inds, [vals]))
            var arg = call.Args[0];
            if (!TryGetIndsAndValues(arg, out var vars, out var vals))
                return call;

            var inds = vars.Items;
            int num = Math.Min(inds.Length, vals.Length);
            var bldr = ArgTuple.CreateBuilder(num, init: true);
            var type = DType.I8Req;
            for (int i = 0; i < num; i++)
                bldr[i] = BndCastNumNode.Create(inds[i], type);
            return BndVariadicOpNode.Create(type, BinaryOp.Add, bldr.ToImmutable(), default);
        }

        private BoundNode ReduceSum(BndCallNode call)
        {
            Validation.Assert(SumFunc.Sum.IsValidCall(call));

            if (call.Args.Length > 2)
                return call;

            // REVIEW: A known empty sequence should be handled by ordinary reduction, so technically
            // this shouldn't be needed.
            if (call.Args[0].IsNullValue)
                return BndDefaultNode.Create(call.Type);

            if (call.Indices.Length > 0 && call.Indices[0] != null)
            {
                // REVIEW: Support indexing?
                return call;
            }

            var typeSum = call.Type;
            Validation.Assert(typeSum.IsNumericReq);

            if (call.Args.Length == 1)
            {
                var typeItem = call.Args[0].Type.ItemTypeOrThis;
                if (!typeItem.IsNumericReq)
                    return call;

                DType typeNew;
                switch (typeItem.RootKind)
                {
                case DKind.R8:
                case DKind.IA:
                    typeNew = typeItem;
                    break;
                case DKind.R4:
                    typeNew = DType.R8Req;
                    break;
                case DKind.I8:
                case DKind.I4:
                case DKind.I2:
                case DKind.I1:
                case DKind.U4:
                case DKind.U2:
                case DKind.U1:
                case DKind.Bit:
                    typeNew = DType.I8Req;
                    break;
                case DKind.U8:
                    typeNew = DType.U8Req;
                    break;
                default:
                    return call;
                }

                // REVIEW: Can we assert that typeNew == typeSum?
                if (typeNew != typeSum)
                    return call;

                if (typeNew != typeItem)
                {
                    var scope = ArgScope.Create(ScopeKind.SeqItem, typeItem);
                    call = BndCallNode.Create(call.Oper, typeNew,
                        ArgTuple.Create(call.Args[0], BndCastNumNode.Create(BndScopeRefNode.Create(scope), typeNew)),
                        ScopeTuple.Create(scope));
                }
            }

            var seq = call.Args[0];
            Validation.Assert(seq.Type.IsSequence);

            // REVIEW: Handle constant selectors?
            BoundNode selSum;
            if (call.Args.Length < 2)
            {
                // REVIEW: Handle count variation?
                if (typeSum != seq.Type.ItemTypeOrThis)
                    return call;
                selSum = null;
            }
            else
            {
                selSum = call.Args[1];
                var fe = BndCallNode.Create(ForEachFunc.ForEach, selSum.Type.ToSequence(), call.Args, call.Scopes);
                var feNew = ReduceForEach(fe);
                if (feNew != fe)
                    return BndCallNode.Create(SumFunc.Sum, call.Type, ArgTuple.Create(feNew));
                if (!References.OnlyScope(selSum, call.Scopes[0]))
                    return call;

                // REVIEW: Handle count variation?
                if (typeSum != selSum.Type)
                    return call;
            }

            var bop = BinaryOp.Mul;
            BndArrConstNode vals;
            if (seq is BndSequenceNode bsn)
            {
                if (selSum is null)
                    return BndVariadicOpNode.Create(typeSum, BinaryOp.Add, bsn.Items, default);

                if (selSum is BndCastNumNode bcnn && bcnn.Type == typeSum &&
                    bcnn.Child is BndScopeRefNode bsrn && bsrn.Scope == call.Scopes[0])
                {
                    // Apply the cast to the values.
                    var bldr = bsn.Items.ToBuilder();
                    for (int i = 0; i < bldr.Count; i++)
                        bldr[i] = BndCastNumNode.Create(bldr[i], typeSum);
                    return BndVariadicOpNode.Create(typeSum, BinaryOp.Add, bldr.ToImmutable(), default);
                }

                return call;
            }

            if (TryGetIndsAndValues(seq, out var vars, out vals))
            {
                // Apply the sum selector to the values.
                if (selSum != null && !TryMapSeq(ref vals, selSum, call.Scopes[0]))
                    return call;
            }
            else if (TryGetVarsAndValues(seq, out vars, out vals, out var sel, out var scopes))
            {
                if (!References.OnlyScopes(sel, scopes[0], scopes[1]))
                    return call;

                // Apply the sum selector to the foreach selector.
                if (selSum != null)
                {
                    var (s, c) = ReplaceScope(selSum, call.Scopes[0], sel);
                    sel = s;
                }

                // Try to split the selector.
                if (!TrySplit(sel, scopes[0], out var a, out var b, out bop))
                    return call;
                Validation.Assert(References.OnlyScope(a, scopes[0]));
                Validation.Assert(References.OnlyScope(b, scopes[1]));
                if (!(b is BndScopeRefNode bsrn))
                    return call;
                Validation.Assert(bsrn.Scope == scopes[1]);

                if (a is BndCastNumNode cast)
                    a = cast.Child;
                if (a is BndScopeRefNode bsrn2)
                    Validation.Assert(bsrn2.Scope == scopes[0]);
                else
                {
                    // REVIEW: Do substitution?
                    return call;
                }
            }
            else
                return call;

            if (!TryComposeSum(vars, vals.Type.ItemTypeOrThis, vals.Items, out var sum, bop))
                return call;

            return sum;
        }

        private BoundNode ReduceAll(BndCallNode call)
        {
            Validation.Assert(call.Oper == AnyAllFunc.All);

            var arg = call.Args[0];
            if (!(arg is BndSequenceNode seq))
                return call;

            if (call.Args.Length == 1)
                return BndVariadicOpNode.Create(call.Type, BinaryOp.And, seq.Items, default);

            Validation.Assert(call.Args.Length == 2);
            var items = seq.Items;
            var bldr = ArgTuple.CreateBuilder(items.Length, init: true);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var (res, count) = ReplaceScope(call.Args[1], call.Scopes[0], item);
                // REVIEW: If the count is zero, then we can/should do better!
                Validation.Assert(count > 0);
                bldr[i] = res;
            }
            return BndVariadicOpNode.Create(call.Type, BinaryOp.And, bldr.ToImmutable(), default);
        }

        private BoundNode ReduceKeyJoin(BndCallNode call)
        {
            Validation.Assert(KeyJoinFunc.Instance.IsValidCall(call));

            // Before expansion:
            //  KeyJoin(
            //      #1: arrA, // BndArrConstNode
            //      #2: ForEach(
            //          #1: vars, // BndSequenceNode
            //          #2: arrB, // BndArrConstNode
            //          {K:#2, V:#1} // pairing.
            //      ),
            //      #1.PID,
            //      #2.K.PID,
            //      {Need:Mul(#1.Need, #2.V), RID:#1.RID}
            //  )
            //
            // => Find indices and expand the sequences above.
            //
            //  ForEach(
            //      #1: varsExp,
            //      #2: arrAExp,
            //      #3: arrBExp,
            //      {Need:Mul(#2.Need, #1), RID:#2.RID}
            //  )

            // After expansion: This one looks simpler but the variables and values are mixed together, so is much harder.
            //  KeyJoin(
            //      #1: Compound<{Need:r8, PID:s, RID:s}*>,
            //      #2: [{K:Compound<{PID:s, Profit:r8, Want:r8}>, V:$0}, {K:Compound<{PID:s, Profit:r8, Want:r8}>, V:$1}],
            //      #1.PID,
            //      #2.K.PID,
            //      {Need:Mul(#1.Need, #2.V), RID:#1.RID}
            //  )

            // REVIEW: Should we handle outer join at all?
            if (call.Args.Length != 5)
                return call;

            var src0 = call.Args[0];
            var src1 = call.Args[1];
            var key0 = call.Args[2];
            var key1 = call.Args[3];
            var sel = call.Args[4];
            var scope0 = call.Scopes[0];
            var scope1 = call.Scopes[1];

            if (!(src0 is BndArrConstNode arrA))
                return call;
            if (!TryGetVarsAndValues(src1, out var vars, out var arrB, out var pairing, out var scopesInner))
                return call;

            if (!References.OnlyScope(key0, scope0) ||
                !References.OnlyScope(key1, scope1) ||
                !References.OnlyScopes(sel, scope0, scope1))
            {
                return call;
            }

            // Validate the pairing (inner selector) and get the field names.
            if (!(pairing is BndRecordNode brn))
                return call;
            if (brn.Items.Count != 2)
                return call;
            DName fld0 = default;
            DName fld1 = default;
            foreach (var kvp in brn.Items)
            {
                if (!(kvp.Value is BndScopeRefNode bsrn))
                    return call;
                if (bsrn.Scope == scopesInner[0])
                    fld0 = kvp.Key;
                else if (bsrn.Scope == scopesInner[1])
                    fld1 = kvp.Key;
            }
            if (!fld0.IsValid || !fld1.IsValid)
                return call;

            // Need key1 to only depend on fld1 (value field).
            bool good = true;
            void HandleScope(ArgScope s, BndScopeRefNode r, ScopeFinder.IContext ctx)
            {
                Validation.Assert(ctx.Depth > 0);
                if (s != scope1)
                    good = false;
                else if (!(ctx[0] is BndGetFieldNode bgfn))
                    good = false;
                else if (bgfn.Name != fld1)
                    good = false;
            }
            ScopeFinder.Run(key1, HandleScope);
            if (!good)
                return call;

            // Build and evaluate:
            //  KeyJoin(
            //      i:ForEach(a:src0, (a, #a)),
            //      j:ForEach(a:vals, (a, #a)),
            //      key0(i!0),
            //      key1(j!0),
            //      (i!1, j!1))
            // REVIEW: Once KeyJoin supports indexing, this can be simpler.
            var srcNew0 = MakeIndexedPair(arrA);
            var srcNew1 = MakeIndexedPair(arrB);
            var scopeNew0 = ArgScope.Create(ScopeKind.SeqItem, srcNew0.Type.ItemTypeOrThis);
            var scopeNew1 = ArgScope.Create(ScopeKind.SeqItem, srcNew1.Type.ItemTypeOrThis);

            var (keyNew0, _) = ScopeReplacer.Run(_host, key0, scope0, BndGetSlotNode.Create(0, BndScopeRefNode.Create(scopeNew0)));
            var keyNew1 = ScopeItemReplacer.Run(_host, key1, scope1,
                new Dictionary<DName, BoundNode> { { fld1, BndGetSlotNode.Create(0, BndScopeRefNode.Create(scopeNew1)) } }, default);
            var pair = BndTupleNode.Create(ArgTuple.Create(
                BndGetSlotNode.Create(1, BndScopeRefNode.Create(scopeNew0)), BndGetSlotNode.Create(1, BndScopeRefNode.Create(scopeNew1))));
            var join = BndCallNode.Create(
                KeyJoinFunc.Instance, pair.Type.ToSequence(),
                ArgTuple.Create(srcNew0, srcNew1, keyNew0, keyNew1, pair),
                ScopeTuple.Create(scopeNew0, scopeNew1));

            var scopeJoin = ArgScope.Create(ScopeKind.With, join.Type);
            var scopePair0 = ArgScope.Create(ScopeKind.SeqItem, pair.Type);
            var inds0 = BndCallNode.Create(ForEachFunc.ForEach, DType.I8Req.ToSequence(),
                ArgTuple.Create(BndScopeRefNode.Create(scopeJoin), BndGetSlotNode.Create(0, BndScopeRefNode.Create(scopePair0))),
                ScopeTuple.Create(scopePair0));
            var scopePair1 = ArgScope.Create(ScopeKind.SeqItem, pair.Type);
            var inds1 = BndCallNode.Create(ForEachFunc.ForEach, DType.I8Req.ToSequence(),
                ArgTuple.Create(BndScopeRefNode.Create(scopeJoin), BndGetSlotNode.Create(1, BndScopeRefNode.Create(scopePair1))),
                ScopeTuple.Create(scopePair1));
            var indsPair = BndSequenceNode.Create(DType.I8Req.ToSequence(2), ArgTuple.Create(inds0, inds1));
            var indsAll = BndCallNode.Create(WithFunc.With, indsPair.Type, ArgTuple.Create(join, indsPair), ScopeTuple.Create(scopeJoin));

            var code = RunCodeGen(indsAll);
            if (code == null || code.Globals.Length != 1 || !code.Globals[0].IsCtx)
                return call;
            var args = new object[] { ExecCtx.CreateBare() };

            var inds = code.Func(args);
            Validation.Assert(inds is IEnumerable<IEnumerable<long>>);
            var indsArr = ToArray<IEnumerable<long>>((IEnumerable<IEnumerable<long>>)inds);
            Validation.Assert(indsArr.Length == 2);

            var indsA = ToArray<long>(indsArr[0]);
            var indsB = ToArray<long>(indsArr[1]);
            Validation.Assert(indsA.Length == indsB.Length);

            var arrAExp = SelectItems(arrA, indsA);
            var arrBExp = SelectItems(arrB, indsB);
            var varsExp = SelectItems(vars, indsB);

            //  ForEach(
            //      #1: varsExp,
            //      #2: arrAExp,
            //      #3: arrBExp,
            //      {Need:Mul(#2.Need, #1), RID:#2.RID}
            //  )

            var scopeV = ArgScope.Create(ScopeKind.SeqItem, varsExp.Type.ItemTypeOrThis);
            var scopeA = scope0;
            var scopeB = ArgScope.Create(ScopeKind.SeqItem, arrBExp.Type.ItemTypeOrThis);

            //    {Need:Mul(#1.Need, #2.V), RID:#1.RID}
            // to {Need:Mul(#2.Need, #1), RID:#2.RID}
            // No need to map scope0 to scopeA, since we're re-using the scope instance.
            var selExp = ScopeItemMapper.Run(_host, sel, scope1,
                new Dictionary<DName, ArgScope> { { fld0, scopeV }, { fld1, scopeB } }, default);
            var res = BndCallNode.Create(ForEachFunc.ForEach, call.Type,
                ArgTuple.Create(varsExp, arrAExp, arrBExp, selExp),
                ScopeTuple.Create(scopeV, scopeA, scopeB));

            return res;
        }

        /// <summary>
        /// Create a sequence from the given one and the indicated indices.
        /// </summary>
        private static BoundNode SelectItems(BndSequenceNode src, long[] inds)
        {
            Validation.AssertValue(src);
            Validation.AssertValue(inds);

            // REVIEW: Need to deal with the issue of "sharing".
            var items = src.Items;
            var bldr = ArgTuple.CreateBuilder(inds.Length, init: true);
            for (int i = 0; i < inds.Length; i++)
            {
                long ind = inds[i];
                Validation.Assert((ulong)ind < (ulong)items.Length);
                bldr[i] = items[(int)ind];
            }
            return BndSequenceNode.Create(src.Type, bldr.ToImmutable());
        }

        /// <summary>
        /// Create an array-based sequence from the given one and the indicated indices.
        /// </summary>
        private static BndArrConstNode SelectItems(BndArrConstNode src, long[] inds)
        {
            Validation.AssertValue(src);
            Validation.AssertValue(inds);

            var meth = new Func<object[], long[], object[]>(SelectItems).Method
                .GetGenericMethodDefinition().MakeGenericMethod(src.ItemSysType);
            var items = (Array)meth.Invoke(null, new object[] { src.Items, inds });
            return src.SetItems(items);
        }

        /// <summary>
        /// Create an array from the given one and the indicated indices.
        /// </summary>
        private static T[] SelectItems<T>(T[] src, long[] inds)
        {
            Validation.AssertValue(src);
            Validation.AssertValue(inds);

            var dst = new T[inds.Length];
            for (int i = 0; i < inds.Length; i++)
            {
                long ind = inds[i];
                Validation.Assert((ulong)ind < (ulong)src.Length);
                dst[i] = src[(int)ind];
            }
            return dst;
        }

        private BoundNode MakeIndexedPair(BndArrConstNode items)
        {
            var scope = ArgScope.Create(ScopeKind.SeqItem, items.Type.ItemTypeOrThis);
            var index = ArgScope.CreateIndex();
            DType typePair = DType.CreateTuple(false, scope.Type, index.Type);
            var sel = BndTupleNode.Create(ArgTuple.Create(BndScopeRefNode.Create(scope), BndScopeRefNode.Create(index)), typePair);
            return BndCallNode.Create(
                ForEachFunc.ForEach, typePair.ToSequence(),
                ArgTuple.Create(items, sel), ScopeTuple.Create(scope), ScopeTuple.Create(index));
        }

        protected override bool PreVisitImpl(BndGroupByNode bnd, int idx)
        {
            bool ret = base.PreVisitImpl(bnd, idx);
            Validation.Assert(!ret);

            var bndIn = Peek();
            if (bndIn is BndGroupByNode gbIn)
            {
                var res = ReduceGroupBy(gbIn);
                if (res != bndIn)
                {
                    Pop();
                    Push(bndIn, res);
                }
            }

            return false;
        }

        private BoundNode ReduceGroupBy(BndGroupByNode bnd)
        {
            // Morph pure group-by of a SelectSeq to a sequence of SelectSeq's.
            //
            //  GroupBy(
            //      SelectSeq([<inds>], ConstSeq<...*>),
            //      [key] ..., [key] ..., ...) // Any number of pure keys.

            var src = bnd.Source;
            if (bnd.Type == src.Type.ToSequence() &&
                bnd.KeepKeys.Count == 0 && bnd.AggItems.Count == 0 && bnd.MapItems.Count == 0 &&
                TryGetIndsAndValues(src, out var inds, out var vals))
            {
                // GroupBy on SelectSeq of indicators with items.
                Validation.Assert(vals.Type == src.Type);

                _typeManager.TryEnsureSysType(src.Type, out Type stSrc).Verify();
                Validation.Assert(stSrc.IsAssignableFrom(vals.Items.GetType()));
                _typeManager.TryEnsureSysType(src.Type.ItemTypeOrThis, out Type stItem).Verify();

                // REVIEW: Can we get the indices directly by playing the same trick as we do for KeyJoin?
                var gb = BndGroupByNode.Create(
                    bnd.Type, vals,
                    bnd.ScopeForKeys, bnd.IndexForKeys, bnd.PureKeys, bnd.KeepKeys, bnd.KeysCi,
                    bnd.ScopeForMaps, bnd.IndexForMaps, bnd.MapItems,
                    bnd.ScopeForAggs, bnd.AggItems);

                var code = RunCodeGen(gb);
                if (code == null || code.Globals.Length != 1 || !code.Globals[0].IsCtx)
                    return bnd;
                var args = new object[] { ExecCtx.CreateBare() };

                var grps = code.Func(args);
                Validation.Assert(grps != null);
                Validation.Assert(typeof(IEnumerable<>).MakeGenericType(stSrc).IsAssignableFrom(grps.GetType()));

                var meth = new Func<IE, IEnumerable<IE>, (List<int>[], Array[])>(GetPartitionIndices<object>)
                    .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                var (indexMap, arrs) = ((List<int>[], Array[]))meth.Invoke(this, new object[] { vals.Items, grps });
                Validation.Assert(indexMap.Length == arrs.Length);

                var typeInd = inds.Type.ItemTypeOrThis;
                var typeKey = vals.Type.ItemTypeOrThis;
                var bldr = ArgTuple.CreateBuilder(indexMap.Length, init: true);
                int i = 0;
                foreach (var grp in (IEnumerable)grps)
                {
                    Validation.Assert(i < indexMap.Length);
                    var indices = indexMap[i];
                    var keys = arrs[i];
                    Validation.Assert(indices.Count == keys.Length);
                    Validation.Assert(stSrc.IsAssignableFrom(keys.GetType()));

                    var bldrVars = ArgTuple.CreateBuilder(keys.Length, init: true);
                    for (int j = 0; j < keys.Length; j++)
                        bldrVars[j] = inds.Items[indices[j]];

                    bldr[i++] = BndCallNode.Create(
                        SelectFunc.Seq, src.Type,
                        ArgTuple.Create(
                            BndSequenceNode.Create(inds.Type, bldrVars.ToImmutable()),
                            BndArrConstNode.Create(_typeManager, vals.Type, keys)));
                }
                Validation.Assert(i == bldr.Count);

                var res = BndSequenceNode.Create(typeKey.ToSequence(2), bldr.ToImmutable());
                return res;
            }

            return bnd;
        }

        /// <summary>
        /// Try to split an expression into a left and right operand, where the left operand only references
        /// the given <paramref name="scope"/>, and is minimal.
        /// </summary>
        private bool TrySplit(BoundNode bnd, ArgScope scope, out BoundNode a, out BoundNode b, out BinaryOp bop)
        {
            // REVIEW: Generalize this!
            if (bnd is BndVariadicOpNode bvon)
            {
                if (bvon.Args.Length == 2 && References.OnlyScope(bvon.Args[0], scope) && !ScopeCounter.Any(bvon.Args[1], scope))
                {
                    a = bvon.Args[0];
                    if (bvon.Inverted.TestBit(0))
                        a = BndVariadicOpNode.Create(bvon.Type, bvon.Op, ArgTuple.Create(a), 0x1);
                    b = bvon.Args[1];
                    if (bvon.Inverted.TestBit(1))
                        b = BndVariadicOpNode.Create(bvon.Type, bvon.Op, ArgTuple.Create(b), 0x1);
                    bop = bvon.Op;
                    return true;
                }
            }

            a = null;
            b = null;
            bop = 0;
            return false;
        }

        private bool TryComposeSum(BndSequenceNode bsn, DType typeKey, Array values, out BoundNode sum, BinaryOp bop = BinaryOp.Mul)
        {
            Validation.AssertValue(bsn);
            Validation.Assert(typeKey.IsNumericReq);
            Validation.Assert(typeKey.Accepts(bsn.Type.ItemTypeOrThis, DType.UseUnionDefault));
            Validation.AssertValue(values);

            var items = bsn.Items;
            int num = Math.Min(items.Length, values.Length);
            var bldr = ArgTuple.CreateBuilder(num, init: true);
            int ivDst = 0;

            sum = null;
            switch (typeKey.RootKind)
            {
            case DKind.I8:
                if (!(values is long[] ai8))
                    return false;
                for (int ivSrc = 0; ivSrc < num; ivSrc++)
                {
                    var value = ai8[ivSrc];
                    if (value == 0 && bop == BinaryOp.Mul)
                        continue;
                    bldr[ivDst++] = BndVariadicOpNode.Create(typeKey, bop, BndCastNumNode.Create(bsn.Items[ivSrc], typeKey), BndIntNode.CreateI8(value), inv: false);
                }
                if (ivDst == 0)
                {
                    sum = BndIntNode.CreateI8(0);
                    return true;
                }
                break;
            case DKind.R8:
                // REVIEW: What about non-finite values? This will be problematic for them since 0 times non-finite produces NaN.
                if (!(values is double[] ar8))
                    return false;
                for (int ivSrc = 0; ivSrc < num; ivSrc++)
                {
                    var value = ar8[ivSrc];
                    if (value == 0 && bop == BinaryOp.Mul)
                        continue;
                    bldr[ivDst++] = BndVariadicOpNode.Create(typeKey, bop, BndCastNumNode.Create(bsn.Items[ivSrc], typeKey), BndFltNode.CreateR8(value), inv: false);
                }
                if (ivDst == 0)
                {
                    sum = BndFltNode.CreateR8(0);
                    return true;
                }
                break;
            default:
                // REVIEW: Handle other cases, eg, IA, opt, etc.
                return false;
            }
            Validation.Assert(ivDst > 0);
            Validation.AssertIndexInclusive(ivDst, bldr.Count);

            if (ivDst < bldr.Count)
                bldr.RemoveTail(ivDst);
            sum = BndVariadicOpNode.Create(typeKey, BinaryOp.Add, bldr.ToImmutable(), default);
            return true;
        }

        private CodeGenResult RunCodeGen(BoundNode node)
        {
            try { return _codeGen.Run(node); }
            catch { return null; }
        }

        /// <summary>
        /// Given a source sequence, <paramref name="src"/>, and grouping of the source values, <paramref name="grps"/>,
        /// return an array of lists of indices, where each list corresponds to a group and contains the indices into
        /// <paramref name="src"/> that make up the group.
        /// 
        /// Note that the return tuple doesn't depend on the type parameter so the extraction on the other end
        /// isn't complicated.
        /// </summary>
        private (List<int>[], Array[]) GetPartitionIndices<T>(IEnumerable<T> src, IEnumerable<IEnumerable<T>> grps)
        {
            var map = new Dictionary<T, int>();
            int cgrp = 0;
            int indexNull = -1;
            int count = 0;
            List<T[]> grpsArr = new List<T[]>();
            foreach (var grp in grps)
            {
                if (!(grp is T[] arr))
                    arr = grp.ToArray();
                grpsArr.Add(arr);
                foreach (var item in arr)
                {
                    count++;
                    if (item == null)
                    {
                        Validation.Assert(indexNull < 0 || indexNull == cgrp);
                        indexNull = cgrp;
                    }
                    else if (map.TryGetValue(item, out int i))
                        Validation.Assert(i == cgrp);
                    else
                        map.Add(item, cgrp);
                }
                cgrp++;
            }
            Validation.Assert(cgrp == grpsArr.Count);

            var inds = new List<int>[cgrp];
            var arrs = new T[cgrp][];
            for (int igrp = 0; igrp < cgrp; igrp++)
            {
                inds[igrp] = new List<int>();
                arrs[igrp] = grpsArr[igrp];
            }

            int iitem = 0;
            foreach (var item in src)
            {
                int igrp;
                if (item == null)
                    igrp = indexNull;
                else if (!map.TryGetValue(item, out igrp))
                    igrp = -1;
                Validation.Assert(0 <= igrp & igrp < cgrp);
                inds[igrp].Add(iitem);
                iitem++;
            }
            Validation.Assert(iitem == count);

            return (inds, arrs);
        }

        private bool TryGetIndsAndValues(BoundNode node, out BndSequenceNode inds, out BndArrConstNode vals)
        {
            if (node is BndCallNode call &&
                call.Oper == SelectFunc.Seq && call.Args.Length == 2 &&
                call.Args[0] is BndSequenceNode bsn && bsn.Type == DType.BitReq.ToSequence() &&
                call.Args[1] is BndArrConstNode bacn)
            {
                inds = bsn;
                vals = bacn;
                return true;
            }

            inds = null;
            vals = null;
            return false;
        }

        private bool TryGetVarsAndValues(BoundNode node, out BndSequenceNode vars, out BndArrConstNode vals, out BoundNode sel, out ScopeTuple scopes)
        {
            if (node is BndCallNode call &&
                call.Oper == ForEachFunc.ForEach && call.Args.Length == 3 &&
                call.Args[0] is BndSequenceNode bsn &&
                call.Args[1] is BndArrConstNode bacn)
            {
                vars = bsn;
                vals = bacn;
                sel = call.Args[2];
                scopes = call.Scopes;
                return true;
            }

            vars = null;
            vals = null;
            sel = null;
            scopes = default;
            return false;
        }

        private bool TryMapSeq(ref BndArrConstNode seq, BoundNode sel, ArgScope scope)
        {
            Validation.AssertValue(seq);
            Validation.AssertValue(sel);
            Validation.AssertValue(scope);
            Validation.Assert(scope.Kind == ScopeKind.SeqItem);
            Validation.Assert(References.OnlyScope(sel, scope));

            if (!_typeManager.TryEnsureSysType(sel.Type, out Type stItemDst))
                return false;

            var call = BndCallNode.Create(ForEachFunc.ForEach, sel.Type.ToSequence(), ArgTuple.Create(seq, sel), ScopeTuple.Create(scope));

            return TryDoMap(call, stItemDst, out seq);
        }

        private bool TryDoMap(BoundNode bnd, Type stItem, out BndArrConstNode arr)
        {
            arr = null;

            var code = RunCodeGen(bnd);
            if (code == null)
                return false;
            var globs = code.Globals;
            if (globs.Length > 1)
                return false;
            bool needExec = globs.Length > 0;
            if (needExec && !globs[0].IsCtx)
                return false;

            object res;
            if (!needExec)
                res = code.Func(Array.Empty<object>());
            else
                res = code.Func(new object[] { ExecCtx.CreateBare() });

            var meth = new Func<object, object[]>(ToArray<object>)
                .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
            var items = (Array)meth.Invoke(null, new object[] { res });

            arr = BndArrConstNode.Create(_typeManager, bnd.Type, items);
            return true;
        }

        private static T[] ToArray<T>(object res)
        {
            if (res is null)
                return Array.Empty<T>();

            Validation.Assert(res is IEnumerable<T>);
            var able = (IEnumerable<T>)res;
            if (able is T[] arr)
                return arr;
            return able.ToArray();
        }

        private static T[] ToArray<T>(IEnumerable<T> able)
        {
            Validation.AssertValue(able);
            if (able is T[] arr)
                return arr;
            return able.ToArray();
        }

        protected override bool PreVisitImpl(BndGetFieldNode bnd, int idx)
        {
            Validation.AssertValue(bnd);

            int cur = idx + 1;
            var with = new WithInfo(this);
            var rec = with.Process(bnd.Record, ref cur);
            Validation.Assert(cur == idx + bnd.NodeCount);

            BoundNode result = bnd;
            if (rec is BndRecConstNode brcn)
            {
                Validation.Assert(brcn.Value is RecordBase);
                Validation.Assert(brcn.TypeManager == _typeManager);
                Validation.Assert(_typeManager.IsOfType(brcn.Value, brcn.Type) != TriState.No);
                var recVal = (RecordBase)brcn.Value;
                if (_typeManager.TryGetFieldValue(rec.Type, bnd.Name, recVal, out var typeFld, out _, out var val) &&
                    _typeManager.TryWrapConst(typeFld, val, out var bcnst))
                {
                    result = bcnst;
                }
            }
            else if (rec is BndCallNode bcn && bcn.Oper == SelectFunc.Req && bcn.Args[1] is BndArrConstNode seq)
            {
                var scope = ArgScope.Create(ScopeKind.SeqItem, seq.Type.ItemTypeOrThis);
                var sel = BndGetFieldNode.Create(bnd.Name, BndScopeRefNode.Create(scope));

                if (TryMapSeq(ref seq, sel, scope))
                {
                    var vars = bcn.Args[0];
                    Validation.Assert(vars.Type == DType.BitReq.ToSequence());
                    result = BndCallNode.Create(SelectFunc.Req, bnd.Type, ArgTuple.Create(vars, seq));
                }
            }

            if (result == bnd && rec != bnd.Record)
                result = BndGetFieldNode.Create(bnd.Name, rec);

            Validation.Coverage(with.HasAny ? 0 : 1);
            Push(bnd, with.Apply(result));
            return false;
        }

        protected override bool PreVisitImpl(BndSequenceNode bnd, int idx)
        {
            if (bnd.ChildKinds == BndNodeKindMask.FreeVar)
            {
                Push(bnd, bnd);
                return false;
            }
            return base.PreVisitImpl(bnd, idx);
        }

        protected override bool PreVisitImpl(BndTensorNode bnd, int idx)
        {
            if (bnd.ChildKinds == BndNodeKindMask.FreeVar)
            {
                Push(bnd, bnd);
                return false;
            }
            return base.PreVisitImpl(bnd, idx);
        }

#if REVIEW // Should we do this as part of symbol reduction or wait until linear extraction?
        protected override BoundNode ReduceFractionalAdd(FracOps frops, BndVariadicOpNode bnd, ArgTuple.Builder bldr)
        {
            Validation.AssertValue(frops);
            Validation.Assert(frops.Kind == DKind.R8);
            Validation.AssertValue(frops);
            Validation.AssertValue(bnd);
            Validation.Assert(bnd.Op == BinaryOp.Add);
            Validation.Assert(bnd.Type == DType.R8Req);
            Validation.AssertValueOrNull(bldr);

            var args = bnd.Args;
            int lenSrc = args.Length;
            Validation.Assert(lenSrc > 0);
            Validation.Assert(args.All(a => a.Type == bnd.Type));
            Validation.Assert(bldr == null || bldr.Count == lenSrc);
            Validation.Assert(bldr == null || bldr.All(a => a.Type == bnd.Type));

            var invs = bnd.Inverted;
            Validation.Assert(!invs.TestAtOrAbove(lenSrc));

            // REVIEW: This assumes associativity, which technically isn't valid for floating point.
            BndFltNode bfnRes = null;
            double res = 0;
            int ivDst = 0;
            for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
            {
                Validation.Assert(0 <= ivDst && ivDst <= ivSrc);

                var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
                bool inv = invs.TestBit(ivSrc);
                if (arg is BndFltNode bfn)
                {
                    bfnRes = bfn;
                    frops.Add(ref res, bfn.Value, inv);
                    continue;
                }

                if (ivDst < ivSrc)
                {
                    invs = inv ? invs.SetBit(ivDst) : invs.ClearBit(ivDst);
                    bldr ??= args.ToBuilder();
                    bldr[ivDst] = arg;
                }
                ivDst++;
            }

            if (ivDst == 0)
            {
                if (bfnRes != null && bfnRes.Value.ToBits() == res.ToBits())
                    return bfnRes;
                return frops.CreateConst(res);
            }

            if (res != 0)
            {
                Validation.Assert(ivDst < lenSrc);
                Validation.Assert(bfnRes != null);

                // If bfnRes is already at the end, no need to change anything.
                if (bfnRes.Value.ToBits() != res.ToBits())
                    bfnRes = frops.CreateConst(res);
                else if (bldr == null && ivDst == lenSrc - 1 && bfnRes == args[lenSrc - 1])
                    return bnd;
                bldr ??= args.ToBuilder();
                invs = invs.ClearBit(ivDst);
                bldr[ivDst++] = bfnRes;
            }
            Validation.Assert(ivDst <= lenSrc);

            if (ivDst == 1 && !invs.TestBit(0))
            {
                var arg = bldr != null ? bldr[0] : args[0];
                return arg;
            }

            if (ivDst < lenSrc)
            {
                invs = invs.ClearAtAndAbove(ivDst);
                bldr ??= args.ToBuilder();
                bldr.RemoveTail(ivDst);
            }

            if (bldr == null || AreEquiv(bldr, bnd.Args))
            {
                Validation.Assert(invs == bnd.Inverted);
                return bnd;
            }

            return BndVariadicOpNode.Create(bnd.Type, BinaryOp.Add, bldr.ToImmutable(), invs);
        }

        protected override BoundNode ReduceFractionalMul(FracOps frops, BndVariadicOpNode bnd, ArgTuple.Builder bldr)
        {
            Validation.AssertValue(frops);
            Validation.Assert(frops.Kind == DKind.R8);
            Validation.AssertValue(frops);
            Validation.AssertValue(bnd);
            Validation.Assert(bnd.Op == BinaryOp.Mul);
            Validation.Assert(bnd.Type == DType.R8Req);
            Validation.AssertValueOrNull(bldr);

            var args = bnd.Args;
            int lenSrc = args.Length;
            Validation.Assert(lenSrc > 0);
            Validation.Assert(args.All(a => a.Type == bnd.Type));
            Validation.Assert(bldr == null || bldr.Count == lenSrc);
            Validation.Assert(bldr == null || bldr.All(a => a.Type == bnd.Type));

            var invs = bnd.Inverted;
            Validation.Assert(!invs.TestAtOrAbove(lenSrc));

            // REVIEW: This assumes associativity, which technically isn't valid for floating point.
            BndFltNode bfnRes = null;
            double res = 1;
            int ivDst = 0;
            for (int ivSrc = 0; ivSrc < lenSrc; ivSrc++)
            {
                Validation.Assert(0 <= ivDst && ivDst <= ivSrc);

                var arg = bldr != null ? bldr[ivSrc] : args[ivSrc];
                bool inv = invs.TestBit(ivSrc);
                if (arg is BndFltNode bfn)
                {
                    // Treat multiplication by zero (or division by infinity) as zero, ignoring the possibility
                    // of infinities and nans.
                    // REVIEW: This may produce unexpected results since the reducer may change
                    // 0 / 0 to nan before this sees it. If we also map nan to zero, then it should be
                    // consistent.
                    if (inv ? bfn.Value.IsInfinite() : bfn.Value == 0)
                        return frops.CreateConst(0);
                    frops.Mul(ref res, bfn.Value, inv);
                    if (res == 0)
                        return frops.CreateConst(0);
                    bfnRes = bfn;
                    continue;
                }

                if (ivDst < ivSrc)
                {
                    invs = inv ? invs.SetBit(ivDst) : invs.ClearBit(ivDst);
                    bldr ??= args.ToBuilder();
                    bldr[ivDst] = arg;
                }
                ivDst++;
            }
            Validation.Assert(res != 0);

            if (ivDst == 0)
            {
                if (bfnRes != null && bfnRes.Value.ToBits() == res.ToBits())
                    return bfnRes;
                return frops.CreateConst(res);
            }

            if (res != 1)
            {
                Validation.Assert(ivDst < lenSrc);
                Validation.Assert(bfnRes != null);

                // If bfnRes is already at the end, no need to change anything.
                if (bfnRes.Value.ToBits() != res.ToBits())
                    bfnRes = frops.CreateConst(res);
                else if (bldr == null && ivDst == lenSrc - 1 && bfnRes == args[lenSrc - 1])
                    return bnd;
                bldr ??= args.ToBuilder();
                invs = invs.ClearBit(ivDst);
                bldr[ivDst++] = bfnRes;
            }
            Validation.Assert(ivDst <= lenSrc);

            if (ivDst == 1 && !invs.TestBit(0))
            {
                var arg = bldr != null ? bldr[0] : args[0];
                return arg;
            }

            if (ivDst < lenSrc)
            {
                invs = invs.ClearAtAndAbove(ivDst);
                bldr ??= args.ToBuilder();
                bldr.RemoveTail(ivDst);
            }

            if (bldr == null || AreEquiv(bldr, bnd.Args))
            {
                Validation.Assert(invs == bnd.Inverted);
                return bnd;
            }

            return BndVariadicOpNode.Create(bnd.Type, BinaryOp.Mul, bldr.ToImmutable(), invs);
        }
#endif
    }
}

public static class References
{
    /// <summary>
    /// Returns true iff the only "external" value referenced (if any) is the given scope.
    /// </summary>
    public static bool OnlyScope(BoundNode bnd, ArgScope scope)
    {
        // If the node references any globals or symbolic variables, return false.
        if ((bnd.AllKinds & (BndNodeKindMask.FreeVar | BndNodeKindMask.Global)) != 0)
            return false;
        var impl = new Impl(bsrn => bsrn.Scope == scope, x => false);
        int num = bnd.Accept(impl, 0);
        Validation.Assert(num == bnd.NodeCount);
        return impl.Ok;
    }

    /// <summary>
    /// Returns true iff the only "external" values referenced (if any) are the given scopes.
    /// </summary>
    public static bool OnlyScopes(BoundNode bnd, ArgScope s0, ArgScope s1)
    {
        // If the node references any globals or symbolic variables, return false.
        if ((bnd.AllKinds & (BndNodeKindMask.FreeVar | BndNodeKindMask.Global)) != 0)
            return false;
        var impl = new Impl(bsrn => bsrn.Scope == s0 || bsrn.Scope == s1, x => false);
        int num = bnd.Accept(impl, 0);
        Validation.Assert(num == bnd.NodeCount);
        return impl.Ok;
    }

    /// <summary>
    /// Returns true iff the only "external" values referenced (if any) are the given scopes.
    /// </summary>
    public static bool OnlyScopes(BoundNode bnd, ScopeTuple scopes)
    {
        // If the node references any globals or symbolic variables, return false.
        if ((bnd.AllKinds & (BndNodeKindMask.FreeVar | BndNodeKindMask.Global)) != 0)
            return false;
        switch (scopes.Length)
        {
        case 1:
            return OnlyScope(bnd, scopes[0]);
        case 2:
            return OnlyScopes(bnd, scopes[0], scopes[1]);
        default:
            var impl = new Impl(bsrn => scopes.Contains(bsrn.Scope), x => false);
            int num = bnd.Accept(impl, 0);
            Validation.Assert(num == bnd.NodeCount);
            return impl.Ok;
        }
    }

    private sealed class Impl : NoopScopedBoundTreeVisitor
    {
        private readonly Func<BndScopeRefNode, bool> _okScope;
        private readonly Func<BndGlobalNode, bool> _okGlobal;

        public bool Ok;

        public Impl(Func<BndScopeRefNode, bool> okScope, Func<BndGlobalNode, bool> okGlobal)
        {
            _okScope = okScope;
            _okGlobal = okGlobal;
            Ok = true;
        }

        protected override void VisitImpl(BndGlobalNode bnd, int idx)
        {
            if (!_okGlobal(bnd))
                Ok = false;
            base.VisitImpl(bnd, idx);
        }

        protected override void VisitCore(BndScopeRefNode bnd, int idx, PushedScope ps)
        {
            Validation.AssertValue(bnd);
            Validation.AssertValueOrNull(ps);

            if (ps == null && !_okScope(bnd))
                Ok = false;
            base.VisitCore(bnd, idx);
        }

        protected override bool PreVisitCore(BndParentNode bnd, int idx)
        {
            return Ok && base.PreVisitCore(bnd, idx);
        }
    }
}