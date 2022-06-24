// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class DerivedSymbol : BaseValueSymbol
    {
        internal const string TypeName = "derived";

        internal DerivedSymbol(string name, JObject jObject, string defaultOverride)
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

        internal override string Type => TypeName;

        internal string ValueTransform { get; init; }

        internal string ValueSource { get; init; }
    }
}
