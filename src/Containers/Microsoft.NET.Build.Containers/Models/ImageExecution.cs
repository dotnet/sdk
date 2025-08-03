// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// ImageConfig defines the execution parameters which should be used as a base when running a container using an image.
/// This is most often seen as the 'Config' property of a application/vnd.oci.image.config.v1+json or application/vnd.docker.container.image.v1+json config object.
/// </summary>
public class ImageExecution
{
    [JsonPropertyName("User")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; set; }

    [JsonConverter(typeof(ContainerPortMapConverter))]
    [JsonPropertyName("ExposedPorts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Port>? ExposedPorts { get; set; }

    [JsonConverter(typeof(ContainerEnvStringConverter))]
    [JsonPropertyName("Env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<KeyValuePair<string, string>>? Env { get; set; }

    [JsonPropertyName("Entrypoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Entrypoint { get; set; }

    [JsonPropertyName("Cmd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Cmd { get; set; }

    [JsonConverter(typeof(ContainerEmptyStructMapConverter))]
    [JsonPropertyName("Volumes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Volumes { get; set; }

    [JsonPropertyName("WorkingDir")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkingDir { get; set; }

    [JsonPropertyName("StopSignal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StopSignal { get; set; }

    [JsonPropertyName("Labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Labels { get; set; }
}

/// <summary>
/// a converter that writes a list of strings as a json object whose keys are the list items and values are empty objects.
/// </summary>
internal class ContainerEmptyStructMapConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictConverter = options.GetConverter(typeof(Dictionary<string, object>)) as JsonConverter<Dictionary<string, object>>;
        var dict = dictConverter?.Read(ref reader, typeToConvert, options);
        return dict?.Keys.ToList();
    }
    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var item in value)
        {
            writer.WritePropertyName(item);
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// a converter that reads a list of strings and splits the strings by '=' to create a list of key-value pairs.
/// </summary>
internal class ContainerEnvStringConverter : JsonConverter<List<KeyValuePair<string, string>>>
{
    public override List<KeyValuePair<string, string>>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var result = new List<KeyValuePair<string, string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                var envVar = reader.GetString();
                var parts = envVar?.Split('=', 2);
                if (parts?.Length == 2)
                {
                    result.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
                }
            }
            return result;
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, List<KeyValuePair<string, string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var kvp in value)
        {
            writer.WriteStringValue($"{kvp.Key}={kvp.Value}");
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// a converter that reads a json object  into a dictionary and then parses the keys of that dictionary into a list of Port objects.
/// it writes a list of such Port objects as a json object where the keys are the port numbers and the values are empty objects.
/// </summary>
public class ContainerPortMapConverter : JsonConverter<List<Port>>
{

    public override List<Port>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var dictConverter = options.GetConverter(typeof(Dictionary<string, object>)) as JsonConverter<Dictionary<string, object>>;
            var dict = dictConverter?.Read(ref reader, typeToConvert, options);
            if (dict != null)
            {
                var ports = new List<Port>();
                foreach (var port in dict.Keys)
                {
                    if (ContainerHelpers.TryParsePort(port, out Port? parsedPort, out ContainerHelpers.ParsePortError? errors))
                    {
                        // If parsing was successful, add the port to the list
                        ports.Add((Port)parsedPort);
                    }
                }
                return ports;
            }
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, List<Port> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var port in value)
        {
            writer.WritePropertyName($"{port.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{port.Type}");
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
