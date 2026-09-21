// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Moq;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests;

public class TerminalTestReporterTests
{
    [Theory]
    [InlineData(false, "Test run completed with non-success exit code: 5. The command-line arguments are invalid.")]
    [InlineData(true, "Test discovery completed with non-success exit code: 5. The command-line arguments are invalid.")]
    public void TestExecutionCompleted_WithKnownExitCode_PrintsDescription(bool isDiscovery, string expected)
    {
        var output = new StringBuilder();
        var console = new Mock<IConsole>(MockBehavior.Loose);
        console.Setup(c => c.Write(It.IsAny<string>())).Callback<string?>(value => output.Append(value));
        console.Setup(c => c.Write(It.IsAny<char>())).Callback<char>(value => output.Append(value));
        console.Setup(c => c.WriteLine()).Callback(() => output.AppendLine());
        console.Setup(c => c.WriteLine(It.IsAny<string>())).Callback<string?>(value => output.AppendLine(value));

        using var reporter = new TerminalTestReporter(console.Object, new TerminalTestReporterOptions
        {
            UseAnsi = true,
            UseCIAnsi = true,
            ShowProgress = false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery, isHelp: false, isRetry: false);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode: TestExitCode.InvalidCommandLine);

        output.ToString().Should().Contain(expected);
    }
}
