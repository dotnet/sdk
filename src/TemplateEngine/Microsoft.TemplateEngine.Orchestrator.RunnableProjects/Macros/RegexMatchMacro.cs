// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMatchMacro : BaseGeneratedSymbolMacro<RegexMatchMacroConfig>
    {
        public override Guid Id { get; } = new Guid("AA5957B0-07B1-4B68-847F-83713973E86F");

        public override string Type => "regexMatch";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, RegexMatchMacroConfig config)
        {
            if (!variableCollection.TryGetValue(config.Source, out object working))
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Source variable '{sourceVar}' was not found, skipping processing for macro '{var}'.", nameof(RegexMatchMacro), config.Source, config.VariableName);
                return;
            }
            if (working == null)
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: The value of source variable '{sourceVar}' is null, skipping processing for macro '{var}'.", nameof(RegexMatchMacro), config.Source, config.VariableName);
                return;
            }
            string value = working.ToString();
            bool result = Regex.IsMatch(value, config.Pattern);
            variableCollection[config.VariableName] = result;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Assigned variable '{var}' to '{value}'.", nameof(RegexMatchMacro), config.VariableName, result);
        }

        public override RegexMatchMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
