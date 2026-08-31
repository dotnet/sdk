// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

internal sealed class PersistentStorageTelemetryBackgroundWorker
{
    private readonly Func<CancellationToken, Task<TelemetryDrainResult>> _drainAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SemaphoreSlim _drainSignal = new(0, 1);
    private readonly object _syncLock = new();
    private CancellationTokenSource? _cancellation;
    private int _drainRequested;
    private bool _isShutdown;
    private Task? _task;

    public PersistentStorageTelemetryBackgroundWorker(
        ITelemetryBlobStorage storage,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain)
        : this(CreateDrainAsync(storage, ingestionTrackUri, leasePeriodMilliseconds, maxBlobsPerDrain))
    {
    }

    internal PersistentStorageTelemetryBackgroundWorker(Func<CancellationToken, Task<TelemetryDrainResult>> drainAsync)
        : this(drainAsync, static (delay, cancellationToken) => Task.Delay(delay, cancellationToken))
    {
    }

    internal PersistentStorageTelemetryBackgroundWorker(
        Func<CancellationToken, Task<TelemetryDrainResult>> drainAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _drainAsync = drainAsync;
        _delayAsync = delayAsync;
    }

    public void RequestDrain()
    {
        lock (_syncLock)
        {
            if (_isShutdown)
            {
                return;
            }

            if (_task is null)
            {
                _cancellation = new CancellationTokenSource();
                _task = Task.Run(() => DrainAsync(_cancellation.Token));
            }

            if (Interlocked.Exchange(ref _drainRequested, 1) == 0)
            {
                _drainSignal.Release();
            }
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        CancellationTokenSource? cancellation;
        Task? task;
        lock (_syncLock)
        {
            _isShutdown = true;
            cancellation = _cancellation;
            task = _task;
        }

        cancellation?.Cancel();
        if (task is null)
        {
            return true;
        }

        try
        {
            return task.Wait(timeoutMilliseconds);
        }
        catch (AggregateException)
        {
            return true;
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await _drainSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Exchange(ref _drainRequested, 0);
                TelemetryDrainResult result;
                do
                {
                    result = await _drainAsync(cancellationToken).ConfigureAwait(false);
                }
                while (result.DeletedBlobCount > 0
                    && !result.ShouldBackOff
                    && !cancellationToken.IsCancellationRequested);

                if (result.RetryAfter is { } retryAfter)
                {
                    await _delayAsync(
                        PersistentStorageTelemetryDrainer.ClampToSupportedTimerDelay(retryAfter),
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
        }
    }

    private static Func<CancellationToken, Task<TelemetryDrainResult>> CreateDrainAsync(
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