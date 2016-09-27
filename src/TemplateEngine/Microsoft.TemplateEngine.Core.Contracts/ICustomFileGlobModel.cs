using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ICustomFileGlobModel
    {
        string Glob { get; }

        IReadOnlyList<ICustomOperationModel> Operations { get; }

        IVariableConfig VariableFormat { get; }

        string FlagPrefix { get; }

        string Condition { get; }

        bool ConditionResult { get; }

        void EvaluateCondition(IVariableCollection variables);
    }
}
