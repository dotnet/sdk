// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Running;

namespace Benchmark;

internal class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run(typeof(Program).Assembly);

        // BenchmarkDotNet bakes a fair amount of assumptions into the way it generates
        // projects for running its benchmarks. One of the key problems we run into is how
        // it figures out the root of the repository. It walks up the directory structure
        // to find the first folder with a `*.sln` or `*.slnx` or `global.json`. It then
        // searches down from there to find the project file. In the SDK repo currently
        // we have a `global.json` in the `artifacts/bin` folder- that prevents it from
        // finding the project file.
        //
        // We work around this currently by redirecting the output to `artifacts/Benchmark`.
        // One partially explored alternative was to derive the `CsProjGenerator` and customize
        // its `GetProjectFilePath(Type benchmarkTarget, ILogger logger)`. There is a fair
        // amount of logic there that needs reimplemented, and you have to create a custom
        // toolchain something like this:
        //
        //    public class CustomToolchain : Toolchain
        //    {
        //        public CustomToolchain(string? tfm = default) : base(
        //            "CustomToolchain",
        //            new CustomCsProjGenerator(
        //                targetFrameworkMoniker: tfm ?? GetCurrentTfm(),
        //                cliPath: null,
        //                packagesPath: null,
        //                runtimeFrameworkVersion: null),
        //            new DotNetCliBuilder(tfm ?? GetCurrentTfm()),
        //            new DotNetCliExecutor(customDotNetCliPath: null)) { }
        //    }
        //
        // Things still break with the `bin/global.json` with this. Pulling in and tweaking
        // everything that is needed to get the temporary project to build successfully looks
        // to be a potential whack-a-mole problem, so starting by rooting the output in a new
        // folder in the `artifacts` directory that ensures the repo root's `global.json` is
        // in "scope" so we don't have to modify the toolchain.
        //
        // https://github.com/dotnet/BenchmarkDotNet/blob/master/src/BenchmarkDotNet/Toolchains/CsProj/CsProjGenerator.cs
    }
}
