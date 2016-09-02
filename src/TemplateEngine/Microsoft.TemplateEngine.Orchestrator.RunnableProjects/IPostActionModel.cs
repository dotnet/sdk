using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IPostActionModel
    {
        int Order { get; }

        IReadOnlyList<IPostActionOperationModel> Operations { get; }

        IReadOnlyList<IPostActionOperationModel> AlternateOperations { get; }
    }
}
