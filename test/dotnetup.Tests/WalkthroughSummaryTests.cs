// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class WalkthroughSummaryTests
{
    [TestMethod]
    public void BuildSummaryChoices_OrdersProceedCustomizeExit()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: false);

        choices.Should().HaveCount(3);
        choices[0].Decision.Should().Be(WalkthroughDecision.Proceed);
        choices[1].Decision.Should().Be(WalkthroughDecision.Customize);
        choices[2].Decision.Should().Be(WalkthroughDecision.Exit);
    }

    [TestMethod]
    public void BuildSummaryChoices_Unconfigured_FirstChoiceProceeds()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: false);

        choices[0].Option.Title.Should().Contain("proceed");
    }

    [TestMethod]
    public void BuildSummaryChoices_Configured_FirstChoiceOffersOverride()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: true);

        choices[0].Option.Title.Should().Contain("override");
    }

    [TestMethod]
    public void GetDefaultChoiceIndex_Unconfigured_DefaultsToProceed()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: false);

        int index = WalkthroughSummary.GetDefaultChoiceIndex(choices, isConfigured: false);

        choices[index].Decision.Should().Be(WalkthroughDecision.Proceed);
    }

    [TestMethod]
    public void GetDefaultChoiceIndex_Configured_DefaultsToCustomize()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: true);

        int index = WalkthroughSummary.GetDefaultChoiceIndex(choices, isConfigured: true);

        choices[index].Decision.Should().Be(WalkthroughDecision.Customize);
    }

    [TestMethod]
    public void BuildModeDescription_TerminalMode_ShowsProfilePathsAndInstallRoot()
    {
        var shellProvider = new TestShellProvider("profile-root", ".bashrc", ".profile");
        var plan = CreatePlan(DotnetAccessMode.Shell, shellProvider);

        string description = WalkthroughSummary.BuildModeDescription(plan);

        description.Should().Contain(DotnetupTheme.Accent("Terminal Mode"));
        foreach (string path in shellProvider.GetProfilePaths())
        {
            description.Should().Contain(DotnetupTheme.Accent(path));
        }

        description.Should().Contain(DotnetupTheme.Accent("PATH"));
        description.Should().Contain(DotnetupTheme.Accent("DOTNET_ROOT"));
        description.Should().Contain(DotnetupTheme.Accent(plan.InstallRoot.Path));
    }

    [TestMethod]
    public void BuildModeDescription_EverywhereMode_ShowsSystemEnvironmentVariablesAndInstallRoot()
    {
        var plan = CreatePlan(DotnetAccessMode.Everywhere, shellProvider: null);

        string description = WalkthroughSummary.BuildModeDescription(plan);

        description.Should().Contain(DotnetupTheme.Accent("Everywhere Mode"));
        description.Should().Contain(DotnetupTheme.Accent(
            Microsoft.DotNet.Tools.Bootstrapper.Strings.SummaryModeSystemEnvironmentVariables));
        description.Should().Contain(DotnetupTheme.Accent("PATH"));
        description.Should().Contain(DotnetupTheme.Accent("DOTNET_ROOT"));
        description.Should().Contain(DotnetupTheme.Accent(plan.InstallRoot.Path));
    }

    [TestMethod]
    public void BuildModeDescription_GlobalJsonInstallRoot_ShowsSourcePath()
    {
        const string globalJsonPath = "repo-root/global.json";
        var plan = CreatePlan(
            DotnetAccessMode.Everywhere,
            shellProvider: null,
            installRootGlobalJsonPath: globalJsonPath);

        string description = WalkthroughSummary.BuildModeDescription(plan);

        description.Should().Contain(DotnetupTheme.Dim($"(inferred from {globalJsonPath})"));
    }

    [TestMethod]
    public void BuildModeDescription_IsolationMode_ExplainsUnsupportedShell()
    {
        var plan = CreatePlan(DotnetAccessMode.None, shellProvider: null);

        string description = WalkthroughSummary.BuildModeDescription(plan);

        description.Should().Contain(DotnetupTheme.Accent("Isolation Mode"));
        description.Should().Contain("cannot be detected or is not supported");
        foreach (IEnvShellProvider supportedShell in ShellDetection.s_supportedShells)
        {
            description.Should().Contain(supportedShell.ArgumentName);
        }
    }

    [TestMethod]
    public void BuildModeDescription_TerminalModeWithoutShellProvider_ThrowsProductException()
    {
        var plan = CreatePlan(DotnetAccessMode.Shell, shellProvider: null);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(
            () => WalkthroughSummary.BuildModeDescription(plan));

        exception.ErrorCode.Should().Be(DotnetInstallErrorCode.InvalidModeSelection);
        ErrorCategoryClassifier.ClassifyInstallError(exception.ErrorCode).Should().Be(ErrorCategory.Product);
    }

    private static WalkthroughPlan CreatePlan(
        DotnetAccessMode accessMode,
        IEnvShellProvider? shellProvider,
        string? installRootGlobalJsonPath = null)
        => new(
            new DotnetInstallRoot("dotnetup-hive", InstallArchitecture.x64),
            accessMode,
            [],
            new DefaultChannelDisplay("latest", GlobalJsonPath: null),
            shellProvider,
            installRootGlobalJsonPath);
}
