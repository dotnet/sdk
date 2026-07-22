// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Uploads telemetry payloads to the Azure Monitor Breeze <c>/v2.1/track</c> endpoint with a
/// plain <see cref="HttpClient"/>. The request body is the newline-delimited JSON produced by
/// <see cref="AzureMonitorTelemetrySerializer"/>, gzip-compressed on the wire (matching the
/// Azure Monitor exporter). Persisted blobs stay uncompressed so a partially-accepted payload
/// can be re-sliced by envelope index.
/// </summary>
internal sealed class HttpTelemetryUploadTransport : ITelemetryUploadTransport
{
    // A single shared client for the process. Timeout is set to InfiniteTimeSpan because
    // cancellation is controlled by the CancellationToken passed to each upload call —
    // either from the provider's Shutdown budget or from the drain loop's per-blob budget.
    private static readonly HttpClient s_httpClient = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    private readonly Uri _trackUri;
    private readonly HttpClient _client;

    public HttpTelemetryUploadTransport(Uri trackUri)
        : this(trackUri, s_httpClient)
    {
    }

    // Test hook: uploads through a caller-supplied handler (its own HttpClient) so gzip and
    // 206 handling can be exercised without a real network call.
    internal HttpTelemetryUploadTransport(Uri trackUri, HttpMessageHandler handler)
        : this(trackUri, new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan })
    {
    }

    private HttpTelemetryUploadTransport(Uri trackUri, HttpClient client)
    {
        _trackUri = trackUri;
        _client = client;
    }

    public async Task<TelemetryUploadResult> TryUploadAsync(byte[] payload, CancellationToken cancellationToken)
    {
        using var content = new GzipJsonContent(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, _trackUri) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return TelemetryUploadResult.Accepted;
        }

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var trackResponse = BreezePartialContent.ParseResponse(body);
            var retriable = BreezePartialContent.GetRetriablePayload(payload, trackResponse);

            // No retriable remainder means every rejected item was permanently rejected, so the
            // blob is done. Otherwise re-persist just the retriable envelopes.
            return retriable is null
                ? TelemetryUploadResult.Accepted
                : TelemetryUploadResult.PartiallyAccepted(retriable, GetRetryAfter(response));
        }

        // Throttling, server errors, etc.: retain the blob and retry it later.
        return GetRetryAfter(response) is { } retryAfter
            ? TelemetryUploadResult.RejectedAfter(retryAfter)
            : TelemetryUploadResult.Rejected;
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        return null;
    }

    /// <summary>
    /// An <see cref="HttpContent"/> that gzip-compresses its source payload directly into the
    /// request stream, so the compressed bytes are never materialized in a separate buffer. The
    /// compressed length is not known up front, so the request uses chunked transfer-encoding.
    /// </summary>
    private sealed class GzipJsonContent : HttpContent
    {
        private readonly ReadOnlyMemory<byte> _payload;

        public GzipJsonContent(ReadOnlyMemory<byte> payload)
        {
            _payload = payload;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Headers.ContentEncoding.Add("gzip");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            using var gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
            await gzip.WriteAsync(_payload).ConfigureAwait(false);
        }

        // The compressed size isn't known without compressing first; stream with chunked encoding.
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
