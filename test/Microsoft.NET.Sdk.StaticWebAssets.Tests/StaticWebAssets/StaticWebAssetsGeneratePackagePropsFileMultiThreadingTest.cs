// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks;

[Collection("StaticWebAssetsGeneratePackagePropsFileMultiThreading")]
public class StaticWebAssetsGeneratePackagePropsFileMultiThreadingTest
{
    [Fact]
    public void WritesPropsFileRelativeToTaskEnvironmentProjectDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(StaticWebAssetsGeneratePackagePropsFileMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        var projectOutputDir = Path.Combine(projectDir, "output");
        var spawnOutputDir = Path.Combine(spawnDir, "output");
        Directory.CreateDirectory(projectOutputDir);
        Directory.CreateDirectory(spawnOutputDir);

        var currentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var buildEngine = new Mock<IBuildEngine>();
            var task = new StaticWebAssetsGeneratePackagePropsFile
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                PropsFileImport = "Microsoft.AspNetCore.StaticWebAssets.props",
                BuildTargetPath = Path.Combine("output", "props.xml")
            };

            task.Execute().Should().BeTrue();

            var expectedPath = Path.Combine(projectDir, "output", "props.xml");
            File.Exists(expectedPath).Should().BeTrue("the file should be written under the project dir, not the process CWD");

            var incorrectPath = Path.Combine(spawnDir, "output", "props.xml");
            File.Exists(incorrectPath).Should().BeFalse();
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
}

[CollectionDefinition("StaticWebAssetsGeneratePackagePropsFileMultiThreading", DisableParallelization = true)]
public class StaticWebAssetsGeneratePackagePropsFileMultiThreadingCollection
{
}
