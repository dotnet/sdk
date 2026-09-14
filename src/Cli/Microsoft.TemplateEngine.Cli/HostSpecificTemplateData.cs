// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.TemplateEngine.Cli
{
    [JsonConverter(typeof(HostSpecificTemplateData.HostSpecificTemplateDataJsonConverter))]
    public class HostSpecificTemplateData
    {
        private const string IsHiddenKey = "isHidden";
        private const string LongNameKey = "longName";
        private const string ShortNameKey = "shortName";
        private const string AlwaysShowKey = "alwaysShow";

        internal HostSpecificTemplateData(JsonObject? jObject)
        {
            var symbolsInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            if (jObject == null)
            {
                SymbolInfo = symbolsInfo;
                return;
            }

            JsonNode? usagesNode = GetPropertyCaseInsensitive(jObject, nameof(UsageExamples));
            if (usagesNode is JsonArray usagesArray)
            {
                UsageExamples = new List<string>(usagesArray
                    .Where(v => v != null && v.GetValueKind() == JsonValueKind.String)
                    .Select(v => v!.GetValue<string>()));
            }

            JsonNode? symbolsNode = GetPropertyCaseInsensitive(jObject, nameof(SymbolInfo));
            if (symbolsNode is JsonObject symbols)
            {
                foreach (var symbolInfo in symbols)
                {
                    if (symbolInfo.Value is not JsonObject symbol)
                    {
                        continue;
                    }

                    var symbolProperties = new Dictionary<string, string>();

                    foreach (var symbolProperty in symbol)
                    {
                        if (symbolProperty.Value is null)
                        {
                            symbolProperties[symbolProperty.Key] = "";
                        }
                        else
                        {
                            var kind = symbolProperty.Value.GetValueKind();
                            symbolProperties[symbolProperty.Key] = kind switch
                            {
                                JsonValueKind.String => symbolProperty.Value.GetValue<string>(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                _ => symbolProperty.Value.ToJsonString()
                            };
                        }
                    }

                    symbolsInfo[symbolInfo.Key] = symbolProperties;
                }
            }
            SymbolInfo = symbolsInfo;

            JsonNode? isHiddenNode = GetPropertyCaseInsensitive(jObject, nameof(IsHidden));
            if (isHiddenNode != null)
            {
                var kind = isHiddenNode.GetValueKind();
                if (kind == JsonValueKind.True)
                {
                    IsHidden = true;
                }
                else if (kind == JsonValueKind.String && bool.TryParse(isHiddenNode.GetValue<string>(), out bool hidden))
                {
                    IsHidden = hidden;
                }
            }
        }

        internal HostSpecificTemplateData(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> symbolInfo,
            IEnumerable<string>? usageExamples = null,
            bool isHidden = false)
        {
            SymbolInfo = symbolInfo;
            UsageExamples = usageExamples?.ToArray() ?? Array.Empty<string>();
            IsHidden = isHidden;
        }

        public IReadOnlyList<string> UsageExamples { get; } = Array.Empty<string>();

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SymbolInfo { get; }

        public bool IsHidden { get; }

        public HashSet<string> HiddenParameterNames
        {
            get
            {
                HashSet<string> hiddenNames = new();
                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(IsHiddenKey, out string? hiddenStringValue)
                        && bool.TryParse(hiddenStringValue, out bool hiddenBoolValue)
                        && hiddenBoolValue)
                    {
                        hiddenNames.Add(paramInfo.Key);
                    }
                }

                return hiddenNames;
            }
        }

        public HashSet<string> ParametersToAlwaysShow
        {
            get
            {
                HashSet<string> parametersToAlwaysShow = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(AlwaysShowKey, out string? alwaysShowValue)
                        && bool.TryParse(alwaysShowValue, out bool alwaysShowBoolValue)
                        && alwaysShowBoolValue)
                    {
                        parametersToAlwaysShow.Add(paramInfo.Key);
                    }
                }

                return parametersToAlwaysShow;
            }
        }

        public Dictionary<string, string> LongNameOverrides
        {
            get
            {
                Dictionary<string, string> map = new();

                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(LongNameKey, out string? longNameOverride))
                    {
                        map.Add(paramInfo.Key, longNameOverride);
                    }
                }

                return map;
            }
        }

        public Dictionary<string, string> ShortNameOverrides
        {
            get
            {
                Dictionary<string, string> map = new();

                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(ShortNameKey, out string? shortNameOverride))
                    {
                        map.Add(paramInfo.Key, shortNameOverride);
                    }
                }

                return map;
            }
        }

        internal static HostSpecificTemplateData Default { get; } = new HostSpecificTemplateData((JsonObject?)null);

        internal string DisplayNameForParameter(string parameterName)
        {
            if (SymbolInfo.TryGetValue(parameterName, out IReadOnlyDictionary<string, string>? configForParam)
                && configForParam.TryGetValue(LongNameKey, out string? longName))
            {
                return longName;
            }

            return parameterName;
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

        private class HostSpecificTemplateDataJsonConverter : JsonConverter<HostSpecificTemplateData>
        {
            public override HostSpecificTemplateData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, HostSpecificTemplateData value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                if (value.IsHidden)
                {
                    writer.WriteBoolean(nameof(IsHidden), value.IsHidden);
                }
                if (value.SymbolInfo.Any())
                {
                    writer.WritePropertyName(nameof(SymbolInfo));
                    writer.WriteStartObject();
                    foreach (var symbol in value.SymbolInfo)
                    {
                        writer.WritePropertyName(symbol.Key);
                        writer.WriteStartObject();
                        foreach (var prop in symbol.Value)
                        {
                            writer.WriteString(prop.Key, prop.Value);
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }

                if (value.UsageExamples != null && value.UsageExamples.Any(e => !string.IsNullOrWhiteSpace(e)))
                {
                    writer.WritePropertyName(nameof(UsageExamples));
                    writer.WriteStartArray();
                    foreach (string example in value.UsageExamples)
                    {
                        if (!string.IsNullOrWhiteSpace(example))
                        {
                            writer.WriteStringValue(example);
                        }
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
        }
    }
}
