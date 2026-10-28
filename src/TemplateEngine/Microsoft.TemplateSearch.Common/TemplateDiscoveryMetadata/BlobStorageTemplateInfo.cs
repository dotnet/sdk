// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("The class is deprecated. Use TemplateSearchCache instead to create search cache data. Deserialization code to be moved to TemplateSearchData.Json.")]
    internal class BlobStorageTemplateInfo : ITemplateInfo
    {
        public BlobStorageTemplateInfo(ITemplateInfo templateInfo)
        {
            if (templateInfo is null)
            {
                throw new ArgumentNullException(nameof(templateInfo));
            }
            if (string.IsNullOrWhiteSpace(templateInfo.Identity))
            {
                throw new ArgumentException($"'{nameof(templateInfo.Identity)}' cannot be null or whitespace.", nameof(templateInfo));
            }

            if (string.IsNullOrWhiteSpace(templateInfo.Name))
            {
                throw new ArgumentException($"'{nameof(templateInfo.Name)}' cannot be null or whitespace.", nameof(templateInfo));
            }

            if (!templateInfo.ShortNameList.Any())
            {
                throw new ArgumentException($"'{nameof(templateInfo.ShortNameList)}' should have at least one entry", nameof(templateInfo));
            }

            Identity = templateInfo.Identity;
            Name = templateInfo.Name;
            ShortNameList = templateInfo.ShortNameList;
            ParameterDefinitions = new ParameterDefinitionSet(templateInfo.ParameterDefinitions?.Select(p => new BlobTemplateParameter(p)));
            Author = templateInfo.Author;
            Classifications = templateInfo.Classifications ?? [];
            Description = templateInfo.Description;
            GroupIdentity = templateInfo.GroupIdentity;
            Precedence = templateInfo.Precedence;
            ThirdPartyNotices = templateInfo.ThirdPartyNotices;
            TagsCollection = templateInfo.TagsCollection ?? new Dictionary<string, string>();
            BaselineInfo = templateInfo.BaselineInfo ?? new Dictionary<string, IBaselineInfo>();
            PostActions = templateInfo.PostActions;
        }

        [System.Text.Json.Serialization.JsonConstructor]
        private BlobStorageTemplateInfo(string identity, string name, IEnumerable<string> shortNameList)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException($"'{nameof(identity)}' cannot be null or whitespace.", nameof(identity));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace.", nameof(name));
            }

            if (!shortNameList.Any())
            {
                throw new ArgumentException($"'{nameof(shortNameList)}' should have at least one entry", nameof(shortNameList));
            }

            Identity = identity;
            Name = name;
            ShortNameList = shortNameList.ToList();
        }

        [JsonPropertyName("Parameters")]
        //reading manually now to support old format
        public IParameterDefinitionSet ParameterDefinitions { get; private set; } = ParameterDefinitionSet.Empty;

        [JsonIgnore]
        [Obsolete("Use ParameterDefinitionSet instead.")]
        public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

        [JsonIgnore]
        string ITemplateLocator.MountPointUri => string.Empty;

        public string? Author { get; private set; }

        public IReadOnlyList<string> Classifications { get; private set; } = new List<string>();

        [JsonIgnore]
        public string DefaultName => string.Empty;

        public string? Description { get; private set; }

        public string Identity { get; private set; }

        [JsonIgnore]
        Guid ITemplateLocator.GeneratorId => Guid.Empty;

        public string? GroupIdentity { get; private set; }

        public int Precedence { get; private set; }

        public string Name { get; private set; }

        [JsonIgnore]
        [Obsolete("Use ShortNameList instead.")]
        string ITemplateInfo.ShortName => ShortNameList.Count > 0 ? ShortNameList[0] : string.Empty;

        public IReadOnlyList<string> ShortNameList { get; private set; }

        [JsonIgnore]
        public bool PreferDefaultName { get; private set; }

        [JsonIgnore]
        [Obsolete]
        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; } = new Dictionary<string, ICacheTag>();

        [JsonIgnore]
        [Obsolete]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; } = new Dictionary<string, ICacheParameter>();

        [JsonIgnore]
        string ITemplateLocator.ConfigPlace => string.Empty;

        [JsonIgnore]
        string IExtendedTemplateLocator.LocaleConfigPlace => string.Empty;

        [JsonIgnore]
        string IExtendedTemplateLocator.HostConfigPlace => string.Empty;

        public string? ThirdPartyNotices { get; private set; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; } = new Dictionary<string, IBaselineInfo>();

        [JsonIgnore]
        [Obsolete("This property is deprecated")]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        public IReadOnlyDictionary<string, string> TagsCollection { get; private set; } = new Dictionary<string, string>();

        public IReadOnlyList<Guid> PostActions { get; private set; } = [];

        [JsonIgnore]
        IReadOnlyList<TemplateConstraintInfo> ITemplateMetadata.Constraints => [];

        public static BlobStorageTemplateInfo FromJObject(JsonObject entry)
        {
            string identity = entry.ToString(nameof(Identity))
                ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Identity)} property.", nameof(entry));
            string name = entry.ToString(nameof(Name))
                ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Name)} property.", nameof(entry));

            JsonNode? shortNameToken = entry.Get<JsonNode>(nameof(ShortNameList));
            IEnumerable<string> shortNames = shortNameToken?.JTokenStringOrArrayToCollection([])
                ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(ShortNameList)} property.", nameof(entry));

            BlobStorageTemplateInfo info = new BlobStorageTemplateInfo(identity, name, shortNames)
            {
                Author = entry.ToString(nameof(Author))
            };
            JsonArray? classificationsArray = entry.Get<JsonArray>(nameof(Classifications));
            if (classificationsArray != null)
            {
                List<string> classifications = new List<string>();
                foreach (JsonNode? item in classificationsArray)
                {
                    classifications.Add(item?.ToString() ?? string.Empty);
                }
                info.Classifications = classifications;
            }
            info.Description = entry.ToString(nameof(Description));
            info.GroupIdentity = entry.ToString(nameof(GroupIdentity));
            info.Precedence = entry.ToInt32(nameof(Precedence));
            info.ThirdPartyNotices = entry.ToString(nameof(ThirdPartyNotices));

            JsonObject? baselineJObject = entry.Get<JsonObject>(nameof(ITemplateInfo.BaselineInfo));
            Dictionary<string, IBaselineInfo> baselineInfo = new Dictionary<string, IBaselineInfo>();
            if (baselineJObject != null)
            {
                foreach (var item in baselineJObject)
                {
                    IBaselineInfo baseline = new BaselineCacheInfo()
                    {
                        Description = item.Value.ToString(nameof(IBaselineInfo.Description)),
                        DefaultOverrides = item.Value?.ToStringDictionary(propertyName: nameof(IBaselineInfo.DefaultOverrides)) ?? new Dictionary<string, string>()
                    };
                    baselineInfo.Add(item.Key, baseline);
                }
                info.BaselineInfo = baselineInfo;
            }

            JsonArray? postActionsArray = entry.Get<JsonArray>(nameof(info.PostActions));
            if (postActionsArray != null)
            {
                List<Guid> postActions = new List<Guid>();
                foreach (JsonNode? item in postActionsArray)
                {
                    if (Guid.TryParse(item?.ToString(), out Guid id))
                    {
                        postActions.Add(id);
                    }
                }
                info.PostActions = postActions;
            }

            //read parameters
            bool readParameters = false;
            List<ITemplateParameter> templateParameters = new List<ITemplateParameter>();
            JsonArray? parametersArray = entry.Get<JsonArray>(nameof(Parameters));
            if (parametersArray != null)
            {
                foreach (JsonNode? item in parametersArray)
                {
                    if (item is JsonObject jObj)
                    {
                        templateParameters.Add(new BlobTemplateParameter(jObj));
                    }
                }
                readParameters = true;
            }

            JsonObject? tagsObject = entry.Get<JsonObject>(nameof(TagsCollection));
            Dictionary<string, string> tags = new Dictionary<string, string>();
            if (tagsObject != null)
            {
                foreach (var item in tagsObject)
                {
                    tags.Add(item.Key, item.Value?.ToString() ?? string.Empty);
                }
            }

            //try read tags and parameters - for compatibility reason
            tagsObject = entry.Get<JsonObject>("tags");
            if (tagsObject != null)
            {
                Dictionary<string, ICacheTag> legacyTags = new Dictionary<string, ICacheTag>();
                foreach (var item in tagsObject)
                {
                    if (item.Value is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
                    {
                        tags[item.Key] = item.Value.ToString();
                        legacyTags[item.Key] = new BlobLegacyCacheTag(
                            description: null,
                            choicesAndDescriptions: new Dictionary<string, string>()
                            {
                                { item.Value.ToString(), string.Empty }
                            },
                            defaultValue: item.Value.ToString(),
                            defaultIfOptionWithoutValue: null);
                    }
                    else if (item.Value is JsonObject tagObj)
                    {
                        JsonObject? choicesObject = tagObj.Get<JsonObject>("ChoicesAndDescriptions");
                        if (choicesObject != null && !readParameters)
                        {
                            Dictionary<string, ParameterChoice> choicesAndDescriptions = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                            Dictionary<string, string> legacyChoices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var cdPair in choicesObject)
                            {
                                choicesAndDescriptions[cdPair.Key] = new ParameterChoice(null, cdPair.Value?.ToString() ?? string.Empty);
                                legacyChoices[cdPair.Key] = cdPair.Value?.ToString() ?? string.Empty;
                            }
                            templateParameters.Add(
                                new BlobTemplateParameter(item.Key, "choice")
                                {
                                    Choices = choicesAndDescriptions
                                });
                            legacyTags[item.Key] = new BlobLegacyCacheTag(
                              description: tagObj.ToString("description"),
                              choicesAndDescriptions: legacyChoices,
                              defaultValue: tagObj.ToString("defaultValue"),
                              defaultIfOptionWithoutValue: tagObj.ToString("defaultIfOptionWithoutValue"));
                        }
                        tags[item.Key] = tagObj.ToString("defaultValue") ?? string.Empty;
                    }
                }
                info.Tags = legacyTags;
            }
            JsonObject? cacheParametersObject = entry.Get<JsonObject>("cacheParameters");
            if (!readParameters && cacheParametersObject != null)
            {
                Dictionary<string, ICacheParameter> legacyParams = new Dictionary<string, ICacheParameter>();
                foreach (var item in cacheParametersObject)
                {
                    if (item.Value is not JsonObject paramObj)
                    {
                        continue;
                    }
                    string dataType = paramObj.ToString(nameof(BlobTemplateParameter.DataType)) ?? "string";
                    templateParameters.Add(new BlobTemplateParameter(item.Key, dataType));
                    legacyParams[item.Key] = new BlobLegacyCacheParameter(
                        description: paramObj.ToString("description"),
                        dataType: paramObj.ToString(nameof(BlobTemplateParameter.DataType)) ?? "string",
                        defaultValue: paramObj.ToString("defaultValue"),
                        defaultIfOptionWithoutValue: paramObj.ToString("defaultIfOptionWithoutValue"));
                }
                info.CacheParameters = legacyParams;
            }

            info.TagsCollection = tags;
            info.ParameterDefinitions = new ParameterDefinitionSet(templateParameters);
            return info;

        }

        private class BaselineCacheInfo : IBaselineInfo
        {
            public string? Description { get; set; }

            public IReadOnlyDictionary<string, string> DefaultOverrides { get; set; } = new Dictionary<string, string>();
        }

        private class BlobTemplateParameter : ITemplateParameter
        {
            internal BlobTemplateParameter(ITemplateParameter parameter)
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }

                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    throw new ArgumentException($"{nameof(Name)} property should not be null or whitespace", nameof(parameter));
                }
                Name = parameter.Name;
                DataType = !string.IsNullOrWhiteSpace(parameter.DataType) ? parameter.DataType : "string";
                Choices = parameter.Choices;

                if (DataType.Equals("choice", StringComparison.OrdinalIgnoreCase) && Choices == null)
                {
                    Choices = new Dictionary<string, ParameterChoice>();
                }

                DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue;
                Description = parameter.Description;
                AllowMultipleValues = parameter.AllowMultipleValues;
                Precedence = parameter.Precedence;
            }

            internal BlobTemplateParameter(string name, string dataType)
            {
                Name = name;
                DataType = dataType;
                Precedence = TemplateParameterPrecedence.Default;
            }

            internal BlobTemplateParameter(JsonObject jObject)
            {
                string? name = jObject.ToString(nameof(Name));
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{nameof(Name)} property should not be null or whitespace", nameof(jObject));
                }

                Name = name!;
                DataType = jObject.ToString(nameof(DataType)) ?? "string";

                if (DataType.Equals("choice", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, ParameterChoice> choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                    JsonObject? cdToken = jObject.Get<JsonObject>(nameof(Choices));
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
                    Choices = choices;
                }
                DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
                Description = jObject.ToString(nameof(Description));
                AllowMultipleValues = jObject.ToBool(nameof(AllowMultipleValues));

                //We currently do not write the precedence to cache - so this code is redundant.
                // However should we decide in future to populate it, this way the client code can consume it without the need to be updated
                Precedence = jObject.ToTemplateParameterPrecedence(nameof(Precedence));
            }

            public string Name { get; internal set; }

            public string DataType { get; internal set; }

            public IReadOnlyDictionary<string, ParameterChoice>? Choices { get; internal set; }

            [JsonIgnore]
            [Obsolete("Use Precedence instead.")]
            public TemplateParameterPriority Priority => Precedence.PrecedenceDefinition.ToTemplateParameterPriority();

            [JsonIgnore]
            public TemplateParameterPrecedence Precedence { get; }

            [JsonIgnore]
            //ParameterDefinitionSet have only "parameter" symbols.
            string ITemplateParameter.Type => "parameter";

            [JsonIgnore]
            bool ITemplateParameter.IsName => false;

            public string? DefaultValue { get; internal set; }

            [JsonIgnore]
            string ITemplateParameter.DisplayName => string.Empty;

            public string? DefaultIfOptionWithoutValue { get; internal set; }

            public string? Description { get; internal set; }

            [Obsolete]
            [JsonIgnore]
            string ITemplateParameter.Documentation => string.Empty;

            public bool AllowMultipleValues { get; internal set; }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj is ITemplateParameter parameter)
                {
                    return Equals(parameter);
                }

                return false;
            }

            public override int GetHashCode() => Name != null ? Name.GetHashCode() : 0;

            public bool Equals(ITemplateParameter other) => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(other.Name) && Name == other.Name;
        }

        private class BlobLegacyCacheTag : ICacheTag
        {
            public BlobLegacyCacheTag(string? description, IReadOnlyDictionary<string, string> choicesAndDescriptions, string? defaultValue, string? defaultIfOptionWithoutValue)
            {
                Description = description;
                ChoicesAndDescriptions = choicesAndDescriptions;
                DefaultValue = defaultValue;
                DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
            }

            public string? Description { get; }

            public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

            public string? DefaultValue { get; }

            public string? DefaultIfOptionWithoutValue { get; }

            [JsonIgnore]
            public string DisplayName => string.Empty;

            [JsonIgnore]
            public IReadOnlyDictionary<string, ParameterChoice> Choices => new Dictionary<string, ParameterChoice>();

        }

        private class BlobLegacyCacheParameter : ICacheParameter
        {
            public BlobLegacyCacheParameter(string? description, string? dataType, string? defaultValue, string? defaultIfOptionWithoutValue)
            {
                Description = description;
                DataType = dataType;
                DefaultValue = defaultValue;
                DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
            }

            public string? DataType { get; }

            public string? DefaultValue { get; }

            public string? Description { get; }

            public string? DefaultIfOptionWithoutValue { get; }

            [JsonIgnore]
            public string DisplayName => string.Empty;
        }

    }
}
