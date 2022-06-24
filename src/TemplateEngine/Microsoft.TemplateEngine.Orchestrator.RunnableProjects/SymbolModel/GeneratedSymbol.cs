// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class GeneratedSymbol : BaseReplaceSymbol
    {
        internal const string TypeName = "generated";

        internal GeneratedSymbol(string name, JObject jObject) : base(jObject, name)
        {
            string? generator = jObject.ToString(nameof(Generator));
            if (string.IsNullOrWhiteSpace(generator))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.SymbolModel_Error_MandatoryPropertyMissing, name, GeneratedSymbol.TypeName, nameof(Generator).ToLowerInvariant()), name);
            }

            Generator = generator!;
            DataType = jObject.ToString(nameof(DataType));
            Parameters = jObject.ToJTokenDictionary(StringComparer.Ordinal, nameof(Parameters));
        }

        internal override string Type => TypeName;

        internal string? DataType { get; init; }

        /// <summary>
        /// Refers to the Type property value of a concrete IMacro.
        /// </summary>
        internal string Generator { get; init; }

        internal IReadOnlyDictionary<string, JToken> Parameters { get; init; }
    }
}
