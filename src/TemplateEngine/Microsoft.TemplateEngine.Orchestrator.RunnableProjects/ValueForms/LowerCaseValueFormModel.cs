using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class LowerCaseValueFormModel : IValueForm
    {
        public string Identifier => "lowerCase";

        public string Name { get; }

        public LowerCaseValueFormModel()
        {
        }

        public LowerCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new LowerCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToLower();
        }
    }
}
