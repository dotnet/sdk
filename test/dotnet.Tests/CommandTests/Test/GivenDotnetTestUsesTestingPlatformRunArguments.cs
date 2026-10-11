// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class GivenDotnetTestUsesTestingPlatformRunArguments : SdkTest
{
    [TestMethod]
    public void ItSelectsTestOutputsAfterAllCanonicalRunArgumentProviders()
    {
        TestAsset asset = CreateProject(
            """
            <Target Name="_LateRunArgumentProvider" AfterTargets="ComputeRunArguments">
              <PropertyGroup>
                <_OriginalRunCommand>$(RunCommand)</_OriginalRunCommand>
                <_OriginalRunArguments>$(RunArguments)</_OriginalRunArguments>
                <_OriginalRunWorkingDirectory>$(RunWorkingDirectory)</_OriginalRunWorkingDirectory>
                <RunCommand>missing-canonical-command</RunCommand>
                <RunArguments>invalid-canonical-arguments</RunArguments>
                <RunWorkingDirectory>missing-canonical-directory</RunWorkingDirectory>
              </PropertyGroup>
            </Target>
            <Target Name="ComputeTestingPlatformRunArguments">
              <Error Condition="'$(TestingPlatformRunArgumentsVersion)' != '1'" Text="Missing SDK launcher capability." />
              <Error Condition="'$(TestingPlatformRunCommand)' != 'missing-canonical-command'" Text="Canonical provider did not finish before test selection." />
              <Error Condition="'$(TestingPlatformRunArguments)' != 'invalid-canonical-arguments'" Text="Canonical arguments were not seeded." />
              <Error Condition="'$(TestingPlatformRunWorkingDirectory)' != 'missing-canonical-directory'" Text="Canonical directory was not seeded." />
              <PropertyGroup>
                <TestingPlatformRunCommand>$(_OriginalRunCommand)</TestingPlatformRunCommand>
                <TestingPlatformRunArguments>$(_OriginalRunArguments)</TestingPlatformRunArguments>
                <TestingPlatformRunWorkingDirectory>$(_OriginalRunWorkingDirectory)</TestingPlatformRunWorkingDirectory>
              </PropertyGroup>
              <Error Condition="'$(RunCommand)' != 'missing-canonical-command'" Text="Canonical command was overwritten." />
            </Target>
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .Execute()
            .Should().Pass();
    }

    [TestMethod]
    public void ItReportsTestRunArgumentTargetFailures()
    {
        TestAsset asset = CreateProject(
            """
            <Target Name="ComputeTestingPlatformRunArguments">
              <Error Text="Test launcher selection failed as requested." />
            </Target>
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandEvaluationExceptionBuildFailed,
                "ComputeTestingPlatformRunArguments"))
            .And.HaveStdOutContaining("Test launcher selection failed as requested.");
    }

    [TestMethod]
    public void ItRejectsProvidersWithoutTheirSelectionTarget()
    {
        TestAsset asset = CreateProject(
            """
            <PropertyGroup>
              <ComputeTestingPlatformRunArgumentsDependsOn>_SelectTestLauncher</ComputeTestingPlatformRunArgumentsDependsOn>
            </PropertyGroup>
            <Target Name="_SelectTestLauncher" />
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandEvaluationExceptionBuildFailed,
                "ComputeTestingPlatformRunArguments"));
    }

    [TestMethod]
    public void ItRejectsAnExplicitlyEmptyTestRunCommand()
    {
        TestAsset asset = CreateProject(
            """
            <Target Name="ComputeTestingPlatformRunArguments">
              <PropertyGroup>
                <TestingPlatformRunCommand />
              </PropertyGroup>
            </Target>
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .Execute()
            .Should().Fail();
    }

    [TestMethod]
    public void ItPreservesExplicitlyEmptyTestRunArguments()
    {
        TestAsset asset = CreateProject(
            """
            <PropertyGroup>
              <UseAppHost>false</UseAppHost>
            </PropertyGroup>
            <Target Name="ComputeTestingPlatformRunArguments">
              <PropertyGroup>
                <TestingPlatformRunArguments />
              </PropertyGroup>
            </Target>
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .Execute()
            .Should().Fail();
    }

    [TestMethod]
    public void DotnetRunDoesNotInvokeTheTestOnlyTarget()
    {
        TestAsset asset = CreateProject(
            """
            <Target Name="ComputeTestingPlatformRunArguments">
              <Error Text="dotnet run must not select a test-only launcher." />
            </Target>
            """);

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(asset.Path)
            .Execute()
            .Should().Pass();
    }

    private TestAsset CreateProject(string targets)
    {
        TestAsset asset = TestAssetsManager.CopyTestAsset("TestProjectWithTests", identifier: Guid.NewGuid().ToString("N"))
            .WithSource();
        string projectPath = Path.Combine(asset.Path, "TestProject.csproj");
        File.WriteAllText(
            projectPath,
            File.ReadAllText(projectPath).Replace("</Project>", targets + Environment.NewLine + "</Project>"));
        return asset;
    }
}
