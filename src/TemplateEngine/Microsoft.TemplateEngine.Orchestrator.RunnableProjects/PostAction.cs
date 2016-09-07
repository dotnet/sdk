using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostAction : IPostAction
    {
        public string Name { get; set; }

        public int Order { get; set; }

        public List<IPostActionOperation> Operations { get; set; }

        public string ManualInstructions { get; set; }

        string IPostAction.Name => Name;

        int IPostAction.Order => Order;

        IReadOnlyList<IPostActionOperation> IPostAction.Operations => Operations;

        string IPostAction.ManualInstructions => ManualInstructions;

        public static PostAction FromModel(string name, IPostActionModel model)
        {
            PostAction postAction = new PostAction();
            postAction.Name = name;
            postAction.Order = model.Order;

            List<IPostActionOperation> operations = new List<IPostActionOperation>();
            foreach (IPostActionOperationModel operationModel in model.Operations)
            {
                operations.Add(PostActionOperation.FromModel(operationModel));
            }
            postAction.Operations = operations;

            postAction.ManualInstructions = model.ManualInstructions;

            return postAction;
        }
    }
}
