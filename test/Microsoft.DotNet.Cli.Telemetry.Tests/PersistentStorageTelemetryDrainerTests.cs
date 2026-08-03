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
        var storage = new FakeBlobStorage(new FakeBlob([1]));
        var transport = new FakeTransport(
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
        var storage = new FakeBlobStorage(new FakeBlob([1]));
        var transport = new FakeTransport(TelemetryUploadResult.Rejected, TelemetryUploadResult.Rejected);
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
        var storage = new FakeBlobStorage(new FakeBlob([1]), new FakeBlob([2]));
        var transport = new FakeTransport(
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

    private sealed class FakeBlobStorage(params FakeBlob[] blobs) : ITelemetryBlobStorage
    {
        public IEnumerable<ITelemetryBlob> GetBlobs() => blobs.Where(blob => !blob.Deleted);

        public bool TryPersist(byte[] data) => true;
    }

    private sealed class FakeBlob(byte[] data) : ITelemetryBlob
    {
        public bool Deleted { get; private set; }

        public bool TryLease(int leasePeriodMilliseconds) => !Deleted;

        public bool TryRead(out byte[]? buffer)
        {
            buffer = data;
            return true;
        }

        public bool TryRelease() => true;

        public bool TryDelete()
        {
            Deleted = true;
            return true;
        }
    }

    private sealed class FakeTransport(params TelemetryUploadResult[] results) : ITelemetryUploadTransport
    {
        private readonly Queue<TelemetryUploadResult> _results = new(results);

        public int UploadCount { get; private set; }

        public Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken)
        {
            UploadCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }
}