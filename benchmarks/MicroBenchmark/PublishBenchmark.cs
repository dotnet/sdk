// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark;

/// <summary>
/// Measures one configured external <c>dotnet publish</c> process per iteration.
/// </summary>
public class PublishBenchmark : OrchardCoreCommandBenchmark
{
    protected override string CommandName => "publish";
    protected override bool IsPublish => true;

    [Benchmark]
    public Task Publish() => MeasureCommandAsync();
}
