// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Takes/skips items if/while the predicate is satisfied.
/// </summary>
public sealed class TakeDropCondFunc : RexlOper
{
    public static readonly TakeDropCondFunc TakeIf = new TakeDropCondFunc("TakeIf", take: true, prefix: false);
    public static readonly TakeDropCondFunc DropIf = new TakeDropCondFunc("DropIf", take: false, prefix: false);
    public static readonly TakeDropCondFunc TakeWhile = new TakeDropCondFunc("TakeWhile", take: true, prefix: true);
    public static readonly TakeDropCondFunc DropWhile = new TakeDropCondFunc("DropWhile", take: false, prefix: true);

    private readonly bool _take;
    private readonly bool _prefix;

    private TakeDropCondFunc(string name, bool take, bool prefix)
        : base(isFunc: true, new DName(name), 2, 2)
    {
        _take = take;
        _prefix = prefix;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(ArityMin == 2 && ArityMax == 2);
        Validation.BugCheckParam(SupportsArity(carg), nameof(carg));
        return ArgTraitsZip.Create(this, indexed: true, eager: false, carg, seqCount: 1);
    }

    protected override bool SupportsDirectiveCore(ArgTraits traits, int slot, Directive dir)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.AssertIndex(slot, traits.SlotCount);
        Validation.Assert(dir != Directive.None);

        if (slot != 1)
            return false;
        switch (dir)
        {
        case Directive.If:
            return !_prefix;
        case Directive.While:
            return _prefix;
        }
        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));
        Validation.Assert(info.Arity == 2);

        var type = info.Args[0].Type;
        EnsureTypeSeq(ref type);
        return (type, Immutable.Array.Create(type, DType.BitReq));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        var args = call.Args;
        if (args[0].Type != type)
            return false;
        if (args[1].Type != DType.BitReq)
            return false;
        // CodeGen should not see one of these. They all should be reduced to a Take or Drop call.
        full = false;
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        var (min, max) = call.Args[0].GetItemCountRange();
        if (call.Args[1].TryGetBool(out bool ok))
        {
            if (ok == _take)
                return (min, max);
            return (0, 0);
        }

        // We don't know the number to keep/drop. We only know that the result is a subsequence.
        return (0, max);
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg < call.Scopes.Length);
        return PullWithFlags.Both;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args;
        if (args[1].TryGetBool(out bool res))
        {
            // Keep all or nothing depending on whether res matches _take.
            if (res == _take)
                return args[0];
            return BndSequenceNode.CreateEmpty(args[0].Type);
        }
        return reducer.Reduce(BndCallNode.Create(_take ? TakeDropFunc.Take : TakeDropFunc.Drop,
            call.Type, args, call.Scopes, call.Indices,
            Immutable.Array<Directive>.Create(Directive.None, _prefix ? Directive.While : Directive.If), default));
    }
}

/// <summary>
/// Produces the first item in the sequence that satisfies the optional predicate or a default value,
/// which may be explicit or implicit.
/// </summary>
public sealed partial class TakeOneFunc : RexlOper
{
    public static readonly TakeOneFunc TakeOne = new TakeOneFunc(defNull: false);
    public static readonly TakeOneFunc First = new TakeOneFunc(defNull: true);

    private readonly bool _defNull;

    private TakeOneFunc(bool defNull)
        : base(isFunc: true, new DName(defNull ? "First" : "TakeOne"), 1, 3)
    {
        _defNull = defNull;
    }

    protected override ArgTraits GetArgTraitsCore(int carg, Immutable.Array<DName> names, BitSet implicitNames, Immutable.Array<Directive> dirs)
    {
        Validation.Assert(SupportsArity(carg));
        Validation.Assert(names.IsDefault || names.Length == carg);
        Validation.Assert(names.IsDefault && implicitNames.IsEmpty || implicitNames.SlotMax < names.Length);
        Validation.Assert(dirs.IsDefault || dirs.Length == carg);
        switch (carg)
        {
        case 1:
            return ArgTraitsSimple.Create(this, eager: true, carg);
        case 2:
            if (dirs.GetItemOrDefault(1) == Directive.Else)
                return ArgTraitsSimple.Create(this, eager: true, carg);
            return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: 1);
        default:
            Validation.Assert(carg == 3);
            return ArgTraitsZip.Create(this, indexed: true, eager: true, carg, seqCount: 1, nonPost: 1);
        }
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

