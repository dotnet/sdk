// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry;

/// <summary>
/// Configuration for the persist-then-drain telemetry pipeline, following the Options pattern
/// used by OpenTelemetry exporters. The same options shape configures both phases the persist
/// exporter owns: persisting spans to durable storage, and the background uploader it starts to
/// drain that storage. Registered via
/// <c>TracerProviderBuilder.AddPersistentStorageExporter</c>.
/// </summary>
public sealed class PersistentStorageTelemetryOptions
{
    /// <summary>
    /// The Application Insights connection string. Its instrumentation key is stamped into every
    /// persisted telemetry envelope and its ingestion endpoint is the target of the upload. When
    /// null, empty, or unparseable, the pipeline is disabled (registration and drain both no-op).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The directory where telemetry blobs are persisted (Phase 1) and later drained from
    /// (Phase 2). Required; when null or empty the pipeline is disabled.
    /// </summary>
    public string? StorageDirectory { get; set; }

    /// <summary>
    /// How long, in milliseconds, a blob is exclusively leased to the draining process while it
    /// is being uploaded. If the process exits mid-upload the lease expires and a later invocation
    /// retries the blob.
    /// </summary>
    public int LeasePeriodMilliseconds { get; set; } = 30_000;

    /// <summary>
    /// The maximum number of blobs uploaded per drain pass, keeping the background work bounded
    /// even when a large backlog has accumulated. Remaining blobs are drained by later invocations.
    /// </summary>
    public int MaxBlobsPerDrain { get; set; } = 200;
}
