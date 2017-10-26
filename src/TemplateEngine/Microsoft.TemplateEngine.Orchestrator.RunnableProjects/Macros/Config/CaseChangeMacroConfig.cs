using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class CaseChangeMacroConfig : IMacroConfig
    {
        public string DataType { get; }

        public string Type => "casing";

        public string VariableName { get; }

        public string SourceVariable { get; }

        public bool ToLower { get; }

        public CaseChangeMacroConfig(string variableName, string dataType, string sourceVariable, bool toLower)
        {
            DataType = dataType;
            VariableName = variableName;
            SourceVariable = sourceVariable;
            ToLower = toLower;
        }
    }
}
