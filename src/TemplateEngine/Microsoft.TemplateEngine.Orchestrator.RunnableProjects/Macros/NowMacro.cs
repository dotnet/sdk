using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public class NowMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("F2B423D7-3C23-4489-816A-41D8D2A98596");

        public string Type => "now";
         
        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            NowMacroConfig config = rawConfig as NowMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as NowMacroConfig");
            }

            DateTime time = config.Utc ? DateTime.UtcNow : DateTime.Now;
            string value = time.ToString(config.Format);
            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };

            vars[config.VariableName] = value;
            setter(p, value);
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("format", out JToken formatToken))
            {
                throw new ArgumentNullException("format");
            }
            string format = formatToken.ToString();

            bool utc;
            if (deferredConfig.Parameters.TryGetValue("utc", out JToken utcToken))
            {
                utc = utcToken.ToBool();
            }
            else
            {
                utc = false;
            }

            IMacroConfig realConfig = new NowMacroConfig(deferredConfig.VariableName, format, utc);
            return realConfig;
        }
    }
}
