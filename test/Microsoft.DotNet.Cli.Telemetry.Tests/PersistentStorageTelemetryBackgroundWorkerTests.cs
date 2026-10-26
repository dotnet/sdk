// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class PersistentStorageTelemetryBackgroundWorkerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RequestDrain_StartsWorkerAndDrainsEachNotification()
    {
        int starts = 0;
        var firstDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new PersistentStorageTelemetryBackgroundWorker(_ =>
        {
            if (Interlocked.Increment(ref starts) == 1)
            {
                firstDrain.SetResult();
            }
            else
            {
                secondDrain.SetResult();
            }

            return Task.FromResult(new TelemetryDrainResult(0, shouldBackOff: false, retryAfter: null));
        });

        worker.RequestDrain();
        await firstDrain.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        worker.RequestDrain();
        await secondDrain.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        worker.Shutdown(1_000).Should().BeTrue();
        starts.Should().Be(2);
    }

    [TestMethod]
    public async Task Shutdown_CancelsAndWaitsForDrain()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedToken = default;
        var worker = new PersistentStorageTelemetryBackgroundWorker(async cancellationToken =>
        {
            observedToken = cancellationToken;
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new TelemetryDrainResult(0, shouldBackOff: false, retryAfter: null);
        });

        worker.RequestDrain();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        worker.Shutdown(1_000).Should().BeTrue();
        observedToken.IsCancellationRequested.Should().BeTrue();
    }

    [TestMethod]
    public async Task RequestDrain_ContinuesUntilTheBacklogIsEmpty()
    {
        int passes = 0;
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new PersistentStorageTelemetryBackgroundWorker(_ =>
        {
            var pass = Interlocked.Increment(ref passes);
            if (pass == 3)
            {
                drained.SetResult();
            }

            return Task.FromResult(new TelemetryDrainResult(
                deletedBlobCount: pass < 3 ? 1 : 0,
                shouldBackOff: false,
                retryAfter: null));
        });

        worker.RequestDrain();
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        worker.Shutdown(1_000).Should().BeTrue();
        passes.Should().Be(3);
    }

    [TestMethod]
    public async Task RequestDrain_HonorsRetryAfterBeforeDrainingAgain()
    {
        var expectedDelay = TimeSpan.FromSeconds(17);
        var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var passes = 0;
        var worker = new PersistentStorageTelemetryBackgroundWorker(
            _ =>
            {
                if (Interlocked.Increment(ref passes) == 1)
                {
                    return Task.FromResult(new TelemetryDrainResult(
                        deletedBlobCount: 0,
                        shouldBackOff: true,
                        retryAfter: expectedDelay));
                }

                secondDrain.SetResult();
                return Task.FromResult(new TelemetryDrainResult(0, shouldBackOff: false, retryAfter: null));
            },
            (delay, _) =>
            {
                delay.Should().Be(expectedDelay);
                delayStarted.SetResult();
                return releaseDelay.Task;
            });

        worker.RequestDrain();
        await delayStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        worker.RequestDrain();

        secondDrain.Task.IsCompleted.Should().BeFalse();
        releaseDelay.SetResult();
        await secondDrain.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        worker.Shutdown(1_000).Should().BeTrue();
        passes.Should().Be(2);
    }
}