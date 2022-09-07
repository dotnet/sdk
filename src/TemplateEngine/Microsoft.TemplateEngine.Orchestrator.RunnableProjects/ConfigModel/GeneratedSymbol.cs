// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the symbol of type "generated".
    /// </summary>
    public sealed class GeneratedSymbol : BaseReplaceSymbol, IGeneratedSymbolConfig
    {
        internal const string TypeName = "generated";

        internal GeneratedSymbol(string name, string generator) : base(name, null)
        {
            if (string.IsNullOrWhiteSpace(generator))
            {
                throw new ArgumentException($"'{nameof(generator)}' cannot be null or whitespace.", nameof(generator));
            }
            Generator = generator;
        }

        internal GeneratedSymbol(string name, string generator, IReadOnlyDictionary<string, string> parameters, string? dataType = null) : this(name, generator)
        {
            DataType = dataType;
            Parameters = parameters;
        }

        internal GeneratedSymbol(string name, JObject jObject) : base(jObject, name)
        {
            string? generator = jObject.ToString(nameof(Generator));
            if (string.IsNullOrWhiteSpace(generator))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, TypeName, nameof(Generator).ToLowerInvariant()), name);
            }

            Generator = generator!;
            DataType = jObject.ToString(nameof(DataType));
            Parameters = jObject.ToJTokenStringDictionary(StringComparer.OrdinalIgnoreCase, nameof(Parameters));
        }

        /// <inheritdoc/>
        public override string Type => TypeName;

        /// <summary>
        /// Defines the data type of generated value.
        /// </summary>
        public string? DataType { get; internal init; }

        /// <summary>
        /// Refers to the Type property value of a concrete IMacro.
        /// </summary>
        public string Generator { get; }

        /// <summary>
        /// Defines the parameters to be used for generating the value.
        /// - the key is a parameter name
        /// - the value is a JSON value.
        /// </summary>
        public IReadOnlyDictionary<string, string> Parameters { get; internal init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string IGeneratedSymbolConfig.DataType => DataType ?? "string";

        string IMacroConfig.VariableName => Name;

        string IMacroConfig.Type => Generator;
    }
}
