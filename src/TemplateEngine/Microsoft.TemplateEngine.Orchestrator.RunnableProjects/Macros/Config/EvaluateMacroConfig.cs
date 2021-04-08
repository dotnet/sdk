using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class EvaluateMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string Value { get; private set; }

        internal string Evaluator { get; set; }

        internal EvaluateMacroConfig(string variableName, string dataType, string value, string evaluator)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "evaluate";
            Value = value;
            Evaluator = evaluator;
        }
    }
}
