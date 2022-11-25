// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    public abstract class BaseValueSymbol : BaseReplaceSymbol
    {
        /// <summary>
        /// Initializes this instance with given JSON data.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="jObject"></param>
        /// <param name="defaultOverride"></param>
        /// <param name="symbolConditionsSupported"></param>
        private protected BaseValueSymbol(string name, JObject jObject, string? defaultOverride, bool symbolConditionsSupported = false) : base(jObject, name)
        {
            DefaultValue = defaultOverride ?? jObject.ToString(nameof(DefaultValue));
            IsRequired = ParseIsRequiredField(jObject, !symbolConditionsSupported);
            DataType = jObject.ToString(nameof(DataType));
            if (!jObject.TryGetValue(nameof(Forms), StringComparison.OrdinalIgnoreCase, out JToken? formsToken) || formsToken is not JObject formsObject)
            {
                // no value forms explicitly defined, use the default ("identity")
                Forms = SymbolValueFormsModel.Default;
            }
            else
            {
                // the config defines forms for the symbol. Use them.
                Forms = SymbolValueFormsModel.FromJObject(formsObject);
            }
        }

        private protected BaseValueSymbol(BaseValueSymbol clone, SymbolValueFormsModel formsFallback) : base(clone)
        {
            DefaultValue = clone.DefaultValue;
            Forms = clone.Forms.GlobalForms.Count != 0 ? clone.Forms : formsFallback;
            IsRequired = clone.IsRequired;
            DataType = clone.DataType;
        }

        private protected BaseValueSymbol(string name, string? replaces) : base(name, replaces)
        {
            Forms = SymbolValueFormsModel.Default;
        }

        /// <summary>
        /// Gets default value of the symbol.
        /// Corresponds to "defaultValue" JSON property.
        /// </summary>
        public string? DefaultValue { get; internal init; }

        /// <summary>
        /// Gets the forms defined for the symbol
        /// Corresponds to "forms" JSON property.
        /// </summary>
        public SymbolValueFormsModel Forms { get; internal init; }

        /// <summary>
        /// Specifies if the symbol is required.
        /// Corresponds to "isRequired" JSON property.
        /// </summary>
        public bool IsRequired { get; internal init; }

        /// <summary>
        /// Gets the data type of the symbol.
        /// Corresponds to "datatype" JSON property.
        /// </summary>
        public string? DataType { get; internal init; }

        private protected bool TryGetIsRequiredField(JToken token, out bool result)
        {
            result = false;
            return (token.Type == JTokenType.Boolean || token.Type == JTokenType.String)
                   &&
                   bool.TryParse(token.ToString(), out result);
        }

        private bool ParseIsRequiredField(JToken token, bool throwOnError)
        {
            if (!token.TryGetValue(nameof(IsRequired), out JToken? isRequiredToken))
            {
                return false;
            }

            if (
                !TryGetIsRequiredField(isRequiredToken!, out bool value)
                &&
                throwOnError)
            {
                throw new ArgumentException(string.Format(LocalizableStrings.Symbol_Error_IsRequiredNotABool, isRequiredToken));
            }

            return value;
        }
    }
}
