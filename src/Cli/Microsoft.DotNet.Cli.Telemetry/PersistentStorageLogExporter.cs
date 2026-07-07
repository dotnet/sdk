// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.DotNet.Cli.Telemetry;

/// <summary>
/// The log counterpart of <see cref="PersistentStorageTraceExporter"/>: an OpenTelemetry log
/// exporter that maps each batch of <see cref="LogRecord"/> instances to the Application
/// Insights wire format and persists it to durable on-disk storage instead of transmitting it,
/// so a short-lived CLI process captures its log telemetry before exiting.
///
/// Like the trace exporter, it owns the persist-then-drain pipeline: the first time it exports
/// it starts a background <see cref="PersistentStorageTelemetryUploader"/> that opportunistically
/// uploads telemetry persisted by this and previous CLI invocations. Because it drains the same
/// storage the trace exporter uses, either exporter's drain uploads every persisted blob (leasing
/// prevents double-upload), so running both signals against one storage directory is safe.
///
/// This exporter should be driven by a <c>SimpleLogRecordExportProcessor</c> so that
/// <see cref="Export"/> runs synchronously as each log record is emitted, guaranteeing the write
/// completes before process shutdown.
/// </summary>
internal sealed class PersistentStorageLogExporter : BaseExporter<LogRecord>
{
    private readonly ITelemetryBlobStorage _storage;
    private readonly string _instrumentationKey;
    private readonly Uri _ingestionTrackUri;
    private readonly int _leasePeriodMilliseconds;
    private readonly int _maxBlobsPerDrain;
    private TelemetryResourceContext? _resourceContext;
    // Guards against starting more than one background drain per exporter.
    private int _drainStarted;

    public PersistentStorageLogExporter(
        ITelemetryBlobStorage storage,
        string instrumentationKey,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain)
    {
        _storage = storage;
        _instrumentationKey = instrumentationKey;
        _ingestionTrackUri = ingestionTrackUri;
        _leasePeriodMilliseconds = leasePeriodMilliseconds;
        _maxBlobsPerDrain = maxBlobsPerDrain;
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            StartBackgroundDrainOnce();

            var resource = _resourceContext ??= TelemetryResourceContextFactory.FromResource(ParentProvider?.GetResource());
            var bytes = AzureMonitorLogSerializer.SerializeBatch(in batch, resource, _instrumentationKey);
            if (bytes is null || bytes.Length == 0)
            {
                return ExportResult.Success;
            }

            return _storage.TryPersist(bytes) ? ExportResult.Success : ExportResult.Failure;
        }
        catch (Exception e)
        {
            // Telemetry must never surface errors to the CLI. Swallow and report failure.
            Debug.Fail(e.ToString());
            return ExportResult.Failure;
        }
    }

    private void StartBackgroundDrainOnce()
    {
        if (Interlocked.Exchange(ref _drainStarted, 1) != 0)
        {
            return;
        }

        var transport = new HttpTelemetryUploadTransport(_ingestionTrackUri);
        new PersistentStorageTelemetryUploader(_storage, transport, _leasePeriodMilliseconds, _maxBlobsPerDrain)
            .StartBackgroundDrain();
    }
}
