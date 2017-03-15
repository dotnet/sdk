namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class ReplacementContext : IReplacementContext
    {
        public ReplacementContext(string before, string after)
        {
            OnlyIfBefore = before;
            OnlyIfAfter = after;
        }

        public string OnlyIfBefore { get; }

        public string OnlyIfAfter { get; }
    }
}