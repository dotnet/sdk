// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.VSTest.Tests;

public class TerminalLoggerDetectorTests
{
    [Theory]
    [InlineData("-tl:true", TerminalLoggerMode.On)]
    [InlineData("-tl:on", TerminalLoggerMode.On)]
    [InlineData("-tl:false", TerminalLoggerMode.Off)]
    [InlineData("-tl:off", TerminalLoggerMode.Off)]
    [InlineData("-tl:invalid", TerminalLoggerMode.Invalid)]
    [InlineData("--terminalLogger:true", TerminalLoggerMode.On)]
    [InlineData("--terminalLogger:false", TerminalLoggerMode.Off)]
    [InlineData("/tl:true", TerminalLoggerMode.On)]
    [InlineData("/tl:false", TerminalLoggerMode.Off)]
    public void ProcessTerminalLoggerConfiguration_WithExplicitSwitch_ReturnsExpectedMode(string tlArg, TerminalLoggerMode expectedMode)
    {
        var parseResult = Parser.Parse($"dotnet test {tlArg}");

        var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

        result.Should().Be(expectedMode);
    }

    [Fact]
    public void ProcessTerminalLoggerConfiguration_WithEnvironmentVariable_ReturnsExpectedMode()
    {
        string? previousValue = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER");
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", "off");

            var parseResult = Parser.Parse("dotnet test");
            var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

            result.Should().Be(TerminalLoggerMode.Off);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", previousValue);
        }
    }

    [Fact]
    public void ProcessTerminalLoggerConfiguration_CommandLineTakesPrecedenceOverEnvironmentVariable()
    {
        string? previousValue = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER");
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", "off");

            var parseResult = Parser.Parse("dotnet test -tl:true");
            var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

            result.Should().Be(TerminalLoggerMode.On);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", previousValue);
        }
    }

    [Fact]
    public void ProcessTerminalLoggerConfiguration_LiveLoggerEnvironmentVariable_IsRespected()
    {
        string? previousTl = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER");
        string? previousLl = Environment.GetEnvironmentVariable("MSBUILDLIVELOGGER");
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", null);
            Environment.SetEnvironmentVariable("MSBUILDLIVELOGGER", "off");

            var parseResult = Parser.Parse("dotnet test");
            var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

            result.Should().Be(TerminalLoggerMode.Off);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", previousTl);
            Environment.SetEnvironmentVariable("MSBUILDLIVELOGGER", previousLl);
        }
    }

    [Fact]
    public void ProcessTerminalLoggerConfiguration_TerminalLoggerEnvTakesPrecedenceOverLiveLogger()
    {
        string? previousTl = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER");
        string? previousLl = Environment.GetEnvironmentVariable("MSBUILDLIVELOGGER");
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", "true");
            Environment.SetEnvironmentVariable("MSBUILDLIVELOGGER", "false");

            var parseResult = Parser.Parse("dotnet test");
            var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

            result.Should().Be(TerminalLoggerMode.On);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", previousTl);
            Environment.SetEnvironmentVariable("MSBUILDLIVELOGGER", previousLl);
        }
    }

    [Fact]
    public void ProcessTerminalLoggerConfiguration_WithTlpDefault_UsesDefaultValue()
    {
        var parseResult = Parser.Parse("dotnet test -tlp:default=off");
        var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

        result.Should().Be(TerminalLoggerMode.Off);
    }

    [Theory]
    [InlineData("-tl:true", TerminalLoggerMode.On)]
    [InlineData("-tl:false", TerminalLoggerMode.Off)]
    public void ProcessTerminalLoggerConfiguration_ExplicitSwitchTakesPrecedenceOverTlpDefault(string tlArg, TerminalLoggerMode expectedMode)
    {
        var parseResult = Parser.Parse($"dotnet test -tlp:default=on {tlArg}");
        var result = TerminalLoggerDetector.ProcessTerminalLoggerConfiguration(parseResult);

        result.Should().Be(expectedMode);
    }
}
