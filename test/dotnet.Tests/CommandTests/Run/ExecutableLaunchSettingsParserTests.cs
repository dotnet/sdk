// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

namespace Microsoft.DotNet.Cli.Run.Tests;

public class ExecutableLaunchSettingsParserTests
{
    [Fact]
    public void MissingExecutablePath()
    {
        var parser = new ExecutableLaunchSettingsParser();

        var result = parser.ParseProfile("path", "name", """
            {
                "Execute": {
                    "commandName": "Executable"
                }
            }
            """);

        Assert.False(result.Successful);
        Assert.Equal(string.Format(CliCommandStrings.LaunchProfile0IsMissingProperty1, "name", "executablePath"), result.FailureReason);
    }
}
