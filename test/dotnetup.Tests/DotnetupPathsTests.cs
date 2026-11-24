// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DotnetupPathsTests
{
    [TestMethod]
    [DataRow("none")]
    [DataRow("admin")]
    public void DataDirectoryEnvironmentVariable_SelectsChildProcessConfiguration(string accessMode)
    {
        using var environment = new TestEnvironment();
        string dataDirectory = Path.Combine(environment.TempRoot, "data with spaces [override]");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, "dotnetup.config.json"),
            $$"""{"schemaVersion":"1","accessMode":"{{accessMode}}","dotnetupOnPath":false,"updateNotifications":false}""");

        var (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            ["--info", "--no-list", "--format", "json"],
            captureOutput: true,
            workingDirectory: environment.TempRoot,
            environmentVariables: new Dictionary<string, string>
            {
                ["DOTNET_DOTNETUP_DATA_DIR"] = dataDirectory,
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            },
            timeoutMilliseconds: 30_000);

        exitCode.Should().Be(0, output);
        using var info = JsonDocument.Parse(output);
        info.RootElement.GetProperty("accessMode").GetString().Should().Be(accessMode);
        info.RootElement.GetProperty("dotnetupOnPath").GetBoolean().Should().BeFalse();
    }

    [TestMethod]
    public void DefaultDotnetInstallPath_IsSubdirectoryOfDataDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "dotnetup-paths-tests", Guid.NewGuid().ToString("N"));
        DotnetupPaths.SetTestDataDirectoryOverride(dataDirectory);

        try
        {
            DotnetupPaths.DefaultDotnetInstallPath.Should().Be(Path.Combine(dataDirectory, "dotnet"));
        }
        finally
        {
            DotnetupPaths.ClearTestDataDirectoryOverride();
        }
    }

    [TestMethod]
    public void UpdateCheckDirectory_IsSubdirectoryOfDataDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "dotnetup-paths-tests", Guid.NewGuid().ToString("N"));
        DotnetupPaths.SetTestDataDirectoryOverride(dataDirectory);

        try
        {
            DotnetupPaths.UpdateCheckDirectory.Should().Be(Path.Combine(dataDirectory, "update-checks"));
        }
        finally
        {
            DotnetupPaths.ClearTestDataDirectoryOverride();
        }
    }

    [TestMethod]
    public void ConfiguredTestEnvironment_OverridesDefaultDotnetInstallPath()
    {
        using TestEnvironment testEnvironment = DotnetupTestUtilities.CreateTestEnvironment();

        DotnetupPaths.DefaultDotnetInstallPath.Should().Be(testEnvironment.InstallPath);
    }
}