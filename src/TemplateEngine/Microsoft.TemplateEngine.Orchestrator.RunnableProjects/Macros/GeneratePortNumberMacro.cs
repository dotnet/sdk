// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GeneratePortNumberMacro : BaseNondeterministicGenSymMacro<GeneratePortNumberConfig>
    {
        public override Guid Id { get; } = new Guid("D49B3690-B1E5-410F-A260-E1D7E873D8B2");

        public override string Type => "port";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, GeneratePortNumberConfig config)
        {
            vars[config.VariableName] = config.Port;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(GeneratePortNumberMacro), config.VariableName, config.Port);
        }

        public override void EvaluateDeterministically(
            IEngineEnvironmentSettings environmentSettings,
            IVariableCollection variables,
            GeneratePortNumberConfig config)
        {
            variables[config.VariableName] = config.Low;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}' in deterministic mode.", nameof(GeneratePortNumberMacro), config.VariableName, config.Low);
        }

        public override GeneratePortNumberConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(environmentSettings.Host.Logger, this, deferredConfig);
    }
}
