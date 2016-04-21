using System;
using System.Collections.Generic;
using Mutant.Chicken.Runner;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mutant.Chicken.Demo
{
    public class SpecialConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JTokenReader realReader = (JTokenReader)reader;
            JObject obj = (JObject) realReader.CurrentToken;
            Dictionary<IPathMatcher, IRunSpec> result = new Dictionary<IPathMatcher, IRunSpec>();

            foreach (JProperty property in obj.Properties())
            {
                IPathMatcher key = MinimatchShim.Get(property.Name);
                IRunSpec value = property.Value.ToObject<DemoRunSpec>(serializer);
                result[key] = value;
            }

            return result;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof (Dictionary<IPathMatcher, IRunSpec>);
        }
    }
}