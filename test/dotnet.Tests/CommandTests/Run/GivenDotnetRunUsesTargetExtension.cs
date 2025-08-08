// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions.Execution;

namespace Microsoft.DotNet.Cli.Run.Tests;

/// <summary>
/// These tests cover the behavior of <c>dotnet run</c> when invoking the new <c>ComputeRunArguments</c> target.
/// </summary>
public class GivenDotnetRunUsesTargetExtension : SdkTest
{

    public GivenDotnetRunUsesTargetExtension(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void ItInvokesTheTargetAndRunsCustomLogic()
    {
        var testAppName = "DotnetRunTargetExtension";
        var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
            .WithSource();
        var testProjectDirectory = testInstance.Path;

        var runResult = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testProjectDirectory)
            .Execute();

        using var scope = new AssertionScope("run outputs");

        // the run command should run the app in the test project directory,
        // so we should both check args and working directory
        runResult.Should()
            .Pass();

        runResult.Should()
            .HaveStdOutContaining("Args: extended");

        runResult.Should()
            .HaveStdOutContaining($"CWD: {testProjectDirectory}");
    }

    [Fact]
    public void ItShowsErrorsDuringCustomLogicExecution()
    {
        var testAppName = "DotnetRunTargetExtensionWithError";
        var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
            .WithSource();
        var testProjectDirectory = testInstance.Path;

        var runResult = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testProjectDirectory)
            .Execute();

        using var scope = new AssertionScope("run outputs");

        // the run command should run the app in the test project directory,
        // so we should both check args and working directory
        runResult.Should()
            .Fail();

        runResult.Should()
            .HaveStdOutContaining("MYAPP001");

        runResult.Should()
            .HaveStdOutContaining($"MYAPP002");

    }
}
