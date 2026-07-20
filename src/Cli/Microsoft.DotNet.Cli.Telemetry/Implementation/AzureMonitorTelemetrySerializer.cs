// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Diagnostics;
using System.Globalization;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Serializes a batch of <see cref="Activity"/> instances into the Application Insights
/// "Breeze" wire format: newline-delimited JSON (NDJSON) of telemetry envelopes, exactly
/// as <c>Azure.Monitor.OpenTelemetry.Exporter</c> would produce for the same spans.
///
/// This is a focused reimplementation of the exporter's internal mapping
/// (<c>TraceHelper.OtelToAzureMonitorTrace</c>) covering the span/event shapes the .NET
/// CLI actually emits:
/// <list type="bullet">
///   <item><description>Server / Consumer spans → <c>RequestData</c>.</description></item>
///   <item><description>Internal / Client / Producer spans → <c>RemoteDependencyData</c>
///     (<c>InProc</c> for internal spans).</description></item>
///   <item><description>Each <see cref="ActivityEvent"/> → <c>MessageData</c>, or
///     <c>ExceptionData</c> when the event is an OpenTelemetry exception event.</description></item>
/// </list>
/// The mapping is intentionally reimplemented because every mapping/serialization type in
/// the Azure Monitor exporter is <c>internal</c> and cannot be reused from this assembly.
/// The envelope is projected onto the source-generated <see cref="TelemetryJsonContext"/>
/// model so the JSON shape stays declarative and AOT/trim-safe. The output bytes can be
/// POSTed directly to the Breeze <c>/v2.1/track</c> endpoint.
/// </summary>
internal static class AzureMonitorTelemetrySerializer
{
    /// <summary>
    /// Serializes <paramref name="batch"/> to NDJSON bytes. Returns <see langword="null"/>
    /// when the batch produces no telemetry.
    /// </summary>
    public static byte[]? SerializeBatch(in Batch<Activity> batch, TelemetryResourceContext resource, string instrumentationKey)
    {
        using var stream = new MemoryStream();
        var wroteAny = false;

        foreach (var activity in batch)
        {
            wroteAny |= WriteActivity(stream, activity, resource, instrumentationKey);
        }

        return wroteAny ? stream.ToArray() : null;
    }

    private static bool WriteActivity(Stream stream, Activity activity, TelemetryResourceContext resource, string instrumentationKey)
    {
        var isRequest = IsRequest(activity.Kind);
        var traceId = activity.TraceId.ToHexString();
        var spanId = activity.SpanId.ToHexString();

        // Events are emitted as their own envelopes (Message / Exception), parented to this span.
        foreach (ref readonly var activityEvent in activity.EnumerateEvents())
        {
            WriteEventEnvelope(stream, in activityEvent, traceId, spanId, resource, instrumentationKey);
        }

        // The span itself becomes a Request or RemoteDependency envelope.
        var spanTags = BuildSpanTags(activity, traceId, isRequest, resource);
        if (isRequest)
        {
            var envelope = new TelemetryEnvelope<RequestData>
            {
                Name = BreezeSchema.RequestEnvelopeName,
                Time = BreezeWriter.FormatTime(activity.StartTimeUtc),
                InstrumentationKey = instrumentationKey,
                Tags = spanTags,
                Data = new TelemetryData<RequestData>
                {
                    BaseType = BreezeSchema.RequestDataType,
                    BaseData = new RequestData
                    {
                        Id = spanId,
                        Name = BreezeSchema.Truncate(activity.DisplayName, BreezeSchema.NameMaxLength),
                        Duration = FormatDuration(activity.Duration),
                        Success = activity.Status != ActivityStatusCode.Error,
                        // CLI request spans carry no HTTP status; the exporter emits "0" in that case.
                        ResponseCode = "0",
                        Properties = BuildProperties(activity.EnumerateTagObjects()),
                    },
                },
            };
            BreezeWriter.WriteEnvelope(stream, envelope, TelemetryJsonContext.Default.RequestEnvelope);        }
        else
        {            var envelope = new TelemetryEnvelope<RemoteDependencyData>
            {
                Name = BreezeSchema.DependencyEnvelopeName,
                Time = BreezeWriter.FormatTime(activity.StartTimeUtc),
                InstrumentationKey = instrumentationKey,
                Tags = spanTags,
                Data = new TelemetryData<RemoteDependencyData>
                {
                    BaseType = BreezeSchema.RemoteDependencyDataType,
                    BaseData = new RemoteDependencyData
                    {
                        Id = spanId,
                        Name = BreezeSchema.Truncate(activity.DisplayName, BreezeSchema.NameMaxLength),
                        // Internal spans are reported as in-process dependencies, matching the exporter.
                        Type = activity.Kind == ActivityKind.Internal ? "InProc" : null,
                        Duration = FormatDuration(activity.Duration),
                        Success = activity.Status != ActivityStatusCode.Error,
                        Properties = BuildProperties(activity.EnumerateTagObjects()),
                    },
                },
            };
            BreezeWriter.WriteEnvelope(stream, envelope, TelemetryJsonContext.Default.DependencyEnvelope);
        }

        return true;
    }

