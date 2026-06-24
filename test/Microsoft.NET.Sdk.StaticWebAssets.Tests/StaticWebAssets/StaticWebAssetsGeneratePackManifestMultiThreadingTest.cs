// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks;

// This test mutates the process-wide current directory, so it must not run
// concurrently with other tests under MSTest's method-level parallelization.
[DoNotParallelize]
[TestClass]
public class StaticWebAssetsGeneratePackManifestMultiThreadingTest
{
    [TestMethod]
    public void WritesManifestRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(StaticWebAssetsGeneratePackManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var relativeManifestPath = Path.Combine("obj", "staticwebassets.pack.json");
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
        Directory.CreateDirectory(Path.Combine(spawnDir, "obj"));

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var task = new StaticWebAssetsGeneratePackManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = [CreateAsset("wwwroot/css/site.css", "css/site.css")],
                AdditionalPackageFiles = [],
                ManifestPath = relativeManifestPath
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

    [TestMethod]
    public void ResolvesExistingManifestCheckRelativeToProjectDirectory_NotProcessCurrentDirectory()
    {
        // Verifies that the File.Exists/File.ReadAllBytes change-detection probe in PersistManifest
        // is rooted against TaskEnvironment.ProjectDirectory rather than the process CWD. A decoy
        // manifest is planted in the process CWD at the same relative path. If the task read that
        // decoy as the "existing" manifest, it would either overwrite the decoy or skip writing the
        // project-dir file. The correct behavior is to ignore the decoy entirely: create the manifest
        // under the project dir and leave the decoy untouched.
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(StaticWebAssetsGeneratePackManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var relativeManifestPath = Path.Combine("obj", "staticwebassets.pack.json");
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
        Directory.CreateDirectory(Path.Combine(spawnDir, "obj"));

        const string decoyContents = "DECOY - must not be read or overwritten";
        var decoyPath = Path.Combine(spawnDir, relativeManifestPath);
        File.WriteAllText(decoyPath, decoyContents);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var task = new StaticWebAssetsGeneratePackManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = [CreateAsset("wwwroot/css/site.css", "css/site.css")],
                AdditionalPackageFiles = [],
                ManifestPath = relativeManifestPath
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

    [TestMethod]
    public void WritesManifestToAbsoluteManifestPath_WhenProcessCurrentDirectoryDiffers()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(StaticWebAssetsGeneratePackManifestMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
        Directory.CreateDirectory(spawnDir);
        var absoluteManifestPath = Path.Combine(projectDir, "obj", "staticwebassets.pack.json");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var task = new StaticWebAssetsGeneratePackManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = [CreateAsset("wwwroot/css/site.css", "css/site.css")],
                AdditionalPackageFiles = [],
                ManifestPath = absoluteManifestPath
            };

            task.Execute().Should().BeTrue();

            File.Exists(absoluteManifestPath).Should().BeTrue("an absolute manifest path must pass through unchanged regardless of the process CWD");
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

    private static ITaskItem CreateAsset(string itemSpec, string targetPath)
    {
        return new TaskItem(itemSpec, new Dictionary<string, string>
        {
            ["TargetPath"] = targetPath
        });
    }
}
