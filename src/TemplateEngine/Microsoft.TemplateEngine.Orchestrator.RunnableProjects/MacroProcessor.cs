// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal static class MacroProcessor
    {
        /// <summary>
        /// Processes the macros defined in <paramref name="macroConfigs"/>.
        /// </summary>
        /// <exception cref="TemplateAuthoringException">when <see cref="IGeneratedSymbolMacro"/> config is invalid.</exception>
        /// <exception cref="MacroProcessingException">when the error occurs when macro is processed.</exception>
        internal static void ProcessMacros(
            IEngineEnvironmentSettings environmentSettings,
            IReadOnlyList<IMacroConfig> macroConfigs,
            IVariableCollection variables)
        {
            bool deterministicMode = IsDeterministicModeEnabled(environmentSettings);
            Dictionary<string, IMacro> knownMacros = environmentSettings.Components.OfType<IMacro>().ToDictionary(m => m.Type, m => m);

            foreach (IMacroConfig config in macroConfigs)
            {
                // Errors in macro dependencies are not supposed to interrupt template generation.
                // Skip this macro and add info in the output.
                if (config is IMacroConfigDependency macroWithDep && macroWithDep.Dependencies.Any())
                {
                    HashSet<IMacroConfig> dependentMacros = GetDependentMacros(config, macroConfigs);
                    if (dependentMacros.OfType<BaseMacroConfig>().Any(bmc => bmc.MacroErrors.Any()))
                    {
                        environmentSettings.Host.Logger.LogWarning(
                            string.Format(
                                LocalizableStrings.MacroProcessing_Warning_DependencyErrors,
                                config.VariableName,
                                string.Join(",", dependentMacros.OfType<BaseMacroConfig>().SelectMany(d => d.MacroErrors))));

                        continue;
                    }
                }

                try
                {
                    if (knownMacros.TryGetValue(config.Type, out IMacro executingMacro))
                    {
                        if (deterministicMode && executingMacro is IDeterministicModeMacro detMacro)
                        {
                            detMacro.EvaluateConfigDeterministically(environmentSettings, variables, config);
                        }
                        else
                        {
                            executingMacro.EvaluateConfig(environmentSettings, variables, config);
                        }
                    }
                    else
                    {
                        environmentSettings.Host.Logger.LogWarning(LocalizableStrings.MacroProcessor_Warning_UnknownMacro, config.VariableName, config.Type);
                    }
                }
                //TemplateAuthoringException means that config was invalid, just pass it.
                catch (Exception ex) when (ex is not TemplateAuthoringException)
                {
                    throw new MacroProcessingException(config, ex);
                }
            }
        }

        internal static IReadOnlyList<IMacroConfig> SortMacroConfigsByDependencies(IReadOnlyList<string> symbols, IReadOnlyList<IMacroConfig> macroConfigs)
        {
            IEnumerable<(IMacroConfig, HashSet<IMacroConfig>)> preparedMacroConfigs = macroConfigs.Select(mc =>
            {
                if (mc is IMacroConfigDependency macroWithDeps)
                {
                    macroWithDeps.ResolveSymbolDependencies(symbols);
                    return (mc, GetDependentMacros(mc, macroConfigs));
                }
                return (mc, new HashSet<IMacroConfig>());
            });

            DirectedGraph<IMacroConfig> parametersDependenciesGraph = new(preparedMacroConfigs.ToDictionary(mc => mc.Item1, mc => mc.Item2));
            if (!parametersDependenciesGraph.TryGetTopologicalSort(out IReadOnlyList<IMacroConfig> sortedConfigs) && parametersDependenciesGraph.HasCycle(out IReadOnlyList<IMacroConfig> cycle))
            {
                throw new TemplateAuthoringException(
                    string.Format(
                        LocalizableStrings.Authoring_CyclicDependencyInSymbols,
                        cycle.Select(p => p.VariableName).ToCsvString()),
                    "Symbol circle");
            }
            return sortedConfigs;
        }

        private static HashSet<IMacroConfig> GetDependentMacros(IMacroConfig config, IReadOnlyList<IMacroConfig> allMacroConfigs)
        {
            HashSet<IMacroConfig> dependents = new();
            if (config is not IMacroConfigDependency macroWithDep)
            {
                return dependents;
            }

            foreach (string dependent in macroWithDep.Dependencies)
            {
                IMacroConfig macro = allMacroConfigs.FirstOrDefault(mc => mc.VariableName == dependent);
                if (macro != null)
                {
                    dependents.Add(macro);
                }
            }
            return dependents;
        }

        private static bool IsDeterministicModeEnabled(IEngineEnvironmentSettings environmentSettings)
        {
            string? unparsedValue = environmentSettings.Environment.GetEnvironmentVariable("TEMPLATE_ENGINE_ENABLE_DETERMINISTIC_MODE");
            return bool.TryParse(unparsedValue, out bool result) && result;
        }
    }
}
