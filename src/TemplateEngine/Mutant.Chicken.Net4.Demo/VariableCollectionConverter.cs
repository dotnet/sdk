using System;
using Newtonsoft.Json;

namespace Mutant.Chicken.Demo
{
    internal class VariableCollectionConverter : JsonConverter
    {
        private Func<VariableCollection, VariableCollection> _variableInstall;

        public VariableCollectionConverter(Func<VariableCollection, VariableCollection> variableInstall)
        {
            _variableInstall = variableInstall;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            VariableCollection coll = VariableCollection.Environment();
            return _variableInstall(coll);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(VariableCollection);
        }
    }
}