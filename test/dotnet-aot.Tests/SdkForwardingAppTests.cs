// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Fsi;
using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[ResourceLock(nameof(SdkDirectoryScope))]
[ResourceLock(WellKnownResources.EnvironmentVariables)]
public class SdkForwardingAppTests
{
    [TestMethod]
    public void NuGetForwardingUsesVersionedSdkDirectory()
    {
        string sdkDirectory = CreateSdkDirectory();
        using var _ = new SdkDirectoryScope(sdkDirectory);

        Assert.AreEqual(
            Path.Combine(sdkDirectory, "NuGet.CommandLine.XPlat.dll"),
            NuGetForwardingApp.GetNuGetExePath());
    }

    [TestMethod]
    public void FsiForwardingUsesVersionedSdkDirectory()
    {
        string sdkDirectory = CreateSdkDirectory();
        using var _ = new SdkDirectoryScope(sdkDirectory);

        Assert.AreEqual(
            Path.Combine(sdkDirectory, "FSharp", "fsi.exe"),
            FsiForwardingApp.GetFsiAppPath());
    }

    [TestMethod]
    public void VSTestForwardingUsesVersionedSdkDirectory()
    {
        string sdkDirectory = CreateSdkDirectory();
        using var _ = new SdkDirectoryScope(sdkDirectory);
        string? previousVSTestConsolePath = Environment.GetEnvironmentVariable("VSTEST_CONSOLE_PATH");

        try
        {
            Environment.SetEnvironmentVariable("VSTEST_CONSOLE_PATH", null);
            Assert.AreEqual(
                Path.Combine(sdkDirectory, "vstest.console.dll"),
                VSTestForwardingApp.GetVSTestExePath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("VSTEST_CONSOLE_PATH", previousVSTestConsolePath);
        }
    }

    private static string CreateSdkDirectory()
        => Path.Combine(Path.GetTempPath(), "dotnet", "sdk", $"test-version-{Guid.NewGuid():N}");
}
