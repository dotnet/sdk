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
        /// Creates a default instance of <see cref="ParameterSymbol"/>.
        /// </summary>
        public ParameterSymbol() { }

        /// <summary>
        /// Creates an instance of <see cref="ParameterSymbol"/> using
        /// the provided Json Data.
        /// </summary>
        /// <param name="jObject">JSON to initialize the symbol with.</param>
        /// <param name="defaultOverride"></param>
        public ParameterSymbol(JObject jObject, string defaultOverride)
            : base(jObject, defaultOverride)
        {
            DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
            DisplayName = jObject.ToString(nameof(DisplayName)) ?? string.Empty;
            Description = jObject.ToString(nameof(Description)) ?? string.Empty;

            var choicesAndDescriptions = new Dictionary<string, ParameterChoice>();

            if (DataType == "choice")
            {
                IsTag = false;
                TagName = jObject.ToString(nameof(TagName));

                foreach (JObject choiceObject in jObject.Items<JObject>(nameof(Choices)))
                {
                    string choiceName = choiceObject.ToString("choice");
                    var choice = new ParameterChoice(
                        choiceObject.ToString("displayName") ?? string.Empty,
                        choiceObject.ToString("description") ?? string.Empty);

                    choicesAndDescriptions.Add(choiceName, choice);
                }
            }
            else if (DataType == "bool" && string.IsNullOrEmpty(DefaultIfOptionWithoutValue))
            {
                // bool flags are considered true if they're provided without a value.
                DefaultIfOptionWithoutValue = "true";
            }

            Choices = choicesAndDescriptions;
        }

        /// <summary>
        /// Creates a clone of the given <see cref="ParameterSymbol"/>.
        /// </summary>
        /// <param name="cloneFrom">The symbol to copy the values from.</param>
        /// <param name="bindingFallback">The value to be used for <see cref="BaseValueSymbol.Binding"/> in the case
        /// that the <paramref name="cloneFrom"/> does not specify a value for <see cref="BaseValueSymbol.Binding"/>.</param>
        /// <param name="formsFallback">The value to be used for <see cref="BaseValueSymbol.Forms"/> in the case
        /// that the <paramref name="cloneFrom"/> does not specify a value for <see cref="BaseValueSymbol.Forms"/>.</param>
        public ParameterSymbol(ParameterSymbol cloneFrom, string bindingFallback, SymbolValueFormsModel formsFallback)
        {
            DefaultValue = cloneFrom.DefaultValue;
            Description = cloneFrom.Description;
            IsRequired = cloneFrom.IsRequired;
            Type = cloneFrom.Type;
            Replaces = cloneFrom.Replaces;
            DataType = cloneFrom.DataType;
            FileRename = cloneFrom.FileRename;
            IsTag = cloneFrom.IsTag;
            TagName = cloneFrom.TagName;
            Choices = cloneFrom.Choices;
            ReplacementContexts = cloneFrom.ReplacementContexts;
            Binding = string.IsNullOrEmpty(cloneFrom.Binding) ? bindingFallback : cloneFrom.Binding;
            Forms = cloneFrom.Forms.GlobalForms.Count != 0 ? cloneFrom.Forms : formsFallback;
        }

        /// <summary>
        /// Gets or sets the friendly name of the symbol to be displayed to the user.
        /// </summary>
        internal string DisplayName { get; private set; }

        internal string Description { get; private set; }

        // only relevant for choice datatype
        internal bool IsTag { get; init; }

        // only relevant for choice datatype
        internal string TagName { get; init; }

        // If this is set, the option can be provided without a value. It will be given this value.
        internal string DefaultIfOptionWithoutValue { get; init; }

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
