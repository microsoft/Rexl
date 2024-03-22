// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl;

public abstract partial class RandomBaseFunc : RexlOper
{
    protected static readonly DName NameRandom = new DName("Random");
    protected static readonly DName NameRandomS = new DName("RandomS");
    protected static readonly NPath NsRandom = NPath.Root.Append(NameRandom);
    protected static readonly NPath NsRandomS = NPath.Root.Append(NameRandomS);

    protected RandomBaseFunc(bool seeded, string name, int arityMin, int arityMax)
        : base(isFunc: true, new DName(name), seeded ? NsRandomS : NsRandom, arityMin, arityMax)
    {
    }
}

/// <summary>
/// Generates floating point numbers over a uniform interval.
/// Supported signatures are:
/// Random.Uniform([count]) -> Over unit interval [0, 1]
/// RandomS.Uniform(seed, [count]) -> Over unit interval [0, 1]
/// Random.Uniform(x, y, [count])
/// RandomS.Uniform(seed, x, y, [count])
///
/// Note we have to accept an explicit count- something like
/// Generate(10, RandomS.Uniform(seed)) just produces a sequence of
/// identical values, and lifting over the seed is expensive.
///
/// REVIEW: Support R4s. Can have "R4" and "R8" suffixes for the names,
/// and have the form without a suffix implicitly use R8s.
/// </summary>
public sealed partial class UniformFunc : RandomBaseFunc
{
    public static readonly UniformFunc Pure = new UniformFunc(seeded: true);
    public static readonly UniformFunc Volatile = new UniformFunc(seeded: false);

    public bool Seeded { get; }

    private UniformFunc(bool seeded)
        : base(seeded, "Uniform", seeded ? 1 : 0, seeded ? 4 : 3)
    {
        Seeded = seeded;
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        if (Seeded)
        {
            // Allow lifting over the seed.
            return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x1);
        }

        return ArgTraitsSimple.Create(this, eager: false, carg);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        var types = info.GetArgTypes();
        var carg = types.Count;
        Validation.Assert(SupportsArity(carg));

        int iarg = 0;
        if (Seeded)
            types[iarg++] = DType.I8Req;

        if (iarg <= carg - 2)
        {
            // Range.
            types[iarg++] = DType.R8Req;
            types[iarg++] = DType.R8Req;
        }

        DType typeRes;
        if (iarg < carg)
        {
            // With count.
            types[iarg++] = DType.I8Req;
            typeRes = DType.R8Req.ToSequence();
        }
        else
            typeRes = DType.R8Req;
        Validation.Assert(iarg == carg);

        return (typeRes, types.ToImmutable());
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        Validation.AssertValue(call);
        var carg = call.Args.Length;
        var args = call.Args;

        int iarg = 0;
        if (Seeded)
        {
            if (args[iarg++].Type != DType.I8Req)
                return false;
        }

        if (iarg <= carg - 2)
        {
            if (args[iarg++].Type != DType.R8Req)
                return false;
            if (args[iarg++].Type != DType.R8Req)
                return false;
        }

        if (iarg < carg)
        {
            if (args[iarg++].Type != DType.I8Req)
                return false;
            if (call.Type != DType.R8Req.ToSequence())
                return false;
        }
        else
        {
            if (call.Type != DType.R8Req)
                return false;
        }
        Validation.Assert(iarg == carg);

        return true;
    }

    public override bool IsVolatile(ArgTraits traits, DType type, Immutable.Array<BoundNode> args,
        Immutable.Array<ArgScope> scopes, Immutable.Array<ArgScope> indices,
        Immutable.Array<Directive> dirs, Immutable.Array<DName> names)
    {
        Validation.AssertValue(traits);
        Validation.Assert(traits.Oper == this);
        Validation.Assert(SupportsArity(traits.SlotCount));
        Validation.Assert(type.IsValid);
        Validation.Assert(!args.IsDefault);
        Validation.Assert(traits.SlotCount == args.Length);
        Validation.Assert(!scopes.IsDefault);
        Validation.Assert(scopes.Length == traits.ScopeCount);
        Validation.Assert(!indices.IsDefault);
        Validation.Assert(indices.Length == traits.ScopeIndexCount);

        return !Seeded;
    }

    protected override (long min, long max) GetItemCountRangeCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call));

        // Note this method only gets called when passed an explicit count as the last arg,
        // which makes the return type a sequence.
        Validation.Assert(call.Type.IsSequence);

        if (call.Args[call.Args.Length - 1].TryGetI8(out long n))
            return n <= 0 ? (0, 0) : (n, n);
        return (0, long.MaxValue);
    }

    protected override BoundNode ReduceCore(IReducer reducer, BndCallNode call)
    {
        Validation.AssertValue(reducer);
        Validation.AssertValue(call);
        Validation.Assert(IsValidCall(call));

        var carg = call.Args.Length;
        if (call.Type.IsSequence && call.Args[carg - 1].TryGetI8(out long n) && n <= 0)
            return BndSequenceNode.CreateEmpty(call.Type);

        int iarg = Seeded ? 1 : 0;
        if (iarg <= carg - 2 &&
            call.Args[iarg].TryGetFractional(out double x) &&
            call.Args[iarg + 1].TryGetFractional(out double y))
        {
            if (IsConst(x, y, out var res))
            {
                var val = BndFltNode.CreateR8(res);
                if (!call.Type.IsSequence)
                    return val;
                return BndCallNode.Create(RepeatFunc.Instance, call.Type,
                    Immutable.Array<BoundNode>.Create(val, call.Args[carg - 1]));
            }

            if (x == 0 && y == 1)
            {
                // Reduce to unit interval.
                return BndCallNode.Create(this, call.Type, call.Args.RemoveMinLim(iarg, iarg + 2));
            }
        }
        return call;
    }

    /// <summary>
    /// Determines whether the range will produce a constant "random" sequence. This happens, for example,
    /// when <paramref name="x"/> and <paramref name="y"/> are equal or when one is infinite and
    /// the other is finite.
    /// </summary>
    public static bool IsConst(double x, double y, out double res)
    {
        if (x == y)
        {
            // They are equal. Note that we don't care about negative zero.
            res = x;
            return true;
        }
        if (!x.IsFinite())
        {
            if (!y.IsFinite())
            {
                // They are both infinite or nan and not the same flavor of infinite, so any
                // convex combination will be nan.
                res = double.NaN;
                return true;
            }
            // x is infinite but y is not, so x wins.
            res = x;
            return true;
        }
        if (!y.IsFinite())
        {
            // y is infinite but x is not, so y wins.
            res = y;
            return true;
        }

        res = default;
        return false;
    }
}
