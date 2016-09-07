using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IPostActionModel
    {
        int Order { get; }

        IReadOnlyList<IPostActionOperationModel> Operations { get; }

        string ManualInstructions { get; }
    }
}
