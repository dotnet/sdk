// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class AzureMonitorLogSerializerTests
{
    private const string InstrumentationKey = "test-ikey";
    private const string SourceName = "Microsoft.DotNet.Tests.LogSerializer";
    private static readonly TelemetryResourceContext Resource =
        new("dotnet-cli", "role-instance", "42.0.0", "dotnet1.0:otel1.0:ext1.0");

    private static readonly ActivitySource Source = new(SourceName);

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
    /// Drives log records through the real OpenTelemetry logging pipeline (so attributes,
    /// formatted message, scopes, category, exception, severity, and trace context are populated
    /// exactly as at runtime) and serializes each exported batch, returning the parsed envelopes.
    /// </summary>
    private static List<JsonNode> Capture(Action<ILogger> emit, bool includeScopes = false)
    {
        var envelopes = new List<JsonNode>();
        var exporter = new CapturingExporter(envelopes);

        using (var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.IncludeScopes = includeScopes;
                options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        }))
        {
            var logger = loggerFactory.CreateLogger("Microsoft.DotNet.Tests.Category");
            emit(logger);
        }

        return envelopes;
    }

    private sealed class CapturingExporter(List<JsonNode> sink) : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            var bytes = AzureMonitorLogSerializer.SerializeBatch(in batch, Resource, InstrumentationKey);
            if (bytes is not null)
            {
                foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    sink.Add(JsonNode.Parse(line)!);
                }
            }

            return ExportResult.Success;
        }
    }

    [TestMethod]
    public void InformationLogBecomesMessageEnvelope()
    {
        var items = Capture(logger => logger.LogInformation("hello {name}", "world"));

        items.Should().HaveCount(1);
        var envelope = items[0];

        envelope["name"]!.GetValue<string>().Should().Be("Message");
        envelope["iKey"]!.GetValue<string>().Should().Be(InstrumentationKey);
        envelope["tags"]!["ai.cloud.role"]!.GetValue<string>().Should().Be("dotnet-cli");
        envelope["tags"]!["ai.internal.sdkVersion"]!.GetValue<string>().Should().Be("dotnet1.0:otel1.0:ext1.0");

        envelope["data"]!["baseType"]!.GetValue<string>().Should().Be("MessageData");
        var baseData = envelope["data"]!["baseData"]!;
        baseData["message"]!.GetValue<string>().Should().Be("hello world");
        baseData["severityLevel"]!.GetValue<string>().Should().Be("Information");
        baseData["ver"]!.GetValue<int>().Should().Be(2);

        var properties = baseData["properties"]!.AsObject();
        properties["name"]!.GetValue<string>().Should().Be("world");
        // The category name and the original message template are stamped as properties.
        properties["CategoryName"]!.GetValue<string>().Should().Be("Microsoft.DotNet.Tests.Category");
    }

    [TestMethod]
    public void ExceptionLogBecomesExceptionEnvelope()
    {
        InvalidOperationException thrown;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException e)
        {
            thrown = e;
        }

        var items = Capture(logger => logger.LogError(thrown, "operation failed"));

        items.Should().HaveCount(1);
        var envelope = items[0];

        envelope["name"]!.GetValue<string>().Should().Be("Exception");
        envelope["data"]!["baseType"]!.GetValue<string>().Should().Be("ExceptionData");

        var baseData = envelope["data"]!["baseData"]!;
        baseData["severityLevel"]!.GetValue<string>().Should().Be("Error");
        var exception = baseData["exceptions"]!.AsArray()[0]!;
        exception["typeName"]!.GetValue<string>().Should().Be("System.InvalidOperationException");
        exception["message"]!.GetValue<string>().Should().Be("boom");
        exception["hasFullStack"]!.GetValue<bool>().Should().BeTrue();
    }

    [TestMethod]
    public void LogLevelsMapToSeverityLevels()
    {
        Severity(LogLevel.Trace).Should().Be("Verbose");
        Severity(LogLevel.Debug).Should().Be("Verbose");
        Severity(LogLevel.Information).Should().Be("Information");
        Severity(LogLevel.Warning).Should().Be("Warning");
        Severity(LogLevel.Error).Should().Be("Error");
        Severity(LogLevel.Critical).Should().Be("Critical");

        static string Severity(LogLevel level)
        {
            var items = Capture(logger => logger.Log(level, "msg"));
            return items[0]["data"]!["baseData"]!["severityLevel"]!.GetValue<string>();
        }
    }

    [TestMethod]
    public void LogEmittedInsideActivityCarriesTraceContext()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("command", ActivityKind.Internal)!;

        var items = Capture(logger => logger.LogInformation("within span"));

        var tags = items[0]["tags"]!;
        tags["ai.operation.id"]!.GetValue<string>().Should().Be(activity.TraceId.ToHexString());
        tags["ai.operation.parentId"]!.GetValue<string>().Should().Be(activity.SpanId.ToHexString());
    }

    [TestMethod]
    public void ScopesAreEmittedAsProperties()
    {
        var items = Capture(
            logger =>
            {
                using (logger.BeginScope(new Dictionary<string, object> { ["scopeKey"] = "scopeValue" }))
                {
                    logger.LogInformation("scoped");
                }
            },
            includeScopes: true);

        var properties = items[0]["data"]!["baseData"]!["properties"]!.AsObject();
        properties["scopeKey"]!.GetValue<string>().Should().Be("scopeValue");
    }

    [TestMethod]
    public void EmptyBatchReturnsNull()
    {
        var batch = new Batch<LogRecord>([], 0);
        AzureMonitorLogSerializer.SerializeBatch(in batch, Resource, InstrumentationKey).Should().BeNull();
    }

    [TestMethod]
    public void EventIdIsEmittedAsProperties()
    {
        var items = Capture(logger => logger.Log(LogLevel.Information, new EventId(42, "MyEvent"), "with event"));

        var properties = items[0]["data"]!["baseData"]!["properties"]!.AsObject();
        properties["EventId"]!.GetValue<string>().Should().Be("42");
        properties["EventName"]!.GetValue<string>().Should().Be("MyEvent");
    }
}
