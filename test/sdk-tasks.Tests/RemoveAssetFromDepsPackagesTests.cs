// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

public class RemoveAssetFromDepsPackagesTests(ITestOutputHelper log) : SdkTest(log)
{
    private const string SampleDepsJson = """
        {
          "targets": {
            ".NETCoreApp,Version=v9.0": {
              "MyApp/1.0.0": {
                "runtime": {
                  "MyApp.dll": {},
                  "Helper.dll": {}
                },
                "resources": {
                  "en/MyApp.resources.dll": {}
                }
              }
            }
          }
        }
        """;

    [Fact]
    public void ItRemovesSpecificAssetFromDeps()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var depsPath = Path.Combine(dir, "specific.deps.json");
        File.WriteAllText(depsPath, SampleDepsJson);

        RemoveAssetFromDepsPackages.DoRemoveAssetFromDepsPackages(depsPath, "runtime", "Helper.dll");

        var result = File.ReadAllText(depsPath);
        result.Should().NotContain("Helper.dll");
        result.Should().Contain("MyApp.dll");
    }

    [Fact]
    public void ItRemovesWildcardSection()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var depsPath = Path.Combine(dir, "wildcard.deps.json");
        File.WriteAllText(depsPath, SampleDepsJson);

        RemoveAssetFromDepsPackages.DoRemoveAssetFromDepsPackages(depsPath, "resources", "*");

        var result = File.ReadAllText(depsPath);
        result.Should().NotContain("resources");
        result.Should().Contain("runtime");
    }

    [Fact]
    public void ItDoesNotModifyFileWhenAssetNotFound()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var depsPath = Path.Combine(dir, "noop.deps.json");
        File.WriteAllText(depsPath, SampleDepsJson);
        var originalContent = File.ReadAllText(depsPath);

        RemoveAssetFromDepsPackages.DoRemoveAssetFromDepsPackages(depsPath, "runtime", "NonExistent.dll");

        // File should not be rewritten when nothing was found
        File.ReadAllText(depsPath).Should().Be(originalContent);
    }
}
