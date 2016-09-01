using System;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Operations;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public static class EvaluatorSelector
    {
        public static ConditionEvaluator Select(string name)
        {
            string evaluatorName = name ?? string.Empty;
            ConditionEvaluator evaluator;

            switch (evaluatorName)
            {
                case "C++":
                case "":
                    evaluator = CppStyleEvaluatorDefinition.CppStyleEvaluator;
                    break;
                default:
                    throw new Exception($"Unrecognized evaluator {evaluatorName}");
            }

            return evaluator;
        }
    }
}
