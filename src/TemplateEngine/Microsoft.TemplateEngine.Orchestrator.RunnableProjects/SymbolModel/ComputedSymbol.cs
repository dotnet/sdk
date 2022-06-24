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
    internal class ComputedSymbol : BaseSymbol
    {
        internal const string TypeName = "computed";

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

        public string Value { get; init; }

        internal override string Type => TypeName;

        internal string? Evaluator { get; init; }

    }
}
