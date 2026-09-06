// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide: the MSTest.Sdk project sets
// MSTestParallelizeScope=None, which emits [assembly: DoNotParallelize] and runs
// tests sequentially, isolating the process-CWD mutation this test performs.
[TestClass]
public class DefineStaticWebAssetsMultiThreadingTest
{
    // Relative candidate identities and a relative ContentRoot must be absolutized against
    // TaskEnvironment.ProjectDirectory, not the process current directory.
    [TestMethod]
    public void ResolvesCandidateIdentityAndContentRootRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(DefineStaticWebAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var decoyCwd = Path.Combine(testRoot, "decoy");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(decoyCwd);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(decoyCwd);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var candidate = new TaskItem(Path.Combine("wwwroot", "candidate.js"), new Dictionary<string, string>
            {
                ["RelativePath"] = "",
                ["TargetPath"] = "",
                ["Link"] = "",
                ["CopyToOutputDirectory"] = "",
                ["CopyToPublishDirectory"] = "",
                // Provide these so the task does not access the disk to compute them.
                ["Integrity"] = "integrity",
                ["Fingerprint"] = "fingerprint",
                ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                ["FileLength"] = "10",
            });

            var task = new DefineStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                // Do not touch the disk when resolving file details.
                TestResolveFileDetails = (identity, originalItemSpec) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
                CandidateAssets = [candidate],
                RelativePathPattern = "wwwroot\\**",
                SourceType = "Discovered",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path",
            };

            task.Execute().Should().BeTrue(string.Join("; ", errorMessages));

            task.Assets.Should().HaveCount(1);
            var asset = task.Assets[0];

            // The candidate identity is absolutized against the project directory, not the decoy CWD.
            asset.ItemSpec.Should().Be(Path.Combine(projectDir, "wwwroot", "candidate.js"));
            asset.ItemSpec.Should().NotStartWith(decoyCwd);

            // The relative ContentRoot is normalized against the project directory, not the decoy CWD.
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot))
                .Should().Be(Path.Combine(projectDir, "wwwroot") + Path.DirectorySeparatorChar);
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

    [TestMethod]
    public void CacheInvalidatesRelativeCandidateWhenAssetFileChanges_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(DefineStaticWebAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var decoyCwd = Path.Combine(testRoot, "decoy");
        Directory.CreateDirectory(Path.Combine(projectDir, "wwwroot"));
        Directory.CreateDirectory(decoyCwd);
        var assetPath = Path.Combine(projectDir, "wwwroot", "candidate.js");
        File.WriteAllText(assetPath, "initial");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(decoyCwd);

            var firstTask = CreateCachedTask(projectDir, CreateCandidate(), "define.cache.json");
            firstTask.Execute().Should().BeTrue();
            firstTask.Assets[0].GetMetadata(nameof(StaticWebAsset.FileLength))
                .Should().Be(new FileInfo(assetPath).Length.ToString(CultureInfo.InvariantCulture));

            File.WriteAllText(assetPath, "updated content with a different length");

            var secondTask = CreateCachedTask(projectDir, CreateCandidate(), "define.cache.json");
            secondTask.Execute().Should().BeTrue();
            secondTask.Assets[0].GetMetadata(nameof(StaticWebAsset.FileLength))
                .Should().Be(new FileInfo(assetPath).Length.ToString(CultureInfo.InvariantCulture));
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

    private static TaskItem CreateCandidate() => new(Path.Combine("wwwroot", "candidate.js"), new Dictionary<string, string>
    {
        ["RelativePath"] = "",
        ["TargetPath"] = "",
        ["Link"] = "",
        ["CopyToOutputDirectory"] = "",
        ["CopyToPublishDirectory"] = "",
    });

    private static DefineStaticWebAssets CreateCachedTask(string projectDir, ITaskItem candidate, string cacheManifestPath)
    {
        var buildEngine = new Mock<IBuildEngine>();
        return new DefineStaticWebAssets
        {
            BuildEngine = buildEngine.Object,
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
            CandidateAssets = [candidate],
            RelativePathPattern = "wwwroot\\**",
            SourceType = "Discovered",
            SourceId = "MyProject",
            ContentRoot = "wwwroot",
            BasePath = "_content/Path",
            CacheManifestPath = cacheManifestPath,
        };
    }
}
