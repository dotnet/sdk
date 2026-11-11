// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

internal sealed class FakeTelemetryBlobStorage(params FakeTelemetryBlob[] blobs) : ITelemetryBlobStorage
{
    public List<FakeTelemetryBlob> Blobs { get; } = [.. blobs];

    public IEnumerable<ITelemetryBlob> GetBlobs() => Blobs.Where(blob => !blob.Deleted);

    public bool TryPersist(byte[] data)
    {
        Blobs.Add(new FakeTelemetryBlob(data));
        return true;
    }
}

internal sealed class FakeTelemetryBlob(byte[]? data) : ITelemetryBlob
{
    public bool CanLease { get; set; } = true;
    public bool Leased { get; private set; }
    public bool Released { get; private set; }
    public bool Deleted { get; private set; }
    public byte[]? Data => data;

    public bool TryLease(int leasePeriodMilliseconds)
    {
        if (!CanLease || Deleted)
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

internal sealed class FakeTelemetryUploadTransport(params TelemetryUploadResult[] results) : ITelemetryUploadTransport
{
    private readonly Queue<TelemetryUploadResult> _results = new(results);

    public int UploadCount { get; private set; }

    public Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken)
    {
        UploadCount++;
        return Task.FromResult(_results.Dequeue());
    }
}
