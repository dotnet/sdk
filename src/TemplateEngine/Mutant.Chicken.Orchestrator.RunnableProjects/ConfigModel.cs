using System.Collections.Generic;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Orchestrator.RunnableProjects
{
    public class ConfigModel
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string ShortName { get; set; }

        [JsonProperty]
        public string DefaultName { get; set; }

        [JsonProperty]
        public FileSource[] Sources { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Macros { get; set; }

        [JsonProperty]
        public Dictionary<string, Parameter> Parameters { get; set; }

        [JsonProperty]
        public Dictionary<string, JObject> Config { get; set; }

        [JsonProperty]
        public Dictionary<string, Dictionary<string, JObject>> Special { get; set; }
    }

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
