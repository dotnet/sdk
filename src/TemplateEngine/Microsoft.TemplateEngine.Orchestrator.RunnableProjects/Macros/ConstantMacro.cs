// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ConstantMacro : BaseGeneratedSymbolMacro<ConstantMacroConfig>
    {
        public override Guid Id { get; } = new Guid("370996FE-2943-4AED-B2F6-EC03F0B75B4A");

        public override string Type => "constant";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, ConstantMacroConfig config)
        {
            vars[config.VariableName] = config.Value;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(ConstantMacro), config.VariableName, config.Value);
        }

        public override ConstantMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
