// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

// Test parallelization is disabled assembly-wide: the MSTest.Sdk project sets
// MSTestParallelizeScope=None, which emits [assembly: DoNotParallelize] and runs
// tests sequentially, isolating the process-CWD mutation this test performs.
[TestClass]
public class FilterStaticWebAssetEndpointsMultiThreadingTest
{
    [TestMethod]
    public void ResolvesRelativeContentRootAgainstTaskEnvironmentProjectDirectoryNotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(FilterStaticWebAssetEndpointsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeContentRoot = "wwwroot";

        var projectAbsoluteContentRoot = Path.Combine(projectDir, relativeContentRoot) + Path.DirectorySeparatorChar;
        var spawnAbsoluteContentRoot = Path.Combine(spawnDir, relativeContentRoot) + Path.DirectorySeparatorChar;
        projectAbsoluteContentRoot.Should().NotBe(spawnAbsoluteContentRoot,
            "the test setup must place project and decoy in different parents so a relative path resolves differently against each");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var buildEngine = new Mock<IBuildEngine>();

            var task = new FilterStaticWebAssetEndpoints
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                // No endpoints, so the asset flows through to AssetsWithoutMatchingEndpoints.
                Endpoints = [],
                Assets = [CreateAssetWithRelativeContentRoot("candidate.js", relativeContentRoot)],
            };

            var result = task.Execute();

            result.Should().BeTrue("the task must run to completion when TaskEnvironment.ProjectDirectory differs from the process CWD");
            task.AssetsWithoutMatchingEndpoints.Should().ContainSingle();

            // The relative ContentRoot must be absolutized against the task's ProjectDirectory,
            // not the process current working directory (the decoy). This is the multithreaded-safe behavior.
            var contentRoot = task.AssetsWithoutMatchingEndpoints[0].GetMetadata("ContentRoot");
            contentRoot.Should().Be(projectAbsoluteContentRoot);
            contentRoot.Should().NotBe(spawnAbsoluteContentRoot);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            if (Directory.Exists(testRoot))
            {
                try { Directory.Delete(testRoot, recursive: true); } catch { }
            }
        }
    }

    private static ITaskItem CreateAssetWithRelativeContentRoot(string itemSpec, string relativeContentRoot)
    {
        // Intentionally skips Normalize() so the relative ContentRoot reaches the task unmodified.
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = "MyPackage",
            SourceType = "Discovered",
            ContentRoot = relativeContentRoot,
            BasePath = "base",
            RelativePath = itemSpec,
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
        };

        return result.ToTaskItem();
    }
}
