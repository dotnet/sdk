using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
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
            JObject tagsObject = (JObject) entry[nameof(Tags)];
            Dictionary<string, string> tags = new Dictionary<string, string>();
            Tags = tags;
            //using (Timing.Over("Read tags"))
                foreach (JProperty item in tagsObject.Properties())
                {
                    tags[item.Name] = item.Value.ToString();
                }
            ConfigPlace = entry[nameof(ConfigPlace)].ToString();

            LocaleConfigMountPointId = Guid.Parse(entry[nameof(LocaleConfigMountPointId)].ToString());
            LocaleConfigPlace = entry[nameof(LocaleConfigPlace)].ToString();
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
        public IReadOnlyDictionary<string, string> Tags { get; set; }

        [JsonProperty]
        public string ConfigPlace { get; set; }

        [JsonProperty]
        public Guid LocaleConfigMountPointId { get; set; }

        [JsonProperty]
        public string LocaleConfigPlace { get; set; }
    }
}