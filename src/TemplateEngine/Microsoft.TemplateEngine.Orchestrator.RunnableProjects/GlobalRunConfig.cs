// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class GlobalRunConfig
    {
        public IReadOnlyList<IOperationProvider> Operations { get; init; } = [];

        public IVariableConfig VariableSetup { get; init; } = VariableConfig.Default;

        public IReadOnlyList<IReplacementTokens> Replacements { get; init; } = [];

        public IReadOnlyList<CustomOperationModel> CustomOperations { get; init; } = [];

        public IReadOnlyList<string> SymbolNames { get; init; } = [];

        public IReadOnlyList<IMacroConfig> Macros { get; init; } = [];
    }
}
