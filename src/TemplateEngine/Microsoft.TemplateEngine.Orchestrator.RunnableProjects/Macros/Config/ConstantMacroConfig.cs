using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class ConstantMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public ConstantMacroConfig(string variableName, string action)
        {
            VariableName = variableName;
            Type = "constant";
            Action = action;
        }
    }
}
