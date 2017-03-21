using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IExtendedTemplateParameter : ITemplateParameter
    {
        string FileRename { get; }
    }
}
