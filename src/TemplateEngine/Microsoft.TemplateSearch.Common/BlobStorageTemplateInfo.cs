// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    [JsonObject(Id = "TemplateInfo")]
    public class BlobStorageTemplateInfo : ITemplateInfo
    {
        [JsonProperty(PropertyName = "Tags")]
        private IReadOnlyDictionary<string, CacheTag> _tags;

        [JsonProperty(PropertyName = "CacheParameters")]
        private IReadOnlyDictionary<string, CacheParameter> _cacheParameters;

        [JsonProperty(PropertyName = "BaselineInfo")]
        private IReadOnlyDictionary<string, BaselineCacheInfo> _baselineInfo;

        private List<ITemplateParameter> _parameters;

        private BlobStorageTemplateInfo() { }

        [JsonIgnore]
        IReadOnlyList<ITemplateParameter> ITemplateInfo.Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    List<ITemplateParameter> parameters = new List<ITemplateParameter>();

                    foreach (KeyValuePair<string, ICacheTag> tagInfo in Tags)
                    {
                        ITemplateParameter param = new TemplateParameter
                        {
                            Name = tagInfo.Key,
                            Documentation = tagInfo.Value.Description,
                            DefaultValue = tagInfo.Value.DefaultValue,
                            Choices = tagInfo.Value.Choices,
                            DataType = "choice"
                        };

                        if (param is IAllowDefaultIfOptionWithoutValue paramWithNoValueDefault
                            && tagInfo.Value is IAllowDefaultIfOptionWithoutValue tagWithNoValueDefault)
                        {
                            paramWithNoValueDefault.DefaultIfOptionWithoutValue = tagWithNoValueDefault.DefaultIfOptionWithoutValue;
                            parameters.Add(paramWithNoValueDefault as TemplateParameter);
                        }
                        else
                        {
                            parameters.Add(param);
                        }
                    }

                    foreach (KeyValuePair<string, ICacheParameter> paramInfo in CacheParameters)
                    {
                        ITemplateParameter param = new TemplateParameter
                        {
                            Name = paramInfo.Key,
                            Documentation = paramInfo.Value.Description,
                            DataType = paramInfo.Value.DataType,
                            DefaultValue = paramInfo.Value.DefaultValue,
                        };

                        if (param is IAllowDefaultIfOptionWithoutValue paramWithNoValueDefault
                            && paramInfo.Value is IAllowDefaultIfOptionWithoutValue infoWithNoValueDefault)
                        {
                            paramWithNoValueDefault.DefaultIfOptionWithoutValue = infoWithNoValueDefault.DefaultIfOptionWithoutValue;
                            parameters.Add(paramWithNoValueDefault as TemplateParameter);
                        }
                        else
                        {
                            parameters.Add(param);
                        }
                    }

                    _parameters = parameters;
                }

                return _parameters;
            }
        }

        [JsonIgnore]
        string ITemplateInfo.MountPointUri => throw new NotImplementedException();

        [JsonProperty]
        public string Author { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; private set; }

        [JsonProperty]
        public string DefaultName { get; private set; }

        [JsonProperty]
        public string Description { get; private set; }

        [JsonProperty]
        public string Identity { get; private set; }

        [JsonIgnore]
        Guid ITemplateInfo.GeneratorId => throw new NotImplementedException();

        [JsonProperty]
        public string GroupIdentity { get; private set; }

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
        public IReadOnlyDictionary<string, ICacheTag> Tags => _tags.ToDictionary(kvp => kvp.Key, kvp => (ICacheTag)kvp.Value);

        [JsonIgnore]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters => _cacheParameters.ToDictionary(kvp => kvp.Key, kvp => (ICacheParameter)kvp.Value);

        [JsonIgnore]
        string ITemplateInfo.ConfigPlace => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.LocaleConfigPlace => throw new NotImplementedException();

        [JsonIgnore]
        string ITemplateInfo.HostConfigPlace => throw new NotImplementedException();

        [JsonProperty]
        public string ThirdPartyNotices { get; private set; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _baselineInfo.ToDictionary(kvp => kvp.Key, kvp => (IBaselineInfo)kvp.Value);

        [JsonIgnore]
        [Obsolete("This property is deprecated")]
        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

        public static BlobStorageTemplateInfo FromJObject(JObject entry)
        {
            return entry.ToObject<BlobStorageTemplateInfo>();
        }
    }

    internal class CacheTag : ICacheTag
    {
        [JsonProperty(PropertyName = "ChoicesAndDescriptions")]
        private IReadOnlyDictionary<string, string> _choices;

        [JsonIgnore]
        public string DisplayName => throw new NotImplementedException();

        [JsonProperty]
        public string Description { get; private set; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, ParameterChoice> Choices => _choices.ToDictionary(kvp => kvp.Key, kvp => new ParameterChoice(null, kvp.Value));

        [JsonProperty]
        public string DefaultValue { get; private set; }
    }

    internal class CacheParameter : ICacheParameter
    {
        [JsonProperty]
        public string DataType { get; private set; }

        [JsonProperty]
        public string DefaultValue { get; private set; }

        [JsonIgnore]
        public string DisplayName => throw new NotImplementedException();

        [JsonProperty]
        public string Description { get; private set; }
    }

    internal class BaselineCacheInfo : IBaselineInfo
    {
        [JsonProperty]
        public string Description { get; private set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, string> DefaultOverrides { get; private set; }
    }

    internal class TemplateParameter : ITemplateParameter
    {
        public string Documentation { get; internal set; }

        public string Name { get; internal set; }

        public TemplateParameterPriority Priority { get; internal set; }

        public string Type { get; internal set; }

        public bool IsName { get; internal set; }

        public string DefaultValue { get; internal set; }

        public string DataType { get; internal set; }

        public IReadOnlyDictionary<string, ParameterChoice> Choices { get; internal set; }
    }
}
