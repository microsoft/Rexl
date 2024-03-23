// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

using ArgTuple = Immutable.Array<BoundNode>;
using ScopeTuple = Immutable.Array<ArgScope>;
using TypeTuple = Immutable.Array<DType>;

public sealed partial class TensorForEachFunc : TensorFunc
{
    public static readonly TensorForEachFunc Eager = new TensorForEachFunc("ForEach", eager: true);
    public static readonly TensorForEachFunc Lazy = new TensorForEachFunc("ForEachLazy", eager: false);

    public bool IsEager { get; }

    private TensorForEachFunc(string name, bool eager)
        : base(name, 2, int.MaxValue)
    {
        IsEager = eager;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));

        var maskTen = BitSet.GetMask(carg - 1);
        var maskSel = BitSet.GetMask(carg - 1, carg);
        return ArgTraits.CreateGeneral(
            this, carg,
            maskLiftOpt: maskTen,
            scopeKind: ScopeKind.TenItem, maskScope: maskTen, maskNested: maskSel, maskLazySeq: maskSel);
    }

    protected override (DType, TypeTuple) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var bldr = info.GetArgTypes();
        int cten = bldr.Count - 1;
        for (int i = 0; i < cten; i++)
            EnsureTypeTen(bldr, i);
        var types = bldr.ToImmutable();
        var typeRes = GetReturnType(types);
        return (typeRes, types);
    }

    private DType GetReturnType(TypeTuple types)
    {
        Validation.Assert(types.Length >= 2);
        int cten = types.Length - 1;
        var typeSel = types[cten];

        int rank = types[0].GetTensorRank();
        for (int i = 1; i < cten; i++)
        {
            int rankCur = types[i].GetTensorRank();
            if (rank < rankCur)
                rank = rankCur;
        }
        return typeSel.ToTensor(false, rank);
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int cten = args.Length - 1;
        int rank = 0;
        for (int slot = 0; slot < cten; slot++)
        {
            var typeTen = args[slot].Type;
            if (!typeTen.IsTensorReq)
                return false;
            int rankCur = typeTen.GetTensorRank();
            if (rank < rankCur)
                rank = rankCur;
        }
        if (type.TensorRank != rank)
            return false;
        if (type.GetTensorItemType() != args[cten].Type)
            return false;
        return true;
    }

    /// <summary>
    /// Flatten nested invocations of <c>Tensor.ForEach</c>. In particular <c>ForEach(x:A, y:ForEach(z:B, sel0), sel1)</c>
    /// flattens to <c>ForEach(x:A, z:B, With(y:sel0, sel1))</c>. Note that the <c>With</c> invocation may be
    /// eliminated by the reducer.
    /// </summary>
    private BndCallNode Flatten(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        ArgTuple.Builder bldr = null;
        ScopeTuple.Builder bldrScopes = null;
        List<(BoundNode arg, ArgScope scope)> withs = null;
        var args = call.Args;
        var scopes = call.Scopes;
        int cten = scopes.Length;
        Validation.Assert(cten == args.Length - 1);
        for (int ivSrc = 0; ivSrc < cten; ivSrc++)
        {
            var arg = args[ivSrc];
            Validation.Assert(arg.Type.IsTensorReq);
            var scope = scopes[ivSrc];
            Validation.Assert(scope.Type == arg.Type.GetTensorItemType());

            // Strip off any ref cast (not needed).
            if (arg is BndCastRefNode bcrn)
            {
                Validation.Assert(bcrn.Child.Type.IsTensorReq);
                Validation.Assert(scope.Type == bcrn.Child.Type.GetTensorItemType());
                arg = bcrn.Child;
                Validation.Assert(!(arg is BndCastRefNode));
            }
            if (arg is BndCallNode bcn && bcn.Oper is TensorForEachFunc)
            {
                if (bldr == null)
                {
                    bldr = args.ToBuilder();
                    bldr.RemoveTail(ivSrc);
                }
                if (bldrScopes == null)
                {
                    bldrScopes = scopes.ToBuilder();
                    bldrScopes.RemoveTail(ivSrc);
                    withs = new List<(BoundNode arg, ArgScope scope)>();
                }

                int num = bcn.Scopes.Length;
                Validation.Assert(num > 0);
                Validation.Assert(num == bcn.Args.Length - 1);
                var argWith = bcn.Args[num];

                for (int i = 0; i < num; i++)
                {
                    Validation.Assert(bcn.Scopes[i].Kind == ScopeKind.TenItem);
                    bldr.Add(bcn.Args[i]);
                    bldrScopes.Add(bcn.Scopes[i]);
                }

                Validation.Assert(argWith.Type == scope.Type);
                withs.Add((argWith, scope));
                continue;
            }

            if (bldr != null)
            {
                bldr.Add(arg);
                if (bldrScopes != null)
                    bldrScopes.Add(scope);
            }
            else if (arg != args[ivSrc])
            {
                bldr = args.ToBuilder();
                bldr.RemoveTail(ivSrc);
                bldr.Add(arg);
            }
        }

        if (bldr == null)
        {
            Validation.Assert(bldrScopes == null);
            Validation.Assert(withs == null);
            return call;
        }

        var sel = args[cten];
        if (bldrScopes == null)
        {
            Validation.Assert(withs == null);
            Validation.Assert(bldr.Count == cten);
        }
        else
        {
            Validation.Assert(bldr.Count == bldrScopes.Count);
            Validation.Assert(withs != null && withs.Count > 0);

            scopes = bldrScopes.ToImmutable();

            // Construct the new selector as an invocation of With(...).
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

            // Create and reduce the new selector.
            bldrWith[withs.Count] = reducer.MapScopes(sel, map);
            sel = BndCallNode.Create(WithFunc.With, sel.Type, bldrWith.ToImmutable(), bldrScopesWith.ToImmutable());
            sel = reducer.Reduce(sel);
        }

        bldr.Add(sel);
        args = bldr.ToImmutable();

#if DEBUG
        var typeOld = call.Type;
        var typeNew = GetReturnType(args.Select(a => a.Type).ToImmutableArray());
        // The new type might be more "refined"? We use the old type below when constructing the new invocation.
        // REVIEW: Ideally, reduction should support type refinement. This will be a big work item.
        if (typeOld != typeNew)
        {
            Validation.Assert(typeOld.GetTensorItemType() == typeNew.GetTensorItemType());
            Validation.Assert(typeOld.Accepts(typeNew, DType.UseUnionDefault));
        }
#endif
        return BndCallNode.Create(this, call.Type, args, scopes);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode bnd)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(bnd));

        // First flatten.
        var call = Flatten(reducer, bnd);
        Validation.Assert(call.Type == bnd.Type);
        Validation.Assert(IsValidCall(call));

        return ReduceCore(this, reducer, call);
    }

    private static BoundNode ReduceCore(TensorForEachFunc func, IReducer reducer, BndCallNode call)
    {
        var args = call.Args;
        var scopes = call.Scopes;
        int carg = args.Length;
        int cten = scopes.Length;
        Validation.Assert(carg == cten + 1);
        Validation.Assert(cten > 0);

        if (cten == 1)
        {
            if (args[carg - 1] is BndScopeRefNode bsr && bsr.Scope == scopes[0])
            {
                // The ForEach is a no-op map.
                Validation.Assert(args[0].Type == call.Type);
                return args[0];
            }
            return call;
        }

        ArgTuple.Builder bldrArgs;
        ScopeTuple.Builder bldrScopes;
        int ivDst;

        // Combine any of the tensor args that are equivalent.
        // REVIEW: Is there a better way than O(n*n)?
        HashSet<int> toss = null;
        Dictionary<ArgScope, ArgScope> map = null;
        for (int i = 1; i < cten; i++)
        {
            var tenCur = args[i];
            Validation.Assert(tenCur.Type.IsTensorReq);
            for (int j = 0; j < i; j++)
            {
                if (toss != null && toss.Contains(j))
                    continue;
                var tenPre = args[j];
                Validation.Assert(tenPre.Type.IsTensorReq);
                if (tenPre.Equivalent(tenCur))
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

        var sel = reducer.MapScopes(args[args.Length - 1], map);
        sel = reducer.Reduce(sel);

        int num = carg - toss.Count;
        Validation.Assert(num >= 2);
        bldrArgs = ArgTuple.CreateBuilder(num, init: true);
        bldrScopes = ScopeTuple.CreateBuilder(num - 1, init: true);
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

        bldrArgs[ivDst++] = sel;
        Validation.Assert(ivDst == bldrArgs.Count);

        var res = BndCallNode.Create(func, call.Type, bldrArgs.ToImmutable(), bldrScopes.ToImmutable());
        // Eliminating dups may require another pass.
        return reducer.Reduce(res);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}
