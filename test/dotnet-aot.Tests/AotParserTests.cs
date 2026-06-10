// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Tests for the AOT-compiled CLI parser (the #if CLI_AOT path in Parser.cs).
///  Validates that --version, --info, and default usage work correctly,
///  and that unsupported commands produce parse errors.
/// </summary>
public class AotParserTests
{
    [Fact]
    public void ParseVersion_HasNoErrors()
    {
        var result = Parser.Parse(["--version"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseInfo_HasNoErrors()
    {
        var result = Parser.Parse(["--info"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseNoArgs_HasNoErrors()
    {
        var result = Parser.Parse([]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseUnrecognizedCommand_HasErrors()
    {
        var result = Parser.Parse(["build"]);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseUnrecognizedOption_HasErrors()
    {
        var result = Parser.Parse(["--list-sdks"]);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseVersionWithExtraArgs_IsHandledGracefully()
    {
        // System.CommandLine may or may not error on extra tokens after --version.
        // The important behavior is that --version is recognized and handled.
        var result = Parser.Parse(["--version", "extra"]);

        if (result.Errors.Count == 0)
        {
            // --version is still recognized and returns 0
            Assert.Equal(0, Parser.Invoke(result));
        }
        else
        {
            // Extra args produce a parse error, which is also acceptable
            Assert.NotEmpty(result.Errors);
        }
    }

    [Fact]
    public void InvokeVersion_ReturnsZeroAndOutputsVersion()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--version"]);

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout), "Expected version output on stdout");
        Assert.Equal(Product.Version, stdout.Trim());
    }

    [Fact]
    public void InvokeInfo_ReturnsZeroAndContainsExpectedSections()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--info"]);

        Assert.Equal(0, exitCode);
        Assert.Contains(".NET SDK:", stdout);
        Assert.Contains("Version:", stdout);
        Assert.Contains("Commit:", stdout);
        Assert.Contains("Runtime Environment:", stdout);
    }

    [Fact]
    public void InvokeInfo_DoesNotContainManagedOnlySections()
    {
        var (_, stdout, _) = InvokeWithCapture(["--info"]);

        // Under CLI_AOT, workload and MSBuild info are excluded
        Assert.DoesNotContain("Workload version:", stdout);
        Assert.DoesNotContain("MSBuild version:", stdout);
    }

    [Fact]
    public void InvokeNoArgs_ReturnsZeroAndShowsUsage()
    {
        var (exitCode, stdout, _) = InvokeWithCapture([]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", stdout);
    }

    /// <summary>
    ///  Invokes the AOT parser and captures output.
    ///  Uses BufferedReporter for Reporter.Output (used by --version, --info)
    ///  and Console.SetOut for direct Console.Out writes (used by default usage action).
    /// </summary>
    private static (int exitCode, string stdout, string stderr) InvokeWithCapture(string[] args)
    {
        var parseResult = Parser.Parse(args);

        var bufferedOutput = new BufferedReporter();
        var bufferedError = new BufferedReporter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var stdoutWriter = new StringWriter();
        var stderrWriter = new StringWriter();

        try
        {
            Reporter.SetOutput(bufferedOutput);
            Reporter.SetError(bufferedError);
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);
            InvocationConfiguration invocationConfiguration = new()
            {
                EnableDefaultExceptionHandler = false, // parity with Parser.InvocationConfiguration
                Output = stdoutWriter,
                Error = stderrWriter
            };
            int exitCode = parseResult.Invoke(invocationConfiguration);

            // Combine Reporter output and direct Console.Out output
            string reporterOut = string.Join(Environment.NewLine, bufferedOutput.Lines);
            string consoleOut = stdoutWriter.ToString();
            string stdout = string.IsNullOrEmpty(reporterOut)
                ? consoleOut
                : (string.IsNullOrEmpty(consoleOut)
                    ? reporterOut
                    : reporterOut + Environment.NewLine + consoleOut);

            string reporterErr = string.Join(Environment.NewLine, bufferedError.Lines);
            string consoleErr = stderrWriter.ToString();
            string stderr = string.IsNullOrEmpty(reporterErr)
                ? consoleErr
                : (string.IsNullOrEmpty(consoleErr)
                    ? reporterErr
                    : reporterErr + Environment.NewLine + consoleErr);

            return (exitCode, stdout, stderr);
        }
        finally
        {
            Reporter.Reset();
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
