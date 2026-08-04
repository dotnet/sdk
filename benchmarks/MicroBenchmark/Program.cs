// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Benchmark;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 &&
            args[0] is "--pack" or "--publish" or "--pack-smoke" or "--publish-smoke")
        {
            SetCommandRunId();
        }

        if (args.Length == 0)
        {
            BenchmarkRunner.Run<InfoTests>();
        }
        else if (args is ["--pack"])
        {
            RunBenchmark<PackBenchmark>();
        }
        else if (args is ["--publish"])
        {
            RunBenchmark<PublishBenchmark>();
        }
        else if (args is ["--pack-smoke"])
        {
            new PackBenchmark().RunSmokeAsync().GetAwaiter().GetResult();
        }
        else if (args is ["--publish-smoke"])
        {
            new PublishBenchmark().RunSmokeAsync().GetAwaiter().GetResult();
        }
        else
        {
            throw new ArgumentException(
                "Supported arguments are --pack, --publish, --pack-smoke, and --publish-smoke.");
        }

        static void RunBenchmark<T>()
            where T : OrchardCoreCommandBenchmark
        {
            Job job = Job.Default
                .WithToolchain(new InProcessEmitToolchain(TimeSpan.FromHours(2), true))
                .WithStrategy(RunStrategy.Monitoring)
                .WithLaunchCount(1)
                .WithWarmupCount(OrchardCoreCommandBenchmark.WarmupCount)
                .WithIterationCount(OrchardCoreCommandBenchmark.IterationCount)
                .WithInvocationCount(1)
                .WithUnrollFactor(1);
            ManualConfig config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job);
            BenchmarkRunner.Run<T>(config);
        }

        static void SetCommandRunId()
        {
            if (Environment.GetEnvironmentVariable("DOTNET_SDK_PACK_PUBLISH_BENCHMARK_RUN_ID") is null)
            {
                Environment.SetEnvironmentVariable(
                    "DOTNET_SDK_PACK_PUBLISH_BENCHMARK_RUN_ID",
                    Guid.NewGuid().ToString("N"));
            }
        }

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
