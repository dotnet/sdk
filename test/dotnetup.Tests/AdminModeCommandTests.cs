// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Covers how <c>init</c>, <c>env set</c>, and the hidden elevated command route admin mode
/// switches. The policy store and elevator are faked, so no test writes HKLM or prompts UAC.
/// </summary>
[TestClass]
[DoNotParallelize]
public class AdminModeCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeDotnetupPolicyStore _store = new();
    private readonly FakeAdminModeElevator _elevator = new();
    private readonly MockDotnetInstallManager _env;
    private readonly FakeEnvironmentStateInspector _inspector = new();

    public AdminModeCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-admin-mode-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
        DotnetupPolicy.Store = _store;
        AccessModeResolver.OverrideForTesting = null;
        _env = new MockDotnetInstallManager(defaultInstallPath: Path.Combine(_tempDir, "dotnet"));
    }

    public void Dispose()
    {
        DotnetupPolicy.ResetStoreForTesting();
        AccessModeResolver.OverrideForTesting = null;
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

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
    [DataRow("insertdotnet")]
    [DataRow("removedotnet")]
    public void ElevatedSystemPath_PathOperation_RequiresDotnetDir(string operation)
    {
        var parseResult = Parser.Parse(["elevatedsystempath", operation, "output.txt"]);

        parseResult.Errors.Should().ContainSingle(e => e.Message.Contains("--dotnet-dir", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ElevatedSystemPath_PathOperation_ParsesWithDotnetDir()
    {
        var parseResult = Parser.Parse(["elevatedsystempath", "insertdotnet", "output.txt", "--dotnet-dir", _tempDir]);

        parseResult.Errors.Should().BeEmpty();
    }

    // ── init --mode ──

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Init_ModeAdmin_Parses()
    {
        var parseResult = Parser.Parse(["init", "--mode", "admin"]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(InitCommandParser.ModeOption).Should().Be(DotnetAccessMode.Admin);
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Init_ModeAdmin_RejectedOffWindows()
    {
        var parseResult = Parser.Parse(["init", "--mode", "admin"]);

        parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Init_ModeShell_Parses()
    {
        var parseResult = Parser.Parse(["init", "--mode", "shell"]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(InitCommandParser.ModeOption).Should().Be(DotnetAccessMode.Shell);
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
    public void Init_ModeShellWhileAdmin_DisablesAdminThenRunsInit()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        // A global.json pinning a local SDK path makes the regular walkthrough stop before any
        // install, so the test observes the admin mode switch without doing heavy work.
        Directory.CreateDirectory(Path.Combine(_tempDir, ".dotnet"));
        File.WriteAllText(Path.Combine(_tempDir, "global.json"), """{ "sdk": { "paths": [".dotnet"] } }""");
        var command = new InitCommand(Parser.Parse(["init", "--mode", "shell", "--interactive", "false"]));

        List<ResolvedInstallRequest> requests = RunFromTempDirectory(
            () => CreateInitWorkflows().RunInit(command, DotnetAccessMode.Shell));

        requests.Should().BeEmpty();
        _elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Init_ModeAdminDryRun_DoesNotSwitch()
    {
        var command = new InitCommand(Parser.Parse(["init", "--mode", "admin", "--dry-run"]));

        CreateInitWorkflows().RunInit(command, DotnetAccessMode.Admin).Should().BeEmpty();

        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
    }

    // ── env set ──

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_Admin_EnablesAdminModeOnly()
    {
        var parseResult = Parser.Parse(["env", "set", "admin"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_AdminCancelled_FailsWithoutChanges()
    {
        var elevator = new FakeAdminModeElevator
        {
            Failure = new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"),
        };
        var switcher = new AdminModeSwitcher(elevator, () => false, AdminModeSwitcherTests.CreateConsole(new StringWriter()));

        int exitCode = new EnvSetCommand(Parser.Parse(["env", "set", "admin"]), _env, _inspector, switcher).Execute();

        exitCode.Should().NotBe(0);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
        AssertNoEnvironmentOrConfigChanges();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_ShellWhileAdmin_DisablesThenApplies()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var parseResult = Parser.Parse(["env", "set", "shell", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
        DotnetupConfig.Read()!.AccessMode.Should().Be(DotnetAccessMode.Shell);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_WithoutModeWhileAdmin_MakesNoChanges()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        int exitCode = new EnvSetCommand(Parser.Parse(["env", "set"]), _env, _inspector, CreateSwitcher()).Execute();

        exitCode.Should().Be(0);
        _elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
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