        switch (dir)
        {
        case Directive.If:
            return slot == 1 && traits.ScopeCount == 1;
        case Directive.Else:
            return slot == 1 + traits.ScopeCount;
        }
        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        EnsureTypeSeq(types, 0);
        var typeItem = types[0].ItemTypeOrThis;

        int carg = types.Count;
        int iargPred = -1;
        int iargElse = -1;
        if (carg == 3)
        {
            iargPred = 1;
            iargElse = 2;
        }
        else if (carg == 2)
        {
            var dir = info.Dirs.GetItemOrDefault(1);
            if (dir != Directive.Else)
                iargPred = 1;
            else
                iargElse = 1;
        }

        if (iargPred > 0)
            types[iargPred] = DType.BitReq;

        var typeRet = typeItem;
        if (iargElse > 0)
        {
            // REVIEW: Use super-type instead?
            if (types[iargElse].IsOpt && !typeRet.IsOpt)
                typeRet = typeRet.ToOpt();
            types[iargElse] = typeRet;
        }
        else if (_defNull)
            typeRet = typeRet.ToOpt();
        Validation.Assert(typeRet == typeItem | typeRet == typeItem.ToOpt());

        return (typeRet, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int carg = args.Length;
        var typeSeq = args[0].Type;
        if (!typeSeq.IsSequence)
            return false;
        var typeItem = typeSeq.ItemTypeOrThis;
        if (typeItem != type && typeItem.ToOpt() != type)
            return false;
        if (carg >= 2)
        {
            int iarg = 1;
            if (carg > 2 || call.GetDirective(1) != Directive.Else)
            {
                if (args[iarg++].Type != DType.BitReq)
                    return false;
            }
            if (iarg < carg && args[iarg++].Type != type)
                return false;
            Validation.Assert(iarg == carg);
        }
        if (_defNull)
            full = false;
        return true;
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));

        if (iarg == 0)
        {
            BoundNode argElse = null;
            if (call.Args.Length == 3)
                argElse = call.Args[2];
            else if (call.Args.Length == 2 && call.GetDirective(1) == Directive.Else)
                argElse = call.Args[1];
            if (argElse is not null && argElse.IsKnownNull)
                return PullWithFlags.Both;
            return PullWithFlags.With;
        }

        Validation.Assert(iarg == 2 | iarg == 1 & call.GetDirective(1) == Directive.Else);
        return PullWithFlags.With;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));
        var args = call.Args;
        var carg = args.Length;

        int iargPred = -1;
        int iargElse = -1;
        if (carg == 3)
        {
            iargPred = 1;
            iargElse = 2;
        }
        else if (carg == 2)
        {
            var dir = call.GetDirective(1);
            if (dir != Directive.Else)
                iargPred = 1;
            else
                iargElse = 1;
        }

        var (_, max) = args[0].GetItemCountRange();
        if (max == 0)
            return iargElse > 0 ? args[iargElse] : BndDefaultNode.Create(call.Type);

        if (iargPred > 0)
        {
            var argPred = args[iargPred];
            if (argPred.TryGetBool(out bool reducedPred))
            {
                if (reducedPred)
                    goto LDropPred;
                goto LDefault;
            }

            Validation.Assert(call.Indices.Length == 1);
            if (argPred is BndCompareNode bndCmp && call.Indices[0] != null
                && bndCmp.TryGetConstIndexCheck(call.Indices[0], out var op, out long value))
            {
                // REVIEW: Some of these cases (and their negations) are always
                // true/false in the general case and would be better handled as reductions.
                var (root, mods) = op.GetParts();
                Validation.Assert(!mods.IsNot() || root == CompareRoot.Equal);
                switch (root)
                {
                case CompareRoot.Less:
                    if (value <= 0)
                        goto LDefault;
                    goto LDropPred;

                case CompareRoot.LessEqual:
                    if (value < 0)
                        goto LDefault;
                    goto LDropPred;

                case CompareRoot.Equal:
                    if (mods.IsNot())
                    {
                        if (value != 0)
                            goto LDropPred;
                        // For # != 0, we can TakeAt 1.
                        value = 1;
                        break;
                    }
                    else
                    {
                        if (value == 0)
                            goto LDropPred;
                        if (!Validation.IsValidIndex(value, max))
                            goto LDefault;
                        // For # = C, we can TakeAt C.
                    }
                    break;

                case CompareRoot.GreaterEqual:
                    if (value >= max)
                        goto LDefault;
                    if (value <= 0)
                        goto LDropPred;
                    // For # >= C, we can TakeAt C.
                    break;

                case CompareRoot.Greater:
                    if (value >= max - 1)
                    {
                        Validation.Coverage(value == long.MaxValue ? 1 : 0);
                        goto LDefault;
                    }
                    if (value < 0)
                        goto LDropPred;
                    // For # > C, we can TakeAt C + 1.
                    Validation.Assert(value < long.MaxValue);
                    value++;
                    break;

                default:
                    Validation.Assert(false);
                    goto LBase;
                }

                var argsTakeAt = args.SetItem(iargPred, BndIntNode.CreateI8(value));
                return reducer.Reduce(BndCallNode.Create(TakeAtFunc.Instance, call.Type, argsTakeAt));
            }
        }
        else if (args[0] is BndSequenceNode bndSeq)
        {
            // Empty case should have already been handled.
            Validation.Assert(bndSeq.Items.Length > 0);
            var bndItem = bndSeq.Items[0];
            if (bndItem.Type != call.Type)
            {
                Validation.Assert(bndItem.Type.ToOpt() == call.Type);
                return BndCastOptNode.Create(bndItem);
            }
            return bndItem;
        }

    LBase:
        var dirs = call.Directives;
        if (iargPred > 0)
        {
            Validation.Assert(iargPred == 1);
            dirs = default;
        }
        if (_defNull)
        {
            Validation.Assert(call.Type.IsOpt | iargElse > 0);
            if (iargElse < 0 && !args[0].Type.ItemTypeOrThis.IsOpt)
            {
                args = args.Add(BndNullNode.Create(call.Type));
                if (iargPred < 0)
                {
                    Validation.Assert(args.Length == 2);
                    Validation.Assert(dirs.IsDefaultOrEmpty);
                    dirs = Immutable.Array<Directive>.Create(default, Directive.Else);
                }
            }
        }
        if (_defNull || !dirs.AreIdentical(call.Directives) ||
            !args.AreIdentical(call.Args) || !call.Names.IsDefaultOrEmpty)
        {
            return reducer.Reduce(BndCallNode.Create(TakeOne, call.Type, args, call.Scopes, call.Indices, dirs, default));
        }
        return base.ReduceCore(reducer, call);
    LDefault:
        return iargElse > 0 ? args[iargElse] : BndDefaultNode.Create(call.Type);
    LDropPred:
        Validation.Assert(iargPred == 1);
        // If there is an "else" arg, it needs a directive.
        if (iargElse < 0)
        {
            if (_defNull && !args[0].Type.ItemTypeOrThis.IsOpt)
            {
                Validation.Assert(call.Type.IsOpt);
                args = args.SetItem(1, BndNullNode.Create(call.Type));
                dirs = Immutable.Array<Directive>.Create(default, Directive.Else);
            }
            else
            {
                args = args.RemoveTail(1);
                dirs = default;
            }
            return reducer.Reduce(BndCallNode.Create(TakeOne, call.Type, args, dirs));
        }
        return reducer.Reduce(BndCallNode.Create(TakeOne, call.Type, args.RemoveAt(iargPred), Immutable.Array<ArgScope>.Empty,
            Immutable.Array<ArgScope>.Empty, Immutable.Array<Directive>.Create(Directive.None, Directive.Else), default));
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        return call.Scopes.Length > 0;
    }
}

