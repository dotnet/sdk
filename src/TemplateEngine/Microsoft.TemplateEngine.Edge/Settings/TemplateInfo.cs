using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
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
            ConfigMountPointId = Guid.Parse(entry["ConfigMountPointId"].ToString());
            Author = entry["Author"].ToString();
            JArray classificationsArray = (JArray)entry["Classifications"];
            List<string> classifications = new List<string>();
            Classifications = classifications;
            //using (Timing.Over("Read classifications"))
                foreach (JToken item in classificationsArray)
                {
                    classifications.Add(item.ToString());
                }
            DefaultName = entry["DefaultName"].ToString();
            Identity = entry["Identity"].ToString();
            GeneratorId = Guid.Parse(entry["GeneratorId"].ToString());
            GroupIdentity = entry["GroupIdentity"].ToString();
            Name = entry["Name"].ToString();
            ShortName = entry["ShortName"].ToString();
            JObject tagsObject = (JObject) entry["Tags"];
            Dictionary<string, string> tags = new Dictionary<string, string>();
            Tags = tags;
            //using (Timing.Over("Read tags"))
                foreach (JProperty item in tagsObject.Properties())
                {
                    tags[item.Name] = item.Value.ToString();
                }
            ConfigPlace = entry["ConfigPlace"].ToString();
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
    }
}