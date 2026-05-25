// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Cli.Test.Tests;

public class LoggerUtilityTests
{
    [Theory]
    [InlineData("-tl")]
    [InlineData("--tl")]
    [InlineData("/tl")]
    [InlineData("-tl:on")]
    [InlineData("-tl:off")]
    [InlineData("-tl:false")]
    [InlineData("-tl:true")]
    [InlineData("-tl:auto")]
    [InlineData("--tl:off")]
    [InlineData("/tl:off")]
    [InlineData("-terminalLogger")]
    [InlineData("-terminallogger")]
    [InlineData("--terminalLogger:off")]
    [InlineData("/terminalLogger:auto")]
    [InlineData("-ll")]
    [InlineData("--livelogger:on")]
    [InlineData("/ll:off")]
    [InlineData("-tlp")]
    [InlineData("-tlp:default=off")]
    [InlineData("--terminalLoggerParameters:default=on")]
    [InlineData("/tlp:default=auto")]
    // Older `=` value separator (MSBuild accepts both : and = on long-form switches).
    [InlineData("--tl=off")]
    [InlineData("--terminalLogger=auto")]
    public void IsTerminalLoggerArgument_ReturnsTrue_ForTerminalLoggerSwitches(string arg)
    {
        Assert.True(LoggerUtility.IsTerminalLoggerArgument(arg), $"Expected '{arg}' to be classified as a terminal-logger argument.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("-bl")]
    [InlineData("--binaryLogger")]
    [InlineData("-tlNoSuchOption")]      // not a bare switch — must not be a substring match
    [InlineData("--terminalLoggerExtra")] // ditto
    [InlineData("-tlpzzz")]
    [InlineData("--show-live-output")]
    [InlineData("MyProject.csproj")]
    [InlineData("-p:Foo=Bar")]
    [InlineData("tl")]                    // missing prefix
    [InlineData("tl:off")]                // missing prefix
    [InlineData("---tl")]                 // too many dashes
    [InlineData("/tlinvalid")]
    public void IsTerminalLoggerArgument_ReturnsFalse_ForNonTerminalLoggerArguments(string arg)
    {
        Assert.False(LoggerUtility.IsTerminalLoggerArgument(arg), $"Expected '{arg}' to NOT be classified as a terminal-logger argument.");
    }
}
