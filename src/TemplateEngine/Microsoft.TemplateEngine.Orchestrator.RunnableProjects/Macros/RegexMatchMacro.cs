// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMatchMacro : IDeferredMacro
    {
        public Guid Id => new Guid("AA5957B0-07B1-4B68-847F-83713973E86F");

        public string Type => "regexMatch";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            string value;

            if (!(rawConfig is RegexMatchMacroConfig config))
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as RegexMatchMacroConfig");
            }

            if (!vars.TryGetValue(config.SourceVariable, out object working))
            {
                value = parameters.TryGetRuntimeValue(environmentSettings, config.SourceVariable, out object resolvedValue, true)
                    ? resolvedValue.ToString()
                    : string.Empty;
            }
            else
            {
                value = working?.ToString() ?? string.Empty;
            }

            bool result = false;

            try
            {
                result = Regex.IsMatch(value, config.Pattern);
            }
            catch (ArgumentException ex)
            {
                environmentSettings.Host.LogDiagnosticMessage(string.Format(LocalizableStrings.Authoring_InvalidRegex, config.Pattern), "Authoring", ex.ToString());
            }

            Parameter p;

            if (parameters.TryGetParameterDefinition(config.VariableName, out ITemplateParameter existingParam))
            {
                // If there is an existing parameter with this name, it must be reused so it can be referenced by name
                // for other processing, for example: if the parameter had value forms defined for creating variants.
                // When the param already exists, use its definition, but set IsVariable = true for consistency.
                p = (Parameter)existingParam;
                p.IsVariable = true;

                if (string.IsNullOrEmpty(p.DataType))
                {
                    p.DataType = config.DataType;
                }
            }
            else
            {
                p = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName,
                    DataType = config.DataType
                };
            }

            vars[config.VariableName] = result;
            setter(p, result.ToString());
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (!(rawConfig is GeneratedSymbolDeferredMacroConfig deferredConfig))
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("source", out JToken sourceVarToken))
            {
                throw new ArgumentNullException("source");
            }

            string sourceVariable = sourceVarToken.ToString();

            if (!deferredConfig.Parameters.TryGetValue("pattern", out JToken patternToken))
            {
                throw new ArgumentNullException("pattern");
            }

            string pattern = patternToken.ToString();

            //Warn the user if they explicitly specify something other than "bool" for DataType for this macro
            if (deferredConfig.DataType != null
                && !string.Equals(deferredConfig.DataType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                environmentSettings.Host.LogDiagnosticMessage(LocalizableStrings.Authoring_NonBoolDataTypeForRegexMatch, "Authoring");
            }

            IMacroConfig realConfig = new RegexMatchMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariable, pattern);
            return realConfig;
        }
    }
}
