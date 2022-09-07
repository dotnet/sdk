// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GeneratePortNumberMacro : BaseGeneratedSymbolMacro<GeneratePortNumberConfig>
    {
        public override Guid Id { get; } = new Guid("D49B3690-B1E5-410F-A260-E1D7E873D8B2");

        public override string Type => "port";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, GeneratePortNumberConfig config)
        {
            vars[config.VariableName] = config.Port;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(GeneratePortNumberMacro), config.VariableName, config.Port);
        }

        protected override GeneratePortNumberConfig CreateConfig(IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
