using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostActionOperation : IPostActionOperation
    {
        public string CommandText { get; set; }

        string IPostActionOperation.CommandText => CommandText;

        public static PostActionOperation FromModel(IPostActionOperationModel model)
        {
            PostActionOperation operation = new PostActionOperation();
            operation.CommandText = model.CommandText;
            return operation;
        }
    }
}
