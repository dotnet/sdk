// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide via
// [assembly:CollectionBehavior(DisableTestParallelization = true)] in
// LegacyStaticWebAssetsV1IntegrationTest.cs, which already isolates the
// process-CWD mutation this test performs.
public class GenerateStaticWebAssetEndpointsManifestMultiThreadingTest
{
    [Fact]
    public void WritesEndpointsManifestAndExclusionCacheRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        // Layout: place project and decoy in disjoint subtrees so that the same
        // relative path produces different absolute paths from each root.
        //   <testRoot>/project/output/   <-- TaskEnvironment.ProjectDirectory
        //   <testRoot>/decoy/spawn/      <-- process CWD (the "decoy")
        //   <testRoot>/decoy/spawn/output/ <-- where the pre-migration code would write
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GenerateStaticWebAssetEndpointsManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
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

            var task = new GenerateStaticWebAssetEndpointsManifest
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = Array.Empty<ITaskItem>(),
                Endpoints = Array.Empty<ITaskItem>(),
                Source = "MyProject",
                ManifestType = "Build",
                ExclusionPatterns = "**/excluded.txt",
                ManifestPath = Path.Combine("output", "endpoints.json"),
                CacheFilePath = Path.Combine("output", "endpoints.cache"),
                ExclusionPatternsCacheFilePath = Path.Combine("output", "exclusions.cache"),
            };

            task.Execute().Should().BeTrue(string.Join("; ", errorMessages));

            var expectedManifest = Path.Combine(projectOutputDir, "endpoints.json");
            var expectedExclusionCache = Path.Combine(projectOutputDir, "exclusions.cache");
            File.Exists(expectedManifest).Should().BeTrue("endpoints manifest must be written under TaskEnvironment.ProjectDirectory, not the process CWD");
            File.Exists(expectedExclusionCache).Should().BeTrue("exclusion-patterns cache must be written under TaskEnvironment.ProjectDirectory, not the process CWD");

            File.Exists(Path.Combine(spawnOutputDir, "endpoints.json")).Should().BeFalse();
            File.Exists(Path.Combine(spawnOutputDir, "exclusions.cache")).Should().BeFalse();
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
