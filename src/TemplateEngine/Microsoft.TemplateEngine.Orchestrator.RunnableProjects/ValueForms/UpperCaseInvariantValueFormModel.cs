using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class UpperCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "upperCaseInvariant";

        public string Name { get; }

        internal UpperCaseInvariantValueFormModel()
        {
        }

        internal UpperCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new UpperCaseInvariantValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToUpperInvariant();
        }
    }
}
