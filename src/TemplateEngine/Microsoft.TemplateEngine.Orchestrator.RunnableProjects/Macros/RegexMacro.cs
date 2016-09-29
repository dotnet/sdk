using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("8A4D4937-E23F-426D-8398-3BDBD1873ADB");

        public string Type => "regex";

        public void EvaluateConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            string value = null;
            RegexMacroConfig config = rawConfig as RegexMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as RegexMacroConfig");
            }

            switch (config.Action)
            {
                case "replace":
                    object working;
                    if (!vars.TryGetValue(config.SourceVariable, out working))
                    {
                        ITemplateParameter param;
                        object resolvedValue;
                        if (!parameters.TryGetParameterDefinition(config.SourceVariable, out param) || !parameters.ResolvedValues.TryGetValue(param, out resolvedValue))
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

                    if (config.Steps != null)
                    {
                        foreach (KeyValuePair<string, string> stepInfo in config.Steps)
                        {
                            value = Regex.Replace(value, stepInfo.Key, stepInfo.Value);
                        }
                    }

                    break;
            }

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

            JToken actionToken;
            if (!deferredConfig.Parameters.TryGetValue("action", out actionToken))
            {
                throw new ArgumentNullException("action");
            }
            string action = actionToken.ToString();

            JToken sourceVarToken;
            if (!deferredConfig.Parameters.TryGetValue("source", out sourceVarToken))
            {
                throw new ArgumentNullException("source");
            }
            string sourceVariable = sourceVarToken.ToString();

            JToken stepListToken;
            List<KeyValuePair<string, string>> replacementSteps = new List<KeyValuePair<string, string>>();
            if (deferredConfig.Parameters.TryGetValue("steps", out stepListToken))
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

            IMacroConfig realConfig = new RegexMacroConfig(deferredConfig.VariableName, action, sourceVariable, replacementSteps);
            EvaluateConfig(vars, realConfig, parameters, setter);
        }
    }
}