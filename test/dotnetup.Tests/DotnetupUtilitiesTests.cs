// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>Verifies runtime identifier construction for the current operating-system family.</summary>
[TestClass]
public class DotnetupUtilitiesTests
{
    [TestMethod]
    [DataRow(InstallArchitecture.x64, "linux-musl-x64", "linux-musl-x64")]
    [DataRow(InstallArchitecture.arm64, "linux-musl-x64", "linux-musl-arm64")]
    public void GetRuntimeIdentifierPreservesLinuxMusl(
        InstallArchitecture architecture,
        string currentRuntimeIdentifier,
        string expected)
    {
        Assert.AreEqual(
            expected,
            DotnetupUtilities.GetRuntimeIdentifier(architecture, currentRuntimeIdentifier));
    }

    [TestMethod]
    [DataRow("linux-bionic-x64")]
    [DataRow("ubuntu.24.04-x64")]
    public void GetRuntimeIdentifierDoesNotPreserveDistroSpecificRuntimeIdentifier(string currentRuntimeIdentifier)
    {
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "unknown";

        Assert.AreEqual(
            $"{os}-arm64",
            DotnetupUtilities.GetRuntimeIdentifier(InstallArchitecture.arm64, currentRuntimeIdentifier));
    }
}
