using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class ConstantMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Value { get; private set; }

        public ConstantMacroConfig(string variableName, string value)
        {
            VariableName = variableName;
            Type = "constant";
            Value = value;
        }
    }
}
