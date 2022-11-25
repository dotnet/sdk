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
        /// Processes the macros defined in <paramref name="runConfig"/>.
        /// </summary>
        /// <exception cref="TemplateAuthoringException">when <see cref="IGeneratedSymbolMacro"/> config is invalid.</exception>
        /// <exception cref="MacroProcessingException">when the error occurs when macro is processed.</exception>
        internal static void ProcessMacros(
            IEngineEnvironmentSettings environmentSettings,
            GlobalRunConfig runConfig,
            IVariableCollection variables)
        {
            bool deterministicMode = IsDeterministicModeEnabled(environmentSettings);

            foreach (BaseMacroConfig config in runConfig.ComputedMacros)
            {
                try
                {
                    if (deterministicMode)
                    {
                        config.EvaluateDeterministically(environmentSettings, variables);
                    }
                    else
                    {
                        config.Evaluate(environmentSettings, variables);
                    }
                }
                catch (Exception ex)
                {
                    throw new MacroProcessingException(config, ex);
                }
            }

            if (!runConfig.GeneratedSymbolMacros.Any())
            {
                return;
            }

            Dictionary<string, IGeneratedSymbolMacro> generatedSymbolMacros = environmentSettings.Components.OfType<IGeneratedSymbolMacro>().ToDictionary(m => m.Type, m => m);
            foreach (IGeneratedSymbolConfig config in runConfig.GeneratedSymbolMacros)
            {
                if (generatedSymbolMacros.TryGetValue(config.Type, out IGeneratedSymbolMacro generatedSymbolMacro))
                {
                    try
                    {
                        if (deterministicMode && generatedSymbolMacro is IDeterministicModeMacro<IGeneratedSymbolConfig> deterministicMacro)
                        {
                            deterministicMacro.EvaluateDeterministically(environmentSettings, variables, config);
                        }
                        else
                        {
                            generatedSymbolMacro.Evaluate(environmentSettings, variables, config);
                        }
                    }
                    //TemplateAuthoringException means that config was invalid, just pass it.
                    catch (Exception ex) when (ex is not TemplateAuthoringException)
                    {
                        throw new MacroProcessingException(config, ex);
                    }
                }
                else
                {
                    environmentSettings.Host.Logger.LogWarning(LocalizableStrings.MacroProcessor_Warning_UnknownMacro, config.VariableName, config.Type);
                }
            }
        }

        private static bool IsDeterministicModeEnabled(IEngineEnvironmentSettings environmentSettings)
        {
            string? unparsedValue = environmentSettings.Environment.GetEnvironmentVariable("TEMPLATE_ENGINE_ENABLE_DETERMINISTIC_MODE");
            return bool.TryParse(unparsedValue, out bool result) && result;
        }
    }
}
