// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[ResourceLock(WellKnownResources.EnvironmentVariables)]
public class ProgramTests
{
    [TestMethod]
    public void PreCanceledExternalCommandIsNotLaunched()
    {
        string toolDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-canceled-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(toolDirectory);
        string markerPath = Path.Combine(toolDirectory, "launched");
        string originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        try
        {
            const string commandName = "canceledtool";
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(
                    Path.Combine(toolDirectory, $"dotnet-{commandName}.cmd"),
                    $"@echo off{Environment.NewLine}echo launched>\"{markerPath}\"{Environment.NewLine}");
            }
            else
            {
                string toolPath = Path.Combine(toolDirectory, $"dotnet-{commandName}");
                File.WriteAllText(
                    toolPath,
                    $"#!/bin/sh{Environment.NewLine}touch '{markerPath}'{Environment.NewLine}");
                File.SetUnixFileMode(
                    toolPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            Environment.SetEnvironmentVariable("PATH", toolDirectory + Path.PathSeparator + originalPath);
            string[] args = [commandName];
            var parseResult = Parser.Parse(args);
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                Program.ExecuteExternalCommand(args, parseResult, cancellationSource.Token));

            Assert.IsFalse(File.Exists(markerPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(toolDirectory, recursive: true);
        }
    }
}
