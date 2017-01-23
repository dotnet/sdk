using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IConditionedConfigurationElement
    {
        string Condition { get; }

        bool ConditionResult { get; }

        void EvaluateCondition(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables);
    }
}
