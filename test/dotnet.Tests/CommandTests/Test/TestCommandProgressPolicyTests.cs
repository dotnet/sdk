// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public sealed class TestCommandProgressPolicyTests : SdkTest
{
    [TestMethod]
    [DataRow("--progress off")]
    [DataRow("--progress=FaLsE")]
    [DataRow("--progress:disable")]
    [DataRow("--progress 0")]
    [DataRow("--no-progress")]
    public void SharedProgress_LaunchProfileOptOut_DisablesProgress(string arguments)
    {
        TestModule module = CreateTestModule(launchProfileArguments: arguments);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [module],
            CreateBuildOptions([])).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("--progress off")]
    [DataRow("--no-progress")]
    public void SharedProgress_ModuleRunArgumentsOptOut_DisablesProgress(string arguments)
    {
        TestModule module = CreateTestModule(arguments: arguments);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [module],
            CreateBuildOptions([])).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("forwarded")]
    [DataRow("module")]
    [DataRow("profile")]
    public void SharedProgress_RelativeResponseFile_UsesEachModuleWorkingDirectory(string argumentSource)
    {
        string root = TestAssetsManager.CreateTestDirectory(identifier: argumentSource).Path;
        string enabledDirectory = Directory.CreateDirectory(Path.Combine(root, "enabled module")).FullName;
        string disabledDirectory = Directory.CreateDirectory(Path.Combine(root, "disabled module")).FullName;
        File.WriteAllText(Path.Combine(enabledDirectory, "progress.rsp"), "--progress on");
        File.WriteAllText(Path.Combine(disabledDirectory, "progress.rsp"), "--progress off");
        BuildOptions buildOptions = CreateBuildOptions(argumentSource == "forwarded" ? ["@progress.rsp"] : []);
        TestModule enabledModule = CreateModule(enabledDirectory);
        TestModule disabledModule = CreateModule(disabledDirectory);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [enabledModule],
            buildOptions).Should().BeTrue();
        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [disabledModule],
            buildOptions).Should().BeFalse();
        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [enabledModule, disabledModule],
            buildOptions).Should().BeFalse();

        TestModule CreateModule(string workingDirectory)
            => CreateTestModule(
                arguments: argumentSource == "module" ? "@progress.rsp" : null,
                launchProfileArguments: argumentSource == "profile" ? "@progress.rsp" : null,
                workingDirectory: workingDirectory);
    }

    [TestMethod]
    [DataRow("--progress off")]
    [DataRow("--no-progress")]
    public void SharedProgress_SuppressedLaunchProfileArguments_AreIgnored(string arguments)
    {
        TestModule module = CreateTestModule(launchProfileArguments: arguments);
        BuildOptions buildOptions = CreateBuildOptions([], noLaunchProfileArguments: true);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [module],
            buildOptions).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SharedProgress_MixedModulePolicies_DisableProgressRegardlessOfOrder(bool disabledModuleFirst)
    {
        TestModule enabledModule = CreateTestModule(arguments: "--progress on");
        TestModule disabledModule = CreateTestModule(launchProfileArguments: "--progress off");
        TestModule[] modules = disabledModuleFirst
            ? [disabledModule, enabledModule]
            : [enabledModule, disabledModule];

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            modules,
            CreateBuildOptions([])).Should().BeFalse();
    }

    [TestMethod]
    public void SharedProgress_AllModulesEnabled_EnablesProgress()
    {
        TestModule[] modules =
        [
            CreateTestModule(),
            CreateTestModule(arguments: "--progress on"),
            CreateTestModule(launchProfileArguments: "--progress auto"),
        ];

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            modules,
            CreateBuildOptions([])).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("--progress on")]
    [DataRow("--progress=true")]
    [DataRow("--progress:enable")]
    [DataRow("--progress 1")]
    [DataRow("--progress auto")]
    [DataRow("--progress ON")]
    public void SharedProgress_ExplicitProgress_OverridesLegacyOptOut(string arguments)
    {
        TestModule module = CreateTestModule(
            arguments: "--no-progress",
            launchProfileArguments: arguments);
        BuildOptions buildOptions = CreateBuildOptions(["--no-progress"]);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: true,
            [module],
            buildOptions).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("--progress off", "on", "--progress on", false)]
    [DataRow("--progress on", "off", "--progress off", true)]
    [DataRow(null, "off", "--progress on", false)]
    [DataRow(null, "on", "--progress off", true)]
    public void SharedProgress_ArgumentSources_PreserveExistingPrecedence(
        string? moduleArguments,
        string forwardedValue,
        string launchProfileArguments,
        bool expected)
    {
        TestModule module = CreateTestModule(moduleArguments, launchProfileArguments);
        BuildOptions buildOptions = CreateBuildOptions(["--progress", forwardedValue]);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress: false,
            [module],
            buildOptions).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(false, null, true)]
    [DataRow(true, null, false)]
    [DataRow(false, "off", false)]
    [DataRow(true, "on", true)]
    [DataRow(true, "auto", true)]
    public void SharedProgress_NoModules_PreservesFallbackPolicy(bool noProgress, string? progressValue, bool expected)
    {
        BuildOptions buildOptions = CreateBuildOptions(progressValue is null ? [] : ["--progress", progressValue]);

        MicrosoftTestingPlatformTestCommand.GetSharedProgressEnabled(
            noProgress,
            [],
            buildOptions).Should().Be(expected);
    }

    private static TestModule CreateTestModule(
        string? arguments = null,
        string? launchProfileArguments = null,
        string? workingDirectory = null)
        => new(
            new RunProperties("dotnet", $"exec tests.dll {arguments}", workingDirectory),
            ProjectFullPath: "tests.csproj",
            TargetFramework: null,
            IsTestingPlatformApplication: true,
            LaunchSettings: launchProfileArguments is null ? null : new ProjectLaunchProfile
            {
                CommandLineArgs = launchProfileArguments,
                EnvironmentVariables = ImmutableDictionary<string, string>.Empty,
            },
            TargetPath: "tests.dll",
            DotnetRootArchVariableName: null,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty);

    private static BuildOptions CreateBuildOptions(
        ImmutableArray<string> testApplicationArguments,
        bool noLaunchProfileArguments = false)
        => new(
            new PathOptions(
                ProjectOrSolutionPath: null,
                SolutionPath: null,
                TestModules: null,
                ResultsDirectoryPath: null,
                ResultsDirectoryLayout.Flat,
                ConfigFilePath: null,
                DiagnosticOutputDirectoryPath: null),
            HasNoRestore: false,
            HasNoBuild: false,
            Verbosity: null,
            NoLaunchProfile: false,
            NoLaunchProfileArguments: noLaunchProfileArguments,
            testApplicationArguments,
            MSBuildArgs: [],
            Device: null,
            ListDevices: false,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty);
}
