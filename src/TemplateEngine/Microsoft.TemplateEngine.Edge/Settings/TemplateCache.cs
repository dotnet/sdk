using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateCache
    {
        public TemplateCache()
        {
            TemplateInfo = new List<TemplateInfo>();
        }

        public TemplateCache(JObject parsed)
            : this()
        {
            JToken templateInfoToken;
            if (parsed.TryGetValue("TemplateInfo", StringComparison.OrdinalIgnoreCase, out templateInfoToken))
            {
                JArray arr = templateInfoToken as JArray;
                if (arr != null)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            TemplateInfo.Add(new TemplateInfo((JObject) entry));
                        }
                    }
                }
            }
        }

        [JsonProperty]
        public List<TemplateInfo> TemplateInfo { get; set; }
    }
}
