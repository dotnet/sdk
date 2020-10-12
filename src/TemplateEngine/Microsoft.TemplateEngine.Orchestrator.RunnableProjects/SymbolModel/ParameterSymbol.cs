using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    public class ParameterSymbol : BaseValueSymbol
    {
        internal const string TypeName = "parameter";

        // Used when the template explicitly defines the symbol "name".
        // The template definition is used exclusively, except for the case where it doesn't define any value forms.
        // When that is the case, the default value forms are used.
        public static ParameterSymbol ExplicitNameSymbolMergeWithDefaults(ISymbolModel templateDefinedName, ISymbolModel defaultDefinedName)
        {
            if (!(templateDefinedName is ParameterSymbol templateSymbol))
            {
                throw new InvalidCastException("templateDefinedName is not a ParameterSymbol");
            }

            if (!(defaultDefinedName is ParameterSymbol defaultSymbol))
            {
                throw new InvalidCastException("defaultDefinedName is not a ParameterSymbol");
            }

            // the merged symbol is mostly the user defined symbol, except the conditional cases below.
            ParameterSymbol mergedSymbol = new ParameterSymbol()
            {
                DefaultValue = templateSymbol.DefaultValue,
                Description = templateSymbol.Description,
                IsRequired = templateSymbol.IsRequired,
                Type = templateSymbol.Type,
                Replaces = templateSymbol.Replaces,
                DataType = templateSymbol.DataType,
                FileRename = templateSymbol.FileRename,
                IsTag = templateSymbol.IsTag,
                TagName = templateSymbol.TagName,
                Choices = templateSymbol.Choices,
                ReplacementContexts = templateSymbol.ReplacementContexts,
            };

            // If the template hasn't explicitly defined a binding to the name symbol, use the default.
            if (string.IsNullOrEmpty(templateSymbol.Binding))
            {
                mergedSymbol.Binding = defaultDefinedName.Binding;
            }
            else
            {
                mergedSymbol.Binding = templateSymbol.Binding;
            }

            // if the template defined name symbol doesn't have any value forms defined, use the defaults.
            if (templateSymbol.Forms.GlobalForms.Count == 0)
            {
                mergedSymbol.Forms = defaultSymbol.Forms;
            }
            else
            {
                mergedSymbol.Forms = templateSymbol.Forms;
            }

            return mergedSymbol;
        }

        // only relevant for choice datatype
        public bool IsTag { get; set; }

        // only relevant for choice datatype
        public string TagName { get; set; }

        // If this is set, the option can be provided without a value. It will be given this value.
        public string DefaultIfOptionWithoutValue { get; set; }

        private IReadOnlyDictionary<string, string> _choices;

        public IReadOnlyDictionary<string, string> Choices
        {
            get
            {
                return _choices;
            }
            set
            {
                _choices = value.CloneIfDifferentComparer(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static ISymbolModel FromJObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            ParameterSymbol symbol = FromJObject<ParameterSymbol>(jObject, localization, defaultOverride);
            symbol.DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));

            Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>();

            if (symbol.DataType == "choice")
            {
                symbol.IsTag = jObject.ToBool(nameof(IsTag), true);
                symbol.TagName = jObject.ToString(nameof(TagName));

                foreach (JObject choiceObject in jObject.Items<JObject>(nameof(Choices)))
                {
                    string choice = choiceObject.ToString("choice");

                    if (localization == null
                        || !localization.ChoicesAndDescriptions.TryGetValue(choice, out string choiceDescription))
                    {
                        choiceDescription = choiceObject.ToString("description");
                    }
                    choicesAndDescriptions.Add(choice, choiceDescription ?? string.Empty);
                }
            }
            else if (symbol.DataType == "bool" && string.IsNullOrEmpty(symbol.DefaultIfOptionWithoutValue))
            {
                // bool flags are considred true if they're provided without a value.
                symbol.DefaultIfOptionWithoutValue = "true";
            }

            symbol.Choices = choicesAndDescriptions;

            return symbol;
        }

        public static ISymbolModel FromDeprecatedConfigTag(string value)
        {
            ParameterSymbol symbol = new ParameterSymbol
            {
                DefaultValue = value,
                Type = TypeName,
                DataType = "choice",
                IsTag = true,
                Choices = new Dictionary<string, string>() { { value, string.Empty } },
            };

            return symbol;
        }
    }
}
