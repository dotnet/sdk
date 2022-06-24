// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class JoinMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("6A2C58E5-8743-484B-AF3C-536770D31CEE");

        public string Type => "join";

        public void EvaluateConfig(
            IEngineEnvironmentSettings environmentSettings,
            IVariableCollection vars,
            IMacroConfig rawConfig,
            IParameterSet parameters,
            ParameterSetter setter)
        {
            JoinMacroConfig config = rawConfig as JoinMacroConfig;
            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as ConcatenationMacroConfig");
            }

            List<string> values = new List<string>();
            foreach (KeyValuePair<string, string> symbol in config.Symbols)
            {
                switch (symbol.Key)
                {
                    case "ref":
                        if (!vars.TryGetValue(symbol.Value, out object working))
                        {
                            values.Add(string.Empty);
                        }
                        else if (working != null && working is MultiValueParameter multiValue)
                        {
                            values.AddRange(multiValue.Values);
                        }
                        else
                        {
                            values.Add(working?.ToString() ?? string.Empty);
                        }

                        break;
                    case "const":
                        values.Add(symbol.Value);
                        break;
                    default:
                        values.Add(symbol.Value);
                        break;
                }
            }

            string result = string.Join(config.Separator, values.Where(v => !config.RemoveEmptyValues || !string.IsNullOrEmpty(v)));
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
            setter(p, result);
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (!(rawConfig is GeneratedSymbolDeferredMacroConfig deferredConfig))
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            string separator = string.Empty;
            if (deferredConfig.Parameters.TryGetValue("separator", out JToken separatorToken))
            {
                separator = separatorToken?.ToString();
            }

            bool removeEmptyValues =
                deferredConfig.Parameters.TryGetValue("removeEmptyValues", out JToken removeEmptyValuesToken) &&
                removeEmptyValuesToken != null &&
                removeEmptyValuesToken.ToBool();

            List<KeyValuePair<string, string>> symbolsList = new List<KeyValuePair<string, string>>();
            if (deferredConfig.Parameters.TryGetValue("symbols", out JToken symbolsToken))
            {
                JArray switchJArray = (JArray)symbolsToken;
                foreach (JToken switchInfo in switchJArray)
                {
                    JObject map = (JObject)switchInfo;
                    string condition = map.ToString("type");
                    string value = map.ToString("value");
                    symbolsList.Add(new KeyValuePair<string, string>(condition, value));
                }
            }

            return new JoinMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, symbolsList, separator, removeEmptyValues);
        }
    }
}
