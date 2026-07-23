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
            TelemetryUploadResult.RejectedAfter(TimeSpan.FromSeconds(7)),
            TelemetryUploadResult.Accepted);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);
        var clock = new FakeTimeProvider();
        var delays = new RecordingDelay(clock);

        await PersistentStorageTelemetryDrainer.RunCoreAsync(
            uploader,
            Timeout.InfiniteTimeSpan,
            CancellationToken.None,
            delays.DelayAsync,
            clock);

        delays.RequestedDelays.Should().Equal(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(7),
            TimeSpan.FromMilliseconds(500));
        transport.UploadCount.Should().Be(4);
    }

    [TestMethod]
    public async Task RunCoreAsync_StopsAtLifetimeWithoutWaiting()
    {
        var storage = new FakeBlobStorage(new FakeBlob([1]));
        var transport = new FakeTransport(TelemetryUploadResult.Rejected, TelemetryUploadResult.Rejected);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);
        var clock = new FakeTimeProvider();
        var delays = new RecordingDelay(clock);

        await PersistentStorageTelemetryDrainer.RunCoreAsync(
            uploader,
            TimeSpan.FromMilliseconds(1_500),
            CancellationToken.None,
            delays.DelayAsync,
            clock);

        delays.RequestedDelays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
        transport.UploadCount.Should().Be(2);
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

    private sealed class FakeTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan delay) => _timestamp += delay.Ticks;
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