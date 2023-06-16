// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class NowMacro : BaseNondeterministicGenSymMacro<NowMacroConfig>
    {
        public override Guid Id { get; } = new Guid("F2B423D7-3C23-4489-816A-41D8D2A98596");

        public override string Type => "now";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, NowMacroConfig config)
        {
            DateTime time = config.Utc ? DateTime.UtcNow : DateTime.Now;
            string value = time.ToString(config.Format);
            variableCollection[config.VariableName] = value;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(NowMacro), config.VariableName, value);
        }

        public override void EvaluateDeterministically(
            IEngineEnvironmentSettings environmentSettings,
            IVariableCollection variables,
            NowMacroConfig config)
        {
            DateTime time = new DateTime(1900, 01, 01);
            string value = time.ToString(config.Format);
            variables[config.VariableName] = value;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}' in deterministic mode.", nameof(NowMacro), config.VariableName, value);
        }

        public override NowMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
