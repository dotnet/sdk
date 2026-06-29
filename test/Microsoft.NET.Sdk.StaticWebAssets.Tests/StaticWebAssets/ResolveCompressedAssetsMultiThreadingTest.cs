// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class ResolveCompressedAssetsMultiThreadingTest
{
    [TestMethod]
    public void ResolvesOutputPathRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ResolveCompressedAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeOutputPath = "compressed";
        var expectedOutputPath = Path.GetFullPath(Path.Combine(projectDir, relativeOutputPath));
        var decoyOutputPath = Path.GetFullPath(Path.Combine(spawnDir, relativeOutputPath));
        expectedOutputPath.Should().NotBe(decoyOutputPath,
            "the test setup must place project and decoy in different parents so the migration is actually exercised");

        var assetIdentity = Path.Combine(projectDir, "wwwroot", "js", "site.js");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ResolveCompressedAssets
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                OutputPath = relativeOutputPath,
                Formats = "gzip",
                IncludePatterns = "**/*.js",
                CandidateAssets = [CreateCandidate(assetIdentity, relativePath: "js/site.js")],
            };

            var result = task.Execute();

            result.Should().BeTrue();
            errorMessages.Should().BeEmpty();

            var compressed = task.AssetsToCompress.TakeWhile(a => a != null).ToArray();
            compressed.Should().ContainSingle();

            compressed[0].GetMetadata("ContentRoot").Should().Be(expectedOutputPath,
                "the compressed asset ContentRoot must be absolutized against TaskEnvironment.ProjectDirectory, not the process CWD");
            compressed[0].GetMetadata("ContentRoot").Should().NotBe(decoyOutputPath);
            compressed[0].ItemSpec.Should().StartWith(expectedOutputPath);
            compressed[0].ItemSpec.Should().NotStartWith(decoyOutputPath);
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

    private static ITaskItem CreateCandidate(string identity, string relativePath)
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
            ContentRoot = Path.GetDirectoryName(identity),
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
