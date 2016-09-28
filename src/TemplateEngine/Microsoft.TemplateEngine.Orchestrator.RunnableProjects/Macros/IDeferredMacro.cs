using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    // interface for macros created via generated symbols.
    public interface IDeferredMacro : IIdentifiedComponent
    {
        string Type { get; }

        void EvaluateDeferredConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter);
    }
}
