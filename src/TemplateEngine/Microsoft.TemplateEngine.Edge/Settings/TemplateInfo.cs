using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateInfo : ITemplateInfo
    {
        public TemplateInfo()
        {
        }

        public TemplateInfo(JObject entry)
        {
            ConfigMountPointId = Guid.Parse(entry[nameof(ConfigMountPointId)].ToString());
            Author = entry[nameof(Author)].ToString();
            JArray classificationsArray = (JArray)entry[nameof(Classifications)];
            List<string> classifications = new List<string>();
            Classifications = classifications;
            //using (Timing.Over("Read classifications"))
                foreach (JToken item in classificationsArray)
                {
                    classifications.Add(item.ToString());
                }
            DefaultName = entry[nameof(DefaultName)].ToString();
            Description = entry[nameof(Description)].ToString();
            Identity = entry[nameof(Identity)].ToString();
            GeneratorId = Guid.Parse(entry[nameof(GeneratorId)].ToString());
            GroupIdentity = entry[nameof(GroupIdentity)].ToString();
            Name = entry[nameof(Name)].ToString();
            ShortName = entry[nameof(ShortName)].ToString();

            // parse the cached tags
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            JObject tagsObject = (JObject)entry[nameof(Tags)];
            foreach (JProperty item in tagsObject.Properties())
            {
                Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                JObject cdToken = (JObject)item.Value[nameof(ICacheTag.ChoicesAndDescriptions)];
                foreach (JProperty cdPair in cdToken.Properties())
                {
                    choicesAndDescriptions.Add(cdPair.Name.ToString(), cdPair.Value.ToString());
                }

                CacheTag cacheTag = new CacheTag
                {
                    Description = item.Value[nameof(ICacheTag.Description)].ToString(),
                    ChoicesAndDescriptions = choicesAndDescriptions,
                    DefaultValue = item.Value[nameof(ICacheTag.DefaultValue)].ToString()
                };

                tags.Add(item.Name.ToString(), cacheTag);
            }
            Tags = tags;

            // parse the cached params
            JObject cacheParamsObject = (JObject)entry[nameof(CacheParameters)];
            Dictionary<string, ICacheParameter> cacheParams = new Dictionary<string, ICacheParameter>();
            foreach (JProperty item in cacheParamsObject.Properties())
            {
                ICacheParameter param = new CacheParameter
                {
                    DataType = item.Value[nameof(ICacheParameter.DataType)].ToString(),
                    DefaultValue = item.Value[nameof(ICacheParameter.DefaultValue)].ToString(),
                    Description = item.Value[nameof(ICacheParameter.Description)].ToString()
                };

                cacheParams[item.Name.ToString()] = param;
            }
            CacheParameters = cacheParams;

            ConfigPlace = entry[nameof(ConfigPlace)].ToString();
            LocaleConfigMountPointId = Guid.Parse(entry[nameof(LocaleConfigMountPointId)].ToString());
            LocaleConfigPlace = entry[nameof(LocaleConfigPlace)].ToString();

            HostConfigMountPointId = Guid.Parse(entry[nameof(HostConfigMountPointId)].ToString());
            HostConfigPlace = entry[nameof(HostConfigPlace)].ToString();
        }

        public IParameterSet GetParametersForTemplate()
        {
            IList<ITemplateParameter> parameters = new List<ITemplateParameter>();
            
            foreach (KeyValuePair<string, ICacheTag> tagInfo in Tags)
            {
                ITemplateParameter param = new TemplateParameter
                {
                    Name = tagInfo.Key,
                    Documentation = tagInfo.Value.Description,
                    DefaultValue = tagInfo.Value.DefaultValue,
                    Choices = tagInfo.Value.ChoicesAndDescriptions,
                    DataType = "choice"
                };

                parameters.Add(param);
            }

            foreach (KeyValuePair<string, ICacheParameter> paramInfo in CacheParameters)
            {
                ITemplateParameter param = new TemplateParameter
                {
                    Name = paramInfo.Key,
                    Documentation = paramInfo.Value.Description,
                    DataType = paramInfo.Value.DataType,
                    DefaultValue = paramInfo.Value.DefaultValue
                };

                parameters.Add(param);
            }

            return new TemplateParameterSet(parameters);
        }

        [JsonProperty]
        public Guid ConfigMountPointId { get; set; }

        [JsonProperty]
        public string Author { get; set; }

        [JsonProperty]
        public IReadOnlyList<string> Classifications { get; set; }

        [JsonProperty]
        public string DefaultName { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public string Identity { get; set; }

        [JsonProperty]
        public Guid GeneratorId { get; set; }

        [JsonProperty]
        public string GroupIdentity { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string ShortName { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheTag> Tags { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters { get; set; }

        [JsonProperty]
        public string ConfigPlace { get; set; }

        [JsonProperty]
        public Guid LocaleConfigMountPointId { get; set; }

        [JsonProperty]
        public string LocaleConfigPlace { get; set; }

        [JsonProperty]
        public Guid HostConfigMountPointId { get; set; }

        [JsonProperty]
        public string HostConfigPlace { get; set; }
    }
}