// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("8A4D4937-E23F-426D-8398-3BDBD1873ADB");

        public string Type => "regex";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig)
        {
            if (rawConfig is not RegexMacroConfig config)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as RegexMacroConfig");
            }

            string value = !vars.TryGetValue(config.SourceVariable, out object working) ? string.Empty : working?.ToString() ?? string.Empty;
            if (config.Steps != null)
            {
                foreach (KeyValuePair<string?, string?> stepInfo in config.Steps)
                {
                    value = Regex.Replace(value, stepInfo.Key, stepInfo.Value);
                }
            }
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

            List<KeyValuePair<string?, string?>> replacementSteps = new();
            if (deferredConfig.Parameters.TryGetValue("steps", out JToken stepListToken))
            {
                JArray stepList = (JArray)stepListToken;
                foreach (JToken step in stepList)
                {
                    JObject map = (JObject)step;
                    string? regex = map.ToString("regex");
                    string? replaceWith = map.ToString("replacement");
                    replacementSteps.Add(new KeyValuePair<string?, string?>(regex, replaceWith));
                }
            }

            IMacroConfig realConfig = new RegexMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariable, replacementSteps);
            return realConfig;
        }
    }
}
