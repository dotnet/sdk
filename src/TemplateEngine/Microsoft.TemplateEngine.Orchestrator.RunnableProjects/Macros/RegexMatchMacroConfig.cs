// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMatchMacroConfig : BaseMacroConfig<RegexMatchMacro, RegexMatchMacroConfig>, IMacroConfigDependency
    {
        internal RegexMatchMacroConfig(RegexMatchMacro macro, string variableName, string? dataType, string sourceVariable, string pattern)
             : base(macro, variableName, dataType ?? "bool")
        {
            if (string.IsNullOrWhiteSpace(sourceVariable))
            {
                throw new ArgumentException($"'{nameof(sourceVariable)}' cannot be null or whitespace.", nameof(sourceVariable));
            }

            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentException($"'{nameof(pattern)}' cannot be null or empty.", nameof(pattern));
            }

            Source = sourceVariable;
            IsValidRegex(pattern);
            Pattern = pattern;
        }

        internal RegexMatchMacroConfig(RegexMatchMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
        : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Source = GetMandatoryParameterValue(generatedSymbolConfig, "source");
            Pattern = GetMandatoryParameterValue(generatedSymbolConfig, "pattern");
            IsValidRegex(Pattern!, generatedSymbolConfig);
        }

        internal string Source { get; }

        internal string Pattern { get; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            PopulateMacroConfigDependencies(Source, symbols);
        }
    }
}
