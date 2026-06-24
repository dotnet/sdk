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

public class DiscoverPrecompressedAssetsMultiThreadingTest
{
    [TestMethod]
    public void ResolvesContentRootRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(DiscoverPrecompressedAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeContentRoot = "wwwroot";
        var expectedContentRoot = Path.GetFullPath(Path.Combine(projectDir, relativeContentRoot)) + Path.DirectorySeparatorChar;
        var decoyContentRoot = Path.GetFullPath(Path.Combine(spawnDir, relativeContentRoot)) + Path.DirectorySeparatorChar;
        expectedContentRoot.Should().NotBe(decoyContentRoot,
            "the test setup must place project and decoy in different parents so the migration is actually exercised");

        var baseIdentity = Path.Combine(projectDir, "wwwroot", "js", "site.js");
        var compressedIdentity = baseIdentity + ".gz";

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverPrecompressedAssets
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                CandidateAssets =
                [
                    CreateCandidate(baseIdentity, relativePath: "js/site.js", relativeContentRoot),
                    CreateCandidate(compressedIdentity, relativePath: "js/site.js.gz", relativeContentRoot),
                ],
            };

            var result = task.Execute();

            result.Should().BeTrue();
            errorMessages.Should().BeEmpty();
            task.DiscoveredCompressedAssets.Should().ContainSingle();

            var discovered = task.DiscoveredCompressedAssets[0];
            discovered.GetMetadata("ContentRoot").Should().Be(expectedContentRoot,
                "ContentRoot must be absolutized against TaskEnvironment.ProjectDirectory, not the process CWD");
            discovered.GetMetadata("ContentRoot").Should().NotBe(decoyContentRoot);
            discovered.GetMetadata("RelatedAsset").Should().Be(baseIdentity);
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

    private static ITaskItem CreateCandidate(string identity, string relativePath, string contentRoot)
    {
        var asset = new StaticWebAsset
        {
            Identity = identity,
            RelativePath = relativePath,
            BasePath = "_content/Test",
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMergeSource = string.Empty,
            SourceId = "Test",
            CopyToOutputDirectory = StaticWebAsset.AssetCopyOptions.Never,
            Fingerprint = "fingerprint",
            RelatedAsset = string.Empty,
            ContentRoot = contentRoot,
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            Integrity = "integrity",
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetMergeBehavior = string.Empty,
            AssetTraitValue = string.Empty,
            AssetTraitName = string.Empty,
            OriginalItemSpec = identity,
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest,
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
        };
        return asset.ToTaskItem();
    }
}
