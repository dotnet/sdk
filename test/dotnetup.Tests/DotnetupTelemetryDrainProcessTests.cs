// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Microsoft.DotNet.Tools.Dotnetup.Tests;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

[TestClass]
public class DotnetupTelemetryDrainProcessTests
{
    [TestMethod]
    public void ResolveTelemetryStorageDirectory_HonorsEnvOverride()
    {
        var expected = Path.Combine(Path.GetTempPath(), "custom-telemetry-storage");

        var resolved = DotnetupPaths.ResolveTelemetryStorageDirectory(
            name => name == Constants.Telemetry.StoragePathEnvVar ? expected : null);

        Assert.AreEqual(expected, resolved);
    }

    [TestMethod]
    public void ResolveTelemetryStorageDirectory_IgnoresWhitespaceOverride()
    {
        // A blank/whitespace override must not be treated as a real path.
        var dotnetCliHome = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
        var resolved = DotnetupPaths.ResolveTelemetryStorageDirectory(
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
    public void ResolveTelemetryStorageDirectory_FallsBackToSdkDirectory()
    {
        var dotnetCliHome = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
        var resolved = DotnetupPaths.ResolveTelemetryStorageDirectory(
            name => name == CliFolderPathCalculatorCore.DotnetHomeVariableName ? dotnetCliHome : null);

        Assert.IsFalse(string.IsNullOrWhiteSpace(resolved), "a storage directory must always resolve");
        Assert.AreEqual(
            Path.Combine(dotnetCliHome, CliFolderPathCalculatorCore.DotnetProfileDirectoryName, "TelemetryStorageService"),
            resolved);
    }

    [TestMethod]
    public void TryRunAsDrainer_ReturnsFalse_WhenDrainCommandIsAbsent()
    {
        var ranAsDrainer = DotnetupTelemetryDrainProcess.TryRunAsDrainer([], out var exitCode);

        Assert.IsFalse(ranAsDrainer);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void TryRunAsDrainer_ReturnsFalse_WhenDrainCommandHasAdditionalArguments()
    {
        var ranAsDrainer = DotnetupTelemetryDrainProcess.TryRunAsDrainer(
            [Constants.Telemetry.DrainCommand, "--help"],
            out var exitCode);

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

    [TestMethod]
    [DataRow("renamed-bootstrapper.exe")]
    [DataRow("DOTNETUP")]
    public void CanRelaunchAsDrainer_AcceptsRenamedNativeExecutable(string executableName)
    {
        Assert.IsTrue(DotnetupTelemetryDrainProcess.CanRelaunchAsDrainer(executableName, "dotnetup"));
    }

    [TestMethod]
    [DataRow(null, "dotnetup")]
    [DataRow("", "dotnetup")]
    [DataRow("dotnet.exe", "dotnetup")]
    [DataRow("testhost.exe", "testhost")]
    [DataRow("dotnetup.exe", "testhost")]
    public void CanRelaunchAsDrainer_RejectsManagedAndTestHosts(string? executablePath, string? entryAssemblyName)
    {
        Assert.IsFalse(DotnetupTelemetryDrainProcess.CanRelaunchAsDrainer(executablePath, entryAssemblyName));
    }
}
