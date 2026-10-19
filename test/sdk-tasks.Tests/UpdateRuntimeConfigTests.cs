// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

[TestClass]
public class UpdateRuntimeConfigTests : SdkTest
{
    [TestMethod]
    public void ItUpdatesSingleFrameworkVersion()
    {
    var dir = TestAssetsManager.CreateTestDirectory().Path;
        var configPath = Path.Combine(dir, "single.runtimeconfig.json");
        File.WriteAllText(configPath, """
            {
              "runtimeOptions": {
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "1.0.0"
                }
              }
            }
            """);

        var task = new UpdateRuntimeConfig
        {
            BuildEngine = new MockBuildEngine(),
            RuntimeConfigPaths = [new TaskItem(configPath)],
            MicrosoftNetCoreAppVersion = "9.0.0",
            MicrosoftAspNetCoreAppVersion = "9.0.0"
        };

        task.Execute().Should().BeTrue();

        var result = File.ReadAllText(configPath);
        result.Should().Contain("\"version\": \"9.0.0\"");
        result.Should().Contain("\"name\": \"Microsoft.NETCore.App\"");
    }

    [TestMethod]
    public void ItUpdatesMultipleFrameworkVersions()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var configPath = Path.Combine(dir, "multi.runtimeconfig.json");
        File.WriteAllText(configPath, """
            {
              "runtimeOptions": {
                "frameworks": [
                  {
                    "name": "Microsoft.NETCore.App",
                    "version": "1.0.0"
                  },
                  {
                    "name": "Microsoft.AspNetCore.App",
                    "version": "1.0.0"
                  }
                ]
              }
            }
            """);

        var task = new UpdateRuntimeConfig
        {
            BuildEngine = new MockBuildEngine(),
            RuntimeConfigPaths = [new TaskItem(configPath)],
            MicrosoftNetCoreAppVersion = "9.0.0",
            MicrosoftAspNetCoreAppVersion = "8.0.0"
        };

        task.Execute().Should().BeTrue();

