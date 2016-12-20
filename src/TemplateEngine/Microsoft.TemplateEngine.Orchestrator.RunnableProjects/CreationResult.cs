using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CreationResult : ICreationResult
    {
        public IReadOnlyList<IPostAction> PostActions { get; set; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; set; }
    }
}
