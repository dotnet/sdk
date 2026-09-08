// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine
{
    internal static class JExtensions
    {
        private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

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
            if (element == null || element.GetValueKind() == JsonValueKind.Null)
            {
                return null;
            }

            if (element is JsonValue strVal && strVal.GetValueKind() == JsonValueKind.String)
            {
                return strVal.GetValue<string>();
            }

            return element.ToJsonString();
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

        internal static bool TryParseInt(this JsonNode token, out int result)
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

        internal static T ToEnum<T>(this JsonNode token, string? key = null, T defaultValue = default, bool ignoreCase = false)
            where T : struct
        {
            string? val = token.ToString(key);
            if (val == null || !Enum.TryParse(val, ignoreCase, out T result))
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

        /// <summary>
        /// Reads <paramref name="propertyName"/> as read only string list/>.
        /// Property value may be string or array.
        /// </summary>
        internal static IReadOnlyList<string> ToStringReadOnlyList(this JsonObject jObject, string propertyName, IReadOnlyList<string>? defaultValue = null)
        {
            defaultValue ??= [];
            JsonNode? token = jObject.Get<JsonNode>(propertyName);
            if (token == null)
            {
                return defaultValue;
            }
            return token.JTokenStringOrArrayToCollection(defaultValue) ?? defaultValue;
        }

        internal static IEnumerable<KeyValuePair<string, JsonNode?>> PropertiesOf(this JsonNode? token, string? key = null)
        {
            if (token is not JsonObject obj)
            {
                return [];
            }

            if (key != null)
            {
                JsonNode? element = GetPropertyCaseInsensitive(obj, key);
                if (element == null)
                {
                    return [];
                }
                return element is not JsonObject jObj ? [] : GetObjectProperties(jObj);
            }
            return GetObjectProperties(obj);
        }

        /// <summary>
        /// Enumerates the properties of a <see cref="JsonObject"/>, handling duplicate keys gracefully.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonObject"/> lazily initializes its internal dictionary when first accessed.
        /// If the underlying JSON contains duplicate property keys, initialization throws
        /// <see cref="ArgumentException"/>. In that case this method falls back to re-parsing via
        /// <see cref="JsonDocument"/>, which supports duplicate keys (last-wins semantics, like Newtonsoft.Json).
        /// </remarks>
        private static IReadOnlyList<KeyValuePair<string, JsonNode?>> GetObjectProperties(JsonObject obj)
        {
            try
            {
                return obj.ToList();
            }
            catch (ArgumentException)
            {
                // The JsonObject contains duplicate property keys (malformed JSON).
                // Fall back to JsonDocument-based enumeration which supports duplicate keys.
                return GetObjectPropertiesViaDocument(obj);
            }
        }

        /// <summary>
        /// Enumerates the properties of a <see cref="JsonObject"/> by re-parsing its JSON text via
        /// <see cref="JsonDocument"/>, which tolerates duplicate keys.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonObject.WriteTo"/> falls back to the underlying <see cref="System.Text.Json.JsonElement"/>
        /// when the internal dictionary has not yet been initialized, so <see cref="JsonNode.ToJsonString"/> is
        /// safe to call even after a failed <see cref="JsonObject"/> initialization.
        /// Duplicate keys are de-duplicated with last-wins semantics to match normal JsonObject enumeration behavior.
        /// </remarks>
        private static List<KeyValuePair<string, JsonNode?>> GetObjectPropertiesViaDocument(JsonObject obj)
        {
            string json = obj.ToJsonString();
            var result = new List<KeyValuePair<string, JsonNode?>>();
            var propertyIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            using JsonDocument doc = JsonDocument.Parse(json, DocOptions);
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                var property = new KeyValuePair<string, JsonNode?>(prop.Name, ParseJsonNode(prop.Value.GetRawText()));
                if (propertyIndexes.TryGetValue(prop.Name, out int existingIndex))
                {
                    result[existingIndex] = property;
                }
                else
                {
                    propertyIndexes[prop.Name] = result.Count;
                    result.Add(property);
                }
            }
            return result;
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

        internal static IReadOnlyDictionary<string, string> ToStringDictionary(this JsonNode token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, string> result = new(comparer ?? StringComparer.Ordinal);

            foreach (var property in token.PropertiesOf(propertyName))
            {
                if (property.Value == null || property.Value.GetValueKind() != JsonValueKind.String)
                {
                    continue;
                }

                result[property.Key] = property.Value.GetValue<string>();
            }

            return result;
        }

        /// <summary>
        /// Converts properties of <paramref name="token"/> to dictionary.
        /// Leaves the values as JsonNode.
        /// </summary>
        internal static IReadOnlyDictionary<string, JsonNode> ToJsonNodeDictionary(this JsonNode token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, JsonNode> result = new(comparer ?? StringComparer.Ordinal);

            foreach (var property in token.PropertiesOf(propertyName))
            {
                if (property.Value != null)
                {
                    result[property.Key] = property.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Converts properties of <paramref name="token"/> to dictionary.
        /// Values are serialized to string (as JsonNode). Strings are serialized as JSON, i.e. needs to be parsed prior to be used.
        /// </summary>
        internal static IReadOnlyDictionary<string, string> ToJsonNodeStringDictionary(this JsonNode token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, string> result = new(comparer ?? StringComparer.Ordinal);

            foreach (var property in token.PropertiesOf(propertyName))
            {
                if (property.Value != null)
                {
                    result[property.Key] = property.Value.ToJsonString();
                }
            }

            return result;
        }

        internal static TemplateParameterPrecedence ToTemplateParameterPrecedence(this JsonNode jObject, string? key)
        {
            if (!jObject.TryGetValue(key, out JsonNode? checkToken))
            {
                return TemplateParameterPrecedence.Default;
            }

            PrecedenceDefinition precedenceDefinition = (PrecedenceDefinition)checkToken.ToInt32(nameof(PrecedenceDefinition));
            string? isRequiredCondition = checkToken.ToString(nameof(TemplateParameterPrecedence.IsRequiredCondition));
            string? isEnabledCondition = checkToken.ToString(nameof(TemplateParameterPrecedence.IsEnabledCondition));
            bool isRequired = checkToken.ToBool(nameof(TemplateParameterPrecedence.IsRequired));

            return new TemplateParameterPrecedence(precedenceDefinition, isRequiredCondition, isEnabledCondition, isRequired);
        }

        internal static IReadOnlyList<string> ArrayAsStrings(this JsonNode? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JsonArray>(propertyName);
            }

            if (token is not JsonArray arr)
            {
                return [];
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

        internal static IReadOnlyList<Guid> ArrayAsGuids(this JsonNode? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JsonArray>(propertyName);
            }

            if (token is not JsonArray arr)
            {
                return [];
            }

            List<Guid> values = new();

            foreach (JsonNode? item in arr)
            {
                if (item != null && item.GetValueKind() == JsonValueKind.String)
                {
                    if (Guid.TryParse(item.GetValue<string>(), out Guid val))
                    {
                        values.Add(val);
                    }
                }
            }

            return values;
        }

        internal static IEnumerable<T> Items<T>(this JsonNode? token, string? propertyName = null)
            where T : JsonNode
        {
            if (propertyName != null)
            {
                token = token.Get<JsonArray>(propertyName);
            }

            if (token is not JsonArray arr)
            {
                yield break;
            }

            foreach (JsonNode? item in arr)
            {
                if (item is T res)
                {
                    yield return res;
                }
            }
        }

        internal static JsonObject ReadJObjectFromIFile(this IFile file)
        {
            using Stream s = file.OpenRead();
            using TextReader tr = new StreamReader(s, System.Text.Encoding.UTF8, true);
            string json = tr.ReadToEnd();
            return (JsonObject?)JsonNode.Parse(json, null, DocOptions)
                ?? throw new InvalidOperationException("Failed to parse JSON from file.");
        }

        internal static JsonObject ReadObject(this IPhysicalFileSystem fileSystem, string path)
        {
            using Stream fileStream = fileSystem.OpenRead(path);
            using var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true);
            string json = textReader.ReadToEnd();
            return (JsonObject?)JsonNode.Parse(json, null, DocOptions)
                ?? throw new InvalidOperationException($"Failed to parse JSON from '{path}'.");
        }

        internal static void WriteObject<T>(this IPhysicalFileSystem fileSystem, string path, T obj, JsonTypeInfo<T> jsonTypeInfo)
        {
            using Stream fileStream = fileSystem.CreateFile(path);
            JsonSerializer.Serialize(fileStream, obj, jsonTypeInfo);
        }

        internal static IReadOnlyList<string> JTokenStringOrArrayToCollection(this JsonNode? token, IReadOnlyList<string> defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.GetValueKind() == JsonValueKind.String)
            {
                string tokenValue = token.GetValue<string>();
                return new List<string>() { tokenValue };
            }

            return token.ArrayAsStrings();
        }

        /// <summary>
        /// Converts <paramref name="obj"/> to valid JSON string.
        /// </summary>
#if NET7_0_OR_GREATER
        [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
        [RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
#endif
        internal static string ToJsonString(object obj)
        {
            return JsonSerializer.Serialize(obj, SerializerOptions);
        }

        internal static string ToCamelCase(this string str)
        {
            return str switch
            {
                "" => str,
                _ => str.First().ToString().ToLower() + str.Substring(1),
            };
        }

        /// <summary>
        /// Tries to get a property value from a <see cref="JsonObject"/> using case-insensitive key matching.
        /// </summary>
        internal static bool TryGetValueCaseInsensitive(this JsonObject obj, string key, out JsonNode? result)
        {
            result = GetPropertyCaseInsensitive(obj, key);
            return result != null;
        }

        /// <summary>
        /// Gets a property from a JsonObject with case-insensitive key matching.
        /// Handles duplicate keys gracefully by falling back to JsonDocument-based lookup.
        /// </summary>
        internal static JsonNode? GetPropertyCaseInsensitive(JsonObject obj, string key)
        {
            try
            {
                // Try exact match first (fast path).
                if (obj.TryGetPropertyValue(key, out JsonNode? result))
                {
                    return result;
                }

                // Fall back to case-insensitive search.
                foreach (var kvp in obj)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Value;
                    }
                }

                return null;
            }
            catch (ArgumentException)
            {
                // The JsonObject contains duplicate property keys (malformed JSON).
                // Fall back to JsonDocument-based lookup which supports duplicate keys.
                return GetPropertyCaseInsensitiveViaDocument(obj, key);
            }
        }

        /// <summary>
        /// Gets a property from a JsonObject by re-parsing via JsonDocument (tolerates duplicate keys).
        /// Uses last-wins semantics for duplicate keys, consistent with Newtonsoft.Json behavior.
        /// </summary>
        private static JsonNode? GetPropertyCaseInsensitiveViaDocument(JsonObject obj, string key)
        {
            string json = obj.ToJsonString();
            using JsonDocument doc = JsonDocument.Parse(json, DocOptions);

            JsonNode? result = null;
            JsonNode? caseInsensitiveResult = null;

            // Enumerate all properties; last-wins for duplicates
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.Ordinal))
                {
                    result = ParseJsonNode(prop.Value.GetRawText());
                }
                else if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    caseInsensitiveResult = ParseJsonNode(prop.Value.GetRawText());
                }
            }

            // Prefer exact match, fall back to case-insensitive
            return result ?? caseInsensitiveResult;
        }

        /// <summary>
        /// Parses a JSON string into a JsonObject (with comment/trailing comma support).
        /// </summary>
        internal static JsonObject ParseJsonObject(string json)
        {
            return (JsonObject?)JsonNode.Parse(json, null, DocOptions)
                ?? throw new InvalidOperationException("Failed to parse JSON string as JsonObject.");
        }

        /// <summary>
        /// Parses a JSON string into a JsonNode (with comment/trailing comma support).
        /// </summary>
        internal static JsonNode? ParseJsonNode(string json)
        {
            return JsonNode.Parse(json, null, DocOptions);
        }

        /// <summary>
        /// Serializes an object to a JsonObject via JSON round-trip.
        /// Equivalent to Newtonsoft's JObject.FromObject().
        /// </summary>
