using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CacheParameter : ICacheParameter, IAllowDefaultIfOptionWithoutValue
    {
        public CacheParameter(string dataType, string defaultValue, string description)
            :this(dataType, defaultValue, description, null)
        {
        }

        public CacheParameter(string dataType, string defaultValue, string description, string defaultIfOptionWithoutValue)
        {
            DataType = dataType;
            DefaultValue = defaultValue;
            Description = description;
            DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
        }

        [JsonProperty]
        public string DataType { get; }

        [JsonProperty]
        public string DefaultValue { get; }

        [JsonProperty]
        public string Description { get; }

        [JsonProperty]
        public string DefaultIfOptionWithoutValue { get; set; }

        public bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }
    }
}