    private static void WriteEventEnvelope(
        Stream stream,
        in ActivityEvent activityEvent,
        string traceId,
        string spanId,
        TelemetryResourceContext resource,
        string instrumentationKey)
    {
        var isException = activityEvent.Name == BreezeSchema.ExceptionEventName;
        var tags = BuildEventTags(traceId, spanId, resource);
        var time = BreezeWriter.FormatTime(activityEvent.Timestamp);

        if (isException)
        {
            var envelope = new TelemetryEnvelope<ExceptionData>
            {
                Name = BreezeSchema.ExceptionEnvelopeName,
                Time = time,
                InstrumentationKey = instrumentationKey,
                Tags = tags,
                Data = new TelemetryData<ExceptionData>
                {
                    BaseType = BreezeSchema.ExceptionDataType,
                    BaseData = BuildExceptionData(in activityEvent),
                },
            };
            BreezeWriter.WriteEnvelope(stream, envelope, TelemetryJsonContext.Default.ExceptionEnvelope);
        }
        else
        {
            var envelope = new TelemetryEnvelope<MessageData>
            {
                Name = BreezeSchema.MessageEnvelopeName,
                Time = time,
                InstrumentationKey = instrumentationKey,
                Tags = tags,
                Data = new TelemetryData<MessageData>
                {
                    BaseType = BreezeSchema.MessageDataType,
                    BaseData = new MessageData
                    {
                        Message = BreezeSchema.Truncate(activityEvent.Name, BreezeSchema.MessageMaxLength),
                        Properties = BuildProperties(activityEvent.EnumerateTagObjects()),
                    },
                },
            };
            BreezeWriter.WriteEnvelope(stream, envelope, TelemetryJsonContext.Default.MessageEnvelope);
        }
    }

    private static ExceptionData BuildExceptionData(in ActivityEvent activityEvent)
    {
        string? exceptionType = null;
        string? exceptionMessage = null;
        string? exceptionStack = null;
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, string>? properties = null;

        foreach (ref readonly var tag in activityEvent.EnumerateTagObjects())
        {
            switch (tag.Key)
            {
                case BreezeSchema.ExceptionType:
                    exceptionType = tag.Value?.ToString();
                    break;
                case BreezeSchema.ExceptionMessage:
                    exceptionMessage = tag.Value?.ToString();
                    break;
                case BreezeSchema.ExceptionStacktrace:
                    exceptionStack = tag.Value?.ToString();
                    break;
                default:
                    if (BreezeWriter.TryFormatProperty(tag, seenKeys, out var key, out var value))
                    {
                        properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                        properties[key] = value;
                    }
                    break;
            }
        }

        var hasFullStack = exceptionStack is not null && exceptionStack.Length <= BreezeSchema.ExceptionStackMaxLength;
        return new ExceptionData
        {
            Exceptions =
            [
                new ExceptionDetails
                {
                    TypeName = BreezeSchema.Truncate(exceptionType, BreezeSchema.ExceptionTypeNameMaxLength),
                    Message = BreezeSchema.Truncate(exceptionMessage, BreezeSchema.ExceptionMessageMaxLength),
                    HasFullStack = hasFullStack,
                    Stack = BreezeSchema.Truncate(exceptionStack, BreezeSchema.ExceptionStackMaxLength),
                },
            ],
            Properties = properties ?? [],
        };
    }

    private static Dictionary<string, string> BuildSpanTags(Activity activity, string traceId, bool isRequest, TelemetryResourceContext resource)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        if (activity.ParentSpanId != default)
        {
            tags[BreezeSchema.OperationParentId] = activity.ParentSpanId.ToHexString();
        }
        tags[BreezeSchema.OperationId] = traceId;
        if (isRequest)
        {
            tags[BreezeSchema.OperationName] = BreezeSchema.Truncate(activity.DisplayName, BreezeSchema.OperationNameMaxLength)!;
        }
        BreezeWriter.AddResourceTags(tags, resource);
        return tags;
    }

    private static Dictionary<string, string> BuildEventTags(string traceId, string spanId, TelemetryResourceContext resource)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [BreezeSchema.OperationParentId] = spanId,
            [BreezeSchema.OperationId] = traceId,
        };
        BreezeWriter.AddResourceTags(tags, resource);
        return tags;
    }

    private static Dictionary<string, string>? BuildProperties(Activity.Enumerator<KeyValuePair<string, object?>> tags)
    {
        Dictionary<string, string>? properties = null;
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (ref readonly var tag in tags)
        {
            if (!BreezeWriter.TryFormatProperty(tag, seenKeys, out var key, out var value))
            {
                continue;
            }

            properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
            properties[key] = value;
        }

        return properties;
    }

    private static string FormatDuration(TimeSpan duration)
        => duration.ToString("c", CultureInfo.InvariantCulture);

    private static bool IsRequest(ActivityKind kind)
        => kind is ActivityKind.Server or ActivityKind.Consumer;
}
