// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Tests;

public sealed class LoggerUtilityTests
{
    [Theory]
    [InlineData("-tl", "-tl:auto")]
    [InlineData("/tl", "/tl:auto")]
    [InlineData("--terminalLogger", "--terminalLogger:auto")]
    [InlineData("-tl:off", "-tl:off")]
    [InlineData("-TL:off", "-TL:off")]
    [InlineData("/tl:off", "/tl:off")]
    [InlineData("--terminalLogger:off", "--terminalLogger:off")]
    [InlineData("-tlp:verbosity=quiet", "-tlp:verbosity=quiet")]
    [InlineData("/tlp:DISABLENODEDISPLAY", "/tlp:DISABLENODEDISPLAY")]
    [InlineData("--terminalLoggerParameters:verbosity=quiet", "--terminalLoggerParameters:verbosity=quiet")]
    [InlineData("-clp:NoSummary", "-clp:NoSummary")]
    [InlineData("--consoleLoggerParameters:NoSummary", "--consoleLoggerParameters:NoSummary")]
    public void LoggerArgument_ArgumentForms(string arg, string expectedArg)
    {
        LoggerUtility.SeparateLoggerArguments([arg], out var loggerArgs, out var nonLoggerArgs);

        loggerArgs.Should().Equal(expectedArg);
        nonLoggerArgs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("-tl:invalid")]
    [InlineData("-tlp")]
    [InlineData("-clp")]
    [InlineData("--unknownLogger:off")]
    public void LoggerArgument_InvalidFormsAreNotRecognized(string arg)
    {
        LoggerUtility.SeparateLoggerArguments([arg], out var loggerArgs, out var nonLoggerArgs);

        loggerArgs.Should().BeEmpty();
        nonLoggerArgs.Should().Equal(arg);
    }
}
