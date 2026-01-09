// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 1, invocationCount: 1)]
public class InfoTests
{
    private static readonly string[] s_args = ["--info"];

    [Benchmark]
    public int RunInfoCommand()
    {
        return Microsoft.DotNet.Cli.Program.Main(s_args);
    }
}
