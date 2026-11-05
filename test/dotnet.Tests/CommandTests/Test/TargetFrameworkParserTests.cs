// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class TargetFrameworkParserTests
{
    [TestMethod]
    [DataRow(null, null)]
    [DataRow(".NET Framework", ".NET Framework")]
    [DataRow(".NET Framework 4.6.2", "net462")]
    [DataRow(".NET Framework 4.7.1", "net471")]
    [DataRow(".NET Framework 4.7.2", "net472")]
    [DataRow(".NET Framework 4.8.1", "net481")]
    [DataRow(".NET Framework 4.8.9037.0", "net48")]
    [DataRow(".NET Core", ".NET Core")]
    [DataRow(".NET Core 3.1.32", "netcoreapp3.1")]
    [DataRow(".NET", ".NET")]
    [DataRow(".NET ", ".NET ")]
    [DataRow(".NET .0", ".NET .0")]
    [DataRow(".NET 8.", ".NET 8.")]
    [DataRow(".NET 8.0.15", "net8.0")]
    [DataRow(".NET 10", ".NET 10")]
    [DataRow(".NET 10.", ".NET 10.")]
    [DataRow(".NET 10.0.0", "net10.0")]
    [DataRow("Mono 6.12", "Mono 6.12")]
    [DataRow(".net 8.0.15", ".net 8.0.15")]
    public void GetShortTargetFramework_ReturnsExpectedValue(string? frameworkDescription, string? expected)
    {
        TargetFrameworkParser.GetShortTargetFramework(frameworkDescription).Should().Be(expected);
    }
}
