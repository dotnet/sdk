// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

#nullable enable

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

            JObject? obj = token as JObject;

            if (obj == null)
            {
                return null;
            }

            JToken element;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element) || element.Type != JTokenType.String)
            {
                return null;
            }

            return element.ToString();
        }

        internal static bool ToBool(this JToken? token, string? key = null, bool defaultValue = false)
        {
            JToken checkToken;

            // determine which token to bool-ify
            if (token == null)
            {
                return defaultValue;
            }
            else if (key == null)
            {
                checkToken = token;
            }
            else if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out checkToken))
            {
                return defaultValue;
            }

            // do the conversion on checkToken
            if (checkToken.Type == JTokenType.Boolean || checkToken.Type == JTokenType.String)
            {
                return string.Equals(checkToken.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return defaultValue;
            }
        }

        internal static int ToInt32(this JToken? token, string? key = null, int defaultValue = 0)
        {
            int value = defaultValue;
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out value))
                {
                    return defaultValue;
                }

                return value;
            }

            JObject? obj = token as JObject;

            if (obj == null)
            {
                return defaultValue;
            }

            JToken element;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element))
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

        internal static T ToEnum<T>(this JToken token, string? key = null, T defaultValue = default(T))
            where T : struct
        {
            string? val = token.ToString(key);
            T result;
            if (val == null || !Enum.TryParse(val, out result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static Guid ToGuid(this JToken token, string? key = null, Guid defaultValue = default(Guid))
        {
            string? val = token.ToString(key);
            Guid result;
            if (val == null || !Guid.TryParse(val, out result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static IEnumerable<JProperty> PropertiesOf(this JToken? token, string? key = null)
        {
            JObject? obj = token as JObject;

            if (obj == null)
            {
                return Empty<JProperty>.List.Value;
            }

            if (key != null)
            {
                JToken element;
                if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element))
                {
                    return Empty<JProperty>.List.Value;
                }

                obj = element as JObject;
            }

            if (obj == null)
            {
                return Empty<JProperty>.List.Value;
            }

            return obj.Properties();
        }

        internal static T? Get<T>(this JToken? token, string? key)
            where T : JToken
        {
            JObject? obj = token as JObject;

            if (obj == null)
            {
                return default(T);
            }

            JToken res;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out res))
            {
                return default(T);
            }

            return res as T;
        }

        internal static IReadOnlyDictionary<string, string> ToStringDictionary(this JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(comparer ?? StringComparer.Ordinal);

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

        // reads a dictionary whose values can either be string literals, or arrays of strings.
        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ToStringListDictionary(this JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, IReadOnlyList<string>> result = new Dictionary<string, IReadOnlyList<string>>(comparer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                if (property.Value == null)
                {
                    continue;
                }
                else if (property.Value.Type == JTokenType.String)
                {
                    result[property.Name] = new List<string>() { property.Value.ToString() };
                }
                else if (property.Value.Type == JTokenType.Array)
                {
                    result[property.Name] = property.Value.ArrayAsStrings();
                }
            }

            return result;
        }

        // Leaves the values as JTokens.
        internal static IReadOnlyDictionary<string, JToken> ToJTokenDictionary(this JToken token, StringComparer? comparaer = null, string? propertyName = null)
        {
            Dictionary<string, JToken> result = new Dictionary<string, JToken>(comparaer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                result[property.Name] = property.Value;
            }

            return result;
        }

        internal static IReadOnlyList<string> ArrayAsStrings(this JToken? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray? arr = token as JArray;

            if (arr == null)
            {
                return Empty<string>.List.Value;
            }

            List<string> values = new List<string>();

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

            JArray? arr = token as JArray;

            if (arr == null)
            {
                return Empty<Guid>.List.Value;
            }

            List<Guid> values = new List<Guid>();

            foreach (JToken item in arr)
            {
                if (item != null && item.Type == JTokenType.String)
                {
                    Guid val;
                    if (Guid.TryParse(item.ToString(), out val))
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

            JArray? arr = token as JArray;

            if (arr == null)
            {
                yield break;
            }

            foreach (JToken item in arr)
            {
                T? res = item as T;

                if (res != null)
                {
                    yield return res;
                }
            }
        }
    }
}
