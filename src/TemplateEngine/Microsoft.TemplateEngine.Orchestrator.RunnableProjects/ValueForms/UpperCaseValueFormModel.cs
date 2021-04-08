using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class UpperCaseValueFormModel : IValueForm
    {
        public string Identifier => "upperCase";

        public string Name { get; }

        internal UpperCaseValueFormModel()
        {
        }

        internal UpperCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new UpperCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToUpper();
        }
    }
}
