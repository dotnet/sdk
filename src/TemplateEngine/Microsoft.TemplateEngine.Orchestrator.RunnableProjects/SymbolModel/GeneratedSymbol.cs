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

        public string Binding { get; set; }
        public string Replaces { get; set; }
        public string FileRename { get; set; }
        public string Type { get; set; }
        public IReadOnlyList<IReplacementContext> ReplacementContexts { get; set; }
        internal string DataType { get; set; }

        // Refers to the Type property value of a concrete IMacro
        internal string Generator { get; set; }

        internal IReadOnlyDictionary<string, JToken> Parameters { get; set; }

        internal static GeneratedSymbol FromJObject(JObject jObject)
        {
            GeneratedSymbol sym = new GeneratedSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                Generator = jObject.ToString(nameof(Generator)),
                DataType = jObject.ToString(nameof(DataType)),
                Parameters = jObject.ToJTokenDictionary(StringComparer.Ordinal, nameof(Parameters)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces)),
                FileRename = jObject.ToString(nameof(FileRename)),
                ReplacementContexts = SymbolModelConverter.ReadReplacementContexts(jObject)
            };

            return sym;
        }
    }
}
