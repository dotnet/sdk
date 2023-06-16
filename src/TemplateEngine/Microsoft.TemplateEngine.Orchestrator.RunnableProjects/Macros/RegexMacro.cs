// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacro : BaseGeneratedSymbolMacro<RegexMacroConfig>
    {
        public override Guid Id { get; } = new Guid("8A4D4937-E23F-426D-8398-3BDBD1873ADB");

        public override string Type => "regex";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, RegexMacroConfig config)
        {
            if (!variableCollection.TryGetValue(config.Source, out object working))
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Source variable '{sourceVar}' was not found, skipping processing for macro '{var}'.", nameof(RegexMacro), config.Source, config.VariableName);
                return;
            }
            if (working == null)
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: The value of source variable '{sourceVar}' is null, skipping processing for macro '{var}'.", nameof(RegexMacro), config.Source, config.VariableName);
                return;
            }
            string value = working.ToString();

            foreach ((string Regex, string Replacement) stepInfo in config.Steps)
            {
                value = Regex.Replace(value, stepInfo.Regex, stepInfo.Replacement);
            }
            variableCollection[config.VariableName] = value;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Assigned variable '{var}' to '{value}'.", nameof(RegexMacro), config.VariableName, value);
        }

        public override RegexMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
