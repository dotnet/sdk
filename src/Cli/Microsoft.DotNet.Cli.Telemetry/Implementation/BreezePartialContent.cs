// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text.Json;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Helpers for handling an HTTP 206 (Partial Content) response from the Breeze
/// <c>/v2.1/track</c> endpoint. When ingestion partially succeeds, the response body is a
/// <see cref="TrackResponse"/> listing per-item errors by their (zero-based) index within the
/// NDJSON payload. This class re-slices the original payload down to just the envelopes that
/// failed with a <em>retriable</em> status so they can be persisted and re-sent later.
/// </summary>
internal static class BreezePartialContent
{
    // Per-item status codes the Azure Monitor exporter treats as retriable
    // (ProcessPartialSuccessWithCounting): request timeout, throttling, and server errors.
    private static readonly HashSet<int> s_retriableStatusCodes = [408, 429, 439, 500, 503];

    /// <summary>Deserializes a Breeze partial-success response body. Returns <see langword="null"/> on malformed input.</summary>
    public static TrackResponse? ParseResponse(ReadOnlySpan<byte> responseBody)
    {
        if (responseBody.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(responseBody, TelemetryJsonContext.Default.TrackResponse);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Given the original NDJSON <paramref name="payload"/> and the parsed partial-success
    /// <paramref name="response"/>, returns a new NDJSON payload containing only the envelopes
    /// that were rejected with a retriable status code, or <see langword="null"/> when nothing
    /// is worth retrying.
    /// </summary>
    public static byte[]? GetRetriablePayload(byte[] payload, TrackResponse? response)
    {
        if (response?.Errors is not { Count: > 0 } errors)
        {
            return null;
        }

        var retriableIndices = new HashSet<int>();
        foreach (var error in errors)
        {
            if (s_retriableStatusCodes.Contains(error.StatusCode))
            {
                retriableIndices.Add(error.Index);
            }
        }

        if (retriableIndices.Count == 0)
        {
            return null;
        }

        // Each envelope is a single JSON line terminated by '\n', so the line index maps
        // directly to the Breeze error index.
        using var stream = new MemoryStream();
        var lineIndex = 0;
        var lineStart = 0;
        for (var i = 0; i < payload.Length; i++)
        {
            if (payload[i] != (byte)'\n')
            {
                continue;
            }

            if (retriableIndices.Contains(lineIndex))
            {
                // Re-emit the envelope bytes plus the terminating newline.
                stream.Write(payload, lineStart, i - lineStart + 1);
            }

            lineIndex++;
            lineStart = i + 1;
        }

        return stream.Length == 0 ? null : stream.ToArray();
    }
}
