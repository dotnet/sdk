// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

public class PublishMutationUtilitiesTests(ITestOutputHelper log) : SdkTest(log)
{
    private const string SampleDepsJson = """
        {
          "targets": {
            ".NETCoreApp,Version=v9.0": {
              "OldApp/1.0.0": {
                "runtime": {
                  "OldApp.dll": {}
                }
              }
            }
          },
          "libraries": {
            "OldApp/1.0.0": {
              "type": "project"
            }
          }
        }
        """;

    [Fact]
    public void ItRenamesEntryPointLibrary()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var depsPath = Path.Combine(dir, "rename.deps.json");
        File.WriteAllText(depsPath, SampleDepsJson);

        PublishMutationUtilities.ChangeEntryPointLibraryName(depsPath, "NewApp");

        var result = File.ReadAllText(depsPath);
        result.Should().Contain("NewApp/1.0.0");
        result.Should().NotContain("OldApp/1.0.0");
    }

    [Fact]
    public void ItRemovesEntryPointLibraryWhenNewNameIsNull()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var depsPath = Path.Combine(dir, "remove.deps.json");
        File.WriteAllText(depsPath, SampleDepsJson);

        PublishMutationUtilities.ChangeEntryPointLibraryName(depsPath, null);

        var result = File.ReadAllText(depsPath);
        result.Should().NotContain("OldApp/1.0.0");
        result.Should().NotContain("NewApp");
    }

    [Fact]
    public void ItPreservesVersionInRenamedLibrary()
    {
      var dir = TestAssetsManager.CreateTestDirectory().Path;
        var depsPath = Path.Combine(dir, "version.deps.json");
        File.WriteAllText(depsPath, SampleDepsJson);

        PublishMutationUtilities.ChangeEntryPointLibraryName(depsPath, "RenamedApp");

        var result = File.ReadAllText(depsPath);
        result.Should().Contain("RenamedApp/1.0.0");
        result.Should().Contain("\"type\": \"project\"");
    }
}
