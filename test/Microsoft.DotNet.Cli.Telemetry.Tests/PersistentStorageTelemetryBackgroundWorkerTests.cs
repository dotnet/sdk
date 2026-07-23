// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class PersistentStorageTelemetryBackgroundWorkerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void StartOnce_StartsOnlyOneDrain()
    {
        int starts = 0;
        var worker = new PersistentStorageTelemetryBackgroundWorker(_ =>
        {
            Interlocked.Increment(ref starts);
            return Task.CompletedTask;
        });

        worker.StartOnce();
        worker.StartOnce();

        worker.Shutdown(1_000).Should().BeTrue();
        starts.Should().Be(1);
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
        });

        worker.StartOnce();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        worker.Shutdown(1_000).Should().BeTrue();
        observedToken.IsCancellationRequested.Should().BeTrue();
    }
}