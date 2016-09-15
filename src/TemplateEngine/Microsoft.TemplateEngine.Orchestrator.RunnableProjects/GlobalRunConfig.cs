using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class GlobalRunConfig : IGlobalRunConfig
    {
        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        public IVariableConfig VariableSetup { get; set; }

        public IReadOnlyList<IMacroConfig> Macros { get; set; }

        public IReadOnlyList<IReplacementTokens> Replacements { get; set; }
    }
}
