// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// POCO model for the Application Insights "Breeze" telemetry envelope, plus the
/// source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> used to
/// (de)serialize it. Using source generation instead of hand-rolled
/// <c>Utf8JsonWriter</c>/<c>JsonDocument</c> code keeps the wire mapping declarative and
/// AOT/trim-safe (required because this assembly is consumed by the Native-AOT
/// <c>dotnet-aot</c> CLI).
///
/// <para>The envelope is generic over its <c>baseData</c> payload so each concrete payload
/// type is serialized without a polymorphic <c>object</c> member (which would not be
/// source-gen/AOT friendly).</para>
/// </summary>
internal sealed class TelemetryEnvelope<TData> where TData : class
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("iKey")]
    public string InstrumentationKey { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; } = [];

    [JsonPropertyName("data")]
    public TelemetryData<TData> Data { get; set; } = new();
}

/// <summary>The <c>data</c> object of a telemetry envelope: a type discriminator plus payload.</summary>
internal sealed class TelemetryData<TData> where TData : class
{
    [JsonPropertyName("baseType")]
    public string BaseType { get; set; } = string.Empty;

    [JsonPropertyName("baseData")]
    public TData BaseData { get; set; } = default!;
}

/// <summary>Breeze <c>RequestData</c> payload (Server/Consumer spans).</summary>
internal sealed class RequestData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("responseCode")]
    public string ResponseCode { get; set; } = "0";

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Properties { get; set; }

    [JsonPropertyName("ver")]
    public int Ver { get; set; } = BreezeSchema.DataVersion;
}

/// <summary>Breeze <c>RemoteDependencyData</c> payload (Internal/Client/Producer spans).</summary>
internal sealed class RemoteDependencyData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Properties { get; set; }

    [JsonPropertyName("ver")]
    public int Ver { get; set; } = BreezeSchema.DataVersion;
}

/// <summary>
/// Breeze <c>MessageData</c> payload. Emitted both for an
/// <see cref="System.Diagnostics.ActivityEvent"/> and for an OpenTelemetry
/// <c>LogRecord</c> (in which case <see cref="SeverityLevel"/> is populated).
/// </summary>
internal sealed class MessageData
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Application Insights severity ("Verbose"/"Information"/"Warning"/"Error"/"Critical").
    /// Populated for log records; left null (and omitted) for activity events.
    /// </summary>
    [JsonPropertyName("severityLevel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SeverityLevel { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Properties { get; set; }

    [JsonPropertyName("ver")]
    public int Ver { get; set; } = BreezeSchema.DataVersion;
}

/// <summary>
/// Breeze <c>ExceptionData</c> payload. Emitted both for an OpenTelemetry exception event and
/// for a <c>LogRecord</c> that carries an exception (in which case <see cref="SeverityLevel"/>
/// is populated).
/// </summary>
internal sealed class ExceptionData
{
    [JsonPropertyName("ver")]
    public int Ver { get; set; } = BreezeSchema.DataVersion;

    [JsonPropertyName("exceptions")]
    public List<ExceptionDetails> Exceptions { get; set; } = [];

    /// <summary>
    /// Application Insights severity ("Verbose"/"Information"/"Warning"/"Error"/"Critical").
    /// Populated for log records; left null (and omitted) for activity exception events.
    /// </summary>
    [JsonPropertyName("severityLevel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SeverityLevel { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Properties { get; set; }
}

/// <summary>A single exception entry inside <see cref="ExceptionData.Exceptions"/>.</summary>
internal sealed class ExceptionDetails
{
    [JsonPropertyName("typeName")]
    public string? TypeName { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("hasFullStack")]
    public bool HasFullStack { get; set; }

    [JsonPropertyName("stack")]
    public string? Stack { get; set; }
}

/// <summary>
/// The Breeze ingestion response body, returned on HTTP 206 (Partial Content) and used to
/// determine which envelopes were rejected with a retriable status.
/// </summary>
internal sealed class TrackResponse
{
    [JsonPropertyName("itemsReceived")]
    public int ItemsReceived { get; set; }

    [JsonPropertyName("itemsAccepted")]
    public int ItemsAccepted { get; set; }

    [JsonPropertyName("errors")]
    public List<TrackResponseError>? Errors { get; set; }
}

/// <summary>A per-item error entry in a <see cref="TrackResponse"/>.</summary>
internal sealed class TrackResponseError
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Source-generated serialization metadata for the Breeze telemetry types. Each closed
/// generic envelope is registered with an explicit property name so callers can reference a
/// stable, strongly-typed <c>JsonTypeInfo</c> (for example <c>TelemetryJsonContext.Default.RequestEnvelope</c>).
/// </summary>
[JsonSerializable(typeof(TelemetryEnvelope<RequestData>), TypeInfoPropertyName = "RequestEnvelope")]
[JsonSerializable(typeof(TelemetryEnvelope<RemoteDependencyData>), TypeInfoPropertyName = "DependencyEnvelope")]
[JsonSerializable(typeof(TelemetryEnvelope<MessageData>), TypeInfoPropertyName = "MessageEnvelope")]
[JsonSerializable(typeof(TelemetryEnvelope<ExceptionData>), TypeInfoPropertyName = "ExceptionEnvelope")]
[JsonSerializable(typeof(TrackResponse))]
internal sealed partial class TelemetryJsonContext : JsonSerializerContext;
