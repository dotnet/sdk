// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Transport used to deliver a serialized telemetry payload to the Azure Monitor ingestion
/// endpoint. Abstracted so the drain loop can be unit-tested without real network calls.
/// </summary>
internal interface ITelemetryUploadTransport
{
    /// <summary>
    /// Uploads <paramref name="payload"/> (NDJSON Breeze envelopes) and reports how the server
    /// disposed of it: fully accepted, partially accepted (with a retriable remainder to
    /// persist), or rejected (retain the blob for a later retry).
    /// </summary>
    Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken);
}
