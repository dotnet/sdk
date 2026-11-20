// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestApplicationActionQueueTests
{
    [TestMethod]
    public void NormalizeExitCode_ZeroTestsWithDisposeFailure_ReturnsGenericFailure()
    {
        var console = new CapturingConsole();
        using var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ContextualModuleOutput = true,
        });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        int result = TestApplicationActionQueue.NormalizeExitCode(
            TestExitCode.ZeroTests,
            hasFailureDuringDispose: true,
            reporter);

        result.Should().Be(TestExitCode.GenericFailure);
        reporter.AssemblyRunStarted("Passing.Tests.dll", "net9.0", "x64", "exec-pass", "instance-pass");
        reporter.AssemblyRunCompleted("exec-pass", TestExitCode.Success, null, null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, result);
        console.GetOutput().Should().Contain("##[group]Test run summary:")
            .And.NotContain("##[group]Test run summary: 1 passed, 0 failed");
    }

    [TestMethod]
    [DataRow("true", 2, false, false, false, true)]
    [DataRow("false", 2, false, false, false, false)]
    [DataRow("true", 1, false, false, false, false)]
    [DataRow("true", 2, true, false, false, false)]
    [DataRow("true", 2, false, true, false, false)]
    [DataRow("true", 2, false, false, true, false)]
    public void ShouldUseContextualModuleOutput_RequiresParallelAzurePipelinesRun(
        string? azurePipelines,
        int parallelism,
        bool isHelp,
        bool isDiscovery,
        bool collectTestMap,
        bool expected)
    {
        var options = new TestOptions(isHelp, isDiscovery, TestListFormat.Text)
        {
            CollectTestMap = collectTestMap,
        };

        MicrosoftTestingPlatformTestCommand.ShouldUseContextualModuleOutput(azurePipelines, parallelism, options)
            .Should().Be(expected);
    }

    [TestMethod]
    public void ShouldFailOnAllSkippedTests_UsesStrictPoliciesThatDoNotIgnoreZeroTests()
    {
        TestApplicationActionQueue.ShouldFailOnAllSkippedTests(
        [
            new(FailOnAllSkippedTests: false, IgnoredExitCodes: null),
            new(FailOnAllSkippedTests: true, IgnoredExitCodes: "8"),
        ]).Should().BeFalse();

        TestApplicationActionQueue.ShouldFailOnAllSkippedTests(
        [
            new(FailOnAllSkippedTests: false, IgnoredExitCodes: null),
            new(FailOnAllSkippedTests: true, IgnoredExitCodes: null),
        ]).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyExitCodeIgnorePolicy_RequiresEveryModuleToIgnoreTheExitCode()
    {
        TestApplicationActionQueue.ApplyExitCodeIgnorePolicy(
            TestExitCode.MinimumExpectedTestsPolicyViolation,
            [
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: "9"),
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: "8;9"),
            ]).Should().Be(TestExitCode.Success);

        TestApplicationActionQueue.ApplyExitCodeIgnorePolicy(
            TestExitCode.MinimumExpectedTestsPolicyViolation,
            [
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: "9"),
                new(FailOnAllSkippedTests: false, IgnoredExitCodes: null),
            ]).Should().Be(TestExitCode.MinimumExpectedTestsPolicyViolation);
    }
}
