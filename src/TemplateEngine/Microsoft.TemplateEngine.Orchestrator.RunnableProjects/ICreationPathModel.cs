using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface ICreationPathModel : IConditionedConfigurationElement
    {
        string PathOriginal { get; }

        string PathResolved { get; set; }
    }
}
