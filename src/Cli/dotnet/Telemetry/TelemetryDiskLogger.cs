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
                activitiesArray.AddRange(activies.Select(r => JsonNode.Parse(JsonSerializer.Serialize(r, s_jsonOptions))));
                root["activities"] = activitiesArray;
                File.AppendAllText(logPath, root.ToJsonString(s_jsonOptions));
            }
            catch
            {
                // Swallow any exceptions to avoid interfering with telemetry shutdown.
            }
        }

        //private static object CreateRecord(ApplicationInsights.Channel.ITelemetry item) => item switch
        //{
        //    EventTelemetry e => new
        //    {
        //        type = "Event",
        //        name = e.Name,
        //        time = e.Timestamp,
        //        properties = e.Properties,
        //        metrics = e.Metrics,
        //        sessionId = e.Context?.Session?.Id
        //    },
        //    ExceptionTelemetry ex => new
        //    {
        //        type = "Exception",
        //        message = ex.Exception?.Message,
        //        ex.Exception?.StackTrace,
        //        time = ex.Timestamp,
        //        properties = ex.Properties
        //    },
        //    TraceTelemetry t => new
        //    {
        //        type = "Trace",
        //        message = t.Message,
        //        severity = t.SeverityLevel,
        //        time = t.Timestamp,
        //        properties = t.Properties
        //    },
        //    MetricTelemetry m => new
        //    {
        //        type = "Metric",
        //        name = m.Name,
        //        value = m.Sum,
        //        count = m.Count,
        //        time = m.Timestamp,
        //        properties = m.Properties
        //    },
        //    _ => new
        //    {
        //        type = item.GetType().Name,
        //        time = item.Timestamp
        //    }
        //};
    }
}