        var result = File.ReadAllText(configPath);
        result.Should().Contain("\"Microsoft.NETCore.App\"");
        result.Should().Contain("\"Microsoft.AspNetCore.App\"");
        // Verify the versions were actually updated by checking the output contains both new versions
        result.Should().Contain("\"9.0.0\"");
        result.Should().Contain("\"8.0.0\"");
        result.Should().NotContain("\"1.0.0\"");
    }

    [TestMethod]
    public void ItPreservesUnknownFrameworks()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var configPath = Path.Combine(dir, "unknown.runtimeconfig.json");
        File.WriteAllText(configPath, """
            {
              "runtimeOptions": {
                "framework": {
                  "name": "Microsoft.WindowsDesktop.App",
                  "version": "1.0.0"
                }
              }
            }
            """);

        var task = new UpdateRuntimeConfig
        {
            BuildEngine = new MockBuildEngine(),
            RuntimeConfigPaths = [new TaskItem(configPath)],
            MicrosoftNetCoreAppVersion = "9.0.0",
            MicrosoftAspNetCoreAppVersion = "9.0.0"
        };

        task.Execute().Should().BeTrue();

        var result = File.ReadAllText(configPath);
        // Unknown framework should retain original version
        result.Should().Contain("\"version\": \"1.0.0\"");
    }

    /// <summary>
    /// Verifies a raw config can produce multiple stable destinations without mutation,
    /// track framework-version edits and reverts, and repair one missing output independently.
    /// </summary>
    [TestMethod]
    public void SeparateDestinationsPreserveInputsAndTrackVersionChanges()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string source = Path.Combine(directory, "raw.runtimeconfig.json");
        string first = Path.Combine(directory, "first", "app.runtimeconfig.json");
        string second = Path.Combine(directory, "second", "app.runtimeconfig.json");
        File.WriteAllText(source, """
            { "runtimeOptions": { "framework": { "name": "Microsoft.NETCore.App", "version": "1.0.0" },
              "rollForward": "Major", "configProperties": { "Custom.Property": true } }, "custom": "preserved" }
            """);
        byte[] raw = File.ReadAllBytes(source);
        DateTime sourceTimestamp = File.GetLastWriteTimeUtc(source);
        var task = new UpdateRuntimeConfig
        {
            BuildEngine = new MockBuildEngine(),
            RuntimeConfigPaths = [new TaskItem(source), new TaskItem(source)],
            DestinationPaths = [new TaskItem(first), new TaskItem(second)],
            MicrosoftNetCoreAppVersion = "9.0.0",
            MicrosoftAspNetCoreAppVersion = "8.0.0"
        };
        task.Execute().Should().BeTrue();
        byte[] expected = File.ReadAllBytes(first);
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddMinutes(-5));
        DateTime timestamp = File.GetLastWriteTimeUtc(first);
        task.Execute().Should().BeTrue();
        File.GetLastWriteTimeUtc(first).Should().Be(timestamp);
        File.Delete(second);
        task.Execute().Should().BeTrue();
        File.ReadAllBytes(second).Should().Equal(expected);
        File.GetLastWriteTimeUtc(first).Should().Be(timestamp);

        task.MicrosoftNetCoreAppVersion = "9.1.0";
        task.Execute().Should().BeTrue();
        JsonNode updated = JsonNode.Parse(File.ReadAllText(first))!;
        updated["runtimeOptions"]!["framework"]!["version"]!.GetValue<string>().Should().Be("9.1.0");
        updated["runtimeOptions"]!["rollForward"]!.GetValue<string>().Should().Be("Major");
        updated["runtimeOptions"]!["configProperties"]!["Custom.Property"]!.GetValue<bool>().Should().BeTrue();
        updated["custom"]!.GetValue<string>().Should().Be("preserved");
        task.MicrosoftNetCoreAppVersion = "9.0.0";
        task.Execute().Should().BeTrue();
        File.ReadAllBytes(first).Should().Equal(expected);
        File.ReadAllBytes(source).Should().Equal(raw);
        File.GetLastWriteTimeUtc(source).Should().Be(sourceTimestamp);
    }

    /// <summary>
    /// Verifies positional mapping errors are reported before reading or writing files.
    /// </summary>
    [TestMethod]
    public void MismatchedDestinationCountsFailExplicitly()
    {
        var task = new UpdateRuntimeConfig
        {
            BuildEngine = new MockBuildEngine(),
            RuntimeConfigPaths = [new TaskItem("missing.runtimeconfig.json")],
            DestinationPaths = [],
            MicrosoftNetCoreAppVersion = "9.0.0",
            MicrosoftAspNetCoreAppVersion = "8.0.0"
        };

        task.Execute().Should().BeFalse();
        task.Log.HasLoggedErrors.Should().BeTrue();
    }

    /// <summary>
    /// Verifies missing and malformed sources remain explicit failures and cannot replace
    /// a previously valid destination with partial or default configuration.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InvalidSourcesDoNotReplaceExistingDestinations(bool missing)
    {
        string directory = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path;
        string source = Path.Combine(directory, "raw.runtimeconfig.json");
        string destination = Path.Combine(directory, "app.runtimeconfig.json");
        if (!missing)
        {
            File.WriteAllText(source, "{");
        }
        File.WriteAllText(destination, "previous result");
        var task = new UpdateRuntimeConfig
        {
            BuildEngine = new MockBuildEngine(),
            RuntimeConfigPaths = [new TaskItem(source)],
            DestinationPaths = [new TaskItem(destination)],
            MicrosoftNetCoreAppVersion = "9.0.0",
            MicrosoftAspNetCoreAppVersion = "8.0.0"
        };
        Action execute = () => task.Execute();

        if (missing)
        {
            execute.Should().Throw<FileNotFoundException>();
        }
        else
        {
            execute.Should().Throw<JsonException>();
        }
        File.ReadAllText(destination).Should().Be("previous result");
    }
}
