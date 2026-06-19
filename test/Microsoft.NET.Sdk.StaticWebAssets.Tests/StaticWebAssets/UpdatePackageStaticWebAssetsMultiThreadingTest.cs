// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide via
// [assembly:CollectionBehavior(DisableTestParallelization = true)] in
// LegacyStaticWebAssetsV1IntegrationTest.cs, which already isolates the
// process-CWD mutation this test performs.
[TestClass]
[DoNotParallelize]
public class UpdatePackageStaticWebAssetsMultiThreadingTest
{
    // Relative ContentRoot should resolve against TaskEnvironment.ProjectDirectory, not process CWD.
    [TestMethod]
    public void NormalizesContentRootRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(UpdatePackageStaticWebAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        // Two distinct content-root targets, one under each subtree, so we can tell which root
        // the relative ContentRoot was absolutized against just by inspecting the output string.
        var projectContentRoot = Path.Combine(projectDir, "wwwroot");
        var spawnContentRoot = Path.Combine(spawnDir, "wwwroot");
        Directory.CreateDirectory(projectContentRoot);
        Directory.CreateDirectory(spawnContentRoot);

        // Provide a real backing file so StaticWebAsset.ApplyDefaults().ResolveFile(Identity, ...)
        // doesn't throw. The Identity is absolute and the file exists, so it does not exercise
        // the deeper hazard — only ContentRoot does, and we set it to a relative path on purpose.
        var assetIdentity = Path.Combine(projectContentRoot, "site.css");
        File.WriteAllText(assetIdentity, "body{}");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var packageAsset = new TaskItem(assetIdentity, new Dictionary<string, string>
            {
                [nameof(StaticWebAsset.SourceId)] = "SomeOtherPackage",
                [nameof(StaticWebAsset.SourceType)] = StaticWebAsset.SourceTypes.Package,
                [nameof(StaticWebAsset.ContentRoot)] = "wwwroot",
                [nameof(StaticWebAsset.BasePath)] = "_content/SomeOtherPackage",
                [nameof(StaticWebAsset.RelativePath)] = "site.css",
                [nameof(StaticWebAsset.AssetKind)] = StaticWebAsset.AssetKinds.All,
                [nameof(StaticWebAsset.AssetMode)] = StaticWebAsset.AssetModes.All,
                [nameof(StaticWebAsset.AssetRole)] = StaticWebAsset.AssetRoles.Primary,
                [nameof(StaticWebAsset.CopyToOutputDirectory)] = StaticWebAsset.AssetCopyOptions.Never,
                [nameof(StaticWebAsset.CopyToPublishDirectory)] = StaticWebAsset.AssetCopyOptions.PreserveNewest,
                [nameof(StaticWebAsset.OriginalItemSpec)] = assetIdentity,
                [nameof(StaticWebAsset.Fingerprint)] = "deadbeef",
                [nameof(StaticWebAsset.Integrity)] = "sha256-fake",
                [nameof(StaticWebAsset.FileLength)] = "6",
                [nameof(StaticWebAsset.LastWriteTime)] = DateTimeOffset.UtcNow.ToString("o"),
            });

            var task = new UpdatePackageStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = new ITaskItem[] { packageAsset },
            };

            task.Execute().Should().BeTrue(string.Join("; ", errorMessages));

            task.UpdatedAssets.Should().HaveCount(1);
            var actualContentRoot = task.UpdatedAssets[0].GetMetadata(nameof(StaticWebAsset.ContentRoot));

            actualContentRoot.Should().Be(projectContentRoot + Path.DirectorySeparatorChar);
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
