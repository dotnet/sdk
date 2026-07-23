// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry;

/// <summary>
/// An OpenTelemetry trace exporter that does not transmit anything itself. Instead it maps
/// each batch of spans to the Application Insights wire format and persists it to durable
/// on-disk storage, so a short-lived CLI process is guaranteed to capture its telemetry
/// before exiting.
///
/// The exporter owns the full persist-then-drain pipeline: as soon as it starts exporting
/// (i.e. the first span ends) it kicks off a background <see cref="PersistentStorageTelemetryUploader"/>
/// that opportunistically uploads telemetry persisted by this and previous CLI invocations.
/// Tying the drain to the exporter's own lifecycle means it only runs when telemetry is
/// actually enabled, and its behavior is configured through the same exporter options.
///
/// This exporter should be driven by a <c>SimpleActivityExportProcessor</c> so that
/// <see cref="Export"/> runs synchronously as each span ends, guaranteeing the write
/// completes before process shutdown.
/// </summary>
internal sealed class PersistentStorageTraceExporter : BaseExporter<Activity>
{
    private readonly ITelemetryBlobStorage _storage;
    private readonly string _instrumentationKey;
    private readonly PersistentStorageTelemetryBackgroundWorker? _backgroundWorker;
    private TelemetryResourceContext? _resourceContext;

    public PersistentStorageTraceExporter(
        ITelemetryBlobStorage storage,
        string instrumentationKey,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain,
        bool startBackgroundDrain = true)
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

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            // Phase 2: the first time the exporter runs, start the background upload of telemetry
            // persisted by this and previous invocations. Doing this here (rather than at
            // construction) ties the drain to the exporter actually being started and keeps it
            // from running when telemetry is opted out (no spans are exported in that case).
            _backgroundWorker?.StartOnce();

            var resource = _resourceContext ??= TelemetryResourceContextFactory.FromResource(ParentProvider?.GetResource());
            var bytes = AzureMonitorTelemetrySerializer.SerializeBatch(in batch, resource, _instrumentationKey);
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

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return _backgroundWorker?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
