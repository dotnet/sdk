// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Behavioral tests for <see cref="EnvSetCommand"/>, <see cref="EnvClearCommand"/>, and
/// <see cref="EnvShowCommand"/>. Focus is on what each command persists to the config and which
/// underlying environment APIs it invokes — drift-detection branches that depend on the real
/// Windows registry / arbitrary host shell profiles are intentionally not asserted because they
/// cannot be sandboxed cleanly in CI.
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
        var parseResult = Parser.Parse(["env", "set", "none"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.Env.Should().Be(PathPreference.None);
        // dotnetup-on-PATH defaults to on when not specified and not previously stored.
        config.DotnetupOnPath.Should().BeTrue();
        // None + dotnetup-on-PATH still wires the dotnetup PATH entry (idempotent) but no dotnet env vars.
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.LastDotnetupOnUserPathEnabled.Should().BeTrue();
    }

    [Fact]
    public void EnvSet_DotnetupOnPathOff_PersistsAndUnwires()
    {
        var parseResult = Parser.Parse(["env", "set", "none", "--dotnetup-on-path", "off"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.Env.Should().Be(PathPreference.None);
        config.DotnetupOnPath.Should().BeFalse();
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [Fact]
    public void EnvSet_DotnetupOnPathOnly_LeavesStoredModeUnchanged()
    {
        // Pre-existing config: shell mode, dotnetup on. Change only --dotnetup-on-path off.
        DotnetupConfig.Write(new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "set", "--dotnetup-on-path", "off", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.Env.Should().Be(PathPreference.Shell);   // mode preserved (re-sync)
        config.DotnetupOnPath.Should().BeFalse();          // only this changed
    }

    [Fact]
    public void EnvSet_All_OnNonWindows_RejectedByParser()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parseResult = Parser.Parse(["env", "set", "all"]);

        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
        DotnetupConfig.Read().Should().BeNull();
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
        var config = DotnetupConfig.Read();
        config!.Env.Should().Be(PathPreference.All);
        config.DotnetupOnPath.Should().BeTrue();
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    [Fact]
    public void EnvSet_NoModeAndNoStoredConfig_Fails()
    {
        var parseResult = Parser.Parse(["env", "set"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env).Execute();

        // Nothing to apply → DotnetInstallException → exit code 1.
        exitCode.Should().Be(1);
        DotnetupConfig.Read().Should().BeNull();
    }

    // ── EnvClearCommand ──

    [Fact]
    public void EnvClear_UnwiresEverythingAndPersists()
    {
        DotnetupConfig.Write(new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "clear", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvClearCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.Env.Should().Be(PathPreference.None);
        config.DotnetupOnPath.Should().BeFalse();
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    // ── EnvShowCommand ──

    [Fact]
    public void EnvShow_NoConfig_ExitCodeZero()
    {
        var parseResult = Parser.Parse(["env", "show"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvShowCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
    }

    [Fact]
    public void EnvShow_ConfiguredNone_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { Env = PathPreference.None, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
    }

    [Fact]
    public void EnvShow_ConfiguredShell_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env).Execute();

        exitCode.Should().Be(0);
    }
}
