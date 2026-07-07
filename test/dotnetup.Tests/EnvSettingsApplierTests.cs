// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for the two-axis composition in <see cref="EnvSettingsApplier"/> — dotnet
/// access (<see cref="DotnetAccessMode"/>) × dotnetup-on-PATH — plus the reality-driven removal
/// transitions and the platform / shell-provider edge cases.
///
/// Removal decisions are driven by an <see cref="ObservedEnvironmentState"/> the test supplies
/// directly, so the applier stays pure (no registry / real-environment reads here). Profile
/// writes are asserted via the mock's counters (the mock records, it does not touch disk).
/// Profile removals go through the real <see cref="ShellProfileManager.RemoveProfileEntries"/>
/// static against a temp-dir <see cref="TestShellProvider"/>, so those are asserted on the file
/// system. The Windows user-PATH dotnetup entry is asserted via the mock's
/// <see cref="MockDotnetInstallManager.LastDotnetupOnUserPathEnabled"/>.
/// </summary>
[TestClass]
public class EnvSettingsApplierTests : IDisposable
{
    private const string DotnetRoot = "/fake/dotnet";

    private readonly string _tempDir;
    private readonly MockDotnetInstallManager _env;
    private readonly TestShellProvider _shellProvider;

    public EnvSettingsApplierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-applier-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _env = new MockDotnetInstallManager(defaultInstallPath: DotnetRoot);
        _shellProvider = new TestShellProvider(_tempDir, "profile.sh");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static ObservedEnvironmentState Observed(
        bool dotnetEnvVarsPresent = false,
        bool dotnetEnvVarsComplete = false,
        bool? profileBlockPresent = null,
        bool dotnetupOnUserPath = false)
        => new(dotnetEnvVarsPresent, dotnetEnvVarsComplete, profileBlockPresent, dotnetupOnUserPath);

    // ── dotnetup-on-PATH is always applied (idempotent) ──

