// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class PersistentStorageTelemetryUploaderTests
{
    [TestMethod]
    public async Task ItUploadsAndDeletesBlobsOnSuccess()
    {
        var storage = new FakeBlobStorage(new FakeBlob([1, 2, 3]), new FakeBlob([4, 5, 6]));
        var transport = new FakeTransport(TelemetryUploadResult.Accepted);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        await uploader.DrainAsync(CancellationToken.None);

        transport.UploadCount.Should().Be(2);
        storage.Blobs.Should().OnlyContain(b => b.Leased && b.Deleted);
    }

    [TestMethod]
    public async Task ItDeletesBlobWhenUploadSucceeds()
    {
        var blob = new FakeBlob([10, 20, 30]);
        var storage = new FakeBlobStorage(blob);
        var transport = new FakeTransport(TelemetryUploadResult.Accepted);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        await uploader.DrainAsync(CancellationToken.None);

        blob.Deleted.Should().BeTrue("successfully uploaded blobs must be deleted from storage");
        blob.Leased.Should().BeTrue("blob must be leased before uploading");
    }

    [TestMethod]
    public async Task ItRetainsBlobsWhenUploadFails()
    {
        var storage = new FakeBlobStorage(new FakeBlob([1, 2, 3]));
        var transport = new FakeTransport(TelemetryUploadResult.Rejected);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        await uploader.DrainAsync(CancellationToken.None);

        transport.UploadCount.Should().Be(1);
        storage.Blobs.Single().Deleted.Should().BeFalse("failed uploads must be retried later");
        storage.Blobs.Single().Released.Should().BeTrue("failed uploads must be available to a later drain");
    }

    [TestMethod]
    public async Task ItReleasesLeaseWhenUploadIsCancelled()
    {
        var blob = new FakeBlob([1, 2, 3]);
        var storage = new FakeBlobStorage(blob);
        using var cancellationSource = new CancellationTokenSource();
        var uploader = new PersistentStorageTelemetryUploader(storage, new CancellingTransport(cancellationSource));

        await uploader.DrainAsync(cancellationSource.Token);

        blob.Deleted.Should().BeFalse();
        blob.Released.Should().BeTrue("cancelled uploads must leave the blob available to a later drain");
    }

    [TestMethod]
    public async Task ItSkipsBlobsThatCannotBeLeased()
    {
        var leasable = new FakeBlob([1]);
        var locked = new FakeBlob([2]) { CanLease = false };
        var storage = new FakeBlobStorage(locked, leasable);
        var transport = new FakeTransport(TelemetryUploadResult.Accepted);
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        await uploader.DrainAsync(CancellationToken.None);

        transport.UploadCount.Should().Be(1);
        locked.Deleted.Should().BeFalse();
        leasable.Deleted.Should().BeTrue();
    }

    [TestMethod]
    public async Task ItDeletesUnreadableBlobsWithoutUploading()
    {
        var unreadable = new FakeBlob(null);
        var storage = new FakeBlobStorage(unreadable);
        var transport = new FakeTransport(TelemetryUploadResult.Accepted);

        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        await uploader.DrainAsync(CancellationToken.None);

        transport.UploadCount.Should().Be(0);
        unreadable.Deleted.Should().BeTrue();
    }

    [TestMethod]
    public async Task ItPersistsRetriableRemainderAndDeletesBlobOnPartialAcceptance()
    {
        var remainder = new byte[] { 9, 9, 9 };
        var storage = new FakeBlobStorage(new FakeBlob([1, 2, 3]));
        var transport = new FakeTransport(TelemetryUploadResult.PartiallyAccepted(remainder));
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        var result = await uploader.DrainAsync(CancellationToken.None);

        transport.UploadCount.Should().Be(1);
        // The original blob is deleted (its accepted portion was delivered)...
        storage.Blobs[0].Deleted.Should().BeTrue();
        // ...and the retriable remainder is persisted as a fresh blob for a later retry.
        storage.Blobs.Should().ContainSingle(b => !b.Deleted && b.Data == remainder);
        result.ShouldBackOff.Should().BeTrue();
    }

    [TestMethod]
    public async Task ItStopsPassAndReportsRetryAfterOnRetryableResponse()
    {
        var expectedDelay = TimeSpan.FromSeconds(17);
        var first = new FakeBlob([1, 2, 3]);
        var second = new FakeBlob([4, 5, 6]);
        var storage = new FakeBlobStorage(first, second);
        var transport = new FakeTransport(TelemetryUploadResult.RejectedAfter(expectedDelay));
        var uploader = new PersistentStorageTelemetryUploader(storage, transport);

        var result = await uploader.DrainAsync(CancellationToken.None);

        transport.UploadCount.Should().Be(1, "the service has already asked this pass to retry later");
        first.Released.Should().BeTrue();
        second.Leased.Should().BeFalse();
        result.ForwardProgress.Should().Be(0);
        result.ShouldBackOff.Should().BeTrue();
        result.RetryAfter.Should().Be(expectedDelay);
    }

    [TestMethod]
    public async Task ItStopsPassAndBacksOffWhenTransportThrows()
    {
        var first = new FakeBlob([1, 2, 3]);
        var second = new FakeBlob([4, 5, 6]);
        var storage = new FakeBlobStorage(first, second);
        var uploader = new PersistentStorageTelemetryUploader(storage, new ThrowingTransport());

        var result = await uploader.DrainAsync(CancellationToken.None);

        first.Released.Should().BeTrue();
        second.Leased.Should().BeFalse();
        result.ForwardProgress.Should().Be(0);
        result.ShouldBackOff.Should().BeTrue();
        result.RetryAfter.Should().BeNull();
    }

    private sealed class FakeBlobStorage(params FakeBlob[] blobs) : ITelemetryBlobStorage
    {
        public List<FakeBlob> Blobs { get; } = [.. blobs];

        public bool TryPersist(byte[] data)
        {
            Blobs.Add(new FakeBlob(data));
            return true;
        }

        public IEnumerable<ITelemetryBlob> GetBlobs() => Blobs;
    }

    private sealed class FakeBlob(byte[]? data) : ITelemetryBlob
    {
        public bool CanLease { get; set; } = true;
        public bool Leased { get; private set; }
        public bool Released { get; private set; }
        public bool Deleted { get; private set; }
        public byte[]? Data => data;

        public bool TryLease(int leasePeriodMilliseconds)
        {
            if (!CanLease)
            {
                return false;
            }

            Leased = true;
            return true;
        }

        public bool TryRead(out byte[]? buffer)
        {
            buffer = data;
            return data is not null;
        }

        public bool TryRelease()
        {
            Released = true;
            return true;
        }

        public bool TryDelete()
        {
            Deleted = true;
            return true;
        }
    }

    private sealed class FakeTransport(TelemetryUploadResult result) : ITelemetryUploadTransport
    {
        public int UploadCount { get; private set; }

        public Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken)
        {
            UploadCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class CancellingTransport(CancellationTokenSource cancellationSource) : ITelemetryUploadTransport
    {
        public Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken)
        {
            cancellationSource.Cancel();
            return Task.FromCanceled<TelemetryUploadResult>(cancellationToken);
        }
    }

    private sealed class ThrowingTransport : ITelemetryUploadTransport
    {
        public Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken)
            => throw new HttpRequestException("Transient test failure.");
    }
}
