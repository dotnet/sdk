// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry;

/// <summary>
/// A standalone, out-of-band drainer for the persist-then-drain telemetry pipeline. It repeatedly
/// leases persisted telemetry blobs from a storage directory and POSTs them to Azure Monitor until
/// the storage is drained or a bounded lifetime elapses.
///
/// This exists for hosts that persist telemetry with the background drain disabled
/// (<see cref="PersistentStorageTelemetryOptions.StartBackgroundDrain"/> = <see langword="false"/>)
/// and instead deliver it from a separate, short-lived process — for example a detached child the
/// CLI spawns on exit so it can return immediately without waiting on the network. Because delivery
/// happens in that child, the persisting process never blocks on an HTTP POST, yet the current run's
/// telemetry is still delivered promptly rather than waiting for a future invocation.
///
/// The drainer is safe to over-invoke: an exclusive-share lock file keyed on the storage
/// directory keeps a single instance active per directory, and blob leasing prevents any two
/// processes from uploading the same blob. It never throws.
/// </summary>
public static class PersistentStorageTelemetryDrainer
{
    // Brief pause between drain passes so that concurrent CLI invocations have a chance to persist
    // new blobs (which this same drainer then delivers) and so a transient rejection does not spin
    // the loop. Kept short because the loop already exits as soon as a pass makes no progress.
    private static readonly TimeSpan s_interPassDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan s_initialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_maxRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_maxTaskDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    // Name of the exclusive-share lock file that single-instances the drainer per storage directory.
    private const string LockFileName = ".drain.lock";

    /// <summary>
    /// Drains persisted telemetry until the storage empties or <paramref name="maxLifetime"/>
    /// elapses (whichever comes first), then returns. Never throws.
    /// </summary>
    /// <param name="connectionString">
    /// The Application Insights connection string identifying the instrumentation key and ingestion
    /// endpoint. When null, empty, or unparseable the drainer no-ops.
    /// </param>
    /// <param name="storageDirectory">
    /// The directory persisted telemetry blobs are drained from. When null or empty the drainer
    /// no-ops. Should match the <see cref="PersistentStorageTelemetryOptions.StorageDirectory"/>
    /// the persisting process wrote to.
    /// </param>
    /// <param name="maxLifetime">
    /// An upper bound on how long the drainer runs. A non-positive value means no time bound (the
    /// drainer then runs until the storage empties or <paramref name="cancellationToken"/> fires).
    /// </param>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <param name="leasePeriodMilliseconds">
    /// How long a blob is exclusively leased while it is being uploaded.
    /// </param>
    /// <param name="maxBlobsPerDrain">The maximum number of blobs uploaded per pass.</param>
    public static async Task RunAsync(
        string? connectionString,
        string? storageDirectory,
        TimeSpan maxLifetime,
        CancellationToken cancellationToken = default,
        int leasePeriodMilliseconds = 30_000,
        int maxBlobsPerDrain = 200)
    {
        var parsedConnectionString = AzureMonitorConnectionString.Parse(connectionString);
        if (parsedConnectionString is null || string.IsNullOrWhiteSpace(storageDirectory))
        {
            return;
        }

        FileStream? directoryLock = null;
        try
        {
            directoryLock = TryAcquireDirectoryLock(storageDirectory!);
            if (directoryLock is null)
            {
                // Another drainer is already active for this storage directory, or the platform
                // does not support file locking. Skip this run.
                return;
            }

            var storage = new FileSystemTelemetryBlobStorage(storageDirectory!);
            var transport = new HttpTelemetryUploadTransport(parsedConnectionString.TrackUri);
            var uploader = new PersistentStorageTelemetryUploader(storage, transport, leasePeriodMilliseconds, maxBlobsPerDrain);
            using var lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (maxLifetime > TimeSpan.Zero)
            {
                lifetimeCts.CancelAfter(GetBoundedTaskDelay(maxLifetime));
            }

            await RunCoreAsync(
                uploader,
                maxLifetime,
                lifetimeCts.Token,
                static (delay, token) => Task.Delay(delay, token),
                TimeProvider.System).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // Absolutely never surface telemetry errors to the host.
            Debug.Fail(e.ToString());
        }
        finally
        {
            // Releasing an OS file lock has no thread affinity, so unlike a named Mutex it is safe
            // to dispose on whatever thread pool thread the async loop resumed on. The OS also drops
            // the lock automatically if this process is killed before reaching here.
            directoryLock?.Dispose();
        }
    }

