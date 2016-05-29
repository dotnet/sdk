using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ComputedSymbol : ISymbolModel
    {
        [JsonProperty]
        public string Condition { get; internal set; }

        [JsonProperty]
        public string Type { get; set; }

        [JsonIgnore]
        string ISymbolModel.Binding
        {
            get { return null; }
            set { }
        }

        [JsonIgnore]
        string ISymbolModel.Replaces
        {
            get { return null; }
            set { }
        }
    }
}