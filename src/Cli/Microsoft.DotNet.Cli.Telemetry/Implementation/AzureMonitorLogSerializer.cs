// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Serializes a batch of OpenTelemetry <see cref="LogRecord"/> instances into the Application
/// Insights "Breeze" wire format (newline-delimited JSON of telemetry envelopes), exactly as
/// <c>Azure.Monitor.OpenTelemetry.Exporter</c> would produce via its internal
/// <c>LogsHelper.OtelToAzureMonitorLogs</c>:
/// <list type="bullet">
///   <item><description>A log record with an <see cref="LogRecord.Exception"/> becomes an
///     <c>ExceptionData</c> envelope.</description></item>
///   <item><description>Any other log record becomes a <c>MessageData</c> envelope.</description></item>
/// </list>
/// Both carry the mapped Application Insights <c>severityLevel</c>, the log attributes / scopes
/// as properties (plus <c>CategoryName</c>, <c>EventId</c>, and <c>EventName</c>), and the
/// operation id / parent id from the record's trace context. Feeding the Application Insights
/// <c>traces</c> table this way is what the Azure Monitor log exporter does, so existing
/// log-based dashboards keep working.
///
/// <para>This is a focused reimplementation covering the shapes a CLI actually emits; the more
/// advanced Azure-specific log conventions (custom events, availability results, and the
/// <c>microsoft.*</c> context attributes) are not special-cased and simply flow through as
/// ordinary message properties.</para>
/// </summary>
internal static class AzureMonitorLogSerializer
{
    /// <summary>
    /// Serializes <paramref name="batch"/> to NDJSON bytes. Returns <see langword="null"/>
    /// when the batch produces no telemetry.
    /// </summary>
    public static byte[]? SerializeBatch(in Batch<LogRecord> batch, TelemetryResourceContext resource, string instrumentationKey)
    {
        using var stream = new MemoryStream();
        var wroteAny = false;

        foreach (var logRecord in batch)
        {
            WriteLogRecord(stream, logRecord, resource, instrumentationKey);
            wroteAny = true;
        }

        return wroteAny ? stream.ToArray() : null;
    }

    private static readonly Action<LogRecordScope, (Dictionary<string, string> Properties, HashSet<string> Seen)> AddScopeProperties =
        static (scope, state) =>
        {
            foreach (var scopeItem in scope)
            {
                if (string.IsNullOrEmpty(scopeItem.Key) || scopeItem.Key == BreezeSchema.OriginalFormatKey)
                {
                    continue;
                }

                if (BreezeWriter.TryFormatProperty(scopeItem, state.Seen, out var key, out var value))
                {
                    state.Properties[key] = value;
                }
            }
        };

    private static void WriteLogRecord(Stream stream, LogRecord logRecord, TelemetryResourceContext resource, string instrumentationKey)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        // Mirrors LogsHelper: the exception message wins, then the formatted message, then the
        // raw "{OriginalFormat}" template if nothing else produced a message.
        var message = logRecord.Exception?.Message ?? logRecord.FormattedMessage;

        foreach (var attribute in logRecord.Attributes ?? [])
        {
            if (attribute.Key == BreezeSchema.OriginalFormatKey)
            {
                if (logRecord.Exception?.Message is not null)
                {
                    // Keep the template as a property when the exception supplied the message.
                    if (attribute.Value is not null && seenKeys.Add(BreezeSchema.OriginalFormatProperty))
                    {
                        properties[BreezeSchema.OriginalFormatProperty] =
                            BreezeSchema.Truncate(attribute.Value.ToString(), BreezeSchema.KvpMaxValueLength) ?? "null";
                    }
                }
                else if (message is null)
                {
                    message = attribute.Value?.ToString();
                }

                continue;
            }

            if (BreezeWriter.TryFormatProperty(attribute, seenKeys, out var key, out var value))
            {
                properties[key] = value;
            }
        }

        logRecord.ForEachScope(AddScopeProperties, (properties, seenKeys));

        if (!string.IsNullOrEmpty(logRecord.CategoryName) && seenKeys.Add(BreezeSchema.CategoryNameProperty))
        {
            properties[BreezeSchema.CategoryNameProperty] =
                BreezeSchema.Truncate(logRecord.CategoryName, BreezeSchema.KvpMaxValueLength)!;
        }

        if (logRecord.EventId.Id != 0 && seenKeys.Add(BreezeSchema.EventIdProperty))
        {
            properties[BreezeSchema.EventIdProperty] = logRecord.EventId.Id.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(logRecord.EventId.Name) && seenKeys.Add(BreezeSchema.EventNameProperty))
        {
            properties[BreezeSchema.EventNameProperty] =
                BreezeSchema.Truncate(logRecord.EventId.Name, BreezeSchema.KvpMaxValueLength)!;
        }

        var tags = BuildTags(logRecord, resource);
        var severity = GetSeverityLevel(logRecord.LogLevel);
        var propertyBag = properties.Count > 0 ? properties : null;
        var time = BreezeWriter.FormatTime(logRecord.Timestamp);

        if (logRecord.Exception is not null)
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
                    BaseData = BuildExceptionData(logRecord.Exception, severity, propertyBag),
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
                        Message = BreezeSchema.Truncate(message, BreezeSchema.MessageMaxLength),
                        SeverityLevel = severity,
                        Properties = propertyBag,
                    },
                },
            };
            BreezeWriter.WriteEnvelope(stream, envelope, TelemetryJsonContext.Default.MessageEnvelope);
        }
    }

    private static ExceptionData BuildExceptionData(Exception exception, string severity, Dictionary<string, string>? properties)
    {
        var stack = exception.StackTrace;
        var hasFullStack = stack is not null && stack.Length <= BreezeSchema.ExceptionStackMaxLength;

        return new ExceptionData
        {
            SeverityLevel = severity,
            Exceptions =
            [
                new ExceptionDetails
                {
                    TypeName = BreezeSchema.Truncate(exception.GetType().FullName, BreezeSchema.ExceptionTypeNameMaxLength),
                    Message = BreezeSchema.Truncate(exception.Message, BreezeSchema.ExceptionMessageMaxLength),
                    HasFullStack = hasFullStack,
                    Stack = BreezeSchema.Truncate(stack, BreezeSchema.ExceptionStackMaxLength),
                },
            ],
            Properties = properties,
        };
    }

    private static Dictionary<string, string> BuildTags(LogRecord logRecord, TelemetryResourceContext resource)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);

        // The OTel log record carries the ambient Activity's trace context so Azure Monitor can
        // correlate the log row with the span it was emitted under.
        if (logRecord.TraceId != default)
        {
            tags[BreezeSchema.OperationId] = logRecord.TraceId.ToHexString();
        }
        if (logRecord.SpanId != default)
        {
            tags[BreezeSchema.OperationParentId] = logRecord.SpanId.ToHexString();
        }

        BreezeWriter.AddResourceTags(tags, resource);
        return tags;
    }

    private static string GetSeverityLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Critical => BreezeSchema.SeverityCritical,
        LogLevel.Error => BreezeSchema.SeverityError,
        LogLevel.Warning => BreezeSchema.SeverityWarning,
        LogLevel.Information => BreezeSchema.SeverityInformation,
        _ => BreezeSchema.SeverityVerbose,
    };
}
