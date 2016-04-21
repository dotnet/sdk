using System;
using Mutant.Chicken.Runner;
using Newtonsoft.Json;

namespace Mutant.Chicken.Demo
{
    internal class RunSpecConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new DemoRunSpec();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IRunSpec);
        }
    }
}