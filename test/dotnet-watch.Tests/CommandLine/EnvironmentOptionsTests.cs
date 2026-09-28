// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class BuildReporterTests
{
    [Fact]
    public void GetBinLogPath()
    {
        var root = Path.GetTempPath();
        var projectPath = Path.Combine(root, "project.csproj");
        var workingDirectory = Path.Combine(root, "working");
        var envOptions = TestOptions.GetEnvironmentOptions(workingDirectory);

        AssertEx.Equal(Path.Combine(workingDirectory, "msbuild-dotnet-watch.Restore.project.csproj.1.binlog"),
            envOptions.GetBinLogPath(
                projectPath: projectPath,
                operationName: "Restore",
                new GlobalOptions() { BinaryLogPath = "msbuild.binlog" }));

        AssertEx.Equal(Path.Combine(root, "logs", "test-dotnet-watch.Build.project.csproj.2.binlog"),
            envOptions.GetBinLogPath(
                projectPath: projectPath,
                operationName: "Build",
                new GlobalOptions() { BinaryLogPath = Path.Combine(root, "logs", "test.binlog") }));

    }
}
