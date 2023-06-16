// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class CaseChangeMacroConfig : BaseMacroConfig<CaseChangeMacro, CaseChangeMacroConfig>, IMacroConfigDependency
    {
        internal CaseChangeMacroConfig(CaseChangeMacro macro, string variableName, string? dataType, string sourceVariable, bool toLower)
            : base(macro, variableName, dataType)
        {
            if (string.IsNullOrWhiteSpace(sourceVariable))
            {
                throw new System.ArgumentException($"'{nameof(sourceVariable)}' cannot be null or whitespace.", nameof(sourceVariable));
            }

            Source = sourceVariable;
            ToLower = toLower;
        }

        internal CaseChangeMacroConfig(CaseChangeMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
            : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Source = GetMandatoryParameterValue(generatedSymbolConfig, "source");
            ToLower = GetOptionalParameterValue(generatedSymbolConfig, "toLower", ConvertJTokenToBool, defaultValue: true);
        }

        public string Source { get; }

        internal bool ToLower { get; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            PopulateMacroConfigDependencies(Source, symbols);
        }
    }
}
