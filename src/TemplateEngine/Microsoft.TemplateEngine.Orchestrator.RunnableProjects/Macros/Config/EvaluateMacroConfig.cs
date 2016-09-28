using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class EvaluateMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public string Evaluator { get; set; }

        public EvaluateMacroConfig(string variableName, string action, string evaluator)
        {
            VariableName = variableName;
            Type = "evaluate";
            Action = action;
            Evaluator = evaluator;
        }
    }
}