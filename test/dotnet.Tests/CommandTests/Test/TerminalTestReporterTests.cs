// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Moq;

namespace dotnet.Tests.CommandTests.Test;

public class TerminalTestReporterTests
{
    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/51608: if a test host process exits
    /// before the test session was ever started (so the execution id is never registered with the
    /// reporter), AssemblyRunCompleted must not throw KeyNotFoundException — it must surface the
    /// exit as a handshake failure instead.
    ///
    /// This is a defensive unit test: under the current TestApplicationHandler routing this branch
    /// is unreachable, so it cannot be exercised end-to-end. End-to-end coverage of the recap
    /// behavior triggered when handshake failures are reported lives in
    /// <c>GivenDotnetTestRunsConsoleAppWithoutHandshake</c>.
    /// </summary>
    [Fact]
    public void AssemblyRunCompleted_WhenExecutionIdUnknown_DoesNotThrowAndReportsHandshakeFailure()
    {
        var console = new Mock<IConsole>(MockBehavior.Loose);
        console.SetupGet(c => c.IsOutputRedirected).Returns(true);
        console.SetupGet(c => c.BufferWidth).Returns(120);
        console.SetupGet(c => c.BufferHeight).Returns(30);
        console.Setup(c => c.GetForegroundColor()).Returns(ConsoleColor.Gray);
        console.Setup(c => c.GetBackgroundColor()).Returns(ConsoleColor.Black);

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        };

        using var reporter = new TerminalTestReporter(console.Object, options);

        Action act = () => reporter.AssemblyRunCompleted(
            executionId: "never-registered",
            exitCode: 1,
            outputData: "stdout",
            errorData: "stderr");

        act.Should().NotThrow();
        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/52128: the mid-stream per-assembly
    /// summary printed when an assembly completes (ShowAssembly + ShowAssemblyStartAndComplete)
    /// must include the per-assembly counts in the same compact bracketed form used by the
    /// in-progress indicator. Tests use <see cref="AnsiMode.SimpleAnsi"/> which routes through
    /// <c>SimpleTerminal</c>, so the expected glyphs are the ASCII variants
    /// <c>[+P/xF/?S]</c> (mirroring <c>SimpleTerminal.RenderProgress</c>). The full-ANSI path
    /// uses <c>[✓P/xF/↓S]</c> and is exercised end-to-end by acceptance tests.
    /// </summary>
    [Fact]
    public void AssemblyRunCompleted_WithShowAssemblyStartAndComplete_PrintsPerAssemblyCounts()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/MyTests.dll";
        const string executionId = "exec-1";

