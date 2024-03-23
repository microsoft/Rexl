// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Rexl.Benchmark;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class NumUtilBenchmarks
{
    [Params(1_000)]
    public int IterationCount { get; set; }

    private const double NaN = double.NaN;
    private static readonly (double A, double B)[] Cases = new[] {
        (3.5, 3.5), (3.5, 7.0),
        (3.5, NaN), (NaN, 3.5),
        (NaN, NaN)};

    [Params(0, 1, 2, 3, 4)]
    public int CaseIndex {get; set;}

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EqR8")]
    public void EqOp()
    {
        var (A, B) = Cases[CaseIndex];
        bool any = false;
        for (int i = 0; i < IterationCount; i++)
            any |= A == B;
    }

    [Benchmark]
    [BenchmarkCategory("EqR8")]
    public void DotEquals()
    {
        var (A, B) = Cases[CaseIndex];
        bool any = false;
        for (int i = 0; i < IterationCount; i++)
            any |= A.Equals(B);
    }

    [Benchmark]
    [BenchmarkCategory("EqR8")]
    public void EqCmpDefault()
    {
        var (A, B) = Cases[CaseIndex];
        bool any = false;
        for (int i = 0; i < IterationCount; i++)
            any |= EqualityComparer<double>.Default.Equals(A, B);
    }

    [Benchmark]
    [BenchmarkCategory("EqR8")]
    public void InlineIsNaN()
    {
        var (A, B) = Cases[CaseIndex];
        bool any = false;
        for (int i = 0; i < IterationCount; i++)
            any |= A == B || double.IsNaN(B) && double.IsNaN(A);
    }

    [Benchmark]
    [BenchmarkCategory("EqR8")]
    public void InlineEqOp()
    {
        var (A, B) = Cases[CaseIndex];
        bool any = false;
#pragma warning disable CS1718 // Comparison made to same variable
        for (int i = 0; i < IterationCount; i++)
            any |= A == B || B != B && A != A;
#pragma warning restore CS1718 // Comparison made to same variable
    }
}
