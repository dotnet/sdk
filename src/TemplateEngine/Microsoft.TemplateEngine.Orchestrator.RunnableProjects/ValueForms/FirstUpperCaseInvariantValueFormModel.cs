using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class FirstUpperCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "firstUpperCaseInvariant";

        public string Name { get; }

        public FirstUpperCaseInvariantValueFormModel()
        {
        }

        public FirstUpperCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new FirstUpperCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            switch (value)
            {
                case null: return null;
                case "": return value;
                default: return value.First().ToString().ToUpperInvariant() + value.Substring(1);
            }
        }
    }
}
