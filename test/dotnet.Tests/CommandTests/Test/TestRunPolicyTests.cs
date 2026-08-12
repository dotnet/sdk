// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestRunPolicyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MaximumFailedTestsCancelsWhenThresholdIsReached()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        TestRunCancellationReason? callbackReason = null;
        using var policy = new TestRunPolicy(
            maximumFailedTests: 3,
            timeout: null,
            onCancellation: reason => callbackReason = reason);

        policy.ReportFailedTests(2);

        policy.Token.IsCancellationRequested.Should().BeFalse();
        policy.Reason.Should().Be(TestRunCancellationReason.None);

        policy.ReportFailedTests(1);

        TestRunCancellationReason reason = await policy.Cancellation.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        reason.Should().Be(TestRunCancellationReason.MaximumFailedTests);
        policy.Token.IsCancellationRequested.Should().BeTrue();
        policy.FailedTests.Should().Be(3);
        callbackReason.Should().Be(TestRunCancellationReason.MaximumFailedTests);
    }

    [TestMethod]
    public async Task FailureCountingIsIndependentOfRetryBookkeeping()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        using var policy = new TestRunPolicy(maximumFailedTests: 2, timeout: null);

        policy.ReportFailedTests(1);
        policy.ReportFailedTests(1);

        TestRunCancellationReason reason = await policy.Cancellation.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        reason.Should().Be(TestRunCancellationReason.MaximumFailedTests);
        policy.FailedTests.Should().Be(2);
    }

    [TestMethod]
    public async Task TimeoutStartsWithFirstTestApplication()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        using var policy = new TestRunPolicy(maximumFailedTests: null, timeout: TimeSpan.FromMilliseconds(100));

        await Task.Delay(200, cancellationToken);
        policy.Reason.Should().Be(TestRunCancellationReason.None);

        policy.OnTestApplicationStarted();

        TestRunCancellationReason reason = await policy.Cancellation.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        reason.Should().Be(TestRunCancellationReason.Timeout);
    }

    [TestMethod]
    public void StartingAdditionalTestApplicationsDoesNotRestartTimeout()
    {
        using var policy = new TestRunPolicy(maximumFailedTests: null, timeout: TimeSpan.FromMinutes(1));

        policy.OnTestApplicationStarted();
        policy.OnTestApplicationStarted();

        policy.Reason.Should().Be(TestRunCancellationReason.None);
    }

    [TestMethod]
    public async Task TimeoutOnlyCountsTimeWithActiveTestApplications()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        using var policy = new TestRunPolicy(maximumFailedTests: null, timeout: TimeSpan.FromMilliseconds(300));

        policy.OnTestApplicationStarted();
        await Task.Delay(100, cancellationToken);
        policy.OnTestApplicationExited();

        await Task.Delay(300, cancellationToken);
        policy.Reason.Should().Be(TestRunCancellationReason.None);

        policy.OnTestApplicationStarted();
        TestRunCancellationReason reason = await policy.Cancellation.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        reason.Should().Be(TestRunCancellationReason.Timeout);
    }

    [TestMethod]
    public async Task CompletingPolicyPreventsLateTimeout()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        using var policy = new TestRunPolicy(maximumFailedTests: null, timeout: TimeSpan.FromMilliseconds(100));

        policy.OnTestApplicationStarted();
        TestRunCancellationReason reason = policy.Complete();
        await Task.Delay(200, cancellationToken);

        reason.Should().Be(TestRunCancellationReason.None);
        policy.Reason.Should().Be(TestRunCancellationReason.None);
        policy.Token.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public void CompletingPolicyPreservesCancellationReason()
    {
        using var policy = new TestRunPolicy(maximumFailedTests: 1, timeout: null);

        policy.ReportFailedTests(1);

        policy.Complete().Should().Be(TestRunCancellationReason.MaximumFailedTests);
    }

    [TestMethod]
    public async Task ConcurrentStartAndExitAtTimeoutBoundaryDoesNotThrow()
    {
        // Regression: OnTestApplicationExited can drive the remaining timeout non-positive a moment
        // before it flips Reason to Timeout. A test application starting in that window must not
        // construct a Timer with a negative due time (which previously threw and corrupted the
        // active-application accounting).
        CancellationToken cancellationToken = TestContext.CancellationToken;
        using var policy = new TestRunPolicy(maximumFailedTests: null, timeout: TimeSpan.FromMilliseconds(30));

        var workers = new Task[3];
        for (int w = 0; w < workers.Length; w++)
        {
            workers[w] = Task.Run(async () =>
            {
                while (policy.Reason == TestRunCancellationReason.None)
                {
                    policy.OnTestApplicationStarted();
                    await Task.Delay(1, cancellationToken);
                    policy.OnTestApplicationExited();
                }
            }, cancellationToken);
        }

        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        policy.Reason.Should().Be(TestRunCancellationReason.Timeout);
    }
}
