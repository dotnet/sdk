using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class SourceModifier
    {
        [JsonProperty]
        public string Condition { get; set; }

        [JsonProperty]
        public JToken CopyOnly { get; set; }

        [JsonProperty]
        public JToken Include { get; set; }

        [JsonProperty]
        public JToken Exclude { get; set; }

        [JsonProperty]
        public JObject Rename { get; set; }
    }
}
