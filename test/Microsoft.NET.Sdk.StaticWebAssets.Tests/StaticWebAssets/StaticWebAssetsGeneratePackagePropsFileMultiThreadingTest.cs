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

namespace Microsoft.AspNetCore.Razor.Tasks;

[TestClass]
public class StaticWebAssetsGeneratePackagePropsFileMultiThreadingTest
{
    [TestMethod]
    [DataRow("output/props.xml")]
    [DataRow(" ")]
    public void WritesPropsFileRelativeToTaskEnvironmentProjectDirectory(string relativeBuildTargetPath)
    {
        if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(relativeBuildTargetPath))
        {
            return;
        }

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
                BuildTargetPath = relativeBuildTargetPath
            };

            task.Execute().Should().BeTrue();

            var expectedPath = Path.Combine(projectDir, relativeBuildTargetPath);
            File.Exists(expectedPath).Should().BeTrue("the file should be written under the project dir, not the process CWD");

            var incorrectPath = Path.Combine(spawnDir, relativeBuildTargetPath);
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
