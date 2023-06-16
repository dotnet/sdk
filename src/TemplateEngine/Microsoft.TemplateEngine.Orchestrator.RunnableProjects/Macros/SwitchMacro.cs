// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class SwitchMacro : BaseGeneratedSymbolMacro<SwitchMacroConfig>
    {
        public override Guid Id { get; } = new Guid("B57D64E0-9B4F-4ABE-9366-711170FD5294");

        public override string Type => "switch";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, SwitchMacroConfig config)
        {
            string result = string.Empty;   // default if no condition assigns a value

            foreach ((string? condition, string value) in config.Cases)
            {
                if (string.IsNullOrEmpty(condition))
                {
                    // no condition, this is the default.
                    result = value;
                    break;
                }

                if (config.Evaluator(environmentSettings.Host.Logger, condition!, variableCollection))
                {
                    result = value;
                    break;
                }
            }
            variableCollection[config.VariableName] = result;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Assigned variable '{var}' to '{value}'.", nameof(SwitchMacro), config.VariableName, result);
        }

        public override SwitchMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
