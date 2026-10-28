// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
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
    [DataRow(null, DotnetAccessMode.Admin)]
    [DataRow(DotnetAccessMode.None, DotnetAccessMode.Admin)]
    [DataRow(DotnetAccessMode.Everywhere, DotnetAccessMode.Admin)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.Admin)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.None)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.Shell)]
    [DataRow(DotnetAccessMode.Admin, DotnetAccessMode.Everywhere)]
    internal void AdminTransitions_InstallForSelectedModeAndSave(
        DotnetAccessMode? previousMode, DotnetAccessMode selectedMode)
    {
        if (previousMode is DotnetAccessMode saved)
        {
            DotnetupConfig.Write(new DotnetupConfigData { AccessMode = saved, UpdateNotifications = false });
        }
        var environment = CreateEnvironment();
        List<ResolvedInstallRequest>? installed = null;
        var workflows = CreateWorkflows(environment, selectedMode, requests => installed = requests);
        var command = new InitCommand(Parser.Parse(["init", "--shell", "bash"]), _environment.Policy);

        string output = Run(() => workflows.InitWalkthrough(command));

        bool admin = selectedMode == DotnetAccessMode.Admin;
        var request = installed.Should().ContainSingle().Subject.Request;
        request.Options.InstallMechanism.Should().Be(admin ? InstallMechanism.WindowsInstallerBundle : InstallMechanism.Archive);
        request.Options.Untracked.Should().Be(admin);
        request.Options.RequireSignature.Should().Be(admin);
        InstallPathClassifier.IsAdminInstallPath(request.InstallRoot.Path).Should().Be(admin);
        DotnetupConfig.Read()!.AccessMode.Should().Be(selectedMode);
        environment.LastIncludeDotnetForTerminalProfileModifications.Should()
            .Be(selectedMode is DotnetAccessMode.Shell or DotnetAccessMode.Everywhere);
        output.Should().Contain("Setup complete!");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void AdminSelection_DoesNotMigrateSystemInstalls()
    {
        var architecture = InstallerUtilities.GetDefaultInstallArchitecture();
        var defaults = new InitFormDefaults(
            new DotnetInstallRoot(_environment.TempRoot, architecture),
            DotnetAccessMode.Shell,
            MigrateSystemInstalls: true,
            Migrations: [new MigrationWorkflow.MigrationSelection(
                InstallComponent.SDK, new UpdateChannel("9.0.1xx"), new ReleaseVersion(PinnedVersion), architecture)],
            new DefaultChannelDisplay("latest", GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, "latest")],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        var command = new InitCommand(Parser.Parse(["init"]), _environment.Policy);
        InitExecutionPlan? plan = null;

        Run(() => plan = InitWorkflows.ResolveExecutionPlan(
            command, requests: null, defaults,
            new InitFormSelection(PinnedVersion, DotnetAccessMode.Admin, MigrateSystemInstalls: true)));

        plan!.Migrations.Should().BeEmpty();
        plan.AccessMode.Should().Be(DotnetAccessMode.Admin);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow(false)]
    [DataRow(true)]
    public void DryRun_DoesNotInstallOrChangeEnvironmentOrConfig(bool leavingAdmin)
    {
        var previous = new DotnetupConfigData
        {
            AccessMode = leavingAdmin ? DotnetAccessMode.Admin : DotnetAccessMode.Shell,
        };
        DotnetupConfig.Write(previous);
        var environment = CreateEnvironment();
        bool installed = false;
        var workflows = CreateWorkflows(environment,
            leavingAdmin ? DotnetAccessMode.Shell : DotnetAccessMode.Admin, _ => installed = true);
        var command = new InitCommand(Parser.Parse(["init", "--dry-run"]), _environment.Policy);

        string output = Run(() => workflows.InitWalkthrough(command).Should().BeEmpty());

        installed.Should().BeFalse();
        DotnetupConfig.Read().Should().BeEquivalentTo(previous);
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
        output.Should().Contain("SDK channel");
        output.Should().Contain("Installs in");
        if (!leavingAdmin)
        {
            output.Should().Contain(Strings.InitMigrationNotSupportedInAdminMode);
        }
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void FirstUseInstall_SelectingAdmin_InstallsWithInstallerBundle()
    {
        var environment = CreateEnvironment();
        List<ResolvedInstallRequest>? installed = null;
        var command = new UpdatingInstallCommand(
            Parser.Parse(["sdk", "install", PinnedVersion, "--interactive", "true", "--shell", "bash"]),
            _environment.Policy, environment);
        var workflow = new InstallWorkflow(command, new AdminInstallRootResolver(),
            CreateWorkflows(environment, DotnetAccessMode.Admin, requests => installed = requests, channel: null));

        Run(() => workflow.Execute([new MinimalInstallSpec(InstallComponent.SDK, PinnedVersion)]));

        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        installed.Should().ContainSingle().Which.Request.Options.InstallMechanism
            .Should().Be(InstallMechanism.WindowsInstallerBundle);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void StoredAdmin_DefaultSelection_InstallsWithInstallerBundle()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin, DotnetupOnPath = false });
        var environment = CreateEnvironment();
        List<ResolvedInstallRequest>? installed = null;
        var workflows = new InitWorkflows(environment, new FakeEnvironmentStateInspector())
        {
            // Accept the defaults, as non-interactive init does, with a pinned version to avoid network access.
            FormSelectionOverride = (_, defaults) => new InitFormSelection(
                PinnedVersion, defaults.AccessMode, defaults.MigrateSystemInstalls),
            InstallRequestsOverride = requests => installed = requests,
        };
        var command = new InitCommand(Parser.Parse(["init", "--interactive", "false"]), _environment.Policy);

        Run(() => workflows.InitWalkthrough(command));

        installed.Should().ContainSingle().Which.Request.Options.InstallMechanism
            .Should().Be(InstallMechanism.WindowsInstallerBundle);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        environment.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [ResourceLock(WellKnownResources.Console)]
    public void EnvironmentFailure_DoesNotReportSuccess()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = false });
        var environment = CreateEnvironment();
        environment.BeforeEnvironmentChange = () => throw new IOException("Environment write failed");
        var workflows = CreateWorkflows(environment, DotnetAccessMode.Admin, _ => { });
        var command = new InitCommand(Parser.Parse(["init"]), _environment.Policy);

        string output = Run(() =>
        {
            Action initialize = () => workflows.InitWalkthrough(command);
            initialize.Should().Throw<IOException>();
        });

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
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Form_AdminSelection_KeepsChannelVisibleAndFocus()
    {
        InitFormModel model = CreateModel(DotnetAccessMode.Shell);
        FormField channel = model.Fields.Single(field => field.Label == "SDK Channel");
        channel.SelectChoice(1);
        string? selectedChannel = model.SelectedChannel();
        FormField mode = model.Fields.Single(field => field.Label == "Access mode");
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

        model.SelectedAccessMode().Should().Be(DotnetAccessMode.Admin);
        channel.IsVisible.Should().BeTrue();
        model.SelectedChannel().Should().Be(selectedChannel);
        state.FocusedField.Should().BeSameAs(mode);
        state.IsDone.Should().BeFalse();
    }

    private MockDotnetInstallManager CreateEnvironment() => new(_environment.TempRoot, existingSystemInstalls: []);

    // An exact version resolves without network access.
    private const string PinnedVersion = "9.0.100";

    private InitWorkflows CreateWorkflows(
        MockDotnetInstallManager environment,
        DotnetAccessMode selectedMode,
        Action<List<ResolvedInstallRequest>> install,
        string? channel = PinnedVersion) =>
        new(environment, new FakeEnvironmentStateInspector())
        {
            FormSelectionOverride = (_, _) => new InitFormSelection(channel, selectedMode, MigrateSystemInstalls: true),
            InstallRequestsOverride = install,
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
            AnsiConsole.Console = AdminModeTestEnvironment.CreateConsole(output);
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
