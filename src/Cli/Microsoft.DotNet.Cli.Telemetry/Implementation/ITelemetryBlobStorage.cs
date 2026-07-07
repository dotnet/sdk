// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Durable blob storage for serialized telemetry payloads. Abstracts the underlying
/// <c>OpenTelemetry.PersistentStorage.FileSystem</c> implementation so the persist and
/// drain paths can be unit-tested without touching disk.
/// </summary>
internal interface ITelemetryBlobStorage
{
    /// <summary>Persists <paramref name="data"/> as a new blob. Returns whether the write succeeded.</summary>
    bool TryPersist(byte[] data);

    /// <summary>Enumerates the currently persisted blobs (without leasing them).</summary>
    IEnumerable<ITelemetryBlob> GetBlobs();
}

/// <summary>
/// A single persisted telemetry payload. A blob must be leased before it is read and
/// uploaded, which (via an atomic file rename) prevents other concurrent CLI processes
/// from uploading the same payload.
/// </summary>
internal interface ITelemetryBlob
{
    /// <summary>
    /// Attempts to acquire an exclusive, time-limited lease on the blob. When the lease
    /// expires the blob becomes eligible for retry by another process.
    /// </summary>
    bool TryLease(int leasePeriodMilliseconds);

    /// <summary>Reads the persisted bytes.</summary>
    bool TryRead(out byte[]? data);

    /// <summary>Deletes the blob (called after a successful upload).</summary>
    bool TryDelete();
}
