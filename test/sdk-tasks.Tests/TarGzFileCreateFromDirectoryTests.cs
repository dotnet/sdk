// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

[TestClass]
public class TarGzFileCreateFromDirectoryTests
{
    [TestMethod]
    public void ItPreservesTheDefaultTarArguments()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            SourceDirectory = "source",
            IsPigzAvailable = () => false,
        };

        GetCommandLine(task).Should().StartWith("-czf archive.tar.gz ");
    }

    [TestMethod]
    public void ItNormalizesOwnershipForSourceBuild()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            NormalizeSourceBuildOwnership = true,
            SourceDirectory = "source",
            IsPigzAvailable = () => false,
        };

        GetCommandLine(task).Should().StartWith("--numeric-owner --owner=2001 --group=2001 -czf archive.tar.gz ");
    }

    [TestMethod]
    public void ItUsesPigzWhenAvailable()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            SourceDirectory = "source",
            IsPigzAvailable = () => true,
        };

        GetCommandLine(task).Should().StartWith("--use-compress-program=pigz -cf archive.tar.gz ");
    }

    [TestMethod]
    public void ItUsesPigzWithSourceBuildOwnershipNormalization()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            NormalizeSourceBuildOwnership = true,
            SourceDirectory = "source",
            IsPigzAvailable = () => true,
        };

        GetCommandLine(task).Should().StartWith("--numeric-owner --owner=2001 --group=2001 --use-compress-program=pigz -cf archive.tar.gz ");
    }

    [TestMethod]
    public void ItFallsBackToGzipWhenUsePigzIsFalse()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            SourceDirectory = "source",
            UsePigz = false,
            IsPigzAvailable = () => true,
        };

        GetCommandLine(task).Should().StartWith("-czf archive.tar.gz ");
    }

    [TestMethod]
    public void ItResolvesRelativePathsUsingTaskEnvironment()
    {
        string projectDir = Path.Combine(Path.GetTempPath(), $"targz-mt-{Guid.NewGuid():N}");
        string sourceDir = Path.Combine(projectDir, "content");
        Directory.CreateDirectory(sourceDir);

        try
        {
            var taskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);
            var task = new TarGzFileCreateFromDirectory
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnvironment,
                DestinationArchive = "out.tar.gz",
                SourceDirectory = "content",
                IncludeBaseDirectory = true,
                IsPigzAvailable = () => false,
            };

            ValidateParameters(task).Should().BeTrue();
            task.SourceDirectory.Should().Be(sourceDir + Path.DirectorySeparatorChar);
            GetCommandLine(task).Should().Be($"-czf out.tar.gz --directory {projectDir} content  ");

            File.WriteAllText(Path.Combine(projectDir, "out.tar.gz"), string.Empty);
            ValidateParameters(task).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    private static string GetCommandLine(TarGzFileCreateFromDirectory task)
    {
        MethodInfo method = typeof(TarGzFileCreateFromDirectory).GetMethod(
            "GenerateCommandLineCommands",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (string)method.Invoke(task, null)!;
    }

    private static bool ValidateParameters(TarGzFileCreateFromDirectory task)
    {
        MethodInfo method = typeof(TarGzFileCreateFromDirectory).GetMethod(
            "ValidateParameters",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (bool)method.Invoke(task, null)!;
    }
}
