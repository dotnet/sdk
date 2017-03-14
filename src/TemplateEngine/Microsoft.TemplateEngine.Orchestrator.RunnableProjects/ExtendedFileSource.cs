using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ExtendedFileSource
    {
        [JsonProperty]
        public JToken CopyOnly { get; internal set; }

        [JsonProperty]
        public JToken Include { get; set; }

        [JsonProperty]
        public JToken Exclude { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Rename { get; set; }

        [JsonProperty]
        public string Source { get; set; }

        [JsonProperty]
        public string Target { get; set; }

        [JsonProperty]
        public string Condition { get; set; }

        [JsonProperty]
        public List<SourceModifier> Modifiers { get; internal set; }
    }
}
