// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class CaseChangeMacro : BaseGeneratedSymbolMacro<CaseChangeMacroConfig>
    {
        public override Guid Id { get; } = new Guid("10919118-4E13-4FA9-825C-3B4DA855578E");

        public override string Type => "casing";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, CaseChangeMacroConfig config)
        {
            string value = string.Empty;
            if (!variableCollection.TryGetValue(config.Source, out object? working))
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Source variable '{sourceVar}' was not found, skipping processing for macro '{var}'.", nameof(CaseChangeMacro), config.Source, config.VariableName);
                return;
            }
            if (working == null)
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: The value of source variable '{sourceVar}' is null, skipping processing for macro '{var}'.", nameof(CaseChangeMacro), config.Source, config.VariableName);
                return;
            }
            value = working.ToString();
            value = config.ToLower ? value.ToLowerInvariant() : value.ToUpperInvariant();
            variableCollection[config.VariableName] = value;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Assigned variable '{var}' to '{value}'.", nameof(CaseChangeMacro), config.VariableName, value);
        }

        public override CaseChangeMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
