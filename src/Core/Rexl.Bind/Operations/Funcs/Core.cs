// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using ArgTuple = Immutable.Array<BoundNode>;
using DirTuple = Immutable.Array<Directive>;
using NameTuple = Immutable.Array<DName>;
using ScopeTuple = Immutable.Array<ArgScope>;

/// <summary>
/// The kinds of filtering that are supported by sequence operations such as ForEach, Take, Drop, etc.
/// REVIEW: Change TakeXxx to also use this.
/// </summary>
public enum SeqFilterKind : byte
{
    None,
    If,
    While,
}

/// <summary>
/// The <c>ForEach</c> function family. These handle filtering (with a predicate supporting both if and while)
/// and zipping (multiple sequences). When filtering, there are two nested args, the predicate and the selector.
/// When not filtering, there is only a selector.
/// </summary>
public sealed partial class ForEachFunc : RexlOper
{
    public static readonly ForEachFunc ForEach = new ForEachFunc("ForEach", SeqFilterKind.None, 2);
    public static readonly ForEachFunc ForEachIf = new ForEachFunc("ForEachIf", SeqFilterKind.If, 3);
    public static readonly ForEachFunc ForEachWhile = new ForEachFunc("ForEachWhile", SeqFilterKind.While, 3);

    /// <summary>
    /// The kind of this func. Note that when this is <see cref="SeqFilterKind.None"/> a call may still
    /// filter (when a directive) is used. To determine the filter kind for a particular call, use
    /// <see cref="GetFilterKind(BndCallNode)"/>.
    /// </summary>
    public SeqFilterKind Kind { get; }

    private ForEachFunc(string name, SeqFilterKind kind, int arityMin)
        : base(isFunc: true, new DName(name), arityMin, arityMax: int.MaxValue)
    {
        Kind = kind;
    }

    /// <summary>
    /// Returns whether <paramref name="dir"/> is <see cref="Directive.If"/> or <see cref="Directive.While"/>.
    /// </summary>
    private static bool IsSupportedDirective(Directive dir)
    {
        return dir == Directive.If || dir == Directive.While;
    }

