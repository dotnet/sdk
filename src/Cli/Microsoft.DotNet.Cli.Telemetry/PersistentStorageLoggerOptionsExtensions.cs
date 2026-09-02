// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace OpenTelemetry.Logs;

/// <summary>
/// Extension methods that register the persist-then-drain telemetry exporter on
/// <see cref="OpenTelemetryLoggerOptions"/>, following the same registration convention as the
/// built-in OpenTelemetry log exporters (e.g. <c>AddOtlpExporter</c>,
/// <c>AddAzureMonitorLogExporter</c>).
/// </summary>
public static class PersistentStorageLoggerOptionsExtensions
{
    /// <summary>
    /// Adds the persist-then-drain pipeline as a log exporter: Phase 1 maps each log-record batch
    /// to the Application Insights wire format and writes it to durable on-disk storage instead of
    /// transmitting it (so a short-lived CLI process captures its logs before exiting), and Phase 2
    /// — a background uploader started by the exporter the first time it runs — delivers telemetry
    /// persisted by this and previous invocations. The exporter is driven by a
    /// <see cref="SimpleLogRecordExportProcessor"/> so writes complete synchronously as records are
    /// emitted.
    /// </summary>
    /// <param name="options">The logger options to register the exporter on.</param>
    /// <param name="configure">Configures the connection string, storage directory, and drain behavior.</param>
    /// <returns>
    /// The supplied <paramref name="options"/> for chaining. When the configured connection string
    /// or storage directory is missing or invalid, the options are returned unmodified.
    /// </returns>
    public static OpenTelemetryLoggerOptions AddPersistentStorageExporter(this OpenTelemetryLoggerOptions options, Action<PersistentStorageTelemetryOptions> configure)
    {
        var telemetryOptions = new PersistentStorageTelemetryOptions();
        configure(telemetryOptions);

        var connectionString = AzureMonitorConnectionString.Parse(telemetryOptions.ConnectionString);
        if (connectionString is null || string.IsNullOrWhiteSpace(telemetryOptions.StorageDirectory))
        {
            return options;
        }

        var storage = new FileSystemTelemetryBlobStorage(telemetryOptions.StorageDirectory);
        var exporter = new PersistentStorageLogExporter(
            storage,
            connectionString.InstrumentationKey,
            connectionString.TrackUri,
            telemetryOptions.LeasePeriodMilliseconds,
            telemetryOptions.MaxBlobsPerDrain,
            telemetryOptions.StartBackgroundDrain);
        return options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
    }
}
