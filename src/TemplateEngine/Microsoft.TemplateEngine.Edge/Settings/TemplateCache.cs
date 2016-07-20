using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateCache
    {
        public TemplateCache()
        {
            TemplateInfo = new List<TemplateInfo>();
        }

        [JsonProperty]
        public List<TemplateInfo> TemplateInfo { get; set; }
    }
}
