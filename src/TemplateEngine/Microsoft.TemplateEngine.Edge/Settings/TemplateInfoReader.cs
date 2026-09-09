// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal partial class TemplateInfo
    {
        internal class TemplateInfoReader
        {
            internal static TemplateInfo FromJObject(JsonObject entry)
            {
                string identity = entry.ToString(nameof(Identity)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Identity)} property.", nameof(entry));
                string name = entry.ToString(nameof(Name)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Name)} property.", nameof(entry));
                string mountPointUri = entry.ToString(nameof(MountPointUri)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(MountPointUri)} property.", nameof(entry));
                string configPlace = entry.ToString(nameof(ConfigPlace)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(ConfigPlace)} property.", nameof(entry));
                JsonNode? shortNameToken = entry.Get<JsonNode>(nameof(ShortNameList));
                IEnumerable<string> shortNames = shortNameToken.JTokenStringOrArrayToCollection([]);

                TemplateInfo info = new TemplateInfo(identity, name, shortNames, mountPointUri, configPlace)
                {
                    Author = entry.ToString(nameof(Author))
                };
                JsonArray? classificationsArray = entry.Get<JsonArray>(nameof(Classifications));
                if (classificationsArray != null)
                {
                    List<string> classifications = new List<string>();
                    foreach (JsonNode? item in classificationsArray)
                    {
                        if (item != null)
                        {
                            classifications.Add(item.GetValue<string>());
                        }
                    }
                    info.Classifications = classifications;
                }

                info.DefaultName = entry.ToString(nameof(DefaultName));
                info.PreferDefaultName = entry.ToBool(nameof(PreferDefaultName));
                info.Description = entry.ToString(nameof(Description));
                info.GeneratorId = Guid.Parse(entry.ToString(nameof(GeneratorId)));
                info.GroupIdentity = entry.ToString(nameof(GroupIdentity));
                info.Precedence = entry.ToInt32(nameof(Precedence));

                info.LocaleConfigPlace = entry.ToString(nameof(LocaleConfigPlace));
                info.HostConfigPlace = entry.ToString(nameof(HostConfigPlace));
                info.ThirdPartyNotices = entry.ToString(nameof(ThirdPartyNotices));

                JsonObject? baselineJObject = entry.Get<JsonObject>(nameof(ITemplateInfo.BaselineInfo));
                Dictionary<string, IBaselineInfo> baselineInfo = new Dictionary<string, IBaselineInfo>();
                if (baselineJObject != null)
                {
                    foreach (var item in baselineJObject)
                    {
                        var defaultOverrides = item.Value?.ToStringDictionary(propertyName: nameof(IBaselineInfo.DefaultOverrides));
                        if (defaultOverrides is null)
                        {
                            continue;
                        }

                        IBaselineInfo baseline = new BaselineInfo(defaultOverrides, item.Value.ToString(nameof(IBaselineInfo.Description)));
                        baselineInfo.Add(item.Key, baseline);
                    }
                    info.BaselineInfo = baselineInfo;
                }

                //read parameters
#pragma warning disable CS0618 // Type or member is obsolete
                JsonArray? parametersArray = entry.Get<JsonArray>(nameof(Parameters));
#pragma warning restore CS0618 // Type or member is obsolete
                if (parametersArray != null)
                {
                    List<ITemplateParameter> templateParameters = new List<ITemplateParameter>();
                    foreach (JsonNode? item in parametersArray)
                    {
                        if (item is JsonObject jObj)
                        {
                            templateParameters.Add(ParameterFromJObject(jObj));
                        }
                    }
                    info.ParameterDefinitions = new ParameterDefinitionSet(templateParameters);
                }

                //read tags
                // tags are just "name": "description"
                // e.g.: "language": "C#"
                JsonObject? tagsObject = entry.Get<JsonObject>(nameof(TagsCollection));
                if (tagsObject != null)
                {
                    Dictionary<string, string> tags = new Dictionary<string, string>();
                    foreach (var item in tagsObject)
                    {
                        tags.Add(item.Key, item.Value?.GetValue<string>() ?? string.Empty);
                    }
                    info.TagsCollection = tags;
                }

                info.HostData = entry.ToString(nameof(info.HostData));
                JsonArray? postActionsArray = entry.Get<JsonArray>(nameof(info.PostActions));
                if (postActionsArray != null)
                {
                    List<Guid> postActions = new List<Guid>();
                    foreach (JsonNode? item in postActionsArray)
                    {
                        if (item != null && Guid.TryParse(item.GetValue<string>(), out Guid id))
                        {
                            postActions.Add(id);
                        }
                    }
                    info.PostActions = postActions;
                }

                //read parameters
                JsonArray? constraintsArray = entry.Get<JsonArray>(nameof(info.Constraints));
                if (constraintsArray != null)
                {
                    List<TemplateConstraintInfo> constraints = new List<TemplateConstraintInfo>();
                    foreach (JsonNode? item in constraintsArray)
                    {
                        string? type = item.ToString(nameof(TemplateConstraintInfo.Type));
                        if (string.IsNullOrWhiteSpace(type))
                        {
                            throw new ArgumentException($"{nameof(entry)} has {nameof(info.Constraints)} property which item doesn't have {nameof(TemplateConstraintInfo.Type)}.", nameof(entry));
                        }
                        constraints.Add(new TemplateConstraintInfo(type!, item.ToString(nameof(TemplateConstraintInfo.Args))));
                    }
                    info.Constraints = constraints;
                }

                return info;
            }

            internal static TemplateInfo FromJsonElement(JsonElement entry)
            {
                string identity = GetStringValue(entry, nameof(Identity)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Identity)} property.", nameof(entry));
                string name = GetStringValue(entry, nameof(Name)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Name)} property.", nameof(entry));
                string mountPointUri = GetStringValue(entry, nameof(MountPointUri)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(MountPointUri)} property.", nameof(entry));
                string configPlace = GetStringValue(entry, nameof(ConfigPlace)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(ConfigPlace)} property.", nameof(entry));
                IReadOnlyList<string> shortNames = TryGetPropertyCaseInsensitive(entry, nameof(ShortNameList), out JsonElement shortNameToken)
                    ? GetStringCollection(shortNameToken)
                    : [];

                TemplateInfo info = new(identity, name, shortNames, mountPointUri, configPlace)
                {
                    Author = GetStringValue(entry, nameof(Author))
                };

                if (TryGetPropertyCaseInsensitive(entry, nameof(Classifications), out JsonElement classificationsToken)
                    && classificationsToken.ValueKind == JsonValueKind.Array)
                {
                    List<string> classifications = new();
                    foreach (JsonElement item in classificationsToken.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Null)
                        {
                            classifications.Add(item.GetString()!);
                        }
                    }
                    info.Classifications = classifications;
                }

                info.DefaultName = GetStringValue(entry, nameof(DefaultName));
                info.PreferDefaultName = GetBoolValue(entry, nameof(PreferDefaultName));
                info.Description = GetStringValue(entry, nameof(Description));
                info.GeneratorId = Guid.Parse(GetStringValue(entry, nameof(GeneratorId)));
                info.GroupIdentity = GetStringValue(entry, nameof(GroupIdentity));
                info.Precedence = GetInt32Value(entry, nameof(Precedence));
                info.LocaleConfigPlace = GetStringValue(entry, nameof(LocaleConfigPlace));
                info.HostConfigPlace = GetStringValue(entry, nameof(HostConfigPlace));
                info.ThirdPartyNotices = GetStringValue(entry, nameof(ThirdPartyNotices));

                Dictionary<string, IBaselineInfo> baselineInfo = new();
                if (TryGetPropertyCaseInsensitive(entry, nameof(ITemplateInfo.BaselineInfo), out JsonElement baselineToken)
                    && baselineToken.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty item in baselineToken.EnumerateObject())
                    {
                        if (item.Value.ValueKind == JsonValueKind.Null)
                        {
                            continue;
                        }

                        IReadOnlyDictionary<string, string> defaultOverrides = GetStringDictionary(item.Value, nameof(IBaselineInfo.DefaultOverrides));
                        baselineInfo.Add(item.Name, new BaselineInfo(defaultOverrides, GetStringValue(item.Value, nameof(IBaselineInfo.Description))));
                    }
                    info.BaselineInfo = baselineInfo;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                if (TryGetPropertyCaseInsensitive(entry, nameof(Parameters), out JsonElement parametersToken)
                    && parametersToken.ValueKind == JsonValueKind.Array)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    List<ITemplateParameter> templateParameters = new();
                    foreach (JsonElement item in parametersToken.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            templateParameters.Add(ParameterFromJsonElement(item));
                        }
                    }
                    info.ParameterDefinitions = new ParameterDefinitionSet(templateParameters);
                }

                if (TryGetPropertyCaseInsensitive(entry, nameof(TagsCollection), out JsonElement tagsToken)
                    && tagsToken.ValueKind == JsonValueKind.Object)
                {
                    Dictionary<string, string> tags = new();
                    foreach (JsonProperty item in tagsToken.EnumerateObject())
                    {
                        tags.Add(item.Name, item.Value.ValueKind == JsonValueKind.Null ? string.Empty : item.Value.GetString()!);
                    }
                    info.TagsCollection = tags;
                }

                info.HostData = GetStringValue(entry, nameof(info.HostData));
                if (TryGetPropertyCaseInsensitive(entry, nameof(info.PostActions), out JsonElement postActionsToken)
                    && postActionsToken.ValueKind == JsonValueKind.Array)
                {
                    List<Guid> postActions = new();
                    foreach (JsonElement item in postActionsToken.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Null && Guid.TryParse(item.GetString(), out Guid id))
                        {
                            postActions.Add(id);
                        }
                    }
                    info.PostActions = postActions;
                }

                if (TryGetPropertyCaseInsensitive(entry, nameof(info.Constraints), out JsonElement constraintsToken)
                    && constraintsToken.ValueKind == JsonValueKind.Array)
                {
                    List<TemplateConstraintInfo> constraints = new();
                    foreach (JsonElement item in constraintsToken.EnumerateArray())
                    {
                        string? type = GetStringValue(item, nameof(TemplateConstraintInfo.Type));
                        if (string.IsNullOrWhiteSpace(type))
                        {
                            throw new ArgumentException($"{nameof(entry)} has {nameof(info.Constraints)} property which item doesn't have {nameof(TemplateConstraintInfo.Type)}.", nameof(entry));
                        }
                        constraints.Add(new TemplateConstraintInfo(type, GetStringValue(item, nameof(TemplateConstraintInfo.Args))));
                    }
                    info.Constraints = constraints;
                }

                return info;
            }

            /// <summary>
            /// Parses <see cref="ITemplateParameter"/> from <see cref="JsonObject"/>.
            /// </summary>
            /// <param name="jObject"></param>
            private static ITemplateParameter ParameterFromJObject(JsonObject jObject)
            {
                string? name = jObject.ToString(nameof(ITemplateParameter.Name));
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{nameof(ITemplateParameter.Name)} property should not be null or whitespace", nameof(jObject));
                }

                string type = jObject.ToString(nameof(ITemplateParameter.Type)) ?? "parameter";
                string dataType = jObject.ToString(nameof(ITemplateParameter.DataType)) ?? "string";
                string? description = jObject.ToString(nameof(ITemplateParameter.Description));

                string? defaultValue = jObject.ToString(nameof(ITemplateParameter.DefaultValue));
                string? defaultIfOptionWithoutValue = jObject.ToString(nameof(ITemplateParameter.DefaultIfOptionWithoutValue));
                string? displayName = jObject.ToString(nameof(ITemplateParameter.DisplayName));
                bool isName = jObject.ToBool(nameof(ITemplateParameter.IsName));
                bool allowMultipleValues = jObject.ToBool(nameof(ITemplateParameter.AllowMultipleValues));

                Dictionary<string, ParameterChoice>? choices = null;

                if (dataType.Equals("choice", StringComparison.OrdinalIgnoreCase))
                {
                    choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                    JsonObject? cdToken = jObject.Get<JsonObject>(nameof(ITemplateParameter.Choices));
                    if (cdToken != null)
                    {
                        foreach (var cdPair in cdToken)
                        {
                            choices.Add(
                                cdPair.Key,
                                new ParameterChoice(
                                    cdPair.Value.ToString(nameof(ParameterChoice.DisplayName)),
                                    cdPair.Value.ToString(nameof(ParameterChoice.Description))));
                        }
                    }
                }

                TemplateParameterPrecedence precedence = jObject.ToTemplateParameterPrecedence(nameof(ITemplateParameter.Precedence));

                return new CacheTemplateParameter(
                    new TemplateParameter(name!, type, dataType)
                    {
                        DisplayName = displayName,
                        Precedence = precedence,
                        IsName = isName,
                        DefaultValue = defaultValue,
                        DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue,
                        Description = description,
                        AllowMultipleValues = allowMultipleValues,
                        Choices = choices
                    });
            }

            private static ITemplateParameter ParameterFromJsonElement(JsonElement element)
            {
                string? name = GetStringValue(element, nameof(ITemplateParameter.Name));
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{nameof(ITemplateParameter.Name)} property should not be null or whitespace", nameof(element));
                }

                string type = GetStringValue(element, nameof(ITemplateParameter.Type)) ?? "parameter";
                string dataType = GetStringValue(element, nameof(ITemplateParameter.DataType)) ?? "string";
                Dictionary<string, ParameterChoice>? choices = null;
                if (dataType.Equals("choice", StringComparison.OrdinalIgnoreCase))
                {
                    choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                    if (TryGetPropertyCaseInsensitive(element, nameof(ITemplateParameter.Choices), out JsonElement choicesToken)
                        && choicesToken.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty choice in choicesToken.EnumerateObject())
                        {
                            choices.Add(
                                choice.Name,
                                new ParameterChoice(
                                    GetStringValue(choice.Value, nameof(ParameterChoice.DisplayName)),
                                    GetStringValue(choice.Value, nameof(ParameterChoice.Description))));
                        }
                    }
                }

                return new CacheTemplateParameter(
                    new TemplateParameter(name, type, dataType)
                    {
                        DisplayName = GetStringValue(element, nameof(ITemplateParameter.DisplayName)),
                        Precedence = GetTemplateParameterPrecedence(element, nameof(ITemplateParameter.Precedence)),
                        IsName = GetBoolValue(element, nameof(ITemplateParameter.IsName)),
                        DefaultValue = GetStringValue(element, nameof(ITemplateParameter.DefaultValue)),
                        DefaultIfOptionWithoutValue = GetStringValue(element, nameof(ITemplateParameter.DefaultIfOptionWithoutValue)),
                        Description = GetStringValue(element, nameof(ITemplateParameter.Description)),
                        AllowMultipleValues = GetBoolValue(element, nameof(ITemplateParameter.AllowMultipleValues)),
                        Choices = choices
                    });
            }

            private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty(propertyName, out value))
                    {
                        return true;
                    }

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            value = property.Value;
                            return true;
                        }
                    }
                }

                value = default;
                return false;
            }

            private static string? GetStringValue(JsonElement element, string propertyName)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement value)
                    || value.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            }

            private static bool GetBoolValue(JsonElement element, string propertyName)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement value))
                {
                    return false;
                }

                return value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(value.GetString(), out bool result) && result,
                    _ => false
                };
            }

            private static int GetInt32Value(JsonElement element, string propertyName)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement value))
                {
                    return 0;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result))
                {
                    return result;
                }
                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out result))
                {
                    return result;
                }
                return 0;
            }

            private static IReadOnlyList<string> GetStringCollection(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    return new List<string> { element.GetString()! };
                }
                if (element.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                List<string> values = new();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        values.Add(item.GetString()!);
                    }
                }
                return values;
            }

            private static IReadOnlyDictionary<string, string> GetStringDictionary(JsonElement element, string propertyName)
            {
                Dictionary<string, string> result = new(StringComparer.Ordinal);
                if (TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement dictionaryToken)
                    && dictionaryToken.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in dictionaryToken.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            result[property.Name] = property.Value.GetString()!;
                        }
                    }
                }
                return result;
            }

            private static TemplateParameterPrecedence GetTemplateParameterPrecedence(JsonElement element, string propertyName)
            {
                if (!TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement precedenceToken))
                {
                    return TemplateParameterPrecedence.Default;
                }

                return new TemplateParameterPrecedence(
                    (PrecedenceDefinition)GetInt32Value(precedenceToken, nameof(PrecedenceDefinition)),
                    GetStringValue(precedenceToken, nameof(TemplateParameterPrecedence.IsRequiredCondition)),
                    GetStringValue(precedenceToken, nameof(TemplateParameterPrecedence.IsEnabledCondition)),
                    GetBoolValue(precedenceToken, nameof(TemplateParameterPrecedence.IsRequired)));
            }

            /// <summary>
            /// This class is overload on <see cref="ITemplateParameter"/> controlling JSON serialization for template parameters in cache.
            /// Not all the members are required to be serialized.
            /// </summary>
            private class CacheTemplateParameter : ITemplateParameter
            {
                private readonly ITemplateParameter _parameter;

                internal CacheTemplateParameter(ITemplateParameter parameter)
                {
                    _parameter = parameter;
                }

                public string? Description => _parameter.Description;

                [JsonPropertyName("Name")]
                public string Name => _parameter.Name;

                [JsonPropertyName("Precedence")]
                public TemplateParameterPrecedence Precedence => _parameter.Precedence;

                [JsonPropertyName("Type")]
                public string Type => _parameter.Type;

                [JsonPropertyName("IsName")]
                public bool IsName => _parameter.IsName;

                [JsonPropertyName("DefaultValue")]
                public string? DefaultValue => _parameter.DefaultValue;

                [JsonPropertyName("DefaultIfOptionWithoutValue")]
                public string? DefaultIfOptionWithoutValue => _parameter.DefaultIfOptionWithoutValue;

                [JsonPropertyName("DataType")]
                public string DataType => _parameter.DataType;

                [JsonPropertyName("Choices")]
                public IReadOnlyDictionary<string, ParameterChoice>? Choices => _parameter.Choices;

                [JsonPropertyName("DisplayName")]
                public string? DisplayName => _parameter.DisplayName;

                [JsonPropertyName("AllowMultipleValues")]
                public bool AllowMultipleValues => _parameter.AllowMultipleValues;

                [Obsolete]
                [JsonIgnore]
                public TemplateParameterPriority Priority => _parameter.Priority;

                [Obsolete]
                [JsonIgnore]
                public string? Documentation => _parameter.Documentation;

                public bool Equals(ITemplateParameter other) => _parameter.Equals(other);
            }
        }
    }
}
