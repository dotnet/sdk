// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide: the MSTest.Sdk project sets
// MSTestParallelizeScope=None, which emits [assembly: DoNotParallelize] and runs
// tests sequentially, isolating the process-CWD mutation this test performs.
[TestClass]
public class GenerateStaticWebAssetEndpointsManifestMultiThreadingTest
{
    // Deliberately short: the layout below nests several levels under the test binary
    // directory and Directory.SetCurrentDirectory is still limited to MAX_PATH on Windows.
    private const string TestRootName = "GSWAEndpointsManifestMT";

    [TestMethod]
    public void WritesEndpointsManifestAndExclusionCacheRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        // Layout: place project and decoy in disjoint subtrees so that the same
        // relative path produces different absolute paths from each root.
        //   <testRoot>/p/output/       <-- TaskEnvironment.ProjectDirectory
        //   <testRoot>/d/s/            <-- process CWD (the "decoy")
        //   <testRoot>/d/s/output/     <-- where the pre-migration code would write
        // Path segments are kept short because Directory.SetCurrentDirectory is limited to
        // MAX_PATH on Windows even when long paths are otherwise enabled.
        var testRoot = Path.Combine(AppContext.BaseDirectory, TestRootName, Guid.NewGuid().ToString("N")[..8]);
        var projectDir = Path.Combine(testRoot, "p");
        var spawnDir = Path.Combine(testRoot, "d", "s");
        var projectOutputDir = Path.Combine(projectDir, "output");
        var spawnOutputDir = Path.Combine(spawnDir, "output");
        Directory.CreateDirectory(projectOutputDir);
        Directory.CreateDirectory(spawnOutputDir);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

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
            messages.Should().Contain($"Creating artifact because artifact file '{expectedManifest}' does not exist.");
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
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void WritesEndpointsManifestWhenPathIsWhitespace()
    {
        var projectDir = Path.Combine(AppContext.BaseDirectory, TestRootName, Guid.NewGuid().ToString("N")[..8], "ws");
        Directory.CreateDirectory(projectDir);

        try
        {
            var task = new GenerateStaticWebAssetEndpointsManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = [],
                Endpoints = [],
                Source = "MyProject",
                ManifestType = "Build",
                ManifestPath = " ",
            };

            task.Execute().Should().BeTrue();
            File.Exists(Path.Combine(projectDir, " ")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}
