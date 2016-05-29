using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GeneratedSymbol : ISymbolModel
    {
        [JsonProperty]
        public string Binding { get; set; }

        [JsonProperty]
        public string Generator { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Parameters { get; set; }

        [JsonProperty]
        public string Replaces { get; set; }

        [JsonProperty]
        public string Type { get; set; }
    }
}