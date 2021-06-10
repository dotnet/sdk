// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class GeneratedSymbol : ISymbolModel
    {
        internal const string TypeName = "generated";

        internal GeneratedSymbol(JObject jObject)
        {
            Binding = jObject.ToString(nameof(Binding));
            Replaces = jObject.ToString(nameof(Replaces));
            FileRename = jObject.ToString(nameof(FileRename));
            Type = jObject.ToString(nameof(Type));
            ReplacementContexts = SymbolModelConverter.ReadReplacementContexts(jObject);
            DataType = jObject.ToString(nameof(DataType));
            Generator = jObject.ToString(nameof(Generator));
            Parameters = jObject.ToJTokenDictionary(StringComparer.Ordinal, nameof(Parameters));
        }

        public string Binding { get; init; }

        public string Replaces { get; init; }

        public string FileRename { get; init; }

        public string Type { get; init; }

        public IReadOnlyList<IReplacementContext> ReplacementContexts { get; init; }

        internal string DataType { get; init; }

        // Refers to the Type property value of a concrete IMacro
        internal string Generator { get; init; }

        internal IReadOnlyDictionary<string, JToken> Parameters { get; init; }
    }
}
