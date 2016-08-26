using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal static class JExtensions
    {
        public static string ToString(this JToken token, string key)
        {
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.String)
                {
                    return null;
                }

                return token.ToString();
            }

            JObject obj = token as JObject;

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

        public static bool ToBool(this JToken token, string key = null, bool defaultValue = false)
        {
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.Boolean)
                {
                    return defaultValue;
                }

                return string.Equals(token.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            }

            JObject obj = token as JObject;

            if (obj == null)
            {
                return defaultValue;
            }

            JToken element;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element) || element.Type != JTokenType.Boolean)
            {
                return defaultValue;
            }

            return string.Equals(element.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static int ToInt32(this JToken token, string key = null, int defaultValue = 0)
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

            JObject obj = token as JObject;

            if (obj == null)
            {
                return defaultValue;
            }

            JToken element;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element) || element.Type != JTokenType.Integer || !int.TryParse(element.ToString(), out value))
            {
                return defaultValue;
            }

            return value;
        }

        public static T ToEnum<T>(this JToken token, string key = null, T defaultValue = default(T))
            where T : struct
        {
            string val = token.ToString(key);
            T result;
            if (val == null || !Enum.TryParse(val, out result))
            {
                return defaultValue;
            }

            return result;
        }

        public static Guid ToGuid(this JToken token, string key = null, Guid defaultValue = default(Guid))
        {
            string val = token.ToString(key);
            Guid result;
            if (val == null || !Guid.TryParse(val, out result))
            {
                return defaultValue;
            }

            return result;
        }

        public static IEnumerable<JProperty> PropertiesOf(this JToken token, string key = null)
        {
            JObject obj = token as JObject;

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

        public static T Get<T>(this JToken token, string key)
            where T : JToken
        {
            JObject obj = token as JObject;

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

        public static IReadOnlyDictionary<string, string> ToStringDictionary(this JToken token, StringComparer comparer = null, string propertyName = null)
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

        public static IReadOnlyList<string> ArrayAsStrings(this JToken token, string propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray arr = token as JArray;

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

        public static IReadOnlyList<Guid> ArrayAsGuids(this JToken token, string propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray arr = token as JArray;

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

        public static IEnumerable<T> Items<T>(this JToken token, string propertyName = null)
            where T : JToken
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray arr = token as JArray;

            if (arr == null)
            {
                yield break;
            }

            foreach (JToken item in arr)
            {
                T res = item as T;

                if (res != null)
                {
                    yield return res;
                }
            }
        }
    }
}
