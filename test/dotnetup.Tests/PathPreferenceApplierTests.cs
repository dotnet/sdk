// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for the two-axis composition in <see cref="PathPreferenceApplier"/> — dotnet
/// exposure (<see cref="PathPreference"/>) × dotnetup-on-PATH — plus the reality-driven removal
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
public class PathPreferenceApplierTests : IDisposable
{
    private const string DotnetRoot = "/fake/dotnet";

    private readonly string _tempDir;
    private readonly MockDotnetInstallManager _env;
    private readonly TestShellProvider _shellProvider;

    public PathPreferenceApplierTests()
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

    [Fact]
    public void AlwaysAppliesDotnetupOnUserPath_WithTargetValue()
    {
        PathPreferenceApplier.Apply(PathPreference.None, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);
        _env.ApplyDotnetupOnUserPathCallCount.Should().Be(1);
        _env.LastDotnetupOnUserPathEnabled.Should().BeTrue();
    }

    [Fact]
    public void None_DotnetupOff_NoProfile_NoEnvVars()
    {
        // First-time config (nothing observed as wired): no env vars, no profile block.
        PathPreferenceApplier.Apply(PathPreference.None, targetDotnetupOnPath: false, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [Fact]
    public void None_DotnetupOn_WritesDotnetupOnlyProfile()
    {
        PathPreferenceApplier.Apply(PathPreference.None, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeFalse();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
    }

    [Fact]
    public void Shell_DotnetupOn_WritesBothInProfile_NoEnvVars()
    {
        PathPreferenceApplier.Apply(PathPreference.Shell, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeTrue();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
    }

    [Fact]
    public void Shell_DotnetupOff_WritesDotnetOnlyProfile()
    {
        PathPreferenceApplier.Apply(PathPreference.Shell, targetDotnetupOnPath: false, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeTrue();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeFalse();
    }

    [Fact]
    public void All_DotnetupOn_WritesEnvVarsAndProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        PathPreferenceApplier.Apply(PathPreference.All, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeTrue();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
    }

    // ── Removal transitions (driven by observed reality) ──

    [Fact]
    public void All_To_Shell_RemovesEnvVarsButKeepsProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        PathPreferenceApplier.Apply(
            PathPreference.Shell, targetDotnetupOnPath: true,
            Observed(dotnetEnvVarsPresent: true, dotnetEnvVarsComplete: true, profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);  // env-var removal
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);    // profile still written
    }

    [Fact]
    public void All_To_None_DotnetupOff_RemovesEnvVarsAndProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(
            PathPreference.None, targetDotnetupOnPath: false,
            Observed(dotnetEnvVarsPresent: true, dotnetEnvVarsComplete: true, profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileHasManagedBlock().Should().BeFalse();
    }

    [Fact]
    public void Shell_To_None_DotnetupOff_RemovesProfile()
    {
        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(
            PathPreference.None, targetDotnetupOnPath: false,
            Observed(profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileHasManagedBlock().Should().BeFalse();
    }

    [Fact]
    public void Shell_DotnetupOn_To_None_DotnetupOn_RewritesProfileAsDotnetupOnly()
    {
        // Turning off dotnet exposure but keeping dotnetup-on-PATH should rewrite (not remove)
        // the block as dotnetup-only.
        PathPreferenceApplier.Apply(
            PathPreference.None, targetDotnetupOnPath: true,
            Observed(profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastIncludeDotnetForTerminalProfileModifications.Should().BeFalse();
        _env.LastIncludeDotnetupForTerminalProfileModifications.Should().BeTrue();
    }

    // ── Drift correction: remove what is actually observed, even when no prior config recorded it ──

    [Fact]
    public void StrayProfileBlock_RemovedEvenWithoutPriorConfig()
    {
        // The config never recorded a block (Empty would say "unknown"), but one is actually
        // present. Targeting none + dotnetup-off must still remove it.
        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(
            PathPreference.None, targetDotnetupOnPath: false,
            Observed(profileBlockPresent: true),
            _env, DotnetRoot, _shellProvider);

        ProfileHasManagedBlock().Should().BeFalse();
    }

    [Fact]
    public void ObservedNoProfileBlock_NothingRemoved()
    {
        // Nothing observed and nothing targeted: the removal branch must not run.
        PathPreferenceApplier.Apply(
            PathPreference.None, targetDotnetupOnPath: false,
            Observed(profileBlockPresent: false),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
    }

    [Fact]
    public void StrayDotnetEnvVars_RemovedWhenTargetShell()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Reality has 'all'-mode env vars wired but the target is shell: remove them, even though
        // no prior 'all' config is supplied.
        PathPreferenceApplier.Apply(
            PathPreference.Shell, targetDotnetupOnPath: true,
            Observed(dotnetEnvVarsPresent: true),
            _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
    }

    // ── Edge cases ──

    [Fact]
    public void Target_All_On_NonWindows_Throws()
    {
        if (OperatingSystem.IsWindows()) return;

        Action act = () => PathPreferenceApplier.Apply(
            PathPreference.All, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, _shellProvider);

        act.Should().Throw<PlatformNotSupportedException>();
    }

    [Fact]
    public void ProfileWrite_Without_ShellProvider_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            // GetCurrentShellProvider always resolves PowerShell on Windows, so the null path
            // is unreachable there.
            return;
        }

        var originalShell = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", null);

            Action act = () => PathPreferenceApplier.Apply(
                PathPreference.Shell, targetDotnetupOnPath: true, ObservedEnvironmentState.Empty, _env, DotnetRoot, shellProvider: null);

            act.Should().Throw<DotnetInstallException>()
                .Which.Message.Should().Contain("--shell");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", originalShell);
        }
    }

    [Fact]
    public void ArgumentNullException_When_EnvironmentIsNull()
    {
        Action act = () => PathPreferenceApplier.Apply(PathPreference.None, false, ObservedEnvironmentState.Empty, environment: null!, DotnetRoot, _shellProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ArgumentNullException_When_ObservedIsNull()
    {
        Action act = () => PathPreferenceApplier.Apply(PathPreference.None, false, observed: null!, _env, DotnetRoot, _shellProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ArgumentException_When_DotnetRootIsEmpty()
    {
        Action act = () => PathPreferenceApplier.Apply(PathPreference.None, false, ObservedEnvironmentState.Empty, _env, dotnetRoot: "", _shellProvider);
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
