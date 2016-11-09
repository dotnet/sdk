using System;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Expressions.MSBuild;
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
                case "C++2":
                    evaluator = Cpp2StyleEvaluatorDefinition.Evaluate;
                    break;
                case "C++":
                case "":
                    evaluator = CppStyleEvaluatorDefinition.Evaluate;
                    break;
                case "MSBUILD":
                    evaluator = MSBuildStyleEvaluatorDefinition.Evaluate;
                    break;
                default:
                    throw new Exception($"Unrecognized evaluator {evaluatorName}");
            }

            return evaluator;
        }
    }
}
