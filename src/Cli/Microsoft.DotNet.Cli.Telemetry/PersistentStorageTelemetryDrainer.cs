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
/// The drainer is safe to over-invoke: a named mutex keyed on the storage directory keeps a single
/// instance active per directory, and blob leasing prevents any two processes from uploading the
/// same blob. It never throws.
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

        Mutex? mutex = null;
        var acquired = false;
        try
        {
            mutex = new Mutex(initiallyOwned: false, BuildMutexName(storageDirectory!));
            try
            {
                acquired = mutex.WaitOne(TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                // A previous drainer exited without releasing the mutex; we now own it.
                acquired = true;
            }

            if (!acquired)
            {
                // Another drainer is already active for this storage directory.
                return;
            }

            using var lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (maxLifetime > TimeSpan.Zero)
            {
                lifetimeCts.CancelAfter(GetBoundedTaskDelay(maxLifetime));
            }

            var token = lifetimeCts.Token;
            var storage = new FileSystemTelemetryBlobStorage(storageDirectory!);
            var transport = new HttpTelemetryUploadTransport(parsedConnectionString.TrackUri);
            var uploader = new PersistentStorageTelemetryUploader(storage, transport, leasePeriodMilliseconds, maxBlobsPerDrain);
            var consecutiveRetryPasses = 0;

            while (!token.IsCancellationRequested)
            {
                TelemetryDrainResult result;
                try
                {
                    result = await uploader.DrainAsync(token).ConfigureAwait(false);
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

                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            // Absolutely never surface telemetry errors to the host.
            Debug.Fail(e.ToString());
        }
        finally
        {
            if (mutex is not null)
            {
                if (acquired)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (Exception e)
                    {
                        Debug.Fail(e.ToString());
                    }
                }

                mutex.Dispose();
            }
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

    // A stable, filesystem/path-independent mutex name derived from the storage directory so that
    // exactly one drainer is active per directory. Uses the "Local\" namespace (per-session), which
    // is sufficient because drainers only race against other CLI invocations in the same session.
    private static string BuildMutexName(string storageDirectory)
    {
        // Normalize casing on Windows where paths are case-insensitive so two spellings of the same
        // directory map to one mutex.
        var normalized = OperatingSystem.IsWindows()
            ? storageDirectory.ToUpperInvariant()
            : storageDirectory;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "Local\\dotnet-cli-telemetry-drain-" + Convert.ToHexString(hash);
    }
}
