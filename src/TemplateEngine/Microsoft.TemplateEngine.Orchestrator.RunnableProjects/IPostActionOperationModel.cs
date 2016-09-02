using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IPostActionOperationModel
    {
        // Placeholder until we better define what sort of actions are allowed, and what info will be needed.
        string CommandText { get; }
    }
}
