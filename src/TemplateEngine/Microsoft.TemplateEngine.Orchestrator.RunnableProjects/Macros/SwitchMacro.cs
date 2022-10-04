// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class SwitchMacro : IMacro, IDeferredMacro
    {
        internal const EvaluatorType DefaultEvaluator = EvaluatorType.CPP2;

        public Guid Id => new Guid("B57D64E0-9B4F-4ABE-9366-711170FD5294");

        public string Type => "switch";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, IMacroConfig rawConfig)
        {
            if (rawConfig is not SwitchMacroConfig config)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as SwitchMacroConfig");
            }

            ConditionStringEvaluator evaluator = EvaluatorSelector.SelectStringEvaluator(EvaluatorSelector.ParseEvaluatorName(config.Evaluator, DefaultEvaluator));
            string result = string.Empty;   // default if no condition assigns a value

            foreach (KeyValuePair<string?, string?> switchInfo in config.Switches)
            {
                string? condition = switchInfo.Key;
                string? value = switchInfo.Value;

                if (string.IsNullOrEmpty(condition))
                {
                    // no condition, this is the default.
                    result = value ?? string.Empty;
                    break;
                }
                else
                {
                    if (evaluator(environmentSettings.Host.Logger, condition!, variableCollection))
                    {
                        result = value ?? string.Empty;
                        break;
                    }
                }
            }
            variableCollection[config.VariableName] = result.ToString();
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not GeneratedSymbolDeferredMacroConfig deferredConfig)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a SwitchMacroConfig");
            }

            string? evaluator = null;
            if (deferredConfig.Parameters.TryGetValue("evaluator", out JToken evaluatorToken))
            {
                evaluator = evaluatorToken.ToString();
            }

            string? dataType = null;
            if (deferredConfig.Parameters.TryGetValue("datatype", out JToken dataTypeToken))
            {
                dataType = dataTypeToken.ToString();
            }

            List<KeyValuePair<string?, string?>> switchList = new();
            if (deferredConfig.Parameters.TryGetValue("cases", out JToken switchListToken))
            {
                JArray switchJArray = (JArray)switchListToken;
                foreach (JToken switchInfo in switchJArray)
                {
                    JObject map = (JObject)switchInfo;
                    string? condition = map.ToString("condition");
                    string? value = map.ToString("value");
                    switchList.Add(new KeyValuePair<string?, string?>(condition, value));
                }
            }

            IMacroConfig realConfig = new SwitchMacroConfig(deferredConfig.VariableName, evaluator, dataType, switchList);
            return realConfig;
        }
    }
}
