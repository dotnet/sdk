using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ParameterSymbol : ISymbolModel
    {
        // Used when the template explicitly defines the symbol "name".
        // The template definition is used exclusively, except for the case where it doesn't define any value forms.
        // When that is the case, the default value forms are used.
        //
        // When we add file-specific forms, this'll need some work.
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

            if (templateSymbol.Forms.GlobalForms.Count > 0)
            {   // template symbol has forms, use them
                return templateSymbol;
            }

            ParameterSymbol mergedSymbol = new ParameterSymbol()
            {
                Binding = templateSymbol.Binding,
                DefaultValue = templateSymbol.DefaultValue,
                Description = templateSymbol.Description,
                Forms = defaultSymbol.Forms,    // this is the only thing that gets replaced from the default
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

            return mergedSymbol;
        }

        public string Binding { get; set; }

        public string DefaultValue { get; set; }

        public string Description { get; set; }

        public SymbolValueFormsModel Forms { get; set; }

        public bool IsRequired { get; set; }

        public string Type { get; private set; }

        public string Replaces { get; set; }

        public string DataType { get; set; }

        public string FileRename { get; set; }

        // only relevant for choice datatype
        public bool IsTag { get; set; }

        // only relevant for choice datatype
        public string TagName { get; set; }

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

        public IReadOnlyList<IReplacementContext> ReplacementContexts { get; set; }

        public static ISymbolModel FromJObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            ParameterSymbol symbol = new ParameterSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                DefaultValue = defaultOverride ?? jObject.ToString(nameof(DefaultValue)),
                Description = localization?.Description ?? jObject.ToString(nameof(Description)) ?? string.Empty,
                FileRename = jObject.ToString(nameof(FileRename)),
                IsRequired = jObject.ToBool(nameof(IsRequired)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces)),
                DataType = jObject.ToString(nameof(DataType)),
                ReplacementContexts = ReadReplacementContexts(jObject),
            };

            if (!jObject.TryGetValue(nameof(symbol.Forms), StringComparison.OrdinalIgnoreCase, out JToken formsToken) || !(formsToken is JObject formsObject))
            {
                // no value forms explicitly defined, use the default ("identity")
                symbol.Forms = SymbolValueFormsModel.Default;
            }
            else
            {
                // the config defines forms for the symbol. Use them.
                symbol.Forms = SymbolValueFormsModel.FromJObject(formsObject);
            }

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
                    choicesAndDescriptions.Add(choice, choiceDescription ?? string.Empty);
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
                Choices = new Dictionary<string, string>() { { value, string.Empty } },
            };

            return symbol;
        }

        private static IReadOnlyList<IReplacementContext> ReadReplacementContexts(JObject jObject)
        {
            JArray onlyIf = jObject.Get<JArray>("onlyIf");

            if (onlyIf != null)
            {
                List<IReplacementContext> contexts = new List<IReplacementContext>();
                foreach (JToken entry in onlyIf.Children())
                {
                    if (!(entry is JObject x))
                    {
                        continue;
                    }

                    string before = entry.ToString("before");
                    string after = entry.ToString("after");
                    contexts.Add(new ReplacementContext(before, after));
                }

                return contexts;
            }
            else
            {
                return Empty<IReplacementContext>.List.Value;
            }
        }
    }
}
