using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CacheParameter : ICacheParameter
    {
        [JsonProperty]
        public string DataType { get; set; }

        [JsonProperty]
        public string DefaultValue { get; set; }

        [JsonProperty]
        public string Description { get; set; }
    }
}
