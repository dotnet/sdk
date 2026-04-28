// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Run.Tests;

/// <summary>
/// Integration tests for target framework selection in dotnet run
/// </summary>
public partial class GivenDotnetRunSelectsTargetFramework : SdkTest
{
    public GivenDotnetRunSelectsTargetFramework(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void ItRunsMultiTFMProjectWhenFrameworkIsSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
            .Should().Pass()
            .And.HaveStdOutContaining("This string came from the test library!");
    }

    [Fact]
    public void ItFailsInNonInteractiveMode_WhenMultiTFMProjectHasNoFrameworkSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
    }

    [Fact]
    public void ItRunsWithShortFormFrameworkOption()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .Execute("-f", ToolsetInfo.CurrentTargetFramework)
            .Should().Pass()
            .And.HaveStdOutContaining("This string came from the test library!");
    }

    [Fact]
    public void ItRunsWithFrameworkPropertySyntax()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .Execute("-p:TargetFramework=" + ToolsetInfo.CurrentTargetFramework)
            .Should().Pass()
            .And.HaveStdOutContaining("This string came from the test library!");
    }

    [Fact]
    public void ItShowsErrorMessageWithAvailableFrameworks_InNonInteractiveMode()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
    }

    [Fact]
    public void ItFailsForMultiTargetedAppWithoutFramework_InNonInteractiveMode()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetRunMultiTarget")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
    }

    [Theory]
    [InlineData("net8.0", ".NETCoreApp,Version=v8.0")]
    [InlineData("net9.0", ".NETCoreApp,Version=v9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkMoniker)]
    public void ItRunsDifferentFrameworksInMultiTargetedApp(string targetFramework, string expectedMoniker)
    {
        // Skip net8.0 and net9.0 on arm64 as they may not be available on CI
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
            (targetFramework == "net8.0" || targetFramework == "net9.0"))
        {
            return;
        }

        var testInstance = TestAssetsManager.CopyTestAsset("DotnetRunMultiTarget")
            .WithSource();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", targetFramework)
            .Should().Pass()
            .And.HaveStdOutContaining($"Target Framework: {expectedMoniker}");
    }

    [Fact]
    public void ItTreatsEmptyFrameworkSpecificationAsNotSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("-p:TargetFramework=", "--no-interactive"); // Empty string

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
    }

    [Fact]
    public void ItTreatsWhitespaceFrameworkSpecificationAsNotSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("-p:TargetFramework=   ", "--no-interactive"); // Whitespace

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
    }

    [Fact]
    public void ItAutoSelectsSingleFrameworkInTargetFrameworksProperty()
    {
        // Reuse the DotnetRunMultiTarget project and modify it to have only one framework
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetRunMultiTarget")
            .WithSource();

        // Read the existing .csproj file
        var projectPath = Path.Combine(testInstance.Path, "DotnetRunMultiTarget.csproj");
        var projectContent = File.ReadAllText(projectPath);

        // Replace TargetFrameworks with a single framework
        projectContent = TargetFrameworksRegex()
            .Replace(projectContent, $"<TargetFrameworks>{ToolsetInfo.CurrentTargetFramework}</TargetFrameworks>");
        File.WriteAllText(projectPath, projectContent);

        // Run without specifying --framework - it should auto-select the single framework
        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        result.Should().Pass()
            .And.HaveStdOutContaining($"Target Framework: {ToolsetInfo.CurrentTargetFrameworkMoniker}");
    }

    [GeneratedRegex(@"<TargetFrameworks>.*?</TargetFrameworks>")]
    private static partial Regex TargetFrameworksRegex();
}
