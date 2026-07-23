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
    /// Leases, uploads, and deletes persisted blobs. Blobs that fail to upload are left for a
    /// later retry. This method never throws.
    /// </summary>
    public async Task<TelemetryDrainResult> DrainAsync(CancellationToken cancellationToken)
    {
        var processed = 0;
        var forwardProgress = 0;
        var shouldBackOff = false;
        TimeSpan? retryAfter = null;
        // Retriable remainders from partially-accepted uploads. Persisted AFTER the enumeration
        // completes so we never mutate the storage collection while iterating it.
        List<byte[]>? retriableRemainders = null;

        foreach (var blob in _storage.GetBlobs())
        {
            if (processed++ >= _maxBlobsPerDrain || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var leased = false;
            var deleted = false;
            try
            {
                // Leasing is an atomic rename, so only one process uploads a given blob.
                if (!(leased = blob.TryLease(_leasePeriodMilliseconds)))
                {
                    continue;
                }

                if (!blob.TryRead(out var data) || data is null || data.Length == 0)
                {
                    // Unreadable or empty blob: discard it so it doesn't linger forever.
                    deleted = blob.TryDelete();
                    continue;
                }

                // Each upload gets a per-blob timeout derived from the lease period. If the
                // caller already has a tighter deadline (e.g. provider Shutdown budget), the
                // linked token fires first and cancellation flows through to the HTTP call.
                using var perBlobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                perBlobCts.CancelAfter(TimeSpan.FromMilliseconds(_leasePeriodMilliseconds));

                var result = await _transport.TryUploadAsync(data, perBlobCts.Token).ConfigureAwait(false);
                switch (result.Outcome)
                {
                    case TelemetryUploadOutcome.PartiallyAccepted:
                        // The accepted portion is delivered; queue the retriable remainder to
                        // persist as a fresh blob once we finish draining.
                        if (result.RetryPayload is { Length: > 0 } remainder)
                        {
                            (retriableRemainders ??= []).Add(remainder);
                        }
                        shouldBackOff = true;
                        retryAfter = result.RetryAfter;
                        deleted = blob.TryDelete();
                        break;

                    case TelemetryUploadOutcome.Accepted:
                        deleted = blob.TryDelete();
                        break;

                    case TelemetryUploadOutcome.PermanentlyRejected:
                        // Retrying cannot succeed. Delete this poison blob so later telemetry
                        // in the storage directory can continue draining.
                        deleted = blob.TryDelete();
                        break;

                    case TelemetryUploadOutcome.Rejected:
                        // Leave the blob in place; its lease will expire and a later invocation
                        // will retry it.
                        shouldBackOff = true;
                        retryAfter = result.RetryAfter;
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The overall drain was cancelled (e.g. provider shutdown budget expired).
                // Stop processing and persist any retriable remainders we already have.
                break;
            }
            catch (Exception e)
            {
                // Network and storage failures are normally transient. Stop this pass and let the
                // caller back off before trying the blob again rather than moving immediately to
                // the rest of the backlog.
                Debug.Fail(e.ToString());
                shouldBackOff = true;
            }
            finally
            {
                if (deleted)
                {
                    forwardProgress++;
                }
                else if (leased)
                {
                    blob.TryRelease();
                }
            }

            if (shouldBackOff)
            {
                // A retryable response normally indicates service throttling or a transient
                // failure. Stop this pass rather than submitting every remaining blob to a
                // service that has already asked us to retry.
                break;
            }
        }

        if (retriableRemainders is not null)
        {
            foreach (var remainder in retriableRemainders)
            {
                _storage.TryPersist(remainder);
            }
        }

        return new TelemetryDrainResult(forwardProgress, shouldBackOff, retryAfter);
    }
}
