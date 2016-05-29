using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class FileSource
    {
        [JsonProperty]
        public string[] Include { get; set; }

        [JsonProperty]
        public string[] Exclude { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Rename { get; set; }

        [JsonProperty]
        public string Source { get; set; }

        [JsonProperty]
        public string Target { get; set; }

        [JsonProperty]
        public string[] CopyOnly { get; set; }
    }
}