// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the symbol of type "computed".
    /// </summary>
    public sealed class ComputedSymbol : BaseSymbol
    {
        internal const string TypeName = "computed";

        internal ComputedSymbol(string name, string value) : base(name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"'{nameof(value)}' cannot be null or whitespace.", nameof(value));
            }

            Value = value;
        }

        internal ComputedSymbol(string name, JObject jObject) : base(name)
        {
            string? value = jObject.ToString(nameof(Value));
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, ComputedSymbol.TypeName, nameof(Value).ToLowerInvariant()), name);
            }

            Value = value!;

            Evaluator = jObject.ToString(nameof(Evaluator));
        }

        /// <summary>
        /// Defines the value of the computed symbol.
        /// Corresponds to "value" JSON property.
        /// </summary>
        public string Value { get; }

        /// <inheritdoc/>
        public override string Type => TypeName;

        /// <summary>
        /// Defines the evaluator to be used when computing the symbol value.
        /// Corresponds to "evaluator" JSON property.
        /// </summary>
        public string? Evaluator { get; internal init; }

    }
}
