using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IGlobalRunConfig
    {
        IReadOnlyList<IOperationProvider> Operations { get; }

        IVariableConfig VariableSetup { get; }

        IReadOnlyList<IMacroConfig> Macros { get; set; }

        IReadOnlyList<IReplacementTokens> Replacements { get; }
    }
}
