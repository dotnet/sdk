// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class GenerateStaticWebAssetsManifestMultiThreadingTest
{
    [TestMethod]
    [DataRow("output/manifest.json", "output/manifest.cache")]
    [DataRow(" ", "  ")]
    public void WritesManifestAndCacheRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory(
        string relativeManifestPath,
        string relativeManifestCacheFilePath)
    {
        // Layout: place project and decoy in disjoint subtrees so that the same
        // relative path produces different absolute paths from each root.
        //   <testRoot>/project/output/   <-- TaskEnvironment.ProjectDirectory
        //   <testRoot>/decoy/spawn/      <-- process CWD (the "decoy")
        //   <testRoot>/decoy/spawn/output/ <-- where the pre-migration code would write
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GenerateStaticWebAssetsManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        var projectOutputDir = Path.Combine(projectDir, "output");
        var spawnOutputDir = Path.Combine(spawnDir, "output");
        Directory.CreateDirectory(projectOutputDir);
        Directory.CreateDirectory(spawnOutputDir);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));
            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = Array.Empty<ITaskItem>(),
                Endpoints = Array.Empty<ITaskItem>(),
                ReferencedProjectsConfigurations = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = Array.Empty<ITaskItem>(),
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = relativeManifestPath,
                ManifestCacheFilePath = relativeManifestCacheFilePath,
            };

            if (OperatingSystem.IsWindows() &&
                (string.IsNullOrWhiteSpace(relativeManifestPath) || string.IsNullOrWhiteSpace(relativeManifestCacheFilePath)))
            {
                task.Execute().Should().BeFalse();
                errorMessages.Should().NotBeEmpty();
                return;
            }

            task.Execute().Should().BeTrue(string.Join("; ", errorMessages));

            var expectedManifest = Path.Combine(projectDir, relativeManifestPath);
            var expectedCache = Path.Combine(projectDir, relativeManifestCacheFilePath);
            File.Exists(expectedManifest).Should().BeTrue("manifest must be written under TaskEnvironment.ProjectDirectory, not the process CWD");
            File.Exists(expectedCache).Should().BeTrue("cache must be written under TaskEnvironment.ProjectDirectory, not the process CWD");

            File.Exists(Path.Combine(spawnDir, relativeManifestPath)).Should().BeFalse();
            File.Exists(Path.Combine(spawnDir, relativeManifestCacheFilePath)).Should().BeFalse();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
