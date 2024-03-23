// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

/// <summary>
/// Function to do cross (Cartesian) product "join", with predicate, and optional outer selectors.
/// Example: CrossJoin(x: Range(10), y: Range(20), Mod(x + y, 2) == 0, { x, y, product: x * y })
/// </summary>
public sealed partial class CrossJoinFunc : RexlOper
{
    public static readonly CrossJoinFunc Instance = new CrossJoinFunc();

    private CrossJoinFunc()
        : base(isFunc: true, new DName("CrossJoin"), 4, 6)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));

        // The first two args (0 and 1) are the left and right sequences. These define seq item scopes.
        // * Arg 2 is the predicate used to determine a match. Both left and right items are in scope.
        // * Arg 3 is the match selector used to produce the output when there is a match. Both left and right are in scope.
        // When they are provided:
        // * Arg 4 is the left  selector, for when a left  value has no matching right items. Only the left  is in scope.
        // * Arg 5 is the right selector, for when a right value has no matching left  items. Only the right is in scope.
        return ArgTraitsJoin.Create(this, carg, indexed: true, 0x3, 0x3, 0x1, 0x2);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot != 2)
            return false;
        if (dir != Directive.If)
            return false;
        return true;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        const int cseq = 2;
        int carg = types.Count;
        for (int i = 0; i < cseq; i++)
            EnsureTypeSeq(types, i);
        types[cseq] = DType.BitReq;

        DType typeRes = types[cseq + 1];
        for (int iarg = cseq + 2; iarg < carg; iarg++)
            typeRes = DType.GetSuperType(typeRes, types[iarg], DType.UseUnionDefault);
        for (int iarg = cseq + 1; iarg < carg; iarg++)
            types[iarg] = typeRes;

        return (typeRes.ToSequence(), types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!args[0].Type.IsSequence)
            return false;
        if (!args[1].Type.IsSequence)
            return false;
        if (args[2].Type != DType.BitReq)
            return false;
        var typeRes = args[3].Type;
        for (int slot = 4; slot < args.Length; slot++)
        {
            if (args[slot].Type != typeRes)
                return false;
        }
        if (type != typeRes.ToSequence())
            return false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        int len = call.Args.Length;
        Validation.Assert(4 <= len & len <= 6);
        var (m, M) = call.Args[0].GetItemCountRange();
        var (n, N) = call.Args[1].GetItemCountRange();
        Validation.AssertIndexInclusive(m, M);
        Validation.AssertIndexInclusive(n, N);

        bool allSame = call.Args[2].TryGetBool(out bool allTrue);
        if (allSame && !allTrue)
        {
            // No matches, so only "outer" can contribute.
            switch (len)
            {
            case 4: return (0, 0);
            case 5: return (m, M);
            }
            return (NumUtil.AddCounts(m, n), NumUtil.AddCounts(M, N));
        }

        // Maximum number from matches.
        long P = NumUtil.MulCounts(M, N);

        if (allTrue)
        {
            // Any comparisons are matches, but if either is empty, there are no comparisons,
            // so "outer" can still contribute.
            Validation.Assert(allSame);
            // Minimum number from matches.
            long p = NumUtil.MulCounts(m, n);
            switch (len)
            {
            case 4: return (p, P);
            case 5: return (Math.Max(m, p), Math.Max(M, P));
            }
            return (Math.Max(Math.Max(m, n), p), Math.Max(Math.Max(M, N), P));
        }

        switch (len)
        {
        case 4: return (0, P);
        case 5: return (m, Math.Max(M, P));
        }

        // The maximum number from outer.
        long S = NumUtil.AddCounts(M, N);

        // If one side is empty, the min is from outer, so is the other.
        // Else if one side has 1 item, the min is from matching, so is the other.
        // Else the min is from outer, so is the sum.
        long min = m <= 1 || n <= 1 ? Math.Max(m, n) : NumUtil.AddCounts(m, n);

        return (min, Math.Max(S, P));
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        Validation.Assert(call.Scopes.Length == 2);
        Validation.Assert(call.Indices.Length == 2 | call.Indices.Length == 0);

        // See if we can reduce to KeyJoin.
        var cond = call.Args[2];

        if (cond.HasVolatile)
            return call;

        CompareOp op;
        if (cond is not BndCompareNode bcn || bcn.Ops.Length != 1 || !(op = bcn.Ops[0]).IsEqualPos)
            return call;

        // See if the operands are "separate".
        var v0 = bcn.Args[0];
        var v1 = bcn.Args[1];
        var s0 = call.Scopes[0];
        var s1 = call.Scopes[1];
        var i0 = call.Indices.GetItemOrDefault(0);
        var i1 = call.Indices.GetItemOrDefault(1);

        if (ScopeCounter.Any(v0, s0) || i0 is not null && ScopeCounter.Any(v0, i0))
        {
            if (ScopeCounter.Any(v0, s1) || i1 is not null && ScopeCounter.Any(v0, i1))
                return call;
            if (ScopeCounter.Any(v1, s0) || i0 is not null && ScopeCounter.Any(v1, i0))
                return call;
        }
        else if (ScopeCounter.Any(v1, s0) || i0 is not null && ScopeCounter.Any(v1, i0))
        {
            if (ScopeCounter.Any(v1, s1) || i1 is not null && ScopeCounter.Any(v1, i1))
                return call;
            Util.Swap(ref v0, ref v1);
        }

        if (bcn.HasOpt)
        {
            if (!v0.Type.IsOpt)
                v0 = BndCastOptNode.Create(v0);
            if (!v1.Type.IsOpt)
                v1 = BndCastOptNode.Create(v1);
        }
        Validation.Assert(v0.Type == v1.Type);

        var bldr = call.Args.ToBuilder();
        bldr[2] = v0;
        bldr.Insert(3, v1);
        var args = bldr.ToImmutable();

        var mods = op.Mods;
        Directive dir;
        if (mods.IsStrict())
            dir = mods.IsCi() ? Directive.Ci : Directive.None;
        else
            dir = mods.IsCi() ? Directive.EqCi : Directive.Eq;
        Immutable.Array<Directive> dirs = default;
        if (dir != Directive.None)
        {
            var bldrDirs = Immutable.Array<Directive>.CreateBuilder(args.Length, init: true);
            bldrDirs[2] = dir;
            dirs = bldrDirs.ToImmutable();
        }

        return BndCallNode.Create(KeyJoinFunc.Instance, call.Type, args, call.Scopes, call.Indices, dirs, default);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}

