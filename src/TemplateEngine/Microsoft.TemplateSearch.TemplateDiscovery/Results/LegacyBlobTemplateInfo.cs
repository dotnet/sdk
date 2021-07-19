// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    internal class LegacyBlobTemplateInfo : ITemplateInfo
    {
        public LegacyBlobTemplateInfo(ITemplateInfo templateInfo)
        {
            Author = templateInfo.Author;
            Classifications = templateInfo.Classifications;
            DefaultName = templateInfo.DefaultName;
            Description = templateInfo.Description;
            Identity = templateInfo.Identity;
            GeneratorId = templateInfo.GeneratorId;
            GroupIdentity = templateInfo.GroupIdentity;
            Precedence = templateInfo.Precedence;
            Name = templateInfo.Name;
            ShortNameList = templateInfo.ShortNameList;
            ConfigPlace = templateInfo.ConfigPlace;
            LocaleConfigPlace = templateInfo.LocaleConfigPlace;
            HostConfigPlace = templateInfo.HostConfigPlace;
            ThirdPartyNotices = templateInfo.ThirdPartyNotices;
            BaselineInfo = templateInfo.BaselineInfo;

            //new properties - not written to json
            MountPointUri = templateInfo.MountPointUri;
            Parameters = templateInfo.Parameters;
            TagsCollection = templateInfo.TagsCollection;
#pragma warning disable CS0618 // Type or member is obsolete
            Tags = templateInfo.Tags;
            CacheParameters = templateInfo.CacheParameters;
#pragma warning restore CS0618 // Type or member is obsolete

            //compatibility for old way to manage parameters
            Dictionary<string, LegacyCacheTag> tags = new Dictionary<string, LegacyCacheTag>();
            foreach (KeyValuePair<string, string> tag in TagsCollection)
            {
                Dictionary<string, string> choices = new Dictionary<string, string>();
                choices.Add(tag.Value, "");
                tags[tag.Key] = new LegacyCacheTag(null, choices, tag.Value);
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
                tags[choiceParam.Name] = new LegacyCacheTag(choiceParam.Description, choices, choiceParam.DefaultValue, choiceParam.DefaultIfOptionWithoutValue);
            }
            LegacyTags = tags;

            Dictionary<string, LegacyCacheParameter> cacheParameters = new Dictionary<string, LegacyCacheParameter>();
            foreach (ITemplateParameter param in Parameters.Where(param => !param.IsChoice()))
            {
                cacheParameters[param.Name] = new LegacyCacheParameter()
                {
                    DataType = param.DataType,
                    DefaultIfOptionWithoutValue = param.DefaultIfOptionWithoutValue,
                    DefaultValue = param.DefaultValue,
                    Description = param.Description
                };
            }
            LegacyCacheParameters = cacheParameters;
        }

        [JsonProperty]
        public Guid ConfigMountPointId { get; set; }

        [JsonProperty]
        public string? Author { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; private set; }

        [JsonProperty]
        public string? DefaultName { get; private set; }

        [JsonProperty]
        public string? Description { get; private set; }

        [JsonProperty]
        public string Identity { get; private set; }

        [JsonProperty]
        public Guid GeneratorId { get; private set; }

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

        public IReadOnlyList<string> ShortNameList { get; private set; }

        [JsonProperty(nameof(Tags))]
        public IReadOnlyDictionary<string, LegacyCacheTag> LegacyTags { get; private set; }

        [JsonProperty(nameof(CacheParameters))]
        public IReadOnlyDictionary<string, LegacyCacheParameter> LegacyCacheParameters { get; private set; }

        [JsonProperty]
        public string ConfigPlace { get; private set; }

        [JsonProperty]
        public Guid LocaleConfigMountPointId { get; private set; }

        [JsonProperty]
        public string? LocaleConfigPlace { get; private set; }

        [JsonProperty]
        public Guid HostConfigMountPointId { get; private set; }

        [JsonProperty]
        public string? HostConfigPlace { get; private set; }

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
        public string MountPointUri { get; private set; }

        [JsonIgnore]
#pragma warning disable CS0618 // Type or member is obsolete
        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; }
#pragma warning restore CS0618 // Type or member is obsolete

    [JsonIgnore]
#pragma warning disable CS0618 // Type or member is obsolete
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; }
#pragma warning restore CS0618 // Type or member is obsolete

        // ShortName should get deserialized when it exists, for backwards compat.
        // But moving forward, ShortNameList should be the definitive source.
        // It can still be ShortName in the template.json, but in the caches it'll be ShortNameList
        public bool ShouldSerializeShortName()
        {
            return false;
        }

        internal class LegacyCacheTag
        {
            public LegacyCacheTag(string? description, IReadOnlyDictionary<string, string> choicesAndDescriptions, string defaultValue)
                : this(description, choicesAndDescriptions, defaultValue, null)
            {
            }

            public LegacyCacheTag(string? description, IReadOnlyDictionary<string, string> choicesAndDescriptions, string? defaultValue, string? defaultIfOptionWithoutValue)
            {
                Description = description;
                ChoicesAndDescriptions = choicesAndDescriptions.CloneIfDifferentComparer(StringComparer.OrdinalIgnoreCase);
                DefaultValue = defaultValue;
                DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
            }

            public string? Description { get; }

            public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

            public string? DefaultValue { get; }

            public string? DefaultIfOptionWithoutValue { get; set; }

            public bool ShouldSerializeDefaultIfOptionWithoutValue()
            {
                return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
            }
        }

        internal class LegacyCacheParameter
        {
            public string? DataType { get; set; }

            public string? DefaultValue { get; set; }

            public string? Description { get; set; }

            public string? DefaultIfOptionWithoutValue { get; set; }

            public bool ShouldSerializeDefaultIfOptionWithoutValue()
            {
                return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
            }
        }
    }
}
