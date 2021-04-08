using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class MacrosOperationConfig
    {
        private static IReadOnlyDictionary<string, IMacro> _macroObjects;
        private static IReadOnlyDictionary<string, IDeferredMacro> _deferredMacroObjects;

        // Warning: if there are unknown macro "types", they are quietly ignored here.
        // This applies to both the regular and deferred macros.
        internal IEnumerable<IOperationProvider> ProcessMacros(IEngineEnvironmentSettings environmentSettings, IComponentManager componentManager, IReadOnlyList<IMacroConfig> macroConfigs, IVariableCollection variables, IParameterSet parameters)
        {
            EnsureMacros(componentManager);
            EnsureDeferredMacros(componentManager);

            ParameterSetter setter = (p, value) =>
            {
                ((RunnableProjectGenerator.ParameterSet)parameters).AddParameter(p);
                parameters.ResolvedValues[p] = RunnableProjectGenerator.InternalConvertParameterValueToType(environmentSettings, p, value, out bool valueResolutionError);
                // TODO: consider checking the valueResolutionError and act on it, if needed.
                // Should be safe to ignore, params should be verified by the time this occurs.
            };

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

                if (_macroObjects.TryGetValue(config.Type, out IMacro macroObject))
                {
                    macroObject.EvaluateConfig(environmentSettings, variables, config, parameters, setter);
                }
            }

            List<Tuple<IMacro, IMacroConfig>> deferredConfigs = new List<Tuple<IMacro, IMacroConfig>>();

            // Set up all deferred macro configurations - this must be done separately from running them
            //  as certain generation types may require (like generating port numbers) that a shared resource
            //  be held in a particular state to influence the production of other values
            foreach (GeneratedSymbolDeferredMacroConfig deferredConfig in deferredConfigList)
            {
                IDeferredMacro deferredMacroObject;
                if (_deferredMacroObjects.TryGetValue(deferredConfig.Type, out deferredMacroObject))
                {
                    deferredConfigs.Add(Tuple.Create((IMacro)deferredMacroObject, deferredMacroObject.CreateConfig(environmentSettings, deferredConfig)));
                }
            }

            foreach(Tuple<IMacro, IMacroConfig> config in deferredConfigs)
            {
                config.Item1.EvaluateConfig(environmentSettings, variables, config.Item2, parameters, setter);
            }

            return Empty<IOperationProvider>.List.Value;
        }

        private static void EnsureMacros(IComponentManager componentManager)
        {
            if (_macroObjects == null)
            {
                Dictionary<string, IMacro> macroObjects = new Dictionary<string, IMacro>();

                foreach (IMacro macro in componentManager.OfType<IMacro>())
                {
                    macroObjects[macro.Type] = macro;
                }

                _macroObjects = macroObjects;
            }
        }

        private static void EnsureDeferredMacros(IComponentManager componentManager)
        {
            if (_deferredMacroObjects == null)
            {
                Dictionary<string, IDeferredMacro> deferredMacroObjects = new Dictionary<string, IDeferredMacro>();

                foreach (IDeferredMacro deferredMacro in componentManager.OfType<IDeferredMacro>())
                {
                    deferredMacroObjects[deferredMacro.Type] = deferredMacro;
                }

                _deferredMacroObjects = deferredMacroObjects;
            }
        }
    }
}
