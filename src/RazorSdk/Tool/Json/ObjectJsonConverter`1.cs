// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal abstract class ObjectJsonConverter<T> : JsonConverter<T>
    where T : class
{
    protected abstract T ReadFromProperties(JsonDataReader reader);
    protected abstract void WriteProperties(JsonDataWriter writer, T value);

    public sealed override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // Parse the current JSON value into a JsonDocument/JsonElement.
        // This advances the reader past the entire value automatically.
        using var doc = JsonDocument.ParseValue(ref reader);

        var dataReader = new JsonDataReader(doc.RootElement);
        return ReadFromProperties(dataReader);
    }

    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        var dataWriter = new JsonDataWriter(writer);
        WriteProperties(dataWriter, value);

        writer.WriteEndObject();
    }
}
