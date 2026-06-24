// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[DoNotParallelize]
[TestClass]
public class ComputeStaticWebAssetsTargetPathsMultiThreadingTest
{
    [TestMethod]
    public void ResolvesRelativeContentRootAgainstTaskEnvironmentProjectDirectoryNotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ComputeStaticWebAssetsTargetPathsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeContentRoot = "wwwroot";

        var projectAbsoluteContentRoot = Path.GetFullPath(Path.Combine(projectDir, relativeContentRoot));
        var spawnAbsoluteContentRoot = Path.GetFullPath(Path.Combine(spawnDir, relativeContentRoot));
        projectAbsoluteContentRoot.Should().NotBe(spawnAbsoluteContentRoot,
            "the test setup must place project and decoy in different parents so a relative path resolves differently against each");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsTargetPaths
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = [CreateCandidateWithRelativeContentRoot(Path.Combine(relativeContentRoot, "candidate.js"), relativeContentRoot)],
                PathPrefix = "wwwroot",
            };

            var result = task.Execute();

            result.Should().Be(true, "the task must run to completion when TaskEnvironment.ProjectDirectory differs from the process CWD");
            errorMessages.Should().BeEmpty();
            task.AssetsWithTargetPath.Should().ContainSingle();

            // The relative ContentRoot must be absolutized against the task's ProjectDirectory,
            // not the process current working directory (the decoy). This is the multithreaded-safe behavior.
            var contentRoot = task.AssetsWithTargetPath[0].GetMetadata("ContentRoot");
            contentRoot.Should().StartWith(projectAbsoluteContentRoot);
            contentRoot.Should().NotStartWith(spawnAbsoluteContentRoot);

            // The computed TargetPath is unaffected by the ContentRoot resolution.
            task.AssetsWithTargetPath[0].GetMetadata("TargetPath").Should().Be(Path.Combine("wwwroot", "candidate.js"));
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

    private static ITaskItem CreateCandidateWithRelativeContentRoot(string itemSpec, string relativeContentRoot)
    {
        // Intentionally skips Normalize() so the relative ContentRoot reaches the task unmodified.
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = "MyPackage",
            SourceType = "Discovered",
            ContentRoot = relativeContentRoot,
            BasePath = "base",
            RelativePath = "candidate.js",
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
