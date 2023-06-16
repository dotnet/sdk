// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacroConfig : BaseMacroConfig<RegexMacro, RegexMacroConfig>, IMacroConfigDependency
    {
        private const string StepsPropertyName = "steps";
        private const string StepsRegexPropertyName = "regex";
        private const string StepsReplacementPropertyName = "replacement";

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
            Source = GetMandatoryParameterValue(generatedSymbolConfig, "source");

            List<(string Type, string Value)> steps = new();
            JArray jArray = GetMandatoryParameterArray(generatedSymbolConfig, StepsPropertyName);

            foreach (JToken entry in jArray)
            {
                if (entry is not JObject jobj)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_ArrayShouldContainObjects, generatedSymbolConfig.VariableName, StepsPropertyName), generatedSymbolConfig.VariableName);
                }
                string? regex = jobj.ToString(StepsRegexPropertyName);
                string? replacement = jobj.ToString(StepsReplacementPropertyName);

                if (string.IsNullOrEmpty(regex))
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_MissingValueProperty, generatedSymbolConfig.VariableName, StepsPropertyName, StepsRegexPropertyName), generatedSymbolConfig.VariableName);
                }
                IsValidRegex(regex!, generatedSymbolConfig);

                if (replacement == null)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_MissingValueProperty, generatedSymbolConfig.VariableName, StepsPropertyName, StepsReplacementPropertyName), generatedSymbolConfig.VariableName);
                }

                steps.Add((regex!, replacement));
            }

            Steps = steps;
        }

        internal string Source { get; private set; }

        internal IReadOnlyList<(string Regex, string Replacement)> Steps { get; private set; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            PopulateMacroConfigDependencies(Source, symbols);
        }
    }
}
