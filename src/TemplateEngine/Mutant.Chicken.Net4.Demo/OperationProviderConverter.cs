using System;
using Mutant.Chicken.Expressions.Cpp;
using Newtonsoft.Json;

namespace Mutant.Chicken.Demo
{
    internal class OperationProviderConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (++Count == 1)
            {
                return new Conditional("#if", "#else", "#elseif", "#endif", true, true, CppStyleEvaluatorDefinition.CppStyleEvaluator);
            }

            return new ExpandVariables();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IOperationProvider);
        }

        public static int Count { get; set; }
    }
}