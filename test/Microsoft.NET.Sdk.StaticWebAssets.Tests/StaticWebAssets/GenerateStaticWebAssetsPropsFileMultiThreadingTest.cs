// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class GenerateStaticWebAssetsPropsFileMultiThreadingTest
{
    [TestMethod]
    public void WritesPropsFileRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GenerateStaticWebAssetsPropsFileMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var projectOutputDir = Path.Combine(projectDir, "output");
        var spawnOutputDir = Path.Combine(spawnDir, "output");
        Directory.CreateDirectory(projectOutputDir);
        Directory.CreateDirectory(spawnOutputDir);

        var currentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var buildEngine = new Mock<IBuildEngine>();
            var task = new GenerateStaticWebAssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                TargetPropsFilePath = Path.Combine("output", "build.props"),
                StaticWebAssets = new TaskItem[]
                {
                    new TaskItem(Path.Combine("wwwroot", "js", "app.js"), new Dictionary<string, string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = "js/app.js",
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["Fingerprint"] = "fp",
                        ["Integrity"] = "int",
                        ["OriginalItemSpec"] = Path.Combine("wwwroot", "js", "app.js"),
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest",
                        ["FileLength"] = "10",
                        ["LastWriteTime"] = new DateTimeOffset(new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc)).ToString(StaticWebAsset.DateTimeAssetFormat),
                    }),
                }
            };

            task.Execute().Should().BeTrue();

            var expectedPath = Path.Combine(projectDir, "output", "build.props");
            File.Exists(expectedPath).Should().BeTrue("the props file should be written under TaskEnvironment.ProjectDirectory, not the process CWD");

            var incorrectPath = Path.Combine(spawnDir, "output", "build.props");
            File.Exists(incorrectPath).Should().BeFalse("the props file must NOT be written relative to the process CWD");
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
