// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests;

/// <summary>
/// Integration tests for target framework selection in dotnet run
/// </summary>
public class GivenDotnetRunSelectsTargetFramework : SdkTest
{
    public GivenDotnetRunSelectsTargetFramework(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void ItRunsMultiTFMProjectWhenFrameworkIsSpecified()
    {
        var testInstance = _testAssetsManager.CopyTestAsset(
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
        var testInstance = _testAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--no-interactive");

        result.Should().Fail();
        
        if (!TestContext.IsLocalized())
        {
            result.Should().HaveStdErrContaining("Unable to run your project");
            result.Should().HaveStdErrContaining("multiple frameworks");
            result.Should().HaveStdErrContaining("--framework");
        }
    }

    [Fact]
    public void ItRunsWithShortFormFrameworkOption()
    {
        var testInstance = _testAssetsManager.CopyTestAsset(
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
        var testInstance = _testAssetsManager.CopyTestAsset(
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
    public void ItPrefersExplicitFrameworkOptionOverProperty()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunMultiTarget")
            .WithSource();

        // Pass both --framework and -p:TargetFramework
        // The --framework option should take precedence
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute(
                "--framework", ToolsetInfo.CurrentTargetFramework,
                "-p:TargetFramework=net8.0")
            .Should().Pass()
            .And.HaveStdOutContaining($"Target Framework: {ToolsetInfo.CurrentTargetFrameworkMoniker}");
    }

    [Fact]
    public void ItShowsErrorMessageWithAvailableFrameworks_InNonInteractiveMode()
    {
        var testInstance = _testAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--no-interactive");

        result.Should().Fail();
        
        if (!TestContext.IsLocalized())
        {
            // Should show the "Unable to run your project" message
            result.Should().HaveStdErrContaining("Unable to run your project");
            result.Should().HaveStdErrContaining("Your project targets multiple frameworks");
            
            // Should show available frameworks
            result.Should().HaveStdErrContaining("Available target frameworks:");
            
            // Should show example command
            result.Should().HaveStdErrContaining("Example:");
            result.Should().HaveStdErrContaining("dotnet run --framework");
        }
    }

    [Fact]
    public void ItFailsForMultiTargetedAppWithoutFramework_InNonInteractiveMode()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunMultiTarget")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--no-interactive");

        result.Should().Fail();
        
        if (!TestContext.IsLocalized())
        {
            result.Should().HaveStdErrContaining("Unable to run your project");
            result.Should().HaveStdErrContaining("multiple frameworks");
        }
    }

    [Theory]
    [InlineData("net8.0", ".NETCoreApp,Version=v8.0")]
    [InlineData("net9.0", ".NETCoreApp,Version=v9.0")]
    [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkMoniker)]
    public void ItRunsDifferentFrameworksInMultiTargetedApp(string targetFramework, string expectedMoniker)
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunMultiTarget")
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
        var testInstance = _testAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("-p:TargetFramework=", "--no-interactive"); // Empty string

        result.Should().Fail();
        
        if (!TestContext.IsLocalized())
        {
            result.Should().HaveStdErrContaining("Unable to run your project");
            result.Should().HaveStdErrContaining("multiple frameworks");
        }
    }

    [Fact]
    public void ItTreatsWhitespaceFrameworkSpecificationAsNotSpecified()
    {
        var testInstance = _testAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("-p:TargetFramework=   ", "--no-interactive"); // Whitespace

        result.Should().Fail();
        
        if (!TestContext.IsLocalized())
        {
            result.Should().HaveStdErrContaining("Unable to run your project");
            result.Should().HaveStdErrContaining("multiple frameworks");
        }
    }
}