    internal static TimeSpan GetRetryDelay(int consecutiveRetryPasses, TimeSpan? serverRetryAfter)
    {
        if (serverRetryAfter is { } requestedDelay)
        {
            return GetBoundedTaskDelay(
                requestedDelay < s_initialRetryDelay ? s_initialRetryDelay : requestedDelay);
        }

        var exponent = Math.Clamp(consecutiveRetryPasses - 1, 0, 30);
        var delayMilliseconds = s_initialRetryDelay.TotalMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(Math.Min(delayMilliseconds, s_maxRetryDelay.TotalMilliseconds));
    }

    internal static TimeSpan GetBoundedTaskDelay(TimeSpan delay)
        => delay > s_maxTaskDelay ? s_maxTaskDelay : delay;

    internal static async Task RunCoreAsync(
        PersistentStorageTelemetryUploader uploader,
        TimeSpan maxLifetime,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeProvider timeProvider)
    {
        var startTimestamp = timeProvider.GetTimestamp();
        var consecutiveRetryPasses = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var remainingLifetime = GetRemainingLifetime(maxLifetime, timeProvider, startTimestamp);
            if (remainingLifetime is { } remaining && remaining <= TimeSpan.Zero)
            {
                break;
            }

            TelemetryDrainResult result;
            try
            {
                result = await uploader.DrainAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                // The drainer must never surface errors. Stop on unexpected failure.
                Debug.Fail(e.ToString());
                break;
            }

            if (result.ForwardProgress == 0 && !result.ShouldBackOff)
            {
                // No forward progress: the storage is drained, or all remaining blobs are
                // leased by another process or were rejected. Either way, stop.
                break;
            }

            var delay = s_interPassDelay;
            if (result.ShouldBackOff)
            {
                consecutiveRetryPasses++;
                delay = GetRetryDelay(consecutiveRetryPasses, result.RetryAfter);
            }
            else
            {
                consecutiveRetryPasses = 0;
            }

            remainingLifetime = GetRemainingLifetime(maxLifetime, timeProvider, startTimestamp);
            if (remainingLifetime is { } remainingDelay)
            {
                if (remainingDelay <= TimeSpan.Zero)
                {
                    break;
                }

                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks, remainingDelay.Ticks));
            }

            try
            {
                await delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static TimeSpan? GetRemainingLifetime(TimeSpan maxLifetime, TimeProvider timeProvider, long startTimestamp)
    {
        if (maxLifetime <= TimeSpan.Zero)
        {
            return null;
        }

        return maxLifetime - timeProvider.GetElapsedTime(startTimestamp);
    }

    // Single-instance-per-directory guard for the drainer. Returns a held lock handle, or null when
    // another drainer already owns the directory (or the platform cannot lock).
    //
    // Uses an exclusive-share lock file rather than a named Mutex because the drain loop awaits: a
    // Mutex is thread-affine and must be released on the same thread that acquired it, but an async
    // continuation can resume on any thread pool thread, so releasing/disposing it could throw. A
    // file handle has no thread affinity, so it can be released on any thread, and the OS drops it if
    // the process is killed mid-drain (no abandoned-lock recovery needed).
    //
    // On Windows, FileShare.None is a mandatory share-mode lock. On Unix it is an advisory flock,
    // honored by every other .NET FileStream opener — which is the only contender here. On the rare
    // filesystem that does not support locking, the runtime silently takes no lock and two drainers
    // may run at once; that is harmless because blob leasing (an atomic rename) still prevents any
    // blob from being uploaded twice. Exclusivity comes from holding the handle, not from the file
    // existing, so a lock file left behind by a killed drainer is simply reopened and re-locked on
    // the next run; it is intentionally not deleted on close.
    internal static FileStream? TryAcquireDirectoryLock(string storageDirectory)
    {
        try
        {
            Directory.CreateDirectory(storageDirectory);
            var lockPath = Path.Combine(storageDirectory, LockFileName);
            return new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (IOException)
        {
            // Sharing violation (Windows) or EWOULDBLOCK (Unix): another drainer holds the lock.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Some platforms surface a sharing violation as an access denial.
            return null;
        }
    }
}
