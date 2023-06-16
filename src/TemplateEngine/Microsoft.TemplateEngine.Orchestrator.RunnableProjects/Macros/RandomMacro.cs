// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RandomMacro : BaseNondeterministicGenSymMacro<RandomMacroConfig>
    {
        public override Guid Id { get; } = new Guid("011E8DC1-8544-4360-9B40-65FD916049B7");

        public override string Type => "random";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, RandomMacroConfig config)
        {
            int value = CryptoRandom.NextInt(config.Low, config.High);
            vars[config.VariableName] = value;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(RandomMacro), config.VariableName, value);
        }

        public override void EvaluateDeterministically(
            IEngineEnvironmentSettings environmentSettings,
            IVariableCollection variables,
            RandomMacroConfig config)
        {
            variables[config.VariableName] = config.Low;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}' in deterministic mode.", nameof(RandomMacro), config.VariableName, config.Low);
        }

        public override RandomMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
