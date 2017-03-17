using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public interface IMacro : IIdentifiedComponent
    {
        string Type { get; }

        void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config, IParameterSet parameters, ParameterSetter setter);
    }
}
