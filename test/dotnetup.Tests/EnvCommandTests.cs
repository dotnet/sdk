// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.NET.TestFramework;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Behavioral tests for <see cref="EnvSetCommand"/>, <see cref="EnvClearCommand"/>, and
/// <see cref="EnvShowCommand"/>. Focus is on what each command persists to the config and which
/// underlying environment APIs it invokes — drift-detection branches that depend on the real
/// Windows registry / arbitrary host shell profiles are intentionally not asserted because they
/// cannot be sandboxed cleanly in CI.
/// </summary>
[TestClass]
[DoNotParallelize]
public class EnvCommandTests : IDisposable
{
    private readonly AdminModeTestEnvironment _environment = new();
    private string _tempDir => _environment.TempRoot;
    private FakeDotnetupPolicyStore _store => _environment.Store;
    private readonly FakeAdminModeElevator _elevator;
    private readonly MockDotnetInstallManager _env;
    private readonly FakeEnvironmentStateInspector _inspector = new();

    public EnvCommandTests()
    {
        _elevator = _environment.CreateElevator();
        _env = new MockDotnetInstallManager(defaultInstallPath: Path.Combine(_tempDir, "dotnet"));
    }

    public void Dispose() => _environment.Dispose();

    // ── EnvSetCommand ──

    [TestMethod]
    public void EnvSet_None_PersistsConfig()
    {
        // Pass --shell so the dotnetup-only profile write (None still keeps dotnetup on PATH) does
        // not depend on host shell auto-detection, which is unavailable on some CI agents.
        var parseResult = Parser.Parse(["env", "set", "none", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.None);
        // dotnetup-on-PATH defaults to on when not specified and not previously stored.
        config.DotnetupOnPath.Should().BeTrue();
        // None + dotnetup-on-PATH still wires the dotnetup PATH entry (idempotent) but no dotnet env vars.
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.LastDotnetupOnUserPathEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void EnvSet_DotnetupOnPathOff_PersistsAndRemoves()
    {
        var parseResult = Parser.Parse(["env", "set", "none", "--dotnetup-on-path", "false"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.None);
        config.DotnetupOnPath.Should().BeFalse();
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void EnvSet_DotnetupOnPathOnly_LeavesStoredModeUnchanged()
    {
        // Pre-existing config: shell mode, dotnetup on. Change only --dotnetup-on-path false.
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "set", "--dotnetup-on-path", "false", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.Shell);   // mode preserved (re-sync)
        config.DotnetupOnPath.Should().BeFalse();          // only this changed
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void EnvSet_Everywhere_OnNonWindows_RejectedByParser()
    {
        var parseResult = Parser.Parse(["env", "set", "everywhere"]);

        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void EnvSet_Admin_OnNonWindows_RejectedByParser()
    {
        var parseResult = Parser.Parse(["env", "set", "admin"]);

        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_Everywhere_FromNone_OnWindows_AppliesAndPersists()
    {
        var parseResult = Parser.Parse(["env", "set", "everywhere"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.Everywhere);
        config.DotnetupOnPath.Should().BeTrue();
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    [TestMethod]
    public void EnvSet_NoModeAndNoStoredConfig_Fails()
    {
        var parseResult = Parser.Parse(["env", "set"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        // Nothing to apply → DotnetInstallException → exit code 1.
        exitCode.Should().Be(1);
        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod]
    public void EnvSet_NoModeAndCorruptStoredConfig_Fails()
    {
        DotnetupPaths.EnsureDataDirectoryExists();
        File.WriteAllText(DotnetupPaths.ConfigPath, "not valid json{{{");

        var parseResult = Parser.Parse(["env", "set"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        // A config exists but is unreadable → DotnetInstallException → exit code 1.
        exitCode.Should().Be(1);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_Admin_PersistsModeAndBlocksUnsignedDownloads()
    {
        var parseResult = Parser.Parse(["env", "set", "admin", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeTrue();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        _env.LastDotnetupOnUserPathEnabled.Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_Admin_PreservesOtherSettings()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = false, UpdateNotifications = false });
        int exitCode = new EnvSetCommand(Parser.Parse(["env", "set", "admin"]), _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        DotnetupConfig.Read()!.DotnetupOnPath.Should().BeFalse();
        DotnetupConfig.Read()!.UpdateNotifications.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_Admin_FromShell_CompletesAfterEnablingPolicy()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = false });
        var parseResult = Parser.Parse(["env", "set", "admin", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeTrue();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        DotnetupConfig.Read()!.DotnetupOnPath.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_AdminCancelled_FailsWithoutChanges()
    {
        var elevator = _environment.CreateElevator(new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"));
        var switcher = _environment.CreateSwitcher(elevator);

        int exitCode = new EnvSetCommand(Parser.Parse(["env", "set", "admin"]), _env, _inspector, switcher).Execute();

        exitCode.Should().NotBe(0);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_ShellWhileAdmin_DisablesThenApplies()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin, DotnetupOnPath = true });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var parseResult = Parser.Parse(["env", "set", "shell", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Shell);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_ShellWhileAdminCancelled_AppliesNothing()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin, DotnetupOnPath = true });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var elevator = _environment.CreateElevator(new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"));
        var switcher = _environment.CreateSwitcher(elevator);

        int exitCode = new EnvSetCommand(Parser.Parse(["env", "set", "shell", "--shell", "bash"]), _env, _inspector, switcher).Execute();

        exitCode.Should().NotBe(0);
        elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeTrue();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_WithoutModeWhileAdmin_ResynchronizesAdminMode()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin, DotnetupOnPath = true });

        int exitCode = new EnvSetCommand(Parser.Parse(["env", "set", "--shell", "bash"]), _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeTrue();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void EnvSet_Admin_HonorsPathOptionAndRepairsEnvironment(bool alreadyAdmin, bool dotnetupOnPath)
    {
        DotnetAccessMode previousMode = alreadyAdmin ? DotnetAccessMode.Admin : DotnetAccessMode.Everywhere;
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = previousMode, DotnetupOnPath = !dotnetupOnPath });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = alreadyAdmin;
        var environment = new MockDotnetInstallManager(_env.GetDefaultDotnetInstallPath())
        {
            BeforeEnvironmentChange = () =>
            {
                _environment.Policy.IsUnsignedDownloadBlocked().Should().BeTrue();
                DotnetupConfig.Read()!.AccessMode.Should().Be(previousMode);
                DotnetupConfig.Read()!.DotnetupOnPath.Should().Be(!dotnetupOnPath);
            },
        };
        var inspector = new FakeEnvironmentStateInspector(new ObservedEnvironmentState(
            DotnetUserEnvVarsPresent: true,
            DotnetUserEnvVarsComplete: true,
            ProfileBlock: ProfileBlockState.Unknown,
            DotnetupOnUserPath: !dotnetupOnPath));

        int exitCode = new EnvSetCommand(
            Parser.Parse(["env", "set", "admin", "--dotnetup-on-path", dotnetupOnPath ? "true" : "false", "--shell", "bash"]),
            environment, inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        DotnetupConfig.Read()!.DotnetupOnPath.Should().Be(dotnetupOnPath);
        environment.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
        environment.LastDotnetupOnUserPathEnabled.Should().Be(dotnetupOnPath);
        environment.ApplyTerminalProfileModificationsCallCount.Should().Be(dotnetupOnPath ? 1 : 0);
        if (dotnetupOnPath)
        {
            environment.LastIncludeDotnetForTerminalProfileModifications.Should().BeFalse();
        }
        _elevator.Operations.Should().HaveCount(alreadyAdmin ? 0 : 1);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_AdminPolicyNotChanged_DoesNotApplyOrPersist()
    {
        var elevator = _environment.CreateElevator(appliesChange: false);

        int exitCode = new EnvSetCommand(
            Parser.Parse(["env", "set", "admin"]), _env, _inspector, _environment.CreateSwitcher(elevator)).Execute();

        exitCode.Should().Be(1);
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_AdminEnvironmentFailure_DoesNotPersistMode()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None });
        var environment = new MockDotnetInstallManager(_env.GetDefaultDotnetInstallPath())
        {
            BeforeEnvironmentChange = () => throw new IOException("Environment write failed"),
        };

        int exitCode = new EnvSetCommand(
            Parser.Parse(["env", "set", "admin", "--shell", "bash"]), environment, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(1);
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.None);
    }

    [TestMethod]
    public void EnvSet_ShellWhileNotAdmin_DoesNotTouchPolicy()
    {
        var parseResult = Parser.Parse(["env", "set", "shell", "--shell", "bash"]);

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
    }

    private AdminModeSwitcher CreateSwitcher() => _environment.CreateSwitcher(_elevator);

    private void AssertNoEnvironmentOrConfigChanges()
    {
        DotnetupConfig.Exists().Should().BeFalse();
        _env.GetExistingSystemInstallsCallCount.Should().Be(0);
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        _env.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
    }

    // ── EnvClearCommand ──

    [TestMethod]
    public void EnvClear_RemovesEverythingAndPersists()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "clear", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvClearCommand(parseResult, _env, _inspector, _environment.Policy).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.None);
        config.DotnetupOnPath.Should().BeFalse();
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public void EnvClear_AndExplicitNoneWhileAdmin_DisableBeforeApplying(bool clear)
    {
        DotnetupConfig.Write(new DotnetupConfigData
        {
            AccessMode = DotnetAccessMode.Admin,
            UpdateNotifications = false,
        });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var environment = new MockDotnetInstallManager(_env.GetDefaultDotnetInstallPath())
        {
            BeforeEnvironmentChange = () =>
            {
                _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
                DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
            },
        };

        int exitCode = clear
            ? new EnvClearCommand(Parser.Parse(["env", "clear"]), environment, _inspector,
                adminModeSwitcher: CreateSwitcher()).Execute()
            : new EnvSetCommand(Parser.Parse(["env", "set", "none", "--dotnetup-on-path", "false"]),
                environment, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        DotnetupConfig.Read().Should().BeEquivalentTo(new DotnetupConfigData
        {
            AccessMode = DotnetAccessMode.None,
            DotnetupOnPath = false,
            UpdateNotifications = false,
        });
        environment.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public void EnvClear_AdminPolicyFailure_LeavesConfigurationAndEnvironment(bool cancelled)
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var elevator = _environment.CreateElevator(
            failure: cancelled ? new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled") : null,
            appliesChange: false);

        int exitCode = new EnvClearCommand(Parser.Parse(["env", "clear"]), _env, _inspector,
            adminModeSwitcher: _environment.CreateSwitcher(elevator)).Execute();

        exitCode.Should().Be(1);
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Admin);
        _env.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
    }

    // ── EnvShowCommand ──

    [TestMethod]
    public void EnvShow_NoConfig_ExitCodeZero()
    {
        var parseResult = Parser.Parse(["env", "show"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvShowCommand(parseResult, _env, _inspector, _environment.Policy).Execute();

        exitCode.Should().Be(0);
    }

    [TestMethod]
    public void EnvShow_ConfiguredNone_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env, _inspector, _environment.Policy).Execute();

        exitCode.Should().Be(0);
    }

    [TestMethod]
    public void EnvShow_ConfiguredShell_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env, _inspector, _environment.Policy).Execute();

        exitCode.Should().Be(0);
    }
}
