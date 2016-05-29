using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ParameterSymbol : ISymbolModel
    {
        [JsonProperty]
        public string Binding { get; set; }

        [JsonProperty]
        public string DefaultValue { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public bool IsRequired { get; set; }

        [JsonProperty]
        public string Type { get; set; }

        [JsonProperty]
        public string Replaces { get; set; }
    }
}