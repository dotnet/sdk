using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SymbolModelConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(ISymbolModel).GetTypeInfo().IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject model = JObject.Load(reader);
            switch (model["type"].ToObject<string>())
            {
                case "parameter":
                    return model.ToObject<ParameterSymbol>();
                case "computed":
                    return model.ToObject<ComputedSymbol>();
                case "generated":
                    return model.ToObject<GeneratedSymbol>();
                default:
                    return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}