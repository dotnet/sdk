// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry;

internal abstract class PersistentStorageTelemetryExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly ITelemetryBlobStorage _storage;
    private readonly string _instrumentationKey;
    private readonly PersistentStorageTelemetryBackgroundWorker? _backgroundWorker;
    private TelemetryResourceContext? _resourceContext;

    protected PersistentStorageTelemetryExporter(
        ITelemetryBlobStorage storage,
        string instrumentationKey,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain,
        bool startBackgroundDrain)
    {
        _storage = storage;
        _instrumentationKey = instrumentationKey;
        if (startBackgroundDrain)
        {
            _backgroundWorker = new PersistentStorageTelemetryBackgroundWorker(
                storage,
                ingestionTrackUri,
                leasePeriodMilliseconds,
                maxBlobsPerDrain);
        }
    }

    public sealed override ExportResult Export(in Batch<T> batch)
    {
        try
        {
            var resource = _resourceContext ??= TelemetryResourceContextFactory.FromResource(ParentProvider?.GetResource());
            var bytes = SerializeBatch(in batch, resource, _instrumentationKey);
            if (bytes is null || bytes.Length == 0)
            {
                _backgroundWorker?.RequestDrain();
                return ExportResult.Success;
            }

            var persisted = _storage.TryPersist(bytes);
            _backgroundWorker?.RequestDrain();

            return persisted
                ? ExportResult.Success
                : ExportResult.Failure;
        }
        catch (Exception e)
        {
            // Telemetry must never surface errors to the CLI. Swallow and report failure.
            Debug.Fail(e.ToString());
            return ExportResult.Failure;
        }
    }

    protected abstract byte[]? SerializeBatch(
        in Batch<T> batch,
        TelemetryResourceContext resource,
        string instrumentationKey);

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return _backgroundWorker?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
