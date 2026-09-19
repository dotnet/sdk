// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Telemetry.Implementation;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class AzureMonitorTelemetrySerializerTests
{
    private const string InstrumentationKey = "test-ikey";
    private const string SourceName = "Microsoft.DotNet.Tests.TelemetrySerializer";
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

    private static JsonNode[] Serialize(params Activity[] activities)
    {
        var batch = new Batch<Activity>(activities, activities.Length);
        var bytes = AzureMonitorTelemetrySerializer.SerializeBatch(in batch, Resource, InstrumentationKey);
        bytes.Should().NotBeNull();

        return Encoding.UTF8.GetString(bytes!)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!)
            .ToArray();
    }

    [TestMethod]
    public void InternalSpanBecomesRemoteDependencyInProc()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("dotnet build", ActivityKind.Internal)!;
        activity.SetTag("exitCode", "0");
        activity.Stop();

        var items = Serialize(activity);
        items.Should().HaveCount(1);

        var envelope = items[0];
        envelope["name"]!.GetValue<string>().Should().Be("RemoteDependency");
        envelope["iKey"]!.GetValue<string>().Should().Be(InstrumentationKey);
        envelope["tags"]!["ai.operation.id"]!.GetValue<string>().Should().Be(activity.TraceId.ToHexString());
        envelope["tags"]!["ai.cloud.role"]!.GetValue<string>().Should().Be("dotnet-cli");
        envelope["tags"]!["ai.cloud.roleInstance"]!.GetValue<string>().Should().Be("role-instance");
        envelope["tags"]!["ai.application.ver"]!.GetValue<string>().Should().Be("42.0.0");
        envelope["tags"]!["ai.internal.sdkVersion"]!.GetValue<string>().Should().Be("dotnet1.0:otel1.0:ext1.0");

        var data = envelope["data"]!;
        data["baseType"]!.GetValue<string>().Should().Be("RemoteDependencyData");

        var baseData = data["baseData"]!;
        baseData["id"]!.GetValue<string>().Should().Be(activity.SpanId.ToHexString());
        baseData["name"]!.GetValue<string>().Should().Be("dotnet build");
        baseData["type"]!.GetValue<string>().Should().Be("InProc");
        baseData["success"]!.GetValue<bool>().Should().BeTrue();
        baseData["ver"]!.GetValue<int>().Should().Be(2);
        baseData["properties"]!["exitCode"]!.GetValue<string>().Should().Be("0");
    }

    [TestMethod]
    public void ServerSpanBecomesRequest()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("dotnet build", ActivityKind.Server)!;
        activity.Stop();

        var items = Serialize(activity);
        var envelope = items[0];

        envelope["name"]!.GetValue<string>().Should().Be("Request");
        envelope["tags"]!["ai.operation.name"]!.GetValue<string>().Should().Be("dotnet build");
        envelope["data"]!["baseType"]!.GetValue<string>().Should().Be("RequestData");
        envelope["data"]!["baseData"]!["responseCode"]!.GetValue<string>().Should().Be("0");
        envelope["data"]!["baseData"]!["id"]!.GetValue<string>().Should().Be(activity.SpanId.ToHexString());
    }

    [TestMethod]
    public void ErrorStatusSetsSuccessFalse()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("dotnet build", ActivityKind.Internal)!;
        activity.SetStatus(ActivityStatusCode.Error);
        activity.Stop();

        var items = Serialize(activity);
        items[0]["data"]!["baseData"]!["success"]!.GetValue<bool>().Should().BeFalse();
    }

    [TestMethod]
    public void ActivityEventBecomesMessageEnvelopeParentedToSpan()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("dotnet build", ActivityKind.Internal)!;
        var tags = new ActivityTagsCollection { { "exitCode", "0" }, { "event id", "guid" } };
        activity.AddEvent(new ActivityEvent("dotnet/cli/command/finish", tags: tags));
        activity.Stop();

        var items = Serialize(activity);
        // Event envelope is emitted before the span envelope.
        items.Should().HaveCount(2);

        var message = items[0];
        message["name"]!.GetValue<string>().Should().Be("Message");
        message["tags"]!["ai.operation.id"]!.GetValue<string>().Should().Be(activity.TraceId.ToHexString());
        message["tags"]!["ai.operation.parentId"]!.GetValue<string>().Should().Be(activity.SpanId.ToHexString());

        var baseData = message["data"]!["baseData"]!;
        message["data"]!["baseType"]!.GetValue<string>().Should().Be("MessageData");
        baseData["message"]!.GetValue<string>().Should().Be("dotnet/cli/command/finish");
        baseData["ver"]!.GetValue<int>().Should().Be(2);
        baseData["properties"]!["exitCode"]!.GetValue<string>().Should().Be("0");
    }

    [TestMethod]
    public void DuplicatePropertyKeysAreDeduplicated()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("dotnet build", ActivityKind.Internal)!;
        // Two tags with the same key: only the first should be written to avoid invalid JSON.
        activity.SetTag("dup", "first");
        activity.SetTag("dup", "second");
        activity.Stop();

        var items = Serialize(activity);
        var properties = items[0]["data"]!["baseData"]!["properties"]!.AsObject();
        properties["dup"]!.GetValue<string>().Should().Be("second");
        // The activity itself dedups SetTag by key, so this asserts valid single-key JSON.
        properties.Count.Should().Be(1);
    }

    [TestMethod]
    public void EmptyBatchReturnsNull()
    {
        var batch = new Batch<Activity>([], 0);
        AzureMonitorTelemetrySerializer.SerializeBatch(in batch, Resource, InstrumentationKey).Should().BeNull();
    }

    [TestMethod]
    public void ProducedLinesAreValidJson()
    {
        using var listener = CreateListener();
        using var activity = Source.StartActivity("dotnet build", ActivityKind.Internal)!;
        activity.AddEvent(new ActivityEvent("dotnet/cli/command/finish"));
        activity.Stop();

        var batch = new Batch<Activity>([activity], 1);
        var bytes = AzureMonitorTelemetrySerializer.SerializeBatch(in batch, Resource, InstrumentationKey)!;
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().EndWith("\n");
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var act = () => JsonDocument.Parse(line);
            act.Should().NotThrow();
        }
    }
}
