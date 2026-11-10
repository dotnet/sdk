// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class TestFrameworkDisplayTests : SdkTest
{
    [TestMethod]
    [DataRow("net472", "x64", "net481", "net472|x64, runtime: net481")]
    [DataRow("net471", "x64", "net472", "net471|x64, runtime: net472")]
    [DataRow("net48", "x64", "net481", "net48|x64, runtime: net481")]
    [DataRow("net481", "x86", "net481", "net481|x86")]
    [DataRow("net8.0", "arm64", "net8.0", "net8.0|arm64")]
    [DataRow("net8.0-windows10.0.19041.0", "x64", "net8.0", "net8.0-windows10.0.19041.0|x64")]
    [DataRow("NET8.0-windows", "x64", "net8.0", "NET8.0-windows|x64")]
    [DataRow("net8.0", "x64", "net9.0", "net8.0|x64, runtime: net9.0")]
    [DataRow("netstandard2.0", "x64", "net8.0", "netstandard2.0|x64, runtime: net8.0")]
    [DataRow(null, "x64", "net8.0", "x64, runtime: net8.0")]
    [DataRow(null, null, "net8.0", "runtime: net8.0")]
    [DataRow(null, " ", "net8.0", "runtime: net8.0")]
    [DataRow("net9.0", null, "net9.0", "net9.0")]
    [DataRow("net9.0", "x64", "Unknown runtime", "net9.0|x64, runtime: Unknown runtime")]
    [DataRow("Unknown", "x64", "Unknown", "Unknown|x64, runtime: Unknown")]
    [DataRow(null, null, null, "")]
    [DataRow("", "", "", "")]
    [DataRow("net472", "x64", null, "net472|x64")]
    public void GetDisplayName_PreservesIdentityAndOnlySuppressesKnownMatchingRuntime(
        string? targetFramework, string? architecture, string? runtimeFramework, string expected)
    {
        TestFrameworkDisplay.GetDisplayName(targetFramework, architecture, runtimeFramework).Should().Be(expected);
    }
}
