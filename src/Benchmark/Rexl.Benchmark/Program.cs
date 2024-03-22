// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using BenchmarkDotNet.Running;

namespace Microsoft.Rexl.Benchmark;

internal class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
    }
}
