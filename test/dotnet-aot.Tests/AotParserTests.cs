// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Tests for the AOT-compiled CLI parser (the #if CLI_AOT path in Parser.cs).
///  Validates that --version, --info, --help, and default usage are served entirely
///  from AOT, that the full command surface now parses (matching the managed CLI),
///  and that commands which require the managed CLI report this via
///  <see cref="CommandNotAvailableInAotException"/> so the bridge can fall back.
/// </summary>
[TestClass]
public class AotParserTests
{
    // File-based app detection (GetFileBasedAppEntryPointToken -> VirtualProjectBuilder.IsValidEntryPointPath)
    // pulls in the Microsoft.Build assembly, which cannot be loaded into a NativeAOT image, so the call
    // always throws under AOT. Skip the affected tests when running AOT-compiled (no dynamic code support),
    // while still exercising them in the managed test run. Tracked by https://github.com/dotnet/sdk/issues/54806.
    private static void SkipIfFileBasedAppDetectionUnavailableUnderAot()
    {
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            Assert.Inconclusive("https://github.com/dotnet/sdk/issues/54806 - GetFileBasedAppEntryPointToken requires Microsoft.Build, which cannot be loaded under NativeAOT.");
        }
    }

    private static Exception? RecordException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    [TestMethod]
    public void ParseVersion_HasNoErrors()
    {
        var result = Parser.Parse(["--version"]);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void ParseInfo_HasNoErrors()
    {
        var result = Parser.Parse(["--info"]);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void ParseNoArgs_HasNoErrors()
    {
        var result = Parser.Parse([]);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void ParseKnownCommand_HasNoErrors()
    {
        // The AOT parser now builds the full command tree, so real commands like `build`
        // parse cleanly (they no longer surface as unknown). Execution still falls back.
        var result = Parser.Parse(["build"]);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void ParseSdkCheck_HasNoErrors()
    {
        // `sdk check` is AOT-capable, so it parses cleanly from the shared command tree.
        var result = Parser.Parse(["sdk", "check"]);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void DetectFileBasedApp_WhenFirstArgIsCSharpFile()
    {
        SkipIfFileBasedAppDetectionUnavailableUnderAot();

        // `dotnet app.cs` is an implicit file-based app invocation. The AOT parser only sees the
        // path as an unmatched root argument, so the shared detection (reused from the managed CLI)
        // identifies it so NativeEntryPoint can defer to the managed run pipeline.
        var csFile = Path.Combine(Path.GetTempPath(), $"aot-filebased-{Guid.NewGuid():N}.cs");
        File.WriteAllText(csFile, "Console.WriteLine(\"hi\");");
        try
        {
            var result = Parser.Parse([csFile]);
            Assert.IsEmpty(result.Errors);
            Assert.IsNotNull(result.GetFileBasedAppEntryPointToken());
        }
        finally
        {
            File.Delete(csFile);
        }
    }

    [TestMethod]
    public void DoesNotDetectFileBasedApp_ForBuiltInCommand()
    {
        SkipIfFileBasedAppDetectionUnavailableUnderAot();

        var result = Parser.Parse(["build"]);
        Assert.IsNull(result.GetFileBasedAppEntryPointToken());
    }

    [TestMethod]
    public void DoesNotDetectFileBasedApp_ForNonExistentFile()
    {
        SkipIfFileBasedAppDetectionUnavailableUnderAot();

        // IsValidEntryPointPath requires the file to exist, so a bogus *.cs argument is not
        // treated as a file-based app (it would resolve as an external `dotnet-<name>` command).
        var result = Parser.Parse([$"does-not-exist-{Guid.NewGuid():N}.cs"]);
        Assert.IsNull(result.GetFileBasedAppEntryPointToken());
    }

    [TestMethod]
    [DataRow("ef")]                                  // external command: dotnet-ef
    [DataRow("does-not-exist-command")]              // unknown external command
    public void DetectExternalCommand_RequiresManagedResolution(string command)
    {
        // `dotnet <external>` doesn't match a built-in command, so it lands on the root's hidden
        // subcommand argument and must be deferred to the managed CLI's external command resolution
        // rather than executed by the AOT root usage action.
        var result = Parser.Parse([command]);
        Assert.IsEmpty(result.Errors);
        Assert.IsTrue(result.RequiresManagedCommandResolution());
    }

    [TestMethod]
    [DataRow("")]                                    // `dotnet` (usage)
    [DataRow("--version")]
    [DataRow("--info")]
    public void RootInvocations_DoNotRequireManagedResolution(string arg)
    {
        // Bare `dotnet`, `--version`, `--info`, etc. are handled entirely in AOT.
        string[] args = arg.Length == 0 ? [] : [arg];
        var result = Parser.Parse(args);
        Assert.IsFalse(result.RequiresManagedCommandResolution());
    }

    [TestMethod]
    public void ParseHostHandledOption_HasNoErrors()
    {
        // --list-sdks / --list-runtimes are host-handled options defined on the root command
        // so they appear in help and parse without error. The host resolves them before AOT.
        Assert.IsEmpty(Parser.Parse(["--list-sdks"]).Errors);
        Assert.IsEmpty(Parser.Parse(["--list-runtimes"]).Errors);
    }

    [TestMethod]
    public void ParseUnknownToken_IsToleratedForExternalCommandForwarding()
    {
        // The dotnet root command is intentionally tolerant of unknown tokens so that
        // `dotnet foo` can be forwarded to an external `dotnet-foo` command. Unknown tokens
        // therefore do not produce parse errors; they are resolved by the managed CLI on fallback.
        var result = Parser.Parse(["--this-option-does-not-exist"]);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void InvokeKnownCommand_FallsBackToManaged()
    {
        // Commands that cannot run in AOT must signal a managed fallback rather than execute.
        var result = Parser.Parse(["build"]);
        Assert.IsEmpty(result.Errors);
        Assert.ThrowsExactly<CommandNotAvailableInAotException>(() => Parser.Invoke(result));
    }

    [TestMethod]
    public void InvokeBareSdk_RendersHelpFromAot()
    {
        // `dotnet sdk` with no subcommand renders its missing-command error and help entirely
        // from AOT (no managed fallback), matching the managed CLI behavior.
        var (exitCode, stdout, stderr) = InvokeWithCapture(["sdk"]);

        Assert.AreEqual(1, exitCode);
        stdout.Should().Contain("check");
        stderr.Should().NotBeNullOrWhiteSpace("Expected a missing-command error on stderr");
    }

    [TestMethod]
    public void InvokeBareSln_RendersHelpFromAot()
    {
        // `dotnet sln` with no subcommand renders its missing-command error and help entirely
        // from AOT (no managed fallback). Only `sln add` falls back to the managed CLI.
        var (exitCode, stdout, stderr) = InvokeWithCapture(["sln"]);

        Assert.AreEqual(1, exitCode);
        stdout.Should().Contain("list");
        stderr.Should().NotBeNullOrWhiteSpace("Expected a missing-command error on stderr");
    }

    [TestMethod]
    public void InvokeSdkCheckHelp_RendersFromAotWithoutFallback()
    {
        // `sdk check` is wired to its real AOT implementation (not the managed fallback), so its
        // help renders entirely from AOT and must not request a managed fallback.
        var result = Parser.Parse(["sdk", "check", "--help"]);
        var exception = RecordException(() => Parser.Invoke(result));

        Assert.IsNull(exception);
    }

    [TestMethod]
    public void InvokeRootHelp_RendersUsageFromAot()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--help"]);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain("dotnet");
        stdout.Should().Contain("build");
    }

    [TestMethod]
    public void InvokeCommandHelp_RendersFromAotWithoutFallback()
    {
        // Help for a definition-backed command (one that does not shell out to an external
        // tool) renders entirely from AOT and must not request a managed fallback.
        var result = Parser.Parse(["build", "--help"]);
        var exception = RecordException(() => Parser.Invoke(result));

        Assert.IsNull(exception);
    }

    [TestMethod]
    public void InvokeExternalToolHelp_RendersFromAotWithoutFallback()
    {
        // Help for the external-tool commands (msbuild/nuget/vstest/format/fsi) now shells out to the
        // underlying tool from AOT instead of falling back to the managed CLI. The forwarded process
        // may fail in the test environment, but help must never request a managed fallback.
        var result = Parser.Parse(["msbuild", "--help"]);
        var exception = RecordException(() => Parser.Invoke(result));

        Assert.IsFalse(exception is CommandNotAvailableInAotException);
    }

    [TestMethod]
    public void InvokeCliSchema_RendersSchemaJsonFromAot()
    {
        // --cli-schema serializes the command tree via a source-generated JsonSerializerContext,
        // so it runs entirely in AOT (no managed fallback) and emits the command surface as JSON.
        var (exitCode, stdout, _) = InvokeWithCapture(["--cli-schema"]);

        Assert.AreEqual(0, exitCode);
        // The root command name reflects the host executable, so assert on stable content instead:
        // the SDK version, the subcommands collection, and a representative built-in subcommand.
        stdout.Should().Contain($"\"version\": \"{Product.Version}\"");
        stdout.Should().Contain("\"subcommands\"");
        stdout.Should().Contain("\"build\"");
    }

    [TestMethod]
    public void ParseVersionWithExtraArgs_IsHandledGracefully()
    {
        // System.CommandLine may or may not error on extra tokens after --version.
        // The important behavior is that --version is recognized and handled.
        var result = Parser.Parse(["--version", "extra"]);

        if (result.Errors.Count == 0)
        {
            // --version is still recognized and returns 0
            Assert.AreEqual(0, Parser.Invoke(result));
        }
        else
        {
            // Extra args produce a parse error, which is also acceptable
            Assert.IsNotEmpty(result.Errors);
        }
    }

    [TestMethod]
    public void InvokeVersion_ReturnsZeroAndOutputsVersion()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--version"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "Expected version output on stdout");
        Assert.AreEqual(Product.Version, stdout.Trim());
    }

    [TestMethod]
    public void InvokeInfo_ReturnsZeroAndContainsExpectedSections()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--info"]);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain(".NET SDK:");
        stdout.Should().Contain("Version:");
        stdout.Should().Contain("Commit:");
        stdout.Should().Contain("Workload version:");
        stdout.Should().Contain("MSBuild version:");
        stdout.Should().Contain("Runtime Environment:");
    }

    [TestMethod]
    public void InvokeInfo_ReportsMSBuildAndWorkloadVersions()
    {
        var (exitCode, stdout, _) = InvokeWithCapture(["--info"]);

        // Workload and MSBuild reporting used to be excluded from the AOT build; the AOT --info now
        // matches the managed CLI. Assert the MSBuild line renders a real (non-empty) version so a
        // future trim/substitution regression that blanks it out would be caught.
        Assert.AreEqual(0, exitCode);
        stdout.Should().MatchRegex(@"MSBuild version:\s+\S");
        stdout.Should().Contain("Workload version:");
    }

    [TestMethod]
    public void InvokeNoArgs_ReturnsZeroAndShowsUsage()
    {
        var (exitCode, stdout, _) = InvokeWithCapture([]);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain("Usage:");
    }

    [TestMethod]
    [DataRow("tool list")]
    [DataRow("tool list --local")]
    [DataRow("tool run mytool")]
    [DataRow("tool uninstall mypackage")]
    [DataRow("tool search mysearchterm")]
    public void ParseAotToolCommand_HasNoErrors(string commandLine)
    {
        // The AOT-capable `tool` subcommands (local list/uninstall, run, search) parse cleanly
        // because their real implementations are linked into the AOT CLI.
        var result = Parser.Parse(commandLine.Split(' '));
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    [DataRow("tool list")]
    [DataRow("tool list --local")]
    public void InvokeAotToolListCommand_ExecutesWithoutManagedFallback(string commandLine)
    {
        // `tool list` is AOT-capable: the real ToolListLocalCommand is linked in. It succeeds even
        // when no manifest is present (empty output), so invoking it should return 0 rather than
        // throw CommandNotAvailableInAotException. This catches mis-wired actions that parse-only
        // tests would miss.
        var (exitCode, _, _) = InvokeWithCapture(commandLine.Split(' '));

        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    [DataRow("tool list --global")]                  // global list dispatches to managed CLI
    [DataRow("tool list --tool-path somepath")]      // tool-path list dispatches to managed CLI
    [DataRow("tool uninstall mypackage --global")]   // global uninstall dispatches to managed CLI
    [DataRow("tool install mypackage")]              // install is not AOT-capable
    [DataRow("tool update mypackage")]               // update is not AOT-capable
    [DataRow("tool restore")]                        // restore is not AOT-capable
    [DataRow("tool execute dotnetsay")]              // execute is not AOT-capable
    public void InvokeManagedOnlyToolCommand_FallsBackToManaged(string commandLine)
    {
        // The global/tool-path variants and install/update/restore depend on NuGet package
        // install/restore infrastructure that isn't AOT-ready, so they must signal a managed
        // fallback via CommandNotAvailableInAotException rather than execute.
        var result = Parser.Parse(commandLine.Split(' '));
        Assert.IsEmpty(result.Errors);
        Assert.ThrowsExactly<CommandNotAvailableInAotException>(() => Parser.Invoke(result));
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