    private static bool HasPredicate(ArgTraits traits)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper is ForEachFunc);
        return traits.NestedCount == 2;
    }

    /// <summary>
    /// This expects <paramref name="call"/> to be an invocation of <see cref="ForEachFunc"/> of <see cref="SeqFilterKind.None"/>.
    /// If <paramref name="call"/> has a predicate, returns its directive. Otherwise returns <see cref="Directive.None"/>.
    /// </summary>
    private static Directive GetPredDir(BndCallNode call)
    {
        Validation.Assert(call.Oper.IsValidCall(call));
        Validation.Assert(call.Oper == ForEach);
        Validation.AssertValue(call.Traits);
        if (!HasPredicate(call.Traits))
            return Directive.None;
        var dir = call.GetDirective(call.Args.Length - 2);
        Validation.Assert(IsSupportedDirective(dir));
        return dir;
    }

    /// <summary>
    /// This expects <paramref name="call"/> to be an invocation of <see cref="ForEachFunc"/>.
    /// Returns whether that invocation has a predicate or just a selector.
    /// </summary>
    public static bool HasPredicate(BndCallNode call)
    {
        Validation.BugCheckValue(call, nameof(call));
        Validation.BugCheckParam(call.Oper is ForEachFunc, nameof(call));
        Validation.BugCheckParam(call.Oper.IsValidCall(call), nameof(call));
        return HasPredicate(call.Traits);
    }

    /// <summary>
    /// Get the filter kind for the given invocation of a <c>ForEachXx</c> function.
    /// </summary>
    public static SeqFilterKind GetFilterKind(BndCallNode call)
    {
        Validation.BugCheckValue(call, nameof(call));
        if (call.Oper is not ForEachFunc fef)
            throw Validation.BugExceptParam(nameof(call));
        Validation.BugCheckParam(call.Oper.IsValidCall(call), nameof(call));

        if (fef.Kind != SeqFilterKind.None)
        {
            Validation.Assert(HasPredicate(call.Traits));
            return fef.Kind;
        }

        if (!HasPredicate(call.Traits))
            return SeqFilterKind.None;

        var dir = call.GetDirective(call.Args.Length - 2);
        Validation.Assert(IsSupportedDirective(dir));
        return dir == Directive.If ? SeqFilterKind.If : SeqFilterKind.While;
    }

    protected override ArgTraits GetArgTraitsCore(int carg, NameTuple names, BitSet implicitNames, DirTuple dirs)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        bool hasPred = Kind != SeqFilterKind.None ||
            (carg > 2 && IsSupportedDirective(dirs.GetItemOrDefault(carg - 2)));
        return ArgTraitsZip.Create(this, indexed: true, eager: false, carg, seqCount: carg - (hasPred ? 2 : 1));
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        throw new InvalidOperationException();
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot == 0 || slot != traits.SlotCount - 2)
            return false;
        if (HasPredicate(traits))
        {
            if (dir == Directive.If && Kind != SeqFilterKind.While)
                return true;
            if (dir == Directive.While && Kind != SeqFilterKind.If)
                return true;
        }

        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.AssertValue(info.Traits);

        var types = info.GetArgTypes();
        int arity = types.Count;
        var traits = info.Traits;
        int cseq = traits.ScopeCount;

        Validation.Assert(cseq == arity - traits.NestedCount);

        for (int i = 0; i < cseq; i++)
            EnsureTypeSeq(types, i);
        if (HasPredicate(traits))
            types[arity - 2] = DType.BitReq;
        return (types[arity - 1].ToSequence(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int carg = args.Length;
        var traits = call.Traits;
        int cseq = traits.ScopeCount;

        Validation.Assert(cseq == carg - traits.NestedCount);

        if (Kind != SeqFilterKind.None)
            full = false;

        var typeItem = args[carg - 1].Type;
        if (type != typeItem.ToSequence())
            return false;
        if (HasPredicate(traits) && args[carg - 2].Type != DType.BitReq)
            return false;
        for (int slot = 0; slot < cseq; slot++)
        {
            if (!args[slot].Type.IsSequence)
                return false;
        }
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        int cseq = call.Scopes.Length;
        bool full = !HasPredicate(call.Traits);
        if (!full && call.Args[cseq].TryGetBool(out bool ok))
        {
            if (!ok)
                return (0, 0);
            full = true;
        }

        long min = long.MaxValue;
        long max = long.MaxValue;
        for (int i = 0; i < cseq; i++)
        {
            var (a, b) = call.Args[i].GetItemCountRange();
            if (min > a)
                min = a;
            if (max > b)
                max = b;
        }
        return (full ? min : 0, max);
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg < call.Scopes.Length);
        return PullWithFlags.Both;
    }

    /// <summary>
    /// Flatten nested invocations of <c>ForEach</c>. In particular <c>ForEach(x:A, y:ForEach(z:B, sel0), sel1)</c>
    /// flattens to <c>ForEach(x:A, z:B, With(y:sel0, sel1))</c>. Note that the <c>With</c> invocation may be
    /// eliminated by the reducer. Also note that the outermost ForEach can also be ForEachIf, with a similar
    /// transformation of the predicate.
    /// 
    /// Note that <c>ForEach(x: ForEach(z:B, [if]b, sel0), sel1)</c> can be flattened to
    /// <c>ForEach(x: B, [if]b, With(z:sel0, sel1)</c>. But we can't do the same if there are
    /// multiple sequences such as <c>ForEach(x: S, y:ForEach(z:B, [if]b, sel0), sel1)</c>.
    /// 
    /// </summary>
    private BndCallNode Flatten(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        Validation.Assert(Kind == SeqFilterKind.None);

        ArgTuple.Builder bldr = null;
        ScopeTuple.Builder bldrScopes = null;
        ArgTuple.Builder bldrPred = null;
        List<(BoundNode arg, ArgScope scope)> withs = null;
        var args = call.Args;
        var carg = call.Args.Length;
        var scopes = call.Scopes;
        var cseq = scopes.Length;
        var ctail = call.Traits.NestedCount;
        Validation.Assert(cseq == carg - ctail);
        Validation.Assert(cseq > 0);

        var indices = call.Indices;
        Validation.Assert(indices.Length == 1);

        var index = indices[0];
        Directive dirNested = Directive.None;
        var dir = GetPredDir(call);
        var bndSel = args[carg - 1];
        var bndPred = dir == Directive.None ? null : args[carg - 2];
        for (int ivSrc = 0; ivSrc < cseq; ivSrc++)
        {
            var arg = args[ivSrc];
            var scope = scopes[ivSrc];
            var separate = true;
            if (dir != Directive.None)
            {
                Validation.AssertValue(bndPred);
                var usePred = ScopeCounter.Any(bndPred, scope);
                var useSel = ScopeCounter.Any(bndSel, scope);
                separate = !usePred || !useSel;
            }

            Directive dirInner;
            if (separate && arg is BndCallNode bcn && bcn.Oper is ForEachFunc &&
                ((dirInner = GetPredDir(bcn)) != Directive.If || cseq == 1) &&
                (dir == Directive.None || dirInner == Directive.None || dir == dirInner))
            {
                if (bldr == null)
                {
                    bldr = args.ToBuilder();
                    bldrScopes = scopes.ToBuilder();
                    bldr.RemoveTail(ivSrc);
                    bldrScopes.RemoveTail(ivSrc);
                    withs = new List<(BoundNode arg, ArgScope scope)>();
                }

                int iarg = 0;
                int num = bcn.Scopes.Length;
                Validation.Assert(num > 0);

                for (; iarg < num; iarg++)
                {
                    Validation.Assert(bcn.Scopes[iarg].Kind == ScopeKind.SeqItem);
                    bldr.Add(bcn.Args[iarg]);
                    bldrScopes.Add(bcn.Scopes[iarg]);
                }
                Validation.Assert(bcn.Indices.Length == 1);
                var indCur = bcn.Indices[0];

                if (dirInner != 0)
                {
                    Validation.Assert(dirInner == Directive.While || dirInner == Directive.If && cseq == 1);
                    bldrPred ??= ArgTuple.CreateBuilder(cseq);
                    bldrPred.Add(bcn.Args[iarg]);
                    dirNested = bcn.Directives[iarg];
                    iarg++;
                }

                var argWith = bcn.Args[iarg];
                Validation.Assert(argWith.Type == scope.Type);
                if (indCur != null)
                {
                    if (index == null)
                        index = indCur;
                    else
                        argWith = reducer.MapScope(argWith, indCur, index);
                }

                withs.Add((argWith, scope));
            }
            else if (bldr != null)
            {
                bldr.Add(arg);
                bldrScopes.Add(scope);
            }
        }

        if (bldr == null)
        {
            Validation.Assert(bldrScopes == null);
            Validation.Assert(withs == null);
            Validation.Assert(call.Indices[0] == index);
            return call;
        }

        Validation.Assert(bldrScopes != null);
        Validation.Assert(bldr.Count == bldrScopes.Count);
        Validation.Assert(withs != null && withs.Count > 0);

        var map = new Dictionary<ArgScope, ArgScope>(withs.Count);
        var bldrWith = Immutable.Array.CreateBuilder<BoundNode>(withs.Count + 1, init: true);
        var bldrScopesWith = Immutable.Array.CreateBuilder<ArgScope>(withs.Count, init: true);
        for (int i = 0; i < withs.Count; i++)
        {
            var (argSrc, scopeSrc) = withs[i];
            Validation.Assert(argSrc.Type == scopeSrc.Type);
            var scope = ArgScope.Create(ScopeKind.With, scopeSrc.Type);
            map.Add(scopeSrc, scope);
            bldrWith[i] = argSrc;
            bldrScopesWith[i] = scope;
        }

        DirTuple dirs;
        var scopesWith = bldrScopesWith.ToImmutable();
        if (ctail == 2)
        {
            // Create and reduce the new predicate.
            bldrWith[withs.Count] = bndPred;
            BoundNode pred = BndCallNode.Create(WithFunc.With, bndPred.Type,
                bldrWith.ToImmutableCopy(), scopesWith);
            if (bldrPred != null)
            {
                bldrPred.Add(pred);
                pred = BndVariadicOpNode.Create(DType.BitReq, BinaryOp.And, bldrPred.ToImmutable(), default);
            }
            pred = reducer.MapScopes(pred, map);
            pred = reducer.Reduce(pred);
            bldr.Add(pred);

            // Use the same with builder.
            // Note that this is only valid because we only flatten scopes which are not
            // shared between the predicate and selector. Otherwise we'd need to reinstantiate
            // the scope refs to avoid reuse.
            bldrWith[withs.Count] = null;

            // Flattening may grow the number of args. Match the directives if there are any.
            int cargNew = bldr.Count + 1;
            dirs = call.Directives;
            if (cargNew != dirs.Length)
                dirs = MakeDirectives(cargNew, dir);
            Validation.Assert(dirs[cargNew - 2] == dir);
        }
        else if (bldrPred != null)
        {
            Validation.Assert(dirNested != Directive.None);
            Validation.Assert(bldrPred.Count > 0);
            var pred = bldrPred[0];
            if (bldrPred.Count > 1)
            {
                Validation.Assert(dirNested == Directive.While);
                pred = BndVariadicOpNode.Create(DType.BitReq, BinaryOp.And, bldrPred.ToImmutable(), default);
            }
            pred = reducer.MapScopes(pred, map);
            pred = reducer.Reduce(pred);
            bldr.Add(pred);
            dirs = MakeDirectives(bldr.Count + 1, dirNested);
        }
        else
            dirs = default;

        // Create and reduce the new selector.
        bldrWith[withs.Count] = reducer.MapScopes(bndSel, map);
        BoundNode sel = BndCallNode.Create(WithFunc.With, bndSel.Type, bldrWith.ToImmutable(), scopesWith);
        sel = reducer.Reduce(sel);
        bldr.Add(sel);

        args = bldr.ToImmutable();
        scopes = bldrScopes.ToImmutable();

        if (index != indices[0])
        {
            Validation.Assert(indices[0] == null);
            indices = ScopeTuple.Create(index);
        }

        return BndCallNode.Create(this, sel.Type.ToSequence(), args, scopes, indices, dirs, default);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode bnd)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(bnd));

        // First, map ForEachIf calls onto ForEach.
        DirTuple dirs;
        Directive dir;
        int carg;
        if (Kind != SeqFilterKind.None)
        {
            Validation.Assert(HasPredicate(bnd.Traits));
            dir = Kind == SeqFilterKind.If ? Directive.If : Directive.While;
            carg = bnd.Args.Length;
            dirs = MakeDirectives(carg, dir);
            Validation.Assert(carg == dirs.Length);
            return reducer.Reduce(BndCallNode.Create(ForEach, bnd.Type, bnd.Args, bnd.Scopes, bnd.Indices, dirs, bnd.Names));
        }

        // All calls should be of CallKind.None at this point.
        Validation.Assert(Kind == SeqFilterKind.None);

        // Then flatten.
        var call = Flatten(reducer, bnd);
        dir = GetPredDir(call);

        Validation.Assert(call.Type == bnd.Type);
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Oper == ForEach);

        var args = call.Args;
        var scopes = call.Scopes;
        var indices = call.Indices;
        int cseq = scopes.Length;
        var ctail = call.Traits.NestedCount;
        carg = args.Length;
        Validation.Assert((ctail == 2) == (dir != Directive.None));
        Validation.Assert(carg == cseq + ctail);
        Validation.Assert(cseq > 0);
        Validation.Assert(indices.Length == 1);

        // REVIEW: If there are any Range(n) sequences that don't determine the length, they
        // can be replaced with indexing. Range scopes?

        // * If at least one of the sequences is empty/null, the result is empty.
        // * If at least one of the sequences is known size, the known-size ones can be clipped to
        //   the minimal known length. Eg, ForEach([ x ], [ y, z ], sel) can truncate the 2nd arg.
        // * Any sequence that isn't used in the selector/predicate and isn't needed to impose size
        //   can be dropped.
        long lenMaxOfMin = 0;
        long lenMinOfMax = long.MaxValue;
        // The number of sequences whose max is lenMinOfMax.
        int numMinOfMax = 0;
        for (int i = 0; i < cseq; i++)
        {
            var seqCur = args[i];
            Validation.Assert(seqCur.Type.IsSequence);

            var (min, max) = seqCur.GetItemCountRange();
            Validation.AssertIndexInclusive(min, max);
            if (max == 0)
                return BndSequenceNode.CreateEmpty(call.Type);

            if (lenMinOfMax > max)
            {
                lenMinOfMax = max;
                numMinOfMax = 1;
            }
            else if (lenMinOfMax == max)
                numMinOfMax++;

            if (lenMaxOfMin < min)
                lenMaxOfMin = min;
        }
        Validation.Assert(lenMinOfMax > 0);

        if (dir != Directive.None && args[cseq].TryGetBool(out bool cond))
        {
            // Predicate is constant.
            if (!cond)
                return BndSequenceNode.CreateEmpty(call.Type);

            // Drop the predicate.
            --carg;
            --ctail;
            dir = Directive.None;
            args = args.RemoveAt(carg - 1);
            call = BndCallNode.Create(this, call.Type, args, scopes, indices);
        }

        Validation.Assert(call.Args.Length == carg);
        Validation.Assert(call.Args.AreIdentical(args));
        Validation.Assert(ctail == call.Traits.NestedCount);

        ArgTuple.Builder bldrArgs;
        ScopeTuple.Builder bldrScopes;
        int ivDst;

        if (lenMinOfMax <= lenMaxOfMin)
        {
            Validation.Assert(numMinOfMax > 0);

            // Truncate sequences to lenMinOfMax. Also drop sequences that aren't used in the
            // selector/predicate and can't affect the final length.
            bldrArgs = null;
            bldrScopes = null;
            ivDst = 0;
            var val0 = call.Args[cseq];
            var val1 = dir != Directive.None ? call.Args[cseq + 1] : null;
            for (int ivSrc = 0; ivSrc < cseq; ivSrc++)
            {
                var seqCur = args[ivSrc];
                Validation.Assert(seqCur.Type.IsSequence);

                var (min, max) = seqCur.GetItemCountRange();
                Validation.Assert(lenMaxOfMin >= min);
                Validation.Assert(lenMinOfMax <= max);

                // See if we can drop it.
                if (min >= lenMinOfMax &&
                    !ScopeCounter.Any(val0, scopes[ivSrc]) &&
                    (val1 == null || !ScopeCounter.Any(val1, scopes[ivSrc])))
                {
                    if (max == lenMinOfMax)
                        numMinOfMax--;
                    continue;
                }

                // See if we can clip it.
                var seqNew = seqCur;
                if (min > lenMinOfMax)
                {
                    if (seqCur is BndSequenceNode bsn)
                    {
                        seqNew = BndSequenceNode.Create(bsn.Type, bsn.Items.RemoveTail((int)lenMinOfMax));
                        numMinOfMax++;
                    }
                    else if (seqCur is BndCallNode c)
                    {
                        // REVIEW: Clip invocations of Repeat, Range, Take, etc.
                        if (c.Oper == RepeatFunc.Instance)
                        {
                            var argsNew = ArgTuple.Create(c.Args[0], BndIntNode.CreateI8(lenMinOfMax));
                            seqNew = BndCallNode.Create(c.Oper, c.Type, argsNew);
                            numMinOfMax++;
                        }
                        else if (c.Oper == RangeFunc.Instance)
                        {
                            int arity = c.Args.Length;
                            bldrArgs ??= args.ToBuilder();
                            ArgTuple argsNew;
                            if (arity == 1)
                                argsNew = ArgTuple.Create(BndIntNode.CreateI8(lenMinOfMax));
                            else if (arity == 2)
                            {
                                c.Args[0].TryGetIntegral(out var start).Verify();
                                argsNew = ArgTuple.Create(c.Args[0],
                                    BndIntNode.CreateI8((long)start + lenMinOfMax));
                            }
                            else
                            {
                                Validation.Assert(arity == 3);
                                c.Args[0].TryGetIntegral(out var start).Verify();
                                c.Args[2].TryGetIntegral(out var step).Verify();
                                argsNew = ArgTuple.Create(c.Args[0],
                                    BndIntNode.CreateI8((long)start + lenMinOfMax * (long)step), c.Args[2]);
                            }
                            seqNew = BndCallNode.Create(c.Oper, c.Type, argsNew);
                            numMinOfMax++;
                        }
                        else
                        {
                            // REVIEW: Handle others, eg, Take.
                        }
                    }
                }

                if (ivDst < ivSrc || seqNew != seqCur)
                    (bldrArgs ??= args.ToBuilder())[ivDst] = seqNew;
                if (ivDst < ivSrc)
                    (bldrScopes ??= scopes.ToBuilder())[ivDst] = scopes[ivSrc];
                ivDst++;
            }
            Validation.AssertIndexInclusive(ivDst, cseq);

            // If the index scope is defined, it is used in a non sequence slot. This is guaranteed
            // by the constructor of BndCallNode.
            var index = indices[0];
            Validation.Assert(index == null || ScopeCounter.Any(val0, index) ||
                (val1 != null && ScopeCounter.Any(val1, index)));
            var noIndex = index == null;
            if (ivDst < cseq)
            {
                if (ivDst == 0 && dir == Directive.None && noIndex)
                {
                    // All sequences can be tossed.
                    if (lenMinOfMax == 1)
                        return BndSequenceNode.Create(call.Type, ArgTuple.Create(val0));
                    if (!val0.HasVolatile)
                    {
                        return BndCallNode.Create(RepeatFunc.Instance, call.Type,
                            ArgTuple.Create(val0, BndIntNode.CreateI8(lenMinOfMax)));
                    }
                }

                if (numMinOfMax == 0)
                {
                    // We need a sequence just for the length, but avoid infinite reduction. That is, if we're only
                    // dropping the last sequence and it looks like one we would manufacture, just keep it.
                    if (bldrArgs != null || ivDst < cseq - 1 || !(call.Args[cseq - 1] is BndCallNode c) ||
                        c.Oper != RepeatFunc.Instance)
                    {
                        bldrArgs ??= args.ToBuilder();
                        bldrScopes ??= scopes.ToBuilder();
                        if (noIndex)
                        {
                            bldrArgs[ivDst] = BndCallNode.Create(
                                RepeatFunc.Instance, DType.BitReq.ToSequence(),
                                ArgTuple.Create(BndIntNode.False, BndIntNode.CreateI8(lenMinOfMax)));
                            bldrScopes[ivDst] = ArgScope.Create(ScopeKind.SeqItem, DType.BitReq);
                        }
                        else
                        {
                            bldrArgs[ivDst] = BndCallNode.Create(RangeFunc.Instance, DType.I8Req.ToSequence(),
                                ArgTuple.Create(BndIntNode.CreateI8(lenMinOfMax)));
                            var scopeNew = ArgScope.Create(ScopeKind.SeqItem, DType.I8Req);
                            bldrScopes[ivDst] = scopeNew;
                            val0 = reducer.MapScope(val0, index, scopeNew);
                            if (val1 != null)
                                val1 = reducer.MapScope(val1, index, scopeNew);
                            indices = indices.SetItem(0, null);
                            index = null;
                        }
                    }
                    ivDst++;
                }
            }

            if (ivDst < cseq)
            {
                cseq = ivDst;
                if (bldrScopes != null)
                {
                    bldrScopes.RemoveTail(cseq);
                    scopes = bldrScopes.ToImmutable();
                }
                else
                    scopes = scopes.RemoveTail(cseq);
                bldrArgs ??= args.ToBuilder();
            }
            else if (bldrScopes != null)
                scopes = bldrScopes.ToImmutable();

            if (bldrArgs != null)
            {
                bldrArgs[ivDst++] = val0;
                Validation.Assert((val1 != null) == (dir != Directive.None));
                if (val1 != null)
                    bldrArgs[ivDst++] = val1;
                bldrArgs.RemoveTail(carg = ivDst);
                args = bldrArgs.ToImmutable();

                if (dir != Directive.None)
                {
                    Validation.Assert(IsSupportedDirective(dir));
                    dirs = MakeDirectives(args.Length, dir);
                }
                else
                    dirs = default;

                call = BndCallNode.Create(this, call.Type, args, scopes, indices, dirs, default);
            }
        }

        Validation.Assert(carg == call.Args.Length);
        Validation.Assert(carg == cseq + ctail);
        Validation.Assert(cseq == call.Traits.ScopeCount);
        Validation.Assert(cseq > 0);
        Validation.Assert(indices.Length == 1);
        if (cseq == 1)
        {
            Validation.Assert(scopes.Length == 1);

            var argSel = args[carg - 1];
            if (argSel is BndScopeRefNode bsr && bsr.Scope == scopes[0])
            {
                // The ForEach is a no-op map.
                Validation.Assert(args[0].Type == call.Type);
                if (dir == Directive.None)
                    return args[0];
                // Map to Take, with the selector dropped.
                return BndCallNode.Create(TakeDropFunc.Take, call.Type, args.RemoveTail(carg - 1), call.Scopes, call.Indices,
                    Immutable.Array<Directive>.Create(Directive.None, dir), default);
            }

            if (dir == Directive.None && args[0] is BndCallNode bcn && bcn.Args.Length == 3)
            {
                if (bcn.Oper is ScanFunc scan && (scan == ScanFunc.ScanZ || indices[0] == null) && bcn.Args[1].Type == bcn.Args[2].Type)
                {
                    // The selector can just become Scan's 4th arg.
                    Validation.Assert(bcn.Scopes.Length == 2);
                    Validation.Assert(bcn.Indices.Length == 1);

                    var indsScan = bcn.Indices;
                    var argsScan = bcn.Args.ToBuilder();
                    var scopeMap = new Dictionary<ArgScope, ArgScope> { { scopes[0], bcn.Scopes[1] } };
                    if (indices[0] != null)
                    {
                        if (indsScan[0] != null)
                        {
                            argsScan[2] = reducer.MapScope(bcn.Args[2], indsScan[0], indices[0]);
                            scopeMap.Add(indsScan[0], indices[0]);
                        }
                        Validation.Coverage(indsScan[0] == null ? 1 : 0);
                        indsScan = indices;
                    }
                    Validation.Coverage(indices[0] == null ? 1 : 0);

                    argSel = reducer.MapScopes(argSel, scopeMap);
                    argsScan.Add(argSel);
                    return BndCallNode.Create(scan, call.Type, argsScan.ToImmutable(),
                        bcn.Scopes, indsScan, dirs: default, bcn.Names);
                }

                if (bcn.Oper == GenerateFunc.Instance)
                {
                    // The selector can just become Generate's 4th arg.
                    Validation.Assert(bcn.Scopes.Length == 2);

                    var scopeMap = new Dictionary<ArgScope, ArgScope> { { scopes[0], bcn.Scopes[1] } };
                    if (indices[0] != null)
                        scopeMap.Add(indices[0], bcn.Scopes[0]);
                    argSel = reducer.MapScopes(argSel, scopeMap);

                    return BndCallNode.Create(bcn.Oper, call.Type, bcn.Args.Add(argSel),
                        bcn.Scopes, bcn.Indices, dirs: default, bcn.Names);
                }
            }

            return call;
        }

        // Combine any of the sequence args that are equivalent.
        // REVIEW: Is there a better way than O(n*n)?
        HashSet<int> toss = null;
        Dictionary<ArgScope, ArgScope> map = null;
        for (int i = 1; i < cseq; i++)
        {
            var seqCur = args[i];
            Validation.Assert(seqCur.Type.IsSequence);
            for (int j = 0; j < i; j++)
            {
                if (toss != null && toss.Contains(j))
                    continue;
                var seqPre = args[j];
                Validation.Assert(seqPre.Type.IsSequence);
                if (seqPre.Equivalent(seqCur))
                {
                    Util.Add(ref toss, i);
                    Util.Add(ref map, scopes[i], scopes[j]);
                    break;
                }
            }
        }

        Validation.Assert((toss != null) == (map != null));
        if (toss == null)
            return call;

        Validation.Assert(toss.Count > 0);
        Validation.Assert(toss.Count == map.Count);

        BoundNode pred = null;
        if (dir != Directive.None)
        {
            pred = reducer.MapScopes(args[carg - 2], map);
            pred = reducer.Reduce(pred);
        }

        var sel = reducer.MapScopes(args[args.Length - 1], map);
        sel = reducer.Reduce(sel);

        int num = carg - toss.Count;
        Validation.Assert(num >= 1 + ctail);
        bldrArgs = ArgTuple.CreateBuilder(num, init: true);
        bldrScopes = ScopeTuple.CreateBuilder(num - ctail, init: true);
        ivDst = 0;
        for (int ivSrc = 0; ivSrc < scopes.Length; ivSrc++)
        {
            if (!toss.Contains(ivSrc))
            {
                bldrArgs[ivDst] = args[ivSrc];
                bldrScopes[ivDst] = scopes[ivSrc];
                ivDst++;
            }
        }
        Validation.Assert(ivDst == bldrScopes.Count);
        Validation.Assert((pred != null) == (dir != Directive.None));
        dirs = call.Directives;
        if (pred != null)
        {
            bldrArgs[ivDst++] = pred;
            Validation.Assert(IsSupportedDirective(dir));
            if (dirs.Length != ivDst + 1)
                dirs = MakeDirectives(ivDst + 1, dir);
        }
        else
            dirs = default;

        bldrArgs[ivDst++] = sel;
        Validation.Assert(ivDst == bldrArgs.Count);

        var res = BndCallNode.Create(this, call.Type, bldrArgs.ToImmutable(), bldrScopes.ToImmutable(), indices, dirs, default);
        // Eliminating dups may require another pass.
        return reducer.Reduce(res);
    }

    private static Immutable.Array<Directive> MakeDirectives(int carg, Directive dir)
    {
        Validation.Assert(carg >= 3);
        Validation.Assert(IsSupportedDirective(dir));
        var bldrDirs = DirTuple.CreateBuilder(carg, init: true);
        bldrDirs[carg - 2] = dir;
        Validation.Assert(bldrDirs[carg - 2] == dir);
        return bldrDirs.ToImmutable();
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        if (GetFilterKind(call) == SeqFilterKind.If)
            return true;
        return false;
    }
}
