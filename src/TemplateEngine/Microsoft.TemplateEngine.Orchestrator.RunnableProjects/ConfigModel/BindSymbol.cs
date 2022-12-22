// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the symbol of type "bind".
    /// </summary>
    public sealed class BindSymbol : BaseReplaceSymbol
    {
        internal const string TypeName = "bind";

        internal BindSymbol(string name, string binding) : base(name, null)
        {
            if (string.IsNullOrWhiteSpace(binding))
            {
                throw new ArgumentException($"'{nameof(binding)}' cannot be null or whitespace.", nameof(binding));
            }

            Binding = binding;
        }

        internal BindSymbol(string name, JObject jObject) : base(jObject, name)
        {
            string? binding = jObject.ToString(nameof(Binding));
            if (string.IsNullOrWhiteSpace(binding))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, BindSymbol.TypeName, nameof(Binding).ToLowerInvariant()), name);
            }

            Binding = binding!;
            DefaultValue = jObject.ToString(nameof(DefaultValue));
            DataType = jObject.ToString(nameof(DataType));
        }

        /// <summary>
        /// Gets the name of the host property or the environment variable which will provide the value of this symbol.
        /// </summary>
        public string Binding { get; }

        /// <inheritdoc />
        public override string Type => TypeName;

        /// <summary>
        /// Gets default value of the symbol.
        /// Corresponds to "defaultValue" JSON property.
        /// </summary>
        public string? DefaultValue { get; internal init; }

        /// <summary>
        /// Gets the data type of the symbol.
        /// Corresponds to "datatype" JSON property.
        /// </summary>
        public string? DataType { get; internal init; }
    }
}
