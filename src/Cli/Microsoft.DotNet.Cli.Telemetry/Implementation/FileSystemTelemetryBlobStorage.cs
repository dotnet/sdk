// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// <see cref="ITelemetryBlobStorage"/> backed by the
/// <c>OpenTelemetry.PersistentStorage.FileSystem</c> <see cref="FileBlobProvider"/>. Blobs
/// are stored as files under a per-user telemetry storage directory. This is a public copy
/// of the same library the Azure Monitor exporter vendors internally, so it has its own
/// storage directory and does not conflict with the exporter's.
/// </summary>
internal sealed class FileSystemTelemetryBlobStorage : ITelemetryBlobStorage
{
    private readonly PersistentBlobProvider _provider;

    public FileSystemTelemetryBlobStorage(string storageDirectory)
    {
        _provider = new FileBlobProvider(storageDirectory);
    }

    public bool TryPersist(byte[] data)
        => _provider.TryCreateBlob(data.AsSpan(), out _);

    public IEnumerable<ITelemetryBlob> GetBlobs()
    {
        foreach (var blob in _provider.GetBlobs())
        {
            yield return new FileSystemTelemetryBlob(blob);
        }
    }

    private sealed class FileSystemTelemetryBlob(PersistentBlob blob) : ITelemetryBlob
    {
        public bool TryLease(int leasePeriodMilliseconds) => blob.TryLease(leasePeriodMilliseconds);

        public bool TryRead(out byte[]? data) => blob.TryRead(out data);

        public bool TryDelete() => blob.TryDelete();
    }
}
