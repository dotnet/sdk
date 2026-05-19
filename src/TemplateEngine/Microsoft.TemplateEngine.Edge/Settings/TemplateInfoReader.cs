// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
