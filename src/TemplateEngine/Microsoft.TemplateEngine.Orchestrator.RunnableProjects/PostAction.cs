using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostAction : IPostAction
    {
        public string Name { get; set; }

        public int Order { get; set; }

        public List<IPostActionOperation> Operations { get; set; }

        public List<IPostActionOperation> AlternateOperations { get; set; }

        string IPostAction.Name => Name;

        int IPostAction.Order => Order;

        IReadOnlyList<IPostActionOperation> IPostAction.Operations => Operations;

        IReadOnlyList<IPostActionOperation> IPostAction.AlternateOperations => AlternateOperations;

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

            List<IPostActionOperation> altOperations = new List<IPostActionOperation>();
            foreach (IPostActionOperationModel altOperationModel in model.AlternateOperations)
            {
                altOperations.Add(PostActionOperation.FromModel(altOperationModel));
            }
            postAction.AlternateOperations = altOperations;

            return postAction;
        }
    }
}