#if NET7_0_OR_GREATER
        [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
        [RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
#endif
        internal static JsonObject FromObject(object obj)
        {
            string json = JsonSerializer.Serialize(obj, SerializerOptions);
            return (JsonObject?)JsonNode.Parse(json, null, DocOptions)
                ?? throw new InvalidOperationException("Failed to round-trip object to JsonObject.");
        }

        /// <summary>
        /// Creates a deep clone of a <see cref="JsonObject"/> by round-tripping through JSON text.
        /// </summary>
        internal static JsonObject DeepCloneObject(this JsonObject source)
        {
            return (JsonObject?)JsonNode.Parse(source.ToJsonString(), null, DocOptions)
                ?? throw new InvalidOperationException("Failed to deep clone JsonObject.");
        }

        /// <summary>
        /// Merges properties from <paramref name="source"/> into <paramref name="target"/>.
        /// Equivalent to Newtonsoft's JObject.Merge() with default settings:
        /// - Objects are recursively merged
        /// - Arrays are concatenated
        /// - Other values (including null) from source overwrite target.
        /// </summary>
        internal static void Merge(this JsonObject target, JsonObject source)
        {
            foreach (var property in source)
            {
                if (property.Value is JsonObject sourceObj
                    && target.TryGetPropertyValue(property.Key, out JsonNode? targetNode)
                    && targetNode is JsonObject targetObj)
                {
                    // Recursively merge nested objects
                    targetObj.Merge(sourceObj);
                }
                else if (property.Value is JsonArray sourceArr
                    && target.TryGetPropertyValue(property.Key, out targetNode)
                    && targetNode is JsonArray targetArr)
                {
                    // Concatenate arrays
                    foreach (var item in sourceArr)
                    {
                        targetArr.Add(item != null ? JsonNode.Parse(item.ToJsonString()) : null);
                    }
                }
                else
                {
                    // Overwrite (or add) the property; clone value to detach from source tree
                    target[property.Key] = property.Value != null
                        ? JsonNode.Parse(property.Value.ToJsonString())
                        : null;
                }
            }
        }
    }
}