/// <summary>
/// Drops the first item in the sequence that satisfies the optional predicate.
/// </summary>
public sealed class DropOneFunc : RexlOper
{
    public static readonly DropOneFunc Instance = new DropOneFunc();

    private DropOneFunc()
        : base(isFunc: true, new DName("DropOne"), 1, 2)
    {
    }

    protected override ArgTraits GetArgTraitsCore(int carg, Immutable.Array<DName> names, BitSet implicitNames, Immutable.Array<Directive> dirs)
    {
        Validation.Assert(SupportsArity(carg));
        Validation.Assert(names.IsDefault || names.Length == carg);
        Validation.Assert(names.IsDefault && implicitNames.IsEmpty || implicitNames.SlotMax < names.Length);
        Validation.Assert(dirs.IsDefault || dirs.Length == carg);
        if (carg == 1)
            return ArgTraitsSimple.Create(this, eager: false, carg);
        Validation.Assert(carg == 2);
        return ArgTraitsZip.Create(this, indexed: true, eager: false, carg, seqCount: 1);
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

        if (slot != 1)
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
        EnsureTypeSeq(types, 0);

        int carg = types.Count;
        if (carg == 2)
            types[1] = DType.BitReq;

        return (types[0], types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        var args = call.Args;
        int carg = args.Length;
        var typeSeq = args[0].Type;
        if (!typeSeq.IsSequence)
            return false;
        if (typeSeq != type)
            return false;
        if (carg > 1)
        {
            Validation.Assert(carg == 2);
            Validation.Assert(call.Traits.IsNested(1));
            if (args[1].Type != DType.BitReq)
                return false;
        }

        // Should always be reduced to Drop.
        full = false;
        return true;
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));
        Validation.Assert(iarg == 0);
        return PullWithFlags.Both;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        var args = call.Args.Insert(1, BndIntNode.CreateI8(1));
        var dirs = call.Directives;
        if (!dirs.IsDefault)
            dirs = dirs.Insert(1, Directive.None);
        return reducer.Reduce(BndCallNode.Create(TakeDropFunc.Drop, call.Type, args, call.Scopes, call.Indices, dirs, default));
    }
}

