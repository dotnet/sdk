using System;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

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

        public static EvaluateMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            string evaluator = config.ToString("evaluator");

            return new EvaluateMacroConfig(variableName, action, evaluator);
        }
    }
}