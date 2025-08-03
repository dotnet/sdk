// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Image defines execution, provenance, and other metadata about a container image.
/// This is most often seen as the thing referenced by the `Config` descriptor in a manifest.
/// </summary>
public class Image
{
    /// <summary>
    /// RFC 3339, section 5.6 date-time string representing the time at which the image was built.
    /// </summary>
    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Created { get; set; }

    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; set; }

    [JsonPropertyName("architecture")]
    public required string Architecture { get; set; }

    [JsonPropertyName("os")]
    public required string OS { get; set; }

    [JsonPropertyName("os.version")]
    public string? OsVersion { get; set; }

    [JsonPropertyName("os.features")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? OsFeatures { get; set; }

    [JsonPropertyName("variant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Variant { get; set; }


    [JsonPropertyName("config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageExecution? Config { get; set; }

    [JsonPropertyName("rootfs")]
    public required RootFS RootFS { get; set; }

    [JsonPropertyName("history")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public History[]? History { get; set; }
}

public class RootFS
{
    [JsonPropertyName("type")]

    public required string Type { get; set; }

    [JsonConverter(typeof(DigestArrayConverter))]
    [JsonPropertyName("diff_ids")]
    public required Digest[] DiffIDs { get; set; }
}

public class History
{
    /// <summary>
    /// RFC 3339, section 5.6 date-time string representing the time at which the image was built.
    /// </summary>
    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Created { get; set; }

    /// <summary>
    /// RFC 3339, section 5.6 date-time string representing the time at which the image was built.
    /// </summary>
    [JsonPropertyName("created_by")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; set; }

    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }

    [JsonPropertyName("empty_layer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? EmptyLayer { get; set; }
}

internal class DigestArrayConverter : JsonConverter<Digest[]>
{
    public override Digest[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Custom deserialization logic
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var digests = new List<Digest>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return digests.ToArray();
                }
                digests.Add(Digest.Parse(reader.GetString()!));
            }
        }
        return [];
    }

    public override void Write(Utf8JsonWriter writer, Digest[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var digest in value)
        {
            writer.WriteStringValue(digest.ToString());
        }
        writer.WriteEndArray();
    }
}
