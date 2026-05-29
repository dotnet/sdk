// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    /// Companion fix to https://github.com/dotnet/sdk/issues/51608: when a test host fails to
    /// hand-shake the failure details (assembly, exit code, stdout, stderr) must be re-printed at
    /// the end of the run so the user does not have to scroll through diagnostic output to find
    /// the actionable cause.
    /// </summary>
    [Fact]
    public void TestExecutionCompleted_AfterHandshakeFailure_ReprintsFailureDetailsAtEndOfRun()
    {
        var console = new CapturingConsole();
        using var reporter = NewReporter(console);

        const string assemblyPath = "/repo/bin/Debug/net9.0/MyTest.dll";
        const string targetFramework = "net9.0";
        const string stdout = "stdout-payload";
        const string stderr = "stderr-payload";

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);
        reporter.HandshakeFailure(assemblyPath, targetFramework, exitCode: 42, outputData: stdout, errorData: stderr);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow.AddSeconds(1), exitCode: 42);

        string rendered = console.GetOutput();

        // Inline output (emitted when HandshakeFailure was called) is still present.
        rendered.Should().Contain(assemblyPath);
        rendered.Should().Contain(stdout);
        rendered.Should().Contain(stderr);

        // The new end-of-run recap header is present, and the recap repeats the assembly path,
        // exit code and stderr at the very end of the output so the user sees the actionable
        // cause without scrolling.
        rendered.Should().Contain("Handshake failures:");
        int headerIndex = rendered.IndexOf("Handshake failures:", StringComparison.Ordinal);
        int lastAssemblyIndex = rendered.LastIndexOf(assemblyPath, StringComparison.Ordinal);
        int lastStderrIndex = rendered.LastIndexOf(stderr, StringComparison.Ordinal);
        lastAssemblyIndex.Should().BeGreaterThan(headerIndex, "recap must list the failing assembly after the header");
        lastStderrIndex.Should().BeGreaterThan(headerIndex, "recap must re-print stderr after the header");

        // Final summary line must say "Failed!" rather than "Zero tests ran" even though no
        // assembly registered any tests — the handshake failure means the run failed, and
        // "Zero tests ran" would otherwise mask that. (The inline per-assembly status still
        // legitimately contains "Zero tests ran" because the assembly really did register zero
        // tests; we only care that the SUMMARY headline reflects failure.)
        int summaryIndex = rendered.IndexOf("Test run summary:", StringComparison.Ordinal);
        summaryIndex.Should().BeGreaterThanOrEqualTo(0, "the run should print a summary section");
        string summarySection = rendered.Substring(summaryIndex);
        summarySection.Should().Contain("Failed!");
        summarySection.Should().NotContain("Zero tests ran");
    }

    [Fact]
    public void TestExecutionCompleted_WithMultipleHandshakeFailures_ListsAllOfThemAtEndOfRun()
    {
        var console = new CapturingConsole();
        using var reporter = NewReporter(console);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);
        reporter.HandshakeFailure("/repo/bin/A.dll", "net9.0", exitCode: 1, outputData: "out-a", errorData: "err-a");
        reporter.HandshakeFailure("/repo/bin/B.dll", "net10.0", exitCode: 2, outputData: "out-b", errorData: "err-b");
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow.AddSeconds(1), exitCode: 1);

        string rendered = console.GetOutput();
        int headerIndex = rendered.IndexOf("Handshake failures:", StringComparison.Ordinal);
        headerIndex.Should().BeGreaterThanOrEqualTo(0);

        string recap = rendered.Substring(headerIndex);
        recap.Should().Contain("/repo/bin/A.dll");
        recap.Should().Contain("err-a");
        recap.Should().Contain("/repo/bin/B.dll");
        recap.Should().Contain("err-b");
    }

    [Fact]
    public void TestExecutionCompleted_WithNoHandshakeFailures_DoesNotEmitRecapHeader()
    {
        var console = new CapturingConsole();
        using var reporter = NewReporter(console);

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow.AddSeconds(1), exitCode: 0);

        console.GetOutput().Should().NotContain("Handshake failures:");
    }

    private static TerminalTestReporter NewReporter(IConsole console)
    {
        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = false,
            ShowAssemblyStartAndComplete = false,
            ShowPassedTests = false,
            ShowActiveTests = false,
        };

        return new TerminalTestReporter(console, options);
    }
}
