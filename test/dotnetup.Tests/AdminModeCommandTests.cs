// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Covers how <c>init</c> and the hidden elevated command route admin mode
/// switches. The policy store and elevator are faked, so no test writes HKLM or prompts UAC.
/// </summary>
[TestClass]
[DoNotParallelize]
public class AdminModeCommandTests : IDisposable
{
    private readonly AdminModeTestEnvironment _environment = new();
    private string _tempDir => _environment.TempRoot;
    private FakeDotnetupPolicyStore _store => _environment.Store;
    private readonly FakeAdminModeElevator _elevator = new();
    private readonly MockDotnetInstallManager _env;

    public AdminModeCommandTests()
    {
        _env = new MockDotnetInstallManager(defaultInstallPath: Path.Combine(_tempDir, "dotnet"), existingSystemInstalls: []);
    }

    public void Dispose() => _environment.Dispose();

    // ── elevatedsystempath parsing ──

    [TestMethod]
    [DataRow("enableadminmode")]
    [DataRow("disableadminmode")]
    public void ElevatedSystemPath_AdminModeOperation_ParsesWithoutDotnetDir(string operation)
    {
        var parseResult = Parser.Parse(["elevatedsystempath", operation, "output.txt"]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(ElevatedSystemPathCommandParser.DotnetDir).Should().BeNull();
    }

    [TestMethod]
    public void ElevatedSystemPath_PathOperation_ParsesWithDotnetDir()
    {
        var parseResult = Parser.Parse(["elevatedsystempath", "insertdotnet", "output.txt", "--dotnet-dir", _tempDir]);

        parseResult.Errors.Should().BeEmpty();
    }

    // ── init --mode ──

    [TestMethod]
    [DataRow("admin", DotnetAccessMode.Admin)]
    [DataRow("shell", DotnetAccessMode.Shell)]
    [DataRow("none", DotnetAccessMode.None)]
    [DataRow("everywhere", DotnetAccessMode.Everywhere)]
    internal void Init_Mode_ParsesOnSupportedPlatforms(string token, DotnetAccessMode mode)
    {
        var parseResult = Parser.Parse(["init", "--mode", token]);

        if (DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(mode))
        {
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValue(InitCommandParser.ModeOption).Should().Be(mode);
        }
        else
        {
            parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Init_UnknownMode_Rejected()
    {
        Parser.Parse(["init", "--mode", "bogus"]).Errors.Should().NotBeEmpty();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Init_ModeAdmin_OnlySwitchesMode()
    {
        var command = new InitCommand(Parser.Parse(["init", "--mode", "admin", "--interactive", "false"]));

        List<ResolvedInstallRequest> requests = CreateInitWorkflows().RunInit(command, DotnetAccessMode.Admin);

        requests.Should().BeEmpty();
        _elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Init_WithoutModeWhileAdmin_MakesNoChanges()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var command = new InitCommand(Parser.Parse(["init", "--interactive", "false"]));

        List<ResolvedInstallRequest> requests = CreateInitWorkflows().RunInit(command, requestedAccessMode: null);

        requests.Should().BeEmpty();
        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void Init_ModeShellWhileAdmin_LocalSdkPathLeavesAdminModeOn()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        // A global.json pinning a local SDK path makes the walkthrough stop without changes.
        Directory.CreateDirectory(Path.Combine(_tempDir, ".dotnet"));
        File.WriteAllText(Path.Combine(_tempDir, "global.json"), """{ "sdk": { "paths": [".dotnet"] } }""");
        var command = new InitCommand(Parser.Parse(["init", "--mode", "shell", "--interactive", "false"]));

        List<ResolvedInstallRequest> requests = RunFromTempDirectory(
            () => CreateInitWorkflows().RunInit(command, DotnetAccessMode.Shell));

        requests.Should().BeEmpty();
        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void Init_ModeShellWhileAdmin_FormExitLeavesAdminModeOn()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var command = new InitCommand(Parser.Parse(["init", "--mode", "shell"]));
        var workflows = new InitWorkflows(_env, CreateSwitcher()) { FormSelectionOverride = (_, _) => null };

        RunFromTempDirectory(() => workflows.RunInit(command, DotnetAccessMode.Shell)).Should().BeEmpty();

        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        DotnetupConfig.Exists().Should().BeFalse();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void Init_FormSelectsAdminWhileAdmin_MakesNoChanges()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var command = new InitCommand(Parser.Parse(["init", "--mode", "shell"]));
        var workflows = new InitWorkflows(_env, CreateSwitcher())
        {
            FormSelectionOverride = (_, defaults) => new InitFormSelection(null, DotnetAccessMode.Admin, defaults.MigrateSystemInstalls),
        };

        RunFromTempDirectory(() => workflows.RunInit(command, DotnetAccessMode.Shell)).Should().BeEmpty();

        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        DotnetupConfig.Exists().Should().BeFalse();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void Init_ModeShellWhileAdmin_DisablesAdminBeforeApplyingSetup()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var elevator = new FakeAdminModeElevator
        {
            Failure = new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"),
        };
        var switcher = new AdminModeSwitcher(elevator, () => false, AdminModeSwitcherTests.CreateConsole(new StringWriter()));
        var command = new InitCommand(Parser.Parse(["init", "--mode", "shell", "--interactive", "false"]));
        var workflows = new InitWorkflows(_env, switcher);

        // Declining elevation stops setup, which shows the disable precedes every setup change.
        var exception = Assert.ThrowsExactly<DotnetInstallException>(
            () => RunFromTempDirectory(() => workflows.RunInit(command, DotnetAccessMode.Shell)));

        exception.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeChangeFailed);
        elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        DotnetupConfig.Exists().Should().BeFalse();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        _env.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Init_ModeShellWhileAdminDryRun_DoesNotDisable()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var command = new InitCommand(Parser.Parse(["init", "--mode", "shell", "--dry-run", "--interactive", "false"]));
        var workflows = new InitWorkflows(_env, CreateSwitcher());

        workflows.RunInit(command, DotnetAccessMode.Shell).Should().BeEmpty();

        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        DotnetupConfig.Exists().Should().BeFalse();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Init_ModeAdminDryRun_DoesNotSwitch()
    {
        var command = new InitCommand(Parser.Parse(["init", "--mode", "admin", "--dry-run"]));

        CreateInitWorkflows().RunInit(command, DotnetAccessMode.Admin).Should().BeEmpty();

        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
    }

    private AdminModeSwitcher CreateSwitcher()
        => new(_elevator, () => false, AdminModeSwitcherTests.CreateConsole(new StringWriter()));

    private InitWorkflows CreateInitWorkflows() => new(_env, CreateSwitcher());

    private void AssertNoEnvironmentOrConfigChanges()
    {
        DotnetupConfig.Exists().Should().BeFalse();
        _env.GetExistingSystemInstallsCallCount.Should().Be(0);
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        _env.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
    }

    private T RunFromTempDirectory<T>(Func<T> action)
    {
        string originalCurrentDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = _tempDir;
            return action();
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }
}
