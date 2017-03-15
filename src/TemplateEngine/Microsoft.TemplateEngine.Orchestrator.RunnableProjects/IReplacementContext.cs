namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IReplacementContext
    {
        string OnlyIfBefore { get; }

        string OnlyIfAfter { get; }
    }
}