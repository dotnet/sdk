using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacro : IMacro
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

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            string action = def.ToString("action");
            string value = null;

            switch (action)
            {
                case "replace":
                    string sourceVar = def.ToString("source");
                    JArray steps = def.Get<JArray>("steps");
                    object working;
                    if (!vars.TryGetValue(sourceVar, out working))
                    {
                        ITemplateParameter param;
                        object resolvedValue;
                        if (!parameters.TryGetParameterDefinition(sourceVar, out param) || !parameters.ResolvedValues.TryGetValue(param, out resolvedValue))
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

                    if (steps != null)
                    {
                        foreach (JToken child in steps)
                        {
                            JObject map = (JObject)child;
                            string regex = map.ToString("regex");
                            string replaceWith = map.ToString("replacement");

                            value = Regex.Replace(value, regex, replaceWith);
                        }
                    }
                    break;
            }

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            setter(p, value);
        }
    }
}