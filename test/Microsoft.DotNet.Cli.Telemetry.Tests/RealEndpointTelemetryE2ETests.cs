// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

/// <summary>
/// Opt-in end-to-end tests that POST telemetry to a <em>real</em> Application Insights
/// ingestion endpoint and assert the accepted/rejected breakdown returned by the Breeze
/// <c>/v2.1/track</c> service.
///
/// <para>
/// These tests are <b>skipped</b> (reported <see cref="UnitTestOutcome.Inconclusive"/>)
/// unless the <c>DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING</c> environment variable holds a
/// full Application Insights connection string. They therefore never run — or hit the network —
/// during a normal CI test pass. To run them locally:
/// </para>
/// <code>
/// $env:DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING = "InstrumentationKey=&lt;guid&gt;;IngestionEndpoint=https://&lt;region&gt;.in.applicationinsights.azure.com/"
/// dotnet test test/Microsoft.DotNet.Cli.Telemetry.Tests --filter "FullyQualifiedName~RealEndpointTelemetryE2ETests"
/// </code>
///
/// <para>
/// The Breeze endpoint returns the <c>itemsReceived</c> / <c>itemsAccepted</c> / <c>errors</c>
/// breakdown <em>synchronously</em> in the POST response, so these tests measure the effective
/// drop rate immediately. (The ~1 hour ingestion delay only applies to querying the data back
/// out through the Analytics API, which is a separate manual step.) Their purpose is twofold:
/// prove the shipping upload path works against the live service, and act as a canary that
/// fails if the App Insights ARM ingestion layer makes a breaking change to the wire contract
/// this library depends on.
/// </para>
/// </summary>
[TestClass]
public class RealEndpointTelemetryE2ETests
{
    private const string ConnectionStringEnvVar = "DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING";
    private const string RunIdEnvVar = "DOTNET_CLI_TELEMETRY_E2E_RUN_ID";
    private const string SourceName = "Microsoft.DotNet.Tests.RealEndpointTelemetry";

    // A stable marker plus a per-run id uniquely identify one test run. The run id can be pinned
    // via DOTNET_CLI_TELEMETRY_E2E_RUN_ID so a harness script can choose the id up front, run the
    // tests, then query for exactly that id once ingestion completes (~1 hour).
    //
    // The dotnet CLI does not query the raw Application Insights tables — its telemetry flows
    // through a processing pipeline into a downstream table searched by the Message / EventName
    // field. That field is populated from the MessageData.message envelope, which in turn comes
    // from the activity *event name*. So the run token is embedded in the event name (below), not
    // just in customDimensions, to make emitted rows findable in the destination table.
    private const string RunMarker = "cli-telemetry-real-endpoint-e2e";
    private static readonly string RunId =
        Environment.GetEnvironmentVariable(RunIdEnvVar) is { Length: > 0 } pinned
            ? pinned
            : Guid.NewGuid().ToString("N");

    // The searchable Message / EventName value stamped onto the message envelope for this run.
    private static readonly string MessageToken = $"{RunMarker}/{RunId}";

    private static readonly ActivitySource Source = new(SourceName);

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Sends a batch of valid, CLI-representative telemetry through the shipping
    /// <see cref="HttpTelemetryUploadTransport"/> and asserts the live endpoint accepts it
    /// (HTTP 200 → <see cref="TelemetryUploadOutcome.Accepted"/>). This exercises the exact
    /// production code path — gzip content, request headers, and 200/206 handling.
    /// </summary>
    [TestMethod]
    public async Task ItUploadsValidTelemetryThroughTheProductionTransport()
    {
        var connection = RequireConnectionString();
        var payload = BuildValidPayload(connection.InstrumentationKey);

        var transport = new HttpTelemetryUploadTransport(connection.TrackUri);
        var result = await transport.TryUploadAsync(payload, TestTimeoutToken());

        result.Outcome.Should().Be(
            TelemetryUploadOutcome.Accepted,
            "the live ingestion endpoint should accept well-formed CLI telemetry through the shipping transport");
        result.RetryPayload.Should().BeNull("a fully-accepted upload leaves nothing to retry");
    }

