using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class TitleCaseValueFormModel : IValueForm
    {
        public string Identifier => "titleCase";

        public string Name { get; }

        public TitleCaseValueFormModel()
        {
        }

        public TitleCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new TitleCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            switch (value)
            {
                case null: return null;
                case "": return value;
                default: return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
            }
        }
    }
}
