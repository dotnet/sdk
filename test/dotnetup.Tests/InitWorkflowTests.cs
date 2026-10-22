// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InitWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public InitWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe for parallel test execution.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [TestMethod]
    public void ShouldReplaceSystemConfiguration_ReturnsFalse_ForNone()
    {
        DotnetAccessModePolicy.ShouldReplaceSystemConfiguration(DotnetAccessMode.None)
            .Should().BeFalse();
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.Everywhere)]
    internal void ShouldReplaceSystemConfiguration_ReturnsTrue_ForPathReplacingModes(DotnetAccessMode accessMode)
    {
        DotnetAccessModePolicy.ShouldReplaceSystemConfiguration(accessMode)
            .Should().BeTrue();
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.None, false)]
    [DataRow(DotnetAccessMode.Shell, true)]
    [DataRow(DotnetAccessMode.Everywhere, true)]
    internal void ShouldMigrateSystemInstallsByDefault_MatchesAccessMode(
        DotnetAccessMode accessMode,
        bool expected)
    {
        DotnetAccessModePolicy.ShouldMigrateSystemInstallsByDefault(accessMode)
            .Should().Be(expected);
    }

    // ── PromptInstallsToMigrateIfDesired — early-exit paths ──

    [TestMethod]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenNoSystemInstallsExist()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);

        string manifestPath = Path.Combine(_tempDir, "manifest.json");
        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock, installRoot, manifestPath);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    [TestMethod]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenInteractiveIsFalse()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock,
            installRoot,
            interactive: false);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    // ── GetExistingSystemInstalls — architecture filtering ──

    [TestMethod]
    public void GetExistingSystemInstalls_FiltersToNativeArchOnly()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var foreignArch = nativeArch == InstallArchitecture.x64 ? InstallArchitecture.arm64 : InstallArchitecture.x64;

        var nativeRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var foreignRoot = new DotnetInstallRoot(_tempDir, foreignArch);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(nativeRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(foreignRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(nativeRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
                new DotnetInstall(foreignRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime),
            ]);

        var result = mock.GetExistingSystemInstalls();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.InstallRoot.Architecture == nativeArch);
        // Verify descending sort order from the real filter
        result[0].Version.ToString().Should().BeOneOf("10.0.100", "10.0.0");
        result[0].Version.CompareTo(result[1].Version).Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public void GetExistingSystemInstalls_DeduplicatesSameComponentVersionAndArch()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("8.0.22"), InstallComponent.Runtime),
                new DotnetInstall(installRoot, new ReleaseVersion("8.0.22"), InstallComponent.Runtime),
            ]);

        var result = mock.GetExistingSystemInstalls();

        result.Should().HaveCount(2);
        result.Should().ContainSingle(i => i.Component == InstallComponent.SDK && i.Version.ToString() == "10.0.100");
        result.Should().ContainSingle(i => i.Component == InstallComponent.Runtime && i.Version.ToString() == "8.0.22");
    }

    [TestMethod]
    public void FormatMigrationDisplayItems_IncludesArchitecture_WhenMultipleArchitecturesArePresent()
    {
        List<MigrationWorkflow.MigrationSelection> migrationSelections =
        [
            new(InstallComponent.SDK, new UpdateChannel("10.0.1xx"), new ReleaseVersion("10.0.100"), InstallArchitecture.x64),
            new(InstallComponent.SDK, new UpdateChannel("10.0.1xx"), new ReleaseVersion("10.0.100"), InstallArchitecture.arm64),
        ];

        var items = InitWorkflows.FormatMigrationDisplayItems(migrationSelections);

        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.Contains("10.0.1xx") && i.Contains("["));
    }

    // ── GetDefaultAccessMode ──

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void GetDefaultAccessMode_ReturnsShell_WhenShellProviderIsAvailableOnNonWindows()
    {
        InitDefaultsResolver.GetDefaultAccessMode(new BashEnvShellProvider())
            .Should().Be(DotnetAccessMode.Shell);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void GetDefaultAccessMode_ReturnsEverywhere_WhenShellProviderIsAvailableOnWindows()
    {
        InitDefaultsResolver.GetDefaultAccessMode(new BashEnvShellProvider())
            .Should().Be(DotnetAccessMode.Everywhere);
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.None)]
    [DataRow(DotnetAccessMode.Shell)]
    internal void GetDefaultAccessMode_ReturnsConfiguredMode(DotnetAccessMode configuredAccessMode)
    {
        InitDefaultsResolver.GetDefaultAccessMode(
            shellProvider: null,
            configuredAccessMode: configuredAccessMode)
            .Should().Be(configuredAccessMode);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(DotnetAccessMode.Everywhere)]
    [DataRow(DotnetAccessMode.Admin)]
    internal void GetDefaultAccessMode_ReturnsConfiguredWindowsMode(DotnetAccessMode accessMode)
    {
        InitDefaultsResolver.GetDefaultAccessMode(
            shellProvider: null,
            configuredAccessMode: accessMode)
            .Should().Be(accessMode);
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void GetDefaultAccessMode_IgnoresConfiguredEverywhereModeOutsideWindows()
    {
        InitDefaultsResolver.GetDefaultAccessMode(
            new BashEnvShellProvider(),
            configuredAccessMode: DotnetAccessMode.Everywhere)
            .Should().Be(DotnetAccessMode.Shell);
    }

    [TestMethod]
    public void GetDefaultDotnetupOnPath_PreservesConfiguredValue()
    {
        InitDefaultsResolver.GetDefaultDotnetupOnPath(
            new DotnetupConfigData { DotnetupOnPath = false })
            .Should().BeFalse();
        InitDefaultsResolver.GetDefaultDotnetupOnPath(configured: null)
            .Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void InitWalkthrough_Admin_PersistsModeWithoutApplyingUserEnvironment()
    {
        var policyStore = new FakeDotnetupPolicyStore();
        var policy = new DotnetupPolicy(policyStore);
        var elevator = new FakeAdminModeElevator(policy);
        var switcher = new AdminModeSwitcher(
            policy,
            elevator,
            () => false,
            AdminModeSwitcherTests.CreateConsole(new StringWriter()));
        var environment = new MockDotnetInstallManager(defaultInstallPath: _tempDir, existingSystemInstalls: []);
        var workflows = new InitWorkflows(environment, switcher)
        {
            FormSelectionOverride = (_, _) => new InitFormSelection(
                Channel: null,
                AccessMode: DotnetAccessMode.Admin,
                MigrateSystemInstalls: false),
        };
        var command = new InitCommand(Parser.Parse(["init"]), policy);
        var previous = new DotnetupConfigData
        {
            AccessMode = DotnetAccessMode.Everywhere,
            DotnetupOnPath = false,
        };
        DotnetupConfig.Write(previous);

        List<ResolvedInstallRequest> requests = RunFromTempDirectory(
            () => workflows.InitWalkthrough(command, requests: []));

        requests.Should().BeEmpty();
        elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        policy.IsUnsignedDownloadBlocked().Should().BeTrue();
        DotnetupConfig.Read().Should().BeEquivalentTo(new DotnetupConfigData
        {
            AccessMode = DotnetAccessMode.Admin,
            DotnetupOnPath = false,
        });
        environment.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        environment.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        environment.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void ResolveExecutionPlan_AdminInitWithoutUntracked_CreatesUntrackedSystemRequest()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin });
        var policyStore = new FakeDotnetupPolicyStore();
        var policy = new DotnetupPolicy(policyStore);
        var parseResult = Parser.Parse(["init"]);
        parseResult.Errors.Should().BeEmpty();
        var command = new InitCommand(parseResult, policy);
        command.Untracked.Should().BeFalse();
        var installRoot = new DotnetInstallRoot(
            _tempDir,
            InstallerUtilities.GetDefaultInstallArchitecture());
        var request = new ResolvedInstallRequest(
            new DotnetInstallRequest(
                installRoot,
                new UpdateChannel("9.0.100"),
                InstallComponent.SDK,
                new InstallRequestOptions()),
            new ReleaseVersion("9.0.100"));
        var defaults = new InitFormDefaults(
            installRoot,
            DotnetAccessMode.Admin,
            MigrateSystemInstalls: false,
            Migrations: [],
            new DefaultChannelDisplay("9.0.100", GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, "9.0.100")],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);

        InitExecutionPlan plan = InitWorkflows.ResolveExecutionPlan(
            command,
            [request],
            defaults,
            new InitFormSelection("9.0.100", DotnetAccessMode.Admin, MigrateSystemInstalls: false));

        plan.InstallRequests.Should().ContainSingle();
        DotnetInstallRequest resolved = plan.InstallRequests[0].Request;
        resolved.Options.Untracked.Should().BeTrue();
        resolved.Options.InstallMechanism.Should().Be(InstallMechanism.WindowsInstallerBundle);
        resolved.InstallRoot.Should().Be(new AdminInstallRootResolver().Resolve(installRoot.Architecture));
        plan.Migrations.Should().BeEmpty();
    }

    // The no-shell isolation fallback (which reads the SHELL environment variable) is covered
    // deterministically by InitWorkflowShellFallbackTests, which mutates SHELL and therefore runs
    // in a serialized collection.

    [TestMethod]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void InitWalkthrough_SkipsForLocalSdkPath()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".dotnet"));
        File.WriteAllText(
            Path.Combine(_tempDir, "global.json"),
            """
            {
              "sdk": {
                "paths": [".dotnet"]
              }
            }
            """);

        var mock = new MockDotnetInstallManager(defaultInstallPath: _tempDir);
        var parseResult = Parser.Parse(["init"]);
        var command = new InitCommand(parseResult);

        List<ResolvedInstallRequest> requests = RunFromTempDirectory(
            () => new InitWorkflows(mock).InitWalkthrough(command));

        requests.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
        mock.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        mock.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        mock.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
        DotnetupConfig.Exists().Should().BeFalse();
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void HasLocalSdkPathGlobalJson_ReturnsFalseForHostSentinel()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "global.json"),
            """
            {
              "sdk": {
                "paths": ["$host$"]
              }
            }
            """);

        bool hasLocalSdkPath = RunFromTempDirectory(InitWorkflows.HasLocalSdkPathGlobalJson);

        hasLocalSdkPath.Should().BeFalse();
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void InitWalkthrough_DryRunIgnoresLocalSdkPath()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".dotnet"));
        File.WriteAllText(
            Path.Combine(_tempDir, "global.json"),
            """
            {
              "sdk": {
                "paths": [".dotnet"]
              }
            }
            """);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);
        var parseResult = Parser.Parse(["init", "--dry-run"]);
        var command = new InitCommand(parseResult);

        List<ResolvedInstallRequest> requests = RunFromTempDirectory(
            () => new InitWorkflows(mock).InitWalkthrough(command));

        requests.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
        mock.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        mock.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        mock.ApplyDotnetupOnUserPathCallCount.Should().Be(0);
        DotnetupConfig.Exists().Should().BeFalse();
    }

    // ── ResolveDefaultMigrations ──

    [TestMethod]
    public void ResolveDefaultMigrations_ReturnsCandidates()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = InitDefaultsResolver.ResolveDefaultMigrations(
            mock, installRoot, manifestPath: null);

        result.Should().NotBeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    [TestMethod]
    public void DisplayEnvironmentSetupProgress_ExplainsPostInstallDelay()
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            Ansi = AnsiSupport.No,
        });

        InitWorkflows.DisplayEnvironmentSetupProgress(console);

        output.ToString().Should().Contain("Setting up your environment.");
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
