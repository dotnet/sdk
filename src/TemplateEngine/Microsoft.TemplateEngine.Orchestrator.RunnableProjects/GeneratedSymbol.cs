using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GeneratedSymbol : ISymbolModel
    {
        // NOTE (scp 2016-09-16): Not used (i think). Only here to satisfy the interface
        public string Binding { get; set; }

        // NOTE (scp 2016-09-16): Not used (i think). Only here to satisfy the interface
        public string Replaces { get; set; }

        // Refers to the Type property value of a concrete IMacro
        public string Generator { get; set; }

        public IReadOnlyDictionary<string, JToken> Parameters { get; set; }

        public string Type { get; set; }

        public IReadOnlyList<IReplacementContext> ReplacementContexts { get; set; }

        public static ISymbolModel FromJObject(JObject jObject)
        {
            GeneratedSymbol sym = new GeneratedSymbol
            {
                Binding = jObject.ToString(nameof(Binding)),
                Generator = jObject.ToString(nameof(Generator)),
                Parameters = jObject.ToJTokenDictionary(StringComparer.Ordinal, nameof(Parameters)),
                Type = jObject.ToString(nameof(Type)),
                Replaces = jObject.ToString(nameof(Replaces)),
                ReplacementContexts = ReadReplacementContexts(jObject)
            };

            return sym;
        }

        private static IReadOnlyList<IReplacementContext> ReadReplacementContexts(JObject jObject)
        {
            JArray onlyIf = jObject.Get<JArray>("onlyIf");

            if (onlyIf != null)
            {
                List<IReplacementContext> contexts = new List<IReplacementContext>();
                foreach (JToken entry in onlyIf.Children())
                {
                    if (!(entry is JObject x))
                    {
                        continue;
                    }

                    string before = entry.ToString("before");
                    string after = entry.ToString("after");
                    contexts.Add(new ReplacementContext(before, after));
                }

                return contexts;
            }
            else
            {
                return Empty<IReplacementContext>.List.Value;
            }
        }
    }
}