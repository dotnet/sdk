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
            ConfigMountPointId = Guid.Parse(entry.ToString(nameof(ConfigMountPointId)));
            Author = entry.ToString(nameof(Author));
            JArray classificationsArray = entry.Get<JArray>(nameof(Classifications));

            List<string> classifications = new List<string>();
            Classifications = classifications;
            //using (Timing.Over("Read classifications"))
                foreach (JToken item in classificationsArray)
                {
                    classifications.Add(item.ToString());
                }

            DefaultName = entry.ToString(nameof(DefaultName));
            Description = entry.ToString(nameof(Description));
            Identity = entry.ToString(nameof(Identity));
            GeneratorId = Guid.Parse(entry.ToString(nameof(GeneratorId)));
            GroupIdentity = entry.ToString(nameof(GroupIdentity));
            Name = entry.ToString(nameof(Name));
            ShortName = entry.ToString(nameof(ShortName));

            // parse the cached tags
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            JObject tagsObject = entry.Get<JObject>(nameof(Tags));
            foreach (JProperty item in tagsObject.Properties())
            {
                Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                JObject cdToken = item.Value.Get<JObject>(nameof(ICacheTag.ChoicesAndDescriptions));
                foreach (JProperty cdPair in cdToken.Properties())
                {
                    choicesAndDescriptions.Add(cdPair.Name.ToString(), cdPair.Value.ToString());
                }

                ICacheTag cacheTag = new CacheTag(
                    item.Value.ToString(nameof(ICacheTag.Description)),
                    choicesAndDescriptions, 
                    item.Value.ToString(nameof(ICacheTag.DefaultValue)));
                tags.Add(item.Name.ToString(), cacheTag);
            }
            Tags = tags;

            // parse the cached params
            JObject cacheParamsObject = entry.Get<JObject>(nameof(CacheParameters));
            Dictionary<string, ICacheParameter> cacheParams = new Dictionary<string, ICacheParameter>();
            foreach (JProperty item in cacheParamsObject.Properties())
            {
                ICacheParameter param = new CacheParameter
                {
                    DataType = item.Value.ToString(nameof(ICacheParameter.DataType)),
                    DefaultValue = item.Value.ToString(nameof(ICacheParameter.DefaultValue)),
                    Description = item.Value.ToString(nameof(ICacheParameter.Description))
                };

                cacheParams[item.Name.ToString()] = param;
            }
            CacheParameters = cacheParams;

            ConfigPlace = entry.ToString(nameof(ConfigPlace));
            LocaleConfigMountPointId = Guid.Parse(entry.ToString(nameof(LocaleConfigMountPointId)));
            LocaleConfigPlace = entry.ToString(nameof(LocaleConfigPlace));

            HostConfigMountPointId = Guid.Parse(entry.ToString(nameof(HostConfigMountPointId)));
            HostConfigPlace = entry.ToString(nameof(HostConfigPlace));
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
                    Choices = tagInfo.Value.ChoicesAndDescriptions
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
