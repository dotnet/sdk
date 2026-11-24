// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

/// <summary>
/// Differential ("golden") tests that prove our hand-ported Breeze serializer
/// (<see cref="AzureMonitorTelemetrySerializer"/>) produces byte-for-byte the same telemetry
/// envelopes the real <c>Azure.Monitor.OpenTelemetry.Exporter</c> would have POSTed for the same
/// span/event input.
///
/// <para>
/// Every mapping and serialization type inside the Azure Monitor exporter is <c>internal</c>, so
/// the shipping <c>Microsoft.DotNet.Cli.Telemetry</c> library cannot reuse them and instead
/// reimplements the mapping. That reimplementation is only trustworthy if it stays in lock-step
/// with the exporter, which is exactly what these tests enforce: the same <see cref="Activity"/>
/// is run through both the real exporter (whose HTTP transport is intercepted to capture the wire
/// payload) and our serializer, and the two NDJSON envelope batches are asserted equal.
/// </para>
///
/// <para>
/// The comparison is intentionally exact. Two things make byte-for-byte equality achievable:
/// <list type="bullet">
///   <item><description><see cref="BreezeWriter.FormatTime(DateTime)"/> emits the UTC <c>Z</c>
///     timestamp form the exporter uses.</description></item>
///   <item><description>Our serializer is driven through the real
///     <see cref="TelemetryResourceContextFactory"/>, so the <c>ai.internal.sdkVersion</c> tag is
///     computed from the same loaded assemblies the exporter reads, yielding an identical string.
///     </description></item>
/// </list>
/// Our serializer runs <em>after</em> the exporter has flushed so both observe the identical final
/// <see cref="Activity"/> state (including any tags the exporter's own processors stamp onto it,
/// such as <c>_MS.ProcessedByMetricExtractors</c>) — the fairest possible input for the mapping.
/// JSON object member ordering is not significant; <see cref="JsonNode.DeepEquals"/> compares
/// objects by key regardless of order.
/// </para>
/// </summary>
[TestClass]
public class AzureMonitorExporterDifferentialTests
{
    private const string RoleName = "dotnet-cli-test";
    private const string RoleInstance = "role-instance-test";
    private const string AppVersion = "42.0.0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ServerSpanMatchesExporterRequestEnvelope()
    {
        AssertSerializersAgree(ActivityKind.Server, "dotnet build", activity =>
        {
            activity.SetTag("verb", "build");
            activity.SetTag("exitCode", "0");
        });
    }

    [TestMethod]
    public void InternalSpanMatchesExporterInProcDependencyEnvelope()
    {
        AssertSerializersAgree(ActivityKind.Internal, "resolve workloads", activity =>
        {
            activity.SetTag("workload.count", "3");
        });
    }

    [TestMethod]
    public void FailedSpanMatchesExporterEnvelope()
    {
        AssertSerializersAgree(ActivityKind.Server, "dotnet build", activity =>
        {
            activity.SetTag("verb", "build");
            activity.SetStatus(ActivityStatusCode.Error, "build failed");
        });
    }

    [TestMethod]
    public void SpanWithMessageEventMatchesExporterMessageEnvelope()
    {
        AssertSerializersAgree(ActivityKind.Server, "dotnet build", activity =>
        {
            activity.SetTag("verb", "build");
            activity.AddEvent(new ActivityEvent(
                "dotnet/cli/toplevelparser/command",
                tags: new ActivityTagsCollection
                {
                    ["verb"] = "build",
                    ["arguments"] = "3",
                }));
        });
    }

