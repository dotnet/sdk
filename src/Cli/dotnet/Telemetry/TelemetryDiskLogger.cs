// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Telemetry;

internal static class TelemetryDiskLogger
{
    private static readonly JsonSerializerOptions s_jsonOptions;

    private static readonly TelemetryDiskLoggerJsonSerializerContext s_jsonContext;

    public record EventModel(
        string name,
        DateTimeOffset timestamp,
        Dictionary<string, object?> tags);

    public record SourceModel(
        string name,
        string? version,
        Dictionary<string, object?>? tags);

    public record IdentifiersModel(
        string? id,
        string traceId,
        string spanId,
        string parentSpanId,
        string? parentId,
        string? rootId);

    public record ActivityModel(
        string operationName,
        string displayName,
        TimeSpan duration,
        IdentifiersModel identifiers,
        SourceModel source,
        Dictionary<string, string?> tags,
        EventModel[] events);

    static TelemetryDiskLogger()
    {
        s_jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
        s_jsonContext = new(s_jsonOptions);
    }

    public static void WriteLog(string logPath, IEnumerable<Activity> activies)
    {
        try
        {
            var jsonText = !File.Exists(logPath) ? """{"activities":[]}""" : File.ReadAllText(logPath);
            var root = JsonNode.Parse(jsonText)!;
            var activitiesArray = root["activities"]!.AsArray();
            activitiesArray.AddRange(activies.Select(r => JsonNode.Parse(JsonSerializer.Serialize(CreateActivityJsonModel(r), s_jsonContext.ActivityModel))));
            root["activities"] = activitiesArray;
            File.WriteAllText(logPath, root.ToJsonString(s_jsonOptions));
        }
        catch
        {
            // Swallow any exceptions to avoid interfering with telemetry shutdown.
        }
    }

    private static ActivityModel CreateActivityJsonModel(Activity activity) => new(
        operationName: activity.OperationName,
        displayName: activity.DisplayName,
        duration: activity.Duration,
        identifiers: new(
            id: activity.Id,
            traceId: activity.TraceId.ToString(),
            spanId: activity.SpanId.ToString(),
            parentSpanId: activity.ParentSpanId.ToString(),
            parentId: activity.ParentId,
            rootId: activity.RootId
        ),
        source: new(
            name: activity.Source.Name,
            version: activity.Source.Version,
            tags: activity.Source.Tags?.ToDictionary()
        ),
        tags: activity.Tags.ToDictionary(),
        events: [.. activity.Events.Select(e => new EventModel(
            name: e.Name,
            timestamp: e.Timestamp,
            tags: e.Tags.ToDictionary()
        ))]
    );
}

[JsonSerializable(typeof(TelemetryDiskLogger.ActivityModel))]
internal partial class TelemetryDiskLoggerJsonSerializerContext : JsonSerializerContext;
