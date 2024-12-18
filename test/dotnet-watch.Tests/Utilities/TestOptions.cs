// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal static class TestOptions
{
    public static readonly ProjectOptions ProjectOptions = GetProjectOptions([]);

    public static EnvironmentOptions GetEnvironmentOptions(string workingDirectory = "", string muxerPath = "")
        => new(workingDirectory, muxerPath, TestFlags: TestFlags.RunningAsTest);

    public static CommandLineOptions GetCommandLineOptions(string[] args)
        => CommandLineOptions.Parse(args, NullReporter.Singleton, TextWriter.Null, out _);

    public static ProjectOptions GetProjectOptions(string[] args = null)
    {
        var options = GetCommandLineOptions(args ?? []);
        return options.GetProjectOptions(options.ProjectPath ?? "test.csproj", workingDirectory: "");
    }
}
