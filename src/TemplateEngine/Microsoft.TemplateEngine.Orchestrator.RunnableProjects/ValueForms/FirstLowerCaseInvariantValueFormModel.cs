using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class FirstLowerCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "firstLowerCaseInvariant";

        public string Name { get; }

        internal FirstLowerCaseInvariantValueFormModel()
        {
        }

        internal FirstLowerCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new FirstLowerCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            switch (value)
            {
                case null: return null;
                case "": return value;
                default: return value.First().ToString().ToLowerInvariant() + value.Substring(1);
            }
        }
    }
}
