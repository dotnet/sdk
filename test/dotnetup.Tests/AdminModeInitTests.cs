// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;
using Strings = Microsoft.DotNet.Tools.Bootstrapper.Strings;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminModeInitTests : IDisposable
{
    private readonly AdminModeTestEnvironment _environment = new();

    public void Dispose() => _environment.Dispose();

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow(DotnetAccessMode.None, DotnetAccessMode.Admin)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.Admin)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.None)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.Shell)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.Everywhere)]
    internal void AdminTransitions_ConfigureBeforeSavingWithoutResolvingInstalls(
        DotnetAccessMode previousMode, DotnetAccessMode selectedMode)
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = previousMode, UpdateNotifications = false });
        _environment.Store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = previousMode == DotnetAccessMode.Admin;
        var environment = CreateEnvironment();
        environment.BeforeEnvironmentChange = () =>
        {
            _environment.Policy.IsUnsignedDownloadBlocked().Should().Be(previousMode == DotnetAccessMode.Admin);
            DotnetupConfig.Read()!.AccessMode.Should().Be(previousMode);
        };
        var workflows = CreateWorkflows(environment, selectedMode);
        var command = new InitCommand(Parser.Parse(["init", "--shell", "bash"]), _environment.Policy);

        string output = Run(() => workflows.InitWalkthrough(command).Should().BeEmpty());

        DotnetupConfig.Read()!.AccessMode.Should().Be(selectedMode);
        DotnetupConfig.Read()!.UpdateNotifications.Should().BeFalse();
        _environment.Store.Writes.Should().BeEmpty();
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(1);
        environment.LastIncludeDotnetForTerminalProfileModifications.Should()
            .Be(selectedMode is DotnetAccessMode.Shell or DotnetAccessMode.Everywhere);
        output.Should().Contain("No .NET was installed.");
        output.Should().NotContain("Migrate system installs");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow(false)]
    [DataRow(true)]
    public void AdminSetup_DoesNotRequestPolicyElevation(bool cancelled)
    {
        var previous = new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = false };
        DotnetupConfig.Write(previous);
        var environment = CreateEnvironment();
        var elevator = _environment.CreateElevator(
            failure: cancelled ? new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled") : null,
            appliesChange: false);
        var workflows = CreateWorkflows(environment, DotnetAccessMode.Admin, elevator);
        var command = new InitCommand(Parser.Parse(["init", "--shell", "bash"]), _environment.Policy);

        string output = Run(() => workflows.InitWalkthrough(command).Should().BeEmpty());

        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        elevator.Operations.Should().BeEmpty();
        _environment.Store.Writes.Should().BeEmpty();
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(1);
        output.Should().Contain(Strings.AdminModeEnabled);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow(false)]
    [DataRow(true)]
    public void DryRun_DoesNotChangePolicyEnvironmentOrConfig(bool leavingAdmin)
    {
        var previous = new DotnetupConfigData
        {
            AccessMode = leavingAdmin ? DotnetAccessMode.Admin : DotnetAccessMode.Shell,
        };
        DotnetupConfig.Write(previous);
        var environment = CreateEnvironment();
        var elevator = _environment.CreateElevator();
        var workflows = CreateWorkflows(environment,
            leavingAdmin ? DotnetAccessMode.Shell : DotnetAccessMode.Admin, elevator);
        var command = new InitCommand(Parser.Parse(["init", "--dry-run"]), _environment.Policy);

        string output = Run(() => workflows.InitWalkthrough(command).Should().BeEmpty());

        DotnetupConfig.Read().Should().BeEquivalentTo(previous);
        elevator.Operations.Should().BeEmpty();
        _environment.Store.Writes.Should().BeEmpty();
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
        output.Should().Contain(Strings.InitConfigureOnlyDescription);
        output.Should().NotContain("SDK channel");
        output.Should().NotContain("Migrate system installs");
        output.Should().NotContain("Installs in");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void FirstUseInstall_SelectingAdmin_DoesNotContinueToGlobalJsonUpdate()
    {
        var environment = CreateEnvironment();
        var command = new UpdatingInstallCommand(
            Parser.Parse(["sdk", "install", "--interactive", "true", "--shell", "bash"]),
            _environment.Policy, environment);
        var workflow = new InstallWorkflow(command, new AdminInstallRootResolver(),
            CreateWorkflows(environment, DotnetAccessMode.Admin));

        Run(() => workflow.Execute([new MinimalInstallSpec(InstallComponent.SDK, null)]));

        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        environment.ApplyGlobalJsonModificationsCallCount.Should().Be(0);
        File.Exists(Path.Combine(_environment.TempRoot, "global.json")).Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void StoredAdmin_NonInteractiveSetup_LeavesPolicyUnchangedWithoutInstalling()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin, DotnetupOnPath = false });
        var environment = CreateEnvironment();
        var elevator = _environment.CreateElevator();
        var workflows = new InitWorkflows(environment, _environment.CreateSwitcher(elevator),
            new FakeEnvironmentStateInspector());
        var command = new InitCommand(Parser.Parse(["init", "--interactive", "false"]), _environment.Policy);

        Run(() => workflows.InitWalkthrough(command).Should().BeEmpty());

        elevator.Operations.Should().BeEmpty();
        _environment.Store.Writes.Should().BeEmpty();
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        environment.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void EnvironmentFailure_DoesNotSaveModeOrReportSuccess()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = false });
        var environment = CreateEnvironment();
        environment.BeforeEnvironmentChange = () => throw new IOException("Environment write failed");
        var workflows = CreateWorkflows(environment, DotnetAccessMode.Admin);
        var command = new InitCommand(Parser.Parse(["init"]), _environment.Policy);

        string output = Run(() =>
        {
            Action initialize = () => workflows.InitWalkthrough(command);
            initialize.Should().Throw<IOException>();
        });

        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Shell);
        output.Should().NotContain(Strings.AdminModeEnabled);
        output.Should().NotContain("Setup complete!");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void ExitForm_DoesNotChangePolicyEnvironmentOrConfig()
    {
        var environment = CreateEnvironment();
        var workflows = new InitWorkflows(environment)
        {
            FormSelectionOverride = (_, _) => null,
        };

        Run(() => workflows.InitWalkthrough(new InitCommand(Parser.Parse(["init"])))).Should()
            .NotContain("Setup complete!");

        DotnetupConfig.Exists().Should().BeFalse();
        _environment.Store.Writes.Should().BeEmpty();
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Form_AdminVisibilityChanges_PreserveFocusAndChannelSelection()
    {
        InitFormModel model = CreateModel(DotnetAccessMode.Shell);
        FormField channel = model.Fields.Single(field => field.Label == "SDK Channel");
        channel.SelectChoice(1);
        string? selectedChannel = model.SelectedChannel();
        FormField mode = model.Fields.Single(field => field.Label == "Access mode");
        int shellIndex = mode.SelectedIndex;
        int adminIndex = mode.Choices.Select((choice, index) => (choice, index))
            .Single(item => item.choice.Title == "Admin").index;
        var state = new InitFormState(model.Fields);
        while (!ReferenceEquals(state.FocusedField, mode))
        {
            state.MoveUp();
        }
        state.Enter();
        while (state.EditChoiceIndex < adminIndex)
        {
            state.MoveDown();
        }
        state.Enter();

        model.ConfigureOnly.Should().BeTrue();
        channel.IsVisible.Should().BeFalse();
        model.MigrateSelected().Should().BeFalse();
        state.FocusedField.Should().BeSameAs(mode);
        state.IsAcceptFocused.Should().BeFalse();
        state.IsDone.Should().BeFalse();

        state.Enter();
        while (state.EditChoiceIndex > shellIndex)
        {
            state.MoveUp();
        }
        state.Enter();

        model.ConfigureOnly.Should().BeFalse();
        channel.IsVisible.Should().BeTrue();
        model.SelectedChannel().Should().Be(selectedChannel);
        state.FocusedField.Should().BeSameAs(mode);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Form_LeavingSavedAdmin_RemainsConfigurationOnly()
    {
        InitFormModel model = CreateModel(DotnetAccessMode.Admin);
        FormField mode = model.Fields.Single(field => field.Label == "Access mode");
        mode.SelectChoice(mode.Choices.Select((choice, index) => (choice, index))
            .Single(item => item.choice.Title == "Shell").index);

        model.ConfigureOnly.Should().BeTrue();
        model.Fields.Single(field => field.Label == "SDK Channel").IsVisible.Should().BeFalse();
    }

    private MockDotnetInstallManager CreateEnvironment() => new(_environment.TempRoot, existingSystemInstalls: []);

    private InitWorkflows CreateWorkflows(
        MockDotnetInstallManager environment, DotnetAccessMode selectedMode, FakeAdminModeElevator? elevator = null) =>
        new(environment, _environment.CreateSwitcher(elevator ?? _environment.CreateElevator()),
            new FakeEnvironmentStateInspector())
        {
            FormSelectionOverride = (_, _) => new InitFormSelection(
                "invalid-channel-that-must-not-be-resolved", selectedMode, MigrateSystemInstalls: true),
        };

    private static InitFormModel CreateModel(DotnetAccessMode mode) =>
        InitFormModel.Create(new InitFormDefaults(
            new DotnetInstallRoot(Path.GetTempPath(), InstallerUtilities.GetDefaultInstallArchitecture()),
            mode, false, [], new DefaultChannelDisplay("latest", null),
            [new MinimalInstallSpec(InstallComponent.SDK, "latest")], null, null), shellProvider: null);

    private string Run(Action action)
    {
        string previousDirectory = Environment.CurrentDirectory;
        IAnsiConsole previousConsole = AnsiConsole.Console;
        using var output = new StringWriter();
        try
        {
            Environment.CurrentDirectory = _environment.TempRoot;
            AnsiConsole.Console = AdminModeSwitcherTests.CreateConsole(output);
            action();
            return output.ToString();
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            AnsiConsole.Console = previousConsole;
        }
    }

    private sealed class UpdatingInstallCommand(
        ParseResult result, DotnetupPolicy policy, IDotnetEnvironmentManager environment)
        : InstallCommand(result, "sdk/install", policy, environment)
    {
        public override bool UpdateGlobalJson => true;

        protected override void ExecuteCore() => throw new NotImplementedException();
    }
}
