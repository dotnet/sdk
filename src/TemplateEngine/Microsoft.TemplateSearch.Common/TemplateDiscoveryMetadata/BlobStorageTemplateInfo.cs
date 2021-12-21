// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    [JsonObject(Id = "TemplateInfo")]
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
            Parameters = templateInfo.Parameters?.Select(p => new BlobTemplateParameter(p)).ToArray() ?? Array.Empty<BlobTemplateParameter>();
            Author = templateInfo.Author;
            Classifications = templateInfo.Classifications ?? Array.Empty<string>();
            Description = templateInfo.Description;
            GroupIdentity = templateInfo.GroupIdentity;
            Precedence = templateInfo.Precedence;
            ThirdPartyNotices = templateInfo.ThirdPartyNotices;
            TagsCollection = templateInfo.TagsCollection ?? new Dictionary<string, string>();
            BaselineInfo = templateInfo.BaselineInfo ?? new Dictionary<string, IBaselineInfo>();
            PostActions = templateInfo.PostActions;
        }

        [JsonConstructor]
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

        [JsonProperty]
        //reading manually now to support old format
        public IReadOnlyList<ITemplateParameter> Parameters { get; private set; } = new List<ITemplateParameter>();

        [JsonIgnore]
        string ITemplateInfo.MountPointUri => throw new NotImplementedException();

        [JsonProperty]
        public string? Author { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; private set; } = new List<string>();

        [JsonIgnore]
        public string? DefaultName => throw new NotImplementedException();

        [JsonProperty]
        public string? Description { get; private set; }

        [JsonProperty]
        public string Identity { get; private set; }

        [JsonIgnore]
        Guid ITemplateInfo.GeneratorId => throw new NotImplementedException();

        [JsonProperty]
        public string? GroupIdentity { get; private set; }

        [JsonProperty]
        public int Precedence { get; private set; }

        [JsonProperty]
        public string Name { get; private set; }

        [JsonIgnore]
        [Obsolete("Use ShortNameList instead.")]
        string ITemplateInfo.ShortName => throw new NotImplementedException();

        [JsonProperty]
        public IReadOnlyList<string> ShortNameList { get; private set; }

        [JsonIgnore]
        [Obsolete]
        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; } = new Dictionary<string, ICacheTag>();

        [JsonIgnore]
        [Obsolete]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; } = new Dictionary<string, ICacheParameter>();

        [JsonIgnore]
        string ITemplateInfo.ConfigPlace => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.LocaleConfigPlace => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.HostConfigPlace => throw new NotImplementedException();

        [JsonProperty]
        public string? ThirdPartyNotices { get; private set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; } = new Dictionary<string, IBaselineInfo>();

        [JsonIgnore]
        [Obsolete("This property is deprecated")]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, string> TagsCollection { get; private set; } = new Dictionary<string, string>();

        [JsonProperty]
        public IReadOnlyList<Guid> PostActions { get; private set; } = Array.Empty<Guid>();

        public static BlobStorageTemplateInfo FromJObject(JObject entry)
        {
            string identity = entry.ToString(nameof(Identity))
                ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Identity)} property.", nameof(entry));
            string name = entry.ToString(nameof(Name))
                ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Name)} property.", nameof(entry));

            JToken? shortNameToken = entry.Get<JToken>(nameof(ShortNameList));
            IEnumerable<string> shortNames = shortNameToken?.JTokenStringOrArrayToCollection(Array.Empty<string>())
                ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(ShortNameList)} property.", nameof(entry));

            BlobStorageTemplateInfo info = new BlobStorageTemplateInfo(identity, name, shortNames);
            info.Author = entry.ToString(nameof(Author));
            JArray? classificationsArray = entry.Get<JArray>(nameof(Classifications));
            if (classificationsArray != null)
            {
                List<string> classifications = new List<string>();
                foreach (JToken item in classificationsArray)
                {
                    classifications.Add(item.ToString());
                }
                info.Classifications = classifications;
            }
            info.Description = entry.ToString(nameof(Description));
            info.GroupIdentity = entry.ToString(nameof(GroupIdentity));
            info.Precedence = entry.ToInt32(nameof(Precedence));
            info.ThirdPartyNotices = entry.ToString(nameof(ThirdPartyNotices));

            JObject? baselineJObject = entry.Get<JObject>(nameof(ITemplateInfo.BaselineInfo));
            Dictionary<string, IBaselineInfo> baselineInfo = new Dictionary<string, IBaselineInfo>();
            if (baselineJObject != null)
            {
                foreach (JProperty item in baselineJObject.Properties())
                {
                    IBaselineInfo baseline = new BaselineCacheInfo()
                    {
                        Description = item.Value.ToString(nameof(IBaselineInfo.Description)),
                        DefaultOverrides = item.Value.ToStringDictionary(propertyName: nameof(IBaselineInfo.DefaultOverrides))
                    };
                    baselineInfo.Add(item.Name, baseline);
                }
                info.BaselineInfo = baselineInfo;
            }

            JArray? postActionsArray = entry.Get<JArray>(nameof(info.PostActions));
            if (postActionsArray != null)
            {
                List<Guid> postActions = new List<Guid>();
                foreach (JToken item in postActionsArray)
                {
                    if (Guid.TryParse(item.ToString(), out Guid id))
                    {
                        postActions.Add(id);
                    }
                }
                info.PostActions = postActions;
            }

            //read parameters
            bool readParameters = false;
            List<ITemplateParameter> templateParameters = new List<ITemplateParameter>();
            JArray? parametersArray = entry.Get<JArray>(nameof(Parameters));
            if (parametersArray != null)
            {
                foreach (JObject item in parametersArray)
                {
                    templateParameters.Add(new BlobTemplateParameter(item));
                }
                readParameters = true;
            }

            JObject? tagsObject = entry.Get<JObject>(nameof(TagsCollection));
            Dictionary<string, string> tags = new Dictionary<string, string>();
            if (tagsObject != null)
            {
                foreach (JProperty item in tagsObject.Properties())
                {
                    tags.Add(item.Name.ToString(), item.Value.ToString());
                }
            }

            //try read tags and parameters - for compatibility reason
            tagsObject = entry.Get<JObject>("tags");
            if (tagsObject != null)
            {
                Dictionary<string, ICacheTag> legacyTags = new Dictionary<string, ICacheTag>();
                foreach (JProperty item in tagsObject.Properties())
                {
                    if (item.Value.Type == JTokenType.String)
                    {
                        tags[item.Name.ToString()] = item.Value.ToString();
                        legacyTags[item.Name.ToString()] = new BlobLegacyCacheTag(
                            description: null,
                            choicesAndDescriptions: new Dictionary<string, string>()
                            {
                                { item.Value.ToString(), string.Empty }
                            },
                            defaultValue: item.Value.ToString(),
                            defaultIfOptionWithoutValue: null);
                    }
                    else if (item.Value is JObject tagObj)
                    {
                        JObject? choicesObject = tagObj.Get<JObject>("ChoicesAndDescriptions");
                        if (choicesObject != null && !readParameters)
                        {
                            Dictionary<string, ParameterChoice> choicesAndDescriptions = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                            Dictionary<string, string> legacyChoices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (JProperty cdPair in choicesObject.Properties())
                            {
                                choicesAndDescriptions[cdPair.Name.ToString()] = new ParameterChoice(null, cdPair.Value.ToString());
                                legacyChoices[cdPair.Name.ToString()] = cdPair.Value.ToString();
                            }
                            templateParameters.Add(
                                new BlobTemplateParameter(item.Name.ToString(), "choice")
                                {
                                    Choices = choicesAndDescriptions
                                });
                            legacyTags[item.Name.ToString()] = new BlobLegacyCacheTag(
                              description: tagObj.ToString("description"),
                              choicesAndDescriptions: legacyChoices,
                              defaultValue: tagObj.ToString("defaultValue"),
                              defaultIfOptionWithoutValue: tagObj.ToString("defaultIfOptionWithoutValue"));
                        }
                        tags[item.Name.ToString()] = tagObj.ToString("defaultValue") ?? "";
                    }
                }
                info.Tags = legacyTags;
            }
            JObject? cacheParametersObject = entry.Get<JObject>("cacheParameters");
            if (!readParameters && cacheParametersObject != null)
            {
                Dictionary<string, ICacheParameter> legacyParams = new Dictionary<string, ICacheParameter>();
                foreach (JProperty item in cacheParametersObject.Properties())
                {
                    JObject paramObj = (JObject)item.Value;
                    if (paramObj == null)
                    {
                        continue;
                    }
                    string dataType = paramObj.ToString(nameof(BlobTemplateParameter.DataType)) ?? "string";
                    templateParameters.Add(new BlobTemplateParameter(item.Name.ToString(), dataType));
                    legacyParams[item.Name.ToString()] = new BlobLegacyCacheParameter(
                        description: paramObj.ToString("description"),
                        dataType: paramObj.ToString(nameof(BlobTemplateParameter.DataType)) ?? "string",
                        defaultValue: paramObj.ToString("defaultValue"),
                        defaultIfOptionWithoutValue: paramObj.ToString("defaultIfOptionWithoutValue"));
                }
                info.CacheParameters = legacyParams;
            }

            info.TagsCollection = tags;
            info.Parameters = templateParameters;
            return info;

        }

        private class BaselineCacheInfo : IBaselineInfo
        {
            [JsonProperty]
            public string? Description { get; set; }

            [JsonProperty]
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

                Priority = parameter.Priority;
                DefaultIfOptionWithoutValue = parameter.DefaultIfOptionWithoutValue;
                Description = parameter.Description;
            }

            internal BlobTemplateParameter(string name, string dataType)
            {
                Name = name;
                DataType = dataType;
            }

            internal BlobTemplateParameter(JObject jObject)
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
                    JObject? cdToken = jObject.Get<JObject>(nameof(Choices));
                    if (cdToken != null)
                    {
                        foreach (JProperty cdPair in cdToken.Properties())
                        {
                            choices.Add(
                                cdPair.Name.ToString(),
                                new ParameterChoice(
                                    cdPair.Value.ToString(nameof(ParameterChoice.DisplayName)),
                                    cdPair.Value.ToString(nameof(ParameterChoice.Description))));
                        }
                    }
                    Choices = choices;
                }
                Priority = jObject.ToEnum<TemplateParameterPriority>(nameof(Priority));
                DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
                Description = jObject.ToString(nameof(Description));
            }

            [JsonProperty]
            public string Name { get; internal set; }

            [JsonProperty]
            public string DataType { get; internal set; }

            [JsonProperty]
            public IReadOnlyDictionary<string, ParameterChoice>? Choices { get; internal set; }

            [JsonProperty]
            public TemplateParameterPriority Priority { get; internal set; }

            [JsonIgnore]
            string ITemplateParameter.Type => throw new NotImplementedException();

            [JsonIgnore]
            bool ITemplateParameter.IsName => false;

            [JsonProperty]
            public string? DefaultValue { get; internal set; }

            [JsonIgnore]
            string? ITemplateParameter.DisplayName => throw new NotImplementedException();

            [JsonProperty]
            public string? DefaultIfOptionWithoutValue { get; internal set; }

            [JsonProperty]
            public string? Description { get; internal set; }

            [Obsolete]
            [JsonIgnore]
            string? ITemplateParameter.Documentation => throw new NotImplementedException();
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

            [JsonProperty]
            public string? Description { get; }

            [JsonProperty]
            public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

            [JsonProperty]
            public string? DefaultValue { get; }

            [JsonProperty]
            public string? DefaultIfOptionWithoutValue { get; }

            [JsonIgnore]
            public string? DisplayName => throw new NotImplementedException();

            [JsonIgnore]
            public IReadOnlyDictionary<string, ParameterChoice> Choices => throw new NotImplementedException();

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

            [JsonProperty]
            public string? DataType { get; }

            [JsonProperty]
            public string? DefaultValue { get; }

            [JsonProperty]
            public string? Description { get; }

            [JsonProperty]
            public string? DefaultIfOptionWithoutValue { get; }

            [JsonIgnore]
            public string? DisplayName => throw new NotImplementedException();
        }

    }
}
