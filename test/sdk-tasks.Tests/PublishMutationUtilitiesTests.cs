// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

[TestClass]
public class PublishMutationUtilitiesTests : SdkTest
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    /// <summary>
    /// Verifies repeated renaming preserves dependency libraries and their ordering in
    /// every target instead of treating the next library as a new entry point.
    /// </summary>
    [TestMethod]
    public void RepeatedRenamingPreservesOtherLibrariesAndTimestamps()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string path = Path.Combine(directory, "multiple.deps.json");
        JsonNode deps = JsonNode.Parse(SampleDepsJson)!;
        JsonObject target = deps["targets"]![".NETCoreApp,Version=v9.0"]!.AsObject();
        target.Add("Dependency/2.0.0", JsonNode.Parse("""{ "runtime": { "Dependency.dll": {} } }"""));
        target.Add("ReferencedProject/3.0.0", JsonNode.Parse("""{ "runtime": { "ReferencedProject.dll": {} } }"""));
        var ridTarget = new JsonObject
        {
            ["Dependency/2.0.0"] = target["Dependency/2.0.0"]!.DeepClone(),
            ["OldApp/1.0.0"] = target["OldApp/1.0.0"]!.DeepClone(),
            ["ReferencedProject/3.0.0"] = target["ReferencedProject/3.0.0"]!.DeepClone()
        };
        deps["targets"]!.AsObject().Add(".NETCoreApp,Version=v9.0/test-rid", ridTarget);
        deps["libraries"]!.AsObject().Add("Dependency/2.0.0", JsonNode.Parse("""{ "type": "package" }"""));
        deps["libraries"]!.AsObject().Add("ReferencedProject/3.0.0", JsonNode.Parse("""{ "type": "project" }"""));
        File.WriteAllText(path, deps.ToJsonString());

        PublishMutationUtilities.ChangeEntryPointLibraryName(path, "NewApp");
        byte[] expected = File.ReadAllBytes(path);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
        DateTime timestamp = File.GetLastWriteTimeUtc(path);

        PublishMutationUtilities.ChangeEntryPointLibraryName(path, "NewApp");
        PublishMutationUtilities.ChangeEntryPointLibraryName(path, "NewApp");

        File.ReadAllBytes(path).Should().Equal(expected);
        File.GetLastWriteTimeUtc(path).Should().Be(timestamp);
        JsonNode result = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (JsonNode? value in result["targets"]!.AsObject().Select(pair => pair.Value))
        {
            value!.AsObject().Select(pair => pair.Key).Should().Equal(
                "Dependency/2.0.0", "ReferencedProject/3.0.0", "NewApp/1.0.0");
        }
        result["libraries"]!.AsObject().Select(pair => pair.Key).Should().Equal(
            "Dependency/2.0.0", "ReferencedProject/3.0.0", "NewApp/1.0.0");
    }

}
