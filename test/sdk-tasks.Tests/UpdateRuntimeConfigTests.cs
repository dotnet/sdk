// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

public class UpdateRuntimeConfigTests(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
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

    [Fact]
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

    [Fact]
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
}
