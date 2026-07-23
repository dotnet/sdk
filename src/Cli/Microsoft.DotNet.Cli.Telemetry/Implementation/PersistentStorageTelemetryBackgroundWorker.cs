// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

internal sealed class PersistentStorageTelemetryBackgroundWorker
{
    private readonly Func<CancellationToken, Task> _drainAsync;
    private int _started;
    private CancellationTokenSource? _cancellation;
    private Task? _task;

    public PersistentStorageTelemetryBackgroundWorker(
        ITelemetryBlobStorage storage,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain)
        : this(CreateDrainAsync(storage, ingestionTrackUri, leasePeriodMilliseconds, maxBlobsPerDrain))
    {
    }

    internal PersistentStorageTelemetryBackgroundWorker(Func<CancellationToken, Task> drainAsync)
    {
        _drainAsync = drainAsync;
    }

    public void StartOnce()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        _task = Task.Run(() => DrainAsync(_cancellation.Token));
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        _cancellation?.Cancel();
        if (_task is null)
        {
            return true;
        }

        try
        {
            return _task.Wait(timeoutMilliseconds);
        }
        catch (AggregateException)
        {
            return true;
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _drainAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static Func<CancellationToken, Task> CreateDrainAsync(
        ITelemetryBlobStorage storage,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain)
    {
        var transport = new HttpTelemetryUploadTransport(ingestionTrackUri);
        var uploader = new PersistentStorageTelemetryUploader(
            storage,
            transport,
            leasePeriodMilliseconds,
            maxBlobsPerDrain);
        return cancellationToken => uploader.DrainAsync(cancellationToken);
    }
}