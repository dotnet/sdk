using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class FirstUpperCaseValueFormModel : IValueForm
    {
        public string Identifier => "firstUpperCase";

        public string Name { get; }

        public FirstUpperCaseValueFormModel()
        {
        }

        public FirstUpperCaseValueFormModel(string name)
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
                default: return value.First().ToString().ToUpper() + value.Substring(1);
            }
        }
    }
}
