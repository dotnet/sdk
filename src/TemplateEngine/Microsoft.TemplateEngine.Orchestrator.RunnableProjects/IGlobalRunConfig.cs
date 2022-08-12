// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IGlobalRunConfig
    {
        IReadOnlyList<IOperationProvider> Operations { get; }

        IVariableConfig VariableSetup { get; }

        IReadOnlyList<IMacroConfig> Macros { get; }

        IReadOnlyList<IMacroConfig> ComputedMacros { get; }

        IReadOnlyList<IReplacementTokens> Replacements { get; }

        IReadOnlyList<CustomOperationModel> CustomOperations { get; }
    }
}
