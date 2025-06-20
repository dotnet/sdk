// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Microsoft.NET.Sdk.StaticWebAssets.Benchmarks;

[MemoryDiagnoser]
public class EmptyBenchmark
{
    [Benchmark]
    public void Baseline() { }
}
