using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface ICreationPathModel : IConditionedConfigurationElement
    {
        string PathOriginal { get; }

        string PathResolved { get; set; }
    }
}
