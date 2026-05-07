// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine
{
    internal static class JExtensions
    {
        private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        internal static string? ToString(this JsonNode? token, string? key)
        {
            if (key == null)
            {
                if (token == null)
                {
                    return null;
                }

                if (token is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    return val.GetValue<string>();
                }

                return null;
            }

            if (token is not JsonObject obj)
            {
                return null;
            }

            JsonNode? element = GetPropertyCaseInsensitive(obj, key);
            if (element == null || element.GetValueKind() != JsonValueKind.String)
            {
                return null;
            }

            return element.GetValue<string>();
        }

        internal static bool TryGetValue(this JsonNode? token, string? key, out JsonNode? result)
        {
            result = null;

            // determine which token to bool-ify
            if (token == null)
            {
                return false;
            }
            else if (key == null)
            {
                result = token;
            }
            else
            {
                result = GetPropertyCaseInsensitive(token.AsObject(), key);
                if (result == null)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool TryParseBool(this JsonNode token, out bool result)
        {
            result = false;
            var kind = token.GetValueKind();
            if (kind == JsonValueKind.True)
            {
                result = true;
                return true;
            }
            if (kind == JsonValueKind.False)
            {
                result = false;
                return true;
            }
            if (kind == JsonValueKind.String)
            {
                return bool.TryParse(token.GetValue<string>(), out result);
            }
            return false;
        }

        internal static bool ToBool(this JsonNode? token, string? key = null, bool defaultValue = false)
        {
            if (!token.TryGetValue(key, out JsonNode? checkToken))
            {
                return defaultValue;
            }

            if (!checkToken!.TryParseBool(out bool result))
            {
                result = defaultValue;
            }

            return result;
        }

        internal static int ToInt32(this JsonNode? token, string? key = null, int defaultValue = 0)
        {
            if (key == null)
            {
                if (token == null || !token.TryParseInt(out int value))
                {
                    return defaultValue;
                }

                return value;
            }

            if (token is not JsonObject obj)
            {
                return defaultValue;
            }

            JsonNode? element = GetPropertyCaseInsensitive(obj, key);
            if (element == null || !element.TryParseInt(out int result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static T ToEnum<T>(this JsonNode token, string? key = null, T defaultValue = default)
            where T : struct
        {
            string? val = token.ToString(key);
            if (val == null || !Enum.TryParse(val, out T result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static Guid ToGuid(this JsonNode token, string? key = null, Guid defaultValue = default)
        {
            string? val = token.ToString(key);
            if (val == null || !Guid.TryParse(val, out Guid result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static IEnumerable<KeyValuePair<string, JsonNode?>> PropertiesOf(this JsonNode? token, string? key = null)
        {
            if (token is not JsonObject currentJObj)
            {
                return Array.Empty<KeyValuePair<string, JsonNode?>>();
            }

            if (key != null)
            {
                JsonNode? element = GetPropertyCaseInsensitive(currentJObj, key);
                if (element is not JsonObject nested)
                {
                    return Array.Empty<KeyValuePair<string, JsonNode?>>();
                }
                return nested.ToList();
            }

            return currentJObj.ToList();
        }

        internal static T? Get<T>(this JsonNode? token, string? key)
            where T : JsonNode
        {
            if (token is not JsonObject obj || key == null)
            {
                return default;
            }

            JsonNode? res = GetPropertyCaseInsensitive(obj, key);
            return res as T;
        }

        internal static IReadOnlyList<string> ArrayAsStrings(this JsonNode? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JsonArray>(propertyName);
            }

            if (token is not JsonArray arr)
            {
                return Array.Empty<string>();
            }

            List<string> values = new();

            foreach (JsonNode? item in arr)
            {
                if (item != null && item.GetValueKind() == JsonValueKind.String)
                {
                    values.Add(item.GetValue<string>());
                }
            }

            return values;
        }

        internal static JsonObject ReadObject(this IPhysicalFileSystem fileSystem, string path)
        {
            using Stream fileStream = fileSystem.OpenRead(path);
            using var textReader = new StreamReader(fileStream, Encoding.UTF8, true);
            string json = textReader.ReadToEnd();
            return (JsonObject?)JsonNode.Parse(json, null, DocOptions)
                ?? throw new InvalidOperationException($"Failed to parse JSON from '{path}'.");
        }

        internal static void WriteObject(this IPhysicalFileSystem fileSystem, string path, JsonNode obj)
        {
            using Stream fileStream = fileSystem.CreateFile(path);
            using var writer = new Utf8JsonWriter(fileStream);
            obj.WriteTo(writer);
        }

        internal static bool TryParse(this string arg, out JsonNode? token)
        {
            try
            {
                token = JsonNode.Parse(arg, null, DocOptions);
                return true;
            }
            catch
            {
                token = null;
                return false;
            }
        }

        private static bool TryParseInt(this JsonNode token, out int result)
        {
            result = default;
            var kind = token.GetValueKind();
            if (kind == JsonValueKind.Number)
            {
                if (token is JsonValue jv && jv.TryGetValue(out int intVal))
                {
                    result = intVal;
                    return true;
                }
                return int.TryParse(token.ToJsonString(), out result);
            }
            if (kind == JsonValueKind.String)
            {
                return int.TryParse(token.GetValue<string>(), out result);
            }
            return false;
        }

        private static JsonNode? GetPropertyCaseInsensitive(JsonObject obj, string key)
        {
            if (obj.TryGetPropertyValue(key, out JsonNode? result))
            {
                return result;
            }

            foreach (var kvp in obj)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
    }
}
