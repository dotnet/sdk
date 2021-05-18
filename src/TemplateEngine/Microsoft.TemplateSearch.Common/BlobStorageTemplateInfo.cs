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
    public class BlobStorageTemplateInfo : ITemplateInfo
    {
        [JsonProperty(PropertyName = "BaselineInfo")]
        private IReadOnlyDictionary<string, BaselineCacheInfo> _baselineInfo = new Dictionary<string, BaselineCacheInfo>();

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

        [JsonIgnore]
        //reading manually now to support old format
        public IReadOnlyList<ITemplateParameter> Parameters { get; private set; } = new List<ITemplateParameter>();

        [JsonIgnore]
        string ITemplateInfo.MountPointUri => throw new NotImplementedException();

        [JsonProperty]
        public string? Author { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; private set; } = new List<string>();

        [JsonProperty]
        public string? DefaultName { get; private set; }

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
        IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags => throw new NotImplementedException();

        [JsonIgnore]
        [Obsolete]
        IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.ConfigPlace => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.LocaleConfigPlace => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.HostConfigPlace => throw new NotImplementedException();

        [JsonProperty]
        public string? ThirdPartyNotices { get; private set; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _baselineInfo.ToDictionary(kvp => kvp.Key, kvp => (IBaselineInfo)kvp.Value);

        [JsonIgnore]
        [Obsolete("This property is deprecated")]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        public IReadOnlyDictionary<string, string> TagsCollection { get; private set; } = new Dictionary<string, string>();

        public static BlobStorageTemplateInfo FromJObject(JObject entry)
        {
            BlobStorageTemplateInfo info = entry.ToObject<BlobStorageTemplateInfo>();

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

            //try read tags and parameters - for compatibility reason
            JObject? tagsObject = entry.Get<JObject>("tags");
            if (tagsObject != null)
            {
                Dictionary<string, string> tags = new Dictionary<string, string>();
                info.TagsCollection = tags;
                foreach (JProperty item in tagsObject.Properties())
                {
                    if (item.Value.Type == JTokenType.String)
                    {
                        tags[item.Name.ToString()] = item.Value.ToString();
                    }
                    else if (item.Value is JObject tagObj)
                    {
                        JObject? choicesObject = tagObj.Get<JObject>("ChoicesAndDescriptions");
                        if (choicesObject != null && !readParameters)
                        {
                            Dictionary<string, ParameterChoice> choicesAndDescriptions = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                            foreach (JProperty cdPair in choicesObject.Properties())
                            {
                                choicesAndDescriptions.Add(cdPair.Name.ToString(), new ParameterChoice(null, cdPair.Value.ToString()));
                            }
                            templateParameters.Add(
                                new BlobTemplateParameter(item.Name.ToString(), "parameter", "choice")
                                {
                                    Choices = choicesAndDescriptions,
                                    Description = tagObj.ToString(nameof(BlobTemplateParameter.Description)),
                                    DefaultValue = tagObj.ToString(nameof(BlobTemplateParameter.DefaultValue)),
                                });
                        }
                        tags[item.Name.ToString()] = tagObj.ToString(nameof(BlobTemplateParameter.DefaultValue)) ?? "";
                    }
                }
            }
            JObject? cacheParametersObject = entry.Get<JObject>("cacheParameters");
            if (!readParameters && cacheParametersObject != null)
            {
                foreach (JProperty item in cacheParametersObject.Properties())
                {
                    JObject paramObj = (JObject)item.Value;
                    if (paramObj == null)
                    {
                        continue;
                    }
                    string dataType = paramObj.ToString(nameof(BlobTemplateParameter.DataType)) ?? "string";
                    templateParameters.Add(
                        new BlobTemplateParameter(item.Name.ToString(), "parameter", dataType)
                        {
                            Description = paramObj.ToString(nameof(BlobTemplateParameter.Description)),
                            DefaultValue = paramObj.ToString(nameof(BlobTemplateParameter.DefaultValue)),
                        });
                }
            }
            info.Parameters = templateParameters;
            return info;

        }

        internal class BaselineCacheInfo : IBaselineInfo
        {
            [JsonProperty]
            public string? Description { get; private set; }

            [JsonProperty]
            public IReadOnlyDictionary<string, string> DefaultOverrides { get; private set; } = new Dictionary<string, string>();
        }

        internal class BlobTemplateParameter : ITemplateParameter
        {
            internal BlobTemplateParameter(string name, string type, string dataType)
            {
                Name = name;
                Type = type;
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
                Type = jObject.ToString(nameof(Type)) ?? "parameter";
                DataType = jObject.ToString(nameof(DataType)) ?? "string";
                Description = jObject.ToString(nameof(Description));
                Priority = Enum.TryParse(jObject.ToString(nameof(Priority)), out TemplateParameterPriority value) ? value : default;
                DefaultValue = jObject.ToString(nameof(DefaultValue));
                DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
                DisplayName = jObject.ToString(nameof(DisplayName));

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
            }

            [Obsolete]
            public string? Documentation => Description;

            public string Name { get; internal set; }

            public TemplateParameterPriority Priority { get; internal set; }

            public string Type { get; internal set; }

            public bool IsName => false;

            public string? DefaultValue { get; internal set; }

            public string DataType { get; internal set; }

            public IReadOnlyDictionary<string, ParameterChoice> Choices { get; internal set; } = new Dictionary<string, ParameterChoice>();

            public string? DisplayName { get; internal set; }

            public string? DefaultIfOptionWithoutValue { get; internal set; }

            public string? Description { get; internal set; }
        }
    }
}
