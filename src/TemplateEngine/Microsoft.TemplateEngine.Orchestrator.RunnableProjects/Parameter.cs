using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class Parameter : ITemplateParameter
    {
        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public string DefaultValue { get; set; }

        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty]
        public bool IsName { get; set; }

        [JsonProperty]
        public TemplateParameterPriority Requirement { get; set; }

        [JsonProperty]
        public string Type { get; set; }

        [JsonProperty]
        public bool IsVariable { get; set; }

        string ITemplateParameter.Documentation => Description;

        string ITemplateParameter.Name => Name;

        TemplateParameterPriority ITemplateParameter.Priority => Requirement;

        string ITemplateParameter.Type => Type;

        bool ITemplateParameter.IsName => IsName;

        string ITemplateParameter.DefaultValue => DefaultValue;
    }
}