using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ConstantMacro : IMacro, IDeferredMacro
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

            setter(p, config.Value);
        }

        public void EvaluateDeferredConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            JToken valueToken;
            if (!deferredConfig.Parameters.TryGetValue("value", out valueToken))
            {
                throw new ArgumentNullException("value");
            }

            string value = valueToken.ToString();
            IMacroConfig realConfig = new ConstantMacroConfig(deferredConfig.VariableName, value);
            EvaluateConfig(vars, realConfig, parameters, setter);
        }
    }
}
