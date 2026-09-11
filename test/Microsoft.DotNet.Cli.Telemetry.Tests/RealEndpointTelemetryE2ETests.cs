// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class RealEndpointTelemetryE2ETests
{
    private const string ConnectionStringEnvVar = "DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING";
    private const string DefaultConnectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254;IngestionEndpoint=https://southcentralus-0.in.applicationinsights.azure.com/;LiveEndpoint=https://southcentralus.livediagnostics.monitor.azure.com/;ApplicationId=c5108c2c-b0c5-43c6-a703-424eae223a75";
    private static readonly string s_runId = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_E2E_RUN_ID")
        is { Length: > 0 } pinned ? pinned : Guid.NewGuid().ToString("N");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ItUploadsValidTelemetryThroughTheProductionTransport()
    {
        string connection = RequireConnectionString();
        using var settings = new ExporterSettingsScope(ci: true);
        using var scope = new AzureExporterTestScope();
        using var handler = new LiveRecordingHandler();
        using var provider = scope.CreateProvider(handler, connection);
        EmitValidBatch(scope);
        provider.Shutdown(5_000).Should().BeTrue();
        handler.Responses.Should().NotBeEmpty();
        handler.Responses.Should().OnlyContain(response => response.Status == HttpStatusCode.OK);
        scope.StoredFiles().Should().BeEmpty("accepted telemetry should not need a retry");
    }

    [TestMethod]
    public void ItAcceptsEveryValidEnvelopeAtTheIngestionEndpoint()
    {
        string connection = RequireConnectionString();
        using var settings = new ExporterSettingsScope(ci: true);
        using var scope = new AzureExporterTestScope();
        using var handler = new LiveRecordingHandler();
        using var provider = scope.CreateProvider(handler, connection);
        EmitValidBatch(scope);
        provider.Shutdown(5_000).Should().BeTrue();
        handler.Responses.Should().NotBeEmpty();
        foreach (var response in handler.Responses)
        {
            response.Status.Should().Be(HttpStatusCode.OK);
            AssertAcceptance(response, response.SentCount);
            response.Body.GetProperty("errors").GetArrayLength().Should().Be(0);
        }
        handler.Responses.Sum(response => response.SentCount).Should().BeGreaterThanOrEqualTo(3);
    }

    [TestMethod]
    public void ItReportsPerItemErrorsForMalformedEnvelopes()
    {
        string connection = RequireConnectionString();
        using var settings = new ExporterSettingsScope(ci: true);
        using var scope = new AzureExporterTestScope();
        using var handler = new LiveRecordingHandler { AppendMalformedEnvelope = true };
        using var provider = scope.CreateProvider(handler, connection);
        EmitValidBatch(scope);
        provider.Shutdown(5_000).Should().BeTrue();
        handler.Responses.Should().NotBeEmpty();
        foreach (var response in handler.Responses)
        {
            response.Status.Should().Be(HttpStatusCode.PartialContent);
            AssertAcceptance(response, response.SentCount - 1);
            response.Body.GetProperty("errors").EnumerateArray().Should()
                .Contain(error => error.GetProperty("index").GetInt32() == response.SentCount - 1);
        }
    }

    [TestMethod]
    public void EmittedTelemetryUsesARealCliEventShapeAndStampsTheRunIdAsSessionId()
    {
        using var settings = new ExporterSettingsScope(ci: true);
        using var scope = new AzureExporterTestScope();
        using var handler = new RecordingHandler();
        using var provider = scope.CreateProvider(handler);
        EmitValidBatch(scope);
        provider.Shutdown(5_000).Should().BeTrue();
        handler.Envelopes.Select(envelope => envelope.GetProperty("data").GetProperty("baseType").GetString())
            .Where(baseType => baseType != "MetricData")
            .Should().BeEquivalentTo(["MessageData", "RemoteDependencyData", "RequestData"]);
        JsonElement message = handler.Envelopes.Single(envelope => envelope.GetProperty("data").GetProperty("baseType").GetString() == "MessageData")
            .GetProperty("data").GetProperty("baseData");
        message.GetProperty("message").GetString().Should().Be(AzureExporterTestScope.CliEventName);
        message.GetProperty("properties").GetProperty("SessionId").GetString().Should().Be(s_runId);
    }

    private string RequireConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }
        TestContext.WriteLine($"Real-endpoint event='{AzureExporterTestScope.CliEventName}', SessionId='{s_runId}'.");
        TestContext.WriteLine("After ingestion, query the CLI destination table for this SessionId to verify downstream delivery.");
        return connectionString!;
    }

    private static void EmitValidBatch(AzureExporterTestScope scope)
    {
        using var dependency = scope.Emit(s_runId);
        using var request = scope.Emit(s_runId, ActivityKind.Server, includeEvent: false);
    }

    private static void AssertAcceptance(LiveResponse response, int accepted)
    {
        response.Body.GetProperty("itemsReceived").GetInt32().Should().Be(response.SentCount);
        response.Body.GetProperty("itemsAccepted").GetInt32().Should().Be(accepted);
    }

    private sealed record LiveResponse(HttpStatusCode Status, string RawBody, int SentCount)
    {
        internal JsonElement Body => JsonSerializer.Deserialize<JsonElement>(RawBody);
    }

    private sealed class LiveRecordingHandler : DelegatingHandler
    {
        internal bool AppendMalformedEnvelope { get; init; }
        internal List<LiveResponse> Responses { get; } = [];

        internal LiveRecordingHandler() : base(new HttpClientHandler()) { }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            SendAsync(request, cancellationToken).GetAwaiter().GetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string payload = await RecordingHandler.ReadPayloadAsync(request.Content!, cancellationToken);
            int sentCount = RecordingHandler.Parse(payload).Length;
            if (AppendMalformedEnvelope)
            {
                JsonNode malformed = JsonNode.Parse(payload.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0])!;
                malformed["time"] = "not-a-timestamp";
                request.Content!.Dispose();
                request.Content = new StringContent(payload.TrimEnd('\n') + "\n" + malformed.ToJsonString() + "\n", Encoding.UTF8, "application/json");
                sentCount++;
            }
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            Responses.Add(new LiveResponse(response.StatusCode, body, sentCount));
            return response;
        }
    }
}
