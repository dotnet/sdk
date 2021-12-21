// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    [Obsolete]
    internal class LegacyBlobTemplateInfo : ITemplateInfo
    {
        public LegacyBlobTemplateInfo(ITemplateInfo templateInfo)
        {
            Author = templateInfo.Author;
            Classifications = templateInfo.Classifications;
            Description = templateInfo.Description;
            Identity = templateInfo.Identity;
            GroupIdentity = templateInfo.GroupIdentity;
            Precedence = templateInfo.Precedence;
            Name = templateInfo.Name;
            ShortNameList = templateInfo.ShortNameList;
            BaselineInfo = templateInfo.BaselineInfo;

            //new properties - not written to json
            Parameters = templateInfo.Parameters;
            TagsCollection = templateInfo.TagsCollection;
            PostActions = templateInfo.PostActions;

            //compatibility for old way to manage parameters
            if (templateInfo.Tags.Any())
            {
                Tags = templateInfo.Tags;
            }
            else
            {
                Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
                foreach (KeyValuePair<string, string> tag in TagsCollection)
                {
                    Dictionary<string, string> choices = new Dictionary<string, string>() { { tag.Value, string.Empty } };
                    tags[tag.Key] = new BlobLegacyCacheTag(null, choices, tag.Value, null);
                }
                foreach (ITemplateParameter choiceParam in Parameters.Where(param => param.IsChoice()))
                {
                    Dictionary<string, string> choices = new Dictionary<string, string>();
                    if (choiceParam.Choices != null)
                    {
                        foreach (var choice in choiceParam.Choices)
                        {
                            choices.Add(choice.Key, choice.Value.Description ?? string.Empty);
                        }
                    }
                    tags[choiceParam.Name] = new BlobLegacyCacheTag(choiceParam.Description, choices, choiceParam.DefaultValue, choiceParam.DefaultIfOptionWithoutValue);
                }
                Tags = tags;
            }

            if (templateInfo.CacheParameters.Any())
            {
                CacheParameters = templateInfo.CacheParameters;
            }
            else
            {
                Dictionary<string, ICacheParameter> cacheParameters = new Dictionary<string, ICacheParameter>();
                foreach (ITemplateParameter param in Parameters.Where(param => !param.IsChoice()))
                {
                    cacheParameters[param.Name] = new BlobLegacyCacheParameter(param.Description, param.DataType, param.DefaultValue, param.DefaultIfOptionWithoutValue);
                }
                CacheParameters = cacheParameters;
            }
        }

        [JsonProperty]
        public Guid ConfigMountPointId => Guid.Empty;

        [JsonProperty]
        public string? Author { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; private set; }

        [JsonProperty]
        public string? DefaultName => string.Empty;

        [JsonProperty]
        public string? Description { get; private set; }

        [JsonProperty]
        public string Identity { get; private set; }

        [JsonProperty]
        public Guid GeneratorId => Guid.Empty;

        [JsonProperty]
        public string? GroupIdentity { get; private set; }

        [JsonProperty]
        public int Precedence { get; private set; }

        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty]
        public string ShortName
        {
            get
            {
                if (ShortNameList.Count > 0)
                {
                    return ShortNameList[0];
                }

                return string.Empty;
            }

            set
            {
                if (ShortNameList.Count > 0)
                {
                    throw new Exception("Can't set the short name when the ShortNameList already has entries.");
                }

                ShortNameList = new List<string>() { value };
            }
        }

        [JsonProperty]
        public IReadOnlyList<string> ShortNameList { get; private set; }

        [JsonProperty]
        public string ConfigPlace => string.Empty;

        [JsonProperty]
        public Guid LocaleConfigMountPointId => Guid.Empty;

        [JsonProperty]
        public string? LocaleConfigPlace => string.Empty;

        [JsonProperty]
        public Guid HostConfigMountPointId => Guid.Empty;

        [JsonProperty]
        public string? HostConfigPlace => string.Empty;

        [JsonProperty]
        public string? ThirdPartyNotices { get; private set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; }

        [JsonProperty]
        public bool HasScriptRunningPostActions { get; set; }

        [JsonProperty]
        public DateTime? ConfigTimestampUtc { get; private set; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> TagsCollection { get; private set; }

        [JsonIgnore]
        public IReadOnlyList<ITemplateParameter> Parameters { get; private set; }

        [JsonIgnore]
        public string MountPointUri => string.Empty;

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; } = new Dictionary<string, ICacheTag>();

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; } = new Dictionary<string, ICacheParameter>();

        [JsonIgnore]
        public IReadOnlyList<Guid> PostActions { get; private set; }

        // ShortName should get deserialized when it exists, for backwards compat.
        // But moving forward, ShortNameList should be the definitive source.
        // It can still be ShortName in the template.json, but in the caches it'll be ShortNameList
        public bool ShouldSerializeShortName()
        {
            return false;
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
