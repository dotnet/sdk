using System;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class EvaluateMacro : IMacro
    {
        public Guid Id => new Guid("BB625F71-6404-4550-98AF-B2E546F46C5F");

        public string Type => "evaluate";

        public void EvaluateConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            EvaluateMacroConfig config = rawConfig as EvaluateMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as EvaluateMacroConfig");
            }

            ConditionEvaluator evaluator = EvaluatorSelector.Select(config.Evaluator);

            byte[] data = Encoding.UTF8.GetBytes(config.Action);
            int len = data.Length;
            int pos = 0;
            IProcessorState state = new GlobalRunSpec.ProcessorState(vars, data, Encoding.UTF8);
            bool faulted;
            bool result = evaluator(state, ref len, ref pos, out faulted);

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };

            setter(p, result.ToString());
        }
        public void EvaluateDeferredConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            string action;
            if (!deferredConfig.Parameters.TryGetValue("action", out action))
            {
                throw new ArgumentNullException("action");
            }

            string evaluator;
            if (!deferredConfig.Parameters.TryGetValue("evaluator", out evaluator))
            {
                throw new ArgumentNullException("evaluator");
            }

            IMacroConfig realConfig = new EvaluateMacroConfig(deferredConfig.VariableName, action, evaluator);
            EvaluateConfig(vars, realConfig, parameters, setter);
        }

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            string evaluatorName = def.ToString("evaluator");
            ConditionEvaluator evaluator = EvaluatorSelector.Select(evaluatorName);

            byte[] data = Encoding.UTF8.GetBytes(def.ToString("action"));
            int len = data.Length;
            int pos = 0;
            IProcessorState state = new GlobalRunSpec.ProcessorState(vars, data, Encoding.UTF8);
            bool faulted;
            bool res = evaluator(state, ref len, ref pos, out faulted);

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            setter(p, res.ToString());
        }
    }
}