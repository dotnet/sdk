// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the symbol of type "derived".
    /// </summary>
    public sealed class DerivedSymbol : BaseValueSymbol
    {
        internal const string TypeName = "derived";

        internal DerivedSymbol(string name, string valueTransform, string valueSource, string? replaces = null) : base(name, replaces)
        {
            if (string.IsNullOrWhiteSpace(valueTransform))
            {
                throw new ArgumentException($"'{nameof(valueTransform)}' cannot be null or whitespace.", nameof(valueTransform));
            }

            if (string.IsNullOrWhiteSpace(valueSource))
            {
                throw new ArgumentException($"'{nameof(valueSource)}' cannot be null or whitespace.", nameof(valueSource));
            }

            ValueTransform = valueTransform;
            ValueSource = valueSource;
        }

        internal DerivedSymbol(string name, JObject jObject, string? defaultOverride)
            : base(name, jObject, defaultOverride)
        {
            string? valueTransform = jObject.ToString(nameof(ValueTransform));
            if (string.IsNullOrWhiteSpace(valueTransform))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, DerivedSymbol.TypeName, nameof(ValueTransform).ToLowerInvariant()), name);
            }

            ValueTransform = valueTransform!;

            string? valueSource = jObject.ToString(nameof(ValueSource));
            if (string.IsNullOrWhiteSpace(valueSource))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, DerivedSymbol.TypeName, nameof(ValueSource).ToLowerInvariant()), name);
            }

            ValueSource = valueSource!;
        }

        /// <inheritdoc/>
        public override string Type => TypeName;

        /// <summary>
        /// Defines the transformation to be applied to <see cref="ValueSource"/> (value form).
        /// </summary>
        public string ValueTransform { get; }

        /// <summary>
        /// Defines the variable to be transformed.
        /// </summary>
        public string ValueSource { get; }
    }
}
