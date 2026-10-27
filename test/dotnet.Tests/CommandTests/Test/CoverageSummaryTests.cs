// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class CoverageSummaryTests
{
    private const string CurrentCultureResource = nameof(CultureInfo.CurrentCulture);

    [TestMethod]
    [DataRow((int)AnsiMode.NoAnsi)]
    [DataRow((int)AnsiMode.SimpleAnsi)]
    public void SingleApplication_PrintsStableTotal(int ansiMode)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, (AnsiMode)ansiMode, "first");
        AddSummary(reporter, "first", 184, 186);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("  Code Coverage Summary:")
            .And.Contain("    Total - Line: 98.9%");
    }

    [TestMethod]
    public void NoCoverageMessages_DoesNotPrintCoverageSection()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().NotContain(CliCommandStrings.CodeCoverageSummary);
    }

    [TestMethod]
    public void DuplicateFullKey_UsesLastMeasurement()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        AddSummary(reporter, "first", 12, 186);
        AddSummary(reporter, "first", 184, 186);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("Total - Line: 98.9%")
            .And.NotContain("6.5%");
    }

    [TestMethod]
    public void MultipleApplications_KeepScopedMeasurementsWithoutInventingTotal()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first", "second");
        AddSummary(reporter, "first", 12, 186);
        AddSummary(reporter, "second", 184, 186);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("first.Tests.dll (instance|session) - Line: 6.5%")
            .And.Contain("second.Tests.dll (instance|session) - Line: 98.9%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    public void CompleteMerge_UsesMergedCountsRatherThanSummingOverlappingApplications()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first", "second");
        AddSummary(reporter, "first", 12, 186);
        AddSummary(reporter, "second", 184, 186);
        AddMergedSummary(reporter, complete: true, "first", "second");

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.AtLeastOneTestFailed);

        console.GetOutput().Should().Contain("Total - Line: 98.9%")
            .And.NotContain("52.7%")
            .And.NotContain("Line: 6.5%");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void PartialMerge_DoesNotClaimRunTotal(bool completeGroup, bool additionalApplication)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, additionalApplication ? ["first", "second", "legacy"] : ["first", "second"]);
        AddSummary(reporter, "first", 12, 186);
        AddSummary(reporter, "second", 184, 186);
        AddMergedSummary(reporter, completeGroup, "first", "second");

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("Merged coverage (merged.coverage) - Line: 98.9%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    public void MultipleIndependentMergedArtifacts_DoNotBecomeOneTotal()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first", "second");
        AddMergedSummary(reporter, complete: true, "first", "second");
        reporter.MergedCoverageSummaryAdded(
            "another.coverage", RecordCoverageInputs(reporter, "first", "second"), complete: true,
            [Summary(10, 100, sessionUid: null)]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("Line: 98.9%")
            .And.Contain("Line: 10.0%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    public void Cancellation_DoesNotClaimRunTotal()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first", "second");
        AddMergedSummary(reporter, complete: true, "first", "second");
        reporter.MarkCancelled();

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.TestSessionAborted);

        console.GetOutput().Should().NotContain("Total -");
    }

    [TestMethod]
    [DataRow(TestExitCode.TestSessionAborted)]
    [DataRow(TestExitCode.TestExecutionStoppedForMaxFailedTests)]
    [DataRow(TestExitCode.TestExecutionStoppedAtDeadline)]
    [DataRow(null)]
    public void AbortOrUnknownExit_WithoutSdkCancellationToken_DoesNotClaimRunTotal(int? exitCode)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        AddSummary(reporter, "first", 1, 2);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode);

        console.GetOutput().Should().Contain("Line: 50.0%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    [DataRow(TestExitCode.TestSessionAborted, TestExitCode.GenericFailure)]
    [DataRow(TestExitCode.TestExecutionStoppedForMaxFailedTests, TestExitCode.GenericFailure)]
    [DataRow(TestExitCode.TestExecutionStoppedAtDeadline, TestExitCode.GenericFailure)]
    [DataRow(TestExitCode.TestSessionAborted, TestExitCode.Success)]
    public void MixedOrIgnoredAbortExit_DoesNotClaimMergedTotal(int applicationExitCode, int aggregateExitCode)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first", "second");
        AddMergedSummary(reporter, complete: true, "first", "second");
        reporter.AssemblyRunCompleted("first", applicationExitCode, null, null);
        reporter.AssemblyRunCompleted("second", TestExitCode.AtLeastOneTestFailed, null, null);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, aggregateExitCode);

        console.GetOutput().Should().Contain("Merged coverage (merged.coverage) - Line: 98.9%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    public void IgnoredApplicationAbort_DoesNotClaimSingleSourceTotal()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        AddSummary(reporter, "first", 1, 2);
        reporter.AssemblyRunCompleted("first", TestExitCode.TestSessionAborted, null, null);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("Line: 50.0%").And.NotContain("Total -");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MergedMetricUpdates_UseLastWriteWinsWithoutCombiningProducers(bool differentProducer)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first", "second");
        reporter.MergedCoverageSummaryAdded(
            "merged.coverage", RecordCoverageInputs(reporter, "first", "second"), complete: true,
            [
                Summary(10, 100, sessionUid: null),
                Summary(184, 186, sessionUid: null) with { ProducerId = differentProducer ? "another-collector" : "collector" },
            ]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        if (differentProducer)
        {
            output.Should().Contain("Total - Line (collector): 10.0%")
                .And.Contain("Total - Line (another-collector): 98.9%")
                .And.NotContain("Total - Line:");
        }
        else
        {
            output.Should().Contain("Total - Line: 98.9%")
                .And.NotContain("10.0%")
                .And.NotContain("Line (collector)");
        }
    }

    [TestMethod]
    public void ExplicitNoData_IsNotReportedAsZeroOrFullCoverage()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        AddSummary(reporter, "first", 0, 0);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.ZeroTests);

        console.GetOutput().Should().Contain($"Total - Line: {CliCommandStrings.CoverageNoData}")
            .And.NotContain("Line: 0.0%")
            .And.NotContain("Line: 100.0%");
    }

    [TestMethod]
    [DataRow(-1L, 10L)]
    [DataRow(11L, 10L)]
    [DataRow(null, 10L)]
    [DataRow(0L, null)]
    public void InvalidOrMissingCounts_ProduceWarningWithoutFabricatingCoverage(long? covered, long? coverable)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        reporter.CoverageSummaryAdded("first.Tests.dll", "first", "instance", [Summary(covered, coverable)]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain(CliCommandStrings.InvalidCoverageSummary)
            .And.NotContain(CliCommandStrings.CodeCoverageSummary);
    }

    [TestMethod]
    public void MultipleProducers_RemainSeparateAndDisambiguated()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        reporter.CoverageSummaryAdded(
            "first.Tests.dll", "first", "instance",
            [Summary(1, 2), Summary(3, 4) with { ProducerId = "another-collector" }]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("Total - Line (collector): 50.0%")
            .And.Contain("Total - Line (another-collector): 75.0%")
            .And.NotContain("Total - Line:");
    }

    [TestMethod]
    public void DifferentSessionsAndInstances_AreNotAggregated()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        AddSummary(reporter, "first", 1, 2);
        reporter.CoverageSummaryAdded(
            "first.Tests.dll", "first", "another-instance",
            [Summary(3, 4) with { SessionUid = "another-session" }]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain("Line: 50.0%")
            .And.Contain("Line: 75.0%")
            .And.NotContain("Total -");
    }

    [TestMethod]
    [DataRow((int)AnsiMode.NoAnsi, false)]
    [DataRow((int)AnsiMode.SimpleAnsi, false)]
    [DataRow((int)AnsiMode.NoAnsi, true)]
    [DataRow((int)AnsiMode.SimpleAnsi, true)]
    public void ScopedModuleMeasurements_RetainSessionAndInstanceIdentity(int ansiMode, bool sharedSession)
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, (AnsiMode)ansiMode, "first");
        reporter.CoverageSummaryAdded(
            "first.Tests.dll", "first", "first-instance",
            [Summary(1, 2, sharedSession ? "shared-session" : "first-session") with { ScopeLevel = 1, ScopeName = "Library" }]);
        reporter.CoverageSummaryAdded(
            "first.Tests.dll", "first", sharedSession ? "second-instance" : "first-instance",
            [Summary(3, 4, sharedSession ? "shared-session" : "second-session") with { ScopeLevel = 1, ScopeName = "Library" }]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("50.0%").And.Contain("75.0%");
        if (sharedSession)
        {
            output.Should().Contain("first-instance").And.Contain("second-instance");
        }
        else
        {
            output.Should().Contain("first-session").And.Contain("second-session");
        }
    }

    [TestMethod]
    public void CoverageLabels_MakeTerminalControlCharactersVisible()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, AnsiMode.NoAnsi, "first");
        reporter.CoverageSummaryAdded(
            "first.Tests.dll", "first", "instance",
            [Summary(1, 2) with { Metric = (int)CoverageMetric.Custom, CustomMetricName = "metric\n\u001b[31m" }]);

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().NotContain("\u001b[31m")
            .And.NotContain("metric\n")
            .And.Contain(@"metric\u000a\u001b[31m: 50.0%");
    }

    [TestMethod]
    [ResourceLock(CurrentCultureResource)]
    public void CoveragePercentages_AreCultureInvariantAndSupportLongCounts()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var console = new CapturingConsole();
            using var reporter = CreateReporter(console, "first");
            reporter.CoverageSummaryAdded(
                "first.Tests.dll", "first", "instance",
                [Summary(184, 186), Summary(long.MaxValue - 1, long.MaxValue) with { Metric = (int)CoverageMetric.Block }]);

            reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

            console.GetOutput().Should().Contain("Total - Line: 98.9%")
                .And.Contain("Total - Block: 100.0%")
                .And.NotContain("98,9%");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestMethod]
    public void CompletedRun_DoesNotLeakCoverageIntoNextRun()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console, "first");
        AddSummary(reporter, "first", 184, 186);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);
        string previousOutput = console.GetOutput();

        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, isDiscovery: false, isHelp: false, isRetry: false);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput()[previousOutput.Length..].Should().NotContain(CliCommandStrings.CodeCoverageSummary);
    }

    private static TerminalTestReporter CreateReporter(CapturingConsole console, params string[] executionIds)
        => CreateReporter(console, AnsiMode.SimpleAnsi, executionIds);

    private static TerminalTestReporter CreateReporter(CapturingConsole console, AnsiMode ansiMode, params string[] executionIds)
    {
        var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions { AnsiMode = ansiMode, ShowProgress = false });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, executionIds.Length, isDiscovery: false, isHelp: false, isRetry: false);
        foreach (string executionId in executionIds)
        {
            reporter.AssemblyRunStarted($"{executionId}.Tests.dll", "net10.0", "x64", executionId, "instance");
            reporter.TestSessionStarted(executionId, "session");
            reporter.TestSessionEnded(executionId, "session");
        }

        return reporter;
    }

    private static void AddSummary(TerminalTestReporter reporter, string executionId, long covered, long coverable)
        => reporter.CoverageSummaryAdded($"{executionId}.Tests.dll", executionId, "instance", [Summary(covered, coverable)]);

    private static void AddMergedSummary(TerminalTestReporter reporter, bool complete, params string[] executions)
        => reporter.MergedCoverageSummaryAdded(
            "merged.coverage", RecordCoverageInputs(reporter, executions), complete,
            [Summary(184, 186, sessionUid: null) with { ProducerId = "collector.postprocessor" }]);

    private static IReadOnlySet<string> RecordCoverageInputs(TerminalTestReporter reporter, params string[] executions)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string executionId in executions)
        {
            string path = $"{executionId}.coverage";
            reporter.ArtifactAdded(
                false, $"{executionId}.Tests.dll", "net10.0", "x64", executionId, null, path, "instance", "session");
            paths.Add(path);
        }

        return paths;
    }

    private static CoverageSummaryMessage Summary(long? covered, long? coverable, string? sessionUid = "session")
        => new(sessionUid, "collector", 0, null, (int)CoverageMetric.Line, null, covered, coverable);
}