    /// <summary>
    /// POSTs a batch of valid envelopes directly and asserts the endpoint reports every item
    /// received and accepted with no errors. This is the drop-rate measurement: it makes the
    /// <c>itemsReceived</c>/<c>itemsAccepted</c> counts explicit and fails if the ingestion
    /// layer starts silently rejecting our current envelope shapes.
    /// </summary>
    [TestMethod]
    public async Task ItAcceptsEveryValidEnvelopeAtTheIngestionEndpoint()
    {
        var connection = RequireConnectionString();
        var (payload, envelopeCount) = BuildValidPayloadWithCount(connection.InstrumentationKey);

        var (status, response) = await PostAsync(connection.TrackUri, payload, TestTimeoutToken());

        status.Should().Be(HttpStatusCode.OK, "a batch of only-valid envelopes should be fully accepted");
        response.Should().NotBeNull("the Breeze endpoint returns an itemsReceived/itemsAccepted body");
        response!.ItemsReceived.Should().Be(envelopeCount, "every envelope we sent should be received");
        response.ItemsAccepted.Should().Be(envelopeCount, "every valid envelope should be accepted (zero dropped)");
        (response.Errors ?? []).Should().BeEmpty("a fully-accepted batch reports no per-item errors");
    }

    /// <summary>
    /// POSTs valid envelopes mixed with one deliberately-malformed envelope and asserts the
    /// endpoint accepts exactly the valid ones and reports the malformed one as a per-item
    /// error (HTTP 206). This is the schema-drift canary: it validates our HTTP 206 partial-
    /// success handling against the live contract and fails if ingestion changes how it reports
    /// rejections.
    /// </summary>
    [TestMethod]
    public async Task ItReportsPerItemErrorsForMalformedEnvelopes()
    {
        var connection = RequireConnectionString();
        var (validPayload, validCount) = BuildValidPayloadWithCount(connection.InstrumentationKey);

        // Append one clearly-invalid envelope: a syntactically-valid JSON line with an invalid
        // timestamp, which the Breeze validator rejects on a per-item basis.
        var malformedLine = $"{{\"ver\":1,\"name\":\"Microsoft.ApplicationInsights.Message\",\"time\":\"not-a-timestamp\",\"iKey\":\"{connection.InstrumentationKey}\",\"tags\":{{}},\"data\":{{\"baseType\":\"MessageData\",\"baseData\":{{\"ver\":2,\"message\":\"{RunMarker}\"}}}}}}\n";
        var payload = Concat(validPayload, Encoding.UTF8.GetBytes(malformedLine));
        var totalCount = validCount + 1;

        var (status, response) = await PostAsync(connection.TrackUri, payload, TestTimeoutToken());

        status.Should().Be(HttpStatusCode.PartialContent, "a batch mixing valid and invalid envelopes should partially succeed");
        response.Should().NotBeNull();
        response!.ItemsReceived.Should().Be(totalCount, "the endpoint should acknowledge every envelope it received");
        response.ItemsAccepted.Should().Be(validCount, "only the malformed envelope should be dropped");
        response.Errors.Should().NotBeNullOrEmpty("the malformed envelope should surface as a per-item error");
        response.Errors!.Should().Contain(e => e.Index == totalCount - 1, "the reported error index should point at the malformed (last) envelope");
    }

    /// <summary>
    /// Offline regression guard (runs without a connection string): asserts the per-run token is
    /// serialized as the message envelope's <c>message</c> field. That field becomes the
    /// downstream table's searchable <c>Message</c> / <c>EventName</c>, so this protects the
    /// "findable after ingestion" contract even when the networked tests are skipped.
    /// </summary>
    [TestMethod]
    public void EmittedMessageEnvelopeCarriesTheRunTokenForDownstreamSearch()
    {
        var (payload, _) = BuildValidPayloadWithCount("offline-test-ikey");
        var text = Encoding.UTF8.GetString(payload);

        text.Should().Contain(
            $"\"message\":\"{MessageToken}\"",
            "the run token must land in MessageData.message so the downstream table's Message/EventName search can find the row");
    }

