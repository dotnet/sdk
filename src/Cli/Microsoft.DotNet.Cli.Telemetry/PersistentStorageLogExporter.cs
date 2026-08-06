// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
/// Like the trace exporter, it owns the persist-then-drain pipeline: after persisting a batch it
/// notifies a background <see cref="PersistentStorageTelemetryUploader"/> that opportunistically
/// uploads telemetry persisted by this and previous CLI invocations. Because it drains the same
/// storage the trace exporter uses, either exporter's drain uploads every persisted blob (leasing
/// prevents double-upload), so running both signals against one storage directory is safe.
///
/// This exporter should be driven by a <c>SimpleLogRecordExportProcessor</c> so that
/// <c>Export</c> runs synchronously as each log record is emitted, guaranteeing the write
/// completes before process shutdown.
/// </summary>
internal sealed class PersistentStorageLogExporter : PersistentStorageTelemetryExporter<LogRecord>
{
    public PersistentStorageLogExporter(
        ITelemetryBlobStorage storage,
        string instrumentationKey,
        Uri ingestionTrackUri,
        int leasePeriodMilliseconds,
        int maxBlobsPerDrain,
        bool startBackgroundDrain = true)
        : base(
            storage,
            instrumentationKey,
            ingestionTrackUri,
            leasePeriodMilliseconds,
            maxBlobsPerDrain,
            startBackgroundDrain)
    {
    }

    protected override byte[]? SerializeBatch(
        in Batch<LogRecord> batch,
        TelemetryResourceContext resource,
        string instrumentationKey)
        => AzureMonitorLogSerializer.SerializeBatch(in batch, resource, instrumentationKey);
}
