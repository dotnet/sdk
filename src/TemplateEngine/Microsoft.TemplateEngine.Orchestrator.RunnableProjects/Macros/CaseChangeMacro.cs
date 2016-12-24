using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public class CaseChangeMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("10919118-4E13-4FA9-825C-3B4DA855578E");

        public string Type => "casing";

        public void EvaluateConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            string value = null;
            CaseChangeMacroConfig config = rawConfig as CaseChangeMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as CaseChangeMacroConfig");
            }

            if (!vars.TryGetValue(config.SourceVariable, out object working))
            {
                if (!parameters.TryGetParameterDefinition(config.SourceVariable, out ITemplateParameter param) || !parameters.ResolvedValues.TryGetValue(param, out object resolvedValue))
                {
                    value = string.Empty;
                }
                else
                {
                    value = (string)resolvedValue;
                }
            }
            else
            {
                value = working?.ToString() ?? "";
            }

            value = config.ToLower ? value.ToLowerInvariant() : value.ToUpperInvariant();

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };
            setter(p, value);
        }

        public void EvaluateDeferredConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
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

            IMacroConfig realConfig = new CaseChangeMacroConfig(deferredConfig.VariableName, sourceVariable, lowerCase);
            EvaluateConfig(vars, realConfig, parameters, setter);
        }
    }
}
