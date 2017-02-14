using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
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

        // only relevant for choice datatype
        public bool IsTag { get; set; }

        // only relevant for choice datatype
        public string TagName { get; set; }

        public IReadOnlyDictionary<string, string> Choices { get; set; }

        public static ISymbolModel FromJObject(JObject jObject, IParameterSymbolLocalizationModel localization)
        {
            ParameterSymbol symbol = new ParameterSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                DefaultValue = jObject.ToString(nameof(DefaultValue)),
                Description = localization?.Description ?? jObject.ToString(nameof(Description)),
                IsRequired = jObject.ToBool(nameof(IsRequired)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces)),
                DataType = jObject.ToString(nameof(DataType)),
            };

            Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>();

            if (symbol.DataType == "choice")
            {
                symbol.IsTag = jObject.ToBool(nameof(IsTag), true);
                symbol.TagName = jObject.ToString(nameof(TagName));

                foreach (JObject choiceObject in jObject.Items<JObject>(nameof(Choices)))
                {
                    string choice = choiceObject.ToString("choice");

                    if (localization == null
                        || ! localization.ChoicesAndDescriptions.TryGetValue(choice, out string choiceDescription))
                    {
                        choiceDescription = choiceObject.ToString("description");
                    }
                    choicesAndDescriptions.Add(choice, choiceDescription);
                }
            }

            symbol.Choices = choicesAndDescriptions;

            return symbol;
        }

        public static ISymbolModel FromDeprecatedConfigTag(string value)
        {
            ParameterSymbol symbol = new ParameterSymbol
            {
                DefaultValue = value,
                Type = "parameter",
                DataType = "choice",
                IsTag = true,
                Choices = new Dictionary<string, string>() { { value, null } },
            };

            return symbol;
        }
    }
}