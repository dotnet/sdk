// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Moq;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TerminalTestReporterTests
{
    [TestMethod]
    public void AnsiTerminal_StopUpdate_WritesStringBuilder()
    {
        var console = new Mock<IConsole>(MockBehavior.Strict);
        console.Setup(c => c.Write(It.IsAny<StringBuilder>()));
        var terminal = new AnsiTerminal(console.Object, baseDirectory: null);

        terminal.StartUpdate();
        terminal.Append("batched output");
        terminal.StopUpdate();

        console.Verify(c => c.Write(It.Is<StringBuilder>(builder => builder.ToString() == "batched output")), Times.Once);
    }

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
    [TestMethod]
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
    [TestMethod]
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
    [TestMethod]
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

    [TestMethod]
    public void TestExecutionCompleted_WithZeroTestsAndPassingAssemblies_PrintsPassedSummary()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);

        const string emptyAssembly = "/repo/bin/Debug/net9.0/Empty.Tests.dll";
        const string passingAssembly = "/repo/bin/Debug/net9.0/Passing.Tests.dll";

        reporter.AssemblyRunStarted(emptyAssembly, "net9.0", "x64", executionId: "exec-empty", instanceId: "inst-empty");
        reporter.AssemblyRunStarted(passingAssembly, "net9.0", "x64", executionId: "exec-passing", instanceId: "inst-passing");
        ReportTest(reporter, passingAssembly, executionId: "exec-passing", instanceId: "inst-passing", testUid: "passing-1", TestOutcome.Passed);

        reporter.AssemblyRunCompleted(executionId: "exec-empty", exitCode: TestExitCode.ZeroTests, outputData: null, errorData: null);
        reporter.AssemblyRunCompleted(executionId: "exec-passing", exitCode: TestExitCode.Success, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: TestExitCode.Success);

        string output = StripAnsi(capturingConsole.GetOutput());
        output.Should().Contain("Test run summary: Passed!");
        GetAssemblySummaryLine(output, emptyAssembly).Should().Contain("Zero tests ran");
        output.Should().NotContain("error:");
    }

    [TestMethod]
    public void TestExecutionCompleted_WithAllowedZeroTests_PrintsPassingAssemblyAndRunSummary()
    {
        var capturingConsole = new CapturingConsole();
        var options = new TerminalTestReporterOptions
        {
            AllowZeroTests = true,
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/Affected.Tests.dll";
        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId: "exec-empty", instanceId: "inst-empty");
        reporter.AssemblyRunCompleted(
            executionId: "exec-empty",
            exitCode: Microsoft.DotNet.Cli.Commands.Test.ExitCode.ZeroTests,
            outputData: null,
            errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: Microsoft.DotNet.Cli.Commands.Test.ExitCode.Success);

        string output = StripAnsi(capturingConsole.GetOutput());
        output.Should().Contain("Test run summary: Passed!");
        GetAssemblySummaryLine(output, assembly).Should().Contain("passed");
        output.Should().NotContain("Zero tests ran");
        output.Should().NotContain("Test run returned non-zero exit code");
    }

    [TestMethod]
    public void TestExecutionCompleted_WithAllowedZeroTestsAndAllSelectedTestsSkipped_RemainsZeroTests()
    {
        var capturingConsole = new CapturingConsole();
        var options = new TerminalTestReporterOptions
        {
            AllowZeroTests = true,
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/Affected.Tests.dll";
        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId: "exec-skipped", instanceId: "inst-skipped");
        ReportTest(reporter, assembly, executionId: "exec-skipped", instanceId: "inst-skipped", testUid: "skipped-1", TestOutcome.Skipped);
        reporter.AssemblyRunCompleted(
            executionId: "exec-skipped",
            exitCode: Microsoft.DotNet.Cli.Commands.Test.ExitCode.Success,
            outputData: null,
            errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: Microsoft.DotNet.Cli.Commands.Test.ExitCode.Success);

        StripAnsi(capturingConsole.GetOutput()).Should().Contain("Zero tests ran");
    }

    [TestMethod]
    public void TestExecutionCompleted_WithAllowedZeroTestsAndUnexpectedNonZeroExit_PrintsFailedSummary()
    {
        var capturingConsole = new CapturingConsole();
        var options = new TerminalTestReporterOptions
        {
            AllowZeroTests = true,
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);
        reporter.TestExecutionCompleted(
            DateTimeOffset.UtcNow,
            exitCode: Microsoft.DotNet.Cli.Commands.Test.ExitCode.GenericFailure);

        StripAnsi(capturingConsole.GetOutput()).Should().Contain("Test run summary: Failed!");
    }

    /// <summary>
    /// When an assembly's tests were retried, the per-assembly summary should append a
    /// "/r{N}" segment to the compact counts block so users can tell the final counts came from retries.
    /// </summary>
    [TestMethod]
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

    /// <summary>
    /// Output that fits within the summary budget must be echoed verbatim (no truncation marker).
    /// </summary>
    [TestMethod]
    public void TruncateOutputForSummary_WhenOutputIsSmall_ReturnsOutputUnchanged()
    {
        string output = string.Join(Environment.NewLine, Enumerable.Range(1, 10).Select(i => $"line {i}"));

        string? result = TerminalTestReporter.TruncateOutputForSummary(output);

        result.Should().Be(output);
    }

    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/52297: when a failing test host dumps
    /// hundreds of lines (typically its full command-line help after an invalid argument), the summary
    /// must keep the head (where the actual error is) and the tail, collapse the middle, and note how
    /// many lines were omitted — instead of burying the error under a wall of noise.
    /// </summary>
    [TestMethod]
    public void TruncateOutputForSummary_WhenOutputIsLarge_KeepsHeadAndTailAndNotesOmittedCount()
    {
        // The error the user cares about is on the first line; the rest is help noise.
        var lines = new List<string> { "Option '--show-live-output' has invalid arguments: Invalid value 'true' (must be one of: 'on', 'off')" };
        lines.AddRange(Enumerable.Range(1, 699).Select(i => $"help line {i}"));
        string output = string.Join(Environment.NewLine, lines);

        string result = TerminalTestReporter.TruncateOutputForSummary(output)!;
        string[] resultLines = result.Split(Environment.NewLine);

        // Head is preserved so the invalid-argument error stays visible.
        result.Should().Contain("Option '--show-live-output' has invalid arguments");
        // Tail is preserved.
        result.Should().Contain("help line 699");
        // The bulk of the help noise in the middle is dropped.
        result.Should().NotContain("help line 350");
        // The omission is reported (700 total lines, 40 kept -> 660 omitted).
        result.Should().Contain("660 lines omitted");
        // Head + marker + tail only.
        resultLines.Length.Should().Be(30 + 1 + 10);
    }

    /// <summary>
    /// End-to-end through <see cref="TerminalTestReporter.AssemblyRunCompleted"/>: a non-zero exit whose
    /// captured standard output is a large help dump must be truncated in the emitted summary so the
    /// error is not buried (https://github.com/dotnet/sdk/issues/52297).
    /// </summary>
    [TestMethod]
    public void AssemblyRunCompleted_WithLargeStandardOutput_TruncatesInSummary()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/MyTests.dll";
        const string executionId = "exec-1";
        reporter.AssemblyRunStarted(assembly, targetFramework: "net9.0", architecture: "x64", executionId, instanceId: "inst-1");

        var lines = new List<string> { "Option '--show-live-output' has invalid arguments: Invalid value 'true' (must be one of: 'on', 'off')" };
        lines.AddRange(Enumerable.Range(1, 699).Select(i => $"help line {i}"));
        string largeOutput = string.Join(Environment.NewLine, lines);

        reporter.AssemblyRunCompleted(executionId, exitCode: 5, outputData: largeOutput, errorData: null);

        string output = StripAnsi(capturingConsole.GetOutput());

        output.Should().Contain("Option '--show-live-output' has invalid arguments");
        output.Should().Contain("lines omitted");
        output.Should().NotContain("help line 350");
    }

    /// <summary>
    /// '--list-tests json' renders a machine-readable JSON document from the discovered-test data the
    /// SDK already receives over the 'dotnet test' IPC protocol. The document is a versioned envelope
    /// grouped by test container (assembly + TFM + architecture), preserving every field the wire
    /// contract carries (uid, namespace, typeName, methodName, parameterTypeFullNames, traits, location).
    /// See https://github.com/dotnet/sdk/issues/49754.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WhenDiscoveryJsonFormat_EmitsMachineReadableJson()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = false,
            ShowAssembly = false,
            ShowAssemblyStartAndComplete = false,
            ListTestsFormat = TestListFormat.Json,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: true, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/MyTests.dll";
        const string executionId = "exec-1";

        reporter.AssemblyRunStarted(assembly, targetFramework: "net9.0", architecture: "x64", executionId, instanceId: "inst-1");

        reporter.TestDiscovered(executionId, new DiscoveredTestInfo(
            DisplayName: "MyMethod(x: 1)",
            Uid: "uid-1",
            FilePath: "/repo/MyTests.cs",
            LineNumber: 42,
            Namespace: "My.Ns",
            TypeName: "MyClass",
            MethodName: "MyMethod",
            ParameterTypeFullNames: ["System.Int32"],
            Traits: [("Category", "Fast")]));

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 0);

        string output = capturingConsole.GetOutput();
        int start = output.IndexOf('{');
        start.Should().BeGreaterThanOrEqualTo(0, "the discovery output should contain a JSON document");

        using var document = JsonDocument.Parse(output.Substring(start));
        JsonElement root = document.RootElement;

        root.GetProperty("version").GetString().Should().Be("1.0");

        JsonElement containers = root.GetProperty("testContainers");
        containers.GetArrayLength().Should().Be(1);

        JsonElement container = containers[0];
        container.GetProperty("assemblyPath").GetString().Should().Be(assembly);
        container.GetProperty("targetFramework").GetString().Should().Be("net9.0");
        container.GetProperty("architecture").GetString().Should().Be("x64");

        JsonElement tests = container.GetProperty("tests");
        tests.GetArrayLength().Should().Be(1);

        JsonElement test = tests[0];
        test.GetProperty("uid").GetString().Should().Be("uid-1");
        test.GetProperty("displayName").GetString().Should().Be("MyMethod(x: 1)");
        test.GetProperty("namespace").GetString().Should().Be("My.Ns");
        test.GetProperty("typeName").GetString().Should().Be("MyClass");
        test.GetProperty("methodName").GetString().Should().Be("MyMethod");
        test.GetProperty("filePath").GetString().Should().Be("/repo/MyTests.cs");
        test.GetProperty("lineNumber").GetInt32().Should().Be(42);

        JsonElement parameters = test.GetProperty("parameterTypeFullNames");
        parameters.GetArrayLength().Should().Be(1);
        parameters[0].GetString().Should().Be("System.Int32");

        JsonElement traits = test.GetProperty("traits");
        traits.GetArrayLength().Should().Be(1);
        traits[0].GetProperty("key").GetString().Should().Be("Category");
        traits[0].GetProperty("value").GetString().Should().Be("Fast");
    }

    private static void ReportTest(TerminalTestReporter reporter, string assembly, string executionId, string instanceId, string testUid, TestOutcome outcome)
        => ReportTest(reporter, assembly, executionId, instanceId, testUid, outcome, TimeSpan.FromMilliseconds(1));

    private static void ReportTest(TerminalTestReporter reporter, string assembly, string executionId, string instanceId, string testUid, TestOutcome outcome, TimeSpan? duration)
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
            duration: duration,
            exceptions: null,
            expected: null,
            actual: null,
            standardOutput: null,
            errorOutput: null);
    }

    /// <summary>
    /// A test that failed on its first attempt and passed on a retry is "flaky": the run summary reports it in the
    /// dedicated <c>flaky:</c> counter line, in the <c>retried:</c> accounting line, and by name in the
    /// "Flaky tests:" section. See dotnet/sdk#55472 / dotnet/sdk#55473.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WhenRetriedTestRecovers_PrintsFlakyAccountingAndSection()
    {
        var capturingConsole = new CapturingConsole();

        using var reporter = new TerminalTestReporter(capturingConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        const string assembly = "/repo/bin/Debug/net9.0/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "flaky-1", TestOutcome.Fail);

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-2", testUid: "flaky-1", TestOutcome.Passed);

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 0);

        string output = StripAnsi(capturingConsole.GetOutput());

        output.Should().Contain("flaky: 1 (passed after retry)");
        output.Should().Contain("retried: 1 test(s), 1 extra run(s)");
        output.Should().Contain("Flaky tests:");
        output.Should().Contain("flaky-1 failed -> passed (2 attempts)");

        // The old '(+N retried)' suffix on the total line was replaced by the dedicated lines above.
        output.Should().NotContain("(+1 retried)");
    }

    /// <summary>
    /// A test that is retried but keeps failing is retried-but-not-flaky: it is accounted for by the
    /// <c>retried:</c> line, but must not be counted as flaky nor listed in the "Flaky tests:" section, where it
    /// would only duplicate the failure that is already reported with its full error output.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WhenRetriedTestNeverRecovers_ReportsRetriedButNotFlaky()
    {
        var capturingConsole = new CapturingConsole();

        using var reporter = new TerminalTestReporter(capturingConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        const string assembly = "/repo/bin/Debug/net9.0/Broken.Tests.dll";
        const string executionId = "exec-broken";

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "broken-1", TestOutcome.Fail);

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-2", testUid: "broken-1", TestOutcome.Fail);

        reporter.AssemblyRunCompleted(executionId, exitCode: 1, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 1);

        string output = StripAnsi(capturingConsole.GetOutput());

        output.Should().Contain("retried: 1 test(s), 1 extra run(s)");
        output.Should().NotContain("flaky:");
        output.Should().NotContain("Flaky tests:");
    }

    /// <summary>
    /// '--show-flaky-tests off' suppresses both the <c>flaky:</c> counter line and the "Flaky tests:" section, while
    /// the neutral <c>retried:</c> accounting stays.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WhenShowFlakyTestsIsOff_OmitsFlakyLineAndSection()
    {
        var capturingConsole = new CapturingConsole();

        using var reporter = new TerminalTestReporter(capturingConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
            ShowFlakyTests = false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        const string assembly = "/repo/bin/Debug/net9.0/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "flaky-1", TestOutcome.Fail);

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-2", testUid: "flaky-1", TestOutcome.Passed);

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 0);

        string output = StripAnsi(capturingConsole.GetOutput());

        output.Should().Contain("retried: 1 test(s), 1 extra run(s)");
        output.Should().NotContain("flaky:");
        output.Should().NotContain("Flaky tests:");
    }

    /// <summary>
    /// A run without retries keeps its historical summary: neither retry accounting line nor the flaky section is
    /// rendered.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WithoutRetries_OmitsRetryAccountingLines()
    {
        var capturingConsole = new CapturingConsole();

        using var reporter = new TerminalTestReporter(capturingConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/Stable.Tests.dll";
        const string executionId = "exec-stable";

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "stable-1", TestOutcome.Passed);

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 0);

        string output = StripAnsi(capturingConsole.GetOutput());

        output.Should().NotContain("retried:");
        output.Should().NotContain("flaky:");
        output.Should().NotContain("Flaky tests:");
        output.Should().NotContain("Slowest tests:");
    }

    /// <summary>
    /// '--show-slowest-tests N' appends a "Slowest tests:" section ranking the N longest-running tests by their
    /// reported duration, slowest first.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WithSlowestTestsCount_PrintsSlowestSectionInDurationOrder()
    {
        var capturingConsole = new CapturingConsole();

        using var reporter = new TerminalTestReporter(capturingConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
            SlowestTestsCount = 2,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        const string assembly = "/repo/bin/Debug/net9.0/Slow.Tests.dll";
        const string executionId = "exec-slow";

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "fast", TestOutcome.Passed, TimeSpan.FromSeconds(1));
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "slowest", TestOutcome.Passed, TimeSpan.FromSeconds(9));
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "middle", TestOutcome.Passed, TimeSpan.FromSeconds(5));

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 0);

        string output = StripAnsi(capturingConsole.GetOutput());
        string section = output[output.IndexOf("Slowest tests:", StringComparison.Ordinal)..];

        section.Should().Contain("slowest");
        section.Should().Contain("middle");
        // Only the two slowest are listed, in descending duration order.
        section.Should().NotContain("fast");
        section.IndexOf("slowest", StringComparison.Ordinal).Should().BeLessThan(section.IndexOf("middle", StringComparison.Ordinal));
    }

    /// <summary>
    /// The slowest-tests ranking is keyed by test node uid, so a retried test replaces its earlier attempt's timing
    /// instead of appearing twice.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WithSlowestTests_RetryReplacesEarlierAttemptDuration()
    {
        var capturingConsole = new CapturingConsole();

        using var reporter = new TerminalTestReporter(capturingConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
            SlowestTestsCount = 5,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        const string assembly = "/repo/bin/Debug/net9.0/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-1", testUid: "retried-1", TestOutcome.Fail, TimeSpan.FromSeconds(30));

        reporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportTest(reporter, assembly, executionId, instanceId: "inst-2", testUid: "retried-1", TestOutcome.Passed, TimeSpan.FromSeconds(2));

        reporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: 0);

        string output = StripAnsi(capturingConsole.GetOutput());
        string section = output[output.IndexOf("Slowest tests:", StringComparison.Ordinal)..];

        // The final attempt's 2s timing wins; the superseded 30s entry is gone.
        section.Should().Contain("2s 000ms retried-1");
        section.Should().NotContain("30s");
    }

    [TestMethod]
    [DataRow(new string[0], 0)]
    [DataRow(new[] { "--show-slowest-tests" }, 0)]
    [DataRow(new[] { "--show-slowest-tests", "0" }, 0)]
    [DataRow(new[] { "--show-slowest-tests", "abc" }, 0)]
    [DataRow(new[] { "--show-slowest-tests", "-1" }, 0)]
    [DataRow(new[] { "--show-slowest-tests", "3" }, 3)]
    [DataRow(new[] { "test", "--", "--show-slowest-tests", "7" }, 7)]
    public void GetSlowestTestsCount_ParsesForwardedOption(string[] arguments, int expected)
        => MicrosoftTestingPlatformTestCommand.GetSlowestTestsCount(arguments).Should().Be(expected);

    [TestMethod]
    [DataRow(new string[0], true)]
    [DataRow(new[] { "--show-flaky-tests" }, true)]
    [DataRow(new[] { "--show-flaky-tests", "on" }, true)]
    [DataRow(new[] { "--show-flaky-tests", "off" }, false)]
    [DataRow(new[] { "--show-flaky-tests", "Off" }, false)]
    [DataRow(new[] { "--show-flaky-tests", "false" }, false)]
    [DataRow(new[] { "--show-flaky-tests", "disable" }, false)]
    [DataRow(new[] { "--show-flaky-tests", "0" }, false)]
    public void GetShowFlakyTests_ParsesForwardedOption(string[] arguments, bool expected)
        => MicrosoftTestingPlatformTestCommand.GetShowFlakyTests(arguments).Should().Be(expected);

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
