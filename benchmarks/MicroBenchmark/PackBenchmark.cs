// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark;

/// <summary>
/// Measures one configured external <c>dotnet pack</c> process per iteration.
/// </summary>
public class PackBenchmark : OrchardCoreCommandBenchmark
{
    protected override string CommandName => "pack";
    protected override bool IsPublish => false;

    [Benchmark]
    public Task Pack() => MeasureCommandAsync();
}
