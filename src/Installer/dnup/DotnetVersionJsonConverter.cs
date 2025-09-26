// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    /// <summary>
    /// A custom JSON converter for the DotnetVersion struct.
    /// This ensures proper serialization and deserialization of the struct.
    /// </summary>
    public class DotnetVersionJsonConverter : JsonConverter<DotnetVersion>
    {
        /// <summary>
        /// Reads and converts the JSON to a DotnetVersion struct.
        /// </summary>
        public override DotnetVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? versionString = reader.GetString();
                return new DotnetVersion(versionString);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? versionString = null;
                DotnetVersionType versionType = DotnetVersionType.Auto;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? propertyName = reader.GetString();
                        reader.Read(); // Move to the property value

                        if (propertyName != null && propertyName.Equals("value", StringComparison.OrdinalIgnoreCase))
                        {
                            versionString = reader.GetString();
                        }
                        else if (propertyName != null && propertyName.Equals("versionType", StringComparison.OrdinalIgnoreCase))
                        {
                            versionType = (DotnetVersionType)reader.GetInt32();
                        }
                    }
                }

                return new DotnetVersion(versionString, versionType);
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                return new DotnetVersion(null);
            }

            throw new JsonException($"Unexpected token {reader.TokenType} when deserializing DotnetVersion");
        }

        /// <summary>
        /// Writes a DotnetVersion struct as JSON.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, DotnetVersion value, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(value.Value))
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStringValue(value.Value);

        }
    }
}
