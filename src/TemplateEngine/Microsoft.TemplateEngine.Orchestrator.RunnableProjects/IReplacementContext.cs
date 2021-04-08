namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IReplacementContext
    {
        string OnlyIfBefore { get; }

        string OnlyIfAfter { get; }
    }
}