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
public class DiscoverPrecompressedAssetsMultiThreadingTest
{
    [Fact]
    public void ResolvesContentRootRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        // Scope: verify that when CandidateAssets carry a *relative* ContentRoot, the asset
        // gets absolutized against TaskEnvironment.ProjectDirectory rather than the process
        // current directory. This is the MT contract introduced by marking the task as
        // [MSBuildMultiThreadableTask] + IMultiThreadableTask and flowing TaskEnvironment
        // into StaticWebAsset.FromTaskItemGroup.
        //
        // Layout (project and decoy must live in different subtrees so a relative
        // "wwwroot" resolves to *different* absolute paths against each one):
        //   <testRoot>/project/                  <-- TaskEnvironment.ProjectDirectory
        //   <testRoot>/decoy/spawn/              <-- process CWD (the decoy)
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

        // Identities are kept as absolute strings — the task's dictionary lookup in
        // FindRelatedAsset pairs the .gz candidate with its base file by trimming
        // the trailing 3 chars from Identity, so both items must share the same
        // base path prefix.
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
            discovered.GetMetadata("AssetRole").Should().Be(StaticWebAsset.AssetRoles.Alternative);
            discovered.GetMetadata("AssetTraitName").Should().Be("Content-Encoding");
            discovered.GetMetadata("AssetTraitValue").Should().Be("gzip");
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
