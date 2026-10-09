// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    [DataRow(InstallArchitecture.x64, "linux-arm64", "linux-x64")]
    [DataRow(InstallArchitecture.arm64, "osx-x64", "osx-arm64")]
    [DataRow(InstallArchitecture.x86, "win-x64", "win-x86")]
    public void GetRuntimeIdentifierPreservesOperatingSystem(
        InstallArchitecture architecture,
        string currentRuntimeIdentifier,
        string expected)
    {
        Assert.AreEqual(
            expected,
            DotnetupUtilities.GetRuntimeIdentifier(architecture, currentRuntimeIdentifier));
    }
}
