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
///  Validates that --version, --info, --help, and default usage are served entirely
///  from AOT, that the full command surface now parses (matching the managed CLI),
///  and that commands which require the managed CLI report this via
///  <see cref="CommandNotAvailableInAotException"/> so the bridge can fall back.
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
    public void ParseKnownCommand_HasNoErrors()
    {
        // The AOT parser now builds the full command tree, so real commands like `build`
        // parse cleanly (they no longer surface as unknown). Execution still falls back.
        var result = Parser.Parse(["build"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseHostHandledOption_HasNoErrors()
    {
        // --list-sdks / --list-runtimes are host-handled options defined on the root command
        // so they appear in help and parse without error. The host resolves them before AOT.
        Assert.Empty(Parser.Parse(["--list-sdks"]).Errors);
        Assert.Empty(Parser.Parse(["--list-runtimes"]).Errors);
    }

    [Fact]
    public void ParseUnknownToken_IsToleratedForExternalCommandForwarding()
    {
        // The dotnet root command is intentionally tolerant of unknown tokens so that
        // `dotnet foo` can be forwarded to an external `dotnet-foo` command. Unknown tokens
        // therefore do not produce parse errors; they are resolved by the managed CLI on fallback.
        var result = Parser.Parse(["--this-option-does-not-exist"]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void InvokeKnownCommand_FallsBackToManaged()
    {
        // Commands that cannot run in AOT must signal a managed fallback rather than execute.
        var result = Parser.Parse(["build"]);
        Assert.Empty(result.Errors);
        Assert.Throws<CommandNotAvailableInAotException>(() => Parser.Invoke(result));
    }

    [Fact]
    public void InvokeRootHelp_RendersUsageFromAot()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("dotnet", stdout);
        Assert.Contains("build", stdout);
    }

    [Fact]
    public void InvokeCommandHelp_RendersFromAotWithoutFallback()
    {
        // Help for a definition-backed command (one that does not shell out to an external
        // tool) renders entirely from AOT and must not request a managed fallback.
        var result = Parser.Parse(["build", "--help"]);
        var exception = Record.Exception(() => Parser.Invoke(result));

        Assert.Null(exception);
    }

    [Fact]
    public void InvokeExternalToolHelp_RendersFromAotWithoutFallback()
    {
        // Help for the external-tool commands (msbuild/nuget/vstest/format/fsi) now shells out to the
        // underlying tool from AOT instead of falling back to the managed CLI. The forwarded process
        // may fail in the test environment, but help must never request a managed fallback.
        var result = Parser.Parse(["msbuild", "--help"]);
        var exception = Record.Exception(() => Parser.Invoke(result));

        Assert.IsNotType<CommandNotAvailableInAotException>(exception);
    }

    [Fact]
    public void InvokeCliSchema_RendersSchemaJsonFromAot()
    {
        // --cli-schema serializes the command tree via a source-generated JsonSerializerContext,
        // so it runs entirely in AOT (no managed fallback) and emits the command surface as JSON.
        var (exitCode, stdout, _) = InvokeWithCapture(["--cli-schema"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"name\": \"dotnet\"", stdout);
        Assert.Contains("\"subcommands\"", stdout);
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