/// <summary>
/// Takes/skips the first N items in the sequence that satisfy the optional predicate.
/// </summary>
public sealed partial class TakeDropFunc : RexlOper
{
    public static readonly TakeDropFunc Take = new TakeDropFunc(take: true);
    public static readonly TakeDropFunc Drop = new TakeDropFunc(take: false);

    /// <summary>
    /// Whether this is <c>Take</c> vs <c>Drop</c>.
    /// </summary>
    public bool IsTake { get; }

    private TakeDropFunc(bool take)
        : base(isFunc: true, new DName(take ? "Take" : "Drop"), 2, 3)
    {
        IsTake = take;
    }

    protected override ArgTraits GetArgTraitsCore(int carg, Immutable.Array<DName> names, BitSet implicitNames, Immutable.Array<Directive> dirs)
    {
        Validation.Assert(SupportsArity(carg));
        Validation.Assert(names.IsDefault || names.Length == carg);
        Validation.Assert(names.IsDefault && implicitNames.IsEmpty || implicitNames.SlotMax < names.Length);
        Validation.Assert(dirs.IsDefault || dirs.Length == carg);


        if (carg == 2)
        {
            var dir = dirs.GetItemOrDefault(1);
            if (dir != Directive.If && dir != Directive.While)
                return ArgTraitsSimple.Create(this, eager: false, 2);
        }

        // Only the predicate is nested, not the count.
        return ArgTraitsZip.Create(this, indexed: true, eager: false, carg, seqCount: 1, nonPre: carg - 2);
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

        if (slot != traits.SlotCount - 1)
            return false;
        if (dir == Directive.If || dir == Directive.While)
            return true;
        return false;
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        Immutable.Array<DType>.Builder types = info.GetArgTypes();
        Validation.AssertValue(types);

        EnsureTypeSeq(types, 0);
        var ctype = types.Count;
        if (ctype > 2)
        {
            types[1] = DType.I8Req;
            types[2] = DType.BitReq;
        }
        else if (info.Scopes.Length > 0)
            types[1] = DType.BitReq;
        else
            types[1] = DType.I8Req;
        return (types[0], types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        var type = call.Type;
        if (!type.IsSequence)
            return false;
        var args = call.Args;
        if (args[0].Type != type)
            return false;
        var carg = args.Length;

        if (carg == 3)
        {
            if (args[1].Type != DType.I8Req)
                return false;
            if (args[2].Type != DType.BitReq)
                return false;
        }

        if (carg == 2)
        {
            if (call.Scopes.Length > 0)
            {
                if (args[1].Type != DType.BitReq)
                    return false;
            }
            else if (args[1].Type != DType.I8Req)
                return false;
        }
        return true;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        int carg = call.Args.Length;
        int cscope = call.Scopes.Length;
        Validation.Assert(call.Indices.Length == cscope);
        bool hasPred = cscope > 0;
        bool hasCount = !hasPred || carg == 3;

        var (min, max) = call.Args[0].GetItemCountRange();

        if (hasPred && call.Args[carg - 1].TryGetBool(out bool ok))
        {
            if (!ok)
            {
                // Predicate is false, so we keep or drop none.
                return IsTake ? (0, 0) : (min, max);
            }

            if (carg == 2)
            {
                // Predicate is true, so we keep or drop all.
                return IsTake ? (min, max) : (0, 0);
            }
            // Predicate is true, so the same as no predicate.
            hasPred = false;
        }

        if (hasCount)
        {
            var count = call.Args[1];
            if (count.TryGetI8(out long num))
            {
                if (num <= 0)
                    return IsTake ? (0, 0) : (min, max);
                if (IsTake)
                    return (!hasPred ? Math.Min(min, num) : 0, Math.Min(max, num));
                if (!hasPred && max < long.MaxValue)
                    max = Math.Max(0, max - num);
                return (Math.Max(0, min - num), max);
            }
            else if (IsTake && count is BndCastNumNode bcnn)
            {
                var typeNum = bcnn.Child.Type;
                Validation.Assert(typeNum.IsIntegralReq);

                long hi;
                switch (typeNum.Kind)
                {
                case DKind.Bit: hi = 1; break;
                case DKind.I1: hi = sbyte.MaxValue; break;
                case DKind.I2: hi = short.MaxValue; break;
                case DKind.I4: hi = int.MaxValue; break;
                case DKind.U1: hi = byte.MaxValue; break;
                case DKind.U2: hi = ushort.MaxValue; break;
                case DKind.U4: hi = uint.MaxValue; break;
                default: hi = long.MaxValue; break;
                }

                return (0, Math.Min(hi, max));
            }
        }

        // We don't know the number to keep/drop. We only know that the result is a subsequence.
        return (0, max);
    }

    protected override PullWithFlags GetPullWithFlagsCore(BndCallNode call, int iarg)
    {
        Validation.Assert(IsValidCall(call));
        Validation.Assert(!call.Traits.IsRepeated(iarg));

        if (iarg == 0)
            return PullWithFlags.Both;

        Validation.Assert(iarg == 1 & call.Args[1].Type == DType.I8Req);
        return PullWithFlags.With;
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.Assert(IsValidCall(call));

        int carg = call.Args.Length;
        int cscope = call.Scopes.Length;
        Validation.Assert(call.Indices.Length == cscope);
        bool hasPred = cscope > 0;
        bool isWhile = hasPred && call.GetDirective(carg - 1) == Directive.While;
        bool hasCount = !hasPred || carg == 3;

        var (min, max) = call.Args[0].GetItemCountRange();

        if (max == 0)
            return BndSequenceNode.CreateEmpty(call.Type);

        if (hasPred && call.Args[carg - 1].TryGetBool(out bool ok))
        {
            if (!ok)
            {
                // Predicate is false, so we keep or drop none.
                return IsTake ? BndSequenceNode.CreateEmpty(call.Type) : call.Args[0];
            }

            if (carg == 2)
            {
                // Predicate is true, so we keep or drop all.
                return IsTake ? call.Args[0] : BndSequenceNode.CreateEmpty(call.Type);
            }
            // Predicate is true, so we drop the predicate.
            return reducer.Reduce(BndCallNode.Create(this, call.Type, call.Args.RemoveTail(2)));
        }

        if (hasCount && call.Args[1].TryGetI8(out long num))
        {
            if (num <= 0)
            {
                // If the count is not positive, we'll take/drop nothing.
                return IsTake ? BndSequenceNode.CreateEmpty(call.Type) : call.Args[0];
            }

            if (max <= num)
            {
                if (!hasPred)
                    return IsTake ? call.Args[0] : BndSequenceNode.CreateEmpty(call.Type);

                // The num doesn't restrict, so that arg can be dropped.
                return BndCallNode.Create(this, call.Type, call.Args.RemoveAt(1), call.Scopes, call.Indices,
                        Immutable.Array<Directive>.Create(Directive.None, isWhile ? Directive.While : Directive.If), default);
            }
            Validation.Assert(0 < num & num < max);
            if (!hasPred && call.Args[0] is BndSequenceNode bsn)
            {
                Validation.Assert(min == max);
                Validation.Assert(min == bsn.Items.Length);
                var items = bsn.Items;
                if (IsTake)
                    items = items.RemoveTail((int)num);
                else
                    items = items.RemoveMinLim(0, (int)num);
                return BndSequenceNode.Create(bsn.Type, items);
            }
        }
        return base.ReduceCore(reducer, call);
    }

    protected override bool IsUnboundedCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));
        if (!IsTake)
            return true;
        if (call.Scopes.Length == 0)
            return false;
        if (call.GetDirective(call.Args.Length - 1) == Directive.While)
            return false;
        return true;
    }
}
