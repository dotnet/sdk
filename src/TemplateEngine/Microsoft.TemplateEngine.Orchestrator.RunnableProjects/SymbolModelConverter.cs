// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SymbolModelConverter
    {
        // Note: Only ParameterSymbol has a Description property, this it's the only one that gets localization
        // TODO: change how localization gets merged in, don't do it here.
        internal static BaseSymbol GetModelForObject(string name, JObject jObject, ILogger logger, string defaultOverride)
        {
            try
            {
                switch (jObject.ToString(nameof(BaseSymbol.Type)))
                {
                    case ParameterSymbol.TypeName:
                        return new ParameterSymbol(name, jObject, defaultOverride);
                    case DerivedSymbol.TypeName:
                        return new DerivedSymbol(name, jObject, defaultOverride);
                    case ComputedSymbol.TypeName:
                        return new ComputedSymbol(name, jObject);
                    case BindSymbol.TypeName:
                        return new BindSymbol(name, jObject);
                    case GeneratedSymbol.TypeName:
                        return new GeneratedSymbol(name, jObject);
                    default:
                        return null;
                }
            }
            catch (TemplateAuthoringException ex)
            {
                logger.LogWarning(ex.Message);
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
                return Array.Empty<IReplacementContext>();
            }
        }
    }
}
