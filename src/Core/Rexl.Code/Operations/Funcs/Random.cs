// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

public sealed class UniformGen : RexlOperationGenerator<UniformFunc>
{
    public static UniformGen Instance = new UniformGen();

    private readonly ReadOnly.Array<MethodInfo> _methsSeeded;
    private readonly ReadOnly.Array<MethodInfo> _methsVolatile;

    private UniformGen()
    {
        _methsSeeded = new[]
        {
            new Func<long, ExecCtx, int, double>(ExecSeed).Method,
            new Func<long, long, ExecCtx, int, IEnumerable<double>>(ExecSeed).Method,
            new Func<long, double, double, ExecCtx, int, double>(ExecSeed).Method,
            new Func<long, double, double, long, ExecCtx, int, IEnumerable<double>>(ExecSeed).Method,
        };

        _methsVolatile = new[]
        {
            new Func<ExecCtx, int, double>(Exec).Method,
            new Func<long, ExecCtx, int, ICachingEnumerable<double>>(Exec).Method,
            new Func<double, double, ExecCtx, int, double>(Exec).Method,
            new Func<double, double, long, ExecCtx, int, ICachingEnumerable<double>>(Exec).Method,
        };
    }

    protected override bool NeedsExecCtxCore(BndCallNode call)
    {
        Validation.Assert(IsValidCall(call, true));
        return true;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var fn = GetOper(call);

        ReadOnly.Array<MethodInfo> meths;
        int index;
        if (fn.Seeded)
        {
            meths = _methsSeeded;
            index = call.Args.Length - 1;
            wrap = default;
        }
        else
        {
            meths = _methsVolatile;
            index = call.Args.Length;
            // We take care of caching explicitly when not seeded.
            wrap = SeqWrapKind.DontWrap;
        }

        if (!Validation.IsValidIndex(index, meths.Length))
        {
            Validation.Assert(false);
            stRet = default;
            wrap = default;
            return false;
        }

        var meth = meths[index];
        if (!fn.Seeded)
            Validation.Assert(!call.Type.IsSequence | typeof(ICachingEnumerable).IsAssignableFrom(meth.ReturnType));

        stRet = GenCallCtxId(codeGen, meth, sts, call);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ExecSeed(long seed, ExecCtx ctx, int id)
    {
        return ctx.GetRandomValue(id, seed);
    }

    private static double ExecSeed(long seed, double x, double y, ExecCtx ctx, int id)
    {
        if (UniformFunc.IsConst(x, y, out var res))
            return res;
        var r = ExecSeed(seed, ctx, id);
        return (1 - r) * x + r * y;
    }

    private static IEnumerable<double> ExecSeed(long seed, long n, ExecCtx ctx, int id)
    {
        if (n <= 0)
            return null;
        return WrapWithCount.Create(n, ExecSeedCore(seed, n, ctx, id));
    }

    private static IEnumerable<double> ExecSeedCore(long seed, long n, ExecCtx ctx, int id)
    {
        Validation.Assert(n > 0);

        var random = ctx.GetRandom(id, seed);
        while (--n >= 0)
            yield return random.NextDouble();
    }

    private static IEnumerable<double> ExecSeed(long seed, double x, double y, long n, ExecCtx ctx, int id)
    {
        if (n <= 0)
            return null;
        if (UniformFunc.IsConst(x, y, out var res))
            return new RepeatSequence<double>(res, n);
        return WrapWithCount.Create(n, ExecSeedCore(seed, x, y, n, ctx, id));
    }

    private static IEnumerable<double> ExecSeedCore(long seed, double x, double y, long n, ExecCtx ctx, int id)
    {
        Validation.Assert(n > 0);

        var random = ctx.GetRandom(id, seed);
        while (--n >= 0)
        {
            var r = random.NextDouble();
            yield return (1 - r) * x + r * y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Exec(ExecCtx ctx, int id)
    {
        return ctx.GetRandomValue(id);
    }

    private static double Exec(double x, double y, ExecCtx ctx, int id)
    {
        if (UniformFunc.IsConst(x, y, out var res))
            return res;
        var r = ctx.GetRandomValue(id);
        return (1 - r) * x + r * y;
    }

    private static ICachingEnumerable<double> Exec(long n, ExecCtx ctx, int id)
    {
        if (n <= 0)
            return null;
        return new CachingEnumerable<double>(n, ExecCore(n, ctx, id));
    }

    private static IEnumerable<double> ExecCore(long n, ExecCtx ctx, int id)
    {
        Validation.Assert(n > 0);

        var random = ctx.GetRandom(id);
        while (--n >= 0)
            yield return random.NextDouble();
    }

    private static ICachingEnumerable<double> Exec(double x, double y, long n, ExecCtx ctx, int id)
    {
        if (n <= 0)
            return null;
        if (UniformFunc.IsConst(x, y, out var res))
            return new RepeatSequence<double>(res, n);
        return new CachingEnumerable<double>(n, ExecCore(x, y, n, ctx, id));
    }

    private static IEnumerable<double> ExecCore(double x, double y, long n, ExecCtx ctx, int id)
    {
        Validation.Assert(n > 0);

        var random = ctx.GetRandom(id);
        while (--n >= 0)
        {
            var r = random.NextDouble();
            yield return (1 - r) * x + r * y;
        }
    }
}
