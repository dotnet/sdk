using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public delegate void ParameterSetter(ITemplateParameter parameter, string value);
}
