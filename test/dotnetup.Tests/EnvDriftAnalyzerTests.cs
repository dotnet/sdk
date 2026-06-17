// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for <see cref="EnvDriftAnalyzer"/>, the pure comparison of configured settings vs an
/// observed environment snapshot. No environment reads are involved, so every case is fully
/// controlled via a hand-built <see cref="ObservedEnvironmentState"/>. Windows-only drift
/// (env vars, user PATH) is only asserted when running on Windows.
/// </summary>
public class EnvDriftAnalyzerTests
{
    private static ObservedEnvironmentState Observed(
        bool dotnetEnvVarsPresent = false,
        bool dotnetEnvVarsComplete = false,
        bool? profileBlockPresent = null,
        bool dotnetupOnUserPath = false)
        => new(dotnetEnvVarsPresent, dotnetEnvVarsComplete, profileBlockPresent, dotnetupOnUserPath);

    [Fact]
    public void ProfileExpectedButMissing_ReportsMissingBlock()
    {
        var config = new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: false, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("missing the dotnetup managed block", StringComparison.Ordinal));
    }

    [Fact]
    public void ProfileNotExpectedButPresent_ReportsStrayBlock()
    {
        var config = new DotnetupConfigData { Env = PathPreference.None, DotnetupOnPath = false };
        var observed = Observed(profileBlockPresent: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("contains a dotnetup managed block", StringComparison.Ordinal));
    }

    [Fact]
    public void ProfileStateUnknown_NoProfileDrift()
    {
        var config = new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: null, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().NotContain(d => d.Contains("managed block", StringComparison.Ordinal));
    }

    [Fact]
    public void InSync_NoDrift()
    {
        // Shell + dotnetup-on, profile present, dotnetup on the user PATH (Windows). Non-Windows
        // ignores the user-PATH axis, so this is in sync on both.
        var config = new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: true, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().BeEmpty();
    }

    [Fact]
    public void ConfiguredAllButIncomplete_ReportsEnvVarDrift()
    {
        if (!OperatingSystem.IsWindows()) return;

        var config = new DotnetupConfigData { Env = PathPreference.All, DotnetupOnPath = true };
        var observed = Observed(dotnetEnvVarsComplete: false, profileBlockPresent: true, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("'all' mode expectations", StringComparison.Ordinal));
    }

    [Fact]
    public void NotAllButResidualEnvVars_ReportsStrayWiring()
    {
        if (!OperatingSystem.IsWindows()) return;

        var config = new DotnetupConfigData { Env = PathPreference.Shell, DotnetupOnPath = true };
        var observed = Observed(dotnetEnvVarsPresent: true, profileBlockPresent: true, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("still has 'all'-mode wiring", StringComparison.Ordinal));
    }

    [Fact]
    public void DotnetupExpectedOnPathButMissing_ReportsDrift()
    {
        if (!OperatingSystem.IsWindows()) return;

        var config = new DotnetupConfigData { Env = PathPreference.None, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: true, dotnetupOnUserPath: false);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("missing from the user PATH", StringComparison.Ordinal));
    }

    [Fact]
    public void DotnetupOnPathButConfiguredOff_ReportsDrift()
    {
        if (!OperatingSystem.IsWindows()) return;

        var config = new DotnetupConfigData { Env = PathPreference.None, DotnetupOnPath = false };
        var observed = Observed(profileBlockPresent: false, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("on the user PATH but is configured to be off", StringComparison.Ordinal));
    }
}
