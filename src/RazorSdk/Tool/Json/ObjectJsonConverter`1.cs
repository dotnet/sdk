// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal abstract class ObjectJsonConverter<T> : JsonConverter<T>
    where T : class
{
    protected abstract T ReadFromProperties(JsonDataReader reader);
    protected abstract void WriteProperties(JsonDataWriter writer, T value);

    public sealed override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        reader.ReadToken(JsonToken.StartObject);

        T result;

        var dataReader = new JsonDataReader(reader);
        result = ReadFromProperties(dataReader);

        // JSON.NET serialization expects that we don't advance passed the end object token,
        // but we should verify that it's there.
        reader.CheckToken(JsonToken.EndObject);

        return result;
    }

    public sealed override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        var dataWriter = new JsonDataWriter(writer);
        WriteProperties(dataWriter, value);

        writer.WriteEndObject();
    }
}
