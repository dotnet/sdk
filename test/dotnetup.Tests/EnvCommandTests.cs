// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Behavioral tests for <see cref="EnvSetCommand"/> and <see cref="EnvShowCommand"/>.
/// Focus is on what each command does to its persisted state (config file) and which
/// underlying environment APIs it invokes — drift-detection branches that depend on
/// real Windows registry / arbitrary host shell profiles are intentionally not asserted
/// because they cannot be sandboxed cleanly in CI.
/// </summary>
public class EnvCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MockDotnetInstallManager _env;

    public EnvCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-env-cmd-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe under xunit's parallel test execution because each
        // test gets its own AsyncLocal context.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);

        _env = new MockDotnetInstallManager(defaultInstallPath: Path.Combine(_tempDir, "dotnet"));
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // ── EnvSetCommand ──

    [Fact]
    public void EnvSet_None_PersistsConfig()
    {
        // Simplest happy path: target=None means the applier does nothing (no profile or
        // env-var work), so this is cross-platform safe and exercises only the persistence.
        var parseResult = Parser.Parse(["env", "set", "none"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        DotnetupConfig.ReadPathPreference().Should().Be(PathPreference.None);
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(0);
    }

    [Fact]
    public void EnvSet_All_OnNonWindows_RejectedByParser()
    {
        if (OperatingSystem.IsWindows())
        {
            // The positive case is covered separately; here we only exercise the rejection.
            return;
        }

        var parseResult = Parser.Parse(["env", "set", "all"]);

        // On non-Windows, the parser rejects 'all' up-front so the user gets a clear
        // "not supported on this platform" error before any side effects can happen.
        // The runtime guard inside EnvSetCommand.ExecuteCore is kept as defense-in-depth
        // (in case a future change accidentally relaxes the parser), but is unreachable
        // through normal command-line invocation now.
        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
        // Nothing should have been persisted, since Execute was never called.
        DotnetupConfig.ReadPathPreference().Should().BeNull();
    }

    [Fact]
    public void EnvSet_All_FromNone_OnWindows_AppliesAndPersists()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var parseResult = Parser.Parse(["env", "set", "all"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        DotnetupConfig.ReadPathPreference().Should().Be(PathPreference.All);
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    [Fact]
    public void EnvSet_OverwritesExistingPreference()
    {
        // Start with None already configured and switch to None again: a re-sync should
        // succeed (idempotent) and leave the file at None.
        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = PathPreference.None });

        var parseResult = Parser.Parse(["env", "set", "none"]);
        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        DotnetupConfig.ReadPathPreference().Should().Be(PathPreference.None);
    }

    // ── EnvShowCommand ──

    [Fact]
    public void EnvShow_NoConfig_ExitCodeZero()
    {
        var parseResult = Parser.Parse(["env", "show"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvShowCommand(parseResult, _env).Execute();

        // 'env show' is read-only and must succeed even when no config exists; it
        // prints a hint and returns 0 so scripts can run it safely.
        exitCode.Should().Be(0);
    }

    [Fact]
    public void EnvShow_ConfiguredNone_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = PathPreference.None });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
    }

    [Fact]
    public void EnvShow_ConfiguredShell_ExitCodeZero()
    {
        // Drift may or may not be reported depending on whether the user's actual
        // shell profile has a dotnetup managed block; either way the command must
        // succeed (drift is informational, not a failure).
        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = PathPreference.Shell });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
    }
}
