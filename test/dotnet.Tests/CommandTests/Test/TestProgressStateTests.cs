// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Moq;
using Xunit;

namespace dotnet.Tests.CommandTests.Test;

public class TestProgressStateTests
{
    /// <summary>
    /// Tests that reporting skipped tests multiple times updates state correctly:
    /// - First call adds a new entry and increments SkippedTests.
    /// - Second call with same instance increments counts.
    /// - Third call with a new instance triggers retry logic.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReportSkippedTest_MultipleCalls_UpdatesStateCorrectly(int callCount)
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);
        string testUid = "test1";
        string instanceA = "instanceA";
        string instanceB = "instanceB";
        state.NotifyHandshake(instanceA);
        state.NotifyHandshake(instanceB);
        for (int i = 1; i <= callCount; i++)
        {
            var instanceId = i <= 2 ? instanceA : instanceB;
            state.ReportSkippedTest(testUid, instanceId);
        }

        switch (callCount)
        {
            case 1:
                state.SkippedTests.Should().Be(1);
                state.RetriedFailedTests.Should().Be(0);
                state.TotalTests.Should().Be(1);
                break;
            case 2:
                state.SkippedTests.Should().Be(2);
                state.RetriedFailedTests.Should().Be(0);
                state.TotalTests.Should().Be(2);
                break;
            case 3:
                state.SkippedTests.Should().Be(1);
                state.RetriedFailedTests.Should().Be(0);
                state.TotalTests.Should().Be(1);
                break;
        }
    }

    /// <summary>
    /// Tests that reporting a skipped test with a previously seen instance after retry throws.
    /// </summary>
    [Fact]
    public void ReportSkippedTest_RepeatedInstanceAfterRetry_ThrowsInvalidOperationException()
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);
        string testUid = "test1";
        string instanceA = "instanceA";
        string instanceB = "instanceB";
        state.NotifyHandshake("instanceA");
        state.ReportSkippedTest(testUid, instanceA);
        state.ReportSkippedTest(testUid, instanceA);
        state.NotifyHandshake("instanceB");
        state.ReportSkippedTest(testUid, instanceB);

        Action act = () => state.ReportSkippedTest(testUid, instanceA);
        act.Should().Throw<UnreachableException>()
           .WithMessage("Unexpected test result for attempt '1' while the last attempt is '2'");
    }

    /// <summary>
    /// Tests that repeated calls to ReportFailedTest with the same UID and instance ID
    /// increment FailedTests and TotalTests without affecting RetriedFailedTests.
    /// </summary>
    /// <param name="callCount">The number of times ReportFailedTest is invoked.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReportFailedTest_RepeatedCalls_IncrementsFailedTests(int callCount)
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);
        state.NotifyHandshake("instance1");
        for (int i = 0; i < callCount; i++)
        {
            state.ReportFailedTest("testUid", "instance1");
        }

        state.FailedTests.Should().Be(callCount);
        state.TotalTests.Should().Be(callCount);
        state.RetriedFailedTests.Should().Be(0);
    }

    /// <summary>
    /// Tests that ReportFailedTest with a new instance ID after failures
    /// resets the failure count, increments RetriedFailedTests, and reports one failure.
    /// </summary>
    [Fact]
    public void ReportFailedTest_DifferentInstanceId_RetriesFailureAndResetsCount()
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);
        state.NotifyHandshake("id1");
        state.ReportFailedTest("testUid", "id1");
        state.ReportFailedTest("testUid", "id1");
        state.NotifyHandshake("id2");
        state.ReportFailedTest("testUid", "id2");

        state.RetriedFailedTests.Should().Be(2);
        state.FailedTests.Should().Be(1);
        state.TotalTests.Should().Be(1);
    }

    /// <summary>
    /// Tests that reusing an old instance ID after a retry throws an InvalidOperationException.
    /// </summary>
    [Fact]
    public void ReportFailedTest_ReusingOldInstanceId_ThrowsInvalidOperationException()
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);
        state.NotifyHandshake("id1");
        state.ReportFailedTest("testUid", "id1");
        state.NotifyHandshake("id2");
        state.ReportFailedTest("testUid", "id2");

        Action act = () => state.ReportFailedTest("testUid", "id1");

        act.Should()
           .Throw<UnreachableException>()
           .WithMessage("Unexpected test result for attempt '1' while the last attempt is '2'");
    }

    /// <summary>
    /// Tests that reporting with a new instance id clears old reports.
    /// </summary>
    [Fact]
    public void ReportTest_WithNewInstanceId_ClearsOldReports()
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);
        state.NotifyHandshake("id1");
        state.ReportFailedTest("testUid", "id1");
        state.ReportFailedTest("testUid", "id1");
        state.ReportFailedTest("testUid", "id1");
        state.ReportSkippedTest("testUid", "id1");
        state.ReportSkippedTest("testUid", "id1");

        state.NotifyHandshake("id2");
        state.ReportFailedTest("testUid", "id2");
        state.ReportPassingTest("testUid", "id2");
        state.ReportPassingTest("testUid", "id2");
        state.ReportPassingTest("testUid", "id2");
        state.ReportSkippedTest("testUid", "id2");

        state.PassedTests.Should().Be(3);
        state.FailedTests.Should().Be(1);
        state.SkippedTests.Should().Be(1);
        state.RetriedFailedTests.Should().Be(3);
    }
    /// <summary>
    /// Tests that DiscoverTest increments PassedTests and adds the displayName and uid to DiscoveredTests.
    /// </summary>
    /// <param name="displayName">The display name of the test, can be null, empty, or whitespace.</param>
    /// <param name="uid">The unique identifier of the test, can be null, empty, or whitespace.</param>
    /// <remarks>After invocation, PassedTests should be 1 and DiscoveredTests should contain a single entry matching the inputs.</remarks>
    [Theory]
    [InlineData("testName", "uid123")]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData(null, null)]
    public void DiscoverTest_DisplayNameAndUid_AddsEntryAndIncrementsPassedTests(string? displayName, string? uid)
    {
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(
            id: 1,
            assembly: "assembly.dll",
            targetFramework: null,
            architecture: null,
            stopwatch: stopwatchMock.Object);

        state.DiscoverTest(displayName, uid);

        state.PassedTests.Should().Be(1);
        state.DiscoveredTests.Count.Should().Be(1);
        state.DiscoveredTests[0].DisplayName.Should().Be(displayName);
        state.DiscoveredTests[0].UID.Should().Be(uid);
    }

    [Fact]
    public void FailedTestRetryShouldShouldShowTheSameTotalCountsInEachRetry()
    {
        // Tests are retried, total test count stays 3 to give use comparable counts, no matter how many times we retry.
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);

        // First run
        state.NotifyHandshake("run1");
        state.ReportFailedTest("failed-test", "run1");
        state.ReportPassingTest("passed-test", "run1");
        state.ReportSkippedTest("skipped-test", "run1");
        
        state.RetriedFailedTests.Should().Be(0);
        state.FailedTests.Should().Be(1);
        state.PassedTests.Should().Be(1);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(3);

        // Second run (first retry)
        state.NotifyHandshake("run2");
        state.ReportFailedTest("failed-test", "run2");

        state.RetriedFailedTests.Should().Be(1);
        state.FailedTests.Should().Be(1);
        state.PassedTests.Should().Be(1);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(3);

        // Third run (second retry) - failing test passes
        state.NotifyHandshake("run3");
        state.ReportPassingTest("failed-test", "run3");
        state.RetriedFailedTests.Should().Be(2);
        state.FailedTests.Should().Be(0);
        state.PassedTests.Should().Be(2);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(3);
    }

    [Fact]
    public void FailedTestRetryShouldNotFailTheRunWhenSecondRunProducesLessDynamicTests()
    {
        // This is special test for dynamic tests where we don't know how many tests will be produced in the second run.
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);

        // First run
        state.NotifyHandshake("run1");
        state.ReportFailedTest("failed-test1", "run1"); // 2 test cases
        state.ReportFailedTest("failed-test1", "run1");

        state.ReportPassingTest("passed-test", "run1");
        state.ReportSkippedTest("skipped-test", "run1");

        state.RetriedFailedTests.Should().Be(0);
        state.FailedTests.Should().Be(2);
        state.PassedTests.Should().Be(1);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(4);

        // Second run (first retry)
        state.NotifyHandshake("run2");
        state.ReportPassingTest("failed-test1", "run2"); // 1 test case, now passes

        state.RetriedFailedTests.Should().Be(2);
        state.FailedTests.Should().Be(0);
        state.PassedTests.Should().Be(2);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(3);
    }

    [Fact]
    public void FailedTestRetryShouldAccountPassedTestsInRetry()
    {
        // This is special test for dynamic tests where we cannot avoid re-running even non-failing tests from dynamic tests.
        var stopwatchMock = new Mock<IStopwatch>();
        var state = new TestProgressState(1, "assembly.dll", null, null, stopwatchMock.Object);

        // First run
        state.NotifyHandshake("run1");
        state.ReportFailedTest("failed-test1", "run1"); // 2 test cases, one passes, one fails
        state.ReportPassingTest("failed-test1", "run1");

        state.ReportPassingTest("passed-test", "run1");
        state.ReportSkippedTest("skipped-test", "run1");

        state.RetriedFailedTests.Should().Be(0);
        state.FailedTests.Should().Be(1);
        state.PassedTests.Should().Be(2);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(4);

        // Second run (first retry)
        state.NotifyHandshake("run2");
        state.ReportFailedTest("failed-test1", "run2"); // 1 test case still fails, but we also re-run the passing one
        state.ReportPassingTest("failed-test1", "run2");

        state.RetriedFailedTests.Should().Be(1);
        state.FailedTests.Should().Be(1);
        state.PassedTests.Should().Be(2);
        state.SkippedTests.Should().Be(1);
        state.TotalTests.Should().Be(4);
    }
}
