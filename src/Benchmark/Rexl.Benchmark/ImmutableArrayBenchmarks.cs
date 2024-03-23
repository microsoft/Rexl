// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Microsoft.Rexl.Benchmark;

using SysImmutable = System.Collections.Immutable;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ImmutableArrayBenchmarks
{
    private int[] _randomValues;

    [Params(100, 10_000, 1_000_000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        Random random = new Random(42);

        _randomValues = Enumerable.Range(0, ItemCount).Select(i => random.Next()).ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ToImmutable")]
    public void SystemToImmutable()
    {
        var builder = SysImmutable.ImmutableArray.CreateBuilder<int>();
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.ToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void RexlToImmutable()
    {
        var builder = Immutable.Array.CreateBuilder<int>();
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.ToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void SystemToImmutableWithCapacity()
    {
        var builder = SysImmutable.ImmutableArray.CreateBuilder<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.ToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void SystemToImmutableWithCapacityMove()
    {
        var builder = SysImmutable.ImmutableArray.CreateBuilder<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.MoveToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void RexlToImmutableWithCapacity()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: false);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.ToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void RexlToImmutableWithCapacityCopy()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: false);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.ToImmutableCopy();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void RexlToImmutableWithCapacityInit()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: true);
        for (int i = 0; i < ItemCount; i++)
            builder[i] = i;

        var array = builder.ToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("ToImmutable")]
    public void SystemMoveToImmutableWithCapacity()
    {
        var builder = SysImmutable.ImmutableArray.CreateBuilder<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(i);

        var array = builder.MoveToImmutable();
    }

    [Benchmark]
    [BenchmarkCategory("Sort")]
    public void RexlSort()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: false);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(_randomValues[i]);

        builder.Sort((x, y) => x.CompareTo(y));
    }

    [Benchmark]
    [BenchmarkCategory("Sort")]
    public void RexlSortInit()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: true);
        for (int i = 0; i < ItemCount; i++)
            builder[i] = _randomValues[i];

        builder.Sort((x, y) => x.CompareTo(y));
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Sort")]
    public void SystemSort()
    {
        var builder = SysImmutable.ImmutableArray.CreateBuilder<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(_randomValues[i]);

        builder.Sort((x, y) => x.CompareTo(y));
    }

    [Benchmark]
    [BenchmarkCategory("Reverse")]
    public void RexlReverse()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: false);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(_randomValues[i]);

        builder.Reverse();
    }

    [Benchmark]
    [BenchmarkCategory("Reverse")]
    public void RexlReverseInit()
    {
        var builder = Immutable.Array.CreateBuilder<int>(ItemCount, init: true);
        for (int i = 0; i < ItemCount; i++)
            builder[i] = _randomValues[i];

        builder.Reverse();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Reverse")]
    public void SystemReverse()
    {
        var builder = SysImmutable.ImmutableArray.CreateBuilder<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            builder.Add(_randomValues[i]);

        builder.Reverse();
    }
}