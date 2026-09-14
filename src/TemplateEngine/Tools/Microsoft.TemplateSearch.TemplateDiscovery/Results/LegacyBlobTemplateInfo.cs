// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

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
            ParameterDefinitions = templateInfo.ParameterDefinitions;
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
                foreach (ITemplateParameter choiceParam in ParameterDefinitions.Where(param => param.IsChoice()))
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
                foreach (ITemplateParameter param in ParameterDefinitions.Where(param => !param.IsChoice()))
                {
                    cacheParameters[param.Name] = new BlobLegacyCacheParameter(param.Description, param.DataType, param.DefaultValue, param.DefaultIfOptionWithoutValue);
                }
                CacheParameters = cacheParameters;
            }
        }

        public Guid ConfigMountPointId => Guid.Empty;

        public string? Author { get; private set; }

        public IReadOnlyList<string> Classifications { get; private set; }

        public string DefaultName => string.Empty;

        public string? Description { get; private set; }

        public string Identity { get; private set; }

        public Guid GeneratorId => Guid.Empty;

        public string? GroupIdentity { get; private set; }

        public int Precedence { get; private set; }

        public string Name { get; private set; }

        [JsonIgnore]
        public bool PreferDefaultName { get; }

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

        public string ConfigPlace => string.Empty;

        public Guid LocaleConfigMountPointId => Guid.Empty;

        public string LocaleConfigPlace => string.Empty;

        public Guid HostConfigMountPointId => Guid.Empty;

        public string HostConfigPlace => string.Empty;

        public string? ThirdPartyNotices { get; private set; }

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; private set; }

        public bool HasScriptRunningPostActions { get; set; }

        public DateTime? ConfigTimestampUtc { get; private set; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> TagsCollection { get; }

        [JsonIgnore]
        public IParameterDefinitionSet ParameterDefinitions { get; }

        [JsonIgnore]
        [Obsolete("Use ParameterDefinitionSet instead.")]
        public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

        [JsonIgnore]
        public string MountPointUri => string.Empty;

        public IReadOnlyDictionary<string, ICacheTag> Tags { get; private set; } = new Dictionary<string, ICacheTag>();

        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; private set; } = new Dictionary<string, ICacheParameter>();

        [JsonIgnore]
        public IReadOnlyList<Guid> PostActions { get; }

        [JsonIgnore]
        IReadOnlyList<TemplateConstraintInfo> ITemplateMetadata.Constraints => [];

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

            public string? Description { get; }

            public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

            public string? DefaultValue { get; }

            public string? DefaultIfOptionWithoutValue { get; }

            [JsonIgnore]
            public string? DisplayName => null;

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
