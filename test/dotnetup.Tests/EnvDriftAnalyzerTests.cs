// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for <see cref="EnvDriftAnalyzer"/>, the pure comparison of configured settings vs an
/// observed environment snapshot. No environment reads are involved, so every case is fully
/// controlled via a hand-built <see cref="ObservedEnvironmentState"/>. Windows-only drift
/// (env vars, user PATH) is only asserted when running on Windows.
/// </summary>
[TestClass]
public class EnvDriftAnalyzerTests
{
    private static ObservedEnvironmentState Observed(
        bool dotnetEnvVarsPresent = false,
        bool dotnetEnvVarsComplete = false,
        bool? profileBlockPresent = null,
        bool dotnetupOnUserPath = false)
        => new(dotnetEnvVarsPresent, dotnetEnvVarsComplete, profileBlockPresent, dotnetupOnUserPath);

    [TestMethod]
    public void ProfileExpectedButMissing_ReportsMissingBlock()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: false, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("missing the dotnetup managed block", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ProfileNotExpectedButPresent_ReportsStrayBlock()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = false };
        var observed = Observed(profileBlockPresent: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("contains a dotnetup managed block", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ProfileStateUnknown_NoProfileDrift()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: null, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().NotContain(d => d.Contains("managed block", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InSync_NoDrift()
    {
        // Shell + dotnetup-on, profile present, dotnetup on the user PATH (Windows). Non-Windows
        // ignores the user-PATH axis, so this is in sync on both.
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: true, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().BeEmpty();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void ConfiguredAllButIncomplete_ReportsEnvVarDrift()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.Full, DotnetupOnPath = true };
        var observed = Observed(dotnetEnvVarsComplete: false, profileBlockPresent: true, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("'full' mode expectations", StringComparison.Ordinal));
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void NotAllButResidualEnvVars_ReportsStrayWiring()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true };
        var observed = Observed(dotnetEnvVarsPresent: true, profileBlockPresent: true, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("still has 'full'-mode wiring", StringComparison.Ordinal));
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void DotnetupExpectedOnPathButMissing_ReportsDrift()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = true };
        var observed = Observed(profileBlockPresent: true, dotnetupOnUserPath: false);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("missing from the user PATH", StringComparison.Ordinal));
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void DotnetupOnPathButConfiguredOff_ReportsDrift()
    {
        var config = new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = false };
        var observed = Observed(profileBlockPresent: false, dotnetupOnUserPath: true);

        var drift = EnvDriftAnalyzer.Compare(config, observed);

        drift.Should().Contain(d => d.Contains("on the user PATH but is configured to be off", StringComparison.Ordinal));
    }
}
