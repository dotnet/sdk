// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Fakes
{
    internal class FakeMacroConfig : BaseMacroConfig<FakeMacro, FakeMacroConfig>, IMacroConfigDependency
    {
        public FakeMacroConfig(FakeMacro macro, string variableName, string sourceName, string nameToGreet = "", string? dataType = null) : base(macro, variableName, dataType)
        {
            Source = sourceName;
            NameToGreet = nameToGreet;
        }

        public FakeMacroConfig(FakeMacro macro, IGeneratedSymbolConfig generatedSymbolConfig) : base(macro, generatedSymbolConfig.VariableName)
        {
            Source = GetMandatoryParameterValue(generatedSymbolConfig, "source");
            Fallback = GetOptionalParameterValue(generatedSymbolConfig, "fallback");
            NameToGreet = GetMandatoryParameterValue(generatedSymbolConfig, "name");
        }

        public string Source { get; }

        public string? Fallback { get; }

        public string NameToGreet { get; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            PopulateMacroConfigDependencies(Source, symbols);
        }
    }
}
