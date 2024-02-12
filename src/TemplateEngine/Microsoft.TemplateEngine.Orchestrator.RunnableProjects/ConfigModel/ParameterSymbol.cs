// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the symbol of type "parameter".
    /// </summary>
    public sealed class ParameterSymbol : BaseValueSymbol
    {
        internal const string TypeName = "parameter";

        private IReadOnlyDictionary<string, ParameterChoice>? _choices;
        private string? _description;
        private string? _displayName;

        /// <summary>
        /// Creates an instance of <see cref="ParameterSymbol"/> using
        /// the provided JSON Data.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="jObject">JSON to initialize the symbol with.</param>
        /// <param name="defaultOverride"></param>
        internal ParameterSymbol(string name, JObject jObject, string? defaultOverride)
            : base(name, jObject, defaultOverride, true)
        {
            DefaultIfOptionWithoutValue = jObject.ToString(nameof(DefaultIfOptionWithoutValue));
            DisplayName = jObject.ToString(nameof(DisplayName)) ?? string.Empty;
            Description = jObject.ToString(nameof(Description)) ?? string.Empty;

            var choicesAndDescriptions = new Dictionary<string, ParameterChoice>();

            if (DataType == "choice")
            {
                TagName = jObject.ToString(nameof(TagName));

                foreach (JObject choiceObject in jObject.Items<JObject>(nameof(Choices)))
                {
                    string? choiceName = choiceObject.ToString("choice");

                    if (string.IsNullOrWhiteSpace(choiceName))
                    {
                        throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, ParameterSymbol.TypeName, "choice"), name);
                    }

                    var choice = new ParameterChoice(
                        choiceObject.ToString("displayName") ?? string.Empty,
                        choiceObject.ToString("description") ?? string.Empty);

                    choicesAndDescriptions.Add(choiceName!, choice);
                }
            }
            else if (DataType == "bool" && string.IsNullOrEmpty(DefaultIfOptionWithoutValue))
            {
                // bool flags are considered true if they're provided without a value.
                DefaultIfOptionWithoutValue = "true";
            }

            if (DefaultValue == null && IsRequired != true)
            {
                DefaultValue = ParameterConverter.GetDefault(DataType);
            }

            Choices = choicesAndDescriptions;
            AllowMultipleValues = jObject.ToBool(nameof(AllowMultipleValues));
            EnableQuotelessLiterals = jObject.ToBool(nameof(EnableQuotelessLiterals));

            this.Precedence = GetPrecedence(IsRequired, jObject);
        }

        /// <summary>
        /// Creates a clone of the given <see cref="ParameterSymbol"/>.
        /// </summary>
        /// <param name="cloneFrom">The symbol to copy the values from.</param>
        /// <param name="formsFallback">The value to be used for <see cref="BaseValueSymbol.Forms"/> in the case
        /// that the <paramref name="cloneFrom"/> does not specify a value for <see cref="BaseValueSymbol.Forms"/>.</param>
        internal ParameterSymbol(ParameterSymbol cloneFrom, SymbolValueFormsModel formsFallback) : base(cloneFrom, formsFallback)
        {
            Description = cloneFrom.Description;
            IsTag = cloneFrom.IsTag;
            TagName = cloneFrom.TagName;
            Choices = cloneFrom.Choices;
            AllowMultipleValues = cloneFrom.AllowMultipleValues;
            EnableQuotelessLiterals = cloneFrom.EnableQuotelessLiterals;
            Precedence = cloneFrom.Precedence;
        }

        /// <summary>
        /// Creates a default instance of <see cref="ParameterSymbol"/>.
        /// </summary>
        internal ParameterSymbol(string name, string? replaces = null) : base(name, replaces)
        {
            Precedence = TemplateParameterPrecedence.Default;
        }

        public override string Type => TypeName;

        /// <summary>
        /// Gets the friendly name of the symbol to be displayed to the user.
        /// </summary>
        public string? DisplayName
        {
            get => _displayName;
            internal init => _displayName = value;
        }

        /// <summary>
        /// Gets the description of the parameter.
        /// </summary>
        public string? Description
        {
            get => _description;
            internal init => _description = value;
        }

        /// <summary>
        /// If this is set, the option can be provided without a value. It will be given this value.
        /// </summary>
        public string? DefaultIfOptionWithoutValue { get; internal init; }

        /// <summary>
        /// If this is set, it's allowed to specify multiple values of that parameter.
        /// </summary>
        public bool AllowMultipleValues { get; internal init; }

        /// <summary>
        ///  If this is set, it's allowed to specify choice literals without quotation within conditions.
        /// </summary>
        public bool EnableQuotelessLiterals { get; internal init; }

        public TemplateParameterPrecedence Precedence { get; internal init; }

        public string? IsEnabledCondition { get; internal init; }

        public string? IsRequiredCondition { get; internal init; }

        public IReadOnlyDictionary<string, ParameterChoice>? Choices
        {
            get => _choices;

            internal init => _choices = value?.CloneIfDifferentComparer(StringComparer.OrdinalIgnoreCase);
        }

        // only relevant for choice datatype
        internal bool IsTag { get; init; }

        // only relevant for choice datatype
        internal string? TagName { get; init; }

        internal static ParameterSymbol FromDeprecatedConfigTag(string name, string value)
        {
            ParameterSymbol symbol = new ParameterSymbol(name)
            {
                DefaultValue = value,
                DataType = "choice",
                Precedence = GetPrecedence(false, true, true, null, null),
                Choices = new Dictionary<string, ParameterChoice>()
                {
                    { value, new ParameterChoice(string.Empty, string.Empty) }
                },
                Forms = SymbolValueFormsModel.Default
            };

            return symbol;
        }

        internal void Localize(IParameterSymbolLocalizationModel locModel)
        {
            _displayName = locModel.DisplayName ?? _displayName;
            _description = locModel.Description ?? _description;
            if (Choices == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ParameterChoice> choice in Choices)
            {
                if (locModel.Choices.TryGetValue(choice.Key, out ParameterChoiceLocalizationModel? locChoiceModel))
                {
                    choice.Value.Localize(locChoiceModel);
                }
            }

        }

        private static TemplateParameterPrecedence GetPrecedence(bool isRequired, JObject jObject)
        {
            string? isRequiredCondition = ParseIsRequiredConditionField(jObject);

            // Initialize IsEnabled - as a condition or a constant
            string? isEnabledCondition = null;
            bool isEnabled = true;
            if (jObject != null && jObject.TryGetValue("IsEnabled", StringComparison.OrdinalIgnoreCase, out JToken? isEnabledToken))
            {
                if (isEnabledToken!.TryParseBool(out bool enabledConst))
                {
                    isEnabled = enabledConst;
                }
                else if (isEnabledToken.Type == JTokenType.String)
                {
                    isEnabledCondition = isEnabledToken.ToString();
                }
            }

            return GetPrecedence(isRequired, isEnabled, false, isRequiredCondition, isEnabledCondition);
        }

        private static TemplateParameterPrecedence GetPrecedence(bool isRequired, bool isEnabled, bool isTag, string? isRequiredCondition, string? isEnabledCondition)
        {
            // If enable condition is set - parameter is conditionally disabled (regardless if require condition is set or not)
            // Conditionally required is if and only if the only require condition is set

            if (!isEnabled)
            {
                return new TemplateParameterPrecedence(PrecedenceDefinition.Disabled);
            }

            if (!string.IsNullOrEmpty(isEnabledCondition))
            {
                return new TemplateParameterPrecedence(PrecedenceDefinition.ConditionalyDisabled, isRequiredCondition, isEnabledCondition, isRequired);
            }

            if (isTag)
            {
                return new TemplateParameterPrecedence(PrecedenceDefinition.Implicit);
            }

            if (!string.IsNullOrEmpty(isRequiredCondition))
            {
                return new TemplateParameterPrecedence(PrecedenceDefinition.ConditionalyRequired, isRequiredCondition, null);
            }

            if (isRequired)
            {
                return new TemplateParameterPrecedence(PrecedenceDefinition.Required, null, null, true);
            }

            return TemplateParameterPrecedence.Default;
        }

        private static string? ParseIsRequiredConditionField(JToken token)
        {
            if (!token.TryGetValue(nameof(IsRequired), out JToken? isRequiredToken))
            {
                return null;
            }

            // Attribute parsable as a bool - so we do not want to present it as a condition
            if (isRequiredToken!.TryParseBool(out _))
            {
                return null;
            }

            if (isRequiredToken!.Type != JTokenType.String)
            {
                throw new ArgumentException(string.Format(LocalizableStrings.Symbol_Error_IsRequiredNotABoolOrString, isRequiredToken));
            }

            return isRequiredToken.ToString();
        }
    }
}
