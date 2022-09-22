// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    internal sealed class SymbolModelConverter
    {
        // Note: Only ParameterSymbol has a Description property, this it's the only one that gets localization
        // TODO: change how localization gets merged in, don't do it here.
        internal static BaseSymbol? GetModelForObject(string name, JObject jObject, ILogger? logger, string? defaultOverride)
        {
            try
            {
                return jObject.ToString(nameof(BaseSymbol.Type)) switch
                {
                    ParameterSymbol.TypeName => new ParameterSymbol(name, jObject, defaultOverride),
                    DerivedSymbol.TypeName => new DerivedSymbol(name, jObject, defaultOverride),
                    ComputedSymbol.TypeName => new ComputedSymbol(name, jObject),
                    BindSymbol.TypeName => new BindSymbol(name, jObject),
                    GeneratedSymbol.TypeName => new GeneratedSymbol(name, jObject),
                    _ => null,
                };
            }
            catch (TemplateAuthoringException ex)
            {
                logger?.LogWarning(ex.Message);
                return null;
            }
        }
    }
}
