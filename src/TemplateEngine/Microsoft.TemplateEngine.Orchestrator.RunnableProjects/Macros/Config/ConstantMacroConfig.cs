using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class ConstantMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        internal string DataType { get; }

        public string Type { get; private set; }

        internal string Value { get; private set; }

        internal ConstantMacroConfig(string dataType, string variableName, string value)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "constant";
            Value = value;
        }
    }
}
