// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

public class TestFrameworkDisplayTests
{
    [Theory]
    [InlineData("net472", "x64", "net481", "net472|x64, runtime: net481")]
    [InlineData("net471", "x64", "net472", "net471|x64, runtime: net472")]
    [InlineData("net48", "x64", "net481", "net48|x64, runtime: net481")]
    [InlineData("net481", "x86", "net481", "net481|x86")]
    [InlineData("net10.0", "arm64", "net10.0", "net10.0|arm64")]
    [InlineData("net10.0-windows10.0.19041.0", "x64", "net10.0", "net10.0-windows10.0.19041.0|x64")]
    [InlineData("NET10.0-windows", "x64", "net10.0", "NET10.0-windows|x64")]
    [InlineData("net8.0", "x64", "net10.0", "net8.0|x64, runtime: net10.0")]
    [InlineData("netstandard2.0", "x64", "net10.0", "netstandard2.0|x64, runtime: net10.0")]
    [InlineData(null, "x64", "net10.0", "x64, runtime: net10.0")]
    [InlineData(null, null, "net10.0", "runtime: net10.0")]
    [InlineData(null, " ", "net10.0", "runtime: net10.0")]
    [InlineData("net10.0", null, "net10.0", "net10.0")]
    [InlineData("net10.0", "x64", "Unknown runtime", "net10.0|x64, runtime: Unknown runtime")]
    [InlineData("Unknown", "x64", "Unknown", "Unknown|x64, runtime: Unknown")]
    [InlineData(null, null, null, "")]
    [InlineData("", "", "", "")]
    [InlineData("net472", "x64", null, "net472|x64")]
    public void GetDisplayName_PreservesIdentityAndOnlySuppressesKnownMatchingRuntime(
        string? targetFramework, string? architecture, string? runtimeFramework, string expected)
    {
        TestFrameworkDisplay.GetDisplayName(targetFramework, architecture, runtimeFramework).Should().Be(expected);
    }

    [Theory]
    [InlineData("net10.0", ".NET 10.0.15", "net10.0|x64")]
    [InlineData("net8.0", ".NET 10.0.15", "net8.0|x64, runtime: net10.0")]
    [InlineData("net48", ".NET Framework 4.8.10008.0", "net48|x64, runtime: net481")]
    [InlineData("net471", ".NET Framework 4.7.2", "net471|x64, runtime: net472")]
    public void RuntimeNormalization_PreservesFrameworkPatchDistinctions(
        string targetFramework, string runtimeDescription, string expected)
    {
        TestFrameworkDisplay.GetDisplayName(
            targetFramework, "x64", TargetFrameworkParser.GetShortTargetFramework(runtimeDescription)).Should().Be(expected);
    }
}
