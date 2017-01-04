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

        public IReadOnlyDictionary<string, string> Choices { get; set; }

        public static ISymbolModel FromJObject(JObject jObject, string localizedDescription = null, IReadOnlyDictionary<string, string> localizedChoiceDescriptions = null)
        {
            ParameterSymbol symbol = new ParameterSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                DefaultValue = jObject.ToString(nameof(DefaultValue)),
                Description = localizedDescription ?? jObject.ToString(nameof(Description)),
                IsRequired = jObject.ToBool(nameof(IsRequired)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces)),
                DataType = jObject.ToString(nameof(DataType))
            };

            Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>();

            if (symbol.DataType == "choice")
            {
                foreach (JObject choiceObject in jObject.Items<JObject>(nameof(Choices)))
                {
                    string choice = choiceObject.ToString("choice");

                    if (localizedChoiceDescriptions == null ||
                        ! localizedChoiceDescriptions.TryGetValue(choice, out string description))
                    {   // no localized description, use the default
                        description = choiceObject.ToString("description");
                    }
                    choicesAndDescriptions.Add(choice, description);
                }
            }

            symbol.Choices = choicesAndDescriptions;

            return symbol;
        }
    }
}