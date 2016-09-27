
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface ICreationPathModel
    {
        string PathOriginal { get; }

        string PathResolved { get; set; }

        string Condition { get; }

        bool ConditionResult { get; }

        void EvaluateCondition(IVariableCollection variables);
    }
}
