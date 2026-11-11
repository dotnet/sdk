// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestApplicationActionQueueTests : SdkTest
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void SelectedApplicationLaunchFailure_KeepsCoverageScoped(bool merged, bool ignoreFailure)
    {
        var console = new CapturingConsole();
        using var reporter = new TerminalTestReporter(
            console, new TerminalTestReporterOptions { AnsiMode = AnsiMode.SimpleAnsi, ShowProgress = false });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, isDiscovery: false, isHelp: false, isRetry: false);
        string[] executions = merged ? ["first", "second"] : ["first"];
        foreach (string executionId in executions)
        {
            reporter.AssemblyRunStarted($"{executionId}.Tests.dll", ToolsetInfo.CurrentTargetFramework, null, executionId, "instance");
            reporter.TestSessionStarted(executionId, "session", "instance");
            reporter.TestSessionEnded(executionId, "session", "instance");
            reporter.CoverageSummaryAdded(
                $"{executionId}.Tests.dll", executionId, "instance",
                [new("session", "collector", 0, null, (int)CoverageMetric.Line, null, 1, 2)]);
            reporter.ArtifactAdded(
                false, $"{executionId}.Tests.dll", ToolsetInfo.CurrentTargetFramework, null, executionId, null,
                $"{executionId}.coverage", "instance", "session");
        }

        string missingCommand = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, Guid.NewGuid().ToString("N"), "missing-test-app");
        File.Exists(missingCommand).Should().BeFalse();
        var module = new TestModule(
            new RunProperties(missingCommand, string.Empty, null), ProjectFullPath: null, ToolsetInfo.CurrentTargetFramework,
            IsTestingPlatformApplication: true, LaunchSettings: null, missingCommand, DotnetRootArchVariableName: null,
            ImmutableDictionary<string, string>.Empty.Add("TESTINGPLATFORM_EXITCODE_IGNORE", ignoreFailure ? "1" : string.Empty));
        var buildOptions = new BuildOptions(
            new PathOptions(null, null, null, null, ResultsDirectoryLayout.Flat, null, null),
            HasNoRestore: true, HasNoBuild: true, Verbosity: null, NoLaunchProfile: false, NoLaunchProfileArguments: false,
            TestApplicationArguments: [], MSBuildArgs: [], Device: null, ListDevices: false,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty);
        using var ctrlC = new CtrlCCancellationManager(() => { }, subscribeToConsole: false);
        using var policy = new TestRunPolicy(maximumFailedTests: null, timeout: null);
        var queue = new TestApplicationActionQueue(
            1, buildOptions, new TestOptions(false, false, TestListFormat.Text),
            TestResultsDirectoryResolver.CreateShared(buildOptions.PathOptions, SdkTestContext.Current.TestExecutionDirectory),
            reporter, _ => { }, ctrlC, new ArtifactPostProcessingManager(), policy, CancellationToken.None,
            OutputOptions.Normal, outputOptionSpecified: false, TestApplicationSettings.Default);
        queue.Enqueue(new ParallelizableTestModuleGroupWithSequentialInnerModules(module));

        int exitCode = queue.CompleteEnqueueAndWait();

        exitCode.Should().Be(TestExitCode.GenericFailure);
        reporter.HasHandshakeFailure.Should().BeFalse();
        if (merged)
        {
            reporter.MergedCoverageSummaryAdded(
                "merged.coverage", new HashSet<string> { "first.coverage", "second.coverage" }, complete: true,
                [new(null, "collector", 0, null, (int)CoverageMetric.Line, null, 1, 2)]);
        }

        int aggregateExitCode = queue.ApplyExitCodeIgnorePolicy(exitCode);
        aggregateExitCode.Should().Be(ignoreFailure ? TestExitCode.Success : TestExitCode.GenericFailure);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, aggregateExitCode);
        console.GetOutput().Should().Contain(
            merged ? "Merged coverage (merged.coverage) - Line: 50.0%" : "first.Tests.dll (instance|session) - Line: 50.0%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    public void NormalizeExitCode_ZeroTestsWithDisposeFailure_ReturnsGenericFailure()
    {
        int result = TestApplicationActionQueue.NormalizeExitCode(
            TestExitCode.ZeroTests,
            hasFailureDuringDispose: true);

        result.Should().Be(TestExitCode.GenericFailure);
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
