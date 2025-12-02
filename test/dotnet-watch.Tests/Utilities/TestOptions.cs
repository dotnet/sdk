// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.Watch.UnitTests;

internal static class TestOptions
{
    private static int s_testPort = 7000;

    public static int GetTestPort()
        => Interlocked.Increment(ref s_testPort);

    public static readonly ProjectOptions ProjectOptions = GetProjectOptions([]);

    public static EnvironmentOptions GetEnvironmentOptions(string workingDirectory = "", string muxerPath = "", TestAsset? asset = null)
        // 0 timeout for process cleanup in tests. We can't send Ctrl+C, so process termination must be forced.
        => new(workingDirectory, muxerPath, ProcessCleanupTimeout: TimeSpan.FromSeconds(0), TestFlags: TestFlags.RunningAsTest, TestOutput: asset != null ? GetWatchTestOutputPath(asset) : "");

    public static CommandLineOptions GetCommandLineOptions(string[] args)
        => CommandLineOptions.Parse(args, NullReporter.Singleton, TextWriter.Null, out _) ?? throw new InvalidOperationException();

    public static ProjectOptions GetProjectOptions(string[]? args = null)
    {
        var options = GetCommandLineOptions(args ?? []);
        return options.GetProjectOptions(options.ProjectPath ?? "test.csproj", workingDirectory: "");
    }

    public static string GetWatchTestOutputPath(this TestAsset asset)
        => Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is { } ciOutputRoot
            ? Path.Combine(ciOutputRoot, ".hotreload", asset.Name)
            : asset.Path + ".hotreload";
}
