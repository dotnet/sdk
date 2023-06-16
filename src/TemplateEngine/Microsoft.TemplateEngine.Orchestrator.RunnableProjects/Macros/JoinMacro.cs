// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class JoinMacro : BaseGeneratedSymbolMacro<JoinMacroConfig>
    {
        public override Guid Id { get; } = new Guid("6A2C58E5-8743-484B-AF3C-536770D31CEE");

        public override string Type => "join";

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, JoinMacroConfig config)
        {
            List<string> values = new();
            foreach ((JoinMacroConfig.JoinType Type, string Value) symbol in config.Symbols)
            {
                switch (symbol.Type)
                {
                    case JoinMacroConfig.JoinType.Ref:
                        object? working = null;
                        if (!vars.TryGetValue(symbol.Value, out working) || working == null)
                        {
                            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was not found, using empty value instead'.", nameof(JoinMacro), symbol.Value);
                            values.Add(string.Empty);
                        }
                        else if (working is MultiValueParameter multiValue)
                        {
                            values.AddRange(multiValue.Values);
                        }
                        else
                        {
                            values.Add(working.ToString());
                        }
                        break;
                    case JoinMacroConfig.JoinType.Const:
                        values.Add(symbol.Value);
                        break;
                }
            }

            string result = string.Join(config.Separator, values.Where(v => !config.RemoveEmptyValues || !string.IsNullOrEmpty(v)));
            vars[config.VariableName] = result;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(JoinMacro), config.VariableName, result);
        }

        public override JoinMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
