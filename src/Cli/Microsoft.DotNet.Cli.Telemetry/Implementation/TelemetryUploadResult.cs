// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>The disposition of a telemetry upload attempt.</summary>
internal enum TelemetryUploadOutcome
{
    /// <summary>The server accepted the entire payload (HTTP 200, or an HTTP 206 whose
    /// rejected items were all non-retriable). The blob can be deleted.</summary>
    Accepted,

    /// <summary>The server accepted the payload but rejected some envelopes with a retriable
    /// status (HTTP 206). The accepted portion is done; the retriable remainder is carried in
    /// <see cref="TelemetryUploadResult.RetryPayload"/> and should be persisted for a later retry.</summary>
    PartiallyAccepted,

    /// <summary>The server did not accept the payload (throttling, server error, etc.). The
    /// blob should be retained and retried later.</summary>
    Rejected,
}

/// <summary>
/// The result of a single <see cref="ITelemetryUploadTransport.TryUploadAsync"/> attempt.
/// For <see cref="TelemetryUploadOutcome.PartiallyAccepted"/>, <see cref="RetryPayload"/>
/// holds a re-sliced NDJSON payload containing only the envelopes that failed with a
/// retriable status code.
/// </summary>
internal readonly struct TelemetryUploadResult
{
    private TelemetryUploadResult(TelemetryUploadOutcome outcome, byte[]? retryPayload)
    {
        Outcome = outcome;
        RetryPayload = retryPayload;
    }

    public TelemetryUploadOutcome Outcome { get; }

    /// <summary>
    /// The retriable remainder to persist, present only when <see cref="Outcome"/> is
    /// <see cref="TelemetryUploadOutcome.PartiallyAccepted"/>.
    /// </summary>
    public byte[]? RetryPayload { get; }

    public static TelemetryUploadResult Accepted { get; } = new(TelemetryUploadOutcome.Accepted, null);

    public static TelemetryUploadResult Rejected { get; } = new(TelemetryUploadOutcome.Rejected, null);

    public static TelemetryUploadResult PartiallyAccepted(byte[] retryPayload)
        => new(TelemetryUploadOutcome.PartiallyAccepted, retryPayload);
}
