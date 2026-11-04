// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class PersistentStorageTelemetryDrainerTests
{
    [TestMethod]
    public async Task RunCoreAsync_EscalatesRetryDelaysAndHonorsRetryAfter()
    {
        var storage = new FakeTelemetryBlobStorage(new FakeTelemetryBlob([1]));
        var transport = new FakeTelemetryUploadTransport(
            TelemetryUploadResult.Rejected,
            TelemetryUploadResult.Rejected,
            TelemetryUploadResult.RejectedRetryAfter(TimeSpan.FromSeconds(7)),
            TelemetryUploadResult.Accepted);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);
        var clock = new FakeTimeProvider();
        var delays = new RecordingDelay(clock);

        await PersistentStorageTelemetryDrainer.RunCoreAsync(
            uploader,
            CancellationToken.None,
            delays.DelayAsync,
            new FixedRandom(0.5));

        delays.RequestedDelays.Should().Equal(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(7),
            TimeSpan.FromMilliseconds(500));
        transport.UploadCount.Should().Be(4);
    }

    [TestMethod]
    public async Task RunCoreAsync_StopsWhenRetryDelayIsCancelled()
    {
        var storage = new FakeTelemetryBlobStorage(new FakeTelemetryBlob([1]));
        var transport = new FakeTelemetryUploadTransport(TelemetryUploadResult.Rejected, TelemetryUploadResult.Rejected);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);
        using var cancellation = new CancellationTokenSource();
        var delays = new CancellingDelay(cancellation);

        await PersistentStorageTelemetryDrainer.RunCoreAsync(
            uploader,
            cancellation.Token,
            delays.DelayAsync,
            new FixedRandom(0.5));

        delays.RequestedDelays.Should().Equal(TimeSpan.FromSeconds(10));
        transport.UploadCount.Should().Be(1);
    }

    [TestMethod]
    public async Task RunCoreAsync_DrainsBacklogBeforePostDrainGracePeriod()
    {
        var storage = new FakeTelemetryBlobStorage(new FakeTelemetryBlob([1]), new FakeTelemetryBlob([2]));
        var transport = new FakeTelemetryUploadTransport(
            TelemetryUploadResult.Accepted,
            TelemetryUploadResult.Accepted);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport, maxBlobsPerDrain: 1);
        var clock = new FakeTimeProvider();
        var delays = new RecordingDelay(clock);

        await PersistentStorageTelemetryDrainer.RunCoreAsync(
            uploader,
            CancellationToken.None,
            delays.DelayAsync,
            new FixedRandom(0.5));

        transport.UploadCount.Should().Be(2);
        delays.RequestedDelays.Should().Equal(TimeSpan.FromMilliseconds(500));
    }

    [TestMethod]
    public void ClampToSupportedTimerDelay_ClampsRuntimeUnsupportedDurations()
    {
        var delay = PersistentStorageTelemetryDrainer.ClampToSupportedTimerDelay(TimeSpan.FromDays(50));

        delay.Should().Be(TimeSpan.FromMilliseconds(uint.MaxValue - 1));
    }

    [TestMethod]
    public void TryAcquireDirectoryLock_AllowsOnlyOneActiveDrainer()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using (var firstLock = PersistentStorageTelemetryDrainer.TryAcquireDirectoryLock(directory))
            using (var secondLock = PersistentStorageTelemetryDrainer.TryAcquireDirectoryLock(directory))
            {
                firstLock.Should().NotBeNull();
                secondLock.Should().BeNull();
            }

        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RecordingDelay(FakeTimeProvider clock)
    {
        public List<TimeSpan> RequestedDelays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            RequestedDelays.Add(delay);
            clock.Advance(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingDelay(CancellationTokenSource cancellation)
    {
        public List<TimeSpan> RequestedDelays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            RequestedDelays.Add(delay);
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan delay) => _timestamp += delay.Ticks;
    }

    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

}