// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Phase 2 of the persist-then-drain pipeline: an in-process background worker that leases
/// persisted telemetry blobs and POSTs them to Azure Monitor. Because a CLI process is
/// short-lived it will usually exit before draining its <em>own</em> telemetry; that data is
/// delivered by a subsequent CLI invocation (eventual consistency across invocations).
///
/// The drain is best-effort and must never affect the CLI: every failure is swallowed, the
/// work runs on a background thread that is abandoned on process exit, and blobs that fail to
/// upload are left in place for a later retry once their lease expires.
/// </summary>
internal sealed class PersistentStorageTelemetryUploader
{
    // How long a blob is reserved to this process while uploading. If the process dies
    // mid-upload the lease expires and another invocation retries the blob.
    private const int DefaultLeasePeriodMilliseconds = 30_000;

    // Upper bound on blobs processed per drain, to keep the background work bounded even if a
    // large backlog accumulates. Remaining blobs are picked up by later invocations.
    private const int DefaultMaxBlobsPerDrain = 200;

    private readonly ITelemetryBlobStorage _storage;
    private readonly ITelemetryUploadTransport _transport;
    private readonly int _leasePeriodMilliseconds;
    private readonly int _maxBlobsPerDrain;

    public PersistentStorageTelemetryUploader(
        ITelemetryBlobStorage storage,
        ITelemetryUploadTransport transport,
        int leasePeriodMilliseconds = DefaultLeasePeriodMilliseconds,
        int maxBlobsPerDrain = DefaultMaxBlobsPerDrain)
    {
        _storage = storage;
        _transport = transport;
        _leasePeriodMilliseconds = leasePeriodMilliseconds;
        _maxBlobsPerDrain = maxBlobsPerDrain;
    }

    /// <summary>
    /// Starts draining on a background thread and returns immediately. Safe to call once per
    /// process; never throws.
    /// </summary>
    public void StartBackgroundDrain()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await DrainAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // Background telemetry drain must never surface errors.
                Debug.Fail(e.ToString());
            }
        });
    }

    /// <summary>
    /// Leases, uploads, and deletes persisted blobs. Blobs that fail to upload are left for a
    /// later retry. This method never throws.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        var processed = 0;
        // Retriable remainders from partially-accepted uploads. Persisted AFTER the enumeration
        // completes so we never mutate the storage collection while iterating it.
        List<byte[]>? retriableRemainders = null;

        foreach (var blob in _storage.GetBlobs())
        {
            if (processed++ >= _maxBlobsPerDrain || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Leasing is an atomic rename, so only one process uploads a given blob.
                if (!blob.TryLease(_leasePeriodMilliseconds))
                {
                    continue;
                }

                if (!blob.TryRead(out var data) || data is null || data.Length == 0)
                {
                    // Unreadable or empty blob: discard it so it doesn't linger forever.
                    blob.TryDelete();
                    continue;
                }

                var result = await _transport.TryUploadAsync(data, cancellationToken).ConfigureAwait(false);
                switch (result.Outcome)
                {
                    case TelemetryUploadOutcome.PartiallyAccepted:
                        // The accepted portion is delivered; queue the retriable remainder to
                        // persist as a fresh blob once we finish draining.
                        if (result.RetryPayload is { Length: > 0 } remainder)
                        {
                            (retriableRemainders ??= []).Add(remainder);
                        }
                        blob.TryDelete();
                        break;

                    case TelemetryUploadOutcome.Accepted:
                        blob.TryDelete();
                        break;

                    case TelemetryUploadOutcome.Rejected:
                        // Leave the blob in place; its lease will expire and a later invocation
                        // will retry it.
                        break;
                }
            }
            catch (Exception e)
            {
                // Swallow per-blob failures and keep going; the blob is retried later.
                Debug.Fail(e.ToString());
            }
        }

        if (retriableRemainders is not null)
        {
            foreach (var remainder in retriableRemainders)
            {
                _storage.TryPersist(remainder);
            }
        }
    }
}
