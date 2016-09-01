using System;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class EvaluateMacro : IMacro
    {
        public string Type => "evaluate";

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            string evaluatorName = def.ToString("evaluator");
            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorName);

            byte[] data = Encoding.UTF8.GetBytes(def.ToString("action"));
            int len = data.Length;
            int pos = 0;
            IProcessorState state = new GlobalRunSpec.ProcessorState(vars, data, Encoding.UTF8);
            bool res = evaluator(state, ref len, ref pos);

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            setter(p, res.ToString());
        }
    }
}