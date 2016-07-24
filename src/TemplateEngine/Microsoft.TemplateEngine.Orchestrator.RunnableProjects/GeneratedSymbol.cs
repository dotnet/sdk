using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GeneratedSymbol : ISymbolModel
    {
        public string Binding { get; set; }

        public string Generator { get; set; }

        public IReadOnlyDictionary<string, string> Parameters { get; set; }

        public string Replaces { get; set; }

        public string Type { get; set; }

        public static ISymbolModel FromJObject(JObject jObject)
        {
            GeneratedSymbol sym = new GeneratedSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                Generator = jObject.ToString(nameof(Generator)),
                Parameters = jObject.ToStringDictionary(StringComparer.Ordinal, nameof(Parameters)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces))
            };

            return sym;
        }
    }
}