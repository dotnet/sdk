using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class CaseChangeMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("10919118-4E13-4FA9-825C-3B4DA855578E");

        public string Type => "casing";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            string value = null;
            CaseChangeMacroConfig config = rawConfig as CaseChangeMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as CaseChangeMacroConfig");
            }

            if (!vars.TryGetValue(config.SourceVariable, out object working))
            {
                if (RuntimeValueUtil.TryGetRuntimeValue(parameters, environmentSettings, config.SourceVariable, out object resolvedValue, true))
                {
                    value = resolvedValue.ToString();
                }
                else
                {
                    value = string.Empty;
                }
            }
            else
            {
                value = working?.ToString() ?? "";
            }

            value = config.ToLower ? value.ToLowerInvariant() : value.ToUpperInvariant();

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

            vars[config.VariableName] = value;
            setter(p, value);
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("source", out JToken sourceVarToken))
            {
                throw new ArgumentNullException("source");
            }
            string sourceVariable = sourceVarToken.ToString();

            bool lowerCase = true;
            List<KeyValuePair<string, string>> replacementSteps = new List<KeyValuePair<string, string>>();
            if (deferredConfig.Parameters.TryGetValue("toLower", out JToken stepListToken))
            {
                lowerCase = stepListToken.ToBool(defaultValue: true);
            }

            IMacroConfig realConfig = new CaseChangeMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariable, lowerCase);
            return realConfig;
        }
    }
}
