using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class GeneratedSymbol : ISymbolModel
    {
        internal const string TypeName = "generated";

        internal string DataType { get; set; }

        public string Binding { get; set; }

        public string Replaces { get; set; }

        public string FileRename { get; set; }

        // Refers to the Type property value of a concrete IMacro
        internal string Generator { get; set; }

        internal IReadOnlyDictionary<string, JToken> Parameters { get; set; }

        public string Type { get; set; }

        public IReadOnlyList<IReplacementContext> ReplacementContexts { get; set; }

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