    /// <summary>
    /// Returns the parsed connection string, or aborts the test as inconclusive when the opt-in
    /// environment variable is not configured.
    /// </summary>
    private AzureMonitorConnectionString RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        var parsed = AzureMonitorConnectionString.Parse(connectionString);
        if (parsed is null)
        {
            Assert.Inconclusive(
                $"Set {ConnectionStringEnvVar} to an Application Insights connection string to run the real-endpoint telemetry tests.");
        }

        TestContext.WriteLine($"Real-endpoint telemetry E2E run: marker='{RunMarker}', runId='{RunId}', endpoint='{parsed!.IngestionEndpoint}'.");
        TestContext.WriteLine(
            $"After ingestion completes (~1 hour), confirm delivery by searching the telemetry destination table for a row whose Message / EventName equals: {MessageToken}");
        return parsed;
    }

    /// <summary>
    /// Builds a batch of valid, CLI-representative telemetry — an internal span (dependency), a
    /// server span (request), and a command-finish activity event (message) — serialized to the
    /// Breeze NDJSON wire format by the shipping <see cref="AzureMonitorTelemetrySerializer"/>.
    /// </summary>
    private static byte[] BuildValidPayload(string instrumentationKey) => BuildValidPayloadWithCount(instrumentationKey).Payload;

    private static (byte[] Payload, int EnvelopeCount) BuildValidPayloadWithCount(string instrumentationKey)
    {
        var resource = new TelemetryResourceContext(
            RoleName: "dotnet-cli-e2e",
            RoleInstance: Environment.MachineName,
            ApplicationVersion: "42.0.0-e2e",
            SdkVersion: "dotnet1.0:otel1.0:ext1.0");

        using var listener = CreateListener();

        // Internal span → RemoteDependencyData, carrying an event whose name embeds the run token
        // → MessageData.message → the downstream table's searchable Message / EventName field.
        using var internalSpan = Source.StartActivity("dotnet build", ActivityKind.Internal)!;
        StampRunTags(internalSpan);
        var eventTags = new ActivityTagsCollection
        {
            { "e2e.marker", RunMarker },
            { "e2e.run.id", RunId },
            { "exitCode", "0" },
        };
        internalSpan.AddEvent(new ActivityEvent(MessageToken, tags: eventTags));
        internalSpan.Stop();

        // Server span → RequestData.
        using var serverSpan = Source.StartActivity("dotnet build", ActivityKind.Server)!;
        StampRunTags(serverSpan);
        serverSpan.Stop();

        var batch = new Batch<Activity>([internalSpan, serverSpan], 2);
        var payload = AzureMonitorTelemetrySerializer.SerializeBatch(in batch, resource, instrumentationKey);
        payload.Should().NotBeNull("the serializer should produce telemetry for a non-empty batch");

        var envelopeCount = CountLines(payload!);
        return (payload!, envelopeCount);
    }

    private static void StampRunTags(Activity activity)
    {
        activity.SetTag("e2e.marker", RunMarker);
        activity.SetTag("e2e.run.id", RunId);
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>
    /// gzip-compresses and POSTs <paramref name="ndjson"/> to the Breeze <c>/v2.1/track</c>
    /// endpoint (mirroring the production transport's request shape) and returns the response
    /// status together with the parsed accepted/rejected breakdown, which the endpoint returns
    /// on both 200 and 206.
    /// </summary>
    private static async Task<(HttpStatusCode Status, TrackResponse? Response)> PostAsync(Uri trackUri, byte[] ndjson, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        var compressed = Gzip(ndjson);
        using var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");

        using var request = new HttpRequestMessage(HttpMethod.Post, trackUri) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (response.StatusCode, BreezePartialContent.ParseResponse(body));
    }

    private static byte[] Gzip(byte[] payload)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
        }
        return buffer.ToArray();
    }

    private static byte[] Concat(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static int CountLines(byte[] ndjson)
    {
        var count = 0;
        foreach (var b in ndjson)
        {
            if (b == (byte)'\n')
            {
                count++;
            }
        }
        return count;
    }

    // Bound each live request so a hung endpoint fails the test rather than the run.
    private static CancellationToken TestTimeoutToken() => new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;
}
