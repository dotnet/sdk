// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Running;

namespace Microsoft.NET.Sdk.StaticWebAssets.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
