using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public interface IMacro : IIdentifiedComponent
    {
        string Type { get; }

        void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter);

        void EvaluateConfig(IVariableCollection vars, IMacroConfig config, IParameterSet parameters, ParameterSetter setter);

        void EvaluateDeferredConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter);
    }
}