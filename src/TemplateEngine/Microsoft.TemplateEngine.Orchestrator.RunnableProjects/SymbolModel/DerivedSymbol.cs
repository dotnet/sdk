// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class DerivedSymbol : BaseValueSymbol
    {
        internal const string TypeName = "derived";

        internal DerivedSymbol(JObject jObject, string defaultOverride)
            : base(jObject, defaultOverride)
        {
            ValueTransform = jObject.ToString(nameof(ValueTransform));
            ValueSource = jObject.ToString(nameof(ValueSource));
        }

        internal string ValueTransform { get; init; }

        internal string ValueSource { get; init; }
    }
}
