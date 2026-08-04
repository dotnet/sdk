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
    // FileBlobProvider creates the storage directory in its constructor, so defer building it
    // until telemetry is actually persisted or drained. Registering the exporter happens even
    // when telemetry is opted out (the registration runs in a static constructor, before the
    // per-invocation opt-out check), and merely registering it must not create the ~/.dotnet
    // telemetry folder. Lazy<T> defaults to thread-safe (ExecutionAndPublication) initialization,
    // which matters because the synchronous export path and the background drain can first touch
    // the provider concurrently.
    private readonly Lazy<PersistentBlobProvider> _provider;

    public FileSystemTelemetryBlobStorage(string storageDirectory)
    {
        _provider = new Lazy<PersistentBlobProvider>(() => new FileBlobProvider(storageDirectory));
    }

    public bool TryPersist(byte[] data)
        => _provider.Value.TryCreateBlob(data.AsSpan(), out _);

    public IEnumerable<ITelemetryBlob> GetBlobs()
    {
        foreach (var blob in _provider.Value.GetBlobs())
        {
            yield return new FileSystemTelemetryBlob(blob);
        }
    }

    private sealed class FileSystemTelemetryBlob(PersistentBlob blob) : ITelemetryBlob
    {
        public bool TryLease(int leasePeriodMilliseconds) => blob.TryLease(leasePeriodMilliseconds);

        public bool TryRead(out byte[]? data) => blob.TryRead(out data);

        public bool TryRelease()
        {
            if (blob is not FileBlob { FullPath: var lockedPath }
                || !lockedPath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var atSignIndex = lockedPath.LastIndexOf('@');
            if (atSignIndex < 0)
            {
                return false;
            }

            try
            {
                File.Move(lockedPath, lockedPath[..atSignIndex]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryDelete() => blob.TryDelete();
    }
}
