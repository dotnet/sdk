// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class CaseChangeMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("10919118-4E13-4FA9-825C-3B4DA855578E");

        public string Type => "casing";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig)
        {
            string value = string.Empty;

            if (rawConfig is not CaseChangeMacroConfig config)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as CaseChangeMacroConfig");
            }

            if (vars.TryGetValue(config.SourceVariable, out object working))
            {
                value = working?.ToString() ?? string.Empty;
            }

            value = config.ToLower ? value.ToLowerInvariant() : value.ToUpperInvariant();

            vars[config.VariableName] = value;
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not GeneratedSymbolDeferredMacroConfig deferredConfig)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("source", out JToken sourceVarToken))
            {
                throw new ArgumentNullException("source");
            }
            string sourceVariable = sourceVarToken.ToString();

            bool lowerCase = true;
            if (deferredConfig.Parameters.TryGetValue("toLower", out JToken stepListToken))
            {
                lowerCase = stepListToken.ToBool(defaultValue: true);
            }

            IMacroConfig realConfig = new CaseChangeMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariable, lowerCase);
            return realConfig;
        }
    }
}
