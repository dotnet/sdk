// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacroConfig : BaseMacroConfig<RegexMacro, RegexMacroConfig>
    {
        internal RegexMacroConfig(RegexMacro macro, string variableName, string? dataType, string sourceVariable, IReadOnlyList<(string, string)> steps)
             : base(macro, variableName, dataType)
        {
            if (string.IsNullOrWhiteSpace(sourceVariable))
            {
                throw new ArgumentException($"'{nameof(sourceVariable)}' cannot be null or whitespace.", nameof(sourceVariable));
            }

            Source = sourceVariable;
            Steps = steps;
        }

        internal RegexMacroConfig(RegexMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
            : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Source = GetMandatoryParameterValue(generatedSymbolConfig, nameof(Source));

            List<(string Type, string Value)> steps = new();
            JArray jArray = GetMandatoryParameterArray(generatedSymbolConfig, nameof(Steps));

            foreach (JToken entry in jArray)
            {
                if (entry is not JObject jobj)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': array '{nameof(Steps)}' should contain JSON objects.", generatedSymbolConfig.VariableName);
                }
                string? regex = jobj.ToString("regex");
                string? replacement = jobj.ToString("replacement");

                if (string.IsNullOrEmpty(regex))
                {
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': array '{nameof(Steps)}' should contain JSON objects with property 'regex'", generatedSymbolConfig.VariableName);
                }
                IsValidRegex(regex!, generatedSymbolConfig);

                if (replacement == null)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': array '{nameof(Steps)}' should contain JSON objects with property 'replacement'", generatedSymbolConfig.VariableName);
                }

                steps.Add((regex!, replacement));
            }

            Steps = steps;
        }

        internal string Source { get; private set; }

        internal IReadOnlyList<(string Regex, string Replacement)> Steps { get; private set; }
    }
}
