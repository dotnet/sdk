// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Microsoft.DotNet.Tools.Dotnetup.Tests;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

[TestClass]
public class DotnetupTelemetryDrainProcessTests
{
    [TestMethod]
    public void ResolveLocalTelemetryStorageDirectory_HonorsEnvOverride()
    {
        var expected = Path.Combine(Path.GetTempPath(), "custom-telemetry-storage");

        var resolved = DotnetupPaths.ResolveLocalTelemetryStorageDirectory(
            name => name == Constants.Telemetry.StoragePathEnvVar ? expected : null);

        Assert.AreEqual(expected, resolved);
    }

    [TestMethod]
    public void ResolveLocalTelemetryStorageDirectory_IgnoresWhitespaceOverride()
    {
        // A blank/whitespace override must not be treated as a real path.
        var dotnetCliHome = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
        var resolved = DotnetupPaths.ResolveLocalTelemetryStorageDirectory(
            name => name switch
            {
                Constants.Telemetry.StoragePathEnvVar => "   ",
                CliFolderPathCalculatorCore.DotnetHomeVariableName => dotnetCliHome,
                _ => null,
            });

        Assert.AreEqual(
            Path.Combine(dotnetCliHome, CliFolderPathCalculatorCore.DotnetProfileDirectoryName, "TelemetryStorageService"),
            resolved);
    }

    [TestMethod]
    public void ResolveLocalTelemetryStorageDirectory_FallsBackToSdkDirectory()
    {
        var dotnetCliHome = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
        var resolved = DotnetupPaths.ResolveLocalTelemetryStorageDirectory(
            name => name == CliFolderPathCalculatorCore.DotnetHomeVariableName ? dotnetCliHome : null);

        Assert.IsFalse(string.IsNullOrWhiteSpace(resolved), "a storage directory must always resolve");
        Assert.AreEqual(
            Path.Combine(dotnetCliHome, CliFolderPathCalculatorCore.DotnetProfileDirectoryName, "TelemetryStorageService"),
            resolved);
    }

    [TestMethod]
    public void TryRunAsDrainer_ReturnsFalse_WhenDrainModeUnset()
    {
        // Unit tests never run with DOTNETUP_TELEMETRY_DRAIN=1, so this must be a no-op fast path
        // that neither drains nor throws.
        var ranAsDrainer = DotnetupTelemetryDrainProcess.TryRunAsDrainer(out var exitCode);

        Assert.IsFalse(ranAsDrainer);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void SpawnDetachedDrainer_DoesNotThrow_UnderTestHost()
    {
        // The test host's process path is not "dotnetup", so this must bail without spawning.
        var exception = Record.Exception(DotnetupTelemetryDrainProcess.SpawnDetachedDrainer);

        Assert.IsNull(exception);
    }
}
