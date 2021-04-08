using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class LowerCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "lowerCaseInvariant";

        public string Name { get; }

        internal LowerCaseInvariantValueFormModel()
        {
        }

        internal LowerCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new LowerCaseInvariantValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToLowerInvariant();
        }
    }
}
