using System;
using Mutant.Chicken.Runner;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Demo
{
    internal class PathMatcherConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JTokenReader realReader = (JTokenReader) reader;
            JValue val = realReader.CurrentToken as JValue;
            return MinimatchShim.Get(val.Value<string>());
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IPathMatcher);
        }
    }
}