    [TestMethod]
    public void AlwaysAppliesDotnetupOnUserPath_WithTargetValue()
    {
        EnvSettingsApplier.Apply(DotnetAccessMode.None, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);
        _env.ApplyDotnetupOnUserPathCallCount.Should().Be(1);
        _env.LastDotnetupOnUserPathEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void None_DotnetupOff_NoProfile_NoEnvVars()
    {
        // First-time config (nothing observed as wired): no env vars, no profile block.
        EnvSettingsApplier.Apply(DotnetAccessMode.None, targetDotnetupOnPath: false, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void None_DotnetupOn_WritesDotnetupOnlyProfile()
    {
        EnvSettingsApplier.Apply(DotnetAccessMode.None, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeFalse();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
    }

    [TestMethod]
    public void Shell_DotnetupOn_WritesBothInProfile_NoEnvVars()
    {
        EnvSettingsApplier.Apply(DotnetAccessMode.Shell, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeTrue();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
    }

    [TestMethod]
    public void Shell_DotnetupOff_WritesDotnetOnlyProfile()
    {
        EnvSettingsApplier.Apply(DotnetAccessMode.Shell, targetDotnetupOnPath: false, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeTrue();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void All_DotnetupOn_WritesEnvVarsAndProfile()
    {
        EnvSettingsApplier.Apply(DotnetAccessMode.Full, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeTrue();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
    }

    // ── Removal transitions (driven by observed reality) ──

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void All_To_Shell_RemovesEnvVarsButKeepsProfile()
    {
        EnvSettingsApplier.Apply(
            DotnetAccessMode.Shell, targetDotnetupOnPath: true,
            Observed(dotnetEnvVarsPresent: true, dotnetEnvVarsComplete: true, profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);  // env-var removal
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);    // profile still written
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void All_To_None_DotnetupOff_RemovesEnvVarsAndProfile()
    {
        WriteManagedBlockToProfile();

        EnvSettingsApplier.Apply(
            DotnetAccessMode.None, targetDotnetupOnPath: false,
            Observed(dotnetEnvVarsPresent: true, dotnetEnvVarsComplete: true, profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileHasManagedBlock().Should().BeFalse();
    }

    [TestMethod]
    public void Shell_To_None_DotnetupOff_RemovesProfile()
    {
        WriteManagedBlockToProfile();

        EnvSettingsApplier.Apply(
            DotnetAccessMode.None, targetDotnetupOnPath: false,
            Observed(profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileHasManagedBlock().Should().BeFalse();
    }

    [TestMethod]
    public void Shell_DotnetupOn_To_None_DotnetupOn_RewritesProfileAsDotnetupOnly()
    {
        // Turning off dotnet access but keeping dotnetup-on-PATH should rewrite (not remove)
        // the block as dotnetup-only.
        EnvSettingsApplier.Apply(
            DotnetAccessMode.None, targetDotnetupOnPath: true,
            Observed(profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeFalse();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
    }

    // ── Drift correction: remove what is actually observed, even when no prior config recorded it ──

    [TestMethod]
    public void StrayProfileBlock_RemovedEvenWithoutPriorConfig()
    {
        // The config never recorded a block (Empty would say "unknown"), but one is actually
        // present. Targeting none + dotnetup-off must still remove it.
        WriteManagedBlockToProfile();

        EnvSettingsApplier.Apply(
            DotnetAccessMode.None, targetDotnetupOnPath: false,
            Observed(profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        ProfileHasManagedBlock().Should().BeFalse();
    }

    [TestMethod]
    public void ObservedNoProfileBlock_NothingRemoved()
    {
        // Nothing observed and nothing targeted: the removal branch must not run.
        EnvSettingsApplier.Apply(
            DotnetAccessMode.None, targetDotnetupOnPath: false,
            Observed(profileBlockPresent: false),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void StrayDotnetEnvVars_RemovedWhenTargetShell()
    {
        // Reality has 'full'-mode env vars wired but the target is shell: remove them, even though
        // no prior 'full' config is supplied.
        EnvSettingsApplier.Apply(
            DotnetAccessMode.Shell, targetDotnetupOnPath: true,
            Observed(dotnetEnvVarsPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
    }

    // ── Edge cases ──

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void Target_All_On_NonWindows_Throws()
    {
        Action act = () => EnvSettingsApplier.Apply(
            DotnetAccessMode.Full, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        act.Should().Throw<PlatformNotSupportedException>();
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void ProfileWrite_Without_ShellProvider_Throws()
    {
        var originalShell = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", null);

            Action act = () => EnvSettingsApplier.Apply(
                DotnetAccessMode.Shell, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, shellProvider: null);

            act.Should().Throw<DotnetInstallException>()
                .Which.Message.Should().Contain("--shell");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", originalShell);
        }
    }

    [TestMethod]
    public void ArgumentNullException_When_EnvironmentIsNull()
    {
        Action act = () => EnvSettingsApplier.Apply(DotnetAccessMode.None, false, ObservedEnvironmentState.Empty, environment: null!, DotnetRoot, _shellProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ArgumentNullException_When_ObservedIsNull()
    {
        Action act = () => EnvSettingsApplier.Apply(DotnetAccessMode.None, false, observed: null!, _env, DotnetRoot, _shellProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ArgumentException_When_DotnetRootIsEmpty()
    {
        Action act = () => EnvSettingsApplier.Apply(DotnetAccessMode.None, false, ObservedEnvironmentState.Empty, _env, dotnetRoot: "", _shellProvider);
        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ──

    private string ProfilePath => Path.Combine(_tempDir, "profile.sh");

    private bool ProfileHasManagedBlock()
        => File.Exists(ProfilePath) && File.ReadAllText(ProfilePath).Contains("# dotnetup: begin", StringComparison.Ordinal);

    private void WriteManagedBlockToProfile()
    {
        File.WriteAllText(ProfilePath, "# pre-existing user content\n# dotnetup: begin\necho 'managed by dotnetup'\n# dotnetup: end\n");
    }
}