        reporter.AssemblyRunStarted(assembly, targetFramework: "net9.0", architecture: "x64", executionId, instanceId: "inst-1");

        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-1", TestOutcome.Passed);
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-2", TestOutcome.Passed);
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-3", TestOutcome.Passed);
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "t-skip-1", TestOutcome.Skipped);

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(capturingConsole.GetOutput(), assembly);
        assemblyLine.Should().Contain("[+3/x0/?1]");
    }

    /// <summary>
    /// In the final test-run summary, when more than one assembly ran, each assembly entry
    /// must include its own per-assembly counts in the compact bracketed form
    /// (https://github.com/dotnet/sdk/issues/52128). See the note on
    /// <see cref="AssemblyRunCompleted_WithShowAssemblyStartAndComplete_PrintsPerAssemblyCounts"/>
    /// for why the SimpleAnsi (ASCII) variant is asserted here.
    /// </summary>
    [Fact]
    public void TestExecutionCompleted_WithMultipleAssemblies_PrintsPerAssemblyCountsInSummary()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            // Suppress mid-stream per-assembly lines so we can assert against the final summary only.
            ShowAssemblyStartAndComplete = false,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);

        const string assemblyA = "/repo/bin/Debug/net9.0/A.Tests.dll";
        const string assemblyB = "/repo/bin/Debug/net9.0/B.Tests.dll";

        reporter.AssemblyRunStarted(assemblyA, "net9.0", "x64", executionId: "exec-A", instanceId: "inst-A");
        reporter.AssemblyRunStarted(assemblyB, "net9.0", "x64", executionId: "exec-B", instanceId: "inst-B");

        // Assembly A: 2 passed, 1 failed, 0 skipped.
        ReportTest(reporter, assemblyA, executionId: "exec-A", instanceId: "inst-A", testUid: "a-1", TestOutcome.Passed);
        ReportTest(reporter, assemblyA, executionId: "exec-A", instanceId: "inst-A", testUid: "a-2", TestOutcome.Passed);
        ReportTest(reporter, assemblyA, executionId: "exec-A", instanceId: "inst-A", testUid: "a-3", TestOutcome.Fail);

        // Assembly B: 5 passed, 0 failed, 2 skipped.
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-1", TestOutcome.Passed);
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-2", TestOutcome.Passed);
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-3", TestOutcome.Passed);
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-4", TestOutcome.Passed);
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-5", TestOutcome.Passed);
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-6", TestOutcome.Skipped);
        ReportTest(reporter, assemblyB, executionId: "exec-B", instanceId: "inst-B", testUid: "b-7", TestOutcome.Skipped);

        reporter.AssemblyRunCompleted(executionId: "exec-A", exitCode: 1, outputData: null, errorData: null);
        reporter.AssemblyRunCompleted(executionId: "exec-B", exitCode: 0, outputData: null, errorData: null);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 1);

        string output = capturingConsole.GetOutput();

        GetAssemblySummaryLine(output, assemblyA).Should().Contain("[+2/x1/?0]");
        GetAssemblySummaryLine(output, assemblyB).Should().Contain("[+5/x0/?2]");
    }

    /// <summary>
    /// When an assembly's tests were retried, the per-assembly summary should append a
    /// "/r{N}" segment to the compact counts block so users can tell the final counts came from retries.
    /// </summary>
    [Fact]
    public void AssemblyRunCompleted_WhenTestsWereRetried_ShowsRetriedCount()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        const string assembly = "/repo/bin/Debug/net9.0/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        // Attempt 1: register the first instance and report a failure.
        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "flaky-1", TestOutcome.Fail);

        // Attempt 2: a new instance id triggers a retry; the failing test now passes.
        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-2", testUid: "flaky-1", TestOutcome.Passed);

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(capturingConsole.GetOutput(), assembly);
        assemblyLine.Should().Contain("[+1/x0/?0/r1]");
    }

    private static void ReportTest(TerminalTestReporter reporter, string assembly, string executionId, string instanceId, string testUid, TestOutcome outcome)
    {
        reporter.TestCompleted(
            assembly: assembly,
            targetFramework: "net9.0",
            architecture: "x64",
            executionId: executionId,
            instanceId: instanceId,
            testNodeUid: testUid,
            displayName: testUid,
            informativeMessage: null,
            outcome: outcome,
            duration: TimeSpan.FromMilliseconds(1),
            exceptions: null,
            expected: null,
            actual: null,
            standardOutput: null,
            errorOutput: null);
    }

    /// <summary>
    /// Finds the per-assembly summary line for the given assembly. Multiple lines may mention the
    /// assembly (e.g. the "Running tests from ..." banner and the summary line). The summary line
    /// is the one that contains the compact counts block written by <c>AppendAssemblyTestCounts</c>.
    /// ANSI color escape sequences are stripped so callers can use plain-text assertions like
    /// <c>Contain("[+3/x0/?1]")</c> (SimpleAnsi/SimpleTerminal mode uses the ASCII glyph set).
    /// </summary>
    private static string GetAssemblySummaryLine(string output, string assemblyPath)
    {
        foreach (string line in output.Split('\n'))
        {
            string stripped = StripAnsi(line);
            if (stripped.Contains(assemblyPath, StringComparison.Ordinal)
                && stripped.Contains("[+", StringComparison.Ordinal))
            {
                return stripped;
            }
        }

        throw new InvalidOperationException(
            $"Expected output to contain a per-assembly summary line for '{assemblyPath}', but it did not. Full output:{Environment.NewLine}{output}");
    }

    private static string StripAnsi(string value) => s_ansiEscapeRegex.Replace(value, string.Empty);

    private static readonly Regex s_ansiEscapeRegex = new("\x1b\\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);
}
