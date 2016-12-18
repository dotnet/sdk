using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class MacrosOperationConfig
    {
        private static IReadOnlyDictionary<string, IMacro> _macroObjects;
        private static IReadOnlyDictionary<string, IDeferredMacro> _deferredMacroObjects;

        // Warning: if there are unknown macro "types", they are quietly ignored here.
        // This applies to both the regular and deferred macros.
        public IEnumerable<IOperationProvider> ProcessMacros(IComponentManager componentManager, IReadOnlyList<IMacroConfig> macroConfigs, IVariableCollection variables, IParameterSet parameters)
        {
            EnsureMacros(componentManager);
            EnsureDeferredMacros(componentManager);

            ParameterSetter setter = (p, value) =>
            {
                ((RunnableProjectGenerator.ParameterSet)parameters).AddParameter(p);
                parameters.ResolvedValues[p] = RunnableProjectGenerator.InternalConvertParameterValueToType(p, value);
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
                    macroObject.EvaluateConfig(variables, config, parameters, setter);
                }
            }

            // run the deferred macros
            foreach (GeneratedSymbolDeferredMacroConfig deferredConfig in deferredConfigList)
            {
                IDeferredMacro deferredMacroObject;
                if (_deferredMacroObjects.TryGetValue(deferredConfig.Type, out deferredMacroObject))
                {
                    deferredMacroObject.EvaluateDeferredConfig(variables, deferredConfig, parameters, setter);
                }
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