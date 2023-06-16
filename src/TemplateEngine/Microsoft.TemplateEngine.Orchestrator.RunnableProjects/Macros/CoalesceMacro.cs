// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class CoalesceMacro : BaseGeneratedSymbolMacro<CoalesceMacroConfig>
    {
        public override string Type => "coalesce";

        public override Guid Id { get; } = new("11C6EACF-8D24-42FD-8FC6-84063FCD8F14");

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, CoalesceMacroConfig config)
        {
            if (variableCollection.TryGetValue(config.SourceVariableName, out object currentSourceValue) && currentSourceValue != null)
            {
                // The value is equal to the coalesce recognized default value (see coalesce macro doc for details).
                if (config.DefaultValue != null && currentSourceValue.ToString().Equals(config.DefaultValue))
                {
                    environmentSettings.Host.Logger.LogDebug("[{macro}]: '{var}': source value '{source}' is not used, because it is equal to default value '{default}'.", nameof(CoalesceMacro), config.VariableName, currentSourceValue, config.DefaultValue);
                }
                // The value is not specified by user: either coming from default value or host specific default value, etc.
                else if (variableCollection is ParameterBasedVariableCollection paramsVariableCollection &&
                    paramsVariableCollection.ParameterSetData.TryGetValue(config.SourceVariableName, out ParameterData? parameterData) &&
                    parameterData!.DataSource is not DataSource.User and not DataSource.DefaultIfNoValue)
                {
                    environmentSettings.Host.Logger.LogDebug(
                        "[{macro}]: '{var}': source value '{source}' not specified by user (data source: '{dataSource}'), fall back.",
                        nameof(CoalesceMacro),
                        config.VariableName,
                        currentSourceValue,
                        parameterData.DataSource);
                }
                else if (currentSourceValue is string str && string.IsNullOrEmpty(str))
                {
                    //do nothing, empty value for string is equivalent to null.
                    environmentSettings.Host.Logger.LogDebug("[{macro}]: '{var}': source value '{source}' is an empty string, fall back.", nameof(CoalesceMacro), config.VariableName, currentSourceValue);
                }
                else
                {
                    variableCollection[config.VariableName] = currentSourceValue;
                    environmentSettings.Host.Logger.LogDebug("[{macro}]: Assigned variable '{var}' to '{value}'.", nameof(CoalesceMacro), config.VariableName, currentSourceValue);
                    return;
                }
            }
            if (variableCollection.TryGetValue(config.FallbackVariableName, out object currentFallbackValue) && currentFallbackValue != null)
            {
                variableCollection[config.VariableName] = currentFallbackValue;
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Assigned variable '{var}' to fallback value '{value}'.", nameof(CoalesceMacro), config.VariableName, currentFallbackValue);
                return;
            }
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was not assigned, neither source nor fallback variable was found.", nameof(CoalesceMacro), config.VariableName);
            config.MacroErrors.Add(string.Format(LocalizableStrings.CoalesceMacro_Exception_MissedVariables, nameof(CoalesceMacro), config.VariableName));
        }

        public override CoalesceMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig)
            => new CoalesceMacroConfig(this, deferredConfig);
    }
}
