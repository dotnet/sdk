using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ParameterSymbol : ISymbolModel
    {
        public string Binding { get; set; }

        public string DefaultValue { get; set; }

        public string Description { get; set; }

        public bool IsRequired { get; set; }

        public string Type { get; private set; }

        public string Replaces { get; set; }

        public string DataType { get; set; }

        public IReadOnlyList<string> Choices { get; set; }

        public static ISymbolModel FromJObject(JObject jObject)
        {
            ParameterSymbol sym = new ParameterSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                DefaultValue = jObject.ToString(nameof(DefaultValue)),
                Description = jObject.ToString(nameof(Description)),
                IsRequired = jObject.ToBool(nameof(IsRequired)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces)),
                DataType = jObject.ToString(nameof(DataType))
            };

            if (sym.DataType == "choice")
            {
                List<string> choiceList = new List<string>();

                JArray choices = (JArray)jObject["choices"];
                foreach (JToken choice in choices)
                {
                    choiceList.Add(choice.ToString());
                }

                sym.Choices = choiceList.AsReadOnly();
            }

            return sym;
        }
    }
}