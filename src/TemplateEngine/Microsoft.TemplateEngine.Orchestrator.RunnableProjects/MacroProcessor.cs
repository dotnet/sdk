// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal static class MacroProcessor
    {
        // Warning: if there are unknown macro "types", they are quietly ignored here.
        // This applies to both the regular and deferred macros.
        internal static void ProcessMacros(
            IEngineEnvironmentSettings environmentSettings,
            GlobalRunConfig runConfig,
            IVariableCollection variables)
        {
            foreach (BaseMacroConfig config in runConfig.ComputedMacros)
            {
                config.Evaluate(environmentSettings, variables);
            }

            if (!runConfig.GeneratedSymbolMacros.Any())
            {
                return;
            }

            Dictionary<string, IGeneratedSymbolMacro> generatedSymbolMacros = environmentSettings.Components.OfType<IGeneratedSymbolMacro>().ToDictionary(m => m.Type, m => m);
            foreach (IGeneratedSymbolConfig config in runConfig.GeneratedSymbolMacros)
            {
                if (generatedSymbolMacros.TryGetValue(config.Type, out IGeneratedSymbolMacro deferredMacroObject))
                {
                    deferredMacroObject.Evaluate(environmentSettings, variables, config);
                }
            }
        }
    }
}