/// <summary>
/// Function to do key-based "join", with key expressions, matching selector, and optional outer selectors.
/// Example: KeyJoin(x: Range(10), y: Range(20), x, 9 - y, { x, y, product: x * y })
/// </summary>
public sealed partial class KeyJoinFunc : RexlOper
{
    public static readonly KeyJoinFunc Instance = new KeyJoinFunc();

    private KeyJoinFunc()
        : base(isFunc: true, new DName("KeyJoin"), 5, 7)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));

        // The first two args (0 and 1) are the left and right sequences. These define seq item scopes.
        // * Arg 2 is the key for the left.  Only the left  is in scope.
        // * Arg 3 is the key for the right. Only the right is in scope.
        // * Arg 4 is the match selector used to produce the output when there is a match. Both left and right are in scope.
        // When they are provided:
        // * Arg 5 is the left  selector, for when a left  value has no matching right items. Only the left  is in scope.
        // * Arg 6 is the right selector, for when a right value has no matching left  items. Only the right is in scope.
        return ArgTraitsJoin.Create(this, carg, indexed: true, 0x1, 0x2, 0x3, 0x1, 0x2);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        // The key slots allow an optional [key] or ci directive.
        switch (dir)
        {
        case Directive.Ci:
        case Directive.Eq:
        case Directive.EqCi:
        case Directive.Key:
            return 2 <= slot && slot <= 3;
        default:
            return false;
        }
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var args = info.Args;

        // Builder for the arg types.
        var bldr = Immutable.Array.CreateBuilder<DType>(args.Length, init: true);

        int cseq = 2;
        int carg = args.Length;
        int iarg = 0;
        for (; iarg < cseq; iarg++)
        {
            bldr[iarg] = args[iarg].Type;
            EnsureTypeSeq(bldr, iarg);
        }

        // Determine the key type.
        // REVIEW: Should records with groupable fields be groupable?
        var typeKey0 = args[iarg].Type;
        var typeKey1 = args[iarg + 1].Type;
        var typeKey = DType.GetSuperType(typeKey0, typeKey1, union: DType.UseUnionDefault);
        bool err = false;
        if (!typeKey.IsEquatable)
        {
            err = true;
            if (typeKey.RootType.IsEquatable)
                typeKey = typeKey.RootType;
            else if (typeKey0.RootType.IsEquatable)
                typeKey = typeKey0.RootType;
            else if (typeKey1.RootType.IsEquatable)
                typeKey = typeKey1.RootType;
            else
            {
                typeKey = DType.I8Req;
                if (info.ParseArity > iarg)
                {
                    info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(iarg),
                        ErrorStrings.ErrNotJoinableType_Type, typeKey0));
                }
                if (info.ParseArity > iarg + 1)
                {
                    info.PostDiagnostic(RexlDiagnostic.Error(info.GetParseArg(iarg + 1),
                        ErrorStrings.ErrNotJoinableType_Type, typeKey1));
                }
            }
        }
        bldr[iarg++] = typeKey;
        bldr[iarg++] = typeKey;

        var dirs = info.Dirs;
        if (!err && !typeKey.HasText && !dirs.IsDefaultOrEmpty)
        {
            ExprNode parg = null;
            Directive dir;
            if ((dir = info.Dirs.GetItemOrDefault(2)).IsCi())
                parg = info.GetParseArg(2);
            else if ((dir = info.Dirs.GetItemOrDefault(3)).IsCi())
                parg = info.GetParseArg(3);

            if (parg != null)
            {
                RexlDiagnostic diag;
                if (parg is DirectiveNode dn)
                {
                    diag = RexlDiagnostic.WarningGuess(dn.DirToken, dn, ErrorStrings.WrnCmpCi_Type,
                        dir.IsEq() ? "[=]" : "[key]", dn.DirToken.Range, typeKey);
                }
                else
                    diag = RexlDiagnostic.Warning(parg, ErrorStrings.WrnCmpCi_Type, typeKey);
                info.PostDiagnostic(diag);
            }
        }

        DType typeRes = args[iarg].Type;
        for (int iargTmp = iarg + 1; iargTmp < carg; iargTmp++)
            typeRes = DType.GetSuperType(typeRes, args[iargTmp].Type, DType.UseUnionDefault);
        for (; iarg < carg; iarg++)
            bldr[iarg] = typeRes;

        return (typeRes.ToSequence(), bldr.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        if (!args[0].Type.IsSequence)
            return false;
        if (!args[1].Type.IsSequence)
            return false;
        var typeKey = args[2].Type;
        if (!typeKey.IsEquatable)
            return false;
        if (args[3].Type != typeKey)
            return false;
        var typeRes = args[4].Type;
        for (int slot = 5; slot < args.Length; slot++)
        {
            if (args[slot].Type != typeRes)
                return false;
        }
        if (type != typeRes.ToSequence())
            return false;
        if (args[2].HasVolatile || args[3].HasVolatile)
            full = false;
        return true;
    }

    public override bool HasBadVolatile(BndCallNode call, out int slot)
    {
        Validation.Assert(IsValidCall(call));
        if (call.Args[2].HasVolatile)
        {
            slot = 2;
            return true;
        }
        if (call.Args[3].HasVolatile)
        {
            slot = 3;
            return true;
        }

        slot = -1;
        return false;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        int len = call.Args.Length;
        Validation.Assert(5 <= len & len <= 7);
        var (m, M) = call.Args[0].GetItemCountRange();
        var (n, N) = call.Args[1].GetItemCountRange();
        Validation.AssertIndexInclusive(m, M);
        Validation.AssertIndexInclusive(n, N);

        // Maximum number from matches.
        long P = NumUtil.MulCounts(M, N);

        switch (len)
        {
        case 5: return (0, P);
        case 6: return (m, Math.Max(M, P));
        }

        // The maximum number from outer.
        long S = NumUtil.AddCounts(M, N);

        // If one side is empty, the min is from outer, so is the other.
        // Else if one side has 1 item, the min is from matching, so is the other.
        // Else the min is from outer, so is the sum.
        long min = m <= 1 || n <= 1 ? Math.Max(m, n) : NumUtil.AddCounts(m, n);

        return (min, Math.Max(S, P));
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return true;
    }
}
