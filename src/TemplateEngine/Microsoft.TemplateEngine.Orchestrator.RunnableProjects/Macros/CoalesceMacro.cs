// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class CoalesceMacro : IMacro, IDeferredMacro
    {
        public string Type => "coalesce";

        public Guid Id => new Guid("11C6EACF-8D24-42FD-8FC6-84063FCD8F14");

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not GeneratedSymbolDeferredMacroConfig deferredConfig)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            string? sourceVariableName = null;
            if (deferredConfig.Parameters.TryGetValue("sourceVariableName", out JToken sourceVariableToken) && sourceVariableToken.Type == JTokenType.String)
            {
                sourceVariableName = sourceVariableToken.ToString();
            }

            string? defaultValue = null;
            if (deferredConfig.Parameters.TryGetValue("defaultValue", out JToken defaultValueToken) && defaultValueToken.Type == JTokenType.String)
            {
                defaultValue = defaultValueToken.ToString();
            }

            string? fallbackVariableName = null;
            if (deferredConfig.Parameters.TryGetValue("fallbackVariableName", out JToken fallbackVariableNameToken) && fallbackVariableNameToken.Type == JTokenType.String)
            {
                fallbackVariableName = fallbackVariableNameToken.ToString();
            }

            IMacroConfig realConfig = new CoalesceMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariableName, defaultValue, fallbackVariableName);
            return realConfig;
        }

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config)
        {
            if (config is not CoalesceMacroConfig realConfig)
            {
                throw new InvalidCastException("Unable to cast config as a CoalesceMacroConfig");
            }

            object? targetValue = null;
            if (!string.IsNullOrEmpty(realConfig.SourceVariableName)
                && vars.TryGetValue(realConfig.SourceVariableName!, out object currentSourceValue)
                && !Equals(currentSourceValue ?? string.Empty, realConfig.DefaultValue ?? string.Empty))
            {
                targetValue = currentSourceValue;
            }
            else
            {
                if (!string.IsNullOrEmpty(realConfig.FallbackVariableName)
                    && !vars.TryGetValue(realConfig.FallbackVariableName!, out targetValue))
                {
                    environmentSettings.Host.Logger.LogDebug("Unable to find a variable to fall back to called " + realConfig.FallbackVariableName);
                    targetValue = realConfig.DefaultValue;
                }
            }
            if (targetValue is not null)
            {
                vars[config.VariableName] = targetValue.ToString();
            }
        }
    }
}
