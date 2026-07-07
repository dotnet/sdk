// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Shared primitives for writing Application Insights "Breeze" telemetry envelopes as
/// newline-delimited JSON. Used by both the trace serializer
/// (<see cref="AzureMonitorTelemetrySerializer"/>) and the log serializer
/// (<see cref="AzureMonitorLogSerializer"/>) so the envelope framing, resource-tag mapping,
/// property formatting, and timestamp formatting stay identical across signals.
/// </summary>
internal static class BreezeWriter
{
    private const byte NewLine = (byte)'\n';

    /// <summary>
    /// Serializes <paramref name="envelope"/> using the supplied source-generated
    /// <paramref name="typeInfo"/> and terminates it with the NDJSON newline separator.
    /// </summary>
    public static void WriteEnvelope<TData>(Stream stream, TelemetryEnvelope<TData> envelope, JsonTypeInfo<TelemetryEnvelope<TData>> typeInfo)
        where TData : class
    {
        JsonSerializer.Serialize(stream, envelope, typeInfo);
        stream.WriteByte(NewLine);
    }

    /// <summary>
    /// Adds the cloud role / role instance / application version / SDK version context tags that
    /// every envelope carries, derived from the process telemetry resource.
    /// </summary>
    public static void AddResourceTags(Dictionary<string, string> tags, TelemetryResourceContext resource)
    {
        if (resource.RoleName is not null)
        {
            tags[BreezeSchema.CloudRole] = BreezeSchema.Truncate(resource.RoleName, BreezeSchema.CloudRoleMaxLength)!;
        }
        if (resource.RoleInstance is not null)
        {
            tags[BreezeSchema.CloudRoleInstance] = BreezeSchema.Truncate(resource.RoleInstance, BreezeSchema.CloudRoleInstanceMaxLength)!;
        }
        if (resource.ApplicationVersion is not null)
        {
            tags[BreezeSchema.ApplicationVersion] = BreezeSchema.Truncate(resource.ApplicationVersion, BreezeSchema.ApplicationVersionMaxLength)!;
        }
        tags[BreezeSchema.InternalSdkVersion] = BreezeSchema.Truncate(resource.SdkVersion, BreezeSchema.SdkVersionMaxLength)!;
    }

    /// <summary>
    /// Formats a telemetry property, mirroring the Azure Monitor exporter: drops keys over the
    /// length limit, drops null values, keeps only the first occurrence of a duplicate key, and
    /// renders arrays as comma-separated values.
    /// </summary>
    public static bool TryFormatProperty(KeyValuePair<string, object?> tag, HashSet<string> seenKeys, out string key, out string value)
    {
        key = tag.Key;
        value = string.Empty;

        if (tag.Value is null || key.Length > BreezeSchema.KvpMaxKeyLength || !seenKeys.Add(key))
        {
            return false;
        }

        var formatted = tag.Value is Array arrayValue
            ? string.Join(",", arrayValue.Cast<object?>().Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)))
            : Convert.ToString(tag.Value, CultureInfo.InvariantCulture);

        value = BreezeSchema.Truncate(formatted, BreezeSchema.KvpMaxValueLength) ?? "null";
        return true;
    }

    public static string FormatTime(DateTime utcTimestamp)
        => new DateTimeOffset(DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc)).ToString("O", CultureInfo.InvariantCulture);

    public static string FormatTime(DateTimeOffset timestamp)
        => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
