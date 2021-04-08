using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("8A4D4937-E23F-426D-8398-3BDBD1873ADB");

        public string Type => "regex";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            string value = null;
            RegexMacroConfig config = rawConfig as RegexMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as RegexMacroConfig");
            }

            if (!vars.TryGetValue(config.SourceVariable, out object working))
            {
                if (parameters.TryGetRuntimeValue(environmentSettings, config.SourceVariable, out object resolvedValue, true))
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

            if (config.Steps != null)
            {
                foreach (KeyValuePair<string, string> stepInfo in config.Steps)
                {
                    value = Regex.Replace(value, stepInfo.Key, stepInfo.Value);
                }
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

            List<KeyValuePair<string, string>> replacementSteps = new List<KeyValuePair<string, string>>();
            if (deferredConfig.Parameters.TryGetValue("steps", out JToken stepListToken))
            {
                JArray stepList = (JArray)stepListToken;
                foreach (JToken step in stepList)
                {
                    JObject map = (JObject)step;
                    string regex = map.ToString("regex");
                    string replaceWith = map.ToString("replacement");
                    replacementSteps.Add(new KeyValuePair<string, string>(regex, replaceWith));
                }
            }

            IMacroConfig realConfig = new RegexMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariable, replacementSteps);
            return realConfig;
        }
    }
}
