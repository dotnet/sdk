using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CacheParameter : ICacheParameter
    {
        public CacheParameter(string dataType, string defaultvalue, string description)
        {
            DataType = dataType;
            DefaultValue = defaultvalue;
            Description = description;
        }

        [JsonProperty]
        public string DataType { get; }

        [JsonProperty]
        public string DefaultValue { get; }

        [JsonProperty]
        public string Description { get; }
    }
}
