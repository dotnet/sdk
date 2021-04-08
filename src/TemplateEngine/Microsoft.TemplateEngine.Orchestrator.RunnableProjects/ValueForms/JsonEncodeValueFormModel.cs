using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class JsonEncodeValueFormModel : IValueForm
    {
        public string Identifier => "jsonEncode";

        public string Name { get; }

        internal JsonEncodeValueFormModel()
        {
        }

        internal JsonEncodeValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new JsonEncodeValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return JsonConvert.SerializeObject(value);
        }
    }
}
