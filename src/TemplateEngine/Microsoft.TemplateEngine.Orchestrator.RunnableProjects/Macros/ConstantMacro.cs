using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ConstantMacro : IMacro
    {
        public Guid Id => new Guid("370996FE-2943-4AED-B2F6-EC03F0B75B4A");

        public string Type => "constant";

        public void EvaluateConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            ConstantMacroConfig config = rawConfig as ConstantMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as ConstantMacroConfig");
            }

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };

            setter(p, config.Action);
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

            IMacroConfig realConfig = new ConstantMacroConfig(deferredConfig.VariableName, action);
            EvaluateConfig(vars, realConfig, parameters, setter);
        }

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            string value = def.ToString("action");
            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            setter(p, value);
        }
    }
}
