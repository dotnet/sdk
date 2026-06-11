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
/// Unit tests for the 9-cell (previous × target) transition matrix in
/// <see cref="PathPreferenceApplier"/>, plus edge cases for unsupported platform
/// and missing shell provider.
///
/// Conventions used by these tests:
///   * "EnvVars apply" = ApplyEnvironmentModifications(InstallType.User, dotnetRoot)
///   * "EnvVars unwind" = ApplyEnvironmentModifications(InstallType.System) [no dotnetRoot]
///   * "Profile apply" = ApplyTerminalProfileModifications(...) on the mock
///   * "Profile unwind" = ShellProfileManager.RemoveProfileEntries(...) on a real
///     test shell provider backed by a temp directory; verified via the actual
///     file system because RemoveProfileEntries is a static helper, not a mocked API.
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

    // ── No-op transitions (target == previous, or both no-op modes) ──

    [Fact]
    public void None_To_None_DoesNothing()
    {
        PathPreferenceApplier.Apply(PathPreference.None, previous: PathPreference.None, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileFileExists().Should().BeFalse();
    }

    [Fact]
    public void None_From_Null_DoesNothing()
    {
        // First-time configuration where no previous preference was stored.
        PathPreferenceApplier.Apply(PathPreference.None, previous: null, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
    }

    // ── Apply paths (no previous to unwind) ──

    [Fact]
    public void None_To_Shell_WritesProfile()
    {
        PathPreferenceApplier.Apply(PathPreference.Shell, previous: PathPreference.None, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        _env.LastDotnetRootForTerminalProfileModifications.Should().Be(DotnetRoot);
    }

    [Fact]
    public void None_To_All_WritesEnvVarsAndProfile()
    {
        if (!OperatingSystem.IsWindows())
        {
            // All is Windows-only; the Throws test below covers the non-Windows path.
            return;
        }

        PathPreferenceApplier.Apply(PathPreference.All, previous: PathPreference.None, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.LastDotnetRootForEnvironmentModifications.Should().Be(DotnetRoot);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    // ── Re-sync paths (target == previous, non-None) ──

    [Fact]
    public void Shell_To_Shell_RewritesProfile()
    {
        PathPreferenceApplier.Apply(PathPreference.Shell, previous: PathPreference.Shell, _env, DotnetRoot, _shellProvider);

        // No unwind because profile is still desired; one fresh apply call to re-sync.
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    [Fact]
    public void All_To_All_RewritesEnvVarsAndProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        PathPreferenceApplier.Apply(PathPreference.All, previous: PathPreference.All, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    // ── Unwind paths (target lacks what previous wrote) ──

    [Fact]
    public void Shell_To_None_RemovesProfile()
    {
        WriteManagedBlockToProfile();
        ProfileFileExists().Should().BeTrue();

        PathPreferenceApplier.Apply(PathPreference.None, previous: PathPreference.Shell, _env, DotnetRoot, _shellProvider);

        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileFileHasManagedBlock().Should().BeFalse();
    }

    [Fact]
    public void All_To_None_UnwindsEnvVarsAndRemovesProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(PathPreference.None, previous: PathPreference.All, _env, DotnetRoot, _shellProvider);

        // Unwind via InstallType.System (the inverse of the User-mode apply).
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
        ProfileFileHasManagedBlock().Should().BeFalse();
    }

    [Fact]
    public void All_To_Shell_UnwindsEnvVarsButKeepsProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(PathPreference.Shell, previous: PathPreference.All, _env, DotnetRoot, _shellProvider);

        // Env vars get unwound; profile is still desired so it is re-applied (not removed).
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        // The pre-existing managed block we wrote manually is preserved (ApplyTerminal
        // would normally replace it, but our mock no-ops, so the original write survives).
        ProfileFileHasManagedBlock().Should().BeTrue();
    }

    [Fact]
    public void Shell_To_All_WritesEnvVarsAndRewritesProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(PathPreference.All, previous: PathPreference.Shell, _env, DotnetRoot, _shellProvider);

        // No env-var unwind (previous didn't write them); fresh env-var apply + profile re-apply.
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    [Fact]
    public void None_To_Shell_ThenShell_To_None_LeavesNoTrace()
    {
        // Round-trip: apply Shell then unwind to None; the profile file should end empty
        // of any dotnetup-managed block (we use a real RemoveProfileEntries here).
        WriteManagedBlockToProfile();

        PathPreferenceApplier.Apply(PathPreference.Shell, previous: PathPreference.Shell, _env, DotnetRoot, _shellProvider);
        PathPreferenceApplier.Apply(PathPreference.None, previous: PathPreference.Shell, _env, DotnetRoot, _shellProvider);

        ProfileFileHasManagedBlock().Should().BeFalse();
    }

    // ── Edge cases ──

    [Fact]
    public void Target_All_On_NonWindows_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            // Negative test only meaningful off Windows; on Windows this path is supported.
            return;
        }

        Action act = () => PathPreferenceApplier.Apply(
            PathPreference.All, previous: PathPreference.None, _env, DotnetRoot, _shellProvider);

        act.Should().Throw<PlatformNotSupportedException>();
    }

    [Fact]
    public void Unwind_Profile_Without_ShellProvider_Throws()
    {
        // Simulate the case where the user previously configured a profile but we can't
        // detect the active shell now (no --shell flag, no SHELL env var that maps to a
        // supported shell). We must not silently leave the managed block behind.
        // Skip on Windows: ShellDetection.GetCurrentShellProvider() always returns the
        // PowerShell provider on Windows, so the null-provider path is unreachable.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WriteManagedBlockToProfile();

        // Clear SHELL so the applier's auto-detect fallback also returns null.
        var originalShell = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", null);

            Action act = () => PathPreferenceApplier.Apply(
                PathPreference.None,
                previous: PathPreference.Shell,
                _env,
                DotnetRoot,
                shellProvider: null);

            // Expect DotnetInstallException so CommandBase records it in telemetry;
            // confirm the message mentions --shell so the user knows how to recover.
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
        Action act = () => PathPreferenceApplier.Apply(PathPreference.None, null, environment: null!, DotnetRoot, _shellProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ArgumentException_When_DotnetRootIsEmpty()
    {
        Action act = () => PathPreferenceApplier.Apply(PathPreference.None, null, _env, dotnetRoot: "", _shellProvider);
        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ──

    private string ProfilePath => Path.Combine(_tempDir, "profile.sh");

    private bool ProfileFileExists() => File.Exists(ProfilePath);

    private bool ProfileFileHasManagedBlock()
        => ProfileFileExists() && File.ReadAllText(ProfilePath).Contains("# dotnetup: begin", StringComparison.Ordinal);

    private void WriteManagedBlockToProfile()
    {
        // Hand-write a managed block matching ShellProfileManager's markers so we can
        // assert that the real RemoveProfileEntries call inside the applier removes it.
        File.WriteAllText(ProfilePath, "# pre-existing user content\n# dotnetup: begin\necho 'managed by dotnetup'\n# dotnetup: end\n");
    }
}
