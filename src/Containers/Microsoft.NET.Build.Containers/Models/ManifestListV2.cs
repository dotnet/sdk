// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Marker interface that signals that this contains sub-manifests
/// </summary>
/// <remarks>
/// In order for the fields of the _subtypes_ to be serialized, we have to annotate this interface
/// with the child types
/// </remarks>
[JsonDerivedType(typeof(ManifestListV2))]
[JsonDerivedType(typeof(ImageIndexV1))]
public interface IMultiImageManifest : IManifest;

public record struct ManifestListV2(
    int schemaVersion,
    [property:JsonConverter(typeof(MediaTypeConverter))]
    [field:JsonConverter(typeof(MediaTypeConverter))]
    string mediaType,
    PlatformSpecificManifest[] manifests) : IMultiImageManifest
{
    [JsonIgnore]
    public string? MediaType => mediaType;
}

public record struct PlatformInformation(
    string architecture,
    string os,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? variant,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? features,
    [property: JsonPropertyName("os.version")]
    [field: JsonPropertyName("os.version")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? osVersion,
    [property: JsonPropertyName("os.features")]
    [field: JsonPropertyName("os.features")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? osFeatures);

public record struct PlatformSpecificManifest(
    [property:JsonConverter(typeof(MediaTypeConverter))]
    [field:JsonConverter(typeof(MediaTypeConverter))]
    string mediaType,
    long size,
    Digest digest,
    PlatformInformation platform);

public record struct ImageIndexV1(
    int schemaVersion,
    [property:JsonConverter(typeof(MediaTypeConverter))]
    [field:JsonConverter(typeof(MediaTypeConverter))]
    string mediaType,
    PlatformSpecificOciManifest[] manifests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, string>? annotations) : IMultiImageManifest
{
    [JsonIgnore]
    public string? MediaType => mediaType;
}

public record struct PlatformSpecificOciManifest(
    [property:JsonConverter(typeof(MediaTypeConverter))]
    [field:JsonConverter(typeof(MediaTypeConverter))]
    string mediaType,
    long size,
    Digest digest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PlatformInformation? platform,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [field: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, string>? annotations);

public class MediaTypeConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping));
    }
}
