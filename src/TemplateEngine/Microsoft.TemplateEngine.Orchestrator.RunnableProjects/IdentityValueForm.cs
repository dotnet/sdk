using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class IdentityValueForm : IValueForm
    {
        public string Identifier => "identity";

        public string Name { get; }

        public IdentityValueForm()
        {
        }

        public IdentityValueForm(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new IdentityValueForm(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value;
        }
    }
}