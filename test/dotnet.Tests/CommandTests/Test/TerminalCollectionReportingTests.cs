// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Moq;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class TerminalCollectionReportingTests
{
    [TestMethod]
    public void CollectionProgress_IsBoundedAndStopsBeforeFinalSummary()
    {
        long ticks = 0;
        TimerCallback callback = null!;
        object? callbackState = null;
        var timer = new Mock<ITimer>();
        timer.Setup(timer => timer.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.SetupGet(provider => provider.TimestampFrequency).Returns(TimeSpan.TicksPerSecond);
        timeProvider.Setup(provider => provider.GetTimestamp()).Returns(() => ticks);
        timeProvider.Setup(provider => provider.CreateTimer(
                It.IsAny<TimerCallback>(), It.IsAny<object?>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Callback<TimerCallback, object?, TimeSpan, TimeSpan>((action, state, _, _) =>
            {
                callback = action;
                callbackState = state;
            })
            .Returns(timer.Object);
        var console = new CapturingConsole();
        using var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            CollectTestMap = true,
            ShowProgress = true,
        }, timeProvider.Object);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        reporter.TestMapCollectionStarted(2);
        reporter.TestMapCollectionModuleStarted("First.dll");

        ticks = TimeSpan.FromSeconds(59).Ticks;
        callback(callbackState);
        console.GetOutput().Should().NotContain("Collecting test maps:");

        ticks = TimeSpan.FromSeconds(60).Ticks;
        callback(callbackState);
        console.GetOutput().Should().Contain("Collecting test maps: 0/2 module(s) finished. Current module: First.dll");
        reporter.TestMapCollectionModuleCompleted();
        reporter.TestMapCollectionModuleStarted("Second.dll");

        ticks = TimeSpan.FromSeconds(119).Ticks;
        callback(callbackState);
        Regex.Matches(console.GetOutput(), "Collecting test maps:").Should().ContainSingle();

        ticks = TimeSpan.FromSeconds(120).Ticks;
        callback(callbackState);
        console.GetOutput().Should().Contain("Collecting test maps: 1/2 module(s) finished. Current module: Second.dll");
        reporter.TestMapCollectionModuleCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        string completedOutput = console.GetOutput();
        completedOutput.Should().Contain("Test map collection: Completed.")
            .And.Contain("modules finished: 2/2")
            .And.NotContain("Test run summary:")
            .And.NotContain("succeeded:")
            .And.NotContain("total:");
        ticks = TimeSpan.FromSeconds(180).Ticks;
        callback(callbackState);
        console.GetOutput().Should().Be(completedOutput);
        timer.Verify(timer => timer.DisposeAsync(), Times.Once);
    }

    [TestMethod]
    [DataRow(false, 60)]
    [DataRow(true, 0)]
    public void CollectionProgress_RespectsExplicitSuppression(bool showProgress, int silenceSeconds)
    {
        var timeProvider = new Mock<TimeProvider>(MockBehavior.Strict);
        timeProvider.Setup(provider => provider.GetTimestamp()).Returns(0);
        var console = new CapturingConsole();
        using var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            CollectTestMap = true,
            ShowProgress = showProgress,
            HeartbeatSilenceThreshold = TimeSpan.FromSeconds(silenceSeconds),
        }, timeProvider.Object);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        reporter.TestMapCollectionStarted(1);
        reporter.TestMapCollectionModuleStarted("Tests.dll");
        reporter.TestMapCollectionModuleCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        console.GetOutput().Should().Contain("Test map collection: Completed.")
            .And.NotContain("Collecting test maps:");
    }

    [TestMethod]
    [DataRow(1, false, "Failed.")]
    [DataRow(13, true, "Aborted.")]
    public void CollectionSummary_PreservesFailureAndCancellation(int exitCode, bool cancelled, string status)
    {
        var console = new CapturingConsole();
        using var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            CollectTestMap = true,
        });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        reporter.TestMapCollectionStarted(2);
        reporter.TestMapCollectionModuleStarted("Tests.dll");
        reporter.TestMapCollectionModuleCompleted();
        if (cancelled)
        {
            reporter.MarkCancelled();
        }

        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, exitCode);

        console.GetOutput().Should().Contain($"Test map collection: {status}")
            .And.Contain("modules finished: 1/2")
            .And.NotContain("Passed")
            .And.NotContain("total:");
    }

    [TestMethod]
    [DataRow(null, 60)]
    [DataRow("invalid", 60)]
    [DataRow("-1", 60)]
    [DataRow("0", 0)]
    [DataRow("1", 1)]
    [DataRow("60", 60)]
    [DataRow("2147483647", int.MaxValue)]
    public void ProgressThreshold_MatchesPlatformConfiguration(string? value, int expectedSeconds)
        => MicrosoftTestingPlatformTestCommand.GetProgressThreshold(value).Should().Be(TimeSpan.FromSeconds(expectedSeconds));

    [TestMethod]
    [DataRow(true, "on", true)]
    [DataRow(false, "off", false)]
    [DataRow(false, "false", false)]
    [DataRow(true, "auto", true)]
    [DataRow(false, "auto", true)]
    public void ProgressOption_OverridesDeprecatedNoProgress(bool noProgress, string value, bool expected)
        => MicrosoftTestingPlatformTestCommand.GetProgressEnabled(noProgress, ["--progress", value]).Should().Be(expected);
}
