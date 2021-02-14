// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal static class JsonReaderExtensions
    {
        public static string ReadNextStringProperty(this ref Utf8JsonReader reader, string propertyName)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(propertyName))
                {
                    return reader.Read() ? reader.GetString() : null;
                }
            }

            throw new JsonException($"Could not find string property '{propertyName}'.");
        }

        public static bool IsValidStartObject(this ref Utf8JsonReader reader)
        {
            return reader.Read() && reader.TokenType == JsonTokenType.StartObject;
        }

        public static bool IsValidStartArray(this ref Utf8JsonReader reader)
        {
            return reader.Read() && reader.TokenType == JsonTokenType.StartArray;
        }
    }
}
