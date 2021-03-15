using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class Parameter : ITemplateParameter, IExtendedTemplateParameter, IAllowDefaultIfOptionWithoutValue
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

        [JsonProperty]
        public string DataType { get; set; }

        [JsonProperty]
        public string DefaultIfOptionWithoutValue { get; set; }

        public bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }

        [JsonProperty]
        [Obsolete("This property is no longer used. It is populated only when creating parameters from parameter and derived symbols for compatibility reason.")]
        public string FileRename { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ParameterChoice> Choices { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Forms { get; set; }

        [JsonIgnore]
        public string Documentation
        {
            get { return Description; }
            set { Description = value; }
        }

        string ITemplateParameter.Name => Name;

        TemplateParameterPriority ITemplateParameter.Priority => Requirement;

        string ITemplateParameter.Type => Type;

        bool ITemplateParameter.IsName => IsName;

        string ITemplateParameter.DefaultValue => DefaultValue;

        string ITemplateParameter.DataType => DataType;

        string IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue
        {
            get
            {
                return DefaultIfOptionWithoutValue;
            }
            set
            {
                DefaultIfOptionWithoutValue = value;
            }
        }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }
    }
}
