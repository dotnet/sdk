using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class JsonEncodeValueFormModel : IValueForm
    {
        public string Identifier => "jsonEncode";

        public string Name { get; }

        public JsonEncodeValueFormModel()
        {
        }

        public JsonEncodeValueFormModel(string name)
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
