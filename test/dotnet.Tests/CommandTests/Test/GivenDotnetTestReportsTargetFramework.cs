// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public sealed class GivenDotnetTestReportsTargetFramework : SdkTest
{
    [TestMethod]
    public void MatchingRuntime_KeepsModuleIdentityCompact()
    {
        TestAsset asset = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .Execute("-c", "Debug", "--no-progress", "--show-test-results", "none");

        result.Should().Pass();
        AssertModuleIdentity(result.StdOut, ToolsetInfo.CurrentTargetFramework, expectDifferentRuntime: false);
    }

    [TestMethod]
    public void RollForward_PreservesTargetAndReportsExecutingRuntime()
    {
        TestAsset asset = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();
        asset.WithProjectChanges(project =>
        {
            XElement properties = project.Root!.Elements("PropertyGroup").First();
            properties.SetElementValue("TargetFramework", "net8.0");
            properties.SetElementValue("RollForward", "LatestMajor");
        });

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .WithEnvironmentVariable("DOTNET_ROLL_FORWARD_TO_PRERELEASE", "1")
            .Execute("-c", "Debug", "--no-progress", "--show-test-results", "none");

        result.Should().Pass();
        AssertModuleIdentity(result.StdOut, "net8.0", expectDifferentRuntime: true);
        if (!SdkTestContext.IsLocalized())
        {
            result.StdOut.Should().Contain($"runtime: {ToolsetInfo.CurrentTargetFramework}");
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NetFrameworkAndModernTargets_KeepSeparateModuleIdentities()
    {
        TestAsset asset = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();
        asset.WithProjectChanges(project =>
        {
            XElement properties = project.Root!.Elements("PropertyGroup").First();
            properties.Element("TargetFramework")!.Remove();
            properties.SetElementValue("TargetFrameworks", $"net472;{ToolsetInfo.CurrentTargetFramework}");
            properties.SetElementValue("LangVersion", "preview");
        });

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .Execute("-c", "Debug", "--no-progress", "--show-test-results", "none");

        result.Should().Pass();
        AssertModuleIdentity(result.StdOut, "net472", expectDifferentRuntime: null);
        AssertModuleIdentity(result.StdOut, ToolsetInfo.CurrentTargetFramework, expectDifferentRuntime: false);
    }

    private static void AssertModuleIdentity(string? output, string targetFramework, bool? expectDifferentRuntime)
    {
        Assert.IsNotNull(output);
        string runtimePattern = expectDifferentRuntime switch
        {
            true => @", [^)]+",
            false => string.Empty,
            null => @"(?:, [^)]+)?",
        };
        string identityPattern = $@"TestProject\.(?:exe|dll) \({Regex.Escape(targetFramework)}\|[a-z0-9]+{runtimePattern}\)";
        Regex.Matches(output, identityPattern).Should().HaveCount(2,
            "both the running-tests line and completion line must identify the actual target framework");
    }
}
