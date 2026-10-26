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
/// The exporter owns the full persist-then-drain pipeline: after persisting a batch it notifies a
/// background <see cref="PersistentStorageTelemetryUploader"/> that opportunistically uploads
/// telemetry persisted by this and previous CLI invocations.
/// Tying the drain to the exporter's own lifecycle means it only runs when telemetry is
/// actually enabled, and its behavior is configured through the same exporter options.
///
/// This exporter should be driven by a <c>SimpleActivityExportProcessor</c> so that
/// <c>Export</c> runs synchronously as each span ends, guaranteeing the write
/// completes before process shutdown.
/// </summary>
internal sealed class PersistentStorageTraceExporter : PersistentStorageTelemetryExporter<Activity>
{
    public PersistentStorageTraceExporter(
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
        in Batch<Activity> batch,
        TelemetryResourceContext resource,
        string instrumentationKey)
        => AzureMonitorTelemetrySerializer.SerializeBatch(in batch, resource, instrumentationKey);
}
