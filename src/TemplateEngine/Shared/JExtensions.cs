// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine
{
    internal static class JExtensions
    {
        internal static string? ToString(this JToken? token, string? key)
        {
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.String)
                {
                    return null;
                }

                return token.ToString();
            }

            if (token is not JObject obj)
            {
                return null;
            }

            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? element) || element.Type == JTokenType.Null)
            {
                return null;
            }

            return element.ToString();
        }

        internal static bool TryGetValue(this JToken? token, string? key, out JToken? result)
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
            else if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out result))
            {
                return false;
            }

            return true;
        }

        internal static bool TryParseBool(this JToken token, out bool result)
        {
            result = false;
            return (token.Type == JTokenType.Boolean || token.Type == JTokenType.String)
                   &&
                   bool.TryParse(token.ToString(), out result);
        }

        internal static bool ToBool(this JToken? token, string? key = null, bool defaultValue = false)
        {
            if (!token.TryGetValue(key, out JToken? checkToken))
            {
                return defaultValue;
            }

            if (!checkToken!.TryParseBool(out bool result))
            {
                result = defaultValue;
            }

            return result;
        }

        internal static bool TryParseInt(this JToken token, out int result)
        {
            result = default;
            return (token.Type == JTokenType.Integer || token.Type == JTokenType.String)
                   &&
                   int.TryParse(token.ToString(), out result);
        }

        internal static int ToInt32(this JToken? token, string? key = null, int defaultValue = 0)
        {
            int value;
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out value))
                {
                    return defaultValue;
                }

                return value;
            }

            if (token is not JObject obj)
            {
                return defaultValue;
            }

            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? element))
            {
                return defaultValue;
            }
            else if (element.Type == JTokenType.Integer)
            {
                return element.ToInt32();
            }
            else if (int.TryParse(element.ToString(), out value))
            {
                return value;
            }

            return defaultValue;
        }

        internal static T ToEnum<T>(this JToken token, string? key = null, T defaultValue = default, bool ignoreCase = false)
            where T : struct
        {
            string? val = token.ToString(key);
            if (val == null || !Enum.TryParse(val, ignoreCase, out T result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static Guid ToGuid(this JToken token, string? key = null, Guid defaultValue = default)
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
        internal static IReadOnlyList<string> ToStringReadOnlyList(this JObject jObject, string propertyName, IReadOnlyList<string>? defaultValue = null)
        {
            defaultValue ??= Array.Empty<string>();
            JToken? token = jObject.Get<JToken>(propertyName);
            if (token == null)
            {
                return defaultValue;
            }
            return token.JTokenStringOrArrayToCollection(defaultValue) ?? defaultValue;
        }

        internal static IEnumerable<JProperty> PropertiesOf(this JToken? token, string? key = null)
        {
            if (token is not JObject obj)
            {
                return Array.Empty<JProperty>();
            }

            if (key != null)
            {
                if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? element))
                {
                    return Array.Empty<JProperty>();
                }
                return element is not JObject jObj ? Array.Empty<JProperty>() : jObj.Properties();
            }
            return obj.Properties();
        }

        internal static T? Get<T>(this JToken? token, string? key)
            where T : JToken
        {
            if (token is not JObject obj || key == null)
            {
                return default;
            }

            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? res))
            {
                return default;
            }

            return res as T;
        }

        internal static IReadOnlyDictionary<string, string> ToStringDictionary(this JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, string> result = new(comparer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                if (property.Value == null || property.Value.Type != JTokenType.String)
                {
                    continue;
                }

                result[property.Name] = property.Value.ToString();
            }

            return result;
        }

        /// <summary>
        /// Converts properties of <paramref name="token"/> to dictionary.
        /// Leaves the values as JToken.
        /// </summary>
        internal static IReadOnlyDictionary<string, JToken> ToJTokenDictionary(this JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, JToken> result = new(comparer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                result[property.Name] = property.Value;
            }

            return result;
        }

        /// <summary>
        /// Converts properties of <paramref name="token"/> to dictionary.
        /// Values are serialized to string (as JToken). Strings are serialized as <see cref="JToken"/>, i.e. needs to be parsed prior to be used.
        /// </summary>
        internal static IReadOnlyDictionary<string, string> ToJTokenStringDictionary(this JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, string> result = new(comparer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                result[property.Name] = property.Value.ToString(Formatting.None);
            }

            return result;
        }

        internal static TemplateParameterPrecedence ToTemplateParameterPrecedence(this JToken jObject, string? key)
        {
            if (!jObject.TryGetValue(key, out JToken? checkToken))
            {
                return TemplateParameterPrecedence.Default;
            }

            PrecedenceDefinition precedenceDefinition = (PrecedenceDefinition)checkToken.ToInt32(nameof(PrecedenceDefinition));
            string? isRequiredCondition = checkToken.ToString(nameof(TemplateParameterPrecedence.IsRequiredCondition));
            string? isEnabledCondition = checkToken.ToString(nameof(TemplateParameterPrecedence.IsEnabledCondition));
            bool isRequired = checkToken.ToBool(nameof(TemplateParameterPrecedence.IsRequired));

            return new TemplateParameterPrecedence(precedenceDefinition, isRequiredCondition, isEnabledCondition, isRequired);
        }

        internal static IReadOnlyList<string> ArrayAsStrings(this JToken? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            if (token is not JArray arr)
            {
                return Array.Empty<string>();
            }

            List<string> values = new();

            foreach (JToken item in arr)
            {
                if (item != null && item.Type == JTokenType.String)
                {
                    values.Add(item.ToString());
                }
            }

            return values;
        }

        internal static IReadOnlyList<Guid> ArrayAsGuids(this JToken? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            if (token is not JArray arr)
            {
                return Array.Empty<Guid>();
            }

            List<Guid> values = new();

            foreach (JToken item in arr)
            {
                if (item != null && item.Type == JTokenType.String)
                {
                    if (Guid.TryParse(item.ToString(), out Guid val))
                    {
                        values.Add(val);
                    }
                }
            }

            return values;
        }

        internal static IEnumerable<T> Items<T>(this JToken? token, string? propertyName = null)
            where T : JToken
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            if (token is not JArray arr)
            {
                yield break;
            }

            foreach (JToken item in arr)
            {
                if (item is T res)
                {
                    yield return res;
                }
            }
        }

        internal static JObject ReadJObjectFromIFile(this IFile file)
        {
            using Stream s = file.OpenRead();
            using TextReader tr = new StreamReader(s, System.Text.Encoding.UTF8, true);
            using JsonReader r = new JsonTextReader(tr);
            {
                return JObject.Load(r);
            }
        }

        internal static JObject ReadObject(this IPhysicalFileSystem fileSystem, string path)
        {
            using Stream fileStream = fileSystem.OpenRead(path);
            using var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true);
            using var jsonReader = new JsonTextReader(textReader);
            {
                return JObject.Load(jsonReader);
            }
        }

        internal static void WriteObject(this IPhysicalFileSystem fileSystem, string path, object obj)
        {
            using Stream fileStream = fileSystem.CreateFile(path);
            using var textWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8);
            using var jsonWriter = new JsonTextWriter(textWriter);
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, obj);
            }
        }

        internal static IReadOnlyList<string> JTokenStringOrArrayToCollection(this JToken? token, IReadOnlyList<string> defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.Type == JTokenType.String)
            {
                string tokenValue = token.ToString();
                return new List<string>() { tokenValue };
            }

            return token.ArrayAsStrings();
        }

        /// <summary>
        /// Converts <paramref name="obj"/> to valid JSON string.
        /// JToken.ToString() doesn't provide a valid JSON string for JTokenType == String.
        /// </summary>
        internal static string ToJsonString(object obj)
        {
            return JToken.FromObject(obj).ToString(Formatting.None);
        }

        internal static string ToCamelCase(this string str)
        {
            return str switch
            {
                "" => str,
                _ => str.First().ToString().ToLower() + str.Substring(1),
            };
        }

    }
}
