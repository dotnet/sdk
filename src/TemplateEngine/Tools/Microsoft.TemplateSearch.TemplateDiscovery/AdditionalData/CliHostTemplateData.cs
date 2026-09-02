// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.TemplateSearch.TemplateDiscovery.AdditionalData
{
    [System.Text.Json.Serialization.JsonConverter(typeof(CliHostTemplateData.CustomJsonConverter))]
    internal class CliHostTemplateData
    {
        private const string IsHiddenKey = "isHidden";
        private const string LongNameKey = "longName";
        private const string ShortNameKey = "shortName";
        private const string AlwaysShowKey = "alwaysShow";

        internal CliHostTemplateData(JsonObject? jObject)
        {
            var symbolsInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            if (jObject == null)
            {
                SymbolInfo = symbolsInfo;
                return;
            }

            JsonNode? usagesNode = Microsoft.TemplateEngine.JExtensions.GetPropertyCaseInsensitive(jObject, nameof(UsageExamples));
            if (usagesNode is JsonArray usagesArray)
            {
                UsageExamples = new List<string>(usagesArray.Select(v => v?.ToString()).Where(v => v != null).OfType<string>());
            }

            JsonNode? symbolsNode = Microsoft.TemplateEngine.JExtensions.GetPropertyCaseInsensitive(jObject, nameof(SymbolInfo));
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
                        symbolProperties[symbolProperty.Key] = symbolProperty.Value?.ToString() ?? string.Empty;
                    }

                    symbolsInfo[symbolInfo.Key] = symbolProperties;
                }
            }
            SymbolInfo = symbolsInfo;

            IsHidden = jObject.TryGetPropertyValue(nameof(IsHidden), out JsonNode? isHiddenNode)
                && isHiddenNode is JsonValue isHiddenVal
                && isHiddenVal.TryGetValue<bool>(out bool boolVal) && boolVal;

        }

        internal CliHostTemplateData(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> symbolInfo,
            IEnumerable<string>? usageExamples = null,
            bool isHidden = false)
        {
            SymbolInfo = symbolInfo;
            UsageExamples = usageExamples?.ToArray() ?? [];
            IsHidden = isHidden;
        }

        public IReadOnlyList<string> UsageExamples { get; } = [];

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SymbolInfo { get; }

        public bool IsHidden { get; }

        public HashSet<string> HiddenParameterNames
        {
            get
            {
                HashSet<string> hiddenNames = new HashSet<string>();
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
                HashSet<string> parametersToAlwaysShow = new HashSet<string>(StringComparer.Ordinal);
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
                Dictionary<string, string> map = new Dictionary<string, string>();

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
                Dictionary<string, string> map = new Dictionary<string, string>();

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

        internal static CliHostTemplateData Default { get; } = new CliHostTemplateData(null);

        internal string DisplayNameForParameter(string parameterName)
        {
            if (SymbolInfo.TryGetValue(parameterName, out IReadOnlyDictionary<string, string>? configForParam)
                && configForParam.TryGetValue(LongNameKey, out string? longName))
            {
                return longName;
            }

            return parameterName;
        }

        private class CustomJsonConverter : System.Text.Json.Serialization.JsonConverter<CliHostTemplateData>
        {
            public override CliHostTemplateData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, CliHostTemplateData value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    return;
                }
                writer.WriteStartObject();
                if (value.IsHidden)
                {
                    writer.WritePropertyName(nameof(IsHidden));
                    writer.WriteBooleanValue(value.IsHidden);
                }
                if (value.SymbolInfo.Any())
                {
                    writer.WritePropertyName(nameof(SymbolInfo));
                    JsonSerializer.Serialize(writer, value.SymbolInfo, options);
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
