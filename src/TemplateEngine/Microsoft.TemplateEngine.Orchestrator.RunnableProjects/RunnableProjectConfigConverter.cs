using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectConfigConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IRunnableProjectConfig).GetTypeInfo().IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject o = JObject.Load(reader);
            JToken extendedConfigToken;
            if (o.TryGetValue("config", StringComparison.OrdinalIgnoreCase, out extendedConfigToken))
            {
                return o.ToObject<ConfigModel>(new JsonSerializer());
            }

            return o.ToObject<SimpleConfigModel>(new JsonSerializer());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}