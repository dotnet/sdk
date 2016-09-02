namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostActionOperationModel : IPostActionOperationModel
    {
        public PostActionOperationModel(string commandText)
        {
            CommandText = commandText;
        }

        public string CommandText { get; private set; }
    }
}
