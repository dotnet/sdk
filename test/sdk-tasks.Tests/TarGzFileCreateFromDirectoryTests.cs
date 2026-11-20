// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
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
        };

        GetCommandLine(task).Should().StartWith("--numeric-owner --owner=2001 --group=2001 -czf archive.tar.gz ");
    }

    private static string GetCommandLine(TarGzFileCreateFromDirectory task)
    {
        MethodInfo method = typeof(TarGzFileCreateFromDirectory).GetMethod(
            "GenerateCommandLineCommands",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (string)method.Invoke(task, null)!;
    }
}