    [TestMethod]
    public void SpanWithExceptionEventMatchesExporterExceptionEnvelope()
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        AssertSerializersAgree(ActivityKind.Server, "dotnet build", activity =>
        {
            activity.SetTag("verb", "build");
            activity.AddException(exception);
        });
    }

    /// <summary>
    /// Emits a single <see cref="Activity"/> of the given shape through both the real Azure Monitor
    /// exporter (capturing the wire payload) and our serializer, then asserts the two envelope
    /// batches are equal.
    /// </summary>
    private void AssertSerializersAgree(ActivityKind kind, string displayName, Action<Activity> shape)
    {
        // A unique source per test keeps the per-test TracerProvider listeners from cross-capturing
        // when the test class runs in parallel with itself.
        var sourceName = "Microsoft.DotNet.Tests.Differential." + TestContext.TestName;
        using var source = new ActivitySource(sourceName);

        // The Azure Monitor exporter caches one transmitter (and thus one HTTP transport) per
        // connection string in a process-wide dictionary, so a fresh instrumentation key per test
        // guarantees our capturing transport is the one actually used.
        var instrumentationKey = Guid.NewGuid().ToString();
        var connectionString = $"InstrumentationKey={instrumentationKey};IngestionEndpoint=https://localhost/";

        // Build the resource once and share it: the exporter reads it for its context tags, and we
        // feed the same resource through the real factory so both compute identical role / version
        // / sdkVersion tags.
        var resourceBuilder = ResourceBuilder.CreateEmpty()
            .AddService(RoleName, serviceVersion: AppVersion, serviceInstanceId: RoleInstance);
        var resourceContext = TelemetryResourceContextFactory.FromResource(resourceBuilder.Build());

        var handler = new CapturingHandler();
        Activity activity;
        using (var provider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(sourceName)
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = connectionString;
                options.DisableOfflineStorage = true;
                options.Transport = new HttpClientTransport(new HttpClient(handler));
            })
            .Build())
        {
            activity = source.StartActivity(displayName, kind)
                ?? throw new InvalidOperationException("The tracer provider did not sample the activity.");
            shape(activity);
            activity.Stop();

            // ForceFlush drives the batch processor -> exporter -> our capturing transport, so the
            // exporter's wire payload is captured before the provider is disposed.
            provider.ForceFlush();
        }

        var ours = AzureMonitorTelemetrySerializer.SerializeBatch(
            new Batch<Activity>([activity], 1), resourceContext, instrumentationKey);
        ours.Should().NotBeNull("our serializer must emit telemetry for a recorded activity");

        activity.Dispose();

        AssertEnvelopeBatchesEqual(handler.CapturedPayloads, Encoding.UTF8.GetString(ours!));
    }

    // Trace-signal envelope discriminators. AddAzureMonitorTraceExporter also runs a standard-metric
    // extraction processor that POSTs a separate auto-collected "Metric" envelope; our library emits
    // only trace and log telemetry, never metrics, so those envelopes are out of scope for this
    // mapping comparison and are filtered out.
    private static readonly HashSet<string> TraceEnvelopeNames =
        new(StringComparer.Ordinal) { "Request", "RemoteDependency", "Message", "Exception" };

    private void AssertEnvelopeBatchesEqual(IReadOnlyList<string> exporterPayloads, string ourNdjson)
    {
        var exporterNdjson = string.Concat(exporterPayloads);
        TestContext.WriteLine("Exporter envelopes:\n" + exporterNdjson);
        TestContext.WriteLine("Our envelopes:\n" + ourNdjson);

        var expected = ParseEnvelopes(exporterNdjson)
            .Where(e => TraceEnvelopeNames.Contains(e["name"]?.GetValue<string>() ?? string.Empty))
            .ToList();
        var actual = ParseEnvelopes(ourNdjson);

        expected.Should().NotBeEmpty("the exporter must have produced at least one trace envelope to compare against");

        actual.Count.Should().Be(
            expected.Count,
            "we must emit the same number of trace envelopes the exporter does");

        var remaining = new List<JsonNode>(actual);
        foreach (var expectedEnvelope in expected)
        {
            var index = remaining.FindIndex(candidate => JsonNode.DeepEquals(expectedEnvelope, candidate));
            index.Should().BeGreaterThanOrEqualTo(
                0,
                $"the exporter emitted an envelope with no byte-equal match in ours:\n{expectedEnvelope.ToJsonString()}\n\nremaining ours:\n{string.Join("\n", remaining.Select(r => r.ToJsonString()))}");
            remaining.RemoveAt(index);
        }

        remaining.Should().BeEmpty("we must not emit any envelope the exporter did not");
    }

    private static List<JsonNode> ParseEnvelopes(string ndjson) =>
        ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!)
            .ToList();

    /// <summary>
    /// Intercepts the exporter's outbound HTTP requests, decompresses each gzip Breeze body, and
    /// returns a synthetic <c>200 OK</c> track response so the exporter treats the batch as
    /// accepted and never touches the network or offline storage. The exporter may POST more than
    /// once per flush (e.g. traces plus auto-collected metrics), so every body is accumulated.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<string> _payloads = [];

        public IReadOnlyList<string> CapturedPayloads
        {
            get { lock (_gate) { return _payloads.ToList(); } }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var raw = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            var isGzip = request.Content.Headers.ContentEncoding.Contains("gzip");
            var bytes = isGzip ? Gunzip(raw) : raw;
            var text = Encoding.UTF8.GetString(bytes);
            lock (_gate)
            {
                _payloads.Add(text);
            }

            var lineCount = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"itemsReceived\":{lineCount},\"itemsAccepted\":{lineCount},\"errors\":[]}}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        private static byte[] Gunzip(byte[] compressed)
        {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
