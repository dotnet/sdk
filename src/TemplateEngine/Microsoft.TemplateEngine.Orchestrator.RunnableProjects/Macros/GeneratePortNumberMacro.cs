using System;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public class GeneratePortNumberMacro : IDeferredMacro
    {
        public Guid Id => new Guid("D49B3690-B1E5-410F-A260-E1D7E873D8B2");

        public string Type => "port";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GeneratePortNumberConfig config = rawConfig as GeneratePortNumberConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as EvaluateMacroConfig");
            }

            config.Socket?.Dispose();

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };

            vars[config.VariableName] = config.Port.ToString();
            setter(p, config.Port.ToString());
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            int fallback = 0;
            if(deferredConfig.Parameters.TryGetValue("fallback", out JToken fallbackToken) && fallbackToken.Type == JTokenType.Integer)
            {
                fallback = fallbackToken.ToInt32();
            }

            IMacroConfig realConfig = new GeneratePortNumberConfig(deferredConfig.VariableName, fallback);
            return realConfig;
        }
    }
}
