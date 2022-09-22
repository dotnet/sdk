// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig
{
    internal class MacrosOperationConfig
    {
        private static IReadOnlyDictionary<string, IMacro>? s_macroObjects;
        private static IReadOnlyDictionary<string, IDeferredMacro>? s_deferredMacroObjects;

        // Warning: if there are unknown macro "types", they are quietly ignored here.
        // This applies to both the regular and deferred macros.
        internal IEnumerable<IOperationProvider> ProcessMacros(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IMacroConfig> macroConfigs, IVariableCollection variables)
        {
            EnsureMacros(environmentSettings.Components);
            EnsureDeferredMacros(environmentSettings.Components);

            IList<IMacroConfig> allMacroConfigs = new List<IMacroConfig>(macroConfigs);
            IList<GeneratedSymbolDeferredMacroConfig> deferredConfigList = new List<GeneratedSymbolDeferredMacroConfig>();

            // run the macros that are already setup, stash the deferred ones for afterwards
            foreach (IMacroConfig config in allMacroConfigs)
            {
                if (config is GeneratedSymbolDeferredMacroConfig deferredConfig)
                {
                    deferredConfigList.Add(deferredConfig);
                    continue;
                }

                if (s_macroObjects!.TryGetValue(config.Type, out IMacro macroObject))
                {
                    macroObject.EvaluateConfig(environmentSettings, variables, config);
                }
            }

            List<Tuple<IMacro, IMacroConfig>> deferredConfigs = new List<Tuple<IMacro, IMacroConfig>>();

            // Set up all deferred macro configurations - this must be done separately from running them
            //  as certain generation types may require (like generating port numbers) that a shared resource
            //  be held in a particular state to influence the production of other values
            foreach (GeneratedSymbolDeferredMacroConfig deferredConfig in deferredConfigList)
            {
                if (s_deferredMacroObjects!.TryGetValue(deferredConfig.Type, out IDeferredMacro deferredMacroObject))
                {
                    deferredConfigs.Add(Tuple.Create((IMacro)deferredMacroObject, deferredMacroObject.CreateConfig(environmentSettings, deferredConfig)));
                }
            }

            foreach (Tuple<IMacro, IMacroConfig> config in deferredConfigs)
            {
                config.Item1.EvaluateConfig(environmentSettings, variables, config.Item2);
            }

            return Array.Empty<IOperationProvider>();
        }

        private static void EnsureMacros(IComponentManager componentManager)
        {
            if (s_macroObjects == null)
            {
                Dictionary<string, IMacro> macroObjects = new Dictionary<string, IMacro>();

                foreach (IMacro macro in componentManager.OfType<IMacro>())
                {
                    macroObjects[macro.Type] = macro;
                }

                s_macroObjects = macroObjects;
            }
        }

        private static void EnsureDeferredMacros(IComponentManager componentManager)
        {
            if (s_deferredMacroObjects == null)
            {
                Dictionary<string, IDeferredMacro> deferredMacroObjects = new Dictionary<string, IDeferredMacro>();

                foreach (IDeferredMacro deferredMacro in componentManager.OfType<IDeferredMacro>())
                {
                    deferredMacroObjects[deferredMacro.Type] = deferredMacro;
                }

                s_deferredMacroObjects = deferredMacroObjects;
            }
        }
    }
}
