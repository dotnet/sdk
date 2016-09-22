using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GlobalRunConfig : IGlobalRunConfig
    {
        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        public IVariableConfig VariableSetup { get; set; }

        public IReadOnlyList<IMacroConfig> Macros { get; set; }

        public IReadOnlyList<IMacroConfig> ComputedMacros { get; set; }

        public IReadOnlyList<IReplacementTokens> Replacements { get; set; }

        public IReadOnlyList<ICustomOperationModel> CustomOperations { get; set; }
    }
}
