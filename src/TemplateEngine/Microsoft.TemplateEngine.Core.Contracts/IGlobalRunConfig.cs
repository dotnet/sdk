// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IGlobalRunConfig
    {
        IReadOnlyList<IOperationProvider> Operations { get; }

        IVariableConfig VariableSetup { get; }

        IReadOnlyList<IMacroConfig> Macros { get; }

        IReadOnlyList<IMacroConfig> ComputedMacros { get; }

        IReadOnlyList<IReplacementTokens> Replacements { get; }

        IReadOnlyList<ICustomOperationModel> CustomOperations { get; }
    }
}
