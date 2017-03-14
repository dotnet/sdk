using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class EvaluateMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Value { get; private set; }

        public string Evaluator { get; set; }

        public EvaluateMacroConfig(string variableName, string value, string evaluator)
        {
            VariableName = variableName;
            Type = "evaluate";
            Value = value;
            Evaluator = evaluator;
        }
    }
}
