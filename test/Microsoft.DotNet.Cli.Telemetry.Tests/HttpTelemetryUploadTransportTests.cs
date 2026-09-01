// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class HttpTelemetryUploadTransportTests
{
    private static readonly Uri TrackUri = new("https://example.test/v2.1/track");

    [TestMethod]
    public async Task ItGzipsTheRequestBodyAndReportsAcceptedOn200()
    {
        var payload = Encoding.UTF8.GetBytes("{\"env\":0}\n");
        var handler = new StubHandler(HttpStatusCode.OK);
        var transport = new HttpTelemetryUploadTransport(TrackUri, handler);

        var result = await transport.TryUploadAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(TelemetryUploadOutcome.Accepted);
        handler.RequestContentEncoding.Should().Be("gzip");
        handler.DecompressedRequestBody.Should().Equal(payload);
    }

    [TestMethod]
    public async Task ItReportsPartiallyAcceptedAndReslicesRetriableEnvelopesOn206()
    {
        var payload = Encoding.UTF8.GetBytes("{\"env\":0}\n{\"env\":1}\n");
        var body = "{\"itemsReceived\":2,\"itemsAccepted\":1,\"errors\":[{\"index\":1,\"statusCode\":500,\"message\":\"retry\"}]}";
        var handler = new StubHandler(HttpStatusCode.PartialContent, body);
        var transport = new HttpTelemetryUploadTransport(TrackUri, handler);

        var result = await transport.TryUploadAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(TelemetryUploadOutcome.PartiallyAccepted);
        Encoding.UTF8.GetString(result.RetryPayload!).Should().Be("{\"env\":1}\n");
    }

    [TestMethod]
    public async Task ItReportsAcceptedWhen206HasNoRetriableErrors()
    {
        var payload = Encoding.UTF8.GetBytes("{\"env\":0}\n");
        var body = "{\"itemsReceived\":1,\"itemsAccepted\":0,\"errors\":[{\"index\":0,\"statusCode\":400,\"message\":\"bad\"}]}";
        var handler = new StubHandler(HttpStatusCode.PartialContent, body);
        var transport = new HttpTelemetryUploadTransport(TrackUri, handler);

        var result = await transport.TryUploadAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(TelemetryUploadOutcome.Accepted);
        result.RetryPayload.Should().BeNull();
    }

    [TestMethod]
    public async Task ItReportsRejectedOnServerError()
    {
        var payload = Encoding.UTF8.GetBytes("{\"env\":0}\n");
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var transport = new HttpTelemetryUploadTransport(TrackUri, handler);

        var result = await transport.TryUploadAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(TelemetryUploadOutcome.Rejected);
    }

    private sealed class StubHandler(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        public string? RequestContentEncoding { get; private set; }
        public byte[]? DecompressedRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestContentEncoding = string.Join(",", request.Content!.Headers.ContentEncoding);

            var raw = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            using var input = new MemoryStream(raw);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            await gzip.CopyToAsync(output, cancellationToken);
            DecompressedRequestBody = output.ToArray();

            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = new StringContent(body);
            }
            return response;
        }
    }
}
