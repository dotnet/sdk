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
// process-CWD mutation these tests perform.
public class GenerateStaticWebAssetsDevelopmentManifestMultiThreadingTest
{
    [Fact]
    public void WritesManifestRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GenerateStaticWebAssetsDevelopmentManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var relativeManifestPath = Path.Combine("obj", "staticwebassets.development.json");
        var relativeCacheFilePath = Path.Combine("obj", "staticwebassets.build.cache.json");
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
        Directory.CreateDirectory(Path.Combine(spawnDir, "obj"));

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var task = new GenerateStaticWebAssetsDevelopmentManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Source = "CurrentProjectId",
                Assets = [CreateAssetItem("css/site.css")],
                DiscoveryPatterns = [],
                ManifestPath = relativeManifestPath,
                CacheFilePath = relativeCacheFilePath
            };

            task.Execute().Should().BeTrue();

            var expectedPath = Path.Combine(projectDir, relativeManifestPath);
            File.Exists(expectedPath).Should().BeTrue("the manifest should be written under the project dir, not the process CWD");

            var incorrectPath = Path.Combine(spawnDir, relativeManifestPath);
            File.Exists(incorrectPath).Should().BeFalse();
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

    [Fact]
    public void ResolvesExistingManifestProbeRelativeToProjectDirectory_NotProcessCurrentDirectory()
    {
        // Verifies that the File.Exists/File.ReadAllBytes change-detection probe in PersistManifest (and
        // the up-to-date check in Execute) is rooted against TaskEnvironment.ProjectDirectory rather than
        // the process CWD. A decoy manifest is planted in the process CWD at the same relative path. If the
        // task read that decoy as the "existing" manifest it would overwrite the decoy or skip writing the
        // project-dir file. The correct behavior is to ignore the decoy entirely: create the manifest under
        // the project dir and leave the decoy untouched.
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GenerateStaticWebAssetsDevelopmentManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var relativeManifestPath = Path.Combine("obj", "staticwebassets.development.json");
        var relativeCacheFilePath = Path.Combine("obj", "staticwebassets.build.cache.json");
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
        Directory.CreateDirectory(Path.Combine(spawnDir, "obj"));

        const string decoyContents = "DECOY - must not be read or overwritten";
        var decoyPath = Path.Combine(spawnDir, relativeManifestPath);
        File.WriteAllText(decoyPath, decoyContents);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var task = new GenerateStaticWebAssetsDevelopmentManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Source = "CurrentProjectId",
                Assets = [CreateAssetItem("css/site.css")],
                DiscoveryPatterns = [],
                ManifestPath = relativeManifestPath,
                CacheFilePath = relativeCacheFilePath
            };

            task.Execute().Should().BeTrue();

            var expectedPath = Path.Combine(projectDir, relativeManifestPath);
            File.Exists(expectedPath).Should().BeTrue("the existence probe must target the project dir, find nothing, and create the manifest there");

            File.ReadAllText(decoyPath).Should().Be(decoyContents, "the decoy in the process CWD must be neither read nor overwritten");
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

    private static ITaskItem CreateAssetItem(string relativePath)
    {
        var asset = new StaticWebAsset
        {
            Identity = relativePath,
            SourceId = "OtherPackage",
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            ContentRoot = "wwwroot",
            BasePath = "_content/Base",
            RelativePath = relativePath,
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = relativePath,
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            LastWriteTime = DateTime.UtcNow,
            FileLength = 10,
        };

        return asset.ToTaskItem();
    }
}
