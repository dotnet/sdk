
namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface ICreationPathModel
    {
        string PathOriginal { get; }

        string PathResolved { get; }

        string Condition { get; }
    }
}
