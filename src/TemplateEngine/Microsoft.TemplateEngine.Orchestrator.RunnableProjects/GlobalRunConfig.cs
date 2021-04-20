// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class GlobalRunConfig : IGlobalRunConfig
    {
        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        public IVariableConfig VariableSetup { get; set; }

        public IReadOnlyList<IMacroConfig> Macros { get; set; }

        public IReadOnlyList<IMacroConfig> ComputedMacros { get; set; }

        public IReadOnlyList<IReplacementTokens> Replacements { get; set; }

        public IReadOnlyList<ICustomOperationModel> CustomOperations { get; set; }
    }
}
