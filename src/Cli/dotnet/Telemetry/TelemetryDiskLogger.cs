// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TelemetryDiskLogger
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

        public static void WriteLog(string logPath, IEnumerable<Activity> activies)
        {
            try
            {
                var jsonText = !File.Exists(logPath) ? """{"activities":[]}""" : File.ReadAllText(logPath);
                var root = JsonNode.Parse(jsonText)!;
                var activitiesArray = root["activities"]!.AsArray();
                activitiesArray.AddRange(activies.Select(r => JsonNode.Parse(JsonSerializer.Serialize(CreateActivityJsonModel(r), s_jsonOptions))));
                root["activities"] = activitiesArray;
                File.AppendAllText(logPath, root.ToJsonString(s_jsonOptions));
            }
            catch
            {
                // Swallow any exceptions to avoid interfering with telemetry shutdown.
            }
        }

        private static object CreateActivityJsonModel(Activity activity) => new
        {
            operationName = activity.OperationName,
            displayName = activity.DisplayName,
            source = activity.Source,
            duration = activity.Duration,
            id = activity.Id,
            parentId = activity.ParentId,
            rootId = activity.RootId,
            //tags = activity.Tags,
            tagObjects = activity.TagObjects,
            events = activity.Events,
            spanId = activity.SpanId.ToString(),
            traceId =  activity.TraceId.ToString(),
            parentSpanId = activity.ParentSpanId.ToString()
        };
    }
}
