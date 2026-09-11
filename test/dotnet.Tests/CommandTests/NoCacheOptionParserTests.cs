// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Tests.ParserTests;

[TestClass]
public class NoCacheOptionParserTests
{
    [TestMethod]
    [DataRow("1", false, true)]
    [DataRow("0", false, false)]
    [DataRow("false", true, true)]
    public void NoCacheOptionHonorsEnvironmentVariable(
        string environmentVariableValue,
        bool explicitlyEnabled,
        bool expected)
    {
        string? previousValue = Environment.GetEnvironmentVariable(
            NuGetRestoreOptions.NoCacheEnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(
                NuGetRestoreOptions.NoCacheEnvironmentVariableName,
                environmentVariableValue);

            var command = new Command("test");
            var options = new NuGetRestoreOptions(forward: true);
            options.AddTo(command.Options);
            var result = command.Parse(explicitlyEnabled ? ["--no-cache"] : []);

            result.GetRequiredValue(options.NoCacheOption).Should().Be(expected);
            var forwardedOptions = result.OptionValuesToBeForwarded(command);
            if (expected)
            {
                forwardedOptions.Should().ContainSingle(option => option == "--no-cache");
            }
            else
            {
                forwardedOptions.Should().NotContain("--no-cache");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                NuGetRestoreOptions.NoCacheEnvironmentVariableName,
                previousValue);
        }
    }
}
