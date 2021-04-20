// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SymbolModelConverter
    {
        internal const string BindSymbolTypeName = "bind";

        // Note: Only ParameterSymbol has a Description property, this it's the only one that gets localization
        // TODO: change how localization gets merged in, don't do it here.
        internal static ISymbolModel GetModelForObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            switch (jObject.ToString(nameof(ISymbolModel.Type)))
            {
                case ParameterSymbol.TypeName:
                    return ParameterSymbol.FromJObject(jObject, localization, defaultOverride);
                case DerivedSymbol.TypeName:
                    return DerivedSymbol.FromJObject(jObject, localization, defaultOverride);
                case ComputedSymbol.TypeName:
                    return ComputedSymbol.FromJObject(jObject);
                case BindSymbolTypeName:
                case GeneratedSymbol.TypeName:
                    return GeneratedSymbol.FromJObject(jObject);
                default:
                    return null;
            }
        }

        internal static IReadOnlyList<IReplacementContext> ReadReplacementContexts(JObject jObject)
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
