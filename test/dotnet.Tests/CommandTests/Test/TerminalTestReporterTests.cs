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
}
