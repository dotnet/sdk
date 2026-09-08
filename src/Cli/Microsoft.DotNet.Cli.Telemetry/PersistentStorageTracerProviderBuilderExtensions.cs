// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods that register the persist-then-drain telemetry exporter on a
/// <see cref="TracerProviderBuilder"/>, following the same registration convention as the
/// built-in OpenTelemetry exporters (e.g. <c>AddOtlpExporter</c>,
/// <c>AddAzureMonitorTraceExporter</c>).
/// </summary>
public static class PersistentStorageTracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds the persist-then-drain pipeline as a trace exporter: Phase 1 maps each span batch to
    /// the Application Insights wire format and writes it to durable on-disk storage instead of
    /// transmitting it (so a short-lived CLI process captures its telemetry before exiting), and
    /// Phase 2 — a background uploader started by the exporter the first time it runs — delivers
    /// telemetry persisted by this and previous invocations. The exporter is driven by a
    /// <see cref="SimpleActivityExportProcessor"/> so writes complete synchronously as spans end.
    /// </summary>
    /// <param name="builder">The tracer provider builder to register the exporter on.</param>
    /// <param name="configure">Configures the connection string, storage directory, and drain behavior.</param>
    /// <returns>
    /// The supplied <paramref name="builder"/> for chaining. When the configured connection string
    /// or storage directory is missing or invalid, the builder is returned unmodified.
    /// </returns>
    public static TracerProviderBuilder AddPersistentStorageExporter(this TracerProviderBuilder builder, Action<PersistentStorageTelemetryOptions> configure)
    {
        var options = new PersistentStorageTelemetryOptions();
        configure(options);

        var connectionString = AzureMonitorConnectionString.Parse(options.ConnectionString);
        if (connectionString is null || string.IsNullOrWhiteSpace(options.StorageDirectory))
        {
            return builder;
        }

        var storage = new FileSystemTelemetryBlobStorage(options.StorageDirectory);
        var exporter = new PersistentStorageTraceExporter(
            storage,
            connectionString.InstrumentationKey,
            connectionString.TrackUri,
            options.LeasePeriodMilliseconds,
            options.MaxBlobsPerDrain);
        return builder.AddProcessor(new SimpleActivityExportProcessor(exporter));
    }
}
