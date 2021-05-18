// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class ParameterSymbol : BaseValueSymbol
    {
        internal const string TypeName = "parameter";

        private IReadOnlyDictionary<string, ParameterChoice> _choices;

        /// <summary>
        /// Gets or sets the friendly name of the symbol to be displayed to the user.
        /// </summary>
        internal string DisplayName { get; set; }

        internal string Description { get; set; }

        // only relevant for choice datatype
        internal bool IsTag { get; set; }

        // only relevant for choice datatype
        internal string TagName { get; set; }

        // If this is set, the option can be provided without a value. It will be given this value.
        internal string DefaultIfOptionWithoutValue { get; set; }

        internal IReadOnlyDictionary<string, ParameterChoice> Choices
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

        // Used when the template explicitly defines the symbol "name".
        // The template definition is used exclusively, except for the case where it doesn't define any value forms.
        // When that is the case, the default value forms are used.
        internal static ParameterSymbol ExplicitNameSymbolMergeWithDefaults(ISymbolModel templateDefinedName, ISymbolModel defaultDefinedName)
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

        internal static ISymbolModel FromJObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            ParameterSymbol symbol = FromJObject<ParameterSymbol>(jObject, localization, defaultOverride);
            symbol.DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
            symbol.DisplayName = localization?.DisplayName ?? jObject.ToString(nameof(DisplayName)) ?? string.Empty;
            symbol.Description = localization?.Description ?? jObject.ToString(nameof(Description)) ?? string.Empty;

            var choicesAndDescriptions = new Dictionary<string, ParameterChoice>();

            if (symbol.DataType == "choice")
            {
                symbol.IsTag = false;
                symbol.TagName = jObject.ToString(nameof(TagName));

                foreach (JObject choiceObject in jObject.Items<JObject>(nameof(Choices)))
                {
                    string choiceName = choiceObject.ToString("choice");
                    var choice = new ParameterChoice(
                        choiceObject.ToString("displayName") ?? string.Empty,
                        choiceObject.ToString("description") ?? string.Empty);

                    if (localization != null
                        && localization.Choices.TryGetValue(choiceName, out ParameterChoiceLocalizationModel choiceLocalization))
                    {
                        choice.Localize(choiceLocalization);
                    }

                    choicesAndDescriptions.Add(choiceName, choice);
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

        internal static ISymbolModel FromDeprecatedConfigTag(string value)
        {
            ParameterSymbol symbol = new ParameterSymbol
            {
                DefaultValue = value,
                Type = TypeName,
                DataType = "choice",
                IsTag = true,
                Choices = new Dictionary<string, ParameterChoice>()
                {
                    { value, new ParameterChoice(string.Empty, string.Empty) }
                },
                Forms = SymbolValueFormsModel.Default
            };

            return symbol;
        }
    }
}
