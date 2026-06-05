// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks;

// Test parallelization is disabled assembly-wide via
// [assembly:CollectionBehavior(DisableTestParallelization = true)] in
// LegacyStaticWebAssetsV1IntegrationTest.cs, which already isolates the
// process-CWD mutation this test performs.
public class CollectStaticWebAssetsToCopyMultiThreadingTest
{
    [Fact]
    public void ResolvesOutputPathRelativeToTaskEnvironmentProjectDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(CollectStaticWebAssetsToCopyMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var refsDir = Path.Combine(projectDir, "refs");
        Directory.CreateDirectory(refsDir);
        Directory.CreateDirectory(spawnDir);

        var assetPath = Path.Combine(refsDir, "example.txt");
        File.WriteAllText(assetPath, "example");

        var currentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var buildEngine = new Mock<IBuildEngine>();
            var task = new CollectStaticWebAssetsToCopy
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                OutputPath = "output",
                Assets =
                [
                    CreateDiscoveredAsset(refsDir, assetPath)
                ]
            };

            task.Execute().Should().BeTrue();

            task.AssetsToCopy.Should().ContainSingle();
            task.AssetsToCopy[0].GetMetadata("TargetPath").Should().Be(
                Path.Combine(projectDir, "output", "example.txt"),
                "OutputPath should be resolved under the project dir, not the process CWD");
            task.AssetsToCopy[0].GetMetadata("TargetPath").Should().NotBe(Path.Combine(spawnDir, "output", "example.txt"));
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

    private static ITaskItem CreateDiscoveredAsset(string contentRoot, string assetPath)
    {
        var result = new StaticWebAsset
        {
            Identity = assetPath,
            SourceId = "TestProject",
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            ContentRoot = StaticWebAsset.NormalizeContentRootPath(contentRoot),
            BasePath = "/",
            RelativePath = "example.txt",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest,
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.Never,
            OriginalItemSpec = assetPath,
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            LastWriteTime = File.GetLastWriteTimeUtc(assetPath),
            FileLength = new FileInfo(assetPath).Length,
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }
}
